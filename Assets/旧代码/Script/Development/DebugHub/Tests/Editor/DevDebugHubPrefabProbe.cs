#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DevDebugHubPrefabProbe
{
    public const string PrefabPath =
        "Assets/Resources/Development/PF_DevDebugHub.prefab";

    public static string Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert(prefab != null, "Development debug hub prefab is missing");
        Canvas canvas = prefab.GetComponent<Canvas>();
        Assert(canvas != null, "Debug hub Canvas is missing");
        Assert(canvas.sortingOrder >= 9000,
            "Debug hub must render above ordinary modal UI");
        CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
        Assert(scaler != null, "Debug hub CanvasScaler is missing");
        Assert(scaler.referenceResolution == new Vector2(2560f, 1440f),
            "Debug hub must author against the project QHD baseline");
        Assert(prefab.GetComponent<GraphicRaycaster>() != null,
            "Debug hub GraphicRaycaster is missing");
        Assert(prefab.GetComponent<CanvasGroup>() != null,
            "Debug hub CanvasGroup is missing");

        DevDebugHubController controller =
            prefab.GetComponent<DevDebugHubController>();
        Assert(controller != null, "DevDebugHubController is missing");
        Require(prefab.transform, "Backdrop");
        Require(prefab.transform, "SafeAreaRoot/Frame/Header/CloseButton");
        Transform categoryTemplate = Require(
            prefab.transform,
            "SafeAreaRoot/Frame/Body/CategoryRail/CategoryButtonTemplate");
        Transform actionTemplate = Require(
            prefab.transform,
            "SafeAreaRoot/Frame/Body/ActionPanel/ActionScroll/Viewport/Content/ActionButtonTemplate");
        Require(prefab.transform, "SafeAreaRoot/Frame/Body/DetailPanel/Title");
        Require(prefab.transform, "SafeAreaRoot/Frame/Body/DetailPanel/Summary");
        Require(prefab.transform, "SafeAreaRoot/Frame/Body/DetailPanel/Details");
        Require(prefab.transform, "SafeAreaRoot/Frame/Footer/Result");

        Assert(!categoryTemplate.gameObject.activeSelf,
            "Category template must stay inactive in the authored prefab");
        Assert(!actionTemplate.gameObject.activeSelf,
            "Action template must stay inactive in the authored prefab");
        Assert(categoryTemplate.GetComponent<Button>() != null,
            "Category template requires a Button");
        DevDebugActionButtonView actionView =
            actionTemplate.GetComponent<DevDebugActionButtonView>();
        Assert(actionView != null,
            "Action template requires DevDebugActionButtonView");
        Assert(actionTemplate.GetComponent<Button>() != null,
            "Action template requires a Button");
        Assert(actionTemplate.GetComponentInChildren<TMP_Text>(true) != null,
            "Action template requires visible TMP content");

        SerializedObject serialized = new SerializedObject(controller);
        RequireReference(serialized, "canvasGroup");
        RequireReference(serialized, "frame");
        RequireReference(serialized, "categoryRoot");
        RequireReference(serialized, "categoryButtonTemplate");
        RequireReference(serialized, "actionRoot");
        RequireReference(serialized, "actionButtonTemplate");
        RequireReference(serialized, "detailTitleText");
        RequireReference(serialized, "detailSummaryText");
        RequireReference(serialized, "detailBodyText");
        RequireReference(serialized, "detailStateText");
        RequireReference(serialized, "resultText");
        RequireReference(serialized, "closeButton");

        return "PASS responsive development debug hub prefab";
    }

    private static Transform Require(Transform root, string path)
    {
        Transform result = root.Find(path);
        Assert(result != null, "Missing debug hub hierarchy path: " + path);
        return result;
    }

    private static void RequireReference(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        Assert(property != null, "Missing serialized field: " + propertyName);
        Assert(property.objectReferenceValue != null,
            "Unbound debug hub field: " + propertyName);
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
