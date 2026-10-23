public readonly struct DRPGEnemyActionPlanSnapshot
{
    public DRPGEnemyActionPlanSnapshot(
        long planId,
        int actorRuntimeId,
        DRPGEnemyPlannedActionKind actionKind,
        DRPGEnemyPlanState state,
        int warningValue,
        int maximumWarningValue,
        int disclosedTargetRuntimeId,
        string skillId = "",
        string skillDisplayName = "",
        UnityEngine.Sprite skillIcon = null,
        DRPGDamageTag attribute = DRPGDamageTag.None,
        int? panel = null)
    {
        PlanId = planId;
        ActorRuntimeId = actorRuntimeId;
        ActionKind = actionKind;
        State = state;
        WarningValue = warningValue;
        MaximumWarningValue = maximumWarningValue;
        DisclosedTargetRuntimeId = disclosedTargetRuntimeId;
        SkillId = skillId ?? string.Empty;
        SkillDisplayName = skillDisplayName ?? string.Empty;
        SkillIcon = skillIcon;
        Attribute = attribute;
        Panel = panel;
    }

    public long PlanId { get; }
    public int ActorRuntimeId { get; }
    public DRPGEnemyPlannedActionKind ActionKind { get; }
    public DRPGEnemyPlanState State { get; }
    public int WarningValue { get; }
    public int MaximumWarningValue { get; }
    public int DisclosedTargetRuntimeId { get; }
    public string SkillId { get; }
    public string SkillDisplayName { get; }
    public UnityEngine.Sprite SkillIcon { get; }
    public DRPGDamageTag Attribute { get; }
    public int? Panel { get; }
    public bool HasEnemyWarning =>
        ActionKind == DRPGEnemyPlannedActionKind.Attack &&
        (MaximumWarningValue > 0 || State != DRPGEnemyPlanState.Windup);
    public bool IsEnemyAttackReady =>
        HasEnemyWarning &&
        State != DRPGEnemyPlanState.Windup &&
        WarningValue == 0;
}
