using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGBattleFlowRoot : MonoBehaviour, IDRPGBattleFlowController
{
    [SerializeField] private MonoBehaviour presentationComponent;
    [SerializeField] private List<DRPGBattleItemDefinition> battleItemDefinitions =
        new List<DRPGBattleItemDefinition>();

    private GameFlowRoot gameFlowRoot;
    private ExplorationBattleSuspension suspension;
    private IDisposable suspensionLease;
    private IDRPGBattlePresentation presentation;
    private DRPGProgressionService progressionService;
    private DRPGEquipmentRuntimeRoot equipmentRuntime;
    private DRPGBattleRecoveryBridge recoveryBridge;
    private IDRPGBattleResonanceAccess resonanceAccess;
    private DRPGBattleStartRequest activeRequest;
    private bool awaitingVictorySettlement;
    private bool isInitialized;
    private readonly DRPGBattleSafeEventPublisher eventPublisher = new DRPGBattleSafeEventPublisher();
    internal IReadOnlyList<string> ObserverDiagnostics => eventPublisher.Diagnostics;

    public DRPGBattleSession CurrentSession { get; private set; }
    public DRPGBattleStartResult LastStartResult { get; private set; } =
        DRPGBattleStartResult.FlowUnavailable;
    public string LastStartError { get; private set; } = string.Empty;
    public bool IsAwaitingVictorySettlement => awaitingVictorySettlement;

    public event Action<DRPGBattleSession, DRPGBattleStartRequest>
        BattleStarted;
    public event Action<DRPGBattleCompletionContext> BattleCompleted;

    public void ConfigureResonanceAccess(
        IDRPGBattleResonanceAccess access)
    {
        resonanceAccess = access;
    }

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized)
            return;

        gameFlowRoot = root;
        presentation = presentationComponent as IDRPGBattlePresentation;
        progressionService = root != null && root.drpgProgression != null
            ? root.drpgProgression.Service
            : null;
        equipmentRuntime = root != null ? root.drpgEquipment : null;
        suspension = new ExplorationBattleSuspension(root);
        isInitialized = true;
    }

    public DRPGBattleStartResult RequestBattle(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGBattleEnvironmentContext environmentContext = default,
        BasicAttackOnlyAcceptanceOptions acceptanceOptions = default)
    {
        if (CurrentSession != null || awaitingVictorySettlement)
            return DRPGBattleStartResult.Busy;

        if (gameFlowRoot != null && gameFlowRoot.drpgGameLoop != null &&
            !gameFlowRoot.drpgGameLoop.CanBeginBattle)
        {
            return DRPGBattleStartResult.FlowUnavailable;
        }

        if (gameFlowRoot == null || gameFlowRoot.gameFlowArbitrator == null)
            return DRPGBattleStartResult.FlowUnavailable;

        bool accepted = gameFlowRoot.gameFlowArbitrator.SubmitRequest(
            new DRPGBattleStartRequest(party, encounter, environmentContext,
                acceptanceOptions.Enabled ? acceptanceOptions.BattleSeed : Environment.TickCount, acceptanceOptions));
        return accepted ? DRPGBattleStartResult.Started : LastStartResult;
    }

    public DRPGBattleStartResult RequestBattle(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGNodeBattleContext nodeContext,
        BasicAttackOnlyAcceptanceOptions acceptanceOptions = default)
    {
        if (nodeContext == null)
        {
            LastStartResult = DRPGBattleStartResult.InvalidConfiguration;
            LastStartError = "In-place battle context is missing.";
            return LastStartResult;
        }
        if (!nodeContext.TryValidate(out string contextError))
        {
            LastStartResult = DRPGBattleStartResult.InvalidConfiguration;
            LastStartError = contextError;
            return LastStartResult;
        }
        DRPGPartyRosterSnapshot roster;
        try
        {
            roster = nodeContext.CaptureRoster(party);
        }
        catch (Exception exception)
        {
            LastStartResult = DRPGBattleStartResult.InvalidConfiguration;
            LastStartError = exception.Message;
            return LastStartResult;
        }
        if (CurrentSession != null || awaitingVictorySettlement)
            return DRPGBattleStartResult.Busy;
        if (gameFlowRoot == null || gameFlowRoot.gameFlowArbitrator == null)
            return DRPGBattleStartResult.FlowUnavailable;

        int seed = acceptanceOptions.Enabled
            ? acceptanceOptions.BattleSeed
            : Environment.TickCount;
        bool accepted = gameFlowRoot.gameFlowArbitrator.SubmitRequest(
            new DRPGBattleStartRequest(
                party,
                encounter,
                default,
                seed,
                acceptanceOptions,
                roster,
                nodeContext));
        return accepted ? DRPGBattleStartResult.Started : LastStartResult;
    }

    public bool TryBeginBattle(
        DRPGBattleStartRequest request,
        out DRPGBattleSession session,
        out string error)
    {
        session = null;
        error = string.Empty;

        if (CurrentSession != null || awaitingVictorySettlement)
        {
            LastStartResult = DRPGBattleStartResult.Busy;
            error = "A DRPG battle session is already active.";
            LastStartError = error;
            return false;
        }

        if (gameFlowRoot != null && gameFlowRoot.drpgGameLoop != null &&
            !gameFlowRoot.drpgGameLoop.CanBeginBattle)
        {
            LastStartResult = DRPGBattleStartResult.FlowUnavailable;
            error = "Battle can only begin during expedition exploration.";
            LastStartError = error;
            return false;
        }

        if (request == null)
        {
            LastStartResult = DRPGBattleStartResult.InvalidConfiguration;
            error = "Battle request is missing.";
            LastStartError = error;
            return false;
        }

        if (request.Encounter == null ||
            string.IsNullOrWhiteSpace(request.Encounter.EncounterId))
        {
            LastStartResult = DRPGBattleStartResult.InvalidConfiguration;
            error = "Battle encounter must define a non-empty EncounterId.";
            LastStartError = error;
            return false;
        }

        DRPGPartyRosterSnapshot rosterSnapshot = request.RosterSnapshot;
        var transactionPolicy = new DRPGBattleTransactionPolicy(request.Encounter.Presentation != null &&
            request.Encounter.Presentation.StandardBasicAttackTransactionEnabled);
        try { DRPGBattleSessionFactory.ValidateAcceptanceRequest(request, transactionPolicy); }
        catch (ArgumentException exception)
        {
            LastStartResult = DRPGBattleStartResult.InvalidConfiguration;
            LastStartError = error = exception.Message;
            return false;
        }
        DRPGProgressionRoot progressionRoot = gameFlowRoot != null
            ? gameFlowRoot.drpgProgression
            : null;
        if (!request.HasExplicitRosterSnapshot &&
            progressionRoot != null &&
            progressionRoot.PartyComposition != null &&
            ReferenceEquals(request.Party, progressionRoot.PartySetup))
        {
            rosterSnapshot = progressionRoot.PartyComposition.CreateSnapshot();
        }

        DRPGBattleSessionCreationResult roster =
            DRPGBattleSessionFactory.CreateRoster(
                rosterSnapshot,
                request.Encounter,
                progressionService,
                request.NodeContext != null
                    ? request.NodeContext.PartyRoot.BattleState
                    : null);
        if (!roster.IsSuccess)
        {
            LastStartResult = DRPGBattleStartResult.InvalidConfiguration;
            error = !string.IsNullOrWhiteSpace(request.RosterSnapshotError)
                ? request.RosterSnapshotError
                : roster.ErrorMessage;
            LastStartError = error;
            return false;
        }

        if (suspension == null || !suspension.TryAcquire(out suspensionLease))
        {
            LastStartResult = DRPGBattleStartResult.SuspensionUnavailable;
            error = "Exploration control could not be suspended.";
            LastStartError = error;
            return false;
        }

        try
        {
            IDRPGBattleItemSource battleItemSource = CreateBattleItemSource();
            equipmentRuntime?.ApplyEquipmentBuilds(roster.Participants);
            IDRPGBattleEquipmentService battleEquipment =
                equipmentRuntime?.CreateBattleEquipmentService(
                    roster.Participants);
            IDRPGBattleWeaponDurabilityService durability =
                equipmentRuntime?.CreateDurabilityService();
            CurrentSession = DRPGBattleSessionFactory.CreateConfiguredSession(
                roster, request, transactionPolicy,
                battleItemSource,
                battleEquipment,
                durability,
                resonanceAccess: resonanceAccess);
            recoveryBridge?.Dispose();
            recoveryBridge = null;
            if (equipmentRuntime != null && equipmentRuntime.IsInitialized)
            {
                recoveryBridge = new DRPGBattleRecoveryBridge(
                    CurrentSession,
                    equipmentRuntime.Equipment.FieldRecovery,
                    Guid.NewGuid().ToString("N"));
                if (presentation is DRPGBattlePresentationRoot presentationRoot)
                {
                    presentationRoot.ConfigureEquipment(
                        equipmentRuntime.Equipment);
                }
            }
            if (transactionPolicy.StandardBasicAttackEnabled)
            {
                if (!(presentation is DRPGBattlePresentationRoot boundPresentation))
                    throw new InvalidOperationException("Standard battle requires its configured transaction presentation root.");
                boundPresentation.ShowPrepared(CurrentSession, request);
                if (CurrentSession.PrepareStart(boundPresentation.PreparedClock) != BattleStartupPrepareStatus.Prepared)
                    throw new InvalidOperationException("Standard battle startup presentation is unavailable.");
            }
            else
            {
                presentation?.Show(CurrentSession, request);
                CurrentSession.Start();
            }
            activeRequest = request;
            session = CurrentSession;
            LastStartResult = DRPGBattleStartResult.Started;
            LastStartError = string.Empty;
            eventPublisher.Publish(BattleStarted, CurrentSession, request, nameof(BattleStarted));
            if (transactionPolicy.StandardBasicAttackEnabled &&
                !CurrentSession.EnqueuePreparedStartup(CurrentSession.TransactionArbiter.CurrentOwner.BattleStart))
                throw new InvalidOperationException("Prepared standard battle could not enqueue startup after BattleStarted.");
            return true;
        }
        catch (Exception exception)
        {
            recoveryBridge?.Dispose();
            recoveryBridge = null;
            presentation?.Hide();
            suspensionLease?.Dispose();
            suspensionLease = null;
            CurrentSession = null;
            activeRequest = null;
            LastStartResult = DRPGBattleStartResult.PresentationFailed;
            error = exception.Message;
            LastStartError = error;
            Debug.LogException(exception, this);
            return false;
        }
    }

    public void CompleteBattle(
        DRPGBattleSession session,
        DRPGBattleResult result)
    {
        if (session == null || !ReferenceEquals(session, CurrentSession))
            return;

        DRPGBattleStartRequest request = activeRequest;
        presentation?.Hide();
        recoveryBridge?.Dispose();
        recoveryBridge = null;
        CurrentSession = null;
        activeRequest = null;

        var context = new DRPGBattleCompletionContext(
            session,
            request,
            result);
        awaitingVictorySettlement =
            result.Outcome == DRPGBattleOutcome.Victory;

        if (!awaitingVictorySettlement)
            ReleaseSuspension();

        Action<DRPGBattleCompletionContext> completed = BattleCompleted;
        eventPublisher.Publish(completed, context, nameof(BattleCompleted));

        // Keep production fail-open if the game-loop composition is absent.
        if (awaitingVictorySettlement && completed == null)
            FinishVictorySettlement();
    }

    public bool FinishVictorySettlement()
    {
        if (!awaitingVictorySettlement)
            return false;
        awaitingVictorySettlement = false;
        ReleaseSuspension();
        return true;
    }

    private void OnDestroy()
    {
        if (CurrentSession != null && CurrentSession.Outcome == DRPGBattleOutcome.None)
            CurrentSession.Abort();

        recoveryBridge?.Dispose();
        recoveryBridge = null;
        suspensionLease?.Dispose();
        suspensionLease = null;
        CurrentSession = null;
        activeRequest = null;
        awaitingVictorySettlement = false;
    }

    private IDRPGBattleItemSource CreateBattleItemSource()
    {
        CharacterRoot characterRoot = FindObjectOfType<CharacterRoot>(true);
        CharacterPackageSystem package = characterRoot != null
            ? characterRoot.characterPackageSystem
            : null;
        return package != null
            ? new PackageBattleItemSource(package, battleItemDefinitions)
            : null;
    }

    private void ReleaseSuspension()
    {
        suspensionLease?.Dispose();
        suspensionLease = null;
    }
}
