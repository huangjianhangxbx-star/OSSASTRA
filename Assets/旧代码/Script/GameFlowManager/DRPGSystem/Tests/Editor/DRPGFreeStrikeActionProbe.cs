#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGFreeStrikeActionProbe
{
    public static string Run()
    {
        VerifyCadence();
        VerifySessionImpacts();
        return "PASS DRPG free-strike action";
    }

    private static void VerifyCadence()
    {
        Type sequenceType = Type.GetType(
            "DRPGFreeStrikeTimingSequence, Assembly-CSharp");
        AssertNotNull(sequenceType,
            "Removing free-strike cadence would make click rate frame-rate dependent");
        object sequence = Activator.CreateInstance(
            sequenceType,
            new object[] { 3f, 0.18f });
        MethodInfo accept = sequenceType.GetMethod("TryAccept");
        MethodInfo advance = sequenceType.GetMethod("Advance");
        PropertyInfo complete = sequenceType.GetProperty("IsComplete");
        AssertNotNull(accept, "Free-strike timing needs one input-acceptance interface");
        AssertNotNull(advance, "Free-strike timing must advance from unscaled elapsed time");

        AssertTrue((bool)accept.Invoke(sequence, new object[] { 0f }),
            "The first strike must be accepted immediately");
        AssertFalse((bool)accept.Invoke(sequence, new object[] { 0.10f }),
            "A strike inside the 0.18 second cadence must be rejected");
        AssertTrue((bool)accept.Invoke(sequence, new object[] { 0.18f }),
            "The inclusive cadence boundary must accept a strike");
        advance.Invoke(sequence, new object[] { 3f });
        AssertTrue((bool)complete.GetValue(sequence),
            "The sequence must complete at its configured duration");
        AssertFalse((bool)accept.Invoke(sequence, new object[] { 3f }),
            "A completed sequence must reject additional strikes");
    }

    private static void VerifySessionImpacts()
    {
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            100f,
            DRPGBattleSide.Player);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            1f,
            DRPGBattleSide.Enemy);
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            SetField(skill, "skillId", "test_point_free_strike");
            SetField(skill, "power", 20f);
            SetField(skill, "recoveryDelay", 0f);
            SetEnumField(skill, "executionMode", "FreeStrike");
            SetField(skill, "freeStrikePostureCost", 12f);
            SetField(playerProfile, "skills", new List<DRPGSkillSO> { skill });

            BattleParticipantRuntime player = CreateParticipant(
                1, playerProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember);
            BattleParticipantRuntime enemy = CreateParticipant(
                2, enemyProfile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            session.Start();

            var command = new DRPGBattleCommand(
                player.RuntimeId,
                enemy.RuntimeId,
                skill);
            AssertTrue(session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction action,
                out string error), error);
            AssertTrue(session.TryBeginInteractiveResolution(action, out error), error);

            Spend(session, action, 12f, false);
            float hpBeforeBodyHit = enemy.CurrentHp;
            ResolveStrike(session, action, "Body", 1f);
            AssertNear(hpBeforeBodyHit - 20f, enemy.CurrentHp,
                "A body strike must apply the skill's resolved base damage");

            Spend(session, action, 12f, false);
            float hpBeforeMiss = enemy.CurrentHp;
            ResolveStrike(session, action, "Miss", 0f);
            AssertNear(hpBeforeMiss, enemy.CurrentHp,
                "A missed free strike must consume posture but deal no damage");
            AssertNear(76f, player.CurrentPosture,
                "Two accepted inputs must each spend 12 attacker posture");

            Spend(session, action, 200f, true);
            AssertNear(0f, player.CurrentPosture,
                "Free strikes must clamp posture at zero");
            AssertEqual(
                DRPGPostureBreakState.ExposedPendingSkip,
                player.PostureBreakState,
                "Exhausting free-strike posture must enter the explicit break lifecycle");

            AssertTrue(session.TryEndInteractiveResolution(
                action,
                out DRPGBattleActionResolution resolution,
                out error), error);
            AssertEqual(2, resolution.Hits.Count,
                "Only submitted impacts, including a miss, belong to the resolution");
            AssertTrue(session.TryCompleteAction(action, out error), error);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }
    }

    private static void Spend(
        DRPGBattleSession session,
        DRPGBattlePreparedAction action,
        float amount,
        bool expectedExhausted)
    {
        MethodInfo spend = typeof(DRPGBattleSession).GetMethod(
            "TrySpendInteractiveActorPosture");
        AssertNotNull(spend,
            "The session must own free-strike posture spending");
        object[] args = { action, amount, false, null };
        AssertTrue((bool)spend.Invoke(session, args),
            args[3] as string ?? "Posture spending failed");
        AssertEqual(expectedExhausted, (bool)args[2],
            "Posture spending reported the wrong exhausted state");
    }

    private static void ResolveStrike(
        DRPGBattleSession session,
        DRPGBattlePreparedAction action,
        string grade,
        float multiplier)
    {
        DRPGStrikeGrade parsed = (DRPGStrikeGrade)Enum.Parse(
            typeof(DRPGStrikeGrade), grade);
        DRPGBattleImpactContext context =
            DRPGBattleImpactContext.ForStrike(parsed, multiplier);
        AssertTrue(session.TryResolveInteractiveImpact(
            action,
            context,
            out DRPGBattleHitResult hit,
            out string error), error);
        AssertEqual(parsed, hit.StrikeGrade,
            "The hit result must preserve free-strike classification");
    }

    private static DRPGCombatantProfileSO CreateProfile(
        float agility,
        DRPGBattleSide side)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "maxHp", 100f);
        SetField(profile, "maxPosture", 100f);
        SetField(profile, "postureRecoveryPerTurn", 0f);
        SetField(profile, "agility", agility);
        SetField(profile, "baseTimelineDelay", 1000f);
        SetField(profile, "baseRecoveryDelay", 0f);
        return profile;
    }

    private static BattleParticipantRuntime CreateParticipant(
        int runtimeId,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(profile, side, role, 0));
    }

    private static void SetEnumField(object target, string fieldName, string value)
    {
        FieldInfo field = RequireField(target.GetType(), fieldName);
        field.SetValue(target, Enum.Parse(field.FieldType, value));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        RequireField(target.GetType(), fieldName).SetValue(target, value);
    }

    private static FieldInfo RequireField(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertNotNull(field, $"Missing required field {type.Name}.{fieldName}");
        return field;
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

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Mathf.Abs(expected - actual) > 0.001f)
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
