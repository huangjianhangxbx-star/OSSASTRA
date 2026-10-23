#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGBattlePerspectivePresentationAssetProbe
{
    public const string PresentationPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGBattlePresentation_Test.asset";
    public const string StageProfilePath =
        "Assets/GameResources/DRPG/Config/Test/Presentation/Perspective/BattleStageProfile_Default.asset";
    public const string EntryRecipePath =
        "Assets/GameResources/DRPG/Config/Test/Presentation/Perspective/BattleEntryPresentationRecipe_Default.asset";
    public const string DefaultSemanticRecipePath =
        "Assets/GameResources/DRPG/Config/Test/Presentation/Perspective/BattleSkillPresentation_Default.asset";
    public const string BasicAttackSemanticRecipePath =
        "Assets/GameResources/DRPG/Config/Test/Presentation/Perspective/BattleSkillPresentation_BasicAttack.asset";
    public const string SemanticCatalogPath =
        "Assets/GameResources/DRPG/Config/Test/Presentation/Perspective/BattlePresentationRecipeCatalog_Default.asset";
    public const string PresentationPrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    public const string WorldActorPrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattleWorldActor.prefab";

    public static string Run()
    {
        DRPGBattlePresentationSO presentation = Load<DRPGBattlePresentationSO>(
            PresentationPath);
        BattleStageProfileSO stage = Load<BattleStageProfileSO>(StageProfilePath);
        BattleEntryPresentationRecipeSO entry =
            Load<BattleEntryPresentationRecipeSO>(EntryRecipePath);
        BattleSkillPresentationRecipeSO defaultSemantic =
            Load<BattleSkillPresentationRecipeSO>(DefaultSemanticRecipePath);
        BattleSkillPresentationRecipeSO basicAttackSemantic =
            Load<BattleSkillPresentationRecipeSO>(BasicAttackSemanticRecipePath);
        BattlePresentationRecipeCatalogSO semanticCatalog =
            Load<BattlePresentationRecipeCatalogSO>(SemanticCatalogPath);
        GameObject actorPrefab = Load<GameObject>(WorldActorPrefabPath);
        GameObject presentationPrefab = Load<GameObject>(PresentationPrefabPath);
        GameObject worldPrefab = Load<GameObject>(
            DRPGBattleWorldUiSeparationProbe.WorldPrefabPath);

        AssertTrue(stage.TryValidate(out string stageError),
            "Default perspective stage profile is invalid: " + stageError);
        AssertAnchor(stage, BattleStageAnchorIds.PlayerFront);
        AssertAnchor(stage, BattleStageAnchorIds.EnemyFront);
        AssertAnchor(stage, BattleStageAnchorIds.PresentationCenter);
        AssertNotNull(entry.SpawnShot, "Entry spawn shot");
        AssertNotNull(entry.ReadyShot, "Entry ready shot");

        var serializedPresentation = new SerializedObject(presentation);
        AssertSame(entry,
            serializedPresentation.FindProperty("fixedEntryRecipe")
                .objectReferenceValue,
            "Presentation profile fixed entry recipe");
        AssertSame(stage,
            serializedPresentation.FindProperty("defaultStageProfile")
                .objectReferenceValue,
            "Presentation profile default perspective stage");
        AssertSame(defaultSemantic,
            serializedPresentation.FindProperty("defaultSkillRecipe")
                .objectReferenceValue,
            "Presentation profile default semantic recipe");
        AssertSame(semanticCatalog,
            serializedPresentation.FindProperty("semanticRecipeCatalog")
                .objectReferenceValue,
            "Presentation profile semantic recipe catalog");

        var recipeSet = serializedPresentation.FindProperty("recipeSet")
            .objectReferenceValue as BattlePresentationRecipeSetSO;
        AssertNotNull(recipeSet, "Presentation recipe set");
        var serializedRecipeSet = new SerializedObject(recipeSet);
        AssertSame(semanticCatalog,
            serializedRecipeSet.FindProperty("semanticRecipeCatalog")
                .objectReferenceValue,
            "Recipe set semantic catalog fallback");

        IBattleSemanticRecipeResolver semanticResolver =
            semanticCatalog.CreateResolver();
        AssertTrue(semanticResolver.TryResolve(
                "basic_attack",
                out ResolvedBattleSkillPresentation basicResolved,
                out string basicError),
            "Basic attack semantic recipe must resolve: " + basicError);
        AssertTrue(basicResolved.Camera.Enabled,
            "Basic attack semantic camera module must be enabled");
        AssertEqual("conflict.default",
            basicResolved.Camera.Definition.CameraRigKey,
            "Basic attack must use the default conflict shot");
        AssertEqual(BattleCameraFramingMode.FrameActorAndTargets,
            basicResolved.Camera.Definition.FramingMode,
            "DD1-style conflict shots must frame the attacker and target together");
        AssertTrue(basicResolved.Camera.Definition.FieldOfView <= 34f,
            "The default conflict shot must create a visible close-up");
        AssertTrue(semanticResolver.TryResolve(
                "unconfigured.skill",
                out ResolvedBattleSkillPresentation fallbackResolved,
                out string fallbackError),
            "Unconfigured skills must resolve the default conflict recipe: " +
            fallbackError);
        AssertEqual(defaultSemantic.PresentationKey,
            fallbackResolved.PresentationKey,
            "The catalog fallback must remain traceable to the default recipe");

        BattleWorldActorView actorView =
            AssertComponent<BattleWorldActorView>(actorPrefab);
        AssertNotNull(actorView.VisualRoot, "World actor visual root");
        AssertNotNull(actorView.VfxAnchor, "World actor VFX anchor");
        AssertComponentInChildren<BattleWorldActorSpriteDriver>(actorPrefab);
        SpriteRenderer actorRenderer =
            AssertComponentInChildren<SpriteRenderer>(actorPrefab);
        AssertNotNull(actorRenderer.sharedMaterial, "World actor material");
        AssertTrue(
            actorRenderer.sharedMaterial.shader != null &&
            actorRenderer.sharedMaterial.shader.name.IndexOf(
                "Lit",
                StringComparison.OrdinalIgnoreCase) < 0,
            "World actor material must use an unlit sprite shader");

        Camera battleCamera = AssertComponentInChildren<Camera>(worldPrefab);
        AssertTrue(!battleCamera.orthographic,
            "Perspective presentation camera must not be orthographic");
        int worldLayer = LayerMask.NameToLayer("DRPGBattleWorld");
        AssertTrue(worldLayer >= 0,
            "Perspective presentation requires the DRPGBattleWorld layer");
        AssertTrue((battleCamera.cullingMask & (1 << worldLayer)) != 0,
            "Perspective presentation camera must render the battle-world layer");
        AssertTrue((battleCamera.cullingMask &
            (1 << LayerMask.NameToLayer("UI"))) == 0,
            "Perspective presentation camera must not render the UGUI layer");
        AssertComponentInChildren<BattleCameraPresenter>(worldPrefab);
        AssertComponentInChildren<BattleStageRoot>(worldPrefab);
        AssertComponentInChildren<BattleStagePresenter>(worldPrefab);
        AssertComponentInChildren<BattleWorldActorPool>(worldPrefab);
        BattleWorldRosterPresenter roster =
            AssertComponentInChildren<BattleWorldRosterPresenter>(worldPrefab);
        var serializedRoster = new SerializedObject(roster);
        AssertTrue(serializedRoster.FindProperty("availableWidth").floatValue >= 4f,
            "The active lineup must not compress mixed-width actors into overlap");
        AssertTrue(serializedRoster.FindProperty("actorSpacing").floatValue >= 0.65f,
            "Allies and enemies need readable breathing room inside each row");
        AssertTrue(serializedRoster.FindProperty("rowDepthStep").floatValue <= 0.25f,
            "DD1-style rows must use shallow perspective depth");
        AssertTrue(serializedRoster.FindProperty("rowHorizontalStep").floatValue >= 1f,
            "Each logical row must advance horizontally into a six-slot lineup");
        BattleWorldTargetingPresenter worldTargeting =
            AssertComponentInChildren<BattleWorldTargetingPresenter>(presentationPrefab);
        AssertComponentInChildren<BattleEntryPresentationCoordinator>(
            presentationPrefab);
        AssertComponentInChildren<BattlePresentationResourceCatalog>(
            worldPrefab);
        AssertComponentInChildren<BattleHudPresenter>(presentationPrefab);
        AssertComponentInChildren<BattleBackgroundEffectPresenter>(
            presentationPrefab);

        DRPGBattleUGUISurface surface =
            AssertComponent<DRPGBattleUGUISurface>(presentationPrefab);
        var serializedSurface = new SerializedObject(surface);
        AssertSame(null,
            serializedSurface.FindProperty("worldRosterPresenter")
                .objectReferenceValue,
            "UGUI surface must receive its world roster at runtime");
        AssertSame(worldTargeting,
            serializedSurface.FindProperty("worldTargetingPresenter")
                .objectReferenceValue,
            "UGUI surface world targeting presenter");
        BattleImpactFeedbackPresenter impactFeedback =
            AssertComponentInChildren<BattleImpactFeedbackPresenter>(
                presentationPrefab);
        var serializedImpact = new SerializedObject(impactFeedback);
        AssertSame(worldTargeting,
            serializedImpact.FindProperty("worldTargeting")
                .objectReferenceValue,
            "Impact feedback world targeting presenter");
        BattleWorldRuntimeHost worldHost =
            AssertComponent<BattleWorldRuntimeHost>(presentationPrefab);
        var serializedHost = new SerializedObject(worldHost);
        AssertSame(worldPrefab,
            serializedHost.FindProperty("worldPrefab").objectReferenceValue,
            "Battle-world runtime host prefab");

        Transform visualRoot = presentationPrefab.transform.Find("VisualRoot");
        AssertNotNull(visualRoot, "Presentation VisualRoot");
        AssertInactive(visualRoot.Find("BattleBackground"),
            "Legacy UGUI battle background must be disabled");
        AssertInactive(visualRoot.Find("EnemyBattlefield"),
            "Legacy enemy cards must be disabled in favor of world actors");
        AssertInactive(visualRoot.Find("CurrentActorPanel"),
            "DD1-style staging must not show the old right-side actor portrait");

        BattleActorPresenter actorPresenter =
            AssertComponentInChildren<BattleActorPresenter>(presentationPrefab);
        var serializedActorPresenter = new SerializedObject(actorPresenter);
        AssertTrue(
            serializedActorPresenter.FindProperty("suppressCurrentActorPanel")
                .boolValue,
            "DD1-style staging must keep the legacy current-actor portrait " +
            "suppressed when runtime binding refreshes the presenter");

        RectTransform commandRoot = presentationPrefab.transform.Find(
            "BattleCommandSystem/CommandRoot") as RectTransform;
        AssertNotNull(commandRoot, "DD1 command root");
        AssertTrue(commandRoot.anchorMin.x > 0.9f &&
            commandRoot.anchorMin.y < 0.1f &&
            commandRoot.anchorMax.x > 0.9f &&
            commandRoot.anchorMax.y < 0.1f,
            "DD1 command root must live in the lower-right HUD band");
        AssertTrue(commandRoot.sizeDelta.y <= 260f,
            "DD1 command root must be a compact bottom command deck");

        return "PASS DRPG perspective presentation assets and prefab wiring";
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        AssertNotNull(asset, path);
        return asset;
    }

    private static T AssertComponent<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        AssertNotNull(component, typeof(T).Name + " on " + root.name);
        return component;
    }

    private static T AssertComponentInChildren<T>(GameObject root)
        where T : Component
    {
        T component = root.GetComponentInChildren<T>(true);
        AssertNotNull(component, typeof(T).Name + " under " + root.name);
        return component;
    }

    private static void AssertAnchor(BattleStageProfileSO stage, string id)
    {
        AssertTrue(stage.TryGetAnchor(id, out _), "Missing stage anchor: " + id);
    }

    private static void AssertInactive(Transform target, string message)
    {
        AssertNotNull(target, message);
        AssertTrue(!target.gameObject.activeSelf, message);
    }

    private static void AssertSame(
        UnityEngine.Object expected,
        UnityEngine.Object actual,
        string message)
    {
        if (expected != actual)
            throw new Exception(message + " is not wired to the expected asset.");
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new Exception(message + " must not be null.");
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
                message + $". Expected={expected}, Actual={actual}");
        }
    }
}
#endif
