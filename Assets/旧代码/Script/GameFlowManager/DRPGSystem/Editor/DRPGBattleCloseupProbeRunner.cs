#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

internal static class DRPGBattleCloseupProbeRunner
{
    [MenuItem("Tools/ANKALUTE/DRPG/Close-up/Run Geometry Probe")]
    internal static void RunGeometryFromMenu()
    {
        DRPGBattleCloseupGeometryProbe.Run();
        DRPGBattleCloseupOwnershipProbe.Run();
        Debug.Log("PASS battle close-up geometry probe.");
    }

    internal static void RunGeometryFromCommandLine()
    {
        try
        {
            DRPGBattleCloseupGeometryProbe.Run();
            DRPGBattleCloseupOwnershipProbe.Run();
            Debug.Log("PASS battle close-up geometry probe.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
#endif
