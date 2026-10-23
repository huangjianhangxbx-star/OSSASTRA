using System;
using System.Collections.Generic;

internal static class DRPGEquipmentSaveCodec
{
    private const int LegacyVersion = 1;
    private const int CurrentVersion = 2;
    private const string Backpack = "backpack";
    private const string Settlement = "settlement";
    private const string Character = "character";
    private const string RecoveryBag = "recoveryBag";

    private sealed class OwnershipLocation
    {
        public OwnershipLocation(string kind, string ownerId)
        {
            Kind = kind;
            OwnerId = ownerId;
        }

        public string Kind { get; }
        public string OwnerId { get; }
    }

    public static DRPGEquipmentSaveData Export(DRPGEquipmentRoot root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        var save = new DRPGEquipmentSaveData { version = CurrentVersion };
        var weaponSaves = new Dictionary<string, DRPGWeaponInstanceSaveData>(
            StringComparer.Ordinal);
        var ownership = new HashSet<string>(StringComparer.Ordinal);

        var backpackWeapons = new List<DRPGWeaponInstance>(
            root.PartyInventory.BackpackWeapons);
        backpackWeapons.Sort(CompareWeapons);
        for (int i = 0; i < backpackWeapons.Count; i++)
        {
            DRPGWeaponInstance weapon = backpackWeapons[i];
            AddWeapon(weaponSaves, ownership, weapon, Backpack);
            save.backpackWeaponInstanceIds.Add(weapon.InstanceId);
        }

        var settlementWeapons = new List<DRPGWeaponInstance>(
            root.PartyInventory.SettlementRecoveryWeapons);
        settlementWeapons.Sort(CompareWeapons);
        for (int i = 0; i < settlementWeapons.Count; i++)
        {
            DRPGWeaponInstance weapon = settlementWeapons[i];
            AddWeapon(weaponSaves, ownership, weapon, Settlement);
            save.settlementRecoveryWeaponInstanceIds.Add(weapon.InstanceId);
        }

        save.backpackAccessories = ExportAccessories(
            root.PartyInventory.BackpackAccessories);
        save.settlementRecoveryAccessories = ExportAccessories(
            root.PartyInventory.SettlementRecoveryAccessories);
        ExportBackpackSlots(root.PartyInventory, save.backpackSlots);

        var characterIds = new List<string>(root.States.Keys);
        characterIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < characterIds.Count; i++)
        {
            string characterId = characterIds[i];
            DRPGCharacterEquipmentState state = root.States[characterId];
            var character = new DRPGCharacterEquipmentSaveData
            {
                characterId = characterId,
                equippedWeaponInstanceId = state.EquippedWeapon != null
                    ? state.EquippedWeapon.InstanceId
                    : null,
                outfitDefinitionId = state.CurrentOutfit != null
                    ? state.CurrentOutfit.OutfitId
                    : null,
                traitPool = state.TraitPool.ExportSnapshot()
            };
            if (state.EquippedWeapon != null)
            {
                AddWeapon(
                    weaponSaves,
                    ownership,
                    state.EquippedWeapon,
                    Character + ":" + characterId);
            }
            for (int slot = 0;
                 slot < DRPGCharacterEquipmentState.AccessorySlotCount;
                 slot++)
            {
                DRPGAccessoryDefinitionSO accessory = state.Accessories[slot];
                character.accessoryDefinitionIds.Add(
                    accessory != null ? accessory.AccessoryId : null);
            }
            var memories = new List<KeyValuePair<string, string>>(
                state.ExportLastWeaponMemory());
            memories.Sort((left, right) => string.CompareOrdinal(
                left.Key,
                right.Key));
            for (int j = 0; j < memories.Count; j++)
            {
                character.lastWeaponByProfession.Add(
                    new DRPGProfessionWeaponMemorySaveData
                    {
                        professionId = memories[j].Key,
                        weaponInstanceId = memories[j].Value
                    });
            }
            save.characters.Add(character);
        }

        save.recoveryBags = root.FieldRecovery.ExportBagSnapshots();
        save.processedDeaths =
            root.FieldRecovery.ExportProcessedDeathSnapshots();
        var activeBags = new Dictionary<string, DRPGFieldRecoveryBagState>(
            StringComparer.Ordinal);
        foreach (DRPGFieldRecoveryBagState bag in root.FieldRecovery.ActiveBags)
            activeBags.Add(bag.BagId, bag);
        for (int i = 0; i < save.recoveryBags.Count; i++)
        {
            DRPGRecoveryBagSaveData bagSave = save.recoveryBags[i];
            DRPGFieldRecoveryBagState bag = activeBags[bagSave.bagId];
            if (bag.Weapon != null)
            {
                AddWeapon(
                    weaponSaves,
                    ownership,
                    bag.Weapon,
                    RecoveryBag + ":" + bag.BagId);
            }
        }

        foreach (KeyValuePair<string, int> pair in
                 root.PartyInventory.WeaponFragmentCounts)
        {
            save.weaponFragments.Add(new DRPGWeaponFragmentSaveData
            {
                weaponDefinitionId = pair.Key,
                count = pair.Value
            });
        }
        save.weaponFragments.Sort((left, right) => string.CompareOrdinal(
            left.weaponDefinitionId,
            right.weaponDefinitionId));

        for (int i = 0; i < root.QuarantinedWeaponEntries.Count; i++)
        {
            DRPGQuarantinedWeaponEntry entry =
                root.QuarantinedWeaponEntries[i];
            if (!weaponSaves.ContainsKey(entry.SaveData.instanceId))
            {
                weaponSaves.Add(
                    entry.SaveData.instanceId,
                    CloneWeaponSave(entry.SaveData));
                ApplyQuarantinedLocation(save, entry);
            }
        }

        var weaponIds = new List<string>(weaponSaves.Keys);
        weaponIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < weaponIds.Count; i++)
            save.weaponInstances.Add(weaponSaves[weaponIds[i]]);
        return save;
    }

    public static DRPGEquipmentOperationResult Import(
        DRPGEquipmentRoot root,
        DRPGEquipmentSaveData save)
    {
        if (root == null || save == null ||
            save.version < LegacyVersion || save.version > CurrentVersion ||
            root.DefinitionRegistry == null || !HasRequiredLists(save))
        {
            return Invalid("Equipment save version or registry is invalid.");
        }

        var savedWeapons =
            new Dictionary<string, DRPGWeaponInstanceSaveData>(StringComparer.Ordinal);
        for (int i = 0; i < save.weaponInstances.Count; i++)
        {
            DRPGWeaponInstanceSaveData weapon = save.weaponInstances[i];
            if (weapon == null || string.IsNullOrWhiteSpace(weapon.instanceId) ||
                string.IsNullOrWhiteSpace(weapon.definitionId) ||
                savedWeapons.ContainsKey(weapon.instanceId))
            {
                return Invalid("Weapon instances are invalid or duplicated.");
            }
            savedWeapons.Add(weapon.instanceId, weapon);
        }

        DRPGEquipmentOperationResult locationsResult = BuildOwnershipLocations(
            save,
            savedWeapons,
            out Dictionary<string, OwnershipLocation> locations);
        if (!locationsResult.Success)
            return locationsResult;

        var restoredWeapons =
            new Dictionary<string, DRPGWeaponInstance>(StringComparer.Ordinal);
        var quarantinedIds = new HashSet<string>(StringComparer.Ordinal);
        var quarantined = new List<DRPGQuarantinedWeaponEntry>();
        var warnings = new List<string>();
        foreach (KeyValuePair<string, DRPGWeaponInstanceSaveData> pair in
                 savedWeapons)
        {
            DRPGWeaponInstanceSaveData saved = pair.Value;
            if (!root.DefinitionRegistry.TryGetWeapon(
                    saved.definitionId,
                    out DRPGWeaponDefinitionSO definition))
            {
                quarantinedIds.Add(saved.instanceId);
                OwnershipLocation location = locations[saved.instanceId];
                quarantined.Add(new DRPGQuarantinedWeaponEntry(
                    CloneWeaponSave(saved),
                    location.Kind,
                    location.OwnerId));
                warnings.Add(
                    "Weapon instance '" + saved.instanceId +
                    "' was quarantined because definition '" +
                    saved.definitionId + "' is missing.");
                continue;
            }

            DRPGEquipmentOperationResult references =
                ValidateWeaponReferences(root.DefinitionRegistry, definition, saved);
            if (!references.Success)
                return references;
            try
            {
                restoredWeapons.Add(
                    saved.instanceId,
                    DRPGWeaponInstance.Restore(saved, definition));
            }
            catch (Exception exception)
            {
                return Invalid("Weapon restore failed: " + exception.Message);
            }
        }

        DRPGEquipmentOperationResult accessoryOwnership =
            ValidateAccessoryItemOwnership(save, root.DefinitionRegistry);
        if (!accessoryOwnership.Success)
            return accessoryOwnership;

        var importedInventory = new DRPGPartyInventoryState(
            root.PartyInventory.SlotCapacity,
            root.PartyInventory.WeightCapacity);
        DRPGEquipmentOperationResult backpack = RestoreBackpack(
            importedInventory,
            root.DefinitionRegistry,
            save,
            restoredWeapons,
            quarantinedIds);
        if (!backpack.Success)
            return backpack;

        var settlementWeapons = new List<DRPGWeaponInstance>();
        for (int i = 0;
             i < save.settlementRecoveryWeaponInstanceIds.Count;
             i++)
        {
            string id = save.settlementRecoveryWeaponInstanceIds[i];
            if (!quarantinedIds.Contains(id))
                settlementWeapons.Add(restoredWeapons[id]);
        }
        var settlementAccessories = new List<DRPGRecoveredAccessoryItem>();
        for (int i = 0; i < save.settlementRecoveryAccessories.Count; i++)
        {
            DRPGAccessoryItemSaveData saved =
                save.settlementRecoveryAccessories[i];
            root.DefinitionRegistry.TryGetAccessory(
                saved.definitionId,
                out DRPGAccessoryDefinitionSO definition);
            settlementAccessories.Add(new DRPGRecoveredAccessoryItem(
                saved.instanceId,
                definition));
        }
        for (int i = 0; i < settlementWeapons.Count; i++)
        {
            DRPGEquipmentOperationResult added =
                importedInventory.TryAddSettlementRecoveryContents(
                    settlementWeapons[i],
                    i == 0
                        ? settlementAccessories
                        : Array.Empty<DRPGRecoveredAccessoryItem>());
            if (!added.Success)
                return added;
        }
        if (settlementWeapons.Count == 0 && settlementAccessories.Count > 0)
        {
            DRPGEquipmentOperationResult added =
                importedInventory.TryAddSettlementRecoveryContents(
                    null,
                    settlementAccessories);
            if (!added.Success)
                return added;
        }
        DRPGEquipmentOperationResult fragments =
            importedInventory.TryRestoreWeaponFragments(save.weaponFragments);
        if (!fragments.Success)
            return fragments;

        var importedRoot = new DRPGEquipmentRoot(
            importedInventory,
            root.DefinitionRegistry);
        DRPGEquipmentOperationResult characters = RestoreCharacters(
            root,
            importedRoot,
            save.characters,
            restoredWeapons,
            quarantinedIds);
        if (!characters.Success)
            return characters;

        DRPGEquipmentOperationResult recovery =
            importedRoot.FieldRecovery.ImportSnapshots(
                save.recoveryBags,
                save.processedDeaths,
                restoredWeapons,
                root.DefinitionRegistry,
                quarantinedIds);
        if (!recovery.Success)
            return recovery;

        root.ReplaceImportedState(importedRoot, quarantined, warnings);
        return DRPGEquipmentOperationResult.Ok();
    }

    private static void ExportBackpackSlots(
        DRPGPartyInventoryState inventory,
        ICollection<DRPGInventorySlotSaveData> destination)
    {
        for (int slotIndex = 0;
             slotIndex < inventory.SlotCapacity;
             slotIndex++)
        {
            DRPGInventoryItemStack item = inventory.GetSlot(slotIndex).Item;
            if (item == null)
                continue;

            var entry = new DRPGInventorySlotSaveData
            {
                slotIndex = slotIndex,
                itemKind = item.Kind,
                quantity = item.Quantity
            };
            switch (item.Kind)
            {
                case DRPGInventoryItemKind.Weapon:
                    entry.instanceId = item.Weapon.InstanceId;
                    entry.definitionId = item.Weapon.Definition.WeaponId;
                    break;
                case DRPGInventoryItemKind.Accessory:
                    entry.instanceId = item.Accessory.InstanceId;
                    entry.definitionId =
                        item.Accessory.Definition.AccessoryId;
                    break;
                case DRPGInventoryItemKind.Loot:
                    entry.definitionId = item.LootDefinition.ItemId;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Backpack contains an unsupported item kind.");
            }
            destination.Add(entry);
        }
    }

    private static DRPGEquipmentOperationResult RestoreBackpack(
        DRPGPartyInventoryState inventory,
        DRPGEquipmentDefinitionRegistry registry,
        DRPGEquipmentSaveData save,
        IReadOnlyDictionary<string, DRPGWeaponInstance> restoredWeapons,
        ISet<string> quarantinedWeaponIds)
    {
        if (save.version == LegacyVersion)
        {
            for (int i = 0; i < save.backpackWeaponInstanceIds.Count; i++)
            {
                string id = save.backpackWeaponInstanceIds[i];
                if (quarantinedWeaponIds.Contains(id))
                    continue;
                DRPGEquipmentOperationResult added =
                    inventory.TryAddWeapon(restoredWeapons[id]);
                if (!added.Success)
                    return added;
            }
            for (int i = 0; i < save.backpackAccessories.Count; i++)
            {
                DRPGAccessoryItemSaveData saved = save.backpackAccessories[i];
                registry.TryGetAccessory(
                    saved.definitionId,
                    out DRPGAccessoryDefinitionSO definition);
                DRPGEquipmentOperationResult added = inventory.TryAddAccessory(
                    new DRPGRecoveredAccessoryItem(
                        saved.instanceId,
                        definition));
                if (!added.Success)
                    return added;
            }
            return DRPGEquipmentOperationResult.Ok();
        }

        var expectedWeapons = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < save.backpackWeaponInstanceIds.Count; i++)
        {
            string id = save.backpackWeaponInstanceIds[i];
            if (!quarantinedWeaponIds.Contains(id))
                expectedWeapons.Add(id);
        }
        var expectedAccessories = new Dictionary<
            string,
            DRPGRecoveredAccessoryItem>(StringComparer.Ordinal);
        for (int i = 0; i < save.backpackAccessories.Count; i++)
        {
            DRPGAccessoryItemSaveData saved = save.backpackAccessories[i];
            registry.TryGetAccessory(
                saved.definitionId,
                out DRPGAccessoryDefinitionSO definition);
            expectedAccessories.Add(
                saved.instanceId,
                new DRPGRecoveredAccessoryItem(saved.instanceId, definition));
        }

        var seenSlots = new HashSet<int>();
        var seenWeapons = new HashSet<string>(StringComparer.Ordinal);
        var seenAccessories = new HashSet<string>(StringComparer.Ordinal);
        var restoredSlots = new List<DRPGInventorySlotSnapshot>();
        for (int i = 0; i < save.backpackSlots.Count; i++)
        {
            DRPGInventorySlotSaveData entry = save.backpackSlots[i];
            if (entry == null || entry.slotIndex < 0 ||
                entry.slotIndex >= inventory.SlotCapacity ||
                !seenSlots.Add(entry.slotIndex) || entry.quantity < 1)
            {
                return Invalid(
                    "Backpack slot save data is invalid or duplicated.");
            }

            DRPGInventoryItemStack item;
            switch (entry.itemKind)
            {
                case DRPGInventoryItemKind.Weapon:
                    if (entry.quantity != 1 ||
                        string.IsNullOrWhiteSpace(entry.instanceId) ||
                        !ContainsOrdinal(
                            save.backpackWeaponInstanceIds,
                            entry.instanceId))
                    {
                        return Invalid(
                            "Backpack weapon slot does not match ownership.");
                    }
                    if (quarantinedWeaponIds.Contains(entry.instanceId))
                        continue;
                    if (!seenWeapons.Add(entry.instanceId) ||
                        !restoredWeapons.TryGetValue(
                            entry.instanceId,
                            out DRPGWeaponInstance weapon) ||
                        !string.IsNullOrWhiteSpace(entry.definitionId) &&
                        !string.Equals(
                            entry.definitionId,
                            weapon.Definition.WeaponId,
                            StringComparison.Ordinal))
                    {
                        return Invalid(
                            "Backpack weapon slot is unknown or duplicated.");
                    }
                    item = DRPGInventoryItemStack.FromWeapon(weapon);
                    break;

                case DRPGInventoryItemKind.Accessory:
                    if (entry.quantity != 1 ||
                        string.IsNullOrWhiteSpace(entry.instanceId) ||
                        !seenAccessories.Add(entry.instanceId) ||
                        !expectedAccessories.TryGetValue(
                            entry.instanceId,
                            out DRPGRecoveredAccessoryItem accessory) ||
                        !string.IsNullOrWhiteSpace(entry.definitionId) &&
                        !string.Equals(
                            entry.definitionId,
                            accessory.Definition.AccessoryId,
                            StringComparison.Ordinal))
                    {
                        return Invalid(
                            "Backpack accessory slot is unknown or duplicated.");
                    }
                    item = DRPGInventoryItemStack.FromAccessory(accessory);
                    break;

                case DRPGInventoryItemKind.Loot:
                    if (string.IsNullOrWhiteSpace(entry.definitionId) ||
                        !registry.TryGetLootItem(
                            entry.definitionId,
                            out DRPGLootItemDefinitionSO loot) ||
                        entry.quantity > loot.MaxStack)
                    {
                        return Missing(
                            "Backpack loot definition is missing or stack is invalid.");
                    }
                    item = DRPGInventoryItemStack.FromLoot(
                        loot,
                        entry.quantity);
                    break;

                default:
                    return Invalid("Backpack slot item kind is invalid.");
            }
            restoredSlots.Add(new DRPGInventorySlotSnapshot(
                entry.slotIndex,
                item));
        }

        if (!expectedWeapons.SetEquals(seenWeapons) ||
            expectedAccessories.Count != seenAccessories.Count)
        {
            return Invalid(
                "Backpack slot layout is incomplete for owned equipment.");
        }

        DRPGInventoryOperationResult restored =
            inventory.TryReplaceAllSlots(restoredSlots);
        return restored.Success
            ? DRPGEquipmentOperationResult.Ok()
            : Invalid(restored.Message);
    }

    private static bool ContainsOrdinal(
        IReadOnlyList<string> source,
        string expected)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (string.Equals(source[i], expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static DRPGEquipmentOperationResult RestoreCharacters(
        DRPGEquipmentRoot sourceRoot,
        DRPGEquipmentRoot importedRoot,
        IReadOnlyList<DRPGCharacterEquipmentSaveData> savedCharacters,
        IReadOnlyDictionary<string, DRPGWeaponInstance> weapons,
        ISet<string> quarantinedWeaponIds)
    {
        if (savedCharacters.Count != sourceRoot.Profiles.Count)
            return Invalid("Character equipment save set is incomplete.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < savedCharacters.Count; i++)
        {
            DRPGCharacterEquipmentSaveData saved = savedCharacters[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.characterId) ||
                !seen.Add(saved.characterId) ||
                !sourceRoot.Profiles.TryGetValue(
                    saved.characterId,
                    out DRPGCombatantProfileSO profile) ||
                saved.accessoryDefinitionIds == null ||
                saved.accessoryDefinitionIds.Count !=
                    DRPGCharacterEquipmentState.AccessorySlotCount ||
                saved.lastWeaponByProfession == null || saved.traitPool == null)
            {
                return Invalid("Character equipment save data is invalid.");
            }

            DRPGWeaponInstance weapon = null;
            if (!string.IsNullOrWhiteSpace(saved.equippedWeaponInstanceId) &&
                !quarantinedWeaponIds.Contains(saved.equippedWeaponInstanceId) &&
                !weapons.TryGetValue(saved.equippedWeaponInstanceId, out weapon))
            {
                return Invalid("Equipped weapon instance is unknown.");
            }
            var state = new DRPGCharacterEquipmentState(saved.characterId, weapon);
            if (!string.IsNullOrWhiteSpace(saved.outfitDefinitionId))
            {
                if (!sourceRoot.DefinitionRegistry.TryGetOutfit(
                        saved.outfitDefinitionId,
                        out DRPGOutfitDefinitionSO outfit))
                {
                    return Missing("Outfit definition is missing: " +
                        saved.outfitDefinitionId);
                }
                DRPGEquipmentOperationResult setOutfit = state.TrySetOutfit(outfit);
                if (!setOutfit.Success)
                    return setOutfit;
            }
            for (int slot = 0; slot < saved.accessoryDefinitionIds.Count; slot++)
            {
                string accessoryId = saved.accessoryDefinitionIds[slot];
                if (string.IsNullOrWhiteSpace(accessoryId))
                    continue;
                if (!sourceRoot.DefinitionRegistry.TryGetAccessory(
                        accessoryId,
                        out DRPGAccessoryDefinitionSO accessory))
                {
                    return Missing("Accessory definition is missing: " + accessoryId);
                }
                DRPGEquipmentOperationResult setAccessory =
                    state.TrySetAccessory(slot, accessory);
                if (!setAccessory.Success)
                    return setAccessory;
            }
            DRPGEquipmentOperationResult traits = ValidateAndImportTraits(
                sourceRoot.DefinitionRegistry,
                state,
                saved.traitPool);
            if (!traits.Success)
                return traits;
            DRPGEquipmentOperationResult memory =
                state.ImportLastWeaponMemory(saved.lastWeaponByProfession);
            if (!memory.Success)
                return memory;
            DRPGEquipmentOperationResult registered =
                importedRoot.TryRegisterCharacter(profile, state);
            if (!registered.Success)
                return registered;
        }
        return DRPGEquipmentOperationResult.Ok();
    }

    private static DRPGEquipmentOperationResult ValidateAndImportTraits(
        DRPGEquipmentDefinitionRegistry registry,
        DRPGCharacterEquipmentState state,
        DRPGTraitPoolSnapshot snapshot)
    {
        if (snapshot.regularTraitIds == null || snapshot.sourceBoundTraits == null)
            return Invalid("Trait save data is incomplete.");
        for (int i = 0; i < snapshot.regularTraitIds.Count; i++)
        {
            if (!registry.ContainsTrait(snapshot.regularTraitIds[i]))
                return Missing("Trait definition is missing: " +
                    snapshot.regularTraitIds[i]);
        }
        for (int i = 0; i < snapshot.sourceBoundTraits.Count; i++)
        {
            DRPGSourceBoundTraitSaveData entry = snapshot.sourceBoundTraits[i];
            if (entry == null || !registry.ContainsTrait(entry.traitId))
                return Missing("A source-bound trait definition is missing.");
        }
        return state.TraitPool.ImportSnapshot(snapshot);
    }

    private static DRPGEquipmentOperationResult BuildOwnershipLocations(
        DRPGEquipmentSaveData save,
        IReadOnlyDictionary<string, DRPGWeaponInstanceSaveData> weapons,
        out Dictionary<string, OwnershipLocation> locations)
    {
        locations = new Dictionary<string, OwnershipLocation>(StringComparer.Ordinal);
        for (int i = 0; i < save.backpackWeaponInstanceIds.Count; i++)
        {
            DRPGEquipmentOperationResult result = AddLocation(
                locations,
                weapons,
                save.backpackWeaponInstanceIds[i],
                Backpack,
                null);
            if (!result.Success)
                return result;
        }
        for (int i = 0;
             i < save.settlementRecoveryWeaponInstanceIds.Count;
             i++)
        {
            DRPGEquipmentOperationResult result = AddLocation(
                locations,
                weapons,
                save.settlementRecoveryWeaponInstanceIds[i],
                Settlement,
                null);
            if (!result.Success)
                return result;
        }
        for (int i = 0; i < save.characters.Count; i++)
        {
            DRPGCharacterEquipmentSaveData character = save.characters[i];
            if (character == null)
                return Invalid("Character equipment entry is null.");
            if (!string.IsNullOrWhiteSpace(character.equippedWeaponInstanceId))
            {
                DRPGEquipmentOperationResult result = AddLocation(
                    locations,
                    weapons,
                    character.equippedWeaponInstanceId,
                    Character,
                    character.characterId);
                if (!result.Success)
                    return result;
            }
        }
        for (int i = 0; i < save.recoveryBags.Count; i++)
        {
            DRPGRecoveryBagSaveData bag = save.recoveryBags[i];
            if (bag == null)
                return Invalid("Recovery bag entry is null.");
            if (!string.IsNullOrWhiteSpace(bag.weaponInstanceId))
            {
                DRPGEquipmentOperationResult result = AddLocation(
                    locations,
                    weapons,
                    bag.weaponInstanceId,
                    RecoveryBag,
                    bag.bagId);
                if (!result.Success)
                    return result;
            }
        }
        if (locations.Count != weapons.Count)
            return Invalid("Every weapon instance must have exactly one owner.");
        return DRPGEquipmentOperationResult.Ok();
    }

    private static DRPGEquipmentOperationResult AddLocation(
        IDictionary<string, OwnershipLocation> locations,
        IReadOnlyDictionary<string, DRPGWeaponInstanceSaveData> weapons,
        string instanceId,
        string kind,
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            !weapons.ContainsKey(instanceId))
        {
            return Invalid("Weapon ownership references an unknown instance.");
        }
        if (locations.ContainsKey(instanceId))
            return Invalid("Weapon instance appears in multiple ownership locations.");
        locations.Add(instanceId, new OwnershipLocation(kind, ownerId));
        return DRPGEquipmentOperationResult.Ok();
    }

    private static DRPGEquipmentOperationResult ValidateWeaponReferences(
        DRPGEquipmentDefinitionRegistry registry,
        DRPGWeaponDefinitionSO definition,
        DRPGWeaponInstanceSaveData saved)
    {
        if (saved.affixIds == null || saved.cardIds == null ||
            saved.attachmentIds == null)
        {
            return Invalid("Weapon instance reference lists are incomplete.");
        }
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < saved.affixIds.Count; i++)
        {
            if (!unique.Add(saved.affixIds[i]) ||
                !registry.TryGetAffix(saved.affixIds[i], out _))
            {
                return Missing("Weapon affix definition is missing or duplicated.");
            }
        }
        if (saved.affixIds.Count > definition.RarityBudget.AffixSlots)
            return Invalid("Weapon affix count exceeds rarity budget.");

        unique.Clear();
        int cardCost = 0;
        for (int i = 0; i < saved.cardIds.Count; i++)
        {
            if (!unique.Add(saved.cardIds[i]) ||
                !registry.TryGetCard(
                    saved.cardIds[i],
                    out DRPGWeaponCardDefinitionSO card))
            {
                return Missing("Weapon card definition is missing or duplicated.");
            }
            cardCost += card.Cost;
        }
        if (saved.cardIds.Count > definition.RarityBudget.CardSlots ||
            cardCost > definition.RarityBudget.CardCostCapacity)
        {
            return Invalid("Weapon card state exceeds rarity budget.");
        }

        unique.Clear();
        for (int i = 0; i < saved.attachmentIds.Count; i++)
        {
            if (!unique.Add(saved.attachmentIds[i]) ||
                !registry.TryGetAttachment(saved.attachmentIds[i], out _))
            {
                return Missing(
                    "Weapon attachment definition is missing or duplicated.");
            }
        }
        return saved.attachmentIds.Count <= DRPGWeaponRarityRules.AttachmentSlots
            ? DRPGEquipmentOperationResult.Ok()
            : Invalid("Weapon attachment count exceeds fixed capacity.");
    }

    private static DRPGEquipmentOperationResult ValidateAccessoryItemOwnership(
        DRPGEquipmentSaveData save,
        DRPGEquipmentDefinitionRegistry registry)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        DRPGEquipmentOperationResult result = ValidateAccessoryList(
            save.backpackAccessories,
            registry,
            ids);
        if (!result.Success)
            return result;
        result = ValidateAccessoryList(
            save.settlementRecoveryAccessories,
            registry,
            ids);
        if (!result.Success)
            return result;
        for (int i = 0; i < save.recoveryBags.Count; i++)
        {
            result = ValidateAccessoryList(
                save.recoveryBags[i].accessories,
                registry,
                ids);
            if (!result.Success)
                return result;
        }
        return DRPGEquipmentOperationResult.Ok();
    }

    private static DRPGEquipmentOperationResult ValidateAccessoryList(
        IReadOnlyList<DRPGAccessoryItemSaveData> source,
        DRPGEquipmentDefinitionRegistry registry,
        ISet<string> ids)
    {
        if (source == null)
            return Invalid("Accessory item save list is missing.");
        for (int i = 0; i < source.Count; i++)
        {
            DRPGAccessoryItemSaveData item = source[i];
            if (item == null || string.IsNullOrWhiteSpace(item.instanceId) ||
                !ids.Add(item.instanceId) ||
                !registry.TryGetAccessory(item.definitionId, out _))
            {
                return Missing(
                    "Accessory item definition is missing or ownership is duplicated.");
            }
        }
        return DRPGEquipmentOperationResult.Ok();
    }

    private static List<DRPGAccessoryItemSaveData> ExportAccessories(
        IReadOnlyCollection<DRPGRecoveredAccessoryItem> source)
    {
        var result = new List<DRPGAccessoryItemSaveData>();
        foreach (DRPGRecoveredAccessoryItem item in source)
        {
            result.Add(new DRPGAccessoryItemSaveData
            {
                instanceId = item.InstanceId,
                definitionId = item.Definition.AccessoryId
            });
        }
        result.Sort((left, right) => string.CompareOrdinal(
            left.instanceId,
            right.instanceId));
        return result;
    }

    private static void AddWeapon(
        IDictionary<string, DRPGWeaponInstanceSaveData> saves,
        ISet<string> ownership,
        DRPGWeaponInstance weapon,
        string location)
    {
        if (!ownership.Add(weapon.InstanceId))
        {
            throw new InvalidOperationException(
                "Weapon instance has duplicate live ownership: " +
                weapon.InstanceId + " at " + location);
        }
        saves.Add(weapon.InstanceId, weapon.ExportSnapshot());
    }

    private static int CompareWeapons(
        DRPGWeaponInstance left,
        DRPGWeaponInstance right)
    {
        return string.CompareOrdinal(left.InstanceId, right.InstanceId);
    }

    private static bool HasRequiredLists(DRPGEquipmentSaveData save)
    {
        return save.weaponInstances != null &&
            save.backpackWeaponInstanceIds != null &&
            save.settlementRecoveryWeaponInstanceIds != null &&
            save.backpackAccessories != null &&
            save.settlementRecoveryAccessories != null &&
            save.characters != null && save.recoveryBags != null &&
            save.processedDeaths != null && save.weaponFragments != null &&
            (save.version == LegacyVersion || save.backpackSlots != null);
    }

    private static DRPGWeaponInstanceSaveData CloneWeaponSave(
        DRPGWeaponInstanceSaveData source)
    {
        return new DRPGWeaponInstanceSaveData
        {
            instanceId = source.instanceId,
            definitionId = source.definitionId,
            rolledMaxDurability = source.rolledMaxDurability,
            currentMaxDurability = source.currentMaxDurability,
            currentDurability = source.currentDurability,
            affixIds = new List<string>(source.affixIds ?? new List<string>()),
            cardIds = new List<string>(source.cardIds ?? new List<string>()),
            attachmentIds = new List<string>(
                source.attachmentIds ?? new List<string>())
        };
    }

    private static void ApplyQuarantinedLocation(
        DRPGEquipmentSaveData save,
        DRPGQuarantinedWeaponEntry entry)
    {
        if (entry.OwnershipKind == Backpack)
            save.backpackWeaponInstanceIds.Add(entry.SaveData.instanceId);
        else if (entry.OwnershipKind == Settlement)
            save.settlementRecoveryWeaponInstanceIds.Add(entry.SaveData.instanceId);
        else if (entry.OwnershipKind == Character)
        {
            DRPGCharacterEquipmentSaveData character = save.characters.Find(
                value => string.Equals(
                    value.characterId,
                    entry.OwnerId,
                    StringComparison.Ordinal));
            if (character != null)
                character.equippedWeaponInstanceId = entry.SaveData.instanceId;
        }
        else if (entry.OwnershipKind == RecoveryBag)
        {
            DRPGRecoveryBagSaveData bag = save.recoveryBags.Find(
                value => string.Equals(
                    value.bagId,
                    entry.OwnerId,
                    StringComparison.Ordinal));
            if (bag != null)
                bag.weaponInstanceId = entry.SaveData.instanceId;
        }
    }

    private static DRPGEquipmentOperationResult Invalid(string message)
    {
        return DRPGEquipmentOperationResult.Fail(
            DRPGEquipmentError.InvalidSaveData,
            message);
    }

    private static DRPGEquipmentOperationResult Missing(string message)
    {
        return DRPGEquipmentOperationResult.Fail(
            DRPGEquipmentError.MissingDefinition,
            message);
    }
}
