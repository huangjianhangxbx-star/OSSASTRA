using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative slot-based party backpack for the DRPG project.
/// Equipped items and settlement recovery items intentionally live outside
/// these slots and therefore do not consume backpack capacity or weight.
/// </summary>
public sealed partial class DRPGPartyInventoryState :
    IDRPGInventoryMutableContainer
{
    private const float WeightEpsilon = 0.0001f;
    private const string PartyContainerId = "party-backpack";

    private readonly Dictionary<string, DRPGWeaponInstance> backpackWeapons =
        new Dictionary<string, DRPGWeaponInstance>(StringComparer.Ordinal);
    private readonly Dictionary<string, DRPGWeaponInstance>
        settlementRecoveryWeapons =
            new Dictionary<string, DRPGWeaponInstance>(StringComparer.Ordinal);
    private HashSet<string> equippedWeaponInstanceIds =
        new HashSet<string>(StringComparer.Ordinal);
    private Dictionary<string, int> weaponFragmentCounts =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, DRPGRecoveredAccessoryItem>
        backpackAccessories =
            new Dictionary<string, DRPGRecoveredAccessoryItem>(StringComparer.Ordinal);
    private readonly Dictionary<string, DRPGRecoveredAccessoryItem>
        settlementRecoveryAccessories =
            new Dictionary<string, DRPGRecoveredAccessoryItem>(StringComparer.Ordinal);

    private DRPGInventoryItemStack[] slots;

    public DRPGPartyInventoryState(int slotCapacity, float weightCapacity)
    {
        if (slotCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(slotCapacity));
        if (weightCapacity < 0f)
            throw new ArgumentOutOfRangeException(nameof(weightCapacity));
        SlotCapacity = slotCapacity;
        WeightCapacity = weightCapacity;
        slots = new DRPGInventoryItemStack[slotCapacity];
    }

    public string ContainerId => PartyContainerId;
    public int Capacity => SlotCapacity;
    public int SlotCapacity { get; }
    public float WeightCapacity { get; }
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
    private int revision;
    internal long MutationEpoch { get; private set; }
    public int Revision
    {
        get => revision;
        private set { revision = value; MutationEpoch = unchecked(MutationEpoch + 1); }
    }
    public IReadOnlyCollection<DRPGWeaponInstance> BackpackWeapons =>
        backpackWeapons.Values;
    public IReadOnlyCollection<DRPGWeaponInstance> SettlementRecoveryWeapons =>
        settlementRecoveryWeapons.Values;
    public IReadOnlyCollection<DRPGRecoveredAccessoryItem> BackpackAccessories =>
        backpackAccessories.Values;
    public IReadOnlyCollection<DRPGRecoveredAccessoryItem>
        SettlementRecoveryAccessories => settlementRecoveryAccessories.Values;
    public IReadOnlyList<DRPGLootStackState> BackpackLootStacks
    {
        get
        {
            var aggregated = new Dictionary<string, DRPGLootStackState>(
                StringComparer.Ordinal);
            for (int i = 0; i < slots.Length; i++)
            {
                DRPGInventoryItemStack item = slots[i];
                if (item == null || item.Kind != DRPGInventoryItemKind.Loot ||
                    item.LootDefinition == null)
                {
                    continue;
                }

                string itemId = item.LootDefinition.ItemId;
                if (aggregated.TryGetValue(itemId, out DRPGLootStackState stack))
                    stack.Add(item.Quantity);
                else
                    aggregated.Add(itemId, new DRPGLootStackState(
                        item.LootDefinition,
                        item.Quantity));
            }

            var result = new List<DRPGLootStackState>(aggregated.Values);
            result.Sort((left, right) => string.CompareOrdinal(
                left.Definition.ItemId,
                right.Definition.ItemId));
            return result.AsReadOnly();
        }
    }
    internal IReadOnlyDictionary<string, int> WeaponFragmentCounts =>
        new Dictionary<string, int>(weaponFragmentCounts, StringComparer.Ordinal);

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
        return item != null && slotIndex >= 0 && slotIndex < slots.Length;
    }

    public int GetWeaponFragmentCount(string weaponDefinitionId)
    {
        return !string.IsNullOrWhiteSpace(weaponDefinitionId) &&
            weaponFragmentCounts.TryGetValue(weaponDefinitionId, out int count)
            ? count
            : 0;
    }

    public bool ContainsWeapon(string instanceId)
    {
        return !string.IsNullOrWhiteSpace(instanceId) &&
            backpackWeapons.ContainsKey(instanceId);
    }

    public DRPGWeaponInstance FindWeapon(string instanceId)
    {
        return !string.IsNullOrWhiteSpace(instanceId) &&
            backpackWeapons.TryGetValue(
                instanceId,
                out DRPGWeaponInstance weapon)
            ? weapon
            : null;
    }

    public DRPGEquipmentOperationResult TryAddWeapon(DRPGWeaponInstance weapon)
    {
        if (weapon == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Weapon is missing.");
        }
        if (ContainsWeapon(weapon.InstanceId) ||
            IsReservedAsEquipped(weapon.InstanceId) ||
            ContainsSettlementRecoveryWeapon(weapon.InstanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponAlreadyOwned,
                "Backpack already owns weapon instance: " + weapon.InstanceId);
        }

        int slotIndex = FindFirstEmptySlot();
        DRPGEquipmentOperationResult capacity = CheckCapacity(
            slotIndex >= 0 ? UsedSlots + 1 : SlotCapacity + 1,
            CurrentWeight + weapon.Definition.Weight);
        if (!capacity.Success)
            return capacity;

        slots[slotIndex] = DRPGInventoryItemStack.FromWeapon(weapon);
        backpackWeapons.Add(weapon.InstanceId, weapon);
        CurrentWeight += weapon.Definition.Weight;
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    public bool ContainsSettlementRecoveryWeapon(string instanceId)
    {
        return !string.IsNullOrWhiteSpace(instanceId) &&
            settlementRecoveryWeapons.ContainsKey(instanceId);
    }

    public bool IsReservedAsEquipped(string instanceId)
    {
        return !string.IsNullOrWhiteSpace(instanceId) &&
            equippedWeaponInstanceIds.Contains(instanceId);
    }

    internal DRPGEquipmentOperationResult TryReserveEquippedWeapon(
        DRPGWeaponInstance weapon)
    {
        if (weapon == null)
            return DRPGEquipmentOperationResult.Ok();
        if (ContainsWeapon(weapon.InstanceId) ||
            ContainsSettlementRecoveryWeapon(weapon.InstanceId) ||
            IsReservedAsEquipped(weapon.InstanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidInstanceState,
                "Weapon instance already belongs to another ownership location.");
        }
        equippedWeaponInstanceIds.Add(weapon.InstanceId);
        MutationEpoch = unchecked(MutationEpoch + 1);
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TryAddSettlementRecoveryWeapon(
        DRPGWeaponInstance weapon)
    {
        if (weapon == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Recovery weapon is missing.");
        }
        if (ContainsWeapon(weapon.InstanceId) ||
            IsReservedAsEquipped(weapon.InstanceId) ||
            ContainsSettlementRecoveryWeapon(weapon.InstanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponAlreadyOwned,
                "Weapon instance already belongs to an inventory collection.");
        }

        settlementRecoveryWeapons.Add(weapon.InstanceId, weapon);
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TryAddAccessory(
        DRPGRecoveredAccessoryItem accessory)
    {
        if (accessory == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Accessory is missing.");
        }
        if (backpackAccessories.ContainsKey(accessory.InstanceId) ||
            settlementRecoveryAccessories.ContainsKey(accessory.InstanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponAlreadyOwned,
                "Accessory instance is already owned: " + accessory.InstanceId);
        }

        int slotIndex = FindFirstEmptySlot();
        DRPGEquipmentOperationResult capacity = CheckCapacity(
            slotIndex >= 0 ? UsedSlots + 1 : SlotCapacity + 1,
            CurrentWeight + accessory.Weight);
        if (!capacity.Success)
            return capacity;

        slots[slotIndex] = DRPGInventoryItemStack.FromAccessory(accessory);
        backpackAccessories.Add(accessory.InstanceId, accessory);
        CurrentWeight += accessory.Weight;
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    public bool ContainsAccessory(string instanceId)
    {
        return !string.IsNullOrWhiteSpace(instanceId) &&
            backpackAccessories.ContainsKey(instanceId);
    }

    public int GetLootQuantity(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        int result = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            DRPGInventoryItemStack item = slots[i];
            if (item != null && item.Kind == DRPGInventoryItemKind.Loot &&
                item.LootDefinition != null && string.Equals(
                    item.LootDefinition.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                result += item.Quantity;
            }
        }
        return result;
    }

    /// <summary>
    /// Adds as much stackable loot as real free stack space and weight allow.
    /// Existing partial stacks are filled before new slots are occupied.
    /// </summary>
    public int TryAddLoot(
        DRPGLootItemDefinitionSO definition,
        int requestedQuantity)
    {
        if (definition == null || requestedQuantity <= 0)
            return 0;

        int availableBySlots = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            DRPGInventoryItemStack item = slots[i];
            if (item == null)
            {
                availableBySlots += definition.MaxStack;
            }
            else if (item.Kind == DRPGInventoryItemKind.Loot &&
                     item.LootDefinition != null && string.Equals(
                         item.LootDefinition.ItemId,
                         definition.ItemId,
                         StringComparison.Ordinal))
            {
                if (!ReferenceEquals(item.LootDefinition, definition))
                    return 0;
                availableBySlots += item.MaxStack - item.Quantity;
            }
        }

        int availableByWeight = requestedQuantity;
        if (definition.Weight > WeightEpsilon)
        {
            availableByWeight = Math.Max(0, (int)Math.Floor(
                (WeightCapacity - CurrentWeight + WeightEpsilon) /
                definition.Weight));
        }
        int accepted = Math.Min(
            requestedQuantity,
            Math.Min(availableBySlots, availableByWeight));
        if (accepted <= 0)
            return 0;

        int remaining = accepted;
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            DRPGInventoryItemStack item = slots[i];
            if (item == null || item.Kind != DRPGInventoryItemKind.Loot ||
                item.LootDefinition == null || !string.Equals(
                    item.LootDefinition.ItemId,
                    definition.ItemId,
                    StringComparison.Ordinal) ||
                item.Quantity >= item.MaxStack)
            {
                continue;
            }

            int amount = Math.Min(remaining, item.MaxStack - item.Quantity);
            slots[i] = item.WithQuantity(item.Quantity + amount);
            remaining -= amount;
        }
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i] != null)
                continue;
            int amount = Math.Min(remaining, definition.MaxStack);
            slots[i] = DRPGInventoryItemStack.FromLoot(definition, amount);
            remaining -= amount;
        }

        CurrentWeight += accepted * definition.Weight;
        Revision++;
        return accepted;
    }

    internal DRPGEquipmentOperationResult TryAddSettlementRecoveryContents(
        DRPGWeaponInstance weapon,
        IReadOnlyList<DRPGRecoveredAccessoryItem> accessories)
    {
        if (weapon != null &&
            (ContainsWeapon(weapon.InstanceId) ||
             IsReservedAsEquipped(weapon.InstanceId) ||
             ContainsSettlementRecoveryWeapon(weapon.InstanceId)))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponAlreadyOwned,
                "Recovery weapon is already owned: " + weapon.InstanceId);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (accessories != null)
        {
            for (int i = 0; i < accessories.Count; i++)
            {
                DRPGRecoveredAccessoryItem accessory = accessories[i];
                if (accessory == null ||
                    !ids.Add(accessory.InstanceId) ||
                    backpackAccessories.ContainsKey(accessory.InstanceId) ||
                    settlementRecoveryAccessories.ContainsKey(accessory.InstanceId))
                {
                    return DRPGEquipmentOperationResult.Fail(
                        DRPGEquipmentError.WeaponAlreadyOwned,
                        "Recovery accessory is missing, duplicated, or already owned.");
                }
            }
        }

        if (weapon != null)
            settlementRecoveryWeapons.Add(weapon.InstanceId, weapon);
        if (accessories != null)
        {
            for (int i = 0; i < accessories.Count; i++)
            {
                DRPGRecoveredAccessoryItem accessory = accessories[i];
                settlementRecoveryAccessories.Add(
                    accessory.InstanceId,
                    accessory);
            }
        }
        if (weapon != null || accessories != null && accessories.Count > 0)
            Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TryRecoverSettlementWeaponToBackpack(
        string instanceId)
    {
        if (!settlementRecoveryWeapons.TryGetValue(
                instanceId ?? string.Empty,
                out DRPGWeaponInstance weapon))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponNotFound,
                "Weapon instance is not in settlement recovery.");
        }

        int slotIndex = FindFirstEmptySlot();
        DRPGEquipmentOperationResult capacity = CheckCapacity(
            slotIndex >= 0 ? UsedSlots + 1 : SlotCapacity + 1,
            CurrentWeight + weapon.Definition.Weight);
        if (!capacity.Success)
            return capacity;

        settlementRecoveryWeapons.Remove(instanceId);
        slots[slotIndex] = DRPGInventoryItemStack.FromWeapon(weapon);
        backpackWeapons.Add(instanceId, weapon);
        CurrentWeight += weapon.Definition.Weight;
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGInventoryReplacementPreview PreviewWeaponReplacement(
        string candidateInstanceId,
        DRPGWeaponInstance currentlyEquipped)
    {
        if (!backpackWeapons.TryGetValue(
                candidateInstanceId ?? string.Empty,
                out DRPGWeaponInstance candidate))
        {
            return FailedPreview(
                DRPGEquipmentError.WeaponNotFound,
                "Candidate weapon is not in the backpack.",
                currentlyEquipped,
                UsedSlots,
                CurrentWeight);
        }
        if (currentlyEquipped != null && string.Equals(
                currentlyEquipped.InstanceId,
                candidate.InstanceId,
                StringComparison.Ordinal))
        {
            return FailedPreview(
                DRPGEquipmentError.StateConflict,
                "The candidate is already equipped.",
                currentlyEquipped,
                UsedSlots,
                CurrentWeight);
        }
        if (currentlyEquipped != null &&
            ContainsWeapon(currentlyEquipped.InstanceId))
        {
            return FailedPreview(
                DRPGEquipmentError.WeaponAlreadyOwned,
                "Equipped weapon is duplicated in the backpack.",
                currentlyEquipped,
                UsedSlots,
                CurrentWeight);
        }

        int candidateSlot = FindWeaponSlot(candidate.InstanceId);
        if (candidateSlot < 0)
        {
            return FailedPreview(
                DRPGEquipmentError.StateConflict,
                "Candidate weapon has no matching backpack slot.",
                currentlyEquipped,
                UsedSlots,
                CurrentWeight,
                candidate);
        }

        int finalSlots = UsedSlots - 1 + (currentlyEquipped != null ? 1 : 0);
        float finalWeight = CurrentWeight - candidate.Definition.Weight +
            (currentlyEquipped != null
                ? currentlyEquipped.Definition.Weight
                : 0f);
        DRPGEquipmentOperationResult capacity = CheckCapacity(
            finalSlots,
            finalWeight);
        if (!capacity.Success)
        {
            return FailedPreview(
                capacity.Error,
                capacity.Message,
                currentlyEquipped,
                finalSlots,
                finalWeight,
                candidate);
        }

        return new DRPGInventoryReplacementPreview(
            true,
            DRPGEquipmentError.None,
            string.Empty,
            candidate,
            currentlyEquipped,
            currentlyEquipped != null &&
                IsReservedAsEquipped(currentlyEquipped.InstanceId),
            Revision,
            CurrentWeight,
            finalSlots,
            Math.Max(0f, finalWeight),
            candidateSlot,
            candidateSlot);
    }

    public DRPGInventoryReplacementPreview PreviewWeaponUnequip(
        DRPGWeaponInstance currentlyEquipped)
    {
        if (currentlyEquipped == null)
        {
            return FailedPreview(
                DRPGEquipmentError.NoWeaponEquipped,
                "No weapon is equipped.",
                null,
                UsedSlots,
                CurrentWeight);
        }
        if (ContainsWeapon(currentlyEquipped.InstanceId))
        {
            return FailedPreview(
                DRPGEquipmentError.WeaponAlreadyOwned,
                "Equipped weapon is duplicated in the backpack.",
                currentlyEquipped,
                UsedSlots,
                CurrentWeight);
        }

        int returningSlot = FindFirstEmptySlot();
        int finalSlots = UsedSlots + 1;
        float finalWeight = CurrentWeight + currentlyEquipped.Definition.Weight;
        DRPGEquipmentOperationResult capacity = CheckCapacity(
            returningSlot >= 0 ? finalSlots : SlotCapacity + 1,
            finalWeight);
        if (!capacity.Success)
        {
            return FailedPreview(
                capacity.Error,
                capacity.Message,
                currentlyEquipped,
                finalSlots,
                finalWeight);
        }

        return new DRPGInventoryReplacementPreview(
            true,
            DRPGEquipmentError.None,
            string.Empty,
            null,
            currentlyEquipped,
            IsReservedAsEquipped(currentlyEquipped.InstanceId),
            Revision,
            CurrentWeight,
            finalSlots,
            finalWeight,
            -1,
            returningSlot);
    }

    internal DRPGEquipmentOperationResult Commit(
        DRPGInventoryReplacementPreview preview)
    {
        if (preview == null || !preview.Success ||
            preview.SourceRevision != Revision)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.StateConflict,
                "Backpack changed after equipment validation.");
        }
        if (preview.Candidate != null &&
            (!backpackWeapons.TryGetValue(
                    preview.Candidate.InstanceId,
                    out DRPGWeaponInstance currentCandidate) ||
             !ReferenceEquals(currentCandidate, preview.Candidate) ||
             preview.CandidateSlotIndex < 0 ||
             preview.CandidateSlotIndex >= slots.Length ||
             slots[preview.CandidateSlotIndex] == null ||
             !ReferenceEquals(
                 slots[preview.CandidateSlotIndex].Weapon,
                 preview.Candidate)))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.StateConflict,
                "Candidate weapon changed after equipment validation.");
        }
        if (preview.ReturningWeapon != null &&
            ContainsWeapon(preview.ReturningWeapon.InstanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponAlreadyOwned,
                "Returning weapon is already in the backpack.");
        }
        if (preview.ReturningWeapon != null &&
            (preview.ReturningSlotIndex < 0 ||
             preview.ReturningSlotIndex >= slots.Length ||
             preview.Candidate == null &&
             slots[preview.ReturningSlotIndex] != null))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.StateConflict,
                "The reserved return slot changed after validation.");
        }

        if (preview.Candidate != null)
        {
            backpackWeapons.Remove(preview.Candidate.InstanceId);
            equippedWeaponInstanceIds.Add(preview.Candidate.InstanceId);
            slots[preview.CandidateSlotIndex] = null;
        }
        if (preview.ReturningWeapon != null)
        {
            equippedWeaponInstanceIds.Remove(preview.ReturningWeapon.InstanceId);
            backpackWeapons.Add(
                preview.ReturningWeapon.InstanceId,
                preview.ReturningWeapon);
            slots[preview.ReturningSlotIndex] =
                DRPGInventoryItemStack.FromWeapon(preview.ReturningWeapon);
        }

        CurrentWeight = preview.FinalWeight;
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    internal DRPGEquipmentOperationResult
        TryConvertReservedBrokenWeaponToFragments(
            DRPGWeaponInstance weapon,
            int fragmentCount)
    {
        if (weapon == null || fragmentCount <= 0 || !weapon.IsBroken ||
            weapon.Definition.BreakDisposition !=
                DRPGWeaponBreakDisposition.DestroyIntoFragments)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Only a broken ordinary weapon can become fragments.");
        }
        if (!equippedWeaponInstanceIds.Contains(weapon.InstanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidInstanceState,
                "Broken weapon is not reserved as equipped.");
        }

        equippedWeaponInstanceIds.Remove(weapon.InstanceId);
        weaponFragmentCounts.TryGetValue(
            weapon.Definition.WeaponId,
            out int current);
        weaponFragmentCounts[weapon.Definition.WeaponId] =
            checked(current + fragmentCount);
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    internal DRPGEquipmentOperationResult TryReleaseReservedEquippedWeapon(
        DRPGWeaponInstance weapon)
    {
        if (weapon == null)
            return DRPGEquipmentOperationResult.Ok();
        if (!equippedWeaponInstanceIds.Remove(weapon.InstanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidInstanceState,
                "Equipped weapon is not reserved by party inventory.");
        }
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    internal DRPGEquipmentOperationResult TryRestoreWeaponFragments(
        IReadOnlyList<DRPGWeaponFragmentSaveData> fragments)
    {
        var restored = new Dictionary<string, int>(StringComparer.Ordinal);
        if (fragments != null)
        {
            for (int i = 0; i < fragments.Count; i++)
            {
                DRPGWeaponFragmentSaveData entry = fragments[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.weaponDefinitionId) ||
                    entry.count < 1 ||
                    restored.ContainsKey(entry.weaponDefinitionId))
                {
                    return DRPGEquipmentOperationResult.Fail(
                        DRPGEquipmentError.InvalidSaveData,
                        "Weapon fragment save data is invalid or duplicated.");
                }
                restored.Add(entry.weaponDefinitionId, entry.count);
            }
        }

        weaponFragmentCounts.Clear();
        foreach (KeyValuePair<string, int> pair in restored)
            weaponFragmentCounts.Add(pair.Key, pair.Value);
        MutationEpoch = unchecked(MutationEpoch + 1);
        return DRPGEquipmentOperationResult.Ok();
    }

    internal void RollbackCommittedPreview(
        DRPGInventoryReplacementPreview preview)
    {
        if (preview == null || !preview.Success ||
            Revision != preview.SourceRevision + 1)
        {
            throw new InvalidOperationException(
                "Cannot roll back an inventory preview that is not the latest commit.");
        }

        if (preview.ReturningWeapon != null)
        {
            backpackWeapons.Remove(preview.ReturningWeapon.InstanceId);
            slots[preview.ReturningSlotIndex] = null;
            if (preview.ReturningWasReserved)
            {
                equippedWeaponInstanceIds.Add(
                    preview.ReturningWeapon.InstanceId);
            }
        }
        if (preview.Candidate != null)
        {
            equippedWeaponInstanceIds.Remove(preview.Candidate.InstanceId);
            backpackWeapons[preview.Candidate.InstanceId] = preview.Candidate;
            slots[preview.CandidateSlotIndex] =
                DRPGInventoryItemStack.FromWeapon(preview.Candidate);
        }
        CurrentWeight = preview.SourceWeight;
        Revision = preview.SourceRevision;
    }

    internal DRPGInventoryOperationResult TryReplaceAllSlots(
        IReadOnlyList<DRPGInventorySlotSnapshot> restoredSlots)
    {
        var restored = new DRPGInventoryItemStack[SlotCapacity];
        if (restoredSlots != null)
        {
            for (int i = 0; i < restoredSlots.Count; i++)
            {
                DRPGInventorySlotSnapshot entry = restoredSlots[i];
                if (entry.SlotIndex < 0 || entry.SlotIndex >= restored.Length ||
                    entry.Item == null || restored[entry.SlotIndex] != null)
                {
                    return DRPGInventoryOperationResult.Fail(
                        DRPGInventoryError.InvalidSlot,
                        "Restored slot layout is invalid or duplicated.");
                }
                restored[entry.SlotIndex] = entry.Item;
            }
        }

        DRPGInventoryContainerMemento memento =
            new DRPGInventoryContainerMemento(this, Revision, restored);
        DRPGInventoryOperationResult validation =
            ValidateTransferMemento(memento);
        if (!validation.Success)
            return validation;
        ApplyTransferMemento(memento);
        return DRPGInventoryOperationResult.Ok();
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
            memento.Items.Length != SlotCapacity)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.StateConflict,
                "Party backpack changed during validation.");
        }

        float weight = 0f;
        var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < memento.Items.Length; i++)
        {
            DRPGInventoryItemStack item = memento.Items[i];
            if (item == null)
                continue;
            if (!IsStructurallyValid(item))
            {
                return DRPGInventoryOperationResult.Fail(
                    DRPGInventoryError.InvalidRequest,
                    "Slot " + i + " contains an invalid item stack.");
            }
            if (item.Kind != DRPGInventoryItemKind.Loot &&
                !uniqueKeys.Add(item.StableKey))
            {
                return DRPGInventoryOperationResult.Fail(
                    DRPGInventoryError.DuplicateUniqueItem,
                    "A unique item appears in more than one slot.");
            }
            if (item.Kind == DRPGInventoryItemKind.Weapon &&
                (equippedWeaponInstanceIds.Contains(item.Weapon.InstanceId) ||
                 settlementRecoveryWeapons.ContainsKey(item.Weapon.InstanceId)))
            {
                return DRPGInventoryOperationResult.Fail(
                    DRPGInventoryError.StateConflict,
                    "A weapon cannot belong to two ownership locations.");
            }
            if (item.Kind == DRPGInventoryItemKind.Accessory &&
                settlementRecoveryAccessories.ContainsKey(
                    item.Accessory.InstanceId))
            {
                return DRPGInventoryOperationResult.Fail(
                    DRPGInventoryError.StateConflict,
                    "An accessory cannot belong to two ownership locations.");
            }
            weight += item.TotalWeight;
        }
        if (weight > WeightCapacity + WeightEpsilon)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.WeightCapacityExceeded,
                "Party backpack weight capacity would be exceeded.");
        }
        return DRPGInventoryOperationResult.Ok();
    }

    void IDRPGInventoryMutableContainer.ApplyTransferMemento(
        DRPGInventoryContainerMemento memento)
    {
        ApplyTransferMemento(memento);
    }

    private void ApplyTransferMemento(
        DRPGInventoryContainerMemento memento)
    {
        DRPGInventoryOperationResult validation =
            ValidateTransferMemento(memento);
        if (!validation.Success)
            throw new InvalidOperationException(validation.Message);

        slots = (DRPGInventoryItemStack[])memento.Items.Clone();
        RebuildBackpackIndexes();
        CurrentWeight = CalculateWeight(slots);
        Revision++;
    }

    void IDRPGInventoryMutableContainer.RestoreTransferMemento(
        DRPGInventoryContainerMemento memento)
    {
        if (memento == null || !ReferenceEquals(memento.Owner, this) ||
            memento.Items.Length != SlotCapacity)
        {
            throw new InvalidOperationException(
                "Cannot restore a foreign party backpack snapshot.");
        }

        slots = (DRPGInventoryItemStack[])memento.Items.Clone();
        RebuildBackpackIndexes();
        CurrentWeight = CalculateWeight(slots);
        Revision = memento.SourceRevision;
    }

    private DRPGEquipmentOperationResult CheckCapacity(int usedSlots, float weight)
    {
        if (usedSlots > SlotCapacity)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InventorySlotCapacityExceeded,
                "Backpack slot capacity would be exceeded.");
        }
        if (weight > WeightCapacity + WeightEpsilon)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InventoryWeightCapacityExceeded,
                "Backpack weight capacity would be exceeded.");
        }
        return DRPGEquipmentOperationResult.Ok();
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                return i;
        }
        return -1;
    }

    private int FindWeaponSlot(string instanceId)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            DRPGInventoryItemStack item = slots[i];
            if (item != null && item.Kind == DRPGInventoryItemKind.Weapon &&
                item.Weapon != null && string.Equals(
                    item.Weapon.InstanceId,
                    instanceId,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private void RebuildBackpackIndexes()
    {
        backpackWeapons.Clear();
        backpackAccessories.Clear();
        for (int i = 0; i < slots.Length; i++)
        {
            DRPGInventoryItemStack item = slots[i];
            if (item == null)
                continue;
            if (item.Kind == DRPGInventoryItemKind.Weapon)
                backpackWeapons.Add(item.Weapon.InstanceId, item.Weapon);
            else if (item.Kind == DRPGInventoryItemKind.Accessory)
                backpackAccessories.Add(item.Accessory.InstanceId, item.Accessory);
        }
    }

    private static bool IsStructurallyValid(DRPGInventoryItemStack item)
    {
        if (item == null || item.Quantity < 1 || item.Quantity > item.MaxStack)
            return false;
        switch (item.Kind)
        {
            case DRPGInventoryItemKind.Weapon:
                return item.Quantity == 1 && item.Weapon != null &&
                    item.Weapon.Definition != null;
            case DRPGInventoryItemKind.Accessory:
                return item.Quantity == 1 && item.Accessory != null &&
                    item.Accessory.Definition != null;
            case DRPGInventoryItemKind.Loot:
                return item.LootDefinition != null;
            default:
                return false;
        }
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

    private DRPGInventoryReplacementPreview FailedPreview(
        DRPGEquipmentError error,
        string message,
        DRPGWeaponInstance returningWeapon,
        int finalSlots,
        float finalWeight,
        DRPGWeaponInstance candidate = null)
    {
        return new DRPGInventoryReplacementPreview(
            false,
            error,
            message,
            candidate,
            returningWeapon,
            returningWeapon != null &&
                IsReservedAsEquipped(returningWeapon.InstanceId),
            Revision,
            CurrentWeight,
            finalSlots,
            finalWeight);
    }
}
