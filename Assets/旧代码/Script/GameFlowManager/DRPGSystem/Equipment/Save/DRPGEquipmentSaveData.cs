using System;
using System.Collections.Generic;

[Serializable]
public sealed class DRPGEquipmentSaveData
{
    public int version = 2;
    public List<DRPGWeaponInstanceSaveData> weaponInstances =
        new List<DRPGWeaponInstanceSaveData>();
    public List<string> backpackWeaponInstanceIds = new List<string>();
    public List<string> settlementRecoveryWeaponInstanceIds =
        new List<string>();
    public List<DRPGAccessoryItemSaveData> backpackAccessories =
        new List<DRPGAccessoryItemSaveData>();
    public List<DRPGAccessoryItemSaveData> settlementRecoveryAccessories =
        new List<DRPGAccessoryItemSaveData>();
    public List<DRPGCharacterEquipmentSaveData> characters =
        new List<DRPGCharacterEquipmentSaveData>();
    public List<DRPGRecoveryBagSaveData> recoveryBags =
        new List<DRPGRecoveryBagSaveData>();
    public List<DRPGProcessedDeathSaveData> processedDeaths =
        new List<DRPGProcessedDeathSaveData>();
    public List<DRPGWeaponFragmentSaveData> weaponFragments =
        new List<DRPGWeaponFragmentSaveData>();
    public List<DRPGInventorySlotSaveData> backpackSlots =
        new List<DRPGInventorySlotSaveData>();
}

[Serializable]
public sealed class DRPGInventorySlotSaveData
{
    public int slotIndex;
    public DRPGInventoryItemKind itemKind;
    public string instanceId;
    public string definitionId;
    public int quantity = 1;
}

[Serializable]
public sealed class DRPGWeaponInstanceSaveData
{
    public string instanceId;
    public string definitionId;
    public int rolledMaxDurability;
    public int currentMaxDurability;
    public int currentDurability;
    public List<string> affixIds = new List<string>();
    public List<string> cardIds = new List<string>();
    public List<string> attachmentIds = new List<string>();
}

[Serializable]
public sealed class DRPGAccessoryItemSaveData
{
    public string instanceId;
    public string definitionId;
}

[Serializable]
public sealed class DRPGCharacterEquipmentSaveData
{
    public string characterId;
    public string equippedWeaponInstanceId;
    public string outfitDefinitionId;
    public List<string> accessoryDefinitionIds = new List<string>();
    public List<DRPGProfessionWeaponMemorySaveData> lastWeaponByProfession =
        new List<DRPGProfessionWeaponMemorySaveData>();
    public DRPGTraitPoolSnapshot traitPool = new DRPGTraitPoolSnapshot();
}

[Serializable]
public sealed class DRPGProfessionWeaponMemorySaveData
{
    public string professionId;
    public string weaponInstanceId;
}

[Serializable]
public sealed class DRPGRecoveryBagSaveData
{
    public string bagId;
    public string deathEventId;
    public string characterId;
    public string weaponInstanceId;
    public List<DRPGAccessoryItemSaveData> accessories =
        new List<DRPGAccessoryItemSaveData>();
}

[Serializable]
public sealed class DRPGProcessedDeathSaveData
{
    public string deathEventId;
    public string bagId;
}

[Serializable]
public sealed class DRPGWeaponFragmentSaveData
{
    public string weaponDefinitionId;
    public int count;
}

public sealed class DRPGQuarantinedWeaponEntry
{
    internal DRPGQuarantinedWeaponEntry(
        DRPGWeaponInstanceSaveData saveData,
        string ownershipKind,
        string ownerId)
    {
        SaveData = saveData;
        OwnershipKind = ownershipKind;
        OwnerId = ownerId;
    }

    public DRPGWeaponInstanceSaveData SaveData { get; }
    public string OwnershipKind { get; }
    public string OwnerId { get; }
}
