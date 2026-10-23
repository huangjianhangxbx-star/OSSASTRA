#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGWeaponEquipTransactionProbe
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
        var created = new List<UnityEngine.Object>();
        try
        {
            DRPGProfessionSO blade = CreateProfession("equip_blade", created);
            DRPGProfessionSO revolver = CreateProfession("equip_revolver", created);
            DRPGCombatantProfileSO friend = CreateProfile(
                "equip_friend",
                DRPGPersonnelKind.Friend,
                blade,
                revolver,
                new DRPGCoreAttributes(10, 12, 10, 12, 10, 10),
                created);
            DRPGCombatantProfileSO employee = CreateProfile(
                "equip_employee",
                DRPGPersonnelKind.Employee,
                blade,
                null,
                DRPGCoreAttributes.Default,
                created);

            var random = new MinimumRandom();
            DRPGWeaponInstance oldBlade = CreateWeapon(
                "old_blade",
                "old_blade_instance",
                blade,
                2f,
                DRPGWeaponOwnershipKind.General,
                null,
                null,
                random,
                created);
            DRPGWeaponInstance revolverCandidate = CreateWeapon(
                "revolver_candidate",
                "revolver_candidate_instance",
                revolver,
                3f,
                DRPGWeaponOwnershipKind.General,
                null,
                null,
                random,
                created);
            DRPGWeaponInstance demandingBlade = CreateWeapon(
                "demanding_blade",
                "demanding_blade_instance",
                blade,
                3f,
                DRPGWeaponOwnershipKind.General,
                null,
                new List<DRPGAttributeRequirement>
                {
                    new DRPGAttributeRequirement(
                        DRPGCoreAttributeId.Strength,
                        13)
                },
                random,
                created);
            DRPGWeaponInstance friendSignature = CreateWeapon(
                "friend_signature",
                "friend_signature_instance",
                blade,
                4f,
                DRPGWeaponOwnershipKind.Signature,
                friend.CombatantId,
                new List<DRPGAttributeRequirement>
                {
                    new DRPGAttributeRequirement(
                        DRPGCoreAttributeId.Strength,
                        12),
                    new DRPGAttributeRequirement(
                        DRPGCoreAttributeId.Technique,
                        12)
                },
                random,
                created);
            DRPGWeaponInstance wrongSignature = CreateWeapon(
                "wrong_signature",
                "wrong_signature_instance",
                blade,
                4f,
                DRPGWeaponOwnershipKind.Signature,
                "someone_else",
                null,
                random,
                created);
            DRPGWeaponInstance employeeSignature = CreateWeapon(
                "employee_signature",
                "employee_signature_instance",
                blade,
                4f,
                DRPGWeaponOwnershipKind.Signature,
                employee.CombatantId,
                null,
                random,
                created);

            var inventory = new DRPGPartyInventoryState(8, 100f);
            Add(inventory, revolverCandidate);
            Add(inventory, demandingBlade);
            Add(inventory, friendSignature);
            Add(inventory, wrongSignature);
            Add(inventory, employeeSignature);
            var progression = new DRPGProgressionService();
            var friendEquipment = new DRPGCharacterEquipmentState(
                friend.CombatantId,
                oldBlade);
            var employeeEquipment = new DRPGCharacterEquipmentState(
                employee.CombatantId);
            var root = new DRPGEquipmentRoot(inventory);
            AssertTrue(root.TryRegisterCharacter(friend, friendEquipment).Success,
                "Friend must register in equipment root");
            AssertTrue(root.TryRegisterCharacter(employee, employeeEquipment).Success,
                "Employee must register in equipment root");

            var duplicateInventory = new DRPGPartyInventoryState(2, 10f);
            AssertTrue(duplicateInventory.TryAddWeapon(oldBlade).Success,
                "Duplicate ownership probe must place equipped instance in backpack");
            var duplicateRoot = new DRPGEquipmentRoot(duplicateInventory);
            AssertFalse(
                duplicateRoot.TryRegisterCharacter(
                    friend,
                    new DRPGCharacterEquipmentState(
                        friend.CombatantId,
                        oldBlade)).Success,
                "Equipment root must reject an instance present in both backpack and equipment");
            var service = new DRPGWeaponEquipService(progression, root);

            DRPGEquipmentOperationResult locked = service.TryEquipWeapon(
                friend.CombatantId,
                revolverCandidate.InstanceId,
                revolver.ProfessionId);
            AssertFailureUnchanged(
                locked,
                DRPGEquipmentError.ProfessionLocked,
                inventory,
                revolverCandidate,
                friendEquipment,
                oldBlade,
                progression,
                friend,
                blade);

            AssertTrue(
                progression.TryUnlockProfession(
                    friend,
                    revolver.ProfessionId).Success,
                "Profession must unlock explicitly before equipment can switch it");
            DRPGEquipmentOperationResult equipped = service.TryEquipWeapon(
                friend.CombatantId,
                revolverCandidate.InstanceId,
                revolver.ProfessionId);
            AssertTrue(equipped.Success,
                "Valid weapon equip and profession switch must commit atomically");
            AssertSame(revolverCandidate, friendEquipment.EquippedWeapon,
                "Candidate must become equipped");
            AssertFalse(inventory.ContainsWeapon(revolverCandidate.InstanceId),
                "Equipped candidate must leave backpack");
            AssertTrue(inventory.ContainsWeapon(oldBlade.InstanceId),
                "Previously equipped weapon must return to backpack");
            AssertSame(revolver, progression.GetView(friend).ActiveProfession,
                "Compatible requested profession must become active");
            AssertEqual(
                revolverCandidate.InstanceId,
                friendEquipment.GetLastWeaponInstanceId(revolver.ProfessionId),
                "Successful equipment must update per-profession memory");

            DRPGEquipmentOperationResult attributes = service.TryEquipWeapon(
                friend.CombatantId,
                demandingBlade.InstanceId,
                blade.ProfessionId);
            AssertFailureUnchanged(
                attributes,
                DRPGEquipmentError.AttributeRequirementNotMet,
                inventory,
                demandingBlade,
                friendEquipment,
                revolverCandidate,
                progression,
                friend,
                revolver);

            DRPGEquipmentOperationResult owner = service.TryEquipWeapon(
                friend.CombatantId,
                wrongSignature.InstanceId,
                blade.ProfessionId);
            AssertFailureUnchanged(
                owner,
                DRPGEquipmentError.SignatureOwnerMismatch,
                inventory,
                wrongSignature,
                friendEquipment,
                revolverCandidate,
                progression,
                friend,
                revolver);

            DRPGEquipmentOperationResult employeeRejected =
                service.TryEquipWeapon(
                    employee.CombatantId,
                    employeeSignature.InstanceId,
                    blade.ProfessionId);
            AssertFailureUnchanged(
                employeeRejected,
                DRPGEquipmentError.EmployeeCannotEquipSignature,
                inventory,
                employeeSignature,
                employeeEquipment,
                null,
                progression,
                employee,
                blade);

            DRPGEquipmentOperationResult signatureEquipped =
                service.TryEquipWeapon(
                    friend.CombatantId,
                    friendSignature.InstanceId,
                    blade.ProfessionId);
            AssertTrue(signatureEquipped.Success,
                "A friend must be able to equip their own signature weapon");
            AssertSame(friendSignature, friendEquipment.EquippedWeapon,
                "Owned signature weapon must become equipped");
            AssertSame(blade, progression.GetView(friend).ActiveProfession,
                "Weapon switch must switch to its resolved profession");

            DRPGCoreAttributes simulatedEffectiveBattleAttributes =
                new DRPGCoreAttributes(1, 1, 1, 1, 1, 1);
            AssertEqual(1, simulatedEffectiveBattleAttributes.Strength,
                "Probe must simulate an effective battle debuff to zero-like values");
            AssertSame(friendSignature, friendEquipment.EquippedWeapon,
                "Transient battle attributes are never an input to equip eligibility");

            DRPGEquipmentOperationResult restored =
                service.TryRestoreLastWeaponForProfession(
                    friend.CombatantId,
                    revolver.ProfessionId);
            AssertTrue(restored.Success,
                "Recorded profession weapon must restore when it remains in backpack and eligible");
            AssertSame(revolverCandidate, friendEquipment.EquippedWeapon,
                "Profession restore must re-equip its recorded unique instance");
            AssertSame(revolver, progression.GetView(friend).ActiveProfession,
                "Profession restore must be the same atomic weapon/profession transaction");
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG weapon equip transaction";
    }

    private static DRPGProfessionSO CreateProfession(
        string id,
        ICollection<UnityEngine.Object> created)
    {
        DRPGProfessionSO profession =
            ScriptableObject.CreateInstance<DRPGProfessionSO>();
        SetField(profession, "professionId", id);
        created.Add(profession);
        return profession;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        string characterId,
        DRPGPersonnelKind personnelKind,
        DRPGProfessionSO defaultProfession,
        DRPGProfessionSO lockedProfession,
        DRPGCoreAttributes initialAttributes,
        ICollection<UnityEngine.Object> created)
    {
        DRPGCharacterProgressionDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        SetField(definition, "progressionId", characterId + "_progression");
        SetField(definition, "defaultProfession", defaultProfession);
        var professions = new List<DRPGProfessionSO> { defaultProfession };
        if (lockedProfession != null)
            professions.Add(lockedProfession);
        SetField(definition, "availableProfessions", professions);
        SetField(
            definition,
            "startingUnlockedProfessions",
            new List<DRPGProfessionSO> { defaultProfession });
        SetField(definition, "personnelKind", personnelKind);
        SetField(definition, "initialAttributes", initialAttributes);
        created.Add(definition);

        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", characterId);
        SetField(profile, "progressionDefinition", definition);
        created.Add(profile);
        return profile;
    }

    private static DRPGWeaponInstance CreateWeapon(
        string weaponId,
        string instanceId,
        DRPGProfessionSO profession,
        float weight,
        DRPGWeaponOwnershipKind ownershipKind,
        string ownerId,
        List<DRPGAttributeRequirement> requirements,
        IDRPGRandomSource random,
        ICollection<UnityEngine.Object> created)
    {
        DRPGWeaponDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        SetField(definition, "weaponId", weaponId);
        SetField(definition, "weight", weight);
        SetField(
            definition,
            "compatibleProfessions",
            new List<DRPGProfessionSO> { profession });
        SetField(definition, "ownershipKind", ownershipKind);
        SetField(definition, "signatureCharacterId", ownerId);
        SetField(
            definition,
            "rarity",
            ownershipKind == DRPGWeaponOwnershipKind.Signature
                ? DRPGEquipmentRarity.RedGold
                : DRPGEquipmentRarity.Blue);
        if (requirements != null)
            SetField(definition, "requirements", requirements);
        created.Add(definition);
        return DRPGWeaponInstance.Create(instanceId, definition, random);
    }

    private static void Add(
        DRPGPartyInventoryState inventory,
        DRPGWeaponInstance weapon)
    {
        AssertTrue(inventory.TryAddWeapon(weapon).Success,
            "Probe weapon must enter backpack: " + weapon.InstanceId);
    }

    private static void AssertFailureUnchanged(
        DRPGEquipmentOperationResult result,
        DRPGEquipmentError expectedError,
        DRPGPartyInventoryState inventory,
        DRPGWeaponInstance candidate,
        DRPGCharacterEquipmentState equipment,
        DRPGWeaponInstance expectedEquipped,
        DRPGProgressionService progression,
        DRPGCombatantProfileSO profile,
        DRPGProfessionSO expectedProfession)
    {
        AssertFalse(result.Success, "Operation must fail");
        AssertEqual(expectedError, result.Error, "Failure reason mismatch");
        AssertTrue(inventory.ContainsWeapon(candidate.InstanceId),
            "Failed transaction must preserve backpack candidate");
        AssertSame(expectedEquipped, equipment.EquippedWeapon,
            "Failed transaction must preserve equipped weapon");
        AssertSame(expectedProfession, progression.GetView(profile).ActiveProfession,
            "Failed transaction must preserve active profession");
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
