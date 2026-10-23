using System;
using UnityEngine;

public enum DRPGCoreAttributeId
{
    Vitality = 0,
    Strength = 1,
    Endurance = 2,
    Technique = 3,
    Circuit = 4,
    Reaction = 5
}

[Serializable]
public struct DRPGCoreAttributes : IEquatable<DRPGCoreAttributes>
{
    [SerializeField] private int vitality;
    [SerializeField] private int strength;
    [SerializeField] private int endurance;
    [SerializeField] private int technique;
    [SerializeField] private int circuit;
    [SerializeField] private int reaction;

    public DRPGCoreAttributes(
        int vitality,
        int strength,
        int endurance,
        int technique,
        int circuit,
        int reaction)
    {
        this.vitality = vitality;
        this.strength = strength;
        this.endurance = endurance;
        this.technique = technique;
        this.circuit = circuit;
        this.reaction = reaction;
    }

    public static DRPGCoreAttributes Default =>
        new DRPGCoreAttributes(10, 10, 10, 10, 10, 10);

    public int Vitality => vitality;
    public int Strength => strength;
    public int Endurance => endurance;
    public int Technique => technique;
    public int Circuit => circuit;
    public int Reaction => reaction;
    public int Total => vitality + strength + endurance + technique + circuit + reaction;

    public int Get(DRPGCoreAttributeId attribute)
    {
        switch (attribute)
        {
            case DRPGCoreAttributeId.Vitality: return vitality;
            case DRPGCoreAttributeId.Strength: return strength;
            case DRPGCoreAttributeId.Endurance: return endurance;
            case DRPGCoreAttributeId.Technique: return technique;
            case DRPGCoreAttributeId.Circuit: return circuit;
            case DRPGCoreAttributeId.Reaction: return reaction;
            default: throw new ArgumentOutOfRangeException(nameof(attribute));
        }
    }

    public DRPGCoreAttributes WithDelta(DRPGCoreAttributeId attribute, int delta)
    {
        int next = Mathf.Clamp(Get(attribute) + delta, 1, DRPGGrowthRules.AttributeCap);
        switch (attribute)
        {
            case DRPGCoreAttributeId.Vitality:
                return new DRPGCoreAttributes(next, strength, endurance, technique, circuit, reaction);
            case DRPGCoreAttributeId.Strength:
                return new DRPGCoreAttributes(vitality, next, endurance, technique, circuit, reaction);
            case DRPGCoreAttributeId.Endurance:
                return new DRPGCoreAttributes(vitality, strength, next, technique, circuit, reaction);
            case DRPGCoreAttributeId.Technique:
                return new DRPGCoreAttributes(vitality, strength, endurance, next, circuit, reaction);
            case DRPGCoreAttributeId.Circuit:
                return new DRPGCoreAttributes(vitality, strength, endurance, technique, next, reaction);
            case DRPGCoreAttributeId.Reaction:
                return new DRPGCoreAttributes(vitality, strength, endurance, technique, circuit, next);
            default:
                throw new ArgumentOutOfRangeException(nameof(attribute));
        }
    }

    public DRPGCoreAttributes Sanitized()
    {
        return new DRPGCoreAttributes(
            Mathf.Clamp(vitality, 1, DRPGGrowthRules.AttributeCap),
            Mathf.Clamp(strength, 1, DRPGGrowthRules.AttributeCap),
            Mathf.Clamp(endurance, 1, DRPGGrowthRules.AttributeCap),
            Mathf.Clamp(technique, 1, DRPGGrowthRules.AttributeCap),
            Mathf.Clamp(circuit, 1, DRPGGrowthRules.AttributeCap),
            Mathf.Clamp(reaction, 1, DRPGGrowthRules.AttributeCap));
    }

    public bool Equals(DRPGCoreAttributes other)
    {
        return vitality == other.vitality &&
            strength == other.strength &&
            endurance == other.endurance &&
            technique == other.technique &&
            circuit == other.circuit &&
            reaction == other.reaction;
    }

    public override bool Equals(object obj)
    {
        return obj is DRPGCoreAttributes other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = vitality;
            hash = hash * 397 ^ strength;
            hash = hash * 397 ^ endurance;
            hash = hash * 397 ^ technique;
            hash = hash * 397 ^ circuit;
            return hash * 397 ^ reaction;
        }
    }
}

public sealed class DRPGLevelGainResult
{
    public DRPGLevelGainResult(
        int levelsGained,
        int attributePointsGained,
        int skillPointsGained,
        long unconsumedExperience)
    {
        LevelsGained = levelsGained;
        AttributePointsGained = attributePointsGained;
        SkillPointsGained = skillPointsGained;
        UnconsumedExperience = unconsumedExperience;
    }

    public int LevelsGained { get; }
    public int AttributePointsGained { get; }
    public int SkillPointsGained { get; }
    public long UnconsumedExperience { get; }
}

public sealed class DRPGAttributeAllocationDraft
{
    internal DRPGAttributeAllocationDraft(
        string characterId,
        int revision,
        DRPGCoreAttributes original,
        int availablePoints)
    {
        CharacterId = characterId;
        SourceRevision = revision;
        Original = original;
        Candidate = original;
        AvailablePoints = Mathf.Max(0, availablePoints);
    }

    public string CharacterId { get; }
    public int SourceRevision { get; }
    public DRPGCoreAttributes Original { get; }
    public DRPGCoreAttributes Candidate { get; private set; }
    public int AvailablePoints { get; }
    public int SpentPoints => Mathf.Max(0, Candidate.Total - Original.Total);
    public int RemainingPoints => Mathf.Max(0, AvailablePoints - SpentPoints);

    public bool TryAdd(DRPGCoreAttributeId attribute)
    {
        if (RemainingPoints <= 0 || Candidate.Get(attribute) >= DRPGGrowthRules.AttributeCap)
            return false;
        Candidate = Candidate.WithDelta(attribute, 1);
        return true;
    }

    public bool TryRemove(DRPGCoreAttributeId attribute)
    {
        if (Candidate.Get(attribute) <= Original.Get(attribute))
            return false;
        Candidate = Candidate.WithDelta(attribute, -1);
        return true;
    }

    public void Reset()
    {
        Candidate = Original;
    }
}
