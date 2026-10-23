using System;
using System.Collections.Generic;

public sealed class DRPGCharacterProgressionState
{
    private readonly Dictionary<string, DRPGProfessionProgressState> professions =
        new Dictionary<string, DRPGProfessionProgressState>(StringComparer.Ordinal);

    public DRPGCharacterProgressionState(
        string characterId,
        DRPGCharacterProgressionDefinitionSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        CharacterId = characterId;
        Level = definition.StartingLevel;
        InitialAttributes = definition.InitialAttributes;
        Attributes = InitialAttributes;
        AvailableAttributePoints = definition.StartingAttributePoints;
        TotalSkillPoints = definition.StartingSkillPoints;
        AllowsLevelBreakthrough = definition.AllowsLevelBreakthrough;
        BreakthroughUnlocked = definition.BreakthroughUnlockedByDefault;
    }

    public string CharacterId { get; }
    public string ActiveProfessionId { get; private set; }
    public int Level { get; private set; }
    public long CurrentExperience { get; private set; }
    public bool AllowsLevelBreakthrough { get; }
    public bool BreakthroughUnlocked { get; private set; }
    public DRPGCoreAttributes InitialAttributes { get; }
    public DRPGCoreAttributes Attributes { get; private set; }
    public int AvailableAttributePoints { get; private set; }
    public int TotalSkillPoints { get; private set; }
    public int SpentSkillPoints
    {
        get
        {
            int result = 0;
            foreach (DRPGProfessionProgressState state in professions.Values)
                result += state.SpentPoints;
            return result;
        }
    }
    public int AvailableSkillPoints => Math.Max(0, TotalSkillPoints - SpentSkillPoints);
    public int Revision { get; private set; }
    public IEnumerable<DRPGProfessionProgressState> Professions => professions.Values;

    public void Add(DRPGProfessionProgressState state)
    {
        professions.Add(state.Profession.ProfessionId, state);
        if (string.IsNullOrWhiteSpace(ActiveProfessionId))
            ActiveProfessionId = state.Profession.ProfessionId;
    }

    public bool TryGet(string professionId, out DRPGProfessionProgressState state)
    {
        return professions.TryGetValue(professionId, out state);
    }

    public bool TryGetActive(out DRPGProfessionProgressState state)
    {
        return professions.TryGetValue(ActiveProfessionId, out state);
    }

    public bool IsProfessionUnlocked(string professionId)
    {
        return professions.TryGetValue(
                professionId,
                out DRPGProfessionProgressState profession) &&
            profession.IsProfessionUnlocked;
    }

    public bool UnlockProfession(string professionId)
    {
        if (!professions.TryGetValue(
                professionId,
                out DRPGProfessionProgressState profession) ||
            !profession.UnlockProfession())
        {
            return false;
        }
        Revision++;
        return true;
    }

    public bool SetActive(string professionId)
    {
        if (!professions.TryGetValue(
                professionId,
                out DRPGProfessionProgressState profession) ||
            !profession.IsProfessionUnlocked)
            return false;
        ActiveProfessionId = professionId;
        Revision++;
        return true;
    }

    public DRPGLevelGainResult GainExperience(long amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        int levelCap = AllowsLevelBreakthrough && BreakthroughUnlocked
            ? DRPGGrowthRules.BreakthroughLevelCap
            : DRPGGrowthRules.NormalLevelCap;
        CurrentExperience += amount;
        int levels = 0;
        int attributePoints = 0;
        int skillPoints = 0;
        while (Level < levelCap)
        {
            int required = DRPGGrowthRules.ExperienceToNextLevel(Level);
            if (required <= 0 || CurrentExperience < required)
                break;
            CurrentExperience -= required;
            Level++;
            levels++;
            if (Level <= DRPGGrowthRules.NormalLevelCap)
            {
                AvailableAttributePoints++;
                TotalSkillPoints++;
                attributePoints++;
                skillPoints++;
            }
        }

        // Experience remains character-owned even at the current cap. It can
        // later feed a breakthrough or an explicit point-to-currency exchange.
        if (levels > 0 || amount > 0)
            Revision++;
        return new DRPGLevelGainResult(
            levels,
            attributePoints,
            skillPoints,
            CurrentExperience);
    }

    public DRPGAttributeAllocationDraft BeginAttributeDraft()
    {
        return new DRPGAttributeAllocationDraft(
            CharacterId,
            Revision,
            Attributes,
            AvailableAttributePoints);
    }

    public bool TryCommit(DRPGAttributeAllocationDraft draft)
    {
        if (draft == null || draft.CharacterId != CharacterId ||
            draft.SourceRevision != Revision || !draft.Original.Equals(Attributes) ||
            draft.SpentPoints > AvailableAttributePoints)
        {
            return false;
        }
        Attributes = draft.Candidate;
        AvailableAttributePoints -= draft.SpentPoints;
        Revision++;
        return true;
    }

    public int ResetAllocatedAttributes()
    {
        int refunded = Math.Max(0, Attributes.Total - InitialAttributes.Total);
        if (refunded == 0)
            return 0;
        Attributes = InitialAttributes;
        AvailableAttributePoints += refunded;
        Revision++;
        return refunded;
    }

    public bool UnlockBreakthrough()
    {
        if (!AllowsLevelBreakthrough || BreakthroughUnlocked)
            return false;
        BreakthroughUnlocked = true;
        Revision++;
        // Consume already banked experience immediately once the cap opens.
        GainExperience(0);
        return true;
    }

    public void RestoreGrowth(
        int level,
        long experience,
        bool breakthroughUnlocked,
        DRPGCoreAttributes attributes,
        int availableAttributePoints,
        int totalSkillPoints)
    {
        BreakthroughUnlocked = AllowsLevelBreakthrough && breakthroughUnlocked;
        int cap = AllowsLevelBreakthrough && BreakthroughUnlocked
            ? DRPGGrowthRules.BreakthroughLevelCap
            : DRPGGrowthRules.NormalLevelCap;
        Level = Math.Max(1, Math.Min(cap, level));
        CurrentExperience = Math.Max(0L, experience);
        Attributes = attributes.Sanitized();
        AvailableAttributePoints = Math.Max(0, availableAttributePoints);
        TotalSkillPoints = Math.Max(SpentSkillPoints, totalSkillPoints);
        Revision++;
    }
}
