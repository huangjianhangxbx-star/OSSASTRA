#if UNITY_EDITOR
using System;

public static class BattlePanelIntentProbe
{
    public static void RunAll()
    {
        PanelModifiersResolveBeforeResistance();
        ResistanceOnlyMatchesTheSameAttribute();
        InvalidPanelInputsFailExplicitly();
    }

    private static void ResistanceOnlyMatchesTheSameAttribute()
    {
        var configured = new[]
        {
            new BattleAttributeResistance(DRPGDamageTag.Shot, 3, 0.5f)
        };
        Check(BattleResistanceMath.Resolve(DRPGDamageTag.Shot, configured) == 5,
            "matching resistance was not resolved as an integer panel");
        Check(BattleResistanceMath.Resolve(DRPGDamageTag.Slash, configured) == 0,
            "one attribute resistance leaked into another attribute");
    }

    private static void PanelModifiersResolveBeforeResistance()
    {
        int panel = BattleEffectPanel.Calculate(10d, 2d, .25d);
        Check(panel == 15, "panel additive/percent order changed");
        BattleDamageResult result = BattleDamageMath.Resolve(panel, 3, 5, 10);
        Check(result.Damage == 12 && result.PostureAfter == 0 &&
            result.HpAfter == 3, "panel or resistance was applied twice");
    }

    private static void InvalidPanelInputsFailExplicitly()
    {
        Expect<ArgumentOutOfRangeException>(() =>
            BattleEffectPanel.Calculate(double.NaN, 0d, 0d));
        Expect<ArgumentOutOfRangeException>(() =>
            BattleEffectPanel.Calculate(1d, 0d, -1.01d));
        Expect<OverflowException>(() =>
            BattleEffectPanel.Calculate(int.MaxValue, int.MaxValue, 1d));
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
