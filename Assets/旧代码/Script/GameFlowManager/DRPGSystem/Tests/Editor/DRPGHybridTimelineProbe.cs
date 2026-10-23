#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGHybridTimelineProbe
{
    public static string Run()
    {
        DRPGSkillSO attack = CreateAttack();
        var profiles = new List<DRPGCombatantProfileSO>();
        try
        {
            VerifyWindupEnemiesStayOffTimeline(attack, profiles);
            VerifyWarningZeroEnemyInsertsAfterCurrentPlayer(attack, profiles);
        }
        finally
        {
            for (int i = 0; i < profiles.Count; i++)
                UnityEngine.Object.DestroyImmediate(profiles[i]);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS DRPG hybrid timeline";
    }

    private static void VerifyWindupEnemiesStayOffTimeline(
        DRPGSkillSO attack,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime playerA = Create(
            1, CreateProfile(attack, 10f, 100f, profiles),
            DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0);
        BattleParticipantRuntime playerB = Create(
            2, CreateProfile(attack, 10f, 120f, profiles),
            DRPGBattleSide.Player, DRPGBattleRosterRole.PartyMember, 1);
        BattleParticipantRuntime playerC = Create(
            3, CreateProfile(attack, 10f, 140f, profiles),
            DRPGBattleSide.Player, DRPGBattleRosterRole.PartyMember, 2);
        BattleParticipantRuntime enemyA = Create(
            4, CreateProfile(attack, 10f, 800f, profiles),
            DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0);
        BattleParticipantRuntime enemyB = Create(
            5, CreateProfile(attack, 10f, 900f, profiles),
            DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 1);
        var session = DRPGBattleHeadlessSessionFactory.Create(
            new[] { playerA, enemyA, playerB, enemyB, playerC });
        session.Start();

        IReadOnlyList<DRPGBattleParticipantViewModel> timeline =
            new DRPGBattleViewModel(session).Timeline;
        AssertIds(new[] { 1, 2, 3 }, timeline,
            "Warning 1/2 enemies must stay off the hybrid action bar");
    }

    private static void VerifyWarningZeroEnemyInsertsAfterCurrentPlayer(
        DRPGSkillSO attack,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime currentPlayer = Create(
            11, CreateProfile(attack, 10f, 200f, profiles),
            DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0);
        BattleParticipantRuntime futurePlayer = Create(
            12, CreateProfile(attack, 10f, 300f, profiles),
            DRPGBattleSide.Player, DRPGBattleRosterRole.PartyMember, 1);
        BattleParticipantRuntime enemy = Create(
            13, CreateProfile(attack, 30f, 50f, profiles),
            DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0);
        var session = DRPGBattleHeadlessSessionFactory.Create(
            new[] { futurePlayer, enemy, currentPlayer });
        session.Start();

        var model = new DRPGBattleViewModel(session);
        AssertIds(new[] { 11, 13, 12 }, model.Timeline,
            "A warning-zero enemy portrait must insert after the current ally");

        var factory = new BattlePresentationRequestFactory();
        BattlePresentationRequest request = factory.CreateTurnActivated(
            1L,
            1L,
            1L,
            session);
        AssertIds(new[] { 11, 13, 12 }, request.TimelineRuntimeIds,
            "Presentation requests must consume the sanitized hybrid timeline");

        PropertyInfo leakedTarget = typeof(DRPGBattleParticipantViewModel)
            .GetProperty("TargetRuntimeId");
        if (leakedTarget != null)
        {
            throw new Exception(
                "Timeline cards must not gain a target identifier property");
        }
    }

    private static DRPGSkillSO CreateAttack()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", "hybrid_timeline_attack");
        SetField(skill, "power", 5f);
        SetField(skill, "prepareDelay", 0f);
        SetField(skill, "recoveryDelay", 0f);
        SetField(skill, "enemyWarningSteps", 2);
        SetField(skill, "executionMode", DRPGSkillExecutionMode.Standard);
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO attack,
        float agility,
        float baseDelay,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "baseTimelineDelay", baseDelay);
        SetField(profile, "baseRecoveryDelay", 0f);
        SetField(profile, "skills", new List<DRPGSkillSO> { attack });
        profiles.Add(profile);
        return profile;
    }

    private static BattleParticipantRuntime Create(
        int runtimeId,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        int slot)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(profile, side, role, slot));
    }

    private static void AssertIds(
        IReadOnlyList<int> expected,
        IReadOnlyList<DRPGBattleParticipantViewModel> actual,
        string message)
    {
        if (expected.Count != actual.Count)
        {
            throw new Exception(
                $"{message}. Expected count={expected.Count}, " +
                $"Actual count={actual.Count}");
        }
        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] != actual[i].RuntimeId)
            {
                throw new Exception(
                    $"{message}. Index={i}, Expected={expected[i]}, " +
                    $"Actual={actual[i].RuntimeId}");
            }
        }
    }

    private static void AssertIds(
        IReadOnlyList<int> expected,
        IReadOnlyList<int> actual,
        string message)
    {
        if (expected.Count != actual.Count)
        {
            throw new Exception(
                $"{message}. Expected count={expected.Count}, " +
                $"Actual count={actual.Count}");
        }
        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] != actual[i])
            {
                throw new Exception(
                    $"{message}. Index={i}, Expected={expected[i]}, " +
                    $"Actual={actual[i]}");
            }
        }
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
}
#endif
