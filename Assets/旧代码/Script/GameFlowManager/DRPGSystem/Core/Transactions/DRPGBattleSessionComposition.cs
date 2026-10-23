using System;
using System.Collections.Generic;

internal sealed class DRPGBattleSessionComposition
{
    private DRPGBattleSessionComposition(
        long battleGeneration,
        IReadOnlyList<BattleParticipantRuntime> participants,
        IReadOnlyList<BattleParticipantRuntime> reserveEnemies,
        DRPGTurnScheduler scheduler,
        DRPGBattleIdentityAuthority identityAuthority,
        DRPGBattleActionTraceWriter trace,
        DRPGBattleTransactionArbiter arbiter,
        DRPGBattleSessionState state,
        DRPGBattleProgressPipeline progressPipeline,
        DRPGBasicAttackSelectionRuntimeState selectionState,
        DRPGBattleTransactionPresentationPortSlot presentationPortSlot,
        DRPGStandardBasicAttackTransactionModule standardBasicAttackRunner,
        DRPGLegacyActionRunnerAdapter legacyActionRunner,
        DRPGBattleActionExecutionRouter actionExecutionRouter,
        DRPGBattleTransactionPolicy policy, DRPGEnemyWavePlan wavePlan, DRPGEnemyWavePolicySnapshot? wavePolicy)
    {
        BattleGeneration = battleGeneration;
        Participants = participants;
        ReserveEnemies = reserveEnemies;
        Scheduler = scheduler;
        IdentityAuthority = identityAuthority;
        Trace = trace;
        Arbiter = arbiter;
        State = state;
        ProgressPipeline = progressPipeline;
        SelectionState = selectionState;
        PresentationPortSlot = presentationPortSlot;
        StandardBasicAttackRunner = standardBasicAttackRunner;
        LegacyActionRunner = legacyActionRunner;
        ActionExecutionRouter = actionExecutionRouter;
        Policy = policy;
        WavePolicy = wavePolicy;
        ContinuationInbox = new DRPGBattleContinuationInbox(arbiter);
        if (wavePlan != null)
        {
            WaveLifecycle = new DRPGEnemyWaveLifecycle(state, ContinuationInbox, wavePlan, reserveEnemies);
            state.TurnCandidates.BindWaveOutcomePlan(wavePlan);
        }
    }

    internal long BattleGeneration { get; }
    internal IReadOnlyList<BattleParticipantRuntime> Participants { get; }
    internal IReadOnlyList<BattleParticipantRuntime> ReserveEnemies { get; }
    internal DRPGTurnScheduler Scheduler { get; }
    internal DRPGBattleIdentityAuthority IdentityAuthority { get; }
    internal DRPGBattleActionTraceWriter Trace { get; }
    internal DRPGBattleTransactionArbiter Arbiter { get; }
    internal DRPGBattleSessionState State { get; }
    internal DRPGBattleProgressPipeline ProgressPipeline { get; }
    internal DRPGBasicAttackSelectionRuntimeState SelectionState { get; }
    internal DRPGBattleTransactionPresentationPortSlot PresentationPortSlot { get; }
    internal DRPGStandardBasicAttackTransactionModule StandardBasicAttackRunner { get; }
    internal DRPGLegacyActionRunnerAdapter LegacyActionRunner { get; }
    internal DRPGBattleActionExecutionRouter ActionExecutionRouter { get; }
    internal DRPGBattleTransactionPolicy Policy { get; }
    internal DRPGBattleContinuationInbox ContinuationInbox { get; }
    internal DRPGEnemyWaveLifecycle WaveLifecycle { get; }
    internal DRPGEnemyWavePolicySnapshot? WavePolicy { get; }

    internal static DRPGBattleSessionComposition Build(
        IEnumerable<BattleParticipantRuntime> participants,
        IEnumerable<BattleParticipantRuntime> reserveEnemies,
        DRPGTurnScheduler scheduler,
        IDRPGBattleGenerationSource generationSource,
        DRPGBattleTransactionPolicy policy,
        DRPGBattleProgressPipeline progressPipeline,
        IDRPGBattleWeaponDurabilityService weaponService = null, IDRPGBattleResonanceAccess resonanceAccess = null,
        DRPGEnemyWavePlan wavePlan = null, DRPGEnemyWavePolicySnapshot? wavePolicy = null)
    {
        if (participants == null)
            throw new ArgumentNullException(nameof(participants));
        if (generationSource == null)
            throw new ArgumentNullException(nameof(generationSource));

        var active = new List<BattleParticipantRuntime>(participants);
        var reserve = reserveEnemies != null
            ? new List<BattleParticipantRuntime>(reserveEnemies)
            : new List<BattleParticipantRuntime>();
        ValidateParticipants(active, reserve);
        if (wavePlan != null && (!policy.StandardBasicAttackEnabled || active.Exists(p => p.Side == DRPGBattleSide.Enemy)))
            throw new ArgumentException("Explicit Wave composition requires Standard policy and no initially active enemies.");
        long generation = generationSource.NextGeneration();
        if (generation <= 0L)
        {
            throw new InvalidOperationException(
                "The battle generation source returned an invalid identity.");
        }
        var identityAuthority = new DRPGBattleIdentityAuthority(generation);
        var trace = new DRPGBattleActionTraceWriter(
            new DRPGBoundedActionTraceSink(512));
        var arbiter = new DRPGBattleTransactionArbiter(generation, trace);
        var allParticipants = new List<BattleParticipantRuntime>(
            active.Count + reserve.Count);
        allParticipants.AddRange(active);
        allParticipants.AddRange(reserve);
        DRPGTurnScheduler resolvedScheduler =
            scheduler ?? new DRPGTurnScheduler();
        DRPGBattleProgressPipeline resolvedProgressPipeline = progressPipeline ?? new DRPGBattleProgressPipeline();
        DRPGBattleActionResourceBindings resources = null;
        if (resolvedProgressPipeline.Router.TryGetStore(DRPGProgressScope.Battle, out var progressStore) && progressStore is DRPGBattleProgressLedger ledger)
            resources = weaponService is DRPGBattleWeaponDurabilityService weapons ?
                weapons.CreateActionResourceBindings(ledger, allParticipants, resonanceAccess as DRPGBattleResonanceAccess) :
                new DRPGBattleActionResourceBindings(ledger, new DRPGWeaponUseLedger(), null, resonanceAccess: resonanceAccess as DRPGBattleResonanceAccess);
        var state = new DRPGBattleSessionState(
            generation,
            allParticipants,
            resolvedScheduler,
            arbiter, actionResources: resources);
        for (int i = 0; i < reserve.Count; i++)
        {
            state.InitializeParticipantDeployment(
                new ParticipantId(reserve[i].RuntimeId),
                false);
        }
        var presentationSlot =
            new DRPGBattleTransactionPresentationPortSlot(generation);
        var selectionState = new DRPGBasicAttackSelectionRuntimeState(
            allParticipants.AsReadOnly(),
            arbiter,
            resolvedProgressPipeline,
            presentationSlot,
            policy.StandardBasicAttackEnabled);
        var standardRunner = new DRPGStandardBasicAttackTransactionModule(
            presentationSlot);
        var legacyRunner = new DRPGLegacyActionRunnerAdapter();
        var selectionAuthority = new DRPGBasicAttackSelectionAuthority(
            identityAuthority,
            selectionState,
            trace);
        var router = new DRPGBattleActionExecutionRouter(
            policy,
            selectionAuthority,
            legacyRunner,
            standardRunner,
            identityAuthority,
            arbiter);
        return new DRPGBattleSessionComposition(
            generation,
            active.AsReadOnly(),
            reserve.AsReadOnly(),
            resolvedScheduler,
            identityAuthority,
            trace,
            arbiter,
            state,
            resolvedProgressPipeline,
            selectionState,
            presentationSlot,
            standardRunner,
            legacyRunner,
            router,
            policy, wavePlan, wavePolicy);
    }

    private static void ValidateParticipants(
        IReadOnlyList<BattleParticipantRuntime> active,
        IReadOnlyList<BattleParticipantRuntime> reserve)
    {
        if (active.Count == 0)
        {
            throw new ArgumentException(
                "At least one participant is required.",
                nameof(active));
        }
        var ids = new HashSet<int>();
        for (int i = 0; i < active.Count; i++)
        {
            BattleParticipantRuntime participant = active[i];
            if (participant == null)
                throw new ArgumentException("An active participant is missing.");
            if (!ids.Add(participant.RuntimeId))
            {
                throw new ArgumentException(
                    "Duplicate runtime ID " + participant.RuntimeId + ".");
            }
        }
        for (int i = 0; i < reserve.Count; i++)
        {
            BattleParticipantRuntime participant = reserve[i];
            if (participant == null)
                throw new ArgumentException("A reserve enemy is missing.");
            if (participant.Side != DRPGBattleSide.Enemy)
                throw new ArgumentException("Only enemies may be reserved.");
            if (!ids.Add(participant.RuntimeId))
            {
                throw new ArgumentException(
                    "Duplicate runtime ID " + participant.RuntimeId + ".");
            }
        }
    }
}
