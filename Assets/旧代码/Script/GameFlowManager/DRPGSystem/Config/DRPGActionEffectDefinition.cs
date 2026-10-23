using System;
using UnityEngine;

[Flags]
public enum DRPGDamageTag
{
    None = 0,
    Slash = 1 << 0,
    Pierce = 1 << 1,
    Blunt = 1 << 2,
    Shot = 1 << 3,
    Flame = 1 << 4,
    Frost = 1 << 5,
    Lightning = 1 << 6,
    Corrosion = 1 << 7,
    Soul = 1 << 8,
    Void = 1 << 9
}

public enum DRPGActionEffectKind
{
    Damage = 0,
    Heal = 1,
    Shield = 2,
    PostureDamage = 3,
    PostureRestore = 4,
    ApplyStatus = 5,
    FormationShift = 6,
    TimelineShift = 7
}

public enum DRPGActionEffectTarget
{
    PrimaryTarget = 0,
    Actor = 1,
    AllOpponents = 2,
    AllAllies = 3
}

public enum DRPGEffectMagnitudeMode
{
    Fixed = 0,
    SkillPower = 1
}

[Serializable]
public sealed class DRPGDamageResponseDefinition
{
    [SerializeField] private DRPGDamageTag tags = DRPGDamageTag.None;
    [Min(0f)]
    [SerializeField] private float damageMultiplier = 1f;

    public DRPGDamageResponseDefinition()
    {
    }

    public DRPGDamageResponseDefinition(
        DRPGDamageTag tags,
        float damageMultiplier)
    {
        this.tags = tags;
        this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
    }

    public DRPGDamageTag Tags => tags;
    public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);

    public bool Matches(DRPGDamageTag incomingTags)
    {
        return tags != DRPGDamageTag.None &&
            (tags & incomingTags) != DRPGDamageTag.None;
    }
}

[Serializable]
public sealed class DRPGActionEffectDefinition
{
    [SerializeField] private DRPGActionEffectKind kind =
        DRPGActionEffectKind.Damage;
    [SerializeField] private DRPGActionEffectTarget target =
        DRPGActionEffectTarget.PrimaryTarget;
    [SerializeField] private DRPGEffectMagnitudeMode magnitudeMode =
        DRPGEffectMagnitudeMode.SkillPower;
    [Tooltip("Fixed value, or a multiplier when Magnitude Mode is Skill Power.")]
    [SerializeField] private float magnitude = 1f;
    [SerializeField] private DRPGDamageTag damageTags = DRPGDamageTag.None;
    [SerializeField] private DRPGStatusDefinitionSO status;
    [Min(1)]
    [SerializeField] private int statusStacks = 1;
    [Tooltip("Zero uses the status definition duration.")]
    [Min(0)]
    [SerializeField] private int statusDurationOverride;
    [Range(-2, 2)]
    [SerializeField] private int formationRowDelta;
    [Tooltip("Negative values advance the target; positive values delay it.")]
    [SerializeField] private float timelineDelta;
    [SerializeField] private int timelinePriorityDelta;

    public DRPGActionEffectKind Kind => kind;
    public DRPGActionEffectTarget Target => target;
    public DRPGEffectMagnitudeMode MagnitudeMode => magnitudeMode;
    public float Magnitude => magnitude;
    public DRPGDamageTag DamageTags => damageTags;
    public DRPGStatusDefinitionSO Status => status;
    public int StatusStacks => Mathf.Max(1, statusStacks);
    public int StatusDurationOverride => Mathf.Max(0, statusDurationOverride);
    public int FormationRowDelta => Mathf.Clamp(formationRowDelta, -2, 2);
    public float TimelineDelta => timelineDelta;
    public int TimelinePriorityDelta => timelinePriorityDelta;

    public static DRPGActionEffectDefinition Damage(
        DRPGActionEffectTarget target,
        float skillPowerMultiplier = 1f,
        DRPGDamageTag tags = DRPGDamageTag.None)
    {
        return Create(
            DRPGActionEffectKind.Damage,
            target,
            DRPGEffectMagnitudeMode.SkillPower,
            skillPowerMultiplier,
            tags);
    }

    public static DRPGActionEffectDefinition Heal(
        DRPGActionEffectTarget target,
        float amount)
    {
        return Create(
            DRPGActionEffectKind.Heal,
            target,
            DRPGEffectMagnitudeMode.Fixed,
            amount);
    }

    public static DRPGActionEffectDefinition Shield(
        DRPGActionEffectTarget target,
        float amount)
    {
        return Create(
            DRPGActionEffectKind.Shield,
            target,
            DRPGEffectMagnitudeMode.Fixed,
            amount);
    }

    public static DRPGActionEffectDefinition PostureDamage(
        DRPGActionEffectTarget target,
        float amount)
    {
        return Create(
            DRPGActionEffectKind.PostureDamage,
            target,
            DRPGEffectMagnitudeMode.Fixed,
            amount);
    }

    public static DRPGActionEffectDefinition PostureRestore(
        DRPGActionEffectTarget target,
        float amount)
    {
        return Create(
            DRPGActionEffectKind.PostureRestore,
            target,
            DRPGEffectMagnitudeMode.Fixed,
            amount);
    }

    public static DRPGActionEffectDefinition ApplyStatus(
        DRPGActionEffectTarget target,
        DRPGStatusDefinitionSO status,
        int stacks = 1,
        int durationOverride = 0)
    {
        return new DRPGActionEffectDefinition
        {
            kind = DRPGActionEffectKind.ApplyStatus,
            target = target,
            magnitudeMode = DRPGEffectMagnitudeMode.Fixed,
            magnitude = 0f,
            status = status,
            statusStacks = Mathf.Max(1, stacks),
            statusDurationOverride = Mathf.Max(0, durationOverride)
        };
    }

    public static DRPGActionEffectDefinition FormationShift(
        DRPGActionEffectTarget target,
        int rowDelta)
    {
        return new DRPGActionEffectDefinition
        {
            kind = DRPGActionEffectKind.FormationShift,
            target = target,
            magnitudeMode = DRPGEffectMagnitudeMode.Fixed,
            formationRowDelta = Mathf.Clamp(rowDelta, -2, 2)
        };
    }

    public static DRPGActionEffectDefinition TimelineShift(
        DRPGActionEffectTarget target,
        float timelineDelta,
        int priorityDelta = 0)
    {
        return new DRPGActionEffectDefinition
        {
            kind = DRPGActionEffectKind.TimelineShift,
            target = target,
            magnitudeMode = DRPGEffectMagnitudeMode.Fixed,
            timelineDelta = timelineDelta,
            timelinePriorityDelta = priorityDelta
        };
    }

    private static DRPGActionEffectDefinition Create(
        DRPGActionEffectKind kind,
        DRPGActionEffectTarget target,
        DRPGEffectMagnitudeMode magnitudeMode,
        float magnitude,
        DRPGDamageTag tags = DRPGDamageTag.None)
    {
        return new DRPGActionEffectDefinition
        {
            kind = kind,
            target = target,
            magnitudeMode = magnitudeMode,
            magnitude = magnitude,
            damageTags = tags
        };
    }
}
