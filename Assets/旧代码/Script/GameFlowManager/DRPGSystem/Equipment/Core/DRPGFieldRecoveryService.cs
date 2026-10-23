using System;
using System.Collections.Generic;

public sealed class DRPGFieldRecoveryService
{
    private readonly DRPGEquipmentRoot equipmentRoot;
    private readonly Dictionary<string, DRPGFieldRecoveryBagState> bags =
        new Dictionary<string, DRPGFieldRecoveryBagState>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> processedDeathBagIds =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public DRPGFieldRecoveryService(DRPGEquipmentRoot equipmentRoot)
    {
        this.equipmentRoot = equipmentRoot ??
            throw new ArgumentNullException(nameof(equipmentRoot));
    }

    public IReadOnlyCollection<DRPGFieldRecoveryBagState> ActiveBags =>
        new List<DRPGFieldRecoveryBagState>(bags.Values).AsReadOnly();
    public event Action Changed;

    public DRPGEquipmentOperationResult TryHandlePermanentDeath(
        string deathEventId,
        string characterId,
        out DRPGFieldRecoveryBagState bag)
    {
        bag = null;
        if (string.IsNullOrWhiteSpace(deathEventId) ||
            string.IsNullOrWhiteSpace(characterId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Death event ID and character ID are required.");
        }
        if (processedDeathBagIds.TryGetValue(deathEventId, out string priorBagId))
        {
            if (!string.IsNullOrWhiteSpace(priorBagId))
                bags.TryGetValue(priorBagId, out bag);
            return DRPGEquipmentOperationResult.Ok();
        }
        if (!equipmentRoot.TryGetCharacter(
                characterId,
                out DRPGCombatantProfileSO profile,
                out DRPGCharacterEquipmentState equipment))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CharacterNotFound,
                "Character is not registered in the equipment root.");
        }

        DRPGPersonnelKind personnelKind = profile.ProgressionDefinition != null
            ? profile.ProgressionDefinition.PersonnelKind
            : DRPGPersonnelKind.Friend;
        if (personnelKind != DRPGPersonnelKind.Employee)
        {
            processedDeathBagIds.Add(deathEventId, null);
            return DRPGEquipmentOperationResult.Ok();
        }

        DRPGCharacterEquipmentState.Snapshot equipmentSnapshot =
            equipment.CaptureSnapshot();
        DRPGWeaponInstance weapon = equipment.EquippedWeapon;
        var recoveredAccessories = new List<DRPGRecoveredAccessoryItem>();
        for (int i = 0; i < equipment.Accessories.Count; i++)
        {
            DRPGAccessoryDefinitionSO accessory = equipment.Accessories[i];
            if (accessory == null)
                continue;
            recoveredAccessories.Add(new DRPGRecoveredAccessoryItem(
                deathEventId + ":accessory:" + i,
                accessory));
        }

        if (weapon != null)
        {
            DRPGEquipmentOperationResult cleared = equipment.TryReplaceWeapon(
                weapon,
                null,
                string.Empty);
            if (!cleared.Success)
                return cleared;
        }
        for (int i = 0; i < equipment.Accessories.Count; i++)
        {
            if (equipment.Accessories[i] != null)
                equipment.TrySetAccessory(i, null);
        }

        DRPGEquipmentOperationResult released =
            equipmentRoot.PartyInventory.TryReleaseReservedEquippedWeapon(weapon);
        if (!released.Success)
        {
            equipment.RestoreSnapshot(equipmentSnapshot);
            return released;
        }

        string bagId = "recovery:" + deathEventId;
        bag = new DRPGFieldRecoveryBagState(
            bagId,
            deathEventId,
            characterId,
            weapon,
            recoveredAccessories);
        bags.Add(bagId, bag);
        processedDeathBagIds.Add(deathEventId, bagId);
        Changed?.Invoke();
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TryCollectWeapon(string bagId)
    {
        DRPGFieldRecoveryBagState bag = FindBag(bagId);
        if (bag == null)
            return MissingBag();
        if (bag.Weapon == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.RecoveryItemNotFound,
                "Recovery bag has no weapon.");
        }

        DRPGEquipmentOperationResult added =
            equipmentRoot.PartyInventory.TryAddWeapon(bag.Weapon);
        if (added.Success)
        {
            bag.RemoveWeapon();
            Changed?.Invoke();
        }
        return added;
    }

    public DRPGEquipmentOperationResult TryCollectAccessory(
        string bagId,
        string accessoryInstanceId)
    {
        DRPGFieldRecoveryBagState bag = FindBag(bagId);
        if (bag == null)
            return MissingBag();
        DRPGRecoveredAccessoryItem accessory =
            bag.FindAccessory(accessoryInstanceId);
        if (accessory == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.RecoveryItemNotFound,
                "Recovery accessory was not found.");
        }

        DRPGEquipmentOperationResult added =
            equipmentRoot.PartyInventory.TryAddAccessory(accessory);
        if (added.Success)
        {
            bag.RemoveAccessory(accessory.InstanceId);
            Changed?.Invoke();
        }
        return added;
    }

    public DRPGEquipmentOperationResult ResolveExtraction(bool succeeded)
    {
        var active = new List<DRPGFieldRecoveryBagState>(bags.Values);
        for (int i = 0; i < active.Count; i++)
        {
            DRPGFieldRecoveryBagState bag = active[i];
            DRPGWeaponInstance recoveryWeapon = succeeded ||
                bag.Weapon != null &&
                bag.Weapon.Definition.OwnershipKind ==
                    DRPGWeaponOwnershipKind.Signature
                ? bag.Weapon
                : null;
            IReadOnlyList<DRPGRecoveredAccessoryItem> recoveryAccessories =
                succeeded
                    ? bag.Accessories
                    : Array.Empty<DRPGRecoveredAccessoryItem>();
            DRPGEquipmentOperationResult recovered =
                equipmentRoot.PartyInventory.TryAddSettlementRecoveryContents(
                    recoveryWeapon,
                    recoveryAccessories);
            if (!recovered.Success)
                return recovered;
        }

        for (int i = 0; i < active.Count; i++)
        {
            DRPGFieldRecoveryBagState bag = active[i];
            bag.MarkResolved();
            bags.Remove(bag.BagId);
        }
        Changed?.Invoke();
        return DRPGEquipmentOperationResult.Ok();
    }

    internal List<DRPGRecoveryBagSaveData> ExportBagSnapshots()
    {
        var result = new List<DRPGRecoveryBagSaveData>();
        var ordered = new List<DRPGFieldRecoveryBagState>(bags.Values);
        ordered.Sort((left, right) => string.CompareOrdinal(
            left.BagId,
            right.BagId));
        for (int i = 0; i < ordered.Count; i++)
        {
            DRPGFieldRecoveryBagState bag = ordered[i];
            var saved = new DRPGRecoveryBagSaveData
            {
                bagId = bag.BagId,
                deathEventId = bag.DeathEventId,
                characterId = bag.CharacterId,
                weaponInstanceId = bag.Weapon != null
                    ? bag.Weapon.InstanceId
                    : null
            };
            for (int j = 0; j < bag.Accessories.Count; j++)
            {
                DRPGRecoveredAccessoryItem accessory = bag.Accessories[j];
                saved.accessories.Add(new DRPGAccessoryItemSaveData
                {
                    instanceId = accessory.InstanceId,
                    definitionId = accessory.Definition.AccessoryId
                });
            }
            result.Add(saved);
        }
        return result;
    }

    internal List<DRPGProcessedDeathSaveData> ExportProcessedDeathSnapshots()
    {
        var result = new List<DRPGProcessedDeathSaveData>();
        var ids = new List<string>(processedDeathBagIds.Keys);
        ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            string deathEventId = ids[i];
            result.Add(new DRPGProcessedDeathSaveData
            {
                deathEventId = deathEventId,
                bagId = processedDeathBagIds[deathEventId]
            });
        }
        return result;
    }

    internal DRPGEquipmentOperationResult ImportSnapshots(
        IReadOnlyList<DRPGRecoveryBagSaveData> bagSnapshots,
        IReadOnlyList<DRPGProcessedDeathSaveData> processedSnapshots,
        IReadOnlyDictionary<string, DRPGWeaponInstance> weapons,
        DRPGEquipmentDefinitionRegistry registry,
        ISet<string> quarantinedWeaponIds)
    {
        if (bagSnapshots == null || processedSnapshots == null ||
            weapons == null || registry == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidSaveData,
                "Recovery save data is incomplete.");
        }

        var restoredBags =
            new Dictionary<string, DRPGFieldRecoveryBagState>(StringComparer.Ordinal);
        for (int i = 0; i < bagSnapshots.Count; i++)
        {
            DRPGRecoveryBagSaveData saved = bagSnapshots[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.bagId) ||
                string.IsNullOrWhiteSpace(saved.deathEventId) ||
                string.IsNullOrWhiteSpace(saved.characterId) ||
                saved.accessories == null ||
                restoredBags.ContainsKey(saved.bagId))
            {
                return InvalidRecoverySave();
            }
            DRPGWeaponInstance weapon = null;
            if (!string.IsNullOrWhiteSpace(saved.weaponInstanceId) &&
                !quarantinedWeaponIds.Contains(saved.weaponInstanceId) &&
                !weapons.TryGetValue(saved.weaponInstanceId, out weapon))
            {
                return InvalidRecoverySave();
            }
            var accessories = new List<DRPGRecoveredAccessoryItem>();
            var accessoryIds = new HashSet<string>(StringComparer.Ordinal);
            for (int j = 0; j < saved.accessories.Count; j++)
            {
                DRPGAccessoryItemSaveData item = saved.accessories[j];
                if (item == null || string.IsNullOrWhiteSpace(item.instanceId) ||
                    !accessoryIds.Add(item.instanceId) ||
                    !registry.TryGetAccessory(
                        item.definitionId,
                        out DRPGAccessoryDefinitionSO definition))
                {
                    return InvalidRecoverySave();
                }
                accessories.Add(new DRPGRecoveredAccessoryItem(
                    item.instanceId,
                    definition));
            }
            restoredBags.Add(
                saved.bagId,
                new DRPGFieldRecoveryBagState(
                    saved.bagId,
                    saved.deathEventId,
                    saved.characterId,
                    weapon,
                    accessories));
        }

        var restoredProcessed = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < processedSnapshots.Count; i++)
        {
            DRPGProcessedDeathSaveData saved = processedSnapshots[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.deathEventId) ||
                restoredProcessed.ContainsKey(saved.deathEventId))
            {
                return InvalidRecoverySave();
            }
            if (!string.IsNullOrWhiteSpace(saved.bagId) &&
                !restoredBags.ContainsKey(saved.bagId))
            {
                // A processed death may refer to a bag already resolved before save.
                restoredProcessed.Add(saved.deathEventId, saved.bagId);
                continue;
            }
            restoredProcessed.Add(saved.deathEventId, saved.bagId);
        }

        bags.Clear();
        foreach (KeyValuePair<string, DRPGFieldRecoveryBagState> pair in
                 restoredBags)
        {
            bags.Add(pair.Key, pair.Value);
        }
        processedDeathBagIds.Clear();
        foreach (KeyValuePair<string, string> pair in restoredProcessed)
            processedDeathBagIds.Add(pair.Key, pair.Value);
        Changed?.Invoke();
        return DRPGEquipmentOperationResult.Ok();
    }

    internal DRPGFieldRecoveryService CloneFor(DRPGEquipmentRoot newRoot)
    {
        var clone = new DRPGFieldRecoveryService(newRoot);
        foreach (KeyValuePair<string, DRPGFieldRecoveryBagState> pair in bags)
        {
            DRPGFieldRecoveryBagState bag = pair.Value;
            clone.bags.Add(
                pair.Key,
                new DRPGFieldRecoveryBagState(
                    bag.BagId,
                    bag.DeathEventId,
                    bag.CharacterId,
                    bag.Weapon,
                    bag.Accessories));
        }
        foreach (KeyValuePair<string, string> pair in processedDeathBagIds)
            clone.processedDeathBagIds.Add(pair.Key, pair.Value);
        return clone;
    }

    private DRPGFieldRecoveryBagState FindBag(string bagId)
    {
        return !string.IsNullOrWhiteSpace(bagId) &&
            bags.TryGetValue(bagId, out DRPGFieldRecoveryBagState bag)
            ? bag
            : null;
    }

    private static DRPGEquipmentOperationResult MissingBag()
    {
        return DRPGEquipmentOperationResult.Fail(
            DRPGEquipmentError.RecoveryBagNotFound,
            "Recovery bag was not found.");
    }

    private static DRPGEquipmentOperationResult InvalidRecoverySave()
    {
        return DRPGEquipmentOperationResult.Fail(
            DRPGEquipmentError.InvalidSaveData,
            "Recovery bag save data is invalid or duplicated.");
    }
}
