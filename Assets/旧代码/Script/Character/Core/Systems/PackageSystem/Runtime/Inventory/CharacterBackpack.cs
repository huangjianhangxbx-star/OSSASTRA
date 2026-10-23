using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// General-purpose backpack with N slots. First version: 30 slots.
/// </summary>
[Serializable]
public class CharacterBackpack : IItemContainer
{
    [SerializeField] private int capacity = 30;
    [SerializeField] private List<InventorySlotData> slots = new List<InventorySlotData>();

    public int Capacity => capacity;
    public IReadOnlyList<InventorySlotData> Slots => slots;

    public CharacterBackpack(int capacity = 30)
    {
        this.capacity = Mathf.Max(1, capacity);
        InitSlots();
    }

    private void InitSlots()
    {
        slots.Clear();
        for (int i = 0; i < capacity; i++)
            slots.Add(new InventorySlotData(i));
    }

    public ItemInstance GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return null;
        return slots[slotIndex].item;
    }

    public bool CanAccept(ItemInstance item, int slotIndex)
    {
        if (item == null || item.definition == null) return false;
        if (slotIndex < 0 || slotIndex >= slots.Count) return false;
        return true;
    }

    public bool IsFull()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].IsEmpty) return false;
        return true;
    }

    public int FindFirstEmpty()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].IsEmpty) return i;
        return -1;
    }

    /// <summary>
    /// Try to find an existing stack that can accept more of the same item.
    /// Returns slot index or -1.
    /// </summary>
    public int FindStackableSlot(ItemInstance item)
    {
        if (item == null || !item.Stackable) return -1;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty) continue;
            if (!CanStack(slot.item, item)) continue;
            if (slot.item.quantity >= slot.item.definition.MaxStack) continue;
            return i;
        }
        return -1;
    }

    public static bool CanStack(ItemInstance a, ItemInstance b)
    {
        if (a == null || b == null) return false;
        if (a.definition == null || b.definition == null) return false;
        if (a.definition != b.definition) return false;
        if (!a.definition.Stackable) return false;
        if (a.definition.Category == ItemCategory.Weapon) return false;
        return true;
    }

    // --- Internal set/remove (only called by InventoryTransferService) ---

    internal void SetItemInternal(int slotIndex, ItemInstance item)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return;
        slots[slotIndex].item = item;
    }

    internal ItemInstance RemoveItemInternal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return null;
        var item = slots[slotIndex].item;
        slots[slotIndex].item = null;
        return item;
    }

    /// <summary>
    /// Try to add an item instance. Returns leftover quantity that didn't fit (0 = success).
    /// For weapons: always occupies a new empty slot, quantity must be 1.
    /// For stackables: fills existing stacks first, then new slots.
    /// </summary>
    internal int TryAddItemInternal(ItemInstance item)
    {
        if (item == null || item.definition == null) return 0;

        int remaining = item.quantity;

        // Stackable: try existing stacks first
        if (item.Stackable)
        {
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty) continue;
                if (!CanStack(slot.item, item)) continue;
                int space = slot.item.definition.MaxStack - slot.item.quantity;
                if (space <= 0) continue;
                int add = Mathf.Min(space, remaining);
                slot.item.quantity += add;
                remaining -= add;
            }
        }

        // New slots for remaining
        while (remaining > 0)
        {
            int emptyIdx = FindFirstEmpty();
            if (emptyIdx < 0) break;

            int maxStack = item.Stackable ? item.definition.MaxStack : 1;
            int add = Mathf.Min(maxStack, remaining);
            var newInstance = new ItemInstance
            {
                instanceId = item.instanceId,
                definition = item.definition,
                quantity = add
            };
            if (!item.Stackable)
            {
                // Non-stackable: keep the original instance (unique instanceId)
                newInstance = item;
                remaining = 0;
            }
            else
            {
                remaining -= add;
            }
            SetItemInternal(emptyIdx, newInstance);
        }

        return remaining;
    }
}
