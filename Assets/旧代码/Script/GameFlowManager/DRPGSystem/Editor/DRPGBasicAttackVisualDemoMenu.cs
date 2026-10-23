#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DRPGBasicAttackVisualDemoMenu
{
    private const string ArmedKey = "ANKALUTE.DRPG.VisualDemo.Armed.v1";
    private const string StageKey = "ANKALUTE.DRPG.VisualDemo.Stage.v1";
    private const string StatusKey = "ANKALUTE.DRPG.VisualDemo.Status.v1";
    private const string MenuRoot = "Tools/ANKALUTE/DRPG/Basic Attack/";

    static DRPGBasicAttackVisualDemoMenu()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    [MenuItem(MenuRoot + "Watch Current Visual Demo", priority = 1)]
    private static void Watch()
    {
        if (!ArmForProbe()) return;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        if (EditorApplication.isPlaying) BeginCurrentStage();
        else EditorApplication.EnterPlaymode();
    }

    [MenuItem(MenuRoot + "Show Visual Demo Status", priority = 2)]
    private static void ShowStatus()
    {
        Debug.Log("[ANKALUTE Visual Demo] " + SessionState.GetString(StatusKey, "Not started"));
    }

    [MenuItem(MenuRoot + "Stop Visual Demo", priority = 3)]
    private static void Stop()
    {
        SessionState.SetBool(ArmedKey, false);
        SessionState.SetString(StatusKey, "Stopped by user");
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    internal static bool ArmForProbe()
    {
        if (SessionState.GetBool(ArmedKey, false)) return false;
        SessionState.SetInt(StageKey, 0);
        SessionState.SetString(StatusKey, "Armed: waiting for Play Mode");
        SessionState.SetBool(ArmedKey, true);
        return true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ArmedKey, false))
            BeginCurrentStage();
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ArmedKey, false))
        {
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetString(StatusKey, "Interrupted: Play Mode ended");
        }
    }

    private static void BeginCurrentStage()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ArmedKey, false)) return;
        int stage = SessionState.GetInt(StageKey, 0);
        try
        {
            string name;
            if (stage == 0)
            {
                name = "1/3 TurnEntry and lifecycle";
                DRPGTurnEntryModuleProductionProbe.StartProduction();
            }
            else if (stage == 1)
            {
                name = "2/3 Stage9 settlement and deaths";
                DRPGSettlementProductionProbe.StartProduction();
            }
            else if (stage == 2)
            {
                name = "3/3 hit, reaction, return and formation";
                DRPGBasicAttackReturnBarrierProbe.StartProduction();
            }
            else
            {
                Complete("Completed: all 3 visual suites");
                return;
            }
            SessionState.SetString(StatusKey, name);
            Debug.Log("[ANKALUTE Visual Demo] " + name);
        }
        catch (System.Exception error) { Complete("Failed to start: " + error.Message); }
    }

    private static void Poll()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ArmedKey, false)) return;
        int stage = SessionState.GetInt(StageKey, 0);
        string status = stage == 0 ? DRPGTurnEntryModuleProductionProbe.Status :
            stage == 1 ? DRPGSettlementProductionProbe.Status :
            stage == 2 ? DRPGBasicAttackReturnBarrierProbe.ProductionStatus : string.Empty;
        if (status.StartsWith("FAIL") || status.StartsWith("Interrupted"))
        { Complete("Stopped at suite " + (stage + 1) + ": " + status); return; }
        if (!status.StartsWith("PASS")) return;
        SessionState.SetInt(StageKey, stage + 1);
        BeginCurrentStage();
    }

    private static void Complete(string status)
    {
        SessionState.SetBool(ArmedKey, false);
        SessionState.SetString(StatusKey, status);
        Debug.Log("[ANKALUTE Visual Demo] " + status);
    }
}
#endif
