using System;
using System.Collections.Generic;

public sealed class DRPGProfessionProgressState
{
    private readonly HashSet<string> grantedNodeIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> purchasedNodeIds =
        new HashSet<string>(StringComparer.Ordinal);

    public DRPGProfessionProgressState(
        DRPGProfessionSO profession,
        bool isProfessionUnlocked = true)
    {
        Profession = profession ?? throw new ArgumentNullException(nameof(profession));
        IsProfessionUnlocked = isProfessionUnlocked;
        Loadout = new DRPGSkillLoadout();
    }

    public DRPGProfessionSO Profession { get; }
    public bool IsProfessionUnlocked { get; private set; }
    public int SpentPoints { get; private set; }
    public DRPGSkillLoadout Loadout { get; }
    public IReadOnlyCollection<string> GrantedNodeIds => grantedNodeIds;
    public IReadOnlyCollection<string> PurchasedNodeIds => purchasedNodeIds;

    public bool IsUnlocked(string nodeId)
    {
        return grantedNodeIds.Contains(nodeId) || purchasedNodeIds.Contains(nodeId);
    }

    public bool UnlockProfession()
    {
        if (IsProfessionUnlocked)
            return false;
        IsProfessionUnlocked = true;
        return true;
    }

    public void GrantNode(string nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
            grantedNodeIds.Add(nodeId);
    }

    public void Purchase(string nodeId, int pointCost)
    {
        purchasedNodeIds.Add(nodeId);
        SpentPoints += Math.Max(0, pointCost);
    }

    public void ResetPurchased()
    {
        purchasedNodeIds.Clear();
        SpentPoints = 0;
    }

    public void RestorePurchased(
        IEnumerable<string> nodeIds,
        DRPGSkillTreeSO tree)
    {
        purchasedNodeIds.Clear();
        SpentPoints = 0;
        if (nodeIds == null || tree == null)
            return;

        foreach (string nodeId in nodeIds)
        {
            DRPGSkillTreeNodeDefinition node = tree.FindNode(nodeId);
            if (node == null || grantedNodeIds.Contains(nodeId))
                continue;
            purchasedNodeIds.Add(nodeId);
            SpentPoints += Math.Max(0, node.PointCost);
        }
    }
}
