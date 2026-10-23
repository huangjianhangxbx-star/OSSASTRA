using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DRPGSkillTreeNodeDefinition
{
    [SerializeField] private string nodeId = "node";
    [SerializeField] private string displayName = "Node";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Vector2 layoutPosition;
    [Min(0)]
    [SerializeField] private int pointCost = 1;
    [SerializeField] private List<string> prerequisiteNodeIds =
        new List<string>();
    [SerializeField] private DRPGSkillTreeRewardKind rewardKind =
        DRPGSkillTreeRewardKind.Skill;
    [SerializeField] private DRPGSkillSO skill;
    [SerializeField] private DRPGPassiveModifierSO passiveModifier;

    public string NodeId => nodeId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? nodeId
        : displayName;
    public string Description => description;
    public Vector2 LayoutPosition => layoutPosition;
    public int PointCost => Mathf.Max(0, pointCost);
    public IReadOnlyList<string> PrerequisiteNodeIds =>
        prerequisiteNodeIds ?? (IReadOnlyList<string>)Array.Empty<string>();
    public DRPGSkillTreeRewardKind RewardKind => rewardKind;
    public DRPGSkillSO Skill => skill;
    public DRPGPassiveModifierSO PassiveModifier => passiveModifier;

    public bool HasValidReward => rewardKind == DRPGSkillTreeRewardKind.Skill
        ? skill != null
        : passiveModifier != null;
}
