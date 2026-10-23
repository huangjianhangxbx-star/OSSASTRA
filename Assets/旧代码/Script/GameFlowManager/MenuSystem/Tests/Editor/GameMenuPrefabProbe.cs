#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GameMenuPrefabProbe
{
    public const string PrefabPath =
        "Assets/Resources/DRPG/UI/PF_GameMenu.prefab";

    public static string Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert(prefab != null, "Game menu prefab is missing");
        Canvas canvas = prefab.GetComponent<Canvas>();
        Assert(canvas != null, "Canvas is missing");
        Assert(canvas.sortingOrder > 4500,
            "System menu must render above package and battle canvases");
        Assert(prefab.GetComponent<CanvasGroup>() != null, "CanvasGroup is missing");
        Assert(prefab.GetComponent<GraphicRaycaster>() != null,
            "GraphicRaycaster is missing");
        Assert(prefab.GetComponent<GameMenuController>() != null,
            "GameMenuController is missing");

        CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
        Assert(scaler != null, "CanvasScaler is missing");
        Assert(scaler.referenceResolution == new Vector2(2560f, 1440f),
            "Game menu must author against 2560x1440");
        Require(prefab.transform, "Backdrop");
        Require(prefab.transform, "Frame");
        Require(prefab.transform, "Frame/StatusPage");
        for (int i = 0; i < 6; i++)
        {
            Require(
                prefab.transform,
                "Frame/StatusPage/GrowthPanel/AttributeRow_" + i);
        }
        Require(prefab.transform, "Frame/StatusPage/GrowthPanel/ResetDraft");
        Require(prefab.transform, "Frame/StatusPage/GrowthPanel/ConfirmAttributes");
        Require(prefab.transform, "Frame/SkillTreePage");
        Require(prefab.transform, "Frame/LoadoutPage");
        Transform equipmentPage =
            Require(prefab.transform, "Frame/EquipmentPage");
        Assert(equipmentPage.GetComponent<EquipmentPageView>() != null,
            "EquipmentPageView is missing");
        Require(prefab.transform, "Frame/EquipmentPage/CurrentPanel");
        Require(prefab.transform, "Frame/EquipmentPage/CandidatePanel/Viewport/Content");
        Require(prefab.transform, "Tooltip");
        Require(prefab.transform, "DragLayer");

        Transform slots = Require(
            prefab.transform,
            "Frame/LoadoutPage/EquippedPanel/Slots");
        Assert(slots.childCount == DRPGSkillLoadout.SlotCount,
            "Loadout prefab must expose exactly four slots");
        for (int i = 0; i < slots.childCount; i++)
        {
            Transform card = Require(slots.GetChild(i), "Card");
            Require(card, "Background");
            Require(card, "Glow");
            Require(card, "Icon");
            Require(card, "Label");
        }
        Transform node = Require(
            prefab.transform,
            "Frame/SkillTreePage/NodeRoot/NodeTemplate");
        Require(node, "Background");
        Require(node, "Glow");
        Require(node, "Icon");
        Require(node, "Label");

        return "PASS layered game-menu prefab";
    }

    private static Transform Require(Transform root, string path)
    {
        Transform result = root.Find(path);
        Assert(result != null, "Missing hierarchy path: " + path);
        return result;
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
