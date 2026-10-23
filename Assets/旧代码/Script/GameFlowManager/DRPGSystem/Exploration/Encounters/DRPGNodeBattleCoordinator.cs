using UnityEngine;

[DisallowMultipleComponent]
public sealed class DRPGNodeBattleCoordinator : MonoBehaviour
{
    [SerializeField] private DRPGBattleFlowRoot battleFlow;
    [SerializeField] private DRPGPartySetupSO party;
    [SerializeField] private DRPGNodeBattleContext context;

    public string LastStartError { get; private set; } = string.Empty;
    public DRPGNodeBattleContext Context => context;
    public event System.Action<DRPGBattleCompletionContext> BattleFinished;
    public event System.Action BattleBegan;
    private DRPGNodePartyBattleBridge partyBridge;

    private void OnEnable()
    {
        Resolve();
        if (battleFlow != null)
        {
            battleFlow.BattleStarted += HandleBattleStarted;
            battleFlow.BattleCompleted += HandleBattleCompleted;
        }
    }

    private void OnDisable()
    {
        if (battleFlow != null)
        {
            battleFlow.BattleStarted -= HandleBattleStarted;
            battleFlow.BattleCompleted -= HandleBattleCompleted;
        }
        partyBridge?.Dispose();
        partyBridge = null;
    }

    public DRPGBattleStartResult TryStart(
        DRPGEncounterDefinitionSO encounter,
        BasicAttackOnlyAcceptanceOptions acceptanceOptions = default)
    {
        Resolve();
        if (battleFlow == null || party == null || context == null)
        {
            LastStartError = "In-place battle coordinator is not configured.";
            return DRPGBattleStartResult.InvalidConfiguration;
        }
        context.CaptureCurrentSceneComposition();
        if (!context.TryValidate(out string error))
        {
            LastStartError = error;
            return DRPGBattleStartResult.InvalidConfiguration;
        }

        DRPGBattleStartResult result = battleFlow.RequestBattle(
            party,
            encounter,
            context,
            acceptanceOptions);
        LastStartError = result == DRPGBattleStartResult.Started
            ? string.Empty
            : battleFlow.LastStartError;
        return result;
    }

    public void ConfigureForRuntime(
        DRPGBattleFlowRoot flow,
        DRPGPartySetupSO partySetup,
        DRPGNodeBattleContext battleContext)
    {
        battleFlow = flow;
        party = partySetup;
        context = battleContext;
    }

    private void Resolve()
    {
        if (battleFlow == null && GameFlowRoot.Instance != null)
            battleFlow = GameFlowRoot.Instance.drpgBattleFlow;
        if (context == null)
            context = FindFirstObjectByType<DRPGNodeBattleContext>();
    }

    private void HandleBattleCompleted(DRPGBattleCompletionContext completion)
    {
        if (completion.Request == null ||
            !ReferenceEquals(completion.Request.NodeContext, context))
        {
            return;
        }
        partyBridge?.CaptureCommittedState();
        partyBridge?.Dispose();
        partyBridge = null;
        if (context.Mode == DRPGNodeBattleMode.ExplorationEncounter)
            context.PartyRoot.SetExplorationEnabled(true);
        BattleFinished?.Invoke(completion);
    }

    private void HandleBattleStarted(
        DRPGBattleSession session,
        DRPGBattleStartRequest request)
    {
        if (request == null || !ReferenceEquals(request.NodeContext, context))
            return;
        partyBridge?.Dispose();
        partyBridge = new DRPGNodePartyBattleBridge(context.PartyRoot);
        partyBridge.Begin(session);
        context.PartyRoot.SetExplorationEnabled(false);
        BattleBegan?.Invoke();
    }
}
