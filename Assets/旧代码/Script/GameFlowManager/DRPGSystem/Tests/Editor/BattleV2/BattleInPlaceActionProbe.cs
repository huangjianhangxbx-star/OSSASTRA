#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;

public static class BattleInPlaceActionProbe
{
    public static void RunAll()
    {
        RecoveryButtonIsASeparateNonSkillControl();
    }

    private static void RecoveryButtonIsASeparateNonSkillControl()
    {
        var root = new GameObject("RecoveryBar");
        try
        {
            Button button = root.AddComponent<Button>();
            BattleFixedSkillBar bar = root.AddComponent<BattleFixedSkillBar>();
            typeof(BattleFixedSkillBar).GetField("postureRecoveryButton",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(bar, button);
            int calls = 0;
            bar.PostureRecoveryRequested += () => calls++;
            bar.SetInputEnabled(true);
            button.onClick.Invoke();
            Check(calls == 1 && bar.SelectedSlot == -1,
                "posture recovery entered or selected a four-slot attack");
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
