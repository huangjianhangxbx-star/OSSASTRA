using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "DRPGCharacterProgression",
    menuName = "ANKALUTE/DRPG/Progression/Character Progression")]
public sealed class DRPGCharacterProgressionDefinitionSO : ScriptableObject
{
    [SerializeField] private string progressionId = "character_progression";
    [SerializeField] private DRPGProfessionSO defaultProfession;
    [SerializeField] private List<DRPGProfessionSO> availableProfessions =
        new List<DRPGProfessionSO>();
    [SerializeField] private List<DRPGProfessionSO> startingUnlockedProfessions =
        new List<DRPGProfessionSO>();
    [SerializeField] private DRPGPersonnelKind personnelKind =
        DRPGPersonnelKind.Friend;
    [Header("Level and attributes")]
    [Range(1, DRPGGrowthRules.NormalLevelCap)]
    [SerializeField] private int startingLevel = 1;
    [SerializeField] private bool allowsLevelBreakthrough;
    [SerializeField] private bool breakthroughUnlockedByDefault;
    [SerializeField] private DRPGCoreAttributes initialAttributes =
        new DRPGCoreAttributes(10, 10, 10, 10, 10, 10);
    [Min(0)]
    [SerializeField] private int startingAttributePoints;
    [Min(0)]
    [FormerlySerializedAs("startingSkillPointsPerProfession")]
    [SerializeField] private int startingSkillPoints;

    public string ProgressionId => progressionId;
    public DRPGProfessionSO DefaultProfession => defaultProfession;
    public IReadOnlyList<DRPGProfessionSO> AvailableProfessions =>
        availableProfessions ?? (IReadOnlyList<DRPGProfessionSO>)
            Array.Empty<DRPGProfessionSO>();
    public IReadOnlyList<DRPGProfessionSO> StartingUnlockedProfessions =>
        startingUnlockedProfessions ?? (IReadOnlyList<DRPGProfessionSO>)
            Array.Empty<DRPGProfessionSO>();
    public DRPGPersonnelKind PersonnelKind => personnelKind;
    public int StartingLevel => Mathf.Clamp(
        startingLevel,
        1,
        DRPGGrowthRules.NormalLevelCap);
    public bool AllowsLevelBreakthrough => allowsLevelBreakthrough;
    public bool BreakthroughUnlockedByDefault =>
        allowsLevelBreakthrough && breakthroughUnlockedByDefault;
    public DRPGCoreAttributes InitialAttributes =>
        initialAttributes.Total > 0
            ? initialAttributes.Sanitized()
            : DRPGCoreAttributes.Default;
    public int StartingAttributePoints => Mathf.Max(0, startingAttributePoints);
    public int StartingSkillPoints => Mathf.Max(0, startingSkillPoints);
    [Obsolete("Skill points are character-owned. Use StartingSkillPoints.")]
    public int StartingSkillPointsPerProfession => StartingSkillPoints;

    public bool ContainsProfession(DRPGProfessionSO profession)
    {
        if (profession == null)
            return false;

        IReadOnlyList<DRPGProfessionSO> source = AvailableProfessions;
        for (int i = 0; i < source.Count; i++)
        {
            if (ReferenceEquals(source[i], profession))
                return true;
        }

        return false;
    }

    public DRPGProfessionSO FindProfession(string professionId)
    {
        if (string.IsNullOrWhiteSpace(professionId))
            return null;
        IReadOnlyList<DRPGProfessionSO> source = AvailableProfessions;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGProfessionSO profession = source[i];
            if (profession != null &&
                string.Equals(
                    profession.ProfessionId,
                    professionId,
                    StringComparison.Ordinal))
            {
                return profession;
            }
        }
        return null;
    }

    public bool IsStartingProfessionUnlocked(DRPGProfessionSO profession)
    {
        if (profession == null)
            return false;
        if (ReferenceEquals(defaultProfession, profession) ||
            defaultProfession != null &&
            string.Equals(
                defaultProfession.ProfessionId,
                profession.ProfessionId,
                StringComparison.Ordinal))
        {
            return true;
        }

        IReadOnlyList<DRPGProfessionSO> source = StartingUnlockedProfessions;
        for (int i = 0; i < source.Count; i++)
        {
            if (ReferenceEquals(source[i], profession) ||
                source[i] != null &&
                string.Equals(
                    source[i].ProfessionId,
                    profession.ProfessionId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
