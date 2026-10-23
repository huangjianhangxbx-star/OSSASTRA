#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGFreeStrikeBattlefieldProbe
{
    public static string Run()
    {
        VerifyBattlefieldGeometryCanHitAnyEnemy();
        VerifyInteractiveResolutionCanRedirectToScreenHitTarget();
        return "PASS DRPG battlefield-wide free strike";
    }

    private static void VerifyBattlefieldGeometryCanHitAnyEnemy()
    {
        Type geometryType = Type.GetType(
            "DRPGFreeStrikeBattlefieldGeometry, Assembly-CSharp");
        Type targetType = Type.GetType(
            "DRPGFreeStrikeTargetGeometry, Assembly-CSharp");
        AssertNotNull(geometryType,
            "Free strike must classify hits in one battlefield coordinate space");
        AssertNotNull(targetType,
            "Free strike needs geometry for every living enemy, not one selected target");

        ConstructorInfo targetConstructor = targetType.GetConstructor(new[]
        {
            typeof(int),
            typeof(Rect)
        });
        MethodInfo classify = geometryType.GetMethod(
            "ClassifyTargets",
            BindingFlags.Public | BindingFlags.Static);
        AssertNotNull(targetConstructor,
            "Battlefield target geometry constructor is missing");
        AssertNotNull(classify,
            "Battlefield free strike needs one multi-target classification entry point");

        Array targets = Array.CreateInstance(targetType, 2);
        targets.SetValue(targetConstructor.Invoke(new object[]
        {
            2,
            new Rect(0.10f, 0.30f, 0.20f, 0.40f)
        }), 0);
        targets.SetValue(targetConstructor.Invoke(new object[]
        {
            3,
            new Rect(0.65f, 0.30f, 0.20f, 0.40f)
        }), 1);

        var point = new DRPGFreeStrikeSample(
            DRPGFreeStrikeShape.Point,
            new Vector2(0.75f, 0.50f),
            Vector2.right,
            0f,
            0f);
        Array pointHits = InvokeHits(classify, point, targets);
        AssertEqual(1, pointHits.Length,
            "A point attack over enemy B must not stay bound to enemy A");
        AssertHit(pointHits.GetValue(0), 3);

        var line = new DRPGFreeStrikeSample(
            DRPGFreeStrikeShape.Line,
            new Vector2(0.48f, 0.50f),
            Vector2.right,
            0.82f,
            0.04f);
        Array lineHits = InvokeHits(classify, line, targets);
        AssertEqual(2, lineHits.Length,
            "One battlefield slash crossing two enemies must resolve both hits");
        AssertHit(lineHits.GetValue(0), 2);
        AssertHit(lineHits.GetValue(1), 3);
    }

    private static void VerifyInteractiveResolutionCanRedirectToScreenHitTarget()
    {
        var created = new List<UnityEngine.Object>();
        try
        {
            DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
            created.Add(skill);
            SetField(skill, "skillId", "probe_full_battlefield_strike");
            SetField(skill, "power", 10f);
            SetField(skill, "recoveryDelay", 0f);
            SetField(skill, "executionMode", DRPGSkillExecutionMode.FreeStrike);
            SetField(skill, "targetRule", DRPGSkillTargetRule.BattlefieldPoint);

            BattleParticipantRuntime player = CreateRuntime(
                created, 1, "Player", 100f, 100f,
                DRPGBattleSide.Player, DRPGBattleRosterRole.PartyMember, skill);
            BattleParticipantRuntime enemyA = CreateRuntime(
                created, 2, "Enemy A", 100f, 1f,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, skill);
            BattleParticipantRuntime enemyB = CreateRuntime(
                created, 3, "Enemy B", 100f, 1f,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, skill);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemyA, enemyB });
            session.Start();

            MethodInfo prepare = typeof(DRPGBattleSession).GetMethod(
                "TryPrepareUntargetedPlayerCommand",
                BindingFlags.Public | BindingFlags.Instance);
            AssertNotNull(prepare,
                "Free-strike skill selection must start without selecting an enemy");
            object[] prepareArguments = { skill, null, null };
            AssertTrue((bool)prepare.Invoke(session, prepareArguments),
                prepareArguments[2] as string);
            var action = prepareArguments[1] as DRPGBattlePreparedAction;
            AssertNotNull(action, "Untargeted free strike did not prepare an action");
            AssertTrue(session.TryBeginInteractiveResolution(action, out string beginError),
                beginError);

            MethodInfo resolve = typeof(DRPGBattleSession).GetMethod(
                "TryResolveInteractiveImpactForTarget",
                new[]
                {
                    typeof(DRPGBattlePreparedAction),
                    typeof(int),
                    typeof(DRPGBattleImpactContext),
                    typeof(DRPGBattleHitResult).MakeByRefType(),
                    typeof(string).MakeByRefType()
                });
            AssertNotNull(resolve,
                "Interactive resolution must accept the enemy found under the cursor");
            object[] impactArguments =
            {
                action,
                enemyB.RuntimeId,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f),
                null,
                null
            };
            AssertTrue((bool)resolve.Invoke(session, impactArguments),
                impactArguments[4] as string);
            AssertApproximately(100f, enemyA.CurrentHp, 0.001f,
                "The hidden command anchor must not receive cursor damage");
            AssertApproximately(90f, enemyB.CurrentHp, 0.001f,
                "The enemy under the cursor must receive the free-strike hit");
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }
    }

    private static Array InvokeHits(
        MethodInfo classify,
        DRPGFreeStrikeSample sample,
        Array targets)
    {
        object value = classify.Invoke(null, new object[] { sample, targets });
        AssertNotNull(value, "Battlefield classification returned no hit collection");
        return value as Array ?? throw new Exception(
            "Battlefield classification must return a stable hit array");
    }

    private static void AssertHit(
        object hit,
        int expectedRuntimeId)
    {
        AssertNotNull(hit, "Battlefield hit entry is missing");
        Type type = hit.GetType();
        int runtimeId = (int)type.GetProperty("TargetRuntimeId").GetValue(hit);
        AssertEqual(expectedRuntimeId, runtimeId,
            "Battlefield hit resolved the wrong participant");
        AssertTrue(type.GetProperty("Grade") == null,
            "Battlefield body hits must not expose spatial weak-point grades");
    }

    private static BattleParticipantRuntime CreateRuntime(
        ICollection<UnityEngine.Object> created,
        int runtimeId,
        string displayName,
        float hp,
        float agility,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        DRPGSkillSO skill)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        created.Add(profile);
        SetField(profile, "displayName", displayName);
        SetField(profile, "maxHp", hp);
        SetField(profile, "agility", agility);
        SetField(profile, "baseTimelineDelay", 100f);
        SetField(profile, "baseRecoveryDelay", 0f);
        SetField(profile, "maxPosture", 100f);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(profile, side, role, runtimeId));
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(string.IsNullOrWhiteSpace(message)
                ? "Expected true"
                : message);
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertApproximately(
        float expected,
        float actual,
        float tolerance,
        string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
