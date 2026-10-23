using System;
using System.Collections.Generic;

public sealed class DRPGBattleProgressLedger : IDRPGProgressStore
{
    private Dictionary<Key, Entry> entries =
        new Dictionary<Key, Entry>();

    public DRPGProgressScope Scope => DRPGProgressScope.Battle;
    internal long Revision { get; private set; }

    public DRPGProgressMutationResult Apply(
        DRPGProgressMutationRequest request)
    {
        DRPGProgressMutationResult result = ApplyCore(request);
        if (result.WasApplied) Revision++;
        return result;
    }

    private DRPGProgressMutationResult ApplyCore(
        DRPGProgressMutationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (request.Scope != Scope)
        {
            return DRPGProgressMutationResult.Reject(
                request,
                DRPGProgressMutationState.RejectedUnsupportedScope,
                "Battle ledger cannot mutate " + request.Scope + ".");
        }

        var key = new Key(request.OwnerStableId, request.ResourceId);
        if (request.Kind == DRPGProgressMutationKind.Register)
            return Register(key, request);
        if (!entries.TryGetValue(key, out Entry entry))
        {
            return DRPGProgressMutationResult.Reject(
                request,
                DRPGProgressMutationState.RejectedInvalidResource,
                "Progress resource is not registered for this owner.");
        }

        float before = entry.CurrentValue;
        switch (request.Kind)
        {
            case DRPGProgressMutationKind.Gain:
            {
                float after = Math.Min(
                    entry.Definition.Capacity,
                    before + request.Amount);
                float applied = after - before;
                entry.CurrentValue = after;
                return Applied(
                    request,
                    before,
                    after,
                    entry.Definition.Capacity,
                    applied,
                    request.Amount - applied);
            }
            case DRPGProgressMutationKind.Spend:
                if (before + 0.0001f < request.Amount)
                {
                    return new DRPGProgressMutationResult(
                        request,
                        DRPGProgressMutationState.RejectedInsufficientValue,
                        before,
                        before,
                        entry.Definition.Capacity,
                        0f,
                        0f,
                        "Insufficient progress value.");
                }
                entry.CurrentValue = Math.Max(0f, before - request.Amount);
                return Applied(
                    request,
                    before,
                    entry.CurrentValue,
                    entry.Definition.Capacity,
                    request.Amount,
                    0f);
            case DRPGProgressMutationKind.Reset:
                entry.CurrentValue = entry.Definition.InitialValue;
                return Applied(
                    request,
                    before,
                    entry.CurrentValue,
                    entry.Definition.Capacity,
                    Math.Abs(before - entry.CurrentValue),
                    0f);
            default:
                return DRPGProgressMutationResult.Reject(
                    request,
                    DRPGProgressMutationState.RejectedInvalidRequest,
                    "Unsupported mutation kind.");
        }
    }

    public bool TryGetSnapshot(
        string ownerStableId,
        string resourceId,
        out DRPGProgressSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(ownerStableId) ||
            string.IsNullOrWhiteSpace(resourceId) ||
            !entries.TryGetValue(
                new Key(ownerStableId, resourceId),
                out Entry entry))
        {
            snapshot = null;
            return false;
        }

        snapshot = new DRPGProgressSnapshot(
            ownerStableId.Trim(),
            entry.Definition,
            entry.CurrentValue);
        return true;
    }

    public IReadOnlyList<DRPGProgressSnapshot> CreateSnapshot()
    {
        var snapshots = new List<DRPGProgressSnapshot>(entries.Count);
        foreach (KeyValuePair<Key, Entry> pair in entries)
        {
            snapshots.Add(new DRPGProgressSnapshot(
                pair.Key.OwnerStableId,
                pair.Value.Definition,
                pair.Value.CurrentValue));
        }
        snapshots.Sort((left, right) =>
        {
            int owner = string.CompareOrdinal(
                left.OwnerStableId,
                right.OwnerStableId);
            return owner != 0
                ? owner
                : string.CompareOrdinal(left.ResourceId, right.ResourceId);
        });
        return snapshots.Count == 0
            ? Array.Empty<DRPGProgressSnapshot>()
            : snapshots.AsReadOnly();
    }

    public void ResetAll()
    {
        foreach (Entry entry in entries.Values)
            entry.CurrentValue = entry.Definition.InitialValue;
        Revision++;
    }

    internal DRPGBattleProgressLedger Fork()
    {
        var copy = new DRPGBattleProgressLedger { Revision = Revision };
        foreach (var pair in entries)
            copy.entries.Add(pair.Key, new Entry(pair.Value.Definition) { CurrentValue = pair.Value.CurrentValue });
        return copy;
    }

    internal static DRPGBattleProgressLedger FromSnapshots(IReadOnlyList<DRPGProgressSnapshot> snapshots)
    {
        var ledger = new DRPGBattleProgressLedger();
        foreach (var snapshot in snapshots)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.OwnerStableId) ||
                snapshot.Scope != DRPGProgressScope.Battle || float.IsNaN(snapshot.CurrentValue) ||
                float.IsInfinity(snapshot.CurrentValue) || snapshot.CurrentValue < 0f || snapshot.CurrentValue > snapshot.Capacity)
                throw new InvalidOperationException("Invalid captured battle progress value.");
            ledger.entries.Add(new Key(snapshot.OwnerStableId, snapshot.ResourceId),
                new Entry(snapshot.Definition) { CurrentValue = snapshot.CurrentValue });
        }
        return ledger;
    }

    // Called only after State validates the complete journal. No rule callbacks,
    // allocations, or fallible per-request Apply calls belong in this phase.
    internal void ApplyPreparedValues(DRPGBattleProgressLedger prepared)
    {
        entries = prepared.entries;
        Revision++;
    }

    private DRPGProgressMutationResult Register(
        Key key,
        DRPGProgressMutationRequest request)
    {
        DRPGProgressResourceDefinition definition = request.Definition;
        if (definition == null || definition.Scope != Scope ||
            !string.Equals(
                definition.ResourceId,
                request.ResourceId,
                StringComparison.Ordinal))
        {
            return DRPGProgressMutationResult.Reject(
                request,
                DRPGProgressMutationState.RejectedInvalidRequest,
                "Registration definition does not match the request.");
        }

        if (entries.TryGetValue(key, out Entry existing))
        {
            float before = existing.CurrentValue;
            existing.Definition = definition;
            existing.CurrentValue = Math.Min(before, definition.Capacity);
            return Applied(
                request,
                before,
                existing.CurrentValue,
                definition.Capacity,
                0f,
                Math.Max(0f, before - existing.CurrentValue));
        }

        entries.Add(key, new Entry(definition));
        return Applied(
            request,
            0f,
            definition.InitialValue,
            definition.Capacity,
            definition.InitialValue,
            0f);
    }

    private static DRPGProgressMutationResult Applied(
        DRPGProgressMutationRequest request,
        float before,
        float after,
        float capacity,
        float applied,
        float overflow)
    {
        return new DRPGProgressMutationResult(
            request,
            DRPGProgressMutationState.Applied,
            before,
            after,
            capacity,
            applied,
            Math.Max(0f, overflow));
    }

    private sealed class Entry
    {
        public Entry(DRPGProgressResourceDefinition definition)
        {
            Definition = definition;
            CurrentValue = definition.InitialValue;
        }

        public DRPGProgressResourceDefinition Definition { get; set; }
        public float CurrentValue { get; set; }
    }

    private readonly struct Key : IEquatable<Key>
    {
        public Key(string ownerStableId, string resourceId)
        {
            OwnerStableId = ownerStableId.Trim();
            ResourceId = resourceId.Trim();
        }

        public string OwnerStableId { get; }
        public string ResourceId { get; }

        public bool Equals(Key other)
        {
            return string.Equals(
                    OwnerStableId,
                    other.OwnerStableId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ResourceId,
                    other.ResourceId,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Key other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((OwnerStableId != null
                    ? StringComparer.Ordinal.GetHashCode(OwnerStableId)
                    : 0) * 397) ^
                    (ResourceId != null
                        ? StringComparer.Ordinal.GetHashCode(ResourceId)
                        : 0);
            }
        }
    }
}
