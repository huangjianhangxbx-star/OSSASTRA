using System;

public readonly struct BattleDamageResult
{
    public BattleDamageResult(
        int damage,
        long postureAfter,
        int hpAfter,
        int hpDamage)
    {
        Damage = damage;
        PostureAfter = postureAfter;
        HpAfter = hpAfter;
        HpDamage = hpDamage;
    }

    public int Damage { get; }
    public long PostureAfter { get; }
    public int HpAfter { get; }
    public int HpDamage { get; }
}

public static class BattleDamageMath
{
    public static BattleDamageResult Resolve(
        int panel,
        int resistance,
        long posture,
        int hp)
    {
        if (panel < 0)
            throw new ArgumentOutOfRangeException(nameof(panel));
        if (resistance < 0)
            throw new ArgumentOutOfRangeException(nameof(resistance));
        if (posture < 0L)
            throw new ArgumentOutOfRangeException(nameof(posture));
        if (hp < 0)
            throw new ArgumentOutOfRangeException(nameof(hp));

        int damage = panel > resistance ? panel - resistance : 0;
        long absorbed = Math.Min(posture, (long)damage);
        long postureAfter = posture - absorbed;
        long overflow = (long)damage - absorbed;
        int hpDamage = (int)Math.Min((long)hp, overflow);
        return new BattleDamageResult(
            damage,
            postureAfter,
            hp - hpDamage,
            hpDamage);
    }

    public static int HealHp(int hp, int maxHp, int panel)
    {
        if (hp < 0)
            throw new ArgumentOutOfRangeException(nameof(hp));
        if (maxHp < 0 || hp > maxHp)
            throw new ArgumentOutOfRangeException(nameof(maxHp));
        if (panel < 0)
            throw new ArgumentOutOfRangeException(nameof(panel));
        long healed = checked((long)hp + panel);
        return (int)Math.Min((long)maxHp, healed);
    }

    public static long RestorePosture(long posture, int panel)
    {
        if (posture < 0L)
            throw new ArgumentOutOfRangeException(nameof(posture));
        if (panel < 0)
            throw new ArgumentOutOfRangeException(nameof(panel));
        return checked(posture + panel);
    }
}
