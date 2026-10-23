#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGRecoveryBagProbe
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
        Type serviceType = FindType("DRPGFieldRecoveryService");
        if (serviceType == null)
            throw new Exception("DRPGFieldRecoveryService is missing.");

        var created = new List<UnityEngine.Object>();
        try
        {
            DRPGProfessionSO profession = CreateProfession(created);
            DRPGCombatantProfileSO friend = CreateProfile(
                "recovery_friend",
                DRPGPersonnelKind.Friend,
                profession,
                created);
            DRPGCombatantProfileSO employeeA = CreateProfile(
                "recovery_employee_a",
                DRPGPersonnelKind.Employee,
                profession,
                created);
            DRPGCombatantProfileSO employeeB = CreateProfile(
                "recovery_employee_b",
                DRPGPersonnelKind.Employee,
                profession,
                created);

            var random = new MinimumRandom();
            DRPGWeaponInstance friendWeapon = CreateWeapon(
                "recovery_friend_weapon",
                "recovery_friend_weapon_instance",
                profession,
                1f,
                random,
                created);
            DRPGWeaponInstance employeeWeaponA = CreateWeapon(
                "recovery_employee_weapon_a",
                "recovery_employee_weapon_a_instance",
                profession,
                3f,
                random,
                created);
            DRPGWeaponInstance employeeWeaponB = CreateWeapon(
                "recovery_employee_weapon_b",
                "recovery_employee_weapon_b_instance",
                profession,
                2f,
                random,
                created);
            DRPGAccessoryDefinitionSO accessoryA = CreateAccessory(
                "recovery_accessory_a",
                1f,
                created);
            DRPGAccessoryDefinitionSO accessoryB = CreateAccessory(
                "recovery_accessory_b",
                1f,
                created);

            var inventory = new DRPGPartyInventoryState(2, 3f);
            var root = new DRPGEquipmentRoot(inventory);
            var friendEquipment = new DRPGCharacterEquipmentState(
                friend.CombatantId,
                friendWeapon);
            var employeeEquipmentA = new DRPGCharacterEquipmentState(
                employeeA.CombatantId,
                employeeWeaponA);
            var employeeEquipmentB = new DRPGCharacterEquipmentState(
                employeeB.CombatantId,
                employeeWeaponB);
            AssertSuccess(employeeEquipmentA.TrySetAccessory(0, accessoryA),
                "Employee accessory A must equip.");
            AssertSuccess(employeeEquipmentA.TrySetAccessory(1, accessoryB),
                "Employee accessory B must equip.");
            AssertSuccess(root.TryRegisterCharacter(friend, friendEquipment),
                "Friend must register.");
            AssertSuccess(root.TryRegisterCharacter(employeeA, employeeEquipmentA),
                "Employee A must register.");
            AssertSuccess(root.TryRegisterCharacter(employeeB, employeeEquipmentB),
                "Employee B must register.");

            object service = Activator.CreateInstance(serviceType, root);
            MethodInfo handleDeath = RequireMethod(
                serviceType,
                "TryHandlePermanentDeath");
            MethodInfo collectAccessory = RequireMethod(
                serviceType,
                "TryCollectAccessory");
            MethodInfo collectWeapon = RequireMethod(
                serviceType,
                "TryCollectWeapon");
            MethodInfo resolveExtraction = RequireMethod(
                serviceType,
                "ResolveExtraction");

            object[] friendDeath =
                { "death_friend", friend.CombatantId, null };
            AssertSuccess(handleDeath.Invoke(service, friendDeath),
                "Friend defeat must be a valid no-recovery event.");
            AssertNull(friendDeath[2],
                "Friend defeat must not create a recovery bag.");
            AssertSame(friendWeapon, friendEquipment.EquippedWeapon,
                "Friend defeat must not detach equipment.");

            object[] employeeDeath =
                { "death_employee_a", employeeA.CombatantId, null };
            AssertSuccess(handleDeath.Invoke(service, employeeDeath),
                "Employee permanent death must create a recovery bag.");
            object bag = employeeDeath[2];
            AssertNotNull(bag, "Employee recovery bag is missing.");
            AssertNull(employeeEquipmentA.EquippedWeapon,
                "Employee weapon must leave equipped ownership.");
            AssertNull(employeeEquipmentA.Accessories[0],
                "Employee accessories must leave equipped ownership.");
            AssertEqual(0, inventory.UsedSlots,
                "Recovery bag contents must remain outside backpack capacity.");
            AssertEqual(1, CountProperty(service, "ActiveBags"),
                "Exactly one recovery bag must exist.");

            object[] replay =
                { "death_employee_a", employeeA.CombatantId, null };
            AssertSuccess(handleDeath.Invoke(service, replay),
                "Replayed death event must be idempotent.");
            AssertSame(bag, replay[2],
                "Replayed death event must resolve to the existing bag.");
            AssertEqual(1, CountProperty(service, "ActiveBags"),
                "Replayed death event must not duplicate bags.");

            string bagId = GetProperty<string>(bag, "BagId");
            object firstAccessory = First(GetProperty<IEnumerable>(bag, "Accessories"));
            string accessoryItemId = GetProperty<string>(
                firstAccessory,
                "InstanceId");
            AssertSuccess(
                collectAccessory.Invoke(
                    service,
                    new object[] { bagId, accessoryItemId }),
                "One accessory must transfer to backpack item-by-item.");
            AssertEqual(1, inventory.UsedSlots,
                "A manually recovered accessory must consume one slot.");
            AssertFailure(
                collectWeapon.Invoke(service, new object[] { bagId }),
                "Manual weapon pickup must honor current weight capacity.");
            AssertNotNull(GetProperty<object>(bag, "Weapon"),
                "Failed manual pickup must leave the weapon in its bag.");

            AssertSuccess(resolveExtraction.Invoke(service, new object[] { true }),
                "Successful extraction must resolve all active bags.");
            AssertEqual(0, CountProperty(service, "ActiveBags"),
                "Successful extraction must close recovered bags.");
            AssertTrue(
                inventory.ContainsSettlementRecoveryWeapon(
                    employeeWeaponA.InstanceId),
                "Successful extraction must bypass field capacity into settlement recovery.");
            AssertEqual(1, CountProperty(inventory, "SettlementRecoveryAccessories"),
                "Uncollected accessory must enter settlement recovery.");

            object[] failedDeath =
                { "death_employee_b", employeeB.CombatantId, null };
            AssertSuccess(handleDeath.Invoke(service, failedDeath),
                "Second employee death must create another bag.");
            AssertSuccess(resolveExtraction.Invoke(service, new object[] { false }),
                "Failed extraction must resolve active bags.");
            AssertFalse(inventory.ContainsWeapon(employeeWeaponB.InstanceId),
                "Ordinary weapon must not enter backpack after failed extraction.");
            AssertFalse(
                inventory.ContainsSettlementRecoveryWeapon(
                    employeeWeaponB.InstanceId),
                "Ordinary weapon must be lost after failed extraction.");
            AssertEqual(0, CountProperty(service, "ActiveBags"),
                "Failed extraction must clear unresolved field bags.");
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG recovery bag";
    }

    private static DRPGProfessionSO CreateProfession(
        ICollection<UnityEngine.Object> created)
    {
        var profession = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        SetField(profession, "professionId", "recovery_profession");
        created.Add(profession);
        return profession;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        string id,
        DRPGPersonnelKind personnelKind,
        DRPGProfessionSO profession,
        ICollection<UnityEngine.Object> created)
    {
        var progression =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        SetField(progression, "progressionId", id + "_progression");
        SetField(progression, "personnelKind", personnelKind);
        SetField(progression, "defaultProfession", profession);
        SetField(
            progression,
            "availableProfessions",
            new List<DRPGProfessionSO> { profession });
        created.Add(progression);

        var profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", id);
        SetField(profile, "progressionDefinition", progression);
        created.Add(profile);
        return profile;
    }

    private static DRPGWeaponInstance CreateWeapon(
        string weaponId,
        string instanceId,
        DRPGProfessionSO profession,
        float weight,
        IDRPGRandomSource random,
        ICollection<UnityEngine.Object> created)
    {
        var definition = ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        SetField(definition, "weaponId", weaponId);
        SetField(definition, "weight", weight);
        SetField(
            definition,
            "compatibleProfessions",
            new List<DRPGProfessionSO> { profession });
        created.Add(definition);
        return DRPGWeaponInstance.Create(instanceId, definition, random);
    }

    private static DRPGAccessoryDefinitionSO CreateAccessory(
        string id,
        float weight,
        ICollection<UnityEngine.Object> created)
    {
        var accessory = ScriptableObject.CreateInstance<DRPGAccessoryDefinitionSO>();
        SetField(accessory, "accessoryId", id);
        SetField(accessory, "weight", weight);
        created.Add(accessory);
        return accessory;
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

    private static T GetProperty<T>(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, name);
        return (T)property.GetValue(target);
    }

    private static int CountProperty(object target, string name)
    {
        int count = 0;
        foreach (object ignored in GetProperty<IEnumerable>(target, name))
            count++;
        return count;
    }

    private static object First(IEnumerable source)
    {
        foreach (object item in source)
            return item;
        throw new Exception("Expected at least one item.");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
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

    private static void AssertNull(object value, string message)
    {
        if (value != null)
            throw new Exception(message);
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
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
