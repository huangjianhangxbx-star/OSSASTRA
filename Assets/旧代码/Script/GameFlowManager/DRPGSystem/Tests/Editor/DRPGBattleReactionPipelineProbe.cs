#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleReactionPipelineProbe
{
    public static string Run()
    {
        RunResolutionOrderAndDeduplication();
        RunDepthAndBudgetLimits();
        return "PASS battle event and passive reaction pipeline";
    }

    private static void RunResolutionOrderAndDeduplication()
    {
        DRPGSkillSO attack = CreateDamageSkill("probe_reaction_root", 10f);
        DRPGSkillSO highShield = CreateShieldSkill("probe_shield_high", 3f);
        DRPGSkillSO lowShield = CreateShieldSkill("probe_shield_low", 5f);
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            30f,
            attack,
            highShield,
            lowShield);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            20f,
            attack,
            highShield,
            lowShield);

        try
        {
            BattleParticipantRuntime player = CreateParticipant(
                9101,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(
                9102,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var pipeline = new DRPGBattleReactionPipeline(
                new IDRPGBattleReactionSource[]
                {
                    new ShieldOnDamageSource(
                        "source.low",
                        "reaction.low",
                        10,
                        lowShield,
                        false),
                    new ShieldOnDamageSource(
                        "source.high",
                        "reaction.high",
                        20,
                        highShield,
                        true)
                },
                new DRPGBattleReactionOptions(8, 4));
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { player, enemy },
                reactionPipeline: pipeline);
            int actionResolvedCount = 0;
            int reactionResolvedCount = 0;
            int runCompletedCount = 0;
            int damageEventCount = 0;
            session.ActionResolved += _ => actionResolvedCount++;
            session.ReactionResolved += _ => reactionResolvedCount++;
            session.ReactionRunCompleted += _ => runCompletedCount++;
            session.BattleEventPublished += battleEvent =>
            {
                if (battleEvent.Kind == DRPGBattleEventKind.DamageApplied)
                    damageEventCount++;
            };
            session.Start();

            float hpBefore = enemy.CurrentHp;
            AssertTrue(session.TryPrepareCommand(
                DRPGBattleCommand.BasicAttack(
                    player.RuntimeId,
                    enemy.RuntimeId,
                    attack),
                out DRPGBattlePreparedAction prepared,
                out string error), error);
            AssertTrue(session.TryResolveImpact(
                prepared,
                out _,
                out error), error);

            AssertNear(hpBefore - 10f, enemy.CurrentHp,
                "Root damage must still resolve exactly once");
            AssertNear(8f, enemy.CurrentShield,
                "Both accepted shield reactions must resolve through the action resolver");
            AssertEqual(1, actionResolvedCount,
                "Reaction resolutions must not masquerade as root ActionResolved events");
            AssertEqual(2, reactionResolvedCount,
                "Duplicate requests must be suppressed");
            AssertEqual(1, runCompletedCount,
                "One root impact must publish one reaction run");
            AssertTrue(damageEventCount >= 1,
                "The root damage fact must be published");
            AssertTrue(session.LastReactionRun != null,
                "The session must retain the latest immutable reaction run");
            AssertEqual(2, session.LastReactionRun.Resolutions.Count,
                "Exactly two distinct reactions should resolve");
            AssertEqual(
                "reaction.high",
                session.LastReactionRun.Resolutions[0].Request.ReactionId,
                "Higher-priority reactions must resolve first regardless of source registration order");
            AssertEqual(
                "reaction.low",
                session.LastReactionRun.Resolutions[1].Request.ReactionId,
                "Lower-priority reactions must follow deterministically");
            AssertTraceContains(
                session.LastReactionRun,
                DRPGBattleReactionTraceState.SkippedDuplicate);
        }
        finally
        {
            Destroy(enemyProfile, playerProfile, lowShield, highShield, attack);
        }
    }

    private static void RunDepthAndBudgetLimits()
    {
        DRPGSkillSO rootAttack = CreateDamageSkill("probe_root_loop", 4f);
        DRPGSkillSO retaliation = CreateDamageSkill("probe_retaliation", 1f);
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            30f,
            rootAttack,
            retaliation);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            20f,
            rootAttack,
            retaliation);

        try
        {
            BattleParticipantRuntime player = CreateParticipant(
                9201,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(
                9202,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var depthPipeline = new DRPGBattleReactionPipeline(
                new IDRPGBattleReactionSource[]
                {
                    new RetaliateOnDamageSource(retaliation, false)
                },
                new DRPGBattleReactionOptions(8, 2));
            var depthSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { player, enemy },
                reactionPipeline: depthPipeline);
            depthSession.Start();
            AssertTrue(depthSession.TryPrepareCommand(
                DRPGBattleCommand.BasicAttack(
                    player.RuntimeId,
                    enemy.RuntimeId,
                    rootAttack),
                out DRPGBattlePreparedAction prepared,
                out string error), error);
            AssertTrue(depthSession.TryResolveImpact(
                prepared,
                out _,
                out error), error);
            AssertEqual(2, depthSession.LastReactionRun.Resolutions.Count,
                "Depth two must accept exactly two recursive reactions");
            AssertTraceContains(
                depthSession.LastReactionRun,
                DRPGBattleReactionTraceState.SkippedDepthLimit);

            BattleParticipantRuntime budgetPlayer = CreateParticipant(
                9301,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime budgetEnemy = CreateParticipant(
                9302,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var budgetPipeline = new DRPGBattleReactionPipeline(
                new IDRPGBattleReactionSource[]
                {
                    new RetaliateOnDamageSource(retaliation, true)
                },
                new DRPGBattleReactionOptions(1, 8));
            var budgetSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { budgetPlayer, budgetEnemy },
                reactionPipeline: budgetPipeline);
            budgetSession.Start();
            AssertTrue(budgetSession.TryPrepareCommand(
                DRPGBattleCommand.BasicAttack(
                    budgetPlayer.RuntimeId,
                    budgetEnemy.RuntimeId,
                    rootAttack),
                out prepared,
                out error), error);
            AssertTrue(budgetSession.TryResolveImpact(
                prepared,
                out _,
                out error), error);
            AssertEqual(1, budgetSession.LastReactionRun.Resolutions.Count,
                "Reaction budget one must resolve exactly one request");
            AssertTraceContains(
                budgetSession.LastReactionRun,
                DRPGBattleReactionTraceState.SkippedBudgetLimit);
        }
        finally
        {
            Destroy(enemyProfile, playerProfile, retaliation, rootAttack);
        }
    }

    private sealed class ShieldOnDamageSource : IDRPGBattleReactionSource
    {
        private readonly string reactionId;
        private readonly int priority;
        private readonly DRPGSkillSO skill;
        private readonly bool enqueueDuplicate;

        public ShieldOnDamageSource(
            string sourceId,
            string reactionId,
            int priority,
            DRPGSkillSO skill,
            bool enqueueDuplicate)
        {
            SourceId = sourceId;
            this.reactionId = reactionId;
            this.priority = priority;
            this.skill = skill;
            this.enqueueDuplicate = enqueueDuplicate;
        }

        public string SourceId { get; }

        public void Collect(
            DRPGBattleReactionContext context,
            ICollection<DRPGBattleReactionRequest> requests)
        {
            if (context.Event.Kind != DRPGBattleEventKind.DamageApplied)
                return;

            string key = SourceId + ":" + reactionId + ":" +
                context.Event.RootActionId;
            DRPGBattleReactionRequest request = context.CreateSkillRequest(
                SourceId,
                reactionId,
                key,
                priority,
                context.Event.TargetRuntimeId,
                context.Event.TargetRuntimeId,
                skill,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            requests.Add(request);
            if (enqueueDuplicate)
                requests.Add(request);
        }
    }

    private sealed class RetaliateOnDamageSource : IDRPGBattleReactionSource
    {
        private readonly DRPGSkillSO skill;
        private readonly bool duplicatePerEvent;

        public RetaliateOnDamageSource(
            DRPGSkillSO skill,
            bool duplicatePerEvent)
        {
            this.skill = skill;
            this.duplicatePerEvent = duplicatePerEvent;
        }

        public string SourceId => "source.retaliate";

        public void Collect(
            DRPGBattleReactionContext context,
            ICollection<DRPGBattleReactionRequest> requests)
        {
            if (context.Event.Kind != DRPGBattleEventKind.DamageApplied)
                return;

            Add("reaction.retaliate.a", context, requests);
            if (duplicatePerEvent)
                Add("reaction.retaliate.b", context, requests);
        }

        private void Add(
            string reactionId,
            DRPGBattleReactionContext context,
            ICollection<DRPGBattleReactionRequest> requests)
        {
            requests.Add(context.CreateSkillRequest(
                SourceId,
                reactionId,
                SourceId + ":" + reactionId + ":" + context.Event.EventId,
                0,
                context.Event.TargetRuntimeId,
                context.Event.SourceActorRuntimeId,
                skill,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }

    private static DRPGSkillSO CreateDamageSkill(string id, float power)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "power", power);
        SetField(skill, "effects", new List<DRPGActionEffectDefinition>
        {
            DRPGActionEffectDefinition.Damage(
                DRPGActionEffectTarget.PrimaryTarget,
                1f,
                DRPGDamageTag.Slash)
        });
        return skill;
    }

    private static DRPGSkillSO CreateShieldSkill(string id, float amount)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "targetRule", DRPGSkillTargetRule.Self);
        SetField(skill, "effects", new List<DRPGActionEffectDefinition>
        {
            DRPGActionEffectDefinition.Shield(
                DRPGActionEffectTarget.Actor,
                amount)
        });
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        float agility,
        params DRPGSkillSO[] skills)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "maxHp", 200f);
        SetField(profile, "skills", new List<DRPGSkillSO>(skills));
        return profile;
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

    private static void AssertTraceContains(
        DRPGBattleReactionRun run,
        DRPGBattleReactionTraceState expected)
    {
        for (int i = 0; i < run.Trace.Count; i++)
        {
            if (run.Trace[i].State == expected)
                return;
        }
        throw new Exception("Missing reaction trace state: " + expected);
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
