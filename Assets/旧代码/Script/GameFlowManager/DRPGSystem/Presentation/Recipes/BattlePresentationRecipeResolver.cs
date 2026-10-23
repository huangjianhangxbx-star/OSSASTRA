using System;
using System.Collections.Generic;

public sealed class BattlePresentationRecipeResolver
{
    private readonly IBattleSemanticRecipeResolver semanticResolver;
    private readonly IBattleSemanticRecipeCompiler semanticCompiler;

    public BattlePresentationRecipeResolver()
    {
    }

    public BattlePresentationRecipeResolver(
        IBattleSemanticRecipeResolver semanticResolver,
        IBattleSemanticRecipeCompiler semanticCompiler)
    {
        this.semanticResolver = semanticResolver ??
            throw new ArgumentNullException(nameof(semanticResolver));
        this.semanticCompiler = semanticCompiler ??
            throw new ArgumentNullException(nameof(semanticCompiler));
    }

    public ResolvedBattlePresentationRecipe Resolve(
        BattlePresentationRequest request,
        BattlePresentationRecipeSetSO recipeSet)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (recipeSet == null)
            throw new ArgumentNullException(nameof(recipeSet));

        ResolvedBattlePresentationRecipe legacy =
            ResolveLegacy(request, recipeSet);
        if (semanticResolver == null || semanticCompiler == null)
            return legacy;

        if (!semanticResolver.TryResolve(
            request.SkillPresentationKey,
            out ResolvedBattleSkillPresentation semantic,
            out _))
        {
            return legacy;
        }

        ResolvedBattlePresentationRecipe compiled =
            semanticCompiler.Compile(semantic, request);
        return Merge(request, legacy, compiled);
    }

    private static ResolvedBattlePresentationRecipe ResolveLegacy(
        BattlePresentationRequest request,
        BattlePresentationRecipeSetSO recipeSet)
    {

        BattlePresentationRecipeSO baseRecipe = recipeSet.ResolveBase(request.Moment);
        BattlePresentationRecipeSO skillRecipe = null;
        BattlePresentationSkillRecipeMode skillMode =
            BattlePresentationSkillRecipeMode.Overlay;
        if (recipeSet.TryResolveSkillOverride(
            request.SkillPresentationKey,
            request.Moment,
            out skillRecipe,
            out skillMode) &&
            skillMode == BattlePresentationSkillRecipeMode.ReplaceBase)
        {
            baseRecipe = skillRecipe;
        }

        if (baseRecipe == null)
        {
            return new ResolvedBattlePresentationRecipe(
                Array.Empty<string>(),
                BattlePresentationGateKind.None,
                0f,
                Array.Empty<ResolvedBattlePresentationBeat>());
        }

        var layers = new List<BattlePresentationRecipeSO> { baseRecipe };
        if (skillRecipe != null &&
            skillMode == BattlePresentationSkillRecipeMode.Overlay)
        {
            layers.Add(skillRecipe);
        }
        if (request.Facts.AnyHitTriggeredDamageBroken &&
            recipeSet.BreakOverlay != null)
        {
            layers.Add(recipeSet.BreakOverlay);
        }
        if (request.Facts.AnyTargetDefeated &&
            recipeSet.DefeatedOverlay != null)
        {
            layers.Add(recipeSet.DefeatedOverlay);
        }
        if (request.Facts.AnyHitRedirected &&
            recipeSet.RedirectedHitOverlay != null)
        {
            layers.Add(recipeSet.RedirectedHitOverlay);
        }

        for (int i = 0; i < layers.Count; i++)
            Validate(layers[i], request.Moment, i == 0);

        var sourceIds = new List<string>(layers.Count);
        var resolvedBeats = new List<ResolvedBattlePresentationBeat>();
        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            BattlePresentationRecipeSO layer = layers[layerIndex];
            sourceIds.Add(layer.RecipeId);
            AppendLayer(layer, request, resolvedBeats);
        }

        BattlePresentationGateKind gateKind = baseRecipe.GateKind;
        if (gateKind != BattlePresentationGateKind.None &&
            resolvedBeats.Count > 0)
        {
            NormalizeGateReleaseToFinalBeat(resolvedBeats);
        }

        return new ResolvedBattlePresentationRecipe(
            sourceIds,
            gateKind,
            baseRecipe.MaximumGateSeconds,
            resolvedBeats);
    }

    private static ResolvedBattlePresentationRecipe Merge(
        BattlePresentationRequest request,
        ResolvedBattlePresentationRecipe legacy,
        ResolvedBattlePresentationRecipe semantic)
    {
        if (semantic == null || !semantic.HasContent)
            return legacy;
        if (legacy == null || !legacy.HasContent)
            return semantic;

        var sourceIds = new List<string>(
            legacy.SourceRecipeIds.Count + semantic.SourceRecipeIds.Count);
        sourceIds.AddRange(legacy.SourceRecipeIds);
        sourceIds.AddRange(semantic.SourceRecipeIds);

        var beats = new List<ResolvedBattlePresentationBeat>(
            legacy.Beats.Count + semantic.Beats.Count);
        beats.AddRange(legacy.Beats);
        if (request != null &&
            request.Moment == BattlePresentationMoment.ImpactResolved &&
            beats.Count > 0)
        {
            int destinationIndex = beats.Count - 1;
            ResolvedBattlePresentationBeat destination = beats[destinationIndex];
            var cues = new List<ResolvedBattlePresentationCue>(destination.Cues);
            float minimumDuration = destination.MinimumDurationSeconds;
            bool releasesGate = destination.ReleasesGate;
            for (int beatIndex = 0; beatIndex < semantic.Beats.Count; beatIndex++)
            {
                ResolvedBattlePresentationBeat source = semantic.Beats[beatIndex];
                cues.AddRange(source.Cues);
                minimumDuration = Math.Max(
                    minimumDuration,
                    source.MinimumDurationSeconds);
                releasesGate |= source.ReleasesGate;
            }
            beats[destinationIndex] = new ResolvedBattlePresentationBeat(
                destination.BeatId + "+semantic-impact",
                destination.DelayBeforeSeconds,
                minimumDuration,
                releasesGate,
                cues);
        }
        else
        {
            beats.AddRange(semantic.Beats);
        }
        BattlePresentationGateKind gateKind =
            legacy.GateKind != BattlePresentationGateKind.None
                ? legacy.GateKind
                : semantic.GateKind;
        float maximumGateSeconds = Math.Max(
            legacy.MaximumGateSeconds,
            semantic.MaximumGateSeconds);
        if (gateKind != BattlePresentationGateKind.None)
            NormalizeGateReleaseToFinalBeat(beats);

        return new ResolvedBattlePresentationRecipe(
            sourceIds,
            gateKind,
            maximumGateSeconds,
            beats);
    }

    private static void Validate(
        BattlePresentationRecipeSO recipe,
        BattlePresentationMoment requestedMoment,
        bool isBase)
    {
        if (recipe == null)
            throw new InvalidOperationException("Recipe layer is null.");
        if (recipe.Moment != requestedMoment)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipe.RecipeId}' does not match moment {requestedMoment}.");
        }
        if (!isBase && recipe.GateKind != BattlePresentationGateKind.None)
        {
            throw new InvalidOperationException(
                $"Overlay recipe '{recipe.RecipeId}' cannot own a gate.");
        }
        if (recipe.MaximumGateSeconds < 0f)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipe.RecipeId}' has negative duration.");
        }
        if (recipe.Beats == null || recipe.Beats.Count == 0)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipe.RecipeId}' has no beat.");
        }

        bool hasGateRelease = false;
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            BattlePresentationBeat beat = recipe.Beats[beatIndex];
            if (beat == null)
            {
                throw new InvalidOperationException(
                    $"Recipe '{recipe.RecipeId}' contains a null beat.");
            }
            if (beat.DelayBeforeSeconds < 0f ||
                beat.MinimumDurationSeconds < 0f)
            {
                throw new InvalidOperationException(
                    $"Recipe '{recipe.RecipeId}' has negative duration.");
            }

            hasGateRelease |= beat.ReleasesGate;
            IReadOnlyList<BattlePresentationCueEntry> entries = beat.Cues;
            if (entries == null)
            {
                throw new InvalidOperationException(
                    $"Recipe '{recipe.RecipeId}' has a null cue list.");
            }
            for (int cueIndex = 0; cueIndex < entries.Count; cueIndex++)
            {
                BattlePresentationCueEntry entry = entries[cueIndex];
                if (entry == null || entry.Cue == null)
                {
                    throw new InvalidOperationException(
                        $"Recipe '{recipe.RecipeId}' contains a null cue.");
                }
                if (entry.Cue.DurationSeconds < 0f)
                {
                    throw new InvalidOperationException(
                        $"Recipe '{recipe.RecipeId}' has negative duration.");
                }
            }
        }

        if (recipe.GateKind == BattlePresentationGateKind.ActionFlow &&
            !hasGateRelease)
        {
            throw new InvalidOperationException(
                "ActionFlow recipe has no gate release beat: " +
                recipe.RecipeId);
        }
    }

    private static void AppendLayer(
        BattlePresentationRecipeSO recipe,
        BattlePresentationRequest request,
        ICollection<ResolvedBattlePresentationBeat> destination)
    {
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            BattlePresentationBeat sourceBeat = recipe.Beats[beatIndex];
            var cues = new List<ResolvedBattlePresentationCue>();
            for (int cueIndex = 0; cueIndex < sourceBeat.Cues.Count; cueIndex++)
            {
                BattlePresentationCueEntry sourceCue =
                    sourceBeat.Cues[cueIndex];
                if (!Matches(sourceCue.Condition, request))
                    continue;

                cues.Add(new ResolvedBattlePresentationCue(
                    sourceCue.Cue,
                    sourceCue.Lane,
                    sourceCue.ConflictPolicy,
                    sourceCue.BlocksBeat));
            }

            destination.Add(new ResolvedBattlePresentationBeat(
                sourceBeat.BeatId,
                sourceBeat.DelayBeforeSeconds,
                sourceBeat.MinimumDurationSeconds,
                sourceBeat.ReleasesGate,
                cues));
        }
    }

    private static bool Matches(
        BattlePresentationCondition condition,
        BattlePresentationRequest request)
    {
        switch (condition)
        {
            case BattlePresentationCondition.Always:
                return true;
            case BattlePresentationCondition.ActorIsPlayer:
                return request.Actor != null &&
                    request.Actor.Side == DRPGBattleSide.Player;
            case BattlePresentationCondition.ActorIsEnemy:
                return request.Actor != null &&
                    request.Actor.Side == DRPGBattleSide.Enemy;
            case BattlePresentationCondition.AnyHitTriggeredDamageBroken:
                return request.Facts.AnyHitTriggeredDamageBroken;
            case BattlePresentationCondition.AnyTargetDefeated:
                return request.Facts.AnyTargetDefeated;
            case BattlePresentationCondition.AnyHitRedirected:
                return request.Facts.AnyHitRedirected;
            default:
                return false;
        }
    }

    private static void NormalizeGateReleaseToFinalBeat(
        IList<ResolvedBattlePresentationBeat> beats)
    {
        for (int i = 0; i < beats.Count; i++)
        {
            ResolvedBattlePresentationBeat beat = beats[i];
            beats[i] = new ResolvedBattlePresentationBeat(
                beat.BeatId,
                beat.DelayBeforeSeconds,
                beat.MinimumDurationSeconds,
                i == beats.Count - 1,
                beat.Cues);
        }
    }
}
