## ItemCategory.cs

```csharp
/// <summary>
/// Item category for inventory sorting and weapon-slot filtering.
/// </summary>
public enum ItemCategory
{
    Weapon,
    Consumable,
    Material,
    Quest,
    Misc
}
```

## ItemDefinitionSO.cs

```csharp
using UnityEngine;

/// <summary>
/// Static definition of "what an item is". Subclassed per category.
/// SO can be referenced by unlimited ItemInstances, but the SO itself is not a specific item.
/// </summary>
public abstract class ItemDefinitionSO : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private ItemCategory category;
    [SerializeField] private Sprite inventoryIcon;
    [SerializeField] private bool stackable = false;
    [SerializeField] private int maxStack = 1;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public ItemCategory Category => category;
    public Sprite InventoryIcon => inventoryIcon;
    public bool Stackable => stackable;
    public int MaxStack => Mathf.Max(1, maxStack);
}
```

## WeaponItemDefinitionSO.cs

```csharp
using UnityEngine;

/// <summary>
/// Weapon item definition (Plan B: wraps existing WeaponConfigSO without modifying it).
/// </summary>
[CreateAssetMenu(fileName = "WeaponItemDef", menuName = "ScriptableObject/Package/WeaponItemDefinition")]
public class WeaponItemDefinitionSO : ItemDefinitionSO
{
    [SerializeField] private WeaponConfigSO weaponConfig;

    public WeaponConfigSO WeaponConfig => weaponConfig;
}
```

## ItemInstance.cs

```csharp
using System;
using UnityEngine;

/// <summary>
/// Runtime instance of "which specific item this is".
/// Weapons always get a unique instanceId and quantity=1, never stack.
/// Stackable items with same definition can merge up to maxStack.
/// </summary>
[Serializable]
public sealed class ItemInstance
{
    public string instanceId;
    public ItemDefinitionSO definition;
    public int quantity = 1;

    public ItemInstance() { }

    public ItemInstance(ItemDefinitionSO def, int qty = 1)
    {
        instanceId = Guid.NewGuid().ToString("N");
        definition = def;
        quantity = Mathf.Max(1, qty);
    }

    public ItemCategory Category => definition != null ? definition.Category : ItemCategory.Misc;
    public bool IsWeapon => Category == ItemCategory.Weapon;
    public bool Stackable => definition != null && definition.Stackable;
    public string DisplayName => definition != null ? definition.DisplayName : "Unknown";
    public Sprite Icon => definition != null ? definition.InventoryIcon : null;
}
```

## InventorySlotData.cs

```csharp
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
```

## IItemContainer.cs

```csharp
/// <summary>
/// Abstraction for any container that holds ItemInstances.
/// Backpack, WeaponSlots, StorageContainer, ShopContainer, WorldDropContainer all implement this.
/// </summary>
public interface IItemContainer
{
    int Capacity { get; }
    ItemInstance GetItem(int slotIndex);
    bool CanAccept(ItemInstance item, int slotIndex);
}
```

## CharacterBackpack.cs

```csharp
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
```

## CharacterWeaponSlots.cs

```csharp
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
```

## InventoryTransferService.cs

```csharp
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

    /// <summary>
    /// Move weapon from backpack slot to weapon slot.
    /// If target slot occupied, performs deterministic swap: old weapon 鈫?source backpack slot.
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
    /// If target backpack slot has an item, performs swap: backpack item 鈫?weapon slot.
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
            // Target backpack slot has an item 鈥?swap if it's a weapon
            if (existingBackpackItem.Category != ItemCategory.Weapon)
                return TransferResult.Failed("Target backpack slot has non-weapon item");

            // Swap: weapon slot 鈫?backpack, backpack 鈫?weapon slot
            _weaponSlots.SetItemInternal(weaponSlotIndex, existingBackpackItem);
            _backpack.SetItemInternal(targetBackpackSlot, weaponItem);
        }
        else
        {
            // Target slot empty 鈥?simple move
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
```

## CharacterPackageSystem.cs

```csharp
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
        if (_weaponSlots.IsSlotEmpty(index)) return false;
        int prev = _weaponSlots.ActiveSlotIndex;
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
```

## WorldPickupItem.cs

```csharp
using UnityEngine;

/// <summary>
/// World-space pickup entity. Holds an ItemDefinitionSO and creates an ItemInstance on start.
/// If injected with an existing ItemInstance (e.g. player dropped weapon), preserves it.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class WorldPickupItem : MonoBehaviour
{
    [Header("Item Definition")]
    [SerializeField] private ItemDefinitionSO itemDefinition;
    [SerializeField] private int quantity = 1;

    [Header("Prompt Anchor")]
    [SerializeField] private Transform promptAnchor;

    /// <summary>
    /// If set, this pickup was dropped by the player and should preserve the original instance.
    /// </summary>
    private ItemInstance _injectedInstance;

    public ItemInstance Instance { get; private set; }
    public ItemDefinitionSO Definition => itemDefinition;
    public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

    private void Awake()
    {
        // Ensure collider is trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void Start()
    {
        if (_injectedInstance != null)
        {
            Instance = _injectedInstance;
        }
        else if (itemDefinition != null)
        {
            Instance = new ItemInstance(itemDefinition, quantity);
        }

        // Set sprite from definition if available
        if (itemDefinition != null && itemDefinition.InventoryIcon != null)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = itemDefinition.InventoryIcon;
        }
    }

    /// <summary>
    /// Inject an existing ItemInstance (e.g. from player dropping a weapon).
    /// Must be called before Start().
    /// </summary>
    public void InjectInstance(ItemInstance instance)
    {
        _injectedInstance = instance;
        if (instance?.definition != null)
        {
            itemDefinition = instance.definition;
            quantity = instance.quantity;
        }
    }

    /// <summary>
    /// Called by CharacterPickupController when the item is successfully picked up.
    /// </summary>
    public void OnPickedUp()
    {
        Destroy(gameObject);
    }
}
```

## PickupCandidateDetector.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects nearby WorldPickupItems via trigger overlaps.
/// Maintains a sorted list of candidates by distance.
/// Must be on the same GameObject as a trigger Collider2D.
/// </summary>
public class PickupCandidateDetector : MonoBehaviour
{
    private readonly List<WorldPickupItem> _candidates = new List<WorldPickupItem>();
    private WorldPickupItem _currentBest;
    private float _switchCooldown = 0.15f;
    private float _lastSwitchTime;

    public WorldPickupItem CurrentBest => _currentBest;
    public int CandidateCount => _candidates.Count;
    public bool HasCandidate => _currentBest != null;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pickup = other.GetComponent<WorldPickupItem>();
        if (pickup != null && !_candidates.Contains(pickup))
        {
            _candidates.Add(pickup);
            UpdateBestCandidate(force: true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var pickup = other.GetComponent<WorldPickupItem>();
        if (pickup != null)
        {
            _candidates.Remove(pickup);
            if (_currentBest == pickup)
            {
                _currentBest = null;
                UpdateBestCandidate(force: true);
            }
        }
    }

    private void Update()
    {
        // Clean up destroyed/missing candidates
        _candidates.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        // Periodically re-evaluate best candidate (with small cooldown to prevent flicker)
        if (Time.time - _lastSwitchTime >= _switchCooldown)
        {
            UpdateBestCandidate(force: false);
        }
    }

    private void UpdateBestCandidate(bool force)
    {
        if (_candidates.Count == 0)
        {
            if (_currentBest != null)
            {
                _currentBest = null;
                _lastSwitchTime = Time.time;
            }
            return;
        }

        WorldPickupItem best = null;
        float bestDist = float.MaxValue;

        foreach (var candidate in _candidates)
        {
            if (candidate == null) continue;
            float dist = (candidate.transform.position - transform.position).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        if (best != _currentBest)
        {
            if (force || Time.time - _lastSwitchTime >= _switchCooldown)
            {
                _currentBest = best;
                _lastSwitchTime = Time.time;
            }
        }
    }

    /// <summary>
    /// Remove a candidate from the list (e.g. after pickup).
    /// </summary>
    public void RemoveCandidate(WorldPickupItem item)
    {
        if (item == null) return;
        _candidates.Remove(item);
        if (_currentBest == item)
            _currentBest = null;
    }
}
```

## CharacterPickupController.cs

```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main pickup controller. Placed on the character alongside CharacterPackageSystem.
/// Receives E-press events from InputHandler, manages candidate detection, and shows prompts.
/// The prompt lives inside a HUD Canvas (PickupPromptLayer) 鈥?never under this character's Transform.
/// </summary>
public class CharacterPickupController : MonoBehaviour, ICharacterSubsystem
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private WorldPickupPrompt promptPrefab;
    [Tooltip("鑻ユ寚瀹氾紝Prompt 浼氬疄渚嬪寲鍒拌 HUD 鎻愮ず鍥惧眰涓嬫柟銆備负绌哄垯鑷姩鏌ユ壘/鍒涘缓 PickupPromptLayer銆?)]
    [SerializeField] private RectTransform promptParent;

    private CharacterRoot _characterRoot;
    private CharacterPackageSystem _packageSystem;
    private PickupCandidateDetector _detector;
    private WorldPickupPrompt _promptInstance;
    private Camera _mainCamera;
    private bool _isInitialized;
    private bool _inputBlocked;

    public bool IsInitialized => _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized) return;
        _characterRoot = root;
        _packageSystem = root.characterPackageSystem;

        if (_packageSystem == null)
        {
            Debug.LogError("[CharacterPickupController] CharacterPackageSystem not found", this);
            return;
        }

        // Create dedicated trigger child for pickup detection.
        // Never touch the character's own collider (it's used for ground collision).
        var triggerGO = new GameObject("PickupTrigger");
        triggerGO.transform.SetParent(transform, false);
        triggerGO.transform.localPosition = Vector3.zero;
        var triggerCircle = triggerGO.AddComponent<CircleCollider2D>();
        triggerCircle.isTrigger = true;
        triggerCircle.radius = detectionRadius;

        _detector = triggerGO.AddComponent<PickupCandidateDetector>();

        _mainCamera = Camera.main;

        CreatePromptInstance();

        _isInitialized = true;
    }

    /// <summary>
    /// Creates the pickup prompt inside the HUD PickupPromptLayer.
    /// If a HUD layer already exists it is reused, otherwise a scene-level HUD Canvas is created.
    /// </summary>
    private void CreatePromptInstance()
    {
        Transform promptHost = ResolvePromptLayer();

        if (promptPrefab != null)
        {
            _promptInstance = Instantiate(promptPrefab, promptHost, false);
        }
        else
        {
            // Build a simple prompt UI at runtime inside the HUD layer.
            var promptGO = new GameObject("WorldPickupPrompt");
            promptGO.transform.SetParent(promptHost, false);
            var promptRT = promptGO.AddComponent<RectTransform>();
            promptRT.sizeDelta = new Vector2(140, 50);

            var cg = promptGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var bg = promptGO.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            bg.raycastTarget = false;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(promptGO.transform, false);
            var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = "[E]";
            text.fontSize = 14;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            _promptInstance = promptGO.AddComponent<WorldPickupPrompt>();
        }

        if (_promptInstance != null)
            _promptInstance.Initialize(_mainCamera);
    }

    /// <summary>
    /// Locates an existing PickupPromptLayer, or creates a scene-level HUD Canvas containing one.
    /// Never falls back to this character's Transform.
    /// </summary>
    private Transform ResolvePromptLayer()
    {
        // Explicit inspector assignment takes priority.
        if (promptParent != null)
            return promptParent;

        // Reuse an existing HUD prompt layer.
        var layer = Object.FindObjectOfType<PickupPromptLayer>();
        if (layer != null)
            return layer.transform;

        // Host canvas 鈥?scene-level Screen Space Overlay, detached from any character.
        var canvasGO = new GameObject("HUDCanvas_Pickup");
        canvasGO.transform.position = Vector3.zero;
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // The dedicated prompt layer rectangle (full-screen stretch) inside the HUD Canvas.
        var layerGO = new GameObject("PickupPromptLayer");
        layerGO.transform.SetParent(canvasGO.transform, false);
        var layerRT = layerGO.AddComponent<RectTransform>();
        layerRT.anchorMin = Vector2.zero;
        layerRT.anchorMax = Vector2.one;
        layerRT.offsetMin = Vector2.zero;
        layerRT.offsetMax = Vector2.zero;
        layerGO.AddComponent<PickupPromptLayer>();

        return layerGO.transform;
    }

    /// <summary>
    /// Called by InputHandler when E is pressed. Input system routes this here.
    /// </summary>
    public void OnPickupPressed()
    {
        if (!_isInitialized || _inputBlocked) return;
        if (_detector == null || !_detector.HasCandidate) return;

        var candidate = _detector.CurrentBest;
        if (candidate == null || candidate.Instance == null) return;

        bool success = _packageSystem.TryPickupItem(candidate.Instance);

        if (success)
        {
            _detector.RemoveCandidate(candidate);
            candidate.OnPickedUp();
        }
        else
        {
            // Backpack full 鈥?keep world object, prompt will show "Backpack Full"
        }
    }

    /// <summary>
    /// Block/unblock pickup input when UI is open.
    /// </summary>
    public void SetInputBlocked(bool blocked)
    {
        _inputBlocked = blocked;
        if (blocked && _promptInstance != null)
            _promptInstance.Hide();
    }

    private void Update()
    {
        if (!_isInitialized || _detector == null) return;
        if (_inputBlocked) return;

        if (_promptInstance == null) return;

        if (_detector.HasCandidate)
        {
            var candidate = _detector.CurrentBest;
            bool backpackFull = _packageSystem.Backpack != null && _packageSystem.Backpack.IsFull();
            _promptInstance.Show(
                candidate.PromptAnchor.position + Vector3.up * 0.5f,
                candidate.Instance?.DisplayName ?? "???",
                backpackFull);
        }
        else
        {
            _promptInstance.Hide();
        }
    }
}
```

## CharacterUIInputRouter.cs

```csharp
using UnityEngine;

/// <summary>
/// Routes input between Gameplay and PackageUI contexts.
/// Owns the Package Canvas as a scene-level UI (never a child of the character),
/// owns the system-cursor state, and toggles the character's gameplay input master switch.
/// </summary>
public class CharacterUIInputRouter : MonoBehaviour, ICharacterSubsystem
{
    public enum PlayerInputContext
    {
        Gameplay,
        PackageUI
    }

    [SerializeField] private GameObject packageCanvasPrefab;

    public PlayerInputContext CurrentContext { get; private set; } = PlayerInputContext.Gameplay;
    public bool IsUIOpen => CurrentContext == PlayerInputContext.PackageUI;

    private CharacterRoot _characterRoot;
    private CharacterPickupController _pickupController;
    private PackageUIController _packageUI;
    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized) return;
        _characterRoot = root;
        _pickupController = root.characterPickupController;

        // Auto-load prefab if not assigned
        if (packageCanvasPrefab == null)
        {
#if UNITY_EDITOR
            packageCanvasPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Script/Character/Core/Systems/PackageSystem/Prefabs/UI/PackageCanvas.prefab");
#endif
        }

        TryCreatePackageCanvas();

        _isInitialized = true;
    }

    /// <summary>
    /// Instantiates the Package Canvas under the scene-level GameUIRoot (never under the character),
    /// and refuses to dynamically add a missing PackageUIController (prefab must be complete).
    /// </summary>
    private void TryCreatePackageCanvas()
    {
        if (packageCanvasPrefab == null)
        {
            Debug.LogError("[CharacterUIInputRouter] packageCanvasPrefab 鏈厤缃紝鏃犳硶鍒涘缓鑳屽寘 UI銆?, this);
            return;
        }

        // 1. Locate or create the scene-level UI root (detached from character hierarchy)
        var uiRoot = FindObjectOfType<GameUIRoot>();
        Transform uiRootTransform;
        if (uiRoot != null)
        {
            uiRootTransform = uiRoot.transform;
        }
        else
        {
            var go = new GameObject("GameUIRoot");
            go.AddComponent<GameUIRoot>();
            uiRootTransform = go.transform;
        }

        // 2. Instantiate the canvas under the UI root. Do NOT parent to the character.
        var canvasGO = Instantiate(packageCanvasPrefab, uiRootTransform, false);

        // 3. Require a valid PackageUIController in the prefab 鈥?never dynamically add one.
        _packageUI = canvasGO.GetComponent<PackageUIController>();
        if (_packageUI == null)
        {
            Debug.LogError(
                "[CharacterUIInputRouter] PackageCanvas prefab 缂哄皯 PackageUIController锛岃鍏堝湪 Inspector 涓厤缃畬鏁寸殑 prefab銆?,
                canvasGO);
            return;
        }

        // 4. Initialize directly; controller hides itself after init.
        _packageUI.Initialize(_characterRoot.characterPackageSystem, this);
    }

    public void TogglePackageUI()
    {
        if (CurrentContext == PlayerInputContext.Gameplay)
            OpenPackageUI();
        else
            ClosePackageUI();
    }

    public void OpenPackageUI()
    {
        if (CurrentContext == PlayerInputContext.PackageUI) return;

        CurrentContext = PlayerInputContext.PackageUI;

        // 1) 绂佺敤 Gameplay 杈撳叆锛堝惈鐘舵€佹竻鐞嗭級
        if (_characterRoot != null && _characterRoot.inputHandler != null)
            _characterRoot.inputHandler.SetGameplayInputEnabled(false);

        // 2) 闃绘鎷惧彇骞堕殣钘忔嬀鍙栨彁绀?        if (_pickupController != null)
            _pickupController.SetInputBlocked(true);

        // 3) 缁熶竴绠＄悊绯荤粺榧犳爣鐘舵€?        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 4) 鎵撳紑鑳屽寘闈㈡澘
        if (_packageUI != null)
            _packageUI.Show();

        Debug.Log($"[PackageUI] Open frame={Time.frameCount} ctx={CurrentContext}");
    }

    public void ClosePackageUI()
    {
        if (CurrentContext == PlayerInputContext.Gameplay)
            return;

        // 1) 鍏堝叧闂?UI锛岄伩鍏嶅悓涓€甯х偣鍑荤┛閫忓埌 Gameplay 鏀诲嚮
        if (_packageUI != null)
            _packageUI.Hide();

        // 2) 鎭㈠ Gameplay 涓婁笅鏂?        CurrentContext = PlayerInputContext.Gameplay;

        // 3) 瑙ｉ櫎鎷惧彇闃绘
        if (_pickupController != null)
            _pickupController.SetInputBlocked(false);

        // 4) 鎭㈠ Gameplay 杈撳叆
        if (_characterRoot != null && _characterRoot.inputHandler != null)
            _characterRoot.inputHandler.SetGameplayInputEnabled(true);

        // 5) 鍏抽棴鑳屽寘鏃朵笉闅愯棌绯荤粺榧犳爣锛涘彲瑙佹€т笌閿佸畾鐘舵€佷氦鐢卞悇鑷殑鐬勫噯/鍑嗘槦绯荤粺鎺ョ锛?        //    Router 鍙礋璐ｇ‘淇濅笉鍐嶅皢榧犳爣闄愬埗鍦?Game 绐楀彛锛岄伩鍏嶅奖鍝嶅彸閿瀯鍑嗗悗鐨勭姸鎬併€?        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"[PackageUI] Close frame={Time.frameCount} ctx={CurrentContext}");
    }

    public bool IsGameplayActive => CurrentContext == PlayerInputContext.Gameplay;
}
```

## GameUIRoot.cs

```csharp
using UnityEngine;

/// <summary>
/// Marker component marking the scene-level Game UI root.
/// UI canvases (HUD, Package) are instantiated here, NOT under the character,
/// so they are unaffected by character flip / scale / Spine / pixelation.
/// Router locates this via FindObjectOfType at initialization.
/// </summary>
public sealed class GameUIRoot : MonoBehaviour
{
}
```

## PickupPromptLayer.cs

```csharp
using UnityEngine;

/// <summary>
/// Marker component marking the HUD canvas layer that hosts pickup prompts.
/// WorldPickupPrompt belongs inside a HUD Canvas, never under the character Transform.
/// </summary>
public sealed class PickupPromptLayer : MonoBehaviour
{
}
```

## WorldPickupPrompt.cs

```csharp
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Screen-space pickup prompt. Follows a world anchor position.
/// Lives inside a HUD Canvas (PickupPromptLayer), never under the character Transform.
/// Owned and instantiated by CharacterPickupController.
/// </summary>
public class WorldPickupPrompt : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private UnityEngine.UI.Image background;
    [SerializeField] private TMPro.TextMeshProUGUI labelText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color fullColor = new Color(0.6f, 0.1f, 0.1f, 0.85f);

    private Camera _mainCamera;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (background == null)
            background = GetComponentInChildren<UnityEngine.UI.Image>();
        if (labelText == null)
            labelText = GetComponentInChildren<TMPro.TextMeshProUGUI>();

        if (canvasGroup != null)
        {
            // Prompt 姘镐笉闃绘尅 UI Raycast
            canvasGroup.blocksRaycasts = false;
        }
        Hide();
    }

    public void Initialize(Camera cam)
    {
        _mainCamera = cam;
    }

    public void Show(Vector3 worldAnchor, string displayName, bool backpackFull)
    {
        if (_mainCamera == null) return;

        // 鐩爣鍦ㄧ浉鏈哄悗鏂癸紙鑳岄潰锛夋椂闅愯棌
        Vector3 viewport = _mainCamera.WorldToViewportPoint(worldAnchor);
        if (viewport.z < 0f)
        {
            Hide();
            return;
        }

        var parentRT = transform.parent as RectTransform;
        if (parentRT == null)
        {
            // 鐖惰妭鐐瑰繀椤绘槸 RectTransform锛堜綅浜庢煇涓?Canvas 鍥惧眰鍐咃級
            Hide();
            return;
        }

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldAnchor);

        // 渚?Canvas 娓叉煋妯″紡閫夋嫨姝ｇ‘鐨?UI 鐩告満锛?        //  Screen Space - Overlay锛歎I 鐩告満涓?null
        //  Screen Space - Camera锛欳anvas.worldCamera
        Canvas canvas = GetComponentInParent<Canvas>();
        DecideUiCamera(canvas, out Camera uiCamera);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                screenPos,
                uiCamera,
                out Vector2 localPos))
        {
            // 杞崲澶辫触锛堥€氬父鐩爣鍦ㄥ睆骞曞锛夛紝闅愯棌
            Hide();
            return;
        }

        ((RectTransform)transform).anchoredPosition = localPos;

        if (labelText != null)
        {
            if (backpackFull)
                labelText.text = $"[E] {displayName}\n<color=#ff6666>Backpack Full</color>";
            else
                labelText.text = $"[E] {displayName}";
        }

        if (background != null)
            background.color = backpackFull ? fullColor : normalColor;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void DecideUiCamera(Canvas canvas, out Camera uiCamera)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = null; // Overlay canvas 浣跨敤 null UI camera
        }
        else
        {
            uiCamera = canvas.worldCamera;
        }
    }

    public void Hide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}
```

## BackpackSlotView.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Binds a single inventory slot's visual elements to data.
/// Implements drag/drop interfaces to forward interactions to PackageUIController.
/// Does NOT own item data 鈥?only displays and forwards events.
/// </summary>
public class BackpackSlotView : MonoBehaviour,
    IPointerDownHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image selectionFrame;
    [SerializeField] private Image blockedOverlay;

    public int SlotIndex { get; private set; }

    private PackageUIController _controller;
    private bool _draggedThisFrame;
    private bool _dragFirstMove = true;

    private static string GetPath(Transform t)
    {
        if (t == null) return "null";
        var sb = new System.Text.StringBuilder(t.name);
        var p = t.parent;
        while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
    }

    private void Awake()
    {
        if (iconImage == null) { var t = transform.Find("Icon"); if (t != null) iconImage = t.GetComponent<Image>(); }
        if (quantityText == null) { var t = transform.Find("Qty"); if (t != null) quantityText = t.GetComponent<TextMeshProUGUI>(); }
        if (selectionFrame == null) { var t = transform.Find("Selection"); if (t != null) selectionFrame = t.GetComponent<Image>(); }
        if (blockedOverlay == null) { var t = transform.Find("Blocked"); if (t != null) blockedOverlay = t.GetComponent<Image>(); }

        var slotImage = GetComponent<Image>();
        if (slotImage != null) slotImage.raycastTarget = true;
        if (iconImage != null) iconImage.raycastTarget = false;
        if (selectionFrame != null) selectionFrame.raycastTarget = false;
        if (blockedOverlay != null) blockedOverlay.raycastTarget = false;
    }

    public void Initialize(int index, PackageUIController controller)
    {
        SlotIndex = index;
        _controller = controller;
        SetSelected(false);
        SetBlocked(false);
        Clear();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnPointerDown slot={SlotIndex} path={GetPath(transform)} pointerPress={eventData.pointerPress} button={eventData.button}");
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnInitializePotentialDrag slot={SlotIndex} path={GetPath(transform)}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        var item = _controller.GetBackpackItem(SlotIndex);
        if (item == null) { Debug.Log($"[Drag] OnBeginDrag ABORT slot={SlotIndex} item is null"); return; }

        _draggedThisFrame = true;
        _dragFirstMove = true;
        _controller.BeginDragFromBackpack(SlotIndex);
        Debug.Log($"[Drag] OnBeginDrag slot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragFirstMove)
        {
            Debug.Log($"[Drag] OnDrag (first) slot={SlotIndex} path={GetPath(transform)} raycast={eventData.pointerCurrentRaycast.gameObject}");
            _dragFirstMove = false;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnDrop TARGET slot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");

        if (_controller == null) return;
        if (!_controller.IsDragging) { Debug.Log("[Drag] OnDrop but controller not dragging"); return; }

        // Use controller's saved drag state instead of GetComponent
        _controller.HandleDropOnBackpackSlot(SlotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnEndDrag slot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag}");
        // Cleanup is handled by controller after drop, or as fallback here
        if (_controller != null && _controller.IsDragging)
            _controller.EndDrag();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_draggedThisFrame)
        {
            _draggedThisFrame = false;
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        var item = _controller.GetBackpackItem(SlotIndex);
        if (item != null)
            _controller.ToggleBackpackSelection(SlotIndex);
    }

    // --- Data binding ---

    public void Refresh(ItemInstance item)
    {
        if (item == null || item.definition == null) { Clear(); return; }
        if (iconImage != null) { iconImage.sprite = item.Icon; iconImage.enabled = item.Icon != null; iconImage.color = Color.white; }
        if (quantityText != null) { quantityText.text = item.quantity > 1 ? item.quantity.ToString() : ""; quantityText.gameObject.SetActive(item.quantity > 1); }
    }

    public void Clear()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (quantityText != null) { quantityText.text = ""; quantityText.gameObject.SetActive(false); }
    }

    public void SetSelected(bool selected) { if (selectionFrame != null) selectionFrame.gameObject.SetActive(selected); }
    public void SetBlocked(bool blocked) { if (blockedOverlay != null) blockedOverlay.gameObject.SetActive(blocked); }
}
```

## WeaponSlotView.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Binds a weapon slot visual to data. Shows weapon icon or empty state.
/// Implements drag/drop interfaces to forward interactions to PackageUIController.
/// </summary>
public class WeaponSlotView : MonoBehaviour,
    IPointerDownHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private Image selectionFrame;
    [SerializeField] private Image activeHighlight;

    public int SlotIndex { get; private set; }

    private PackageUIController _controller;
    private bool _draggedThisFrame;
    private bool _dragFirstMove = true;

    private static string GetPath(Transform t)
    {
        if (t == null) return "null";
        var sb = new System.Text.StringBuilder(t.name);
        var p = t.parent;
        while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
    }

    public void Initialize(int index, PackageUIController controller)
    {
        SlotIndex = index;
        _controller = controller;
        SetSelected(false);
        SetActive(false);
        if (slotLabel != null) slotLabel.text = $"Slot {index}";
        Clear();

        var slotImage = GetComponent<Image>();
        if (slotImage != null) slotImage.raycastTarget = true;
        if (iconImage != null) iconImage.raycastTarget = false;
        if (selectionFrame != null) selectionFrame.raycastTarget = false;
        if (activeHighlight != null) activeHighlight.raycastTarget = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnPointerDown weaponSlot={SlotIndex} path={GetPath(transform)} pointerPress={eventData.pointerPress} button={eventData.button}");
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnInitializePotentialDrag weaponSlot={SlotIndex} path={GetPath(transform)}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        var item = _controller.GetWeaponSlotItem(SlotIndex);
        if (item == null) { Debug.Log($"[Drag] OnBeginDrag ABORT weaponSlot={SlotIndex} item is null"); return; }

        _draggedThisFrame = true;
        _dragFirstMove = true;
        _controller.BeginDragFromWeaponSlot(SlotIndex);
        Debug.Log($"[Drag] OnBeginDrag weaponSlot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragFirstMove)
        {
            Debug.Log($"[Drag] OnDrag (first) weaponSlot={SlotIndex} path={GetPath(transform)} raycast={eventData.pointerCurrentRaycast.gameObject}");
            _dragFirstMove = false;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnDrop TARGET weaponSlot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");

        if (_controller == null) return;
        if (!_controller.IsDragging) { Debug.Log("[Drag] OnDrop but controller not dragging"); return; }

        _controller.HandleDropOnWeaponSlot(SlotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnEndDrag weaponSlot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag}");
        if (_controller != null && _controller.IsDragging)
            _controller.EndDrag();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_draggedThisFrame) { _draggedThisFrame = false; return; }
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        _controller.TrySelectionEquip(SlotIndex);
        _controller.SetActiveWeapon(SlotIndex);
    }

    // --- Data binding ---

    public void Refresh(ItemInstance item)
    {
        if (item == null || item.definition == null) { Clear(); return; }
        if (iconImage != null) { iconImage.sprite = item.Icon; iconImage.enabled = item.Icon != null; }
    }

    public void Clear() { if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; } }
    public void SetSelected(bool selected) { if (selectionFrame != null) selectionFrame.gameObject.SetActive(selected); }
    public void SetActive(bool active) { if (activeHighlight != null) activeHighlight.gameObject.SetActive(active); }
}
```

## BackpackGridView.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the backpack grid: creates slot views and refreshes them from data.
/// Passes PackageUIController reference to each slot for drag/drop forwarding.
/// </summary>
public class BackpackGridView : MonoBehaviour
{
    [SerializeField] private BackpackSlotView slotViewPrefab;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private int slotsPerPage = 30;

    [Header("Runtime Fallback Sizing")]
    [SerializeField] private Vector2 runtimeSlotSize = new Vector2(64, 64);
    [SerializeField] private int runtimeColumns = 6;
    [SerializeField] private float runtimeSpacing = 4f;

    private readonly List<BackpackSlotView> _slotViews = new List<BackpackSlotView>();
    private CharacterPackageSystem _packageSystem;
    private PackageUIController _controller;
    private int _selectedSlot = -1;

    public int SelectedSlot => _selectedSlot;

    public void Initialize(CharacterPackageSystem packageSystem, PackageUIController controller)
    {
        _packageSystem = packageSystem;
        _controller = controller;
        CreateSlotViews();
    }

    private void CreateSlotViews()
    {
        if (gridContainer == null)
            gridContainer = transform;

        for (int i = gridContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(gridContainer.GetChild(i).gameObject);

        _slotViews.Clear();

        int capacity = _packageSystem?.Backpack?.Capacity ?? slotsPerPage;

        var grid = gridContainer.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = gridContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = runtimeSlotSize;
            grid.spacing = Vector2.one * runtimeSpacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = runtimeColumns;
            grid.padding = new RectOffset(2, 2, 2, 2);
        }

        for (int i = 0; i < capacity; i++)
        {
            BackpackSlotView view;
            if (slotViewPrefab != null)
            {
                view = Instantiate(slotViewPrefab, gridContainer);
            }
            else
            {
                view = CreateRuntimeSlotView(gridContainer, i);
            }
            view.Initialize(i, _controller);
            _slotViews.Add(view);
        }
    }

    private BackpackSlotView CreateRuntimeSlotView(Transform parent, int index)
    {
        var go = new GameObject($"Slot_{index}");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        bg.raycastTarget = true;

        var selGO = new GameObject("Selection");
        selGO.transform.SetParent(go.transform, false);
        var selRT = selGO.AddComponent<RectTransform>();
        selRT.anchorMin = Vector2.zero; selRT.anchorMax = Vector2.one;
        selRT.offsetMin = Vector2.zero; selRT.offsetMax = Vector2.zero;
        var selImg = selGO.AddComponent<Image>();
        selImg.color = new Color(0.35f, 0.5f, 0.8f, 0.6f);
        selImg.raycastTarget = false;
        selGO.SetActive(false);

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.15f);
        iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.offsetMin = Vector2.zero; iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.enabled = false;
        iconImg.raycastTarget = false;

        var qtyGO = new GameObject("Qty");
        qtyGO.transform.SetParent(go.transform, false);
        var qtyRT = qtyGO.AddComponent<RectTransform>();
        qtyRT.anchorMin = new Vector2(0.5f, 0f);
        qtyRT.anchorMax = new Vector2(1f, 0.4f);
        qtyRT.offsetMin = Vector2.zero; qtyRT.offsetMax = Vector2.zero;
        var qtyText = qtyGO.AddComponent<TextMeshProUGUI>();
        qtyText.fontSize = 12;
        qtyText.alignment = TextAlignmentOptions.BottomRight;
        qtyText.raycastTarget = false;
        qtyGO.SetActive(false);

        return go.AddComponent<BackpackSlotView>();
    }

    public void RefreshAll()
    {
        if (_packageSystem?.Backpack == null) return;
        for (int i = 0; i < _slotViews.Count; i++)
            _slotViews[i].Refresh(_packageSystem.Backpack.GetItem(i));
    }

    public void RefreshSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotViews.Count) return;
        if (_packageSystem?.Backpack == null) return;
        _slotViews[slotIndex].Refresh(_packageSystem.Backpack.GetItem(slotIndex));
    }

    public void SetSelected(int slotIndex)
    {
        if (_selectedSlot >= 0 && _selectedSlot < _slotViews.Count)
            _slotViews[_selectedSlot].SetSelected(false);
        _selectedSlot = slotIndex;
        if (_selectedSlot >= 0 && _selectedSlot < _slotViews.Count)
            _slotViews[_selectedSlot].SetSelected(true);
    }

    public BackpackSlotView GetSlotView(int index)
    {
        if (index < 0 || index >= _slotViews.Count) return null;
        return _slotViews[index];
    }
}
```

## PackageUIController.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Main UI controller for the package panel.
/// Manages backpack grid + weapon slots + selection + drag/drop.
/// Subscribes to CharacterPackageSystem events; never owns data.
/// </summary>
public class PackageUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BackpackGridView backpackGrid;
    [SerializeField] private WeaponSlotView[] weaponSlotViews = new WeaponSlotView[3];
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image dragIcon;
    [SerializeField] private GameObject panelRoot;

    private CharacterPackageSystem _packageSystem;
    private CharacterUIInputRouter _inputRouter;
    private bool _isOpen;
    private int _selectedBackpackSlot = -1;
    private bool _isDragging;

    // Drag state
    private int _dragSourceSlot = -1;
    private bool _dragFromWeaponSlot;

    public bool IsOpen => _isOpen;
    public bool IsDragging => _isDragging;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (panelRoot == null)
        {
            var panelT = transform.Find("Panel");
            if (panelT != null)
                panelRoot = panelT.gameObject;
        }

        if (dragIcon != null)
            dragIcon.raycastTarget = false;
    }

    public void Initialize(CharacterPackageSystem packageSystem, CharacterUIInputRouter inputRouter)
    {
        _packageSystem = packageSystem;
        _inputRouter = inputRouter;

        if (backpackGrid != null)
            backpackGrid.Initialize(packageSystem, this);

        for (int i = 0; i < weaponSlotViews.Length; i++)
        {
            if (weaponSlotViews[i] != null)
                weaponSlotViews[i].Initialize(i, this);
        }

        if (_packageSystem != null)
        {
            _packageSystem.OnBackpackSlotChanged += OnBackpackSlotChanged;
            _packageSystem.OnWeaponSlotChanged += OnWeaponSlotChanged;
            _packageSystem.OnActiveWeaponSlotChanged += OnActiveWeaponSlotChanged;
        }

        RefreshAll();
        Hide();
    }

    private void OnDestroy()
    {
        if (_packageSystem != null)
        {
            _packageSystem.OnBackpackSlotChanged -= OnBackpackSlotChanged;
            _packageSystem.OnWeaponSlotChanged -= OnWeaponSlotChanged;
            _packageSystem.OnActiveWeaponSlotChanged -= OnActiveWeaponSlotChanged;
        }
    }

    // --- Visibility ---

    public void Show()
    {
        SetVisible(true);
        RefreshAll();
    }

    public void Hide()
    {
        CancelSelection();
        SetVisible(false);
    }

    public void Toggle()
    {
        if (_isOpen) Hide();
        else Show();
    }

    private void SetVisible(bool visible)
    {
        _isOpen = visible;

        if (panelRoot != null)
            panelRoot.SetActive(visible);

        if (canvasGroup != null)
        {
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.alpha = 1f;
        }
    }

    // --- Event handlers ---

    private void OnBackpackSlotChanged(int slotIndex, ItemInstance item)
    {
        if (backpackGrid != null)
            backpackGrid.RefreshSlot(slotIndex);
    }

    private void OnWeaponSlotChanged(int slotIndex, ItemInstance item)
    {
        if (slotIndex >= 0 && slotIndex < weaponSlotViews.Length && weaponSlotViews[slotIndex] != null)
            weaponSlotViews[slotIndex].Refresh(item);
    }

    private void OnActiveWeaponSlotChanged(int prevIndex, int newIndex)
    {
        if (prevIndex >= 0 && prevIndex < weaponSlotViews.Length && weaponSlotViews[prevIndex] != null)
            weaponSlotViews[prevIndex].SetActive(false);
        if (newIndex >= 0 && newIndex < weaponSlotViews.Length && weaponSlotViews[newIndex] != null)
            weaponSlotViews[newIndex].SetActive(true);
    }

    // --- Data accessors for slot views ---

    public ItemInstance GetBackpackItem(int slotIndex)
    {
        return _packageSystem?.Backpack?.GetItem(slotIndex);
    }

    public ItemInstance GetWeaponSlotItem(int slotIndex)
    {
        return _packageSystem?.WeaponSlots?.GetItem(slotIndex);
    }

    // --- Selection ---

    public void ToggleBackpackSelection(int slotIndex)
    {
        if (_selectedBackpackSlot == slotIndex)
        {
            CancelSelection();
        }
        else
        {
            _selectedBackpackSlot = slotIndex;
            if (backpackGrid != null)
                backpackGrid.SetSelected(slotIndex);
        }
    }

    public void CancelSelection()
    {
        _selectedBackpackSlot = -1;
        if (backpackGrid != null)
            backpackGrid.SetSelected(-1);
        CancelDrag();
    }

    // --- Drag state: OnBeginDrag sets, OnEndDrag clears ---

    public void BeginDragFromBackpack(int slotIndex)
    {
        var item = GetBackpackItem(slotIndex);
        if (item == null) return;

        _isDragging = true;
        _dragSourceSlot = slotIndex;
        _dragFromWeaponSlot = false;

        if (dragIcon != null)
        {
            dragIcon.sprite = item.Icon;
            dragIcon.enabled = item.Icon != null;
            dragIcon.color = Color.white;
            dragIcon.transform.SetAsLastSibling();
        }
    }

    public void BeginDragFromWeaponSlot(int slotIndex)
    {
        var item = GetWeaponSlotItem(slotIndex);
        if (item == null) return;

        _isDragging = true;
        _dragSourceSlot = slotIndex;
        _dragFromWeaponSlot = true;

        if (dragIcon != null)
        {
            dragIcon.sprite = item.Icon;
            dragIcon.enabled = item.Icon != null;
            dragIcon.color = Color.white;
            dragIcon.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// Clears drag state. Called by OnEndDrag ONLY. Not called from OnDrop or Update.
    /// </summary>
    public void EndDrag()
    {
        _isDragging = false;
        _dragSourceSlot = -1;
        _dragFromWeaponSlot = false;

        if (dragIcon != null)
            dragIcon.enabled = false;
    }

    public void CancelDrag()
    {
        EndDrag();
    }

    // --- Drop handlers: called by slot views' OnDrop ---
    // These use the saved drag state (_dragSourceSlot, _dragFromWeaponSlot).
    // They do NOT call EndDrag 鈥?OnEndDrag handles cleanup after OnDrop returns.

    /// <summary>
    /// Target = backpack slot. Routes based on drag source.
    /// </summary>
    public void HandleDropOnBackpackSlot(int targetSlot)
    {
        if (!_isDragging) return;

        TransferResult result;

        if (_dragFromWeaponSlot)
        {
            // Weapon slot 鈫?Backpack slot
            result = _packageSystem.UnequipWeaponToSlot(_dragSourceSlot, targetSlot);
        }
        else
        {
            // Backpack 鈫?Backpack
            if (_dragSourceSlot == targetSlot)
            {
                // Same slot = no-op
                return;
            }
            result = _packageSystem.MoveBackpackItem(_dragSourceSlot, targetSlot);
        }

        if (!result.success)
            Debug.Log($"[PackageUI] Drop on backpack slot {targetSlot} failed: {result.errorMessage}");
    }

    /// <summary>
    /// Target = weapon slot. Routes based on drag source.
    /// </summary>
    public void HandleDropOnWeaponSlot(int targetSlot)
    {
        if (!_isDragging) return;

        TransferResult result;

        if (_dragFromWeaponSlot)
        {
            // Weapon slot 鈫?Weapon slot
            if (_dragSourceSlot == targetSlot)
            {
                // Same slot = no-op
                return;
            }
            result = _packageSystem.SwapWeaponSlots(_dragSourceSlot, targetSlot);
        }
        else
        {
            // Backpack 鈫?Weapon slot
            result = _packageSystem.EquipWeapon(_dragSourceSlot, targetSlot);
        }

        if (!result.success)
            Debug.Log($"[PackageUI] Drop on weapon slot {targetSlot} failed: {result.errorMessage}");
    }

    /// <summary>
    /// Selection-box equip: click backpack item, then click weapon slot.
    /// </summary>
    public void TrySelectionEquip(int weaponSlotIndex)
    {
        if (_selectedBackpackSlot < 0) return;

        var result = _packageSystem.EquipWeapon(_selectedBackpackSlot, weaponSlotIndex);
        if (!result.success)
            Debug.Log($"[PackageUI] Equip failed: {result.errorMessage}");

        CancelSelection();
    }

    /// <summary>
    /// Set active weapon slot (clicking a weapon slot).
    /// </summary>
    public void SetActiveWeapon(int slotIndex)
    {
        if (_packageSystem?.WeaponSlots == null) return;
        if (!_packageSystem.WeaponSlots.IsSlotEmpty(slotIndex))
            _packageSystem.SetActiveWeaponSlot(slotIndex);
    }

    // --- Refresh ---

    public void RefreshAll()
    {
        if (backpackGrid != null)
            backpackGrid.RefreshAll();

        if (_packageSystem?.WeaponSlots != null)
        {
            for (int i = 0; i < weaponSlotViews.Length; i++)
            {
                if (weaponSlotViews[i] == null) continue;
                var item = _packageSystem.WeaponSlots.GetItem(i);
                weaponSlotViews[i].Refresh(item);
                weaponSlotViews[i].SetActive(i == _packageSystem.WeaponSlots.ActiveSlotIndex);
            }
        }
    }

    private void Update()
    {
        if (!_isOpen) return;

        if (_isDragging && dragIcon != null && dragIcon.enabled)
        {
            dragIcon.transform.position = Input.mousePosition;
        }
    }
}
```

