#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleSemanticRecipeCompilationProbe
{
    public static string Run()
    {
        var created = new List<ScriptableObject>();
        try
        {
            BattleSkillPresentationRecipeSO recipe =
                ScriptableObject.CreateInstance<BattleSkillPresentationRecipeSO>();
            created.Add(recipe);
            SetField(recipe, "presentationKey", "semantic-probe");
            ConfigureModule(recipe, "hud", BattlePresentationOverrideMode.Override);
            ConfigureModule(recipe, "camera", BattlePresentationOverrideMode.Override);
            ConfigureModule(recipe, "spine", BattlePresentationOverrideMode.Override);
            ConfigureModule(recipe, "feedback", BattlePresentationOverrideMode.Override);
            ConfigureModule(recipe, "exit", BattlePresentationOverrideMode.Override);

            BattlePresentationRecipeCatalogSO catalog =
                ScriptableObject.CreateInstance<BattlePresentationRecipeCatalogSO>();
            created.Add(catalog);
            SetField(catalog, "defaultSkillRecipe", recipe);
            SetField(catalog, "recipes", new List<BattleSkillPresentationRecipeSO> { recipe });

            IBattleSemanticRecipeResolver semanticResolver = catalog.CreateResolver();
            AssertTrue(
                semanticResolver.TryResolve(
                    "semantic-probe",
                    out ResolvedBattleSkillPresentation semantic,
                    out string error),
                "Catalog recipe must resolve: " + error);

            using (var compiler = new BattleSemanticRecipeCompiler())
            {
                ResolvedBattlePresentationRecipe windup = compiler.Compile(
                    semantic,
                    BuildRequest(
                        "semantic-probe",
                        BattlePresentationMoment.ActionWindup));
                AssertEqual("semantic:semantic-probe", windup.SourceRecipeIds[0],
                    "Compiled recipes need a traceable semantic source id");
                AssertTrue(ContainsModule(windup, BattleSemanticPresentationModuleKind.Hud),
                    "Enabled HUD module must compile into a typed cue");
                AssertTrue(ContainsModule(windup, BattleSemanticPresentationModuleKind.Camera),
                    "Enabled camera module must compile into a typed cue");
                AssertTrue(ContainsModule(windup, BattleSemanticPresentationModuleKind.Spine),
                    "Windup must start the acting character presentation");
                AssertTrue(!ContainsModule(windup, BattleSemanticPresentationModuleKind.Feedback),
                    "Impact feedback must not fire during windup");
                AssertTrue(!ContainsModule(windup, BattleSemanticPresentationModuleKind.Exit),
                    "Windup must keep the conflict scope alive through impact");
                AssertEqual(BattlePresentationGateKind.ActionFlow, windup.GateKind,
                    "Windup must hold the action-flow gate until the attack pose is readable");
                AssertTrue(IsModuleBlocking(
                        windup,
                        BattleSemanticPresentationModuleKind.Spine),
                    "The semantic actor animation must own the conflict beat lifetime");
                AssertTrue(IsModuleGateRelease(
                        windup,
                        BattleSemanticPresentationModuleKind.Spine),
                    "The attack-pose beat must release the windup gate only after it finishes");
                VerifyReadableWindupPhases(windup);

                ResolvedBattlePresentationRecipe impact = compiler.Compile(
                    semantic,
                    BuildRequest(
                        "semantic-probe",
                        BattlePresentationMoment.ImpactResolved));
                AssertTrue(ContainsModule(
                        impact,
                        BattleSemanticPresentationModuleKind.Feedback),
                    "Impact must compile feedback exactly in its own moment");
                AssertTrue(!ContainsModule(
                        impact,
                        BattleSemanticPresentationModuleKind.Camera),
                    "Impact must preserve the windup camera instead of replaying it");
                AssertTrue(!ContainsModule(
                        impact,
                        BattleSemanticPresentationModuleKind.Exit),
                    "Impact must not restore the conflict scope before settlement");
                AssertTrue(IsModuleBlocking(
                        impact,
                        BattleSemanticPresentationModuleKind.Feedback),
                    "Impact feedback must hold the conflict close-up long enough to read");
                AssertEqual(BattlePresentationGateKind.ActionFlow,
                    impact.GateKind,
                    "The readable impact hold must own the action-flow gate");
                AssertTrue(GetModuleBeatDuration(
                        impact,
                        BattleSemanticPresentationModuleKind.Feedback) >= 0.70f,
                    "Impact feedback needs a readable minimum beat, not a one-frame flash");
                VerifyReadableImpactHold();

                ResolvedBattlePresentationRecipe settled = compiler.Compile(
                    semantic,
                    BuildRequest(
                        "semantic-probe",
                        BattlePresentationMoment.ActionSettled));
                AssertTrue(ContainsModule(
                        settled,
                        BattleSemanticPresentationModuleKind.Exit),
                    "Action settlement must compile the restoration cue");
                AssertTrue(!ContainsModule(
                        settled,
                        BattleSemanticPresentationModuleKind.Camera),
                    "Settlement must restore rather than replay the conflict camera");
                AssertTrue(IsModuleBlocking(
                        settled,
                        BattleSemanticPresentationModuleKind.Exit),
                    "Settlement must wait for the visible return-to-slot motion");
                AssertTrue(GetModuleBeatDuration(
                        settled,
                        BattleSemanticPresentationModuleKind.Exit) >= 0.28f,
                    "Return-to-slot needs a readable minimum duration");

                VerifyConflictLifecyclePolicy();

                VerifyIntegratedResolverKeepsLegacyBase(created, semanticResolver, compiler);
                VerifyImpactFeedbackSharesOneBeat(
                    created,
                    semanticResolver,
                    compiler);
            }
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG semantic recipe compilation";
    }

    private static void VerifyReadableWindupPhases(
        ResolvedBattlePresentationRecipe windup)
    {
        int focusBeat = FindModuleBeat(
            windup,
            BattleSemanticPresentationModuleKind.Hud);
        int intentBeat = FindModuleBeat(
            windup,
            BattleSemanticPresentationModuleKind.Spine);
        AssertTrue(focusBeat >= 0 && intentBeat > focusBeat,
            "Conflict setup/focus must be visible before the attack pose starts");
        AssertTrue(windup.Beats[focusBeat].MinimumDurationSeconds >= 0.18f,
            "Conflict focus needs a readable setup beat");
        AssertTrue(windup.Beats[intentBeat].MinimumDurationSeconds >= 0.50f,
            "The attack pose needs enough time to be read for both sides");
    }

    private static void VerifyConflictLifecyclePolicy()
    {
        AssertTrue(!BattleConflictPresentationLifecycle.ShouldRestoreAfterRun(
                BattlePresentationMoment.ActionWindup,
                BattlePresentationOutcome.Completed,
                BattlePresentationOutcome.Completed),
            "A completed windup must keep the conflict scope alive");
        AssertTrue(!BattleConflictPresentationLifecycle.ShouldRestoreAfterRun(
                BattlePresentationMoment.ImpactResolved,
                BattlePresentationOutcome.Completed,
                BattlePresentationOutcome.Completed),
            "A completed impact must keep the conflict scope alive until settlement");
        AssertTrue(BattleConflictPresentationLifecycle.ShouldRestoreAfterRun(
                BattlePresentationMoment.ActionSettled,
                BattlePresentationOutcome.Completed,
                BattlePresentationOutcome.Completed),
            "A completed action settlement must restore the conflict scope");
        AssertTrue(BattleConflictPresentationLifecycle.ShouldRestoreAfterRun(
                BattlePresentationMoment.ActionWindup,
                BattlePresentationOutcome.Faulted,
                BattlePresentationOutcome.Faulted),
            "A faulted windup must fail open and restore the conflict scope");
        AssertTrue(BattleConflictPresentationLifecycle.ShouldRestoreAfterRun(
                BattlePresentationMoment.ImpactResolved,
                BattlePresentationOutcome.Cancelled,
                BattlePresentationOutcome.Cancelled),
            "A cancelled impact must restore the conflict scope");
        AssertTrue(!BattleConflictPresentationLifecycle.ShouldRestoreAfterRun(
                BattlePresentationMoment.ActionWindup,
                BattlePresentationOutcome.Cancelled,
                BattlePresentationOutcome.Completed),
            "A windup whose action-flow gate completed must not be rolled back " +
            "when a non-blocking decoration is replaced by the next phase");
    }

    private static void VerifyReadableImpactHold()
    {
        PropertyInfo property = typeof(BattleFeedbackPresentationDefinition)
            .GetProperty(
                "HoldSeconds",
                BindingFlags.Public | BindingFlags.Instance);
        AssertTrue(property != null,
            "Feedback definitions require an authored impact hold duration");
        var definition = new BattleFeedbackPresentationDefinition();
        float seconds = (float)property.GetValue(definition);
        AssertTrue(seconds >= 0.35f,
            "Default impact hold must remain visually readable");
    }

    private static void VerifyIntegratedResolverKeepsLegacyBase(
        ICollection<ScriptableObject> created,
        IBattleSemanticRecipeResolver semanticResolver,
        IBattleSemanticRecipeCompiler compiler)
    {
        BattlePresentationRecipeSetSO emptySet =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(emptySet);
        var resolver = new BattlePresentationRecipeResolver(
            semanticResolver,
            compiler);
        ResolvedBattlePresentationRecipe resolved = resolver.Resolve(
            BuildRequest(
                "semantic-probe",
                BattlePresentationMoment.ActionWindup),
            emptySet);

        AssertTrue(resolved.HasContent,
            "Semantic content must be usable when the legacy recipe set has no base");
        AssertEqual(BattlePresentationGateKind.ActionFlow, resolved.GateKind,
            "An empty legacy set must still preserve the semantic windup gate");

        ResolvedBattlePresentationRecipe legacyOnly =
            new BattlePresentationRecipeResolver().Resolve(
                BuildRequest(
                    "semantic-probe",
                    BattlePresentationMoment.ActionWindup),
                emptySet);
        AssertTrue(!legacyOnly.HasContent,
            "The default resolver must preserve the old no-catalog behavior");
    }

    private static void VerifyImpactFeedbackSharesOneBeat(
        ICollection<ScriptableObject> created,
        IBattleSemanticRecipeResolver semanticResolver,
        IBattleSemanticRecipeCompiler compiler)
    {
        BattleImpactCueSO impactCue =
            ScriptableObject.CreateInstance<BattleImpactCueSO>();
        created.Add(impactCue);
        SetField(impactCue, "holdSeconds", 1f);

        var entry = new BattlePresentationCueEntry();
        SetField(entry, "cue", impactCue);
        SetField(entry, "condition", BattlePresentationCondition.Always);
        SetField(entry, "lane", BattlePresentationLaneKind.TargetFeedback);
        SetField(entry, "conflictPolicy", BattlePresentationConflictPolicy.Blend);
        SetField(entry, "blocksBeat", true);

        var beat = new BattlePresentationBeat();
        SetField(beat, "beatId", "legacy-impact");
        SetField(beat, "delayBeforeSeconds", 0f);
        SetField(beat, "minimumDurationSeconds", 1f);
        SetField(beat, "releasesGate", true);
        SetField(beat, "cues", new List<BattlePresentationCueEntry> { entry });

        BattlePresentationRecipeSO legacyRecipe =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSO>();
        created.Add(legacyRecipe);
        SetField(legacyRecipe, "recipeId", "legacy-impact-probe");
        SetField(legacyRecipe, "moment", BattlePresentationMoment.ImpactResolved);
        SetField(legacyRecipe, "gateKind", BattlePresentationGateKind.ActionFlow);
        SetField(legacyRecipe, "maximumGateSeconds", 5f);
        SetField(legacyRecipe, "beats", new List<BattlePresentationBeat> { beat });

        BattlePresentationRecipeSetSO set =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(set);
        SetField(set, "baseRecipes", new List<BattlePresentationRecipeSO>
        {
            legacyRecipe
        });

        var resolver = new BattlePresentationRecipeResolver(
            semanticResolver,
            compiler);
        ResolvedBattlePresentationRecipe resolved = resolver.Resolve(
            BuildRequest(
                "semantic-probe",
                BattlePresentationMoment.ImpactResolved),
            set);
        int legacyBeat = FindCueKindBeat(
            resolved,
            BattlePresentationCueKind.ImpactFeedback);
        int semanticBeat = FindModuleBeat(
            resolved,
            BattleSemanticPresentationModuleKind.Feedback);
        AssertTrue(legacyBeat >= 0,
            "The legacy damage-number cue must remain present");
        AssertEqual(legacyBeat, semanticBeat,
            "Damage numbers and world hit reaction must run concurrently, not as two flashes");
    }

    private static bool ContainsModule(
        ResolvedBattlePresentationRecipe recipe,
        BattleSemanticPresentationModuleKind kind)
    {
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            IReadOnlyList<ResolvedBattlePresentationCue> cues = recipe.Beats[beatIndex].Cues;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                if (cues[cueIndex].Cue is BattleSemanticPresentationCueSO semantic &&
                    semantic.ModuleKind == kind)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static int FindModuleBeat(
        ResolvedBattlePresentationRecipe recipe,
        BattleSemanticPresentationModuleKind kind)
    {
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            IReadOnlyList<ResolvedBattlePresentationCue> cues =
                recipe.Beats[beatIndex].Cues;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                if (cues[cueIndex].Cue is BattleSemanticPresentationCueSO semantic &&
                    semantic.ModuleKind == kind)
                {
                    return beatIndex;
                }
            }
        }
        return -1;
    }

    private static bool IsModuleBlocking(
        ResolvedBattlePresentationRecipe recipe,
        BattleSemanticPresentationModuleKind kind)
    {
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            IReadOnlyList<ResolvedBattlePresentationCue> cues =
                recipe.Beats[beatIndex].Cues;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                if (cues[cueIndex].Cue is BattleSemanticPresentationCueSO semantic &&
                    semantic.ModuleKind == kind)
                {
                    return cues[cueIndex].BlocksBeat;
                }
            }
        }
        return false;
    }

    private static bool IsModuleGateRelease(
        ResolvedBattlePresentationRecipe recipe,
        BattleSemanticPresentationModuleKind kind)
    {
        int index = FindModuleBeat(recipe, kind);
        return index >= 0 && recipe.Beats[index].ReleasesGate;
    }

    private static float GetModuleBeatDuration(
        ResolvedBattlePresentationRecipe recipe,
        BattleSemanticPresentationModuleKind kind)
    {
        int index = FindModuleBeat(recipe, kind);
        return index >= 0
            ? recipe.Beats[index].MinimumDurationSeconds
            : -1f;
    }

    private static int FindCueKindBeat(
        ResolvedBattlePresentationRecipe recipe,
        BattlePresentationCueKind kind)
    {
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            IReadOnlyList<ResolvedBattlePresentationCue> cues =
                recipe.Beats[beatIndex].Cues;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                if (cues[cueIndex].Cue != null &&
                    cues[cueIndex].Cue.Kind == kind)
                {
                    return beatIndex;
                }
            }
        }
        return -1;
    }

    private static BattlePresentationRequest BuildRequest(
        string key,
        BattlePresentationMoment moment)
    {
        return new BattlePresentationRequest(
            1,
            moment,
            new BattlePresentationScope(1, 1, 0),
            null,
            Array.Empty<BattleParticipantPresentationSnapshot>(),
            Array.Empty<int>(),
            key,
            EAnimationLogicState.None,
            Array.Empty<BattleHitPresentationSnapshot>(),
            BattlePresentationFacts.Empty);
    }

    private static void ConfigureModule(
        object recipe,
        string fieldName,
        BattlePresentationOverrideMode mode)
    {
        object module = GetField(recipe, fieldName);
        SetField(module, "mode", mode);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
