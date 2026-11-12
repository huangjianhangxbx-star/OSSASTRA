/* 运动状态：方向采样在请求前，唯一真实位移在动作事实之后，使用本帧最新许可。 */
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class MovementStateMachine : CharacterStateMachine
    {
        public override string StateMachineName => "MovementStateMachine";
        public bool IsAirborne => owner.StateMovement != null && owner.StateMovement.IsSideView &&
            (jump.Phase == OrdinaryJumpPhase.Rising || jump.Phase == OrdinaryJumpPhase.Falling);
        public override bool IsActive => IsAirborne || (owner.StateMovement != null && owner.StateMovement.IsMoving);
        public override string CurrentStateName => IsAirborne ? "Airborne/" + jump.Phase : IsActive ? "Move" : "Idle";
        public int AcceptedJumps { get; private set; }

        private readonly OrdinaryJumpCycle jump = new OrdinaryJumpCycle();
        private CharacterInputButton pendingGesture, launchGesture;
        private CharacterActionRequest pendingRequest;
        private bool pendingTap, launchHeld, anyHeld;
        private int pendingAge, inputGeneration, controlRevision;
        private uint contactRevision;
        private float accumulator;

        public override bool AllowsRequest(CharacterActionKind kind, out string reason)
        {
            reason = null;
            if (kind != CharacterActionKind.Jump) return true;
            var motor = owner.StateMovement;
            if (motor == null || !motor.IsSideView || !jump.CanLaunch(motor.JumpProfile.ledgeGraceSteps))
                reason = "jump-no-support";
            else if (!owner.DataBus.AllowsMovement || !owner.DataBus.MoveReady ||
                owner.DataBus.MotionOverride?.IsActive == true)
                reason = "jump-motion-not-ready";
            else if (!motor.Grounded && !motor.HasJumpHeadroom())
                reason = "jump-headroom";
            return reason == null;
        }
        public void ResolveDirections(CharacterInputFrame frame)
        {
            var canMove = owner.StateFocused && !owner.IsPaused && owner.DataBus.AllowsMovement && owner.DataBus.MoveReady;
            if (owner.StateFocused && !owner.IsPaused)
                owner.StateOrienter.ResolveDirections(frame, owner.StatePointerDirection(frame), canMove, owner.StateInput.HasPlayerControl);
        }
        public void ResolvePresentation()
            =>owner.StateOrienter.ResolvePresentation(owner.DataBus,owner.StateAbilities,owner.StateInput.HasPlayerControl);
        public override void Update(float deltaTime)
        {
            var canMove = owner.StateFocused && !owner.IsPaused && owner.DataBus.AllowsMovement && (owner.DataBus.MoveReady || owner.DataBus.AbilityMotionActive);
            float elapsed = owner.IsSimulationSuspended ? 0f : deltaTime;
            var motor = owner.StateMovement;
            if (motor == null || !motor.IsSideView)
            {
                owner.StateMover.Tick(canMove, elapsed);
                return;
            }
            if (contactRevision != motor.ContactRevision)
            {
                ResetJump();
                contactRevision = motor.ContactRevision;
            }
            if (elapsed <= 0f)
            {
                accumulator = 0f;
                InvalidateInput();
                return;
            }

            var profile = motor.JumpProfile;
            SampleJump(owner.DataBus.Input, profile);
            float step = Mathf.Clamp(profile.stepSeconds, .005f, .05f);
            accumulator = Mathf.Min(accumulator + elapsed, step * 8f);
            var before = motor.transform.position;
            int steps = 0;
            // 仍在 Root 集中更新内步进，输入只采一次，每物理步只由原移动器提交一次。
            while (steps < 8 && accumulator + .000001f >= step)
            {
                TryLaunch();
                var command = jump.Step(profile.sustainSteps, profile.minimumSustainSteps);
                bool wasRising = jump.Phase == OrdinaryJumpPhase.Rising;
                bool held = launchGesture.HasIdentity ? launchHeld : anyHeld;
                float gravity = jump.GravityScale(step, held, profile.hangInitialGravity, profile.hangGravityRamp);
                motor.TickSideView(canMove, step, command, gravity);
                bool bumped = wasRising && (motor.LastCollision & CollisionFlags.Above) != 0;
                jump.Observe(wasRising, motor.Grounded, bumped, motor.VerticalSpeed > 0f, profile.headLockSteps);
                if (motor.Grounded) { launchGesture = default; launchHeld = false; }
                if (pendingGesture.HasIdentity && (pendingTap || ++pendingAge > profile.queueSteps)) ClearPending();
                accumulator = Mathf.Max(0f, accumulator - step);
                steps++;
            }
            if (steps > 0) motor.PublishSideViewVelocity(before, step * steps);
        }

        private void SampleJump(CharacterInputFrame frame, CharacterJumpProfile profile)
        {
            int revision = owner.ActionControlRevision;
            if (frame.jumpInputGeneration != inputGeneration)
                InvalidateInput();
            else if (revision != controlRevision)
                ClearPending();
            controlRevision = revision;
            inputGeneration = frame.jumpInputGeneration;
            if (!frame.jumpInputAvailable || !owner.HasPlayerInput || !owner.CanSimulateIntent)
            {
                InvalidateInput();
                return;
            }
            anyHeld = false;
            if (frame.jumpRequestsBlocked) ClearPending();
            if (frame.jumpButtons == null) return;
            foreach (var button in frame.jumpButtons)
            {
                if (!button.HasIdentity) continue;
                anyHeld |= button.Held;
                if (button.Pressed && !frame.jumpRequestsBlocked && (!button.Released || jump.CanLaunch(profile.ledgeGraceSteps)))
                {
                    pendingGesture = button;
                    pendingRequest = owner.Actions.CreateRequest(CharacterActionKind.Jump, CharacterActionSource.Player, "jump-input");
                    pendingAge = 0;
                    pendingTap = button.Released;
                }
                if (!button.Released) continue;
                if (launchGesture.SameGesture(button))
                {
                    jump.Release();
                    launchHeld = false;
                }
                if (pendingGesture.SameGesture(button) && !pendingTap) ClearPending();
            }
        }

        private void TryLaunch()
        {
            if (!pendingGesture.HasIdentity || !AllowsRequest(CharacterActionKind.Jump, out _)) return;
            owner.Actions.TryCommit(pendingRequest, 0f, () =>
            {
                jump.Launch();
                launchGesture = pendingGesture;
                launchHeld = !pendingTap;
                if (pendingTap) jump.Release();
                AcceptedJumps++;
                return pendingRequest.RequestId;
            }, out _);
            // 当前准备就绪时只向账本提交一次。延迟资格仍由前置 gate 和缓冲步数控制。
            ClearPending();
        }

        private void ClearPending()
        {
            pendingGesture = default;
            pendingRequest = default;
            pendingAge = 0;
            pendingTap = false;
        }

        private void InvalidateInput()
        {
            ClearPending();
            launchGesture = default;
            launchHeld = anyHeld = false;
            jump.CancelHold();
        }

        internal void ResetJump()
        {
            InvalidateInput();
            jump.Reset();
            accumulator = 0f;
        }
    }
}
