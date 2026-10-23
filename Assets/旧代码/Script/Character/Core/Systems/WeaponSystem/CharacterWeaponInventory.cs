using System;
using UnityEngine;

/// <summary>
/// Adapter layer between the new PackageSystem (CharacterWeaponSlots) and the existing weapon pipeline.
/// Reads from CharacterWeaponSlots at runtime; no longer accepts test injection from Inspector.
/// </summary>
[DisallowMultipleComponent]
public class CharacterWeaponInventory :
    MonoBehaviour,
    ICharacterSubsystem
{
    private const int SlotCount = 3;

    [Header("Weapon Slots")]
    [SerializeField]
    private CharacterWeaponSlot[] slots =
        new CharacterWeaponSlot[SlotCount];

    [Header("Runtime")]
    [SerializeField]
    private WeaponSlotId activeSlotId =
        WeaponSlotId.Slot1;

    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;
    private CharacterPackageSystem packageSystem;
    private bool isInitialized;

    public WeaponSlotId ActiveSlotId => activeSlotId;

    public WeaponRuntimeData ActiveWeapon
    {
        get
        {
            CharacterWeaponSlot slot =
                GetSlot(activeSlotId);

            return slot != null
                ? slot.Weapon
                : null;
        }
    }

    public bool HasActiveWeapon =>
        ActiveWeapon != null;

    public bool ActiveWeaponIsFirearm =>
        ActiveWeapon != null &&
        ActiveWeapon.IsFirearm;

    public bool ActiveWeaponIsMelee =>
        ActiveWeapon != null &&
        ActiveWeapon.IsMelee;

    public event Action<
        WeaponSlotId,
        WeaponRuntimeData,
        WeaponSlotId,
        WeaponRuntimeData> ActiveWeaponChanged;

    public void Initialize(CharacterRoot root)
    {
        if (isInitialized)
            return;

        characterRoot = root;

        if (characterRoot == null)
        {
            Debug.LogError("[CharacterWeaponInventory] CharacterRoot == null", this);
            return;
        }

        dataBus = characterRoot.characterDataBus;
        packageSystem = characterRoot.characterPackageSystem;

        if (dataBus == null)
        {
            Debug.LogError("[CharacterWeaponInventory] CharacterDataBus == null", this);
            return;
        }

        EnsureSlots();

        // Sync from PackageSystem if available (new game init already placed weapons there)
        if (packageSystem != null && packageSystem.WeaponSlots != null)
        {
            // Initialize activeSlotId from PackageSystem
            activeSlotId = (WeaponSlotId)packageSystem.WeaponSlots.ActiveSlotIndex;

            SyncFromPackageSystem();

            // Subscribe to runtime weapon slot and active slot changes
            packageSystem.OnWeaponSlotChanged += OnPackageWeaponSlotChanged;
            packageSystem.OnActiveWeaponSlotChanged += OnPackageActiveWeaponSlotChanged;
        }

        dataBus.weaponInventory = this;

        MigrateLegacyCurrentWeapon();
        SyncLegacyCurrentWeapon();

        isInitialized = true;
    }

    private void OnPackageActiveWeaponSlotChanged(int previousIndex, int newIndex)
    {
        WeaponRuntimeData previousWeapon = ActiveWeapon;
        activeSlotId = (WeaponSlotId)newIndex;
        SyncLegacyCurrentWeapon();
        NotifyActiveWeaponChanged((WeaponSlotId)previousIndex, previousWeapon, activeSlotId, ActiveWeapon);
    }

    /// <summary>
    /// Called when CharacterPackageSystem weapon slots change at runtime (e.g. drag-drop equip/unequip).
    /// Syncs the new weapon data into the legacy pipeline.
    /// Does NOT auto-switch active slot when a weapon is removed — allows empty active slot.
    /// </summary>
    private void OnPackageWeaponSlotChanged(int slotIndex, ItemInstance item)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;

        CharacterWeaponSlot slot = GetSlot((WeaponSlotId)slotIndex);
        if (slot == null) return;

        bool isAcitveSlot = (slotIndex == (int)activeSlotId);

        if (item == null)
        {
            // Weapon was removed from this slot
            WeaponRuntimeData previousActive = isAcitveSlot ? ActiveWeapon : null;

            if (!slot.IsEmpty)
                slot.RemoveWeapon();

            // Only notify if active slot changed
            if (isAcitveSlot)
            {
                NotifyActiveWeaponChanged(activeSlotId, previousActive, activeSlotId, null);
            }
        }
        else if (item.definition is WeaponItemDefinitionSO weaponDef)
        {
            WeaponRuntimeData previousActive = isAcitveSlot ? ActiveWeapon : null;

            // Cache: reuse existing WeaponRuntimeData if same instanceId
            WeaponRuntimeData runtimeData = FindOrCreateRuntimeData(item, weaponDef);
            slot.SetWeapon(runtimeData);

            if (isAcitveSlot)
            {
                NotifyActiveWeaponChanged(activeSlotId, previousActive, activeSlotId, slot.Weapon);
            }
        }

        SyncLegacyCurrentWeapon();
    }

    private readonly System.Collections.Generic.Dictionary<string, WeaponRuntimeData> _runtimeDataCache = new();

    private WeaponRuntimeData FindOrCreateRuntimeData(ItemInstance item, WeaponItemDefinitionSO weaponDef)
    {
        if (!string.IsNullOrEmpty(item.instanceId) && _runtimeDataCache.TryGetValue(item.instanceId, out var cached))
            return cached;

        var data = new WeaponRuntimeData(weaponDef.WeaponConfig);
        if (!string.IsNullOrEmpty(item.instanceId))
            _runtimeDataCache[item.instanceId] = data;
        return data;
    }

    /// <summary>
    /// Copy weapon instances from CharacterWeaponSlots into the legacy slot array.
    /// This bridges the new ItemInstance-based system with the existing WeaponRuntimeData pipeline.
    /// </summary>
    private void SyncFromPackageSystem()
    {
        var weaponSlots = packageSystem.WeaponSlots;
        for (int i = 0; i < SlotCount && i < weaponSlots.Capacity; i++)
        {
            var itemInstance = weaponSlots.GetItem(i);
            if (itemInstance == null) continue;

            // Extract WeaponConfigSO from the WeaponItemDefinitionSO
            if (itemInstance.definition is WeaponItemDefinitionSO weaponDef)
            {
                var slot = GetSlot((WeaponSlotId)i);
                if (slot != null && slot.IsEmpty)
                {
                    slot.SetWeapon(new WeaponRuntimeData(weaponDef.WeaponConfig));
                }
            }
        }
    }

    public CharacterWeaponSlot GetSlot(WeaponSlotId slotId)
    {
        int index = (int)slotId;
        if (slots == null || index < 0 || index >= slots.Length)
            return null;
        return slots[index];
    }

    public WeaponRuntimeData GetWeapon(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        return slot != null ? slot.Weapon : null;
    }

    public bool HasWeapon(WeaponSlotId slotId)
    {
        return GetWeapon(slotId) != null;
    }

    public bool CanSelectSlot(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        return slot != null && slot.CanSelect;
    }

    public bool TryAssignWeapon(
        WeaponSlotId slotId,
        WeaponRuntimeData weapon,
        bool replaceExisting = false)
    {
        if (weapon == null)
            return false;

        CharacterWeaponSlot slot = GetSlot(slotId);
        if (slot == null || !slot.IsUnlocked)
            return false;

        if (!slot.IsEmpty && !replaceExisting)
            return false;

        WeaponRuntimeData oldActiveWeapon = ActiveWeapon;
        slot.SetWeapon(weapon);

        if (slotId == activeSlotId)
        {
            NotifyActiveWeaponChanged(activeSlotId, oldActiveWeapon, activeSlotId, weapon);
        }

        return true;
    }

    public WeaponRuntimeData RemoveWeapon(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        if (slot == null || slot.IsEmpty)
            return null;

        WeaponRuntimeData oldActiveWeapon = ActiveWeapon;
        WeaponRuntimeData removedWeapon = slot.RemoveWeapon();

        if (slotId == activeSlotId)
        {
            NotifyActiveWeaponChanged(activeSlotId, oldActiveWeapon, activeSlotId, null);
        }

        return removedWeapon;
    }

    public bool TrySetActiveSlot(WeaponSlotId slotId)
    {
        // Route through PackageSystem when available
        if (packageSystem != null && packageSystem.WeaponSlots != null)
        {
            return packageSystem.SetActiveWeaponSlot((int)slotId);
        }

        // Legacy fallback (no PackageSystem)
        if (!CanSelectSlot(slotId))
            return false;
        if (slotId == activeSlotId)
            return true;

        WeaponSlotId previousSlotId = activeSlotId;
        WeaponRuntimeData previousWeapon = ActiveWeapon;
        activeSlotId = slotId;
        NotifyActiveWeaponChanged(previousSlotId, previousWeapon, activeSlotId, ActiveWeapon);
        return true;
    }

    public bool TryGetNextOccupiedSlot(out WeaponSlotId nextSlotId)
    {
        return TryGetNextOccupiedSlotFrom(activeSlotId, out nextSlotId);
    }

    public bool TrySelectNextOccupiedSlot()
    {
        if (!TryGetNextOccupiedSlot(out WeaponSlotId nextSlotId))
            return false;
        return TrySetActiveSlot(nextSlotId);
    }

    private void EnsureSlots()
    {
        if (slots == null || slots.Length != SlotCount)
            Array.Resize(ref slots, SlotCount);

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                slots[i] = new CharacterWeaponSlot();
            slots[i].Initialize((WeaponSlotId)i);
        }
    }

    private void MigrateLegacyCurrentWeapon()
    {
        if (dataBus.currentWeaponRuntimeData == null)
            return;

        CharacterWeaponSlot firstSlot = GetSlot(WeaponSlotId.Slot1);
        if (firstSlot == null || !firstSlot.IsEmpty)
            return;

        firstSlot.SetWeapon(dataBus.currentWeaponRuntimeData);
        activeSlotId = WeaponSlotId.Slot1;

        Debug.Log("[CharacterWeaponInventory] Migrated currentWeaponRuntimeData to Slot1", characterRoot);
    }

    private void NotifyActiveWeaponChanged(
        WeaponSlotId previousSlotId,
        WeaponRuntimeData previousWeapon,
        WeaponSlotId newSlotId,
        WeaponRuntimeData newWeapon)
    {
        SyncLegacyCurrentWeapon();
        ActiveWeaponChanged?.Invoke(previousSlotId, previousWeapon, newSlotId, newWeapon);
    }

    public bool TryGetNextOccupiedSlotFrom(WeaponSlotId fromSlotId, out WeaponSlotId nextSlotId)
    {
        nextSlotId = fromSlotId;
        if (slots == null || slots.Length == 0)
            return false;

        int startIndex = (int)fromSlotId;
        for (int offset = 1; offset <= slots.Length; offset++)
        {
            int candidateIndex = (startIndex + offset) % slots.Length;
            CharacterWeaponSlot candidate = slots[candidateIndex];
            if (candidate == null) continue;
            if (!candidate.CanSelect) continue;
            nextSlotId = candidate.SlotId;
            return true;
        }
        return false;
    }

    private void SyncLegacyCurrentWeapon()
    {
        if (dataBus == null)
            return;
        dataBus.currentWeaponRuntimeData = ActiveWeapon;
    }
}
