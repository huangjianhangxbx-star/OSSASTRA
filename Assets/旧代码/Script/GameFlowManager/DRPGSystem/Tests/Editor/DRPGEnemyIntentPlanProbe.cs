#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGEnemyIntentPlanProbe
{
    public static string Run()
    {
        DRPGSkillSO hiddenSkill = CreateSkill(
            "enemy_hidden_attack",
            2,
            DRPGEnemyTargetDisclosureMode.Hidden);
        DRPGSkillSO disclosedSkill = CreateSkill(
            "enemy_disclosed_attack",
            2,
            DRPGEnemyTargetDisclosureMode.RevealAtCommit);

        try
        {
            AssertEqual(
                DRPGEnemyTargetDisclosureMode.Hidden,
                hiddenSkill.EnemyTargetDisclosureMode,
                "Enemy target disclosure must be opt-in");

            DRPGEnemyActionPlan plan = DRPGEnemyActionPlan.CreateAttack(
                11L,
                7,
                hiddenSkill,
                0f,
                12f,
                hiddenSkill.EnemyWarningSteps);

            AssertEqual(2, plan.SampleWarningValue(1f),
                "Early windup must display the authored maximum warning");
            AssertEqual(1, plan.SampleWarningValue(8f),
                "Warning sampling must be proportional and may skip stages");
            AssertEqual(0, plan.SampleWarningValue(12f),
                "Ready time must sample warning zero");

            DRPGEnemyActionPlan sampled = plan.WithSampledWarning(1f);
            DRPGEnemyActionPlan closeSample = sampled.WithSampledWarning(2f);
            AssertNotSame(plan, sampled,
                "Sampling must return a new immutable plan");
            AssertEqual(2, sampled.CurrentWarningValue,
                "The sampled warning must be stored on the new plan");
            AssertEqual(2, closeSample.CurrentWarningValue,
                "Close player turns may legitimately retain the same warning value");

            DRPGEnemyActionPlan hiddenCommit = sampled.CommitTarget(42, 5L);
            DRPGEnemyActionPlanSnapshot hiddenSnapshot =
                hiddenCommit.CreatePublicSnapshot();
            AssertEqual(0, hiddenSnapshot.DisclosedTargetRuntimeId,
                "Hidden plans must never leak their committed target");
            AssertEqual(DRPGEnemyPlanState.AwaitingPlayerResponse,
                hiddenSnapshot.State,
                "A warning-zero commit must wait for the player response gate");

            DRPGEnemyActionPlan disclosedPlan =
                DRPGEnemyActionPlan.CreateAttack(
                    12L,
                    8,
                    disclosedSkill,
                    0f,
                    12f,
                    disclosedSkill.EnemyWarningSteps)
                .CommitTarget(84, 9L);
            DRPGEnemyActionPlanSnapshot disclosedSnapshot =
                disclosedPlan.CreatePublicSnapshot();
            AssertEqual(84, disclosedSnapshot.DisclosedTargetRuntimeId,
                "RevealAtCommit plans must expose exactly the committed target");

            DRPGEnemyActionPlan executable =
                disclosedPlan.WithSatisfiedPlayerResponse(10L);
            AssertEqual(DRPGEnemyPlanState.Executable, executable.State,
                "A later completed player action must release the response gate");
            AssertThrows<InvalidOperationException>(
                () => executable.WithSampledWarning(4f),
                "An executable plan must reject backward windup transitions");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hiddenSkill);
            UnityEngine.Object.DestroyImmediate(disclosedSkill);
        }

        return "PASS DRPG enemy intent plan contracts";
    }

    private static DRPGSkillSO CreateSkill(
        string id,
        int warningSteps,
        DRPGEnemyTargetDisclosureMode disclosureMode)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "enemyWarningSteps", warningSteps);
        SetField(skill, "enemyTargetDisclosureMode", disclosureMode);
        return skill;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertNotSame(object left, object right, string message)
    {
        if (ReferenceEquals(left, right))
            throw new Exception(message);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new Exception(message);
    }
}
#endif
