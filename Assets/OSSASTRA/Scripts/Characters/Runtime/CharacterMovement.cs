/*
 * 脚本：CharacterMovement
 * 功能：依据角色输入与移动许可执行XZ位移、碰撞及贴地。
 * 边界：只管理运动；不决定攻击方向、不结算伤害。
 * 协作：Root初始化Bus并调用Tick；CharacterController实际位移决定速度和Run资格。
 */
using System;
using UnityEngine;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMovement : MonoBehaviour, ICharacterMovement
    {
        [SerializeField, Min(.01f)] private float speed = 3;
        [SerializeField, Min(.01f)] private float groundStick = 2;
        private CharacterController body;
        private CharacterDataBus dataBus;
        private Ossastra.Combat.CombatStatusEffects statusEffects;
        private ICharacterLocomotionScale locomotionScale;
        private ICharacterEnvironmentClearance environmentClearance;
        public bool IsMoving { get; private set; }
        public Vector3 Velocity { get; private set; }
        // 只读诊断：区分未获许可、未请求速度与真实碰撞截断，不参与运动决策。
        public bool LastAllowed {get;private set;}
        public Vector3 RequestedVelocity {get;private set;}
        public bool LastEnvironmentSideBlocked {get;private set;}
        internal void BindEnvironmentClearance(ICharacterEnvironmentClearance clearance) { environmentClearance = clearance; }

        public bool Initialize(CharacterDataBus dataBus)
        {
            if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
            this.dataBus = dataBus;
            body = GetComponent<CharacterController>();
            statusEffects=GetComponent<Ossastra.Combat.CombatStatusEffects>();
            locomotionScale=GetComponent<CharacterAbilities>();
            return body != null;
        }

        public void Tick(bool allowed, float delta)
        {
            Stop();LastAllowed=allowed;
            if (dataBus == null || body == null || !body.enabled || delta <= 0) return;
            var move = allowed ? Vector2.ClampMagnitude(dataBus.Input.move, 1) : Vector2.zero;
            var before = transform.position;
            // 平时与闪避共同走这一处Move；覆盖速度仍接受本体移动权限和真实墙体碰撞。
            var velocity=allowed&&dataBus.MotionOverride!=null&&dataBus.MotionOverride.IsActive
                ?dataBus.MotionOverride.MotionVelocity:new Vector3(move.x,0,move.y)*speed*(locomotionScale?.LocomotionScale??1);
            // 受迫位移优先于自愿移动，但仍只经过本方法一次Move和墙体碰撞。
            if(statusEffects!=null)velocity=statusEffects.HasForcedMotion?statusEffects.ForcedVelocity:
                statusEffects.BlocksIntent?Vector3.zero:velocity*statusEffects.SpeedScale;
            RequestedVelocity=velocity;
            var requestedDisplacement=(velocity + Vector3.down * groundStick) * delta;
            var constrained=environmentClearance==null
                ?new CharacterClearanceResult(requestedDisplacement,false):environmentClearance.Constrain(requestedDisplacement);
            LastEnvironmentSideBlocked=constrained.SideBlocked;
            var collision=body.Move(constrained.Displacement);
            if(((collision&CollisionFlags.Sides)!=0||LastEnvironmentSideBlocked)&&statusEffects!=null&&statusEffects.HasForcedMotion)statusEffects.StopForcedMotion();
            // 用碰撞处理后的实际位移报告运动，而不是把“按住移动键”当成真的跑动。
            var displacement = transform.position - before;
            displacement.y = 0;
            Velocity = displacement / delta;
            IsMoving = displacement.sqrMagnitude > .000001f;
        }

        /// <summary>只清运动事实，不Move、不改坐标；死亡/禁用路径也必须形成真实退出边沿。</summary>
        public void Stop()
        { LastAllowed=false;RequestedVelocity=Vector3.zero;LastEnvironmentSideBlocked=false;IsMoving=false;Velocity=Vector3.zero; }
        private void OnDisable(){Stop();}

        public void Teleport(Vector3 position)
        {
            if (body == null) return;
            var wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = position;
            body.enabled = wasEnabled;
            IsMoving = false;
            Velocity = Vector3.zero;
        }
    }
}
