using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGOutfit",
    menuName = "ANKALUTE/DRPG/Equipment/Outfit")]
public sealed class DRPGOutfitDefinitionSO : ScriptableObject
{
    [SerializeField] private string outfitId = "outfit";
    [SerializeField] private string displayName = "Outfit";
    [SerializeField] private Sprite icon;
    [Tooltip("Optional always-on trait contributed by this outfit.")]
    [SerializeField] private DRPGTraitDefinitionSO grantedTrait;
    [HideInInspector]
    [SerializeField] private string grantedTraitId;

    public string OutfitId => outfitId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public DRPGTraitDefinitionSO GrantedTrait => grantedTrait;
    public string GrantedTraitId => grantedTrait != null
        ? grantedTrait.TraitId
        : grantedTraitId;
}
