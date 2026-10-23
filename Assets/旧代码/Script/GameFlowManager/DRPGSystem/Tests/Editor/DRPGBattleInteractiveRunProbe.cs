#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleInteractiveRunProbe
{
    public static string Run()
    {
        Type runType = Type.GetType(
            "BattleInteractiveActionRun, Assembly-CSharp");
        AssertNotNull(runType,
            "Removing the shared interaction run would duplicate defense and free-strike lifecycles");

        DRPGSkillSO defenseSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO strikeSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            SetField(defenseSkill, "specialImpactTimes",
                new List<float> { 0.65f, 1.10f, 1.55f });
            SetField(defenseSkill, "guardLeadSeconds", 0.42f);
            SetField(defenseSkill, "perfectLeadSeconds", 0.12f);
            SetField(strikeSkill, "freeStrikeDuration", 3f);
            SetField(strikeSkill, "freeStrikeCadence", 0.18f);

            object defense = InvokeFactory(runType, "CreateDefense", defenseSkill);
            InvokeAdvance(runType, defense, 0.23f);
            AssertTrue(InvokeBool(runType, defense, "SubmitGuard"),
                "The shared defense run must route an eligible guard press");
            InvokeAdvance(runType, defense, 0.42f);
            IList decisions = (IList)runType.GetMethod("DrainGuardDecisions")
                .Invoke(defense, null);
            AssertEqual(1, decisions.Count,
                "Advancing through one hit must emit one guard decision");
            AssertEqual("Guard",
                decisions[0].GetType().GetProperty("Grade").GetValue(decisions[0]).ToString(),
                "The run must preserve the underlying guard grade");

            MethodInfo freeStrikeFactory = runType.GetMethod(
                "CreateFreeStrike",
                new[]
                {
                    typeof(DRPGSkillSO),
                    typeof(DRPGResolvedFreeStrikeSpec)
                });
            AssertNotNull(freeStrikeFactory,
                "The free-strike run must consume the window's resolved spec");
            object strike = freeStrikeFactory.Invoke(
                null,
                new object[]
                {
                    strikeSkill,
                    DRPGFreeStrikeSpecResolver.Resolve(strikeSkill)
                });
            AssertTrue(InvokeBool(runType, strike, "TryAcceptStrike"),
                "The first free strike must be available immediately");
            InvokeAdvance(runType, strike, 0.10f);
            AssertFalse(InvokeBool(runType, strike, "TryAcceptStrike"),
                "The shared run must enforce free-strike cadence");
            InvokeAdvance(runType, strike, 0.08f);
            AssertTrue(InvokeBool(runType, strike, "TryAcceptStrike"),
                "The shared run must accept at the cadence boundary");
            InvokeAdvance(runType, strike, 2.82f);
            AssertTrue(ReadBool(runType, strike, "IsComplete"),
                "A free-strike run must settle on timeout");

            MethodInfo end = runType.GetMethod("End");
            AssertNotNull(end, "Every interaction exit needs one idempotent settlement method");
            end.Invoke(defense, new object[] { DRPGBattleInteractionEndReason.Cancelled });
            end.Invoke(defense, new object[] { DRPGBattleInteractionEndReason.Faulted });
            AssertEqual("Cancelled",
                runType.GetProperty("EndReason").GetValue(defense).ToString(),
                "The first terminal reason must win and remain stable");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(defenseSkill);
            UnityEngine.Object.DestroyImmediate(strikeSkill);
        }

        return "PASS DRPG interactive run";
    }

    private static object InvokeFactory(Type type, string name, DRPGSkillSO skill)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        AssertNotNull(method, $"Missing shared run factory {name}");
        return method.Invoke(null, new object[] { skill });
    }

    private static void InvokeAdvance(Type type, object target, float delta)
    {
        type.GetMethod("Advance").Invoke(target, new object[] { delta });
    }

    private static bool InvokeBool(Type type, object target, string name)
    {
        return (bool)type.GetMethod(name).Invoke(target, null);
    }

    private static bool ReadBool(Type type, object target, string name)
    {
        return (bool)type.GetProperty(name).GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertNotNull(field, $"Missing required field {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
