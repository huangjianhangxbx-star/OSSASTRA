using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGBattleItem",
    menuName = "ANKALUTE/DRPG/Battle Item")]
public sealed class DRPGBattleItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId = "medicine";
    [SerializeField] private string displayName = "Medicine";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private ItemDefinitionSO packageItemDefinition;
    [Min(0f)]
    [SerializeField] private float healAmount = 30f;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public ItemDefinitionSO PackageItemDefinition => packageItemDefinition;
    public float HealAmount => Mathf.Max(0f, healAmount);
}
