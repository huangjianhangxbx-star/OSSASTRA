#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleImpactSequenceProbe
{
    public static string Run()
    {
        VerifyLedgerContract();
        VerifyIndexedSessionCommits();
        VerifyTimeoutFlushesPendingImpacts();
        return "PASS DRPG impact partition sequence";
    }

    private static void VerifyLedgerContract()
    {
        var sequence = new DRPGResolvedImpactSequence(new[]
        {
            new DRPGResolvedImpactPartition(
                0,
                2,
                DRPGBattleImpactContext.ForStrike(
                    DRPGStrikeGrade.Body,
                    0.25f)),
            new DRPGResolvedImpactPartition(
                1,
                2,
                DRPGBattleImpactContext.ForStrike(
                    DRPGStrikeGrade.Body,
                    0.25f)),
            new DRPGResolvedImpactPartition(
                2,
                2,
                DRPGBattleImpactContext.ForStrike(
                    DRPGStrikeGrade.Body,
                    0.5f))
        });
        var ledger = new DRPGBattleImpactCommitLedger(sequence.Count);

        AssertEqual(3, sequence.Count, "Sequence must freeze all partitions");
        AssertEqual(2, sequence.GetAt(2).Index,
            "Sequence lookup must preserve stable indices");
        AssertFalse(ledger.TryBeginCommit(1),
            "Out-of-order impact commits must be rejected");
        AssertTrue(ledger.TryBeginCommit(0),
            "The first pending impact must be accepted");
        AssertFalse(ledger.TryBeginCommit(0),
            "An in-flight impact cannot begin twice");
        ledger.RollBackBegin(0);
        AssertTrue(ledger.TryBeginCommit(0),
            "A rolled-back impact must become pending again");
        ledger.CompleteCommit(0);
        AssertTrue(ledger.IsCommitted(0),
            "A completed impact must stay committed");
        AssertFalse(ledger.TryBeginCommit(0),
            "A committed impact cannot resolve twice");
        AssertEqual(2, ledger.PendingIndices.Count,
            "Only unresolved impacts should remain pending");
    }

    private static void VerifyIndexedSessionCommits()
    {
        Fixture fixture = CreateFixture(
            new[] { 0.25f, 0.25f, 0.5f },
            initialEnemyHitPoints: 10f);
        try
        {
            AssertTrue(fixture.Session.TryPrepareCommand(
                new DRPGBattleCommand(
                    fixture.Player.RuntimeId,
                    fixture.Enemy.RuntimeId,
                    fixture.Skill),
                out DRPGBattlePreparedAction action,
                out string error), error);
            float hpBefore = fixture.Enemy.CurrentHp;

            AssertEqual(3, action.ImpactSequence.Count,
                "Rule authoring must freeze a three-impact sequence");
            AssertFalse(fixture.Session.TryCommitImpactPartition(
                    action.ActionId,
                    1,
                    out _,
                    out _),
                "A later marker cannot overtake an earlier impact");
            AssertFalse(fixture.Session.TryCommitImpactPartition(
                    action.ActionId + 1,
                    0,
                    out _,
                    out _),
                "A marker from another action must be rejected");
            AssertTrue(fixture.Session.TryCommitImpactPartition(
                action.ActionId,
                0,
                out DRPGBattleActionResolution first,
                out error), error);
            AssertEqual(0, first.ImpactIndex,
                "The emitted resolution must identify its partition");
            AssertNear(hpBefore - 10f, fixture.Enemy.CurrentHp,
                "The first partition must apply only its frozen share");
            AssertFalse(fixture.Session.TryCommitImpactPartition(
                    action.ActionId,
                    0,
                    out _,
                    out _),
                "A duplicate marker must not apply damage twice");
            AssertFalse(fixture.Session.TryCompleteAction(action, out _),
                "An action cannot complete while impacts remain pending");

            AssertNear(0f, fixture.Enemy.CurrentHp,
                "The first real impact must defeat the low-HP target");
            long revisionBeforeExternalAttempt =
                fixture.Session.TransactionState.BattleRevision;
            AssertFalse(fixture.Session.TryCommitTestHarnessMutation(
                    workspace => workspace.For(
                            new ParticipantId(fixture.Enemy.RuntimeId))
                        .SetHitPoints(fixture.Enemy.EffectiveMaxHp),
                    out _,
                    out _),
                "An external TestHarness owner mutated a pending Legacy action");
            AssertEqual(
                revisionBeforeExternalAttempt,
                fixture.Session.TransactionState.BattleRevision,
                "A rejected external mutation changed battle revision");
            AssertTrue(fixture.Session.TryCommitImpactPartition(
                action.ActionId,
                1,
                out DRPGBattleActionResolution second,
                out error), error);
            AssertEqual(0, second.Hits.Count,
                "A target killed between markers must fail open as a no-op");
            AssertTrue(fixture.Session.TryCommitImpactPartition(
                action.ActionId,
                2,
                out DRPGBattleActionResolution third,
                out error), error);
            AssertTrue(third.CompletesImpactSequence,
                "The final partition must mark the sequence complete");
            AssertTrue(fixture.Session.TryCompleteAction(action, out error), error);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static void VerifyTimeoutFlushesPendingImpacts()
    {
        Fixture fixture = CreateFixture(new[] { 0.2f, 0.3f, 0.5f });
        try
        {
            AssertTrue(fixture.Session.TryPrepareCommand(
                new DRPGBattleCommand(
                    fixture.Player.RuntimeId,
                    fixture.Enemy.RuntimeId,
                    fixture.Skill),
                out DRPGBattlePreparedAction action,
                out string error), error);
            float hpBefore = fixture.Enemy.CurrentHp;

            AssertTrue(fixture.Session.TryResolveImpact(
                action,
                out DRPGBattleActionResolution aggregate,
                out error), error);
            AssertNear(hpBefore - 40f, fixture.Enemy.CurrentHp,
                "Fallback resolution must flush every pending impact in order");
            AssertEqual(3, aggregate.Hits.Count,
                "Fallback resolution must expose the aggregate hit snapshot");
            AssertEqual(0, action.ImpactLedger.PendingIndices.Count,
                "Timeout fallback must leave no pending partitions");
            AssertTrue(fixture.Session.TryCompleteAction(action, out error), error);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static Fixture CreateFixture(
        IReadOnlyList<float> multipliers,
        float? initialEnemyHitPoints = null)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "power", 40f);
        SetField(skill, "impactDamageMultipliers", new List<float>(multipliers));
        DRPGCombatantProfileSO playerProfile = CreateProfile(skill, 30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(skill, 10f);
        var player = new BattleParticipantRuntime(
            1,
            new BattleParticipantSpec(
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                0));
        var enemy = new BattleParticipantRuntime(
            2,
            new BattleParticipantSpec(
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                0));
        if (initialEnemyHitPoints.HasValue)
        {
            DRPGBattleStateMutationFixture.PrepareHitPoints(
                enemy,
                initialEnemyHitPoints.Value);
        }
        var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
        session.Start();
        return new Fixture(
            skill,
            playerProfile,
            enemyProfile,
            player,
            enemy,
            session);
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
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException(
                $"{message}; expected={expected}; actual={actual}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(
                $"{message}; expected={expected}; actual={actual}");
    }

    private readonly struct Fixture : IDisposable
    {
        public Fixture(
            DRPGSkillSO skill,
            DRPGCombatantProfileSO playerProfile,
            DRPGCombatantProfileSO enemyProfile,
            BattleParticipantRuntime player,
            BattleParticipantRuntime enemy,
            DRPGBattleSession session)
        {
            Skill = skill;
            PlayerProfile = playerProfile;
            EnemyProfile = enemyProfile;
            Player = player;
            Enemy = enemy;
            Session = session;
        }

        public DRPGSkillSO Skill { get; }
        public DRPGCombatantProfileSO PlayerProfile { get; }
        public DRPGCombatantProfileSO EnemyProfile { get; }
        public BattleParticipantRuntime Player { get; }
        public BattleParticipantRuntime Enemy { get; }
        public DRPGBattleSession Session { get; }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(EnemyProfile);
            UnityEngine.Object.DestroyImmediate(PlayerProfile);
            UnityEngine.Object.DestroyImmediate(Skill);
        }
    }
}
#endif
