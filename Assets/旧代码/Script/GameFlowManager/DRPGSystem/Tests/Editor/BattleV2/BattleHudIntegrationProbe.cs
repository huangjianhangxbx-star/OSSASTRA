#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class BattleHudIntegrationProbe
{
    private const string PresentationPrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";

    public static void RunAll()
    {
        FormalF8PresentationOwnsOneCompleteBattleV2Binding();
        GeneratedAssetsAreImportedAsTransparentSprites();
        UnbindingPreservesExplorationHudVisibility();
    }

    private static void UnbindingPreservesExplorationHudVisibility()
    {
        var instance = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath));
        try
        {
            var hud = instance.transform.Find("BattleV2Hud");
            Check(hud != null, "BattleV2Hud missing");
            hud.gameObject.SetActive(true);
            instance.GetComponent<BattleHudBinding>().Unbind();
            Check(hud.gameObject.activeSelf, "unbinding hid the HUD required in exploration");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void FormalF8PresentationOwnsOneCompleteBattleV2Binding()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath);
        Check(prefab != null, "formal F8 presentation prefab missing");
        Check(prefab.GetComponentsInChildren<BattleHudBinding>(true).Length == 1,
            "formal presentation must own exactly one BattleV2 HUD binding");
        DRPGBattleUGUISurface surface =
            prefab.GetComponentInChildren<DRPGBattleUGUISurface>(true);
        Check(surface != null && surface.BattleV2SkillBar != null &&
            surface.BattleV2CardHand != null && surface.BattleV2CardArrow != null &&
            surface.BattleV2PipRow != null,
            "formal surface has incomplete BattleV2 references");
        Check(surface.BattleV2SkillBar.VisibleSlotCount == 4 &&
            surface.BattleV2CardHand.VisibleSlotCount == 4,
            "formal HUD does not preserve four skill and four card slots");
    }

    private static void GeneratedAssetsAreImportedAsTransparentSprites()
    {
        string[] files = { "panel-metal.png", "card-frame.png", "arrow-tip.png",
            "arrow-segment.png", "pip-filled.png", "pip-empty.png", "target-corner.png" };
        for (int i = 0; i < files.Length; i++)
        {
            string path = "Assets/GameResources/DRPG/UI/BattleV2/" + files[i];
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Check(importer != null && importer.textureType == TextureImporterType.Sprite &&
                importer.alphaIsTransparency,
                "generated UI asset is not an alpha sprite: " + path);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
