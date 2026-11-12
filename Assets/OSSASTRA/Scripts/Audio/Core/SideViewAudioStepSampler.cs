/*
 * 脚本：SideViewAudioStepSampler
 * 功能：把单角色权威模拟步变成脚步／普通起跳／普通落地的短音候选。
 * 边界：只消费已结算运动，不读 Transform、按键、渲染帧或物理；不拥有角色状态。
 * 协作：SideViewLocomotionAudioAdapter 为每个运动来源持有一份累计器。
 */
using System;

namespace Ossastra.Audio
{
    public enum SideViewAudioCue { None, Walk, Run, Jump, Land }

    public sealed class SideViewAudioStepSampler
    {
        private readonly double walkStride, runStride, runSpeed;
        private double distance;
        private bool seenStep, seenAir;
        private long lastStep, contactEpoch;

        public SideViewAudioStepSampler(float walkStride, float runStride, float runSpeed)
        {
            if (!Positive(walkStride) || !Positive(runStride) || !Positive(runSpeed))
                throw new ArgumentOutOfRangeException(nameof(walkStride), "步距及跑步速度阈值必须是有限正数。");
            this.walkStride = walkStride;
            this.runStride = runStride;
            this.runSpeed = runSpeed;
        }

        public void Reset()
        {
            seenStep = seenAir = false;
            distance = 0;
        }

        /// <summary>同一序号只消费一次。没有模拟步时不调用，已有累计不会被显示帧清空。</summary>
        public SideViewAudioCue Observe(long stepId, long epoch, bool groundedBefore, bool groundedAfter,
            bool launched, bool allowsFootstep, bool hasSupport, float horizontalDistance, float seconds, float downwardSpeed)
        {
            if (seenStep && stepId <= lastStep) return SideViewAudioCue.None;
            if (!seenStep || contactEpoch != epoch)
            {
                // 出生、传送或运动退出切断旧空中历史，恢复接地不能被听成落地。
                distance = 0;
                seenAir = false;
            }
            seenStep = true;
            lastStep = stepId;
            contactEpoch = epoch;

            if (!Positive(seconds) || !Finite(horizontalDistance) || horizontalDistance < 0 || !Finite(downwardSpeed))
            {
                distance = 0;
                return SideViewAudioCue.None;
            }
            if (!groundedAfter)
            {
                distance = 0;
                seenAir = true;
                return launched ? SideViewAudioCue.Jump : SideViewAudioCue.None;
            }

            bool landed = seenAir && !groundedBefore && hasSupport && downwardSpeed > 0;
            seenAir = false;
            if (!groundedBefore || !hasSupport || !allowsFootstep || horizontalDistance <= .001f)
            {
                distance = 0;
                return landed ? SideViewAudioCue.Land : SideViewAudioCue.None;
            }

            bool running = horizontalDistance / seconds >= runSpeed;
            double stride = running ? runStride : walkStride;
            distance += horizontalDistance;
            // 只保留不足一步的余量，既不补发旧声，也不因浮点边界让整步漂到下一帧。
            const double epsilon = .000001;
            if (distance + epsilon < stride) return SideViewAudioCue.None;
            distance = Math.Max(0, distance - Math.Floor((distance + epsilon) / stride) * stride);
            return running ? SideViewAudioCue.Run : SideViewAudioCue.Walk;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0;
    }
}
