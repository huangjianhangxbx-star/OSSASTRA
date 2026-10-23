using System;
using System.Collections.Generic;
using UnityEngine;

public enum DRPGResonanceNodeRewardKind
{
    ActiveSkill = 0,
    Passive = 1
}

[Serializable]
public sealed class DRPGResonanceNodeDefinition
{
    [SerializeField] private string nodeId = "resonance_node";
    [SerializeField] private string displayName = "同律节点";
    [TextArea]
    [SerializeField] private string description;
    [Min(0)]
    [SerializeField] private int requiredConditionValue;
    [SerializeField] private List<string> prerequisiteNodeIds =
        new List<string>();
    [SerializeField] private DRPGResonanceNodeRewardKind rewardKind;
    [SerializeField] private DRPGSkillSO activeSkill;
    [SerializeField] private DRPGPassiveModifierSO passiveModifier;
    [Min(1)]
    [SerializeField] private int requiredBattleProgress = 1;
    [SerializeField] private List<DRPGResonanceProgressSource> progressSources =
        new List<DRPGResonanceProgressSource>();

    public DRPGResonanceNodeDefinition()
    {
    }

    public DRPGResonanceNodeDefinition(
        string nodeId,
        int requiredConditionValue,
        DRPGResonanceNodeRewardKind rewardKind,
        DRPGSkillSO activeSkill,
        DRPGPassiveModifierSO passiveModifier)
    {
        this.nodeId = nodeId;
        this.requiredConditionValue = Mathf.Max(0, requiredConditionValue);
        this.rewardKind = rewardKind;
        this.activeSkill = activeSkill;
        this.passiveModifier = passiveModifier;
    }

    public string NodeId => nodeId;
    public string DisplayName => displayName;
    public string Description => description;
    public int RequiredConditionValue => Mathf.Max(0, requiredConditionValue);
    public IReadOnlyList<string> PrerequisiteNodeIds =>
        prerequisiteNodeIds ?? (IReadOnlyList<string>)Array.Empty<string>();
    public DRPGResonanceNodeRewardKind RewardKind => rewardKind;
    public DRPGSkillSO ActiveSkill => activeSkill;
    public DRPGPassiveModifierSO PassiveModifier => passiveModifier;
    public int RequiredBattleProgress => Mathf.Max(1, requiredBattleProgress);
    public IReadOnlyList<DRPGResonanceProgressSource> ProgressSources =>
        progressSources ?? (IReadOnlyList<DRPGResonanceProgressSource>)
            Array.Empty<DRPGResonanceProgressSource>();
    public bool HasValidReward =>
        rewardKind == DRPGResonanceNodeRewardKind.ActiveSkill
            ? activeSkill != null
            : passiveModifier != null;
}
