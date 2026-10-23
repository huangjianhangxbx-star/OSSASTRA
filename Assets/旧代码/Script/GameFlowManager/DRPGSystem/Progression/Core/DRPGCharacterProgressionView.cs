using System;
using System.Collections.Generic;

public sealed class DRPGCharacterProgressionView
{
    private readonly HashSet<string> unlockedSet;

    public DRPGCharacterProgressionView(
        DRPGCombatantProfileSO profile,
        DRPGProfessionSO activeProfession,
        int level,
        long experience,
        int experienceToNext,
        bool allowsLevelBreakthrough,
        bool breakthroughUnlocked,
        int availableAttributePoints,
        int availableSkillPoints,
        DRPGCoreAttributes attributes,
        DRPGDerivedStats derivedStats,
        IReadOnlyList<string> unlockedNodeIds,
        IReadOnlyList<DRPGSkillSO> unlockedSkills,
        IReadOnlyList<DRPGPassiveModifierSO> passives,
        IReadOnlyList<DRPGSkillSO> equippedSkills)
    {
        Profile = profile;
        ActiveProfession = activeProfession;
        Level = level;
        Experience = experience;
        ExperienceToNext = experienceToNext;
        AllowsLevelBreakthrough = allowsLevelBreakthrough;
        BreakthroughUnlocked = breakthroughUnlocked;
        AvailableAttributePoints = availableAttributePoints;
        AvailableSkillPoints = availableSkillPoints;
        Attributes = attributes;
        DerivedStats = derivedStats;
        UnlockedNodeIds = unlockedNodeIds ?? Array.Empty<string>();
        UnlockedSkills = unlockedSkills ?? Array.Empty<DRPGSkillSO>();
        PassiveModifiers = passives ?? Array.Empty<DRPGPassiveModifierSO>();
        EquippedSkills = equippedSkills ?? Array.Empty<DRPGSkillSO>();
        unlockedSet = new HashSet<string>(UnlockedNodeIds, StringComparer.Ordinal);
    }

    public DRPGCombatantProfileSO Profile { get; }
    public DRPGProfessionSO ActiveProfession { get; }
    public int Level { get; }
    public long Experience { get; }
    public int ExperienceToNext { get; }
    public bool AllowsLevelBreakthrough { get; }
    public bool BreakthroughUnlocked { get; }
    public int AvailableAttributePoints { get; }
    public int AvailableSkillPoints { get; }
    public int AvailablePoints => AvailableSkillPoints;
    public DRPGCoreAttributes Attributes { get; }
    public DRPGDerivedStats DerivedStats { get; }
    public IReadOnlyList<string> UnlockedNodeIds { get; }
    public IReadOnlyList<DRPGSkillSO> UnlockedSkills { get; }
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers { get; }
    public IReadOnlyList<DRPGSkillSO> EquippedSkills { get; }

    public bool IsNodeUnlocked(string nodeId)
    {
        return unlockedSet.Contains(nodeId);
    }
}
