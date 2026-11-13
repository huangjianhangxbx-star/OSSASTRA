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
        public override bool IsActive => IsAirborne || traversal?.OwnsAirState == true || (owner.StateMovement != null && owner.StateMovement.IsMoving);
        public override string CurrentStateName => traversal?.OwnsAirState == true ? TraversalMode.ToString() :
            IsAirborne ? "Airborne/" + jump.Phase : IsActive ? "Move" : "Idle";
        public int AcceptedJumps { get; private set; }
        public SideViewTraversalMode TraversalMode => traversal?.Mode ??
            (IsAirborne ? SideViewTraversalMode.Airborne : SideViewTraversalMode.Grounded);
        public int TraversalRevision => traversal?.Revision ?? 0;
        public int TraversalWallSide => traversal?.WallSide ?? 0;
        public string TraversalLastRejection => traversal?.LastRejection;
        internal bool TraversalBlocksAttack => traversal?.BlocksAttack == true;

        private enum JumpLaunch { None, Ordinary, Wall }

        private readonly OrdinaryJumpCycle jump = new OrdinaryJumpCycle();
        private SideViewTraversal traversal;
        private CharacterInputButton pendingGesture, launchGesture;
        private CharacterActionRequest pendingRequest;
        private bool pendingTap, launchHeld, anyHeld;
        private int pendingAge, inputGeneration, controlRevision;
        private uint contactRevision;
        private float accumulator;

        public override bool AllowsRequest(CharacterActionKind kind, out string reason)
            => AllowsRequest(kind, kind == CharacterActionKind.Main || kind == CharacterActionKind.Offhand, out reason);

        internal bool AllowsRequest(CharacterActionKind kind, bool isAttack, out string reason)
        {
            reason = null;
            if (TraversalBlocksAttack && (isAttack || kind == CharacterActionKind.Jump || kind == CharacterActionKind.Dodge))
            {
                reason = "ledge-action-blocked";
                return false;
            }
            if (kind != CharacterActionKind.Jump) return true;
            return AllowsJump(out _, out reason);
        }

        private bool AllowsJump(out JumpLaunch launch, out string reason)
        {
            var motor = owner.StateMovement;
            var profile = motor?.JumpProfile;
            // 普通地面和土狼跳优先。墙跳的能力和空间资格只从同一子状态取得。
            launch = profile == null ? JumpLaunch.None :
                jump.CanLaunch(profile.ledgeGraceSteps) ? JumpLaunch.Ordinary :
                traversal?.CanKick(profile.traversal) == true ? JumpLaunch.Wall : JumpLaunch.None;
            reason = null;
            if (launch == JumpLaunch.None)
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
                motor.ResetSideViewPose();
                InvalidateInput();
                traversal?.SuspendInput();
                return;
            }

            var profile = motor.JumpProfile;
            if (profile.traversal?.enabled == true && traversal == null) traversal = new SideViewTraversal(motor);
            if (profile.traversal?.enabled != true) traversal = null;
            var frame = owner.DataBus.Input;
            SampleJump(frame);
            bool playerMovement = owner.HasPlayerInput && owner.EffectiveIntentKind == CharacterIntentKind.Player &&
                frame.jumpInputAvailable && !frame.jumpRequestsBlocked;
            int side = frame.move.x > .25f ? 1 : frame.move.x < -.25f ? -1 : 0;
            int device = playerMovement ? owner.StateInput.InputSources.MoveDeviceId : -1;
            // 即使本渲染帧没有执行运动步，也要观察松键与切控，不能把旧方向留给下一个抓边。
            traversal?.ObserveInput(side, playerMovement, owner.ActionControlRevision, device);
            float step = Mathf.Clamp(profile.stepSeconds, .005f, .05f);
            accumulator = Mathf.Min(accumulator + elapsed, step * 8f);
            var before = motor.transform.position;
            int steps = 0;
            // 仍在 Root 集中更新内步进，输入只采一次，每物理步只由原移动器提交一次。
            while (steps < 8 && accumulator + .000001f >= step)
            {
                bool canTraverse = canMove && owner.DataBus.MoveReady && !owner.DataBus.ActionActive &&
                    owner.DataBus.MotionOverride?.IsActive != true && owner.StateStatusEffects?.BlocksIntent != true &&
                    owner.StatePosture?.IsStaggered != true && owner.StateOffhand?.IsAccepting != true &&
                    (owner.StateAbilities as CharacterAbilities)?.OccupiesTraversal != true;
                traversal?.SampleContact(canTraverse, profile.traversal);
                bool ordinaryJump = TryLaunch(profile) == JumpLaunch.Ordinary;
                bool wasHanging = TraversalBlocksAttack;
                var motion = ordinaryJump ? default : traversal?.Plan(canTraverse, step, profile) ?? default;
                if (!wasHanging && TraversalBlocksAttack)
                {
                    ClearPending();
                    jump.EnterLedge();
                    owner.StateArbitrator.Combat.ClearBuffer();
                    if (owner.DataBus.Input.attackHeld) owner.StateArbitrator.Combat.SuppressInput();
                }
                var command = jump.Step(profile.sustainSteps, profile.minimumSustainSteps);
                bool wasRising = jump.Phase == OrdinaryJumpPhase.Rising;
                bool held = launchGesture.HasIdentity ? launchHeld : anyHeld;
                float gravity = jump.GravityScale(step, held, profile.hangInitialGravity, profile.hangGravityRamp);
                motor.TickSideView(canMove, step, command, gravity, ordinaryJump, motion);
                traversal?.Observe(profile.traversal);
                bool bumped = wasRising && (motor.LastCollision & CollisionFlags.Above) != 0;
                jump.Observe(wasRising, motor.Grounded, bumped, motor.VerticalSpeed > 0f, profile.headLockSteps);
                if (motor.Grounded) { launchGesture = default; launchHeld = false; }
                if (pendingGesture.HasIdentity && (pendingTap || ++pendingAge > profile.queueSteps)) ClearPending();
                accumulator = Mathf.Max(0f, accumulator - step);
                steps++;
            }
            if (steps > 0) motor.PublishSideViewVelocity(before, step * steps);
            motor.SetSideViewPresentationFraction(accumulator / step);
        }

        private void SampleJump(CharacterInputFrame frame)
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
                if (button.Pressed && !frame.jumpRequestsBlocked && !TraversalBlocksAttack)
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

        private JumpLaunch TryLaunch(CharacterJumpProfile profile)
        {
            if (!pendingGesture.HasIdentity || TraversalBlocksAttack || !AllowsJump(out var launch, out _))
                return JumpLaunch.None;
            bool accepted = owner.Actions.TryCommit(pendingRequest, 0f, () =>
            {
                if (launch == JumpLaunch.Wall)
                {
                    jump.LaunchWall();
                    traversal.Kick(profile.traversal);
                }
                else jump.Launch();
                launchGesture = pendingGesture;
                launchHeld = !pendingTap;
                if (pendingTap) jump.Release();
                AcceptedJumps++;
                return pendingRequest.RequestId;
            }, out _);
            // 当前准备就绪时只向账本提交一次。延迟资格仍由前置 gate 和缓冲步数控制。
            ClearPending();
            return accepted ? launch : JumpLaunch.None;
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
            traversal?.Reset();
            accumulator = 0f;
        }
    }
}
