#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

internal static class GameRenderPreviewProbe
{
    [MenuItem("Tools/ANKALUTE/GameRender/Run Preview Probe")]
    internal static void Run()
    {
        using (new GameRenderProbeScene()) RunIsolated();
    }

    private static void RunIsolated()
    {
        var source = ScriptableObject.CreateInstance<GameRenderProfile>();
        source.inkStrength = 0.24f;
        var host = new GameObject("GameRenderPreviewProbe", typeof(Camera), typeof(GameRenderManager));
        host.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var manager = host.GetComponent<GameRenderManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("profile").objectReferenceValue = source;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var preview = manager.Profile;
            if (preview == source) throw new Exception("Preview edits source asset directly");
            var inspector = new SerializedObject(preview);
            var ink = inspector.FindProperty("inkStrength");
            if (!ink.editable)
                throw new Exception("Inspector preview slider is read-only; user cannot edit inkStrength");
            ink.floatValue = 0.77f;
            inspector.ApplyModifiedPropertiesWithoutUndo();
            if (Math.Abs(manager.Profile.inkStrength - 0.77f) > 0.0001f)
                throw new Exception("Inspector edit did not reach the rendered preview");
            if (Math.Abs(source.inkStrength - 0.24f) > 0.0001f)
                throw new Exception("Editing preview mutated source profile");
            manager.ResetPreview();
            if (Math.Abs(manager.Profile.inkStrength - 0.24f) > 0.0001f)
                throw new Exception("Reset did not restore source values");
            source.name = "Original preset name";
            manager.Profile.inkStrength = 0.52f;
            GameRenderManagerEditor.SavePreviewToProfile(manager);
            if (source.name != "Original preset name" ||
                Math.Abs(source.inkStrength - 0.52f) > 0.0001f)
                throw new Exception("Saving preview changed asset identity or lost parameters");
            Debug.Log("PASS GameRender preview probe (copy/edit/reset/save-identity)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }
}
#endif
