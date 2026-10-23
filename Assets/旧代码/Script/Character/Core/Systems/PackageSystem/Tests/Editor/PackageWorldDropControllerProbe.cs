#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine.EventSystems;

public static class PackageWorldDropControllerProbe
{
    public static string Run()
    {
        var type = typeof(PackageUIController);
        var pointerEndDrag = type.GetMethod(
            "EndDrag",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(PointerEventData) },
            null);
        var cancelEndDrag = type.GetMethod(
            "EndDrag",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);
        var spawnerField = type.GetField(
            "worldItemDropSpawner",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var policyField = type.GetField(
            "worldDropPolicy",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (pointerEndDrag == null || cancelEndDrag == null ||
            spawnerField == null || policyField == null)
            throw new Exception("World-drop drag integration is missing");

        return "PASS world drop controller API";
    }
}
#endif
