#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGGameLoopUIPrefabProbe
{
    public static string Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DRPGGameLoopUIInstaller.PrefabPath);
        if (prefab == null)
            throw new Exception("DRPG game-loop UI prefab is missing");
        if (prefab.GetComponent<Canvas>() == null ||
            prefab.GetComponent<DRPGGameLoopView>() == null)
        {
            throw new Exception("Game-loop prefab lacks Canvas or view owner");
        }
        if (prefab.transform.Find("TitleRoot/StartButton") == null)
            throw new Exception("Title start button is missing");
        Transform strategicHub = prefab.transform.Find("StrategicHubRoot");
        if (strategicHub == null)
            throw new Exception("StrategicHubRoot is missing");
        Transform hubHome = strategicHub.Find("HubHomeRoot");
        Transform dungeonSelection = strategicHub.Find("DungeonSelectionRoot");
        if (hubHome == null || dungeonSelection == null)
            throw new Exception("Strategic Hub page roots are missing");
        AssertButton(hubHome.Find("OpenDungeonSelectionButton"),
            "OpenDungeonSelectionButton");
        AssertButton(hubHome.Find("ReturnToTitleButton"),
            "ReturnToTitleButton");
        AssertButton(dungeonSelection.Find("TestExpeditionButton"),
            "TestExpeditionButton");
        AssertButton(dungeonSelection.Find("BackButton"), "BackButton");
        if (strategicHub.gameObject.activeSelf ||
            hubHome.gameObject.activeSelf ||
            dungeonSelection.gameObject.activeSelf)
        {
            throw new Exception(
                "Strategic Hub roots must default inactive in the prefab");
        }

        SerializedObject view = new SerializedObject(
            prefab.GetComponent<DRPGGameLoopView>());
        AssertAssigned(view, "strategicHubRoot");
        AssertAssigned(view, "hubHomeRoot");
        AssertAssigned(view, "dungeonSelectionRoot");
        AssertAssigned(view, "openDungeonSelectionButton");
        AssertAssigned(view, "testExpeditionButton");
        AssertAssigned(view, "hubBackButton");
        AssertAssigned(view, "returnToTitleButton");
        Transform settlement = prefab.transform.Find("SettlementRoot");
        if (settlement == null ||
            settlement.Find("BackpackPanel") == null ||
            settlement.Find("LootPanel") == null)
        {
            throw new Exception("Settlement dual-container layout is missing");
        }
        if (settlement.Find("BackpackPanel").GetComponent<DRPGInventoryDropZone>() == null)
            throw new Exception("Backpack must accept manual loot drag/drop");

        AssertListOwnsRowHeight(
            settlement.Find("ExperiencePanel/ExperienceViewport/ExperienceContent"),
            "experience");
        AssertListOwnsRowHeight(
            settlement.Find("LootPanel/LootViewport/LootContent"),
            "encounter loot");
        AssertListOwnsRowHeight(
            settlement.Find("BackpackPanel/BackpackViewport/BackpackContent"),
            "backpack");
        return "PASS DRPG title, Strategic Hub and settlement UGUI prefab";
    }

    private static void AssertButton(Transform target, string label)
    {
        if (target == null || target.GetComponent<Button>() == null)
            throw new Exception(label + " is missing or not interactive");
    }

    private static void AssertAssigned(
        SerializedObject owner,
        string propertyName)
    {
        SerializedProperty property = owner.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
        {
            throw new Exception(
                "DRPGGameLoopView reference is missing: " + propertyName);
        }
    }

    private static void AssertListOwnsRowHeight(
        Transform content,
        string label)
    {
        VerticalLayoutGroup layout = content != null
            ? content.GetComponent<VerticalLayoutGroup>()
            : null;
        if (layout == null || !layout.childControlHeight)
        {
            throw new Exception(
                "The " + label +
                " list must control row height to prevent stacked rows");
        }
    }
}
#endif
