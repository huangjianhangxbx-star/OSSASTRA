/* 脚本：CharacterDefinitionSO
 * 功能：角色基础动作库与表现配置引用；双角色可共享定义但独立执行。
 * 边界：不持有HP、体力、动作、耐久与CD；SO替换在初始化时生效。
 */
using System;
using UnityEngine;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Characters/Character Definition")]
    public sealed class CharacterDefinitionSO:ScriptableObject
    {
        public string definitionId,displayName;
        public Ossastra.CharacterAnimation.AnimationCalibrationSO eventCalibration;
        [Tooltip("角色专用的横版运动配置。留空时使用场景或预制件原有配置。")]
        public CharacterJumpProfile sideViewMovementOverride;
        [Tooltip("此角色实际战斗接触和运动接触的表现覆盖。")]
        public Ossastra.Audio.SideViewFeedbackProfileSO sideViewFeedback;
        [HideInInspector] public CharacterAnimationProfileSO animationProfile; // 历史场/迁移来源；正式动画在ConfigManager原式SO中维护。
        public CharacterRigProfileSO rigProfile;
        [Tooltip("当场景指定此副手作为初始装备时，新队伍将它收入背包，角色以空副手出场。")]
        public EquipmentDefinitionSO startingOffhandInBag;
        [Tooltip("可选模型方向表现；空引用保留单面角色原行为，不改变玩法方向。")]
        public CharacterDirectionProfileSO directionProfile;
        public ActionDefinitionSO[] basicAttacks=Array.Empty<ActionDefinitionSO>();
        [Min(.01f)] public float dodgeDuration=.45f,dodgeDistance=2.4f;
        [Min(0)] public float actionPostureRecoveryMultiplier = 1;
        [Min(0)] public float dodgeCost=22,invulnerableFrom=.12f,invulnerableTo=.32f;
        [Min(0)] public float preInput=.12f,bufferLifetime=.17f,continuationWindow=.45f;
        [Tooltip("普通连段默认允许空挥接段；仅特殊设计开启命中接续要求。")]
        public bool requireHitForContinuation;
    }
}
