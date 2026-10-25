/*
 * 脚本：CharacterDataBus
 * 功能：为单个角色提供输入、运动、朝向、动作和权限的只读访问门面。
 * 边界：状态仍由原模块唯一写入；不复制可写HP、动作时钟或全局控制状态。
 * 协作：Root创建；Movement／Spine及消费者实时读取接口拥有者。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    // Per-character read facade; referenced modules remain the sole mutable-state owners.
    public sealed class CharacterDataBus
    {
        private readonly ICharacterInputSource input;
        private readonly ICharacterMovement movement;
        private readonly ICharacterOrientation orientation;
        private readonly BasicActionRuntime actionRuntime;
        private readonly ControlAuthority controlAuthority;

        public string ActorId { get; }
        public CharacterInputFrame Input => input.CurrentFrame;
        public bool IsMoving => movement.IsMoving;
        public Vector3 Velocity => movement.Velocity;
        public Vector3 AttackDirection => orientation.AttackDirection;
        public Vector3 PresentationDirection => orientation.PresentationDirection;
        public Vector3 LastMoveDirection => orientation.LastMoveDirection;
        public string DirectionReason => orientation.DirectionReason;
        public bool UsesMovementAim => orientation.UsesMovementAim;
        public Vector3 ResolveDeliveryDirection(Vector3 acceptedDirection,bool frozenMotion) => orientation.ResolveDeliveryDirection(acceptedDirection,frozenMotion);
        public float VisualFacingX => orientation.VisualFacingX;
        public bool ActionActive => actionRuntime.IsActive;
        public Guid ActionId => actionRuntime.ActionId;
        public bool MoveReady => actionRuntime.MoveReady;
        public bool AttackReady => actionRuntime.AttackReady;
        public bool AllowsMovement => controlAuthority.Allows(ControlChannel.Move);
        public bool AllowsAttack => controlAuthority.Allows(ControlChannel.Attack);
        public ICharacterStamina Stamina {get;private set;}
        private ICharacterMotionOverride primaryMotion,abilityMotion,mainMotion;
        public bool AbilityMotionActive=>abilityMotion!=null&&abilityMotion.IsActive||mainMotion!=null&&mainMotion.IsActive;
        public ICharacterMotionOverride MotionOverride=>primaryMotion!=null&&primaryMotion.IsActive?primaryMotion:
            abilityMotion!=null&&abilityMotion.IsActive?abilityMotion:mainMotion;
        internal void BindResources(ICharacterStamina stamina,ICharacterMotionOverride motion){Stamina=stamina;primaryMotion=motion;}
        internal void BindAbilityMotion(ICharacterMotionOverride motion){abilityMotion=motion;}
        internal void BindMainMotion(ICharacterMotionOverride motion){mainMotion=motion;}

        public CharacterDataBus(string actorId, ICharacterInputSource input, ICharacterMovement movement,
            ICharacterOrientation orientation, BasicActionRuntime actionRuntime, ControlAuthority controlAuthority)
        {
            if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            if (orientation == null) throw new ArgumentNullException(nameof(orientation));
            if (actionRuntime == null) throw new ArgumentNullException(nameof(actionRuntime));
            if (controlAuthority == null) throw new ArgumentNullException(nameof(controlAuthority));
            ActorId = actorId;
            this.input = input;
            this.movement = movement;
            this.orientation = orientation;
            this.actionRuntime = actionRuntime;
            this.controlAuthority = controlAuthority;
        }
    }
}
