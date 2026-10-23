using System;
using UnityEngine;

public enum DRPGInventoryItemKind
{
    Weapon = 0,
    Accessory = 1,
    Loot = 2
}

/// <summary>
/// Immutable content of one logical DRPG inventory slot.
/// Unique equipment always has quantity one; ordinary loot may stack.
/// </summary>
public sealed class DRPGInventoryItemStack
{
    private DRPGInventoryItemStack(
        DRPGInventoryItemKind kind,
        DRPGWeaponInstance weapon,
        DRPGRecoveredAccessoryItem accessory,
        DRPGLootItemDefinitionSO lootDefinition,
        int quantity)
    {
        Kind = kind;
        Weapon = weapon;
        Accessory = accessory;
        LootDefinition = lootDefinition;
        Quantity = quantity;
    }

    public DRPGInventoryItemKind Kind { get; }
    public DRPGWeaponInstance Weapon { get; }
    public DRPGRecoveredAccessoryItem Accessory { get; }
    public DRPGLootItemDefinitionSO LootDefinition { get; }
    public int Quantity { get; }
    public int MaxStack => Kind == DRPGInventoryItemKind.Loot &&
        LootDefinition != null
            ? LootDefinition.MaxStack
            : 1;
    public bool IsStackable => Kind == DRPGInventoryItemKind.Loot &&
        MaxStack > 1;
    public float UnitWeight
    {
        get
        {
            switch (Kind)
            {
                case DRPGInventoryItemKind.Weapon:
                    return Weapon != null && Weapon.Definition != null
                        ? Weapon.Definition.Weight
                        : 0f;
                case DRPGInventoryItemKind.Accessory:
                    return Accessory != null ? Accessory.Weight : 0f;
                default:
                    return LootDefinition != null ? LootDefinition.Weight : 0f;
            }
        }
    }
    public float TotalWeight => UnitWeight * Quantity;
    public string StableKey
    {
        get
        {
            switch (Kind)
            {
                case DRPGInventoryItemKind.Weapon:
                    return "weapon:" + (Weapon != null
                        ? Weapon.InstanceId
                        : string.Empty);
                case DRPGInventoryItemKind.Accessory:
                    return "accessory:" + (Accessory != null
                        ? Accessory.InstanceId
                        : string.Empty);
                default:
                    return "loot:" + (LootDefinition != null
                        ? LootDefinition.ItemId
                        : string.Empty);
            }
        }
    }
    public string DisplayName
    {
        get
        {
            switch (Kind)
            {
                case DRPGInventoryItemKind.Weapon:
                    return Weapon != null && Weapon.Definition != null
                        ? Weapon.Definition.DisplayName
                        : string.Empty;
                case DRPGInventoryItemKind.Accessory:
                    return Accessory != null && Accessory.Definition != null
                        ? Accessory.Definition.DisplayName
                        : string.Empty;
                default:
                    return LootDefinition != null
                        ? LootDefinition.DisplayName
                        : string.Empty;
            }
        }
    }
    public string Description
    {
        get
        {
            switch (Kind)
            {
                case DRPGInventoryItemKind.Weapon:
                    return Weapon != null && Weapon.Definition != null
                        ? Weapon.Definition.Description
                        : string.Empty;
                case DRPGInventoryItemKind.Loot:
                    return LootDefinition != null
                        ? LootDefinition.Description
                        : string.Empty;
                default:
                    return string.Empty;
            }
        }
    }
    public Sprite Icon
    {
        get
        {
            switch (Kind)
            {
                case DRPGInventoryItemKind.Weapon:
                    return Weapon != null && Weapon.Definition != null
                        ? Weapon.Definition.Icon
                        : null;
                case DRPGInventoryItemKind.Accessory:
                    return Accessory != null && Accessory.Definition != null
                        ? Accessory.Definition.Icon
                        : null;
                default:
                    return LootDefinition != null ? LootDefinition.Icon : null;
            }
        }
    }

    public static DRPGInventoryItemStack FromWeapon(DRPGWeaponInstance weapon)
    {
        if (weapon == null)
            throw new ArgumentNullException(nameof(weapon));
        return new DRPGInventoryItemStack(
            DRPGInventoryItemKind.Weapon,
            weapon,
            null,
            null,
            1);
    }

    public static DRPGInventoryItemStack FromAccessory(
        DRPGRecoveredAccessoryItem accessory)
    {
        if (accessory == null)
            throw new ArgumentNullException(nameof(accessory));
        return new DRPGInventoryItemStack(
            DRPGInventoryItemKind.Accessory,
            null,
            accessory,
            null,
            1);
    }

    public static DRPGInventoryItemStack FromLoot(
        DRPGLootItemDefinitionSO definition,
        int quantity)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (quantity < 1 || quantity > definition.MaxStack)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        return new DRPGInventoryItemStack(
            DRPGInventoryItemKind.Loot,
            null,
            null,
            definition,
            quantity);
    }

    public bool CanStackWith(DRPGInventoryItemStack other)
    {
        return other != null && Kind == DRPGInventoryItemKind.Loot &&
            other.Kind == DRPGInventoryItemKind.Loot &&
            LootDefinition != null && other.LootDefinition != null &&
            string.Equals(
                LootDefinition.ItemId,
                other.LootDefinition.ItemId,
                StringComparison.Ordinal) &&
            ReferenceEquals(LootDefinition, other.LootDefinition);
    }

    internal DRPGInventoryItemStack WithQuantity(int quantity)
    {
        if (Kind != DRPGInventoryItemKind.Loot)
        {
            if (quantity != 1)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            return this;
        }
        return FromLoot(LootDefinition, quantity);
    }
}

public readonly struct DRPGInventorySlotSnapshot
{
    public DRPGInventorySlotSnapshot(
        int slotIndex,
        DRPGInventoryItemStack item)
    {
        SlotIndex = slotIndex;
        Item = item;
    }

    public int SlotIndex { get; }
    public DRPGInventoryItemStack Item { get; }
    public bool IsEmpty => Item == null;
}

