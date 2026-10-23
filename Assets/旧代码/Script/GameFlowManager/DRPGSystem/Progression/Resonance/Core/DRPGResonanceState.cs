using System;
using System.Collections.Generic;

public sealed class DRPGResonanceState
{
    private readonly Dictionary<string, HashSet<string>> unlockedByBranch =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> equippedActiveByBranch =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public string ResonancePartnerCharacterId { get; private set; } =
        string.Empty;
    internal long Revision { get; private set; }

    public bool IsNodeUnlocked(string branchId, string nodeId)
    {
        return !string.IsNullOrWhiteSpace(branchId) &&
            !string.IsNullOrWhiteSpace(nodeId) &&
            unlockedByBranch.TryGetValue(branchId, out HashSet<string> nodes) &&
            nodes.Contains(nodeId);
    }

    public string GetEquippedActiveSkillId(string partnerCharacterId)
    {
        if (string.IsNullOrWhiteSpace(partnerCharacterId))
            return string.Empty;
        return equippedActiveByBranch.TryGetValue(
            partnerCharacterId,
            out string skillId)
                ? skillId
                : string.Empty;
    }

    public IReadOnlyCollection<string> GetUnlockedNodeIds(string branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId) ||
            !unlockedByBranch.TryGetValue(branchId, out HashSet<string> nodes))
        {
            return Array.Empty<string>();
        }
        return new List<string>(nodes).AsReadOnly();
    }

    internal void SetPartner(string characterId)
    {
        if (ResonancePartnerCharacterId == (characterId ?? string.Empty)) return;
        Revision = checked(Revision + 1);
        ResonancePartnerCharacterId = characterId ?? string.Empty;
    }

    internal void Unlock(string branchId, string nodeId)
    {
        if (!unlockedByBranch.TryGetValue(branchId, out HashSet<string> nodes))
        {
            nodes = new HashSet<string>(StringComparer.Ordinal);
            unlockedByBranch.Add(branchId, nodes);
        }
        if (nodes.Add(nodeId)) Revision = checked(Revision + 1);
    }

    internal void SetEquippedActive(
        string partnerCharacterId,
        string skillId)
    {
        if (GetEquippedActiveSkillId(partnerCharacterId) == (skillId ?? string.Empty)) return;
        Revision = checked(Revision + 1);
        equippedActiveByBranch[partnerCharacterId] = skillId ?? string.Empty;
    }

    internal void CopyFrom(DRPGResonanceState source)
    {
        Revision = checked(Revision + 1);
        unlockedByBranch.Clear();
        equippedActiveByBranch.Clear();
        SetPartner(source != null
            ? source.ResonancePartnerCharacterId
            : string.Empty);
        if (source == null)
            return;
        foreach (KeyValuePair<string, HashSet<string>> pair in
            source.unlockedByBranch)
        {
            unlockedByBranch[pair.Key] = new HashSet<string>(
                pair.Value,
                StringComparer.Ordinal);
        }
        foreach (KeyValuePair<string, string> pair in
            source.equippedActiveByBranch)
        {
            equippedActiveByBranch[pair.Key] = pair.Value;
        }
    }
}
