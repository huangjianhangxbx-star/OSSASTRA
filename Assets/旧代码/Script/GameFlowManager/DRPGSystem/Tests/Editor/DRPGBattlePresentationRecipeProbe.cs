#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattlePresentationRecipeProbe
{
    public static string Run()
    {
        var created = new List<ScriptableObject>();
        try
        {
            BattlePresentationRequest request = BuildImpactRequest();
            BattlePresentationRecipeSetSO recipeSet =
                BuildLayeredRecipeSet(created);
            var resolver = new BattlePresentationRecipeResolver();

            ResolvedBattlePresentationRecipe resolved =
                resolver.Resolve(request, recipeSet);

            AssertEqual(
                BattlePresentationMoment.ImpactResolved,
                request.Moment,
                "The request must preserve its semantic moment");
            AssertEqual(
                1,
                request.Facts.DamageBrokenCutInTargetIds.Count,
                "The immutable facts must retain the break target");
            AssertSequenceEqual(
                new[]
                {
                    "impact_default",
                    "skill_heavy_slash",
                    "result_break"
                },
                resolved.SourceRecipeIds,
                "Recipe layers must resolve in deterministic order");
            AssertTrue(
                resolved.Beats.Count >= 3,
                "Base, skill, and result layers must all contribute beats");
            AssertEqual(
                BattlePresentationGateKind.ActionFlow,
                resolved.GateKind,
                "The base moment recipe must own the action-flow gate");
            AssertTrue(
                resolved.Beats[resolved.Beats.Count - 1].ReleasesGate,
                "Layering must keep the gate closed until the final beat");

            AssertThrowsMessage(
                "ActionFlow recipe has no gate release beat",
                () => resolver.Resolve(
                    request,
                    BuildMissingGateRecipeSet(created)));
            AssertThrowsMessage(
                "negative duration",
                () => resolver.Resolve(
                    request,
                    BuildNegativeDurationRecipeSet(created)));

            VerifyReplacementOverrideOwnsGate(created, request, resolver);

            VerifyRequestCopiesIncomingLists();
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG presentation recipe resolution";
    }

    private static BattlePresentationRequest BuildImpactRequest()
    {
        var actor = new BattleParticipantPresentationSnapshot(
            1,
            "Actor",
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.Protagonist,
            120f,
            120f,
            DRPGLowHpPresentationState.Normal,
            null,
            null,
            null);
        var target = new BattleParticipantPresentationSnapshot(
            2,
            "Target",
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            20f,
            100f,
            DRPGLowHpPresentationState.DamageBroken,
            null,
            null,
            null);
        var hit = new BattleHitPresentationSnapshot(
            1,
            2,
            "heavy_slash",
            60f,
            40f,
            20f,
            true,
            false,
            false,
            DRPGLowHpPresentationState.DamageBroken,
            DRPGBattleRosterRole.Enemy);

        return new BattlePresentationRequest(
            42,
            BattlePresentationMoment.ImpactResolved,
            new BattlePresentationScope(3, 8, 17),
            actor,
            new[] { target },
            new[] { 1, 2 },
            "heavy_slash",
            EAnimationLogicState.Skill_1,
            new[] { hit },
            BattlePresentationFacts.FromHits(new[] { hit }));
    }

    private static BattlePresentationRecipeSetSO BuildLayeredRecipeSet(
        ICollection<ScriptableObject> created)
    {
        BattlePresentationRecipeSO baseRecipe = CreateRecipe(
            created,
            "impact_default",
            BattlePresentationGateKind.ActionFlow,
            releasesGate: true,
            duration: 0.1f,
            condition: BattlePresentationCondition.Always);
        BattlePresentationRecipeSO skillRecipe = CreateRecipe(
            created,
            "skill_heavy_slash",
            BattlePresentationGateKind.None,
            releasesGate: false,
            duration: 0.2f,
            condition: BattlePresentationCondition.ActorIsPlayer);
        BattlePresentationRecipeSO breakRecipe = CreateRecipe(
            created,
            "result_break",
            BattlePresentationGateKind.None,
            releasesGate: false,
            duration: 0.3f,
            condition: BattlePresentationCondition.AnyHitTriggeredDamageBroken);

        BattlePresentationRecipeSetSO set =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(set);
        SetField(set, "baseRecipes", new List<BattlePresentationRecipeSO>
        {
            baseRecipe
        });
        SetField(set, "skillOverrides", new List<BattlePresentationSkillRecipeOverride>
        {
            new BattlePresentationSkillRecipeOverride(
                "heavy_slash",
                BattlePresentationMoment.ImpactResolved,
                skillRecipe)
        });
        SetField(set, "breakOverlay", breakRecipe);
        return set;
    }

    private static BattlePresentationRecipeSetSO BuildMissingGateRecipeSet(
        ICollection<ScriptableObject> created)
    {
        BattlePresentationRecipeSO recipe = CreateRecipe(
            created,
            "impact_missing_gate",
            BattlePresentationGateKind.ActionFlow,
            releasesGate: false,
            duration: 0.1f,
            condition: BattlePresentationCondition.Always);
        return CreateBaseOnlySet(created, recipe);
    }

    private static void VerifyReplacementOverrideOwnsGate(
        ICollection<ScriptableObject> created,
        BattlePresentationRequest request,
        BattlePresentationRecipeResolver resolver)
    {
        BattlePresentationRecipeSO fallback = CreateRecipe(
            created,
            "impact_fallback",
            BattlePresentationGateKind.ActionFlow,
            releasesGate: true,
            duration: 1f,
            condition: BattlePresentationCondition.Always);
        BattlePresentationRecipeSO replacement = CreateRecipe(
            created,
            "impact_skill_replacement",
            BattlePresentationGateKind.ActionFlow,
            releasesGate: true,
            duration: 0.12f,
            condition: BattlePresentationCondition.ActorIsPlayer);

        BattlePresentationRecipeSetSO set =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(set);
        SetField(set, "baseRecipes", new List<BattlePresentationRecipeSO>
        {
            fallback
        });
        SetField(set, "skillOverrides", new List<BattlePresentationSkillRecipeOverride>
        {
            new BattlePresentationSkillRecipeOverride(
                "heavy_slash",
                BattlePresentationMoment.ImpactResolved,
                replacement,
                BattlePresentationSkillRecipeMode.ReplaceBase)
        });

        ResolvedBattlePresentationRecipe resolved = resolver.Resolve(request, set);
        AssertSequenceEqual(
            new[] { "impact_skill_replacement" },
            resolved.SourceRecipeIds,
            "A replacement override must replace, not append after, the fallback recipe");
        AssertEqual(
            BattlePresentationGateKind.ActionFlow,
            resolved.GateKind,
            "A replacement override must own the action-flow gate");
    }

    private static BattlePresentationRecipeSetSO BuildNegativeDurationRecipeSet(
        ICollection<ScriptableObject> created)
    {
        BattlePresentationRecipeSO recipe = CreateRecipe(
            created,
            "impact_negative_duration",
            BattlePresentationGateKind.ActionFlow,
            releasesGate: true,
            duration: -0.1f,
            condition: BattlePresentationCondition.Always);
        return CreateBaseOnlySet(created, recipe);
    }

    private static BattlePresentationRecipeSetSO CreateBaseOnlySet(
        ICollection<ScriptableObject> created,
        BattlePresentationRecipeSO recipe)
    {
        BattlePresentationRecipeSetSO set =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(set);
        SetField(set, "baseRecipes", new List<BattlePresentationRecipeSO>
        {
            recipe
        });
        return set;
    }

    private static BattlePresentationRecipeSO CreateRecipe(
        ICollection<ScriptableObject> created,
        string recipeId,
        BattlePresentationGateKind gateKind,
        bool releasesGate,
        float duration,
        BattlePresentationCondition condition)
    {
        TestScreenFlashCueSO cue =
            ScriptableObject.CreateInstance<TestScreenFlashCueSO>();
        created.Add(cue);
        SetField(cue, "cueId", recipeId + "_cue");
        SetField(cue, "kind", BattlePresentationCueKind.ScreenFlash);
        SetField(cue, "durationSeconds", duration);

        var entry = new BattlePresentationCueEntry();
        SetField(entry, "cue", cue);
        SetField(entry, "condition", condition);
        SetField(entry, "lane", BattlePresentationLaneKind.ScreenFX);
        SetField(entry, "conflictPolicy", BattlePresentationConflictPolicy.Blend);
        SetField(entry, "blocksBeat", true);

        var beat = new BattlePresentationBeat();
        SetField(beat, "beatId", recipeId + "_beat");
        SetField(beat, "delayBeforeSeconds", 0f);
        SetField(beat, "minimumDurationSeconds", 0f);
        SetField(beat, "releasesGate", releasesGate);
        SetField(beat, "cues", new List<BattlePresentationCueEntry> { entry });

        BattlePresentationRecipeSO recipe =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSO>();
        created.Add(recipe);
        SetField(recipe, "recipeId", recipeId);
        SetField(recipe, "moment", BattlePresentationMoment.ImpactResolved);
        SetField(recipe, "gateKind", gateKind);
        SetField(recipe, "maximumGateSeconds", 5f);
        SetField(recipe, "beats", new List<BattlePresentationBeat> { beat });
        return recipe;
    }

    private static void VerifyRequestCopiesIncomingLists()
    {
        var targets = new List<BattleParticipantPresentationSnapshot>();
        var timeline = new List<int> { 1, 2 };
        var hits = new List<BattleHitPresentationSnapshot>();
        var request = new BattlePresentationRequest(
            7,
            BattlePresentationMoment.TurnActivated,
            new BattlePresentationScope(1, 1, 0),
            null,
            targets,
            timeline,
            string.Empty,
            EAnimationLogicState.None,
            hits,
            BattlePresentationFacts.Empty);

        targets.Add(null);
        timeline.Add(3);
        hits.Add(null);

        AssertEqual(0, request.Targets.Count,
            "Request targets must not alias the incoming list");
        AssertEqual(2, request.TimelineRuntimeIds.Count,
            "Request timeline must not alias the incoming list");
        AssertEqual(0, request.Hits.Count,
            "Request hits must not alias the incoming list");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }

        throw new MissingFieldException(target.GetType().Name, fieldName);
    }

    private static void AssertThrowsMessage(string expected, Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            if (exception.Message.IndexOf(
                expected,
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            throw new Exception(
                $"Expected error containing '{expected}', got '{exception.Message}'.");
        }

        throw new Exception($"Expected error containing '{expected}'.");
    }

    private static void AssertSequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        string message)
    {
        AssertEqual(expected.Count, actual.Count, message + " count");
        for (int i = 0; i < expected.Count; i++)
            AssertEqual(expected[i], actual[i], message + " at " + i);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private sealed class TestScreenFlashCueSO : BattlePresentationCueSO
    {
    }
}
#endif
