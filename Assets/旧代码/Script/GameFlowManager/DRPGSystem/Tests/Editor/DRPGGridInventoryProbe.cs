#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGGridInventoryProbe
{
    public static string Run()
    {
        Type itemType = Type.GetType(
            "DRPGInventoryItemStack, Assembly-CSharp");
        Type containerType = Type.GetType(
            "IDRPGInventoryContainer, Assembly-CSharp");
        Type transferType = Type.GetType(
            "DRPGInventoryTransferService, Assembly-CSharp");

        if (itemType == null || containerType == null || transferType == null)
        {
            throw new Exception(
                "DRPG inventory requires slot items, a container contract, " +
                "and one atomic transfer service.");
        }

        if (typeof(DRPGPartyInventoryState).GetMethod("GetSlot") == null)
        {
            throw new Exception(
                "Party inventory must expose stable logical slots.");
        }

        DRPGLootItemDefinitionSO alloy = CreateLoot(
            "probe_alloy",
            "Probe Alloy",
            1f,
            5);
        DRPGLootItemDefinitionSO crystal = CreateLoot(
            "probe_crystal",
            "Probe Crystal",
            2f,
            3);
        try
        {
            ProbePartySlots(alloy, crystal);
            ProbeTransfers(alloy, crystal);
            ProbeDiscard(alloy);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(alloy);
            UnityEngine.Object.DestroyImmediate(crystal);
        }

        return "PASS DRPG grid inventory contract + atomic behavior";
    }

    private static void ProbePartySlots(
        DRPGLootItemDefinitionSO alloy,
        DRPGLootItemDefinitionSO crystal)
    {
        var party = new DRPGPartyInventoryState(4, 20f);
        Require(party.TryAddLoot(alloy, 7) == 7,
            "Party should accept seven alloy units.");
        Require(party.UsedSlots == 2,
            "Seven units at max-stack five must occupy two real slots.");
        Require(party.GetSlot(0).Item.Quantity == 5 &&
                party.GetSlot(1).Item.Quantity == 2,
            "Party loot stacks must be placed deterministically.");

        DRPGInventoryOperationResult move =
            DRPGInventoryTransferService.Transfer(party, 1, party, 3);
        Require(move.Success && party.GetSlot(1).IsEmpty &&
                party.GetSlot(3).Item.Quantity == 2,
            "Same-container moves must preserve the requested slot.");

        Require(party.TryAddLoot(crystal, 1) == 1,
            "Party should accept a second loot type.");
        DRPGInventoryOperationResult swap =
            DRPGInventoryTransferService.Transfer(party, 1, party, 3);
        Require(swap.Success && swap.Swapped &&
                party.GetSlot(1).Item.LootDefinition == alloy &&
                party.GetSlot(3).Item.LootDefinition == crystal,
            "Different items must swap atomically.");
    }

    private static void ProbeTransfers(
        DRPGLootItemDefinitionSO alloy,
        DRPGLootItemDefinitionSO crystal)
    {
        var source = new DRPGTransientInventoryContainerState(
            "probe-source",
            3,
            20f);
        var target = new DRPGTransientInventoryContainerState(
            "probe-target",
            3,
            20f);
        Require(source.TryPlace(
            0,
            DRPGInventoryItemStack.FromLoot(alloy, 3)).Success,
            "Probe source setup failed.");
        Require(source.TryPlace(
            1,
            DRPGInventoryItemStack.FromLoot(alloy, 4)).Success,
            "Probe source setup failed.");

        DRPGInventoryOperationResult merge =
            DRPGInventoryTransferService.Transfer(source, 0, source, 1);
        Require(merge.Success && merge.Merged &&
                merge.TransferredQuantity == 1 &&
                source.GetSlot(0).Item.Quantity == 2 &&
                source.GetSlot(1).Item.Quantity == 5,
            "Partial stack merge must leave its remainder in the source.");

        DRPGInventoryOperationResult cross =
            DRPGInventoryTransferService.Transfer(source, 0, target, 2);
        Require(cross.Success && source.GetSlot(0).IsEmpty &&
                target.GetSlot(2).Item.Quantity == 2,
            "Cross-container transfer must commit both containers.");

        var tooHeavy = new DRPGTransientInventoryContainerState(
            "probe-heavy-target",
            1,
            0f);
        Require(source.TryPlace(
            0,
            DRPGInventoryItemStack.FromLoot(crystal, 1)).Success,
            "Heavy rejection setup failed.");
        int sourceRevision = source.Revision;
        DRPGInventoryOperationResult rejected =
            DRPGInventoryTransferService.Transfer(source, 0, tooHeavy, 0);
        Require(!rejected.Success &&
                rejected.Error == DRPGInventoryError.WeightCapacityExceeded &&
                source.Revision == sourceRevision &&
                source.GetSlot(0).Item.LootDefinition == crystal &&
                tooHeavy.GetSlot(0).IsEmpty,
            "Rejected transfers must not partially mutate either container.");
    }

    private static void ProbeDiscard(DRPGLootItemDefinitionSO alloy)
    {
        var container = new DRPGTransientInventoryContainerState(
            "probe-discard",
            2);
        Require(container.TryPlace(
            0,
            DRPGInventoryItemStack.FromLoot(alloy, 2)).Success,
            "Discard setup failed.");

        var staleRequest = new DRPGInventoryDiscardRequest(container, 0);
        Require(DRPGInventoryTransferService.Transfer(
            container,
            0,
            container,
            1).Success,
            "Discard stale-request move failed.");
        Require(!staleRequest.Execute().Success,
            "A stale discard confirmation must never delete a moved item.");

        var validRequest = new DRPGInventoryDiscardRequest(container, 1);
        DRPGInventoryOperationResult discarded = validRequest.Execute();
        Require(discarded.Success && discarded.TransferredQuantity == 2 &&
                container.GetSlot(1).IsEmpty,
            "Confirmed discard must remove exactly the requested stack.");
    }

    private static DRPGLootItemDefinitionSO CreateLoot(
        string itemId,
        string displayName,
        float weight,
        int maxStack)
    {
        DRPGLootItemDefinitionSO result =
            ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();
        result.name = itemId;
        SetField(result, "itemId", itemId);
        SetField(result, "displayName", displayName);
        SetField(result, "weight", weight);
        SetField(result, "maxStack", maxStack);
        return result;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new Exception("Probe field not found: " + fieldName);
        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
#endif
