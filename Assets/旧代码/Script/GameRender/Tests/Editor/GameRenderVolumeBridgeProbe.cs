#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

internal static class GameRenderVolumeBridgeProbe
{
    [MenuItem("Tools/ANKALUTE/GameRender/Run Volume Bridge Probe")]
    internal static void Run()
    {
        var originalScene = SceneManager.GetActiveScene();
        bool wasDirty = originalScene.isDirty;
        using (new GameRenderProbeScene()) RunIsolated();
        if (originalScene.isDirty != wasDirty)
            throw new Exception("Volume probe dirtied the user's open scene");
    }

    private static void RunIsolated()
    {
        var source = ScriptableObject.CreateInstance<VolumeProfile>();
        source.Add<ColorAdjustments>(true).postExposure.value = 0.3f;
        var host = new GameObject("GameRenderVolumeBridgeProbe", typeof(Camera),
            typeof(Volume), typeof(GameRenderManager));
        host.hideFlags = HideFlags.HideAndDontSave;
        var volume = host.GetComponent<Volume>();
        var manager = host.GetComponent<GameRenderManager>();
        volume.isGlobal = true;
        volume.sharedProfile = source;
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("sceneVolume").objectReferenceValue = volume;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var bridge = new GameRenderVolumeBridge();
        try
        {
            bridge.Begin(volume);
            if (volume.profile == source) throw new Exception("Volume profile was not cloned");
            if (volume.sharedProfile != source) throw new Exception("Serialized source reference changed");
            volume.profile.TryGet(out ColorAdjustments preview);
            preview.postExposure.value = 0.8f;
            source.TryGet(out ColorAdjustments original);
            if (Math.Abs(original.postExposure.value - 0.3f) > 0.0001f)
                throw new Exception("Preview modified source Volume component");
            bridge.Begin(volume);
            bridge.Restore();
            if (volume.sharedProfile != source || volume.HasInstantiatedProfile())
                throw new Exception("Original Volume profile state was not restored");
            original.name = "Original component name";
            manager.BeginVolumePreview();
            manager.VolumePreview.TryGet(out ColorAdjustments edited);
            edited.postExposure.value = 0.6f;
            GameRenderManagerEditor.SaveVolumePreviewToSource(manager);
            if (original.name != "Original component name" ||
                Math.Abs(original.postExposure.value - 0.6f) > 0.0001f)
                throw new Exception("Saving Volume preview changed component identity or lost parameters");
            manager.RestoreVolumeBaseline();
            Debug.Log("PASS GameRender Volume bridge probe (deep copy/idempotent/restore/save-identity)");
        }
        finally
        {
            bridge.Restore();
            manager.RestoreVolumeBaseline();
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }
}
#endif
