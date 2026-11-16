/*
 * 普通跳跃的子阶段与步预算。外部空中动作切断旧跳跃输入，碰撞和速度仍由原移动器拥有。
 */
namespace Ossastra.CharacterCore
{
    public enum OrdinaryJumpPhase { Uninitialized, Grounded, Rising, Falling }
    public enum JumpVerticalCommand { Gravity, Sustain, Cut }

    public sealed class OrdinaryJumpCycle
    {
        public OrdinaryJumpPhase Phase { get; private set; }
        private int steps, naturalAirSteps, headLock;
        private bool used, leftSupport, hold, release;
        private bool startedHang, externalFlight, automaticAscent;
        private float hangGravity = 1f;

        public bool CanLaunch(int graceSteps)
            => Phase == OrdinaryJumpPhase.Grounded ||
                (headLock == 0 && leftSupport && !used && naturalAirSteps <= graceSteps);

        public void Launch()
        {
            used = true;
            automaticAscent = false;
            hold = true;
            release = false;
            steps = 0;
            Phase = OrdinaryJumpPhase.Rising;
        }

        /// <summary>墙跳上行初速由墙边状态提供，不重新进入普通跳持续提速。</summary>
        public void LaunchWall()
        {
            Launch();
            CancelHold();
        }

        /// <summary>命中腾起复用一次完整普通上升，且不接受跳跃键松开截速。</summary>
        public void LaunchRebound()
        {
            EnterExternalAirMotion();
            Launch();
            automaticAscent = true;
        }

        public void Release() { if (used && hold && !automaticAscent) release = true; }

        public void CancelHold()
        {
            hold = false;
            release = false;
        }

        public void EnterLedge()
        {
            leftSupport = automaticAscent = false;
            CancelHold();
        }

        /// <summary>外部位移接管本次空中过程，只有重新获得支撑才重开普通跳与长按重力。</summary>
        public void EnterExternalAirMotion()
        {
            used = externalFlight = true;
            automaticAscent = false;
            leftSupport = false;
            Phase = OrdinaryJumpPhase.Falling;
            CancelHold();
        }

        public JumpVerticalCommand Step(int sustainSteps, int minSteps)
        {
            if (Phase != OrdinaryJumpPhase.Rising || (!hold && !automaticAscent)) return JumpVerticalCommand.Gravity;
            if (release && steps >= minSteps)
            {
                return JumpVerticalCommand.Cut;
            }
            return steps++ < sustainSteps ? JumpVerticalCommand.Sustain : JumpVerticalCommand.Gravity;
        }

        public float GravityScale(float dt, bool jumpHeld, float initial, float ramp)
        {
            if (externalFlight || Phase != OrdinaryJumpPhase.Falling) return 1f;
            if (!startedHang)
            {
                startedHang = true;
                hangGravity = initial;
            }
            hangGravity = jumpHeld ? System.Math.Min(1f, hangGravity + ramp * dt) : 1f;
            return hangGravity;
        }

        public void Observe(bool wasRising, bool supported, bool hitHead, bool stillRising, int headLockSteps = 3)
        {
            if (headLock > 0) headLock--;
            if (hitHead)
            {
                automaticAscent = false;
                CancelHold();
                headLock = headLockSteps;
                leftSupport = false;
            }
            if (supported && !hitHead)
            {
                Phase = OrdinaryJumpPhase.Grounded;
                used = leftSupport = startedHang = externalFlight = automaticAscent = false;
                naturalAirSteps = steps = 0;
                hangGravity = 1f;
                CancelHold();
                return;
            }
            if (!hitHead && Phase == OrdinaryJumpPhase.Grounded && !wasRising)
            {
                leftSupport = true;
                naturalAirSteps = 0;
            }
            if (leftSupport && naturalAirSteps < int.MaxValue) naturalAirSteps++;
            Phase = stillRising && !hitHead ? OrdinaryJumpPhase.Rising : OrdinaryJumpPhase.Falling;
            if (Phase == OrdinaryJumpPhase.Falling)
            {
                automaticAscent = false;
                CancelHold();
            }
        }

        public void Reset()
        {
            Phase = OrdinaryJumpPhase.Uninitialized;
            steps = naturalAirSteps = headLock = 0;
            used = leftSupport = startedHang = externalFlight = automaticAscent = false;
            hangGravity = 1f;
            CancelHold();
        }
    }
}
