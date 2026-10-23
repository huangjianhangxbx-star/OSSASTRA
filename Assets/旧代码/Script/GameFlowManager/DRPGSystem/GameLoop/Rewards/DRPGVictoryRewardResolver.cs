using System;
using System.Collections.Generic;

public interface IDRPGExperienceRewardSink
{
    DRPGExperienceAward Grant(
        DRPGCombatantProfileSO profile,
        long experience);
}

public sealed class DRPGProgressionExperienceRewardSink :
    IDRPGExperienceRewardSink
{
    private readonly DRPGProgressionService progression;

    public DRPGProgressionExperienceRewardSink(
        DRPGProgressionService progression)
    {
        this.progression = progression;
    }

    public DRPGExperienceAward Grant(
        DRPGCombatantProfileSO profile,
        long experience)
    {
        long safeExperience = Math.Max(0L, experience);
        if (progression == null || profile == null ||
            profile.ProgressionDefinition == null)
        {
            return new DRPGExperienceAward(
                profile,
                safeExperience,
                1,
                1,
                0,
                0);
        }

        DRPGCharacterProgressionView before = progression.GetView(profile);
        DRPGLevelGainResult gain = progression.GainExperience(
            profile,
            safeExperience);
        DRPGCharacterProgressionView after = progression.GetView(profile);
        return new DRPGExperienceAward(
            profile,
            safeExperience,
            before.Level,
            after.Level,
            gain.AttributePointsGained,
            gain.SkillPointsGained);
    }
}

public static class DRPGVictoryRewardResolver
{
    public static DRPGVictorySettlement ResolveAndCommit(
        DRPGEncounterDefinitionSO encounter,
        IReadOnlyList<BattleParticipantRuntime> participants,
        IDRPGExperienceRewardSink experienceSink,
        IDRPGRandomSource randomSource)
    {
        var awards = new List<DRPGExperienceAward>();
        var awardedProfiles = new HashSet<DRPGCombatantProfileSO>();
        long experience = encounter != null
            ? encounter.ExperienceRewardPerCharacter
            : 0L;
        if (participants != null && experienceSink != null)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipantRuntime participant = participants[i];
                if (participant == null ||
                    participant.Side != DRPGBattleSide.Player ||
                    participant.Profile == null ||
                    !awardedProfiles.Add(participant.Profile))
                {
                    continue;
                }

                awards.Add(experienceSink.Grant(
                    participant.Profile,
                    experience));
            }
        }

        var loot = new DRPGLootContainerState();
        IDRPGRandomSource random = randomSource ?? new DRPGUnityRandomSource();
        IReadOnlyList<DRPGLootDropDefinition> drops = encounter != null
            ? encounter.LootDrops
            : Array.Empty<DRPGLootDropDefinition>();
        if (drops != null)
        {
            for (int i = 0; i < drops.Count; i++)
            {
                DRPGLootDropDefinition drop = drops[i];
                if (drop == null || drop.Item == null)
                    continue;
                float roll = random.Range(0f, 1f);
                if (roll > drop.DropChance)
                    continue;
                int quantity = random.RangeInclusive(
                    drop.MinimumQuantity,
                    drop.MaximumQuantity);
                loot.Add(drop.Item, quantity);
            }
        }

        return new DRPGVictorySettlement(awards.AsReadOnly(), loot);
    }
}
