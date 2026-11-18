/*
 * 工坊打开、保存、复制套件和隔离试玩入口。
 * 由现有横版场保留角色、镜头和音画接线，再换入原创地形；正式入口和原场景不承担作者数据。
 */
using System;
using System.IO;
using System.Linq;
using Ossastra.Characters;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ossastra.SideView.TerrainWorkshop
{
    [InitializeOnLoad]
    public static class TerrainWorkshopScenes
    {
        public const string ExampleFolder = "Assets/OSSASTRA/SideView/PrivateValidation/OS-TERRAIN-01";
        public const string ExampleScene = ExampleFolder + "/SC_TerrainWorkshop.unity";
        private const string GameplayTemplate = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        private const string PreviousPlayScene = "Ossastra.TerrainWorkshop.PreviousPlayScene";
        private const string OwnPlayScene = "Ossastra.TerrainWorkshop.OwnPlayScene";
        private static readonly string[] RetiredRoots =
        {
            "background", "core", "decor", "foreground", "atmosphere", "findings", "碰撞轮廓", "横版地图边界",
            "Tut_01 left3 入口", "Tut_01 过门读数", "Tut_01 世界状态测试档", "SV-SLICE-01", "EsVisualReference"
        };

        static TerrainWorkshopScenes()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
                    RestoreStartup();
            };
            EditorApplication.quitting += RestoreStartup;
        }

        public static TerrainWorkshopRoot OpenExample()
        {
            if (!CanChangeScene()) return null;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ExampleScene) != null)
                return Find(EditorSceneManager.OpenScene(ExampleScene, OpenSceneMode.Single));
            Directory.CreateDirectory(ExampleFolder);
            AssetDatabase.Refresh();
            return CreatePlayScene(TerrainWorkshopExample.Create(), ExampleScene, true);
        }

        public static TerrainMap CreateMap(string path, TerrainMap basis)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) != null) return null;
            var map = ScriptableObject.CreateInstance<TerrainMap>();
            if (basis != null)
            {
                map.kits = basis.kits.ToArray();
                map.library = basis.library;
                map.cellSize = basis.cellSize;
                map.collisionDepth = basis.collisionDepth;
            }
            AssetDatabase.CreateAsset(map, path);
            return map;
        }

        public static TerrainKit CopyKit(TerrainKit source, string path)
        {
            if (source == null || string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) != null) return null;
            var kit = Object.Instantiate(source);
            kit.state = Mathf.Min(kit.state, 1);
            AssetDatabase.CreateAsset(kit, path);
            kit.SetAuthoringId(AssetDatabase.AssetPathToGUID(path));
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssetIfDirty(kit);
            return kit;
        }

        public static TerrainWorkshopRoot Find(Scene scene) => scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<TerrainWorkshopRoot>(true)).SingleOrDefault()
            : null;

        public static bool Save(TerrainWorkshopRoot root, TerrainMap map)
        {
            if (map == null) return false;
            AssetDatabase.SaveAssetIfDirty(map);
            if (map.library != null)
            {
                AssetDatabase.SaveAssetIfDirty(map.library);
                foreach (var art in map.library.items.Where(art => art != null)) AssetDatabase.SaveAssetIfDirty(art);
            }
            foreach (var kit in map.kits.Where(kit => kit != null)) AssetDatabase.SaveAssetIfDirty(kit);
            return root == null || EditorSceneManager.SaveScene(root.gameObject.scene);
        }

        public static string Play(TerrainWorkshopRoot root)
        {
            if (root == null || root.map == null) return "先打开示例，或为地图新建试玩场。";
            var scene = root.gameObject.scene;
            if (scene.path.Length == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return "请在编辑模式中保存试玩场。";
            if (FindLocal<CharacterControlCoordinator>(scene) == null || FindLocal<SideViewCameraFollow>(scene) == null ||
                FindLocal<Ossastra.SideView.Slice.FormalSideViewScene>(scene) == null)
                return "当前场没有双角色、镜头或正式玩法装配。请从工坊新建试玩场。";
            if (SceneManager.sceneCount > 1 && !CanChangeScene()) return "请先处理其他场景的未保存改动。";
            if (!Save(root, root.map)) return "保存失败。";
            if (SceneManager.sceneCount > 1) EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            var startup = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            SessionState.SetString(PreviousPlayScene, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(OwnPlayScene, scene.path);
            EditorSceneManager.playModeStartScene = startup;
            try { EditorApplication.EnterPlaymode(); }
            catch { RestoreStartup(); throw; }
            return null;
        }

        private static void RestoreStartup()
        {
            string own = SessionState.GetString(OwnPlayScene, "");
            if (string.IsNullOrEmpty(own)) return;
            var current = EditorSceneManager.playModeStartScene;
            if (current != null && AssetDatabase.GetAssetPath(current) == own)
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousPlayScene, ""));
            SessionState.EraseString(OwnPlayScene);
            SessionState.EraseString(PreviousPlayScene);
        }

        public static TerrainWorkshopRoot CreatePlayScene(TerrainMap map, string path, bool confirmed = false)
        {
            if (map == null || string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) != null) return null;
            if (!confirmed && !CanChangeScene()) return null;
            string mapPath = AssetDatabase.GetAssetPath(map);
            string priorScene = SceneManager.GetActiveScene().path;
            // 先从空场打开已保存模板。原场的美术与导航对象在内存移除后才保存新路径。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(GameplayTemplate, OpenSceneMode.Additive);
            try
            {
                var party = FindLocal<CharacterControlCoordinator>(scene);
                var follow = FindLocal<SideViewCameraFollow>(scene);
                var comparison = FindLocal<SideViewRenderComparison>(scene);
                var catalog = FindLocal<SideViewComparisonCardCatalog>(scene);
                if (party == null || party.Characters.Count != 2 || follow == null || comparison == null || catalog == null)
                    throw new InvalidOperationException("正式横版模板缺少双角色、镜头或画片清单。");

                foreach (var source in scene.GetRootGameObjects())
                {
                    if (RetiredRoots.Contains(source.name)) Object.DestroyImmediate(source);
                    else if (source.name == "横版动作练习")
                        foreach (var child in source.transform.Cast<Transform>().ToArray())
                            if (child.name != "击打木桩") Object.DestroyImmediate(child.gameObject);
                }
                catalog.SetAuthoringEntries(Array.Empty<SideViewComparisonCardCatalog.Entry>());
                var render = new SerializedObject(comparison);
                render.FindProperty("startupRecipe").enumValueIndex = 2;
                render.FindProperty("useAuthoredSilksongMasks").boolValue = false;
                render.FindProperty("darknessSourceRoot").objectReferenceValue = null;
                render.ApplyModifiedPropertiesWithoutUndo();

                // 试作跨三个镜头。相机约束存一份本地资产，不改正式相机或镜头预设。
                var cameraProfile = ScriptableObject.CreateInstance<SideViewCameraProfile>();
                var profileData = new SerializedObject(cameraProfile);
                profileData.FindProperty("worldBounds").rectValue = new Rect(-18, -8, 80, 23);
                profileData.FindProperty("verticalOffset").floatValue = 3.5f;
                profileData.ApplyModifiedPropertiesWithoutUndo();
                string profilePath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, null) + "-镜头.asset");
                AssetDatabase.CreateAsset(cameraProfile, profilePath);
                var followData = new SerializedObject(follow);
                followData.FindProperty("profile").objectReferenceValue = cameraProfile;
                followData.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < party.Characters.Count; i++)
                    party.Characters[i].transform.position = new Vector3(1.5f * i, .06f, 0);
                var camera = follow.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 3.5f, camera.transform.position.z);
                camera.backgroundColor = new Color(.115f, .175f, .235f);
                foreach (var light in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<SideViewAceLight>(false)))
                    if (!light.name.Contains("Diagnostic")) light.transform.position += Vector3.right * 3f;
                var dummy = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "横版动作练习")?.transform.Find("击打木桩");
                if (dummy != null) dummy.position = new Vector3(6, .05f, 0);

                SceneManager.SetActiveScene(scene);
                var root = new GameObject("横版地形工坊").AddComponent<TerrainWorkshopRoot>();
                // 切换场景可能卸载旧场未引用的作者资产；按已核实路径读回。
                root.map = AssetDatabase.LoadAssetAtPath<TerrainMap>(mapPath);
                if (root.map == null) throw new InvalidOperationException("无法读回作者地图 " + mapPath);
                TerrainWorkshopBake.Rebuild(root);
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException("无法保存隔离试玩场。");
                return Find(EditorSceneManager.OpenScene(path, OpenSceneMode.Single));
            }
            catch
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (!string.IsNullOrEmpty(priorScene)) EditorSceneManager.OpenScene(priorScene, OpenSceneMode.Single);
                throw;
            }
        }

        private static T FindLocal<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<T>(false)).FirstOrDefault();

        private static bool CanChangeScene() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }
}
