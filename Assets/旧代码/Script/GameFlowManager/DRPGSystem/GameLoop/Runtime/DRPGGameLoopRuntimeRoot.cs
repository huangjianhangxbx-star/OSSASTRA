using System;
using UnityEngine;

/// <summary>
/// Runtime composition root for title, exploration, battle and settlement.
/// It coordinates existing subsystems but does not own battle simulation.
/// </summary>
public sealed class DRPGGameLoopRuntimeRoot : MonoBehaviour
{
    [SerializeField] private MonoBehaviour viewComponent;

    private GameFlowRoot root;
    private DRPGBattleFlowRoot battleFlow;
    private IDRPGGameLoopView view;
    private AnkaluteGameLoopRuntimeCoordinator coordinator;
    private AnkaluteExplorationPhaseGate phaseGate;
    private DRPGVictorySettlement activeSettlement;
    private DRPGResonanceExpeditionState resonanceExpedition;
    private DRPGBattleResonanceAccess resonanceAccess;
    private bool isInitialized;

    public AnkaluteGameLoopPhase AnkalutePhase => coordinator != null
        ? coordinator.Current.Phase
        : AnkaluteGameLoopPhase.Title;
    public StrategicHubSnapshot HubSnapshot =>
        coordinator != null ? coordinator.HubCurrent : null;
    public DRPGGameLoopPhase Phase => ProjectLegacyPhase(AnkalutePhase);
    public bool CanBeginBattle =>
        isInitialized && CanUseExplorationUI && HasActiveExploration();
    public bool CanUseExplorationUI =>
        isInitialized &&
        AnkalutePhase == AnkaluteGameLoopPhase.ExpeditionExploration &&
        phaseGate != null && phaseGate.IsExplorationEnabled;
    public DRPGVictorySettlement ActiveSettlement => activeSettlement;
    public DRPGResonanceExpeditionState ResonanceExpedition =>
        resonanceExpedition;
    public IDRPGBattleResonanceAccess ResonanceAccess => resonanceAccess;

    public void Initialize(GameFlowRoot gameFlowRoot)
    {
        if (isInitialized)
            return;

        root = gameFlowRoot;
        battleFlow = root != null ? root.drpgBattleFlow : null;
        view = viewComponent as IDRPGGameLoopView;
        if (view == null)
        {
            MonoBehaviour[] children = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] is IDRPGGameLoopView candidate)
                {
                    viewComponent = children[i];
                    view = candidate;
                    break;
                }
            }
        }

        coordinator = new AnkaluteGameLoopRuntimeCoordinator();
        phaseGate = new AnkaluteExplorationPhaseGate(root);
        DRPGResonanceService resonanceService = root != null &&
            root.drpgProgression != null
                ? root.drpgProgression.ResonanceService
                : null;
        if (resonanceService != null)
        {
            resonanceExpedition = new DRPGResonanceExpeditionState();
            resonanceAccess = new DRPGBattleResonanceAccess(
                resonanceService,
                resonanceExpedition,
                root.drpgProgression.PartyComposition);
        }
        battleFlow?.ConfigureResonanceAccess(resonanceAccess);
        if (battleFlow != null)
        {
            battleFlow.BattleStarted += OnBattleStarted;
            battleFlow.BattleCompleted += OnBattleCompleted;
        }
        view?.Configure(this);
        phaseGate.Apply(false);
        view?.ShowTitle();
        isInitialized = true;
    }

    public bool StartGame()
    {
        if (coordinator == null)
            return false;
        AnkaluteGameLoopRequestResult result =
            coordinator.EnterStrategicHub(SessionEntryReason.NewGame);
        if (result.Status != AnkaluteGameLoopRequestStatus.Applied)
            return false;
        activeSettlement = null;
        phaseGate.Apply(false);
        view?.ShowStrategicHub(HubSnapshot);
        return true;
    }

    public bool OpenDungeonSelection()
    {
        if (coordinator == null ||
            AnkalutePhase != AnkaluteGameLoopPhase.StrategicHub)
        {
            return false;
        }

        StrategicHubRequestResult result = coordinator.OpenDestination(
            StrategicHubRoute.DungeonSelection);
        if (!IsAccepted(result.Status))
            return false;
        view?.ShowStrategicHub(HubSnapshot);
        return true;
    }

    public bool BackStrategicHub()
    {
        if (coordinator == null ||
            AnkalutePhase != AnkaluteGameLoopPhase.StrategicHub)
        {
            return false;
        }

        StrategicHubRequestResult result = coordinator.Back();
        if (!IsAccepted(result.Status))
            return false;
        view?.ShowStrategicHub(HubSnapshot);
        return true;
    }

    public bool LaunchExpedition(ExpeditionStartRequest request)
    {
        if (!isInitialized || coordinator == null ||
            root == null || root.gameFlowArbitrator == null)
        {
            return false;
        }

        bool launched = coordinator.TryLaunchExpeditionWithActivation(
            request,
            () => root.gameFlowArbitrator.TryActivateHunt(),
            out _,
            out AnkaluteGameLoopRequestResult transition);
        if (!launched)
        {
            if (transition.Status == AnkaluteGameLoopRequestStatus.Applied)
            {
                root.gameFlowArbitrator.DeactivateHunt();
                phaseGate.Apply(false);
                view?.ShowStrategicHub(HubSnapshot);
            }
            return false;
        }

        activeSettlement = null;
        resonanceExpedition?.ClearAll();
        phaseGate.Apply(true);
        view?.HideAll();
        return true;
    }

    public bool EndExpedition(ExpeditionExitReason reason)
    {
        if (coordinator == null)
            return false;
        AnkaluteGameLoopRequestResult result =
            coordinator.EndExpedition(reason);
        if (result.Status != AnkaluteGameLoopRequestStatus.Applied)
            return false;

        root?.gameFlowArbitrator?.DeactivateHunt();
        activeSettlement = null;
        resonanceExpedition?.ClearAll();
        phaseGate.Apply(false);
        view?.ShowStrategicHub(HubSnapshot);
        return true;
    }

    public DRPGLootTransferSummary CollectAllLoot()
    {
        DRPGVictorySettlement settlement = ActiveSettlement;
        DRPGPartyInventoryState inventory = ResolveInventory();
        DRPGLootTransferSummary result = DRPGLootTransferService.TransferAll(
            settlement?.Loot,
            inventory);
        RefreshSettlement(result.Complete
            ? "战利品已全部存入团队背包"
            : "背包槽位或负重不足，剩余战利品仍在结算区");
        return result;
    }

    public DRPGLootTransferResult TransferLoot(
        string itemId,
        int quantity)
    {
        DRPGVictorySettlement settlement = ActiveSettlement;
        DRPGLootTransferResult result = DRPGLootTransferService.Transfer(
            settlement?.Loot,
            ResolveInventory(),
            itemId,
            quantity);
        RefreshSettlement(result.TransferredQuantity > 0
            ? "已存入 " + result.TransferredQuantity + " 件物品"
            : "背包槽位或负重不足");
        return result;
    }

    public bool CompleteSettlement()
    {
        if (coordinator == null)
            return false;
        AnkaluteGameLoopRequestResult result =
            coordinator.CompleteBattleSettlement();
        if (result.Status != AnkaluteGameLoopRequestStatus.Applied)
            return false;
        battleFlow?.FinishVictorySettlement();
        activeSettlement = null;
        phaseGate.Apply(true);
        view?.HideAll();
        return true;
    }

    public void ReturnToTitle()
    {
        if (coordinator == null)
            return;
        AnkaluteGameLoopRequestResult result = coordinator.ReturnToTitle();
        if (result.Status != AnkaluteGameLoopRequestStatus.Applied &&
            result.Status != AnkaluteGameLoopRequestStatus.NoOp)
        {
            return;
        }

        root?.gameFlowArbitrator?.DeactivateHunt();
        activeSettlement = null;
        resonanceExpedition?.ClearAll();
        phaseGate.Apply(false);
        view?.ShowTitle();
    }

    private void OnBattleStarted(
        DRPGBattleSession session,
        DRPGBattleStartRequest request)
    {
        string encounterId = request != null && request.Encounter != null
            ? request.Encounter.EncounterId
            : string.Empty;
        AnkaluteGameLoopRequestResult result = coordinator != null
            ? coordinator.BeginBattle(
                new EncounterStartRequest(new EncounterId(encounterId)))
            : default;
        if (result.Status != AnkaluteGameLoopRequestStatus.Applied)
        {
            Debug.LogError(
                "[DRPGGameLoop] Battle began outside exploration phase.",
                this);
        }
        activeSettlement = null;
        phaseGate?.Apply(false);
        view?.HideAll();
    }

    private void OnBattleCompleted(DRPGBattleCompletionContext context)
    {
        if (context == null || coordinator == null)
            return;

        DRPGVictorySettlement settlement = null;
        if (context.Result.Outcome == DRPGBattleOutcome.Victory)
        {
            settlement = DRPGVictoryRewardResolver.ResolveAndCommit(
                context.Request != null ? context.Request.Encounter : null,
                context.Participants,
                new DRPGProgressionExperienceRewardSink(
                    root != null && root.drpgProgression != null
                        ? root.drpgProgression.Service
                        : null),
                new DRPGUnityRandomSource());
        }

        BattleResolution resolution = MapBattleResolution(
            context.Result.Outcome);
        AnkaluteGameLoopRequestResult transition =
            coordinator.ResolveBattle(resolution);
        if (transition.Status != AnkaluteGameLoopRequestStatus.Applied)
        {
            Debug.LogError(
                "[DRPGGameLoop] Rejected battle completion transition: " +
                context.Result.Outcome,
                this);
            battleFlow?.FinishVictorySettlement();
            return;
        }

        switch (context.Result.Outcome)
        {
            case DRPGBattleOutcome.Victory:
                activeSettlement = settlement;
                if (view != null)
                {
                    view.ShowSettlement(settlement, ResolveInventory());
                }
                else
                {
                    // A missing view must never deadlock the game loop.
                    CompleteSettlement();
                }
                break;

            case DRPGBattleOutcome.Defeat:
                activeSettlement = null;
                resonanceExpedition?.ClearAll();
                root?.gameFlowArbitrator?.DeactivateHunt();
                phaseGate?.Apply(false);
                view?.ShowStrategicHub(HubSnapshot);
                break;

            default:
                activeSettlement = null;
                phaseGate?.Apply(true);
                view?.HideAll();
                break;
        }
    }

    private DRPGPartyInventoryState ResolveInventory()
    {
        return root != null && root.drpgEquipment != null &&
            root.drpgEquipment.IsInitialized
            ? root.drpgEquipment.Equipment.PartyInventory
            : null;
    }

    private void RefreshSettlement(string feedback)
    {
        view?.RefreshSettlement(
            ActiveSettlement,
            ResolveInventory(),
            feedback ?? string.Empty);
    }

    private bool HasActiveExploration()
    {
        HuntFlowDataBus huntData = root != null && root.huntFlow != null
            ? root.huntFlow.huntFlowDataBus
            : null;
        bool hasParty = huntData != null && (huntData.player != null ||
            (root.huntFlow.huntPlayerSpawner != null &&
             root.huntFlow.huntPlayerSpawner.HasPreparedNodeParty));
        return hasParty &&
            root.gameFlowArbitrator != null &&
            root.gameFlowArbitrator.huntFlowSM != null &&
            root.gameFlowArbitrator.huntFlowSM.IsActive;
    }

    private static bool IsAccepted(StrategicHubRequestStatus status)
    {
        return status == StrategicHubRequestStatus.Applied ||
            status == StrategicHubRequestStatus.NoOp;
    }

    private static DRPGGameLoopPhase ProjectLegacyPhase(
        AnkaluteGameLoopPhase phase)
    {
        switch (phase)
        {
            case AnkaluteGameLoopPhase.StrategicHub:
                return DRPGGameLoopPhase.StrategicHub;
            case AnkaluteGameLoopPhase.ExpeditionExploration:
                return DRPGGameLoopPhase.Exploration;
            case AnkaluteGameLoopPhase.Battle:
                return DRPGGameLoopPhase.Battle;
            case AnkaluteGameLoopPhase.BattleSettlement:
                return DRPGGameLoopPhase.VictorySettlement;
            default:
                return DRPGGameLoopPhase.Title;
        }
    }

    private static BattleResolution MapBattleResolution(
        DRPGBattleOutcome outcome)
    {
        switch (outcome)
        {
            case DRPGBattleOutcome.Victory:
                return BattleResolution.Victory;
            case DRPGBattleOutcome.Defeat:
                return BattleResolution.PartyDefeated;
            case DRPGBattleOutcome.Escaped:
                return BattleResolution.Escaped;
            case DRPGBattleOutcome.Aborted:
                return BattleResolution.Aborted;
            default:
                return BattleResolution.Invalid;
        }
    }

    private void OnDestroy()
    {
        if (battleFlow != null)
        {
            battleFlow.BattleStarted -= OnBattleStarted;
            battleFlow.BattleCompleted -= OnBattleCompleted;
        }
        activeSettlement = null;
        resonanceExpedition?.ClearAll();
        phaseGate?.Release();
    }
}
