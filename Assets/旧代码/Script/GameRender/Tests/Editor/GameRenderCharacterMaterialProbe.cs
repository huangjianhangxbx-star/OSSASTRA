#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

internal static class GameRenderCharacterMaterialProbe
{
    [MenuItem("Tools/ANKALUTE/GameRender/Run Character Material Probe")]
    internal static void Run()
    {
        var shader = Shader.Find("ANKALUTE/GameRender/Character2DLit");
        if (shader == null || !shader.isSupported)
            throw new Exception("The optional 2D-lit Sprite character shader is unavailable");
        var material = new Material(shader);
        try
        {
            foreach (string name in new[] { "_MainTex", "_GRUseInkOverride", "_GRInkOverride",
                         "_GRUseOutlineOverride", "_GROutlineOverride",
                         "_GRUseHighlightOverride", "_GRHighlightOverride", "_GRPremultipliedAlpha" })
                if (!material.HasProperty(name)) throw new Exception("Missing material property " + name);
            if (material.FindPass("Character2DLit") < 0)
                throw new Exception("Missing Universal2D lit pass");
            var profile = ScriptableObject.CreateInstance<GameRenderProfile>();
            try
            {
                profile.characterInkStrength = 0.17f;
                profile.characterHighlightLift = 0.03f;
                GameRenderCharacterGlobals.Apply(profile, true);
                if (Math.Abs(Shader.GetGlobalFloat("_GRGlobalCharacterInk") - 0.17f) > 0.0001f ||
                    Math.Abs(Shader.GetGlobalFloat("_GRGlobalCharacterHighlight") - 0.03f) > 0.0001f ||
                    Shader.GetGlobalFloat("_GRGlobalStyleEnabled") != 1f)
                    throw new Exception("Global character defaults did not follow profile");
                GameRenderCharacterGlobals.Apply(profile, false);
                if (Shader.GetGlobalFloat("_GRGlobalCharacterInk") != 0f ||
                    Shader.GetGlobalFloat("_GRGlobalStyleEnabled") != 0f)
                    throw new Exception("Turning GameRender off left character style active");
            }
            finally
            {
                GameRenderCharacterGlobals.Apply(null, false);
                UnityEngine.Object.DestroyImmediate(profile);
            }
            Debug.Log("PASS GameRender character material probe (shader/pass/individual overrides)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    [MenuItem("Tools/ANKALUTE/GameRender/Run Scene Sprite Material Probe")]
    internal static void RunSceneSprite()
    {
        const string path = "Assets/Script/GameRender/Materials/GameRender_fff_Sprite.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null || material.shader == null ||
            material.shader.name != "ANKALUTE/GameRender/Character2DLit")
            throw new Exception("The reversible scene Sprite sample material is not installed");
        SpriteRenderer target = null;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer.name != "fff" || renderer.gameObject.scene.path !=
                "Assets/Scenes/测试场3_电车45.unity") continue;
            if (renderer.sprite == null || AssetDatabase.GetAssetPath(renderer.sprite) !=
                "Assets/随便尝试/New Folder 1/fff.png") continue;
            if (target != null) throw new Exception("Scene Sprite sample target is ambiguous");
            target = renderer;
        }
        if (target == null || target.sharedMaterial != material)
            throw new Exception("Scene Sprite sample is not using the expected material");
        foreach (string property in new[] { "_GRUseInkOverride", "_GRUseOutlineOverride",
                     "_GRUseHighlightOverride" })
            if (material.GetFloat(property) != 0f)
                throw new Exception("Scene Sprite sample must inherit global " + property);
        Debug.Log("PASS GameRender scene Sprite material probe (fff; inherited global defaults)");
    }
}
#endif
