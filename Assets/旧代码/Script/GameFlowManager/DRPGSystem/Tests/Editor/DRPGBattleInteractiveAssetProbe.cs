#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DRPGBattleInteractiveAssetProbe
{
    private const string Root = "Assets/GameResources/DRPG";
    private const string PrefabPath =
        Root + "/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string FreeShotPath =
        Root + "/Config/Test/DRPGSkill_FreeShot.asset";
    private const string FreeSlashPath =
        Root + "/Config/Test/DRPGSkill_FreeSlash.asset";
    private const string EnemySpecialPath =
        Root + "/Config/Test/DRPGSkill_EnemySpecial.asset";
    private const string ProtagonistPath =
        Root + "/Config/Test/DRPGCombatant_ankalute_protagonist.asset";
    private const string VanguardPath =
        Root + "/Config/Test/DRPGCombatant_ankalute_party_vanguard.asset";
    private const string WardenPath =
        Root + "/Config/Test/DRPGCombatant_ankalute_enemy_warden.asset";
    private const string AudioPalettePath =
        Root + "/Audio/Test/DRPGBattleAudioPalette_Test.asset";
    private const string DefenseCrestPath =
        Root + "/UI/Generated/DRPG_InteractiveDefenseCrest.png";

    public static string Run()
    {
        VerifyPrefab();
        DRPGSkillSO freeShot = VerifySkill(
            FreeShotPath,
            DRPGSkillExecutionMode.FreeStrike,
            DRPGFreeStrikeShape.Point);
        DRPGSkillSO freeSlash = VerifySkill(
            FreeSlashPath,
            DRPGSkillExecutionMode.FreeStrike,
            DRPGFreeStrikeShape.Line);
        DRPGSkillSO enemySpecial = VerifySkill(
            EnemySpecialPath,
            DRPGSkillExecutionMode.TelegraphedSpecial,
            DRPGFreeStrikeShape.Point);
        AssertEqual(DRPGFreeStrikeContactPolicy.FirstContact,
            freeShot.FreeStrikeContactPolicy,
            "FreeShot must resolve only the first primary contact");
        AssertEqual(DRPGFreeStrikeContactPolicy.AllContacts,
            freeSlash.FreeStrikeContactPolicy,
            "FreeSlash must preserve all crossed primary contacts");
        AssertEqual(DRPGSecondaryImpactKind.None,
            freeShot.FreeStrikeSecondaryImpactKind,
            "Compatibility FreeShot must not emit splash");
        AssertEqual(DRPGSecondaryImpactKind.None,
            freeSlash.FreeStrikeSecondaryImpactKind,
            "Compatibility FreeSlash must not emit splash");
        VerifyProfileContains(ProtagonistPath, freeShot, false);
        VerifyProfileContains(VanguardPath, freeSlash, false);
        VerifyProfileContains(WardenPath, enemySpecial, true);
        VerifyAudioPalette();
        AssertNotNull(
            AssetDatabase.LoadAssetAtPath<Sprite>(DefenseCrestPath),
            "The generated defense crest must import as a UI sprite");
        return "PASS DRPG interactive prefab and test content";
    }

    private static void VerifyPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        AssertNotNull(prefab, "The DRPG presentation prefab must exist");
        DRPGBattleUGUISurface surface =
            prefab.GetComponent<DRPGBattleUGUISurface>();
        AssertNotNull(surface, "The prefab must contain its UGUI surface");
        SerializedObject surfaceObject = new SerializedObject(surface);
        BattleInteractiveCombatView view = surfaceObject
            .FindProperty("interactiveCombatView")
            .objectReferenceValue as BattleInteractiveCombatView;
        AssertNotNull(view,
            "The UGUI surface must reference the interactive combat view");

        SerializedObject viewObject = new SerializedObject(view);
        string[] requiredReferences =
        {
            "panelGroup",
            "panelTransform",
            "frameImage",
            "alertText",
            "modeText",
            "targetText",
            "countdownText",
            "promptText",
            "feedbackText",
            "timerFill",
            "postureFill",
            "defenseCrest",
            "timingPulse",
            "timingPulseImage",
            "crosshair",
            "slashPreview",
            "telegraphGroup",
            "telegraphText"
        };
        for (int i = 0; i < requiredReferences.Length; i++)
        {
            SerializedProperty property = viewObject.FindProperty(
                requiredReferences[i]);
            AssertTrue(property != null && property.objectReferenceValue != null,
                "Interactive view reference is missing: " +
                requiredReferences[i]);
        }
    }

    private static DRPGSkillSO VerifySkill(
        string path,
        DRPGSkillExecutionMode executionMode,
        DRPGFreeStrikeShape shape)
    {
        DRPGSkillSO skill = AssetDatabase.LoadAssetAtPath<DRPGSkillSO>(path);
        AssertNotNull(skill, "Missing interactive test skill: " + path);
        AssertEqual(executionMode, skill.ExecutionMode,
            "Interactive skill execution mode is wrong: " + path);
        if (executionMode == DRPGSkillExecutionMode.FreeStrike)
        {
            AssertEqual(shape, skill.FreeStrikeShape,
                "Free-strike geometry is wrong: " + path);
        }
        return skill;
    }

    private static void VerifyProfileContains(
        string path,
        DRPGSkillSO expectedSkill,
        bool mustBeFirst)
    {
        DRPGCombatantProfileSO profile =
            AssetDatabase.LoadAssetAtPath<DRPGCombatantProfileSO>(path);
        AssertNotNull(profile, "Missing combatant profile: " + path);
        bool found = false;
        for (int i = 0; i < profile.Skills.Count; i++)
        {
            if (!ReferenceEquals(profile.Skills[i], expectedSkill))
                continue;
            found = true;
            if (mustBeFirst)
                AssertEqual(0, i,
                    "The enemy special must be first so test AI selects it");
        }
        AssertTrue(found,
            profile.DisplayName + " must expose " + expectedSkill.DisplayName);
    }

    private static void VerifyAudioPalette()
    {
        DRPGBattleAudioPaletteSO palette =
            AssetDatabase.LoadAssetAtPath<DRPGBattleAudioPaletteSO>(
                AudioPalettePath);
        AssertNotNull(palette, "The DRPG audio palette must exist");
        SerializedObject serialized = new SerializedObject(palette);
        string[] required =
        {
            "specialWarning",
            "interactiveWindowOpen",
            "guard",
            "perfectGuard",
            "miss",
            "weakHit",
            "interactiveWindowClose"
        };
        for (int i = 0; i < required.Length; i++)
        {
            SerializedProperty property = serialized.FindProperty(required[i]);
            AssertTrue(property != null && property.objectReferenceValue != null,
                "Audio palette is missing interactive cue: " + required[i]);
        }
    }

    private static void AssertNotNull(object value, string message)
    {
        AssertTrue(value != null, message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
