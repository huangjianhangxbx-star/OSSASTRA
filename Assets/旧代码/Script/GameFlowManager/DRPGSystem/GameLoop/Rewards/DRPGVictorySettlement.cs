using System;
using System.Collections.Generic;

public sealed class DRPGExperienceAward
{
    public DRPGExperienceAward(
        DRPGCombatantProfileSO profile,
        long experience,
        int levelBefore,
        int levelAfter,
        int attributePointsGained,
        int skillPointsGained)
    {
        Profile = profile;
        Experience = Math.Max(0L, experience);
        LevelBefore = Math.Max(1, levelBefore);
        LevelAfter = Math.Max(LevelBefore, levelAfter);
        AttributePointsGained = Math.Max(0, attributePointsGained);
        SkillPointsGained = Math.Max(0, skillPointsGained);
    }

    public DRPGCombatantProfileSO Profile { get; }
    public long Experience { get; }
    public int LevelBefore { get; }
    public int LevelAfter { get; }
    public int AttributePointsGained { get; }
    public int SkillPointsGained { get; }
}

public sealed class DRPGVictorySettlement
{
    public DRPGVictorySettlement(
        IReadOnlyList<DRPGExperienceAward> experienceAwards,
        DRPGLootContainerState loot)
    {
        ExperienceAwards = experienceAwards ??
            Array.Empty<DRPGExperienceAward>();
        Loot = loot ?? new DRPGLootContainerState();
    }

    public IReadOnlyList<DRPGExperienceAward> ExperienceAwards { get; }
    public DRPGLootContainerState Loot { get; }
}
