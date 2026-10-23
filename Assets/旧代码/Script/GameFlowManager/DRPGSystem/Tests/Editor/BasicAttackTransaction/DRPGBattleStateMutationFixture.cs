#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

internal sealed class DRPGBattleStateMutationFixture : IDisposable
{
    private static long nextGeneration = 200000L;
    private readonly List<UnityEngine.Object> ownedObjects =
        new List<UnityEngine.Object>();

    private DRPGBattleStateMutationFixture(bool enemyStartsDefeated,
        DRPGBattleActionResourceBindings actionResources = null, bool consumePlayer = false)
    {
        BattleGeneration = Interlocked.Increment(ref nextGeneration);
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            "workspace_player",
            100f,
            30f,
            100f,
            20f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            "workspace_enemy",
            120f,
            20f,
            80f,
            10f);
        Player = new BattleParticipantRuntime(
            9801,
            new BattleParticipantSpec(
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                0));
        Enemy = new BattleParticipantRuntime(
            9802,
            new BattleParticipantSpec(
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                0));
        Scheduler = new DRPGTurnScheduler();
        Scheduler.ScheduleAt(Player, 10f, 0);
        Scheduler.ScheduleAt(Enemy, 20f, 0);
        if (consumePlayer) Scheduler.TakeNextEntry();
        if (enemyStartsDefeated)
        {
            Enemy.ApplyDamage(Enemy.CurrentHp);
            Scheduler.RemoveParticipant(Enemy.RuntimeId);
        }
        var trace = new DRPGBattleActionTraceWriter(
            new DRPGBoundedActionTraceSink(128));
        Arbiter = new DRPGBattleTransactionArbiter(
            BattleGeneration,
            trace);
        State = new DRPGBattleSessionState(
            BattleGeneration,
            new[] { Player, Enemy },
            Scheduler,
            Arbiter,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "focus", 3 }
            }, actionResources);
        Owner = BattleTransactionIdentity.ForTestHarness(
            new BattleStartIdentity(BattleGeneration, 1L));
        if (!Arbiter.TryAcquire(Owner, out BattleRuleMutationLease lease, out string error))
            throw new InvalidOperationException(error);
        Lease = lease;
    }

    internal long BattleGeneration { get; }
    internal BattleParticipantRuntime Player { get; }
    internal BattleParticipantRuntime Enemy { get; }
    internal DRPGTurnScheduler Scheduler { get; }
    internal DRPGBattleTransactionArbiter Arbiter { get; }
    internal DRPGBattleSessionState State { get; }
    internal BattleTransactionIdentity Owner { get; }
    internal BattleRuleMutationLease Lease { get; }

    internal static DRPGBattleStateMutationFixture Create(
        bool enemyStartsDefeated = false)
    {
        return new DRPGBattleStateMutationFixture(enemyStartsDefeated);
    }

    internal DRPGBattleMutationWorkspace CreateWorkspace()
    {
        return State.CreateWorkspace();
    }

    internal static DRPGBattleStateMutationFixture CreateWithActionResources(
        DRPGBattleActionResourceBindings actionResources, bool consumePlayer = false) =>
        new DRPGBattleStateMutationFixture(false, actionResources, consumePlayer);

    internal BattleParticipantRuntime CreateDetachedParticipant(
        int runtimeId,
        DRPGBattleSide side = DRPGBattleSide.Player)
    {
        DRPGCombatantProfileSO profile = CreateProfile(
            "workspace_detached_" + runtimeId,
            90f,
            20f,
            70f,
            15f);
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(
                profile,
                side,
                side == DRPGBattleSide.Player
                    ? DRPGBattleRosterRole.PartyMember
                    : DRPGBattleRosterRole.Enemy,
                1));
    }

    internal static void PrepareHitPoints(
        BattleParticipantRuntime participant,
        float hitPoints)
    {
        if (participant == null)
            throw new ArgumentNullException(nameof(participant));
        if (hitPoints < 0f || hitPoints > participant.EffectiveMaxHp)
            throw new ArgumentOutOfRangeException(nameof(hitPoints));
        if (participant.CurrentHp > hitPoints)
            participant.ApplyDamage(participant.CurrentHp - hitPoints);
        else if (participant.CurrentHp < hitPoints)
            participant.RestoreHp(hitPoints - participant.CurrentHp);
    }

    internal static void PreparePosture(
        BattleParticipantRuntime participant,
        float posture)
    {
        if (participant == null)
            throw new ArgumentNullException(nameof(participant));
        if (posture < 0f || posture > participant.EffectiveMaxPosture)
            throw new ArgumentOutOfRangeException(nameof(posture));
        if (participant.CurrentPosture > posture)
            participant.ReducePosture(participant.CurrentPosture - posture);
        else if (participant.CurrentPosture < posture)
            participant.RestorePosture(posture - participant.CurrentPosture);
    }

    internal static void PrepareStatus(
        BattleParticipantRuntime participant,
        DRPGResolvedStatusSpec status)
    {
        if (participant == null)
            throw new ArgumentNullException(nameof(participant));
        participant.Statuses.Apply(status);
    }

    internal DRPGResolvedStatusSpec CreateStatus(
        string statusId,
        int maxStacks = 3,
        int durationTurns = 2)
    {
        DRPGStatusDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        ownedObjects.Add(definition);
        SetField(definition, "statusId", statusId);
        SetField(definition, "maxStacks", maxStacks);
        SetField(definition, "durationTurns", durationTurns);
        return DRPGResolvedStatusSpec.From(definition);
    }

    public void Dispose()
    {
        Lease?.Dispose();
        for (int i = ownedObjects.Count - 1; i >= 0; i--)
        {
            if (ownedObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
        }
        ownedObjects.Clear();
    }

    private DRPGCombatantProfileSO CreateProfile(
        string combatantId,
        float maxHp,
        float maxMp,
        float maxPosture,
        float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        ownedObjects.Add(profile);
        SetField(profile, "combatantId", combatantId);
        SetField(profile, "maxHp", maxHp);
        SetField(profile, "maxMp", maxMp);
        SetField(profile, "maxPosture", maxPosture);
        SetField(profile, "agility", agility);
        return profile;
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
