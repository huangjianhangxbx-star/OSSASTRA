/* 登记本工程自有图片并建立可保存的裁片引用。导入和校准分别保留媒体与元数据。 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    internal static class TerrainWorkshopArtImport
    {
        private const string WrapperFolder = "TerrainArt";

        public static TerrainArt RegisterForKit(TerrainKit kit, UnityEngine.Object asset)
        {
            string folder = SetupFolder(kit);
            return folder == null ? null : Register(folder, asset);
        }

        public static string ImportSelection(TerrainLibrary library, Action<TerrainArt> select)
        {
            string folder = SetupFolder(library);
            if (folder == null) return "请先把素材库保存为 Assets 中的资源。";
            var assets = new HashSet<UnityEngine.Object>();
            foreach (var selected in Selection.objects)
            {
                if (selected is Sprite || selected is TerrainArt)
                {
                    assets.Add(selected);
                    continue;
                }
                string path = AssetDatabase.GetAssetPath(selected);
                if (!IsProjectAsset(path)) continue;
                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                        ReadImage(AssetDatabase.GUIDToAssetPath(guid), assets);
                }
                else if (selected is Texture2D)
                    ReadImage(path, assets);
            }
            if (assets.Count == 0) return "请在 Project 中选中原创 Sprite、图片或其所在文件夹。";
            var prior = library.items ?? Array.Empty<TerrainArt>();
            var existing = new HashSet<TerrainArt>(prior.Where(item => item != null));
            var added = new List<TerrainArt>();
            var wrappers = ReadWrappers(folder);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var asset in assets.OrderBy(asset => asset.name, StringComparer.CurrentCulture))
                {
                    var item = asset as TerrainArt ?? Register(folder, asset, wrappers);
                    if (item != null && existing.Add(item))
                        added.Add(item);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            if (added.Count == 0) return "所选素材已收录。";
            Undo.RecordObject(library, "添加原创地形素材");
            library.items = prior.Concat(added).ToArray();
            EditorUtility.SetDirty(library);
            select?.Invoke(added[0]);
            return $"已添加 {added.Count} 项，新增项目素材归入待核。";
        }

        public static TerrainArt AddCrop(TerrainLibrary library, TerrainArt original, Rect pixels)
        {
            string folder = SetupFolder(library);
            if (folder == null || original == null || original.Image == null || pixels.width < 1 || pixels.height < 1)
                return original;
            var items = library.items ?? Array.Empty<TerrainArt>();
            var known = items.FirstOrDefault(art => art != null && art.sprite == null && art.Image == original.Image && art.crop == pixels);
            if (known != null) return known;

            var mapped = ScriptableObject.CreateInstance<TerrainArt>();
            mapped.texture = original.Image;
            mapped.crop = pixels;
            mapped.theme = original.theme;
            mapped.purpose = "待核";
            mapped.origin = original.origin;
            mapped.name = $"{original.name} · {pixels.x:0},{pixels.y:0}";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SafeName(mapped.name)}.asset");
            AssetDatabase.CreateAsset(mapped, path);
            Undo.RecordObject(library, "添加原页裁片");
            library.items = items.Append(mapped).ToArray();
            EditorUtility.SetDirty(library);
            return mapped;
        }

        private static void ReadImage(string path, HashSet<UnityEngine.Object> assets)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length != 0)
            {
                foreach (var sprite in sprites) assets.Add(sprite);
                return;
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) assets.Add(texture);
        }

        private static TerrainArt Register(string folder, UnityEngine.Object asset,
            Dictionary<UnityEngine.Object, TerrainArt> wrappers = null)
        {
            if (!(asset is Sprite) && !(asset is Texture2D)) return null;
            string source = AssetDatabase.GetAssetPath(asset);
            if (!IsProjectAsset(source)) return null;
            wrappers ??= ReadWrappers(folder);
            if (wrappers.TryGetValue(asset, out var known)) return known;
            var art = ScriptableObject.CreateInstance<TerrainArt>();
            art.sprite = asset as Sprite;
            art.texture = asset as Texture2D;
            art.name = asset.name;
            art.theme = "项目原创";
            art.purpose = "待核";
            art.origin = source;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SafeName(asset.name)}.asset");
            AssetDatabase.CreateAsset(art, path);
            wrappers[asset] = art;
            return art;
        }

        private static Dictionary<UnityEngine.Object, TerrainArt> ReadWrappers(string folder)
        {
            var mapped = new Dictionary<UnityEngine.Object, TerrainArt>();
            foreach (var guid in AssetDatabase.FindAssets("t:TerrainArt", new[] { folder }))
            {
                var art = AssetDatabase.LoadAssetAtPath<TerrainArt>(AssetDatabase.GUIDToAssetPath(guid));
                if (art == null || (art.sprite == null && art.crop.width > 0 && art.crop.height > 0)) continue;
                var source = art.sprite != null ? (UnityEngine.Object)art.sprite : art.texture;
                if (source != null) mapped[source] = art;
            }
            return mapped;
        }

        private static string SetupFolder(UnityEngine.Object owner)
        {
            string path = AssetDatabase.GetAssetPath(owner);
            if (!IsProjectAsset(path)) return null;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = $"{parent}/{WrapperFolder}";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, WrapperFolder);
            return AssetDatabase.IsValidFolder(folder) ? folder : null;
        }

        private static bool IsProjectAsset(string path) =>
            !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal);

        private static string SafeName(string name) =>
            string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    }
}
