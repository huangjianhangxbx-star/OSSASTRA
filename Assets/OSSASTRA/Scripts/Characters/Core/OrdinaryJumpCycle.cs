/*
 * 普通跳跃的子阶段与步预算。由 MovementStateMachine 推进，碰撞和速度仍由原移动器拥有。
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
        private bool startedHang;
        private float hangGravity = 1f;

        public bool CanLaunch(int graceSteps)
            => Phase == OrdinaryJumpPhase.Grounded ||
                (headLock == 0 && leftSupport && !used && naturalAirSteps <= graceSteps);

        public void Launch()
        {
            used = true;
            hold = true;
            release = false;
            steps = 0;
            Phase = OrdinaryJumpPhase.Rising;
        }

        public void Release() { if (used && hold) release = true; }

        public void CancelHold()
        {
            hold = false;
            release = false;
        }

        public JumpVerticalCommand Step(int sustainSteps, int minSteps)
        {
            if (Phase != OrdinaryJumpPhase.Rising || !hold) return JumpVerticalCommand.Gravity;
            if (release && steps >= minSteps)
            {
                return JumpVerticalCommand.Cut;
            }
            return steps++ < sustainSteps ? JumpVerticalCommand.Sustain : JumpVerticalCommand.Gravity;
        }

        public float GravityScale(float dt, bool jumpHeld, float initial, float ramp)
        {
            if (Phase != OrdinaryJumpPhase.Falling) return 1f;
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
                CancelHold();
                headLock = headLockSteps;
                leftSupport = false;
            }
            if (supported && !hitHead)
            {
                Phase = OrdinaryJumpPhase.Grounded;
                used = leftSupport = startedHang = false;
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
            if (Phase == OrdinaryJumpPhase.Falling) CancelHold();
        }

        public void Reset()
        {
            Phase = OrdinaryJumpPhase.Uninitialized;
            steps = naturalAirSteps = headLock = 0;
            used = leftSupport = startedHang = false;
            hangGravity = 1f;
            CancelHold();
        }
    }
}
