using System;
using System.Collections.Generic;

public interface IBattleSemanticRecipeResolver
{
    bool TryResolve(
        string presentationKey,
        out ResolvedBattleSkillPresentation result,
        out string error);
}

public sealed class BattleSemanticRecipeResolver : IBattleSemanticRecipeResolver
{
    private readonly Dictionary<string, BattleSkillPresentationRecipeSO> recipes =
        new Dictionary<string, BattleSkillPresentationRecipeSO>(StringComparer.Ordinal);
    private readonly BattleSkillPresentationRecipeSO fallback;
    private readonly string catalogError;

    public BattleSemanticRecipeResolver(
        IEnumerable<BattleSkillPresentationRecipeSO> recipes,
        BattleSkillPresentationRecipeSO fallback)
    {
        this.fallback = fallback;
        if (recipes == null)
            return;

        foreach (BattleSkillPresentationRecipeSO recipe in recipes)
        {
            if (recipe == null)
                continue;
            string key = recipe.PresentationKey;
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (this.recipes.ContainsKey(key))
            {
                catalogError = "Duplicate PresentationKey '" + key + "'.";
                continue;
            }
            this.recipes.Add(key, recipe);
        }
    }

    public bool TryResolve(
        string presentationKey,
        out ResolvedBattleSkillPresentation result,
        out string error)
    {
        result = null;
        error = catalogError;
        if (!string.IsNullOrEmpty(error))
            return false;

        BattleSkillPresentationRecipeSO recipe = null;
        if (!string.IsNullOrWhiteSpace(presentationKey))
            recipes.TryGetValue(presentationKey, out recipe);
        if (recipe == null)
            recipe = fallback;
        if (recipe == null)
        {
            error = "No semantic presentation recipe or fallback is configured.";
            return false;
        }

        var chain = new List<BattleSkillPresentationRecipeSO>();
        var visiting = new HashSet<BattleSkillPresentationRecipeSO>();
        if (!BuildChain(recipe, visiting, chain, out error))
            return false;

        result = ResolveChain(chain, recipe.PresentationKey);
        return true;
    }

    private static bool BuildChain(
        BattleSkillPresentationRecipeSO recipe,
        ISet<BattleSkillPresentationRecipeSO> visiting,
        IList<BattleSkillPresentationRecipeSO> chain,
        out string error)
    {
        if (!visiting.Add(recipe))
        {
            var keys = new List<string>();
            bool include = false;
            for (int i = 0; i < chain.Count; i++)
            {
                include |= chain[i] == recipe;
                if (include)
                    keys.Add(chain[i].PresentationKey);
            }
            keys.Add(recipe.PresentationKey);
            error = "Semantic recipe inheritance cycle: " +
                string.Join(" -> ", keys) + ".";
            return false;
        }

        chain.Add(recipe);
        if (recipe.Parent != null &&
            !BuildChain(recipe.Parent, visiting, chain, out error))
        {
            return false;
        }
        visiting.Remove(recipe);
        error = string.Empty;
        return true;
    }

    private static ResolvedBattleSkillPresentation ResolveChain(
        IReadOnlyList<BattleSkillPresentationRecipeSO> leafToRoot,
        string key)
    {
        ResolvedBattleSkillPresentation current = CreateDefaults(key);
        for (int i = leafToRoot.Count - 1; i >= 0; i--)
            current = Apply(leafToRoot[i], current, key);
        return current;
    }

    private static ResolvedBattleSkillPresentation Apply(
        BattleSkillPresentationRecipeSO recipe,
        ResolvedBattleSkillPresentation inherited,
        string key)
    {
        return new ResolvedBattleSkillPresentation(
            key,
            Resolve(recipe.Hud.Mode, recipe.Hud.Definition, inherited.Hud, value => value.Snapshot()),
            Resolve(recipe.Stage.Mode, recipe.Stage.Definition, inherited.Stage, value => value.Snapshot()),
            Resolve(recipe.Camera.Mode, recipe.Camera.Definition, inherited.Camera, value => value.Snapshot()),
            Resolve(recipe.Attacker.Mode, recipe.Attacker.Definition, inherited.Attacker, value => value.Snapshot()),
            Resolve(recipe.Target.Mode, recipe.Target.Definition, inherited.Target, value => value.Snapshot()),
            Resolve(recipe.Background.Mode, recipe.Background.Definition, inherited.Background, value => value.Snapshot()),
            Resolve(recipe.Spine.Mode, recipe.Spine.Definition, inherited.Spine, value => value.Snapshot()),
            Resolve(recipe.Feedback.Mode, recipe.Feedback.Definition, inherited.Feedback, value => value.Snapshot()),
            Resolve(recipe.Exit.Mode, recipe.Exit.Definition, inherited.Exit, value => value.Snapshot()));
    }

    private static ResolvedBattlePresentationModule<T> Resolve<T>(
        BattlePresentationOverrideMode mode,
        T own,
        ResolvedBattlePresentationModule<T> inherited,
        Func<T, T> snapshot)
        where T : class
    {
        switch (mode)
        {
            case BattlePresentationOverrideMode.Override:
                return new ResolvedBattlePresentationModule<T>(
                    mode,
                    true,
                    snapshot(own));
            case BattlePresentationOverrideMode.Disabled:
                return new ResolvedBattlePresentationModule<T>(
                    mode,
                    false,
                    snapshot(own));
            default:
                return new ResolvedBattlePresentationModule<T>(
                    mode,
                    inherited.Enabled,
                    inherited.Definition);
        }
    }

    private static ResolvedBattleSkillPresentation CreateDefaults(string key)
    {
        return new ResolvedBattleSkillPresentation(
            key,
            Default(new BattleHudPresentationDefinition()),
            Default(new BattleStagePresentationDefinition()),
            Default(new BattleCameraPresentationDefinition()),
            Default(new BattleActorPresentationDefinition()),
            Default(new BattleTargetPresentationDefinition()),
            Default(new BattleBackgroundPresentationDefinition()),
            Default(new BattleSpinePresentationDefinition()),
            Default(new BattleFeedbackPresentationDefinition()),
            Default(new BattleExitPresentationDefinition()));
    }

    private static ResolvedBattlePresentationModule<T> Default<T>(T definition)
        where T : class
    {
        return new ResolvedBattlePresentationModule<T>(
            BattlePresentationOverrideMode.Inherit,
            true,
            definition);
    }
}
