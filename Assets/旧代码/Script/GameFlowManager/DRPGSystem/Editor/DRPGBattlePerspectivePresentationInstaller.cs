#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattlePerspectivePresentationInstaller
{
    private const string MenuPath =
        "Tools/ANKALUTE/DRPG/Install Perspective Battle Presentation";
    private const string PresentationPath =
        DRPGBattlePerspectivePresentationAssetProbe.PresentationPath;
    private const string StageProfilePath =
        DRPGBattlePerspectivePresentationAssetProbe.StageProfilePath;
    private const string EntryRecipePath =
        DRPGBattlePerspectivePresentationAssetProbe.EntryRecipePath;
    private const string DefaultSemanticRecipePath =
        DRPGBattlePerspectivePresentationAssetProbe.DefaultSemanticRecipePath;
    private const string BasicAttackSemanticRecipePath =
        DRPGBattlePerspectivePresentationAssetProbe.BasicAttackSemanticRecipePath;
    private const string SemanticCatalogPath =
        DRPGBattlePerspectivePresentationAssetProbe.SemanticCatalogPath;
    private const string PresentationPrefabPath =
        DRPGBattlePerspectivePresentationAssetProbe.PresentationPrefabPath;
    private const string WorldActorPrefabPath =
        DRPGBattlePerspectivePresentationAssetProbe.WorldActorPrefabPath;
    private const string WorldPrefabPath =
        DRPGBattleWorldUiSeparationProbe.WorldPrefabPath;
    private const string StagePrefabPath =
        DRPGBattleWorldUiSeparationProbe.StagePrefabPath;
    private const string WorldLayerName = "DRPGBattleWorld";
    private const string RenderTexturePath =
        "Assets/GameResources/DRPG/Config/Test/Presentation/Perspective/RT_DRPGBattlePerspective.renderTexture";
    private const string WorldActorMaterialPath =
        "Assets/GameResources/DRPG/Config/Test/Presentation/Perspective/MAT_DRPGBattleWorldActor_Unlit.mat";

    [MenuItem(MenuPath)]
    public static void InstallFromMenu()
    {
        Debug.Log(Install());
    }

    public static string Install()
    {
        EnsureAssetFolder("Assets/GameResources/DRPG/Config/Test/Presentation/Perspective");
        EnsureAssetFolder("Assets/GameResources/DRPG/Prefabs");
        int worldLayer = EnsureLayer(WorldLayerName);

        BattleCameraShotDefinition spawnShot = new BattleCameraShotDefinition(
            "entry.spawn",
            new Vector3(0.35f, 0.58f, -10.9f),
            Vector3.zero,
            37f,
            0f);
        BattleCameraShotDefinition readyShot = new BattleCameraShotDefinition(
            "entry.ready",
            new Vector3(0f, 0.48f, -12.45f),
            Vector3.zero,
            40f,
            0.22f);
        BattleCameraShotDefinition conflictShot = new BattleCameraShotDefinition(
            "conflict.default",
            new Vector3(0f, 0.48f, -11.9f),
            Vector3.zero,
            32f,
            0.16f);

        BattleStageProfileSO stageProfile = LoadOrCreate<BattleStageProfileSO>(
            StageProfilePath);
        stageProfile.Configure(
            "default-perspective",
            null,
            CreateDefaultAnchors(),
            new BattleCameraSequenceDefinition(
                readyShot.ShotId,
                new[] { spawnShot, readyShot, conflictShot }));
        EditorUtility.SetDirty(stageProfile);

        BattleEntryPresentationRecipeSO entryRecipe =
            LoadOrCreate<BattleEntryPresentationRecipeSO>(EntryRecipePath);
        entryRecipe.Configure(
            spawnShot,
            readyShot,
            actorSpawnSeconds: 0.62f,
            holdSeconds: 0.12f,
            revealSeconds: 0.2f,
            initialScale: 0.72f);
        EditorUtility.SetDirty(entryRecipe);

        BattleSkillPresentationRecipeSO defaultSemantic =
            LoadOrCreate<BattleSkillPresentationRecipeSO>(
                DefaultSemanticRecipePath);
        ConfigureDefaultSemanticRecipe(defaultSemantic);
        BattleSkillPresentationRecipeSO basicAttackSemantic =
            LoadOrCreate<BattleSkillPresentationRecipeSO>(
                BasicAttackSemanticRecipePath);
        ConfigureBasicAttackSemanticRecipe(
            basicAttackSemantic,
            defaultSemantic);
        BattlePresentationRecipeCatalogSO semanticCatalog =
            LoadOrCreate<BattlePresentationRecipeCatalogSO>(
                SemanticCatalogPath);
        ConfigureSemanticCatalog(
            semanticCatalog,
            defaultSemantic,
            basicAttackSemantic);

        Material actorMaterial = LoadOrCreateWorldActorMaterial();
        BattleWorldActorView actorPrefab = CreateWorldActorPrefab(
            actorMaterial,
            worldLayer);
        GameObject stagePrefab = CreateDefaultStagePrefab(
            actorMaterial,
            worldLayer);
        stageProfile.Configure(
            "default-perspective",
            stagePrefab,
            CreateDefaultAnchors(),
            new BattleCameraSequenceDefinition(
                readyShot.ShotId,
                new[] { spawnShot, readyShot, conflictShot }));
        EditorUtility.SetDirty(stageProfile);
        GameObject worldPrefab = CreateBattleWorldPrefab(
            actorPrefab,
            stageProfile,
            new[] { spawnShot, readyShot, conflictShot },
            worldLayer);
        RenderTexture renderTexture = LoadOrCreateRenderTexture();
        WirePresentationPrefab(
            worldPrefab,
            renderTexture,
            worldLayer);
        WirePresentationProfile(
            stageProfile,
            entryRecipe,
            defaultSemantic,
            semanticCatalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "PASS installed ANKALUTE perspective battle presentation assets";
    }

    private static IReadOnlyList<BattleStageAnchorDefinition> CreateDefaultAnchors()
    {
        return new[]
        {
            new BattleStageAnchorDefinition(
                BattleStageAnchorIds.PlayerFront,
                new Vector3(-2.35f, -1.7f, 0f),
                Vector3.zero),
            new BattleStageAnchorDefinition(
                BattleStageAnchorIds.PlayerMiddle,
                new Vector3(-2.35f, -1.7f, 0.12f),
                Vector3.zero,
                Vector3.one * 0.98f),
            new BattleStageAnchorDefinition(
                BattleStageAnchorIds.PlayerRear,
                new Vector3(-2.35f, -1.7f, 0.24f),
                Vector3.zero,
                Vector3.one * 0.96f),
            new BattleStageAnchorDefinition(
                BattleStageAnchorIds.EnemyFront,
                new Vector3(1.25f, -1.7f, 0f),
                Vector3.zero),
            new BattleStageAnchorDefinition(
                BattleStageAnchorIds.EnemyMiddle,
                new Vector3(1.25f, -1.7f, 0.12f),
                Vector3.zero,
                Vector3.one * 0.98f),
            new BattleStageAnchorDefinition(
                BattleStageAnchorIds.EnemyRear,
                new Vector3(1.25f, -1.7f, 0.24f),
                Vector3.zero,
                Vector3.one * 0.96f),
            new BattleStageAnchorDefinition(
                BattleStageAnchorIds.PresentationCenter,
                new Vector3(0f, -0.9f, 0f),
                Vector3.zero)
        };
    }

    private static BattleWorldActorView CreateWorldActorPrefab(
        Material material,
        int worldLayer)
    {
        var root = new GameObject(
            "PF_DRPGBattleWorldActor",
            typeof(BattleWorldActorView));
        try
        {
            SetLayerRecursively(root, worldLayer);
            BattleWorldActorView view = root.GetComponent<BattleWorldActorView>();
            Transform visual = CreateChild("VisualRoot", root.transform);
            SpriteRenderer renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 300;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            BattleWorldActorSpriteDriver driver =
                visual.gameObject.AddComponent<BattleWorldActorSpriteDriver>();
            driver.Configure(renderer, 2.95f);
            SetFloat(driver, "anticipationDistance", 0.12f);
            SetFloat(driver, "lungeDistance", 0.92f);
            SetFloat(driver, "anticipationSeconds", 0.05f);
            SetFloat(driver, "lungeSeconds", 0.1f);
            view.EnsureAnchors();
            view.ConfigureVisualDriver(driver);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root,
                WorldActorPrefabPath);
            if (saved == null)
                throw new InvalidOperationException(
                    "Could not save " + WorldActorPrefabPath);
            return saved.GetComponent<BattleWorldActorView>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateDefaultStagePrefab(
        Material material,
        int worldLayer)
    {
        var root = new GameObject(
            "PF_DRPGBattleStage_Default",
            typeof(BattleWorldBackgroundPresenter));
        try
        {
            SetLayerRecursively(root, worldLayer);
            Transform visual = CreateChild("Background", root.transform);
            SpriteRenderer renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -1000;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            root.GetComponent<BattleWorldBackgroundPresenter>().Configure(
                renderer,
                28f,
                1.08f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root,
                StagePrefabPath);
            if (saved == null)
                throw new InvalidOperationException(
                    "Could not save " + StagePrefabPath);
            return saved;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateBattleWorldPrefab(
        BattleWorldActorView actorPrefab,
        BattleStageProfileSO stageProfile,
        IReadOnlyList<BattleCameraShotDefinition> cameraShots,
        int worldLayer)
    {
        var world = new GameObject("PF_DRPGBattleWorld");
        try
        {
            SetLayerRecursively(world, worldLayer);
            Transform cameraOrigin = CreateChild(
                "BattleCameraOrigin",
                world.transform);
            GameObject cameraObject = new GameObject(
                "PerspectiveBattleCamera",
                typeof(Camera),
                typeof(BattleCameraPresenter));
            cameraObject.transform.SetParent(world.transform, false);
            cameraObject.layer = worldLayer;
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << worldLayer;
            camera.depth = 25f;
            camera.targetTexture = null;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.enabled = false;
            cameraObject.GetComponent<BattleCameraPresenter>().Configure(
                camera,
                cameraOrigin);

            GameObject stageObject = new GameObject(
                "PerspectiveStageRuntime",
                typeof(BattleStageRoot),
                typeof(BattleStagePresenter));
            stageObject.transform.SetParent(world.transform, false);
            stageObject.layer = worldLayer;
            Transform stageInstances = CreateChild(
                "StageInstances",
                stageObject.transform);
            BattleStageRoot stageRoot = stageObject.GetComponent<BattleStageRoot>();
            stageRoot.Configure(stageInstances);
            stageObject.GetComponent<BattleStagePresenter>().Configure(stageRoot);

            GameObject actorsObject = new GameObject(
                "WorldActors",
                typeof(BattleWorldActorPool),
                typeof(BattleWorldRosterPresenter));
            actorsObject.transform.SetParent(world.transform, false);
            actorsObject.layer = worldLayer;
            Transform actorPoolRoot = CreateChild("Pool", actorsObject.transform);
            Transform playerOrigin = CreateChild(
                "PlayerFrontOrigin",
                actorsObject.transform);
            playerOrigin.localPosition = new Vector3(-3.55f, -1.7f, 0f);
            Transform enemyOrigin = CreateChild(
                "EnemyFrontOrigin",
                actorsObject.transform);
            enemyOrigin.localPosition = new Vector3(2.35f, -1.7f, 0f);
            BattleWorldActorPool pool = actorsObject.GetComponent<BattleWorldActorPool>();
            pool.Configure(actorPrefab, actorPoolRoot);
            BattleWorldRosterPresenter roster =
                actorsObject.GetComponent<BattleWorldRosterPresenter>();
            roster.Configure(pool, playerOrigin, enemyOrigin);
            SetFloat(roster, "availableWidth", 5f);
            SetFloat(roster, "actorSpacing", 0.75f);
            SetFloat(roster, "rowDepthStep", 0.1f);
            SetFloat(roster, "rowHorizontalStep", 1.05f);
            SetFloat(roster, "middleRowScale", 0.97f);
            SetFloat(roster, "rearRowScale", 0.94f);
            SetFloat(roster, "transitionSeconds", 0.18f);

            BattlePresentationResourceCatalog resources =
                world.AddComponent<BattlePresentationResourceCatalog>();
            resources.Configure(new[] { stageProfile }, cameraShots);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                world,
                WorldPrefabPath);
            if (saved == null)
                throw new InvalidOperationException(
                    "Could not save " + WorldPrefabPath);
            return saved;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(world);
        }
    }

    private static RenderTexture LoadOrCreateRenderTexture()
    {
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
            RenderTexturePath);
        if (texture != null)
            return texture;

        texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
        {
            name = "RT_DRPGBattlePerspective",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        AssetDatabase.CreateAsset(texture, RenderTexturePath);
        return texture;
    }

    private static Material LoadOrCreateWorldActorMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            WorldActorMaterialPath);
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            throw new InvalidOperationException("No unlit Sprite shader is available.");

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "MAT_DRPGBattleWorldActor_Unlit"
            };
            AssetDatabase.CreateAsset(material, WorldActorMaterialPath);
        }
        else
        {
            material.shader = shader;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void WirePresentationPrefab(
        GameObject worldPrefab,
        RenderTexture renderTexture,
        int worldLayer)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PresentationPrefabPath);
        try
        {
            Transform existing = root.transform.Find("PerspectiveBattleWorld");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            Transform visualRoot = root.transform.Find("VisualRoot");
            if (visualRoot == null)
                throw new InvalidOperationException("Presentation prefab has no VisualRoot.");

            DisableLegacyVisual(visualRoot, "BattleBackground");
            DisableLegacyVisual(visualRoot, "EnemyBattlefield");
            DisableLegacyVisual(visualRoot, "CurrentActorPanel");
            ConfigureDD1HudLayout(root);

            RawImage output = CreateOrResetStageOutput(
                visualRoot,
                renderTexture);
            RectTransform proxyLayer = CreateOrResetHitProxyLayer(visualRoot);
            BattleWorldTargetingPresenter worldTargeting =
                GetOrAdd<BattleWorldTargetingPresenter>(root);
            worldTargeting.Configure(null, null, proxyLayer);

            BattleWorldRuntimeHost worldHost =
                GetOrAdd<BattleWorldRuntimeHost>(root);
            worldHost.Configure(
                worldPrefab,
                BattleWorldOutputMode.DirectCamera,
                output,
                renderTexture);

            BattleHudPresenter hudPresenter = GetOrAdd<BattleHudPresenter>(root);
            hudPresenter.Configure(CreateHudGroups(root, visualRoot));
            BattleBackgroundEffectPresenter backgroundEffects =
                GetOrAdd<BattleBackgroundEffectPresenter>(root);
            Transform dimmerTransform = visualRoot.Find("AtmosphereDimmer");
            CanvasGroup dimmer = dimmerTransform != null
                ? GetOrAdd<CanvasGroup>(dimmerTransform.gameObject)
                : null;
            SetReference(backgroundEffects, "dimOverlay", dimmer);

            BattleEntryPresentationCoordinator entry =
                GetOrAdd<BattleEntryPresentationCoordinator>(root);
            entry.Configure(GetOrAdd<CanvasGroup>(root), null, null);

            DRPGBattlePresentationRoot presentationRoot =
                root.GetComponent<DRPGBattlePresentationRoot>();
            DRPGBattleUGUISurface surface =
                root.GetComponent<DRPGBattleUGUISurface>();
            SetReference(
                presentationRoot,
                "battleWorldHost",
                worldHost);
            SetReference(surface, "worldRosterPresenter", null);
            SetReference(surface, "worldTargetingPresenter", worldTargeting);
            BattleImpactFeedbackPresenter impactFeedback =
                root.GetComponentInChildren<BattleImpactFeedbackPresenter>(true);
            if (impactFeedback != null)
                SetReference(impactFeedback, "worldTargeting", worldTargeting);

            output.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PresentationPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RawImage CreateOrResetStageOutput(
        Transform visualRoot,
        RenderTexture renderTexture)
    {
        Transform existing = visualRoot.Find("PerspectiveStageOutput");
        GameObject outputObject = existing != null
            ? existing.gameObject
            : new GameObject(
                "PerspectiveStageOutput",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
        outputObject.transform.SetParent(visualRoot, false);
        RectTransform rect = outputObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        RawImage output = outputObject.GetComponent<RawImage>();
        output.texture = renderTexture;
        output.color = Color.white;
        output.raycastTarget = false;
        outputObject.transform.SetSiblingIndex(1);
        return output;
    }

    private static RectTransform CreateOrResetHitProxyLayer(Transform visualRoot)
    {
        Transform existing = visualRoot.Find("WorldActorHitProxyLayer");
        GameObject layerObject = existing != null
            ? existing.gameObject
            : new GameObject("WorldActorHitProxyLayer", typeof(RectTransform));
        layerObject.transform.SetParent(visualRoot, false);
        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        layerObject.transform.SetSiblingIndex(2);
        return rect;
    }

    private static IReadOnlyList<BattleHudGroupView> CreateHudGroups(
        GameObject root,
        Transform visualRoot)
    {
        var groups = new List<BattleHudGroupView>();
        AddHudGroup(groups, visualRoot.Find("PartyHud"), BattleHudGroupKind.PartyStatus);
        AddHudGroup(groups, visualRoot.Find("ActionTimeline"), BattleHudGroupKind.TurnTimeline);
        AddHudGroup(groups, root.transform.Find("BattleCommandSystem"), BattleHudGroupKind.CommandPanel);
        AddHudGroup(groups, visualRoot.Find("TargetPrompt"), BattleHudGroupKind.TargetingPrompt);
        AddHudGroup(groups, visualRoot.Find("TurnQuote"), BattleHudGroupKind.PresentationFeedback);
        AddHudGroup(groups, root.transform.Find("BattleRecoverySystem"), BattleHudGroupKind.SpecialSystems);
        return groups;
    }

    private static void AddHudGroup(
        ICollection<BattleHudGroupView> result,
        Transform target,
        BattleHudGroupKind kind)
    {
        if (target == null)
            return;
        CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(target.gameObject);
        BattleHudGroupView group = GetOrAdd<BattleHudGroupView>(target.gameObject);
        group.Configure(kind, canvasGroup);
        result.Add(group);
    }

    private static void DisableLegacyVisual(Transform visualRoot, string name)
    {
        Transform target = visualRoot != null ? visualRoot.Find(name) : null;
        if (target != null)
            target.gameObject.SetActive(false);
    }

    private static void ConfigureDD1HudLayout(GameObject root)
    {
        if (root == null)
            return;

        BattleActorPresenter actorPresenter =
            root.GetComponentInChildren<BattleActorPresenter>(true);
        if (actorPresenter != null)
            SetBool(actorPresenter, "suppressCurrentActorPanel", true);

        Transform commandSystem = root.transform.Find("BattleCommandSystem");
        if (commandSystem == null)
            return;

        RectTransform commandRoot =
            commandSystem.Find("CommandRoot") as RectTransform;
        if (commandRoot != null)
        {
            ConfigureBottomRightPanel(
                commandRoot,
                new Vector2(900f, 230f));
            Image panelImage = commandRoot.GetComponent<Image>();
            if (panelImage != null)
                panelImage.color = new Color(0.018f, 0.020f, 0.030f, 0.94f);

            RectTransform title = commandRoot.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(1f, 1f);
                title.pivot = new Vector2(0f, 1f);
                title.anchoredPosition = new Vector2(22f, -14f);
                title.sizeDelta = new Vector2(-44f, 38f);
            }

            string[] buttonNames =
            {
                "BasicAttack", "Skill", "Item",
                "Weapon", "Defend", "Escape"
            };
            for (int i = 0; i < buttonNames.Length; i++)
            {
                RectTransform button =
                    commandRoot.Find(buttonNames[i]) as RectTransform;
                if (button == null)
                    continue;
                int column = i % 3;
                int row = i / 3;
                button.anchorMin = Vector2.zero;
                button.anchorMax = Vector2.zero;
                button.pivot = Vector2.zero;
                button.anchoredPosition = new Vector2(
                    22f + column * 286f,
                    24f + (1 - row) * 68f);
                button.sizeDelta = new Vector2(264f, 56f);
            }
        }

        ConfigureBottomRightPanel(
            commandSystem.Find("ItemRoot") as RectTransform,
            new Vector2(900f, 540f));
        ConfigureBottomRightPanel(
            commandSystem.Find("WeaponRoot") as RectTransform,
            new Vector2(900f, 540f));

        BattleSkillMenu skillMenu =
            root.GetComponentInChildren<BattleSkillMenu>(true);
        ConfigureBottomRightPanel(
            skillMenu != null ? skillMenu.transform as RectTransform : null,
            new Vector2(900f, 540f));
    }

    private static void ConfigureBottomRightPanel(
        RectTransform rect,
        Vector2 size)
    {
        if (rect == null)
            return;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-28f, 28f);
        rect.sizeDelta = size;
    }

    private static void WirePresentationProfile(
        BattleStageProfileSO stage,
        BattleEntryPresentationRecipeSO entry,
        BattleSkillPresentationRecipeSO defaultSemantic,
        BattlePresentationRecipeCatalogSO semanticCatalog)
    {
        DRPGBattlePresentationSO presentation =
            AssetDatabase.LoadAssetAtPath<DRPGBattlePresentationSO>(
                PresentationPath);
        if (presentation == null)
            throw new InvalidOperationException("Missing " + PresentationPath);
        SetReference(presentation, "fixedEntryRecipe", entry);
        SetReference(presentation, "defaultStageProfile", stage);
        SetReference(presentation, "defaultSkillRecipe", defaultSemantic);
        SetReference(presentation, "semanticRecipeCatalog", semanticCatalog);
        BattlePresentationRecipeSetSO recipeSet = presentation.RecipeSet;
        if (recipeSet != null)
        {
            SetReference(
                recipeSet,
                "semanticRecipeCatalog",
                semanticCatalog);
            EditorUtility.SetDirty(recipeSet);
        }
        EditorUtility.SetDirty(presentation);
    }

    private static void ConfigureDefaultSemanticRecipe(
        BattleSkillPresentationRecipeSO recipe)
    {
        var serialized = new SerializedObject(recipe);
        serialized.FindProperty("presentationKey").stringValue =
            "default.conflict";
        serialized.FindProperty("parent").objectReferenceValue = null;

        ConfigureModule(serialized, "hud", BattlePresentationOverrideMode.Override);
        serialized.FindProperty("hud.definition.visibility").enumValueIndex =
            (int)BattleHudVisibilityPolicy.HideAll;

        ConfigureModule(serialized, "stage", BattlePresentationOverrideMode.Override);
        serialized.FindProperty("stage.definition.policy").enumValueIndex =
            (int)BattleStagePresentationPolicy.KeepCurrent;

        ConfigureCameraModule(serialized, 32f, 0.16f);

        ConfigureModule(serialized, "attacker", BattlePresentationOverrideMode.Override);
        serialized.FindProperty("attacker.definition.visible").boolValue = true;
        serialized.FindProperty("attacker.definition.scale").floatValue = 1.09f;

        ConfigureModule(serialized, "target", BattlePresentationOverrideMode.Override);
        serialized.FindProperty("target.definition.focus").boolValue = true;
        serialized.FindProperty("target.definition.scale").floatValue = 1.08f;
        serialized.FindProperty("target.definition.retreatDistance").floatValue = 0.22f;
        serialized.FindProperty("target.definition.others").enumValueIndex =
            (int)BattleOtherTargetPresentationPolicy.Dim;

        ConfigureModule(serialized, "background", BattlePresentationOverrideMode.Override);
        serialized.FindProperty("background.definition.policy").enumValueIndex =
            (int)BattleBackgroundPresentationPolicy.Dim;
        serialized.FindProperty("background.definition.intensity").floatValue = 0.5f;

        ConfigureModule(serialized, "spine", BattlePresentationOverrideMode.Override);
        ConfigureModule(serialized, "feedback", BattlePresentationOverrideMode.Override);
        serialized.FindProperty("feedback.definition.holdSeconds").floatValue = 0.4f;
        serialized.FindProperty("feedback.definition.targetRecoilDistance").floatValue = 0.16f;
        ConfigureModule(serialized, "exit", BattlePresentationOverrideMode.Override);
        serialized.FindProperty("exit.definition.policy").enumValueIndex =
            (int)BattlePresentationExitPolicy.Fade;
        serialized.FindProperty("exit.definition.restoreSeconds").floatValue = 0.2f;
        serialized.FindProperty("exit.definition.watchdogSeconds").floatValue = 4f;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static void ConfigureBasicAttackSemanticRecipe(
        BattleSkillPresentationRecipeSO recipe,
        BattleSkillPresentationRecipeSO parent)
    {
        var serialized = new SerializedObject(recipe);
        serialized.FindProperty("presentationKey").stringValue = "basic_attack";
        serialized.FindProperty("parent").objectReferenceValue = parent;
        ConfigureModule(serialized, "hud", BattlePresentationOverrideMode.Inherit);
        ConfigureModule(serialized, "stage", BattlePresentationOverrideMode.Inherit);
        ConfigureCameraModule(serialized, 32f, 0.16f);
        ConfigureModule(serialized, "attacker", BattlePresentationOverrideMode.Inherit);
        ConfigureModule(serialized, "target", BattlePresentationOverrideMode.Inherit);
        ConfigureModule(serialized, "background", BattlePresentationOverrideMode.Inherit);
        ConfigureModule(serialized, "spine", BattlePresentationOverrideMode.Inherit);
        ConfigureModule(serialized, "feedback", BattlePresentationOverrideMode.Inherit);
        ConfigureModule(serialized, "exit", BattlePresentationOverrideMode.Inherit);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static void ConfigureCameraModule(
        SerializedObject serialized,
        float fieldOfView,
        float transitionSeconds)
    {
        ConfigureModule(
            serialized,
            "camera",
            BattlePresentationOverrideMode.Override);
        serialized.FindProperty("camera.definition.cameraRigKey").stringValue =
            "conflict.default";
        serialized.FindProperty("camera.definition.fieldOfView").floatValue =
            fieldOfView;
        serialized.FindProperty("camera.definition.transitionSeconds").floatValue =
            transitionSeconds;
        serialized.FindProperty("camera.definition.framingMode").enumValueIndex =
            (int)BattleCameraFramingMode.FrameActorAndTargets;
        serialized.FindProperty("camera.definition.horizontalPadding").floatValue =
            0.6f;
        serialized.FindProperty("camera.definition.verticalPadding").floatValue =
            0.8f;
    }

    private static void ConfigureModule(
        SerializedObject serialized,
        string moduleName,
        BattlePresentationOverrideMode mode)
    {
        SerializedProperty property = serialized.FindProperty(
            moduleName + ".mode");
        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.GetType().Name +
                " has no semantic module " + moduleName);
        }
        property.enumValueIndex = (int)mode;
    }

    private static void ConfigureSemanticCatalog(
        BattlePresentationRecipeCatalogSO catalog,
        BattleSkillPresentationRecipeSO fallback,
        BattleSkillPresentationRecipeSO basicAttack)
    {
        var serialized = new SerializedObject(catalog);
        serialized.FindProperty("defaultSkillRecipe").objectReferenceValue =
            fallback;
        SerializedProperty recipes = serialized.FindProperty("recipes");
        recipes.arraySize = 2;
        recipes.GetArrayElementAtIndex(0).objectReferenceValue = fallback;
        recipes.GetArrayElementAtIndex(1).objectReferenceValue = basicAttack;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
            return existing;

        UnityEngine.Object tagManagerAsset =
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var tagManager = new SerializedObject(tagManagerAsset);
        tagManager.Update();
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
                continue;
            layer.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(tagManagerAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return i;
        }

        throw new InvalidOperationException(
            "No free Unity layer is available for " + layerName);
    }

    private static Transform CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.layer = parent.gameObject.layer;
        return child.transform;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null || layer < 0)
            return;
        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
    }

    private static void SetReference(
        UnityEngine.Object owner,
        string propertyName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(owner);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                owner.GetType().Name + " has no serialized field " + propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(
        UnityEngine.Object owner,
        string propertyName,
        float value)
    {
        var serialized = new SerializedObject(owner);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                owner.GetType().Name + " has no serialized field " + propertyName);
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(
        UnityEngine.Object owner,
        string propertyName,
        bool value)
    {
        var serialized = new SerializedObject(owner);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                owner.GetType().Name + " has no serialized field " + propertyName);
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
