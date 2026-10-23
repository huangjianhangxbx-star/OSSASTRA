using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Unified entry point for character inventory: backpack + weapon slots + transfer service.
/// </summary>
public class CharacterPackageSystem : MonoBehaviour, ICharacterSubsystem
{
    [FoldoutGroup("Backpack", false)]
    [SerializeField] private int backpackCapacity = 30;

    [FoldoutGroup("Starting Weapon", false)]
    [SerializeField] private WeaponItemDefinitionSO startingWeapon;
    [FoldoutGroup("Starting Weapon", false)]
    [SerializeField] private int startingWeaponSlot = 0;

    [FoldoutGroup("Runtime", false)]
    [ShowInInspector, ReadOnly] private CharacterBackpack _backpack;
    [ShowInInspector, ReadOnly] private CharacterWeaponSlots _weaponSlots;
    private InventoryTransferService _transferService;
    private CharacterRoot _characterRoot;
    private bool _isInitialized;

    public CharacterBackpack Backpack => _backpack;
    public CharacterWeaponSlots WeaponSlots => _weaponSlots;
    public InventoryTransferService TransferService => _transferService;

    // Events
    public event System.Action<int, ItemInstance> OnBackpackSlotChanged; // (slotIndex, item)
    public event System.Action<int, ItemInstance> OnWeaponSlotChanged;   // (slotIndex, item)
    public event System.Action<int, int> OnActiveWeaponSlotChanged;       // (prevIndex, newIndex)

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized) return;
        _characterRoot = root;

        _backpack = new CharacterBackpack(backpackCapacity);
        _weaponSlots = new CharacterWeaponSlots();
        _transferService = new InventoryTransferService(_backpack, _weaponSlots);

        _isInitialized = true;
    }

    /// <summary>
    /// Called once for new game initialization. Not called on scene reload or component re-enable.
    /// </summary>
    public void InitializeNewGame()
    {
        if (startingWeapon != null)
        {
            var instance = new ItemInstance(startingWeapon);
            _weaponSlots.SetItemInternal(startingWeaponSlot, instance);
            OnWeaponSlotChanged?.Invoke(startingWeaponSlot, instance);
        }
    }

    /// <summary>
    /// Attempt to pick up an item from the world. Returns true if fully consumed.
    /// </summary>
    public bool TryPickupItem(ItemInstance item)
    {
        if (item == null || _transferService == null)
            return false;

        int leftover = _transferService.TryAddPickup(item);

        for (int i = 0; i < _backpack.Capacity; i++)
        {
            OnBackpackSlotChanged?.Invoke(
                i,
                _backpack.GetItem(i)
            );
        }

        return leftover == 0;
    }

    public bool TryExtractBackpackItem(
        int slotIndex,
        WorldDropPolicySO policy,
        out ItemInstance item)
    {
        item = null;
        if (_transferService == null)
            return false;

        if (!_transferService.TryExtractBackpackItem(slotIndex, policy, out item))
            return false;

        OnBackpackSlotChanged?.Invoke(slotIndex, null);
        return true;
    }

    public bool TryRestoreBackpackItem(int slotIndex, ItemInstance item)
    {
        if (_transferService == null ||
            !_transferService.TryRestoreBackpackItem(slotIndex, item))
            return false;

        OnBackpackSlotChanged?.Invoke(slotIndex, item);
        return true;
    }

    public bool TryConsumeBackpackItem(ItemDefinitionSO definition, int amount)
    {
        if (definition == null || amount <= 0 || _backpack == null)
            return false;

        int available = 0;
        for (int i = 0; i < _backpack.Capacity; i++)
        {
            ItemInstance item = _backpack.GetItem(i);
            if (item != null && item.definition == definition)
                available += Mathf.Max(0, item.quantity);
        }
        if (available < amount)
            return false;

        int remaining = amount;
        for (int i = 0; i < _backpack.Capacity && remaining > 0; i++)
        {
            ItemInstance item = _backpack.GetItem(i);
            if (item == null || item.definition != definition)
                continue;

            int consumed = Mathf.Min(remaining, Mathf.Max(0, item.quantity));
            item.quantity -= consumed;
            remaining -= consumed;
            if (item.quantity <= 0)
                _backpack.RemoveItemInternal(i);
            OnBackpackSlotChanged?.Invoke(i, _backpack.GetItem(i));
        }
        return remaining == 0;
    }

    // --- Convenience wrappers for transfer service ---

    public TransferResult EquipWeapon(int backpackSlotIndex, int weaponSlotIndex)
    {
        var result = _transferService.TryEquipWeapon(backpackSlotIndex, weaponSlotIndex);
        if (result.success)
        {
            OnBackpackSlotChanged?.Invoke(backpackSlotIndex, _backpack.GetItem(backpackSlotIndex));
            OnWeaponSlotChanged?.Invoke(weaponSlotIndex, _weaponSlots.GetItem(weaponSlotIndex));
        }
        return result;
    }

    public TransferResult UnequipWeapon(int weaponSlotIndex)
    {
        var result = _transferService.TryUnequipWeapon(weaponSlotIndex);
        if (result.success)
        {
            OnWeaponSlotChanged?.Invoke(weaponSlotIndex, null);
            // Refresh all backpack slots since item went to first-available
            for (int i = 0; i < _backpack.Capacity; i++)
                OnBackpackSlotChanged?.Invoke(i, _backpack.GetItem(i));
        }
        return result;
    }

    /// <summary>
    /// Unequip weapon to a specific backpack slot (with swap if occupied).
    /// </summary>
    public TransferResult UnequipWeaponToSlot(int weaponSlotIndex, int backpackSlotIndex)
    {
        var result = _transferService.TryUnequipWeaponToSlot(weaponSlotIndex, backpackSlotIndex);
        if (result.success)
        {
            OnWeaponSlotChanged?.Invoke(weaponSlotIndex, _weaponSlots.GetItem(weaponSlotIndex));
            OnBackpackSlotChanged?.Invoke(backpackSlotIndex, _backpack.GetItem(backpackSlotIndex));
        }
        return result;
    }

    public TransferResult MoveBackpackItem(int fromSlot, int toSlot)
    {
        var result = _transferService.TryMoveBackpackItem(fromSlot, toSlot);
        if (result.success)
        {
            OnBackpackSlotChanged?.Invoke(fromSlot, _backpack.GetItem(fromSlot));
            OnBackpackSlotChanged?.Invoke(toSlot, _backpack.GetItem(toSlot));
        }
        return result;
    }

    public TransferResult SwapWeaponSlots(int fromSlot, int toSlot)
    {
        var result = _transferService.TrySwapWeaponSlots(fromSlot, toSlot);
        if (result.success)
        {
            OnWeaponSlotChanged?.Invoke(fromSlot, _weaponSlots.GetItem(fromSlot));
            OnWeaponSlotChanged?.Invoke(toSlot, _weaponSlots.GetItem(toSlot));
        }
        return result;
    }

    public bool SetActiveWeaponSlot(int index)
    {
        if (index < 0 || index >= CharacterWeaponSlots.SlotCount) return false;
        if (_weaponSlots.IsSlotEmpty(index)) return false;

        int prev = _weaponSlots.ActiveSlotIndex;
        if (prev == index) return true; // Already active, no event

        if (_weaponSlots.TrySetActiveSlot(index))
        {
            OnActiveWeaponSlotChanged?.Invoke(prev, index);
            return true;
        }
        return false;
    }

    public int CycleNextWeaponSlot()
    {
        int next = _weaponSlots.FindNextOccupiedSlot(_weaponSlots.ActiveSlotIndex);
        if (next >= 0)
            SetActiveWeaponSlot(next);
        return next;
    }
}
