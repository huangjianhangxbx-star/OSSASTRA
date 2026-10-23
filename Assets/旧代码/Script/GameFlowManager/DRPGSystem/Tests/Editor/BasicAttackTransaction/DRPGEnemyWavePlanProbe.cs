#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class DRPGEnemyWavePlanProbe
{
    // Break caught: profile deduplication, shuffled authored order, or an AI/hit
    // random call altering the frozen deployment batches.
    internal static void AuthoredMembersArePartitionedOnceWithoutProfileDeduplication()
    {
        var profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            var members = Enumerable.Range(0, 13).Select(i => new BattleParticipantSpec(profile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, i)).ToArray();
            var plan = Freeze(42, "encounter-a", members, 3, 5, 6);
            var batches = plan.Waves;
            Check(batches.Select(x => x.Count).SequenceEqual(new[]{5,5,3}), "Seed 42 should partition thirteen members as 5/5/3.");
            int ordinal = 0, wave = 0;
            foreach (var batch in batches)
            {
                wave++;
                var entries = batch.ToArray();
                Check(entries.Length >= 1 && entries.Length <= 5, "Wave size escaped policy.");
                if (ordinal + entries.Length < 13) Check(entries.Length >= 3, "Non-final wave is below minimum.");
                foreach (var entry in entries)
                {
                    Check(entry.Id.AuthoredOrdinal == ordinal,
                        "Authored order was shuffled or a repeated profile was removed.");
                    Check(ReferenceEquals(entry.Spec, members[ordinal]),
                        "Plan replaced the frozen participant specification.");
                    ordinal++;
                }
            }
            Check(ordinal == 13 && wave >= 3, "Some authored deployments were lost.");
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); }
    }

    internal static void SeedStreamMatchesIndependentGoldenVectors()
    {
        foreach (var sample in new[] {
            new { Id="encounter-a", Seed=0, Hash=0xA6808828E65781EAUL, Values=new[]{0x59EB80966A4A454CUL,0x7DFB83C63AF846CDUL,0x45C922FAEB71629FUL}},
            new { Id="encounter-a", Seed=42, Hash=0x660B50E04EF5B660UL, Values=new[]{0x2A6D40E5AC7B1D5FUL,0x1C4B860113386151UL,0x74B61D0A877FD791UL}},
            new { Id="ankalute-debug", Seed=-1, Hash=0x234F16CD5DD69DD2UL, Values=new[]{0xB6D16C6A4BEBF8C2UL,0xA9B419C7EDC421B6UL,0x0E8CABD8971EE5D7UL}} })
        {
            var rng = new DRPGEnemyWaveRandom(sample.Id, sample.Seed);
            Check(rng.SeedHash == sample.Hash, "Wave seed encoding changed.");
            foreach (ulong expected in sample.Values)
                Check(rng.NextUInt64() == expected, "Wave random substream changed.");
        }
    }

    private static DRPGEnemyWavePlan Freeze(int seed, string id, IReadOnlyList<BattleParticipantSpec> members, int min, int max, int capacity)
        => DRPGEnemyWavePlan.Freeze(seed,id,members,min,max,capacity);

    internal static void ExistingEncounterPolicyFreezesWithoutDirtyingAssets()
    {
        var guids = UnityEditor.AssetDatabase.FindAssets("t:DRPGEncounterDefinitionSO");
        Check(guids.Length > 0, "No real encounter assets were examined.");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>(path);
            string json = UnityEditor.EditorJsonUtility.ToJson(asset);
            var hash = UnityEditor.AssetDatabase.GetAssetDependencyHash(path);
            bool dirty = UnityEditor.EditorUtility.IsDirty(asset);
            var result = asset.FreezeWavePolicy(.75d,.50d);
            int minimum = result.MinWaveSize;
            int maximum = result.MaxWaveSize;
            Check(minimum == 1 && maximum == Math.Min(asset.FormationRules.ActiveEnemyLimit, asset.CreateResolvedEnemyDeployments().Count),
                "Legacy 0/0 policy did not use the bounded authored roster: " + path);
            Check(json == UnityEditor.EditorJsonUtility.ToJson(asset) && hash == UnityEditor.AssetDatabase.GetAssetDependencyHash(path) &&
                dirty == UnityEditor.EditorUtility.IsDirty(asset), "Read-only wave migration changed asset: " + path);
        }
    }

    internal static void ExplicitInvalidPoliciesNeverFallBackToLegacyDefaults()
    {
        foreach (var policy in new[]{new DRPGEnemyWavePolicy(0,0,.75,.5), new DRPGEnemyWavePolicy(-1,3,.75,.5),
            new DRPGEnemyWavePolicy(4,3,.75,.5), new DRPGEnemyWavePolicy(1,7,.75,.5),
            new DRPGEnemyWavePolicy(1,3,0,.5), new DRPGEnemyWavePolicy(1,3,double.NaN,.5),
            new DRPGEnemyWavePolicy(1,3,.75,.51), new DRPGEnemyWavePolicy(1,3,.75,-.1)})
            Reject(() => policy.Freeze(6,10,.75,.5));
        var frozen = new DRPGEnemyWavePolicy(2,4,1.2,0).Freeze(6,10,.75,.5);
        Check(frozen.MinWaveSize==2 && frozen.MaxWaveSize==4 && frozen.AnnouncementDuration==1.2 && frozen.EntryStaggerMaximum==0,
            "Explicit valid authoring was replaced by legacy defaults.");
    }

    internal static void BattleStartCarriesAnExplicitReplaySeed()
    {
        var constructor = typeof(DRPGBattleStartRequest).GetConstructor(new[]{typeof(DRPGPartySetupSO),
            typeof(DRPGEncounterDefinitionSO),typeof(DRPGBattleEnvironmentContext),typeof(int),
            typeof(BasicAttackOnlyAcceptanceOptions)});
        Check(constructor != null, "Battle start request has no explicit battle seed.");
        var request = constructor.Invoke(new object[]{null,null,default(DRPGBattleEnvironmentContext),-1,
            default(BasicAttackOnlyAcceptanceOptions)});
        Check((int)typeof(DRPGBattleStartRequest).GetProperty("BattleSeed").GetValue(request)==-1,
            "Replay seed was replaced during start request creation.");
    }

    internal static void FrozenPlanIgnoresCallerEditsAndCanonicalizesEncounterIdentity()
    {
        var profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            var members = Enumerable.Range(0,7).Select(i => new BattleParticipantSpec(profile,DRPGBattleSide.Enemy,DRPGBattleRosterRole.Enemy,i)).ToArray();
            var frozen = Freeze(-1,"caf\u0065\u0301",members,3,3,6);
            var other = Freeze(-1,"caf\u00e9",members,3,3,6);
            members[0] = null;
            Check(frozen.Waves.Select(w=>w.Count).SequenceEqual(new[]{3,3,1}) && frozen.Waves[0][0].Spec!=null,
                "Caller edit changed the frozen plan or legal last remainder was dropped.");
            Check(frozen.Waves[0][0].Id.Equals(other.Waves[0][0].Id) &&
                new DRPGEnemyWaveRandom("caf\u0065\u0301",-1).SeedHash == new DRPGEnemyWaveRandom("caf\u00e9",-1).SeedHash,
                "Unicode-equivalent encounter identities diverged.");
            Reject(() => Freeze(0,"id",members,1,6,6));
            Reject(() => Freeze(0,"id",Array.Empty<BattleParticipantSpec>(),1,6,6));
            foreach (var range in new[]{new[]{0,1,6},new[]{1,0,6},new[]{3,2,6},new[]{1,7,6},new[]{1,1,0}})
                Reject(() => Freeze(0,"id",new[]{other.Waves[0][0].Spec},range[0],range[1],range[2]));
            Check(((IList<IReadOnlyList<DRPGEnemyWaveMember>>)frozen.Waves).IsReadOnly &&
                ((IList<DRPGEnemyWaveMember>)frozen.Waves[0]).IsReadOnly, "Frozen wave collections expose mutation.");
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); }
    }

    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Invalid wave authoring was accepted."); }
    private static void Check(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
}
#endif
