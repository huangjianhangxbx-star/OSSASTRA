/* 脚本：CombatStatusEffects
 * 功能：中立受控状态适配；眩晕、减速及受击推移不依赖玩家/敌人根类型。
 * 边界：没有Update，没有Transform/CharacterController.Move，没有第二个HP；状态只能影响原运动端口。
 * 协作：根在动作采样前Tick，ControlChanged撤销未释放动作；Movement消费本帧受迫速度。
 */
using System;
using UnityEngine;
namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatStatusEffects:MonoBehaviour
    {
        [SerializeField,Range(0,1)] private float impactWeight=1;
        private readonly CombatStatusState state=new CombatStatusState();
        private Vector3 knockVelocity,frameVelocity;
        private float knockRemaining;
        public event Action ControlChanged;
        public bool BlocksIntent=>isActiveAndEnabled&&(state.IsStunned||knockRemaining>0||frameVelocity.sqrMagnitude>0);
        public bool IsStunned=>isActiveAndEnabled&&state.IsStunned;
        public bool HasForcedMotion=>isActiveAndEnabled&&frameVelocity.sqrMagnitude>0;
        public Vector3 ForcedVelocity=>HasForcedMotion?frameVelocity:Vector3.zero;
        public float SpeedScale=>isActiveAndEnabled?(float)state.SpeedScale:1;
        public void ApplySlow(Guid id,float ratio,float seconds){if(isActiveAndEnabled)state.ApplySlow(id,ratio,seconds);}
        public void ApplyStun(Guid id,float seconds)
        {if(isActiveAndEnabled&&state.ApplyStun(id,seconds))ControlChanged?.Invoke();}
        public void ApplyKnockback(Guid id,Vector3 displacement,float seconds)
        {
            if(!isActiveAndEnabled||id==Guid.Empty||knockRemaining>0||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds)||impactWeight<=0)return;
            displacement.y=0;var distance=displacement.magnitude;if(distance<=0||float.IsNaN(distance)||float.IsInfinity(distance))return;
            knockVelocity=displacement*impactWeight/seconds;knockRemaining=seconds;ControlChanged?.Invoke();
        }
        public void Tick(float delta)
        {
            if(!isActiveAndEnabled||delta<=0)return;bool blocked=BlocksIntent;frameVelocity=Vector3.zero;
            state.Tick(delta);
            if(knockRemaining>0){var step=Mathf.Min(delta,knockRemaining);frameVelocity=knockVelocity*(step/delta);knockRemaining=Mathf.Max(0,knockRemaining-delta);}
            if(blocked!=BlocksIntent)ControlChanged?.Invoke();
        }
        public void StopForcedMotion(){knockRemaining=0;knockVelocity=frameVelocity=Vector3.zero;ControlChanged?.Invoke();}
        public void Clear(){state.Clear();knockRemaining=0;knockVelocity=frameVelocity=Vector3.zero;ControlChanged?.Invoke();}
        public void Remove(Guid id){bool blocked=BlocksIntent;state.Remove(id);if(blocked!=BlocksIntent)ControlChanged?.Invoke();}
        private void OnDisable()=>Clear();
    }
}
