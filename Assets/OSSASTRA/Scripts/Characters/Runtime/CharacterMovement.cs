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
        public bool IsMoving { get; private set; }
        public Vector3 Velocity { get; private set; }

        public bool Initialize(CharacterDataBus dataBus)
        {
            if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
            this.dataBus = dataBus;
            body = GetComponent<CharacterController>();
            return body != null;
        }

        public void Tick(bool allowed, float delta)
        {
            IsMoving = false;
            Velocity = Vector3.zero;
            if (dataBus == null || body == null || !body.enabled || delta <= 0) return;
            var move = allowed ? Vector2.ClampMagnitude(dataBus.Input.move, 1) : Vector2.zero;
            var before = transform.position;
            // 平时与闪避共同走这一处Move；覆盖速度仍接受本体移动权限和真实墙体碰撞。
            var velocity=allowed&&dataBus.MotionOverride!=null&&dataBus.MotionOverride.IsActive
                ?dataBus.MotionOverride.MotionVelocity:new Vector3(move.x,0,move.y)*speed;
            body.Move((velocity + Vector3.down * groundStick) * delta);
            // 用碰撞处理后的实际位移报告运动，而不是把“按住移动键”当成真的跑动。
            var displacement = transform.position - before;
            displacement.y = 0;
            Velocity = displacement / delta;
            IsMoving = displacement.sqrMagnitude > .000001f;
        }

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
