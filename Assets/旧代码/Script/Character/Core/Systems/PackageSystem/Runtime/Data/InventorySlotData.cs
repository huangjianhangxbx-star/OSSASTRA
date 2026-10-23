using System;

/// <summary>
/// A single slot in any IItemContainer. Holds at most one ItemInstance.
/// </summary>
[Serializable]
public class InventorySlotData
{
    public int slotIndex;
    public ItemInstance item;

    public bool IsEmpty => item == null || item.definition == null;

    public InventorySlotData(int index)
    {
        slotIndex = index;
        item = null;
    }
}
