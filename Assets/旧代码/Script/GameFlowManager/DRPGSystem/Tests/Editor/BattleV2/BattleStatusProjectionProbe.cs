#if UNITY_EDITOR
using System;
using UnityEngine;

public static class BattleStatusProjectionProbe
{
    public static void RunAll()
    {
        EnemyStatusNeverLeaksResourcePips();
        IntentUnknownPanelUsesExplicitDash();
    }

    private static void EnemyStatusNeverLeaksResourcePips()
    {
        var ownerObject = new GameObject("StatusProbe", typeof(RectTransform));
        try
        {
            BattleWorldActorStatusView view = BattleWorldActorStatusView.Create(
                ownerObject.GetComponent<RectTransform>());
            view.BindBattleV2(3, 10, 1005, null, null, null);
            BattlePipRow pips = view.GetComponentInChildren<BattlePipRow>(true);
            Check(pips != null && pips.VisiblePipCount == 0,
                "enemy resource leaked");
            Check(view.CurrentPostureLabel == "1005",
                "posture silently clamped or truncated");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    private static void IntentUnknownPanelUsesExplicitDash()
    {
        var root = new GameObject("IntentProbe", typeof(RectTransform));
        try
        {
            BattleIntentView view = BattleIntentView.Create(
                root.GetComponent<RectTransform>());
            view.Bind(new BattleIntentSnapshot(
                "enemy-preview", "撕裂", null, "physical", null, 2, null));
            Check(view.RenderedPanel == "—", "unknown enemy panel was fabricated");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
