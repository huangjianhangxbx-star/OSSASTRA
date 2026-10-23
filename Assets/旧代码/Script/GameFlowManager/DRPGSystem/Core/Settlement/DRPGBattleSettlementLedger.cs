using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Rules only: each instance is one visible step, even when it has many stacks.
// Views consume these immutable before/after values, never the already-final live HP.
internal sealed class DRPGSettlementStatusStep
{
    internal DRPGSettlementStatusStep(BattleTransactionIdentity transaction, int index, DRPGBattleSide lane, int homeSlot,
        DRPGStatusBoundary boundary, DRPGStatusBoundaryStep status, float hpBefore, float hpAfter,
        float postureBefore, float postureAfter, DRPGStatusCollectionSnapshot statusesBefore, DRPGStatusCollectionSnapshot statusesAfter)
    {
        EventId = transaction.Kind == BattleTransactionKind.TurnEntry
            ? string.Format(CultureInfo.InvariantCulture, "turn-entry-settlement:{0}:{1}:{2}:{3}",
                transaction.BattleGeneration, transaction.TurnEntry.CandidateSerial, transaction.TurnEntry.Actor.Value, index)
            : string.Format(CultureInfo.InvariantCulture, "settlement:{0}:{1}:{2}:{3}",
                transaction.BattleGeneration, transaction.Action.TurnSerial, transaction.Action.ActionId, index);
        Transaction = transaction; Lane = lane; HomeSlot = homeSlot; Boundary = boundary; Status = status;
        HpBefore = hpBefore; HpAfter = hpAfter; PostureBefore = postureBefore; PostureAfter = postureAfter;
        StatusesBefore = statusesBefore; StatusesAfter = statusesAfter;
    }
    internal string EventId { get; }
    internal BattleTransactionIdentity Transaction { get; }
    internal ActionIdentity Identity => Transaction.Action;
    internal DRPGBattleSide Lane { get; }
    internal int HomeSlot { get; }
    internal ParticipantId Source => Status.Before.SourceParticipantId;
    internal ParticipantId Owner => Status.Before.OwnerParticipantId;
    internal DRPGStatusBoundary Boundary { get; }
    internal DRPGStatusBoundaryStep Status { get; }
    internal float HpBefore { get; }
    internal float HpAfter { get; }
    internal float PostureBefore { get; }
    internal float PostureAfter { get; }
    internal DRPGStatusCollectionSnapshot StatusesBefore { get; }
    internal DRPGStatusCollectionSnapshot StatusesAfter { get; }
    internal bool RuleDefeated => HpBefore > 0f && HpAfter <= 0f;
}

internal sealed class DRPGBattleSettlementLedger
{
    private readonly Dictionary<ParticipantId, int> homeSlots;
    private readonly DRPGImmediateReactionLedgerBuilder.Continuation continuation;
    private DRPGBattleSettlementLedger(BattleTransactionIdentity owner, ParticipantId actor, long revision,
        Dictionary<ParticipantId, int> homes, List<DRPGSettlementStatusStep> steps,
        List<DRPGSettlementCausalGroup> groups, DRPGImmediateReactionLedgerBuilder.Continuation continuation,
        BattleRuleSnapshot snapshot, DRPGBattleMutationJournal journal, DRPGTurnCandidateReservation? reservation,
        DRPGTurnEntryLifecycle lifecycle)
    {
        Owner = owner; Actor = actor; BaseRevision = revision; CommittedRevision = checked(revision + 1);
        MutationJournal = journal;
        TurnEntryReservation = reservation.HasValue ? new DRPGTurnEntryReservationFacts(reservation.Value) :
            (DRPGTurnEntryReservationFacts?)null;
        TurnEntryLifecycle = lifecycle;
        homeSlots = homes;
        Steps = Array.AsReadOnly(steps.ToArray());
        Groups = Array.AsReadOnly(groups.ToArray()); this.continuation = continuation;
        var sides = new Dictionary<ParticipantId, DRPGBattleSide>();
        foreach (var participant in snapshot.Participants) sides.Add(participant.Participant, participant.Side);
        ParticipantSides = new System.Collections.ObjectModel.ReadOnlyDictionary<ParticipantId, DRPGBattleSide>(sides);
        Fingerprint = DRPGActionResolutionFingerprint.ComputeSettlement(this, homeSlots);
    }
    internal BattleTransactionIdentity Owner { get; }
    internal ActionIdentity Identity => Owner.Action;
    internal ParticipantId Actor { get; }
    internal long BaseRevision { get; }
    internal long CommittedRevision { get; }
    internal IReadOnlyList<DRPGSettlementStatusStep> Steps { get; }
    internal IReadOnlyList<DRPGSettlementCausalGroup> Groups { get; }
    internal IReadOnlyDictionary<ParticipantId, DRPGBattleSide> ParticipantSides { get; }
    internal ulong Fingerprint { get; }
    internal DRPGBattleMutationJournal MutationJournal { get; }
    internal DRPGTurnEntryReservationFacts? TurnEntryReservation { get; }
    internal DRPGTurnEntryLifecycle TurnEntryLifecycle { get; }

    internal bool MatchesRequest(ParticipantId actor, IReadOnlyDictionary<ParticipantId, int> homes,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation)
    {
        if (actor != Actor || !ReferenceEquals(this.continuation, continuation) || homes == null || homes.Count != homeSlots.Count) return false;
        foreach (var pair in homeSlots)
            if (!homes.TryGetValue(pair.Key, out int value) || value != pair.Value) return false;
        return true;
    }

    // Rules only: each root's complete child tree is staged before the next
    // root is rechecked. Production still needs the serial presentation and
    // finalization barriers; this builder never publishes action completion.
    internal static DRPGBattleSettlementLedger Build(ActionIdentity identity, ParticipantId actor,
        DRPGBattleMutationWorkspace draft, IReadOnlyDictionary<ParticipantId, int> homes,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation = null)
        => Build(BattleTransactionIdentity.ForRootAction(identity), actor, draft, homes, continuation);

    internal static DRPGBattleSettlementLedger Build(BattleTransactionIdentity owner, ParticipantId actor,
        DRPGBattleMutationWorkspace draft, IReadOnlyDictionary<ParticipantId, int> homes,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation = null, DRPGTurnCandidateReservation? reservation = null)
    {
        bool turnEntry = owner.Kind == BattleTransactionKind.TurnEntry;
        long boundarySerial = turnEntry ? owner.TurnEntry.CandidateSerial : owner.Action.TurnSerial;
        if (!owner.IsValid || (!turnEntry && owner.Kind != BattleTransactionKind.RootAction) ||
            (turnEntry && owner.TurnEntry.Actor != actor) ||
            draft == null || draft.Snapshot.BattleGeneration != owner.BattleGeneration ||
            draft.Snapshot.StatusApplicationBoundarySerial != boundarySerial ||
            !draft.Snapshot.TryGetParticipant(actor, out _))
            throw new InvalidOperationException("Settlement requires a matching captured transaction, candidate and boundary.");
        var frozenHomes = FreezeHomes(draft.Snapshot, homes);
        if (reservation.HasValue)
        {
            if (!turnEntry || !reservation.Value.IsValid || reservation.Value.Candidate.Participant.RuntimeId != actor.Value)
                throw new InvalidOperationException("TurnEntry lifecycle requires its matching reservation.");
            draft.AdvanceTurnEntryLogicalTime((float)reservation.Value.ReservedLogicalTime);
            draft.For(actor).ClearDefendForTurnEntry();
        }
        var reactions = continuation?.BeginSettlement(owner, draft);
        var due = new List<DueStatus>();
        var timings = turnEntry ? new[] { DRPGStatusTriggerTiming.TurnStart }
            : new[] { DRPGStatusTriggerTiming.AfterAction, DRPGStatusTriggerTiming.ActorTurnEnd };
        foreach (var participant in draft.Snapshot.Participants)
        {
            if (!participant.IsDeployed || participant.CurrentHp <= 0f) continue;
            var projection = DRPGStatusRuntimeCollection.FromSnapshot(participant.Statuses);
            foreach (var timing in timings)
            {
                var boundary = new DRPGStatusBoundary(timing, boundarySerial, actor);
                foreach (var status in projection.GetDueAtBoundary(boundary))
                    due.Add(new DueStatus(participant.Side, frozenHomes[participant.Participant], boundary, status));
            }
        }
        due.Sort(Compare);
        var steps = new List<DRPGSettlementStatusStep>();
        var groups = new List<DRPGSettlementCausalGroup>();
        foreach (var item in due)
        {
            var target = draft.For(item.Status.OwnerParticipantId);
            var source = draft.For(item.Status.SourceParticipantId);
            // Ordinary ticks from dead sources are cancelled, and ordinary
            // healing cannot revive. Skipped items do not consume duration.
            if (target.HitPoints <= 0f || !target.IsDeployed ||
                (source.HitPoints <= 0f && !item.Status.Spec.PersistsAfterSourceDefeat)) continue;
            var before = target.CreateStatusProjection().Snapshot();
            float hpBefore = target.HitPoints, postureBefore = target.Posture;
            if (!target.TryConsumeStatusBoundary(item.Boundary, item.Status.StatusInstanceId, out var consumed)) continue;
            float hpAfter = ApplyDelta(hpBefore, consumed.HpDelta, target.Snapshot.MaximumHp);
            float postureAfter = ApplyDelta(postureBefore, consumed.PostureDelta, target.Snapshot.MaximumPosture);
            if (hpAfter != hpBefore) target.SetHitPoints(hpAfter);
            if (postureAfter != postureBefore) target.SetPosture(postureAfter);
            var step = new DRPGSettlementStatusStep(owner, steps.Count, item.Lane, item.HomeSlot,
                item.Boundary, consumed, hpBefore, hpAfter, postureBefore, postureAfter,
                before, target.CreateStatusProjection().Snapshot());
            steps.Add(step);
            groups.Add(new DRPGSettlementCausalGroup(step, reactions?.ExpandSettlement(step)));
        }
        var lifecycle = reservation.HasValue ? draft.For(actor).ResolveTurnEntryLifecycle() : null;
        var journal = draft.BuildValidatedJournal();
        return new DRPGBattleSettlementLedger(owner, actor, draft.BaseRevision, frozenHomes, steps, groups, continuation,
            draft.Snapshot, journal, reservation, lifecycle);
    }

    private static Dictionary<ParticipantId, int> FreezeHomes(BattleRuleSnapshot snapshot, IReadOnlyDictionary<ParticipantId, int> homes)
    {
        if (homes == null) throw new ArgumentNullException(nameof(homes));
        var copy = new Dictionary<ParticipantId, int>();
        var occupied = new HashSet<(DRPGBattleSide, int)>();
        foreach (var pair in homes)
        {
            if (pair.Value < 0 || !snapshot.TryGetParticipant(pair.Key, out var actor) || !actor.IsDeployed || actor.CurrentHp <= 0f ||
                !Enum.IsDefined(typeof(DRPGBattleSide), actor.Side) || !occupied.Add((actor.Side, pair.Value)))
                throw new InvalidOperationException("Settlement Home Slots must uniquely cover captured living deployed actors per side.");
            copy.Add(pair.Key, pair.Value);
        }
        foreach (var actor in snapshot.Participants)
            if (actor.IsDeployed && actor.CurrentHp > 0f && !copy.ContainsKey(actor.Participant))
                throw new InvalidOperationException("A living deployed participant is missing its frozen Settlement Home Slot.");
        return copy;
    }

    private static float ApplyDelta(float before, float delta, float maximum)
    {
        float sum = before + delta;
        if (float.IsNaN(sum) || float.IsInfinity(sum)) throw new InvalidOperationException("Settlement resource delta overflowed.");
        return Math.Max(0f, Math.Min(maximum, sum));
    }
    private static int Compare(DueStatus a, DueStatus b)
    {
        int order = a.Lane.CompareTo(b.Lane);
        if (order == 0) order = a.HomeSlot.CompareTo(b.HomeSlot);
        if (order == 0) order = a.Boundary.Timing.CompareTo(b.Boundary.Timing);
        if (order == 0) order = b.Status.Priority.CompareTo(a.Status.Priority);
        return order != 0 ? order : StringComparer.Ordinal.Compare(a.Status.StatusInstanceId, b.Status.StatusInstanceId);
    }
    private readonly struct DueStatus
    {
        internal DueStatus(DRPGBattleSide lane, int slot, DRPGStatusBoundary boundary, DRPGStatusRuntimeSnapshot status)
        { Lane = lane; HomeSlot = slot; Boundary = boundary; Status = status; }
        internal DRPGBattleSide Lane { get; }
        internal int HomeSlot { get; }
        internal DRPGStatusBoundary Boundary { get; }
        internal DRPGStatusRuntimeSnapshot Status { get; }
    }
}

internal sealed class DRPGSettlementCausalGroup
{
    internal DRPGSettlementCausalGroup(DRPGSettlementStatusStep root, IReadOnlyList<DRPGReactionLedgerEntry> children)
    {
        Root = root;
        var copy = new DRPGReactionLedgerEntry[children != null ? children.Count : 0];
        for (int i = 0; i < copy.Length; i++) copy[i] = children[i];
        Children = Array.AsReadOnly(copy);
    }
    internal DRPGSettlementStatusStep Root { get; }
    internal DRPGBattleSide Lane => Root.Lane;
    internal IReadOnlyList<DRPGReactionLedgerEntry> Children { get; }
}

internal static partial class DRPGActionResolutionFingerprint
{
    internal static ulong ComputeSettlement(DRPGBattleSettlementLedger ledger, IReadOnlyDictionary<ParticipantId, int> homes)
    {
        var builder = new StringBuilder();
        Append(builder, "causal-settlement-v5");
        Append(builder, (int)ledger.Owner.Kind); Append(builder, ledger.Owner.BattleGeneration);
        Append(builder, ledger.Owner.TurnEntry.CandidateSerial); Append(builder, ledger.Owner.TurnEntry.Actor.Value);
        Append(builder, ledger.Identity.BattleGeneration); Append(builder, ledger.Identity.TurnSerial); Append(builder, ledger.Identity.ActionId);
        Append(builder, ledger.Actor.Value); Append(builder, ledger.BaseRevision); Append(builder, ledger.CommittedRevision);
        AppendJournal(builder, ledger.MutationJournal);
        Append(builder, ledger.TurnEntryReservation.HasValue ? 1 : 0);
        if (ledger.TurnEntryReservation.HasValue)
        {
            var r = ledger.TurnEntryReservation.Value;
            Append(builder, r.ReservationId); Append(builder, (int)r.SourceKind);
            Append(builder, r.Actor.Value); Append(builder, r.ScheduledTime);
            Append(builder, r.Priority); Append(builder, r.LogicalTime);
            var life = ledger.TurnEntryLifecycle;
            Append(builder, life.SkipAction ? 1 : 0); Append(builder, life.CandidateAlive ? 1 : 0);
            Append(builder, life.ClearedDefend ? 1 : 0); AppendPostureState(builder, life.Before); AppendPostureState(builder, life.After);
            Append(builder, life.PostureBefore); Append(builder, life.PostureAfter);
        }
        var ids = new List<ParticipantId>(homes.Keys); ids.Sort((a, b) => a.Value.CompareTo(b.Value));
        Append(builder, ids.Count);
        foreach (var id in ids) { Append(builder, id.Value); Append(builder, homes[id]); }
        var participants = new List<ParticipantId>(ledger.ParticipantSides.Keys); participants.Sort((a, b) => a.Value.CompareTo(b.Value));
        Append(builder, participants.Count);
        foreach (var id in participants) { Append(builder, id.Value); Append(builder, (int)ledger.ParticipantSides[id]); }
        Append(builder, ledger.Steps.Count);
        foreach (var step in ledger.Steps)
        {
            Append(builder, step.EventId); Append(builder, (int)step.Lane); Append(builder, step.HomeSlot);
            Append(builder, (int)step.Boundary.Timing); Append(builder, step.Boundary.Serial); Append(builder, step.Boundary.Actor.Value);
            Append(builder, step.Status.Before.StatusInstanceId); Append(builder, step.Source.Value); Append(builder, step.Owner.Value);
            Append(builder, step.HpBefore); Append(builder, step.HpAfter); Append(builder, step.PostureBefore); Append(builder, step.PostureAfter);
            Append(builder, step.Status.HpDelta); Append(builder, step.Status.PostureDelta);
            AppendStatusSnapshots(builder, step.StatusesBefore); AppendStatusSnapshots(builder, step.StatusesAfter);
            AppendStatusSnapshots(builder, new[] { step.Status.Before, step.Status.After });
        }
        Append(builder, ledger.Groups.Count);
        foreach (var group in ledger.Groups)
        {
            Append(builder, group.Root.EventId); Append(builder, (int)group.Lane);
            AppendReactions(builder, group.Children);
        }
        return Hash(builder);
    }
}
