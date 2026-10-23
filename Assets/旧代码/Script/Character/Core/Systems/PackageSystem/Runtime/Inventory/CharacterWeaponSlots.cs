using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed 3 weapon slots. Only accepts ItemCategory.Weapon.
/// Integrates with existing CharacterWeaponInventory as an adapter.
/// </summary>
[Serializable]
public class CharacterWeaponSlots : IItemContainer
{
    public const int SlotCount = 3;

    [SerializeField] private List<InventorySlotData> slots = new List<InventorySlotData>(SlotCount);
    [SerializeField] private int activeSlotIndex = 0;

    public int Capacity => SlotCount;
    public int ActiveSlotIndex => activeSlotIndex;
    public IReadOnlyList<InventorySlotData> Slots => slots;

    public CharacterWeaponSlots()
    {
        for (int i = 0; i < SlotCount; i++)
            slots.Add(new InventorySlotData(i));
    }

    public ItemInstance GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        return slots[slotIndex].item;
    }

    public bool CanAccept(ItemInstance item, int slotIndex)
    {
        if (item == null || item.definition == null) return false;
        if (slotIndex < 0 || slotIndex >= SlotCount) return false;
        return item.Category == ItemCategory.Weapon;
    }

    public bool IsSlotEmpty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return true;
        return slots[slotIndex].IsEmpty;
    }

    public bool HasActiveWeapon => !IsSlotEmpty(activeSlotIndex);

    public ItemInstance ActiveWeapon => GetItem(activeSlotIndex);

    public bool TrySetActiveSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return false;
        activeSlotIndex = index;
        return true;
    }

    public int FindNextOccupiedSlot(int from)
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            int idx = (from + i) % SlotCount;
            if (!IsSlotEmpty(idx)) return idx;
        }
        return -1;
    }

    // --- Internal (called by InventoryTransferService) ---

    internal void SetItemInternal(int slotIndex, ItemInstance item)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        slots[slotIndex].item = item;
    }

    internal ItemInstance RemoveItemInternal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        var item = slots[slotIndex].item;
        slots[slotIndex].item = null;
        return item;
    }
}
