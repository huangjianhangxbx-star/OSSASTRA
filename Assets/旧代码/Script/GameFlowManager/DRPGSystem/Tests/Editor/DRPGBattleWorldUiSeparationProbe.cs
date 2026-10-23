#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattleWorldUiSeparationProbe
{
    public const string WorldPrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattleWorld.prefab";
    public const string StagePrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattleStage_Default.prefab";
    private const string WorldLayerName = "DRPGBattleWorld";

    public static string Run()
    {
        GameObject presentation = Load<GameObject>(
            DRPGBattlePerspectivePresentationAssetProbe.PresentationPrefabPath);
        GameObject world = Load<GameObject>(WorldPrefabPath);
        GameObject actor = Load<GameObject>(
            DRPGBattlePerspectivePresentationAssetProbe.WorldActorPrefabPath);
        GameObject stage = Load<GameObject>(StagePrefabPath);
        BattleStageProfileSO stageProfile = Load<BattleStageProfileSO>(
            DRPGBattlePerspectivePresentationAssetProbe.StageProfilePath);

        Type hostType = FindType("BattleWorldRuntimeHost");
        AssertNotNull(hostType, "BattleWorldRuntimeHost runtime type");
        Component host = presentation.GetComponent(hostType);
        AssertNotNull(host, "BattleWorldRuntimeHost on the UGUI presentation root");

        AssertNull(world.GetComponent<RectTransform>(),
            "Battle world root must use a normal Transform");
        AssertNull(world.GetComponentInChildren<Canvas>(true),
            "Battle world must not contain a Canvas");
        AssertEqual(0, world.GetComponentsInChildren<Graphic>(true).Length,
            "Battle world must not contain uGUI Graphic components");
        AssertNull(presentation.GetComponentInChildren<Camera>(true),
            "UGUI presentation prefab must not own the world camera");
        AssertNull(presentation.GetComponentInChildren<BattleWorldActorPool>(true),
            "UGUI presentation prefab must not own the world actor pool");
        AssertNull(presentation.GetComponentInChildren<BattleWorldRosterPresenter>(true),
            "UGUI presentation prefab must not own the world roster");
        AssertNull(presentation.GetComponentInChildren<BattleStageRoot>(true),
            "UGUI presentation prefab must not own the scene stage");

        Camera camera = RequireComponentInChildren<Camera>(world);
        AssertTrue(!camera.orthographic,
            "Battle world camera must be perspective");
        AssertNull(camera.targetTexture,
            "Direct-screen mode must be the prefab default");
        int worldLayer = LayerMask.NameToLayer(WorldLayerName);
        int uiLayer = LayerMask.NameToLayer("UI");
        AssertTrue(worldLayer >= 0, "Dedicated DRPGBattleWorld layer must exist");
        AssertTrue((camera.cullingMask & (1 << worldLayer)) != 0,
            "Battle camera must render DRPGBattleWorld");
        AssertTrue((camera.cullingMask & (1 << uiLayer)) == 0,
            "Battle camera must not render UI");

        AssertTrue(stageProfile.StagePrefab == stage,
            "Default stage profile must reference the scene-space stage prefab");
        RequireComponentInChildren<SpriteRenderer>(stage);
        RequireComponentInChildren<SpriteRenderer>(actor);
        AssertEqual(0, actor.GetComponentsInChildren<Graphic>(true).Length,
            "World actor prefab must not use uGUI Graphics");
        AssertTrue(actor.layer == worldLayer,
            "World actor prefab must use the dedicated battle-world layer");

        var serializedHost = new SerializedObject(host);
        AssertSame(world,
            serializedHost.FindProperty("worldPrefab")?.objectReferenceValue,
            "Runtime host world prefab");
        SerializedProperty outputMode =
            serializedHost.FindProperty("outputMode");
        AssertNotNull(outputMode, "Runtime host output mode");
        AssertEqual(0, outputMode.enumValueIndex,
            "DirectCamera must be the default output mode");
        RawImage optionalOutput =
            serializedHost.FindProperty("renderTextureOutput")
                ?.objectReferenceValue as RawImage;
        AssertNotNull(optionalOutput, "Optional RenderTexture output surface");
        AssertTrue(!optionalOutput.gameObject.activeSelf,
            "Optional RenderTexture output must be disabled by default");

        return "PASS battle scene world is separated from the UGUI HUD";
    }

    private static Type FindType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try { return assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception)
                {
                    return exception.Types.Where(type => type != null);
                }
            })
            .FirstOrDefault(type => type.Name == name);
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        AssertNotNull(asset, path);
        return asset;
    }

    private static T RequireComponentInChildren<T>(GameObject root)
        where T : Component
    {
        T component = root.GetComponentInChildren<T>(true);
        AssertNotNull(component, typeof(T).Name + " under " + root.name);
        return component;
    }

    private static void AssertSame(
        UnityEngine.Object expected,
        UnityEngine.Object actual,
        string message)
    {
        if (expected != actual)
            throw new Exception(message + " is not wired to the expected asset.");
    }

    private static void AssertNull(UnityEngine.Object value, string message)
    {
        if (value != null)
            throw new Exception(message + " must be null.");
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
