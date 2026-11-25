using UnityEngine;

namespace Ossastra.Characters
{
    public enum SideMovementDriver { Locomotion, Traversal, Ability, Rebound, Reaction, SwitchRetreat, ActionImpact }

    /// <summary>横版运动权威完成的一次物理步。显示插值和音频不会再推进模拟。</summary>
    public readonly struct SideMovementStep
    {
        public long StepId { get; }
        public long ContactEpoch { get; }
        public bool GroundedBefore { get; }
        public bool GroundedAfter { get; }
        public bool Launched { get; }
        public bool AllowsFootstep { get; }
        public float HorizontalDistance { get; }
        public float SimulatedSeconds { get; }
        public float LandingDownwardSpeed { get; }
        public Vector3 WorldPosition { get; }
        public Collider SupportCollider { get; }
        public Vector3? SupportPosition { get; }
        public Collider LaunchSupportCollider { get; }
        public Vector3? LaunchSupportPosition { get; }
        public Vector3 PreviousWorldPosition { get; }
        public SideMovementDriver Driver { get; }
        public bool HitHead { get; }
        public bool HorizontalBlocked { get; }
        public bool MovingSupport { get; }
        public float RequestedHorizontalSpeed { get; }
        public float ActualHorizontalSpeed => (WorldPosition.x - PreviousWorldPosition.x) / SimulatedSeconds;
        public float VerticalSpeed { get; }
        public float HorizontalInput { get; }
        public float InputHorizontalSpeed { get; }
        public float EffectHorizontalSpeed { get; }

        internal SideMovementStep(long stepId, long contactEpoch, bool groundedBefore,
            bool groundedAfter, bool launched, bool allowsFootstep, float horizontalDistance,
            float simulatedSeconds, float landingDownwardSpeed, Vector3 worldPosition, Collider supportCollider,
            Vector3? supportPosition, Collider launchSupportCollider, Vector3? launchSupportPosition,
            Vector3 previousWorldPosition, SideMovementDriver driver, bool hitHead, bool horizontalBlocked,
            bool movingSupport, float requestedHorizontalSpeed, float verticalSpeed, float horizontalInput, float inputHorizontalSpeed = 0, float effectHorizontalSpeed = 0)
        {
            StepId = stepId;
            ContactEpoch = contactEpoch;
            GroundedBefore = groundedBefore;
            GroundedAfter = groundedAfter;
            Launched = launched;
            AllowsFootstep = allowsFootstep;
            HorizontalDistance = horizontalDistance;
            SimulatedSeconds = simulatedSeconds;
            LandingDownwardSpeed = landingDownwardSpeed;
            WorldPosition = worldPosition;
            SupportCollider = supportCollider;
            SupportPosition = supportPosition;
            LaunchSupportCollider = launchSupportCollider;
            LaunchSupportPosition = launchSupportPosition;
            PreviousWorldPosition = previousWorldPosition;
            Driver = driver;
            HitHead = hitHead;
            HorizontalBlocked = horizontalBlocked;
            MovingSupport = movingSupport;
            RequestedHorizontalSpeed = requestedHorizontalSpeed;
            VerticalSpeed = verticalSpeed;
            HorizontalInput = horizontalInput;
            InputHorizontalSpeed = inputHorizontalSpeed;
            EffectHorizontalSpeed = effectHorizontalSpeed;
        }
    }
}
