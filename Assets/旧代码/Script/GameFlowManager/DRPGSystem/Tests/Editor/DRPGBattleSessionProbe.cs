#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleSessionProbe
{
    public static string Run()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(attack, 30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(attack, 20f);
        DRPGCombatantProfileSO fastEnemyProfile = CreateProfile(attack, 40f);

        try
        {
            var player = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var enemyA = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var enemyB = new BattleParticipantRuntime(
                3,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    1));

            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemyA, enemyB });
            int actorEvents = 0;
            session.CurrentActorChanged += _ => actorEvents++;
            session.Start();

            AssertSame(player, session.CurrentActor,
                "The fastest living participant must become current actor");
            AssertEqual(1, actorEvents,
                "A new current actor must publish one coherent event");

            var ordinaryAttack = new DRPGBattleCommand(
                player.RuntimeId,
                enemyA.RuntimeId,
                attack);
            AssertSame(attack, ordinaryAttack.Skill,
                "Ordinary attack must be represented by a normal skill command");

            DRPGBattleCommand invalidTarget = new DRPGBattleCommand(
                player.RuntimeId,
                player.RuntimeId,
                attack);
            AssertFalse(session.TrySubmitCommand(invalidTarget, out _),
                "A player command targeting its own side must be rejected");

            AssertTrue(session.TrySubmitCommand(ordinaryAttack, out string error), error);
            AssertTrue(enemyA.CurrentHp < enemyA.Profile.MaxHp,
                "A valid command must apply damage to its target");

            AssertSame(player, session.CurrentActor,
                "A newly committed ordinary enemy attack must grant one player response");
            AssertTrue(session.TrySubmitCommand(
                DRPGBattleCommand.Wait(player.RuntimeId),
                out error), error);
            AssertSame(enemyA, session.CurrentActor,
                "The earliest warning-zero enemy must act after the player response");
            AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand aiCommand),
                "Enemy AI must select its first valid skill and a living player target");
            AssertEqual(player.RuntimeId, aiCommand.TargetRuntimeId,
                "Enemy AI must target a living player");

            var randomTargetA = new BattleParticipantRuntime(
                20,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            var randomTargetB = new BattleParticipantRuntime(
                21,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    1));
            var deployedProtagonist = new BattleParticipantRuntime(
                22,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    2));
            var randomEnemy = new BattleParticipantRuntime(
                23,
                new BattleParticipantSpec(
                    fastEnemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var randomSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { randomTargetA, randomTargetB, deployedProtagonist, randomEnemy },
                randomSource: new FixedDRPGBattleRandom(1));
            randomSession.Start();
            AssertEqual(DRPGBattleSide.Player, randomSession.CurrentActor.Side,
                "A ready ordinary enemy must first expose a player response turn");
            AssertTrue(randomSession.TrySubmitCommand(
                DRPGBattleCommand.Wait(randomSession.CurrentActor.RuntimeId),
                out error), error);
            AssertSame(randomEnemy, randomSession.CurrentActor,
                "The random-target probe requires the enemy after one response");
            AssertTrue(randomSession.TryCreateEnemyCommand(out DRPGBattleCommand randomCommand),
                "Enemy AI must create a command from the legal target pool");
            AssertEqual(randomTargetB.RuntimeId, randomCommand.TargetRuntimeId,
                "Enemy AI must use the injected random source instead of always targeting slot one");

            var timelinePlayer = new BattleParticipantRuntime(
                30,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var timelineSurvivor = new BattleParticipantRuntime(
                31,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    1));
            var timelineEnemy = new BattleParticipantRuntime(
                32,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            DRPGStatusDefinitionSO fatalTurnStart =
                ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            SetField(fatalTurnStart, "statusId", "session_probe_fatal_turn_start");
            SetField(fatalTurnStart, "turnStartHpDelta", -1000f);
            DRPGBattleStateMutationFixture.PrepareStatus(
                timelinePlayer,
                DRPGResolvedStatusSpec.From(fatalTurnStart));
            UnityEngine.Object.DestroyImmediate(fatalTurnStart);
            var timelineSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { timelinePlayer, timelineSurvivor, timelineEnemy });
            timelineSession.Start();
            timelineSession.RemoveDefeatedFromTimeline();
            AssertEqual(0f, timelinePlayer.CurrentHp,
                "The first scheduled actor must be defeated by its real turn-start status");
            AssertFalse(Contains(timelineSession.Timeline, timelinePlayer),
                "An actor defeated after scheduler initialization must be removed from the timeline");

            var victoryPlayer = new BattleParticipantRuntime(
                33,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var defeatedEnemyA = new BattleParticipantRuntime(
                34,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var defeatedEnemyB = new BattleParticipantRuntime(
                35,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    1));
            DRPGBattleStateMutationFixture.PrepareHitPoints(
                defeatedEnemyA,
                0f);
            DRPGBattleStateMutationFixture.PrepareHitPoints(
                defeatedEnemyB,
                0f);
            var victorySession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { victoryPlayer, defeatedEnemyA, defeatedEnemyB });
            victorySession.EvaluateOutcome();
            AssertEqual(DRPGBattleOutcome.Victory, victorySession.Outcome,
                "All enemies defeated must produce victory");

            var defeatPlayer = new BattleParticipantRuntime(
                10,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var defeatEnemy = new BattleParticipantRuntime(
                11,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            DRPGBattleStateMutationFixture.PrepareHitPoints(
                defeatPlayer,
                0f);
            var defeatSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { defeatPlayer, defeatEnemy });
            defeatSession.EvaluateOutcome();
            AssertEqual(DRPGBattleOutcome.Defeat, defeatSession.Outcome,
                "All player-side actors defeated must produce defeat");
            AssertEqual(0f, defeatPlayer.CurrentHp,
                "Damage must never reduce HP below zero");

            var mutualPlayer = new BattleParticipantRuntime(41, new BattleParticipantSpec(playerProfile,
                DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            var mutualEnemy = new BattleParticipantRuntime(42, new BattleParticipantSpec(enemyProfile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0));
            DRPGBattleStateMutationFixture.PrepareHitPoints(mutualPlayer, 0f);
            DRPGBattleStateMutationFixture.PrepareHitPoints(mutualEnemy, 0f);
            var mutualSession = DRPGBattleHeadlessSessionFactory.Create(new[] { mutualPlayer, mutualEnemy });
            int prematureResults = 0;
            mutualSession.BattleEnded += _ => prematureResults++;
            mutualSession.EvaluateOutcome();
            AssertEqual(DRPGBattleOutcome.Defeat, mutualSession.Outcome,
                "Mutual annihilation must prefer party defeat in Legacy as in standard finalization");
            AssertEqual(0, prematureResults, "A pre-start outcome query must not publish a live result");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fastEnemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS DRPG battle session";
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO skill,
        float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static bool Contains(
        IReadOnlyList<DRPGTurnScheduleEntry> timeline,
        BattleParticipantRuntime participant)
    {
        for (int i = 0; i < timeline.Count; i++)
        {
            if (ReferenceEquals(timeline[i].Participant, participant))
                return true;
        }

        return false;
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

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private sealed class FixedDRPGBattleRandom : IDRPGBattleRandom
    {
        private readonly int value;

        public FixedDRPGBattleRandom(int value)
        {
            this.value = value;
        }

        public int Next(int exclusiveMax)
        {
            return value;
        }
    }
}
#endif
