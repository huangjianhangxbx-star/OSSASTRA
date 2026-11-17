/*
 * 共用横版设施的序列化装配。角色、地图和存档服务继续由各自作者工具维护。
 */
using System;
using System.Linq;
using Ossastra.Audio;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.Development;
using Ossastra.UserInterface;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ossastra.SideView.Slice
{
    public static class FormalSideViewSceneAuthoring
    {
        private const string FeedbackPrefab = GameMenuAuthoring.Folder + "/SideViewCombatFeedback.prefab";
        private const string FeedbackShader = "Assets/OSSASTRA/Scripts/Combat/Rendering/SideViewCombatEffect.shader";

        [MenuItem("OSSASTRA/横版/装配当前正式玩法场")]
        public static void InstallActive()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
                throw new InvalidOperationException("请在已保存的横版场 Edit 模式执行。");
            InstallGameplay(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("正式装配未保存：" + scene.path);
        }

        public static void InstallGameplay(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("正式装配需要一个打开的横版编辑场景。");

            var installer = One<PlayerInputLayoutInstaller>(scene);
            var party = One<CharacterControlCoordinator>(scene);
            var cameraFollow = One<SideViewCameraFollow>(scene);
            var camera = cameraFollow.GetComponent<Camera>();
            var inventory = installer.inventory;
            var interaction = installer.interaction;
            var hud = One<InventoryHudBridge>(scene);
            var movementAudio = One<SideViewLocomotionAudioAdapter>(scene);
            var context = One<BgmContextHost>(scene);
            var rendering = One<SideViewRenderComparison>(scene);
            var bottle = One<HealingBottleService>(scene);
            var diver = party.Characters.SingleOrDefault(actor => actor != null &&
                actor.ActorId == FormalSideViewScene.StartingDiveActor);
            var mount = diver != null ? diver.GetComponent<SideViewDiveMount>() : null;
            if (installer.control != party || party.Characters.Count != 2 || camera == null ||
                inventory == null || interaction == null || mount == null ||
                !(mount.Definition?.executor is SideViewDiveAbilitySO))
                throw new InvalidOperationException("横版双角色、下冲定义、相机或库存接线缺失：" + scene.path);

            var menu = GameMenuAuthoring.InstallForScene(scene);
            var theme = AssetDatabase.LoadAssetAtPath<Ossastra.UI.GameMenu.GameMenuTheme>(GameMenuAuthoring.ThemePath);
            var prompt = GetOrAdd<SideViewInteractionPrompt>(interaction.gameObject);
            Undo.RecordObject(prompt, "绑定就近交互提示");
            prompt.Configure(interaction, camera, theme);
            Set(mount, ("ownedAtStart", (object)true));
            Set(installer, ("requireGameMenu", (object)true));

            InstallCombatAudio(scene, party, context);
            var feedback = InstallFeedback(scene, cameraFollow, bottle);
            var assembly = GetOrAdd<FormalSideViewScene>(installer.gameObject);
            Set(assembly, ("input", installer), ("hud", hud), ("prompt", prompt),
                ("feedback", feedback), ("cameraFollow", cameraFollow), ("music", context),
                ("movementAudio", movementAudio), ("rendering", rendering));
            if (menu != installer.gameMenu || !prompt.IsConfigured)
                throw new InvalidOperationException("正式菜单或交互提示接线失败：" + scene.path);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void InstallTitleAudio(Scene scene)
        {
            var existing = Items<BgmContextHost>(scene);
            if (existing.Length > 1)
                throw new InvalidOperationException("标题存在重复音乐上下文：" + scene.path);
            var context = existing.Length == 1 ? existing[0] : CreateContext(scene);
            var title = GetOrAdd<BgmSceneContextAdapter>(context.gameObject);
            Set(title, ("host", context), ("titleScene", true));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void InstallCombatAudio(Scene scene)
            => InstallCombatAudio(scene, One<CharacterControlCoordinator>(scene), One<BgmContextHost>(scene));

        private static void InstallCombatAudio(Scene scene, CharacterControlCoordinator party, BgmContextHost context)
        {
            var sessions = Items<CombatEngagementSession>(scene);
            var adapters = Items<BgmCombatContextAdapter>(scene);
            if (sessions.Length > 1 || adapters.Length > 1)
                throw new InvalidOperationException("音频交战事实来源重复：" + scene.path);
            var session = sessions.FirstOrDefault() ?? Undo.AddComponent<CombatEngagementSession>(context.gameObject);
            var adapter = adapters.FirstOrDefault() ?? Undo.AddComponent<BgmCombatContextAdapter>(context.gameObject);
            Set(session, ("coordinator", party));
            Set(adapter, ("host", context), ("session", session));

            var data = new SerializedObject(session);
            var roster = data.FindProperty("enemies");
            var enemies = Items<Ossastra.Enemies.EnemyRoot>(scene);
            roster.arraySize = enemies.Length;
            for (var i = 0; i < enemies.Length; i++) roster.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(session);
        }

        private static BgmContextHost CreateContext(Scene scene)
        {
            var host = new GameObject("Title Audio Context");
            SceneManager.MoveGameObjectToScene(host, scene);
            Undo.RegisterCreatedObjectUndo(host, "装配标题音乐上下文");
            return Undo.AddComponent<BgmContextHost>(host);
        }

        private static CombatFeedbackView InstallFeedback(
            Scene scene, SideViewCameraFollow camera, HealingBottleService bottle)
        {
            var current = Items<CombatFeedbackView>(scene);
            if (current.Length > 1)
                throw new InvalidOperationException("存在多个战斗反馈实例：" + scene.path);
            var previous = current.FirstOrDefault();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FeedbackPrefab);
            if (prefab == null)
            {
                if (scene.path != SliceSceneCatalog.Tut01ScenePath || previous == null)
                    throw new InvalidOperationException("请先从 Tut_01 的正式反馈生成共用预制体。");
                prefab = CreateFeedbackPrefab(previous);
            }

            var feedback = previous;
            if (previous == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(previous.gameObject) != FeedbackPrefab)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null || !instance.TryGetComponent(out feedback))
                    throw new InvalidOperationException("无法实例化共用战斗反馈：" + scene.path);
                Undo.RegisterCreatedObjectUndo(instance, "实例化横版反馈");
                if (previous != null) Undo.DestroyObjectImmediate(previous);
            }
            Set(feedback, ("shakeSink", camera), ("bottle", bottle));
            return feedback;
        }

        private static GameObject CreateFeedbackPrefab(CombatFeedbackView template)
        {
            var scratch = new GameObject("Side View Combat Feedback");
            try
            {
                var feedback = scratch.AddComponent<CombatFeedbackView>();
                EditorUtility.CopySerialized(template, feedback);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(FeedbackShader);
                if (shader == null) throw new InvalidOperationException("横版效果 Shader 缺失：" + FeedbackShader);
                Set(feedback, ("sideView", true), ("shakeSink", null), ("bottle", null),
                    ("sideViewEffects.shader", shader));
                return PrefabUtility.SaveAsPrefabAsset(scratch, FeedbackPrefab) ??
                    throw new InvalidOperationException("无法保存共用战斗反馈预制体。");
            }
            finally { Object.DestroyImmediate(scratch); }
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
            => owner.TryGetComponent<T>(out var found) ? found : Undo.AddComponent<T>(owner);

        private static T One<T>(Scene scene) where T : Component
        {
            var items = Items<T>(scene);
            if (items.Length != 1)
                throw new InvalidOperationException(scene.path + " 需要唯一 " + typeof(T).Name + "，实际 " + items.Length);
            return items[0];
        }

        private static T[] Items<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(false)).ToArray();

        private static void Set(Object target, params (string name, object value)[] fields)
        {
            var data = new SerializedObject(target);
            foreach (var (name, value) in fields)
            {
                var field = data.FindProperty(name) ?? throw new InvalidOperationException(target.name + "/" + name);
                if (value is bool flag) field.boolValue = flag;
                else field.objectReferenceValue = (Object)value;
            }
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
