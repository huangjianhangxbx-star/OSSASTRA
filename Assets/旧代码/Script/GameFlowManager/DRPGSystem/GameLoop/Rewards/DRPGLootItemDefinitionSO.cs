using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGLootItem",
    menuName = "ANKALUTE/DRPG/Loot Item")]
public sealed class DRPGLootItemDefinitionSO : ScriptableObject
{
    [SerializeField] private string itemId = "loot_item";
    [SerializeField] private string displayName = "战利品";
    [TextArea(2, 5)]
    [SerializeField] private string description = string.Empty;
    [SerializeField] private Sprite icon;
    [Min(0f)]
    [SerializeField] private float weight = 0.1f;
    [Min(1)]
    [SerializeField] private int maxStack = 20;

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public string Description => description ?? string.Empty;
    public Sprite Icon => icon;
    public float Weight => Mathf.Max(0f, weight);
    public int MaxStack => Mathf.Max(1, maxStack);
}
