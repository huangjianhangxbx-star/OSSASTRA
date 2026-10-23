#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class BattleSkillRoutingProbe
{
    public static void RunAll()
    {
        SelectionPreservesConfiguredSlotAndSkillIdentity();
        ModePolicyRejectsManualExecutionModes();
    }

    private static void SelectionPreservesConfiguredSlotAndSkillIdentity()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            var token = new ActorTurnToken(7, 3, new ParticipantId(11));
            var selection = new DRPGSkillSelection(token, 2, skill, 19);
            Check(selection.Turn == token && selection.SlotIndex == 2 &&
                ReferenceEquals(selection.Skill, skill) &&
                selection.PrimaryTargetRuntimeId == 19,
                "configured selection lost slot, token, skill or target identity");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    private static void ModePolicyRejectsManualExecutionModes()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            var serialized = new SerializedObject(skill);
            SerializedProperty mode = serialized.FindProperty("executionMode");
            mode.enumValueIndex = (int)DRPGSkillExecutionMode.FreeStrike;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(!DRPGBattleSkillModePolicy.IsAllowed(skill),
                "manual free-strike skill was admitted");
            mode = new SerializedObject(skill).FindProperty("executionMode");
            var standard = new SerializedObject(skill);
            standard.FindProperty("executionMode").enumValueIndex =
                (int)DRPGSkillExecutionMode.Standard;
            standard.ApplyModifiedPropertiesWithoutUndo();
            Check(DRPGBattleSkillModePolicy.IsAllowed(skill),
                "standard skill was rejected");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
