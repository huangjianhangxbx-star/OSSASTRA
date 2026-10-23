#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleProgressPipelineProbe
{
    public static string Run()
    {
        RunPipelineRulesContract();
        RunSessionIntegrationContract();
        return "PASS deterministic battle progress pipeline";
    }

    private static void RunPipelineRulesContract()
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", "progress_player");
        SetField(profile, "maxHp", 100f);
        BattleParticipantRuntime participant = new BattleParticipantRuntime(
            4401,
            new BattleParticipantSpec(
                profile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                0));

        try
        {
            var ledger = new DRPGBattleProgressLedger();
            var router = new DRPGProgressStoreRouter(
                new IDRPGProgressStore[] { ledger });
            var pipeline = new DRPGBattleProgressPipeline(
                new IDRPGBattleProgressSource[]
                {
                    new InvalidMutationSource(),
                    new GainSource("source.low", 10, 4f, false),
                    new GainSource("source.high", 20, 8f, true)
                },
                router);
            var definition = new DRPGProgressResourceDefinition(
                "focus",
                DRPGProgressScope.Battle,
                10f);

            AssertTrue(pipeline.TryRegister(
                "progress_player",
                definition,
                out DRPGProgressMutationResult registerResult,
                out string error), error);
            AssertEqual(DRPGProgressMutationState.Applied, registerResult.State,
                "Battle resource registration must pass through the pipeline");

            DRPGBattleEventBatch root = CreateBatch(
                100L,
                0,
                1L,
                participant.RuntimeId);
            DRPGBattleEventBatch reaction = CreateBatch(
                100L,
                1,
                2L,
                participant.RuntimeId);
            DRPGBattleProgressRun run = pipeline.Process(
                new[] { root, reaction },
                new[] { participant });

            AssertEqual(2, run.Mutations.Count,
                "Only two distinct root-depth gains should be applied");
            AssertEqual("source.high", run.Mutations[0].Request.SourceId,
                "Higher priority source must resolve first");
            AssertEqual("source.low", run.Mutations[1].Request.SourceId,
                "Lower priority source must resolve second");
            AssertNear(8f, run.Mutations[0].AppliedAmount,
                "High-priority gain should apply before capacity fills");
            AssertNear(2f, run.Mutations[1].AppliedAmount,
                "Low-priority gain should be clamped by capacity");
            AssertNear(2f, run.Mutations[1].OverflowAmount,
                "Capacity overflow must be recorded");
            AssertTrue(ReferenceEquals(root, run.ObservedBatches[0]),
                "Progress must observe the exact root event batch object");
            AssertTrue(ReferenceEquals(reaction, run.ObservedBatches[1]),
                "Progress must observe the exact reaction batch object");

            AssertTrue(ledger.TryGetSnapshot(
                "progress_player",
                "focus",
                out DRPGProgressSnapshot snapshot),
                "Registered battle progress must be queryable");
            AssertNear(10f, snapshot.CurrentValue,
                "Ledger value must stop at capacity");
            AssertNear(10f, snapshot.Capacity,
                "Ledger must retain resource capacity without Unity Objects");
            AssertTraceContains(run, DRPGProgressTraceState.SkippedDuplicate);
            AssertTraceContains(run, DRPGProgressTraceState.SkippedReactionDepth);
            AssertTraceContains(run, DRPGProgressTraceState.RejectedInvalidOwner);
            AssertTraceContains(run, DRPGProgressTraceState.RejectedInvalidResource);
            AssertTraceContains(run, DRPGProgressTraceState.RejectedUnsupportedScope);

            var expedition = new DRPGProgressResourceDefinition(
                "resonance",
                DRPGProgressScope.Expedition,
                3f);
            AssertTrue(!pipeline.TryRegister(
                    "progress_player",
                    expedition,
                    out DRPGProgressMutationResult expeditionResult,
                    out _),
                "P3 router must safely reject Expedition registration");
            AssertEqual(
                DRPGProgressMutationState.RejectedUnsupportedScope,
                expeditionResult.State,
                "Unsupported scope must be diagnostic, not an exception");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    private static void RunSessionIntegrationContract()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(attack, "skillId", "probe_progress_session_attack");
        SetField(attack, "power", 1f);
        SetField(attack, "effects", new List<DRPGActionEffectDefinition>
        {
            DRPGActionEffectDefinition.Damage(
                DRPGActionEffectTarget.PrimaryTarget,
                1f,
                DRPGDamageTag.Slash)
        });
        DRPGCombatantProfileSO playerProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO enemyProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(playerProfile, "combatantId", "progress_session_player");
        SetField(playerProfile, "agility", 30f);
        SetField(playerProfile, "maxHp", 100f);
        SetField(playerProfile, "skills", new List<DRPGSkillSO> { attack });
        SetField(enemyProfile, "combatantId", "progress_session_enemy");
        SetField(enemyProfile, "agility", 10f);
        SetField(enemyProfile, "maxHp", 100f);
        SetField(enemyProfile, "skills", new List<DRPGSkillSO> { attack });

        try
        {
            BattleParticipantRuntime player = new BattleParticipantRuntime(
                4501,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            BattleParticipantRuntime enemy = new BattleParticipantRuntime(
                4502,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var progressPipeline = new DRPGBattleProgressPipeline(
                new IDRPGBattleProgressSource[]
                {
                    new GainSource("source.session", 0, 1f, false)
                });
            AssertTrue(progressPipeline.TryRegister(
                "progress_session_player",
                new DRPGProgressResourceDefinition(
                    "focus",
                    DRPGProgressScope.Battle,
                    3f),
                out _,
                out string error), error);
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { player, enemy },
                progressPipeline: progressPipeline);
            int changedCount = 0;
            DRPGProgressMutationResult publishedMutation = null;
            session.ProgressChanged += mutation =>
            {
                changedCount++;
                publishedMutation = mutation;
            };
            session.Start();
            AssertTrue(session.TryPrepareCommand(
                DRPGBattleCommand.BasicAttack(
                    player.RuntimeId,
                    enemy.RuntimeId,
                    attack),
                out DRPGBattlePreparedAction prepared,
                out error), error);
            AssertTrue(session.TryResolveImpact(prepared, out _, out error), error);

            AssertTrue(session.LastProgressRun != null,
                "Session must retain the latest progress run");
            AssertTrue(ReferenceEquals(
                    session.LastReactionRun.RootBatch,
                    session.LastProgressRun.ObservedBatches[0]),
                "Reaction and progress consumers must share the root batch object");
            AssertEqual(1, changedCount,
                "One applied mutation must publish one ProgressChanged event");
            AssertTrue(ReferenceEquals(
                    publishedMutation,
                    session.LastProgressRun.Mutations[0]),
                "Public progress event must expose the retained mutation object");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(attack);
        }
    }

    private sealed class GainSource : IDRPGBattleProgressSource
    {
        private readonly int priority;
        private readonly float amount;
        private readonly bool duplicate;

        public GainSource(
            string sourceId,
            int priority,
            float amount,
            bool duplicate)
        {
            SourceId = sourceId;
            this.priority = priority;
            this.amount = amount;
            this.duplicate = duplicate;
        }

        public string SourceId { get; }

        public void Collect(
            DRPGBattleProgressContext context,
            ICollection<DRPGProgressMutationRequest> requests)
        {
            string owner = context.ResolveOwnerStableId(
                context.Batch.Events[0].SourceActorRuntimeId);
            DRPGProgressMutationRequest request =
                DRPGProgressMutationRequest.Gain(
                    SourceId,
                    "root-gain",
                    priority,
                    owner,
                    "focus",
                    DRPGProgressScope.Battle,
                    amount);
            requests.Add(request);
            if (duplicate)
                requests.Add(request);
        }
    }

    private sealed class InvalidMutationSource : IDRPGBattleProgressSource
    {
        public string SourceId => "source.invalid";

        public void Collect(
            DRPGBattleProgressContext context,
            ICollection<DRPGProgressMutationRequest> requests)
        {
            if (context.Batch.Depth > 0)
                return;

            requests.Add(DRPGProgressMutationRequest.Gain(
                SourceId,
                "bad-owner",
                -10,
                "missing_owner",
                "focus",
                DRPGProgressScope.Battle,
                1f));
            requests.Add(DRPGProgressMutationRequest.Gain(
                SourceId,
                "bad-resource",
                -11,
                "progress_player",
                "missing_resource",
                DRPGProgressScope.Battle,
                1f));
            requests.Add(DRPGProgressMutationRequest.Gain(
                SourceId,
                "bad-scope",
                -12,
                "progress_player",
                "resonance",
                DRPGProgressScope.Expedition,
                1f));
        }
    }

    private static DRPGBattleEventBatch CreateBatch(
        long rootActionId,
        int depth,
        long eventId,
        int actorRuntimeId)
    {
        return new DRPGBattleEventBatch(
            rootActionId,
            depth,
            new[]
            {
                new DRPGBattleEvent(
                    eventId,
                    rootActionId,
                    depth,
                    DRPGBattleEventKind.ActionImpactResolved,
                    actorRuntimeId,
                    actorRuntimeId,
                    "probe.progress")
            });
    }

    private static void AssertTraceContains(
        DRPGBattleProgressRun run,
        DRPGProgressTraceState expected)
    {
        for (int i = 0; i < run.Trace.Count; i++)
        {
            if (run.Trace[i].State == expected)
                return;
        }
        throw new Exception("Missing progress trace state: " + expected);
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
}
#endif
