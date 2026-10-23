using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGPartyBackpackEntry
{
    public DRPGPartyBackpackEntry(
        int slotIndex,
        DRPGInventoryItemStack item)
    {
        SlotIndex = slotIndex;
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Category = ResolveCategory(item.Kind);
        DisplayName = item.DisplayName;
        Detail = ResolveDetail(item);
        Icon = item.Icon;
    }

    public int SlotIndex { get; }
    public DRPGInventoryItemStack Item { get; }
    public string Category { get; }
    public string DisplayName { get; }
    public string Detail { get; }
    public Sprite Icon { get; }
    public int Quantity => Item.Quantity;

    private static string ResolveCategory(DRPGInventoryItemKind kind)
    {
        switch (kind)
        {
            case DRPGInventoryItemKind.Weapon:
                return "武器";
            case DRPGInventoryItemKind.Accessory:
                return "挂饰";
            default:
                return "物资";
        }
    }

    private static string ResolveDetail(DRPGInventoryItemStack item)
    {
        switch (item.Kind)
        {
            case DRPGInventoryItemKind.Weapon:
                DRPGWeaponInstance weapon = item.Weapon;
                return weapon != null && weapon.Definition != null
                    ? weapon.Definition.Rarity + "  耐久 " +
                      weapon.CurrentDurability + "/" +
                      weapon.CurrentMaxDurability + "  重量 " +
                      weapon.Definition.Weight.ToString("0.0")
                    : string.Empty;
            case DRPGInventoryItemKind.Accessory:
                return "重量 " + item.UnitWeight.ToString("0.0");
            default:
                return "数量 " + item.Quantity + "/" + item.MaxStack +
                    "  单重 " + item.UnitWeight.ToString("0.0");
        }
    }
}

public sealed class DRPGPartyBackpackSnapshot
{
    private readonly DRPGPartyBackpackEntry[] slots;

    private DRPGPartyBackpackSnapshot(
        int usedSlots,
        int slotCapacity,
        float currentWeight,
        float weightCapacity,
        DRPGPartyBackpackEntry[] slots,
        IReadOnlyList<DRPGPartyBackpackEntry> entries)
    {
        UsedSlots = usedSlots;
        SlotCapacity = slotCapacity;
        CurrentWeight = currentWeight;
        WeightCapacity = weightCapacity;
        this.slots = slots;
        Entries = entries;
    }

    public int UsedSlots { get; }
    public int SlotCapacity { get; }
    public float CurrentWeight { get; }
    public float WeightCapacity { get; }
    public IReadOnlyList<DRPGPartyBackpackEntry> Entries { get; }
    public int EntryCount => Entries.Count;

    public DRPGPartyBackpackEntry GetSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < slots.Length
            ? slots[slotIndex]
            : null;
    }

    public static DRPGPartyBackpackSnapshot Create(
        DRPGPartyInventoryState inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));

        var slotEntries = new DRPGPartyBackpackEntry[inventory.SlotCapacity];
        var occupied = new List<DRPGPartyBackpackEntry>();
        for (int slotIndex = 0;
             slotIndex < inventory.SlotCapacity;
             slotIndex++)
        {
            DRPGInventoryItemStack item = inventory.GetSlot(slotIndex).Item;
            if (item == null)
                continue;
            var entry = new DRPGPartyBackpackEntry(slotIndex, item);
            slotEntries[slotIndex] = entry;
            occupied.Add(entry);
        }

        return new DRPGPartyBackpackSnapshot(
            inventory.UsedSlots,
            inventory.SlotCapacity,
            inventory.CurrentWeight,
            inventory.WeightCapacity,
            slotEntries,
            occupied.AsReadOnly());
    }
}
