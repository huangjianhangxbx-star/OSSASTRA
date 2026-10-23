#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleEventBatchProbe
{
    public static string Run()
    {
        RunDefensiveCopyContract();
        RunSessionIdentityAndOrderingContract();
        return "PASS immutable battle event batch";
    }

    private static void RunDefensiveCopyContract()
    {
        var source = new List<DRPGBattleEvent>
        {
            new DRPGBattleEvent(
                1L,
                10L,
                0,
                DRPGBattleEventKind.ActionImpactResolved,
                1,
                2,
                "probe")
        };
        var batch = new DRPGBattleEventBatch(10L, 0, source);
        source.Clear();

        AssertEqual(1, batch.Events.Count,
            "Batch must defensively copy its source events");
        AssertEqual(10L, batch.RootActionId,
            "Batch must retain the root action ID");
        AssertEqual(0, batch.Depth,
            "Root batch depth must remain zero");
    }

    private static void RunSessionIdentityAndOrderingContract()
    {
        DRPGSkillSO attack = CreateDamageSkill("probe_batch_attack", 7f);
        DRPGSkillSO shield = CreateShieldSkill("probe_batch_shield", 2f);
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            30f,
            attack,
            shield);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            20f,
            attack,
            shield);

        try
        {
            BattleParticipantRuntime player = CreateParticipant(
                9801,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(
                9802,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var reactionPipeline = new DRPGBattleReactionPipeline(
                new IDRPGBattleReactionSource[]
                {
                    new ShieldOnDamageSource(shield)
                });
            var eventFactory = new DRPGBattleEventFactory();
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { player, enemy },
                reactionPipeline: reactionPipeline,
                eventFactory: eventFactory);
            var published = new List<DRPGBattleEvent>();
            session.BattleEventPublished += published.Add;
            session.Start();

            AssertTrue(session.TryPrepareCommand(
                DRPGBattleCommand.BasicAttack(
                    player.RuntimeId,
                    enemy.RuntimeId,
                    attack),
                out DRPGBattlePreparedAction prepared,
                out string error), error);
            AssertTrue(session.TryResolveImpact(prepared, out _, out error), error);

            DRPGBattleReactionRun run = session.LastReactionRun;
            AssertTrue(run != null, "Reaction run must be retained");
            AssertTrue(run.RootBatch != null,
                "Reaction run must retain the exact root event batch");
            AssertEqual(2, run.Batches.Count,
                "Root action plus one reaction must produce two batches");
            AssertEqual(0, run.Batches[0].Depth,
                "First batch must be the root batch");
            AssertEqual(1, run.Batches[1].Depth,
                "Reaction batch depth must increment");
            AssertEqual(run.RootActionId, run.Batches[1].RootActionId,
                "Reaction batch must preserve the root action ID");
            AssertEqual(run.Events.Count, published.Count,
                "Public publisher and reaction consumer must observe one fact list");

            for (int i = 0; i < published.Count; i++)
            {
                AssertTrue(ReferenceEquals(run.Events[i], published[i]),
                    "Published event must be the same immutable event instance at index " + i);
                if (i > 0)
                {
                    AssertTrue(
                        published[i - 1].EventId < published[i].EventId,
                        "Event IDs must be strictly increasing");
                }
            }

            AssertTrue(ReferenceEquals(run.RootBatch, run.Batches[0]),
                "RootBatch must be the first batch object, not a reconstruction");
            AssertTrue(ReferenceEquals(
                    run.RootBatch.Events[0],
                    published[0]),
                "Root consumer and public publisher must share event identity");
        }
        finally
        {
            Destroy(enemyProfile, playerProfile, shield, attack);
        }
    }

    private sealed class ShieldOnDamageSource : IDRPGBattleReactionSource
    {
        private readonly DRPGSkillSO shield;

        public ShieldOnDamageSource(DRPGSkillSO shield)
        {
            this.shield = shield;
        }

        public string SourceId => "probe.batch.shield";

        public void Collect(
            DRPGBattleReactionContext context,
            ICollection<DRPGBattleReactionRequest> requests)
        {
            if (context.Event.Kind != DRPGBattleEventKind.DamageApplied)
                return;

            requests.Add(context.CreateSkillRequest(
                SourceId,
                "reaction.batch.shield",
                SourceId + ":" + context.Event.RootActionId,
                0,
                context.Event.TargetRuntimeId,
                context.Event.TargetRuntimeId,
                shield,
                DRPGBattleImpactContext.ForStrike(
                    DRPGStrikeGrade.Body,
                    1f)));
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
