/* 脚本：CharacterDodge
 * 功能：Root调用的有限闪避请求、方向快照与机动速度；无敌只在配置窗口内。
 * 边界：不自行Update/Move，不重置主手时钟；所有参数为SAMPLE。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterDodge:MonoBehaviour,ICharacterMotionOverride,ICombatEvasion
    {
        [SerializeField,Min(.01f)] private float duration=.45f,distance=1.5f;
        [SerializeField,Min(0)] private float invulnerableFrom=.12f,invulnerableTo=.32f,cost=22;
        private DodgeState state;
        private CharacterRoot owner;
        private CharacterStamina stamina;
        private Vector3 direction;
        public Guid ActionId {get;private set;}
        public string LastResult {get;private set;}="无";
        public bool IsActive=>isActiveAndEnabled&&state!=null&&state.IsActive;
        public bool IsEvading=>IsActive&&owner!=null&&owner.CanSimulateIntent&&state.IsInvulnerable;
        public Vector3 MotionVelocity=>IsActive?direction*(distance/duration):Vector3.zero;
        public void Initialize(CharacterRoot root,CharacterStamina resource){owner=root;stamina=resource;state=new DodgeState(duration,invulnerableFrom,invulnerableTo);}
        public bool Request(Vector2 move,bool playerInput)
        {
            if(!isActiveAndEnabled||state==null||owner==null||!owner.CanSimulateIntent||!playerInput||IsActive||
                !owner.DataBus.AllowsMovement||!owner.DataBus.MoveReady||stamina==null||!stamina.CanSpend(cost))
            {LastResult="拒绝：控制/动作/体力资格";return false;}
            direction=move.sqrMagnitude>.0001f?new Vector3(move.x,0,move.y).normalized:owner.DataBus.AttackDirection;
            direction.y=0;if(direction.sqrMagnitude<.0001f){LastResult="无可靠方向";return false;}
            direction.Normalize();
            var request=owner.Actions.CreateRequest(CharacterActionKind.Dodge,CharacterActionSource.Player);
            if(!owner.Actions.TryCommit(request,cost,()=>state.TryStart(owner.SimulationTime)?request.RequestId:Guid.Empty,out var reason))
            {LastResult="拒绝："+reason;return false;}
            ActionId=request.RequestId;
            LastResult="Accepted SAMPLE，未配置专用动画";return true;
        }
        public void Tick(double time){state?.Tick(time);}
        public void Cancel(){state?.Cancel();}
        private void OnDisable(){Cancel();}
    }
}
