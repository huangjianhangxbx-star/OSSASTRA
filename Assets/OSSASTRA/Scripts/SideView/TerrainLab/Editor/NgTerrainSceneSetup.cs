using System;
using System.Linq;
using Ossastra.Characters;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.SideView.TerrainLab
{
    [InitializeOnLoad]
    public static class NgTerrainSceneSetup
    {
        public const string Folder = "Assets/OSSASTRA/SideView/PrivateValidation/NG-TERRAIN-01";
        public const string Lab = Folder + "/SC_TerrainLab.unity";
        private const string Template = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        private const string PriorStartup = "Ossastra.NGTerrainLab.PriorStartup";
        private const string CameraProfile = Folder + "/TerrainLabCamera.asset";

        private static readonly string[] retiredRoots =
        {
            "background", "core", "decor", "foreground", "atmosphere", "findings", "碰撞轮廓", "横版地图边界",
            "Tut_01 left3 入口", "Tut_01 过门读数", "Tut_01 世界状态测试档", "SV-SLICE-01", "EsVisualReference"
        };

        static NgTerrainSceneSetup() => EditorApplication.playModeStateChanged += RestoreStartup;

        [MenuItem("OSSASTRA/横版/打开 Never Grave 地形样板")]
        public static void Open() => EditorSceneManager.OpenScene(Lab, OpenSceneMode.Single);

        [MenuItem("OSSASTRA/横版/试玩 Never Grave 地形样板")]
        public static void Play()
        {
            RequireSafeEditor(allowDirtyLab: true);
            var lab = AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab);
            if (lab == null) throw new InvalidOperationException("地形样板尚未保存。" + Lab);
            var active = SceneManager.GetActiveScene();
            if (active.path == Lab)
            {
                if (active.isDirty && !EditorSceneManager.SaveScene(active)) throw new InvalidOperationException("无法保存地形样板。");
            }
            else Open();
            SessionState.SetString(PriorStartup, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = lab;
            EditorApplication.EnterPlaymode();
        }

        private static void RestoreStartup(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode && state != PlayModeStateChange.EnteredEditMode) return;
            var previous = SessionState.GetString(PriorStartup, "missing");
            if (previous == "missing") return;
            var current = EditorSceneManager.playModeStartScene;
            if (current != null && AssetDatabase.GetAssetPath(current) == Lab)
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
                    ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.EraseString(PriorStartup);
        }

        public static NgTerrainLayout Create(NgTerrainStyle[] styles, Rect cameraBounds, Vector2 spawn,
            float cellSize, float collisionDepth = 3f)
        {
            RequireSafeEditor();
            if (styles == null || styles.Length == 0 || styles.Any(style => style == null))
                throw new ArgumentException("至少需要一组已存地形套件。", nameof(styles));
            if (cellSize < .125f || collisionDepth < .2f || cameraBounds.width <= 0 || cameraBounds.height <= 0)
                throw new ArgumentException("需要有效格宽、厚度和相机边界。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab) != null || AssetDatabase.LoadAssetAtPath<SideViewCameraProfile>(CameraProfile) != null)
                throw new InvalidOperationException("样板或镜头资产已存在，请打开现有样板。" + Lab);

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/OSSASTRA/SideView/PrivateValidation", "NG-TERRAIN-01");
            var template = SceneManager.GetActiveScene();
            if (template.path != Template) template = EditorSceneManager.OpenScene(Template, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(template, Lab, true))
                throw new InvalidOperationException("无法建立正式玩法的私有场景副本。" + Lab);
            var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Single);
            var party = One<CharacterControlCoordinator>(scene);
            var follow = One<SideViewCameraFollow>(scene);
            var comparison = One<SideViewRenderComparison>(scene);
            var catalog = One<SideViewComparisonCardCatalog>(scene);
            if (party.Characters.Count != 2) throw new InvalidOperationException("样板需沿用两个原始角色。");

            foreach (var root in scene.GetRootGameObjects())
            {
                if (retiredRoots.Contains(root.name)) root.SetActive(false);
                if (root.name == "横版动作练习")
                    foreach (Transform child in root.transform)
                        if (child.name != "击打木桩") child.gameObject.SetActive(false);
            }
            var dummy = scene.GetRootGameObjects().Single(root => root.name == "横版动作练习").transform.Find("击打木桩");
            if (dummy != null) dummy.position = new Vector3(spawn.x + 9f, spawn.y, 0f);
            party.Characters[0].transform.position = new Vector3(spawn.x, spawn.y + .055f, 0f);
            party.Characters[1].transform.position = new Vector3(spawn.x + 1.5f, spawn.y + .055f, 0f);
            var camera = follow.GetComponent<Camera>();
            if (camera == null) throw new InvalidOperationException("正式主相机缺失。");
            camera.transform.position = new Vector3(spawn.x + 1f, spawn.y + 4f, camera.transform.position.z);

            var profile = ScriptableObject.CreateInstance<SideViewCameraProfile>();
            var cameraData = new SerializedObject(profile);
            cameraData.FindProperty("worldBounds").rectValue = cameraBounds;
            cameraData.FindProperty("verticalOffset").floatValue = 4;
            cameraData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, CameraProfile);
            var followData = new SerializedObject(follow);
            followData.FindProperty("profile").objectReferenceValue = profile;
            followData.ApplyModifiedPropertiesWithoutUndo();
            var rendering = new SerializedObject(comparison);
            rendering.FindProperty("startupRecipe").enumValueIndex = 2;
            rendering.FindProperty("useAuthoredSilksongMasks").boolValue = false;
            rendering.FindProperty("darknessSourceRoot").objectReferenceValue = null;
            rendering.ApplyModifiedPropertiesWithoutUndo();
            catalog.SetAuthoringEntries(Array.Empty<SideViewComparisonCardCatalog.Entry>());

            var layout = new GameObject("Never Grave Terrain Lab").AddComponent<NgTerrainLayout>();
            SceneManager.MoveGameObjectToScene(layout.gameObject, scene);
            var layoutData = new SerializedObject(layout);
            layoutData.FindProperty("cellSize").floatValue = cellSize;
            layoutData.FindProperty("collisionDepth").floatValue = collisionDepth;
            var styleField = layoutData.FindProperty("styles");
            styleField.arraySize = styles.Length;
            for (int i = 0; i < styles.Length; i++) styleField.GetArrayElementAtIndex(i).objectReferenceValue = styles[i];
            layoutData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            return layout;
        }

        private static T One<T>(Scene scene) where T : Component
        {
            var values = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(false)).ToArray();
            if (values.Length != 1) throw new InvalidOperationException(scene.path + "需要唯一组件 " + typeof(T).Name);
            return values[0];
        }

        private static void RequireSafeEditor(bool allowDirtyLab = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式打开地形样板。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && (!allowDirtyLab || scene.path != Lab)) throw new InvalidOperationException("请先保存当前场景 " + scene.path);
            }
        }
    }
}
