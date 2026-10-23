using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattlePresentationSkillRecipeMode
{
    Overlay = 0,
    ReplaceBase = 1
}

[Serializable]
public sealed class BattlePresentationSkillRecipeOverride
{
    [SerializeField] private string presentationKey;
    [SerializeField] private BattlePresentationMoment moment;
    [SerializeField] private BattlePresentationRecipeSO recipe;
    [SerializeField] private BattlePresentationSkillRecipeMode mode =
        BattlePresentationSkillRecipeMode.Overlay;

    public BattlePresentationSkillRecipeOverride(
        string presentationKey,
        BattlePresentationMoment moment,
        BattlePresentationRecipeSO recipe,
        BattlePresentationSkillRecipeMode mode =
            BattlePresentationSkillRecipeMode.Overlay)
    {
        this.presentationKey = presentationKey ?? string.Empty;
        this.moment = moment;
        this.recipe = recipe;
        this.mode = mode;
    }

    public string PresentationKey => presentationKey ?? string.Empty;
    public BattlePresentationMoment Moment => moment;
    public BattlePresentationRecipeSO Recipe => recipe;
    public BattlePresentationSkillRecipeMode Mode => mode;
}

[CreateAssetMenu(
    fileName = "BattlePresentationRecipeSet",
    menuName = "ANKALUTE/DRPG/Presentation Recipe Set")]
public sealed class BattlePresentationRecipeSetSO : ScriptableObject
{
    [SerializeField] private List<BattlePresentationRecipeSO> baseRecipes =
        new List<BattlePresentationRecipeSO>();
    [SerializeField] private List<BattlePresentationSkillRecipeOverride>
        skillOverrides = new List<BattlePresentationSkillRecipeOverride>();
    [SerializeField] private BattlePresentationRecipeSO breakOverlay;
    [SerializeField] private BattlePresentationRecipeSO defeatedOverlay;
    [SerializeField] private BattlePresentationRecipeSO redirectedHitOverlay;
    [SerializeField] private BattlePresentationRecipeCatalogSO semanticRecipeCatalog;

    public BattlePresentationRecipeSO BreakOverlay => breakOverlay;
    public BattlePresentationRecipeSO DefeatedOverlay => defeatedOverlay;
    public BattlePresentationRecipeSO RedirectedHitOverlay => redirectedHitOverlay;
    public BattlePresentationRecipeCatalogSO SemanticRecipeCatalog =>
        semanticRecipeCatalog;

    public BattlePresentationRecipeSO ResolveBase(
        BattlePresentationMoment moment)
    {
        if (baseRecipes == null)
            return null;

        for (int i = 0; i < baseRecipes.Count; i++)
        {
            BattlePresentationRecipeSO candidate = baseRecipes[i];
            if (candidate != null && candidate.Moment == moment)
                return candidate;
        }
        return null;
    }

    public bool TryResolveSkillOverride(
        string presentationKey,
        BattlePresentationMoment moment,
        out BattlePresentationRecipeSO recipe)
    {
        return TryResolveSkillOverride(
            presentationKey,
            moment,
            out recipe,
            out _);
    }

    public bool TryResolveSkillOverride(
        string presentationKey,
        BattlePresentationMoment moment,
        out BattlePresentationRecipeSO recipe,
        out BattlePresentationSkillRecipeMode mode)
    {
        recipe = null;
        mode = BattlePresentationSkillRecipeMode.Overlay;
        if (string.IsNullOrWhiteSpace(presentationKey) || skillOverrides == null)
            return false;

        for (int i = 0; i < skillOverrides.Count; i++)
        {
            BattlePresentationSkillRecipeOverride candidate = skillOverrides[i];
            if (candidate != null &&
                candidate.Moment == moment &&
                string.Equals(
                    candidate.PresentationKey,
                    presentationKey,
                    StringComparison.Ordinal))
            {
                recipe = candidate.Recipe;
                mode = candidate.Mode;
                return recipe != null;
            }
        }
        return false;
    }
}
