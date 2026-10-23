using System;
using System.Collections.Generic;

// One State-owned reservation source shared by the legacy bridge and TurnEntry.
// Reserving never advances time; a physical TurnEntry pin forbids legacy consumption.
internal sealed class DRPGTurnCandidateAuthority
{
    private readonly DRPGBattleSessionState state;
    private readonly DRPGTurnScheduler scheduler;
    private readonly IReadOnlyDictionary<ParticipantId, BattleParticipantRuntime> participants;
    private long nextReservationId = 1;
    private DRPGTurnCandidateReservation active;
    private DRPGEnemyActionPlan reservedPlan;
    private BattleRuleMutationLease pinnedLease;
    private bool preparingFinalization;
    private bool committingFinalization;
    private DRPGBattleTurnEntryModule preparedModule;
    private DRPGTurnCandidateFinalizationFacts preparedFinalization;
    private DRPGBattleSession planningSession;
    private DRPGTurnEntrySettlementCompletion pendingTerminalCompletion;
    private DRPGBattleIdentityAuthority pendingTerminalAuthority;
    private long pendingTerminalRevision;
    private DRPGEnemyWavePlan waveOutcomePlan;
    internal void BindWaveOutcomePlan(DRPGEnemyWavePlan plan)
    {
        if (plan == null || waveOutcomePlan != null || HasReservation || state.BattleRevision != 0)
            throw new InvalidOperationException("Wave outcome context must be frozen once during composition.");
        waveOutcomePlan = plan;
    }

    internal bool TryTakeTerminalCompletion(in DRPGTurnEntrySettlementCompletion completion,
        BattleRuleMutationLease lease, DRPGBattleIdentityAuthority identities)
    {
        if ((completion.Kind != DRPGTurnEntrySettlementCompletionKind.ActorStillEligible &&
             completion.Kind != DRPGTurnEntrySettlementCompletionKind.BattleResult) ||
            !pendingTerminalCompletion.HasSameValues(completion) ||
            !ReferenceEquals(pendingTerminalAuthority, identities) ||
            !ReferenceEquals(completion.Lease, lease) ||
            !state.OwnsRuleMutationLease(lease, completion.Owner) ||
            state.BattleRevision != pendingTerminalRevision || HasReservation)
            return false;
        pendingTerminalCompletion = default;
        pendingTerminalAuthority = null;
        return true;
    }
    internal void BindPlanningSession(DRPGBattleSession session)
    {
        if (session == null || !ReferenceEquals(session.TransactionState, state) ||
            planningSession != null && !ReferenceEquals(planningSession, session))
            throw new InvalidOperationException("Candidate planning must bind to its owning Session.");
        planningSession = session;
    }
    internal DRPGTurnCandidateAuthority(DRPGBattleSessionState state, DRPGTurnScheduler scheduler,
        IReadOnlyDictionary<ParticipantId, BattleParticipantRuntime> participants)
    { this.state = state; this.scheduler = scheduler; this.participants = participants; }
    // Existing Session planning code uses this same table during migration;
    // there is no second dictionary reconstructed after the entry completes.
    internal Dictionary<int, DRPGEnemyActionPlan> Plans { get; } = new Dictionary<int, DRPGEnemyActionPlan>();
    internal Dictionary<int, float> PendingTimelineDeltas { get; } = new Dictionary<int, float>();
    internal Dictionary<int, int> PendingTimelinePriorities { get; } = new Dictionary<int, int>();
    internal bool HasReservation => active.IsValid;
    internal void CalculateNextSchedule(BattleParticipantRuntime actor, DRPGResolvedActionSpec spec,
        out float readyAt, out int priority)
    {
        float delay = scheduler.CalculateNextDelayFromSpec(actor, spec, null);
        PendingTimelineDeltas.TryGetValue(actor.RuntimeId, out float delta);
        PendingTimelinePriorities.TryGetValue(actor.RuntimeId, out priority);
        readyAt = Math.Max(scheduler.CurrentTime + .001f, scheduler.CurrentTime + delay + delta);
    }
    internal bool TryPrepareFinalization(DRPGBattleTurnEntryModule module,
        out DRPGTurnCandidateFinalizationFacts facts, out string error)
    {
        facts = null; error = string.Empty;
        if (preparingFinalization || module == null || !HasReservation || pinnedLease == null ||
            !state.OwnsRuleMutationLease(pinnedLease, pinnedLease.Owner) || !PlanStillMatches() ||
            preparedModule != null && !ReferenceEquals(preparedModule, module))
        { error = "Finalization requires the original pinned candidate and module."; return false; }
        try
        {
            preparingFinalization = true;
            // Readiness is not a caller-supplied bool. The issuing module checks
            // its original State, physical lease, reservation and live render scope.
            if (!module.TryAuthorizeFinalization(state, pinnedLease, active))
            { error = "The candidate's final rendered gates are unavailable or stale."; return false; }
            if (!PlanStillMatches() || !state.OwnsRuleMutationLease(pinnedLease, pinnedLease.Owner))
            { error = "The candidate changed during finalization authorization."; return false; }
            if (preparedFinalization == null)
            {
                var snapshot = state.CaptureRuleSnapshot();
                var prepared = new DRPGTurnCandidateFinalizationFacts(pinnedLease.Owner,
                    new DRPGTurnEntryReservationFacts(active), module.Ledger.TurnEntryLifecycle,
                    snapshot, module.Reconciliation);
                preparedModule = module; preparedFinalization = prepared;
            }
            if (preparedFinalization.Snapshot.Revision != state.BattleRevision)
            { error = "The prepared candidate no longer describes current rule state."; return false; }
            facts = preparedFinalization; return true;
        }
        finally { preparingFinalization = false; }
    }
    internal bool TryFinalize(DRPGBattleTurnEntryModule module,
        DRPGBattleIdentityAuthority identities,
        out DRPGTurnEntrySettlementCompletion completion, out string error)
    {
        completion = default; error = string.Empty;
        if (!TryPrepareFinalization(module, out var facts, out error) ||
            identities == null ||
            !identities.CanPublishTurnEntry(facts.Owner.TurnEntry, pinnedLease))
        {
            if (error.Length == 0)
                error = "Eligible terminal requires its exact prepared candidate and identity authority.";
            return false;
        }

        var finalLease = pinnedLease;
        bool playerAlive = false, enemyAlive = false;
        foreach (var participant in facts.Snapshot.Participants)
        {
            if (!participant.IsDeployed || participant.CurrentHp <= 0) continue;
            if (participant.Side == DRPGBattleSide.Player) playerAlive = true;
            if (participant.Side == DRPGBattleSide.Enemy) enemyAlive = true;
        }
        // Player defeat is decisive even when the final enemy also died.
        // Ownership stays with the outer continuation until it routes the result.
        if (!playerAlive)
        {
            if (!identities.TryDiscardTurnEntry(facts.Owner.TurnEntry, pinnedLease))
            { error = "The defeated candidate identity changed."; return false; }
            Plans.Remove(facts.Reservation.Actor.Value);
            ClearReservation();
            completion = new DRPGTurnEntrySettlementCompletion(
                DRPGTurnEntrySettlementCompletionKind.BattleResult, facts, default, DRPGBattleOutcome.Defeat, finalLease, state.BattleRevision);
            pendingTerminalCompletion = completion;
            pendingTerminalAuthority = identities;
            pendingTerminalRevision = state.BattleRevision;
            return true;
        }
        // An empty field does not prove that no later wave exists. The wave
        // journal/outcome route must be bound before this branch can continue.
        if (!enemyAlive)
        {
            if (waveOutcomePlan == null)
            { error = "Enemy field is empty; wave outcome context is required before candidate publication."; return false; }
            bool reservesAlive = false;
            foreach (var participant in facts.Snapshot.Participants)
                if (participant.Side == DRPGBattleSide.Enemy && !participant.IsDeployed && participant.CurrentHp > 0)
                    reservesAlive = true;
            if (!identities.TryDiscardTurnEntry(facts.Owner.TurnEntry, pinnedLease))
            { error = "The final wave candidate identity changed."; return false; }
            Plans.Remove(facts.Reservation.Actor.Value);
            PendingTimelineDeltas.Remove(facts.Reservation.Actor.Value);
            PendingTimelinePriorities.Remove(facts.Reservation.Actor.Value);
            ClearReservation();
            completion = new DRPGTurnEntrySettlementCompletion(reservesAlive ? DRPGTurnEntrySettlementCompletionKind.WavePending :
                DRPGTurnEntrySettlementCompletionKind.BattleResult, facts, default,
                reservesAlive ? DRPGBattleOutcome.None : DRPGBattleOutcome.Victory, finalLease, state.BattleRevision);
            if (!reservesAlive)
            {
                pendingTerminalCompletion = completion; pendingTerminalAuthority = identities;
                pendingTerminalRevision = state.BattleRevision;
            }
            return true;
        }
        if (!facts.Lifecycle.CandidateAlive)
        {
            if (!identities.TryDiscardTurnEntry(facts.Owner.TurnEntry, pinnedLease))
            { error = "The departed candidate identity changed."; return false; }
            Plans.Remove(facts.Reservation.Actor.Value);
            PendingTimelineDeltas.Remove(facts.Reservation.Actor.Value);
            PendingTimelinePriorities.Remove(facts.Reservation.Actor.Value);
            ClearReservation();
            completion = new DRPGTurnEntrySettlementCompletion(
                DRPGTurnEntrySettlementCompletionKind.SelectAgain, facts, default, DRPGBattleOutcome.None, finalLease, state.BattleRevision);
            return true;
        }
        if (facts.Lifecycle.CandidateAlive && facts.Lifecycle.SkipAction && reservedPlan != null &&
            reservedPlan.Skill != null && reservedPlan.Skill.PostureBreakInterruptionPolicy == DRPGPostureBreakInterruptionPolicy.Uninterruptible)
        {
            // Preserve the original target, action and preparation time. The
            // next candidate entry will perform its own recovery presentation.
            var retry = reservedPlan.MarkExecuting().ReturnToExecutableAfterForcedSkip();
            if (!identities.TryDiscardTurnEntry(facts.Owner.TurnEntry, pinnedLease))
            { error = "The skipped candidate identity changed."; return false; }
            Plans[retry.ActorRuntimeId] = retry;
            ClearReservation();
            completion = new DRPGTurnEntrySettlementCompletion(
                DRPGTurnEntrySettlementCompletionKind.SelectAgain, facts, default, DRPGBattleOutcome.None, finalLease, state.BattleRevision);
            return true;
        }
        if (facts.Lifecycle.CandidateAlive && facts.Lifecycle.SkipAction)
        {
            var actor = active.Candidate.Participant;
            CalculateNextSchedule(actor, null, out float readyAt, out int priority);
            DRPGEnemyActionPlan nextPlan = null;
            if (actor.Side == DRPGBattleSide.Enemy)
            {
                if (planningSession == null || !planningSession.TryPrepareSkippedEnemyPlan(actor, reservedPlan,
                    out nextPlan, out readyAt, out priority))
                { error = "Ordinary enemy skip requires the owning Session planning policy."; return false; }
                if (!TryPrepareFinalization(module, out _, out error) ||
                    !identities.CanPublishTurnEntry(facts.Owner.TurnEntry, pinnedLease) ||
                    !planningSession.CanCommitPreparedEnemyPlan(nextPlan))
                { if (error.Length == 0) error = "Candidate changed while preparing the ordinary enemy plan."; return false; }
            }
            var draft = state.CreateWorkspace();
            draft.ScheduleAt(facts.Reservation.Actor, readyAt, priority);
            var committed = state.TryCommitJournal(pinnedLease, draft);
            if (!committed.Succeeded) { error = committed.Error; return false; }
            if (!identities.TryDiscardTurnEntry(facts.Owner.TurnEntry, pinnedLease))
                throw new InvalidOperationException("A preflighted skipped candidate identity failed discard.");
            if (actor.Side == DRPGBattleSide.Enemy)
            {
                planningSession.CommitPreparedEnemyPlanId(nextPlan);
                if (nextPlan == null) Plans.Remove(actor.RuntimeId);
                else Plans[actor.RuntimeId] = nextPlan;
            }
            PendingTimelineDeltas.Remove(actor.RuntimeId);
            PendingTimelinePriorities.Remove(actor.RuntimeId);
            ClearReservation();
            completion = new DRPGTurnEntrySettlementCompletion(
                DRPGTurnEntrySettlementCompletionKind.SelectAgain, facts, default, DRPGBattleOutcome.None, finalLease, state.BattleRevision);
            return true;
        }
        if (!module.CandidateStillEligible)
        { error = "Candidate skip or death requires rescheduling before continuation."; return false; }

        if (active.SourceKind == DRPGTurnCandidateSourceKind.SchedulerTimeline)
        {
            // TurnStart may have moved the candidate behind another entry.
            // Consume its current row through the revision journal, without
            // advancing time a second time or consulting the new queue head.
            committingFinalization = true;
            try
            {
                if (!state.TryConsumeTurnEntryTimelineCandidate(pinnedLease, active, out error)) return false;
            }
            finally { committingFinalization = false; }
        }
        else
        {
            if (!PlanStillMatches())
            {
                error = "The executable enemy plan changed before terminal consumption.";
                return false;
            }
            Plans[reservedPlan.ActorRuntimeId] = reservedPlan.MarkExecuting();
        }

        var owner = facts.Owner;
        ClearReservation();
        if (!identities.TryPublishTurnEntry(owner.TurnEntry, out var turn))
            throw new InvalidOperationException("A preflighted TurnEntry identity failed publication.");
        completion = new DRPGTurnEntrySettlementCompletion(
            DRPGTurnEntrySettlementCompletionKind.ActorStillEligible,
            facts,
            turn,
            DRPGBattleOutcome.None, finalLease, state.BattleRevision);
        pendingTerminalCompletion = completion;
        pendingTerminalAuthority = identities;
        pendingTerminalRevision = state.BattleRevision;
        return true;
    }
    // An unpinned legacy reservation does not acquire rule authority. Once pinned,
    // every subsequent commit and presentation tick must retain the same plan.
    internal bool AllowsPinnedWork(BattleRuleMutationLease lease) => pinnedLease == null ||
        ReferenceEquals(pinnedLease, lease) && PlanStillMatches();
    internal bool IsCommittingFinalization(BattleRuleMutationLease lease, DRPGTurnCandidateReservation reservation) =>
        committingFinalization && ReferenceEquals(lease, pinnedLease) && Matches(reservation) && PlanStillMatches();
    internal bool TryReserve(out DRPGTurnCandidateReservation reservation, out string error)
    {
        reservation = default; error = string.Empty;
        if (HasReservation) { error = "A turn candidate reservation is already active."; return false; }
        TryPeekExecutable(out var selected);
        DRPGTurnScheduleEntry candidate; DRPGTurnCandidateSourceKind source; double time;
        if (selected != null)
        {
            if (!participants.TryGetValue(new ParticipantId(selected.ActorRuntimeId), out var actor))
            { error = "The executable enemy plan has no State participant."; return false; }
            candidate = new DRPGTurnScheduleEntry(actor, selected.ReadyAtTime, 0);
            source = DRPGTurnCandidateSourceKind.ExecutableEnemyPlan; time = scheduler.CurrentTime;
        }
        else
        {
            var head = scheduler.PeekNextEntry();
            if (!head.HasValue) { error = "The scheduler has no turn candidate."; return false; }
            candidate = head.Value; source = DRPGTurnCandidateSourceKind.SchedulerTimeline; time = candidate.ScheduledTime;
        }
        if (!Finite(candidate.ScheduledTime) || candidate.ScheduledTime < 0 || double.IsNaN(time) || double.IsInfinity(time) || time < 0)
        { error = "The candidate source has an invalid time."; return false; }
        long id = nextReservationId; nextReservationId = checked(nextReservationId + 1);
        reservation = new DRPGTurnCandidateReservation(id, source, candidate, time);
        active = reservation; reservedPlan = selected; return true;
    }
    internal bool TryPin(BattleRuleMutationLease lease, DRPGTurnCandidateReservation reservation, out string error)
    {
        error = string.Empty;
        if (!Matches(reservation) || lease == null || lease.Owner.Kind != BattleTransactionKind.TurnEntry ||
            lease.Owner.TurnEntry.Actor.Value != active.Candidate.Participant.RuntimeId || !state.OwnsRuleMutationLease(lease, lease.Owner) ||
            pinnedLease != null && !ReferenceEquals(pinnedLease, lease) || !PlanStillMatches())
        { error = "Candidate pin requires its exact reservation, plan and original active State lease."; return false; }
        if (pinnedLease == null && !state.ValidateTurnEntryReservation(reservation, out error)) return false;
        pinnedLease = lease; return true;
    }
    internal bool Matches(DRPGTurnCandidateReservation reservation) => HasReservation && reservation.IsValid &&
        reservation.ReservationId == active.ReservationId && reservation.SourceKind == active.SourceKind &&
        ReferenceEquals(reservation.Candidate.Participant, active.Candidate.Participant) &&
        reservation.Candidate.ScheduledTime == active.Candidate.ScheduledTime && reservation.Candidate.Priority == active.Candidate.Priority &&
        reservation.ReservedLogicalTime == active.ReservedLogicalTime;
    internal bool TryCommitLegacy(DRPGTurnCandidateReservation reservation, out DRPGEnemyActionPlan plan, out string error)
    {
        plan = null; error = string.Empty;
        if (!Matches(reservation) || pinnedLease != null)
        { error = "The candidate is stale or owned by TurnEntry, not the legacy bridge."; return false; }
        if (!PlanStillMatches()) { error = "The exact reserved enemy plan changed before commit."; return false; }
        if (active.SourceKind == DRPGTurnCandidateSourceKind.ExecutableEnemyPlan) Plans.Remove(reservedPlan.ActorRuntimeId);
        else if (!scheduler.TryConsumeReservedEntry(active.Candidate))
        { error = "The reserved scheduler entry changed before commit."; return false; }
        plan = reservedPlan; ClearReservation(); return true;
    }
    internal bool TryDiscardLegacy(DRPGTurnCandidateReservation reservation, out string error)
    {
        error = string.Empty;
        if (!Matches(reservation) || pinnedLease != null)
        { error = "The candidate is stale or owned by TurnEntry, not legacy discard."; return false; }
        ClearReservation(); return true;
    }
    internal void ResetLegacy()
    {
        if (pinnedLease != null) throw new InvalidOperationException("Legacy startup cannot reset a TurnEntry reservation.");
        ClearReservation();
        // Even legacy restart must not reuse a reservation retained by a callback.
    }
    private void ClearReservation()
    { active = default; reservedPlan = null; pinnedLease = null; preparedModule = null; preparedFinalization = null; }
    internal bool TryPeekExecutable(out DRPGEnemyActionPlan selected)
    {
        selected = null;
        foreach (var plan in Plans.Values)
            if (plan.State == DRPGEnemyPlanState.Executable && (selected == null || Compare(plan, selected) < 0)) selected = plan;
        return selected != null;
    }
    private bool PlanStillMatches() => active.SourceKind != DRPGTurnCandidateSourceKind.ExecutableEnemyPlan ||
        reservedPlan != null && reservedPlan.State == DRPGEnemyPlanState.Executable &&
        Plans.TryGetValue(reservedPlan.ActorRuntimeId, out var plan) && ReferenceEquals(plan, reservedPlan);
    private int Compare(DRPGEnemyActionPlan a, DRPGEnemyActionPlan b)
    {
        int time = a.ReadyAtTime.CompareTo(b.ReadyAtTime); if (time != 0) return time;
        int left = participants.TryGetValue(new ParticipantId(a.ActorRuntimeId), out var leftActor) ? leftActor.FormationSlot : int.MaxValue;
        int right = participants.TryGetValue(new ParticipantId(b.ActorRuntimeId), out var rightActor) ? rightActor.FormationSlot : int.MaxValue;
        int slot = left.CompareTo(right); return slot != 0 ? slot : a.ActorRuntimeId.CompareTo(b.ActorRuntimeId);
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

internal enum DRPGTurnEntrySettlementCompletionKind
{
    None,
    ActorStillEligible,
    SelectAgain,
    BattleResult,
    WavePending
}

internal readonly struct DRPGTurnEntrySettlementCompletion : IBattleTransactionCompletionPayload
{
    internal DRPGTurnEntrySettlementCompletion(
        DRPGTurnEntrySettlementCompletionKind kind,
        DRPGTurnCandidateFinalizationFacts facts,
        ActorTurnToken turn,
        DRPGBattleOutcome outcome, BattleRuleMutationLease lease, long finalBattleRevision)
    {
        if (facts == null || facts.Snapshot == null || finalBattleRevision < facts.Snapshot.Revision)
            throw new ArgumentException("TurnEntry completion requires the final committed revision.");
        FinalBattleRevision = finalBattleRevision;
        Kind = kind;
        Owner = facts.Owner;
        Turn = turn;
        Outcome = outcome;
        Departures = facts.Reconciliation.Departures;
        Lease = lease;
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            hash = (hash ^ (ulong)finalBattleRevision) * 1099511628211UL;
            hash = (hash ^ Departures.SourceFingerprint) * 1099511628211UL;
            hash = (hash ^ (ulong)kind) * 1099511628211UL;
            hash = (hash ^ (ulong)turn.TurnSerial) * 1099511628211UL;
            hash = (hash ^ (ulong)outcome) * 1099511628211UL;
            Fingerprint = hash == 0 ? 1UL : hash;
        }
    }

    internal DRPGTurnEntrySettlementCompletionKind Kind { get; }
    BattleTransactionCompletionKind IBattleTransactionCompletionPayload.Kind => BattleTransactionCompletionKind.TurnEntry;
    public BattleTransactionIdentity Owner { get; }
    public DRPGBattleDepartureJournal Departures { get; }
    public long FinalBattleRevision { get; }
    public ulong Fingerprint { get; }
    internal BattleRuleMutationLease Lease { get; }
    internal ActorTurnToken Turn { get; }
    internal DRPGBattleOutcome Outcome { get; }
    internal bool HasSameValues(in DRPGTurnEntrySettlementCompletion other) =>
        Owner == other.Owner && Kind == other.Kind && Turn == other.Turn && Outcome == other.Outcome &&
        FinalBattleRevision == other.FinalBattleRevision && Fingerprint == other.Fingerprint && ReferenceEquals(Lease, other.Lease) &&
        Departures != null && Departures.HasSameValues(other.Departures);
}

// Detached terminal input. This is not an outcome decision or permission to
// publish an actor; continuation must still complete its atomic terminal step.
internal sealed class DRPGTurnCandidateFinalizationFacts
{
    internal DRPGTurnCandidateFinalizationFacts(BattleTransactionIdentity owner,
        DRPGTurnEntryReservationFacts reservation, DRPGTurnEntryLifecycle lifecycle,
        BattleRuleSnapshot snapshot, DRPGBattlePostReturnResult reconciliation)
    { Owner = owner; Reservation = reservation; Lifecycle = lifecycle; Snapshot = snapshot; Reconciliation = reconciliation; }
    internal BattleTransactionIdentity Owner { get; }
    internal DRPGTurnEntryReservationFacts Reservation { get; }
    internal DRPGTurnEntryLifecycle Lifecycle { get; }
    internal BattleRuleSnapshot Snapshot { get; }
    internal DRPGBattlePostReturnResult Reconciliation { get; }
}
