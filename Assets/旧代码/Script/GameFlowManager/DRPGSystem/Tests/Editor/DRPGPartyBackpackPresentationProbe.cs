#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DRPGPartyBackpackPresentationProbe
{
    public static string Run()
    {
        Type snapshotType = Type.GetType(
            "DRPGPartyBackpackSnapshot, Assembly-CSharp");
        if (snapshotType == null)
            throw new Exception("DRPG backpack requires an inventory snapshot model");
        Type slotViewType = Type.GetType(
            "DRPGBackpackSlotView, Assembly-CSharp");
        if (slotViewType == null)
            throw new Exception("DRPG backpack requires reusable 100x100 slot views");

        MethodInfo create = snapshotType.GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static);
        if (create == null)
            throw new Exception("DRPG backpack snapshot requires a public Create entry point");

        var inventory = new DRPGPartyInventoryState(24, 80f);
        object snapshot = create.Invoke(null, new object[] { inventory });
        AssertEqual(24, (int)snapshotType.GetProperty("SlotCapacity").GetValue(snapshot));
        AssertEqual(0, (int)snapshotType.GetProperty("UsedSlots").GetValue(snapshot));
        AssertEqual(0, (int)snapshotType.GetProperty("EntryCount").GetValue(snapshot));

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DRPGPartyBackpackUIInstaller.PrefabPath);
        if (prefab == null || prefab.GetComponent<DRPGPartyBackpackController>() == null)
            throw new Exception("DRPG party backpack prefab is missing its controller");
        Type controllerType = typeof(DRPGPartyBackpackController);
        if (controllerType.GetMethod("ShowWithContainer") == null ||
            controllerType.GetMethod("TryNavigateBack") == null)
        {
            throw new Exception(
                "Backpack controller requires external-container and back-navigation contracts");
        }
        return "PASS DRPG party backpack presentation model";
    }

    private static void AssertEqual(int expected, int actual)
    {
        if (expected != actual)
            throw new Exception($"Expected {expected}, got {actual}");
    }
}
#endif
