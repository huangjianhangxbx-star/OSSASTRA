#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGPostureBreakSessionProbe
{
    public static string Run()
    {
        VerifyInterruptionPolicyIsFrozenAndPhaseAware();
        VerifyExposedDamageIsCapturedByTheHit();
        VerifySessionBreakDoesNotSkipTheNextTurn();
        VerifyExposedDefenseInputIsRejected();
        return "PASS DRPG posture-break session integration";
    }

    private static void VerifyInterruptionPolicyIsFrozenAndPhaseAware()
    {
        DRPGSkillSO interruptible = CreateSkill(
            "probe_break_interruptible",
            10f,
            0f);
        DRPGSkillSO uninterruptible = CreateSkill(
            "probe_break_uninterruptible",
            10f,
            0f);
        SetField(
            uninterruptible,
            "postureBreakInterruptionPolicy",
            DRPGPostureBreakInterruptionPolicy.Uninterruptible);
        try
        {
            DRPGEnemyActionPlan windup = DRPGEnemyActionPlan.CreateAttack(
                1L,
                101,
                interruptible,
                0f,
                10f,
                2);
            AssertTrue(windup.CanBeInterruptedByPostureBreak,
                "Default attack windups must be interruptible");

            DRPGEnemyActionPlan committed = windup.CommitTarget(201, 0L);
            AssertTrue(committed.CanBeInterruptedByPostureBreak,
                "Warning-zero attacks must remain interruptible before execution");
            DRPGEnemyActionPlan executing = committed
                .WithSatisfiedPlayerResponse(1L)
                .MarkExecuting();
            AssertFalse(executing.CanBeInterruptedByPostureBreak,
                "Executing actions must never be rolled back by posture break");

            DRPGEnemyActionPlan protectedWindup =
                DRPGEnemyActionPlan.CreateAttack(
                    2L,
                    102,
                    uninterruptible,
                    0f,
                    10f,
                    2);
            AssertFalse(protectedWindup.CanBeInterruptedByPostureBreak,
                "Explicitly uninterruptible skills must retain their plan");

            DRPGCombatantProfileSO profile = CreateProfile(
                interruptible,
                10f,
                100f);
            try
            {
                BattleParticipantRuntime actor = CreateParticipant(
                    301,
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist);
                DRPGBattleCommand command = DRPGBattleCommand.UseSkill(
                    actor.RuntimeId,
                    999,
                    interruptible);
                DRPGResolvedActionSpec spec =
                    DRPGActionSpecResolver.Resolve(command, actor);
                AssertEqual(
                    DRPGPostureBreakInterruptionPolicy.InterruptBeforeExecution,
                    spec.PostureBreakInterruptionPolicy,
                    "Prepared specs must freeze the authored interruption policy");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(interruptible);
            UnityEngine.Object.DestroyImmediate(uninterruptible);
        }
    }

    private static void VerifyExposedDamageIsCapturedByTheHit()
    {
        DRPGSkillSO attack = CreateSkill("probe_exposed_damage", 20f, 0f);
        DRPGCombatantProfileSO attackerProfile = CreateProfile(
            attack,
            20f,
            100f);
        DRPGCombatantProfileSO targetProfile = CreateProfile(
            attack,
            10f,
            100f);
        try
        {
            BattleParticipantRuntime attacker = CreateParticipant(
                401,
                attackerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime target = CreateParticipant(
                402,
                targetProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            BattleParticipantRuntime[] participants = { attacker, target };
            DRPGBattleSessionState state = CreateState(
                51003L,
                participants,
                out BattleRuleMutationLease lease);
            try
            {
                DRPGBattleMutationWorkspace postureWorkspace =
                    state.CreateWorkspace();
                postureWorkspace.For(new ParticipantId(target.RuntimeId))
                    .SetPosture(0f);
                BattleJournalCommitResult postureCommit =
                    state.TryCommitJournal(lease, postureWorkspace);
                AssertTrue(postureCommit.Succeeded,
                    "Posture-break workspace setup failed: " +
                    postureCommit.Error);

                DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                    attacker.RuntimeId,
                    target.RuntimeId,
                    attack);
                DRPGBattleHitResult hit = ResolveAndCommit(
                    new DRPGActionResolver(),
                    command,
                    attacker,
                    target,
                    participants,
                    state,
                    lease,
                    DRPGBattleImpactContext.ForStrike(
                        DRPGStrikeGrade.Body,
                        1f));

                AssertTrue(hit.WasPostureBreakExposed,
                    "The hit must snapshot exposure before damage is applied");
                AssertNear(20f, hit.AppliedDamage,
                    "Simplified posture break must not amplify HP damage");
            }
            finally
            {
                lease.Dispose();
            }
        }
        finally
        {
            Destroy(targetProfile, attackerProfile, attack);
        }
    }

    private static void VerifySessionBreakDoesNotSkipTheNextTurn()
    {
        DRPGSkillSO breakAttack = CreateSkill(
            "probe_session_break",
            1f,
            200f);
        DRPGSkillSO enemyAttack = CreateSkill(
            "probe_session_enemy",
            1f,
            0f);
        SetField(enemyAttack, "enemyWarningSteps", 2);
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            breakAttack,
            30f,
            40f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            enemyAttack,
            5f,
            40f);
        try
        {
            BattleParticipantRuntime player = CreateParticipant(
                501,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(
                502,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            var kinds = new List<DRPGBattleEventKind>();
            session.BattleEventPublished += battleEvent =>
                kinds.Add(battleEvent.Kind);
            session.Start();

            AssertSame(player, session.CurrentActor,
                "The faster player must own the break setup turn");
            AssertTrue(session.TrySubmitCommand(
                DRPGBattleCommand.BasicAttack(
                    player.RuntimeId,
                    enemy.RuntimeId,
                    breakAttack),
                out string error), error);

            AssertContains(kinds, DRPGBattleEventKind.PostureBreakEntered,
                "Break entry must be an explicit battle fact");
            AssertEqual(
                DRPGPostureBreakState.Stable,
                enemy.PostureBreakState,
                "Simplified posture break must not enter the legacy skip/recovery state machine");
            AssertFalse(kinds.Contains(DRPGBattleEventKind.PostureBreakTurnSkipped),
                "Simplified posture break must not consume the enemy turn as a forced skip");
            AssertSame(enemy, session.CurrentActor,
                "The broken enemy must retain its next actionable turn");
            AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand next),
                "The broken enemy must still materialize its normal command");
            AssertTrue(session.TrySubmitCommand(next, out error), error);
            AssertFalse(kinds.Contains(DRPGBattleEventKind.PostureBreakTurnSkipped),
                "Executing the next action must not publish a legacy forced-skip fact");
        }
        finally
        {
            Destroy(enemyProfile, playerProfile, enemyAttack, breakAttack);
        }
    }

    private static void VerifyExposedDefenseInputIsRejected()
    {
        DRPGSkillSO special = CreateSkill(
            "probe_break_guard_rejection",
            10f,
            0f);
        SetField(
            special,
            "executionMode",
            DRPGSkillExecutionMode.TelegraphedSpecial);
        try
        {
            BattleInteractiveActionRun run =
                BattleInteractiveActionRun.CreateDefenseWithAvailability(
                    special,
                    false,
                    "PostureBreak exposure disables active defense.");
            AssertFalse(run.SubmitGuard(),
                "An exposed defender must not submit guard input");
            AssertTrue(!string.IsNullOrWhiteSpace(run.GuardUnavailableReason),
                "Rejected defense must expose a diagnostic reason");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(special);
        }
    }

    private static void SubmitCurrentAction(DRPGBattleSession session)
    {
        if (session.CurrentActor == null)
            throw new Exception("The scheduler produced no actionable actor.");
        DRPGBattleCommand command;
        if (session.CurrentActor.Side == DRPGBattleSide.Enemy)
        {
            AssertTrue(session.TryCreateEnemyCommand(out command),
                "The recovered enemy could not materialize a command");
        }
        else
        {
            command = DRPGBattleCommand.Wait(session.CurrentActor.RuntimeId);
        }
        AssertTrue(session.TrySubmitCommand(command, out string error), error);
    }

    private static DRPGSkillSO CreateSkill(
        string id,
        float power,
        float postureDamage)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "power", power);
        var effects = new List<DRPGActionEffectDefinition>
        {
            DRPGActionEffectDefinition.Damage(
                DRPGActionEffectTarget.PrimaryTarget,
                1f,
                DRPGDamageTag.Slash)
        };
        if (postureDamage > 0f)
        {
            effects.Add(DRPGActionEffectDefinition.PostureDamage(
                DRPGActionEffectTarget.PrimaryTarget,
                postureDamage));
        }
        SetField(skill, "effects", effects);
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO basicAttack,
        float agility,
        float maxPosture)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", "probe_" + Guid.NewGuid());
        SetField(profile, "maxHp", 200f);
        SetField(profile, "maxPosture", maxPosture);
        SetField(profile, "agility", agility);
        SetField(profile, "skills", new List<DRPGSkillSO> { basicAttack });
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

    private static DRPGBattleSessionState CreateState(
        long battleGeneration,
        IReadOnlyList<BattleParticipantRuntime> participants,
        out BattleRuleMutationLease lease)
    {
        var scheduler = new DRPGTurnScheduler();
        var arbiter = new DRPGBattleTransactionArbiter(
            battleGeneration,
            new DRPGBattleActionTraceWriter(
                new DRPGBoundedActionTraceSink(64)));
        var state = new DRPGBattleSessionState(
            battleGeneration,
            participants,
            scheduler,
            arbiter);
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForTestHarness(
                new BattleStartIdentity(battleGeneration, 1L));
        if (!arbiter.TryAcquire(owner, out lease, out string error))
            throw new InvalidOperationException(error);
        return state;
    }

    private static DRPGBattleHitResult ResolveAndCommit(
        DRPGActionResolver resolver,
        DRPGBattleCommand command,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleSessionState state,
        BattleRuleMutationLease lease,
        DRPGBattleImpactContext context)
    {
        DRPGBattleMutationWorkspace workspace = state.CreateWorkspace();
        DRPGWorkspaceResolutionResult resolution =
            resolver.ResolveIntoWorkspace(
                DRPGActionSpecResolver.Resolve(command, actor),
                actor,
                target,
                participants,
                workspace,
                context);
        AssertEqual(
            DRPGWorkspaceResolutionStatus.Succeeded,
            resolution.Status,
            "Exposed damage must stage in the workspace");
        AssertEqual(1, resolution.Batch.Hits.Count,
            "Exposed damage must produce one staged hit");
        BattleJournalCommitResult committed =
            state.TryCommitJournal(lease, workspace);
        AssertTrue(committed.Succeeded,
            "Exposed damage workspace commit failed: " + committed.Error);
        return resolution.Batch.Hits[0];
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

    private static void Destroy(params UnityEngine.Object[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
                UnityEngine.Object.DestroyImmediate(values[i]);
        }
    }

    private static void AssertContains<T>(
        IList<T> values,
        T expected,
        string message)
    {
        if (!values.Contains(expected))
            throw new Exception(message + ". Missing=" + expected);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
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
}
#endif
