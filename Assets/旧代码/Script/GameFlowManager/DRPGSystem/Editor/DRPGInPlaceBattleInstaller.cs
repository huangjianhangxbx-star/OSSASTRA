#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DRPGInPlaceBattleInstaller
{
    public const string RuntimeRootName = "DRPGInPlaceBattle";
    public const string NaturalEncounterTestPath =
        "Assets/GameResources/DRPG/Config/Test/InPlaceBattle/DRPGEncounter_NaturalEncounterTest.asset";

    [MenuItem("Tools/ANKALUTE/DRPG/In Place Battle/Install Current Scene")]
    public static void InstallCurrentScene()
    {
        GameObject manager = GameObject.Find("GameFlowManager");
        if (manager == null)
            throw new InvalidOperationException("GameFlowManager was not found.");
        DRPGBattleFlowRoot flow = manager.GetComponent<DRPGBattleFlowRoot>();
        DRPGBattleDebugLauncher launcher =
            manager.GetComponent<DRPGBattleDebugLauncher>();
        if (flow == null || launcher == null)
            throw new InvalidOperationException("DRPG flow or F8 launcher is missing.");

        SerializedObject launcherData = new SerializedObject(launcher);
        DRPGPartySetupSO party = launcherData.FindProperty("party")
            ?.objectReferenceValue as DRPGPartySetupSO;
        if (party == null)
            throw new InvalidOperationException("F8 party setup is missing.");

        Camera sceneCamera = GameObject.Find("CameraSystem/ProCamera_World")
            ?.GetComponent<Camera>();
        if (sceneCamera == null)
            sceneCamera = Camera.main;
        if (sceneCamera == null)
            throw new InvalidOperationException("Scene camera was not found.");

        GameObject runtime = GameObject.Find(RuntimeRootName);
        if (runtime == null)
        {
            runtime = new GameObject(RuntimeRootName);
            Undo.RegisterCreatedObjectUndo(runtime, "Create in-place battle root");
        }

        DRPGNodeBattleContext context =
            GetOrAdd<DRPGNodeBattleContext>(runtime);
        DRPGNodeBattleCoordinator coordinator =
            GetOrAdd<DRPGNodeBattleCoordinator>(runtime);

        Transform origin = EnsureChild(runtime.transform, "BattleOrigin");
        Transform enemyRoot = EnsureChild(runtime.transform, "EnemyRoot");
        Transform partyTransform = EnsureChild(runtime.transform, "PartyRoot");
        DRPGNodePartyRoot partyRoot = GetOrAdd<DRPGNodePartyRoot>(partyTransform.gameObject);

        Vector3 cameraPosition = sceneCamera.transform.position;
        origin.position = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
        partyTransform.position = origin.position;
        enemyRoot.position = origin.position;
        BuildPartyIdentityMarkers(partyRoot, party);
        partyRoot.ConfigureForRuntime(
            2f,
            cameraPosition.x - 50f,
            cameraPosition.x + 50f,
            2.0f);
        partyRoot.SetExplorationEnabled(true);

        context.ConfigureForRuntime(
            "test-scene-3",
            DRPGNodeBattleMode.ExplorationEncounter,
            partyRoot,
            sceneCamera,
            origin,
            enemyRoot,
            new DRPGNodeBattleBounds(cameraPosition.x - 50f, cameraPosition.x + 50f),
            6f,
            9f);
        SerializedObject contextData = new SerializedObject(context);
        SerializedProperty drivers = contextData.FindProperty("explorationCameraDrivers");
        drivers.arraySize = 0;
        ProCamera2D proCamera = sceneCamera.GetComponent<ProCamera2D>();
        if (proCamera != null)
        {
            drivers.arraySize = 1;
            drivers.GetArrayElementAtIndex(0).objectReferenceValue = proCamera;
        }
        contextData.ApplyModifiedPropertiesWithoutUndo();
        contextData.Update();
        contextData.FindProperty("protagonistStableId").stringValue =
            party.Protagonist.CombatantId;
        contextData.ApplyModifiedPropertiesWithoutUndo();

        coordinator.ConfigureForRuntime(flow, party, context);
        launcherData.Update();
        launcherData.FindProperty("inPlaceCoordinator").objectReferenceValue = coordinator;
        launcherData.ApplyModifiedPropertiesWithoutUndo();

        EnsureNaturalEncounterPoints(
            runtime.transform,
            coordinator,
            context,
            launcherData.FindProperty("encounter")?.objectReferenceValue
                as DRPGEncounterDefinitionSO);

        EditorUtility.SetDirty(context);
        EditorUtility.SetDirty(coordinator);
        EditorUtility.SetDirty(launcher);
        EditorSceneManager.MarkSceneDirty(runtime.scene);
        EditorSceneManager.SaveScene(runtime.scene);
        Debug.Log("[DRPG In Place] Installed current-scene F8 context.");
    }

    [MenuItem("Tools/ANKALUTE/DRPG/In Place Battle/Install Natural Encounter Points")]
    public static void InstallNaturalEncounterPointsCurrentScene()
    {
        GameObject runtime = GameObject.Find(RuntimeRootName);
        DRPGNodeBattleCoordinator coordinator = runtime != null
            ? runtime.GetComponent<DRPGNodeBattleCoordinator>()
            : null;
        DRPGNodeBattleContext context = runtime != null
            ? runtime.GetComponent<DRPGNodeBattleContext>()
            : null;
        DRPGBattleDebugLauncher launcher =
            UnityEngine.Object.FindFirstObjectByType<DRPGBattleDebugLauncher>();
        if (runtime == null || coordinator == null || context == null || launcher == null)
        {
            throw new InvalidOperationException(
                "Install the current-scene in-place battle context first.");
        }

        SerializedObject launcherData = new SerializedObject(launcher);
        DRPGEncounterDefinitionSO encounter = launcherData.FindProperty("encounter")
            ?.objectReferenceValue as DRPGEncounterDefinitionSO;
        EnsureNaturalEncounterPoints(
            runtime.transform,
            coordinator,
            context,
            encounter);
        EditorSceneManager.MarkSceneDirty(runtime.scene);
        EditorSceneManager.SaveScene(runtime.scene);
        Debug.Log("[DRPG In Place] Installed two natural encounter points.");
    }

    private static void EnsureNaturalEncounterPoints(
        Transform runtime,
        DRPGNodeBattleCoordinator coordinator,
        DRPGNodeBattleContext context,
        DRPGEncounterDefinitionSO encounter)
    {
        DRPGEncounterDefinitionSO naturalTestEncounter =
            AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>(
                NaturalEncounterTestPath);
        if (naturalTestEncounter != null)
            encounter = naturalTestEncounter;
        if (encounter == null)
            throw new InvalidOperationException("The authored encounter is missing.");
        if (context.PartyRoot == null || context.BattleBounds == null)
            throw new InvalidOperationException("The node party or battle bounds are missing.");

        SerializedObject contextData = new SerializedObject(context);
        float leftExtent = contextData.FindProperty("requiredLeftExtent").floatValue;
        float rightExtent = contextData.FindProperty("requiredRightExtent").floatValue;
        float startX = context.PartyRoot.QueueRoot.position.x;
        float firstX = startX + 6f;
        float secondX = startX + 14f;
        if (!context.BattleBounds.ContainsBattle(firstX, leftExtent, rightExtent) ||
            !context.BattleBounds.ContainsBattle(secondX, leftExtent, rightExtent))
        {
            throw new InvalidOperationException(
                "The two test encounter points do not fit inside the authored battle-safe bounds.");
        }

        Transform pointsRoot = EnsureChild(runtime, "EncounterPoints");
        ConfigureEncounterPoint(pointsRoot, "EncounterPoint_01", "test-corridor-1",
            firstX, context.PartyRoot.QueueRoot.position.y, coordinator, encounter);
        ConfigureEncounterPoint(pointsRoot, "EncounterPoint_02", "test-corridor-2",
            secondX, context.PartyRoot.QueueRoot.position.y, coordinator, encounter);
    }

    private static void ConfigureEncounterPoint(
        Transform parent,
        string objectName,
        string pointId,
        float x,
        float y,
        DRPGNodeBattleCoordinator coordinator,
        DRPGEncounterDefinitionSO encounter)
    {
        Transform pointTransform = EnsureChild(parent, objectName);
        pointTransform.position = new Vector3(x, y, parent.position.z);
        DRPGEncounterPoint point = GetOrAdd<DRPGEncounterPoint>(pointTransform.gameObject);
        point.ConfigureForRuntime(pointId, coordinator, encounter, .75f);
        EditorUtility.SetDirty(point);
        EditorUtility.SetDirty(pointTransform);
    }

    private static void BuildPartyIdentityMarkers(
        DRPGNodePartyRoot root,
        DRPGPartySetupSO party)
    {
        var profiles = new List<DRPGCombatantProfileSO> { party.Protagonist };
        if (party.PartyMembers != null)
            profiles.AddRange(party.PartyMembers);
        for (int i = 0; i < profiles.Count; i++)
        {
            DRPGCombatantProfileSO profile = profiles[i];
            if (profile == null || string.IsNullOrWhiteSpace(profile.CombatantId))
                throw new InvalidOperationException("Party profile stable id is missing.");
            Transform marker = root.transform.Find("Member_" + profile.CombatantId);
            if (marker == null)
                marker = EnsureChild(root.transform, "Member_" + profile.CombatantId);
            DRPGScenePartyMember member = GetOrAdd<DRPGScenePartyMember>(marker.gameObject);
            CharacterRoot existingCharacter = member.Character;
            BattleWorldActorView existingView = member.View;
            if (existingCharacter == null)
                existingView = BuildPersistentPlaceholder(marker, profile);
            member.ConfigureForRuntime(
                profile.CombatantId,
                existingCharacter,
                existingView);
            EditorUtility.SetDirty(member);
        }
        root.RebuildFromChildren();
        EditorUtility.SetDirty(root);
    }

    private static BattleWorldActorView BuildPersistentPlaceholder(
        Transform marker,
        DRPGCombatantProfileSO profile)
    {
        BattleWorldActorView view = GetOrAdd<BattleWorldActorView>(marker.gameObject);
        view.EnsureAnchors();
        Transform visual = view.VisualRoot;
        SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(visual.gameObject);
        renderer.sprite = profile.FullPortrait;
        if (SortingLayer.NameToID("Character1") != 0)
            renderer.sortingLayerName = "Character1";
        renderer.sortingOrder = 10;
        if (renderer.sprite != null && renderer.sprite.bounds.size.y > 0.001f)
        {
            float scale = 4.2f / renderer.sprite.bounds.size.y;
            // Keep the node-owned actor root independent from sprite fitting.
            view.transform.localScale = Vector3.one;
            visual.localScale = Vector3.one * scale;
            Vector3 memberLocal = view.transform.localPosition;
            memberLocal.y = -1.28f;
            view.transform.localPosition = memberLocal;
            Vector3 local = visual.localPosition;
            // Preview and live binding share the same floor-aligned baseline.
            local.y = -renderer.sprite.bounds.min.y * scale;
            visual.localPosition = local;
        }
        BattleWorldActorSpriteDriver driver =
            GetOrAdd<BattleWorldActorSpriteDriver>(visual.gameObject);
        driver.ConfigureNodePreview(renderer, 4.2f);
        view.ConfigureVisualDriver(driver);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(driver);
        EditorUtility.SetDirty(view);
        return view;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;
        var created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Create " + name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
    }
}
#endif
