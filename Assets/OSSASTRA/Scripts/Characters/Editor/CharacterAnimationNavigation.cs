/*
 * 脚本：CharacterAnimationNavigation
 * 功能：只读解释角色根当前序列化view绑定，为Inspector/作者目录提供相同导航入口。
 * 边界：不猜层级、不初始化角色/装备、不解析表现规则、不写共享SO或游戏状态。
 * 协作：CharacterRootEditor读取快照；四个导航命令只改变Editor选择并打开已存在资产。
 */
using UnityEditor;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.CharacterAnimation;
using Spine.Unity;

namespace Ossastra.Authoring
{
    using CharacterRoot = Ossastra.Characters.CharacterRoot;
    using CharacterAnimationController = Ossastra.CharacterAnimation.CharacterAnimationController;
    using CharacterAnimationConfigManager = Ossastra.CharacterAnimation.CharacterAnimationConfigManager;
    using CharacterAnimationComposer = Ossastra.CharacterAnimation.CharacterAnimationComposer;
    using AnimationHandleSystem = Ossastra.CharacterAnimation.AnimationHandleSystem;
    using AnimationPlanMappingSO = Ossastra.CharacterAnimation.AnimationPlanMappingSO;
    using AnimationLogicRouterSO = Ossastra.CharacterAnimation.AnimationLogicRouterSO;
    public enum CharacterAnimationNavigationTarget { Body, BaseMap, Router, Rig, Direction }

    public sealed class CharacterAnimationBindings
    {
        public CharacterRoot root;
        public MonoBehaviour boundView;
        public CharacterAnimationController controller;
        public CharacterAnimationConfigManager config;
        public CharacterAnimationComposer composer;
        public AnimationHandleSystem handle;
        public SkeletonAnimation skeleton;
        public AnimationPlanMappingSO baseMap;
        public AnimationLogicRouterSO router;
        public CharacterDefinitionSO definition;
        public CharacterRigProfileSO rig;
        public CharacterDirectionProfileSO direction;
        public EquipmentDefinitionSO rightEquipment, leftEquipment;
        public string equipmentOrigin, problem;
    }

    public static class CharacterAnimationNavigation
    {
        public static CharacterAnimationBindings Read(CharacterRoot root)
        {
            var result = new CharacterAnimationBindings { root = root };
            if (root == null) { result.problem = "未选择角色根"; return result; }
            result.definition = root.Definition;
            result.rig = result.definition == null ? null : result.definition.rigProfile;
            result.direction = result.definition == null ? null : result.definition.directionProfile;
            result.boundView = new SerializedObject(root).FindProperty("view")?.objectReferenceValue as MonoBehaviour;
            result.controller = result.boundView as CharacterAnimationController;
            if (result.controller == null)
                result.problem = result.boundView == null ? "根的view未绑定；没有按层级猜测替代对象。" : "当前view为历史表现组件，未绑定正式CharacterAnimationController。";
            else
            {
                result.config = result.controller.GetComponent<CharacterAnimationConfigManager>();
                result.composer = result.controller.GetComponent<CharacterAnimationComposer>();
                result.handle = result.controller.GetComponent<AnimationHandleSystem>();
                result.skeleton = new SerializedObject(result.controller).FindProperty("skeleton")?.objectReferenceValue as SkeletonAnimation;
                // Controller的正式Initialize也仅在自身取Skeleton；此处只读已存在组件，不调用Initialize。
                if (result.skeleton == null) result.skeleton = result.controller.GetComponent<SkeletonAnimation>();
                if (result.config != null) { result.baseMap = result.config.baseMap; result.router = result.config.logicRouter; }
                else result.problem = "已绑定身体缺少CharacterAnimationConfigManager。";
            }
            var equipment = root.GetComponent<CharacterEquipment>();
            if (Application.isPlaying && equipment != null)
            {
                result.rightEquipment = equipment.RightDefinition; result.leftEquipment = equipment.LeftDefinition;
                result.equipmentOrigin = equipment.InventoryBound ? "Play：当前库存手槽真实装备" : "Play：当前本体装备";
            }
            else
            {
                result.rightEquipment = equipment == null ? null : new SerializedObject(equipment).FindProperty("rightDefinition")?.objectReferenceValue as EquipmentDefinitionSO;
                result.equipmentOrigin = "Edit：右手仅为组件预置引用；左右实际手槽均在Play由库存绑定。未创建装备实例。";
            }
            return result;
        }

        public static Object Target(CharacterAnimationBindings bindings, CharacterAnimationNavigationTarget target)
        {
            switch (target)
            {
                case CharacterAnimationNavigationTarget.Body: return bindings.boundView == null ? null : bindings.boundView.gameObject;
                case CharacterAnimationNavigationTarget.BaseMap: return bindings.baseMap;
                case CharacterAnimationNavigationTarget.Router: return bindings.router;
                case CharacterAnimationNavigationTarget.Rig: return bindings.rig;
                case CharacterAnimationNavigationTarget.Direction: return bindings.direction;
                default: return null;
            }
        }

        public static bool Navigate(CharacterRoot root, CharacterAnimationNavigationTarget target)
        {
            var destination = Target(Read(root), target);
            if (destination == null) return false;
            Selection.activeObject = destination;
            EditorGUIUtility.PingObject(destination);
            return !EditorUtility.IsPersistent(destination) || AssetDatabase.OpenAsset(destination);
        }
    }
}
