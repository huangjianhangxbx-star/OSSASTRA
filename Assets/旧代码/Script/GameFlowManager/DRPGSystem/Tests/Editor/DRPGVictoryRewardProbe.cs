#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGVictoryRewardProbe
{
    private sealed class MinimumRandom : IDRPGRandomSource
    {
        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return minimumInclusive;
        }

        public int RangeInclusive(int minimumInclusive, int maximumInclusive)
        {
            return minimumInclusive;
        }
    }

    private sealed class RecordingSink : IDRPGExperienceRewardSink
    {
        public readonly List<DRPGCombatantProfileSO> Profiles =
            new List<DRPGCombatantProfileSO>();

        public DRPGExperienceAward Grant(
            DRPGCombatantProfileSO profile,
            long experience)
        {
            Profiles.Add(profile);
            return new DRPGExperienceAward(profile, experience, 1, 1, 0, 0);
        }
    }

    public static string Run()
    {
        DRPGCombatantProfileSO player =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO enemy =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGLootItemDefinitionSO loot =
            ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();
        DRPGEncounterDefinitionSO encounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();
        try
        {
            SetField(loot, "itemId", "reward_probe_loot");
            SetField(encounter, "experienceRewardPerCharacter", 125L);
            SetField(encounter, "lootDrops", new List<DRPGLootDropDefinition>
            {
                new DRPGLootDropDefinition(loot, 1f, 2, 4)
            });

            var participants = new[]
            {
                CreateRuntime(1, player, DRPGBattleSide.Player),
                CreateRuntime(2, player, DRPGBattleSide.Player),
                CreateRuntime(3, enemy, DRPGBattleSide.Enemy)
            };
            var sink = new RecordingSink();
            DRPGVictorySettlement settlement =
                DRPGVictoryRewardResolver.ResolveAndCommit(
                    encounter,
                    participants,
                    sink,
                    new MinimumRandom());

            AssertEqual(1, sink.Profiles.Count,
                "A profile must receive encounter XP exactly once");
            AssertEqual(1, settlement.ExperienceAwards.Count,
                "Settlement must report one player-character XP award");
            AssertEqual(125L, settlement.ExperienceAwards[0].Experience,
                "Settlement must preserve configured XP");
            AssertEqual(2, settlement.Loot.GetQuantity(loot.ItemId),
                "Deterministic minimum roll must populate the loot container");

            return "PASS DRPG victory reward resolution";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(encounter);
            UnityEngine.Object.DestroyImmediate(loot);
            UnityEngine.Object.DestroyImmediate(enemy);
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    private static BattleParticipantRuntime CreateRuntime(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(
                profile,
                side,
                side == DRPGBattleSide.Player
                    ? DRPGBattleRosterRole.PartyMember
                    : DRPGBattleRosterRole.Enemy,
                id));
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
