using System;

/// <summary>
/// Reusable slot container for chests, field bags and debug fixtures.
/// Higher-level systems may wrap it with their own lifetime and permissions.
/// </summary>
public sealed class DRPGTransientInventoryContainerState :
    IDRPGInventoryMutableContainer
{
    private const float WeightEpsilon = 0.0001f;
    private DRPGInventoryItemStack[] slots;

    public DRPGTransientInventoryContainerState(
        string containerId,
        int capacity,
        float weightCapacity = float.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(containerId))
            throw new ArgumentException("Container ID is required.", nameof(containerId));
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if (weightCapacity < 0f)
            throw new ArgumentOutOfRangeException(nameof(weightCapacity));
        ContainerId = containerId;
        Capacity = capacity;
        WeightCapacity = weightCapacity;
        slots = new DRPGInventoryItemStack[capacity];
    }

    public string ContainerId { get; }
    public int Capacity { get; }
    public int UsedSlots
    {
        get
        {
            int result = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                    result++;
            }
            return result;
        }
    }
    public float CurrentWeight { get; private set; }
    public float WeightCapacity { get; }
    public int Revision { get; private set; }

    public DRPGInventorySlotSnapshot GetSlot(int slotIndex)
    {
        return new DRPGInventorySlotSnapshot(
            slotIndex,
            slotIndex >= 0 && slotIndex < slots.Length
                ? slots[slotIndex]
                : null);
    }

    public bool CanAccept(DRPGInventoryItemStack item, int slotIndex)
    {
        return item != null && slotIndex >= 0 && slotIndex < Capacity;
    }

    public DRPGInventoryOperationResult TryPlace(
        int slotIndex,
        DRPGInventoryItemStack item)
    {
        if (!CanAccept(item, slotIndex) || slots[slotIndex] != null)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.TargetRejected,
                "Transient container slot cannot accept the item.");
        }
        DRPGInventoryContainerMemento after = CaptureTransferMemento().Copy();
        after.Items[slotIndex] = item;
        DRPGInventoryOperationResult validation = ValidateTransferMemento(after);
        if (!validation.Success)
            return validation;
        ApplyTransferMemento(after);
        return DRPGInventoryOperationResult.Ok(item.Quantity);
    }

    DRPGInventoryContainerMemento
        IDRPGInventoryMutableContainer.CaptureTransferMemento()
    {
        return CaptureTransferMemento();
    }

    private DRPGInventoryContainerMemento CaptureTransferMemento()
    {
        return new DRPGInventoryContainerMemento(
            this,
            Revision,
            (DRPGInventoryItemStack[])slots.Clone());
    }

    DRPGInventoryOperationResult
        IDRPGInventoryMutableContainer.ValidateTransferMemento(
            DRPGInventoryContainerMemento memento)
    {
        return ValidateTransferMemento(memento);
    }

    private DRPGInventoryOperationResult ValidateTransferMemento(
        DRPGInventoryContainerMemento memento)
    {
        if (memento == null || !ReferenceEquals(memento.Owner, this) ||
            memento.SourceRevision != Revision ||
            memento.Items.Length != Capacity)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.StateConflict,
                "Transient container changed during validation.");
        }
        float weight = CalculateWeight(memento.Items);
        if (weight > WeightCapacity + WeightEpsilon)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.WeightCapacityExceeded,
                "Transient container weight capacity would be exceeded.");
        }
        return DRPGInventoryOperationResult.Ok();
    }

    void IDRPGInventoryMutableContainer.ApplyTransferMemento(
        DRPGInventoryContainerMemento memento)
    {
        ApplyTransferMemento(memento);
    }

    private void ApplyTransferMemento(DRPGInventoryContainerMemento memento)
    {
        DRPGInventoryOperationResult validation = ValidateTransferMemento(memento);
        if (!validation.Success)
            throw new InvalidOperationException(validation.Message);
        slots = (DRPGInventoryItemStack[])memento.Items.Clone();
        CurrentWeight = CalculateWeight(slots);
        Revision++;
    }

    void IDRPGInventoryMutableContainer.RestoreTransferMemento(
        DRPGInventoryContainerMemento memento)
    {
        if (memento == null || !ReferenceEquals(memento.Owner, this) ||
            memento.Items.Length != Capacity)
        {
            throw new InvalidOperationException(
                "Cannot restore a foreign transient container snapshot.");
        }
        slots = (DRPGInventoryItemStack[])memento.Items.Clone();
        CurrentWeight = CalculateWeight(slots);
        Revision = memento.SourceRevision;
    }

    private static float CalculateWeight(DRPGInventoryItemStack[] items)
    {
        float result = 0f;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                result += items[i].TotalWeight;
        }
        return result;
    }
}

