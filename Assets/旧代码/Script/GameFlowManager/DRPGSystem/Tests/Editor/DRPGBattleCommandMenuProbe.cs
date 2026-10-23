#if UNITY_EDITOR
using System;
using System.Reflection;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattleCommandMenuProbe
{
    private const string PrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string BasicAttackPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGSkill_BasicAttack.asset";

    public static string Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert(prefab != null, "Battle presentation prefab is missing");
        BattleCommandMenu menu = prefab.GetComponentInChildren<BattleCommandMenu>(true);
        Assert(menu != null, "Six-category BattleCommandMenu is missing");
        Transform root = menu.transform;
        Require(root, "CommandRoot/BasicAttack");
        Require(root, "CommandRoot/Skill");
        Require(root, "CommandRoot/Item");
        Require(root, "CommandRoot/Weapon");
        Require(root, "CommandRoot/Defend");
        Require(root, "CommandRoot/Escape");
        BattleSkillMenu skillMenu =
            prefab.GetComponentInChildren<BattleSkillMenu>(true);
        Assert(skillMenu != null, "The four-slot skill submenu must remain available");
        Require(skillMenu.transform, "SkillBackButton");
        Require(root, "ItemRoot/ItemEmpty");
        Require(root, "ItemRoot/ItemBackButton");
        Require(root, "WeaponRoot/WeaponBackButton");
        Assert(
            prefab.GetComponentInChildren<BattleWeaponMenu>(true) != null,
            "Dynamic BattleWeaponMenu is missing");
        BattleRecoveryBagView recovery =
            prefab.GetComponentInChildren<BattleRecoveryBagView>(true);
        Assert(recovery != null, "BattleRecoveryBagView is missing");
        Require(prefab.transform, "BattleRecoverySystem/RecoveryPanel/Viewport/Content");
        Require(prefab.transform, "BattleRecoverySystem/RecoveryPanel/BackpackDropZone");
        Require(prefab.transform, "BattleRecoverySystem/DragLayer");
        return "PASS six-category battle command prefab";
    }

    public static string RunHiddenBeforeBattle()
    {
        BattleCommandMenu menu = LoadMenu();
        Assert(!menu.gameObject.activeSelf,
            "Battle command UI must be inactive before a battle session binds it");
        return "PASS battle command hidden before battle";
    }

    public static string RunHoverFeedback()
    {
        BattleCommandMenu menu = LoadMenu();
        string[] paths =
        {
            "CommandRoot/BasicAttack",
            "CommandRoot/Skill",
            "CommandRoot/Item",
            "CommandRoot/Weapon",
            "CommandRoot/Defend",
            "CommandRoot/Escape"
        };

        for (int i = 0; i < paths.Length; i++)
        {
            Transform target = Require(menu.transform, paths[i]);
            Button button = target.GetComponent<Button>();
            Assert(button != null, "Command button is missing: " + paths[i]);
            Assert(button.transition == Selectable.Transition.ColorTint,
                "Command button must use ColorTint feedback: " + paths[i]);
            ColorBlock colors = button.colors;
            float visibleDifference =
                Mathf.Abs(colors.highlightedColor.r - colors.normalColor.r) +
                Mathf.Abs(colors.highlightedColor.g - colors.normalColor.g) +
                Mathf.Abs(colors.highlightedColor.b - colors.normalColor.b);
            Assert(visibleDifference >= 0.45f,
                "Command hover color is not visibly different: " + paths[i]);
        }

        return "PASS battle command hover feedback";
    }

    public static string RunInterruptedTransitionDoesNotDrift()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert(prefab != null, "Battle presentation prefab is missing");
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            BattleCommandMenu menu =
                instance.GetComponentInChildren<BattleCommandMenu>(true);
            Assert(menu != null, "Six-category BattleCommandMenu is missing");
            menu.gameObject.SetActive(true);

            SerializedObject serialized = new SerializedObject(menu);
            GameObject commandRoot = serialized.FindProperty("commandRoot")
                .objectReferenceValue as GameObject;
            RectTransform rect = commandRoot != null
                ? commandRoot.transform as RectTransform
                : null;
            Assert(rect != null, "CommandRoot RectTransform is missing");
            Vector2 expectedRestPosition = rect.anchoredPosition;

            // Snapshot refreshes can interrupt and restart the same entrance
            // tween many times in one action boundary (notably reinforcement +
            // telegraph + turn changes). The configured resting position must
            // remain authoritative instead of accumulating the entrance offset.
            for (int i = 0; i < 16; i++)
                menu.ShowCommandRoot();

            FieldInfo tweenField = typeof(BattleCommandMenu).GetField(
                "transitionTween",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Tween tween = tweenField?.GetValue(menu) as Tween;
            tween?.Complete(true);

            Assert(
                Vector2.Distance(rect.anchoredPosition, expectedRestPosition) <
                    0.01f,
                "Interrupted command-menu transitions drifted from " +
                expectedRestPosition + " to " + rect.anchoredPosition);
            return "PASS interrupted command transition keeps its rest position";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    public static string RunSharedBasicAttackSupportsConfiguredPlayerRows()
    {
        DRPGSkillSO skill =
            AssetDatabase.LoadAssetAtPath<DRPGSkillSO>(BasicAttackPath);
        Assert(skill != null, "Shared test basic attack is missing");

        DRPGFormationRow[] configuredPlayerRows =
        {
            DRPGFormationRow.Front,
            DRPGFormationRow.Middle
        };
        for (int i = 0; i < configuredPlayerRows.Length; i++)
        {
            Assert(
                skill.CanUseFrom(configuredPlayerRows[i]),
                "Shared test basic attack cannot be used from configured " +
                "player row " + configuredPlayerRows[i]);
        }

        Assert(skill.CanTarget(DRPGFormationRow.Front),
            "Shared test basic attack must target the exposed enemy front row");
        return "PASS shared basic attack supports configured player rows";
    }

    private static BattleCommandMenu LoadMenu()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert(prefab != null, "Battle presentation prefab is missing");
        BattleCommandMenu menu =
            prefab.GetComponentInChildren<BattleCommandMenu>(true);
        Assert(menu != null, "Six-category BattleCommandMenu is missing");
        return menu;
    }

    private static Transform Require(Transform root, string path)
    {
        Transform result = root.Find(path);
        Assert(result != null, "Missing battle command path: " + path);
        return result;
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
