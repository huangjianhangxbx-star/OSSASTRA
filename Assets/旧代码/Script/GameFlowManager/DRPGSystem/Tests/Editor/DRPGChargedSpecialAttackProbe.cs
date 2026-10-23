#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGChargedSpecialAttackProbe
{
    public static string Run()
    {
        VerifyAtomicInputTransaction();
        VerifySessionAtomicInputs();

        DRPGProgressResourceSO resource =
            ScriptableObject.CreateInstance<DRPGProgressResourceSO>();
        DRPGSkillSO sourceSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO chargedSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO guardProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO ankaluteProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            SetField(resource, "resourceId", "blood_round");
            SetField(resource, "scope", DRPGProgressScope.Battle);
            SetField(resource, "baseCapacity", 0f);
            DRPGProgressResourceDefinition definition =
                resource.CreateDefinition(6f);
            AssertEqual("blood_round", definition.ResourceId,
                "Resource SO must expose a stable ID");
            AssertNear(6f, definition.Capacity,
                "Weapon capacity contribution must be applied generically");
            AssertNear(0f, definition.InitialValue,
                "Battle charge must begin empty");

            SetField(sourceSkill, "skillId", "guard_valid_attack");
            var rule = new DRPGProgressGainRuleDefinition(
                "guard_to_ankalute_blood_round",
                resource,
                DRPGBattleEventKind.ActionImpactResolved,
                1f,
                DRPGProgressOwnerSelection.ExplicitCharacter,
                "ankalute");
            var source = new DRPGConfiguredProgressSource(
                "configured.charge.probe",
                new[]
                {
                    new DRPGConfiguredProgressRuleBinding(
                        sourceSkill.SkillId,
                        rule)
                });

            SetField(guardProfile, "combatantId", "guard");
            SetField(ankaluteProfile, "combatantId", "ankalute");
            BattleParticipantRuntime guard = Participant(
                5101,
                guardProfile,
                DRPGBattleRosterRole.Protagonist,
                0);
            BattleParticipantRuntime ankalute = Participant(
                5102,
                ankaluteProfile,
                DRPGBattleRosterRole.PartyMember,
                1);

            var pipeline = new DRPGBattleProgressPipeline(
                new IDRPGBattleProgressSource[] { source });
            AssertTrue(pipeline.TryRegister(
                "ankalute",
                definition,
                out _,
                out string error), error);
            DRPGBattleProgressRun run = pipeline.Process(
                new[]
                {
                    Batch(1L, 1L, guard.RuntimeId, sourceSkill.SkillId),
                    Batch(2L, 2L, guard.RuntimeId, sourceSkill.SkillId),
                    Batch(3L, 3L, guard.RuntimeId, sourceSkill.SkillId),
                    Batch(4L, 4L, guard.RuntimeId, sourceSkill.SkillId),
                    Batch(5L, 5L, guard.RuntimeId, sourceSkill.SkillId),
                    Batch(6L, 6L, guard.RuntimeId, sourceSkill.SkillId),
                    Batch(7L, 7L, guard.RuntimeId, sourceSkill.SkillId)
                },
                new[] { guard, ankalute });
            AssertTrue(pipeline.TryGetSnapshot(
                "ankalute",
                "blood_round",
                DRPGProgressScope.Battle,
                out DRPGProgressSnapshot snapshot),
                "Registered charge must remain queryable");
            AssertNear(6f, snapshot.CurrentValue,
                "Seven valid attacks must clamp at revolver capacity six");
            AssertNear(1f, SumOverflow(run),
                "The seventh gain must be retained as overflow diagnostics");

            SetField(chargedSkill, "skillId", "free_shot");
            SetField(chargedSkill, "executionMode", DRPGSkillExecutionMode.FreeStrike);
            SetField(chargedSkill, "inputProgressCost",
                new DRPGSkillProgressCostDefinition(resource, 1f));
            SetField(chargedSkill, "selfPostureDepletionPolicy",
                DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak);
            SetField(ankaluteProfile, "skills",
                new List<DRPGSkillSO> { chargedSkill });
            BattleParticipantRuntime chargedActor = Participant(
                5103,
                ankaluteProfile,
                DRPGBattleRosterRole.PartyMember,
                0);
            DRPGResolvedActionSpec resolved = DRPGActionSpecResolver.Resolve(
                new DRPGBattleCommand(
                    chargedActor.RuntimeId,
                    guard.RuntimeId,
                    chargedSkill),
                chargedActor);
            AssertEqual("blood_round", resolved.InputProgressCost.ResourceId,
                "Prepare must freeze the input charge resource ID");
            AssertNear(1f, resolved.InputProgressCost.Amount,
                "Prepare must freeze per-accepted-input charge cost");
            AssertEqual(
                DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak,
                resolved.SelfPostureDepletionPolicy,
                "Self-break timing must be an immutable action-spec policy");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ankaluteProfile);
            UnityEngine.Object.DestroyImmediate(guardProfile);
            UnityEngine.Object.DestroyImmediate(chargedSkill);
            UnityEngine.Object.DestroyImmediate(sourceSkill);
            UnityEngine.Object.DestroyImmediate(resource);
        }

        return "PASS charged special-attack foundation";
    }

    private static void VerifySessionAtomicInputs()
    {
        DRPGProgressResourceSO resource =
            ScriptableObject.CreateInstance<DRPGProgressResourceSO>();
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO actorProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO enemyProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            SetField(resource, "resourceId", "session_round");
            SetField(resource, "scope", DRPGProgressScope.Battle);
            SetField(resource, "baseCapacity", 2f);
            SetField(skill, "skillId", "session_free_shot");
            SetField(skill, "power", 10f);
            SetField(skill, "executionMode", DRPGSkillExecutionMode.FreeStrike);
            SetField(skill, "freeStrikePostureCost", 12f);
            SetField(skill, "inputProgressCost",
                new DRPGSkillProgressCostDefinition(resource, 1f));
            SetField(skill, "selfPostureDepletionPolicy",
                DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak);
            SetField(actorProfile, "combatantId", "session_actor");
            SetField(actorProfile, "maxHp", 100f);
            SetField(actorProfile, "maxPosture", 100f);
            SetField(actorProfile, "agility", 100f);
            SetField(actorProfile, "skills", new List<DRPGSkillSO> { skill });
            SetField(enemyProfile, "combatantId", "session_enemy");
            SetField(enemyProfile, "maxHp", 100f);
            SetField(enemyProfile, "agility", 1f);

            BattleParticipantRuntime actor = Participant(
                6201,
                actorProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember,
                0);
            BattleParticipantRuntime enemy = Participant(
                6202,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                0);
            var pipeline = new DRPGBattleProgressPipeline();
            AssertTrue(pipeline.TryRegister(
                actorProfile.CombatantId,
                resource.CreateDefinition(),
                out _,
                out string error), error);
            AssertTrue(pipeline.TryGain(
                actorProfile.CombatantId,
                resource.ResourceId,
                DRPGProgressScope.Battle,
                2f,
                out _,
                out error), error);
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { actor, enemy },
                progressPipeline: pipeline);
            session.Start();
            DRPGBattleSkillAvailability available =
                session.GetSkillAvailability(skill);
            AssertTrue(available.IsAvailable,
                "A charged FreeStrike must be selectable");
            AssertNear(2f, available.Progress.CurrentValue,
                "Skill availability must expose current charge");
            AssertNear(2f, available.Progress.Capacity,
                "Skill availability must expose charge capacity");
            string availableLabel =
                BattleSkillMenu.FormatAvailabilityLabel(available);
            AssertTrue(availableLabel.Contains("2/2"),
                "Charged skill labels must expose current/capacity");
            AssertTrue(session.TryPrepareCommand(
                new DRPGBattleCommand(actor.RuntimeId, enemy.RuntimeId, skill),
                out DRPGBattlePreparedAction action,
                out error), error);
            AssertTrue(session.TryBeginInteractiveResolution(action, out error), error);
            AssertTrue(session.TryGetInteractiveInputResourceSnapshot(
                action,
                out DRPGInteractiveInputResourceSnapshot opening),
                "An open FreeStrike must expose a read-only resource snapshot");
            AssertNear(2f, opening.Progress.CurrentValue,
                "Opening snapshot must match the battle ledger");

            AssertTrue(session.TryResolveFreeStrikeInput(
                action,
                Array.Empty<DRPGFreeStrikeBodyContact>(),
                out DRPGFreeStrikeInputResult miss,
                out error), error);
            AssertEqual(0, miss.Impacts.Count,
                "A miss must remain a valid accepted input");
            AssertProgress(pipeline, actorProfile.CombatantId, resource.ResourceId,
                1f, "A miss must consume exactly one charge");
            AssertNear(88f, actor.CurrentPosture,
                "A miss must consume the same posture as a hit");

            AssertTrue(action.LegacyMutationPort.TryCommitTestMutation(
                    action,
                    workspace => workspace.For(
                            new ParticipantId(actor.RuntimeId))
                        .SetPosture(8f),
                    out _,
                    out error),
                error);
            AssertFalse(session.TryResolveFreeStrikeInput(
                action,
                Array.Empty<DRPGFreeStrikeBodyContact>(),
                out _,
                out error),
                "Insufficient posture must reject an in-window click");
            AssertProgress(pipeline, actorProfile.CombatantId, resource.ResourceId,
                1f, "A rejected in-window click must preserve charge");
            AssertTrue(action.LegacyMutationPort.TryCommitTestMutation(
                    action,
                    workspace => workspace.For(
                            new ParticipantId(actor.RuntimeId))
                        .SetPosture(12f),
                    out _,
                    out error),
                error);

            float hpBefore = enemy.CurrentHp;
            AssertTrue(session.TryResolveFreeStrikeInput(
                action,
                new[]
                {
                    new DRPGFreeStrikeBodyContact(
                        enemy.RuntimeId,
                        0f,
                        new Vector2(0.5f, 0.5f),
                        1f,
                        0,
                        DRPGFreeStrikeImpactRole.Primary)
                },
                out DRPGFreeStrikeInputResult hit,
                out error), error);
            AssertTrue(enemy.CurrentHp < hpBefore,
                "A final charged hit must still resolve before self-break");
            AssertTrue(hit.ActorPostureExhausted,
                "Exact posture depletion must enter PostureBreak after impact");
            AssertTrue(hit.WindowResourceExhausted,
                "The last charge must request immediate safe window closure");
            AssertTrue(session.ActiveFreeStrikeWindow.IsResourceExhausted,
                "The active window must reject surplus clicks after zero charge");
            AssertProgress(pipeline, actorProfile.CombatantId, resource.ResourceId,
                0f, "The accepted hit must consume the final charge");
            DRPGBattleSkillAvailability exhausted =
                session.GetSkillAvailability(skill);
            AssertFalse(exhausted.IsAvailable,
                "A zero-charge skill must remain visible but disabled");
            AssertTrue(!string.IsNullOrWhiteSpace(exhausted.UnavailableReason),
                "A disabled charged skill must expose its reason");
            string exhaustedLabel =
                BattleSkillMenu.FormatAvailabilityLabel(exhausted);
            AssertTrue(exhaustedLabel.Contains("0/2") &&
                exhaustedLabel.Contains(exhausted.UnavailableReason),
                "A zero-charge entry must remain visible with its reason");
            AssertFalse(session.TryResolveFreeStrikeInput(
                action,
                Array.Empty<DRPGFreeStrikeBodyContact>(),
                out _,
                out error),
                "A resource-exhausted window must reject surplus clicks");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(actorProfile);
            UnityEngine.Object.DestroyImmediate(skill);
            UnityEngine.Object.DestroyImmediate(resource);
        }
    }

    private static void VerifyAtomicInputTransaction()
    {
        DRPGProgressResourceSO resource =
            ScriptableObject.CreateInstance<DRPGProgressResourceSO>();
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            SetField(resource, "resourceId", "atomic_round");
            SetField(resource, "scope", DRPGProgressScope.Battle);
            SetField(resource, "baseCapacity", 2f);
            SetField(skill, "skillId", "atomic_free_shot");
            SetField(skill, "executionMode", DRPGSkillExecutionMode.FreeStrike);
            SetField(skill, "freeStrikePostureCost", 12f);
            SetField(skill, "inputProgressCost",
                new DRPGSkillProgressCostDefinition(resource, 1f));
            SetField(skill, "selfPostureDepletionPolicy",
                DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak);
            SetField(profile, "combatantId", "atomic_actor");
            SetField(profile, "maxPosture", 100f);
            SetField(profile, "skills", new List<DRPGSkillSO> { skill });

            BattleParticipantRuntime actor = Participant(
                6101,
                profile,
                DRPGBattleRosterRole.PartyMember,
                0);
            DRPGResolvedActionSpec spec = DRPGActionSpecResolver.Resolve(
                new DRPGBattleCommand(actor.RuntimeId, 6102, skill),
                actor);
            var pipeline = new DRPGBattleProgressPipeline();
            AssertTrue(pipeline.TryRegister(
                profile.CombatantId,
                resource.CreateDefinition(),
                out _,
                out string error), error);
            AssertTrue(pipeline.TryGain(
                profile.CombatantId,
                resource.ResourceId,
                DRPGProgressScope.Battle,
                2f,
                out _,
                out error), error);

            actor.SpendPosture(95f);
            AssertFalse(DRPGInteractiveInputTransaction.TryPrepare(
                actor,
                spec,
                pipeline,
                null,
                null,
                out _,
                out error),
                "Insufficient posture must reject before any mutation");
            AssertProgress(pipeline, profile.CombatantId, resource.ResourceId, 2f,
                "Rejected input must preserve charge");
            actor.RestorePosture(95f);

            AssertTrue(DRPGInteractiveInputTransaction.TryPrepare(
                actor,
                spec,
                pipeline,
                null,
                null,
                out DRPGInteractiveInputTransaction first,
                out error), error);
            AssertTrue(first.TryCommit(
                actor,
                pipeline,
                null,
                null,
                out DRPGInteractiveInputReceipt firstReceipt,
                out error), error);
            AssertNear(12f, firstReceipt.SpentPosture,
                "Accepted input must commit posture exactly once");
            AssertProgress(pipeline, profile.CombatantId, resource.ResourceId, 1f,
                "Accepted input must commit exactly one charge");

            actor.SpendPosture(actor.CurrentPosture - 12f);
            AssertTrue(DRPGInteractiveInputTransaction.TryPrepare(
                actor,
                spec,
                pipeline,
                null,
                null,
                out DRPGInteractiveInputTransaction final,
                out error), error);
            AssertTrue(final.TryCommit(
                actor,
                pipeline,
                null,
                null,
                out DRPGInteractiveInputReceipt finalReceipt,
                out error), error);
            AssertTrue(finalReceipt.EnterPostureBreakAfterImpact,
                "Deferred self-break must be returned as a post-impact obligation");
            AssertFalse(actor.IsPostureBroken,
                "Deferred self-break must not begin before impact publication");
            actor.EnterDeferredPostureBreak();
            AssertTrue(actor.IsPostureBroken,
                "The deferred obligation must enter the normal PostureBreak lifecycle");
            AssertProgress(pipeline, profile.CombatantId, resource.ResourceId, 0f,
                "The final accepted input must exhaust charge");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(skill);
            UnityEngine.Object.DestroyImmediate(resource);
        }
    }

    private static void AssertProgress(
        DRPGBattleProgressPipeline pipeline,
        string owner,
        string resourceId,
        float expected,
        string message)
    {
        AssertTrue(pipeline.TryGetSnapshot(
            owner,
            resourceId,
            DRPGProgressScope.Battle,
            out DRPGProgressSnapshot snapshot), message);
        AssertNear(expected, snapshot.CurrentValue, message);
    }

    private static BattleParticipantRuntime Participant(
        int runtimeId,
        DRPGCombatantProfileSO profile,
        DRPGBattleRosterRole role,
        int order)
    {
        return Participant(
            runtimeId,
            profile,
            DRPGBattleSide.Player,
            role,
            order);
    }

    private static BattleParticipantRuntime Participant(
        int runtimeId,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        int order)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(
                profile,
                side,
                role,
                order));
    }

    private static DRPGBattleEventBatch Batch(
        long rootActionId,
        long eventId,
        int sourceRuntimeId,
        string skillId)
    {
        return new DRPGBattleEventBatch(
            rootActionId,
            0,
            new[]
            {
                new DRPGBattleEvent(
                    eventId,
                    rootActionId,
                    0,
                    DRPGBattleEventKind.ActionImpactResolved,
                    sourceRuntimeId,
                    0,
                    skillId)
            });
    }

    private static float SumOverflow(DRPGBattleProgressRun run)
    {
        float total = 0f;
        for (int i = 0; i < run.Mutations.Count; i++)
            total += run.Mutations[i].OverflowAmount;
        return total;
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
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Mathf.Abs(expected - actual) > 0.001f)
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
}
#endif
