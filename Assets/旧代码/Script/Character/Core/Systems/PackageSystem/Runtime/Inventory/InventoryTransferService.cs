using System;
using UnityEngine;

/// <summary>
/// Atomic transfer service. All item movement between containers goes through here.
/// UI must never modify container lists directly.
/// </summary>
public class InventoryTransferService
{
    private readonly CharacterBackpack _backpack;
    private readonly CharacterWeaponSlots _weaponSlots;

    public InventoryTransferService(CharacterBackpack backpack, CharacterWeaponSlots weaponSlots)
    {
        _backpack = backpack;
        _weaponSlots = weaponSlots;
    }

    public bool TryExtractBackpackItem(
        int slotIndex,
        WorldDropPolicySO policy,
        out ItemInstance item)
    {
        item = _backpack.GetItem(slotIndex);
        if (item == null || policy == null || !policy.CanDrop(item))
        {
            item = null;
            return false;
        }

        item = _backpack.RemoveItemInternal(slotIndex);
        return item != null;
    }

    public bool TryRestoreBackpackItem(int slotIndex, ItemInstance item)
    {
        if (item == null || slotIndex < 0 || slotIndex >= _backpack.Capacity)
            return false;
        if (_backpack.GetItem(slotIndex) != null)
            return false;

        _backpack.SetItemInternal(slotIndex, item);
        return true;
    }

    /// <summary>
    /// Move weapon from backpack slot to weapon slot.
    /// If target slot occupied, performs deterministic swap: old weapon → source backpack slot.
    /// </summary>
    public TransferResult TryEquipWeapon(int backpackSlotIndex, int weaponSlotIndex)
    {
        var item = _backpack.GetItem(backpackSlotIndex);
        if (item == null)
            return TransferResult.Failed("Backpack slot is empty");

        if (!_weaponSlots.CanAccept(item, weaponSlotIndex))
            return TransferResult.Failed("Only weapons can go into weapon slots");

        var existing = _weaponSlots.GetItem(weaponSlotIndex);

        // Remove from backpack
        _backpack.RemoveItemInternal(backpackSlotIndex);

        // If weapon slot occupied, swap directly back to the SAME backpack slot
        if (existing != null)
        {
            _backpack.SetItemInternal(backpackSlotIndex, existing);
        }

        // Place weapon into slot
        _weaponSlots.SetItemInternal(weaponSlotIndex, item);
        return TransferResult.Success();
    }

    /// <summary>
    /// Move weapon from weapon slot back to a SPECIFIC backpack slot.
    /// If target backpack slot has an item, performs swap: backpack item → weapon slot.
    /// </summary>
    public TransferResult TryUnequipWeaponToSlot(int weaponSlotIndex, int targetBackpackSlot)
    {
        var weaponItem = _weaponSlots.GetItem(weaponSlotIndex);
        if (weaponItem == null)
            return TransferResult.Failed("Weapon slot is empty");

        if (targetBackpackSlot < 0 || targetBackpackSlot >= _backpack.Capacity)
            return TransferResult.Failed("Invalid backpack slot index");

        var existingBackpackItem = _backpack.GetItem(targetBackpackSlot);

        if (existingBackpackItem != null)
        {
            // Target backpack slot has an item — swap if it's a weapon
            if (existingBackpackItem.Category != ItemCategory.Weapon)
                return TransferResult.Failed("Target backpack slot has non-weapon item");

            // Swap: weapon slot → backpack, backpack → weapon slot
            _weaponSlots.SetItemInternal(weaponSlotIndex, existingBackpackItem);
            _backpack.SetItemInternal(targetBackpackSlot, weaponItem);
        }
        else
        {
            // Target slot empty — simple move
            _weaponSlots.RemoveItemInternal(weaponSlotIndex);
            _backpack.SetItemInternal(targetBackpackSlot, weaponItem);
        }

        return TransferResult.Success();
    }

    /// <summary>
    /// Move weapon from weapon slot back to backpack (first available slot).
    /// </summary>
    public TransferResult TryUnequipWeapon(int weaponSlotIndex)
    {
        var item = _weaponSlots.GetItem(weaponSlotIndex);
        if (item == null)
            return TransferResult.Failed("Weapon slot is empty");

        int emptyIdx = _backpack.FindFirstEmpty();
        if (emptyIdx < 0)
            return TransferResult.Failed("Backpack full, cannot unequip");

        _weaponSlots.RemoveItemInternal(weaponSlotIndex);
        _backpack.SetItemInternal(emptyIdx, item);
        return TransferResult.Success();
    }

    /// <summary>
    /// Add a picked-up item to backpack.
    /// Returns leftover quantity (0 = fully added).
    /// </summary>
    public int TryAddPickup(ItemInstance item)
    {
        return _backpack.TryAddItemInternal(item);
    }

    /// <summary>
    /// Move/swap item between two backpack slots (deterministic).
    /// Same slot = no-op success. Empty target = move. Occupied = swap or stack.
    /// </summary>
    public TransferResult TryMoveBackpackItem(int fromSlot, int toSlot)
    {
        if (fromSlot == toSlot)
            return TransferResult.Success();

        var item = _backpack.GetItem(fromSlot);
        if (item == null)
            return TransferResult.Failed("Source slot is empty");

        var existing = _backpack.GetItem(toSlot);

        _backpack.RemoveItemInternal(fromSlot);

        if (existing != null)
        {
            // Try stacking first
            if (CharacterBackpack.CanStack(existing, item))
            {
                int space = existing.definition.MaxStack - existing.quantity;
                int add = Mathf.Min(space, item.quantity);
                existing.quantity += add;
                item.quantity -= add;

                if (item.quantity > 0)
                    _backpack.SetItemInternal(fromSlot, item);
                return TransferResult.Success();
            }

            // Deterministic swap
            _backpack.SetItemInternal(toSlot, item);
            _backpack.SetItemInternal(fromSlot, existing);
        }
        else
        {
            _backpack.SetItemInternal(toSlot, item);
        }

        return TransferResult.Success();
    }

    /// <summary>
    /// Swap weapon between two weapon slots (deterministic).
    /// Same slot = no-op success.
    /// </summary>
    public TransferResult TrySwapWeaponSlots(int fromSlot, int toSlot)
    {
        if (fromSlot == toSlot)
            return TransferResult.Success();

        var item = _weaponSlots.GetItem(fromSlot);
        if (item == null)
            return TransferResult.Failed("Source weapon slot is empty");

        var existing = _weaponSlots.GetItem(toSlot);
        _weaponSlots.RemoveItemInternal(fromSlot);
        _weaponSlots.SetItemInternal(toSlot, item);
        _weaponSlots.SetItemInternal(fromSlot, existing);
        return TransferResult.Success();
    }
}

public struct TransferResult
{
    public bool success;
    public string errorMessage;

    public static TransferResult Success() => new TransferResult { success = true };
    public static TransferResult Failed(string msg) => new TransferResult { success = false, errorMessage = msg };
}
