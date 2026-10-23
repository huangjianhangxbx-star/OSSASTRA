using System;
using System.Collections.Generic;

internal sealed class DRPGInventoryWeaponOwnershipSnapshot
{
    private readonly HashSet<string> equippedIds;
    internal DRPGInventoryWeaponOwnershipSnapshot(int revision, long epoch, IEnumerable<string> equipped, IReadOnlyDictionary<string, int> fragments)
    {
        Revision = revision;
        MutationEpoch = epoch;
        equippedIds = new HashSet<string>(equipped, StringComparer.Ordinal);
        Equipped = Array.AsReadOnly(new List<string>(equippedIds).ToArray());
        var copy = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in fragments) copy.Add(pair.Key, pair.Value);
        Fragments = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(copy);
    }
    internal int Revision { get; }
    internal long MutationEpoch { get; }
    internal IReadOnlyList<string> Equipped { get; }
    internal IReadOnlyDictionary<string, int> Fragments { get; }
    internal bool Contains(string weaponId) => equippedIds.Contains(weaponId);
    internal bool MatchesEquipped(IEnumerable<string> ids) => equippedIds.SetEquals(ids);
}

internal sealed class DRPGPreparedInventoryWeaponBreaks
{
    internal DRPGPreparedInventoryWeaponBreaks(HashSet<string> equipped, Dictionary<string, int> fragments, int revision)
    { Equipped = equipped; Fragments = fragments; Revision = revision; }
    internal HashSet<string> Equipped { get; }
    internal Dictionary<string, int> Fragments { get; }
    internal int Revision { get; }
}

public sealed partial class DRPGPartyInventoryState
{
    internal DRPGInventoryWeaponOwnershipSnapshot CaptureWeaponOwnership() =>
        new DRPGInventoryWeaponOwnershipSnapshot(Revision, MutationEpoch, equippedWeaponInstanceIds, weaponFragmentCounts);

    internal bool MatchesWeaponOwnership(DRPGInventoryWeaponOwnershipSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Revision != Revision || snapshot.MutationEpoch != MutationEpoch ||
            !snapshot.MatchesEquipped(equippedWeaponInstanceIds) ||
            snapshot.Fragments.Count != weaponFragmentCounts.Count) return false;
        foreach (var pair in snapshot.Fragments)
            if (!weaponFragmentCounts.TryGetValue(pair.Key, out int count) || pair.Value != count) return false;
        return true;
    }

    internal DRPGPreparedInventoryWeaponBreaks PrepareWeaponBreaks(DRPGInventoryWeaponOwnershipSnapshot snapshot,
        IReadOnlyList<DRPGWeaponCompletionMutation> completions)
    {
        if (!MatchesWeaponOwnership(snapshot)) throw new InvalidOperationException("Weapon ownership or fragments changed before commit.");
        var equipped = new HashSet<string>(snapshot.Equipped, StringComparer.Ordinal);
        var fragments = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in snapshot.Fragments) fragments.Add(pair.Key, pair.Value);
        bool changed = false;
        foreach (var completion in completions)
        {
            var plan = completion.Plan;
            if (!plan.RemovesWeapon) continue;
            if (!equipped.Remove(plan.WeaponId)) throw new InvalidOperationException("Broken weapon is no longer reserved as equipped.");
            fragments.TryGetValue(plan.DefinitionId, out int previous);
            fragments[plan.DefinitionId] = checked(previous + 1);
            changed = true;
        }
        return changed ? new DRPGPreparedInventoryWeaponBreaks(equipped, fragments, checked(Revision + 1)) : null;
    }

    internal void ApplyPreparedWeaponBreaks(DRPGPreparedInventoryWeaponBreaks prepared)
    {
        if (prepared == null) return;
        equippedWeaponInstanceIds = prepared.Equipped;
        weaponFragmentCounts = prepared.Fragments;
        Revision = prepared.Revision;
    }
}
