using UnityEngine;

namespace Ossastra.Characters
{
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

        internal SideMovementStep(long stepId, long contactEpoch, bool groundedBefore,
            bool groundedAfter, bool launched, bool allowsFootstep, float horizontalDistance,
            float simulatedSeconds, float landingDownwardSpeed, Vector3 worldPosition, Collider supportCollider)
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
        }
    }
}
