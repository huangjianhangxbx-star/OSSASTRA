using UnityEngine;

[CreateAssetMenu(menuName = "ANKALUTE/DRPG/Battle Card Preview")]
public sealed class DRPGBattleCardPreviewSO : ScriptableObject
{
    [SerializeField] private string cardId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite artwork;
    [SerializeField] private BattleCardTargetRule targetRule;

    public string CardId => cardId ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public string Description => description ?? string.Empty;
    public Sprite Artwork => artwork;
    public BattleCardTargetRule TargetRule => targetRule;
}
