#if UNITY_EDITOR
using System;

public static class BattleDamageMathProbe
{
    public static void RunAll()
    {
        LayeredDamageConsumesPostureBeforeHp();
        ResistanceCannotProduceNegativeDamage();
        PostureRestoreHasNoGameplayCap();
        IntegerRoundingAndOverflowAreExplicit();
    }

    private static void LayeredDamageConsumesPostureBeforeHp()
    {
        BattleDamageResult overflow = BattleDamageMath.Resolve(12, 0, 5, 10);
        Check(overflow.Damage == 12 && overflow.PostureAfter == 0 &&
            overflow.HpAfter == 3 && overflow.HpDamage == 7,
            "posture overflow did not reach HP exactly once");

        BattleDamageResult exact = BattleDamageMath.Resolve(5, 0, 5, 10);
        Check(exact.PostureAfter == 0 && exact.HpAfter == 10 &&
            exact.HpDamage == 0, "exact posture break damaged HP");
    }

    private static void ResistanceCannotProduceNegativeDamage()
    {
        BattleDamageResult result = BattleDamageMath.Resolve(3, 8, 0, 10);
        Check(result.Damage == 0 && result.HpAfter == 10,
            "resistance produced healing or negative damage");
    }

    private static void PostureRestoreHasNoGameplayCap()
    {
        Check(BattleDamageMath.RestorePosture(5, 8) == 13,
            "default posture became a cap");
        Check(BattleDamageMath.RestorePosture(1000, 8) == 1008,
            "posture was clamped to content display range");
        Check(BattleDamageMath.HealHp(8, 10, 5) == 10,
            "HP healing ignored MaxHP");
    }

    private static void IntegerRoundingAndOverflowAreExplicit()
    {
        Check(BattleIntegerMath.RoundNonnegative(2.5) == 3,
            "nonnegative midpoint rounding changed");
        Expect<ArgumentOutOfRangeException>(() =>
            BattleIntegerMath.RoundNonnegative(double.NaN));
        Expect<ArgumentOutOfRangeException>(() =>
            BattleDamageMath.Resolve(-1, 0, 0, 1));
        Expect<OverflowException>(() =>
            BattleDamageMath.RestorePosture(long.MaxValue, 1));
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
