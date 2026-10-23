#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class BattleCloseupRenderingInstaller
{
    private const string RendererPath = "Assets/GameResources/My Universal Render Pipeline Asset_Renderer.asset";

    [MenuItem("Tools/ANKALUTE/DRPG/Close-up/Install Rendering Layers")]
    internal static void Install()
    {
        EnsureSortingLayer(BattleCloseupRenderLayer.SpectatorLayer);
        EnsureSortingLayer(BattleCloseupRenderLayer.CompositeLayer);
        EnsureSortingLayer(BattleCloseupRenderLayer.ParticipantLayer);
        EnsureOrdering();

        UnityEngine.Object renderer = AssetDatabase.LoadMainAssetAtPath(RendererPath);
        if (renderer == null) throw new InvalidOperationException("Renderer2D asset is missing: " + RendererPath);
        var serialized = new SerializedObject(renderer);
        SerializedProperty enabled = serialized.FindProperty("m_UseCameraSortingLayersTexture");
        SerializedProperty bound = serialized.FindProperty("m_CameraSortingLayersTextureBound");
        SerializedProperty downsampling = serialized.FindProperty("m_CameraSortingLayerDownsamplingMethod");
        if (enabled == null || bound == null || downsampling == null)
            throw new InvalidOperationException("Renderer2D camera sorting texture fields are unavailable.");
        enabled.boolValue = true;
        bound.intValue = SortingLayer.NameToID(BattleCloseupRenderLayer.SpectatorLayer);
        downsampling.enumValueIndex = 1; // 2x bilinear; shader supplies the authored blur.
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Installed action-owned battle close-up sorting and Renderer2D sampling.");
    }

    private static void EnsureSortingLayer(string name)
    {
        if (SortingLayer.layers.Any(layer => layer.name == name)) return;
        UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var serialized = new SerializedObject(tagManager);
        SerializedProperty layers = serialized.FindProperty("m_SortingLayers");
        int index = layers.arraySize;
        layers.InsertArrayElementAtIndex(index);
        SerializedProperty added = layers.GetArrayElementAtIndex(index);
        added.FindPropertyRelative("name").stringValue = name;
        SerializedProperty unique = added.FindPropertyRelative("uniqueID");
        int id;
        do { id = Guid.NewGuid().GetHashCode() & int.MaxValue; }
        while (id == 0 || SortingLayer.layers.Any(layer => layer.id == id));
        unique.intValue = id;
        SerializedProperty locked = added.FindPropertyRelative("locked");
        if (locked != null) locked.boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tagManager);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureOrdering()
    {
        string[] expected = { BattleCloseupRenderLayer.SpectatorLayer, BattleCloseupRenderLayer.CompositeLayer,
            BattleCloseupRenderLayer.ParticipantLayer };
        SortingLayer[] layers = SortingLayer.layers;
        int prior = -1;
        foreach (string name in expected)
        {
            int index = Array.FindIndex(layers, item => item.name == name);
            if (index <= prior) throw new InvalidOperationException("Close-up sorting layers are not ordered back-to-front.");
            prior = index;
        }
    }
}
#endif
