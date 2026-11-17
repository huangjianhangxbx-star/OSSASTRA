/*
 * 脚本：GameMenuAuthoring
 * 功能：把原生菜单壳保存为预制体，并在指定横版主场配置唯一 Menu 输入接线。
 * 边界：使用作者提供的主题资源，沿用现有 EventSystem、背包及角色会话。
 */
using System;
using System.Linq;
using System.IO;
using UnityEngine.Video;
using Ossastra.Characters;
using Ossastra.UI.GameMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ossastra.Development
{
    public static class GameMenuAuthoring
    {
        public const string ScenePath = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        public const string Folder = "Assets/OSSASTRA/SideView/GameMenu";
        public const string PrefabPath = Folder + "/GameMenu.prefab";
        public const string ThemePath = Folder + "/ReferenceTheme.asset";
        private static readonly string[] PageNames = { "Record", "Skills", "Equipment", "Relics", "Collection", "Information", "Map" };

        [MenuItem("OSSASTRA/横版/七页菜单/装配当前正式场")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
                throw new InvalidOperationException("请在已保存的横版场的 Edit 模式装配。");
            InstallForScene(scene);
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("横版场菜单接线未保存。");
        }

        public static MenuInputAdapter InstallForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("菜单装配需要已打开的横版编辑场景。");
            var installers = GetSceneItems<PlayerInputLayoutInstaller>(scene).ToArray();
            var systems = GetSceneItems<EventSystem>(scene).ToArray();
            if (installers.Length != 1 || installers[0].layout == null || installers[0].inventory == null ||
                installers[0].control == null || systems.Length != 1)
                throw new InvalidOperationException("正式场需要唯一输入安装器、角色会话、库存界面和 EventSystem。");

            var theme = AssetDatabase.LoadAssetAtPath<GameMenuTheme>(ThemePath) ?? RebuildReferenceTheme();
            if (theme == null) throw new InvalidOperationException("未找到本机参考主题。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab(theme);
            if (prefab == null) throw new InvalidOperationException("菜单预制体未保存。");

            var roots = GetSceneItems<GameMenuRoot>(scene).ToArray();
            if (roots.Length > 1) throw new InvalidOperationException("正式场已经有多个菜单，需先核对归属。");
            var instance = roots.Length == 1 ? roots[0].gameObject : PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException("菜单实例未创建。");
            if (roots.Length == 0) Undo.RegisterCreatedObjectUndo(instance, "安装七页菜单");
            var adapter = instance.GetComponent<MenuInputAdapter>();
            if (adapter == null) throw new InvalidOperationException("菜单适配器缺失。");
            var installer = installers[0];
            if (installer.gameMenu != adapter)
            {
                Undo.RecordObject(installer, "绑定七页菜单");
                installer.gameMenu = adapter;
                PrefabUtility.RecordPrefabInstancePropertyModifications(installer);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            return adapter;
        }

        private static GameMenuTheme RebuildReferenceTheme()
        {
            const string source = Folder + "/LocalReference";
            var sprites = Directory.Exists(source)
                ? Directory.GetFiles(source, "*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            foreach (var file in sprites)
            {
                var path = file.Replace(Path.DirectorySeparatorChar, '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("菜单贴图尚未导入 " + path);
                var border = Path.GetFileNameWithoutExtension(file) == "tabs_line"
                    ? new Vector4(45, 0, 45, 0) : Vector4.zero;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border &&
                    !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    importer.spriteImportMode == SpriteImportMode.Single)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/OSSASTRA/SideView", "GameMenu");
            var theme = ScriptableObject.CreateInstance<GameMenuTheme>();
            var setup = new SerializedObject(theme);
            setup.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(
                source + "/FOT_ChineseSimplified.ttf");
            setup.FindProperty("axePreview").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>(
                source + "/SkillPreviewAxe.mp4");
            var entries = setup.FindProperty("sprites");
            entries.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = Path.GetFileNameWithoutExtension(sprites[i]).Replace('_', '.');
                entry.FindPropertyRelative("sprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(sprites[i].Replace(Path.DirectorySeparatorChar, '/'));
            }
            setup.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            return theme;
        }

        private static GameObject CreatePrefab(GameMenuTheme theme)
        {
            var go = new GameObject("GameMenu", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameMenuRoot), typeof(MenuInputAdapter));
            try
            {
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 220;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                var overlay = Add("Menu Surface", go.transform);
                Stretch(overlay);
                var group = overlay.gameObject.AddComponent<CanvasGroup>();
                var scrim = overlay.gameObject.AddComponent<UnityEngine.UI.Image>();
                scrim.color = new Color32(9, 14, 19, 255);
                scrim.raycastTarget = true;

                var frame = Add("Menu 1920x1080", overlay);
                frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
                frame.sizeDelta = new Vector2(1920f, 1080f);
                var pages = PageNames.Select(name => Add(name, frame)).ToArray();
                foreach (var page in pages) Stretch(page);
                overlay.gameObject.SetActive(false);

                var menu = go.GetComponent<GameMenuRoot>();
                var fields = new SerializedObject(menu);
                fields.FindProperty("theme").objectReferenceValue = theme;
                fields.FindProperty("overlay").objectReferenceValue = group;
                fields.FindProperty("composition").objectReferenceValue = frame;
                var array = fields.FindProperty("pageRoots");
                array.arraySize = pages.Length;
                for (int i = 0; i < pages.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
                fields.ApplyModifiedPropertiesWithoutUndo();
                go.GetComponent<MenuInputAdapter>().menu = menu;
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static T[] GetSceneItems<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static RectTransform Add(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
