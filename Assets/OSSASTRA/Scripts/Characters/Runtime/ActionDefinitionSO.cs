/* 脚本：ActionDefinitionSO
 * 功能：可复用动作定义；语义键、事件资料派生时间线与效果参数。
 * 边界：不保存执行ID/当前阶段/CD/角色资源；接受时冻结为原运行时不可变定义。
 */
using System;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    [Serializable]
    public sealed class ActionTimelineBinding
    {
        public ActionFactKind marker;
        public string sourceEvent;
        [Min(0)] public float seconds;
    }
    [CreateAssetMenu(menuName="OSSASTRA/Actions/Action Definition")]
    public sealed class ActionDefinitionSO:ScriptableObject
    {
        public string definitionId;
        [HideInInspector] public string animationKey; // 历史资产读取兼容；正式作者只维护animationLogicId。
        [Tooltip("正式动画逻辑ID；animationKey仅保留未迁移旧资产的读取兼容。")]
        public Ossastra.CharacterAnimation.EAnimationLogicState animationLogicId = Ossastra.CharacterAnimation.EAnimationLogicState.None;
        public ActionTimelineBinding[] eventTimeline=Array.Empty<ActionTimelineBinding>();
        [Min(0)] public float releaseAt,moveReadyAt,attackReadyAt;
        [Min(.01f)] public float finishAt=1;
        [Min(1)] public int damage=12;
        [Min(0)] public float postureImpact=35,staminaCost=10;
        [TextArea] public string timingEvidence;
        public BasicAttackDefinition Freeze()
        {
            float At(ActionFactKind kind,float fallback)
            {
                if(eventTimeline==null)throw new ArgumentException("Missing event timeline array");
                if(eventTimeline.Length==0)return fallback;
                var matches=eventTimeline.Where(e=>e!=null&&e.marker==kind).ToArray();
                if(matches.Length!=1)throw new ArgumentException("Exactly one timeline marker required: "+kind);
                return matches[0].seconds;
            }
            return new BasicAttackDefinition(At(ActionFactKind.Released,releaseAt),At(ActionFactKind.MoveReady,moveReadyAt),
                At(ActionFactKind.AttackReady,attackReadyAt),At(ActionFactKind.Finished,finishAt),damage,postureImpact);
        }
    }
}
