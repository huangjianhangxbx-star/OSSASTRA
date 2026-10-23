#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleFlowLifecycleProbe
{
    public static string Run()
    {
        VerifyPureFlowRejectsIllegalAndStaleTransitions();
        VerifySessionCommitWaitsForExplicitFinalization();
        return "PASS DRPG hierarchical battle flow lifecycle";
    }

    private static void VerifyPureFlowRejectsIllegalAndStaleTransitions()
    {
        var flow = new DRPGBattleFlowOrchestrator();
        AssertEqual(DRPGBattleLifecycleState.Inactive, flow.LifecycleState,
            "A new flow must be inactive");
        AssertFalse(flow.TryOpenCommandGate(out _),
            "Commands cannot open before battle start and turn begin");

        AssertTrue(flow.TryStartBattle(out string error), error);
        AssertTrue(flow.TryBeginTurn(out error), error);
        AssertTrue(flow.TryOpenCommandGate(out error), error);
        AssertTrue(flow.TryBeginTargetSelection(out error), error);
        AssertTrue(flow.TryCancelTargetSelection(out error), error);
        AssertTrue(flow.TryLockAction(101L, out error), error);
        AssertEqual(DRPGBattleLifecycleState.ActionTransaction,
            flow.LifecycleState,
            "Locking an action must enter the transaction lifecycle");
        AssertEqual(DRPGBattleActionFlowStage.Locked, flow.ActionStage,
            "A prepared action must start at the locked checkpoint");
        AssertFalse(flow.TryBeginImpact(999L, out _),
            "A stale action identity must not advance the transaction");
        AssertTrue(flow.TryBeginImpact(101L, out error), error);
        AssertTrue(flow.TryCompleteImpact(101L, out error), error);
        AssertTrue(flow.TryBeginCommit(101L, out error), error);
        AssertTrue(flow.TryCompleteCommit(101L, out error), error);
        AssertEqual(DRPGBattleLifecycleState.TurnSettlement,
            flow.LifecycleState,
            "Domain commit must enter turn settlement");
        AssertEqual(
            DRPGBattleActionFlowStage.AwaitingSettlementPresentation,
            flow.ActionStage,
            "Domain commit must wait for settlement presentation");
        AssertFalse(flow.TryBeginTurn(out _),
            "A new turn cannot begin while settlement presentation owns the gate");
        AssertTrue(flow.TryBeginFinalization(101L, out error), error);
        AssertTrue(flow.TryCompleteFinalizationToNextTurn(101L, out error),
            error);
        AssertEqual(DRPGBattleLifecycleState.TurnBegin, flow.LifecycleState,
            "Finalization is the only route into the next turn");
        AssertEqual(0L, flow.ActiveActionId,
            "Finalization must release the action identity");
        AssertFalse(flow.TryCompleteFinalizationToNextTurn(101L, out _),
            "A completed action cannot finalize twice");

        AssertTrue(flow.TryOpenCommandGate(out error), error);
        AssertTrue(flow.TryLockAction(102L, out error), error);
        AssertTrue(flow.TryCancelActionToCommand(102L, out error), error);
        AssertEqual(DRPGBattleLifecycleState.AwaitingCommand,
            flow.LifecycleState,
            "A reversible prepared action must cancel back to command input");
    }

    private static void VerifySessionCommitWaitsForExplicitFinalization()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(attack, 30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(attack, 20f);

        try
        {
            SetField(attack, "power", 10f);
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
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            int completedEvents = 0;
            session.ActionCompleted += _ => completedEvents++;
            session.Start();

            var command = new DRPGBattleCommand(
                player.RuntimeId,
                enemy.RuntimeId,
                attack);
            AssertTrue(session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction action,
                out string error), error);
            AssertTrue(session.TryResolveImpact(action, out _, out error),
                error);
            AssertTrue(session.TryCommitAction(action, out error), error);

            AssertSame(player, session.CurrentActor,
                "Commit must retain the acting participant until presentation settles");
            AssertSame(action, session.PendingFinalizationAction,
                "Commit must expose the immutable pending finalization identity");
            AssertEqual(DRPGBattlePhase.ActionEnd, session.Phase,
                "Commit must remain at the legacy ActionEnd projection");
            AssertEqual(0, completedEvents,
                "Commit must not publish ActionCompleted early");
            AssertFalse(session.TryPrepareCommand(command, out _, out _),
                "Commands must remain closed while settlement presentation owns the gate");
            AssertFalse(session.TryCommitAction(action, out _),
                "A domain action cannot commit twice");

            AssertTrue(session.TryFinalizeCommittedAction(action, out error),
                error);
            AssertEqual(1, completedEvents,
                "Finalization must publish ActionCompleted exactly once");
            AssertTrue(session.PendingFinalizationAction == null,
                "Finalization must release the pending action identity");
            AssertFalse(session.TryFinalizeCommittedAction(action, out _),
                "A finalized action cannot finalize twice");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(attack);
        }
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
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
