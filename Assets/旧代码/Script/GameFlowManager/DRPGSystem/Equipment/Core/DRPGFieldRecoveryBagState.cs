using System;
using System.Collections.Generic;

public sealed class DRPGFieldRecoveryBagState
{
    private readonly List<DRPGRecoveredAccessoryItem> accessories;

    internal DRPGFieldRecoveryBagState(
        string bagId,
        string deathEventId,
        string characterId,
        DRPGWeaponInstance weapon,
        IReadOnlyList<DRPGRecoveredAccessoryItem> recoveredAccessories)
    {
        if (string.IsNullOrWhiteSpace(bagId))
            throw new ArgumentException("Bag ID is required.", nameof(bagId));
        if (string.IsNullOrWhiteSpace(deathEventId))
            throw new ArgumentException("Death event ID is required.", nameof(deathEventId));
        if (string.IsNullOrWhiteSpace(characterId))
            throw new ArgumentException("Character ID is required.", nameof(characterId));

        BagId = bagId;
        DeathEventId = deathEventId;
        CharacterId = characterId;
        Weapon = weapon;
        accessories = recoveredAccessories != null
            ? new List<DRPGRecoveredAccessoryItem>(recoveredAccessories)
            : new List<DRPGRecoveredAccessoryItem>();
    }

    public string BagId { get; }
    public string DeathEventId { get; }
    public string CharacterId { get; }
    public DRPGWeaponInstance Weapon { get; private set; }
    public IReadOnlyList<DRPGRecoveredAccessoryItem> Accessories =>
        accessories.AsReadOnly();
    public bool IsEmpty => Weapon == null && accessories.Count == 0;
    public bool IsResolved { get; private set; }

    internal void RemoveWeapon()
    {
        Weapon = null;
    }

    internal bool RemoveAccessory(string instanceId)
    {
        for (int i = 0; i < accessories.Count; i++)
        {
            if (!string.Equals(
                    accessories[i].InstanceId,
                    instanceId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            accessories.RemoveAt(i);
            return true;
        }
        return false;
    }

    internal DRPGRecoveredAccessoryItem FindAccessory(string instanceId)
    {
        for (int i = 0; i < accessories.Count; i++)
        {
            if (string.Equals(
                    accessories[i].InstanceId,
                    instanceId,
                    StringComparison.Ordinal))
            {
                return accessories[i];
            }
        }
        return null;
    }

    internal void MarkResolved()
    {
        IsResolved = true;
        Weapon = null;
        accessories.Clear();
    }
}
