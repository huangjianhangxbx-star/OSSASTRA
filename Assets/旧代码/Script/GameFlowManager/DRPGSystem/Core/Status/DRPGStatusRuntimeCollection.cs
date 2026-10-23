using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGResolvedStatusSpec
{
    private DRPGResolvedStatusSpec(DRPGStatusDefinitionSO source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(source.StatusId))
            throw new InvalidOperationException("A battle status requires an ID.");

        StatusId = source.StatusId;
        DisplayName = source.DisplayName ?? string.Empty;
        Icon = source.Icon;
        IconName = Icon != null ? Icon.name : string.Empty;
        IconInstanceId = Icon != null ? Icon.GetInstanceID() : 0;
        DurationTurns = source.DurationTurns;
        MaxStacks = source.MaxStacks;
        RefreshPolicy = source.RefreshPolicy;
        TriggerTiming = source.TriggerTiming;
        Priority = source.Priority;
        PersistsAfterSourceDefeat = source.PersistsAfterSourceDefeat;
        ValidateTiming();
        OutgoingDamageMultiplier = source.OutgoingDamageMultiplier;
        IncomingDamageMultiplier = source.IncomingDamageMultiplier;
        OutgoingHealingMultiplier = source.OutgoingHealingMultiplier;
        ManaCostMultiplier = source.ManaCostMultiplier;
        PostureCostMultiplier = source.PostureCostMultiplier;
        TurnStartHpDelta = source.TurnStartHpDelta;
        TurnStartPostureDelta = source.TurnStartPostureDelta;
    }

    public string StatusId { get; }
    public string DisplayName { get; }
    public Sprite Icon { get; }
    internal string IconName { get; }
    internal int IconInstanceId { get; }
    public int DurationTurns { get; }
    public int MaxStacks { get; }
    public DRPGStatusRefreshPolicy RefreshPolicy { get; }
    public DRPGStatusTriggerTiming TriggerTiming { get; }
    public int Priority { get; }
    public bool PersistsAfterSourceDefeat { get; }
    public float OutgoingDamageMultiplier { get; }
    public float IncomingDamageMultiplier { get; }
    public float OutgoingHealingMultiplier { get; }
    public float ManaCostMultiplier { get; }
    public float PostureCostMultiplier { get; }
    public float TurnStartHpDelta { get; }
    public float TurnStartPostureDelta { get; }

    public static DRPGResolvedStatusSpec From(DRPGStatusDefinitionSO source)
    {
        return source != null ? new DRPGResolvedStatusSpec(source) : null;
    }

    internal void ValidateForTransaction()
    {
        ValidateTiming();
        if (string.IsNullOrWhiteSpace(StatusId) || DurationTurns < 1 || MaxStacks < 1 ||
            !Enum.IsDefined(typeof(DRPGStatusRefreshPolicy), RefreshPolicy))
            throw new InvalidOperationException("Invalid frozen status definition.");
        RequireFinite(OutgoingDamageMultiplier, true);
        RequireFinite(IncomingDamageMultiplier, true);
        RequireFinite(OutgoingHealingMultiplier, true);
        RequireFinite(ManaCostMultiplier, true);
        RequireFinite(PostureCostMultiplier, true);
        RequireFinite(TurnStartHpDelta, false);
        RequireFinite(TurnStartPostureDelta, false);
    }

    internal bool HasSameValues(DRPGResolvedStatusSpec other) => other != null &&
        StatusId == other.StatusId && DisplayName == other.DisplayName && IconName == other.IconName && IconInstanceId == other.IconInstanceId &&
        DurationTurns == other.DurationTurns && MaxStacks == other.MaxStacks && RefreshPolicy == other.RefreshPolicy &&
        TriggerTiming == other.TriggerTiming && Priority == other.Priority && PersistsAfterSourceDefeat == other.PersistsAfterSourceDefeat &&
        OutgoingDamageMultiplier == other.OutgoingDamageMultiplier && IncomingDamageMultiplier == other.IncomingDamageMultiplier &&
        OutgoingHealingMultiplier == other.OutgoingHealingMultiplier && ManaCostMultiplier == other.ManaCostMultiplier &&
        PostureCostMultiplier == other.PostureCostMultiplier && TurnStartHpDelta == other.TurnStartHpDelta &&
        TurnStartPostureDelta == other.TurnStartPostureDelta;

    private void ValidateTiming()
    {
        if (!Enum.IsDefined(typeof(DRPGStatusTriggerTiming), TriggerTiming))
            throw new InvalidOperationException("Invalid status trigger timing.");
        // The continuous-time scheduler has no RoundBoundaryProvider yet.
        // Reject at rule-spec build instead of silently treating each action as a round.
        if (TriggerTiming == DRPGStatusTriggerTiming.RoundEnd)
            throw new InvalidOperationException("RoundEnd status requires a deterministic RoundBoundaryProvider; this encounter has none.");
    }

    private static void RequireFinite(float value, bool nonnegative)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || (nonnegative && value < 0f))
            throw new InvalidOperationException("A frozen status contains an invalid rule value.");
    }
}

public readonly struct DRPGStatusRuntimeSnapshot
{
    public DRPGStatusRuntimeSnapshot(
        DRPGResolvedStatusSpec spec,
        int stacks,
        int remainingTurns,
        string statusInstanceId = null,
        ParticipantId ownerParticipantId = default,
        ParticipantId sourceParticipantId = default,
        long stableInsertionOrdinal = 0,
        long appliedBoundarySerial = 0,
        long eligibleFromBoundarySerial = 1,
        long lastConsumedBoundarySerial = 0)
    {
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        Stacks = Mathf.Max(1, stacks);
        RemainingTurns = Mathf.Max(0, remainingTurns);
        StatusInstanceId = statusInstanceId ?? string.Empty;
        OwnerParticipantId = ownerParticipantId;
        SourceParticipantId = sourceParticipantId;
        StableInsertionOrdinal = stableInsertionOrdinal;
        AppliedBoundarySerial = appliedBoundarySerial;
        EligibleFromBoundarySerial = eligibleFromBoundarySerial;
        LastConsumedBoundarySerial = lastConsumedBoundarySerial;
    }

    public DRPGResolvedStatusSpec Spec { get; }
    public string StatusId => Spec.StatusId;
    public int Stacks { get; }
    public int RemainingTurns { get; }
    public string StatusInstanceId { get; }
    public ParticipantId OwnerParticipantId { get; }
    public ParticipantId SourceParticipantId { get; }
    public long StableInsertionOrdinal { get; }
    public long AppliedBoundarySerial { get; }
    public long EligibleFromBoundarySerial { get; }
    public long LastConsumedBoundarySerial { get; }
    public int Priority => Spec.Priority;

    internal bool Same(DRPGStatusRuntimeSnapshot other) => Spec != null && Spec.HasSameValues(other.Spec) &&
        Stacks == other.Stacks && RemainingTurns == other.RemainingTurns && StatusInstanceId == other.StatusInstanceId &&
        OwnerParticipantId == other.OwnerParticipantId && SourceParticipantId == other.SourceParticipantId &&
        StableInsertionOrdinal == other.StableInsertionOrdinal && AppliedBoundarySerial == other.AppliedBoundarySerial &&
        EligibleFromBoundarySerial == other.EligibleFromBoundarySerial && LastConsumedBoundarySerial == other.LastConsumedBoundarySerial;
}

// The high-water mark survives an empty roster of statuses; removing the last
// instance must not permit a later application to reuse its receipt identity.
public sealed class DRPGStatusCollectionSnapshot : IReadOnlyList<DRPGStatusRuntimeSnapshot>
{
    private readonly IReadOnlyList<DRPGStatusRuntimeSnapshot> values;
    internal DRPGStatusCollectionSnapshot(ParticipantId owner, long nextOrdinal,
        IReadOnlyList<DRPGStatusRuntimeSnapshot> source, long observedBoundarySerial = 0)
    {
        OwnerParticipantId = owner; NextInsertionOrdinal = nextOrdinal;
        ObservedBoundarySerial = observedBoundarySerial;
        var copy = new DRPGStatusRuntimeSnapshot[source.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
        values = Array.AsReadOnly(copy);
    }
    public ParticipantId OwnerParticipantId { get; }
    public long NextInsertionOrdinal { get; }
    public long ObservedBoundarySerial { get; }
    public int Count => values.Count;
    public DRPGStatusRuntimeSnapshot this[int index] => values[index];
    public IEnumerator<DRPGStatusRuntimeSnapshot> GetEnumerator() => values.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public readonly struct DRPGStatusActionModifiers
{
    public DRPGStatusActionModifiers(
        float outgoingDamageMultiplier,
        float incomingDamageMultiplier,
        float outgoingHealingMultiplier,
        float manaCostMultiplier,
        float postureCostMultiplier)
    {
        OutgoingDamageMultiplier = Mathf.Max(0f, outgoingDamageMultiplier);
        IncomingDamageMultiplier = Mathf.Max(0f, incomingDamageMultiplier);
        OutgoingHealingMultiplier = Mathf.Max(0f, outgoingHealingMultiplier);
        ManaCostMultiplier = Mathf.Max(0f, manaCostMultiplier);
        PostureCostMultiplier = Mathf.Max(0f, postureCostMultiplier);
    }

    public float OutgoingDamageMultiplier { get; }
    public float IncomingDamageMultiplier { get; }
    public float OutgoingHealingMultiplier { get; }
    public float ManaCostMultiplier { get; }
    public float PostureCostMultiplier { get; }

    public static DRPGStatusActionModifiers Neutral =>
        new DRPGStatusActionModifiers(1f, 1f, 1f, 1f, 1f);
}

public readonly struct DRPGStatusTurnResult
{
    public DRPGStatusTurnResult(
        float hpDelta,
        float postureDelta,
        bool changed,
        IReadOnlyList<string> expiredStatusIds)
    {
        HpDelta = hpDelta;
        PostureDelta = postureDelta;
        Changed = changed;
        ExpiredStatusIds = expiredStatusIds ?? Array.Empty<string>();
    }

    public float HpDelta { get; }
    public float PostureDelta { get; }
    public bool Changed { get; }
    public IReadOnlyList<string> ExpiredStatusIds { get; }
}

public sealed class DRPGStatusRuntimeCollection
{
    private DRPGBattleStateMutationCapability stateMutationCapability;
    private int stateMutationScopeDepth;
    private readonly ParticipantId ownerParticipantId;
    private long nextInsertionOrdinal = 1;
    private long observedBoundarySerial;

    public DRPGStatusRuntimeCollection(ParticipantId ownerParticipantId = default)
    { this.ownerParticipantId = ownerParticipantId; }

    private sealed class Entry
    {
        public Entry(DRPGStatusRuntimeSnapshot snapshot)
        {
            Spec = snapshot.Spec;
            Stacks = snapshot.Stacks;
            RemainingTurns = snapshot.RemainingTurns;
            Identity = snapshot;
            LastConsumedBoundarySerial = snapshot.LastConsumedBoundarySerial;
        }

        public DRPGResolvedStatusSpec Spec;
        public int Stacks;
        public int RemainingTurns;
        public readonly DRPGStatusRuntimeSnapshot Identity;
        public long LastConsumedBoundarySerial;
        public DRPGStatusRuntimeSnapshot Snapshot() => new DRPGStatusRuntimeSnapshot(Spec, Stacks, RemainingTurns,
            Identity.StatusInstanceId, Identity.OwnerParticipantId, Identity.SourceParticipantId, Identity.StableInsertionOrdinal,
            Identity.AppliedBoundarySerial, Identity.EligibleFromBoundarySerial, LastConsumedBoundarySerial);
    }

    private List<Entry> entries = new List<Entry>();

    // A detached copy never owns the live collection's capability or mutable entries.
    internal static DRPGStatusRuntimeCollection FromSnapshot(IReadOnlyList<DRPGStatusRuntimeSnapshot> snapshot,
        ParticipantId owner = default)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        var complete = snapshot as DRPGStatusCollectionSnapshot;
        if (complete != null)
        {
            if (owner.Value > 0 && owner != complete.OwnerParticipantId)
                throw new InvalidOperationException("A status snapshot belongs to another owner.");
            owner = complete.OwnerParticipantId;
        }
        var copy = new DRPGStatusRuntimeCollection(owner);
        copy.observedBoundarySerial = complete != null ? complete.ObservedBoundarySerial : 0;
        if (copy.observedBoundarySerial < 0) throw new InvalidOperationException("Invalid captured status boundary serial.");
        foreach (var value in snapshot)
        {
            if (value.Spec == null) throw new InvalidOperationException("A status snapshot has no frozen definition.");
            value.Spec.ValidateForTransaction();
            var migrated = value;
            if (string.IsNullOrEmpty(value.StatusInstanceId))
                migrated = copy.NewSnapshot(value.Spec, value.Stacks, value.RemainingTurns, owner, 0);
            if (migrated.OwnerParticipantId != owner || migrated.StableInsertionOrdinal <= 0 ||
                migrated.AppliedBoundarySerial < 0 || migrated.EligibleFromBoundarySerial <= migrated.AppliedBoundarySerial ||
                migrated.LastConsumedBoundarySerial < 0 || migrated.LastConsumedBoundarySerial > copy.observedBoundarySerial ||
                value.Stacks < 1 || value.Stacks > value.Spec.MaxStacks || value.RemainingTurns < 0 ||
                copy.Find(value.StatusId, migrated.SourceParticipantId) != null ||
                copy.entries.Exists(e => e.Identity.StatusInstanceId == migrated.StatusInstanceId ||
                    e.Identity.StableInsertionOrdinal == migrated.StableInsertionOrdinal))
                throw new InvalidOperationException("Invalid or duplicated captured status.");
            copy.entries.Add(new Entry(migrated));
            copy.nextInsertionOrdinal = Math.Max(copy.nextInsertionOrdinal, checked(migrated.StableInsertionOrdinal + 1));
        }
        if (complete != null)
        {
            if (complete.NextInsertionOrdinal < copy.nextInsertionOrdinal)
                throw new InvalidOperationException("The captured status insertion sequence moved backwards.");
            copy.nextInsertionOrdinal = complete.NextInsertionOrdinal;
        }
        return copy;
    }

    internal sealed class PreparedJournal
    {
        private readonly DRPGStatusRuntimeCollection owner;
        private readonly List<Entry> replacement;
        private readonly long replacementNextOrdinal;
        private readonly long replacementObservedBoundary;
        private bool applied;

        internal PreparedJournal(DRPGStatusRuntimeCollection owner, DRPGBattleStateMutationCapability capability,
            IReadOnlyList<DRPGStatusMutation> mutations)
        {
            owner.RequireStateCapability(capability);
            this.owner = owner;
            var draft = FromSnapshot(owner.Snapshot());
            draft.ApplyDetachedMutations(mutations);
            replacement = draft.entries;
            replacementNextOrdinal = draft.nextInsertionOrdinal;
            replacementObservedBoundary = draft.observedBoundarySerial;
        }

        internal void Apply(DRPGBattleStateMutationCapability capability)
        {
            owner.RequireStateCapability(capability);
            if (applied) return;
            // All allocation, arithmetic and rule validation happened in prepare.
            owner.entries = replacement;
            owner.nextInsertionOrdinal = replacementNextOrdinal;
            owner.observedBoundarySerial = replacementObservedBoundary;
            applied = true;
        }
    }

    internal void ApplyDetachedMutations(IReadOnlyList<DRPGStatusMutation> mutations)
    {
        if (stateMutationCapability != null)
            throw new InvalidOperationException("Status projection requires a detached collection.");
        foreach (var mutation in mutations)
        {
            if (mutation.Kind == DRPGStatusMutationKind.Apply)
            {
                if (mutation.Spec == null || mutation.StatusId != mutation.Spec.StatusId || mutation.Stacks <= 0 || mutation.Duration < 0)
                    throw new InvalidOperationException("Invalid applied status mutation.");
                mutation.Spec.ValidateForTransaction();
                ApplyCore(mutation.Spec, mutation.Stacks, mutation.Duration, mutation.SourceParticipantId, mutation.AppliedBoundarySerial);
            }
            else if (mutation.Kind == DRPGStatusMutationKind.Remove && !string.IsNullOrWhiteSpace(mutation.StatusId))
                RemoveCore(mutation.StatusId);
            else if (mutation.Kind == DRPGStatusMutationKind.ConsumeBoundary)
            {
                var before = mutation.BoundaryBefore;
                var current = entries.Find(e => e.Identity.StatusInstanceId == before.StatusInstanceId);
                if (current == null || !current.Snapshot().Same(before) || mutation.Spec == null ||
                    !mutation.Spec.HasSameValues(before.Spec) || mutation.StatusId != before.StatusId ||
                    mutation.Stacks != before.Stacks || mutation.Duration != before.RemainingTurns ||
                    mutation.SourceParticipantId != before.SourceParticipantId || mutation.AppliedBoundarySerial != before.AppliedBoundarySerial ||
                    !TryConsumeBoundaryInstance(mutation.Boundary, before.StatusInstanceId, out _))
                    throw new InvalidOperationException("Status boundary consumption no longer matches its frozen instance.");
            }
            else throw new InvalidOperationException("Invalid status mutation kind or identity.");
        }
        var modifiers = ResolveActionModifiers();
        if (float.IsInfinity(modifiers.IncomingDamageMultiplier) || float.IsInfinity(modifiers.OutgoingDamageMultiplier) ||
            float.IsInfinity(modifiers.OutgoingHealingMultiplier) || float.IsInfinity(modifiers.ManaCostMultiplier) ||
            float.IsInfinity(modifiers.PostureCostMultiplier) || float.IsNaN(modifiers.IncomingDamageMultiplier) ||
            float.IsNaN(modifiers.OutgoingDamageMultiplier) || float.IsNaN(modifiers.OutgoingHealingMultiplier) ||
            float.IsNaN(modifiers.ManaCostMultiplier) || float.IsNaN(modifiers.PostureCostMultiplier))
            throw new InvalidOperationException("Combined status modifiers are not finite.");
    }

    public int Count => entries.Count;

    public bool Contains(string statusId)
    {
        return Find(statusId) != null;
    }

    public DRPGStatusCollectionSnapshot Snapshot()
    {
        var result = new DRPGStatusRuntimeSnapshot[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            result[i] = entry.Snapshot();
        }
        return new DRPGStatusCollectionSnapshot(ownerParticipantId, nextInsertionOrdinal, result, observedBoundarySerial);
    }

    public DRPGStatusRuntimeSnapshot Apply(
        DRPGResolvedStatusSpec spec,
        int stacks = 1,
        int durationOverride = 0,
        ParticipantId sourceParticipantId = default,
        long appliedBoundarySerial = 0)
    {
        RequireDirectMutationAccess();
        return ApplyCore(spec, stacks, durationOverride, sourceParticipantId, appliedBoundarySerial);
    }

    internal DRPGStatusRuntimeSnapshot Apply(
        DRPGBattleStateMutationCapability capability,
        DRPGResolvedStatusSpec spec,
        int stacks = 1,
        int durationOverride = 0)
    {
        RequireStateCapability(capability);
        return ApplyCore(spec, stacks, durationOverride);
    }

    private DRPGStatusRuntimeSnapshot ApplyCore(
        DRPGResolvedStatusSpec spec,
        int stacks,
        int durationOverride,
        ParticipantId sourceParticipantId = default,
        long appliedBoundarySerial = 0)
    {
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));
        if (appliedBoundarySerial < 0) throw new ArgumentOutOfRangeException(nameof(appliedBoundarySerial));

        int incomingStacks = Mathf.Max(1, stacks);
        int incomingDuration = durationOverride > 0
            ? durationOverride
            : spec.DurationTurns;
        if (sourceParticipantId.Value == 0) sourceParticipantId = ownerParticipantId;
        Entry entry = Find(spec.StatusId, sourceParticipantId);
        if (entry == null)
        {
            entry = new Entry(NewSnapshot(spec, Mathf.Min(spec.MaxStacks, incomingStacks), incomingDuration,
                sourceParticipantId, Math.Max(appliedBoundarySerial, observedBoundarySerial)));
            entries.Add(entry);
        }
        else
        {
            int nextStacks = (int)Math.Min(entry.Spec.MaxStacks, (long)entry.Stacks + incomingStacks);
            int nextDuration;
            switch (entry.Spec.RefreshPolicy)
            {
                case DRPGStatusRefreshPolicy.AddDuration:
                    nextDuration = checked(entry.RemainingTurns + incomingDuration);
                    break;
                case DRPGStatusRefreshPolicy.KeepLongerDuration:
                    nextDuration = Mathf.Max(
                        entry.RemainingTurns,
                        incomingDuration);
                    break;
                default:
                    nextDuration = incomingDuration;
                    break;
            }
            entry.Stacks = nextStacks;
            entry.RemainingTurns = nextDuration;
        }

        return entry.Snapshot();
    }

    private DRPGStatusRuntimeSnapshot NewSnapshot(DRPGResolvedStatusSpec spec, int stacks, int duration, ParticipantId source,
        long appliedBoundarySerial)
    {
        long ordinal = nextInsertionOrdinal;
        long next = checked(ordinal + 1);
        long eligible = checked(appliedBoundarySerial + 1);
        string id = ownerParticipantId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
            source.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
            appliedBoundarySerial.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
            ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var result = new DRPGStatusRuntimeSnapshot(spec, stacks, duration, id, ownerParticipantId, source, ordinal,
            appliedBoundarySerial, eligible);
        nextInsertionOrdinal = next;
        return result;
    }

    public bool Remove(string statusId)
    {
        RequireDirectMutationAccess();
        return RemoveCore(statusId);
    }

    internal bool Remove(
        DRPGBattleStateMutationCapability capability,
        string statusId)
    {
        RequireStateCapability(capability);
        return RemoveCore(statusId);
    }

    private bool RemoveCore(string statusId)
    {
        bool removed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(
                    entries[i].Spec.StatusId,
                    statusId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            entries.RemoveAt(i);
            removed = true;
        }
        return removed;
    }

    public DRPGStatusActionModifiers ResolveActionModifiers()
    {
        float outgoingDamage = 1f;
        float incomingDamage = 1f;
        float outgoingHealing = 1f;
        float manaCost = 1f;
        float postureCost = 1f;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            outgoingDamage *= StackMultiplier(
                entry.Spec.OutgoingDamageMultiplier,
                entry.Stacks);
            incomingDamage *= StackMultiplier(
                entry.Spec.IncomingDamageMultiplier,
                entry.Stacks);
            outgoingHealing *= StackMultiplier(
                entry.Spec.OutgoingHealingMultiplier,
                entry.Stacks);
            manaCost *= StackMultiplier(
                entry.Spec.ManaCostMultiplier,
                entry.Stacks);
            postureCost *= StackMultiplier(
                entry.Spec.PostureCostMultiplier,
                entry.Stacks);
        }
        return new DRPGStatusActionModifiers(
            outgoingDamage,
            incomingDamage,
            outgoingHealing,
            manaCost,
            postureCost);
    }

    public DRPGStatusTurnResult TickTurnStart()
    {
        RequireDirectMutationAccess();
        return TickTurnStartCore();
    }

    internal DRPGStatusTurnResult TickTurnStart(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        return TickTurnStartCore();
    }

    private DRPGStatusTurnResult TickTurnStartCore()
    {
        // Compatibility entry point only. Task10D replaces its presentation with
        // TurnEntry; this aggregate remains for old callers until that migration.
        long serial = observedBoundarySerial;
        foreach (var entry in entries) serial = Math.Max(serial, entry.Identity.AppliedBoundarySerial);
        serial = checked(serial + 1);
        // Standalone legacy collections have no participant identity. Their old
        // interface remains numerical; battle-owned collections always have one.
        float hpDelta = 0f;
        float postureDelta = 0f;
        var expired = new List<string>();
        bool changed = false;
        var prepared = new List<DRPGStatusBoundaryStep>();
        foreach (var entry in entries)
        {
            if (entry.Spec.TriggerTiming != DRPGStatusTriggerTiming.TurnStart) continue;
            var step = PrepareBoundaryStep(entry, serial);
            prepared.Add(step);
            hpDelta += step.HpDelta; postureDelta += step.PostureDelta;
        }
        RequireFiniteDelta(hpDelta); RequireFiniteDelta(postureDelta);
        foreach (var step in prepared)
        {
            ApplyBoundaryStep(step);
            changed = true;
            if (step.IsExpired) expired.Add(step.Before.StatusId);
        }
        observedBoundarySerial = serial;
        return new DRPGStatusTurnResult(
            hpDelta,
            postureDelta,
            changed,
            expired.AsReadOnly());
    }

    internal IReadOnlyList<DRPGStatusRuntimeSnapshot> GetDueAtBoundary(DRPGStatusBoundary boundary)
    {
        var result = new List<DRPGStatusRuntimeSnapshot>();
        foreach (var entry in entries) if (IsDue(entry, boundary)) result.Add(entry.Snapshot());
        return result.AsReadOnly();
    }

    internal bool TryConsumeBoundaryInstance(DRPGStatusBoundary boundary, string instanceId, out DRPGStatusBoundaryStep step)
    {
        RequireDirectMutationAccess();
        step = default;
        var entry = entries.Find(e => e.Identity.StatusInstanceId == instanceId);
        if (entry == null || !IsDue(entry, boundary)) return false;
        step = PrepareBoundaryStep(entry, boundary.Serial);
        ApplyBoundaryStep(step);
        return true;
    }

    private bool IsDue(Entry entry, DRPGStatusBoundary boundary) => boundary.Affects(ownerParticipantId) &&
        boundary.Timing == entry.Spec.TriggerTiming && boundary.Serial >= observedBoundarySerial &&
        boundary.Serial >= entry.Identity.EligibleFromBoundarySerial && boundary.Serial > entry.LastConsumedBoundarySerial &&
        entry.RemainingTurns > 0;

    private static DRPGStatusBoundaryStep PrepareBoundaryStep(Entry entry, long serial)
    {
        var before = entry.Snapshot();
        float hp = entry.Spec.TurnStartHpDelta * entry.Stacks;
        float posture = entry.Spec.TurnStartPostureDelta * entry.Stacks;
        RequireFiniteDelta(hp); RequireFiniteDelta(posture);
        var after = new DRPGStatusRuntimeSnapshot(before.Spec, before.Stacks, Math.Max(0, before.RemainingTurns - 1),
            before.StatusInstanceId, before.OwnerParticipantId, before.SourceParticipantId, before.StableInsertionOrdinal,
            before.AppliedBoundarySerial, before.EligibleFromBoundarySerial, serial);
        return new DRPGStatusBoundaryStep(before, after, hp, posture);
    }

    private void ApplyBoundaryStep(DRPGStatusBoundaryStep step)
    {
        var entry = entries.Find(e => e.Identity.StatusInstanceId == step.Before.StatusInstanceId);
        entry.RemainingTurns = step.After.RemainingTurns;
        entry.LastConsumedBoundarySerial = step.After.LastConsumedBoundarySerial;
        observedBoundarySerial = Math.Max(observedBoundarySerial, step.After.LastConsumedBoundarySerial);
        if (step.IsExpired) entries.Remove(entry);
    }

    private static void RequireFiniteDelta(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new InvalidOperationException("A status boundary delta is not finite.");
    }

    internal void AttachStateMutationCapability(
        DRPGBattleStateMutationCapability capability)
    {
        ValidateStateMutationCapabilityAttach(capability);
        stateMutationCapability = capability;
    }

    internal void ValidateStateMutationCapabilityAttach(
        DRPGBattleStateMutationCapability capability)
    {
        if (capability == null)
            throw new ArgumentNullException(nameof(capability));
        if (stateMutationCapability != null &&
            !ReferenceEquals(stateMutationCapability, capability))
        {
            throw new InvalidOperationException(
                "The status collection already belongs to another battle state.");
        }
    }

    internal void EnterStateMutationScope(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        stateMutationScopeDepth++;
    }

    internal void ExitStateMutationScope(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        if (stateMutationScopeDepth <= 0)
            throw new InvalidOperationException("Status mutation scope underflow.");
        stateMutationScopeDepth--;
    }

    private void RequireDirectMutationAccess()
    {
        if (stateMutationCapability != null && stateMutationScopeDepth <= 0)
        {
            throw new InvalidOperationException(
                "An attached status collection can mutate only through " +
                "its owning battle state.");
        }
    }

    private void RequireStateCapability(
        DRPGBattleStateMutationCapability capability)
    {
        if (capability == null || stateMutationCapability == null ||
            !ReferenceEquals(capability, stateMutationCapability))
        {
            throw new InvalidOperationException(
                "The status mutation capability is missing or stale.");
        }
    }

    private Entry Find(string statusId, ParticipantId? source = null)
    {
        if (string.IsNullOrWhiteSpace(statusId))
            return null;
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(
                    entries[i].Spec.StatusId,
                    statusId,
                    StringComparison.Ordinal) && (!source.HasValue || entries[i].Identity.SourceParticipantId == source.Value))
            {
                return entries[i];
            }
        }
        return null;
    }

    private static float StackMultiplier(float perStackMultiplier, int stacks)
    {
        return Mathf.Max(
            0f,
            1f + (perStackMultiplier - 1f) * Mathf.Max(1, stacks));
    }
}
