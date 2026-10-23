#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGTelegraphedActionProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO enemyProfile = CreateProfile(100f);
        DRPGCombatantProfileSO playerProfile = CreateProfile(1f);
        SetField(playerProfile, "baseTimelineDelay", 3000f);
        DRPGSkillSO special = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            ConfigureSpecial(special);
            SetField(
                enemyProfile,
                "skills",
                new List<DRPGSkillSO> { special });

            BattleParticipantRuntime enemy = CreateParticipant(
                1, enemyProfile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            BattleParticipantRuntime player = CreateParticipant(
                2, playerProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { enemy, player });
            session.Start();

            AssertSame(enemy, session.CurrentActor,
                "The fast enemy must own the first test turn");
            AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand telegraph),
                "The enemy must create its special telegraph command");
            AssertStage("Telegraph", telegraph);
            float hpBefore = player.CurrentHp;
            AssertTrue(session.TrySubmitCommand(telegraph, out string error), error);
            AssertNear(hpBefore, player.CurrentHp,
                "A telegraph turn must not deal damage");
            AssertEqual(1, ReadPendingCount(session),
                "The locked target must persist after the telegraph turn");

            AssertSame(enemy, session.CurrentActor,
                "The configured enemy must reach its release before the slow target");
            AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand release),
                "The next enemy turn must create the pending release command");
            AssertStage("Release", release);
            AssertEqual(player.RuntimeId, release.TargetRuntimeId,
                "A special release must retain its originally locked target");
            AssertTrue(session.TryPrepareCommand(
                release,
                out DRPGBattlePreparedAction action,
                out error), error);

            MethodInfo begin = RequireMethod(
                typeof(DRPGBattleSession), "TryBeginInteractiveResolution");
            object[] beginArgs = { action, null };
            AssertTrue((bool)begin.Invoke(session, beginArgs),
                beginArgs[1] as string ?? "Interactive resolution did not begin");

            ResolveGuardImpact(session, action, "Miss", 1f, 34f);
            ResolveGuardImpact(session, action, "Guard", 0.45f, 22f);
            float hpBeforePerfect = player.CurrentHp;
            ResolveGuardImpact(session, action, "Perfect", 0f, 0f);
            AssertNear(hpBeforePerfect, player.CurrentHp,
                "Perfect guard must negate the complete hit");
            AssertNear(44f, player.CurrentPosture,
                "Miss plus guard must apply literal posture damage and perfect must apply none");

            MethodInfo end = RequireMethod(
                typeof(DRPGBattleSession), "TryEndInteractiveResolution");
            object[] endArgs = { action, null, null };
            AssertTrue((bool)end.Invoke(session, endArgs),
                endArgs[2] as string ?? "Interactive resolution did not end");
            var resolution = endArgs[1] as DRPGBattleActionResolution;
            AssertNotNull(resolution,
                "Ending an interaction must return its aggregate resolution");
            AssertEqual(3, resolution.Hits.Count,
                "The three-hit special must aggregate all resolved impacts");
            AssertTrue(session.TryCompleteAction(action, out error), error);
            AssertEqual(0, ReadPendingCount(session),
                "Completing the release must consume its pending telegraph");

            VerifyDeadLockedTargetFizzles(
                enemyProfile,
                playerProfile,
                special);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(special);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
        }

        return "PASS DRPG telegraphed action";
    }

    private static void VerifyDeadLockedTargetFizzles(
        DRPGCombatantProfileSO enemyProfile,
        DRPGCombatantProfileSO playerProfile,
        DRPGSkillSO special)
    {
        SetField(special, "recoveryDelay", 1000f);
        DRPGCombatantProfileSO doomedTargetProfile = CreateProfile(20f);
        BattleParticipantRuntime enemy = CreateParticipant(
            11, enemyProfile, DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy);
        BattleParticipantRuntime lockedTarget = CreateParticipant(
            12, doomedTargetProfile, DRPGBattleSide.Player,
            DRPGBattleRosterRole.PartyMember);
        DRPGStatusDefinitionSO fatalTurnStart =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        SetField(fatalTurnStart, "statusId", "telegraph_probe_fatal_turn_start");
        SetField(fatalTurnStart, "turnStartHpDelta", -100f);
        DRPGBattleStateMutationFixture.PrepareStatus(
            lockedTarget,
            DRPGResolvedStatusSpec.From(fatalTurnStart));
        UnityEngine.Object.DestroyImmediate(fatalTurnStart);
        BattleParticipantRuntime survivor = CreateParticipant(
            13, playerProfile, DRPGBattleSide.Player,
            DRPGBattleRosterRole.PartyMember);
        try
        {
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { enemy, lockedTarget, survivor },
                randomSource: new FirstTargetRandom());
            session.Start();
            AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand warning),
                "The stale-target scenario needs a telegraph");
            AssertEqual(lockedTarget.RuntimeId, warning.TargetRuntimeId,
                "The deterministic telegraph must lock the doomed target");
            AssertTrue(session.TrySubmitCommand(warning, out string error), error);
            AssertTrue(!lockedTarget.IsAlive,
                "The locked target must die through its real turn-start status before release");

            AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand release),
                "A dead locked target must still produce its pending release");
            AssertTrue(session.TryPrepareCommand(
                release,
                out DRPGBattlePreparedAction action,
                out error),
                "A stale release should fizzle instead of aborting the battle: " + error);
            AssertTrue(session.TryBeginInteractiveResolution(action, out error),
                "A stale release must enter a zero-hit interactive transaction: " + error);
            AssertTrue(session.TryEndInteractiveResolution(
                action,
                out DRPGBattleActionResolution resolution,
                out error), error);
            AssertEqual(0, resolution.Hits.Count,
                "A release whose locked target died must resolve with zero hits");
            AssertTrue(session.TryCompleteAction(action, out error), error);
            AssertEqual(0, ReadPendingCount(session),
                "A fizzled release must still consume the stale telegraph");
            AssertEqual(DRPGBattleOutcome.None, session.Outcome,
                "The surviving teammate must keep the battle alive");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(doomedTargetProfile);
        }
    }

    private static void ResolveGuardImpact(
        DRPGBattleSession session,
        DRPGBattlePreparedAction action,
        string grade,
        float damageMultiplier,
        float postureDamage)
    {
        Type contextType = Type.GetType(
            "DRPGBattleImpactContext, Assembly-CSharp");
        Type gradeType = Type.GetType(
            "DRPGGuardGrade, Assembly-CSharp");
        AssertNotNull(contextType,
            "Interactive impacts need a UGUI-free rule context");
        object gradeValue = Enum.Parse(gradeType, grade);
        MethodInfo factory = contextType.GetMethod(
            "ForGuard",
            BindingFlags.Public | BindingFlags.Static);
        AssertNotNull(factory,
            "Guard impacts need one explicit context factory");
        object context = factory.Invoke(
            null,
            new[] { gradeValue, damageMultiplier, postureDamage });

        MethodInfo resolve = RequireMethod(
            typeof(DRPGBattleSession), "TryResolveInteractiveImpact");
        object[] args = { action, context, null, null };
        AssertTrue((bool)resolve.Invoke(session, args),
            args[3] as string ?? "Interactive impact failed");
        DRPGBattleHitResult hit = args[2] as DRPGBattleHitResult;
        AssertNotNull(hit, "Every accepted guard impact must return one hit result");
        PropertyInfo guardGrade = hit.GetType().GetProperty("GuardGrade");
        AssertNotNull(guardGrade, "Hit results must preserve the guard grade");
        AssertEqual(grade, guardGrade.GetValue(hit).ToString(),
            "The hit result lost its guard grade");
    }

    private static void ConfigureSpecial(DRPGSkillSO skill)
    {
        SetField(skill, "skillId", "test_enemy_special");
        SetField(skill, "power", 20f);
        SetField(skill, "recoveryDelay", 0f);
        FieldInfo executionMode = RequireField(skill.GetType(), "executionMode");
        executionMode.SetValue(
            skill,
            Enum.Parse(executionMode.FieldType, "TelegraphedSpecial"));
    }

    private static DRPGCombatantProfileSO CreateProfile(float agility)
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

    private static void AssertStage(string expected, DRPGBattleCommand command)
    {
        PropertyInfo stage = command.GetType().GetProperty("Stage");
        AssertNotNull(stage,
            "Commands must distinguish telegraph from release without presentation guesses");
        AssertEqual(expected, stage.GetValue(command).ToString(),
            "The enemy command used the wrong execution stage");
    }

    private static int ReadPendingCount(DRPGBattleSession session)
    {
        PropertyInfo pending = session.GetType().GetProperty("PendingTelegraphs");
        AssertNotNull(pending,
            "The session must expose an immutable pending-telegraph snapshot");
        object value = pending.GetValue(session);
        PropertyInfo count = value.GetType().GetProperty("Count");
        return Convert.ToInt32(count.GetValue(value));
    }

    private static MethodInfo RequireMethod(Type type, string name)
    {
        MethodInfo method = type.GetMethod(name);
        AssertNotNull(method, $"Missing required method {type.Name}.{name}");
        return method;
    }

    private static FieldInfo RequireField(Type type, string name)
    {
        FieldInfo field = type.GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertNotNull(field, $"Missing required field {type.Name}.{name}");
        return field;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        RequireField(target.GetType(), fieldName).SetValue(target, value);
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

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
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

    private sealed class FirstTargetRandom : IDRPGBattleRandom
    {
        public int Next(int exclusiveMax) => 0;
    }
}
#endif
