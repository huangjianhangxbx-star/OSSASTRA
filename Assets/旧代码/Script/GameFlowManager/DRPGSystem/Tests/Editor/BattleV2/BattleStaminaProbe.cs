#if UNITY_EDITOR
using System;

public static class BattleStaminaProbe
{
    public static void RunAll()
    {
        FractionAccumulatesButCreditsOnlyAtOwnTurn();
        CapacityAndSpendingAreAtomic();
        InvalidValuesAreRejected();
    }

    private static void FractionAccumulatesButCreditsOnlyAtOwnTurn()
    {
        BattleStaminaValue value = new BattleStaminaValue(3, 0d, 0d)
            .Accrue(.4d).Accrue(.4d);
        Check(value.Available == 0 && Near(value.Pending, .8d),
            "fraction credited before turn entry");
        value = value.CreditOwnTurn();
        Check(value.Available == 0 && Near(value.Current, .8d) &&
            Near(value.Pending, 0d), "turn credit lost the fraction");
        value = value.Accrue(.4d).CreditOwnTurn();
        Check(value.Available == 1 && Near(value.Current, 1.2d),
            "accumulated fraction did not form one pip");
    }

    private static void CapacityAndSpendingAreAtomic()
    {
        BattleStaminaValue full = new BattleStaminaValue(3, 3d, 0d)
            .Accrue(5d);
        Check(Near(full.Current, 3d) && Near(full.Pending, 0d),
            "full stamina retained hidden overflow");
        Check(full.TrySpend(2, out BattleStaminaValue spent) &&
            spent.Available == 1, "valid stamina cost failed");
        Check(!spent.TrySpend(2, out BattleStaminaValue unchanged) &&
            unchanged.Equals(spent), "failed cost mutated stamina");
        Check(new BattleStaminaValue(100, 100d, 0d).Available == 100,
            "enemy-scale capacity was capped by player UI slots");
    }

    private static void InvalidValuesAreRejected()
    {
        Expect<ArgumentOutOfRangeException>(() =>
            new BattleStaminaValue(0, 0d, 0d));
        Expect<ArgumentOutOfRangeException>(() =>
            new BattleStaminaValue(3, 4d, 0d));
        Expect<ArgumentOutOfRangeException>(() =>
            new BattleStaminaValue(3, 0d, double.NaN));
        Expect<ArgumentOutOfRangeException>(() =>
            new BattleStaminaValue(3, 0d, 0d).Accrue(double.PositiveInfinity));
        Expect<ArgumentOutOfRangeException>(() =>
            new BattleStaminaValue(3, 1d, 0d).TrySpend(-1, out _));
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9d;

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
