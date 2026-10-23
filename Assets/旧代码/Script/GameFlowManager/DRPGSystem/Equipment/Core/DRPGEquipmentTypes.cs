using System;

public enum DRPGEquipmentRarity
{
    Blue = 0,
    Purple = 1,
    Gold = 2,
    RedGold = 3
}

public enum DRPGWeaponOwnershipKind
{
    General = 0,
    Signature = 1
}

public enum DRPGPersonnelKind
{
    Friend = 0,
    Employee = 1
}

public enum DRPGWeaponBreakDisposition
{
    DestroyIntoFragments = 0,
    PreserveSignatureAtZero = 1
}

public readonly struct DRPGWeaponRarityBudget
{
    public DRPGWeaponRarityBudget(
        int affixSlots,
        int cardSlots,
        int cardCostCapacity,
        int intrinsicSkillSlots)
    {
        AffixSlots = affixSlots;
        CardSlots = cardSlots;
        CardCostCapacity = cardCostCapacity;
        IntrinsicSkillSlots = intrinsicSkillSlots;
    }

    public int AffixSlots { get; }
    public int CardSlots { get; }
    public int CardCostCapacity { get; }
    public int IntrinsicSkillSlots { get; }
}

public readonly struct DRPGWeaponDurabilityProfile
{
    public DRPGWeaponDurabilityProfile(
        float minimumMaximumFactor,
        float maximumMaximumFactor,
        float repairMaximumLossRatio,
        float repairMinimumRetainedRatio)
    {
        MinimumMaximumFactor = minimumMaximumFactor;
        MaximumMaximumFactor = maximumMaximumFactor;
        RepairMaximumLossRatio = repairMaximumLossRatio;
        RepairMinimumRetainedRatio = repairMinimumRetainedRatio;
    }

    public float MinimumMaximumFactor { get; }
    public float MaximumMaximumFactor { get; }
    public float RepairMaximumLossRatio { get; }
    public float RepairMinimumRetainedRatio { get; }
}

public static class DRPGWeaponRarityRules
{
    public const int AttachmentSlots = 2;

    public static DRPGWeaponRarityBudget GetBudget(
        DRPGEquipmentRarity rarity)
    {
        switch (rarity)
        {
            case DRPGEquipmentRarity.Blue:
                return new DRPGWeaponRarityBudget(1, 1, 2, 0);
            case DRPGEquipmentRarity.Purple:
                return new DRPGWeaponRarityBudget(2, 2, 4, 0);
            case DRPGEquipmentRarity.Gold:
                return new DRPGWeaponRarityBudget(3, 2, 6, 0);
            case DRPGEquipmentRarity.RedGold:
                return new DRPGWeaponRarityBudget(4, 3, 8, 1);
            default:
                throw new ArgumentOutOfRangeException(nameof(rarity));
        }
    }

    public static DRPGWeaponDurabilityProfile GetDurabilityProfile(
        DRPGEquipmentRarity rarity)
    {
        switch (rarity)
        {
            case DRPGEquipmentRarity.Blue:
                return new DRPGWeaponDurabilityProfile(0.85f, 1f, 0.10f, 0.50f);
            case DRPGEquipmentRarity.Purple:
                return new DRPGWeaponDurabilityProfile(0.95f, 1.10f, 0.06f, 0.60f);
            case DRPGEquipmentRarity.Gold:
                return new DRPGWeaponDurabilityProfile(1.05f, 1.20f, 0.03f, 0.75f);
            case DRPGEquipmentRarity.RedGold:
                return new DRPGWeaponDurabilityProfile(1.15f, 1.30f, 0f, 1f);
            default:
                throw new ArgumentOutOfRangeException(nameof(rarity));
        }
    }
}
