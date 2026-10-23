#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

internal static class GameRenderInstaller
{
    internal const string RendererPath = "Assets/GameResources/My Universal Render Pipeline Asset_Renderer.asset";
    internal const string ProfilePath = "Assets/Script/GameRender/Profiles/GameRender_Default.asset";

    [MenuItem("Tools/ANKALUTE/GameRender/Install On Current Map")]
    internal static void Install()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (renderer == null) throw new InvalidOperationException("Missing Renderer2D asset: " + RendererPath);
        var cameraObject = GameObject.Find("ProCamera_World");
        if (cameraObject == null || cameraObject.GetComponent<Camera>() == null)
            throw new InvalidOperationException("Current scene has no ProCamera_World camera");

        var profile = AssetDatabase.LoadAssetAtPath<GameRenderProfile>(ProfilePath);
        if (profile == null)
        {
            const string folder = "Assets/Script/GameRender/Profiles";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Script/GameRender", "Profiles");
            profile = ScriptableObject.CreateInstance<GameRenderProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        var featureCount = 0;
        foreach (var entry in renderer.rendererFeatures)
            if (entry is GameRenderFeature) featureCount++;
        if (featureCount > 1)
            throw new InvalidOperationException("Renderer already has duplicate GameRenderFeature entries");
        if (featureCount == 0)
        {
            var feature = ScriptableObject.CreateInstance<GameRenderFeature>();
            feature.name = "GameRenderFeature";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            var serialized = new SerializedObject(renderer);
            var features = serialized.FindProperty("m_RendererFeatures");
            var featureMap = serialized.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
                throw new InvalidOperationException("Renderer2D feature serialization changed; no asset saved");
            features.InsertArrayElementAtIndex(features.arraySize);
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
                throw new InvalidOperationException("Could not resolve GameRenderFeature local asset ID");
            featureMap.InsertArrayElementAtIndex(featureMap.arraySize);
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }

        var manager = cameraObject.GetComponent<GameRenderManager>();
        if (manager == null) manager = Undo.AddComponent<GameRenderManager>(cameraObject);
        var managerProperties = new SerializedObject(manager);
        var profileReference = managerProperties.FindProperty("profile");
        if (profileReference == null) throw new InvalidOperationException("GameRenderManager.profile missing");
        profileReference.objectReferenceValue = profile;
        var sceneVolume = GameObject.Find("Global Volume_仿Decoy");
        if (sceneVolume == null || sceneVolume.GetComponent<UnityEngine.Rendering.Volume>() == null)
            throw new InvalidOperationException("Current scene has no Global Volume_仿Decoy");
        managerProperties.FindProperty("sceneVolume").objectReferenceValue =
            sceneVolume.GetComponent<UnityEngine.Rendering.Volume>();
        managerProperties.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(cameraObject.scene);
        EditorSceneManager.SaveScene(cameraObject.scene);
        Debug.Log("GameRender installed: " + renderer.name + ", " + cameraObject.name + ", " + ProfilePath);
    }
}
#endif
