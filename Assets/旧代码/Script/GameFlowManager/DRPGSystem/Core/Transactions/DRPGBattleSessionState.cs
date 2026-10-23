using System;
using System.Collections.Generic;

internal interface IDRPGBattleTransactionStatePort
{
    long BattleGeneration { get; }
    long BattleRevision { get; }
    BattleRuleSnapshot CaptureRuleSnapshot();
    BattleJournalCommitResult TryCommitJournal(
        BattleRuleMutationLease lease,
        DRPGBattleMutationWorkspace workspace);
}

internal sealed class DRPGBattleStateMutationCapability
{
    internal DRPGBattleStateMutationCapability(long battleGeneration)
    {
        if (battleGeneration <= 0L)
            throw new ArgumentOutOfRangeException(nameof(battleGeneration));
        BattleGeneration = battleGeneration;
    }

    internal long BattleGeneration { get; }
}

internal sealed class DRPGBattleSessionState : IDRPGBattleTransactionStatePort
{
    private readonly List<BattleParticipantRuntime> participants;
    private readonly Dictionary<ParticipantId, BattleParticipantRuntime>
        participantById;
    private readonly DRPGTurnScheduler scheduler;
    private readonly DRPGBattleTransactionArbiter arbiter;
    private readonly DRPGBattleStateMutationCapability mutationCapability;
    private readonly Dictionary<string, int> resources;
    private readonly DRPGBattleActionResourceBindings actionResources;
    private readonly Queue<Action> deferredNotifications = new Queue<Action>();
    private int compatibilityMutationScopeDepth;
    private DRPGBattleSettlementLedgerStore settlementStore;
    internal DRPGTurnCandidateAuthority TurnCandidates { get; }

    internal bool TryCommitTurnEntry(BattleRuleMutationLease lease, DRPGTurnCandidateReservation reservation,
        IReadOnlyDictionary<ParticipantId, int> homes, out DRPGBattleSettlementLedger ledger, out string error,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation = null)
    {
        ledger = null; error = string.Empty;
        if (lease == null || lease.Owner.Kind != BattleTransactionKind.TurnEntry || !arbiter.Owns(lease, lease.Owner) ||
            !reservation.IsValid || !participantById.TryGetValue(lease.Owner.TurnEntry.Actor, out var actor) ||
            !ReferenceEquals(actor, reservation.Candidate.Participant) ||
            double.IsNaN(reservation.ReservedLogicalTime) || double.IsInfinity(reservation.ReservedLogicalTime) ||
            reservation.ReservedLogicalTime > float.MaxValue)
        { error = "TurnEntry requires its matching candidate, finite time and active State lease."; return false; }
        if (TurnCandidates.HasReservation && !TurnCandidates.TryPin(lease, reservation, out error)) return false;
        if (settlementStore == null) settlementStore = new DRPGBattleSettlementLedgerStore(this);
        // Replay is checked by the store before live eligibility: lethal commits
        // are replayable, but a newly requested dead candidate is not eligible.
        return settlementStore.TryCommit(lease, lease.Owner.TurnEntry.Actor, homes, out ledger, out error,
            continuation, reservation);
    }

    internal bool ValidateTurnEntryReservation(DRPGTurnCandidateReservation reservation, out string error)
    {
        error = string.Empty;
        var actor = reservation.Candidate.Participant;
        if (!actor.IsAlive || !actor.IsDeployed || reservation.ReservedLogicalTime < scheduler.CurrentTime)
        { error = "The candidate is ineligible or its reserved time is stale."; return false; }
        if (reservation.SourceKind == DRPGTurnCandidateSourceKind.SchedulerTimeline)
        {
            var next = scheduler.PeekNextEntry();
            if (!next.HasValue || !ReferenceEquals(next.Value.Participant, actor) ||
                next.Value.Priority != reservation.Candidate.Priority || next.Value.ScheduledTime != reservation.Candidate.ScheduledTime ||
                reservation.ReservedLogicalTime != next.Value.ScheduledTime)
            { error = "The reserved scheduler candidate changed before TurnEntry."; return false; }
            return true;
        }
        // The owning candidate port must additionally pin the executable plan;
        // State never owns or guesses Session's live enemy-plan dictionary.
        if (reservation.SourceKind == DRPGTurnCandidateSourceKind.ExecutableEnemyPlan &&
            actor.Side == DRPGBattleSide.Enemy && reservation.ReservedLogicalTime == scheduler.CurrentTime &&
            !float.IsNaN(reservation.Candidate.ScheduledTime) && !float.IsInfinity(reservation.Candidate.ScheduledTime) &&
            reservation.Candidate.ScheduledTime >= 0f) return true;
        error = "The executable-plan candidate has an invalid logical-time boundary."; return false;
    }

    internal bool TryReconcileSettlement(BattleRuleMutationLease lease, DRPGBattleSettlementLedger ledger,
        DRPGBattleDeparturePresentation departure, out DRPGBattlePostReturnResult result, out string error)
    {
        result = null; error = string.Empty;
        if (settlementStore == null) { error = "This State has no committed settlement."; return false; }
        return settlementStore.TryReconcile(lease, ledger, departure, out result, out error);
    }

    internal bool TryCommitSettlement(BattleRuleMutationLease lease, ParticipantId actor,
        IReadOnlyDictionary<ParticipantId, int> homeSlots, out DRPGBattleSettlementLedger ledger, out string error,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation = null)
    {
        if (settlementStore == null) settlementStore = new DRPGBattleSettlementLedgerStore(this);
        return settlementStore.TryCommit(lease, actor, homeSlots, out ledger, out error, continuation);
    }

    internal DRPGBattleSessionState(
        long battleGeneration,
        IEnumerable<BattleParticipantRuntime> participants,
        DRPGTurnScheduler scheduler,
        DRPGBattleTransactionArbiter arbiter,
        IReadOnlyDictionary<string, int> initialResources = null,
        DRPGBattleActionResourceBindings actionResources = null)
    {
        if (battleGeneration <= 0L)
            throw new ArgumentOutOfRangeException(nameof(battleGeneration));
        if (participants == null)
            throw new ArgumentNullException(nameof(participants));
        this.scheduler = scheduler ??
            throw new ArgumentNullException(nameof(scheduler));
        this.arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        if (arbiter.BattleGeneration != battleGeneration)
        {
            throw new ArgumentException(
                "The state and arbiter must belong to the same battle.",
                nameof(arbiter));
        }

        BattleGeneration = battleGeneration;
        BattleRevision = 0L;
        mutationCapability = new DRPGBattleStateMutationCapability(
            battleGeneration);
        this.participants = new List<BattleParticipantRuntime>();
        participantById = new Dictionary<
            ParticipantId,
            BattleParticipantRuntime>();
        foreach (BattleParticipantRuntime participant in participants)
        {
            if (participant == null)
                throw new ArgumentException("A participant is missing.", nameof(participants));
            var id = new ParticipantId(participant.RuntimeId);
            if (participantById.ContainsKey(id))
            {
                throw new ArgumentException(
                    "Duplicate participant " + id + ".",
                    nameof(participants));
            }
            this.participants.Add(participant);
            participantById.Add(id, participant);
        }

        resources = new Dictionary<string, int>(StringComparer.Ordinal);
        if (initialResources != null)
        {
            foreach (KeyValuePair<string, int> pair in initialResources)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0)
                {
                    throw new ArgumentException(
                        "Initial battle resources must be named and non-negative.",
                        nameof(initialResources));
                }
                resources.Add(pair.Key, pair.Value);
            }
        }

        this.actionResources = actionResources;
        actionResources?.BindParticipants(BattleGeneration, this.participants, mutationCapability);
        for (int i = 0; i < this.participants.Count; i++)
        {
            this.participants[i].ValidateStateMutationCapabilityAttach(
                mutationCapability);
        }
        this.scheduler.ValidateStateMutationCapabilityAttach(
            mutationCapability);

        for (int i = 0; i < this.participants.Count; i++)
        {
            this.participants[i].AttachStateMutationCapability(
                mutationCapability);
        }
        this.scheduler.AttachStateMutationCapability(mutationCapability);
        TurnCandidates = new DRPGTurnCandidateAuthority(this, this.scheduler, participantById);
    }

    public long BattleGeneration { get; }
    public long BattleRevision { get; private set; }

    internal void InitializeParticipantDeployment(
        ParticipantId participantId,
        bool isDeployed)
    {
        if (BattleRevision != 0L ||
            !participantById.TryGetValue(
                participantId,
                out BattleParticipantRuntime participant))
        {
            throw new InvalidOperationException(
                "Initial deployment can only configure a known fresh participant.");
        }
        participant.EnterStateMutationScope(mutationCapability);
        try
        {
            participant.TrySetDeployed(isDeployed);
        }
        finally
        {
            participant.ExitStateMutationScope(mutationCapability);
        }
    }

    public BattleRuleSnapshot CaptureRuleSnapshot()
    {
        var participantSnapshots = new BattleParticipantRuleSnapshot[
            participants.Count];
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            participantSnapshots[i] = new BattleParticipantRuleSnapshot(
                new ParticipantId(participant.RuntimeId),
                participant.CurrentHp,
                participant.EffectiveMaxHp,
                participant.CurrentMp,
                participant.EffectiveMaxMp,
                participant.CurrentPosture,
                participant.EffectiveMaxPosture,
                participant.CurrentShield,
                participant.FormationRow,
                participant.IsDeployed,
                participant.Statuses.Snapshot(),
                participant.Profile?.CombatantId, participant.PostureBreak.CaptureRuleState(), participant.IsDefending,
                participant.Side, participant.PartyOrderIndex, participant.RosterRole,
                participant.EffectivePostureRecoveryPerTurn, participant.BattleStamina,
                participant.Build.EffectiveStaminaRecoveryPerCompletedAction);
        }

        IReadOnlyList<DRPGTurnScheduleEntry> liveTimeline =
            scheduler.GetTimelineSnapshot();
        var timeline = new BattleTimelineRuleSnapshot[liveTimeline.Count];
        for (int i = 0; i < liveTimeline.Count; i++)
        {
            DRPGTurnScheduleEntry entry = liveTimeline[i];
            timeline[i] = new BattleTimelineRuleSnapshot(
                new ParticipantId(entry.Participant.RuntimeId),
                entry.ScheduledTime,
                entry.Priority);
        }

        var resourceSnapshots = new BattleResourceRuleSnapshot[
            resources.Count];
        int resourceIndex = 0;
        foreach (KeyValuePair<string, int> pair in resources)
        {
            resourceSnapshots[resourceIndex++] =
                new BattleResourceRuleSnapshot(pair.Key, pair.Value);
        }

        return new BattleRuleSnapshot(
            BattleGeneration,
            BattleRevision,
            scheduler.CurrentTime,
            participantSnapshots,
            timeline,
            resourceSnapshots,
            CurrentStatusApplicationBoundarySerial);
    }

    private long CurrentStatusApplicationBoundarySerial
    {
        get
        {
            var owner = arbiter.CurrentOwner;
            if (owner.Kind == BattleTransactionKind.RootAction || owner.Kind == BattleTransactionKind.LegacyAction)
                return owner.Action.TurnSerial;
            if (owner.Kind == BattleTransactionKind.TurnEntry) return owner.TurnEntry.CandidateSerial;
            return 0;
        }
    }

    internal DRPGBattleMutationWorkspace CreateWorkspace()
    {
        BattleRuleSnapshot snapshot = CaptureRuleSnapshot();
        return new DRPGBattleMutationWorkspace(BattleRevision, snapshot, actionResources?.Capture());
    }

    public BattleJournalCommitResult TryCommitJournal(
        BattleRuleMutationLease lease,
        DRPGBattleMutationWorkspace workspace)
    {
        if (workspace?.ActionCosts != null)
            return BattleJournalCommitResult.Failure(BattleRevision, "Root cost drafts require the complete action ledger commit gate.");
        return TryCommitJournalCore(lease, workspace);
    }

    internal BattleJournalCommitResult TryCommitCostJournal(BattleRuleMutationLease lease,
        DRPGBattleMutationWorkspace workspace, DRPGActionCostReservation costs)
    {
        if (costs == null || !ReferenceEquals(workspace?.ActionCosts, costs) || !costs.IsCommitting(workspace))
            return BattleJournalCommitResult.Failure(BattleRevision, "Root costs require their locked commit preflight.");
        return TryCommitJournalCore(lease, workspace);
    }

    private BattleJournalCommitResult TryCommitJournalCore(BattleRuleMutationLease lease,
        DRPGBattleMutationWorkspace workspace)
    {
        if (lease == null || !arbiter.Owns(lease, lease.Owner))
        {
            return BattleJournalCommitResult.Failure(
                BattleRevision,
                "The active battle mutation lease is required.");
        }
        if (!TurnCandidates.AllowsPinnedWork(lease))
            return BattleJournalCommitResult.Failure(BattleRevision, "The pinned turn candidate or enemy plan changed.");
        if (workspace == null)
        {
            return BattleJournalCommitResult.Failure(
                BattleRevision,
                "A mutation workspace is required.");
        }
        if (workspace.Snapshot.BattleGeneration != BattleGeneration)
        {
            return BattleJournalCommitResult.Failure(
                BattleRevision,
                "The mutation workspace belongs to another battle.");
        }
        if (workspace.BaseRevision != BattleRevision)
        {
            return BattleJournalCommitResult.Failure(
                BattleRevision,
                "The mutation workspace revision is stale.");
        }
        if (!MatchesCapturedSnapshot(
                workspace.Snapshot,
                out string snapshotError))
        {
            return BattleJournalCommitResult.Failure(
                BattleRevision,
                snapshotError);
        }

        DRPGBattleMutationJournal journal;
        try
        {
            journal = workspace.BuildValidatedJournal();
        }
        catch (Exception exception)
        {
            return BattleJournalCommitResult.Failure(
                BattleRevision,
                exception.Message);
        }

        string validationError = ValidateJournal(journal);
        if (journal.LogicalTime.HasValue && lease.Owner.Kind != BattleTransactionKind.TurnEntry)
            validationError = "Only TurnEntry may advance logical time through a rule journal.";
        if (!string.IsNullOrEmpty(validationError))
        {
            return BattleJournalCommitResult.Failure(
                BattleRevision,
                validationError);
        }

        DRPGBattlePreparedResourceCommit preparedResources;
        DRPGTurnScheduler.PreparedJournal preparedTimeline;
        var preparedStatuses = new List<DRPGStatusRuntimeCollection.PreparedJournal>();
        try
        {
            preparedTimeline = scheduler.PrepareJournal(mutationCapability, journal.TimelineMutations, participantById, journal.LogicalTime);
            foreach (var mutation in journal.ParticipantMutations)
            {
                if (mutation.Statuses.Count != 0)
                    preparedStatuses.Add(new DRPGStatusRuntimeCollection.PreparedJournal(
                        participantById[mutation.Participant].Statuses, mutationCapability, mutation.Statuses));
            }
            if (actionResources == null && journal.ActionResources != null)
                throw new InvalidOperationException("This State has no matching action resource bindings.");
            preparedResources = actionResources != null ? actionResources.Prepare(journal.ActionResources, lease.Owner, journal, workspace.Snapshot) : null;
        }
        catch (Exception exception)
        {
            return BattleJournalCommitResult.Failure(BattleRevision, exception.Message);
        }

        ApplyValidatedJournal(journal);
        preparedTimeline?.Apply(mutationCapability);
        foreach (var statuses in preparedStatuses) statuses.Apply(mutationCapability);
        preparedResources?.Apply(mutationCapability);
        BattleRevision++;
        return BattleJournalCommitResult.Success(
            BattleRevision,
            journal.DirtySet);
    }

    internal bool OwnsRuleMutationLease(BattleRuleMutationLease lease, BattleTransactionIdentity owner) =>
        arbiter.Owns(lease, owner);

    internal bool TryConsumeTurnEntryTimelineCandidate(BattleRuleMutationLease lease,
        DRPGTurnCandidateReservation reservation, out string error)
    {
        error = string.Empty;
        if (lease == null || !arbiter.Owns(lease, lease.Owner) ||
            !TurnCandidates.IsCommittingFinalization(lease, reservation))
        { error = "Timeline consumption requires the candidate authority's active terminal commit."; return false; }
        // This is action consumption, not death departure. Keep the ordinary
        // journal rule forbidding removal of living actors unchanged.
        var mutations = new[] { new DRPGTimelineMutationRecord(
            lease.Owner.TurnEntry.Actor, scheduler.CurrentTime, 0, true) };
        var prepared = scheduler.PrepareJournal(mutationCapability, mutations, participantById);
        prepared.Apply(mutationCapability);
        BattleRevision++;
        return true;
    }

    internal bool TryGetResource(string resourceId, out int value)
    {
        return resources.TryGetValue(resourceId ?? string.Empty, out value);
    }

    internal bool TryCreateLegacyMutationAdapter(
        BattleRuleMutationLease lease,
        out DRPGLiveBattleMutationAdapter adapter,
        out string error)
    {
        adapter = null;
        if (lease == null || !arbiter.Owns(lease, lease.Owner) ||
            lease.Owner.Kind != BattleTransactionKind.LegacyAction)
        {
            error = "A matching LegacyAction lease is required.";
            return false;
        }
        adapter = new DRPGLiveBattleMutationAdapter(this, lease);
        error = string.Empty;
        return true;
    }

    internal IDisposable OpenCompatibilityMutationScope(
        BattleRuleMutationLease lease)
    {
        if (lease == null || !arbiter.Owns(lease, lease.Owner))
        {
            throw new InvalidOperationException(
                "The active battle transaction lease is required.");
        }
        return new CompatibilityMutationScope(
            this);
    }

    internal void PublishNotification(Action notification)
    {
        if (notification == null)
            return;
        if (compatibilityMutationScopeDepth > 0)
        {
            deferredNotifications.Enqueue(notification);
            return;
        }
        notification();
    }

    private void EnterCompatibilityMutationScope()
    {
        int entered = 0;
        try
        {
            for (; entered < participants.Count; entered++)
                participants[entered].EnterStateMutationScope(mutationCapability);
            scheduler.EnterStateMutationScope(mutationCapability);
            compatibilityMutationScopeDepth++;
        }
        catch
        {
            for (int i = entered - 1; i >= 0; i--)
                participants[i].ExitStateMutationScope(mutationCapability);
            throw;
        }
    }

    private void ExitCompatibilityMutationScope()
    {
        if (compatibilityMutationScopeDepth <= 0)
            throw new InvalidOperationException("Compatibility mutation scope underflow.");
        scheduler.ExitStateMutationScope(mutationCapability);
        for (int i = participants.Count - 1; i >= 0; i--)
            participants[i].ExitStateMutationScope(mutationCapability);
        compatibilityMutationScopeDepth--;
        if (compatibilityMutationScopeDepth != 0)
            return;

        while (deferredNotifications.Count > 0)
            deferredNotifications.Dequeue()();
    }

    private bool MatchesCapturedSnapshot(
        BattleRuleSnapshot snapshot,
        out string error)
    {
        if (snapshot == null || snapshot.Participants.Count != participants.Count ||
            snapshot.StatusApplicationBoundarySerial != CurrentStatusApplicationBoundarySerial)
        {
            error = "The captured rule snapshot no longer matches participants.";
            return false;
        }
        for (int i = 0; i < snapshot.Participants.Count; i++)
        {
            BattleParticipantRuleSnapshot captured = snapshot.Participants[i];
            if (!participantById.TryGetValue(
                    captured.Participant,
                    out BattleParticipantRuntime live) ||
                !Near(captured.CurrentHp, live.CurrentHp) ||
                !Near(captured.CurrentMp, live.CurrentMp) ||
                !Near(captured.CurrentPosture, live.CurrentPosture) ||
                !Near(captured.PostureRecoveryPerTurn, live.EffectivePostureRecoveryPerTurn) ||
                !Near(captured.CurrentShield, live.CurrentShield) ||
                !captured.PostureState.Same(live.PostureBreak.CaptureRuleState()) || captured.IsDefending != live.IsDefending ||
                !StringComparer.Ordinal.Equals(captured.OwnerStableId, live.Profile?.CombatantId?.Trim() ?? string.Empty) ||
                captured.FormationRow != live.FormationRow ||
                captured.IsDeployed != live.IsDeployed ||
                !StatusesMatch(captured.Statuses, live.Statuses.Snapshot()))
            {
                error = "The captured rule snapshot drifted from live participant state.";
                return false;
            }
        }

        if (!Near(snapshot.CurrentLogicalTime, scheduler.CurrentTime))
        {
            error = "The captured rule snapshot drifted from scheduler time.";
            return false;
        }
        IReadOnlyList<DRPGTurnScheduleEntry> liveTimeline =
            scheduler.GetTimelineSnapshot();
        if (snapshot.Timeline.Count != liveTimeline.Count)
        {
            error = "The captured rule snapshot drifted from the timeline.";
            return false;
        }
        for (int i = 0; i < liveTimeline.Count; i++)
        {
            BattleTimelineRuleSnapshot captured = snapshot.Timeline[i];
            DRPGTurnScheduleEntry live = liveTimeline[i];
            if (captured.Participant !=
                    new ParticipantId(live.Participant.RuntimeId) ||
                !Near(captured.ScheduledTime, live.ScheduledTime) ||
                captured.Priority != live.Priority)
            {
                error = "The captured rule snapshot drifted from the timeline.";
                return false;
            }
        }

        if (snapshot.Resources.Count != resources.Count)
        {
            error = "The captured rule snapshot drifted from battle resources.";
            return false;
        }
        for (int i = 0; i < snapshot.Resources.Count; i++)
        {
            BattleResourceRuleSnapshot captured = snapshot.Resources[i];
            if (!resources.TryGetValue(captured.ResourceId, out int live) ||
                live != captured.Value)
            {
                error = "The captured rule snapshot drifted from battle resources.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool StatusesMatch(
        IReadOnlyList<DRPGStatusRuntimeSnapshot> left,
        IReadOnlyList<DRPGStatusRuntimeSnapshot> right)
    {
        return DRPGActionStatusChange.SameSnapshots(left, right);
    }

    private static bool Near(float left, float right)
    {
        return Math.Abs(left - right) < 0.0001f;
    }

    private string ValidateJournal(DRPGBattleMutationJournal journal)
    {
        if (journal == null || journal.BaseRevision != BattleRevision)
            return "The mutation journal revision is stale.";

        var finalHitPoints = new Dictionary<ParticipantId, float>();
        for (int i = 0; i < journal.ParticipantMutations.Count; i++)
        {
            DRPGParticipantMutationRecord mutation =
                journal.ParticipantMutations[i];
            if (!participantById.TryGetValue(mutation.Participant, out _))
                return "A participant mutation references a missing participant.";
            finalHitPoints[mutation.Participant] = mutation.HitPoints;
            foreach (var status in mutation.Statuses)
                if (status.Kind == DRPGStatusMutationKind.Apply && !participantById.ContainsKey(status.SourceParticipantId))
                    return "A status application source is not a captured battle participant.";
        }

        for (int i = 0; i < journal.TimelineMutations.Count; i++)
        {
            DRPGTimelineMutationRecord mutation = journal.TimelineMutations[i];
            if (!participantById.TryGetValue(
                    mutation.Participant,
                    out BattleParticipantRuntime participant))
            {
                return "A timeline mutation references a missing participant.";
            }
            if (mutation.ScheduledTime < scheduler.CurrentTime)
                return "A timeline mutation precedes live logical time.";
            float finalHp = finalHitPoints.TryGetValue(
                mutation.Participant,
                out float mutatedHp)
                ? mutatedHp
                : participant.CurrentHp;
            if (mutation.IsRemoval)
            {
                if (finalHp > 0f) return "A departure cannot remove a living scheduler candidate.";
                continue;
            }
            if (finalHp <= 0f)
            {
                bool alreadyScheduled = false;
                foreach (var entry in scheduler.GetTimelineSnapshot())
                    if (entry.Participant.RuntimeId == participant.RuntimeId) { alreadyScheduled = true; break; }
                // Preserve an earlier delta when a later outcome kills its
                // existing candidate. Departure is owned by reconciliation.
                if (!participant.IsAlive || !alreadyScheduled)
                    return "A defeated participant cannot be newly scheduled.";
            }
        }
        return string.Empty;
    }

    private void ApplyValidatedJournal(DRPGBattleMutationJournal journal)
    {
        for (int i = 0; i < journal.ParticipantMutations.Count; i++)
        {
            DRPGParticipantMutationRecord mutation =
                journal.ParticipantMutations[i];
            BattleParticipantRuntime participant =
                participantById[mutation.Participant];
            participant.ApplyCommittedRuleValues(
                mutationCapability,
                mutation.HitPoints,
                mutation.Mana,
                mutation.Posture,
                mutation.Shield,
                mutation.FormationRow,
                mutation.IsDeployed, mutation.PostureState, mutation.IsDefending,
                mutation.Stamina);
        }

        for (int i = 0; i < journal.ResourceMutations.Count; i++)
        {
            BattleResourceRuleSnapshot mutation =
                journal.ResourceMutations[i];
            resources[mutation.ResourceId] = mutation.Value;
        }
    }

    private sealed class CompatibilityMutationScope : IDisposable
    {
        private readonly DRPGBattleSessionState owner;
        private bool disposed;

        internal CompatibilityMutationScope(DRPGBattleSessionState owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            owner.EnterCompatibilityMutationScope();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            owner.ExitCompatibilityMutationScope();
        }
    }
}

internal sealed class DRPGLiveBattleMutationAdapter
{
    private readonly DRPGBattleSessionState state;
    private readonly BattleRuleMutationLease lease;

    internal DRPGLiveBattleMutationAdapter(
        DRPGBattleSessionState state,
        BattleRuleMutationLease lease)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
    }

    internal DRPGBattleMutationWorkspace CreateWorkspace()
    {
        if (!lease.IsActive)
        {
            throw new InvalidOperationException(
                "The Legacy mutation lease is no longer active.");
        }
        return state.CreateWorkspace();
    }

    internal BattleJournalCommitResult TryCommit(
        DRPGBattleMutationWorkspace workspace)
    {
        return state.TryCommitJournal(lease, workspace);
    }
}
