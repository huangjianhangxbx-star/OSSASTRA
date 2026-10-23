using System;

public static class BattleEffectPanel
{
    public static int Calculate(double baseValue, double additive,
        double percent)
    {
        RequireFinite(baseValue, nameof(baseValue));
        RequireFinite(additive, nameof(additive));
        RequireFinite(percent, nameof(percent));
        if (baseValue < 0d)
            throw new ArgumentOutOfRangeException(nameof(baseValue));
        if (percent < -1d)
            throw new ArgumentOutOfRangeException(nameof(percent),
                "A percentage modifier cannot reduce below zero scale.");

        double value = (baseValue + additive) * (1d + percent);
        if (double.IsInfinity(value))
            throw new OverflowException("The battle panel calculation overflowed.");
        return BattleIntegerMath.RoundNonnegative(Math.Max(0d, value));
    }

    private static void RequireFinite(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name,
                "Panel inputs must be finite.");
    }
}
