#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BattleV2PlayModeRunner
{
    private const string PendingGroupKey = "ANKALUTE.BattleV2.PendingGroup";
    private const string PendingRunKey = "ANKALUTE.BattleV2.PendingRun";
    private const string ReportPrefix = "ANKALUTE.BattleV2.Report.";

    static BattleV2PlayModeRunner()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += TryLaunchPending;
    }

    public static string Start(string group)
    {
        if (group != "A6" && group != "B3" && group != "C5")
            throw new ArgumentOutOfRangeException(nameof(group), group,
                "Unsupported BattleV2 play-mode group.");
        string runId = group + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        SessionState.SetString(PendingGroupKey, group);
        SessionState.SetString(PendingRunKey, runId);
        SessionState.SetString(ReportPrefix + runId, "WAITING: entering Play Mode");
        if (EditorApplication.isPlaying)
            EditorApplication.delayCall += TryLaunchPending;
        else
            EditorApplication.EnterPlaymode();
        return runId;
    }

    public static string GetReport(string runId)
    {
        return SessionState.GetString(ReportPrefix + runId, "UNKNOWN RUN");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += TryLaunchPending;
    }

    private static void TryLaunchPending()
    {
        if (!EditorApplication.isPlaying)
            return;
        string runId = SessionState.GetString(PendingRunKey, string.Empty);
        if (string.IsNullOrWhiteSpace(runId))
            return;
        string group = SessionState.GetString(PendingGroupKey, string.Empty);
        DRPGBattleDebugLauncher launcher =
            UnityEngine.Object.FindFirstObjectByType<DRPGBattleDebugLauncher>();
        if (launcher == null)
        {
            SessionState.SetString(ReportPrefix + runId,
                "FAIL " + group + ": no DRPGBattleDebugLauncher in active scene");
            ClearPending();
            return;
        }
        DRPGBattleStartResult result =
            launcher.StartBattleV2ForDevelopment();
        SessionState.SetString(ReportPrefix + runId,
            result == DRPGBattleStartResult.Started
                ? "RUNNING " + group + ": F8 battle started"
                : "FAIL " + group + ": start result " + result);
        ClearPending();
    }

    private static void ClearPending()
    {
        SessionState.EraseString(PendingRunKey);
        SessionState.EraseString(PendingGroupKey);
    }
}
#endif
