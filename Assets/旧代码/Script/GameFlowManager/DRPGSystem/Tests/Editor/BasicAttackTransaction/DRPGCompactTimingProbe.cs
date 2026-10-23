#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGCompactTimingProbe
{
    [MenuItem("Tools/ANKALUTE/DRPG/Compact Battle/Run Timing Probe")]
    public static void Run()
    {
        var timing = new DRPGBasicAttackTimingSnapshot(.04f, .14f, .10f, .07f, .13f, .22f, .30f, 0f);
        Require(Input(timing).IsValid, "Compact finite timing rejected by old readability floors.");
        Require(!Input(new DRPGBasicAttackTimingSnapshot(float.PositiveInfinity,.14f,.1f,.07f,.13f,.22f,.3f,0)).IsValid,
            "Infinite timing must not be accepted.");
        Require(!Input(new DRPGBasicAttackTimingSnapshot(.04f,.14f,-.1f,.07f,.13f,.22f,.3f,0)).IsValid,
            "Negative windup must not be accepted.");
        var profile = ScriptableObject.CreateInstance<DRPGBasicAttackTimingProfile>();
        try
        {
            var so = new SerializedObject(profile);
            so.FindProperty("announcementSeconds").floatValue = .04f;
            so.FindProperty("windupMinimumSeconds").floatValue = .10f;
            so.FindProperty("returnSeconds").floatValue = .22f;
            so.FindProperty("emptySettlementSeconds").floatValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            var frozen = profile.Freeze();
            Require(Math.Abs(frozen.AnnouncementSeconds-.04f)<.001f && Math.Abs(frozen.ReturnSeconds-.22f)<.001f && frozen.EmptySettlementSeconds==0,
                "Profile silently restores legacy timing floors.");
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); }
        Debug.Log("PASS compact timing: finite configurable duration and invalid input protection.");
    }
    private static DRPGBasicAttackPresentationFreezeInput Input(DRPGBasicAttackTimingSnapshot timing) =>
        new DRPGBasicAttackPresentationFreezeInput(new ActionTracePosition(-2,0,0), new ActionTracePosition(-1,0,0),
            new ActionTracePosition(2,0,0),new ActionTracePosition(1,0,0),1,-1,
            DRPGBasicAttackVisualBaseline.DefaultIdle, DRPGBasicAttackVisualBaseline.DefaultIdle, timing,
            new DRPGBasicAttackSemanticSnapshot("Attack",EAnimationLogicState.Skill_1,"attack","Impact","Impact"));
    private static void Require(bool ok, string message) { if(!ok) throw new InvalidOperationException(message); }
}
#endif

