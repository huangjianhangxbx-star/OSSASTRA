using System;
using System.Collections.Generic;

[Serializable]
public sealed class DRPGProgressionSaveData
{
    public int version = 4;
    public List<DRPGCharacterProgressionSaveData> characters =
        new List<DRPGCharacterProgressionSaveData>();
    public DRPGResonanceSaveData resonance;
}

[Serializable]
public sealed class DRPGCharacterProgressionSaveData
{
    public string characterId;
    public int level;
    public long experience;
    public bool breakthroughUnlocked;
    public int availableAttributePoints;
    public int totalSkillPoints;
    public DRPGCoreAttributeSaveData attributes = new DRPGCoreAttributeSaveData();
    public string activeProfessionId;
    public List<DRPGProfessionProgressionSaveData> professions =
        new List<DRPGProfessionProgressionSaveData>();
}

[Serializable]
public sealed class DRPGCoreAttributeSaveData
{
    public int vitality;
    public int strength;
    public int endurance;
    public int technique;
    public int circuit;
    public int reaction;

    public static DRPGCoreAttributeSaveData From(DRPGCoreAttributes value)
    {
        return new DRPGCoreAttributeSaveData
        {
            vitality = value.Vitality,
            strength = value.Strength,
            endurance = value.Endurance,
            technique = value.Technique,
            circuit = value.Circuit,
            reaction = value.Reaction
        };
    }

    public DRPGCoreAttributes ToValue()
    {
        return new DRPGCoreAttributes(
            vitality,
            strength,
            endurance,
            technique,
            circuit,
            reaction).Sanitized();
    }
}

[Serializable]
public sealed class DRPGProfessionProgressionSaveData
{
    public string professionId;
    public bool isUnlocked;
    public List<string> purchasedNodeIds = new List<string>();
    public List<string> equippedSkillIds = new List<string>();
}
