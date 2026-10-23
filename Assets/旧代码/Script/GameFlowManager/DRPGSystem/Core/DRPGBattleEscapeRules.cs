using System;

[Serializable]
public readonly struct DRPGBattleEscapeRules
{
    public DRPGBattleEscapeRules(bool allowed, float baseChance)
    {
        Allowed = allowed;
        BaseChance = Math.Max(0f, Math.Min(1f, baseChance));
    }

    public bool Allowed { get; }
    public float BaseChance { get; }

    public static DRPGBattleEscapeRules Disabled =>
        new DRPGBattleEscapeRules(false, 0f);
}
