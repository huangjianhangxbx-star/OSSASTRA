using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGSkillTree",
    menuName = "ANKALUTE/DRPG/Progression/Skill Tree")]
public sealed class DRPGSkillTreeSO : ScriptableObject
{
    [SerializeField] private string treeId = "skill_tree";
    [SerializeField] private string displayName = "Skill Tree";
    [SerializeField] private List<DRPGSkillTreeNodeDefinition> nodes =
        new List<DRPGSkillTreeNodeDefinition>();

    public string TreeId => treeId;
    public string DisplayName => displayName;
    public IReadOnlyList<DRPGSkillTreeNodeDefinition> Nodes =>
        nodes ?? (IReadOnlyList<DRPGSkillTreeNodeDefinition>)
            Array.Empty<DRPGSkillTreeNodeDefinition>();

    public DRPGSkillTreeNodeDefinition FindNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || nodes == null)
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            DRPGSkillTreeNodeDefinition node = nodes[i];
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

    public void ValidateOrThrow()
    {
        var byId = new Dictionary<string, DRPGSkillTreeNodeDefinition>(
            StringComparer.Ordinal);
        IReadOnlyList<DRPGSkillTreeNodeDefinition> source = Nodes;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGSkillTreeNodeDefinition node = source[i];
            if (node == null)
                throw Invalid("Node at index " + i + " is null.");
            if (string.IsNullOrWhiteSpace(node.NodeId))
                throw Invalid("Node at index " + i + " has a blank ID.");
            if (!byId.TryAdd(node.NodeId, node))
                throw Invalid("Duplicate node ID: " + node.NodeId);
            if (!node.HasValidReward)
                throw Invalid("Node has no valid reward: " + node.NodeId);
        }

        foreach (KeyValuePair<string, DRPGSkillTreeNodeDefinition> pair in byId)
        {
            IReadOnlyList<string> prerequisites = pair.Value.PrerequisiteNodeIds;
            for (int i = 0; i < prerequisites.Count; i++)
            {
                string prerequisiteId = prerequisites[i];
                if (string.IsNullOrWhiteSpace(prerequisiteId) ||
                    !byId.ContainsKey(prerequisiteId))
                {
                    throw Invalid(
                        "Missing prerequisite '" + prerequisiteId +
                        "' for node " + pair.Key);
                }
            }
        }

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in byId.Keys)
            Visit(id, byId, state);
    }

    private void Visit(
        string id,
        IReadOnlyDictionary<string, DRPGSkillTreeNodeDefinition> byId,
        IDictionary<string, int> state)
    {
        if (state.TryGetValue(id, out int visitState))
        {
            if (visitState == 1)
                throw Invalid("Cycle detected at node: " + id);
            if (visitState == 2)
                return;
        }

        state[id] = 1;
        IReadOnlyList<string> prerequisites = byId[id].PrerequisiteNodeIds;
        for (int i = 0; i < prerequisites.Count; i++)
            Visit(prerequisites[i], byId, state);
        state[id] = 2;
    }

    private InvalidOperationException Invalid(string message)
    {
        return new InvalidOperationException(
            "Invalid DRPG skill tree '" + treeId + "': " + message);
    }
}
