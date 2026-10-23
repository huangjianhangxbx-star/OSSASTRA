using System;
using System.Collections.Generic;

public readonly struct DRPGLootTransferResult
{
    public DRPGLootTransferResult(
        string itemId,
        int requestedQuantity,
        int transferredQuantity,
        int remainingQuantity)
    {
        ItemId = itemId ?? string.Empty;
        RequestedQuantity = Math.Max(0, requestedQuantity);
        TransferredQuantity = Math.Max(0, transferredQuantity);
        RemainingQuantity = Math.Max(0, remainingQuantity);
    }

    public string ItemId { get; }
    public int RequestedQuantity { get; }
    public int TransferredQuantity { get; }
    public int RemainingQuantity { get; }
    public bool Complete => TransferredQuantity == RequestedQuantity;
}

public readonly struct DRPGLootTransferSummary
{
    public DRPGLootTransferSummary(
        int requestedQuantity,
        int transferredQuantity)
    {
        RequestedQuantity = Math.Max(0, requestedQuantity);
        TransferredQuantity = Math.Max(0, transferredQuantity);
    }

    public int RequestedQuantity { get; }
    public int TransferredQuantity { get; }
    public int RemainingQuantity =>
        Math.Max(0, RequestedQuantity - TransferredQuantity);
    public bool Complete => RemainingQuantity == 0;
}

public static class DRPGLootTransferService
{
    public static DRPGLootTransferResult Transfer(
        DRPGLootContainerState source,
        DRPGPartyInventoryState destination,
        string itemId,
        int requestedQuantity)
    {
        int available = source != null ? source.GetQuantity(itemId) : 0;
        int requested = Math.Min(Math.Max(0, requestedQuantity), available);
        if (source == null || destination == null || requested <= 0)
        {
            return new DRPGLootTransferResult(
                itemId,
                requested,
                0,
                available);
        }

        DRPGLootItemDefinitionSO definition = null;
        IReadOnlyList<DRPGLootStackState> stacks = source.Stacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (string.Equals(
                    stacks[i].Definition.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                definition = stacks[i].Definition;
                break;
            }
        }

        int accepted = destination.TryAddLoot(definition, requested);
        int removed = source.RemoveUpTo(itemId, accepted);
        if (removed != accepted)
        {
            throw new InvalidOperationException(
                "Loot transfer source and destination diverged.");
        }
        return new DRPGLootTransferResult(
            itemId,
            requested,
            accepted,
            source.GetQuantity(itemId));
    }

    public static DRPGLootTransferSummary TransferAll(
        DRPGLootContainerState source,
        DRPGPartyInventoryState destination)
    {
        if (source == null || destination == null)
            return new DRPGLootTransferSummary(0, 0);

        var snapshot = new List<DRPGLootStackState>(source.Stacks);
        int requested = 0;
        int transferred = 0;
        for (int i = 0; i < snapshot.Count; i++)
        {
            DRPGLootStackState stack = snapshot[i];
            requested += stack.Quantity;
            DRPGLootTransferResult result = Transfer(
                source,
                destination,
                stack.Definition.ItemId,
                stack.Quantity);
            transferred += result.TransferredQuantity;
        }
        return new DRPGLootTransferSummary(requested, transferred);
    }
}
