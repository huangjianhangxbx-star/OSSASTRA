#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleActionTransactionProbe
{
    public static string Run()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(attack, 30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(attack, 20f);

        try
        {
            SetField(attack, "power", 20f);
            var player = CreateParticipant(
                1, playerProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist, 0);
            var enemy = CreateParticipant(
                2, enemyProfile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy, 0);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            int preparedEvents = 0;
            int resolvedEvents = 0;
            int completedEvents = 0;
            int timelineEvents = 0;
            session.ActionPrepared += _ => preparedEvents++;
            session.ActionResolved += _ => resolvedEvents++;
            session.ActionCompleted += _ => completedEvents++;
            session.TimelineChanged += () => timelineEvents++;
            session.Start();
            timelineEvents = 0;

            float hpBefore = enemy.CurrentHp;
            var command = new DRPGBattleCommand(
                player.RuntimeId,
                enemy.RuntimeId,
                attack);

            AssertTrue(session.TryPrepareCommand(
                command, out DRPGBattlePreparedAction prepared, out string error),
                error);
            AssertEqual(1, preparedEvents,
                "Prepare must publish one event");
            AssertEqual(DRPGBattlePhase.PreparingAction, session.Phase,
                "Prepare must enter PreparingAction");
            AssertEqual(hpBefore, enemy.CurrentHp,
                "Prepare must not change target HP");
            AssertSame(player, session.CurrentActor,
                "Prepare must not rotate the current actor");
            AssertFalse(session.TryPrepareCommand(command, out _, out _),
                "A second action cannot be prepared while one is active");

            AssertTrue(session.TryResolveImpact(
                prepared, out DRPGBattleActionResolution resolution, out error),
                error);
            AssertEqual(1, resolvedEvents,
                "Impact must publish one resolution event");
            AssertEqual(DRPGBattlePhase.ExecutingAction, session.Phase,
                "Impact must remain in ExecutingAction during presentation hold");
            AssertEqual(hpBefore - 20f, enemy.CurrentHp,
                "Impact must apply the configured damage exactly once");
            AssertEqual(1, resolution.Hits.Count,
                "A basic single-target attack must produce one hit result");
            AssertTrue(resolution.HasPrimaryHitOutcome,
                "A basic attack resolution must expose one primary hit outcome");
            AssertEqual(DRPGHitOutcome.Hit, resolution.PrimaryHitOutcome,
                "An unguarded basic attack must resolve as one Hit outcome");
            DRPGBattleHitResult hit = resolution.Hits[0];
            AssertEqual(DRPGHitOutcome.Hit, hit.Outcome,
                "The compatibility hit result must preserve its Hit outcome");
            AssertEqual(hpBefore, hit.HpBefore,
                "Hit result must preserve pre-impact HP");
            AssertEqual(20f, hit.AppliedDamage,
                "Hit result must expose applied damage");
            AssertEqual(hpBefore - 20f, hit.HpAfter,
                "Hit result must expose post-impact HP");
            AssertSame(player, session.CurrentActor,
                "Impact feedback hold must keep the acting participant current");
            AssertFalse(session.TryResolveImpact(prepared, out _, out _),
                "The same prepared action cannot resolve twice");

            AssertTrue(session.TryCompleteAction(prepared, out error), error);
            AssertEqual(1, completedEvents,
                "Complete must publish one completion event");
            AssertEqual(1, timelineEvents,
                "Completion must publish one coherent post-rotation timeline snapshot");
            AssertSame(player, session.CurrentActor,
                "Completion may rotate into the mandatory warning-zero response turn");
            AssertEqual(1, session.ReadyEnemyPlans.Count,
                "The deferred enemy must be committed before the response turn begins");
            AssertFalse(session.TryCompleteAction(prepared, out _),
                "The same prepared action cannot complete twice");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS DRPG action transactions";
    }

    private static BattleParticipantRuntime CreateParticipant(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        int slot)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, slot));
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
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}

public static class DRPGUnifiedActionResolutionProbe
{
    public static string Run()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGStatusDefinitionSO bleed =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        DRPGCombatantProfileSO actorProfile = CreateProfile(30f);
        DRPGCombatantProfileSO targetProfile = CreateProfile(20f);

        try
        {
            SetField(skill, "skillId", "probe_unified_action");
            SetField(skill, "power", 20f);
            SetField(skill, "manaCost", 5f);
            SetField(skill, "postureCost", 7f);
            SetField(bleed, "statusId", "probe_bleed");
            SetField(bleed, "durationTurns", 2);

            var effects = new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.Damage(
                    DRPGActionEffectTarget.PrimaryTarget,
                    1f,
                    DRPGDamageTag.Slash),
                DRPGActionEffectDefinition.PostureDamage(
                    DRPGActionEffectTarget.PrimaryTarget,
                    15f),
                DRPGActionEffectDefinition.ApplyStatus(
                    DRPGActionEffectTarget.PrimaryTarget,
                    bleed),
                DRPGActionEffectDefinition.Shield(
                    DRPGActionEffectTarget.Actor,
                    8f)
            };
            SetField(skill, "effects", effects);
            SetField(
                actorProfile,
                "skills",
                new List<DRPGSkillSO> { skill });
            SetField(
                targetProfile,
                "skills",
                new List<DRPGSkillSO> { skill });
            SetField(
                targetProfile,
                "damageResponses",
                new List<DRPGDamageResponseDefinition>
                {
                    new DRPGDamageResponseDefinition(
                        DRPGDamageTag.Slash,
                        0.5f)
                });

            BattleParticipantRuntime actor = CreateParticipant(
                101,
                actorProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime target = CreateParticipant(
                102,
                targetProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { actor, target });
            session.Start();

            float mpBefore = actor.CurrentMp;
            float postureBefore = actor.CurrentPosture;
            float targetHpBefore = target.CurrentHp;
            float targetPostureBefore = target.CurrentPosture;
            var command = new DRPGBattleCommand(
                actor.RuntimeId,
                target.RuntimeId,
                skill);

            AssertTrue(session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction prepared,
                out string error), error);
            AssertEqual(4, prepared.Spec.Effects.Count,
                "Prepare must freeze every authored effect");
            AssertNear(20f, prepared.Spec.Effects[0].Magnitude,
                "Skill power must be resolved into the immutable action spec");
            AssertNear(mpBefore, actor.CurrentMp,
                "Resource reservation must not spend MP during preparation");
            AssertNear(postureBefore, actor.CurrentPosture,
                "Resource reservation must not spend posture during preparation");

            // Simulate inspector/runtime mutation after the player committed.
            SetField(skill, "power", 200f);
            SetField(skill, "manaCost", 20f);
            effects.Clear();

            AssertTrue(session.TryResolveImpact(
                prepared,
                out DRPGBattleActionResolution resolution,
                out error), error);
            AssertNear(mpBefore - 5f, actor.CurrentMp,
                "Impact must commit the originally reserved MP exactly once");
            AssertNear(postureBefore - 7f, actor.CurrentPosture,
                "Impact must commit the originally reserved posture exactly once");
            AssertNear(targetHpBefore - 10f, target.CurrentHp,
                "Frozen power and target Slash response must determine damage");
            AssertNear(targetPostureBefore - 15f, target.CurrentPosture,
                "Posture damage must resolve through the shared effect pipeline");
            AssertNear(8f, actor.CurrentShield,
                "Actor-targeted shield must resolve in the same action");
            AssertTrue(target.Statuses.Contains("probe_bleed"),
                "Status application must resolve in the same action");
            AssertEqual(1, resolution.Hits.Count,
                "Only damage effects should emit compatibility hit results");
            AssertEqual(DRPGHitOutcome.Hit, resolution.PrimaryHitOutcome,
                "Unified action resolution must preserve one primary Hit outcome");
            AssertEqual(4, resolution.Effects.Count,
                "Every effect must emit one immutable semantic result");
            AssertFalse(session.TryResolveImpact(prepared, out _, out _),
                "A committed action cannot spend resources or resolve twice");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetProfile);
            UnityEngine.Object.DestroyImmediate(actorProfile);
            UnityEngine.Object.DestroyImmediate(bleed);
            UnityEngine.Object.DestroyImmediate(skill);
        }

        return "PASS DRPG unified action resolution";
    }

    public static string RunTargeting()
    {
        DRPGSkillSO selfHeal = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO sweep = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO actorProfile = CreateProfile(40f);
        DRPGCombatantProfileSO enemyProfileA = CreateProfile(20f);
        DRPGCombatantProfileSO enemyProfileB = CreateProfile(10f);

        try
        {
            SetField(selfHeal, "skillId", "probe_self_heal");
            SetField(selfHeal, "targetRule", DRPGSkillTargetRule.Self);
            SetField(selfHeal, "purpose", DRPGSkillPurpose.Healing);
            SetField(selfHeal, "effects", new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.Heal(
                    DRPGActionEffectTarget.Actor,
                    12f)
            });
            SetField(sweep, "skillId", "probe_all_enemy_sweep");
            SetField(sweep, "targetRule", DRPGSkillTargetRule.AllEnemies);
            SetField(sweep, "power", 9f);
            SetField(sweep, "effects", new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.Damage(
                    DRPGActionEffectTarget.AllOpponents,
                    1f,
                    DRPGDamageTag.Slash)
            });
            SetField(actorProfile, "skills", new List<DRPGSkillSO>
            {
                selfHeal,
                sweep
            });

            BattleParticipantRuntime actor = CreateParticipant(
                201,
                actorProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemyA = CreateParticipant(
                202,
                enemyProfileA,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            BattleParticipantRuntime enemyB = CreateParticipant(
                203,
                enemyProfileB,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            DRPGBattleStateMutationFixture.PrepareHitPoints(
                actor,
                actor.CurrentHp - 20f);
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { actor, enemyA, enemyB });
            session.Start();

            var healCommand = new DRPGBattleCommand(
                actor.RuntimeId,
                actor.RuntimeId,
                selfHeal);
            AssertTrue(session.TryPrepareCommand(
                healCommand,
                out DRPGBattlePreparedAction preparedHeal,
                out string error), error);
            SetField(selfHeal, "targetRule", DRPGSkillTargetRule.SingleEnemy);
            SetField(selfHeal, "targetFormationRows", DRPGFormationRowMask.None);
            AssertTrue(session.TryResolveImpact(
                preparedHeal,
                out DRPGBattleActionResolution healResolution,
                out error), error);
            AssertNear(actor.EffectiveMaxHp - 8f, actor.CurrentHp,
                "A self-targeted effect must resolve through the shared pipeline");
            AssertEqual(1, healResolution.Effects.Count,
                "Self heal must emit one semantic result");
            AssertTrue(session.TryCompleteAction(preparedHeal, out error), error);

            // Advance enemy turns without changing state until the actor returns.
            while (!ReferenceEquals(session.CurrentActor, actor))
            {
                DRPGBattleCommand wait = DRPGBattleCommand.Wait(
                    session.CurrentActor.RuntimeId);
                AssertTrue(session.TryPrepareCommand(
                    wait,
                    out DRPGBattlePreparedAction preparedWait,
                    out error), error);
                AssertTrue(session.TryResolveImpact(
                    preparedWait,
                    out _,
                    out error), error);
                AssertTrue(session.TryCompleteAction(preparedWait, out error), error);
            }

            float hpABefore = enemyA.CurrentHp;
            float hpBBefore = enemyB.CurrentHp;
            var sweepCommand = new DRPGBattleCommand(
                actor.RuntimeId,
                enemyA.RuntimeId,
                sweep);
            AssertTrue(session.TryPrepareCommand(
                sweepCommand,
                out DRPGBattlePreparedAction preparedSweep,
                out error), error);
            AssertTrue(session.TryResolveImpact(
                preparedSweep,
                out DRPGBattleActionResolution sweepResolution,
                out error), error);
            AssertNear(hpABefore - 9f, enemyA.CurrentHp,
                "All-opponent damage must include the selected enemy");
            AssertNear(hpBBefore - 9f, enemyB.CurrentHp,
                "All-opponent damage must include every deployed living enemy");
            AssertEqual(2, sweepResolution.Hits.Count,
                "All-opponent damage must emit one hit per affected enemy");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyProfileB);
            UnityEngine.Object.DestroyImmediate(enemyProfileA);
            UnityEngine.Object.DestroyImmediate(actorProfile);
            UnityEngine.Object.DestroyImmediate(sweep);
            UnityEngine.Object.DestroyImmediate(selfHeal);
        }

        return "PASS DRPG unified action targeting";
    }

    public static string RunBattlefieldEffects()
    {
        DRPGSkillSO controlSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO actorProfile = CreateProfile(50f);
        DRPGCombatantProfileSO targetProfile = CreateProfile(20f);
        DRPGCombatantProfileSO blockerProfile = CreateProfile(10f);

        try
        {
            SetField(controlSkill, "skillId", "probe_battlefield_effects");
            SetField(controlSkill, "targetRule", DRPGSkillTargetRule.SingleEnemy);
            SetField(controlSkill, "manaCost", 4f);
            SetField(controlSkill, "effects", new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.PostureRestore(
                    DRPGActionEffectTarget.Actor,
                    6f),
                DRPGActionEffectDefinition.FormationShift(
                    DRPGActionEffectTarget.PrimaryTarget,
                    1),
                DRPGActionEffectDefinition.TimelineShift(
                    DRPGActionEffectTarget.PrimaryTarget,
                    -8f,
                    2)
            });
            SetField(actorProfile, "skills", new List<DRPGSkillSO>
            {
                controlSkill
            });
            SetField(actorProfile, "postureRecoveryPerTurn", 0f);

            BattleParticipantRuntime actor = CreateParticipant(
                301,
                actorProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime target = CreateParticipant(
                302,
                targetProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            BattleParticipantRuntime blocker = CreateParticipant(
                303,
                blockerProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            DRPGBattleStateMutationFixture.PreparePosture(
                actor,
                actor.CurrentPosture - 20f);
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { actor, target, blocker });
            session.Start();
            float mpBefore = actor.CurrentMp;
            float postureBefore = actor.CurrentPosture;
            int formationEvents = 0;
            int timelineEvents = 0;
            session.FormationChanged += () => formationEvents++;
            session.TimelineChanged += () => timelineEvents++;

            var command = new DRPGBattleCommand(
                actor.RuntimeId,
                target.RuntimeId,
                controlSkill);
            AssertTrue(session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction prepared,
                out string error), error);
            AssertTrue(session.TryResolveImpact(
                prepared,
                out DRPGBattleActionResolution resolution,
                out error), error);

            AssertNear(mpBefore - 4f, actor.CurrentMp,
                "A battlefield-control action must commit its frozen cost");
            AssertNear(postureBefore + 6f, actor.CurrentPosture,
                "Posture restore must use the shared effect resolver");
            AssertEqual(DRPGFormationRow.Middle, target.FormationRow,
                "Formation shift must move the target by the authored row delta");
            AssertEqual(3, resolution.Effects.Count,
                "Every battlefield-control effect must emit one result");

            DRPGActionEffectResult timelineResult = null;
            for (int i = 0; i < resolution.Effects.Count; i++)
            {
                if (resolution.Effects[i].Kind ==
                    DRPGActionEffectKind.TimelineShift)
                {
                    timelineResult = resolution.Effects[i];
                    break;
                }
            }
            AssertTrue(timelineResult != null && timelineResult.WasApplied,
                "Timeline shift must emit an applied semantic result");
            AssertNear(-8f,
                timelineResult.TimelineAfter - timelineResult.TimelineBefore,
                "Negative timeline delta must advance the scheduled target");
            AssertTrue(formationEvents > 0,
                "Formation effects must publish a formation refresh");
            AssertTrue(timelineEvents > 0,
                "Timeline effects must publish a timeline refresh");

            var cancelSession = DRPGBattleHeadlessSessionFactory.Create(
                new[]
                {
                    CreateParticipant(
                        311,
                        actorProfile,
                        DRPGBattleSide.Player,
                        DRPGBattleRosterRole.Protagonist),
                    CreateParticipant(
                        312,
                        targetProfile,
                        DRPGBattleSide.Enemy,
                        DRPGBattleRosterRole.Enemy)
                });
            cancelSession.Start();
            BattleParticipantRuntime cancelActor = cancelSession.CurrentActor;
            float cancelMpBefore = cancelActor.CurrentMp;
            var cancelCommand = new DRPGBattleCommand(
                cancelActor.RuntimeId,
                312,
                controlSkill);
            AssertTrue(cancelSession.TryPrepareCommand(
                cancelCommand,
                out DRPGBattlePreparedAction cancelled,
                out error), error);
            cancelSession.Abort();
            AssertNear(cancelMpBefore, cancelActor.CurrentMp,
                "Cancelling a reservation must not spend resources");
            AssertEqual(DRPGActionResourceTransactionState.Cancelled,
                cancelled.ResourceTransaction.State,
                "Aborting a prepared action must cancel its reservation");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(blockerProfile);
            UnityEngine.Object.DestroyImmediate(targetProfile);
            UnityEngine.Object.DestroyImmediate(actorProfile);
            UnityEngine.Object.DestroyImmediate(controlSkill);
        }

        return "PASS DRPG unified battlefield effects";
    }

    public static string RunStatusLifecycle()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGStatusDefinitionSO focus =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        DRPGStatusDefinitionSO exposed =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        DRPGStatusDefinitionSO burn =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        DRPGCombatantProfileSO actorProfile = CreateProfile(40f);
        DRPGCombatantProfileSO targetProfile = CreateProfile(20f);

        try
        {
            SetField(attack, "skillId", "probe_status_snapshot");
            SetField(attack, "power", 10f);
            SetField(actorProfile, "skills", new List<DRPGSkillSO> { attack });
            SetField(focus, "statusId", "probe_focus");
            SetField(focus, "durationTurns", 3);
            SetField(focus, "outgoingDamageMultiplier", 2f);
            SetField(exposed, "statusId", "probe_exposed");
            SetField(exposed, "durationTurns", 3);
            SetField(exposed, "incomingDamageMultiplier", 1.5f);
            SetField(burn, "statusId", "probe_burn_tick");
            SetField(burn, "durationTurns", 2);
            SetField(burn, "turnStartHpDelta", -3f);

            BattleParticipantRuntime actor = CreateParticipant(
                401,
                actorProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime target = CreateParticipant(
                402,
                targetProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            DRPGBattleStateMutationFixture.PrepareStatus(
                actor,
                DRPGResolvedStatusSpec.From(focus));
            DRPGBattleStateMutationFixture.PrepareStatus(
                target,
                DRPGResolvedStatusSpec.From(exposed));
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { actor, target });
            session.Start();

            var command = new DRPGBattleCommand(
                actor.RuntimeId,
                target.RuntimeId,
                attack);
            AssertTrue(session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction prepared,
                out string error), error);
            float hpBefore = target.CurrentHp;

            AssertTrue(session.TryResolveImpact(
                prepared,
                out _,
                out error), error);
            AssertNear(hpBefore - 30f, target.CurrentHp,
                "Attacker modifiers must be frozen while target defenses remain live");

            BattleParticipantRuntime tickTarget = CreateParticipant(
                403,
                targetProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            DRPGBattleStateMutationFixture.PrepareStatus(
                tickTarget,
                DRPGResolvedStatusSpec.From(burn));
            float tickHpBefore = tickTarget.CurrentHp;
            DRPGStatusTurnResult firstTick =
                tickTarget.TickStatusesAtTurnStart();
            AssertNear(tickHpBefore - 3f, tickTarget.CurrentHp,
                "Turn-start status damage must change runtime HP");
            AssertTrue(firstTick.Changed &&
                    tickTarget.Statuses.Contains("probe_burn_tick"),
                "A duration-two status must remain after its first tick");
            DRPGStatusTurnResult secondTick =
                tickTarget.TickStatusesAtTurnStart();
            AssertNear(tickHpBefore - 6f, tickTarget.CurrentHp,
                "A duration-two status must tick exactly twice");
            AssertFalse(tickTarget.Statuses.Contains("probe_burn_tick"),
                "An expired status must leave the runtime collection");
            AssertEqual(1, secondTick.ExpiredStatusIds.Count,
                "Status expiry must be observable by presentation and debug tools");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetProfile);
            UnityEngine.Object.DestroyImmediate(actorProfile);
            UnityEngine.Object.DestroyImmediate(burn);
            UnityEngine.Object.DestroyImmediate(exposed);
            UnityEngine.Object.DestroyImmediate(focus);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        // The still-supported Legacy TurnStart checks above remain until 10D.
        // The approved Standard path additionally proves committed duration/
        // expiry only becomes visible at its own ledger cursor, not via tick UI.
        DRPGSettlementLedgerProbe.BoundaryConsumptionCommitsWithVitalsOnce();
        DRPGSettlementWidgetsProbe.ExpiredStatusesLeaveOnlyOnTheirOwnCursor();
        return "PASS DRPG unified status lifecycle";
    }

    public static string RunActionSpecContributors()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGPassiveModifierSO passive =
            ScriptableObject.CreateInstance<DRPGPassiveModifierSO>();
        DRPGCombatantProfileSO profile = CreateProfile(30f);

        try
        {
            SetField(skill, "skillId", "probe_action_contributors");
            SetField(skill, "power", 100f);
            SetField(skill, "manaCost", 20f);
            SetField(skill, "postureCost", 10f);
            SetField(skill, "effects", new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.Damage(
                    DRPGActionEffectTarget.PrimaryTarget,
                    1f,
                    DRPGDamageTag.Slash)
            });
            SetField(profile, "skills", new List<DRPGSkillSO> { skill });

            var applied = new DRPGActionSpecModifierDefinition();
            SetField(applied, "modifierId", "probe_modifier");
            SetField(applied, "skillPowerAdditive", 10f);
            SetField(applied, "skillPowerMultiplier", 2f);
            SetField(applied, "outgoingDamageMultiplier", 1.5f);
            SetField(applied, "manaCostAdditive", 2f);
            SetField(applied, "manaCostMultiplier", 0.5f);
            SetField(applied, "addDamageTags", DRPGDamageTag.Frost);
            SetField(applied, "removeDamageTags", DRPGDamageTag.Slash);
            SetField(
                applied,
                "targetFormationMode",
                DRPGActionFormationModifierMode.Intersect);
            SetField(
                applied,
                "targetFormationRows",
                DRPGFormationRowMask.Front | DRPGFormationRowMask.Middle);

            var duplicate = new DRPGActionSpecModifierDefinition();
            SetField(duplicate, "modifierId", "probe_modifier");
            SetField(duplicate, "skillPowerAdditive", 900f);

            var skipped = new DRPGActionSpecModifierDefinition();
            SetField(skipped, "modifierId", "wrong_skill_only");
            SetField(skipped, "skillPowerAdditive", 500f);
            var condition = new DRPGActionModifierCondition();
            SetField(condition, "requiredSkillId", "another_skill");
            SetField(skipped, "condition", condition);

            SetField(passive, "modifierId", "probe_passive");
            SetField(
                passive,
                "actionModifiers",
                new List<DRPGActionSpecModifierDefinition>
                {
                    applied,
                    duplicate,
                    skipped
                });

            var build = new DRPGResolvedCombatantBuild(
                profile,
                null,
                skill,
                new[] { skill },
                new[] { skill },
                new[] { passive });

            // A resolved Build must own immutable modifier snapshots.
            SetField(applied, "skillPowerAdditive", 999f);
            SetField(applied, "outgoingDamageMultiplier", 9f);

            var actor = new BattleParticipantRuntime(
                501,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0,
                    build: build));
            var command = new DRPGBattleCommand(501, 502, skill);
            DRPGResolvedActionSpec spec =
                DRPGActionSpecResolver.Resolve(command, actor);

            AssertNear(220f, spec.Effects[0].Magnitude,
                "Action power must use (base + additive) * multiplier");
            AssertNear(1.5f, spec.ActorDamageMultiplier,
                "Action-only damage multipliers must enter the frozen spec once");
            AssertNear(11f, spec.ManaCost,
                "Action resource cost must use (base + additive) * multiplier");
            AssertNear(10f, spec.PostureCost,
                "Untouched action fields must preserve their legacy value");
            AssertEqual(DRPGDamageTag.Frost, spec.Effects[0].DamageTags,
                "Damage tags must support deterministic add and remove operations");
            AssertEqual(
                DRPGFormationRowMask.Front | DRPGFormationRowMask.Middle,
                spec.TargetFormationRows,
                "Formation contribution must be frozen into target eligibility");
            AssertEqual(3, spec.ContributionTrace.Count,
                "Applied, duplicate, and condition-skipped sources must be traceable");
            AssertTrue(spec.ContributionTrace[0].WasApplied,
                "The first stable source must apply");
            AssertFalse(spec.ContributionTrace[1].WasApplied,
                "A duplicate stable source must not apply twice");
            AssertTrue(
                spec.ContributionTrace[1].SkipReason.Contains("duplicate"),
                "Duplicate trace must explain why it was skipped");
            AssertFalse(spec.ContributionTrace[2].WasApplied,
                "A condition mismatch must remain observable without mutating the action");
            string traceText = DRPGActionSpecTraceFormatter.Format(spec, 42);
            AssertTrue(traceText.Contains("Action #42"),
                "Formatted action traces must identify the prepared action");
            AssertTrue(traceText.Contains("probe_modifier"),
                "Formatted action traces must name contributing sources");
            AssertTrue(traceText.Contains("duplicate stable source ID"),
                "Formatted action traces must retain skipped-source reasons");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(passive);
            UnityEngine.Object.DestroyImmediate(skill);
        }

        return "PASS DRPG action-spec contributors";
    }

    public static string RunEquipmentActionSpecContributors()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO granted = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO profile = CreateProfile(30f);
        DRPGWeaponDefinitionSO weaponDefinition =
            ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        DRPGWeaponAffixDefinitionSO affix =
            ScriptableObject.CreateInstance<DRPGWeaponAffixDefinitionSO>();
        DRPGWeaponCardDefinitionSO card =
            ScriptableObject.CreateInstance<DRPGWeaponCardDefinitionSO>();
        DRPGWeaponAttachmentDefinitionSO attachment =
            ScriptableObject.CreateInstance<DRPGWeaponAttachmentDefinitionSO>();
        DRPGAccessoryDefinitionSO accessory =
            ScriptableObject.CreateInstance<DRPGAccessoryDefinitionSO>();
        DRPGOutfitDefinitionSO outfit =
            ScriptableObject.CreateInstance<DRPGOutfitDefinitionSO>();
        DRPGTraitDefinitionSO trait =
            ScriptableObject.CreateInstance<DRPGTraitDefinitionSO>();
        DRPGPassiveModifierSO cardPassive =
            ScriptableObject.CreateInstance<DRPGPassiveModifierSO>();

        try
        {
            SetField(attack, "skillId", "probe_equipment_attack");
            SetField(attack, "power", 100f);
            SetField(attack, "effects", new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.Damage(
                    DRPGActionEffectTarget.PrimaryTarget)
            });
            SetField(granted, "skillId", "probe_card_skill");
            SetField(profile, "skills", new List<DRPGSkillSO> { attack });

            SetField(weaponDefinition, "weaponId", "probe_weapon");
            SetField(
                weaponDefinition,
                "rarity",
                DRPGEquipmentRarity.RedGold);
            SetField(weaponDefinition, "baseDurability", 100);
            SetField(weaponDefinition, "actionModifiers", new List<DRPGActionSpecModifierDefinition>
            {
                CreatePowerModifier("weapon_power", 1f)
            });

            SetField(affix, "affixId", "probe_affix");
            SetField(affix, "actionModifiers", new List<DRPGActionSpecModifierDefinition>
            {
                CreatePowerModifier("affix_power", 2f)
            });

            SetField(cardPassive, "modifierId", "probe_card_passive");
            SetField(card, "cardId", "probe_card");
            SetField(card, "cost", 1);
            SetField(card, "grantedSkill", granted);
            SetField(card, "passiveModifiers", new List<DRPGPassiveModifierSO>
            {
                cardPassive
            });
            SetField(card, "actionModifiers", new List<DRPGActionSpecModifierDefinition>
            {
                CreatePowerModifier("card_power", 4f)
            });

            SetField(attachment, "attachmentId", "probe_attachment");
            SetField(attachment, "actionModifiers", new List<DRPGActionSpecModifierDefinition>
            {
                CreatePowerModifier("attachment_power", 8f)
            });

            SetField(accessory, "accessoryId", "probe_accessory");
            SetField(accessory, "actionModifiers", new List<DRPGActionSpecModifierDefinition>
            {
                CreatePowerModifier("accessory_power", 16f)
            });
            SetField(trait, "traitId", "probe_trait");
            SetField(trait, "actionModifiers", new List<DRPGActionSpecModifierDefinition>
            {
                CreatePowerModifier("trait_power", 32f)
            });
            SetField(outfit, "outfitId", "probe_outfit");
            SetField(outfit, "grantedTrait", trait);

            DRPGWeaponInstance weapon = DRPGWeaponInstance.Create(
                "probe_weapon_instance",
                weaponDefinition,
                new ProbeRandomSource(),
                new[] { affix },
                new[] { card },
                new[] { attachment });
            var registry = new DRPGEquipmentDefinitionRegistry(
                new[] { weaponDefinition },
                new[] { outfit },
                new[] { accessory },
                new[] { trait },
                new[] { affix },
                new[] { card },
                new[] { attachment });
            var characterEquipment = new DRPGCharacterEquipmentState(
                "probe_equipment_character",
                weapon);
            AssertTrue(
                characterEquipment.TrySetOutfit(outfit).Success,
                "The probe outfit must bind its source-owned trait");
            AssertTrue(
                characterEquipment.TrySetAccessory(0, accessory).Success,
                "The probe accessory must occupy a character slot");

            DRPGResolvedEquipmentSnapshot equipment =
                DRPGResolvedEquipmentSnapshot.FromCharacterEquipment(
                    characterEquipment,
                    string.Empty,
                    registry);
            AssertEqual(6, equipment.ActionModifiers.Count,
                "Weapon, affix, card, attachment, accessory, and trait contributors must be resolved");
            AssertEqual(1, equipment.PassiveModifiers.Count,
                "Equipment passive modifiers must no longer be dropped");
            AssertEqual(1, equipment.EquipmentGrantedSkills.Count,
                "Weapon-card skills must enter the battle skill snapshot");

            var build = new DRPGResolvedCombatantBuild(
                profile,
                null,
                attack,
                new[] { attack },
                new[] { attack },
                Array.Empty<DRPGPassiveModifierSO>(),
                null,
                equipment,
                Array.Empty<DRPGSkillSO>());
            var actor = new BattleParticipantRuntime(
                601,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0,
                    build: build));
            DRPGResolvedActionSpec spec = DRPGActionSpecResolver.Resolve(
                new DRPGBattleCommand(601, 602, attack),
                actor);
            AssertNear(163f, spec.Effects[0].Magnitude,
                "Every equipment layer must contribute through one action pipeline");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cardPassive);
            UnityEngine.Object.DestroyImmediate(trait);
            UnityEngine.Object.DestroyImmediate(outfit);
            UnityEngine.Object.DestroyImmediate(accessory);
            UnityEngine.Object.DestroyImmediate(attachment);
            UnityEngine.Object.DestroyImmediate(card);
            UnityEngine.Object.DestroyImmediate(affix);
            UnityEngine.Object.DestroyImmediate(weaponDefinition);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(granted);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS DRPG equipment action-spec contributors";
    }

    public static string RunActionSpecSurfaceAndRelease()
    {
        DRPGSkillSO freeStrike = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO telegraphed = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGPassiveModifierSO passive =
            ScriptableObject.CreateInstance<DRPGPassiveModifierSO>();
        DRPGCombatantProfileSO profile = CreateProfile(30f);

        try
        {
            SetField(freeStrike, "skillId", "probe_free_surface");
            SetField(
                freeStrike,
                "executionMode",
                DRPGSkillExecutionMode.FreeStrike);
            SetField(freeStrike, "power", 10f);
            SetField(freeStrike, "freeStrikeDuration", 2f);
            SetField(freeStrike, "freeStrikeCadence", 0.2f);
            SetField(freeStrike, "freeStrikePostureCost", 5f);
            SetField(freeStrike, "freeStrikeLineLength", 0.5f);
            SetField(freeStrike, "freeStrikeLineWidth", 0.1f);
            SetField(freeStrike, "freeStrikeSecondaryRadius", 0.2f);
            SetField(
                freeStrike,
                "freeStrikeSecondaryDamageMultiplier",
                0.5f);

            SetField(telegraphed, "skillId", "probe_guard_surface");
            SetField(
                telegraphed,
                "executionMode",
                DRPGSkillExecutionMode.TelegraphedSpecial);
            SetField(telegraphed, "manaCost", 9f);
            SetField(telegraphed, "postureCost", 6f);
            SetField(telegraphed, "guardLeadSeconds", 0.4f);
            SetField(telegraphed, "perfectLeadSeconds", 0.1f);
            SetField(telegraphed, "guardedDamageMultiplier", 0.5f);
            SetField(telegraphed, "missedGuardPostureDamage", 20f);
            SetField(telegraphed, "guardedPostureDamage", 10f);

            var modifier = new DRPGActionSpecModifierDefinition();
            SetField(modifier, "modifierId", "surface_modifier");
            SetField(modifier, "manaCostAdditive", 3f);
            SetField(modifier, "postureCostAdditive", 4f);
            SetField(modifier, "freeStrikeDurationAdditive", 1f);
            SetField(modifier, "freeStrikeDurationMultiplier", 2f);
            SetField(modifier, "freeStrikeCadenceAdditive", 0.1f);
            SetField(modifier, "freeStrikeCadenceMultiplier", 0.5f);
            SetField(modifier, "freeStrikePostureCostAdditive", 1f);
            SetField(modifier, "freeStrikePostureCostMultiplier", 2f);
            SetField(modifier, "freeStrikeLineLengthAdditive", 0.5f);
            SetField(modifier, "freeStrikeLineLengthMultiplier", 2f);
            SetField(modifier, "freeStrikeLineWidthAdditive", 0.1f);
            SetField(modifier, "freeStrikeLineWidthMultiplier", 2f);
            SetField(modifier, "overrideFreeStrikeShape", true);
            SetField(modifier, "freeStrikeShape", DRPGFreeStrikeShape.Line);
            SetField(modifier, "overrideFreeStrikeContactPolicy", true);
            SetField(
                modifier,
                "freeStrikeContactPolicy",
                DRPGFreeStrikeContactPolicy.LimitedContacts);
            SetField(modifier, "freeStrikeMaxPrimaryContacts", 3);
            SetField(modifier, "overrideSecondaryImpactKind", true);
            SetField(
                modifier,
                "secondaryImpactKind",
                DRPGSecondaryImpactKind.RadialSplash);
            SetField(modifier, "secondaryRadiusAdditive", 0.1f);
            SetField(modifier, "secondaryRadiusMultiplier", 2f);
            SetField(modifier, "secondaryDamageMultiplier", 2f);
            SetField(modifier, "guardLeadSecondsAdditive", 0.1f);
            SetField(modifier, "guardLeadSecondsMultiplier", 2f);
            SetField(modifier, "perfectLeadSecondsAdditive", 0.05f);
            SetField(modifier, "perfectLeadSecondsMultiplier", 2f);
            SetField(modifier, "guardedDamageMultiplier", 0.5f);
            SetField(modifier, "missedGuardPostureDamageAdditive", 5f);
            SetField(modifier, "missedGuardPostureDamageMultiplier", 2f);
            SetField(modifier, "guardedPostureDamageAdditive", 5f);
            SetField(modifier, "guardedPostureDamageMultiplier", 2f);
            var additionalEffects = new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.Shield(
                    DRPGActionEffectTarget.Actor,
                    7f)
            };
            SetField(modifier, "additionalEffects", additionalEffects);
            SetField(passive, "modifierId", "surface_passive");
            SetField(
                passive,
                "actionModifiers",
                new List<DRPGActionSpecModifierDefinition> { modifier });

            var build = new DRPGResolvedCombatantBuild(
                profile,
                null,
                freeStrike,
                new[] { freeStrike, telegraphed },
                new[] { freeStrike, telegraphed },
                new[] { passive });
            var actor = new BattleParticipantRuntime(
                701,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0,
                    build: build));

            // The Build must own the additional-effect snapshot too.
            additionalEffects.Clear();
            DRPGResolvedActionSpec freeSpec = DRPGActionSpecResolver.Resolve(
                new DRPGBattleCommand(701, 702, freeStrike),
                actor);
            AssertEqual(2, freeSpec.Effects.Count,
                "A frozen additional effect must survive source mutation");
            AssertNear(7f, freeSpec.Effects[1].Magnitude,
                "Additional effect magnitude must be frozen");
            AssertTrue(freeSpec.HasFreeStrikeSpec,
                "Free-strike skills must expose an interactive specification");
            AssertEqual(DRPGFreeStrikeShape.Line, freeSpec.FreeStrikeSpec.Shape,
                "Free-strike shape override was lost");
            AssertNear(6f, freeSpec.FreeStrikeSpec.Duration,
                "Free-strike duration must use additive-then-multiplicative order");
            AssertNear(0.15f, freeSpec.FreeStrikeSpec.Cadence,
                "Free-strike cadence must use additive-then-multiplicative order");
            AssertNear(12f, freeSpec.FreeStrikeSpec.PostureCost,
                "Free-strike per-impact posture cost was not resolved");
            AssertNear(2f, freeSpec.FreeStrikeSpec.LineLength,
                "Free-strike geometry was not resolved");
            AssertEqual(3, freeSpec.FreeStrikeSpec.MaxPrimaryContacts,
                "Contact policy and limit must resolve together");
            AssertEqual(
                DRPGSecondaryImpactKind.RadialSplash,
                freeSpec.FreeStrikeSpec.SecondaryImpactKind,
                "Secondary impact override was lost");
            AssertNear(0.6f, freeSpec.FreeStrikeSpec.SecondaryRadius,
                "Secondary radius was not resolved");
            AssertTrue(typeof(DRPGResolvedFreeStrikeSpec).GetProperty(
                    "PartDurabilityDamage") == null,
                "Body-only action specs must not expose part durability");
            AssertTrue(typeof(DRPGResolvedFreeStrikeSpec).GetProperty(
                    "WeakPointDamageMultiplier") == null,
                "Body-only action specs must not expose spatial weak-point multipliers");

            DRPGResolvedActionSpec guardSpec = DRPGActionSpecResolver.Resolve(
                new DRPGBattleCommand(701, 702, telegraphed),
                actor);
            AssertTrue(guardSpec.GuardSpec != null,
                "Telegraphed specials must expose a guard specification");
            AssertNear(1f, guardSpec.GuardSpec.GuardLeadSeconds,
                "Guard lead window was not resolved");
            AssertNear(0.3f, guardSpec.GuardSpec.PerfectLeadSeconds,
                "Perfect guard window was not resolved");
            AssertNear(0.25f, guardSpec.GuardSpec.GuardedDamageMultiplier,
                "Guard damage contribution was not resolved");
            AssertNear(50f, guardSpec.GuardSpec.MissedGuardPostureDamage,
                "Missed-guard posture contribution was not resolved");
            AssertNear(30f, guardSpec.GuardSpec.GuardedPostureDamage,
                "Guarded posture contribution was not resolved");

            DRPGResolvedActionSpec releaseSpec = DRPGActionSpecResolver.Resolve(
                new DRPGBattleCommand(
                    701,
                    702,
                    telegraphed,
                    DRPGBattleCommandStage.Release),
                actor);
            AssertNear(0f, releaseSpec.ManaCost,
                "A release command must never reintroduce reserved MP cost");
            AssertNear(0f, releaseSpec.PostureCost,
                "A release command must never reintroduce reserved posture cost");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(passive);
            UnityEngine.Object.DestroyImmediate(telegraphed);
            UnityEngine.Object.DestroyImmediate(freeStrike);
        }

        return "PASS DRPG action-spec surface and release invariants";
    }

    private static DRPGActionSpecModifierDefinition CreatePowerModifier(
        string modifierId,
        float additive)
    {
        var result = new DRPGActionSpecModifierDefinition();
        SetField(result, "modifierId", modifierId);
        SetField(result, "skillPowerAdditive", additive);
        return result;
    }

    private sealed class ProbeRandomSource : IDRPGRandomSource
    {
        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return maximumInclusive;
        }

        public int RangeInclusive(int minimumInclusive, int maximumInclusive)
        {
            return maximumInclusive;
        }
    }

    private static BattleParticipantRuntime CreateParticipant(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, 0));
    }

    private static DRPGCombatantProfileSO CreateProfile(float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
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
        if (Math.Abs(expected - actual) > 0.001f)
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
