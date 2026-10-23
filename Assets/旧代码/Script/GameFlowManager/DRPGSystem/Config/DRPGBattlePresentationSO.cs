using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGBattlePresentation",
    menuName = "ANKALUTE/DRPG/Battle Presentation")]
public sealed class DRPGBattlePresentationSO : ScriptableObject
{
    [SerializeField] private Color charcoal = new Color32(17, 20, 28, 245);
    [SerializeField] private Color wineRed = new Color32(84, 17, 36, 245);
    [SerializeField] private Color accentMagenta = new Color32(239, 35, 112, 255);
    [SerializeField] private Color metallicWhite = new Color32(226, 229, 236, 255);
    [SerializeField] private Sprite fallbackBattleBackground;
    [SerializeField] private DRPGBattleAudioPaletteSO audioPalette;
    [SerializeField] private BattlePresentationRecipeSetSO recipeSet;
    [SerializeField] private BattleSkillPresentationRecipeSO defaultSkillRecipe;
    [SerializeField] private BattlePresentationRecipeCatalogSO semanticRecipeCatalog;
    [SerializeField] private BattlePresentationRecipeSO entryRecipe;
    [SerializeField] private BattleEntryPresentationRecipeSO fixedEntryRecipe;
    [SerializeField] private BattleStageProfileSO defaultStageProfile;
    [SerializeField] private DRPGBasicAttackTimingProfile basicAttackTimingProfile;
    [SerializeField] private bool standardBasicAttackTransactionEnabled;
    public bool StandardBasicAttackTransactionEnabled => standardBasicAttackTransactionEnabled;

    public Color Charcoal => charcoal;
    public Color WineRed => wineRed;
    public Color AccentMagenta => accentMagenta;
    public Color MetallicWhite => metallicWhite;
    public Sprite FallbackBattleBackground => fallbackBattleBackground;
    public DRPGBattleAudioPaletteSO AudioPalette => audioPalette;
    public BattlePresentationRecipeSetSO RecipeSet => recipeSet;
    public BattleSkillPresentationRecipeSO DefaultSkillRecipe => defaultSkillRecipe;
    public BattlePresentationRecipeCatalogSO SemanticRecipeCatalog =>
        semanticRecipeCatalog;
    public BattlePresentationRecipeSO EntryRecipe => entryRecipe;
    public BattleEntryPresentationRecipeSO FixedEntryRecipe => fixedEntryRecipe;
    public BattleStageProfileSO DefaultStageProfile => defaultStageProfile;
    public DRPGBasicAttackTimingProfile BasicAttackTimingProfile => basicAttackTimingProfile;
}
