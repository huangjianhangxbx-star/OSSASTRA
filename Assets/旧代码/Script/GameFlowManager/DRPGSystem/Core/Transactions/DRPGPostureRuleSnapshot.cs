using System;
using System.Text;

internal readonly struct DRPGPostureRuleSnapshot
{
    internal DRPGPostureRuleSnapshot(DRPGPostureBreakState state, int stability, DRPGPostureBreakRules rules)
    { State = state; RemainingStability = stability; Rules = rules ?? DRPGPostureBreakRules.Default; }
    internal DRPGPostureBreakState State { get; }
    internal int RemainingStability { get; }
    internal DRPGPostureBreakRules Rules { get; }
    internal bool IsExposed => State == DRPGPostureBreakState.ExposedPendingSkip || State == DRPGPostureBreakState.ExposedAfterSkip;
    internal float DamageMultiplier => IsExposed ? Rules.ExposedIncomingDamageMultiplier : 1f;
    internal float PostureMultiplier => State == DRPGPostureBreakState.RecoveryStability ? Rules.StabilityIncomingPostureDamageMultiplier : 1f;
    internal static DRPGPostureRuleSnapshot Stable => new DRPGPostureRuleSnapshot(DRPGPostureBreakState.Stable, 0, DRPGPostureBreakRules.Default);
    internal DRPGPostureRuleSnapshot Enter() => IsExposed ? this : new DRPGPostureRuleSnapshot(DRPGPostureBreakState.ExposedPendingSkip, 0, Rules);
    internal bool Same(DRPGPostureRuleSnapshot other) => State == other.State && RemainingStability == other.RemainingStability &&
        Rules != null && other.Rules != null && Rules.RecoveryPostureRatio == other.Rules.RecoveryPostureRatio &&
        Rules.StabilityOwnActionCount == other.Rules.StabilityOwnActionCount &&
        Rules.StabilityIncomingPostureDamageMultiplier == other.Rules.StabilityIncomingPostureDamageMultiplier &&
        Rules.ExposedIncomingDamageMultiplier == other.Rules.ExposedIncomingDamageMultiplier &&
        Rules.ExposedAilmentBuildupMultiplier == other.Rules.ExposedAilmentBuildupMultiplier;
    internal void Validate()
    {
        if (Rules == null || !Enum.IsDefined(typeof(DRPGPostureBreakState), State) || RemainingStability < 0 ||
            (State == DRPGPostureBreakState.RecoveryStability ? RemainingStability == 0 || RemainingStability > Rules.StabilityOwnActionCount : RemainingStability != 0))
            throw new InvalidOperationException("Invalid captured posture-break lifecycle.");
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private static void AppendPostureState(StringBuilder b, DRPGPostureRuleSnapshot s)
    {
        Append(b, (int)s.State); Append(b, s.RemainingStability);
        Append(b, s.Rules.RecoveryPostureRatio); Append(b, s.Rules.StabilityOwnActionCount);
        Append(b, s.Rules.StabilityIncomingPostureDamageMultiplier); Append(b, s.Rules.ExposedIncomingDamageMultiplier);
        Append(b, s.Rules.ExposedAilmentBuildupMultiplier);
    }
}
