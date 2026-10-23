using System;

/// <summary>
/// The only write path for slot-to-slot movement. Both container snapshots
/// are validated before either live container is changed.
/// </summary>
public static class DRPGInventoryTransferService
{
    public static DRPGInventoryOperationResult Transfer(
        IDRPGInventoryContainer source,
        int sourceSlot,
        IDRPGInventoryContainer destination,
        int destinationSlot)
    {
        if (!(source is IDRPGInventoryMutableContainer mutableSource) ||
            !(destination is IDRPGInventoryMutableContainer mutableDestination))
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.InvalidRequest,
                "Both containers must support transactional mutation.");
        }
        if (!IsValidSlot(source, sourceSlot) ||
            !IsValidSlot(destination, destinationSlot))
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.InvalidSlot,
                "Source or destination slot is outside the container.");
        }

        DRPGInventoryItemStack sourceItem = source.GetSlot(sourceSlot).Item;
        if (sourceItem == null)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.EmptySource,
                "Source slot is empty.");
        }
        if (ReferenceEquals(source, destination) &&
            sourceSlot == destinationSlot)
        {
            return DRPGInventoryOperationResult.Ok();
        }

        DRPGInventoryContainerMemento sourceBefore =
            mutableSource.CaptureTransferMemento();
        DRPGInventoryContainerMemento destinationBefore =
            ReferenceEquals(source, destination)
                ? sourceBefore
                : mutableDestination.CaptureTransferMemento();
        DRPGInventoryContainerMemento sourceAfter = sourceBefore.Copy();
        DRPGInventoryContainerMemento destinationAfter =
            ReferenceEquals(source, destination)
                ? sourceAfter
                : destinationBefore.Copy();

        DRPGInventoryItemStack targetItem =
            destinationAfter.Items[destinationSlot];
        int movedQuantity = sourceItem.Quantity;
        bool merged = false;
        bool swapped = false;

        if (targetItem == null)
        {
            if (!destination.CanAccept(sourceItem, destinationSlot))
                return Rejected();
            sourceAfter.Items[sourceSlot] = null;
            destinationAfter.Items[destinationSlot] = sourceItem;
        }
        else if (targetItem.CanStackWith(sourceItem) &&
                 targetItem.Quantity < targetItem.MaxStack)
        {
            int amount = Math.Min(
                sourceItem.Quantity,
                targetItem.MaxStack - targetItem.Quantity);
            if (amount <= 0)
                return DRPGInventoryOperationResult.Ok();
            int remaining = sourceItem.Quantity - amount;
            sourceAfter.Items[sourceSlot] = remaining > 0
                ? sourceItem.WithQuantity(remaining)
                : null;
            destinationAfter.Items[destinationSlot] =
                targetItem.WithQuantity(targetItem.Quantity + amount);
            movedQuantity = amount;
            merged = true;
        }
        else
        {
            if (!destination.CanAccept(sourceItem, destinationSlot) ||
                !source.CanAccept(targetItem, sourceSlot))
            {
                return Rejected();
            }
            sourceAfter.Items[sourceSlot] = targetItem;
            destinationAfter.Items[destinationSlot] = sourceItem;
            swapped = true;
        }

        DRPGInventoryOperationResult sourceValidation =
            mutableSource.ValidateTransferMemento(sourceAfter);
        if (!sourceValidation.Success)
            return sourceValidation;
        if (!ReferenceEquals(source, destination))
        {
            DRPGInventoryOperationResult destinationValidation =
                mutableDestination.ValidateTransferMemento(destinationAfter);
            if (!destinationValidation.Success)
                return destinationValidation;
        }

        try
        {
            mutableSource.ApplyTransferMemento(sourceAfter);
            if (!ReferenceEquals(source, destination))
                mutableDestination.ApplyTransferMemento(destinationAfter);
        }
        catch (Exception exception)
        {
            mutableSource.RestoreTransferMemento(sourceBefore);
            if (!ReferenceEquals(source, destination))
                mutableDestination.RestoreTransferMemento(destinationBefore);
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.StateConflict,
                "Inventory transfer rolled back: " + exception.Message);
        }

        return DRPGInventoryOperationResult.Ok(
            movedQuantity,
            swapped,
            merged);
    }

    public static DRPGInventoryOperationResult Discard(
        IDRPGInventoryContainer source,
        int sourceSlot,
        IDRPGDiscardPolicy policy)
    {
        if (!(source is IDRPGInventoryMutableContainer mutableSource) ||
            !IsValidSlot(source, sourceSlot))
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.InvalidSlot,
                "Discard source slot is invalid.");
        }
        DRPGInventoryItemStack item = source.GetSlot(sourceSlot).Item;
        if (item == null)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.EmptySource,
                "Discard source slot is empty.");
        }
        IDRPGDiscardPolicy resolvedPolicy = policy ??
            DRPGDefaultDiscardPolicy.Instance;
        DRPGInventoryOperationResult allowed = resolvedPolicy.CanDiscard(item);
        if (!allowed.Success)
            return allowed;

        DRPGInventoryContainerMemento before =
            mutableSource.CaptureTransferMemento();
        DRPGInventoryContainerMemento after = before.Copy();
        after.Items[sourceSlot] = null;
        DRPGInventoryOperationResult validation =
            mutableSource.ValidateTransferMemento(after);
        if (!validation.Success)
            return validation;
        try
        {
            mutableSource.ApplyTransferMemento(after);
            return DRPGInventoryOperationResult.Ok(item.Quantity);
        }
        catch (Exception exception)
        {
            mutableSource.RestoreTransferMemento(before);
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.StateConflict,
                "Inventory discard rolled back: " + exception.Message);
        }
    }

    private static bool IsValidSlot(
        IDRPGInventoryContainer container,
        int slotIndex)
    {
        return container != null && slotIndex >= 0 &&
            slotIndex < container.Capacity;
    }

    private static DRPGInventoryOperationResult Rejected()
    {
        return DRPGInventoryOperationResult.Fail(
            DRPGInventoryError.TargetRejected,
            "The destination container rejected this item.");
    }
}

