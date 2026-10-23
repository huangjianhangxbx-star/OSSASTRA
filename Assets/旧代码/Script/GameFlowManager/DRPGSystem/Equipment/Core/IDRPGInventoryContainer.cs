using System;

public interface IDRPGInventoryContainer
{
    string ContainerId { get; }
    int Capacity { get; }
    int UsedSlots { get; }
    float CurrentWeight { get; }
    float WeightCapacity { get; }
    int Revision { get; }
    DRPGInventorySlotSnapshot GetSlot(int slotIndex);
    bool CanAccept(DRPGInventoryItemStack item, int slotIndex);
}

internal sealed class DRPGInventoryContainerMemento
{
    public DRPGInventoryContainerMemento(
        IDRPGInventoryContainer owner,
        int sourceRevision,
        DRPGInventoryItemStack[] items)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        SourceRevision = sourceRevision;
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IDRPGInventoryContainer Owner { get; }
    public int SourceRevision { get; }
    public DRPGInventoryItemStack[] Items { get; }

    public DRPGInventoryContainerMemento Copy()
    {
        return new DRPGInventoryContainerMemento(
            Owner,
            SourceRevision,
            (DRPGInventoryItemStack[])Items.Clone());
    }
}

internal interface IDRPGInventoryMutableContainer : IDRPGInventoryContainer
{
    DRPGInventoryContainerMemento CaptureTransferMemento();
    DRPGInventoryOperationResult ValidateTransferMemento(
        DRPGInventoryContainerMemento memento);
    void ApplyTransferMemento(DRPGInventoryContainerMemento memento);
    void RestoreTransferMemento(DRPGInventoryContainerMemento memento);
}

public enum DRPGInventoryError
{
    None = 0,
    InvalidRequest = 1,
    InvalidSlot = 2,
    EmptySource = 3,
    TargetRejected = 4,
    SlotCapacityExceeded = 5,
    WeightCapacityExceeded = 6,
    StateConflict = 7,
    DuplicateUniqueItem = 8,
    DiscardDenied = 9
}

public sealed class DRPGInventoryOperationResult
{
    private DRPGInventoryOperationResult(
        bool success,
        DRPGInventoryError error,
        string message,
        int transferredQuantity,
        bool swapped,
        bool merged)
    {
        Success = success;
        Error = error;
        Message = message ?? string.Empty;
        TransferredQuantity = transferredQuantity;
        Swapped = swapped;
        Merged = merged;
    }

    public bool Success { get; }
    public DRPGInventoryError Error { get; }
    public string Message { get; }
    public int TransferredQuantity { get; }
    public bool Swapped { get; }
    public bool Merged { get; }

    public static DRPGInventoryOperationResult Ok(
        int transferredQuantity = 0,
        bool swapped = false,
        bool merged = false)
    {
        return new DRPGInventoryOperationResult(
            true,
            DRPGInventoryError.None,
            string.Empty,
            Math.Max(0, transferredQuantity),
            swapped,
            merged);
    }

    public static DRPGInventoryOperationResult Fail(
        DRPGInventoryError error,
        string message)
    {
        if (error == DRPGInventoryError.None)
            throw new ArgumentOutOfRangeException(nameof(error));
        return new DRPGInventoryOperationResult(
            false,
            error,
            message,
            0,
            false,
            false);
    }
}

