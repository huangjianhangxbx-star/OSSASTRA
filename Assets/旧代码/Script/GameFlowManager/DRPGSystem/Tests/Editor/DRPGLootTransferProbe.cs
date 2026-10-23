#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGLootTransferProbe
{
    public static string Run()
    {
        DRPGLootItemDefinitionSO light = CreateDefinition(
            "loot_light", "轻质素材", 1f, 4);
        DRPGLootItemDefinitionSO heavy = CreateDefinition(
            "loot_heavy", "沉重遗物", 3f, 2);
        try
        {
            var loot = new DRPGLootContainerState(new[]
            {
                new DRPGLootStackState(light, 7),
                new DRPGLootStackState(heavy, 2)
            });
            var inventory = new DRPGPartyInventoryState(3, 10f);

            DRPGLootTransferResult first = DRPGLootTransferService.Transfer(
                loot,
                inventory,
                light.ItemId,
                3);
            AssertEqual(3, first.TransferredQuantity,
                "Manual drag must transfer the requested quantity");
            AssertEqual(4, loot.GetQuantity(light.ItemId),
                "Manual transfer must remove items from the reward container");
            AssertEqual(3, inventory.GetLootQuantity(light.ItemId),
                "Manual transfer must add items to the DRPG backpack");
            AssertEqual(1, inventory.UsedSlots,
                "A partial stack must consume one shared backpack slot");

            DRPGLootTransferSummary all =
                DRPGLootTransferService.TransferAll(loot, inventory);
            AssertEqual(5, all.TransferredQuantity,
                "Collect-all must transfer only what shared weight permits");
            AssertEqual(1, loot.GetQuantity(heavy.ItemId),
                "Capacity overflow must remain in the settlement container");
            AssertEqual(7, inventory.GetLootQuantity(light.ItemId),
                "Existing loot must merge up to its stack rules");
            AssertEqual(1, inventory.GetLootQuantity(heavy.ItemId),
                "The backpack must accept a partial second definition");
            AssertEqual(3, inventory.UsedSlots,
                "Two light stacks plus one heavy stack use all three slots");
            AssertFloat(10f, inventory.CurrentWeight,
                "Accepted loot weight must match the shared capacity exactly");
            AssertTrue(inventory.CurrentWeight <= inventory.WeightCapacity + 0.001f,
                "Loot transfers must never exceed shared backpack weight");

            return "PASS DRPG settlement loot transfer";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(heavy);
            UnityEngine.Object.DestroyImmediate(light);
        }
    }

    private static DRPGLootItemDefinitionSO CreateDefinition(
        string itemId,
        string displayName,
        float weight,
        int maxStack)
    {
        DRPGLootItemDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();
        SetField(definition, "itemId", itemId);
        SetField(definition, "displayName", displayName);
        SetField(definition, "weight", weight);
        SetField(definition, "maxStack", maxStack);
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

    private static void AssertFloat(
        float expected,
        float actual,
        string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
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
}
#endif
