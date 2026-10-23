#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGTurnSchedulerProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO fastProfile = CreateProfile(20f, 1000f);
        DRPGCombatantProfileSO slowProfile = CreateProfile(10f, 1000f);

        try
        {
            BattleParticipantRuntime fast = CreateParticipant(
                2, fastProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember, 1);
            BattleParticipantRuntime slow = CreateParticipant(
                1, slowProfile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy, 0);

            var scheduler = new DRPGTurnScheduler();
            scheduler.Initialize(new[] { slow, fast });
            IReadOnlyList<DRPGTurnScheduleEntry> initial = scheduler.GetTimelineSnapshot();

            AssertNear(88f, initial[0].ScheduledTime,
                "A large speed lead must use the capped relative-speed band");
            DRPGTurnScheduleEntry? peeked = scheduler.PeekNextEntry();
            if (!peeked.HasValue)
                throw new Exception("Peek must return the next live entry");
            AssertSame(fast, peeked.Value.Participant,
                "Peek must expose the same stable ordering as TakeNext");
            AssertNear(0f, scheduler.CurrentTime,
                "Peek must not advance logical scheduler time");
            AssertEqual(2, scheduler.Count,
                "Peek must not remove an entry");

            DRPGTurnScheduleEntry? taken = scheduler.TakeNextEntry();
            if (!taken.HasValue)
                throw new Exception("TakeNextEntry must return a live entry");
            AssertSame(fast, taken.Value.Participant,
                "The lower scheduled time must act first");
            AssertNear(88f, scheduler.CurrentTime,
                "Taking an entry must advance to its scheduled time");

            scheduler.Clear();
            scheduler.ScheduleAt(fast, 0f, 0);
            AssertSame(fast, scheduler.TakeNext(),
                "The compatibility TakeNext adapter must remain functional");
            float preparationDelay = scheduler.CalculatePreparationDelay(
                fast,
                200f);
            AssertNear(17.6f, preparationDelay,
                "Preparation delay must use the existing relative-speed policy");

            BattleParticipantRuntime playerTie = CreateParticipant(
                4, slowProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember, 0);
            BattleParticipantRuntime enemyTie = CreateParticipant(
                3, slowProfile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy, 0);
            scheduler.Clear();
            scheduler.ScheduleAt(playerTie, 10f, 0);
            scheduler.ScheduleAt(enemyTie, 10f, 0);
            AssertSame(enemyTie, scheduler.TakeNext(),
                "Equal time must not automatically prefer the player side");

            BattleParticipantRuntime priorityWinner = CreateParticipant(
                7, slowProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember, 4);
            BattleParticipantRuntime slotWinner = CreateParticipant(
                8, slowProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember, 1);
            scheduler.Clear();
            scheduler.ScheduleAt(slotWinner, 10f, 1);
            scheduler.ScheduleAt(priorityWinner, 10f, 0);
            AssertSame(priorityWinner, scheduler.TakeNext(),
                "Explicit priority must break an equal-time tie before formation slot");

            slow.ApplyDamage(slow.CurrentHp);
            scheduler.Clear();
            scheduler.ScheduleAt(slow, 5f, 0);
            scheduler.ScheduleAt(playerTie, 6f, 0);
            AssertSame(playerTie, scheduler.TakeNext(),
                "Dead participants must be removed from the timeline");

            scheduler.Clear();
            scheduler.ScheduleAt(fast, 0f, 0);
            AssertSame(fast, scheduler.TakeNext(),
                "The actor must be available before rescheduling");
            float baseNextDelay = scheduler.CalculateNextDelay(fast, null, null);
            float modifiedNextDelay = scheduler.CalculateNextDelay(
                fast,
                null,
                new IDRPGActionDelayModifier[] { new AddDelayModifier(25f) });
            AssertNear(baseNextDelay + 25f, modifiedNextDelay,
                "Weapon, skill, and status seams must modify only the next delay");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fastProfile);
            UnityEngine.Object.DestroyImmediate(slowProfile);
        }

        return "PASS DRPG turn scheduler";
    }

    private static DRPGCombatantProfileSO CreateProfile(float agility, float delay)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "baseTimelineDelay", delay);
        SetField(profile, "baseRecoveryDelay", 0f);
        return profile;
    }

    private static BattleParticipantRuntime CreateParticipant(
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

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
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

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private sealed class AddDelayModifier : IDRPGActionDelayModifier
    {
        private readonly float amount;

        public AddDelayModifier(float amount)
        {
            this.amount = amount;
        }

        public float ModifyDelay(DRPGActionDelayContext context, float currentDelay)
        {
            return currentDelay + amount;
        }
    }
}
#endif
