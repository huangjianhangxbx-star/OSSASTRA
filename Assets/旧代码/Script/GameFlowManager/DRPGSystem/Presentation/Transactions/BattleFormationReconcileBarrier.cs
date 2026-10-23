using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class BattleFrozenFormationLayout
{
    private readonly struct Actor
    {
        internal Actor(DRPGBattleParticipantViewModel view)
        { Id = view.RuntimeId; Enemy = view.IsEnemy; Row = view.FormationRow; Slot = view.FormationSlot; Width = view.FormationVisualWidth; }
        internal readonly int Id, Slot;
        internal readonly bool Enemy;
        internal readonly DRPGFormationRow Row;
        internal readonly float Width;
    }
    private readonly List<Actor> actors = new List<Actor>();
    private readonly Vector3 playerOrigin, enemyOrigin;
    private readonly float width, spacing, depth, horizontal, middleScale, rearScale;
    private readonly bool preservePartyHomes;
    private readonly Dictionary<int, Vector3> partyHomes = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, float> partyScales = new Dictionary<int, float>();
    internal BattleFrozenFormationLayout(IEnumerable<BattleWorldActorView> views, Vector3 playerOrigin, Vector3 enemyOrigin,
        float width, float spacing, float depth, float horizontal, float middleScale, float rearScale,
        bool preservePartyHomes = false)
    {
        this.preservePartyHomes = preservePartyHomes;
        foreach (var view in views)
        {
            actors.Add(new Actor(view.BoundActor));
            if (!view.BoundActor.IsEnemy)
            {
                partyHomes.Add(view.RuntimeId, view.transform.position);
                partyScales.Add(view.RuntimeId, view.transform.localScale.x);
            }
        }
        this.playerOrigin = playerOrigin; this.enemyOrigin = enemyOrigin; this.width = width; this.spacing = spacing;
        this.depth = depth; this.horizontal = horizontal; this.middleScale = middleScale; this.rearScale = rearScale;
    }

    internal IReadOnlyList<DRPGWorldFormationPlacement> Plan(DRPGBattleActionResolutionLedger ledger,
        DRPGBattlePostReturnResult rules, DRPGBattleActionProjectionLease lease, DRPGBattlePostReturnResult previous = null)
        => Plan(BattleTransactionIdentity.ForRootAction(ledger.Identity), ledger.MutationJournal, rules, lease, previous);

    internal IReadOnlyList<DRPGWorldFormationPlacement> PlanWave(IReadOnlyList<BattleParticipantRuntime> members,
        int perRowLimit, DRPGBattleActionProjectionLease lease)
    {
        if (lease == null || !lease.IsActive || lease.Owner.Kind != BattleTransactionKind.WaveTransition ||
            members == null || members.Count == 0 || perRowLimit <= 0 || members.Count > perRowLimit * 3)
            throw new InvalidOperationException("Wave homes require a live wave projection and bounded batch.");
        var items = new List<DRPGWorldFormationItem>();
        var ids = new HashSet<int>();
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null || member.Side != DRPGBattleSide.Enemy || member.IsDeployed || !ids.Add(member.RuntimeId))
                throw new InvalidOperationException("Wave homes require unique undeployed enemy identities.");
            items.Add(new DRPGWorldFormationItem(member.RuntimeId, (DRPGFormationRow)(i / perRowLimit),
                member.FormationSlot, member.Profile.FormationVisualWidth));
        }
        return DRPGWorldFormationLayoutCalculator.Calculate(items, enemyOrigin, width, 1f, spacing,
            depth, horizontal, middleScale, rearScale);
    }

    internal IReadOnlyList<DRPGWorldFormationPlacement> Plan(BattleTransactionIdentity owner, DRPGBattleMutationJournal journal,
        DRPGBattlePostReturnResult rules, DRPGBattleActionProjectionLease lease, DRPGBattlePostReturnResult previous = null)
    {
        if (lease == null || !lease.IsActive || owner != lease.Owner || rules == null || rules.Departures.Owner != owner ||
            journal == null || previous != null && previous.Departures.Owner != owner)
            throw new InvalidOperationException("Formation requires one complete transaction owner and its frozen rule journal.");
        var rows = new Dictionary<int, DRPGFormationRow>();
        foreach (var actor in actors) rows.Add(actor.Id, actor.Row);
        foreach (var mutation in journal.ParticipantMutations)
        {
            if (!rows.ContainsKey(mutation.Participant.Value)) throw new InvalidOperationException("Formation mutation escaped the frozen field.");
            rows[mutation.Participant.Value] = mutation.FormationRow;
        }
        var departed = new HashSet<int>();
        if (previous != null) ApplyRows(previous, rows, departed);
        ApplyRows(rules, rows, departed);
        var result = new List<DRPGWorldFormationPlacement>();
        foreach (bool enemy in new[] { false, true })
        {
            var items = new List<DRPGWorldFormationItem>();
            foreach (var actor in actors)
            {
                if (actor.Enemy != enemy || departed.Contains(actor.Id)) continue;
                if (!lease.TryGetView(actor.Id, out _) || lease.GetPresented(actor.Id).DefeatPending)
                    throw new InvalidOperationException("Formation has an untransferred or missing actor.");
                items.Add(new DRPGWorldFormationItem(actor.Id, rows[actor.Id], actor.Slot, actor.Width));
            }
            var origin = enemy ? enemyOrigin : playerOrigin;
            if (!enemy && preservePartyHomes)
            {
                var original = actors.FindAll(a => !a.Enemy);
                original.Sort((a, b) => { int row = a.Row.CompareTo(b.Row);
                    return row != 0 ? row : a.Slot.CompareTo(b.Slot); });
                items.Sort((a, b) => { int row = a.Row.CompareTo(b.Row);
                    return row != 0 ? row : a.Slot.CompareTo(b.Slot); });
                bool changed = original.Count != items.Count;
                for (int i = 0; i < items.Count; i++)
                    changed |= original[i].Id != items[i].RuntimeId;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    int homeId = changed ? original[i].Id : item.RuntimeId;
                    result.Add(new DRPGWorldFormationPlacement(item.RuntimeId, item.Row,
                        partyHomes[homeId], partyScales[item.RuntimeId], item.VisualWidth, 300 + i));
                }
                continue;
            }
            foreach (var placement in DRPGWorldFormationLayoutCalculator.Calculate(items, origin, width, 1f, spacing, depth, horizontal, middleScale, rearScale))
            {
                var position = placement.WorldPosition;
                if (!enemy) position.x = origin.x - (position.x - origin.x);
                result.Add(new DRPGWorldFormationPlacement(placement.RuntimeId, placement.Row, position,
                    placement.UniformScale, placement.ResolvedWidth, placement.SortingOrder));
            }
        }
        for (int i = 0; i < result.Count; i++)
        for (int j = i + 1; j < result.Count; j++)
            if ((result[i].WorldPosition - result[j].WorldPosition).sqrMagnitude < .000001f)
                throw new InvalidOperationException("Formation assigned two active actors the same home.");
        result.Sort((a, b) => a.RuntimeId.CompareTo(b.RuntimeId));
        return result.AsReadOnly();
    }
    internal IReadOnlyDictionary<ParticipantId, int> CaptureHomeSlots(DRPGBattleActionResolutionLedger ledger,
        DRPGBattlePostReturnResult rules, DRPGBattleActionProjectionLease lease)
    {
        var placements = Plan(ledger, rules, lease);
        var rows = new Dictionary<int, DRPGFormationRow>();
        foreach (var placement in placements)
        {
            if (!lease.TryGetView(placement.RuntimeId, out var view) || !lease.IsAtCurrentHome(placement.RuntimeId) ||
                (view.transform.position - placement.WorldPosition).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Settlement homes must be the completed published formation.");
            rows.Add(placement.RuntimeId, placement.Row);
        }
        var homes = new Dictionary<ParticipantId, int>();
        foreach (bool enemy in new[] { false, true })
        {
            var ordered = new List<Actor>();
            foreach (var actor in actors) if (actor.Enemy == enemy && rows.ContainsKey(actor.Id)) ordered.Add(actor);
            // Same semantic ordering as the side-on layout, without comparing
            // floating screen coordinates or reading a mutable live roster.
            ordered.Sort((a, b) => { int row = rows[a.Id].CompareTo(rows[b.Id]);
                if (row != 0) return row; int slot = a.Slot.CompareTo(b.Slot); return slot != 0 ? slot : a.Id.CompareTo(b.Id); });
            for (int i = 0; i < ordered.Count; i++) homes.Add(new ParticipantId(ordered[i].Id), i);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<ParticipantId, int>(homes);
    }
    internal IReadOnlyDictionary<ParticipantId, int> CaptureEntryHomeSlots(DRPGBattleActionProjectionLease lease)
    {
        if (lease == null || !lease.IsActive || lease.Owner.Kind != BattleTransactionKind.TurnEntry)
            throw new InvalidOperationException("Entry homes require the live candidate projection.");
        var positions = new List<Vector3>();
        foreach (var actor in actors)
        {
            if (!lease.TryGetView(actor.Id, out var view))
                throw new InvalidOperationException("Entry actor " + actor.Id + " is missing from its frozen projection.");
            if (!lease.IsAtCurrentHome(actor.Id))
                throw new InvalidOperationException("Entry actor " + actor.Id +
                    " is away from its frozen home (distanceSquared=" +
                    lease.CurrentHomeDistanceSquared(actor.Id) + ").");
            if (lease.GetPresented(actor.Id).DefeatPending)
                throw new InvalidOperationException("Entry actor " + actor.Id + " is already defeat-pending.");
            foreach (var position in positions)
                if ((view.transform.position - position).sqrMagnitude < .000001f)
                    throw new InvalidOperationException("Entry homes overlap.");
            positions.Add(view.transform.position);
        }
        var homes = new Dictionary<ParticipantId, int>();
        foreach (bool enemy in new[] { false, true })
        {
            var ordered = actors.FindAll(a => a.Enemy == enemy);
            ordered.Sort((a, b) => { int row = a.Row.CompareTo(b.Row);
                if (row != 0) return row; int slot = a.Slot.CompareTo(b.Slot); return slot != 0 ? slot : a.Id.CompareTo(b.Id); });
            for (int i = 0; i < ordered.Count; i++) homes.Add(new ParticipantId(ordered[i].Id), i);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<ParticipantId, int>(homes);
    }
    private static void ApplyRows(DRPGBattlePostReturnResult rules, Dictionary<int, DRPGFormationRow> rows, HashSet<int> departed)
    {
        foreach (var change in rules.FormationChanges)
        {
            if (!rows.TryGetValue(change.Participant.Value, out var before) || before != change.Before)
                throw new InvalidOperationException("Formation reconciliation does not match the committed row cursor.");
            rows[change.Participant.Value] = change.After;
        }
        foreach (var member in rules.Departures.Members) departed.Add(member.Participant.Value);
    }
}

// The post-departure layout owns a separate barrier, never a child of Return.
internal sealed class BattleFormationReconcileBarrier : IDisposable
{
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly BattleActorMotionController motion;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly IReadOnlyList<DRPGWorldFormationPlacement> targets;
    private readonly List<PresentationReceipt> movements = new List<PresentationReceipt>();
    private long arrivedFrame = -1;
    private bool disposed;
    internal BattleFormationReconcileBarrier(DRPGBattleActionProjectionLease lease, BattleActorMotionController motion,
        IDRPGBattlePresentationClock clock, DRPGBattleSettlementLedger ledger, DRPGBattlePostReturnResult rules,
        int stepOrdinal = 2)
        : this(lease, motion, clock, ledger.Owner, ledger.MutationJournal, rules, null, stepOrdinal,
            ledger.Fingerprint, ledger.CommittedRevision, DRPGSettlementDepartureFacts.Describe(ledger)) { }

    internal BattleFormationReconcileBarrier(DRPGBattleActionProjectionLease lease, BattleActorMotionController motion,
        IDRPGBattlePresentationClock clock, DRPGBattleActionResolutionLedger ledger, DRPGBattlePostReturnResult rules,
        DRPGBattlePostReturnResult previous = null, int stepOrdinal = 2)
        : this(lease, motion, clock, BattleTransactionIdentity.ForRootAction(ledger.Identity), ledger.MutationJournal,
            rules, previous, stepOrdinal, previous == null ? ledger.Fingerprint : rules.Departures.SourceFingerprint,
            previous == null ? ledger.MutationJournal.BaseRevision + 1 : rules.CommittedRevision - 1) { }

    private BattleFormationReconcileBarrier(DRPGBattleActionProjectionLease lease, BattleActorMotionController motion,
        IDRPGBattlePresentationClock clock, BattleTransactionIdentity owner, DRPGBattleMutationJournal journal,
        DRPGBattlePostReturnResult rules, DRPGBattlePostReturnResult previous, int stepOrdinal, ulong fingerprint, long committedRevision,
        DRPGBattleDepartureJournal expectedDepartures = null)
    {
        this.lease = lease; this.motion = motion; this.clock = clock;
        Receipt = new PresentationReceipt(lease.Owner, rules.Departures.Stage, stepOrdinal);
        StartedAt = clock.Now;
        if (!lease.TryOwnPresentation(this)) { Fail("Formation projection owner unavailable."); return; }
        try
        {
            if (owner != lease.Owner || rules.Departures.Owner != owner || rules.Departures.SourceFingerprint != fingerprint ||
                rules.CommittedRevision != committedRevision + 1 ||
                expectedDepartures != null && !rules.Departures.HasSameValues(expectedDepartures))
                throw new InvalidOperationException("Formation has mismatched owner, ledger fingerprint or reconciliation revision.");
            targets = lease.FrozenFormation.Plan(owner, journal, rules, lease, previous);
            foreach (var target in targets)
            {
                lease.TryGetView(target.RuntimeId, out var view);
                if (view.transform.position == target.WorldPosition && view.transform.localScale == Vector3.one * target.UniformScale) continue;
                var receipt = motion.MoveFormation(owner, target, StartedAt, Receipt.Stage, stepOrdinal);
                movements.Add(receipt);
                if (receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted)
                { Fail(receipt.Completion.Result.Detail); return; }
            }
            if (movements.Count == 0)
            {
                motion.SealFormationHomes(targets);
                Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Explicit empty formation reconciliation."));
            }
        }
        catch (Exception error) { Fail(error.Message); }
    }
    internal PresentationReceipt Receipt { get; }
    internal double StartedAt { get; }
    internal void Tick()
    {
        if (disposed || Receipt.Completion.IsCompleted) return;
        if (!lease.IsActive) { Fail("Formation projection owner lost."); return; }
        foreach (var receipt in movements)
        {
            if (!receipt.Completion.IsCompleted) return;
            if (receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted) { Fail(receipt.Completion.Result.Detail); return; }
        }
        if (clock.Now - StartedAt < .30d) return;
        if (arrivedFrame < 0) { arrivedFrame = clock.RenderedFrame; return; }
        if (clock.RenderedFrame <= arrivedFrame) return;
        try
        {
            motion.SealFormationHomes(targets);
            Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Whole formation rendered at its new homes."));
        }
        catch (Exception error) { Fail(error.Message); }
    }
    private void Fail(string error)
    {
        foreach (var receipt in movements) motion.CancelOwnedReceipt(receipt);
        Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, error));
    }
    public void Dispose() { if (disposed) return; disposed = true; Fail("Formation owner closed."); }
}
