/*
 * 在编辑器中读取六张横版地图的检查点，装配游戏标题、存档会话与正式构建入口。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Commerce.Runtime;
using Ossastra.SideView.Slice;
using Ossastra.UI.GameMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.SideView.Slice.Editor
{
    public static class SideViewSliceTitleAuthoring
    {
        private const string ThemePath = "Assets/OSSASTRA/SideView/GameMenu/ReferenceTheme.asset";
        private const string GameMenuPath = "Assets/OSSASTRA/SideView/GameMenu/GameMenu.prefab";
        [MenuItem("OSSASTRA/横版/装配标题与存档入口")]
        public static void Assemble()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在 Edit 模式装配标题场景。");
            var original = SceneManager.GetActiveScene();
            if (!original.IsValid() || string.IsNullOrWhiteSpace(original.path) || original.isDirty)
                throw new InvalidOperationException("请先保存当前场景，以便恢复原来的编辑视图。");
            for (int i = 0; i < SceneManager.sceneCount; ++i)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("存在未保存的场景：" + SceneManager.GetSceneAt(i).path);
            var theme = ExistingTheme();
            var entries = SliceSceneCatalog.GameplayScenes.Select(ReadMap).ToArray();
            EnsureTitle(theme, entries);
            EnsureBuildSettings();
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            AssetDatabase.SaveAssets();
            Debug.Log("横版标题已保存，六张地图的检查点已经登记。");
        }

        private static GameMenuTheme ExistingTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameMenuTheme>(ThemePath);
            if (theme != null) return theme;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuPath);
            var menu = prefab != null ? prefab.GetComponent<GameMenuRoot>() : null;
            if (menu != null)
                theme = new SerializedObject(menu).FindProperty("theme")?.objectReferenceValue as GameMenuTheme;
            if (theme == null)
                throw new InvalidOperationException("现有七页菜单主题没有可导入的 GameMenuTheme 引用：" + ThemePath);
            return theme;
        }

        private static SliceSceneEntry ReadMap(SliceSceneSpec spec)
        {
            var path = spec.Path;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("地图尚未由 SVSliceSceneAuthoring 装配：" + path);
            bool openedHere;
            var scene = OpenExisting(path, out openedHere);
            try
            {
                if (scene.isDirty)
                    throw new InvalidOperationException("地图有未保存的编辑，无法可靠读取锚点：" + path);
                var commerce = One<CommerceService>(scene, path);
                var party = One<CharacterControlCoordinator>(scene, path);
                if (commerce.mapId != spec.MapId || party.Characters.Count != 2 ||
                    party.Characters.Any(actor => actor == null))
                    throw new InvalidOperationException("地图身份或双角色接线与标题目录不符：" + path);
                var roots = Items<SideViewSliceProgress>(scene).ToArray();
                if (roots.Length != 1)
                    throw new InvalidOperationException("地图必须已有唯一的 SliceProgress：" + path + "；实际 " + roots.Length);
                var progress = roots[0];
                if (progress.commerce != commerce || progress.party != party || progress.mapId != spec.MapId ||
                    progress.initialAnchorId != spec.InitialAnchorId || progress.anchors == null ||
                    progress.anchors.Length == 0 || progress.anchors.Any(anchor => anchor == null ||
                        string.IsNullOrWhiteSpace(anchor.anchorId)) ||
                    spec.RequiredAnchorIds.Any(id => !progress.anchors.Any(anchor => anchor.anchorId == id)))
                    throw new InvalidOperationException("已有进度根与地图、角色或必要安全锚点不一致：" + path);
                var ids = progress.anchors.Select(anchor => anchor.anchorId).ToArray();
                if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                    throw new InvalidOperationException("检查点锚点 ID 重复：" + path);
                return new SliceSceneEntry { mapId = spec.MapId, scenePath = path, anchorIds = ids };
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureTitle(GameMenuTheme theme, SliceSceneEntry[] entries)
        {
            bool alreadyExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(SliceSceneCatalog.TitleScenePath) != null;
            bool openedHere = true;
            var scene = alreadyExists
                ? OpenExisting(SliceSceneCatalog.TitleScenePath, out openedHere)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (alreadyExists && scene.isDirty)
                    throw new InvalidOperationException("标题场已有未保存修改，拒绝覆盖。");
                var flows = Items<SideViewSliceSessionFlow>(scene).ToArray();
                var menus = Items<SideViewSliceTitleMenu>(scene).ToArray();
                if (flows.Length > 1 || menus.Length > 1)
                    throw new InvalidOperationException("标题场有重复的会话流或菜单。");
                var flow = flows.Length == 1 ? flows[0] : CreateTitleComponent<SideViewSliceSessionFlow>(scene, "Slice Session Flow");
                var menu = menus.Length == 1 ? menus[0] : CreateTitleComponent<SideViewSliceTitleMenu>(scene, "Slice Title Menu");
                if (flow.gameObject == menu.gameObject)
                    throw new InvalidOperationException("会话流必须在独立根节点，避免菜单跨场持久化。");
                Undo.RecordObject(flow, "Configure Slice scene catalog");
                flow.titleScenePath = SliceSceneCatalog.TitleScenePath;
                flow.firstMapId = SliceSceneCatalog.Tut01MapId;
                flow.saveDirectory = string.Empty; // 沿用八槽存档的持久化目录与槽位标识。
                flow.scenes = entries;
                Undo.RecordObject(menu, "Configure Slice title menu");
                menu.sessionFlow = flow;
                menu.theme = theme;
                if (!Items<Camera>(scene).Any())
                {
                    var cameraObject = new GameObject("Title Camera", typeof(Camera), typeof(AudioListener));
                    SceneManager.MoveGameObjectToScene(cameraObject, scene);
                    Undo.RegisterCreatedObjectUndo(cameraObject, "Author Slice title camera");
                    cameraObject.tag = "MainCamera";
                    var camera = cameraObject.GetComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = theme.Background;
                    camera.orthographic = true;
                }
                FormalSideViewSceneAuthoring.InstallTitleAudio(scene);
                if (!EditorSceneManager.SaveScene(scene, SliceSceneCatalog.TitleScenePath))
                    throw new InvalidOperationException("标题场景未保存。");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T CreateTitleComponent<T>(Scene scene, string name) where T : Component
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Author Slice title");
            return Undo.AddComponent<T>(root);
        }

        private static Scene OpenExisting(string path, out bool openedHere)
        {
            var scene = SceneManager.GetSceneByPath(path);
            openedHere = !scene.IsValid() || !scene.isLoaded;
            return openedHere ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }

        private static T One<T>(Scene scene, string path) where T : Component
        {
            var items = Items<T>(scene).ToArray();
            if (items.Length != 1)
                throw new InvalidOperationException(path + " 应包含唯一的 " + typeof(T).Name + "；实际 " + items.Length);
            return items[0];
        }

        private static IEnumerable<T> Items<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static void EnsureBuildSettings()
        {
            var existing = EditorBuildSettings.scenes.ToList();
            var ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SliceSceneCatalog.TitleScenePath, true)
            };
            foreach (var entry in existing)
            {
                if (entry.path == SliceSceneCatalog.TitleScenePath) continue;
                if (SliceSceneCatalog.GameplayScenes.Any(spec => spec.Path == entry.path))
                    ordered.Add(new EditorBuildSettingsScene(entry.path, true));
                else ordered.Add(entry);
            }
            foreach (var path in SliceSceneCatalog.GameplayScenes.Select(spec => spec.Path))
                if (!ordered.Any(entry => entry.path == path))
                    ordered.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = ordered.ToArray();
        }
    }
}
