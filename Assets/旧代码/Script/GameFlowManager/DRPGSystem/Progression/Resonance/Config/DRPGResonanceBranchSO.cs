using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGResonanceBranch",
    menuName = "ANKALUTE/DRPG/Resonance/Friend Branch")]
public sealed class DRPGResonanceBranchSO : ScriptableObject
{
    [SerializeField] private string branchId = "friend_branch";
    [SerializeField] private DRPGCombatantProfileSO friendProfile;
    [SerializeField] private List<DRPGResonanceNodeDefinition> nodes =
        new List<DRPGResonanceNodeDefinition>();

    public string BranchId => branchId;
    public DRPGCombatantProfileSO FriendProfile => friendProfile;
    public string FriendCharacterId => friendProfile != null
        ? friendProfile.CombatantId
        : string.Empty;
    public IReadOnlyList<DRPGResonanceNodeDefinition> Nodes =>
        nodes ?? (IReadOnlyList<DRPGResonanceNodeDefinition>)
            Array.Empty<DRPGResonanceNodeDefinition>();

    public DRPGResonanceNodeDefinition FindNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;
        for (int i = 0; i < Nodes.Count; i++)
        {
            DRPGResonanceNodeDefinition node = Nodes[i];
            if (node != null && string.Equals(
                    node.NodeId,
                    nodeId,
                    StringComparison.Ordinal))
            {
                return node;
            }
        }
        return null;
    }

    public DRPGResonanceNodeDefinition FindActiveSkillNode(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return null;
        for (int i = 0; i < Nodes.Count; i++)
        {
            DRPGResonanceNodeDefinition node = Nodes[i];
            if (node != null &&
                node.RewardKind == DRPGResonanceNodeRewardKind.ActiveSkill &&
                node.ActiveSkill != null &&
                string.Equals(
                    node.ActiveSkill.SkillId,
                    skillId,
                    StringComparison.Ordinal))
            {
                return node;
            }
        }
        return null;
    }
}
