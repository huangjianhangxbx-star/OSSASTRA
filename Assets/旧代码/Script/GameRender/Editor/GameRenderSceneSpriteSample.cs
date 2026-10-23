#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class GameRenderSceneSpriteSample
{
    private const string ScenePath = "Assets/Scenes/测试场3_电车45.unity";
    private const string SpritePath = "Assets/随便尝试/New Folder 1/fff.png";
    private const string MaterialPath = "Assets/Script/GameRender/Materials/GameRender_fff_Sprite.mat";
    private const string OriginalMaterialPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

    [MenuItem("Tools/ANKALUTE/GameRender/Install Scene Sprite Sample")]
    internal static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Install the Sprite sample only in Edit Mode");
        var target = FindExactTarget();
        var original = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterialPath);
        if (original == null) throw new InvalidOperationException("Original Sprite-Lit material missing");
        var sample = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (target.sharedMaterial != original && target.sharedMaterial != sample)
            throw new InvalidOperationException("fff has another material; refusing to overwrite it");
        if (sample == null)
        {
            var shader = Shader.Find("ANKALUTE/GameRender/Character2DLit");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("GameRender Character2DLit shader is unavailable");
            const string folder = "Assets/Script/GameRender/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Script/GameRender", "Materials");
            sample = new Material(shader) { name = "GameRender_fff_Sprite" };
            AssetDatabase.CreateAsset(sample, MaterialPath);
            AssetDatabase.SaveAssets();
        }
        if (target.sharedMaterial == sample) return;
        Undo.RecordObject(target, "Install GameRender scene Sprite sample");
        target.sharedMaterial = sample;
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        EditorSceneManager.SaveScene(target.gameObject.scene);
        Debug.Log("GameRender scene Sprite sample installed on fff; original Sprite-Lit material remains untouched");
    }

    [MenuItem("Tools/ANKALUTE/GameRender/Restore Scene Sprite Sample")]
    internal static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Restore the Sprite sample only in Edit Mode");
        var target = FindExactTarget();
        var original = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterialPath);
        var sample = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (target.sharedMaterial == original) return;
        if (target.sharedMaterial != sample)
            throw new InvalidOperationException("fff has another material; refusing to overwrite it");
        if (original == null) throw new InvalidOperationException("Original Sprite-Lit material missing");
        Undo.RecordObject(target, "Restore scene Sprite-Lit material");
        target.sharedMaterial = original;
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        EditorSceneManager.SaveScene(target.gameObject.scene);
        Debug.Log("GameRender scene Sprite sample restored to the original Sprite-Lit material");
    }

    private static SpriteRenderer FindExactTarget()
    {
        SpriteRenderer target = null;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer.name != "fff" || renderer.gameObject.scene.path != ScenePath) continue;
            if (renderer.sprite == null || AssetDatabase.GetAssetPath(renderer.sprite) != SpritePath) continue;
            if (target != null) throw new InvalidOperationException("Scene Sprite sample target is ambiguous");
            target = renderer;
        }
        if (target == null) throw new InvalidOperationException("Exact fff scene Sprite not found");
        return target;
    }
}
#endif
