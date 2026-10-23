using System;

public static class BattleIntegerMath
{
    public static int RoundNonnegative(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Battle values must be finite and nonnegative.");

        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded > int.MaxValue)
            throw new OverflowException("The rounded battle value exceeds Int32.");
        return checked((int)rounded);
    }
}
