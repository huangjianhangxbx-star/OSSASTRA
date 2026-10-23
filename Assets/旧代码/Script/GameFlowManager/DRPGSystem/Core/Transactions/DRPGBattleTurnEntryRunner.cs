using System;

internal interface IBattleTurnEntryPresentationScope : IDisposable
{
    IBattleTurnEntryPresentationPort Port { get; }
    bool IsActive { get; }
}

internal interface IBattleTurnEntryPresentationFactory
{
    bool TryOpen(BattleTransactionIdentity owner, out IBattleTurnEntryPresentationScope scope, out string error);
}

// Session-owned driver. The module owns presentation sequencing; this driver
// owns terminal submission and delivery on separate external ticks.
internal sealed class DRPGBattleTurnEntryRunner : IBattleTurnEntryContinuationRunner, IBattleStartContinuationRunner, IBattleRootContinuationRunner, IBattleWaveContinuationRunner, IBattleLegacyContinuationRunner
{
    private readonly DRPGBattleSession session;
    private readonly DRPGBattleIdentityAuthority identities;
    private readonly DRPGBattleContinuationInbox inbox;
    private readonly DRPGBattleContinuationRouter router;
    private DRPGBattleTurnEntryModule module;
    private BattleRuleMutationLease lease;
    private IDisposable presentationScope;
    private DRPGTurnEntrySettlementCompletion submitted;
    private bool ticking, awaitingDelivery;
    private IBattleTurnEntryPresentationFactory presentationFactory;
    private DRPGTurnCandidateReservation nextCandidate;
    private TurnEntryIdentity nextEntry;
    private DRPGBattleStartupModule startup;
    private bool abortRequested, closed;
    private readonly DRPGEnemyWaveLifecycle waves;
    private readonly DRPGEnemyWavePolicySnapshot? wavePolicy;
    private DRPGEnemyWavePresentationRun wave;
    private IBattleWavePresentationScope waveScope;
    private System.Collections.Generic.IReadOnlyList<BattleParticipantRuntime> waveMembers;
    private WaveTransitionIdentity nextWave;
    private bool waveAdopted;
    private IStandardBasicAttackPresentationPort waveBinding;
    private bool legacyPending;
    private LegacyActionCompletion legacyCompletion;

    internal bool RequestAbort()
    {
        if (closed || !IsBusy) return false;
        // A user cancellation must not turn an already failed transaction into
        // a successful domain BattleEnded notification.
        if (Fault.Length != 0) return true;
        abortRequested = true;
        return true;
    }

    internal BattleStartupPrepareStatus PrepareStart(IDRPGBattlePresentationClock clock)
    {
        if (closed || abortRequested) return BattleStartupPrepareStatus.Closed;
        if (startup != null) return startup.PrepareStart();
        if (IsBusy || session.LifecycleState != DRPGBattleLifecycleState.Inactive) return BattleStartupPrepareStatus.Closed;
        if (presentationFactory == null || clock == null ||
            !session.TransactionPresentationPortSlot.TryGet(identities.BattleGeneration, out var port) || !(port is IBattleStartPresentationPort))
            return BattleStartupPrepareStatus.PresentationUnavailable;
        var pending = new DRPGBattleStartupModule(identities, session.TransactionArbiter,
            session.TransactionPresentationPortSlot, inbox, clock);
        var status = pending.PrepareStart();
        if (status != BattleStartupPrepareStatus.Prepared) return status;
        startup = pending;
        if (!session.TransactionArbiter.TryCaptureCurrentLease(BattleTransactionIdentity.ForBattleStart(startup.Identity), out lease))
        { Fault = "Prepared startup lost its lease."; return BattleStartupPrepareStatus.Closed; }
        try { session.InitializePreparedStartup(lease); }
        catch (Exception error) { Fault = error.ToString(); return BattleStartupPrepareStatus.Closed; }
        return status;
    }

    internal bool EnqueuePreparedStartup(BattleStartIdentity identity) =>
        Fault.Length == 0 && startup != null && startup.EnqueuePreparedStartup(identity);

    internal bool TryBindPresentationFactory(IBattleTurnEntryPresentationFactory factory)
    {
        if (factory == null || ticking || presentationFactory != null && !ReferenceEquals(presentationFactory, factory)) return false;
        presentationFactory = factory;
        return true;
    }

    internal DRPGBattleTurnEntryRunner(DRPGBattleSession session,
        DRPGBattleIdentityAuthority identities, DRPGBattleContinuationInbox inbox,
        DRPGEnemyWaveLifecycle waves = null, DRPGEnemyWavePolicySnapshot? wavePolicy = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.identities = identities ?? throw new ArgumentNullException(nameof(identities));
        this.inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        this.waves = waves; this.wavePolicy = wavePolicy;
        router = new DRPGBattleContinuationRouter(inbox, this, this, this, this, this);
    }

    internal string Fault { get; private set; } = string.Empty;
    internal string CleanupError { get; private set; } = string.Empty;
    internal void PumpRoot()
    {
        router.Pump();
        if (router.Fault.Length != 0) Fault = router.Fault;
    }

    public bool TryPrepare(in RootActionCompletion completion, out BattleTransactionIdentity next, out string error)
    {
        next = completion.Owner; error = string.Empty;
        if (IsBusy || presentationFactory == null)
        { error = "Root continuation requires an idle, bound candidate runner."; return false; }
        // Retain the exact submitted lease even when validating the sealed
        // facts fails; cleanup must not guess an owner from the live roster.
        if (session.TransactionState.OwnsRuleMutationLease(completion.Lease, completion.Owner)) lease = completion.Lease;
        if (waves != null && (!inbox.TryGetSealed(completion.Owner, out var source) || !waves.TryObserveDeparture(source, out error)))
        { if (error.Length == 0) error = "Root departure was not sealed by this Inbox."; return false; }
        if (completion.Outcome == DRPGBattleOutcomeCandidate.WavePending && waves == null)
        { error = "Whole-wave continuation requires a frozen wave lifecycle."; return false; }
        if (!session.TryPrepareStandardContinuation(completion, out error)) return false;
        if (completion.Outcome == DRPGBattleOutcomeCandidate.WavePending)
        {
            nextWave = identities.AllocateWave(waves.CompletedWaveCount + 1);
            next = BattleTransactionIdentity.ForWave(nextWave); return true;
        }
        if (completion.Outcome != DRPGBattleOutcomeCandidate.Continue) return true;
        if (!session.TryReserveActionableTurnCandidate(completion.Lease, out nextCandidate, out error)) return false;
        nextEntry = identities.AllocateTurnEntry(new ParticipantId(nextCandidate.Candidate.Participant.RuntimeId));
        next = BattleTransactionIdentity.ForTurnEntry(nextEntry);
        return true;
    }

    public void Start(in RootActionCompletion completion, BattleRuleMutationLease activeLease)
    {
        if (completion.Outcome == DRPGBattleOutcomeCandidate.Continue) OpenNextCandidate(activeLease);
        else if (completion.Outcome == DRPGBattleOutcomeCandidate.WavePending) OpenWave(activeLease);
        else session.CompleteStandardResult(completion, activeLease);
    }
    internal bool IsBusy => !closed && (legacyPending || startup != null || wave != null || module != null || awaitingDelivery || Fault.Length != 0 || abortRequested);

    internal bool TryEnqueueLegacy(in LegacyActionCompletion completion, out string error)
    {
        error = string.Empty;
        if (IsBusy || !session.TransactionState.OwnsRuleMutationLease(completion.Lease, completion.Owner))
        { error = "Legacy continuation requires an idle runner and the exact physical owner."; return false; }
        var result = inbox.Submit(completion);
        if (!result.IsAccepted) { error = "Legacy completion was rejected by its Inbox."; return false; }
        lease = completion.Lease; legacyCompletion = completion; legacyPending = true; return true;
    }
    public bool TryPrepare(in LegacyActionCompletion completion, out BattleTransactionIdentity next, out string error)
    {
        next = completion.Owner; error = string.Empty;
        if (!legacyPending || !legacyCompletion.HasSameValues(completion))
        { error = "Legacy completion does not belong to this runner."; return false; }
        if (waves != null && (!inbox.TryGetSealed(completion.Owner, out var source) || !waves.TryObserveDeparture(source, out error)))
        { if (error.Length == 0) error = "Legacy departure was not sealed by this Inbox."; return false; }
        if (completion.WavePending && waves == null)
        { error = "Legacy wave continuation requires its frozen lifecycle."; return false; }
        if (!session.TryDeliverLegacyContinuation(completion, out error)) return false;
        if (completion.Outcome != DRPGBattleOutcome.None) return true;
        if (completion.WavePending)
        {
            nextWave = identities.AllocateWave(waves.CompletedWaveCount + 1);
            next = BattleTransactionIdentity.ForWave(nextWave); return true;
        }
        if (!session.TryReserveActionableTurnCandidate(lease, out nextCandidate, out error)) return false;
        nextEntry = identities.AllocateTurnEntry(new ParticipantId(nextCandidate.Candidate.Participant.RuntimeId));
        next = BattleTransactionIdentity.ForTurnEntry(nextEntry); return true;
    }
    public void Start(in LegacyActionCompletion completion, BattleRuleMutationLease activeLease)
    {
        legacyPending = false; legacyCompletion = default;
        if (completion.WavePending) OpenWave(activeLease);
        else if (completion.Outcome == DRPGBattleOutcome.None) OpenNextCandidate(activeLease);
        else { activeLease.Dispose(); lease = null; }
    }

    internal bool TryEnqueue(DRPGBattleTurnEntryModule candidate,
        BattleRuleMutationLease candidateLease, IDisposable scope, out string error)
    {
        error = string.Empty;
        if (IsBusy || candidate == null || candidateLease == null ||
            session.LifecycleState != DRPGBattleLifecycleState.TurnBegin ||
            candidateLease.Owner.Kind != BattleTransactionKind.TurnEntry ||
            !session.TransactionState.OwnsRuleMutationLease(candidateLease, candidateLease.Owner) ||
            !candidate.CanEnqueue(session.TransactionState, candidateLease))
        { error = "An idle runner and the active TurnEntry lease are required."; return false; }
        module = candidate;
        lease = candidateLease;
        presentationScope = scope;
        return true;
    }

    // True means this update belonged to the entry transaction; AI must not
    // start another action on the frame that delivers the new actor either.
    internal bool Tick()
    {
        if (!IsBusy) return false;
        if (ticking) return true;
        ticking = true;
        try
        {
            if (Fault.Length != 0) { CloseFault(); return true; }
            if (abortRequested)
            {
                if (!CloseWave("Session aborted.")) return true;
                if (startup != null)
                {
                    startup.Cancel("Session aborted.", true);
                    startup.Tick();
                    if (!startup.IsClosed) { if (startup.Fault.Length != 0) Fault = startup.Fault; return true; }
                }
                module?.Dispose();
                presentationScope?.Dispose();
                presentationScope = null;
                session.CompleteTurnFlowAbort(lease);
                startup = null; module = null; lease = null; closed = true;
                return true;
            }
            if (startup != null)
            {
                if (startup.CompletionSubmitted)
                {
                    router.Pump();
                    if (router.Fault.Length != 0) Fault = router.Fault;
                }
                else
                {
                    startup.Tick();
                    if (startup.Fault.Length != 0) Fault = startup.Fault;
                }
                return true;
            }
            if (awaitingDelivery || legacyPending)
            {
                router.Pump();
                if (router.Fault.Length != 0) Fault = router.Fault;
                return true;
            }
            if (wave != null)
            {
                if (!session.TransactionPresentationPortSlot.TryGet(identities.BattleGeneration, out var currentBinding) ||
                    !ReferenceEquals(currentBinding, waveBinding))
                    throw new InvalidOperationException("Wave presentation binding was removed or replaced.");
                if (wave.CompletionSubmitted)
                {
                    if (!waveAdopted)
                    {
                        if (!waveScope.TryAdoptDeployed()) throw new InvalidOperationException("Committed wave visual handoff failed.");
                        session.AdoptCommittedWave(waveMembers, lease);
                        waveScope.Dispose(); waveScope = null; waveAdopted = true;
                        return true;
                    }
                    router.Pump();
                    if (router.Fault.Length != 0) Fault = router.Fault;
                }
                else
                {
                    wave.Tick();
                    if (wave.Fault.Length != 0) Fault = wave.Fault;
                }
                return true;
            }
            module.Tick();
            if (module.Fault.Length != 0) { Fault = module.Fault; return true; }
            if (!module.ReadyForFinalization) return true;
            if (!session.TransactionState.TurnCandidates.TryFinalize(module, identities, out submitted, out var error))
            { Fault = error; return true; }
            var result = inbox.Submit(submitted);
            if (!result.IsAccepted) { Fault = "The candidate completion was rejected by its inbox."; return true; }
            awaitingDelivery = true;
            // Never pump on the submission stack, even for an empty entry.
            return true;
        }
        catch (Exception error) { Fault = error.ToString(); return true; }
        finally { ticking = false; }
    }

    private void CloseFault()
    {
        // Do not retry the failed candidate/continuation. Only its owned cleanup
        // is retryable, and no observer is notified while a scope is still live.
        abortRequested = false;
        try
        {
            if (!CloseWave(Fault)) return;
            if (startup != null)
            {
                startup.Cancel(Fault, true);
                startup.Tick();
                if (!startup.IsClosed) { CleanupError = startup.Fault; return; }
            }
            module?.Dispose();
            presentationScope?.Dispose();
            if (presentationScope is IBattleTurnEntryPresentationScope active && active.IsActive)
            { CleanupError = "Candidate projection did not confirm closure."; return; }
            presentationScope = null;
            if (lease != null && lease.Owner.Kind == BattleTransactionKind.RootAction &&
                !session.TryCleanRootContinuationFault(lease, Fault, out var rootCleanupError))
            { CleanupError = rootCleanupError; return; }
            if (lease == null || !session.TransactionState.OwnsRuleMutationLease(lease, lease.Owner))
            { CleanupError = "Fault cleanup requires its original physical mutation lease."; return; }
            var owned = lease;
            // Seal the driver before Session publishes synchronous observers.
            startup = null; module = null; lease = null; awaitingDelivery = false;
            submitted = default; nextCandidate = default; nextEntry = default;
            closed = true;
            session.CompleteTurnFlowFault(owned, Fault);
            CleanupError = string.Empty;
        }
        catch (Exception error) { CleanupError = error.ToString(); }
    }

    public bool TryPrepare(in BattleStartCompletion completion, out BattleTransactionIdentity next, out string error)
    {
        next = default; error = string.Empty;
        if (startup == null || !startup.CompletionSubmitted || completion.Identity != startup.Identity ||
            module != null || presentationFactory == null)
        { error = "Startup completion does not belong to this Session runner."; return false; }
        if (waves != null)
        {
            nextWave = identities.AllocateWave(waves.CompletedWaveCount + 1);
            next = BattleTransactionIdentity.ForWave(nextWave);
            return true;
        }
        if (!session.TryBeginStartupCandidate(lease, out nextCandidate, out error)) return false;
        nextEntry = identities.AllocateTurnEntry(new ParticipantId(nextCandidate.Candidate.Participant.RuntimeId));
        next = BattleTransactionIdentity.ForTurnEntry(nextEntry);
        return true;
    }

    public void Start(in BattleStartCompletion completion, BattleRuleMutationLease activeLease)
    {
        if (startup == null || !startup.CompletionSubmitted || completion.Identity != startup.Identity ||
            activeLease == null || activeLease.Owner != (waves != null ? BattleTransactionIdentity.ForWave(nextWave) : BattleTransactionIdentity.ForTurnEntry(nextEntry)))
            throw new InvalidOperationException("Startup delivery does not belong to this Session runner.");
        startup.Cancel("Entry handed off.");
        startup = null;
        if (waves != null) { OpenWave(activeLease); return; }
        OpenNextCandidate(activeLease);
    }

    private void OpenWave(BattleRuleMutationLease activeLease)
    {
        lease = activeLease;
        if (!wavePolicy.HasValue || !session.TransactionPresentationPortSlot.TryGet(identities.BattleGeneration, out var bound) ||
            !(bound is IBattleWavePresentationFactory factory))
            throw new InvalidOperationException("Wave presentation is not bound to this battle.");
        waveMembers = waves.NextMembers();
        waveBinding = bound;
        bool opened = factory.TryOpen(lease.Owner, waveMembers, session.WavePerRowLimit, out waveScope, out var error);
        if (!opened || waveScope == null || !waveScope.IsActive) throw new InvalidOperationException(error);
        wave = new DRPGEnemyWavePresentationRun(waves, lease, waveScope.Port, waveScope.Clock,
            wavePolicy.Value, session.WavePerRowLimit, session.WaveDeploymentTime, 10);
        waveAdopted = false;
    }

    private bool CloseWave(string reason)
    {
        if (wave != null && !wave.CompletionSubmitted)
        {
            wave.Cancel(reason); wave.Tick();
            if (!wave.FaultCleanupCompleted) { CleanupError = "Wave cleanup has not completed."; return false; }
        }
        waveScope?.Dispose();
        if (waveScope != null && waveScope.IsActive) { CleanupError = "Wave projection remains active."; return false; }
        waveScope = null; wave = null; waveMembers = null; waveBinding = null;
        return true;
    }

    public bool TryPrepare(in WaveTransitionCompletion completion, out BattleTransactionIdentity next, out string error)
    {
        next = default; error = string.Empty;
        if (wave == null || !wave.CompletionSubmitted || !waveAdopted || completion.Identity != nextWave ||
            !ReferenceEquals(completion.Lease, lease) || completion.FinalBattleRevision != session.TransactionState.BattleRevision)
        { error = "Wave completion does not match this runner's committed presentation."; return false; }
        if (!session.TryBeginWaveCandidate(lease, out nextCandidate, out error)) return false;
        nextEntry = identities.AllocateTurnEntry(new ParticipantId(nextCandidate.Candidate.Participant.RuntimeId));
        next = BattleTransactionIdentity.ForTurnEntry(nextEntry); return true;
    }
    public void Start(in WaveTransitionCompletion completion, BattleRuleMutationLease activeLease)
    {
        if (activeLease == null || activeLease.Owner != BattleTransactionIdentity.ForTurnEntry(nextEntry))
            throw new InvalidOperationException("Wave candidate transfer lost ownership.");
        wave = null; waveMembers = null; nextWave = default; waveAdopted = false; waveBinding = null;
        OpenNextCandidate(activeLease);
    }

    private void OpenNextCandidate(BattleRuleMutationLease activeLease)
    {
        lease = activeLease;
        if (!identities.TryBeginTurnEntry(nextEntry, lease))
            throw new InvalidOperationException("The next candidate identity could not acquire its transferred lease.");
        // Even a rejected construction can return owned resources. Adopt them
        // before validation so a throwing cleanup remains retryable by CloseFault.
        bool opened = presentationFactory.TryOpen(lease.Owner, out var scope, out var openError);
        presentationScope = scope;
        if (!opened) throw new InvalidOperationException(openError);
        if (scope == null || !scope.IsActive || scope.Port == null || scope.Port.Clock == null ||
            !session.TransactionState.OwnsRuleMutationLease(lease, lease.Owner))
        {
            throw new InvalidOperationException("Candidate presentation scope is missing or lost ownership during creation.");
        }
        module = new DRPGBattleTurnEntryModule(session.TransactionState, lease, nextCandidate,
            scope.Port, scope.Port.Clock, () => scope.IsActive);
        awaitingDelivery = false;
        submitted = default;
        nextCandidate = default;
        nextEntry = default;
    }

    public bool TryPrepare(in DRPGTurnEntrySettlementCompletion completion,
        out BattleTransactionIdentity next, out string error)
    {
        next = completion.Owner;
        error = string.Empty;
        if (!awaitingDelivery || !submitted.HasSameValues(completion))
        { error = "The candidate completion no longer belongs to this runner."; return false; }
        if (waves != null && (!inbox.TryGetSealed(completion.Owner, out var source) || !waves.TryObserveDeparture(source, out error)))
        { if (error.Length == 0) error = "TurnEntry departure was not sealed by this Inbox."; return false; }
        if (completion.Kind == DRPGTurnEntrySettlementCompletionKind.WavePending)
        {
            if (waves == null) { error = "TurnEntry requires a frozen wave lifecycle."; return false; }
            nextWave = identities.AllocateWave(waves.CompletedWaveCount + 1);
            next = BattleTransactionIdentity.ForWave(nextWave); return true;
        }
        if (completion.Kind == DRPGTurnEntrySettlementCompletionKind.ActorStillEligible ||
            completion.Kind == DRPGTurnEntrySettlementCompletionKind.BattleResult) return true;
        if (completion.Kind != DRPGTurnEntrySettlementCompletionKind.SelectAgain || presentationFactory == null)
        { error = "This entry requires a bound candidate/result continuation before it can proceed."; return false; }
        if (!session.TryReserveActionableTurnCandidate(lease, out nextCandidate, out error)) return false;
        nextEntry = identities.AllocateTurnEntry(new ParticipantId(nextCandidate.Candidate.Participant.RuntimeId));
        next = BattleTransactionIdentity.ForTurnEntry(nextEntry);
        return true;
    }

    public void Start(in DRPGTurnEntrySettlementCompletion completion, BattleRuleMutationLease activeLease)
    {
        bool selectingAgain = completion.Kind == DRPGTurnEntrySettlementCompletionKind.SelectAgain;
        bool nextBatch = completion.Kind == DRPGTurnEntrySettlementCompletionKind.WavePending;
        if (!awaitingDelivery || !submitted.HasSameValues(completion) || activeLease == null ||
            (!selectingAgain && !nextBatch && !ReferenceEquals(activeLease, lease)) ||
            (nextBatch && activeLease.Owner != BattleTransactionIdentity.ForWave(nextWave)) ||
            (selectingAgain && activeLease.Owner != BattleTransactionIdentity.ForTurnEntry(nextEntry)))
            throw new InvalidOperationException("The candidate delivery no longer belongs to this runner.");
        module.Dispose();
        presentationScope?.Dispose();
        presentationScope = null;
        if (nextBatch)
        {
            module = null; awaitingDelivery = false; submitted = default;
            OpenWave(activeLease); return;
        }
        if (selectingAgain)
        {
            OpenNextCandidate(activeLease);
            return;
        }
        string error;
        bool delivered = completion.Kind == DRPGTurnEntrySettlementCompletionKind.BattleResult
            ? session.TryPublishTurnEntryResult(completion, activeLease, out error)
            : session.TryPublishSettledTurnEntry(completion, activeLease, out error);
        if (!delivered)
            throw new InvalidOperationException(error);
        module = null;
        lease = null;
        awaitingDelivery = false;
        submitted = default;
    }
}
