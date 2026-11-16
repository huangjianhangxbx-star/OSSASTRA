using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed class CharacterReactionStateMachine : CharacterStateMachine
    {
        private readonly PostureReactionState reaction = new PostureReactionState();
        public override string StateMachineName => "CharacterReactionStateMachine";
        public override string CurrentStateName => Phase.ToString();
        public override bool IsActive => reaction.IsActive;
        public PostureReactionPhase Phase => reaction.Phase;
        public Guid ActionId => reaction.ActionId;

        public override bool AllowsRequest(CharacterActionKind kind, out string reason)
        {
            reason = IsActive ? "posture-reaction" : null;
            return reason == null;
        }

        internal bool Begin(ResolvedPostureContact contact, float launchSeconds, float downSeconds, float riseSeconds)
        {
            var motor = owner.StateMovement;
            var profile = motor?.JumpProfile;
            if (!owner.IsAlive || motor == null || !motor.IsSideView || profile == null ||
                !reaction.Begin(contact.Outcome.ActionId, contact.DirectionX, profile.runSpeed,
                    profile.jumpSpeed, launchSeconds, downSeconds, riseSeconds))
                return false;

            owner.SetReactionControl(this, true);
            owner.StateStatusEffects?.SetPostureReactionActive(true);
            return true;
        }

        internal bool TryStep(CharacterMovement motor, float deltaTime)
        {
            if (!IsActive) return false;

            var motion = reaction.PlanStep(deltaTime);
            // The normal motor resolves gravity and actual support even though voluntary Move is locked.
            motor.TickSideView(false, deltaTime, JumpVerticalCommand.Gravity, 1f, false, reactionMotion: motion);
            var sideBlocked = motor.LastEnvironmentSideBlocked || (motor.LastCollision & CollisionFlags.Sides) != 0;
            reaction.ObserveStep(deltaTime, motor.Grounded, sideBlocked);
            if (!IsActive) ReleaseControl();
            return true;
        }

        internal void Stop()
        {
            if (!IsActive) return;
            reaction.Reset();
            ReleaseControl();
        }

        private void ReleaseControl()
        {
            owner.StateStatusEffects?.SetPostureReactionActive(false);
            owner.SetReactionControl(this, false);
        }

        // Movement invokes TryStep so landing and recovery share its fixed simulation clock.
        public override void Update(float deltaTime) { }
    }
}
