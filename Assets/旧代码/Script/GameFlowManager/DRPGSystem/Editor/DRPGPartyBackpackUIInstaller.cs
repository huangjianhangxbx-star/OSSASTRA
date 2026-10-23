#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

public static class DRPGPartyBackpackUIInstaller
{
    private const string FontPath =
        "Assets/SiYuanHeiTi-ExtraLight/SourceHanSansSC-ExtraLight-22 SDF.asset";
    public const string PrefabPath =
        "Assets/Resources/DRPG/UI/PF_DRPGPartyBackpack.prefab";

    [MenuItem("Tools/ANKALUTE/DRPG/Install Party Backpack UI")]
    public static void Install()
    {
        string directory = Path.GetDirectoryName(PrefabPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var root = new GameObject(
            "PF_DRPGPartyBackpack",
            typeof(RectTransform),
            typeof(DRPGPartyBackpackController));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null)
        {
            var serialized = new SerializedObject(
                root.GetComponent<DRPGPartyBackpackController>());
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[DRPGBackpack] Installed " + PrefabPath);
    }
}
#endif
