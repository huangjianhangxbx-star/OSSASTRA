#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGEquipmentSaveProbe
{
    private sealed class MinimumRandom : IDRPGRandomSource
    {
        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return minimumInclusive;
        }

        public int RangeInclusive(int minimumInclusive, int maximumInclusive)
        {
            return minimumInclusive;
        }
    }

    public static string Run()
    {
        Type registryType = FindType("DRPGEquipmentDefinitionRegistry");
        Type saveType = FindType("DRPGEquipmentSaveData");
        MethodInfo exportMethod = typeof(DRPGEquipmentRoot).GetMethod(
            "ExportSnapshot",
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo importMethod = typeof(DRPGEquipmentRoot).GetMethod(
            "ImportSnapshot",
            BindingFlags.Instance | BindingFlags.Public);
        if (registryType == null || saveType == null ||
            exportMethod == null || importMethod == null)
        {
            throw new Exception(
                "Equipment save registry, data, export, or import contract is missing.");
        }

        var created = new List<UnityEngine.Object>();
        try
        {
            DRPGProfessionSO profession = CreateProfession(created);
            DRPGCombatantProfileSO friend = CreateProfile(
                "save_friend",
                DRPGPersonnelKind.Friend,
                profession,
                created);
            DRPGCombatantProfileSO employee = CreateProfile(
                "save_employee",
                DRPGPersonnelKind.Employee,
                profession,
                created);
            DRPGWeaponDefinitionSO backpackDefinition = CreateWeaponDefinition(
                "save_backpack_weapon",
                profession,
                2f,
                created);
            DRPGWeaponDefinitionSO equippedDefinition = CreateWeaponDefinition(
                "save_equipped_weapon",
                profession,
                3f,
                created);
            DRPGWeaponDefinitionSO recoveryDefinition = CreateWeaponDefinition(
                "save_recovery_weapon",
                profession,
                4f,
                created);
            DRPGTraitDefinitionSO regularTrait = CreateTrait(
                "save_regular_trait",
                created);
            DRPGTraitDefinitionSO outfitTrait = CreateTrait(
                "save_outfit_trait",
                created);
            DRPGOutfitDefinitionSO outfit = CreateOutfit(
                "save_outfit",
                outfitTrait,
                created);
            DRPGAccessoryDefinitionSO accessory = CreateAccessory(
                "save_accessory",
                1f,
                created);
            DRPGLootItemDefinitionSO loot = CreateLoot(
                "save_loot",
                0.5f,
                5,
                created);

            object registry = Activator.CreateInstance(
                registryType,
                new object[]
                {
                    new[] { backpackDefinition, equippedDefinition, recoveryDefinition },
                    new[] { outfit },
                    new[] { accessory },
                    new[] { regularTrait, outfitTrait },
                    Array.Empty<DRPGWeaponAffixDefinitionSO>(),
                    Array.Empty<DRPGWeaponCardDefinitionSO>(),
                    Array.Empty<DRPGWeaponAttachmentDefinitionSO>(),
                    new[] { loot }
                });
            DRPGEquipmentRoot source = CreateRoot(registryType, registry, 6, 40f);
            var random = new MinimumRandom();
            DRPGWeaponInstance backpackWeapon = DRPGWeaponInstance.Create(
                "save_backpack_instance",
                backpackDefinition,
                random);
            DRPGWeaponInstance equippedWeapon = DRPGWeaponInstance.Create(
                "save_equipped_instance",
                equippedDefinition,
                random);
            DRPGWeaponInstance recoveryWeapon = DRPGWeaponInstance.Create(
                "save_recovery_instance",
                recoveryDefinition,
                random);
            AssertSuccess(source.PartyInventory.TryAddWeapon(backpackWeapon),
                "Backpack weapon must be owned before export.");
            AssertEqual(7, source.PartyInventory.TryAddLoot(loot, 7),
                "Stacked loot must enter the backpack before export.");
            AssertSuccess(DRPGInventoryTransferService.Transfer(
                    source.PartyInventory,
                    1,
                    source.PartyInventory,
                    5),
                "Loot slot must be rearranged before export.");

            var friendEquipment = new DRPGCharacterEquipmentState(
                friend.CombatantId,
                equippedWeapon);
            AssertSuccess(friendEquipment.TrySetOutfit(outfit),
                "Outfit must equip before export.");
            AssertSuccess(friendEquipment.TrySetAccessory(0, accessory),
                "Accessory must equip before export.");
            AssertSuccess(
                friendEquipment.TraitPool.TryAddRegularTrait(regularTrait.TraitId),
                "Regular trait must enter pool before export.");
            var employeeEquipment = new DRPGCharacterEquipmentState(
                employee.CombatantId,
                recoveryWeapon);
            AssertSuccess(source.TryRegisterCharacter(friend, friendEquipment),
                "Friend must register before export.");
            AssertSuccess(source.TryRegisterCharacter(employee, employeeEquipment),
                "Employee must register before export.");

            object recoveryService = GetProperty(source, "FieldRecovery");
            MethodInfo handleDeath = RequireMethod(
                recoveryService.GetType(),
                "TryHandlePermanentDeath");
            object[] deathArgs = { "save_death", employee.CombatantId, null };
            AssertSuccess(handleDeath.Invoke(recoveryService, deathArgs),
                "Employee death must create persisted recovery content.");

            object snapshot = exportMethod.Invoke(source, null);
            AssertEqual(2, GetField<int>(snapshot, "version"),
                "Slot inventory save schema must use version two.");
            AssertEqual(3, GetList(snapshot, "backpackSlots").Count,
                "Only occupied backpack slots should be serialized.");
            string json = JsonUtility.ToJson(snapshot);
            object roundTrip = JsonUtility.FromJson(json, saveType);

            IList detachedBackpack = GetList(snapshot, "backpackWeaponInstanceIds");
            detachedBackpack.Clear();
            AssertTrue(source.PartyInventory.ContainsWeapon(backpackWeapon.InstanceId),
                "Mutating exported data must not mutate live ownership.");

            DRPGEquipmentRoot restored = CreateRoot(registryType, registry, 6, 40f);
            AssertSuccess(restored.TryRegisterCharacter(
                    friend,
                    new DRPGCharacterEquipmentState(friend.CombatantId)),
                "Restore root must know friend profile before import.");
            AssertSuccess(restored.TryRegisterCharacter(
                    employee,
                    new DRPGCharacterEquipmentState(employee.CombatantId)),
                "Restore root must know employee profile before import.");
            AssertSuccess(importMethod.Invoke(restored, new[] { roundTrip }),
                "Valid equipment snapshot must import.");
            AssertTrue(
                restored.PartyInventory.ContainsWeapon(backpackWeapon.InstanceId),
                "Backpack weapon ownership must restore.");
            AssertEqual(7, restored.PartyInventory.GetLootQuantity(loot.ItemId),
                "Stacked loot quantity must restore.");
            AssertTrue(restored.PartyInventory.GetSlot(1).IsEmpty &&
                       restored.PartyInventory.GetSlot(5).Item != null &&
                       restored.PartyInventory.GetSlot(5).Item.Quantity == 5,
                "Backpack slot layout must round-trip exactly.");
            AssertTrue(restored.TryGetCharacter(
                    friend.CombatantId,
                    out _,
                    out DRPGCharacterEquipmentState restoredFriend),
                "Restored friend equipment must exist.");
            AssertEqual(equippedWeapon.InstanceId,
                restoredFriend.EquippedWeapon.InstanceId,
                "Equipped unique instance ID must restore.");
            AssertSame(outfit, restoredFriend.CurrentOutfit,
                "Outfit definition must restore by stable ID.");
            AssertSame(accessory, restoredFriend.Accessories[0],
                "Accessory definition must restore by stable ID.");
            AssertEqual(2, restoredFriend.ResolvedTraitIds.Count,
                "Regular and outfit-bound traits must restore.");
            AssertEqual(1,
                CountProperty(GetProperty(restored, "FieldRecovery"), "ActiveBags"),
                "Unresolved field recovery bag must restore.");

            object legacyV1 = JsonUtility.FromJson(json, saveType);
            SetFieldValue(legacyV1, "version", 1);
            GetList(legacyV1, "backpackSlots").Clear();
            DRPGEquipmentRoot legacyRoot = CreateRoot(
                registryType,
                registry,
                6,
                40f);
            RegisterEmpty(legacyRoot, friend, employee);
            AssertSuccess(importMethod.Invoke(legacyRoot, new[] { legacyV1 }),
                "Legacy v1 equipment snapshots must migrate into stable slots.");
            AssertTrue(
                legacyRoot.PartyInventory.ContainsWeapon(
                    backpackWeapon.InstanceId),
                "Legacy backpack ownership must survive migration.");

            object duplicate = JsonUtility.FromJson(json, saveType);
            object friendSave = FindByField(
                GetList(duplicate, "characters"),
                "characterId",
                friend.CombatantId);
            string equippedId = GetField<string>(
                friendSave,
                "equippedWeaponInstanceId");
            GetList(duplicate, "backpackWeaponInstanceIds").Add(equippedId);
            DRPGEquipmentRoot duplicateRoot = CreateRoot(
                registryType,
                registry,
                6,
                40f);
            RegisterEmpty(duplicateRoot, friend, employee);
            AssertFailure(importMethod.Invoke(duplicateRoot, new[] { duplicate }),
                "Duplicate weapon ownership must reject the whole import.");

            object missing = JsonUtility.FromJson(json, saveType);
            IList weaponSaves = GetList(missing, "weaponInstances");
            object missingEntry = FindByField(
                weaponSaves,
                "instanceId",
                backpackWeapon.InstanceId);
            SetFieldValue(missingEntry, "definitionId", "missing_weapon_definition");
            DRPGEquipmentRoot quarantineRoot = CreateRoot(
                registryType,
                registry,
                6,
                40f);
            RegisterEmpty(quarantineRoot, friend, employee);
            AssertSuccess(importMethod.Invoke(quarantineRoot, new[] { missing }),
                "A missing weapon definition must quarantine instead of deleting save data.");
            AssertEqual(1, CountProperty(quarantineRoot, "QuarantinedWeaponEntries"),
                "Exactly one missing definition entry must be quarantined.");
            AssertEqual(1, CountProperty(quarantineRoot, "LastImportWarnings"),
                "Missing definition must emit one actionable import warning.");
            AssertFalse(
                quarantineRoot.PartyInventory.ContainsWeapon(
                    backpackWeapon.InstanceId),
                "Quarantined weapon must not enter a live ownership location.");
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG equipment save";
    }

    private static DRPGEquipmentRoot CreateRoot(
        Type registryType,
        object registry,
        int slots,
        float weight)
    {
        ConstructorInfo constructor = typeof(DRPGEquipmentRoot).GetConstructor(
            new[] { typeof(DRPGPartyInventoryState), registryType });
        if (constructor == null)
            throw new MissingMethodException("DRPGEquipmentRoot registry constructor");
        return (DRPGEquipmentRoot)constructor.Invoke(
            new object[] { new DRPGPartyInventoryState(slots, weight), registry });
    }

    private static void RegisterEmpty(
        DRPGEquipmentRoot root,
        params DRPGCombatantProfileSO[] profiles)
    {
        for (int i = 0; i < profiles.Length; i++)
        {
            AssertSuccess(root.TryRegisterCharacter(
                    profiles[i],
                    new DRPGCharacterEquipmentState(profiles[i].CombatantId)),
                "Profile must register before import: " + profiles[i].CombatantId);
        }
    }

    private static DRPGProfessionSO CreateProfession(
        ICollection<UnityEngine.Object> created)
    {
        var value = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        SetPrivateField(value, "professionId", "save_profession");
        created.Add(value);
        return value;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        string id,
        DRPGPersonnelKind kind,
        DRPGProfessionSO profession,
        ICollection<UnityEngine.Object> created)
    {
        var progression =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        SetPrivateField(progression, "progressionId", id + "_progression");
        SetPrivateField(progression, "personnelKind", kind);
        SetPrivateField(progression, "defaultProfession", profession);
        SetPrivateField(
            progression,
            "availableProfessions",
            new List<DRPGProfessionSO> { profession });
        created.Add(progression);
        var profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetPrivateField(profile, "combatantId", id);
        SetPrivateField(profile, "progressionDefinition", progression);
        created.Add(profile);
        return profile;
    }

    private static DRPGWeaponDefinitionSO CreateWeaponDefinition(
        string id,
        DRPGProfessionSO profession,
        float weight,
        ICollection<UnityEngine.Object> created)
    {
        var value = ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        SetPrivateField(value, "weaponId", id);
        SetPrivateField(value, "weight", weight);
        SetPrivateField(
            value,
            "compatibleProfessions",
            new List<DRPGProfessionSO> { profession });
        created.Add(value);
        return value;
    }

    private static DRPGTraitDefinitionSO CreateTrait(
        string id,
        ICollection<UnityEngine.Object> created)
    {
        var value = ScriptableObject.CreateInstance<DRPGTraitDefinitionSO>();
        SetPrivateField(value, "traitId", id);
        created.Add(value);
        return value;
    }

    private static DRPGOutfitDefinitionSO CreateOutfit(
        string id,
        DRPGTraitDefinitionSO trait,
        ICollection<UnityEngine.Object> created)
    {
        var value = ScriptableObject.CreateInstance<DRPGOutfitDefinitionSO>();
        SetPrivateField(value, "outfitId", id);
        SetPrivateField(value, "grantedTrait", trait);
        created.Add(value);
        return value;
    }

    private static DRPGAccessoryDefinitionSO CreateAccessory(
        string id,
        float weight,
        ICollection<UnityEngine.Object> created)
    {
        var value = ScriptableObject.CreateInstance<DRPGAccessoryDefinitionSO>();
        SetPrivateField(value, "accessoryId", id);
        SetPrivateField(value, "weight", weight);
        created.Add(value);
        return value;
    }

    private static DRPGLootItemDefinitionSO CreateLoot(
        string id,
        float weight,
        int maxStack,
        ICollection<UnityEngine.Object> created)
    {
        var value = ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();
        SetPrivateField(value, "itemId", id);
        SetPrivateField(value, "displayName", id);
        SetPrivateField(value, "weight", weight);
        SetPrivateField(value, "maxStack", maxStack);
        created.Add(value);
        return value;
    }

    private static Type FindType(string name)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(name, false);
            if (type != null)
                return type;
        }
        return null;
    }

    private static MethodInfo RequireMethod(Type type, string name)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
            throw new MissingMethodException(type.Name, name);
        return method;
    }

    private static object GetProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, name);
        return property.GetValue(target);
    }

    private static int CountProperty(object target, string name)
    {
        int count = 0;
        foreach (object ignored in (IEnumerable)GetProperty(target, name))
            count++;
        return count;
    }

    private static IList GetList(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name);
        if (field == null || !(field.GetValue(target) is IList list))
            throw new MissingFieldException(target.GetType().Name, name);
        return list;
    }

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target);
    }

    private static object FindByField(
        IEnumerable source,
        string fieldName,
        string expected)
    {
        foreach (object item in source)
        {
            if (string.Equals(
                    GetField<string>(item, fieldName),
                    expected,
                    StringComparison.Ordinal))
            {
                return item;
            }
        }
        throw new Exception("Saved entry not found: " + expected);
    }

    private static void SetFieldValue(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static void AssertSuccess(object result, string message)
    {
        PropertyInfo success = result?.GetType().GetProperty("Success");
        if (success == null || !(bool)success.GetValue(result))
            throw new Exception(message);
    }

    private static void AssertFailure(object result, string message)
    {
        PropertyInfo success = result?.GetType().GetProperty("Success");
        if (success == null || (bool)success.GetValue(result))
            throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
            throw new Exception(message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }
}
#endif
