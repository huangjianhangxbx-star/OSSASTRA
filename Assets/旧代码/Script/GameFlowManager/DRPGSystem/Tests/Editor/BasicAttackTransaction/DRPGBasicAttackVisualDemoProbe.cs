#if UNITY_EDITOR
using System;
using UnityEditor;

internal static class DRPGBasicAttackVisualDemoProbe
{
    // Break caught: the viewer is only a menu label and cannot stage a real
    // multi-suite run without entering Play Mode or touching F8.
    private static void ViewerCanArmWithoutChangingTheScene()
    {
        Type type = Type.GetType("DRPGBasicAttackVisualDemoMenu, Assembly-CSharp-Editor");
        Check(type != null, "Visual demo controller is missing.");
        var method = type.GetMethod("ArmForProbe", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Check(method != null, "Visual demo has no testable arming boundary.");
        string before = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        bool dirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;
        object result = method.Invoke(null, null);
        Check(result is bool && (bool)result && SessionState.GetInt("ANKALUTE.DRPG.VisualDemo.Stage.v1", -1) == 0 &&
            SessionState.GetBool("ANKALUTE.DRPG.VisualDemo.Armed.v1", false) &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == before &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty == dirty,
            "Arming did not prepare stage zero or changed the active scene.");
        SessionState.EraseBool("ANKALUTE.DRPG.VisualDemo.Armed.v1");
        SessionState.EraseInt("ANKALUTE.DRPG.VisualDemo.Stage.v1");
    }

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
#endif
