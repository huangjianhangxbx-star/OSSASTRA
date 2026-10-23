#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGWeaponInstanceProbe
{
    public static string Run()
    {
        DRPGWeaponDefinitionSO blue = CreateWeapon(
            "blue_probe_weapon",
            DRPGEquipmentRarity.Blue,
            DRPGWeaponOwnershipKind.General,
            100,
            0.75f);
        DRPGWeaponDefinitionSO redGold = CreateWeapon(
            "redgold_probe_weapon",
            DRPGEquipmentRarity.RedGold,
            DRPGWeaponOwnershipKind.General,
            100,
            0.75f);
        DRPGWeaponDefinitionSO signature = CreateWeapon(
            "signature_probe_weapon",
            DRPGEquipmentRarity.RedGold,
            DRPGWeaponOwnershipKind.Signature,
            100,
            0.75f);
        DRPGWeaponAffixDefinitionSO affix =
            ScriptableObject.CreateInstance<DRPGWeaponAffixDefinitionSO>();
        DRPGWeaponCardDefinitionSO card =
            ScriptableObject.CreateInstance<DRPGWeaponCardDefinitionSO>();
        DRPGWeaponAttachmentDefinitionSO attachment =
            ScriptableObject.CreateInstance<DRPGWeaponAttachmentDefinitionSO>();

        try
        {
            SetField(signature, "signatureCharacterId", "owner");
            SetField(affix, "affixId", "affix_a");
            SetField(card, "cardId", "card_a");
            SetField(card, "cost", 2);
            SetField(attachment, "attachmentId", "attachment_a");

            var affixes = new List<DRPGWeaponAffixDefinitionSO> { affix };
            var cards = new List<DRPGWeaponCardDefinitionSO> { card };
            var attachments =
                new List<DRPGWeaponAttachmentDefinitionSO> { attachment };
            var random = new MinimumRandomSource();
            DRPGWeaponInstance blueInstance = DRPGWeaponInstance.Create(
                "weapon_instance_blue",
                blue,
                random,
                affixes,
                cards,
                attachments);

            AssertEqual(85, blueInstance.RolledMaxDurability,
                "Blue minimum rarity factor must roll 85% of base durability");
            AssertEqual(64, blueInstance.CurrentDurability,
                "Initial durability must use the authored floor ratio and a second roll");
            AssertEqual("affix_a", blueInstance.AffixIds[0],
                "Instance affixes must snapshot stable IDs");
            AssertEqual("card_a", blueInstance.CardIds[0],
                "Instance cards must snapshot stable IDs");
            AssertEqual("attachment_a", blueInstance.AttachmentIds[0],
                "Instance attachments must snapshot stable IDs");
            affixes.Clear();
            cards.Clear();
            attachments.Clear();
            AssertEqual(1, blueInstance.AffixIds.Count,
                "Source-list changes must not mutate an existing instance");

            ApplyDurabilityLoss(blueInstance, 999);
            AssertTrue(blueInstance.IsBroken,
                "Durability loss must clamp at zero");
            AssertTrue(DRPGWeaponRepairRules.ApplyStandardFullRepair(blueInstance),
                "A broken Blue weapon must support a standard repair");
            AssertEqual(76, blueInstance.CurrentMaxDurability,
                "Blue repair must lose 10% of the original rolled maximum");
            AssertEqual(76, blueInstance.CurrentDurability,
                "A full repair must fill the reduced current maximum");

            DRPGWeaponInstance redGoldInstance = DRPGWeaponInstance.Create(
                "weapon_instance_redgold",
                redGold,
                random);
            ApplyDurabilityLoss(redGoldInstance, 999);
            AssertTrue(
                DRPGWeaponRepairRules.ApplyStandardFullRepair(redGoldInstance),
                "RedGold weapons must be repairable");
            AssertEqual(115, redGoldInstance.CurrentMaxDurability,
                "RedGold repair must preserve the rolled maximum");

            DRPGWeaponInstance signatureInstance = DRPGWeaponInstance.Create(
                "weapon_instance_signature",
                signature,
                random);
            AssertEqual(100, signatureInstance.RolledMaxDurability,
                "Signature durability maximum must not use rarity randomization");
            AssertEqual(100, signatureInstance.CurrentDurability,
                "A newly acquired signature weapon must start full");

            var excessiveCards = new List<DRPGWeaponCardDefinitionSO>
            {
                card,
                card
            };
            AssertThrows(
                () => DRPGWeaponInstance.Create(
                    "weapon_instance_invalid",
                    blue,
                    random,
                    null,
                    excessiveCards),
                "Instance creation must reject card slots beyond the rarity budget");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(attachment);
            UnityEngine.Object.DestroyImmediate(card);
            UnityEngine.Object.DestroyImmediate(affix);
            UnityEngine.Object.DestroyImmediate(signature);
            UnityEngine.Object.DestroyImmediate(redGold);
            UnityEngine.Object.DestroyImmediate(blue);
        }

        return "PASS DRPG weapon instance";
    }

    private static DRPGWeaponDefinitionSO CreateWeapon(
        string id,
        DRPGEquipmentRarity rarity,
        DRPGWeaponOwnershipKind ownership,
        int baseDurability,
        float floorRatio)
    {
        DRPGWeaponDefinitionSO weapon =
            ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        SetField(weapon, "weaponId", id);
        SetField(weapon, "rarity", rarity);
        SetField(weapon, "ownershipKind", ownership);
        SetField(weapon, "baseDurability", baseDurability);
        SetField(weapon, "initialDurabilityFloorRatio", floorRatio);
        return weapon;
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

    private static void ApplyDurabilityLoss(
        DRPGWeaponInstance instance,
        int amount)
    {
        MethodInfo method = typeof(DRPGWeaponInstance).GetMethod(
            "ApplyDurabilityLoss",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(
                typeof(DRPGWeaponInstance).Name,
                "ApplyDurabilityLoss");
        method.Invoke(instance, new object[] { amount });
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

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }

    private sealed class MinimumRandomSource : IDRPGRandomSource
    {
        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return minimumInclusive;
        }

        public int RangeInclusive(int minimumInclusive, int maximumInclusive)
        {
            return minimumInclusive;
        }
    }
}
#endif
