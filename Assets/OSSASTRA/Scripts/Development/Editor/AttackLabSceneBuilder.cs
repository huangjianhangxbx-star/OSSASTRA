// 用正式横版装配生成专用测试场。仅在新建的副本内修改，原地图和全局渲染配置保持独立。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.Enemies;
using LabEnemy = Ossastra.Enemies.EnemyRoot;
using Ossastra.UserInterface;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    public static class AttackLabSceneBuilder
    {
        private const string Folder = "Assets/OSSASTRA/SideView/Development";
        public const string ScenePath = Folder + "/SC_SideViewAttackLab.unity";
        private const string Source = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        private static readonly HashSet<string> WorldRoots = new HashSet<string>
        {
            "background", "core", "decor", "foreground", "atmosphere", "findings", "碰撞轮廓", "横版地图边界",
            "Tut_01 left3 入口", "Tut_01 过门读数", "Tut_01 世界状态测试档", "SV-SLICE-01", "EsVisualReference"
        };

        public static bool EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return false;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return EnsureOutcomeFixtures();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OSSASTRA/SideView", "Development");
            var previous = SceneManager.GetActiveScene();
            Scene lab = default;
            var created = new List<string>();
            try
            {
                if (!AssetDatabase.CopyAsset(Source, ScenePath)) throw new IOException("无法复制正式横版装配。");
                created.Add(ScenePath);
                lab = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(lab);

                var party = One<CharacterControlCoordinator>(lab);
                var follow = One<SideViewCameraFollow>(lab);
                var target = lab.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabEnemy>(true))
                    .Single(enemy => enemy.name == "击打木桩");
                if (party.Characters.Count != 2 || party.Characters.Any(a => a == null))
                    throw new InvalidOperationException("正式角色引用不完整。");
                foreach (var obj in lab.GetRootGameObjects())
                {
                    if (obj.name == "SideViewComparisonControls") obj.SetActive(false);
                    else if (WorldRoots.Contains(obj.name)) Object.DestroyImmediate(obj);
                    else if (obj == target.transform.parent.gameObject)
                        foreach (var child in obj.transform.Cast<Transform>().ToArray())
                            if (child != target.transform) Object.DestroyImmediate(child.gameObject);
                }
                var catalog = One<SideViewComparisonCardCatalog>(lab);
                catalog.SetAuthoringEntries(Array.Empty<SideViewComparisonCardCatalog.Entry>());
                var render = new SerializedObject(One<SideViewRenderComparison>(lab));
                render.FindProperty("startupRecipe").enumValueIndex = 2;
                render.FindProperty("useAuthoredSilksongMasks").boolValue = false;
                render.FindProperty("darknessSourceRoot").objectReferenceValue = null;
                render.ApplyModifiedPropertiesWithoutUndo();

                var cameraData = new SerializedObject(follow);
                var profile = Object.Instantiate((SideViewCameraProfile)cameraData.FindProperty("profile").objectReferenceValue);
                var profileData = new SerializedObject(profile);
                profileData.FindProperty("worldBounds").rectValue = new Rect(-55, -8, 110, 24);
                profileData.FindProperty("verticalOffset").floatValue = 2.1f;
                profileData.FindProperty("roomFraming").arraySize = 0;
                profileData.ApplyModifiedPropertiesWithoutUndo();
                string lensPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/攻击调试台镜头.asset");
                AssetDatabase.CreateAsset(profile, lensPath);
                created.Add(lensPath);
                cameraData.FindProperty("profile").objectReferenceValue = profile;
                cameraData.ApplyModifiedPropertiesWithoutUndo();
                var camera = follow.GetComponent<Camera>();
                camera.backgroundColor = new Color(.105f, .16f, .21f);
                camera.transform.position = new Vector3(0, 2.1f, camera.transform.position.z);

                foreach (var ai in lab.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CompanionFollowDirector>(true))) ai.enabled = false;
                foreach (var actor in party.Characters)
                {
                    var ai = actor.GetComponent<CompanionCombatBrain>();
                    if (ai != null) ai.enabled = false;
                }
                var shape = target.GetComponent<CapsuleCollider>();
                if (shape == null) throw new InvalidOperationException("正式击打木桩缺少胶囊。");
                if (target.GetComponent<CombatStatusEffects>() == null) target.gameObject.AddComponent<CombatStatusEffects>();
                if (target.GetComponent<EnemyMovement>() == null) target.gameObject.AddComponent<EnemyMovement>();
                var body = target.GetComponent<CharacterController>();
                body.radius = shape.radius;
                body.height = shape.height;
                body.center = shape.center;
                Object.DestroyImmediate(shape);
                var feedback = lab.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CombatFeedbackView>(true)).FirstOrDefault();
                if (feedback == null)
                    feedback = new GameObject("横版战斗反馈").AddComponent<CombatFeedbackView>();
                feedback.ConfigureSideView(follow);

                // 平地保留同一条战斗平面。台阶、目标托台及墙在每代试验依据方向和高度排布。
                var session = new GameObject("横版攻击调试台").AddComponent<AttackLabSession>();
                var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                var material = new Material(shader) { color = new Color(.22f, .29f, .34f) };
                string materialPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/攻击调试台地面.mat");
                AssetDatabase.CreateAsset(material, materialPath);
                created.Add(materialPath);
                Ground(session.transform, material, "平地", new Vector3(0, -.5f, 0), new Vector3(100, 1, 5));
                var ledge = Ground(session.transform, material, "平台边缘", new Vector3(-9,.35f,0), new Vector3(4,.7f,4));
                var stand = Ground(session.transform, material, "目标托台", new Vector3(7,.35f,0), new Vector3(.65f,.7f,4));
                var wall = Ground(session.transform, material, "练习墙", new Vector3(10,2.5f,0), new Vector3(.75f,5,4));
                session.Configure(party, target, follow, ledge, stand, wall);
                AddOutcomeFixtures(session);
                party.Characters[0].transform.position = new Vector3(0,.035f,0);
                party.Characters[1].transform.position = new Vector3(-35,.035f,0);
                target.transform.position = new Vector3(1.4f,.035f,0);
                if (!EditorSceneManager.SaveScene(lab)) throw new IOException("调试台场景保存失败。");
                created.Clear();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (lab.IsValid() && lab.isLoaded) EditorSceneManager.CloseScene(lab, true);
                foreach (var path in created) AssetDatabase.DeleteAsset(path);
            }
        }

        // 旧训练场只补目标装配。保留当前编辑场景及其未保存内容。
        private static bool EnsureOutcomeFixtures()
        {
            var previous = SceneManager.GetActiveScene();
            var lab = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !lab.isLoaded;
            try
            {
                if (openedHere) lab = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var session = One<AttackLabSession>(lab);
                if (session.HasOutcomeFixtures) return true;
                if (lab.isDirty)
                {
                    Debug.LogWarning("攻击调试台场景有未保存的修改。现有场地可试玩，新增靶组将在场景保存后安装。");
                    return true;
                }
                AddOutcomeFixtures(session);
                EditorSceneManager.MarkSceneDirty(lab);
                return EditorSceneManager.SaveScene(lab);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return false;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (openedHere && lab.IsValid() && lab.isLoaded) EditorSceneManager.CloseScene(lab, true);
            }
        }

        private static void AddOutcomeFixtures(AttackLabSession session)
        {
            var original = session.Target;
            LabEnemy Create(string displayName, string identity, int? hitPoints)
            {
                var instance = Object.Instantiate(original, original.transform.parent);
                instance.name = displayName;
                var enemy = new SerializedObject(instance);
                enemy.FindProperty("actorId").stringValue = identity;
                enemy.FindProperty("attacksEnabled").boolValue = false;
                enemy.ApplyModifiedPropertiesWithoutUndo();
                if (hitPoints.HasValue)
                {
                    var vitals = new SerializedObject(instance.GetComponent<CombatVitals>());
                    vitals.FindProperty("maximumHp").intValue = hitPoints.Value;
                    vitals.ApplyModifiedPropertiesWithoutUndo();
                }
                instance.gameObject.SetActive(false);
                return instance;
            }

            var fragile = Create("致死反馈靶", "attack-lab-fragile", 1);
            var paired = Create("第二命中靶", "attack-lab-paired", null);
            var guard = original.GetComponent<AttackLabGuardFixture>();
            if (guard == null) guard = original.gameObject.AddComponent<AttackLabGuardFixture>();
            guard.enabled = false;
            var substance = original.GetComponent<Ossastra.Audio.AudioSurfaceFact>();
            if (substance == null) substance = original.gameObject.AddComponent<Ossastra.Audio.AudioSurfaceFact>();
            substance.impactMaterial = "metal";
            session.ConfigureOutcomeFixtures(fragile, paired);
        }

        private static T One<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).Single();

        private static Transform Ground(Transform parent, Material material, string name, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
