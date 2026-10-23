using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central policy for deciding which item categories may be dropped into the world.
/// </summary>
[CreateAssetMenu(
    fileName = "WorldDropPolicy",
    menuName = "ScriptableObject/Package/World Drop Policy")]
public sealed class WorldDropPolicySO : ScriptableObject
{
    [SerializeField] private List<ItemCategory> allowedCategories = new List<ItemCategory>();

    public IReadOnlyList<ItemCategory> AllowedCategories => allowedCategories;

    public bool CanDrop(ItemInstance item)
    {
        return item?.definition != null && allowedCategories.Contains(item.Category);
    }

    public bool AllowCategory(ItemCategory category)
    {
        if (allowedCategories.Contains(category))
            return false;

        allowedCategories.Add(category);
        return true;
    }

    public bool DisallowCategory(ItemCategory category)
    {
        return allowedCategories.Remove(category);
    }
}
