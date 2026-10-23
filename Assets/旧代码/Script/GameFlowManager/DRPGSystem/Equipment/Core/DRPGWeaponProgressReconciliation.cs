using System;
using System.Collections.Generic;

// Immutable rule-only facts. Both snapshots and the request contain values, not
// a live actor, weapon instance, authored resource asset or mutable store.
internal sealed class DRPGWeaponProgressReconciliation
{
    internal DRPGWeaponProgressReconciliation(ActionIdentity identity, string weaponId,
        DRPGProgressSnapshot before, DRPGProgressSnapshot after, DRPGProgressMutationResult mutation)
    { Identity = identity; WeaponId = weaponId; Before = before; After = after; Mutation = mutation; }
    internal ActionIdentity Identity { get; }
    internal string WeaponId { get; }
    internal DRPGProgressSnapshot Before { get; }
    internal DRPGProgressSnapshot After { get; }
    internal DRPGProgressMutationResult Mutation { get; }

    internal bool Matches(DRPGWeaponProgressReconciliation other)
    {
        if (other == null || other.Before == null || other.After == null || other.Mutation == null ||
            Identity != other.Identity || WeaponId != other.WeaponId ||
            Before.OwnerStableId != other.Before.OwnerStableId || After.OwnerStableId != other.After.OwnerStableId ||
            !Before.Definition.HasSameValues(other.Before.Definition) || !After.Definition.HasSameValues(other.After.Definition) ||
            Before.CurrentValue != other.Before.CurrentValue || After.CurrentValue != other.After.CurrentValue) return false;
        var left = Mutation; var right = other.Mutation;
        return left.State == right.State && left.ValueBefore == right.ValueBefore && left.ValueAfter == right.ValueAfter &&
            left.Capacity == right.Capacity && left.AppliedAmount == right.AppliedAmount && left.OverflowAmount == right.OverflowAmount &&
            left.Request.SourceId == right.Request.SourceId && left.Request.DeduplicationKey == right.Request.DeduplicationKey &&
            left.Request.OwnerStableId == right.Request.OwnerStableId && left.Request.ResourceId == right.Request.ResourceId &&
            left.Request.Scope == right.Request.Scope && left.Request.Kind == right.Request.Kind && left.Request.Amount == right.Request.Amount &&
            left.Request.Priority == right.Request.Priority && left.Request.IncludeReactionDepth == right.Request.IncludeReactionDepth &&
            left.Request.Definition.HasSameValues(right.Request.Definition);
    }
}

internal sealed partial class DRPGBattleEquipmentSnapshot
{
    internal IReadOnlyList<DRPGWeaponProgressReconciliation> BuildProgressReconciliations(ActionIdentity identity,
        IReadOnlyList<DRPGWeaponCompletionMutation> completions, DRPGBattleProgressLedger afterAction,
        IReadOnlyList<DRPGProgressSnapshot> capturedProgress)
    {
        var result = new List<DRPGWeaponProgressReconciliation>();
        if (completions.Count == 0) return result.AsReadOnly();
        if (!identity.IsValid) throw new InvalidOperationException("Weapon progress reconciliation requires a Root Action identity.");
        var detached = afterAction.Fork();
        var values = afterAction.CreateSnapshot();
        foreach (var completion in completions)
        {
            var plan = completion.Plan;
            if (!weapons.TryGetValue(plan.WeaponId, out var captured) || !ReferenceEquals(plan, Plan(plan.WeaponId, plan.Units)))
                throw new InvalidOperationException("Weapon progress completion belongs to another captured equipment plan.");
            foreach (var before in values)
            {
                if (before.OwnerStableId != plan.CharacterId) continue;
                float previousModifier = CapacityModifier(captured.Build.Equipment, before.ResourceId);
                float nextModifier = CapacityModifier(plan.ReplacementBuild.Equipment, before.ResourceId);
                if (previousModifier == nextModifier) continue;
                DRPGProgressSnapshot original = null;
                foreach (var candidate in capturedProgress)
                    if (candidate.OwnerStableId == before.OwnerStableId && candidate.ResourceId == before.ResourceId) { original = candidate; break; }
                if (original == null || !original.Definition.HasSameValues(before.Definition) ||
                    before.Definition.EquipmentCapacityModifier != previousModifier)
                    throw new InvalidOperationException("Weapon capacity provenance or its captured progress definition changed during the action.");
                var definition = original.Definition.WithEquipmentCapacityModifier(nextModifier);
                var request = DRPGProgressMutationRequest.Register("weapon.completion.capacity",
                    DRPGBattleActionResourceWorkspace.WeaponActionKey(identity, plan.WeaponId) + ":capacity:" + before.ResourceId,
                    int.MaxValue, before.OwnerStableId, definition);
                var mutation = detached.Apply(request);
                if (!mutation.WasApplied || !detached.TryGetSnapshot(before.OwnerStableId, before.ResourceId, out var after))
                    throw new InvalidOperationException("Weapon progress reconciliation could not be prepared.");
                result.Add(new DRPGWeaponProgressReconciliation(identity, plan.WeaponId, before, after, mutation));
            }
        }
        return result.AsReadOnly();
    }

    private static float CapacityModifier(DRPGResolvedEquipmentSnapshot equipment, string resourceId)
    {
        float total = 0f;
        foreach (var modifier in equipment.ProgressCapacityModifiers)
            if (modifier.ResourceId == resourceId) total += modifier.CapacityModifier;
        if (float.IsNaN(total) || float.IsInfinity(total)) throw new InvalidOperationException("Weapon progress capacity modifier is not finite.");
        return total;
    }
}
