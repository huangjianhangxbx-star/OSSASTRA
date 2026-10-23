using UnityEngine;

public sealed class DRPGDerivedStats
{
    public DRPGDerivedStats(
        float maxHp,
        float maxMp,
        float maxPosture,
        float expeditionEnergy,
        float speedScore,
        float healingReceivedMultiplier,
        float statusResistanceBonus,
        float postureDamageMultiplier,
        float postureDamageTakenMultiplier,
        float accuracyBonus,
        float weakPointDamageMultiplier,
        float circuitPowerMultiplier,
        float circuitResistanceBonus,
        float guardWindowMultiplier,
        float freeStrikeResistanceMultiplier,
        float finalPotencyMultiplier)
    {
        MaxHp = maxHp;
        MaxMp = maxMp;
        MaxPosture = maxPosture;
        ExpeditionEnergy = expeditionEnergy;
        SpeedScore = speedScore;
        HealingReceivedMultiplier = healingReceivedMultiplier;
        StatusResistanceBonus = statusResistanceBonus;
        PostureDamageMultiplier = postureDamageMultiplier;
        PostureDamageTakenMultiplier = postureDamageTakenMultiplier;
        AccuracyBonus = accuracyBonus;
        WeakPointDamageMultiplier = weakPointDamageMultiplier;
        CircuitPowerMultiplier = circuitPowerMultiplier;
        CircuitResistanceBonus = circuitResistanceBonus;
        GuardWindowMultiplier = guardWindowMultiplier;
        FreeStrikeResistanceMultiplier = freeStrikeResistanceMultiplier;
        FinalPotencyMultiplier = finalPotencyMultiplier;
    }

    public float MaxHp { get; }
    public float MaxMp { get; }
    public float MaxPosture { get; }
    public float ExpeditionEnergy { get; }
    public float SpeedScore { get; }
    public float HealingReceivedMultiplier { get; }
    public float StatusResistanceBonus { get; }
    public float PostureDamageMultiplier { get; }
    public float PostureDamageTakenMultiplier { get; }
    public float AccuracyBonus { get; }
    public float WeakPointDamageMultiplier { get; }
    public float CircuitPowerMultiplier { get; }
    public float CircuitResistanceBonus { get; }
    public float GuardWindowMultiplier { get; }
    public float FreeStrikeResistanceMultiplier { get; }
    public float FinalPotencyMultiplier { get; }
}

public static class DRPGDerivedStatResolver
{
    public static DRPGDerivedStats Resolve(
        DRPGCombatantProfileSO profile,
        DRPGCoreAttributes initial,
        DRPGCoreAttributes current,
        int level)
    {
        float vitality = Delta(initial.Vitality, current.Vitality);
        float strength = Delta(initial.Strength, current.Strength);
        float endurance = Delta(initial.Endurance, current.Endurance);
        float technique = Delta(initial.Technique, current.Technique);
        float circuit = Delta(initial.Circuit, current.Circuit);
        float reaction = Delta(initial.Reaction, current.Reaction);
        float durability = DRPGGrowthRules.BreakthroughDurabilityMultiplier(level);
        float potency = DRPGGrowthRules.BreakthroughPotencyMultiplier(level);

        return new DRPGDerivedStats(
            profile.MaxHp * (1f + 2.20f * vitality) * durability,
            profile.MaxMp * (1f + 4.30f * circuit),
            profile.MaxPosture * (1f + 1.65f * endurance) * durability,
            profile.ExpeditionEnergy * (1f + 1.22f * endurance),
            profile.Agility + 8.50f * reaction,
            1f + 0.15f * vitality,
            0.25f * vitality,
            1f + 0.55f * strength,
            1f - 0.20f * endurance,
            0.18f * technique,
            1f + 0.60f * technique,
            1f + 0.55f * circuit,
            0.20f * circuit,
            1f + 0.15f * reaction,
            1f - 0.12f * reaction,
            potency);
    }

    private static float Delta(int initial, int current)
    {
        return DRPGGrowthRules.EvaluateGrowthDelta(initial, current);
    }
}
