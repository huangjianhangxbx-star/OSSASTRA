using System;

namespace Ossastra.CharacterCore
{
    public enum PostureReactionPhase { Inactive, Launched, Downed, GettingUp }

    public readonly struct PostureReactionStep
    {
        public float Horizontal { get; }
        public float? LaunchVertical { get; }

        public PostureReactionStep(float horizontal, float? launchVertical)
        {
            Horizontal = horizontal;
            LaunchVertical = launchVertical;
        }
    }

    // The fixed motor reports support. A reaction cannot recover by time spent in the air.
    public sealed class PostureReactionState
    {
        public PostureReactionPhase Phase { get; private set; }
        public Guid ActionId { get; private set; }
        public bool IsActive => Phase != PostureReactionPhase.Inactive;
        private float horizontalSpeed, horizontalRemaining, downSeconds, riseSeconds, phaseRemaining;
        private float? launchVertical;

        public bool Begin(Guid actionId, int direction, float runSpeed, float jumpSpeed,
            float horizontalSeconds, float downSeconds, float riseSeconds)
        {
            if (IsActive || actionId == Guid.Empty) return false;
            ActionId = actionId;
            Phase = PostureReactionPhase.Launched;
            horizontalSpeed = (direction < 0 ? -1f : 1f) * runSpeed * .35f;
            horizontalRemaining = horizontalSeconds;
            launchVertical = jumpSpeed * .4f;
            this.downSeconds = downSeconds;
            this.riseSeconds = riseSeconds;
            return true;
        }

        public PostureReactionStep PlanStep(float delta)
        {
            if (Phase != PostureReactionPhase.Launched || delta <= 0) return default;
            float horizontalTime = Math.Min(horizontalRemaining, delta);
            horizontalRemaining -= horizontalTime;
            var step = new PostureReactionStep(horizontalSpeed * (horizontalTime / delta), launchVertical);
            launchVertical = null;
            return step;
        }

        public void ObserveStep(float delta, bool supported, bool horizontalBlocked)
        {
            if (!IsActive || delta <= 0) return;
            if (horizontalBlocked) horizontalRemaining = 0;
            if (!supported)
            {
                Phase = PostureReactionPhase.Launched;
                return;
            }
            if (Phase == PostureReactionPhase.Launched)
            {
                horizontalRemaining = 0;
                Phase = PostureReactionPhase.Downed;
                phaseRemaining = downSeconds;
                return;
            }

            phaseRemaining -= delta;
            if (phaseRemaining > .000001f) return;
            if (Phase == PostureReactionPhase.Downed)
            {
                Phase = PostureReactionPhase.GettingUp;
                phaseRemaining = riseSeconds;
            }
            else Reset();
        }

        public void Reset()
        {
            Phase = PostureReactionPhase.Inactive;
            ActionId = Guid.Empty;
            horizontalSpeed = horizontalRemaining = phaseRemaining = 0;
            launchVertical = null;
        }
    }
}
