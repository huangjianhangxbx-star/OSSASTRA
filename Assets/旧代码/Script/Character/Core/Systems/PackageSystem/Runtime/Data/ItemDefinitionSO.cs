using UnityEngine;

/// <summary>
/// Static definition of "what an item is". Subclassed per category.
/// SO can be referenced by unlimited ItemInstances, but the SO itself is not a specific item.
/// </summary>
public abstract class ItemDefinitionSO : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private ItemCategory category;
    [SerializeField] private Sprite inventoryIcon;
    [SerializeField] private bool stackable = false;
    [SerializeField] private int maxStack = 1;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public ItemCategory Category => category;
    public Sprite InventoryIcon => inventoryIcon;
    public bool Stackable => stackable;
    public int MaxStack => Mathf.Max(1, maxStack);
}
