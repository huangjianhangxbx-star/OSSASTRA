using System;

public sealed class DRPGPostureBreakRules
{
    private static readonly DRPGPostureBreakRules defaultRules =
        new DRPGPostureBreakRules(
            0.5f,
            3,
            0.5f,
            1.25f,
            1.25f);

    public DRPGPostureBreakRules(
        float recoveryPostureRatio,
        int stabilityOwnActionCount,
        float stabilityIncomingPostureDamageMultiplier,
        float exposedIncomingDamageMultiplier,
        float exposedAilmentBuildupMultiplier)
    {
        RequireFinite(recoveryPostureRatio, nameof(recoveryPostureRatio));
        RequireFinite(
            stabilityIncomingPostureDamageMultiplier,
            nameof(stabilityIncomingPostureDamageMultiplier));
        RequireFinite(
            exposedIncomingDamageMultiplier,
            nameof(exposedIncomingDamageMultiplier));
        RequireFinite(
            exposedAilmentBuildupMultiplier,
            nameof(exposedAilmentBuildupMultiplier));

        RecoveryPostureRatio = Clamp(recoveryPostureRatio, 0f, 1f);
        StabilityOwnActionCount = Math.Max(0, stabilityOwnActionCount);
        StabilityIncomingPostureDamageMultiplier =
            Math.Max(0f, stabilityIncomingPostureDamageMultiplier);
        ExposedIncomingDamageMultiplier =
            Math.Max(0f, exposedIncomingDamageMultiplier);
        ExposedAilmentBuildupMultiplier =
            Math.Max(0f, exposedAilmentBuildupMultiplier);
    }

    public static DRPGPostureBreakRules Default => defaultRules;

    public float RecoveryPostureRatio { get; }
    public int StabilityOwnActionCount { get; }
    public float StabilityIncomingPostureDamageMultiplier { get; }
    public float ExposedIncomingDamageMultiplier { get; }
    public float ExposedAilmentBuildupMultiplier { get; }

    public DRPGPostureBreakRules WithRecoveryPostureRatio(float value)
    {
        return new DRPGPostureBreakRules(
            value,
            StabilityOwnActionCount,
            StabilityIncomingPostureDamageMultiplier,
            ExposedIncomingDamageMultiplier,
            ExposedAilmentBuildupMultiplier);
    }

    private static void RequireFinite(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }
}
