using System;
using UnityEngine;

public enum DRPGBattleInteractionKind
{
    Defense = 0,
    FreeStrike = 1
}

public enum DRPGSkillExecutionMode
{
    Standard = 0,
    TelegraphedSpecial = 1,
    FreeStrike = 2
}

public enum DRPGBattleCommandStage
{
    Immediate = 0,
    Telegraph = 1,
    Release = 2
}

public enum DRPGGuardGrade
{
    Miss = 0,
    Guard = 1,
    Perfect = 2
}

public enum DRPGFreeStrikeShape
{
    Point = 0,
    Line = 1,
    Ray = 2,
    Cone = 3,
    Area = 4
}

public enum DRPGStrikeGrade
{
    Miss = 0,
    Body = 1,
    Part = 2,
    Weak = 3
}

public enum DRPGBattleInteractionEndReason
{
    Completed = 0,
    PostureExhausted = 1,
    TargetInvalid = 2,
    BattleEnded = 3,
    Cancelled = 4,
    Faulted = 5,
    ResourceExhausted = 6
}

public readonly struct DRPGFreeStrikeSample
{
    public DRPGFreeStrikeSample(
        DRPGFreeStrikeShape shape,
        Vector2 normalizedPoint,
        Vector2 normalizedDirection,
        float lineLength,
        float lineWidth)
    {
        if (lineLength < 0f)
            throw new ArgumentOutOfRangeException(nameof(lineLength));
        if (lineWidth < 0f)
            throw new ArgumentOutOfRangeException(nameof(lineWidth));

        Shape = shape;
        NormalizedPoint = normalizedPoint;
        NormalizedDirection = normalizedDirection.sqrMagnitude > 0.000001f
            ? normalizedDirection.normalized
            : Vector2.right;
        LineLength = lineLength;
        LineWidth = lineWidth;
    }

    public DRPGFreeStrikeShape Shape { get; }
    public Vector2 NormalizedPoint { get; }
    public Vector2 NormalizedDirection { get; }
    public float LineLength { get; }
    public float LineWidth { get; }
}

public readonly struct DRPGBattleImpactContext
{
    private DRPGBattleImpactContext(
        bool dealsDamage,
        float damageMultiplier,
        float postureDamage,
        DRPGGuardGrade guardGrade,
        DRPGStrikeGrade strikeGrade,
        bool hasBasePowerOverride,
        float basePowerOverride)
    {
        if (damageMultiplier < 0f)
            throw new ArgumentOutOfRangeException(nameof(damageMultiplier));
        if (postureDamage < 0f)
            throw new ArgumentOutOfRangeException(nameof(postureDamage));
        if (basePowerOverride < 0f)
            throw new ArgumentOutOfRangeException(nameof(basePowerOverride));

        DealsDamage = dealsDamage;
        DamageMultiplier = damageMultiplier;
        PostureDamage = postureDamage;
        GuardGrade = guardGrade;
        StrikeGrade = strikeGrade;
        HasBasePowerOverride = hasBasePowerOverride;
        BasePowerOverride = basePowerOverride;
    }

    public bool DealsDamage { get; }
    public float DamageMultiplier { get; }
    public float PostureDamage { get; }
    public DRPGGuardGrade GuardGrade { get; }
    public DRPGStrikeGrade StrikeGrade { get; }
    public bool HasBasePowerOverride { get; }
    public float BasePowerOverride { get; }

    public static DRPGBattleImpactContext ForGuard(
        DRPGGuardGrade grade,
        float damageMultiplier,
        float postureDamage)
    {
        return new DRPGBattleImpactContext(
            true,
            damageMultiplier,
            postureDamage,
            grade,
            DRPGStrikeGrade.Miss,
            false,
            0f);
    }

    public static DRPGBattleImpactContext ForStrike(
        DRPGStrikeGrade grade,
        float damageMultiplier,
        float postureDamage = 0f)
    {
        return new DRPGBattleImpactContext(
            grade != DRPGStrikeGrade.Miss,
            damageMultiplier,
            postureDamage,
            DRPGGuardGrade.Miss,
            grade,
            false,
            0f);
    }

    public static DRPGBattleImpactContext ForResolvedStrike(
        DRPGStrikeGrade grade,
        float basePower,
        float damageMultiplier,
        float postureDamage = 0f)
    {
        return new DRPGBattleImpactContext(
            grade != DRPGStrikeGrade.Miss,
            damageMultiplier,
            postureDamage,
            DRPGGuardGrade.Miss,
            grade,
            true,
            basePower);
    }
}
