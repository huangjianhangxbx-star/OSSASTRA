using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    // Per-character read facade; referenced modules remain the sole mutable-state owners.
    public sealed class CharacterDataBus
    {
        private readonly CharacterInputHandler input;
        private readonly CharacterMovement movement;
        private readonly CharacterOrientation orientation;
        private readonly BasicActionRuntime actionRuntime;
        private readonly ControlAuthority controlAuthority;

        public string ActorId { get; }
        public CharacterInputFrame Input => input.CurrentFrame;
        public bool IsMoving => movement.IsMoving;
        public Vector3 Velocity => movement.Velocity;
        public Vector3 Direction => orientation.Direction;
        public float FacingX => orientation.FacingX;
        public bool ActionActive => actionRuntime.IsActive;
        public Guid ActionId => actionRuntime.ActionId;
        public bool MoveReady => actionRuntime.MoveReady;
        public bool AttackReady => actionRuntime.AttackReady;
        public bool AllowsMovement => controlAuthority.Allows(ControlChannel.Move);
        public bool AllowsAttack => controlAuthority.Allows(ControlChannel.Attack);

        public CharacterDataBus(string actorId, CharacterInputHandler input, CharacterMovement movement,
            CharacterOrientation orientation, BasicActionRuntime actionRuntime, ControlAuthority controlAuthority)
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
