using System;
using System.Collections.Generic;

public sealed class DRPGBattleSession :
    IDRPGBattleEffectEnvironment
{
    private readonly List<BattleParticipantRuntime> participants;
    private readonly List<BattleParticipantRuntime> reserveEnemies;
    private readonly DRPGTurnScheduler scheduler;
    private readonly DRPGActionResolver resolver;
    private readonly IDRPGBattleRandom randomSource;
    private readonly IDRPGBattleItemSource itemSource;
    private readonly DRPGBattleEscapeRules escapeRules;
    private readonly DRPGFormationRules formationRules;
    private readonly IDRPGBattleEquipmentService equipmentService;
    private readonly IDRPGBattleWeaponDurabilityService weaponDurabilityService;
    private readonly DRPGBattleReactionPipeline reactionPipeline;
    private readonly DRPGBattleEventFactory eventFactory;
    private readonly DRPGBattleProgressPipeline progressPipeline;
    private readonly IDRPGBattleResonanceAccess resonanceAccess;
    private readonly DRPGBattleIdentityAuthority transactionIdentityAuthority;
    private readonly DRPGBattleTurnEntryRunner turnEntryRunner;
    private readonly DRPGStandardBasicAttackTransactionModule standardRoot;
    private readonly DRPGBattleContinuationInbox continuationInbox;
    private readonly bool standardEnabled;
    private readonly bool wholeWaveEnabled;
    private BattleRuleSnapshot legacyInitialSnapshot;
    private RootActionReceipt standardReceipt;
    private DRPGBattlePreparedAction standardPreparedAction;
    private bool standardDelivered, standardAbortRequested, publishingStandard;
    private string standardFault = string.Empty;
    internal DRPGBattleFaultTerminal? LastBattleFault { get; private set; }
    internal event Action<DRPGBattleFaultTerminal> BattleFaulted;
    private readonly DRPGBattleActionTraceWriter transactionTrace;
    private readonly DRPGBattleTransactionArbiter transactionArbiter;
    private readonly DRPGBattleSessionState transactionState;
    private readonly DRPGBattleActionResolutionLedgerStore
        actionResolutionLedgerStore;
    internal DRPGStandardBasicAttackRuleKernel StandardBasicAttackRules { get; private set; }
    internal event Action<DRPGCommittedActionFacts> ActionLedgerCommitted;
    private readonly DRPGBattleSafeEventPublisher eventPublisher = new DRPGBattleSafeEventPublisher();
    internal IReadOnlyList<string> ObserverDiagnostics => eventPublisher.Diagnostics;
    private readonly DRPGBasicAttackSelectionRuntimeState
        basicAttackSelectionState;
    private readonly DRPGBattleTransactionPresentationPortSlot
        transactionPresentationPortSlot;
    private readonly DRPGBattleActionExecutionRouter actionExecutionRouter;
    private readonly DRPGLegacyTurnEntryAdapter legacyTurnEntryAdapter;
    private readonly DRPGBattleFlowOrchestrator flow =
        new DRPGBattleFlowOrchestrator();
    private readonly DRPGEnemyActionPlanner enemyActionPlanner =
        new DRPGEnemyActionPlanner();
    private readonly BasicAttackOnlyAcceptanceOptions acceptanceOptions;
    private readonly DRPGBasicAttackOnlyIntentSource acceptanceIntentSource = new DRPGBasicAttackOnlyIntentSource();
    private readonly List<BattleParticipantRuntime> enemyTargetCandidates =
        new List<BattleParticipantRuntime>();
    private readonly List<DRPGTelegraphedAction> pendingTelegraphs =
        new List<DRPGTelegraphedAction>();
    private readonly List<DRPGBattleHitResult> activeHits =
        new List<DRPGBattleHitResult>();
    private readonly List<DRPGActionEffectResult> activeEffects =
        new List<DRPGActionEffectResult>();
    private readonly Dictionary<int, float> pendingTimelineDeltas;
    private readonly Dictionary<int, int> pendingTimelinePriorities;
    private readonly Dictionary<int, DRPGEnemyActionPlan> enemyPlans;
    private readonly List<DRPGEnemyActionPlan> enemyPlanBuffer =
        new List<DRPGEnemyActionPlan>();
    private long nextEnemyPlanId = 1;
    private long completedPlayerActionSerial;
    private DRPGBattlePreparedAction activeAction;
    private DRPGBattleActionResolution activeResolution;
    private DRPGBattlePreparedAction pendingFinalizationAction;
    private DRPGBattleOutcome pendingFinalizationOutcome;
    private bool deliveringLegacyContinuation;
    private DRPGFreeStrikeWindowContext activeFreeStrikeWindow;
    private object stagedActionConsumer;
    private Action<DRPGBattlePreparedAction> stagedActionConsumerCallback;
    private bool activeEscapeSucceeded;
    private DRPGEnemyActionPlan currentEnemyPlan;
    private bool currentEnemyPlanFizzled;
    private int reinforcementDelayRemaining = -1;
    private ActorTurnToken currentActorTurnToken;
    private TargetSelectionToken activeLegacyBasicAttackSelection;
    private ActionIdentity currentActionIdentity;

    public DRPGBattleSession(
        IEnumerable<BattleParticipantRuntime> participants,
        DRPGTurnScheduler scheduler = null,
        DRPGActionResolver resolver = null,
        IDRPGBattleRandom randomSource = null,
        IDRPGBattleItemSource itemSource = null,
        DRPGBattleEscapeRules? escapeRules = null,
        IEnumerable<BattleParticipantRuntime> reserveEnemies = null,
        DRPGFormationRules formationRules = null,
        IDRPGBattleEquipmentService equipmentService = null,
        IDRPGBattleWeaponDurabilityService weaponDurabilityService = null,
        DRPGBattleReactionPipeline reactionPipeline = null,
        DRPGBattleEventFactory eventFactory = null,
        DRPGBattleProgressPipeline progressPipeline = null,
        IDRPGBattleResonanceAccess resonanceAccess = null)
        : this(
            DRPGBattleSessionComposition.Build(
                participants,
                reserveEnemies,
                scheduler,
                DRPGProcessBattleGenerationSource.Instance,
                DRPGBattleTransactionPolicy.ProductionLegacy,
                progressPipeline, weaponDurabilityService, resonanceAccess),
            resolver,
            randomSource,
            itemSource,
            escapeRules,
            formationRules,
            equipmentService,
            weaponDurabilityService,
            reactionPipeline,
            eventFactory,
            progressPipeline,
            resonanceAccess)
    {
    }

    internal DRPGBattleSession(
        IEnumerable<BattleParticipantRuntime> participants,
        DRPGTurnScheduler scheduler,
        DRPGActionResolver resolver,
        IDRPGBattleRandom randomSource,
        IDRPGBattleItemSource itemSource,
        DRPGBattleEscapeRules? escapeRules,
        IEnumerable<BattleParticipantRuntime> reserveEnemies,
        DRPGFormationRules formationRules,
        IDRPGBattleEquipmentService equipmentService,
        IDRPGBattleWeaponDurabilityService weaponDurabilityService,
        DRPGBattleReactionPipeline reactionPipeline,
        DRPGBattleEventFactory eventFactory,
        DRPGBattleProgressPipeline progressPipeline,
        IDRPGBattleResonanceAccess resonanceAccess,
        IDRPGBattleGenerationSource generationSource)
        : this(
            DRPGBattleSessionComposition.Build(
                participants,
                reserveEnemies,
                scheduler,
                generationSource,
                DRPGBattleTransactionPolicy.ProductionLegacy,
                progressPipeline, weaponDurabilityService, resonanceAccess),
            resolver,
            randomSource,
            itemSource,
            escapeRules,
            formationRules,
            equipmentService,
            weaponDurabilityService,
            reactionPipeline,
            eventFactory,
            progressPipeline,
            resonanceAccess)
    {
    }

    internal DRPGBattleSession(
        DRPGBattleSessionComposition composition,
        DRPGActionResolver resolver,
        IDRPGBattleRandom randomSource,
        IDRPGBattleItemSource itemSource,
        DRPGBattleEscapeRules? escapeRules,
        DRPGFormationRules formationRules,
        IDRPGBattleEquipmentService equipmentService,
        IDRPGBattleWeaponDurabilityService weaponDurabilityService,
        DRPGBattleReactionPipeline reactionPipeline,
        DRPGBattleEventFactory eventFactory,
        DRPGBattleProgressPipeline progressPipeline,
        IDRPGBattleResonanceAccess resonanceAccess,
        BasicAttackOnlyAcceptanceOptions acceptanceOptions = default)
    {
        if (composition == null)
            throw new ArgumentNullException(nameof(composition));
        transactionIdentityAuthority = composition.IdentityAuthority;
        transactionTrace = composition.Trace;
        transactionArbiter = composition.Arbiter;
        transactionPresentationPortSlot = composition.PresentationPortSlot;
        legacyTurnEntryAdapter = new DRPGLegacyTurnEntryAdapter(
            transactionIdentityAuthority);

        this.participants = new List<BattleParticipantRuntime>(
            composition.Participants);
        this.reserveEnemies = new List<BattleParticipantRuntime>(
            composition.ReserveEnemies);

        this.scheduler = composition.Scheduler;
        this.resolver = resolver ?? new DRPGActionResolver();
        this.randomSource = randomSource ?? new SystemDRPGBattleRandom();
        this.itemSource = itemSource;
        this.escapeRules = escapeRules ?? DRPGBattleEscapeRules.Disabled;
        this.formationRules = formationRules ?? DRPGFormationRules.Default;
        this.equipmentService = equipmentService;
        this.weaponDurabilityService = weaponDurabilityService;
        this.reactionPipeline = reactionPipeline ??
            new DRPGBattleReactionPipeline();
        this.eventFactory = eventFactory ?? new DRPGBattleEventFactory();
        this.progressPipeline = composition.ProgressPipeline;
        this.resonanceAccess = resonanceAccess;
        this.acceptanceOptions = acceptanceOptions;

        transactionState = composition.State;
        enemyPlans = transactionState.TurnCandidates.Plans;
        pendingTimelineDeltas = transactionState.TurnCandidates.PendingTimelineDeltas;
        pendingTimelinePriorities = transactionState.TurnCandidates.PendingTimelinePriorities;
        transactionState.TurnCandidates.BindPlanningSession(this);
        actionResolutionLedgerStore =
            new DRPGBattleActionResolutionLedgerStore(
                transactionState,
                transactionTrace);
        basicAttackSelectionState = composition.SelectionState;
        actionExecutionRouter = composition.ActionExecutionRouter;
        var ruleParticipants = new List<BattleParticipantRuntime>(this.participants); ruleParticipants.AddRange(this.reserveEnemies);
        StandardBasicAttackRules = new DRPGStandardBasicAttackRuleKernel(transactionState, actionResolutionLedgerStore,
            this.resolver, ruleParticipants.AsReadOnly(), this.progressPipeline, this.weaponDurabilityService,
            this.resonanceAccess, this.reactionPipeline, PublishCommittedActionLedger);
        composition.StandardBasicAttackRunner.BindRules(StandardBasicAttackRules);
        standardRoot = composition.StandardBasicAttackRunner;
        continuationInbox = composition.ContinuationInbox;
        standardEnabled = composition.Policy.StandardBasicAttackEnabled;
        wholeWaveEnabled = composition.WaveLifecycle != null;
        turnEntryRunner = new DRPGBattleTurnEntryRunner(this, transactionIdentityAuthority, composition.ContinuationInbox,
            composition.WaveLifecycle, composition.WavePolicy);
    }

    private void PublishCommittedActionLedger(DRPGCommittedActionFacts facts)
    {
        // Only immutable committed facts leave the new rule kernel. Legacy event
        // consumers can resolve reactions and resources, so must not receive these.
        PublishNotification(ActionLedgerCommitted, facts, nameof(ActionLedgerCommitted));
    }

    public event Action<BattleParticipantRuntime> CurrentActorChanged;
    public event Action TimelineChanged;
    public event Action<BattleParticipantRuntime> ParticipantChanged;
    public event Action<BattleParticipantRuntime> ParticipantBuildChanged;
    public event Action<DRPGBattleCommand> CommandPreviewed;
    public event Action<DRPGBattleCommand> ActionStarted;
    public event Action<DRPGBattlePreparedAction> ActionPrepared;
    public event Action<DRPGBattleActionResolution> ActionResolved;
    public event Action<DRPGBattleEventBatch> BattleEventBatchPublished;
    public event Action<DRPGBattleEvent> BattleEventPublished;
    public event Action<DRPGBattleReactionResolution> ReactionResolved;
    public event Action<DRPGBattleReactionRun> ReactionRunCompleted;
    public event Action<DRPGProgressMutationResult> ProgressChanged;
    public event Action<DRPGBattleProgressRun> ProgressRunCompleted;
    public event Action<DRPGBattlePreparedAction> ActionCompleted;
    public event Action<DRPGBattleWeaponBreakEvent> WeaponBroken;
    public event Action<DRPGBattleResult> BattleEnded;
    public event Action TelegraphsChanged;
    public event Action EnemyPlansChanged;
    public event Action<DRPGEnemyActionPlanSnapshot>
        EnemyTargetDisclosureChanged;
    public event Action<DRPGEnemyActionPlanSnapshot> EnemyPlanFizzled;
    public event Action FormationChanged;
    public event Action ReinforcementsChanged;

    public IReadOnlyList<BattleParticipantRuntime> Participants => participants;
    public IReadOnlyList<BattleParticipantRuntime> ReserveEnemies => reserveEnemies;
    public IReadOnlyList<DRPGTurnScheduleEntry> Timeline =>
        scheduler.GetTimelineSnapshot();
    internal long BattleGeneration => transactionIdentityAuthority.BattleGeneration;
    internal bool TryEnqueueTurnEntry(DRPGBattleTurnEntryModule module, BattleRuleMutationLease lease,
        IDisposable presentationScope, out string error) => turnEntryRunner.TryEnqueue(module, lease, presentationScope, out error);
    internal bool TickTurnEntry()
    {
        if (LastBattleFault.HasValue) return true;
        if (turnEntryRunner.Fault.Length != 0) return turnEntryRunner.Tick();
        if (standardReceipt != null && standardReceipt.Completion.IsCompleted &&
            standardReceipt.Completion.Result.Kind == RootActionTerminalKind.Faulted)
        {
            standardFault = standardReceipt.Completion.Result.Detail;
            standardRoot.Tick();
            if (standardRoot.FaultCleanupComplete) CompleteStandardFault();
            return true;
        }
        if (standardAbortRequested)
        {
            if (turnEntryRunner.RequestAbort()) { standardAbortRequested = false; return turnEntryRunner.Tick(); }
            standardRoot.Dispose(); standardReceipt = null; standardDelivered = true; standardAbortRequested = false;
            CompleteBattle(DRPGBattleOutcome.Aborted); return true;
        }
        if (standardReceipt == null || standardDelivered) return turnEntryRunner.Tick();
        if (standardFault.Length != 0) return true;
        if (standardRoot.CompletionSubmitted) { turnEntryRunner.PumpRoot(); return true; }
        standardRoot.Tick();
        if (standardReceipt.Completion.IsCompleted && standardReceipt.Completion.Result.Kind == RootActionTerminalKind.Faulted)
        {
            standardFault = standardReceipt.Completion.Result.Detail;
            if (standardRoot.FaultCleanupComplete) CompleteStandardFault();
            return true;
        }
        if (standardRoot.ResolutionLedger != null && flow.ActionStage == DRPGBattleActionFlowStage.Locked)
        {
            RequireFlowTransition(flow.TryBeginImpact(standardReceipt.Identity.ActionId, out var error), error);
            RequireFlowTransition(flow.TryCompleteImpact(standardReceipt.Identity.ActionId, out error), error);
        }
        if (standardRoot.SettlementLedger != null && flow.ActionStage == DRPGBattleActionFlowStage.ImpactResolved)
        {
            RequireFlowTransition(flow.TryBeginCommit(standardReceipt.Identity.ActionId, out var error), error);
            RequireFlowTransition(flow.TryCompleteCommit(standardReceipt.Identity.ActionId, out error), error);
        }
        if (standardRoot.ReadyForFinalization && !standardRoot.TryYieldCompletion(continuationInbox, out var failure)) standardFault = failure;
        return true;
    }

    private void CompleteStandardFault()
    {
        if (LastBattleFault.HasValue || !standardRoot.FaultCleanupComplete || transactionArbiter.HasOwner) return;
        var terminal = new DRPGBattleFaultTerminal(BattleTransactionIdentity.ForRootAction(standardReceipt.Identity),
            transactionState.BattleRevision, "StandardRootFault", standardFault, transactionTrace.Snapshot());
        SealBattleFault(terminal);
        PublishNotification(BattleFaulted, terminal, nameof(BattleFaulted));
    }

    internal bool TryCleanRootContinuationFault(BattleRuleMutationLease lease, string detail, out string error)
    {
        error = string.Empty;
        if (!standardRoot.CompletionSubmitted || !ReferenceEquals(standardRoot.SealedCompletion.Lease, lease) ||
            !transactionState.OwnsRuleMutationLease(lease, lease.Owner))
        { error = "Continuation cleanup does not belong to the sealed Root's physical lease."; return false; }
        standardRoot.CloseForContinuationFault(lease, detail);
        standardRoot.Tick();
        error = standardRoot.FaultCleanupError;
        return standardRoot.FaultCleanupComplete;
    }

    internal void CompleteTurnFlowFault(BattleRuleMutationLease lease, string detail)
    {
        if (LastBattleFault.HasValue) return;
        if (lease == null || !transactionState.OwnsRuleMutationLease(lease, lease.Owner))
            throw new InvalidOperationException("Technical entry terminal requires its exact cleaned transaction owner.");
        var terminal = new DRPGBattleFaultTerminal(lease.Owner, transactionState.BattleRevision,
            lease.Owner.Kind == BattleTransactionKind.BattleStart ? "BattleStartupFault" :
                lease.Owner.Kind == BattleTransactionKind.WaveTransition ? "WaveTransitionFault" :
                lease.Owner.Kind == BattleTransactionKind.RootAction ? "RootContinuationFault" : "TurnEntryFault",
            detail, transactionTrace.Snapshot());
        SealBattleFault(terminal);
        lease.Dispose();
        PublishNotification(BattleFaulted, terminal, nameof(BattleFaulted));
    }

    private void SealBattleFault(in DRPGBattleFaultTerminal terminal)
    {
        // Stop command/AI eligibility before any synchronous observer can call
        // Abort, refresh, Tick or try to start another action.
        LastBattleFault = terminal;
        standardAbortRequested = false;
        basicAttackSelectionState.SetBattleAvailable(false);
        basicAttackSelectionState.ClearTurn();
        currentActorTurnToken = default; currentActionIdentity = default;
        activeLegacyBasicAttackSelection = default; CurrentActor = null;
        standardPreparedAction = null;
        flow.Reset();
    }

    internal bool StandardBasicAttackEnabled => standardEnabled;
    internal RootActionStartResult TryStartConfiguredSkill(
        in DRPGSkillSelection selection)
    {
        if (!standardEnabled || !transactionArbiter.IsCommandInputOpen ||
            CurrentActor == null || !currentActorTurnToken.IsValid ||
            selection.Turn != currentActorTurnToken ||
            standardFault.Length != 0 ||
            (Phase != DRPGBattlePhase.AwaitingCommand &&
             Phase != DRPGBattlePhase.TargetSelection))
        {
            return RootActionStartResult.Rejected(
                BasicAttackRejection.BasicAttackUnavailable);
        }
        RootActionStartResult result = actionExecutionRouter.TryStart(selection);
        if (!result.IsAccepted)
            return result;
        standardReceipt = result.Receipt;
        standardDelivered = false;
        DRPGBattleCommand command = DRPGBattleCommand.UseSkill(
            CurrentActor.RuntimeId,
            selection.PrimaryTargetRuntimeId,
            selection.Skill);
        standardPreparedAction = standardRoot.CreateCompletionAction(command);
        currentActionIdentity = result.Receipt.Identity;
        activeLegacyBasicAttackSelection = default;
        RequireFlowTransition(
            flow.TryLockAction(result.Receipt.Identity.ActionId, out string error),
            error);
        return result;
    }

    internal RootActionStartResult TryStartCurrentConfiguredSkill(
        DRPGBattleCommand command)
    {
        if (command == null || command.Kind != DRPGBattleCommandKind.Skill ||
            command.Stage != DRPGBattleCommandStage.Immediate ||
            CurrentActor == null || command.ActorRuntimeId != CurrentActor.RuntimeId ||
            command.Skill == null || !DRPGBattleSkillModePolicy.IsAllowed(command.Skill))
        {
            return RootActionStartResult.Rejected(
                BasicAttackRejection.BasicAttackUnavailable);
        }

        IReadOnlyList<DRPGSkillSO> configured = CurrentActor.Build != null
            ? CurrentActor.Build.ConfiguredSkills
            : null;
        if (configured == null)
            return RootActionStartResult.Rejected(
                BasicAttackRejection.BasicAttackUnavailable);
        int slotCount = Math.Min(configured.Count, DRPGSkillLoadout.SlotCount);
        for (int slot = 0; slot < slotCount; slot++)
        {
            if (!ReferenceEquals(configured[slot], command.Skill))
                continue;
            return TryStartConfiguredSkill(new DRPGSkillSelection(
                currentActorTurnToken,
                slot,
                command.Skill,
                command.TargetRuntimeId));
        }
        return RootActionStartResult.Rejected(
            BasicAttackRejection.BasicAttackUnavailable);
    }

    internal RootActionStartResult TryStartStandardBasicAttack(ParticipantId target)
    {
        if (!standardEnabled || !transactionArbiter.IsCommandInputOpen || CurrentActor == null || !currentActorTurnToken.IsValid ||
            standardFault.Length != 0 || (Phase != DRPGBattlePhase.AwaitingCommand && Phase != DRPGBattlePhase.TargetSelection))
            return RootActionStartResult.Rejected(BasicAttackRejection.BasicAttackUnavailable);
        var opened = activeLegacyBasicAttackSelection;
        if (!opened.IsValid)
        {
            var selection = actionExecutionRouter.TryOpenBasicAttackSelection(currentActorTurnToken);
            if (!selection.IsAccepted) return RootActionStartResult.Rejected(selection.Rejection);
            opened = selection.Selection;
            activeLegacyBasicAttackSelection = opened;
        }
        var result = actionExecutionRouter.TryStart(new BasicAttackSelection(opened, target));
        if (!result.IsAccepted) return result;
        standardReceipt = result.Receipt; standardDelivered = false;
        standardPreparedAction = standardRoot.CreateCompletionAction(DRPGBattleCommand.BasicAttack(CurrentActor.RuntimeId, target.Value, CurrentActor.Build.BasicAttack));
        currentActionIdentity = result.Receipt.Identity; activeLegacyBasicAttackSelection = default;
        RequireFlowTransition(flow.TryLockAction(result.Receipt.Identity.ActionId, out var error), error);
        return result;
    }

    internal bool TryPrepareStandardContinuation(in RootActionCompletion value, out string error)
    {
        error = string.Empty;
        if (standardDelivered || standardReceipt == null || !standardRoot.CompletionSubmitted ||
            !standardRoot.SealedCompletion.HasSameValues(value) || standardPreparedAction == null ||
            transactionState.BattleRevision != value.FinalBattleRevision ||
            !transactionState.OwnsRuleMutationLease(value.Lease, value.Owner))
        { error = "Root continuation does not match this Session's sealed action/revision."; return false; }
        if (!flow.TryBeginFinalization(value.Identity.ActionId, out error)) return false;
        standardDelivered = true; // seal before any synchronous observer
        var action = standardPreparedAction;
        publishingStandard = true;
        try { PublishNotification(ActionCompleted, action, nameof(ActionCompleted)); }
        finally { publishingStandard = false; }
        if (value.Outcome != DRPGBattleOutcomeCandidate.Continue && value.Outcome != DRPGBattleOutcomeCandidate.WavePending) return true;
        AccrueStaminaAfterCompletedRoot(value.Lease);
        using (transactionState.OpenCompatibilityMutationScope(value.Lease))
        {
            var actor = CurrentActor;
            if (currentEnemyPlan != null && currentEnemyPlan.ActorRuntimeId == actor.RuntimeId)
            { RemoveEnemyPlan(actor.RuntimeId, true); currentEnemyPlan = null; currentEnemyPlanFizzled = false; }
            if (actor.IsAlive && actor.Side == DRPGBattleSide.Player) ReleaseEnemyPlansAfterPlayerResponse();
            if (actor.IsAlive) ScheduleNextActor(actor, action.Spec);
            RequireFlowTransition(flow.TryCompleteFinalizationToNextTurn(value.Identity.ActionId, out error), error);
        }
        currentActionIdentity = default;
        return true;
    }

    private void AccrueStaminaAfterCompletedRoot(BattleRuleMutationLease lease)
    {
        var draft = transactionState.CreateWorkspace();
        bool changed = false;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant == null || !participant.IsAlive ||
                !participant.IsDeployed)
                continue;
            ParticipantMutationBuilder mutation = draft.For(
                new ParticipantId(participant.RuntimeId));
            BattleStaminaValue next = mutation.Stamina.Accrue(
                mutation.Snapshot.StaminaRecoveryPerCompletedAction);
            if (next == mutation.Stamina)
                continue;
            mutation.SetStamina(next);
            changed = true;
        }
        if (!changed)
            return;
        BattleJournalCommitResult committed =
            transactionState.TryCommitJournal(lease, draft);
        if (!committed.Succeeded)
            throw new InvalidOperationException(
                "Completed-action stamina accrual failed: " + committed.Error);
    }

    internal void CompleteStandardResult(in RootActionCompletion value, BattleRuleMutationLease lease)
    {
        if (!standardDelivered || !ReferenceEquals(value.Lease, lease) || !transactionState.OwnsRuleMutationLease(lease, value.Owner))
            throw new InvalidOperationException("Standard result lost its final notification lease.");
        using (transactionState.OpenCompatibilityMutationScope(lease))
            CompleteBattle(value.Outcome == DRPGBattleOutcomeCandidate.Defeat ? DRPGBattleOutcome.Defeat : DRPGBattleOutcome.Victory);
        lease.Dispose(); standardRoot.Dispose();
    }
    internal BattleStartupPrepareStatus PrepareStart(IDRPGBattlePresentationClock clock) => turnEntryRunner.PrepareStart(clock);
    internal bool EnqueuePreparedStartup(BattleStartIdentity identity) => turnEntryRunner.EnqueuePreparedStartup(identity);
    internal bool TryBindTurnEntryPresentationFactory(IBattleTurnEntryPresentationFactory factory) =>
        turnEntryRunner.TryBindPresentationFactory(factory);
    internal string TurnEntryFault => standardFault.Length != 0 ? standardFault : turnEntryRunner.Fault;
    internal ActorTurnToken CurrentActorTurnToken => currentActorTurnToken;
    internal ActionIdentity CurrentActionIdentity => currentActionIdentity;
    internal DRPGBattleTransactionArbiter TransactionArbiter => transactionArbiter;
    internal DRPGBattleSessionState TransactionState => transactionState;
    internal DRPGBattleActionResolutionLedgerStore ActionResolutionLedgerStore =>
        actionResolutionLedgerStore;
    internal DRPGBattleActionExecutionRouter ActionExecutionRouter =>
        actionExecutionRouter;
    internal DRPGBattleTransactionPresentationPortSlot
        TransactionPresentationPortSlot => transactionPresentationPortSlot;
    internal IReadOnlyList<ActionTraceEntry> TransactionTrace =>
        transactionTrace.Snapshot();
    public BattleParticipantRuntime CurrentActor { get; private set; }
    public DRPGBattleOutcome Outcome { get; private set; }
    public DRPGBattlePhase Phase => flow.LegacyPhase;
    public DRPGBattleLifecycleState LifecycleState => flow.LifecycleState;
    public DRPGBattleActionFlowStage ActionStage => flow.ActionStage;
    public bool IsActive =>
        Phase != DRPGBattlePhase.Inactive &&
        Phase != DRPGBattlePhase.Result &&
        Outcome == DRPGBattleOutcome.None;

    private void PublishNotification(Action listeners, string kind)
    {
        transactionState.PublishNotification(() => eventPublisher.Publish(listeners, kind));
    }

    private void PublishNotification<T>(Action<T> listeners, T value, string kind) =>
        transactionState.PublishNotification(() => eventPublisher.Publish(listeners, value, kind));

    internal bool TryCommitTestHarnessMutation(
        Action<DRPGBattleMutationWorkspace> configure,
        out BattleJournalCommitResult result,
        out string error)
    {
        result = default;
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        BattleStartIdentity testIdentity =
            transactionIdentityAuthority.AllocateBattleStart();
        if (!transactionArbiter.TryAcquire(
                BattleTransactionIdentity.ForTestHarness(testIdentity),
                out BattleRuleMutationLease lease,
                out error))
        {
            return false;
        }
        try
        {
            DRPGBattleMutationWorkspace workspace =
                transactionState.CreateWorkspace();
            configure(workspace);
            result = transactionState.TryCommitJournal(lease, workspace);
            error = result.Error;
            return result.Succeeded;
        }
        finally
        {
            lease.Dispose();
        }
    }

    public DRPGBattlePreparedAction ActiveAction => activeAction;
    public DRPGBattlePreparedAction PendingFinalizationAction =>
        pendingFinalizationAction;
    public DRPGBattlePreparedAction LastPreparedAction { get; private set; }
    public DRPGBattleEventBatch LastRootEventBatch { get; private set; }
    public DRPGBattleReactionRun LastReactionRun { get; private set; }
    public DRPGBattleProgressRun LastProgressRun { get; private set; }
    public DRPGBattleProgressPipeline ProgressPipeline => progressPipeline;
    public BasicAttackOnlyAcceptanceOptions AcceptanceOptions =>
        Outcome == DRPGBattleOutcome.None && !LastBattleFault.HasValue ? acceptanceOptions : default;
    public IDRPGBattleResonanceAccess ResonanceAccess => resonanceAccess;
    public IReadOnlyList<DRPGCombatantProfileSO> ResonancePartners =>
        resonanceAccess != null
            ? resonanceAccess.GetSelectablePartners()
            : Array.Empty<DRPGCombatantProfileSO>();
    public DRPGFreeStrikeWindowContext ActiveFreeStrikeWindow =>
        activeFreeStrikeWindow;
    public bool HasStagedActionConsumer => stagedActionConsumer != null;
    public IReadOnlyList<DRPGTelegraphedAction> PendingTelegraphs =>
        pendingTelegraphs;
    public IReadOnlyList<DRPGEnemyActionPlanSnapshot> EnemyPlans =>
        CreateEnemyPlanSnapshots(false);
    public IReadOnlyList<DRPGEnemyActionPlanSnapshot> ReadyEnemyPlans =>
        CreateEnemyPlanSnapshots(true);
    public bool IsCurrentEnemyCommandFromOrdinaryIntent =>
        currentEnemyPlan != null &&
        CurrentActor != null &&
        currentEnemyPlan.ActorRuntimeId == CurrentActor.RuntimeId &&
        currentEnemyPlan.State == DRPGEnemyPlanState.Executing;
    public IReadOnlyList<DRPGBattleItemDefinition> BattleItems =>
        itemSource != null
            ? itemSource.Items
            : Array.Empty<DRPGBattleItemDefinition>();

    public DRPGBattleSkillAvailability GetSkillAvailability(DRPGSkillSO skill)
    {
        BattleParticipantRuntime actor = CurrentActor;
        DRPGProgressSnapshot progress = ResolveSkillProgressSnapshot(
            actor,
            skill);
        if (actor == null || skill == null)
        {
            return new DRPGBattleSkillAvailability(
                skill,
                false,
                "No active actor or skill is available.",
                progress);
        }
        if (!IsSkillAvailable(actor, skill))
        {
            return new DRPGBattleSkillAvailability(
                skill,
                false,
                "The selected skill is not available to the current actor.",
                progress);
        }
        if (!skill.CanUseFrom(actor.FormationRow))
        {
            return new DRPGBattleSkillAvailability(
                skill,
                false,
                "The skill cannot be used from the current formation row.",
                progress);
        }
        bool available = CanAffordSkill(
            actor,
            skill,
            DRPGBattleCommandStage.Immediate,
            out string error);
        return new DRPGBattleSkillAvailability(
            skill,
            available,
            error,
            progress);
    }

    public bool TryGetInteractiveInputResourceSnapshot(
        DRPGBattlePreparedAction action,
        out DRPGInteractiveInputResourceSnapshot snapshot)
    {
        snapshot = null;
        if (!IsActiveAction(action) ||
            Phase != DRPGBattlePhase.InteractiveAction ||
            action.Spec.ExecutionMode != DRPGSkillExecutionMode.FreeStrike)
        {
            return false;
        }

        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        if (actor == null)
            return false;
        DRPGProgressSnapshot progress = ResolveProgressSnapshot(
            actor,
            action.Spec.InputProgressCost);
        DRPGBattleWeaponActionContext weapon = action.WeaponActionContext;
        bool hasWeapon = weapon != null && weapon.Weapon != null;
        int currentDurability = hasWeapon
            ? weapon.Weapon.CurrentDurability
            : 0;
        int projectedDurability = hasWeapon
            ? Math.Max(0, currentDurability - weapon.CommittedUseUnits)
            : 0;
        int maximumDurability = hasWeapon
            ? weapon.Weapon.CurrentMaxDurability
            : 0;
        snapshot = new DRPGInteractiveInputResourceSnapshot(
            progress,
            actor.CurrentPosture,
            actor.EffectiveMaxPosture,
            hasWeapon,
            currentDurability,
            projectedDurability,
            maximumDurability);
        return true;
    }
    public DRPGBattleEscapeRules EscapeRules => escapeRules;
    public DRPGFormationRules FormationRules => formationRules;
    public bool HasIncomingEnemyWarning => reinforcementDelayRemaining >= 0;
    public int ReserveEnemyCount => reserveEnemies.Count;
    public int ActiveEnemyCount => CountLivingDeployedEnemies();

    public bool TryGetProgressSnapshot(
        int actorRuntimeId,
        string resourceId,
        out DRPGProgressSnapshot snapshot)
    {
        snapshot = null;
        BattleParticipantRuntime actor = FindParticipant(actorRuntimeId);
        return actor != null && actor.Profile != null &&
            progressPipeline.TryGetSnapshot(
                actor.Profile.CombatantId,
                resourceId,
                DRPGProgressScope.Battle,
                out snapshot);
    }

    public IReadOnlyList<DRPGBattleWeaponCandidate>
        GetCurrentWeaponCandidates()
    {
        return CurrentActor != null && equipmentService != null
            ? equipmentService.GetCandidates(CurrentActor.RuntimeId) ??
                Array.Empty<DRPGBattleWeaponCandidate>()
            : Array.Empty<DRPGBattleWeaponCandidate>();
    }

    public int GetItemQuantity(DRPGBattleItemDefinition item)
    {
        return itemSource != null ? itemSource.GetQuantity(item) : 0;
    }

    public bool CanTargetWithSkill(
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        DRPGSkillSO skill)
    {
        if (actor == null || target == null || skill == null ||
            !actor.IsAlive || !actor.IsDeployed ||
            !target.IsAlive || !target.IsDeployed ||
            !skill.CanUseFrom(actor.FormationRow) ||
            !skill.CanTarget(target.FormationRow))
        {
            return false;
        }

        switch (skill.TargetRule)
        {
            case DRPGSkillTargetRule.SingleAlly:
            case DRPGSkillTargetRule.AllAllies:
                return actor.Side == target.Side;
            case DRPGSkillTargetRule.Self:
                return ReferenceEquals(actor, target);
            case DRPGSkillTargetRule.BattlefieldPoint:
                return skill.ExecutionMode == DRPGSkillExecutionMode.FreeStrike &&
                    actor.Side != target.Side;
            case DRPGSkillTargetRule.None:
                return false;
            default:
                return actor.Side != target.Side;
        }
    }

    public bool CanCurrentActorTarget(
        BattleParticipantRuntime target,
        DRPGSkillSO skill)
    {
        return CanTargetWithSkill(CurrentActor, target, skill);
    }

    public bool TryAttachStagedActionConsumer(object consumer)
    {
        return TryAttachStagedActionConsumer(consumer, null);
    }

    internal bool TryAttachStagedActionConsumer(
        object consumer,
        Action<DRPGBattlePreparedAction> onPrepared)
    {
        if (consumer == null)
            return false;

        if (stagedActionConsumer == null || ReferenceEquals(stagedActionConsumer, consumer))
        {
            stagedActionConsumer = consumer;
            stagedActionConsumerCallback = onPrepared;
            return true;
        }

        return false;
    }

    public void DetachStagedActionConsumer(object consumer)
    {
        if (consumer != null && ReferenceEquals(stagedActionConsumer, consumer))
        {
            stagedActionConsumer = null;
            stagedActionConsumerCallback = null;
        }
    }

    public void Start()
    {
        if (LastBattleFault.HasValue || turnEntryRunner.Fault.Length != 0 || standardFault.Length != 0) return;
        BattleStartIdentity startIdentity =
            transactionIdentityAuthority.AllocateBattleStart();
        if (!transactionArbiter.TryAcquire(
                BattleTransactionIdentity.ForBattleStart(startIdentity),
                out BattleRuleMutationLease lease,
                out string error))
        {
            throw new InvalidOperationException(error);
        }
        try
        {
            using (transactionState.OpenCompatibilityMutationScope(lease))
                StartLegacyCore();
        }
        finally
        {
            lease.Dispose();
        }
    }

    private void StartLegacyCore()
    {
        InitializeBattleState();
        if (!flow.TryBeginTurn(out var error)) throw new InvalidOperationException(error);
        BeginNextTurn();
    }

    internal void InitializePreparedStartup(BattleRuleMutationLease lease)
    {
        if (lease == null || lease.Owner.Kind != BattleTransactionKind.BattleStart ||
            !transactionState.OwnsRuleMutationLease(lease, lease.Owner))
            throw new InvalidOperationException("Startup requires this Session's active BattleStart lease.");
        using (transactionState.OpenCompatibilityMutationScope(lease)) InitializeBattleState();
    }

    internal bool TryBeginStartupCandidate(BattleRuleMutationLease lease, out DRPGTurnCandidateReservation candidate, out string error)
    {
        candidate = default;
        if (!flow.TryBeginTurn(out error)) return false;
        return TryReserveActionableTurnCandidate(lease, out candidate, out error);
    }

    internal int WavePerRowLimit => formationRules.PerRowLimit;
    internal float WaveDeploymentTime => transactionState.CaptureRuleSnapshot().CurrentLogicalTime;
    internal bool TryBeginWaveCandidate(BattleRuleMutationLease lease, out DRPGTurnCandidateReservation candidate, out string error)
    {
        if (LifecycleState == DRPGBattleLifecycleState.TurnBegin)
            return TryReserveActionableTurnCandidate(lease, out candidate, out error);
        return TryBeginStartupCandidate(lease, out candidate, out error);
    }
    internal void AdoptCommittedWave(IReadOnlyList<BattleParticipantRuntime> members, BattleRuleMutationLease lease)
    {
        if (lease == null || lease.Owner.Kind != BattleTransactionKind.WaveTransition ||
            !transactionState.OwnsRuleMutationLease(lease, lease.Owner))
            throw new InvalidOperationException("Wave roster adoption requires the original committed wave lease.");
        foreach (var member in members)
            if (!member.IsDeployed || !reserveEnemies.Contains(member) || participants.Contains(member))
                throw new InvalidOperationException("Wave roster does not match the committed reserve batch.");
        foreach (var member in members) { reserveEnemies.Remove(member); participants.Add(member); }
        using (transactionState.OpenCompatibilityMutationScope(lease)) PlanInitialEnemyActions();
    }

    private void InitializeBattleState()
    {
        if (!flow.TryStartBattle(out string flowError))
            throw new InvalidOperationException(flowError);

        Outcome = DRPGBattleOutcome.None;
        basicAttackSelectionState.SetBattleAvailable(true);
        basicAttackSelectionState.ClearTurn();
        activeAction = null;
        activeResolution = null;
        pendingFinalizationAction = null;
        pendingFinalizationOutcome = DRPGBattleOutcome.None;
        LastRootEventBatch = null;
        LastReactionRun = null;
        LastProgressRun = null;
        activeFreeStrikeWindow = null;
        activeEscapeSucceeded = false;
        currentEnemyPlan = null;
        currentEnemyPlanFizzled = false;
        currentActorTurnToken = default;
        CurrentActor = null;
        activeLegacyBasicAttackSelection = default;
        currentActionIdentity = default;
        transactionState.TurnCandidates.ResetLegacy();
        completedPlayerActionSerial = 0L;
        nextEnemyPlanId = 1L;
        reinforcementDelayRemaining = -1;
        activeHits.Clear();
        activeEffects.Clear();
        pendingTimelineDeltas.Clear();
        pendingTimelinePriorities.Clear();
        pendingTelegraphs.Clear();
        enemyPlans.Clear();
        NormalizeFormationRows(false);
        scheduler.Initialize(participants);
        PlanInitialEnemyActions();
        PublishTimelineChanged();
    }

    public bool TryBeginTargetSelection(DRPGSkillSO skill, out string error)
    {
        error = string.Empty;
        if (!transactionArbiter.IsCommandInputOpen || !IsActive || CurrentActor == null ||
            CurrentActor.Side != DRPGBattleSide.Player ||
            Phase != DRPGBattlePhase.AwaitingCommand &&
            Phase != DRPGBattlePhase.TargetSelection)
        {
            error = "The current actor cannot begin player target selection.";
            return false;
        }

        if (standardEnabled && !DRPGBattleSkillModePolicy.IsAllowed(skill))
        {
            error = "Manual targeting modes are unavailable in Battle V2.";
            return false;
        }

        if (!IsSkillAvailable(CurrentActor, skill))
        {
            error = "The selected skill is not available to the current actor.";
            return false;
        }

        if (!skill.CanUseFrom(CurrentActor.FormationRow))
        {
            error = "The selected skill cannot be used from the actor's current formation row.";
            return false;
        }

        if (!CanAffordSkill(CurrentActor, skill, DRPGBattleCommandStage.Immediate,
                out error))
        {
            return false;
        }

        if (!HasLegalTarget(CurrentActor, skill))
        {
            error = "No living target is within this skill's formation range.";
            return false;
        }

        bool isBasicAttack = CurrentActor.Build != null &&
            ReferenceEquals(CurrentActor.Build.BasicAttack, skill);
        TargetSelectionToken openedSelection = default;
        if (isBasicAttack)
        {
            TargetSelectionOpenResult opened =
                actionExecutionRouter.TryOpenBasicAttackSelection(
                    currentActorTurnToken);
            if (!opened.IsAccepted)
            {
                error = "Basic Attack selection was rejected: " +
                    opened.Rejection + ".";
                return false;
            }
            openedSelection = opened.Selection;
        }
        if (!flow.TryBeginTargetSelection(out error))
        {
            if (openedSelection.IsValid)
                actionExecutionRouter.TryCancel(openedSelection);
            return false;
        }
        if (openedSelection.IsValid)
            activeLegacyBasicAttackSelection = openedSelection;
        return true;
    }

    public bool TryBeginResonanceTargetSelection(
        DRPGSkillSO skill,
        out string error)
    {
        error = string.Empty;
        if (!transactionArbiter.IsCommandInputOpen || !IsActive || CurrentActor == null ||
            CurrentActor.Side != DRPGBattleSide.Player ||
            Phase != DRPGBattlePhase.AwaitingCommand &&
            Phase != DRPGBattlePhase.TargetSelection)
        {
            error = "The current actor cannot begin resonance target selection.";
            return false;
        }
        if (resonanceAccess == null)
        {
            error = "Resonance battle access is unavailable.";
            return false;
        }

        DRPGResonanceSkillAvailability availability =
            resonanceAccess.GetAvailability(CurrentActor, skill, participants);
        if (!availability.IsAvailable)
        {
            error = availability.Reason;
            return false;
        }
        if (!skill.CanUseFrom(CurrentActor.FormationRow))
        {
            error = "The resonance skill cannot be used from the actor's current formation row.";
            return false;
        }
        if (!CanAffordSkill(
                CurrentActor,
                skill,
                DRPGBattleCommandStage.Immediate,
                out error))
        {
            return false;
        }
        if (!HasLegalTarget(CurrentActor, skill))
        {
            error = "No living target is within this resonance skill's formation range.";
            return false;
        }

        return flow.TryBeginTargetSelection(out error);
    }

    public bool TrySelectResonancePartner(
        DRPGCombatantProfileSO partner,
        out string error)
    {
        if (!transactionArbiter.IsCommandInputOpen || !IsActive || CurrentActor == null ||
            CurrentActor.Side != DRPGBattleSide.Player ||
            Phase != DRPGBattlePhase.AwaitingCommand)
        {
            error = "A resonance partner can only be selected at the player command gate.";
            return false;
        }
        if (resonanceAccess == null)
        {
            error = "Resonance battle access is unavailable.";
            return false;
        }
        return resonanceAccess.TrySelectPartner(partner, out error);
    }

    public bool TryBeginItemTargetSelection(
        DRPGBattleItemDefinition item,
        out string error)
    {
        error = string.Empty;
        if (!transactionArbiter.IsCommandInputOpen || !IsActive || CurrentActor == null ||
            CurrentActor.Side != DRPGBattleSide.Player ||
            Phase != DRPGBattlePhase.AwaitingCommand &&
            Phase != DRPGBattlePhase.TargetSelection)
        {
            error = "The current actor cannot select a battle-item target.";
            return false;
        }
        if (item == null || itemSource == null ||
            itemSource.GetQuantity(item) <= 0)
        {
            error = "The selected battle item is unavailable.";
            return false;
        }
        return flow.TryBeginTargetSelection(out error);
    }

    public bool TryCancelTargetSelection(out string error)
    {
        if (!transactionArbiter.IsCommandInputOpen || !IsActive || CurrentActor == null ||
            CurrentActor.Side != DRPGBattleSide.Player ||
            Phase != DRPGBattlePhase.TargetSelection ||
            activeAction != null)
        {
            error = "There is no cancellable player target selection.";
            return false;
        }

        if (!flow.TryCancelTargetSelection(out error))
            return false;
        if (activeLegacyBasicAttackSelection.IsValid)
        {
            actionExecutionRouter.TryCancel(activeLegacyBasicAttackSelection);
            activeLegacyBasicAttackSelection = default;
        }
        return true;
    }

    public bool TryPreviewCommand(DRPGBattleCommand command, out string error)
    {
        if (!transactionArbiter.IsCommandInputOpen ||
            Phase != DRPGBattlePhase.AwaitingCommand && Phase != DRPGBattlePhase.TargetSelection)
        {
            error = "The battle is not accepting a target preview.";
            return false;
        }

        if (!ValidateCommand(command, out _, out _, out error))
            return false;

        if (IsCurrentEnemyCommandFromOrdinaryIntent)
        {
            // Ordinary intents already committed their target at warning zero.
            // Do not route them through the legacy target-preview disclosure.
            return flow.TryBeginTargetSelection(out error);
        }

        if (!flow.TryBeginTargetSelection(out error))
            return false;
        PublishNotification(CommandPreviewed, command, nameof(CommandPreviewed));
        return true;
    }

    public bool TrySubmitCommand(DRPGBattleCommand command, out string error)
    {
        if (!TryPrepareCommand(command, out DRPGBattlePreparedAction action, out error))
            return false;

        return TryResolveImpact(action, out _, out error) &&
            TryCompleteAction(action, out error);
    }

    public bool TryPrepareUntargetedPlayerCommand(
        DRPGSkillSO skill,
        out DRPGBattlePreparedAction action,
        out string error)
    {
        action = null;
        if (!IsActive || CurrentActor == null ||
            CurrentActor.Side != DRPGBattleSide.Player ||
            Phase != DRPGBattlePhase.AwaitingCommand)
        {
            error = "The current actor cannot begin an untargeted player action.";
            return false;
        }

        if (skill == null ||
            skill.ExecutionMode != DRPGSkillExecutionMode.FreeStrike)
        {
            error = "Only free-strike skills can begin without selecting a target.";
            return false;
        }

        if (!IsSkillAvailable(CurrentActor, skill))
        {
            error = "The selected skill is not available to the current actor.";
            return false;
        }

        DRPGBattleSide opposingSide = CurrentActor.Side == DRPGBattleSide.Player
            ? DRPGBattleSide.Enemy
            : DRPGBattleSide.Player;
        BattleParticipantRuntime commandAnchor =
            FirstLivingLegalTarget(CurrentActor, opposingSide, skill);
        if (commandAnchor == null)
        {
            error = "No living opponent is available for the free-strike action.";
            return false;
        }

        return TryPrepareCommand(
            new DRPGBattleCommand(
                CurrentActor.RuntimeId,
                commandAnchor.RuntimeId,
                skill),
            out action,
            out error);
    }

    public bool TryPrepareUntargetedResonanceCommand(
        DRPGSkillSO skill,
        out DRPGBattlePreparedAction action,
        out string error)
    {
        action = null;
        if (!IsActive || CurrentActor == null ||
            CurrentActor.Side != DRPGBattleSide.Player ||
            Phase != DRPGBattlePhase.AwaitingCommand)
        {
            error = "The current actor cannot begin an untargeted resonance action.";
            return false;
        }
        if (skill == null ||
            skill.ExecutionMode != DRPGSkillExecutionMode.FreeStrike)
        {
            error = "Only free-strike resonance skills can begin without a target.";
            return false;
        }
        if (resonanceAccess == null)
        {
            error = "Resonance battle access is unavailable.";
            return false;
        }
        DRPGResonanceSkillAvailability availability =
            resonanceAccess.GetAvailability(CurrentActor, skill, participants);
        if (!availability.IsAvailable)
        {
            error = availability.Reason;
            return false;
        }

        DRPGBattleSide opposingSide = CurrentActor.Side == DRPGBattleSide.Player
            ? DRPGBattleSide.Enemy
            : DRPGBattleSide.Player;
        BattleParticipantRuntime anchor = FirstLivingLegalTarget(
            CurrentActor,
            opposingSide,
            skill);
        if (anchor == null)
        {
            error = "No living opponent is available for the resonance free strike.";
            return false;
        }
        return TryPrepareCommand(
            DRPGBattleCommand.ResonanceSkill(
                CurrentActor.RuntimeId,
                anchor.RuntimeId,
                skill),
            out action,
            out error);
    }

    public bool TryPrepareCommand(
        DRPGBattleCommand command,
        out DRPGBattlePreparedAction action,
        out string error)
    {
        if (!transactionArbiter.IsCommandInputOpen)
        {
            action = null;
            error = "The current battle transaction still owns command input.";
            return false;
        }
        bool openedForDirectBasicAttack = false;
        if (command != null &&
            command.Kind == DRPGBattleCommandKind.BasicAttack &&
            !activeLegacyBasicAttackSelection.IsValid)
        {
            TargetSelectionOpenResult opened =
                actionExecutionRouter.TryOpenBasicAttackSelection(
                    currentActorTurnToken);
            if (!opened.IsAccepted)
            {
                action = null;
                error = "Basic Attack selection was rejected: " +
                    opened.Rejection + ".";
                return false;
            }
            activeLegacyBasicAttackSelection = opened.Selection;
            openedForDirectBasicAttack = true;
        }
        LegacyActionStartResult result =
            actionExecutionRouter.TryStartLegacy(
                command,
                TryPrepareLegacyCommand);
        if (!result.IsAccepted && openedForDirectBasicAttack &&
            activeLegacyBasicAttackSelection.IsValid)
        {
            actionExecutionRouter.TryCancel(
                activeLegacyBasicAttackSelection);
            activeLegacyBasicAttackSelection = default;
        }
        action = result.Action;
        error = result.Error;
        return result.IsAccepted;
    }

    private LegacyActionStartResult TryPrepareLegacyCommand(
        DRPGBattleCommand command)
    {
        return TryPrepareLegacyCommand(command, out DRPGBattlePreparedAction action,
            out string error)
            ? LegacyActionStartResult.Accepted(action)
            : LegacyActionStartResult.Rejected(error);
    }

    internal bool TryPrepareLegacyCommand(
        DRPGBattleCommand command,
        out DRPGBattlePreparedAction action,
        out string error)
    {
        action = null;
        if (activeAction != null ||
            Phase != DRPGBattlePhase.AwaitingCommand &&
            Phase != DRPGBattlePhase.TargetSelection)
        {
            error = "Another action is already active or commands are not accepted.";
            return false;
        }

        if (!ValidateCommand(
                command,
                out BattleParticipantRuntime actor,
                out _,
                out error))
            return false;
        if (!currentActorTurnToken.IsValid ||
            currentActorTurnToken.BattleGeneration != BattleGeneration ||
            currentActorTurnToken.Actor.Value != actor.RuntimeId)
        {
            error = "The current actor has no matching domain turn token.";
            return false;
        }

        DRPGResolvedActionSpec spec;
        if (command.Stage == DRPGBattleCommandStage.Release)
        {
            DRPGTelegraphedAction pending = FindTelegraph(actor.RuntimeId);
            spec = pending != null && pending.Spec != null
                ? pending.Spec.CreateRelease(command)
                : DRPGActionSpecResolver.Resolve(command, actor);
        }
        else
        {
            spec = DRPGActionSpecResolver.Resolve(command, actor);
        }
        if (!CanAffordResolvedProgress(
                actor,
                spec,
                spec.ExecutionMode == DRPGSkillExecutionMode.FreeStrike,
                out error))
        {
            return false;
        }
        if (!DRPGActionResourceTransaction.TryReserve(
                spec,
                actor,
                out DRPGActionResourceTransaction resourceTransaction,
                out error))
        {
            return false;
        }
        if (!DRPGActionProgressTransaction.TryReserve(
                spec,
                actor,
                progressPipeline,
                out DRPGActionProgressTransaction progressTransaction,
                out error))
        {
            resourceTransaction.Cancel();
            return false;
        }

        DRPGBasicAttackFrozenSnapshot? basicAttackSnapshot = null;
        if (command.Kind == DRPGBattleCommandKind.BasicAttack &&
            command.Skill != null)
        {
            var selection = new BasicAttackSelection(
                activeLegacyBasicAttackSelection,
                new ParticipantId(command.TargetRuntimeId));
            if (!actionExecutionRouter.TryFreezeLegacyBasicAttack(
                    selection,
                    out DRPGBasicAttackFrozenSnapshot frozen,
                    out BasicAttackRejection snapshotRejection))
            {
                resourceTransaction.Cancel();
                progressTransaction?.Cancel();
                action = null;
                error = "The Basic Attack snapshot could not be frozen: " +
                    snapshotRejection + ".";
                return false;
            }
            basicAttackSnapshot = frozen;
        }

        using (DRPGBattleActionIdentityReservation identityReservation =
            transactionIdentityAuthority.ReserveAction(
                currentActorTurnToken))
        {
            long actionId = identityReservation.CandidateActionId;
            var candidateIdentity = new ActionIdentity(
                BattleGeneration,
                currentActorTurnToken.TurnSerial,
                actionId);
            if (!transactionArbiter.TryAcquire(
                    BattleTransactionIdentity.ForLegacyAction(candidateIdentity),
                    out BattleRuleMutationLease legacyLease,
                    out error))
            {
                resourceTransaction.Cancel();
                progressTransaction?.Cancel();
                action = null;
                return false;
            }
            DRPGBattleWeaponActionContext weaponContext =
                ShouldCaptureWeaponUse(command) && weaponDurabilityService != null
                    ? weaponDurabilityService.CaptureAction(actor, actionId)
                    : null;
            var legacyPort = new DRPGLegacyActionMutationPort(
                candidateIdentity,
                legacyLease,
                transactionState,
                TryResolveLegacyImpact,
                TryCommitLegacyImpactPartition,
                TryCommitLegacyAction,
                TryFinalizeLegacyAction);
            DRPGBasicAttackTraceCorrelation basicAttackTrace = default;
            if (!flow.TryLockAction(actionId, out error))
            {
                weaponDurabilityService?.CancelAction(weaponContext);
                resourceTransaction.Cancel();
                progressTransaction?.Cancel();
                legacyPort.CancelBeforeActivation();
                action = null;
                return false;
            }
            if (basicAttackSnapshot.HasValue)
            {
                var selection = new BasicAttackSelection(
                    activeLegacyBasicAttackSelection,
                    new ParticipantId(command.TargetRuntimeId));
                if (!actionExecutionRouter.TryConfirmLegacyBasicAttack(
                        selection,
                        out DRPGBasicAttackFrozenSnapshot confirmed,
                        out basicAttackTrace,
                        out BasicAttackRejection confirmationRejection))
                {
                    weaponDurabilityService?.CancelAction(weaponContext);
                    resourceTransaction.Cancel();
                    progressTransaction?.Cancel();
                    legacyPort.CancelBeforeActivation();
                    RequireFlowTransition(
                        flow.TryCancelActionToCommand(
                            actionId,
                            out string cancelError),
                        cancelError);
                    action = null;
                    error = "The Basic Attack selection could not be " +
                        "confirmed: " + confirmationRejection + ".";
                    return false;
                }
                basicAttackSnapshot = confirmed;
                activeLegacyBasicAttackSelection = default;
            }
            currentActionIdentity = identityReservation.Commit();
            if (currentActionIdentity != candidateIdentity)
            {
                throw new InvalidOperationException(
                    "The committed Legacy action identity changed after lock.");
            }
            if (basicAttackSnapshot.HasValue)
            {
                basicAttackSnapshot = basicAttackSnapshot.Value.WithIdentity(
                    currentActionIdentity);
            }
            action = new DRPGBattlePreparedAction(
                candidateIdentity,
                command,
                spec,
                resourceTransaction,
                weaponContext,
                progressTransaction,
                legacyMutationPort: legacyPort,
                basicAttackSnapshot: basicAttackSnapshot);
            activeAction = action;
            if (standardEnabled) legacyInitialSnapshot = transactionState.CaptureRuleSnapshot();
            if (basicAttackSnapshot.HasValue)
            {
                DRPGBasicAttackTrace.RecordActionLocked(
                    transactionTrace,
                    basicAttackSnapshot.Value,
                    BattleTransactionIdentity.ForLegacyAction(
                        candidateIdentity),
                    legacyPort.LeaseSerial,
                    basicAttackTrace.SelectionConfirmedEventId);
            }
        }
        LastPreparedAction = action;
        activeResolution = null;
        activeEscapeSucceeded = false;
        activeHits.Clear();
        activeEffects.Clear();
        PublishNotification(ActionStarted, command, nameof(ActionStarted));
        DRPGBattlePreparedAction preparedAction = action;
        stagedActionConsumerCallback?.Invoke(preparedAction);
        PublishNotification(ActionPrepared, preparedAction, nameof(ActionPrepared));
        return true;
    }

    public bool TryResolveImpact(
        DRPGBattlePreparedAction action,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        if (!IsActiveAction(action) || action.LegacyMutationPort == null)
        {
            error = "The prepared action has no matching Legacy mutation port.";
            return false;
        }
        return action.LegacyMutationPort.TryResolve(
            action,
            out resolution,
            out error);
    }

    internal bool TryResolveLegacyImpact(
        DRPGBattlePreparedAction action,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        if (!IsActiveAction(action) || activeResolution != null)
        {
            error = "The prepared action cannot be resolved in its current state.";
            return false;
        }

        IReadOnlyList<int> pending = action.ImpactLedger.PendingIndices;
        if (pending.Count == 0)
        {
            error = "The prepared action has no pending impact partitions.";
            return false;
        }
        for (int i = 0; i < pending.Count; i++)
        {
            if (!TryCommitLegacyImpactPartition(
                    action.ActionId,
                    pending[i],
                    out _,
                    out error))
            {
                return false;
            }
        }

        resolution = activeResolution;
        if (resolution == null)
        {
            error = "The impact fallback did not complete the action sequence.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryCommitImpactPartition(
        long actionId,
        int impactIndex,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        DRPGBattlePreparedAction action = activeAction;
        if (action == null || action.ActionId != actionId ||
            action.LegacyMutationPort == null)
        {
            error = "The active action has no matching Legacy mutation port.";
            return false;
        }
        return action.LegacyMutationPort.TryCommitPartition(
            action,
            impactIndex,
            out resolution,
            out error);
    }

    internal bool TryCommitLegacyImpactPartition(
        long actionId,
        int impactIndex,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        DRPGBattlePreparedAction action = activeAction;
        if (action == null || action.ActionId != actionId ||
            activeResolution != null ||
            Phase != DRPGBattlePhase.PreparingAction &&
            Phase != DRPGBattlePhase.ExecutingAction)
        {
            error = "No matching prepared action can accept this impact.";
            return false;
        }
        if (impactIndex < 0 || impactIndex >= action.ImpactSequence.Count)
        {
            error = "The impact index is outside the frozen sequence.";
            return false;
        }
        if (!action.ImpactLedger.TryBeginCommit(impactIndex))
        {
            error = "The impact is duplicate, in flight, or out of order.";
            return false;
        }

        if (flow.ActionStage == DRPGBattleActionFlowStage.Locked)
        {
            if (!flow.TryBeginImpact(actionId, out error))
            {
                action.ImpactLedger.RollBackBegin(impactIndex);
                return false;
            }
        }
        else if (flow.ActionStage !=
                 DRPGBattleActionFlowStage.ResolvingImpact)
        {
            action.ImpactLedger.RollBackBegin(impactIndex);
            error = "The action flow is not accepting resolved impacts.";
            return false;
        }

        bool success;
        if (action.ImpactSequence.Count == 1)
        {
            success = TryResolveSingleImpactCore(
                action,
                out resolution,
                out error);
        }
        else
        {
            success = TryResolveIndexedImpactCore(
                action,
                action.ImpactSequence.GetAt(impactIndex),
                out resolution,
                out error);
        }

        if (!success)
        {
            action.ImpactLedger.RollBackBegin(impactIndex);
            return false;
        }

        action.ImpactLedger.CompleteCommit(impactIndex);
        bool complete = action.ImpactLedger.IsComplete;
        resolution = new DRPGBattleActionResolution(
            action,
            resolution.Hits,
            resolution.Effects,
            impactIndex,
            complete);
        if (complete)
        {
            activeResolution = new DRPGBattleActionResolution(
                action,
                activeHits.ToArray(),
                activeEffects.ToArray(),
                impactIndex,
                true);
            RequireFlowTransition(
                flow.TryCompleteImpact(actionId, out string flowError),
                flowError);
        }
        return true;
    }

    private bool TryResolveSingleImpactCore(
        DRPGBattlePreparedAction action,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        if (!IsActiveAction(action) ||
            activeResolution != null ||
            flow.ActionStage != DRPGBattleActionFlowStage.ResolvingImpact)
        {
            error = "The prepared action cannot be resolved in its current state.";
            return false;
        }

        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        BattleParticipantRuntime target = action.TargetRuntimeId > 0
            ? FindParticipant(action.TargetRuntimeId)
            : null;
        if (actor == null || !actor.IsAlive)
        {
            error = "The prepared action actor is no longer valid.";
            return false;
        }

        if (action.Command.Kind == DRPGBattleCommandKind.Wait)
        {
            if (!TryCommitActionResources(action, actor, out error))
                return false;
            resolution = new DRPGBattleActionResolution(
                action,
                Array.Empty<DRPGBattleHitResult>());
            activeResolution = resolution;
            PublishActionResolution(resolution);
            error = string.Empty;
            return true;
        }

        if (action.Command.Kind == DRPGBattleCommandKind.SwitchWeapon)
        {
            DRPGEquipmentOperationResult switchResult =
                equipmentService.TrySwitchWeapon(
                    actor,
                    action.Command.WeaponInstanceId,
                    out DRPGResolvedCombatantBuild replacementBuild);
            if (!switchResult.Success || replacementBuild == null)
            {
                CancelFailedPreparedAction(action);
                error = !string.IsNullOrWhiteSpace(switchResult.Message)
                    ? switchResult.Message
                    : "The weapon switch could not be resolved.";
                return false;
            }

            if (!TryCommitActionResources(action, actor, out error))
                return false;

            actor.ReplaceBuild(replacementBuild);
            resolution = new DRPGBattleActionResolution(
                action,
                Array.Empty<DRPGBattleHitResult>());
            activeResolution = resolution;
            PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
            PublishNotification(ParticipantBuildChanged, actor, nameof(ParticipantBuildChanged));
            PublishActionResolution(resolution);
            error = string.Empty;
            return true;
        }

        if (action.Command.Kind == DRPGBattleCommandKind.Reposition)
        {
            if (!action.Command.DestinationFormationRow.HasValue)
            {
                error = "The reposition command has no destination row.";
                return false;
            }
            if (!TryApplyReposition(
                    actor,
                    action.Command.DestinationFormationRow.Value,
                    out error))
            {
                return false;
            }
            if (!TryCommitActionResources(action, actor, out error))
                return false;
            resolution = new DRPGBattleActionResolution(
                action,
                Array.Empty<DRPGBattleHitResult>());
            activeResolution = resolution;
            PublishActionResolution(resolution);
            error = string.Empty;
            return true;
        }

        if (action.Command.Kind == DRPGBattleCommandKind.Defend)
        {
            if (!TryCommitActionResources(action, actor, out error))
                return false;
            // Battle V2 defense is an explicit in-place posture recovery action,
            // not a lingering damage-reduction stance. The authored battle
            // default is the current baseline recovery amount, not a cap.
            actor.RestorePosture(actor.EffectiveMaxPosture);
            PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
            resolution = new DRPGBattleActionResolution(
                action,
                Array.Empty<DRPGBattleHitResult>());
            activeResolution = resolution;
            PublishActionResolution(resolution);
            error = string.Empty;
            return true;
        }

        if (action.Command.Kind == DRPGBattleCommandKind.Escape)
        {
            if (!TryCommitActionResources(action, actor, out error))
                return false;
            activeEscapeSucceeded = RollEscape(actor);
            resolution = new DRPGBattleActionResolution(
                action,
                Array.Empty<DRPGBattleHitResult>());
            activeResolution = resolution;
            PublishActionResolution(resolution);
            error = string.Empty;
            return true;
        }

        if (action.Command.Kind == DRPGBattleCommandKind.Item)
        {
            if (target == null || !target.IsAlive || target.Side != actor.Side)
            {
                error = "The prepared item target is no longer valid.";
                return false;
            }
            if (itemSource == null ||
                !itemSource.TryConsume(action.Command.ItemDefinition, 1))
            {
                error = "The battle item is no longer available.";
                return false;
            }

            if (!TryCommitActionResources(action, actor, out error))
                return false;

            float requestedHeal = action.Command.ItemDefinition.HealAmount;
            float appliedHeal = target.RestoreHp(requestedHeal);
            PublishNotification(ParticipantChanged, target, nameof(ParticipantChanged));
            var itemEffects = Array.AsReadOnly(new[]
            {
                new DRPGActionEffectResult(
                    DRPGActionEffectKind.Heal,
                    DRPGActionEffectResultState.Applied,
                    actor.RuntimeId,
                    target.RuntimeId,
                    requestedHeal,
                    appliedHeal,
                    0f,
                    action.Command.ItemDefinition.ItemId)
            });
            resolution = new DRPGBattleActionResolution(
                action,
                Array.Empty<DRPGBattleHitResult>(),
                itemEffects);
            activeResolution = resolution;
            PublishActionResolution(resolution);
            error = string.Empty;
            return true;
        }

        if (target == null || !target.IsAlive ||
            !action.Spec.CanTarget(actor, target))
        {
            error = "The prepared action target is no longer legal.";
            return false;
        }

        if (action.Command.Stage == DRPGBattleCommandStage.Release ||
            action.Spec.ExecutionMode == DRPGSkillExecutionMode.FreeStrike)
        {
            error = "The prepared action requires interactive resolution.";
            return false;
        }

        if (!TryCommitActionResources(action, actor, out error))
            return false;

        if (action.Command.Stage == DRPGBattleCommandStage.Telegraph)
        {
            resolution = new DRPGBattleActionResolution(
                action,
                Array.Empty<DRPGBattleHitResult>());
            activeResolution = resolution;
            PublishActionResolution(resolution);
            error = string.Empty;
            return true;
        }

        DRPGResolvedActionBatch batch = resolver.Resolve(
            action.Spec,
            actor,
            target,
            this,
            action.ImpactSequence.GetAt(0).Context);
        activeHits.AddRange(batch.Hits);
        activeEffects.AddRange(batch.Effects);
        if (!TryRecordCommittedWeaponUse(
                action,
                ResolveWeaponUseKind(action),
                out error))
        {
            return false;
        }
        resolution = new DRPGBattleActionResolution(
            action,
            activeHits.ToArray(),
            activeEffects.ToArray());
        activeResolution = resolution;
        PublishEffectChanges(batch.Effects);
        PublishActionResolution(resolution);
        error = string.Empty;
        return true;
    }

    private bool TryResolveIndexedImpactCore(
        DRPGBattlePreparedAction action,
        DRPGResolvedImpactPartition partition,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        if (actor == null || !actor.IsAlive)
        {
            error = "The prepared action actor is no longer valid.";
            return false;
        }

        if (partition.Index == 0)
        {
            if (flow.ActionStage !=
                DRPGBattleActionFlowStage.ResolvingImpact)
            {
                error = "The first impact must begin from the prepared phase.";
                return false;
            }
            if (!TryCommitActionResources(action, actor, out error))
                return false;
            if (!TryRecordCommittedWeaponUse(
                    action,
                    ResolveWeaponUseKind(action),
                    out error))
            {
                return false;
            }
        }
        else if (flow.ActionStage !=
                 DRPGBattleActionFlowStage.ResolvingImpact)
        {
            error = "A later impact requires an executing action.";
            return false;
        }

        BattleParticipantRuntime target = partition.TargetRuntimeId > 0
            ? FindParticipant(partition.TargetRuntimeId)
            : null;
        DRPGResolvedActionBatch batch;
        if (target == null || !target.IsAlive ||
            !action.Spec.CanTarget(actor, target))
        {
            batch = new DRPGResolvedActionBatch(
                Array.Empty<DRPGBattleHitResult>(),
                Array.Empty<DRPGActionEffectResult>());
        }
        else
        {
            batch = resolver.Resolve(
                action.Spec,
                actor,
                target,
                this,
                partition.Context);
            activeHits.AddRange(batch.Hits);
            activeEffects.AddRange(batch.Effects);
            PublishEffectChanges(batch.Effects);
        }

        resolution = new DRPGBattleActionResolution(
            action,
            batch.Hits,
            batch.Effects,
            partition.Index,
            false);
        PublishActionResolution(resolution);
        error = string.Empty;
        return true;
    }

    public bool TryBeginInteractiveResolution(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (!TryOpenLegacyActionScope(action, out IDisposable scope, out error))
            return false;
        using (scope)
            return TryBeginInteractiveResolutionCore(action, out error);
    }

    private bool TryBeginInteractiveResolutionCore(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (!IsActiveAction(action) ||
            activeResolution != null ||
            Phase != DRPGBattlePhase.PreparingAction)
        {
            error = "The prepared action cannot begin interactive resolution.";
            return false;
        }

        bool isRelease = action.Command.Stage == DRPGBattleCommandStage.Release;
        bool isFreeStrike = action.Spec.ExecutionMode ==
            DRPGSkillExecutionMode.FreeStrike;
        if (!isRelease && !isFreeStrike)
        {
            error = "The prepared action is not interactive.";
            return false;
        }

        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        BattleParticipantRuntime target = FindParticipant(action.TargetRuntimeId);
        if (actor == null || target == null || !actor.IsAlive ||
            !target.IsAlive && !isRelease)
        {
            error = "The interactive action actor or target is no longer valid.";
            return false;
        }

        if (isFreeStrike && !CanAffordResolvedProgress(
                actor,
                action.Spec,
                true,
                out error))
        {
            return false;
        }

        if (!TryCommitActionResources(action, actor, out error))
            return false;

        if (!flow.TryBeginInteractiveResolution(action.ActionId, out error))
            return false;

        activeHits.Clear();
        activeEffects.Clear();
        if (isFreeStrike)
        {
            activeFreeStrikeWindow = new DRPGFreeStrikeWindowContext(
                action.ActionId,
                action.Spec.FreeStrikeSpec);
        }
        else
        {
            activeFreeStrikeWindow = null;
        }
        error = string.Empty;
        return true;
    }

    public bool TryResolveInteractiveImpact(
        DRPGBattlePreparedAction action,
        DRPGBattleImpactContext context,
        out DRPGBattleHitResult hit,
        out string error)
    {
        return TryResolveInteractiveImpactForTarget(
            action,
            action != null ? action.TargetRuntimeId : 0,
            context,
            out hit,
            out error);
    }

    public bool TryResolveInteractiveImpactForTarget(
        DRPGBattlePreparedAction action,
        int targetRuntimeId,
        DRPGBattleImpactContext context,
        out DRPGBattleHitResult hit,
        out string error)
    {
        hit = null;
        if (!TryOpenLegacyActionScope(action, out IDisposable scope, out error))
            return false;
        using (scope)
        {
            return TryResolveInteractiveImpactForTargetCore(
                action,
                targetRuntimeId,
                context,
                out hit,
                out error);
        }
    }

    private bool TryResolveInteractiveImpactForTargetCore(
        DRPGBattlePreparedAction action,
        int targetRuntimeId,
        DRPGBattleImpactContext context,
        out DRPGBattleHitResult hit,
        out string error)
    {
        hit = null;
        if (!IsActiveAction(action) ||
            Phase != DRPGBattlePhase.InteractiveAction)
        {
            error = "No matching interactive action is accepting impacts.";
            return false;
        }

        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        BattleParticipantRuntime target = FindParticipant(targetRuntimeId);
        bool isFreeStrike = action.Spec.ExecutionMode ==
            DRPGSkillExecutionMode.FreeStrike;
        if (actor == null || target == null || !actor.IsAlive || !target.IsAlive ||
            actor.Side == target.Side ||
            !action.Spec.CanTarget(actor, target) ||
            !isFreeStrike && targetRuntimeId != action.TargetRuntimeId)
        {
            error = "The interactive impact actor or target is no longer valid.";
            return false;
        }

        DRPGResolvedActionBatch batch = resolver.Resolve(
            action.Spec,
            actor,
            target,
            this,
            context);
        if (batch.Hits.Count == 0)
        {
            error = "The interactive impact produced no damage hit result.";
            return false;
        }
        hit = batch.Hits[0];
        activeHits.AddRange(batch.Hits);
        activeEffects.AddRange(batch.Effects);
        if (!TryRecordCommittedWeaponUse(
                action,
                ResolveWeaponUseKind(action),
                out error))
        {
            return false;
        }
        PublishEffectChanges(batch.Effects);
        PublishActionResolution(new DRPGBattleActionResolution(
            action,
            batch.Hits,
            batch.Effects));
        error = string.Empty;
        return true;
    }

    public bool TryResolveFreeStrikeInput(
        DRPGBattlePreparedAction action,
        IReadOnlyList<DRPGFreeStrikeBodyContact> contacts,
        out DRPGFreeStrikeInputResult result,
        out string error)
    {
        result = null;
        if (!TryOpenLegacyActionScope(action, out IDisposable scope, out error))
            return false;
        using (scope)
        {
            return TryResolveFreeStrikeInputCore(
                action,
                contacts,
                out result,
                out error);
        }
    }

    private bool TryResolveFreeStrikeInputCore(
        DRPGBattlePreparedAction action,
        IReadOnlyList<DRPGFreeStrikeBodyContact> contacts,
        out DRPGFreeStrikeInputResult result,
        out string error)
    {
        result = null;
        if (contacts == null)
        {
            error = "Free-strike contacts are missing.";
            return false;
        }
        if (!IsActiveAction(action) ||
            Phase != DRPGBattlePhase.InteractiveAction ||
            activeFreeStrikeWindow == null ||
            activeFreeStrikeWindow.ActionId != action.ActionId ||
            activeFreeStrikeWindow.IsResourceExhausted ||
            action.Spec.ExecutionMode != DRPGSkillExecutionMode.FreeStrike)
        {
            error = "No matching free-strike window is accepting input.";
            return false;
        }

        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        if (actor == null || !actor.IsAlive)
        {
            error = "The free-strike actor is no longer valid.";
            return false;
        }

        DRPGFreeStrikeWindowContext window = activeFreeStrikeWindow;
        if (!DRPGInteractiveInputTransaction.TryPrepare(
                actor,
                action.Spec,
                progressPipeline,
                weaponDurabilityService,
                action.WeaponActionContext,
                out DRPGInteractiveInputTransaction inputTransaction,
                out error) ||
            !inputTransaction.TryCommit(
                actor,
                progressPipeline,
                weaponDurabilityService,
                action.WeaponActionContext,
                out DRPGInteractiveInputReceipt inputReceipt,
                out error))
        {
            return false;
        }
        if (inputReceipt.ProgressMutation != null)
        {
            DRPGProgressMutationResult progressMutation =
                inputReceipt.ProgressMutation;
            PublishNotification(ProgressChanged, progressMutation, nameof(ProgressChanged));
        }
        PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));

        var impactResults = new List<DRPGFreeStrikeImpactResult>(contacts.Count);
        DRPGStrikeGrade highestGrade = DRPGStrikeGrade.Miss;
        for (int i = 0; i < contacts.Count; i++)
        {
            DRPGFreeStrikeBodyContact contact = contacts[i];
            BattleParticipantRuntime target = FindParticipant(
                contact.TargetRuntimeId);
            if (target == null || !target.IsAlive || target.Side == actor.Side ||
                !action.Spec.CanTarget(actor, target))
                continue;
            float damageMultiplier = contact.Role ==
                DRPGFreeStrikeImpactRole.Secondary
                    ? window.Spec.SecondaryDamageMultiplier
                    : 1f;
            DRPGResolvedActionBatch batch = resolver.Resolve(
                action.Spec,
                actor,
                target,
                this,
                DRPGBattleImpactContext.ForResolvedStrike(
                    DRPGStrikeGrade.Body,
                    window.Spec.BasePower,
                    damageMultiplier));
            if (batch.Hits.Count == 0)
                continue;
            DRPGBattleHitResult hit = batch.Hits[0];
            var impactResult = new DRPGFreeStrikeImpactResult(
                contact,
                hit);
            impactResults.Add(impactResult);
            activeHits.AddRange(batch.Hits);
            activeEffects.AddRange(batch.Effects);
            PublishEffectChanges(batch.Effects);
            PublishActionResolution(new DRPGBattleActionResolution(
                action,
                batch.Hits,
                batch.Effects));

            highestGrade = DRPGStrikeGrade.Body;
        }

        if (inputReceipt.EnterPostureBreakAfterImpact)
        {
            DRPGPostureBreakTransition transition =
                actor.EnterDeferredPostureBreak();
            if (transition.EnteredBreak)
            {
                PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
                PublishPostureBreakLifecycleEvent(
                    DRPGBattleEventKind.PostureBreakEntered,
                    actor.RuntimeId,
                    "interactive_input:" + action.ActionId);
                TryInterruptEnemyPlanForPostureBreak(actor);
            }
        }

        bool resourceExhausted = !CanAffordInteractiveInputProgress(
            actor,
            action.Spec,
            out _);
        if (resourceExhausted)
            window.MarkResourceExhausted();

        result = new DRPGFreeStrikeInputResult(
            impactResults,
            actor.IsPostureBroken,
            highestGrade,
            resourceExhausted);
        error = string.Empty;
        return true;
    }

    public bool TrySpendInteractiveActorPosture(
        DRPGBattlePreparedAction action,
        float amount,
        out bool exhausted,
        out string error)
    {
        exhausted = false;
        if (!TryOpenLegacyActionScope(action, out IDisposable scope, out error))
            return false;
        using (scope)
        {
            return TrySpendInteractiveActorPostureCore(
                action,
                amount,
                out exhausted,
                out error);
        }
    }

    private bool TrySpendInteractiveActorPostureCore(
        DRPGBattlePreparedAction action,
        float amount,
        out bool exhausted,
        out string error)
    {
        exhausted = false;
        if (amount < 0f)
        {
            error = "Interactive posture cost cannot be negative.";
            return false;
        }
        if (!IsActiveAction(action) ||
            Phase != DRPGBattlePhase.InteractiveAction)
        {
            error = "No matching interactive action can spend posture.";
            return false;
        }

        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        if (actor == null || !actor.IsAlive)
        {
            error = "The interactive action actor is no longer valid.";
            return false;
        }

        actor.SpendPosture(amount);
        exhausted = actor.IsPostureBroken;
        PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
        error = string.Empty;
        return true;
    }

    public bool TryEndInteractiveResolution(
        DRPGBattlePreparedAction action,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        if (!TryOpenLegacyActionScope(action, out IDisposable scope, out error))
            return false;
        using (scope)
            return TryEndInteractiveResolutionCore(action, out resolution, out error);
    }

    private bool TryEndInteractiveResolutionCore(
        DRPGBattlePreparedAction action,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        if (!IsActiveAction(action) ||
            Phase != DRPGBattlePhase.InteractiveAction)
        {
            error = "No matching interactive action can be ended.";
            return false;
        }

        if (!action.ImpactLedger.IsComplete)
        {
            if (!action.ImpactLedger.TryBeginCommit(0))
            {
                error = "The interactive impact ledger cannot be finalized.";
                return false;
            }
            action.ImpactLedger.CompleteCommit(0);
        }
        resolution = new DRPGBattleActionResolution(
            action,
            activeHits.ToArray(),
            activeEffects.ToArray());
        activeResolution = resolution;
        activeFreeStrikeWindow = null;
        RequireFlowTransition(
            flow.TryCompleteImpact(action.ActionId, out string flowError),
            flowError);
        error = string.Empty;
        return true;
    }

    public bool TryCompleteAction(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (!IsActiveAction(action) || action.LegacyMutationPort == null)
        {
            error = "The prepared action has no matching Legacy mutation port.";
            return false;
        }
        return action.LegacyMutationPort.TryComplete(action, out error);
    }

    public bool TryCommitAction(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (!IsActiveAction(action) || action.LegacyMutationPort == null)
        {
            error = "The prepared action has no matching Legacy mutation port.";
            return false;
        }
        return action.LegacyMutationPort.TryCommit(action, out error);
    }

    internal bool TryCommitLegacyAction(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (action != null && !action.ImpactLedger.IsComplete)
        {
            error = "The prepared action still has pending impact partitions.";
            return false;
        }
        if (!IsActiveAction(action) ||
            activeResolution == null ||
            flow.ActionStage != DRPGBattleActionFlowStage.ImpactResolved ||
            pendingFinalizationAction != null)
        {
            error = "The prepared action has not resolved or is no longer active.";
            return false;
        }

        if (!flow.TryBeginCommit(action.ActionId, out error))
            return false;

        BattleParticipantRuntime actor = FindParticipant(action.ActorRuntimeId);
        DRPGBattleCommand command = action.Command;
        bool completedOrdinaryEnemyPlan =
            currentEnemyPlan != null &&
            actor != null &&
            currentEnemyPlan.ActorRuntimeId == actor.RuntimeId;
        bool completedPlayerResponse =
            actor != null &&
            actor.IsAlive &&
            actor.Side == DRPGBattleSide.Player &&
            IsPlayerResponseCommand(command.Kind);
        DRPGBattleWeaponActionCompletion weaponCompletion = null;
        if (weaponDurabilityService != null &&
            action.WeaponActionContext != null)
        {
            weaponCompletion = weaponDurabilityService.CompleteAction(
                actor,
                action.WeaponActionContext);
            if (!weaponCompletion.Success)
            {
                error = weaponCompletion.Message;
                return false;
            }
            if (weaponCompletion.ReplacementBuild != null && actor != null)
            {
                actor.ReplaceBuild(weaponCompletion.ReplacementBuild);
                PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
                PublishNotification(ParticipantBuildChanged, actor, nameof(ParticipantBuildChanged));
            }
        }
        if (command.Kind == DRPGBattleCommandKind.ResonanceSkill)
        {
            if (resonanceAccess == null)
            {
                error = "Resonance battle access is unavailable.";
                return false;
            }
            if (!resonanceAccess.TryConsume(
                    actor,
                    command.Skill,
                    participants,
                    out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "The committed resonance skill could not be consumed.";
                return false;
            }
        }
        if (command.Kind == DRPGBattleCommandKind.Skill &&
            command.Stage == DRPGBattleCommandStage.Telegraph)
            RegisterTelegraph(action);
        else if (command.Kind == DRPGBattleCommandKind.Skill &&
                 command.Stage == DRPGBattleCommandStage.Release)
            RemoveTelegraph(command.ActorRuntimeId, true);
        activeHits.Clear();
        activeEffects.Clear();

        if (weaponCompletion != null &&
            weaponCompletion.BrokeThisCompletion)
        {
            DRPGBattleWeaponBreakEvent breakEvent = new DRPGBattleWeaponBreakEvent(
                action.ActorRuntimeId,
                action.WeaponActionContext.WeaponInstanceId,
                weaponCompletion.Disposition);
            PublishNotification(WeaponBroken, breakEvent, nameof(WeaponBroken));
        }

        DRPGBattleOutcome committedOutcome =
            command.Kind == DRPGBattleCommandKind.Escape &&
            activeEscapeSucceeded
                ? DRPGBattleOutcome.Escaped
                : DRPGBattleOutcome.None;
        activeEscapeSucceeded = false;

        if (committedOutcome == DRPGBattleOutcome.None &&
            actor != null && actor.IsAlive)
        {
            DRPGPostureBreakTransition postureTransition =
                actor.CompletePostureBreakOwnAction();
            if (postureTransition.StateChanged)
                PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
        }

        if (committedOutcome == DRPGBattleOutcome.None)
        {
            NormalizeFormationRows(true);
            if (!wholeWaveEnabled) AdvanceReinforcements();
            committedOutcome = DetermineOutcome();

            if (committedOutcome == DRPGBattleOutcome.None)
            {
                if (completedOrdinaryEnemyPlan)
                {
                    RemoveEnemyPlan(actor.RuntimeId, true);
                    currentEnemyPlan = null;
                    currentEnemyPlanFizzled = false;
                }

                if (completedPlayerResponse)
                    ReleaseEnemyPlansAfterPlayerResponse();

                if (actor != null && actor.IsAlive)
                    ScheduleNextActor(actor, action.Spec);

                RemoveDefeatedFromTimeline(false);
            }
        }

        pendingFinalizationAction = action;
        pendingFinalizationOutcome = committedOutcome;
        activeAction = null;
        activeResolution = null;
        activeFreeStrikeWindow = null;
        RequireFlowTransition(
            flow.TryCompleteCommit(action.ActionId, out string flowError),
            flowError);
        error = string.Empty;
        return true;
    }

    public bool TryFinalizeCommittedAction(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (action == null || pendingFinalizationAction == null ||
            !ReferenceEquals(action, pendingFinalizationAction) ||
            action.Identity != pendingFinalizationAction.Identity ||
            action.LegacyMutationPort == null)
        {
            error = "The prepared action has no matching Legacy mutation port.";
            return false;
        }
        return action.LegacyMutationPort.TryFinalize(action, out error);
    }

    internal bool TryFinalizeLegacyAction(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (action == null || pendingFinalizationAction == null ||
            !ReferenceEquals(action, pendingFinalizationAction) ||
            action.ActionId != pendingFinalizationAction.ActionId)
        {
            error = "No matching committed action is awaiting finalization.";
            return false;
        }
        if (standardEnabled && !deliveringLegacyContinuation)
        {
            if (!transactionArbiter.TryCaptureCurrentLease(BattleTransactionIdentity.ForLegacyAction(action.Identity), out var lease))
            { error = "Legacy finalization lost its physical owner."; return false; }
            if (legacyInitialSnapshot == null)
            { error = "Legacy finalization lost its locked participant snapshot."; return false; }
            // This gate is reached only after the legacy coordinator closes its
            // action presentation. Never undeploy a lethal target at impact.
            var members = new List<DRPGBattleDepartureMember>();
            var draft = transactionState.CreateWorkspace();
            bool undeploy = false;
            foreach (var before in legacyInitialSnapshot.Participants)
            {
                if (!before.IsDeployed || before.CurrentHp <= 0) continue;
                var live = FindParticipant(before.Participant.Value);
                if (live == null || live.IsAlive) continue;
                members.Add(new DRPGBattleDepartureMember(before.Participant, before.Side));
                if (before.Side == DRPGBattleSide.Enemy && live.IsDeployed)
                { draft.For(before.Participant).SetDeployed(false); undeploy = true; }
            }
            if (undeploy && !transactionState.TryCommitJournal(lease, draft).Succeeded)
            { error = "Legacy post-presentation departure could not commit."; return false; }
            // Recovery uses the same completed-root clock as standard attacks.
            // It is staged before the immutable continuation revision is frozen,
            // so the legacy no-cut-in action cannot lose or double-credit it.
            AccrueStaminaAfterCompletedRoot(lease);
            bool wavePending = wholeWaveEnabled && pendingFinalizationOutcome == DRPGBattleOutcome.None &&
                RootActionCompletion.DetermineOutcome(transactionState.CaptureRuleSnapshot()) == DRPGBattleOutcomeCandidate.WavePending;
            var departures = new DRPGBattleDepartureJournal(lease.Owner, (ulong)action.ActionId, members);
            var completion = new LegacyActionCompletion(action.Identity, pendingFinalizationOutcome, transactionState.BattleRevision, departures, lease, wavePending);
            if (!turnEntryRunner.TryEnqueueLegacy(completion, out error)) return false;
            action.LegacyMutationPort.RetainForContinuation();
            return true;
        }
        if (!flow.TryBeginFinalization(action.ActionId, out error))
            return false;

        DRPGBattleOutcome finalizedOutcome = pendingFinalizationOutcome;
        pendingFinalizationAction = null;
        pendingFinalizationOutcome = DRPGBattleOutcome.None;
        legacyInitialSnapshot = null;
        PublishNotification(ActionCompleted, action, nameof(ActionCompleted));
        currentActionIdentity = default;

        if (finalizedOutcome != DRPGBattleOutcome.None)
        {
            pendingTimelineDeltas.Clear();
            pendingTimelinePriorities.Clear();
            pendingTelegraphs.Clear();
            currentEnemyPlan = null;
            currentEnemyPlanFizzled = false;
            ClearEnemyPlans(true);
            Outcome = finalizedOutcome;
            CurrentActor = null;
            currentActorTurnToken = default;
            RequireFlowTransition(
                flow.TryCompleteFinalizationToResult(
                    action.ActionId,
                    out string flowError),
                flowError);
            DRPGBattleResult result = new DRPGBattleResult(
                Outcome,
                scheduler.CurrentTime);
            PublishNotification(BattleEnded, result, nameof(BattleEnded));
            error = string.Empty;
            return true;
        }

        RequireFlowTransition(
            flow.TryCompleteFinalizationToNextTurn(
                action.ActionId,
                out string nextTurnError),
            nextTurnError);
        if (!standardEnabled) BeginNextTurn();
        error = string.Empty;
        return true;
    }

    internal bool TryDeliverLegacyContinuation(in LegacyActionCompletion completion, out string error)
    {
        error = string.Empty;
        if (pendingFinalizationAction == null || pendingFinalizationAction.Identity != completion.Identity ||
            pendingFinalizationOutcome != completion.Outcome || transactionState.BattleRevision != completion.FinalBattleRevision ||
            !transactionState.OwnsRuleMutationLease(completion.Lease, completion.Owner))
        { error = "Legacy continuation no longer matches the sealed completion."; return false; }
        deliveringLegacyContinuation = true;
        try
        {
            using (transactionState.OpenCompatibilityMutationScope(completion.Lease))
                return TryFinalizeLegacyAction(pendingFinalizationAction, out error);
        }
        finally { deliveringLegacyContinuation = false; }
    }

    public bool TryCreateEnemyCommand(out DRPGBattleCommand command)
    {
        command = null;
        BattleParticipantRuntime actor = CurrentActor;
        if (actor == null || !actor.IsAlive || actor.Side != DRPGBattleSide.Enemy)
            return false;

        if (IsCurrentEnemyCommandFromOrdinaryIntent)
            return TryMaterializeCurrentEnemyPlanCommand(actor, out command);

        DRPGTelegraphedAction pending = FindTelegraph(actor.RuntimeId);
        if (pending != null)
        {
            command = new DRPGBattleCommand(
                pending.ActorRuntimeId,
                pending.TargetRuntimeId,
                pending.Skill,
                DRPGBattleCommandStage.Release);
            return true;
        }

        if (TryCreatePreferredReposition(actor, out command))
            return true;

        if (TryCreateEnemyAttack(actor, out command))
            return true;

        if (TryCreateTacticalReposition(actor, out command))
            return true;

        command = DRPGBattleCommand.Wait(actor.RuntimeId);
        return true;
    }

    public void EvaluateOutcome()
    {
        DRPGBattleOutcome nextOutcome = DetermineOutcome();
        if (nextOutcome == DRPGBattleOutcome.None ||
            Outcome != DRPGBattleOutcome.None)
        {
            return;
        }

        // Domain probes and pre-battle composition checks may ask the session
        // to evaluate a roster before Start().  Preserve that snapshot-style
        // query without pretending that a live battle reached its Result
        // state or publishing BattleEnded.  Start() resets this provisional
        // value before opening the real lifecycle.
        if (flow.LifecycleState == DRPGBattleLifecycleState.Inactive)
        {
            Outcome = nextOutcome;
            return;
        }

        CompleteBattle(nextOutcome);
    }

    private DRPGBattleOutcome DetermineOutcome()
    {
        bool playerAlive = FirstLivingBattlefieldMember() != null;
        bool enemyAlive =
            FirstLivingParticipant(DRPGBattleSide.Enemy) != null ||
            reserveEnemies.Count > 0;

        // A simultaneous wipe has the same priority as Root/TurnEntry
        // finalization; an empty enemy roster cannot turn party defeat into victory.
        if (!playerAlive)
            return DRPGBattleOutcome.Defeat;
        return !enemyAlive
            ? DRPGBattleOutcome.Victory
            : DRPGBattleOutcome.None;
    }

    public bool TryForceDevelopmentOutcome(
        DRPGBattleOutcome forcedOutcome,
        out string error)
    {
        if (forcedOutcome != DRPGBattleOutcome.Victory &&
            forcedOutcome != DRPGBattleOutcome.Defeat)
        {
            error = "Only victory or defeat can be forced by development tools.";
            return false;
        }

        if (!IsActive)
        {
            error = "No active battle is available.";
            return false;
        }

        CompleteBattle(forcedOutcome);
        error = string.Empty;
        return true;
    }

    public void Abort()
    {
        if (LastBattleFault.HasValue || standardReceipt != null && standardReceipt.Completion.IsCompleted &&
            standardReceipt.Completion.Result.Kind == RootActionTerminalKind.Faulted) return;
        if (Outcome != DRPGBattleOutcome.None)
            return;

        if (publishingStandard || standardReceipt != null && !standardDelivered) { standardAbortRequested = true; return; }
        if (turnEntryRunner.RequestAbort()) return;

        CompleteBattle(DRPGBattleOutcome.Aborted);
    }

    internal void CompleteTurnFlowAbort(BattleRuleMutationLease lease)
    {
        if (lease == null || !transactionState.OwnsRuleMutationLease(lease, lease.Owner))
            throw new InvalidOperationException("Aborting entry requires its original active mutation lease.");
        using (transactionState.OpenCompatibilityMutationScope(lease)) CompleteBattle(DRPGBattleOutcome.Aborted);
        lease.Dispose();
    }

    private void CompleteBattle(DRPGBattleOutcome outcome)
    {
        DRPGBattlePreparedAction actionToAbort = activeAction;
        DRPGBattlePreparedAction pendingActionToAbort = pendingFinalizationAction;
        activeAction?.ResourceTransaction.Cancel();
        actionToAbort?.LegacyMutationPort?.AbortAndRelease();
        if (!ReferenceEquals(pendingActionToAbort, actionToAbort))
            pendingActionToAbort?.LegacyMutationPort?.AbortAndRelease();
        activeAction = null;
        activeResolution = null;
        pendingFinalizationAction = null;
        pendingFinalizationOutcome = DRPGBattleOutcome.None;
        LastRootEventBatch = null;
        LastReactionRun = null;
        LastProgressRun = null;
        activeFreeStrikeWindow = null;
        activeEscapeSucceeded = false;
        activeHits.Clear();
        activeEffects.Clear();
        pendingTimelineDeltas.Clear();
        pendingTimelinePriorities.Clear();
        pendingTelegraphs.Clear();
        currentEnemyPlan = null;
        currentEnemyPlanFizzled = false;
        ClearEnemyPlans(true);
        Outcome = outcome;
        basicAttackSelectionState.SetBattleAvailable(false);
        RequireFlowTransition(
            flow.TryEnterResult(out string flowError),
            flowError);
        CurrentActor = null;
        currentActorTurnToken = default;
        activeLegacyBasicAttackSelection = default;
        basicAttackSelectionState.ClearTurn();
        currentActionIdentity = default;
        DRPGBattleResult result = new DRPGBattleResult(
            Outcome,
            scheduler.CurrentTime);
        PublishNotification(BattleEnded, result, nameof(BattleEnded));
    }

    public void RemoveDefeatedFromTimeline()
    {
        ParticipantId candidate = CurrentActor != null
            ? new ParticipantId(CurrentActor.RuntimeId)
            : new ParticipantId(participants[0].RuntimeId);
        TurnEntryIdentity identity =
            transactionIdentityAuthority.AllocateTurnEntry(candidate);
        if (!transactionArbiter.TryAcquire(
                BattleTransactionIdentity.ForTurnEntry(identity),
                out BattleRuleMutationLease lease,
                out string error))
        {
            throw new InvalidOperationException(error);
        }
        try
        {
            using (transactionState.OpenCompatibilityMutationScope(lease))
                RemoveDefeatedFromTimeline(true);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private void RemoveDefeatedFromTimeline(bool publishChange)
    {
        bool plansChanged = false;
        for (int i = 0; i < participants.Count; i++)
        {
            if (!participants[i].IsAlive)
            {
                scheduler.RemoveParticipant(participants[i].RuntimeId);
                plansChanged |= RemoveEnemyPlan(
                    participants[i].RuntimeId,
                    false);
            }
        }
        if (plansChanged)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
        if (publishChange)
            PublishTimelineChanged();
    }

    private void BeginNextTurn()
    {
        if (flow.LifecycleState != DRPGBattleLifecycleState.TurnBegin)
        {
            throw new InvalidOperationException(
                $"Turn selection cannot run from {flow.LifecycleState}/" +
                $"{flow.ActionStage}.");
        }

        CurrentActor = null;
        currentActorTurnToken = default;
        activeLegacyBasicAttackSelection = default;
        basicAttackSelectionState.ClearTurn();
        while (Outcome == DRPGBattleOutcome.None)
        {
            RemoveInvalidEnemyPlans();
            if (FirstLivingParticipant(DRPGBattleSide.Enemy) == null &&
                reserveEnemies.Count > 0)
            {
                DeployNextReinforcement(true);
            }
            EvaluateOutcome();
            if (Outcome != DRPGBattleOutcome.None)
                return;

            if (!transactionArbiter.TryCaptureCurrentLease(transactionArbiter.CurrentOwner, out var selectionLease))
                throw new InvalidOperationException("Turn selection lost its mutation owner.");
            if (!TryReserveActionableTurnCandidate(selectionLease,
                    out DRPGTurnCandidateReservation reservation,
                    out string reservationError))
            {
                Abort();
                return;
            }

            BattleParticipantRuntime nextActor =
                reservation.Candidate.Participant;
            if (nextActor == null || !nextActor.IsAlive ||
                !nextActor.IsDeployed)
            {
                if (reservation.SourceKind ==
                    DRPGTurnCandidateSourceKind.ExecutableEnemyPlan)
                {
                    RemoveEnemyPlan(nextActor != null
                        ? nextActor.RuntimeId
                        : reservation.Candidate.Participant.RuntimeId, true);
                }
                else if (nextActor != null)
                {
                    scheduler.RemoveParticipant(nextActor.RuntimeId);
                }
                if (!TryDiscardTurnCandidate(
                        reservation,
                        out reservationError))
                {
                    throw new InvalidOperationException(reservationError);
                }
                continue;
            }

            if (!TryCommitTurnCandidate(
                    reservation,
                    out DRPGEnemyActionPlan executablePlan,
                    out reservationError))
            {
                throw new InvalidOperationException(reservationError);
            }

            if (reservation.SourceKind ==
                DRPGTurnCandidateSourceKind.ExecutableEnemyPlan)
            {
                if (TryBeginParticipantTurn(nextActor, executablePlan))
                    return;
                continue;
            }

            if (TryBeginParticipantTurn(nextActor, null))
                return;
        }
    }

    // Receives only the State-owned terminal, never a caller-supplied ready flag.
    internal bool TryPublishTurnEntryResult(in DRPGTurnEntrySettlementCompletion completion,
        BattleRuleMutationLease lease, out string error)
    {
        error = string.Empty;
        if (flow.LifecycleState != DRPGBattleLifecycleState.TurnBegin || Outcome != DRPGBattleOutcome.None ||
            activeAction != null || pendingFinalizationAction != null ||
            completion.Kind != DRPGTurnEntrySettlementCompletionKind.BattleResult || completion.Turn.IsValid ||
            (completion.Outcome != DRPGBattleOutcome.Defeat && completion.Outcome != DRPGBattleOutcome.Victory) ||
            !transactionState.TurnCandidates.TryTakeTerminalCompletion(completion, lease, transactionIdentityAuthority))
        { error = "The result is not the current State-owned TurnEntry terminal."; return false; }
        using (transactionState.OpenCompatibilityMutationScope(lease))
            CompleteBattle(completion.Outcome);
        lease.Dispose();
        return true;
    }

    // Receives only the State-owned terminal, never a caller-supplied ready flag.
    // TurnStart rules and status feedback have already finished in the module.
    internal bool TryPublishSettledTurnEntry(in DRPGTurnEntrySettlementCompletion completion,
        BattleRuleMutationLease lease, out string error)
    {
        error = string.Empty;
        if (flow.LifecycleState != DRPGBattleLifecycleState.TurnBegin ||
            Outcome != DRPGBattleOutcome.None || activeAction != null ||
            completion.Kind != DRPGTurnEntrySettlementCompletionKind.ActorStillEligible ||
            !completion.Turn.IsValid || completion.Owner.BattleGeneration != BattleGeneration ||
            completion.Turn.Actor != completion.Owner.TurnEntry.Actor)
        { error = "The Session is not awaiting this settled candidate."; return false; }
        var actor = FindParticipant(completion.Turn.Actor.Value);
        if (actor == null || !actor.IsAlive || !actor.IsDeployed ||
            !transactionState.TurnCandidates.TryTakeTerminalCompletion(completion, lease, transactionIdentityAuthority))
        { error = "The candidate terminal, rule revision or physical lease is stale."; return false; }

        RequireFlowTransition(flow.TryOpenCommandGate(out error), error);
        CurrentActor = actor;
        currentActorTurnToken = completion.Turn;
        activeLegacyBasicAttackSelection = default;
        currentEnemyPlan = null;
        currentEnemyPlanFizzled = false;
        if (actor.Side == DRPGBattleSide.Enemy && enemyPlans.TryGetValue(actor.RuntimeId, out var plan) &&
            plan.State == DRPGEnemyPlanState.Executing)
            currentEnemyPlan = plan;
        basicAttackSelectionState.SetBattleAvailable(true);
        basicAttackSelectionState.PublishTurn(currentActorTurnToken);
        DRPGBasicAttackTrace.RecordTurnBegin(transactionTrace, currentActorTurnToken, actor.Side,
            new ActionTracePosition((float)actor.FormationRow, actor.FormationSlot, 0f));

        // All notification observers still see the closed domain gate. They
        // cannot prepare an action on the terminal's publication stack.
        if (actor.Side == DRPGBattleSide.Player) SampleEnemyPlansAtPlayerTurnStart();
        if (currentEnemyPlan != null) PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
        PublishNotification(CurrentActorChanged, actor, nameof(CurrentActorChanged));
        PublishTimelineChanged();
        lease.Dispose();
        return true;
    }

    private bool TryBeginParticipantTurn(
        BattleParticipantRuntime actor,
        DRPGEnemyActionPlan plan)
    {
        if (actor == null)
            return false;

        currentActorTurnToken = default;
        CurrentActor = actor;
        currentEnemyPlan = null;
        currentEnemyPlanFizzled = false;
        if (plan != null)
        {
            currentEnemyPlan = plan.MarkExecuting();
            enemyPlans[actor.RuntimeId] = currentEnemyPlan;
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
        }

        actor.ClearDefendAtTurnStart();
        DRPGStatusTurnResult statusTick = actor.TickStatusesAtTurnStart();
        if (statusTick.Changed)
            PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
        if (!actor.IsAlive)
        {
            RemoveEnemyPlan(actor.RuntimeId, true);
            currentEnemyPlan = null;
            RemoveDefeatedFromTimeline(false);
            EvaluateOutcome();
            CurrentActor = null;
            currentActorTurnToken = default;
            basicAttackSelectionState.ClearTurn();
            return false;
        }

        DRPGPostureBreakTransition postureTurn =
            actor.ResolvePostureBreakOwnTurnStart();
        if (postureTurn.SkipAction)
        {
            PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
            PublishPostureBreakLifecycleEvent(
                DRPGBattleEventKind.PostureBreakTurnSkipped,
                actor.RuntimeId,
                "posture_break_skip");
            if (plan != null &&
                plan.Skill != null &&
                plan.Skill.PostureBreakInterruptionPolicy ==
                    DRPGPostureBreakInterruptionPolicy.Uninterruptible)
            {
                enemyPlans[actor.RuntimeId] =
                    currentEnemyPlan.ReturnToExecutableAfterForcedSkip();
                PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
            }
            else if (plan != null)
            {
                RemoveEnemyPlan(actor.RuntimeId, true);
            }
            currentEnemyPlan = null;
            currentEnemyPlanFizzled = false;
            if (plan == null ||
                plan.Skill == null ||
                plan.Skill.PostureBreakInterruptionPolicy !=
                    DRPGPostureBreakInterruptionPolicy.Uninterruptible)
            {
                ScheduleNextActor(actor, null);
            }
            PublishTimelineChanged();
            CurrentActor = null;
            currentActorTurnToken = default;
            basicAttackSelectionState.ClearTurn();
            return false;
        }

        if (postureTurn.RestorePostureRatio > 0f)
        {
            PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
            PublishPostureBreakLifecycleEvent(
                DRPGBattleEventKind.PostureBreakRecovered,
                actor.RuntimeId,
                "posture_break_recovered");
        }
        else if (actor.RecoverPostureAtTurnStart() > 0f)
            PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));

        if (actor.Side == DRPGBattleSide.Player)
            SampleEnemyPlansAtPlayerTurnStart();

        if (!legacyTurnEntryAdapter.TryIssue(
                actor,
                out currentActorTurnToken,
                out string tokenError))
        {
            throw new InvalidOperationException(tokenError);
        }
        basicAttackSelectionState.PublishTurn(currentActorTurnToken);
        DRPGBasicAttackTrace.RecordTurnBegin(
            transactionTrace,
            currentActorTurnToken,
            actor.Side,
            new ActionTracePosition(
                (float)actor.FormationRow,
                actor.FormationSlot,
                0f));

        RequireFlowTransition(
            flow.TryOpenCommandGate(out string flowError),
            flowError);
        PublishNotification(CurrentActorChanged, actor, nameof(CurrentActorChanged));
        PublishTimelineChanged();
        return true;
    }

    private void PlanInitialEnemyActions()
    {
        IReadOnlyList<DRPGTurnScheduleEntry> initialEntries =
            scheduler.GetTimelineSnapshot();
        bool changed = false;
        for (int i = 0; i < initialEntries.Count; i++)
        {
            DRPGTurnScheduleEntry entry = initialEntries[i];
            BattleParticipantRuntime actor = entry.Participant;
            if (actor.Side != DRPGBattleSide.Enemy)
                continue;

            changed |= TryScheduleOrdinaryEnemyPlan(
                actor,
                0f,
                entry.ScheduledTime,
                entry.Priority,
                false);
        }

        if (changed)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
    }

    private bool TryScheduleOrdinaryEnemyPlan(
        BattleParticipantRuntime actor,
        float plannedAtTime,
        float baseReadyTime,
        int priority,
        bool publish)
    {
        if (!TryCreateOrdinaryEnemyPlan(actor, plannedAtTime, baseReadyTime, null, out var plan)) return false;
        // Reserve the serial before ScheduleEnemyPlan publishes notifications,
        // preserving the original ordering for reentrant observers.
        nextEnemyPlanId++;
        return ScheduleEnemyPlan(actor, plan, priority, publish);
    }

    // Preparation uses the same ordinary AI rules as legacy scheduling, but
    // performs no plan-table/timeline writes or notifications.
    internal bool TryPrepareSkippedEnemyPlan(BattleParticipantRuntime actor, DRPGEnemyActionPlan previous,
        out DRPGEnemyActionPlan plan, out float readyAt, out int priority)
    {
        plan = null; readyAt = 0; priority = 0;
        if (actor == null || !participants.Contains(actor) || actor.Side != DRPGBattleSide.Enemy) return false;
        if (enemyPlans.TryGetValue(actor.RuntimeId, out var current) && !ReferenceEquals(current, previous)) return false;
        transactionState.TurnCandidates.CalculateNextSchedule(actor, null, out readyAt, out priority);
        if (TryCreateOrdinaryEnemyPlan(actor, scheduler.CurrentTime, readyAt, previous, out plan)) readyAt = plan.ReadyAtTime;
        return true;
    }

    internal bool CanCommitPreparedEnemyPlan(DRPGEnemyActionPlan plan) => plan == null || plan.PlanId == nextEnemyPlanId;
    internal void CommitPreparedEnemyPlanId(DRPGEnemyActionPlan plan)
    {
        if (!CanCommitPreparedEnemyPlan(plan)) throw new InvalidOperationException("Prepared enemy plan identity became stale.");
        if (plan != null) nextEnemyPlanId++;
    }

    private bool TryCreateOrdinaryEnemyPlan(BattleParticipantRuntime actor, float plannedAtTime,
        float baseReadyTime, DRPGEnemyActionPlan replacing, out DRPGEnemyActionPlan plan)
    {
        plan = null;
        if (actor == null || actor.Side != DRPGBattleSide.Enemy ||
            !actor.IsAlive || !actor.IsDeployed ||
            FindTelegraph(actor.RuntimeId) != null ||
            enemyPlans.TryGetValue(actor.RuntimeId, out var existing) && !ReferenceEquals(existing, replacing))
        {
            return false;
        }

        if (!AcceptanceOptions.Enabled && TryCreatePreferredReposition(
                actor,
                out DRPGBattleCommand preferredReposition))
        {
            DRPGEnemyActionPlan repositionPlan =
                DRPGEnemyActionPlan.CreateReposition(
                    nextEnemyPlanId,
                    actor.RuntimeId,
                    plannedAtTime,
                    Math.Max(scheduler.CurrentTime, baseReadyTime),
                    preferredReposition.DestinationFormationRow.Value);
            plan = repositionPlan;
            return true;
        }

        DRPGEnemyActionSelection selection = AcceptanceOptions.Enabled
            ? acceptanceIntentSource.Select(actor,
                skill => CanAffordSkill(actor, skill, DRPGBattleCommandStage.Immediate, out _),
                skill => HasOrdinaryEnemyTarget(actor, skill))
            : enemyActionPlanner.SelectNextAction(
                actor,
                skill => CanAffordSkill(
                    actor,
                    skill,
                    DRPGBattleCommandStage.Immediate,
                    out _),
                skill => HasOrdinaryEnemyTarget(actor, skill),
                skill => SelectEnemySupportTargetRuntimeId(actor, skill));
        if (AcceptanceOptions.Enabled && selection.Path != DRPGEnemyActionSelectionPath.OrdinaryStandardAttack)
            throw new InvalidOperationException("BasicAttackOnly has no legal standard basic attack for actor " + actor.RuntimeId + ".");
        if (selection.Path == DRPGEnemyActionSelectionPath.Legacy)
        {
            return false;
        }

        if (selection.Path ==
            DRPGEnemyActionSelectionPath.OrdinaryStandardAttack)
        {
            float preparationDelay = scheduler.CalculatePreparationDelay(
                actor,
                selection.Skill.PrepareDelay);
            float readyAtTime = Math.Max(
                scheduler.CurrentTime,
                baseReadyTime + preparationDelay);
            DRPGEnemyActionPlan attackPlan =
                DRPGEnemyActionPlan.CreateAttack(
                    nextEnemyPlanId,
                    actor.RuntimeId,
                    selection.Skill,
                    plannedAtTime,
                    readyAtTime,
                    selection.Skill.EnemyWarningSteps);
            plan = attackPlan;
            return true;
        }

        if (selection.Path ==
            DRPGEnemyActionSelectionPath.OrdinaryStandardSupport)
        {
            float preparationDelay = scheduler.CalculatePreparationDelay(
                actor,
                selection.Skill.PrepareDelay);
            float readyAtTime = Math.Max(
                scheduler.CurrentTime,
                baseReadyTime + preparationDelay);
            DRPGEnemyActionPlan supportPlan =
                DRPGEnemyActionPlan.CreateSupportSkill(
                    nextEnemyPlanId,
                    actor.RuntimeId,
                    selection.Skill,
                    selection.TargetRuntimeId,
                    plannedAtTime,
                    readyAtTime);
            plan = supportPlan;
            return true;
        }

        if (TryCreateTacticalReposition(
                actor,
                out DRPGBattleCommand tacticalReposition))
        {
            DRPGEnemyActionPlan repositionPlan =
                DRPGEnemyActionPlan.CreateReposition(
                    nextEnemyPlanId,
                    actor.RuntimeId,
                    plannedAtTime,
                    Math.Max(scheduler.CurrentTime, baseReadyTime),
                    tacticalReposition.DestinationFormationRow.Value);
            plan = repositionPlan;
            return true;
        }

        DRPGEnemyActionPlan waitPlan = DRPGEnemyActionPlan.CreateWait(
            nextEnemyPlanId,
            actor.RuntimeId,
            plannedAtTime,
            Math.Max(scheduler.CurrentTime, baseReadyTime));
        plan = waitPlan;
        return true;
    }

    private bool ScheduleEnemyPlan(
        BattleParticipantRuntime actor,
        DRPGEnemyActionPlan plan,
        int priority,
        bool publish)
    {
        if (actor == null || plan == null ||
            plan.ActorRuntimeId != actor.RuntimeId ||
            enemyPlans.ContainsKey(actor.RuntimeId))
        {
            return false;
        }

        enemyPlans.Add(actor.RuntimeId, plan);
        scheduler.ScheduleAt(actor, plan.ReadyAtTime, priority);
        if (publish)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
        return true;
    }

    private void CommitDueEnemyPlansAtCurrentTime()
    {
        IReadOnlyList<DRPGTurnScheduleEntry> entries =
            scheduler.GetTimelineSnapshot();
        for (int i = 0; i < entries.Count; i++)
        {
            DRPGTurnScheduleEntry entry = entries[i];
            if (entry.ScheduledTime > scheduler.CurrentTime + 0.0001f)
                break;
            if (entry.Participant.Side != DRPGBattleSide.Enemy ||
                !enemyPlans.TryGetValue(
                    entry.Participant.RuntimeId,
                    out DRPGEnemyActionPlan plan) ||
                plan.State != DRPGEnemyPlanState.Windup &&
                plan.State != DRPGEnemyPlanState.Scheduled)
            {
                continue;
            }

            scheduler.RemoveParticipant(entry.Participant.RuntimeId);
            if (plan.State == DRPGEnemyPlanState.Windup)
                CommitReadyEnemyPlan(plan);
            else
                PromoteScheduledEnemyPlan(plan);
        }
    }

    private void PromoteScheduledEnemyPlan(DRPGEnemyActionPlan plan)
    {
        if (plan == null || plan.State != DRPGEnemyPlanState.Scheduled)
            return;

        BattleParticipantRuntime actor = FindParticipant(plan.ActorRuntimeId);
        if (actor == null || !actor.IsAlive || !actor.IsDeployed)
        {
            RemoveEnemyPlan(plan.ActorRuntimeId, true);
            return;
        }

        enemyPlans[actor.RuntimeId] = plan.MarkReady();
        PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
    }

    private void CommitReadyEnemyPlan(DRPGEnemyActionPlan plan)
    {
        if (plan == null || plan.State != DRPGEnemyPlanState.Windup)
            return;

        BattleParticipantRuntime actor = FindParticipant(plan.ActorRuntimeId);
        if (actor == null || !actor.IsAlive || !actor.IsDeployed)
        {
            RemoveEnemyPlan(plan.ActorRuntimeId, true);
            return;
        }

        DRPGEnemyActionPlan sampled = plan.WithSampledWarning(
            scheduler.CurrentTime);
        BattleParticipantRuntime target = SelectEnemyTarget(
            actor,
            sampled.Skill);
        DRPGEnemyActionPlan committed = target != null
            ? sampled.CommitTarget(
                target.RuntimeId,
                completedPlayerActionSerial)
            : sampled.ConvertToWait();
        enemyPlans[actor.RuntimeId] = committed;
        PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));

        DRPGEnemyActionPlanSnapshot snapshot =
            committed.CreatePublicSnapshot();
        if (snapshot.DisclosedTargetRuntimeId > 0)
            PublishNotification(EnemyTargetDisclosureChanged, snapshot, nameof(EnemyTargetDisclosureChanged));
    }

    private void SampleEnemyPlansAtPlayerTurnStart()
    {
        enemyPlanBuffer.Clear();
        foreach (DRPGEnemyActionPlan plan in enemyPlans.Values)
        {
            if (plan.State == DRPGEnemyPlanState.Windup)
                enemyPlanBuffer.Add(plan);
        }

        bool changed = false;
        for (int i = 0; i < enemyPlanBuffer.Count; i++)
        {
            DRPGEnemyActionPlan before = enemyPlanBuffer[i];
            DRPGEnemyActionPlan after = before.WithSampledWarning(
                scheduler.CurrentTime);
            if (after.CurrentWarningValue == before.CurrentWarningValue)
                continue;
            enemyPlans[after.ActorRuntimeId] = after;
            changed = true;
        }
        enemyPlanBuffer.Clear();
        if (changed)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
    }

    private void ReleaseEnemyPlansAfterPlayerResponse()
    {
        completedPlayerActionSerial++;
        enemyPlanBuffer.Clear();
        foreach (DRPGEnemyActionPlan plan in enemyPlans.Values)
        {
            if (plan.State == DRPGEnemyPlanState.AwaitingPlayerResponse)
                enemyPlanBuffer.Add(plan);
        }

        bool changed = false;
        for (int i = 0; i < enemyPlanBuffer.Count; i++)
        {
            DRPGEnemyActionPlan before = enemyPlanBuffer[i];
            DRPGEnemyActionPlan after = before.WithSatisfiedPlayerResponse(
                completedPlayerActionSerial);
            if (ReferenceEquals(before, after))
                continue;
            enemyPlans[after.ActorRuntimeId] = after;
            changed = true;
        }
        enemyPlanBuffer.Clear();
        if (changed)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
    }

    internal bool TryReserveNextTurnCandidate(out DRPGTurnCandidateReservation reservation, out string error)
        => transactionState.TurnCandidates.TryReserve(out reservation, out error);

    // Shared by legacy startup and transaction continuations. Enemy preparation
    // rows are scheduler events, not actor turns: never run their TurnStart here.
    internal bool TryReserveActionableTurnCandidate(BattleRuleMutationLease lease,
        out DRPGTurnCandidateReservation reservation, out string error)
    {
        reservation = default;
        error = string.Empty;
        if (flow.LifecycleState != DRPGBattleLifecycleState.TurnBegin || lease == null ||
            !transactionState.OwnsRuleMutationLease(lease, lease.Owner))
        { error = "Candidate preparation requires the current turn-begin owner."; return false; }
        using (transactionState.OpenCompatibilityMutationScope(lease))
        {
            // Each iteration removes a dead row, promotes one preparation row,
            // or revisits a player after same-time preparation. No unbounded AI loop.
            int budget = participants.Count * 3 + 1;
            while (budget-- > 0)
            {
                if (!TryReserveNextTurnCandidate(out var candidate, out error)) return false;
                var actor = candidate.Candidate.Participant;
                if (actor == null || !actor.IsAlive || !actor.IsDeployed)
                {
                    if (actor != null)
                    {
                        RemoveEnemyPlan(actor.RuntimeId, true);
                        scheduler.RemoveParticipant(actor.RuntimeId);
                    }
                    if (!TryDiscardTurnCandidate(candidate, out error)) return false;
                    continue;
                }
                if (candidate.SourceKind == DRPGTurnCandidateSourceKind.ExecutableEnemyPlan)
                { reservation = candidate; return true; }

                if (actor.Side == DRPGBattleSide.Enemy && enemyPlans.TryGetValue(actor.RuntimeId, out var plan) &&
                    (plan.State == DRPGEnemyPlanState.Windup || plan.State == DRPGEnemyPlanState.Scheduled))
                {
                    if (!TryCommitTurnCandidate(candidate, out _, out error)) return false;
                    if (plan.State == DRPGEnemyPlanState.Windup) CommitReadyEnemyPlan(plan);
                    else PromoteScheduledEnemyPlan(plan);
                    continue;
                }
                bool hasDuePreparation = false;
                if (actor.Side == DRPGBattleSide.Player)
                {
                    foreach (var row in scheduler.GetTimelineSnapshot())
                    {
                        if (row.ScheduledTime > candidate.Candidate.ScheduledTime + .0001f) break;
                        if (row.Participant.Side == DRPGBattleSide.Enemy && enemyPlans.TryGetValue(row.Participant.RuntimeId, out var due) &&
                            (due.State == DRPGEnemyPlanState.Windup || due.State == DRPGEnemyPlanState.Scheduled))
                        { hasDuePreparation = true; break; }
                    }
                }
                if (hasDuePreparation)
                {
                    if (!TryCommitTurnCandidate(candidate, out _, out error)) return false;
                    CommitDueEnemyPlansAtCurrentTime();
                    scheduler.ScheduleAt(actor, scheduler.CurrentTime, int.MinValue);
                    continue;
                }
                reservation = candidate;
                return true;
            }
        }
        error = "Candidate preparation exceeded the finite roster event budget.";
        return false;
    }

    internal bool TryCommitTurnCandidate(DRPGTurnCandidateReservation reservation, out DRPGEnemyActionPlan enemyPlan, out string error)
        => transactionState.TurnCandidates.TryCommitLegacy(reservation, out enemyPlan, out error);

    internal bool TryDiscardTurnCandidate(DRPGTurnCandidateReservation reservation, out string error)
        => transactionState.TurnCandidates.TryDiscardLegacy(reservation, out error);

    private IReadOnlyList<DRPGEnemyActionPlanSnapshot>
        CreateEnemyPlanSnapshots(bool readyOnly)
    {
        enemyPlanBuffer.Clear();
        foreach (DRPGEnemyActionPlan plan in enemyPlans.Values)
        {
            if (!readyOnly ||
                plan.State == DRPGEnemyPlanState.AwaitingPlayerResponse ||
                plan.State == DRPGEnemyPlanState.Executable ||
                plan.State == DRPGEnemyPlanState.Executing)
            {
                enemyPlanBuffer.Add(plan);
            }
        }
        enemyPlanBuffer.Sort(CompareEnemyPlans);
        var snapshots = new DRPGEnemyActionPlanSnapshot[
            enemyPlanBuffer.Count];
        for (int i = 0; i < enemyPlanBuffer.Count; i++)
            snapshots[i] = enemyPlanBuffer[i].CreatePublicSnapshot();
        enemyPlanBuffer.Clear();
        return snapshots;
    }

    private int CompareEnemyPlans(
        DRPGEnemyActionPlan left,
        DRPGEnemyActionPlan right)
    {
        int result = left.ReadyAtTime.CompareTo(right.ReadyAtTime);
        if (result != 0)
            return result;

        BattleParticipantRuntime leftActor = FindParticipant(
            left.ActorRuntimeId);
        BattleParticipantRuntime rightActor = FindParticipant(
            right.ActorRuntimeId);
        int leftSlot = leftActor != null
            ? leftActor.FormationSlot
            : int.MaxValue;
        int rightSlot = rightActor != null
            ? rightActor.FormationSlot
            : int.MaxValue;
        result = leftSlot.CompareTo(rightSlot);
        return result != 0
            ? result
            : left.ActorRuntimeId.CompareTo(right.ActorRuntimeId);
    }

    private bool TryMaterializeCurrentEnemyPlanCommand(
        BattleParticipantRuntime actor,
        out DRPGBattleCommand command)
    {
        command = null;
        if (currentEnemyPlan == null ||
            currentEnemyPlan.ActorRuntimeId != actor.RuntimeId)
        {
            return false;
        }

        if (currentEnemyPlan.ActionKind ==
            DRPGEnemyPlannedActionKind.Wait)
        {
            command = DRPGBattleCommand.Wait(actor.RuntimeId);
            return true;
        }

        if (currentEnemyPlan.ActionKind ==
            DRPGEnemyPlannedActionKind.Reposition &&
            currentEnemyPlan.DestinationFormationRow.HasValue)
        {
            // The forward row may have emptied since this plan was published.
            // Consume the stale plan, just like an invalid committed attack,
            // rather than retrying the rejected reposition every frame.
            if (!CanReposition(actor, currentEnemyPlan.DestinationFormationRow.Value, out _))
            {
                command = DRPGBattleCommand.Wait(actor.RuntimeId);
                return true;
            }
            command = DRPGBattleCommand.Reposition(
                actor.RuntimeId,
                currentEnemyPlan.DestinationFormationRow.Value);
            return true;
        }

        if (currentEnemyPlan.ActionKind ==
            DRPGEnemyPlannedActionKind.SupportSkill)
        {
            BattleParticipantRuntime supportTarget = FindParticipant(
                currentEnemyPlan.CommittedTargetRuntimeId);
            if (supportTarget == null || !supportTarget.IsAlive ||
                !supportTarget.IsDeployed ||
                !CanTargetWithSkill(
                    actor,
                    supportTarget,
                    currentEnemyPlan.Skill))
            {
                command = DRPGBattleCommand.Wait(actor.RuntimeId);
                return true;
            }

            command = DRPGBattleCommand.UseSkill(
                actor.RuntimeId,
                supportTarget.RuntimeId,
                currentEnemyPlan.Skill);
            return true;
        }

        BattleParticipantRuntime target = FindParticipant(
            currentEnemyPlan.CommittedTargetRuntimeId);
        if (target == null || !target.IsAlive || !target.IsDeployed ||
            !CanTargetWithSkill(actor, target, currentEnemyPlan.Skill))
        {
            command = DRPGBattleCommand.Wait(actor.RuntimeId);
            if (!currentEnemyPlanFizzled)
            {
                currentEnemyPlanFizzled = true;
                DRPGEnemyActionPlanSnapshot snapshot =
                    currentEnemyPlan.CreatePublicSnapshot();
                PublishNotification(EnemyPlanFizzled, snapshot, nameof(EnemyPlanFizzled));
            }
            return true;
        }

        command = ReferenceEquals(actor.Build.BasicAttack, currentEnemyPlan.Skill)
            ? DRPGBattleCommand.BasicAttack(
                actor.RuntimeId,
                target.RuntimeId,
                currentEnemyPlan.Skill)
            : DRPGBattleCommand.UseSkill(
                actor.RuntimeId,
                target.RuntimeId,
                currentEnemyPlan.Skill);
        return true;
    }

    private bool HasOrdinaryEnemyTarget(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime candidate = participants[i];
            if (candidate.IsOrdinaryEnemyTarget &&
                CanTargetWithSkill(actor, candidate, skill))
            {
                return true;
            }
        }
        return false;
    }

    private int SelectEnemySupportTargetRuntimeId(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill)
    {
        if (actor == null || skill == null)
            return 0;

        BattleParticipantRuntime selected = null;
        float greatestMissingHp = 0f;
        bool requiresMissingHp = skill.Purpose == DRPGSkillPurpose.Healing;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime candidate = participants[i];
            if (!CanTargetWithSkill(actor, candidate, skill))
                continue;

            if (!requiresMissingHp)
                return candidate.RuntimeId;

            float missingHp = Math.Max(
                0f,
                candidate.EffectiveMaxHp - candidate.CurrentHp);
            if (missingHp <= greatestMissingHp + 0.0001f)
                continue;
            greatestMissingHp = missingHp;
            selected = candidate;
        }

        return selected != null ? selected.RuntimeId : 0;
    }

    private BattleParticipantRuntime SelectEnemyTarget(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill)
    {
        enemyTargetCandidates.Clear();
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime candidate = participants[i];
            if (candidate.IsOrdinaryEnemyTarget &&
                CanTargetWithSkill(actor, candidate, skill))
            {
                enemyTargetCandidates.Add(candidate);
            }
        }
        if (enemyTargetCandidates.Count == 0)
            return null;

        DRPGPostureBreakTargetPolicy.ApplyNewSelectionPolicy(
            skill,
            enemyTargetCandidates);

        int selectedIndex = randomSource.Next(enemyTargetCandidates.Count);
        if (selectedIndex < 0 || selectedIndex >= enemyTargetCandidates.Count)
        {
            throw new InvalidOperationException(
                "The battle random source returned an out-of-range target index.");
        }
        return enemyTargetCandidates[selectedIndex];
    }

    private static bool IsPlayerResponseCommand(DRPGBattleCommandKind kind)
    {
        switch (kind)
        {
            case DRPGBattleCommandKind.BasicAttack:
            case DRPGBattleCommandKind.Skill:
            case DRPGBattleCommandKind.Item:
            case DRPGBattleCommandKind.Defend:
            case DRPGBattleCommandKind.Escape:
            case DRPGBattleCommandKind.Wait:
            case DRPGBattleCommandKind.Reposition:
            case DRPGBattleCommandKind.SwitchWeapon:
            case DRPGBattleCommandKind.ResonanceSkill:
                return true;
            default:
                return false;
        }
    }

    private bool RemoveEnemyPlan(int actorRuntimeId, bool publish)
    {
        if (!enemyPlans.Remove(actorRuntimeId))
            return false;
        if (currentEnemyPlan != null &&
            currentEnemyPlan.ActorRuntimeId == actorRuntimeId)
        {
            currentEnemyPlan = null;
            currentEnemyPlanFizzled = false;
        }
        if (publish)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
        return true;
    }

    private bool TryInterruptEnemyPlanForPostureBreak(
        BattleParticipantRuntime actor)
    {
        if (actor == null ||
            !enemyPlans.TryGetValue(actor.RuntimeId, out DRPGEnemyActionPlan plan) ||
            !plan.CanBeInterruptedByPostureBreak)
        {
            return false;
        }

        bool hasScheduledOpportunity = HasScheduledOpportunity(actor.RuntimeId);
        RemoveEnemyPlan(actor.RuntimeId, false);
        if (!hasScheduledOpportunity && actor.IsAlive && actor.IsDeployed)
        {
            scheduler.ScheduleAt(
                actor,
                Math.Max(scheduler.CurrentTime, plan.ReadyAtTime),
                int.MinValue);
        }
        PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
        PublishTimelineChanged();
        PublishPostureBreakLifecycleEvent(
            DRPGBattleEventKind.PreparedActionInterrupted,
            actor.RuntimeId,
            "enemy_plan:" + plan.PlanId);
        return true;
    }

    private bool HasScheduledOpportunity(int actorRuntimeId)
    {
        IReadOnlyList<DRPGTurnScheduleEntry> entries =
            scheduler.GetTimelineSnapshot();
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Participant != null &&
                entries[i].Participant.RuntimeId == actorRuntimeId)
            {
                return true;
            }
        }
        return false;
    }

    private void PublishPostureBreakLifecycleEvent(
        DRPGBattleEventKind kind,
        int actorRuntimeId,
        string semanticId)
    {
        DRPGBattleEvent battleEvent = eventFactory.CreateLifecycleEvent(
            kind,
            actorRuntimeId,
            semanticId);
        PublishNotification(BattleEventPublished, battleEvent, nameof(BattleEventPublished));
    }

    private void RemoveInvalidEnemyPlans()
    {
        enemyPlanBuffer.Clear();
        foreach (DRPGEnemyActionPlan plan in enemyPlans.Values)
        {
            BattleParticipantRuntime actor = FindParticipant(
                plan.ActorRuntimeId);
            if (actor == null || !actor.IsAlive || !actor.IsDeployed)
                enemyPlanBuffer.Add(plan);
        }

        bool changed = false;
        for (int i = 0; i < enemyPlanBuffer.Count; i++)
        {
            changed |= RemoveEnemyPlan(
                enemyPlanBuffer[i].ActorRuntimeId,
                false);
        }
        enemyPlanBuffer.Clear();
        if (changed)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
    }

    private void ClearEnemyPlans(bool publish)
    {
        if (enemyPlans.Count == 0)
            return;
        enemyPlans.Clear();
        enemyPlanBuffer.Clear();
        if (publish)
            PublishNotification(EnemyPlansChanged, nameof(EnemyPlansChanged));
    }

    private bool ValidateCommand(
        DRPGBattleCommand command,
        out BattleParticipantRuntime actor,
        out BattleParticipantRuntime target,
        out string error)
    {
        actor = null;
        target = null;
        error = string.Empty;

        if (!IsActive || CurrentActor == null)
        {
            error = "No actor is currently awaiting a command.";
            return false;
        }

        if (command == null)
        {
            error = "Command is missing.";
            return false;
        }

        actor = FindParticipant(command.ActorRuntimeId);
        target = command.TargetRuntimeId > 0
            ? FindParticipant(command.TargetRuntimeId)
            : null;
        if (!ReferenceEquals(actor, CurrentActor) || !actor.IsAlive)
        {
            error = "Command actor is not the current living actor.";
            return false;
        }

        if (command.Kind == DRPGBattleCommandKind.Defend)
        {
            if (command.Stage != DRPGBattleCommandStage.Immediate)
            {
                error = "Defend cannot use a staged attack command.";
                return false;
            }
            return true;
        }

        if (command.Kind == DRPGBattleCommandKind.SwitchWeapon)
        {
            if (command.Stage != DRPGBattleCommandStage.Immediate)
            {
                error = "Weapon switching cannot use a staged command.";
                return false;
            }
            if (equipmentService == null)
            {
                error = "Battle equipment switching is unavailable.";
                return false;
            }

            IReadOnlyList<DRPGBattleWeaponCandidate> candidates =
                equipmentService.GetCandidates(actor.RuntimeId) ??
                Array.Empty<DRPGBattleWeaponCandidate>();
            for (int i = 0; i < candidates.Count; i++)
            {
                DRPGBattleWeaponCandidate candidate = candidates[i];
                if (candidate == null ||
                    !string.Equals(
                        candidate.WeaponInstanceId,
                        command.WeaponInstanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (!candidate.IsEligible)
                {
                    error = string.IsNullOrWhiteSpace(candidate.RejectionReason)
                        ? "The selected weapon is not eligible."
                        : candidate.RejectionReason;
                    return false;
                }
                return true;
            }

            error = "The selected weapon is not a current switch candidate.";
            return false;
        }

        if (command.Kind == DRPGBattleCommandKind.Wait)
        {
            if (command.Stage != DRPGBattleCommandStage.Immediate)
            {
                error = "Wait cannot use a staged command.";
                return false;
            }
            return true;
        }

        if (command.Kind == DRPGBattleCommandKind.Reposition)
        {
            if (command.Stage != DRPGBattleCommandStage.Immediate ||
                !command.DestinationFormationRow.HasValue)
            {
                error = "Reposition requires one immediate destination row.";
                return false;
            }
            return CanReposition(
                actor,
                command.DestinationFormationRow.Value,
                out error);
        }

        if (command.Kind == DRPGBattleCommandKind.Escape)
        {
            if (actor.Side != DRPGBattleSide.Player || !escapeRules.Allowed)
            {
                error = "Escape is not allowed for this encounter.";
                return false;
            }
            return true;
        }

        if (command.Kind == DRPGBattleCommandKind.Item)
        {
            if (target == null || !target.IsAlive || target.Side != actor.Side)
            {
                error = "A battle item requires a living allied target.";
                return false;
            }
            if (itemSource == null ||
                itemSource.GetQuantity(command.ItemDefinition) <= 0)
            {
                error = "The selected battle item is unavailable.";
                return false;
            }
            return true;
        }

        if (command.Kind == DRPGBattleCommandKind.ResonanceSkill)
        {
            if (command.Stage != DRPGBattleCommandStage.Immediate)
            {
                error = "Resonance skills cannot use staged commands.";
                return false;
            }
            if (target == null || !target.IsAlive)
            {
                error = "A resonance skill requires a living target.";
                return false;
            }
            if (resonanceAccess == null)
            {
                error = "Resonance battle access is unavailable.";
                return false;
            }
            DRPGResonanceSkillAvailability availability =
                resonanceAccess.GetAvailability(actor, command.Skill, participants);
            if (!availability.IsAvailable)
            {
                error = availability.Reason;
                return false;
            }
            if (!CanTargetWithSkill(actor, target, command.Skill))
            {
                error = "The target is outside this resonance skill's formation range.";
                return false;
            }
            return CanAffordSkill(
                actor,
                command.Skill,
                command.Stage,
                out error);
        }

        bool isRelease = command.Stage == DRPGBattleCommandStage.Release;
        if (standardEnabled &&
            (command.Kind == DRPGBattleCommandKind.BasicAttack ||
             command.Kind == DRPGBattleCommandKind.Skill) &&
            !DRPGBattleSkillModePolicy.IsAllowed(command.Skill))
        {
            error = "Manual or telegraphed execution modes are unavailable in Battle V2.";
            return false;
        }
        if (target == null || !target.IsAlive && !isRelease)
        {
            error = "Command target must be a living participant.";
            return false;
        }

        bool skillAvailable;
        if (command.Kind == DRPGBattleCommandKind.BasicAttack)
        {
            skillAvailable = ReferenceEquals(
                actor.Build.BasicAttack,
                command.Skill);
        }
        else
        {
            skillAvailable = command.IsLegacySkillCompatibility
                ? IsSkillAvailable(actor, command.Skill)
                : actor.Build.ContainsBattleSkill(command.Skill.SkillId);
        }
        if (!skillAvailable)
        {
            error = "The selected skill is not available to the current actor.";
            return false;
        }

        // A release whose locked target died must still enter the existing
        // fizzle path so the telegraph is consumed cleanly. Formation range
        // only matters while there is still a live target to hit.
        if ((!isRelease || target.IsAlive) &&
            !CanTargetWithSkill(actor, target, command.Skill))
        {
            error = "The target is outside this skill's formation range.";
            return false;
        }

        if (command.Kind == DRPGBattleCommandKind.BasicAttack &&
            command.Stage != DRPGBattleCommandStage.Immediate)
        {
            error = "Basic attack cannot create a staged special action.";
            return false;
        }

        if (command.Stage == DRPGBattleCommandStage.Telegraph &&
            command.Skill.ExecutionMode != DRPGSkillExecutionMode.TelegraphedSpecial)
        {
            error = "Only telegraphed-special skills can create a telegraph.";
            return false;
        }

        if (command.Stage == DRPGBattleCommandStage.Release)
        {
            DRPGTelegraphedAction pending = FindTelegraph(actor.RuntimeId);
            if (pending == null || !pending.Matches(command))
            {
                error = "The release command does not match a pending telegraph.";
                return false;
            }
        }

        if (command.Stage == DRPGBattleCommandStage.Immediate &&
            command.Skill.ExecutionMode == DRPGSkillExecutionMode.TelegraphedSpecial)
        {
            error = "A telegraphed-special skill cannot execute immediately.";
            return false;
        }

        if (!CanAffordSkill(actor, command.Skill, command.Stage, out error))
            return false;

        return true;
    }

    private BattleParticipantRuntime FindParticipant(int runtimeId)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i].RuntimeId == runtimeId)
                return participants[i];
        }

        return null;
    }

    private bool HasLegalTarget(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (CanTargetWithSkill(actor, participants[i], skill))
                return true;
        }
        return false;
    }

    private BattleParticipantRuntime FirstLivingLegalTarget(
        BattleParticipantRuntime actor,
        DRPGBattleSide targetSide,
        DRPGSkillSO skill)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime candidate = participants[i];
            if (candidate.Side == targetSide &&
                CanTargetWithSkill(actor, candidate, skill))
            {
                return candidate;
            }
        }
        return null;
    }

    private bool TryCreateEnemyAttack(
        BattleParticipantRuntime actor,
        out DRPGBattleCommand command)
    {
        command = null;
        if (TryCreateEnemyAttack(actor, actor.Build.BasicAttack, true, out command))
            return true;

        IReadOnlyList<DRPGSkillSO> skills = actor.Build.BattleSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            DRPGSkillSO skill = skills[i];
            if (skill == null || ReferenceEquals(skill, actor.Build.BasicAttack))
                continue;
            if (TryCreateEnemyAttack(actor, skill, false, out command))
                return true;
        }

        return false;
    }

    private bool TryCreateEnemyAttack(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        bool basicAttack,
        out DRPGBattleCommand command)
    {
        command = null;
        if (skill == null || !skill.CanUseFrom(actor.FormationRow) ||
            !CanAffordSkill(
                actor,
                skill,
                skill.ExecutionMode == DRPGSkillExecutionMode.TelegraphedSpecial
                    ? DRPGBattleCommandStage.Telegraph
                    : DRPGBattleCommandStage.Immediate,
                out _))
            return false;

        enemyTargetCandidates.Clear();
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime candidate = participants[i];
            if (candidate.IsOrdinaryEnemyTarget &&
                CanTargetWithSkill(actor, candidate, skill))
            {
                enemyTargetCandidates.Add(candidate);
            }
        }
        if (enemyTargetCandidates.Count == 0)
            return false;

        DRPGPostureBreakTargetPolicy.ApplyNewSelectionPolicy(
            skill,
            enemyTargetCandidates);

        int selectedIndex = randomSource.Next(enemyTargetCandidates.Count);
        if (selectedIndex < 0 || selectedIndex >= enemyTargetCandidates.Count)
        {
            throw new InvalidOperationException(
                "The battle random source returned an out-of-range target index.");
        }
        BattleParticipantRuntime target = enemyTargetCandidates[selectedIndex];
        if (basicAttack &&
            skill.ExecutionMode != DRPGSkillExecutionMode.TelegraphedSpecial)
        {
            command = DRPGBattleCommand.BasicAttack(
                actor.RuntimeId,
                target.RuntimeId,
                skill);
        }
        else
        {
            DRPGBattleCommandStage stage =
                skill.ExecutionMode == DRPGSkillExecutionMode.TelegraphedSpecial
                    ? DRPGBattleCommandStage.Telegraph
                    : DRPGBattleCommandStage.Immediate;
            command = basicAttack
                ? new DRPGBattleCommand(
                    actor.RuntimeId,
                    target.RuntimeId,
                    skill,
                    stage)
                : DRPGBattleCommand.UseSkill(
                    actor.RuntimeId,
                    target.RuntimeId,
                    skill,
                    stage);
        }
        return true;
    }

    private bool TryCreatePreferredReposition(
        BattleParticipantRuntime actor,
        out DRPGBattleCommand command)
    {
        command = null;
        int current = (int)actor.FormationRow;
        int preferred = (int)actor.PreferredFormationRow;
        if (current == preferred)
            return false;

        DRPGFormationRow destination = (DRPGFormationRow)(
            current + Math.Sign(preferred - current));
        if (!CanReposition(actor, destination, out _) ||
            !CanUseAnySkillFrom(actor, destination))
        {
            return false;
        }

        command = DRPGBattleCommand.Reposition(actor.RuntimeId, destination);
        return true;
    }

    private bool TryCreateTacticalReposition(
        BattleParticipantRuntime actor,
        out DRPGBattleCommand command)
    {
        command = null;
        int current = (int)actor.FormationRow;
        int[] directions = { -1, 1 };
        for (int i = 0; i < directions.Length; i++)
        {
            int destinationIndex = current + directions[i];
            if (destinationIndex < 0 || destinationIndex > 2)
                continue;
            DRPGFormationRow destination =
                (DRPGFormationRow)destinationIndex;
            if (CanReposition(actor, destination, out _) &&
                CanUseAnySkillFrom(actor, destination))
            {
                command = DRPGBattleCommand.Reposition(
                    actor.RuntimeId,
                    destination);
                return true;
            }
        }
        return false;
    }

    private bool CanUseAnySkillFrom(
        BattleParticipantRuntime actor,
        DRPGFormationRow row)
    {
        if (CanUseSkillFromAgainstLivingTarget(
                actor,
                actor.Build.BasicAttack,
                row))
        {
            return true;
        }
        IReadOnlyList<DRPGSkillSO> skills = actor.Build.BattleSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            if (CanUseSkillFromAgainstLivingTarget(actor, skills[i], row))
                return true;
        }
        return false;
    }

    private bool CanUseSkillFromAgainstLivingTarget(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        DRPGFormationRow sourceRow)
    {
        if (skill == null || !skill.CanUseFrom(sourceRow))
            return false;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime target = participants[i];
            if (target.IsOrdinaryEnemyTarget &&
                target.Side != actor.Side &&
                skill.CanTarget(target.FormationRow))
            {
                return true;
            }
        }
        return false;
    }

    private bool CanReposition(
        BattleParticipantRuntime actor,
        DRPGFormationRow destination,
        out string error)
    {
        error = string.Empty;
        if (actor == null || !actor.IsAlive || !actor.IsDeployed)
        {
            error = "Only a living deployed actor can reposition.";
            return false;
        }
        int delta = Math.Abs((int)destination - (int)actor.FormationRow);
        if (delta != 1)
        {
            error = "A reposition action moves exactly one formation row.";
            return false;
        }
        if (CountAtRow(actor.Side, destination, actor) >= formationRules.PerRowLimit)
        {
            error = "The destination formation row is full.";
            return false;
        }

        if ((int)destination > (int)actor.FormationRow)
        {
            for (int row = 0; row < (int)destination; row++)
            {
                if (CountAtRow(actor.Side, (DRPGFormationRow)row, actor) == 0)
                {
                    error = "An actor cannot hide behind an empty forward row.";
                    return false;
                }
            }
        }
        return true;
    }

    private bool TryApplyReposition(
        BattleParticipantRuntime actor,
        DRPGFormationRow destination,
        out string error)
    {
        if (!CanReposition(actor, destination, out error))
            return false;
        actor.TrySetFormationRow(destination);
        NormalizeFormationRows(false);
        PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
        PublishNotification(FormationChanged, nameof(FormationChanged));
        error = string.Empty;
        return true;
    }

    public bool TryApplyFormationShift(
        BattleParticipantRuntime target,
        int rowDelta,
        out DRPGFormationRow before,
        out DRPGFormationRow after,
        out string error)
    {
        before = target != null ? target.FormationRow : DRPGFormationRow.Front;
        after = before;
        if (target == null || !target.IsAlive || !target.IsDeployed)
        {
            error = "Only a living deployed target can be shifted.";
            return false;
        }
        if (rowDelta == 0)
        {
            error = "Formation shift has no row delta.";
            return false;
        }

        int destinationIndex = Math.Max(
            0,
            Math.Min(2, (int)before + rowDelta));
        DRPGFormationRow destination = (DRPGFormationRow)destinationIndex;
        if (destination == before)
        {
            error = "Formation shift was clamped at the battlefield edge.";
            return false;
        }
        if (CountAtRow(target.Side, destination, target) >=
            formationRules.PerRowLimit)
        {
            error = "The forced formation destination is full.";
            return false;
        }

        target.TrySetFormationRow(destination);
        after = target.FormationRow;
        error = string.Empty;
        return true;
    }

    public bool TryApplyTimelineShift(
        BattleParticipantRuntime target,
        float timelineDelta,
        int priorityDelta,
        out float before,
        out float after,
        out string error)
    {
        before = 0f;
        after = 0f;
        if (target == null || !target.IsAlive)
        {
            error = "Only a living target can be shifted on the timeline.";
            return false;
        }

        if (scheduler.TryShiftParticipant(
                target.RuntimeId,
                timelineDelta,
                priorityDelta,
                out before,
                out after))
        {
            error = string.Empty;
            return true;
        }

        if (!ReferenceEquals(target, CurrentActor))
        {
            error = "The target has no scheduled or active turn to shift.";
            return false;
        }

        pendingTimelineDeltas.TryGetValue(
            target.RuntimeId,
            out float currentDelta);
        pendingTimelinePriorities.TryGetValue(
            target.RuntimeId,
            out int currentPriority);
        before = currentDelta;
        after = currentDelta + timelineDelta;
        pendingTimelineDeltas[target.RuntimeId] = after;
        pendingTimelinePriorities[target.RuntimeId] =
            currentPriority + priorityDelta;
        error = string.Empty;
        return true;
    }

    private int CountAtRow(
        DRPGBattleSide side,
        DRPGFormationRow row,
        BattleParticipantRuntime excluded = null)
    {
        int count = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (!ReferenceEquals(participant, excluded) &&
                participant.Side == side && participant.IsAlive &&
                participant.IsDeployed && participant.FormationRow == row)
            {
                count++;
            }
        }
        return count;
    }

    private void NormalizeFormationRows(bool publish)
    {
        bool changed = DRPGFormationNormalizer.CompactOccupiedRows(
            participants,
            DRPGBattleSide.Enemy);
        changed |= DRPGFormationNormalizer.CompactOccupiedRows(
            participants,
            DRPGBattleSide.Player);
        if (changed && publish)
        {
            PublishNotification(FormationChanged, nameof(FormationChanged));
            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipantRuntime participant = participants[i];
                PublishNotification(ParticipantChanged, participant, nameof(ParticipantChanged));
            }
        }
    }

    private void AdvanceReinforcements()
    {
        if (reserveEnemies.Count == 0 ||
            CountLivingDeployedEnemies() >= formationRules.ActiveEnemyLimit)
        {
            ClearReinforcementWarning();
            return;
        }

        if (reinforcementDelayRemaining < 0)
        {
            reinforcementDelayRemaining =
                formationRules.ReinforcementDelayActions;
            PublishNotification(ReinforcementsChanged, nameof(ReinforcementsChanged));
            return;
        }

        if (reinforcementDelayRemaining > 0)
            reinforcementDelayRemaining--;
        if (reinforcementDelayRemaining <= 0)
            DeployNextReinforcement(false);
    }

    private bool DeployNextReinforcement(bool force)
    {
        if (reserveEnemies.Count == 0 ||
            CountLivingDeployedEnemies() >= formationRules.ActiveEnemyLimit)
        {
            ClearReinforcementWarning();
            return false;
        }
        if (!force && reinforcementDelayRemaining > 0)
            return false;

        BattleParticipantRuntime entrant = reserveEnemies[0];
        if (!TryFindAvailableFormationRow(
                entrant.PreferredFormationRow,
                out DRPGFormationRow row))
        {
            return false;
        }

        reserveEnemies.RemoveAt(0);
        entrant.TrySetFormationRow(row);
        entrant.TrySetDeployed(true);
        participants.Add(entrant);
        NormalizeFormationRows(false);
        float initialDelay = scheduler.CalculateInitialDelay(entrant);
        float baseReadyTime =
            scheduler.CurrentTime + Math.Max(0.001f, initialDelay);
        if (!TryScheduleOrdinaryEnemyPlan(
                entrant,
                scheduler.CurrentTime,
                baseReadyTime,
                0,
                true))
        {
            scheduler.ScheduleAt(entrant, baseReadyTime, 0);
        }
        reinforcementDelayRemaining = -1;
        PublishNotification(ParticipantChanged, entrant, nameof(ParticipantChanged));
        PublishNotification(FormationChanged, nameof(FormationChanged));
        PublishNotification(ReinforcementsChanged, nameof(ReinforcementsChanged));
        PublishTimelineChanged();
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool TryForceDeployAllDevelopment(
        out int deployedCount,
        out string error)
    {
        deployedCount = 0;
        if (!IsActive)
        {
            error = "当前没有正在进行的战斗。";
            return false;
        }

        while (reserveEnemies.Count > 0 &&
            CountLivingDeployedEnemies() < formationRules.ActiveEnemyLimit)
        {
            if (!DeployNextReinforcement(true))
                break;
            deployedCount++;
        }

        error = string.Empty;
        return true;
    }
#endif

    private bool TryFindAvailableFormationRow(
        DRPGFormationRow preferred,
        out DRPGFormationRow row)
    {
        for (int distance = 0; distance <= 2; distance++)
        {
            int shallower = (int)preferred - distance;
            if (shallower >= 0 && CountAtRow(
                    DRPGBattleSide.Enemy,
                    (DRPGFormationRow)shallower) < formationRules.PerRowLimit)
            {
                row = (DRPGFormationRow)shallower;
                return true;
            }
            int deeper = (int)preferred + distance;
            if (distance > 0 && deeper <= 2 && CountAtRow(
                    DRPGBattleSide.Enemy,
                    (DRPGFormationRow)deeper) < formationRules.PerRowLimit)
            {
                row = (DRPGFormationRow)deeper;
                return true;
            }
        }
        row = preferred;
        return false;
    }

    private int CountLivingDeployedEnemies()
    {
        int count = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant.Side == DRPGBattleSide.Enemy &&
                participant.IsAlive && participant.IsDeployed)
            {
                count++;
            }
        }
        return count;
    }

    private void ClearReinforcementWarning()
    {
        if (reinforcementDelayRemaining < 0)
            return;
        reinforcementDelayRemaining = -1;
        PublishNotification(ReinforcementsChanged, nameof(ReinforcementsChanged));
    }

    private bool TryOpenLegacyActionScope(
        DRPGBattlePreparedAction action,
        out IDisposable scope,
        out string error)
    {
        scope = null;
        if (!IsActiveAction(action) || action.LegacyMutationPort == null)
        {
            error = "The prepared action has no matching Legacy mutation port.";
            return false;
        }
        return action.LegacyMutationPort.TryOpenScope(
            action,
            out scope,
            out error);
    }

    private bool IsActiveAction(DRPGBattlePreparedAction action)
    {
        return action != null && activeAction != null &&
            action.ActionId == activeAction.ActionId &&
            ReferenceEquals(action, activeAction);
    }

    private bool TryCommitActionResources(
        DRPGBattlePreparedAction action,
        BattleParticipantRuntime actor,
        out string error)
    {
        if (!action.ResourceTransaction.CanCommit(actor, out error) ||
            action.ProgressTransaction != null &&
            !action.ProgressTransaction.CanCommit(progressPipeline, out error))
        {
            CancelFailedPreparedAction(action);
            return false;
        }

        DRPGActionResourceTransactionState previousState =
            action.ResourceTransaction.State;
        if (!action.ResourceTransaction.TryCommit(actor, out _, out error))
        {
            CancelFailedPreparedAction(action);
            return false;
        }
        DRPGProgressMutationResult progressMutation = null;
        if (action.ProgressTransaction != null &&
            !action.ProgressTransaction.TryCommit(
                progressPipeline,
                out progressMutation,
                out error))
        {
            CancelFailedPreparedAction(action);
            return false;
        }
        if (previousState != DRPGActionResourceTransactionState.Committed &&
            (action.Spec.ManaCost > 0f || action.Spec.PostureCost > 0f ||
             action.Spec.StaminaCost > 0))
        {
            PublishNotification(ParticipantChanged, actor, nameof(ParticipantChanged));
        }
        if (progressMutation != null)
            PublishNotification(ProgressChanged, progressMutation, nameof(ProgressChanged));
        return true;
    }

    private bool CanAffordSkill(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        DRPGBattleCommandStage stage,
        out string error)
    {
        error = string.Empty;
        if (actor == null || skill == null)
            return true;
        if (stage == DRPGBattleCommandStage.Release)
            return true;

        DRPGStatusActionModifiers modifiers =
            actor.Statuses.ResolveActionModifiers();
        float manaCost = skill.ManaCost * modifiers.ManaCostMultiplier;
        float postureCost = skill.PostureCost * modifiers.PostureCostMultiplier;
        if (actor.CurrentMp + 0.0001f < manaCost)
        {
            error = "The actor does not have enough MP for this skill.";
            return false;
        }
        if (actor.CurrentPosture + 0.0001f < postureCost)
        {
            error = "The actor does not have enough posture for this skill.";
            return false;
        }
        if (actor.BattleStamina.Available < skill.StaminaCost)
        {
            error = "The actor does not have enough stamina for this skill.";
            return false;
        }
        DRPGResolvedProgressCost actionCost = skill.ActionProgressCost != null
            ? skill.ActionProgressCost.Freeze()
            : DRPGResolvedProgressCost.None;
        DRPGResolvedProgressCost inputCost =
            skill.ExecutionMode == DRPGSkillExecutionMode.FreeStrike &&
            skill.InputProgressCost != null
                ? skill.InputProgressCost.Freeze()
                : DRPGResolvedProgressCost.None;
        return CanAffordProgressCosts(
            actor,
            actionCost,
            inputCost,
            out error);
    }

    private DRPGProgressSnapshot ResolveSkillProgressSnapshot(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill)
    {
        if (skill == null)
            return null;
        DRPGResolvedProgressCost cost =
            skill.ExecutionMode == DRPGSkillExecutionMode.FreeStrike &&
            skill.InputProgressCost != null
                ? skill.InputProgressCost.Freeze()
                : skill.ActionProgressCost != null
                    ? skill.ActionProgressCost.Freeze()
                    : DRPGResolvedProgressCost.None;
        return ResolveProgressSnapshot(actor, cost);
    }

    private DRPGProgressSnapshot ResolveProgressSnapshot(
        BattleParticipantRuntime actor,
        DRPGResolvedProgressCost cost)
    {
        if (actor == null || actor.Profile == null || cost == null ||
            !cost.IsConfigured)
        {
            return null;
        }
        progressPipeline.TryGetSnapshot(
            actor.Profile.CombatantId,
            cost.ResourceId,
            DRPGProgressScope.Battle,
            out DRPGProgressSnapshot snapshot);
        return snapshot;
    }

    private bool CanAffordResolvedProgress(
        BattleParticipantRuntime actor,
        DRPGResolvedActionSpec spec,
        bool includeInputCost,
        out string error)
    {
        return CanAffordProgressCosts(
            actor,
            spec != null
                ? spec.ActionProgressCost
                : DRPGResolvedProgressCost.None,
            includeInputCost && spec != null
                ? spec.InputProgressCost
                : DRPGResolvedProgressCost.None,
            out error);
    }

    private bool CanAffordInteractiveInputProgress(
        BattleParticipantRuntime actor,
        DRPGResolvedActionSpec spec,
        out string error)
    {
        error = string.Empty;
        if (spec == null || spec.InputProgressCost == null ||
            !spec.InputProgressCost.IsConfigured)
        {
            return true;
        }
        if (actor == null || actor.Profile == null ||
            string.IsNullOrWhiteSpace(actor.Profile.CombatantId))
        {
            error = "Interactive progress owner requires a stable character ID.";
            return false;
        }
        return progressPipeline.CanSpend(
            actor.Profile.CombatantId,
            spec.InputProgressCost,
            DRPGProgressScope.Battle,
            out error);
    }

    private bool CanAffordProgressCosts(
        BattleParticipantRuntime actor,
        DRPGResolvedProgressCost actionCost,
        DRPGResolvedProgressCost inputCost,
        out string error)
    {
        error = string.Empty;
        if (actor == null || actor.Profile == null)
            return true;
        string owner = actor.Profile.CombatantId;
        if (actionCost != null && actionCost.IsConfigured &&
            inputCost != null && inputCost.IsConfigured &&
            string.Equals(
                actionCost.ResourceId,
                inputCost.ResourceId,
                StringComparison.Ordinal))
        {
            return progressPipeline.CanSpend(
                owner,
                new DRPGResolvedProgressCost(
                    actionCost.ResourceId,
                    actionCost.Amount + inputCost.Amount),
                DRPGProgressScope.Battle,
                out error);
        }
        if (!progressPipeline.CanSpend(
                owner,
                actionCost,
                DRPGProgressScope.Battle,
                out error))
        {
            return false;
        }
        return progressPipeline.CanSpend(
            owner,
            inputCost,
            DRPGProgressScope.Battle,
            out error);
    }

    private void ScheduleNextActor(
        BattleParticipantRuntime actor,
        DRPGResolvedActionSpec spec)
    {
        transactionState.TurnCandidates.CalculateNextSchedule(actor, spec, out float baseReadyTime, out int priority);
        pendingTimelineDeltas.Remove(actor.RuntimeId);
        pendingTimelinePriorities.Remove(actor.RuntimeId);
        if (actor.Side == DRPGBattleSide.Enemy &&
            TryScheduleOrdinaryEnemyPlan(
                actor,
                scheduler.CurrentTime,
                baseReadyTime,
                priority,
                true))
        {
            return;
        }

        scheduler.ScheduleAt(actor, baseReadyTime, priority);
    }

    private void PublishEffectChanges(
        IReadOnlyList<DRPGActionEffectResult> effects)
    {
        if (effects == null || effects.Count == 0)
            return;

        var changedParticipants = new HashSet<int>();
        bool formationChanged = false;
        bool timelineChanged = false;
        for (int i = 0; i < effects.Count; i++)
        {
            DRPGActionEffectResult effect = effects[i];
            if (effect == null || !effect.WasApplied)
                continue;
            if (effect.TargetRuntimeId > 0)
                changedParticipants.Add(effect.TargetRuntimeId);
            formationChanged |= effect.Kind ==
                DRPGActionEffectKind.FormationShift;
            timelineChanged |= effect.Kind ==
                DRPGActionEffectKind.TimelineShift;
        }

        foreach (int runtimeId in changedParticipants)
        {
            BattleParticipantRuntime participant = FindParticipant(runtimeId);
            if (participant != null)
                PublishNotification(ParticipantChanged, participant, nameof(ParticipantChanged));
        }
        if (formationChanged)
            PublishNotification(FormationChanged, nameof(FormationChanged));
        if (timelineChanged)
            PublishTimelineChanged();
    }

    private void PublishActionResolution(
        DRPGBattleActionResolution resolution)
    {
        if (resolution == null)
            throw new ArgumentNullException(nameof(resolution));

        DRPGBattleEventBatch rootBatch = eventFactory.CreateRoot(resolution);
        LastRootEventBatch = rootBatch;
        PublishNotification(ActionResolved, resolution, nameof(ActionResolved));

        DRPGBattleReactionRun run;
        try
        {
            run = reactionPipeline.ProcessBatch(
                rootBatch,
                this,
                resolver,
                eventFactory);
        }
        catch (Exception exception)
        {
            run = new DRPGBattleReactionRun(
                rootBatch,
                new[] { rootBatch },
                Array.Empty<DRPGBattleReactionResolution>(),
                new[]
                {
                    new DRPGBattleReactionTraceEntry(
                        0L,
                        0,
                        "reaction.pipeline",
                        string.Empty,
                        DRPGBattleReactionTraceState.ResolutionFaulted,
                        exception.Message)
                });
        }

        LastReactionRun = run;
        DRPGBattleProgressRun progressRun;
        try
        {
            progressRun = progressPipeline.Process(
                run.Batches,
                participants);
        }
        catch (Exception exception)
        {
            progressRun = new DRPGBattleProgressRun(
                run.Batches,
                Array.Empty<DRPGProgressMutationResult>(),
                new[]
                {
                    new DRPGProgressTraceEntry(
                        "progress.pipeline",
                        string.Empty,
                        string.Empty,
                        0,
                        DRPGProgressTraceState.StoreFaulted,
                        exception.Message)
                });
        }
        LastProgressRun = progressRun;
        try
        {
            resonanceAccess?.ObserveBattleEvents(run.Batches, participants);
        }
        catch (Exception)
        {
            // Resonance progress is auxiliary and must never break action
            // resolution. Its dedicated probes audit the source rules.
        }
        for (int i = 0; i < progressRun.Mutations.Count; i++)
        {
            DRPGProgressMutationResult mutation = progressRun.Mutations[i];
            PublishNotification(ProgressChanged, mutation, nameof(ProgressChanged));
        }
        PublishNotification(ProgressRunCompleted, progressRun, nameof(ProgressRunCompleted));

        for (int batchIndex = 0;
             batchIndex < run.Batches.Count;
             batchIndex++)
        {
            DRPGBattleEventBatch batch = run.Batches[batchIndex];
            PublishNotification(BattleEventBatchPublished, batch, nameof(BattleEventBatchPublished));
            for (int eventIndex = 0;
                 eventIndex < batch.Events.Count;
                 eventIndex++)
            {
                DRPGBattleEvent battleEvent = batch.Events[eventIndex];
                PublishNotification(BattleEventPublished, battleEvent, nameof(BattleEventPublished));
                if (battleEvent.Kind ==
                    DRPGBattleEventKind.PostureBreakEntered)
                {
                    BattleParticipantRuntime broken = FindParticipant(
                        battleEvent.TargetRuntimeId);
                    TryInterruptEnemyPlanForPostureBreak(broken);
                }
            }
        }

        for (int i = 0; i < run.Resolutions.Count; i++)
        {
            DRPGBattleReactionResolution reaction = run.Resolutions[i];
            PublishEffectChanges(reaction.Batch.Effects);
            PublishNotification(ReactionResolved, reaction, nameof(ReactionResolved));
        }
        PublishNotification(ReactionRunCompleted, run, nameof(ReactionRunCompleted));
    }

    private BattleParticipantRuntime FirstLivingParticipant(DRPGBattleSide side)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant.Side == side && participant.IsAlive &&
                participant.IsDeployed)
                return participant;
        }

        return null;
    }

    private BattleParticipantRuntime FirstLivingBattlefieldMember()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant.IsBattlefieldMember && participant.IsAlive &&
                participant.IsDeployed)
                return participant;
        }

        return null;
    }

    private BattleParticipantRuntime RandomOrdinaryEnemyTarget()
    {
        enemyTargetCandidates.Clear();
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i].IsOrdinaryEnemyTarget)
                enemyTargetCandidates.Add(participants[i]);
        }

        if (enemyTargetCandidates.Count == 0)
            return null;

        int selectedIndex = randomSource.Next(enemyTargetCandidates.Count);
        if (selectedIndex < 0 || selectedIndex >= enemyTargetCandidates.Count)
        {
            throw new InvalidOperationException(
                "The battle random source returned an out-of-range target index.");
        }

        return enemyTargetCandidates[selectedIndex];
    }

    private static DRPGSkillSO FirstAvailableSkill(BattleParticipantRuntime actor)
    {
        if (actor.Build.BasicAttack != null)
            return actor.Build.BasicAttack;

        IReadOnlyList<DRPGSkillSO> skills = actor.Build.BattleSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null)
                return skills[i];
        }

        return null;
    }

    private static bool IsSkillAvailable(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill)
    {
        return actor != null && skill != null &&
            (ReferenceEquals(actor.Build.BasicAttack, skill) ||
             actor.Build.ContainsBattleSkill(skill.SkillId));
    }

    private bool RollEscape(BattleParticipantRuntime actor)
    {
        float chance = Math.Max(
            0f,
            Math.Min(
                1f,
                escapeRules.BaseChance + actor.Build.EscapeChanceBonus));
        const int precision = 10000;
        int roll = randomSource.Next(precision);
        if (roll < 0 || roll >= precision)
        {
            throw new InvalidOperationException(
                "The battle random source returned an out-of-range escape roll.");
        }
        return roll < chance * precision;
    }

    private void CancelFailedPreparedAction(DRPGBattlePreparedAction action)
    {
        if (!IsActiveAction(action))
            return;
        RequireFlowTransition(
            flow.TryCancelActionToCommand(
                action.ActionId,
                out string flowError),
            flowError);
        weaponDurabilityService?.CancelAction(action.WeaponActionContext);
        action.ResourceTransaction.Cancel();
        action.ProgressTransaction?.Cancel();
        action.LegacyMutationPort?.AbortAndRelease();
        activeAction = null;
        activeResolution = null;
        currentActionIdentity = default;
        activeFreeStrikeWindow = null;
        activeEscapeSucceeded = false;
        activeHits.Clear();
        activeEffects.Clear();
    }

    private static void RequireFlowTransition(bool succeeded, string error)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? "The battle flow rejected an internal transition."
                    : error);
        }
    }

    private bool TryRecordCommittedWeaponUse(
        DRPGBattlePreparedAction action,
        DRPGWeaponUseKind useKind,
        out string error)
    {
        if (weaponDurabilityService == null ||
            action == null ||
            action.WeaponActionContext == null)
        {
            error = string.Empty;
            return true;
        }

        DRPGEquipmentOperationResult result =
            weaponDurabilityService.TryRecordCommittedUse(
                action.WeaponActionContext,
                useKind);
        error = result.Success ? string.Empty : result.Message;
        return result.Success;
    }

    private static bool ShouldCaptureWeaponUse(DRPGBattleCommand command)
    {
        return command != null &&
            (command.Kind == DRPGBattleCommandKind.BasicAttack ||
             command.Kind == DRPGBattleCommandKind.Skill) &&
            command.Stage != DRPGBattleCommandStage.Telegraph;
    }

    private static DRPGWeaponUseKind ResolveWeaponUseKind(
        DRPGBattlePreparedAction action)
    {
        return action != null && action.Spec != null &&
            action.Spec.ExecutionMode ==
                DRPGSkillExecutionMode.FreeStrike
            ? DRPGWeaponUseKind.FreeStrikeInput
            : DRPGWeaponUseKind.MeleeCommittedSwing;
    }

    private static bool ContainsSkill(
        IReadOnlyList<DRPGSkillSO> skills,
        DRPGSkillSO skill)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (ReferenceEquals(skills[i], skill))
                return true;
        }

        return false;
    }

    private DRPGTelegraphedAction FindTelegraph(int actorRuntimeId)
    {
        for (int i = 0; i < pendingTelegraphs.Count; i++)
        {
            if (pendingTelegraphs[i].ActorRuntimeId == actorRuntimeId)
                return pendingTelegraphs[i];
        }
        return null;
    }

    private void RegisterTelegraph(DRPGBattlePreparedAction action)
    {
        DRPGBattleCommand command = action.Command;
        RemoveTelegraph(command.ActorRuntimeId, false);
        pendingTelegraphs.Add(new DRPGTelegraphedAction(
            command.ActorRuntimeId,
            command.TargetRuntimeId,
            command.Skill,
            action.Spec));
        PublishNotification(TelegraphsChanged, nameof(TelegraphsChanged));
    }

    private void RemoveTelegraph(int actorRuntimeId, bool publish)
    {
        bool removed = false;
        for (int i = pendingTelegraphs.Count - 1; i >= 0; i--)
        {
            if (pendingTelegraphs[i].ActorRuntimeId != actorRuntimeId)
                continue;
            pendingTelegraphs.RemoveAt(i);
            removed = true;
        }
        if (removed && publish)
            PublishNotification(TelegraphsChanged, nameof(TelegraphsChanged));
    }

    private void PublishTimelineChanged()
    {
        PublishNotification(TimelineChanged, nameof(TimelineChanged));
    }
}
