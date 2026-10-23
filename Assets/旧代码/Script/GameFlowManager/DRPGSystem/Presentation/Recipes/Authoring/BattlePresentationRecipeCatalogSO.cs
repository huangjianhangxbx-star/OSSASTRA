using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BattlePresentationRecipeCatalog",
    menuName = "ANKALUTE/DRPG/Presentation Recipe Catalog")]
public sealed class BattlePresentationRecipeCatalogSO : ScriptableObject
{
    [SerializeField] private BattleSkillPresentationRecipeSO defaultSkillRecipe;
    [SerializeField] private List<BattleSkillPresentationRecipeSO> recipes =
        new List<BattleSkillPresentationRecipeSO>();

    public BattleSkillPresentationRecipeSO DefaultSkillRecipe => defaultSkillRecipe;
    public IReadOnlyList<BattleSkillPresentationRecipeSO> Recipes => recipes;

    public IBattleSemanticRecipeResolver CreateResolver()
    {
        return new BattleSemanticRecipeResolver(recipes, defaultSkillRecipe);
    }
}
