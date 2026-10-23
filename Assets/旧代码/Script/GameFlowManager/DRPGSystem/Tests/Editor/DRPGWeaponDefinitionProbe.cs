#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGWeaponDefinitionProbe
{
    public static string Run()
    {
        AssertBudget(DRPGEquipmentRarity.Blue, 1, 1, 2, 0);
        AssertBudget(DRPGEquipmentRarity.Purple, 2, 2, 4, 0);
        AssertBudget(DRPGEquipmentRarity.Gold, 3, 2, 6, 0);
        AssertBudget(DRPGEquipmentRarity.RedGold, 4, 3, 8, 1);

        var strengthTwelve = new DRPGAttributeRequirement(
            DRPGCoreAttributeId.Strength,
            12);
        AssertFalse(
            strengthTwelve.IsMetBy(new DRPGCoreAttributes(10, 11, 10, 10, 10, 10)),
            "Permanent Strength 11 must not satisfy a Strength 12 requirement");
        AssertTrue(
            strengthTwelve.IsMetBy(new DRPGCoreAttributes(10, 12, 10, 10, 10, 10)),
            "Permanent Strength 12 must satisfy a Strength 12 requirement");

        DRPGWeaponDefinitionSO weapon =
            ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        DRPGSkillSO intrinsicSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            SetField(weapon, "weaponId", "definition_probe_weapon");
            SetField(weapon, "rarity", DRPGEquipmentRarity.Blue);
            SetField(weapon, "intrinsicSkill", intrinsicSkill);
            AssertThrows(
                weapon.ValidateOrThrow,
                "A non-RedGold weapon must reject an intrinsic skill");

            SetField(weapon, "rarity", DRPGEquipmentRarity.RedGold);
            weapon.ValidateOrThrow();

            SetField(
                weapon,
                "ownershipKind",
                DRPGWeaponOwnershipKind.Signature);
            AssertThrows(
                weapon.ValidateOrThrow,
                "A signature weapon without a stable owner ID must be rejected");

            SetField(weapon, "signatureCharacterId", "character_owner");
            weapon.ValidateOrThrow();
            AssertEqual(
                DRPGWeaponBreakDisposition.PreserveSignatureAtZero,
                weapon.BreakDisposition,
                "A signature weapon must use the non-loss break disposition");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(intrinsicSkill);
            UnityEngine.Object.DestroyImmediate(weapon);
        }

        return "PASS DRPG weapon definition";
    }

    private static void AssertBudget(
        DRPGEquipmentRarity rarity,
        int affixSlots,
        int cardSlots,
        int cardCost,
        int intrinsicSkillSlots)
    {
        DRPGWeaponRarityBudget budget = DRPGWeaponRarityRules.GetBudget(rarity);
        AssertEqual(affixSlots, budget.AffixSlots, rarity + " affix slots");
        AssertEqual(cardSlots, budget.CardSlots, rarity + " card slots");
        AssertEqual(cardCost, budget.CardCostCapacity, rarity + " card cost");
        AssertEqual(
            intrinsicSkillSlots,
            budget.IntrinsicSkillSlots,
            rarity + " intrinsic skill slots");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }
}
#endif
