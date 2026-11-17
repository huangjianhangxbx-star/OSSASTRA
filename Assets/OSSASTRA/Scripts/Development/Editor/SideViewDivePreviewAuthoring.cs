using System;
using System.Linq;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Development
{
    internal static class SideViewDivePreviewAuthoring
    {
        private const string ScenePath = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        private const string AssetFolder = "Assets/OSSASTRA/SideView/Action/";
        private const string DefinitionPath = AssetFolder + "Ability_SideViewDive_Preview.asset";
        private const string ExecutorPath = AssetFolder + "Executor_SideViewDive_Preview.asset";
        private const string ActionPath = AssetFolder + "Action_SideViewDive_Preview.asset";
        private const string FreyciaDefinition = "temporary-b.vc02";

        [MenuItem("OSSASTRA/横版/俯冲候选/装配芙蕾茜娅预览")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("装配请在 Edit 模式执行");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("装配需要当前正式横版场已保存");

            var owner = FindFreycia(scene);
            var definition = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(DefinitionPath);
            if (definition == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ExecutorPath) != null ||
                    AssetDatabase.LoadMainAssetAtPath(ActionPath) != null)
                    throw new InvalidOperationException("部分预览资产已存在，请检查引用");
                var executor = ScriptableObject.CreateInstance<SideViewDiveAbilitySO>();
                var action = ScriptableObject.CreateInstance<ActionDefinitionSO>();
                action.definitionId = "side-view.dive.preview.animation";
                action.animationLogicId = Ossastra.CharacterAnimation.EAnimationLogicState.Attack_Normal_1;
                action.finishAt = action.moveReadyAt = action.attackReadyAt = .5f;
                action.staminaCost = 0;
                action.timingEvidence = "沿用该角色第一段攻击动画映射；动作和架势系统处理费用与恢复。";
                definition = ScriptableObject.CreateInstance<AbilityDefinitionSO>();
                definition.definitionId = "side-view.dive.preview";
                definition.displayName = "俯冲下击";
                definition.effectRoot = definition.damageChannel = "side-view-dive";
                definition.occupancy = AbilityOccupancy.Body | AbilityOccupancy.MainHand | AbilityOccupancy.Motion;
                definition.acceptCost = definition.releaseCost = definition.cooldown = 0;
                definition.executor = executor;
                definition.action = action;
                definition.releases = new[] { new AbilityReleaseNode { damage = 18, postureImpact = 12 } };
                definition.Compile();
                AssetDatabase.CreateAsset(executor, ExecutorPath);
                AssetDatabase.CreateAsset(action, ActionPath);
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var mount = owner.GetComponent<SideViewDiveMount>();
            if (mount == null) mount = Undo.AddComponent<SideViewDiveMount>(owner.gameObject);
            if (mount.Definition == definition && mount.OwnedAtStart) return;
            if (mount.Definition != null && mount.Definition != definition)
                throw new InvalidOperationException("芙蕾茜娅已有其他俯冲配置，请先检查");
            var binding = new SerializedObject(mount);
            binding.FindProperty("definition").objectReferenceValue = definition;
            binding.FindProperty("ownedAtStart").boolValue = true;
            binding.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("未保存俯冲预览装配");
            Debug.Log("[SV-DIVE-01] 已登记芙蕾茜娅下冲能力。");
        }

        [MenuItem("OSSASTRA/横版/俯冲候选/开关芙蕾茜娅预览", true)]
        private static bool CanToggle() => Application.isPlaying && SceneManager.GetActiveScene().path == ScenePath;

        [MenuItem("OSSASTRA/横版/俯冲候选/开关芙蕾茜娅预览")]
        public static void Toggle()
        {
            if (!CanToggle()) return;
            var mount = FindFreycia(SceneManager.GetActiveScene()).GetComponent<SideViewDiveMount>();
            if (mount == null || mount.Definition == null)
                throw new InvalidOperationException("芙蕾茜娅尚未安装俯冲预览");
            mount.SetDevelopmentOverride(!mount.IsAvailable);
            Debug.Log("[SV-DIVE-01] 芙蕾茜娅俯冲预览 " + (mount.IsAvailable ? "开启" : "关闭"));
        }

        private static Ossastra.Characters.CharacterRoot FindFreycia(Scene scene)
        {
            var matching = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Ossastra.Characters.CharacterRoot>(true))
                .Where(character => character.Definition != null && character.Definition.definitionId == FreyciaDefinition)
                .ToArray();
            if (matching.Length != 1) throw new InvalidOperationException("正式横版场的芙蕾茜娅身份未唯一匹配");
            return matching[0];
        }

        // Batch Editor API 安装入口沿用唯一正式场，已有现场若不同场景不被覆盖。
        public static void InstallBatch()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            Install();
        }
    }
}
