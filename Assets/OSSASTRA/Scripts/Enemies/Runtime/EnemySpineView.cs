/* 敌方二维事实适配：敌根LateUpdate显式调用；死亡/破势/受击/动作/移动顺序只决定表现。
 * 没有CharacterRoot或玩家Bus依赖；可选观察端口只替换显示左右，缺省仍沿世界X；生命退场仍由CombatVitals负责。
 */
using System;
using Ossastra.AnimationCore;
using Ossastra.AnimationPresentation;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemySpineView : MonoBehaviour,IEntityPresentation
    {
        [SerializeField] private EntitySpinePresenter presenter;
        private EnemyRoot root;private EnemyMovement movement;private CombatPosture posture;private float facing=1;
        private EnemyViewSpaceAdapter viewSpace;
        private void Awake(){root=GetComponent<EnemyRoot>();movement=GetComponent<EnemyMovement>();posture=GetComponent<CombatPosture>();viewSpace=GetComponent<EnemyViewSpaceAdapter>();}
        public void Present()
        {
            if(!isActiveAndEnabled||root==null||root.Vitals==null||presenter==null)return;
            var velocity=movement==null?Vector3.zero:movement.Velocity;
            var direction=root.ActionActive?root.AttackDirection:velocity;
            if(viewSpace!=null&&viewSpace.IsEffective)facing=viewSpace.Facing;
            else if(Mathf.Abs(direction.x)>.01f)facing=Mathf.Sign(direction.x);
            var semantic=!root.Vitals.IsAlive?AnimationSemantic.Death:posture!=null&&posture.IsStaggered?AnimationSemantic.PostureBroken:
                root.Vitals.IsHitReacting?AnimationSemantic.HitReaction:root.ActionActive?AnimationSemantic.BasicAttack:
                velocity.sqrMagnitude>.001f?AnimationSemantic.Move:AnimationSemantic.Idle;
            presenter.Present(new Ossastra.AnimationCore.AnimationIntent(root.ActorId,semantic,"enemy-facts",root.ActionId,root.PresentationGeneration),facing,root.IsSimulationSuspended);
        }
    }
}
