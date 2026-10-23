using System;
using UnityEngine;

/// <summary>
/// Runtime instance of "which specific item this is".
/// Weapons always get a unique instanceId and quantity=1, never stack.
/// Stackable items with same definition can merge up to maxStack.
/// </summary>
[Serializable]
public sealed class ItemInstance
{
    public string instanceId;
    public ItemDefinitionSO definition;
    public int quantity = 1;

    public ItemInstance() { }

    public ItemInstance(ItemDefinitionSO def, int qty = 1)
    {
        instanceId = Guid.NewGuid().ToString("N");
        definition = def;
        quantity = Mathf.Max(1, qty);
    }

    public ItemCategory Category => definition != null ? definition.Category : ItemCategory.Misc;
    public bool IsWeapon => Category == ItemCategory.Weapon;
    public bool Stackable => definition != null && definition.Stackable;
    public string DisplayName => definition != null ? definition.DisplayName : "Unknown";
    public Sprite Icon => definition != null ? definition.InventoryIcon : null;
}