#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class GameRenderLifecycleProbe
{
    [MenuItem("Tools/ANKALUTE/GameRender/Run Lifecycle Probe")]
    internal static void Run()
    {
        using (new GameRenderProbeScene()) RunIsolated();
    }

    private static void RunIsolated()
    {
        var source = ScriptableObject.CreateInstance<GameRenderProfile>();
        var volumeSource = ScriptableObject.CreateInstance<VolumeProfile>();
        var host = new GameObject("GameRenderLifecycleProbe", typeof(Camera),
            typeof(GameRenderManager), typeof(Volume));
        host.hideFlags = HideFlags.HideAndDontSave;
        var manager = host.GetComponent<GameRenderManager>();
        var volume = host.GetComponent<Volume>();
        volume.sharedProfile = volumeSource;
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("profile").objectReferenceValue = source;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var bridge = new GameRenderVolumeBridge();
        try
        {
            for (int i = 0; i < 20; i++)
            {
                var oldProfile = manager.Profile;
                manager.ResetPreview();
                if (oldProfile != null || manager.Profile == null)
                    throw new Exception("Profile clone leaked at cycle " + i);
                bridge.Begin(volume);
                var oldVolume = bridge.Preview;
                bridge.Restore();
                if (oldVolume != null || volume.HasInstantiatedProfile())
                    throw new Exception("Volume clone leaked at cycle " + i);
            }
            Debug.Log("PASS GameRender lifecycle probe (20 preview/Volume cycles)");
        }
        finally
        {
            bridge.Restore();
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(volumeSource);
        }
    }
}
#endif
