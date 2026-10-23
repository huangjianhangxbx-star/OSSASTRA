#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGProfessionUnlockProbe
{
    public static string Run()
    {
        DRPGProfessionSO first = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        DRPGProfessionSO second = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        DRPGCharacterProgressionDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            SetField(first, "professionId", "unlock_probe_first");
            SetField(second, "professionId", "unlock_probe_second");
            SetField(definition, "progressionId", "unlock_probe_definition");
            SetField(definition, "defaultProfession", first);
            SetField(
                definition,
                "availableProfessions",
                new List<DRPGProfessionSO> { first, second });
            SetField(
                definition,
                "startingUnlockedProfessions",
                new List<DRPGProfessionSO> { first });
            SetField(profile, "combatantId", "unlock_probe_actor");
            SetField(profile, "progressionDefinition", definition);

            var service = new DRPGProgressionService();
            AssertFalse(
                service.TrySetActiveProfession(profile, second).Success,
                "An available but locked profession must not become active");
            AssertEqual(
                4,
                new DRPGProgressionSaveData().version,
                "New progression saves must use the resonance-aware v4 format");

            DRPGProgressionSaveData lockedSave = service.CaptureSaveData(
                new[] { profile });
            DRPGProfessionProgressionSaveData lockedSecond = FindProfessionSave(
                lockedSave,
                "unlock_probe_second");
            AssertFalse(lockedSecond.isUnlocked,
                "A v4 save must persist a locked profession as locked");
            var lockedRestore = new DRPGProgressionService();
            AssertTrue(
                lockedRestore.RestoreSaveData(new[] { profile }, lockedSave).Success,
                "A v4 locked-profession save must restore");
            AssertFalse(
                lockedRestore.TrySetActiveProfession(profile, second).Success,
                "A v4 restore must not unlock a locked profession");

            DRPGProgressionOperationResult unlockResult =
                service.TryUnlockProfession(
                    profile,
                    "unlock_probe_second");
            AssertTrue(unlockResult.Success,
                "An available locked profession must unlock explicitly");
            AssertTrue(service.TrySetActiveProfession(profile, second).Success,
                "An explicitly unlocked profession must become selectable");

            DRPGProgressionSaveData unlockedSave = service.CaptureSaveData(
                new[] { profile });
            AssertTrue(
                FindProfessionSave(unlockedSave, "unlock_probe_second").isUnlocked,
                "A v4 save must persist an explicit profession unlock");
            var unlockedRestore = new DRPGProgressionService();
            AssertTrue(
                unlockedRestore.RestoreSaveData(new[] { profile }, unlockedSave).Success,
                "An unlocked v4 save must restore");
            AssertTrue(
                unlockedRestore.TrySetActiveProfession(profile, second).Success,
                "An explicit unlock must survive a v4 round trip");

            DRPGProgressionSaveData legacyV3 = JsonUtility.FromJson<
                DRPGProgressionSaveData>(JsonUtility.ToJson(unlockedSave));
            legacyV3.version = 3;
            legacyV3.resonance = null;
            var migratedV3Service = new DRPGProgressionService();
            AssertTrue(
                migratedV3Service.RestoreSaveData(
                    new[] { profile },
                    legacyV3).Success,
                "A supported v3 save must migrate without resonance data");
            AssertTrue(
                migratedV3Service.TrySetActiveProfession(profile, second).Success,
                "An explicit profession unlock must survive v3 migration");

            DRPGProgressionSaveData legacyV2 = JsonUtility.FromJson<
                DRPGProgressionSaveData>(JsonUtility.ToJson(unlockedSave));
            legacyV2.version = 2;
            for (int i = 0; i < legacyV2.characters[0].professions.Count; i++)
                legacyV2.characters[0].professions[i].isUnlocked = false;
            var migratedService = new DRPGProgressionService();
            AssertTrue(
                migratedService.RestoreSaveData(new[] { profile }, legacyV2).Success,
                "A supported v2 save must migrate instead of being rejected");
            AssertTrue(
                migratedService.TrySetActiveProfession(profile, second).Success,
                "Every previously available profession must remain unlocked after v2 migration");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(first);
        }

        return "PASS DRPG profession unlock";
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

    private static DRPGProfessionProgressionSaveData FindProfessionSave(
        DRPGProgressionSaveData save,
        string professionId)
    {
        for (int i = 0; i < save.characters[0].professions.Count; i++)
        {
            DRPGProfessionProgressionSaveData profession =
                save.characters[0].professions[i];
            if (profession.professionId == professionId)
                return profession;
        }
        throw new Exception("Profession save not found: " + professionId);
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
