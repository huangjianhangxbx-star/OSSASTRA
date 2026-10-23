#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGPartyInventoryProbe
{
    private sealed class MinimumRandom : IDRPGRandomSource
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

    public static string Run()
    {
        DRPGWeaponDefinitionSO candidateDefinition = CreateDefinition(
            "inventory_candidate",
            9f);
        DRPGWeaponDefinitionSO equippedDefinition = CreateDefinition(
            "inventory_equipped",
            8f);
        DRPGWeaponDefinitionSO fillerDefinition = CreateDefinition(
            "inventory_filler",
            1f);
        DRPGWeaponDefinitionSO tooHeavyDefinition = CreateDefinition(
            "inventory_too_heavy",
            12f);
        DRPGOutfitDefinitionSO outfit =
            ScriptableObject.CreateInstance<DRPGOutfitDefinitionSO>();
        DRPGAccessoryDefinitionSO accessoryA =
            ScriptableObject.CreateInstance<DRPGAccessoryDefinitionSO>();
        DRPGAccessoryDefinitionSO accessoryB =
            ScriptableObject.CreateInstance<DRPGAccessoryDefinitionSO>();
        DRPGAccessoryDefinitionSO accessoryC =
            ScriptableObject.CreateInstance<DRPGAccessoryDefinitionSO>();
        try
        {
            var random = new MinimumRandom();
            DRPGWeaponInstance candidate = DRPGWeaponInstance.Create(
                "candidate_instance",
                candidateDefinition,
                random);
            DRPGWeaponInstance equipped = DRPGWeaponInstance.Create(
                "equipped_instance",
                equippedDefinition,
                random);
            DRPGWeaponInstance filler = DRPGWeaponInstance.Create(
                "filler_instance",
                fillerDefinition,
                random);
            DRPGWeaponInstance tooHeavy = DRPGWeaponInstance.Create(
                "too_heavy_instance",
                tooHeavyDefinition,
                random);

            var inventory = new DRPGPartyInventoryState(2, 10f);
            AssertTrue(inventory.TryAddWeapon(candidate).Success,
                "Candidate must enter backpack");
            AssertTrue(inventory.TryAddWeapon(filler).Success,
                "Filler must enter backpack");
            AssertEqual(2, inventory.UsedSlots,
                "Only backpack contents consume slots");
            AssertFloat(10f, inventory.CurrentWeight,
                "Only backpack contents consume weight");

            var equipment = new DRPGCharacterEquipmentState(
                "inventory_actor",
                equipped);
            AssertFalse(inventory.ContainsWeapon(equipped.InstanceId),
                "An equipped weapon must be physically absent from backpack");

            DRPGInventoryReplacementPreview preview =
                inventory.PreviewWeaponReplacement(
                    candidate.InstanceId,
                    equipment.EquippedWeapon);
            AssertTrue(preview.Success,
                "Replacement must be evaluated from the final backpack state");
            AssertEqual(2, preview.FinalSlots,
                "Candidate leaves while old equipment returns");
            AssertFloat(9f, preview.FinalWeight,
                "Final weight must exclude candidate and include old equipment");
            AssertTrue(inventory.ContainsWeapon(candidate.InstanceId),
                "Preview must not mutate backpack ownership");
            AssertSame(equipped, equipment.EquippedWeapon,
                "Preview must not mutate character equipment");

            var blockedEquipment = new DRPGCharacterEquipmentState(
                "blocked_actor",
                tooHeavy);
            DRPGInventoryReplacementPreview blocked =
                inventory.PreviewWeaponReplacement(
                    candidate.InstanceId,
                    blockedEquipment.EquippedWeapon);
            AssertFalse(blocked.Success,
                "Replacement must fail when returned equipment exceeds final capacity");
            AssertEqual(DRPGEquipmentError.InventoryWeightCapacityExceeded,
                blocked.Error,
                "Failure must identify final weight capacity");
            AssertTrue(inventory.ContainsWeapon(candidate.InstanceId),
                "Failed preview must preserve candidate ownership");
            AssertSame(tooHeavy, blockedEquipment.EquippedWeapon,
                "Failed preview must preserve equipment");

            DRPGInventoryReplacementPreview unequipBlocked =
                inventory.PreviewWeaponUnequip(tooHeavy);
            AssertFalse(unequipBlocked.Success,
                "Unequip must fail if the backpack cannot receive the weapon");
            AssertEqual(DRPGEquipmentError.InventorySlotCapacityExceeded,
                unequipBlocked.Error,
                "A full backpack must reject unequip before mutation");

            AssertFalse(inventory.TryAddWeapon(candidate).Success,
                "The same instance cannot be owned twice");

            AssertTrue(equipment.TrySetOutfit(outfit).Success,
                "Character equipment must expose a current outfit slot");
            AssertTrue(equipment.TrySetAccessory(0, accessoryA).Success,
                "Accessory slot zero must be writable");
            AssertTrue(equipment.TrySetAccessory(1, accessoryB).Success,
                "Accessory slot one must be writable");
            AssertTrue(equipment.TrySetAccessory(2, accessoryC).Success,
                "Accessory slot two must be writable");
            AssertEqual(3, equipment.Accessories.Count,
                "Character equipment must have exactly three accessory slots");
            AssertFalse(equipment.TrySetAccessory(3, accessoryA).Success,
                "A fourth accessory slot must not exist");

            var recoveryInventory = new DRPGPartyInventoryState(2, 10f);
            AssertTrue(recoveryInventory.TryAddWeapon(candidate).Success,
                "Recovery probe candidate must enter backpack");
            AssertTrue(
                recoveryInventory.TryAddSettlementRecoveryWeapon(filler).Success,
                "Settlement recovery area must accept capacity-exempt loot");
            AssertTrue(
                recoveryInventory.ContainsSettlementRecoveryWeapon(
                    filler.InstanceId),
                "Recovery ownership must remain separate from backpack ownership");
            AssertEqual(1, recoveryInventory.UsedSlots,
                "Settlement recovery contents must not consume backpack slots yet");
            AssertTrue(
                recoveryInventory.TryRecoverSettlementWeaponToBackpack(
                    filler.InstanceId).Success,
                "Recovery transfer must succeed when final backpack capacity fits");
            AssertFalse(
                recoveryInventory.ContainsSettlementRecoveryWeapon(
                    filler.InstanceId),
                "Recovered weapon must leave settlement collection");
            AssertTrue(recoveryInventory.ContainsWeapon(filler.InstanceId),
                "Recovered weapon must enter backpack exactly once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(accessoryC);
            UnityEngine.Object.DestroyImmediate(accessoryB);
            UnityEngine.Object.DestroyImmediate(accessoryA);
            UnityEngine.Object.DestroyImmediate(outfit);
            UnityEngine.Object.DestroyImmediate(tooHeavyDefinition);
            UnityEngine.Object.DestroyImmediate(fillerDefinition);
            UnityEngine.Object.DestroyImmediate(equippedDefinition);
            UnityEngine.Object.DestroyImmediate(candidateDefinition);
        }

        return "PASS DRPG party inventory";
    }

    private static DRPGWeaponDefinitionSO CreateDefinition(
        string weaponId,
        float weight)
    {
        DRPGWeaponDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        SetField(definition, "weaponId", weaponId);
        SetField(definition, "weight", weight);
        return definition;
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

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
            throw new Exception(message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertFloat(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
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
