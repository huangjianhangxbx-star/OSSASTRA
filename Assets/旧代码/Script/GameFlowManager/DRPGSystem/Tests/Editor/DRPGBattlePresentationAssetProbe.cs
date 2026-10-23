#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattlePresentationAssetProbe
{
    private const string PresentationPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGBattlePresentation_Test.asset";
    private const string BasicAttackPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGSkill_BasicAttack.asset";
    private const string DebugBreakPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGSkill_DebugBreak.asset";
    private const string PrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string PartyPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGParty_Test3.asset";
    private const string EncounterPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset";
    private const string ProtagonistProfilePath =
        "Assets/GameResources/DRPG/Config/Test/DRPGCombatant_ankalute_protagonist.asset";
    private const string VanguardProfilePath =
        "Assets/GameResources/DRPG/Config/Test/DRPGCombatant_ankalute_party_vanguard.asset";

    public static string Run()
    {
        DRPGBattlePresentationSO presentation = Load<DRPGBattlePresentationSO>(
            PresentationPath);
        AssertNotNull(presentation.RecipeSet,
            "The battle presentation profile must own a recipe set");
        AssertNotNull(
            presentation.RecipeSet.ResolveBase(
                BattlePresentationMoment.ActionWindup),
            "The recipe set must resolve the default action-windup recipe");
        AssertNotNull(
            presentation.RecipeSet.ResolveBase(
                BattlePresentationMoment.ImpactResolved),
            "The recipe set must resolve the default impact recipe");
        AssertNotNull(presentation.RecipeSet.BreakOverlay,
            "The recipe set must own a break overlay recipe");
        AssertTrue(
            presentation.RecipeSet.TryResolveSkillOverride(
                "basic_attack",
                BattlePresentationMoment.ActionWindup,
                out BattlePresentationRecipeSO attackWindup),
            "Basic attack must own a windup presentation override");
        AssertTrue(
            presentation.RecipeSet.TryResolveSkillOverride(
                "basic_attack",
                BattlePresentationMoment.ImpactResolved,
                out BattlePresentationRecipeSO attackImpact),
            "Basic attack must own an impact presentation override");
        AssertNoAttackCutInCue(
            attackWindup,
            "Basic attacks must use the same world-actor windup for both sides");
        AssertNoAttackCutInCue(
            attackImpact,
            "Basic attacks must use the same world-actor impact for both sides");

        DRPGSkillSO basicAttack = Load<DRPGSkillSO>(BasicAttackPath);
        DRPGSkillSO debugBreak = Load<DRPGSkillSO>(DebugBreakPath);
        AssertEqual("basic_attack", basicAttack.PresentationKey,
            "Basic attack presentation key");
        AssertEqual(2, basicAttack.EnemyWarningSteps,
            "Ordinary enemy attacks must expose the 2/1/0 warning lifecycle");
        AssertEqual(
            DRPGEnemyTargetDisclosureMode.Hidden,
            basicAttack.EnemyTargetDisclosureMode,
            "Ordinary basic attacks must keep their selected target private");
        AssertEqual("debug_break_strike", debugBreak.PresentationKey,
            "Debug break presentation key");
        VerifyBasicAttackRecipesResolve(
            presentation.RecipeSet,
            basicAttack.PresentationKey);
        VerifyDedicatedAttackPortrait(ProtagonistProfilePath);
        VerifyDedicatedAttackPortrait(VanguardProfilePath);

        GameObject prefab = Load<GameObject>(PrefabPath);
        AssertComponent<DRPGBattlePresentationRoot>(prefab);
        DRPGBattlePresentationDirector director =
            AssertComponent<DRPGBattlePresentationDirector>(prefab);
        AssertComponent<DRPGBattleActionTransactionCoordinator>(prefab);
        AssertComponent<DRPGBattleUGUISurface>(prefab);
        AssertComponent<DRPGBattleInteractionController>(prefab);
        AssertComponent<DRPGBattlePresentationEventBridge>(prefab);
        AssertNoMissingScripts(prefab);

        var serializedDirector = new SerializedObject(director);
        SerializedProperty executors = serializedDirector.FindProperty(
            "cueExecutorBehaviours");
        AssertEqual(8, executors.arraySize,
            "The test prefab must wire eight supported cue executors");
        var kinds = new HashSet<BattlePresentationCueKind>();
        for (int i = 0; i < executors.arraySize; i++)
        {
            MonoBehaviour behaviour = executors.GetArrayElementAtIndex(i)
                .objectReferenceValue as MonoBehaviour;
            AssertNotNull(behaviour, "Cue executor entry " + i);
            AssertTrue(behaviour is IBattlePresentationCueExecutor,
                behaviour.name + " must implement IBattlePresentationCueExecutor");
            var executor = (IBattlePresentationCueExecutor)behaviour;
            AssertTrue(kinds.Add(executor.Kind),
                "Cue executor kind must be unique: " + executor.Kind);
        }

        AssertTrue(kinds.Contains(BattlePresentationCueKind.TimelineTransition),
            "Timeline executor is missing");
        AssertTrue(kinds.Contains(BattlePresentationCueKind.TargetFeedback),
            "Target-feedback executor is missing");
        AssertTrue(kinds.Contains(BattlePresentationCueKind.ActorMotion),
            "Actor-motion executor is missing");
        AssertTrue(kinds.Contains(BattlePresentationCueKind.ImpactFeedback),
            "Impact executor is missing");
        AssertTrue(kinds.Contains(BattlePresentationCueKind.DamageBrokenCutIn),
            "Break cut-in executor is missing");
        AssertTrue(kinds.Contains(BattlePresentationCueKind.TurnQuote),
            "Quote executor is missing");
        AssertTrue(kinds.Contains(BattlePresentationCueKind.Audio),
            "Audio executor is missing");
        AssertTrue(kinds.Contains(BattlePresentationCueKind.AttackCutIn),
            "Attack cut-in executor is missing");

        BattleAttackCutInPresenter attackCutIn =
            prefab.GetComponentInChildren<BattleAttackCutInPresenter>(true);
        AssertNotNull(attackCutIn,
            "The Sprite attack cut-in presenter is missing");
        SerializedObject serializedCutIn = new SerializedObject(attackCutIn);
        AssertNotNull(
            serializedCutIn.FindProperty("visibleRoot").objectReferenceValue,
            "The attack cut-in must own an isolated visible root");
        AssertNotNull(
            serializedCutIn.FindProperty("portrait").objectReferenceValue,
            "The attack cut-in must expose a replaceable actor Sprite layer");
        AssertNotNull(
            serializedCutIn.FindProperty("panel").objectReferenceValue,
            "The attack cut-in must expose a replaceable background panel layer");
        AssertTrue(
            serializedCutIn.FindProperty("openSeconds").floatValue <= 0.18f,
            "The attack cut-in must open in 0.18 seconds or less");

        Image dimmer = serializedCutIn.FindProperty("dimmer")
            .objectReferenceValue as Image;
        RectTransform cutInPanel = serializedCutIn.FindProperty("panel")
            .objectReferenceValue as RectTransform;
        AssertNotNull(dimmer, "The attack cut-in dimmer Image");
        AssertNotNull(cutInPanel, "The attack cut-in panel RectTransform");
        AssertTrue(dimmer.color.a <= 0.52f,
            "The cut-in dimmer must preserve battlefield readability");
        AssertTrue(cutInPanel.sizeDelta.x <= 1120f,
            "The cut-in panel must not cover the enemy presentation zone");

        VerifyInitialActorBinding(prefab);

        return "PASS DRPG presentation assets and prefab composition";
    }

    private static void AssertNoAttackCutInCue(
        BattlePresentationRecipeSO recipe,
        string message)
    {
        AssertNotNull(recipe, message + " (recipe missing)");
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            BattlePresentationBeat beat = recipe.Beats[beatIndex];
            for (int cueIndex = 0; cueIndex < beat.Cues.Count; cueIndex++)
            {
                if (beat.Cues[cueIndex].Cue is BattleAttackCutInCueSO)
                {
                    throw new Exception(
                        message + ". Legacy player-only AttackCutIn cue found.");
                }
            }
        }
    }

    private static void VerifyBasicAttackRecipesResolve(
        BattlePresentationRecipeSetSO recipeSet,
        string presentationKey)
    {
        var actor = new BattleParticipantPresentationSnapshot(
            1,
            "Player",
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
            "Enemy",
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            100f,
            100f,
            DRPGLowHpPresentationState.Normal,
            null,
            null,
            null);
        var hit = new BattleHitPresentationSnapshot(
            1,
            2,
            "basic_attack",
            20f,
            100f,
            80f,
            false,
            false,
            false,
            DRPGLowHpPresentationState.Normal,
            DRPGBattleRosterRole.Enemy);
        var resolver = new BattlePresentationRecipeResolver();

        ResolvedBattlePresentationRecipe windup = resolver.Resolve(
            new BattlePresentationRequest(
                1,
                BattlePresentationMoment.ActionWindup,
                new BattlePresentationScope(1, 1, 1),
                actor,
                new[] { target },
                new[] { 1, 2 },
                presentationKey,
                EAnimationLogicState.Skill_1,
                Array.Empty<BattleHitPresentationSnapshot>(),
                BattlePresentationFacts.Empty),
            recipeSet);
        AssertResolvedCueCount(
            windup,
            BattlePresentationCueKind.AttackCutIn,
            0,
            "Resolved basic-attack windup must not add a player-only cut-in");

        ResolvedBattlePresentationRecipe impact = resolver.Resolve(
            new BattlePresentationRequest(
                2,
                BattlePresentationMoment.ImpactResolved,
                new BattlePresentationScope(1, 1, 1),
                actor,
                new[] { target },
                new[] { 1, 2 },
                presentationKey,
                EAnimationLogicState.Skill_1,
                new[] { hit },
                BattlePresentationFacts.Empty),
            recipeSet);
        AssertResolvedCueCount(
            impact,
            BattlePresentationCueKind.AttackCutIn,
            0,
            "Resolved basic-attack impact must not add a player-only cut-in");
        AssertResolvedCue(
            impact,
            BattlePresentationCueKind.ImpactFeedback,
            "Resolved basic-attack impact must preserve damage-number feedback");
        AssertResolvedCueCount(
            impact,
            BattlePresentationCueKind.ImpactFeedback,
            1,
            "A basic attack must create exactly one damage-number feedback cue");
        AssertEqual(
            "impact_basic_attack",
            impact.SourceRecipeIds[0],
            "Basic attack impact must replace the fallback recipe so all impact cues start together");
        AssertResolvedCueCount(
            impact,
            BattlePresentationCueKind.Audio,
            1,
            "Basic attack impact must trigger exactly one hit SFX cue");
        AssertFirstBeatContains(
            impact,
            BattlePresentationCueKind.ImpactFeedback,
            BattlePresentationCueKind.Audio);
    }

    private static void VerifyDedicatedAttackPortrait(string profilePath)
    {
        DRPGCombatantProfileSO profile = Load<DRPGCombatantProfileSO>(profilePath);
        AssertNotNull(profile.AttackCutInPortrait,
            profile.DisplayName + " must own a dedicated attack cut-in portrait");
        AssertTrue(!ReferenceEquals(profile.AttackCutInPortrait, profile.FullPortrait),
            profile.DisplayName + " attack cut-in must not reuse the idle full portrait");
    }

    private static void AssertFirstBeatContains(
        ResolvedBattlePresentationRecipe recipe,
        params BattlePresentationCueKind[] expectedKinds)
    {
        AssertTrue(recipe.Beats.Count > 0,
            "Resolved recipe must contain an impact beat");
        IReadOnlyList<ResolvedBattlePresentationCue> cues = recipe.Beats[0].Cues;
        for (int expectedIndex = 0; expectedIndex < expectedKinds.Length; expectedIndex++)
        {
            bool found = false;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                found |= cues[cueIndex].Cue.Kind == expectedKinds[expectedIndex];
            }
            AssertTrue(found,
                "First impact beat is missing cue=" + expectedKinds[expectedIndex]);
        }
    }

    private static void AssertResolvedCue(
        ResolvedBattlePresentationRecipe recipe,
        BattlePresentationCueKind expectedKind,
        string message)
    {
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            IReadOnlyList<ResolvedBattlePresentationCue> cues =
                recipe.Beats[beatIndex].Cues;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                if (cues[cueIndex].Cue.Kind == expectedKind)
                    return;
            }
        }

        throw new Exception(message + ". Missing cue=" + expectedKind);
    }

    private static void AssertResolvedCueCount(
        ResolvedBattlePresentationRecipe recipe,
        BattlePresentationCueKind expectedKind,
        int expectedCount,
        string message)
    {
        int actualCount = 0;
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            IReadOnlyList<ResolvedBattlePresentationCue> cues =
                recipe.Beats[beatIndex].Cues;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                if (cues[cueIndex].Cue.Kind == expectedKind)
                    actualCount++;
            }
        }

        AssertEqual(expectedCount, actualCount, message);
    }

    private static void VerifyInitialActorBinding(GameObject prefab)
    {
        DRPGPartySetupSO party = Load<DRPGPartySetupSO>(PartyPath);
        DRPGEncounterDefinitionSO encounter =
            Load<DRPGEncounterDefinitionSO>(EncounterPath);
        DRPGBattleSessionCreationResult roster =
            DRPGBattleSessionFactory.CreateRoster(party, encounter);
        AssertTrue(roster.IsSuccess, roster.ErrorMessage);

        var session = DRPGBattleHeadlessSessionFactory.Create(roster.Participants);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            DRPGBattlePresentationRoot root =
                instance.GetComponent<DRPGBattlePresentationRoot>();
            root.Show(
                session,
                new DRPGBattleStartRequest(party, encounter, default, 42));
            session.Start();

            DRPGBattlePresentationEventBridge bridge =
                instance.GetComponent<DRPGBattlePresentationEventBridge>();
            bridge.FlushQueuedSignals();

            BattleActorPresenter actor =
                instance.GetComponentInChildren<BattleActorPresenter>(true);
            bool persistentlySuppressActorPanel = new SerializedObject(actor)
                .FindProperty("suppressCurrentActorPanel")
                .boolValue;
            AssertEqual(session.CurrentActor.RuntimeId, actor.BoundRuntimeId,
                "Initial actor presenter runtime id");
            Image portrait = new SerializedObject(actor)
                .FindProperty("portrait").objectReferenceValue as Image;
            AssertNotNull(portrait, "Initial actor portrait Image");
            AssertTrue(
                ReferenceEquals(
                    session.CurrentActor.Profile.FullPortrait,
                    portrait.sprite),
                "Initial actor portrait must match the current actor snapshot");

            BattleAttackCutInPresenter cutIn =
                instance.GetComponentInChildren<BattleAttackCutInPresenter>(true);
            Image cutInPortrait = new SerializedObject(cutIn)
                .FindProperty("portrait").objectReferenceValue as Image;
            cutIn.Play(
                BattleAttackCutInStage.Open,
                BattleParticipantPresentationSnapshot.From(session.CurrentActor),
                0.01f);
            AssertTrue(actor.IsPresentationSuppressed,
                "Opening an attack cut-in must hide the normal right-side actor panel");
            AssertTrue(ReferenceEquals(
                    session.CurrentActor.Profile.AttackCutInPortrait,
                    cutInPortrait.sprite),
                "The cut-in must bind the actor's dedicated attack portrait");
            cutIn.Cancel(BattlePresentationCancelReason.ScopeEnded);
            AssertTrue(
                actor.IsPresentationSuppressed == persistentlySuppressActorPanel,
                persistentlySuppressActorPanel
                    ? "Closing the cut-in must preserve the DD1 world-stage " +
                      "suppression of the legacy actor panel"
                    : "Closing or cancelling the cut-in must restore the actor panel");
        }
        finally
        {
            if (session.IsActive)
                session.Abort();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        AssertNotNull(asset, "Missing asset: " + path);
        return asset;
    }

    private static T AssertComponent<T>(GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();
        AssertNotNull(component, "Missing component: " + typeof(T).Name);
        return component;
    }

    private static void AssertNoMissingScripts(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject current = transforms[i].gameObject;
            AssertEqual(
                0,
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current),
                "Missing MonoBehaviour script on " + current.name);
        }
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }
}
#endif
