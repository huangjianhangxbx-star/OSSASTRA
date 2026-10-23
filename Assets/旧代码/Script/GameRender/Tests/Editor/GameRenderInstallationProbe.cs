#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

internal static class GameRenderInstallationProbe
{
    [MenuItem("Tools/ANKALUTE/GameRender/Run Installation Probe")]
    internal static void Run()
    {
        GameRenderInstaller.Install();
        GameRenderInstaller.Install();
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
            "Assets/GameResources/My Universal Render Pipeline Asset_Renderer.asset");
        var count = 0;
        foreach (var feature in data.rendererFeatures)
            if (feature is GameRenderFeature) count++;
        if (count != 1) throw new Exception("Expected exactly one GameRenderFeature, got " + count);
        var profile = AssetDatabase.LoadAssetAtPath<GameRenderProfile>(
            "Assets/Script/GameRender/Profiles/GameRender_Default.asset");
        if (profile == null) throw new Exception("Global profile was not saved");
        var camera = GameObject.Find("ProCamera_World");
        if (camera == null || camera.GetComponent<GameRenderManager>() == null)
            throw new Exception("Current map camera was not bound");
        Debug.Log("PASS GameRender installation probe (idempotent feature/profile/camera)");
    }
}
#endif
