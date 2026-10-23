#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGAutomaticSetup
{
    [MenuItem("Tools/ANKALUTE/DRPG/Install and Run Foundation Probes")]
    public static void RunFromMenu()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "[DRPGSetup] Exit Play Mode and wait for compilation before running setup.");
            return;
        }

        try
        {
            string installation = DRPGTestContentInstaller.Install();
            Debug.Log(installation);
            if (!installation.StartsWith(
                    "[DRPGInstaller] PASS",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(installation);
            }

            string probes = DRPGProbeRunner.RunAll();
            Debug.Log(probes);

            Debug.Log(
                "[DRPGSetup] COMPLETE. " +
                "The active scene is intentionally left dirty for review/save.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError(
                "[DRPGSetup] FAILED. Fix the reported issue, then run the menu command again.");
        }
    }
}
#endif
