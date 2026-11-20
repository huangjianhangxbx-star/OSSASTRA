// 记录正式移动器已执行的物理步；测量和绘图均不触碰角色 Transform。
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using UnityEngine;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabMovementTrace : IDisposable
    {
        internal struct Mark
        {
            public double time;
            public string label;
            public long step;
        }

        internal sealed class Jump
        {
            public Vector3 origin;
            public Vector3 apex;
            public Vector3 end;
            public float elapsed;
            public float ascent;
            public float landingSpeed;
            public string interruption;
            public bool movingSupport;
            public bool finished;

            public bool CompleteFreeJump => finished && interruption == null;
            public float Height => apex.y - origin.y;
            public float Horizontal => end.x - origin.x;

            public void Observe(SideMovementStep step)
            {
                if (finished) return;
                elapsed += step.SimulatedSeconds;
                end = step.WorldPosition;
                if (end.y > apex.y) { apex = end; ascent = elapsed; }
                movingSupport |= step.MovingSupport;
                if (step.HitHead && interruption == null) interruption = "碰顶";
                if (step.Driver != SideMovementDriver.Locomotion && interruption == null)
                    interruption = step.Driver == SideMovementDriver.Traversal ? "挂边或墙边接管" : "外部运动接管";
                if (!step.GroundedAfter) return;
                landingSpeed = step.LandingDownwardSpeed;
                finished = true;
            }
        }

        internal sealed class Trial
        {
            public readonly List<SideMovementStep> steps = new List<SideMovementStep>();
            public readonly List<Mark> marks = new List<Mark>();
            public AttackLabStation station;
            public string version;
            public int generation;
            public string profile;
            public float runSpeed;
            public float jumpSpeed;
            public Jump jump;
            public bool hasMotion;
        }

        private CharacterMovement motor;
        private Ossastra.Characters.MovementStateMachine state;
        private int generation;
        public Trial Live { get; private set; }
        public Trial Baseline { get; private set; }
        public Trial Candidate { get; private set; }
        private long contactEpoch = -1;
        private long stepId;

        public void Observe(AttackLabSession session, bool candidate)
        {
            var actor = session != null ? session.Actor : null;
            var activeMotor = actor != null ? actor.GetComponent<CharacterMovement>() : null;
            if (motor != activeMotor)
            {
                Dispose();
                Baseline = Candidate = null;
                motor = activeMotor;
                if (motor == null) return;
                state = actor.StateArbitrator?.Movement;
                motor.SideStepCompleted += ObserveStep;
                motor.SideTraversalOccurred += ObserveTraversal;
                if (state != null) state.JumpInputObserved += ObserveRequest;
            }
            if (motor == null || generation == session.Generation) return;

            if (Live?.hasMotion == true)
            {
                EndJump("复位");
                if (wasCandidate) Candidate = Live;
                else Baseline = Live;
            }
            generation = session.Generation;
            wasCandidate = candidate;
            var active = motor.JumpProfile;
            Live = new Trial
            {
                station = session.Station,
                version = session.Version,
                generation = generation,
                profile = active != null ? active.name : "无",
                runSpeed = active != null ? active.runSpeed : 0,
                jumpSpeed = active != null ? active.jumpSpeed : 0
            };
            contactEpoch = -1;
            stepId = 0;
        }
        private bool wasCandidate;

        private void ObserveStep(SideMovementStep step)
        {
            if (Live == null) return;
            if (contactEpoch != -1 && contactEpoch != step.ContactEpoch) EndJump("接触代次已变");
            contactEpoch = step.ContactEpoch;
            stepId = step.StepId;
            // 只截取实际运动邻域。落地静止超过容量后仍保留最后一次运动轨迹。
            if (step.Launched || step.HitHead || !step.GroundedAfter ||
                Mathf.Abs(step.HorizontalDistance) > .001f || step.GroundedBefore != step.GroundedAfter)
            {
                Live.hasMotion = true;
                Live.steps.Add(step);
                if (Live.steps.Count > 460) Live.steps.RemoveAt(0);
            }

            if (step.Launched && step.Driver == SideMovementDriver.Locomotion)
            {
                MarkEvent("普通起跳 · " + (step.LaunchSupportCollider != null ? step.LaunchSupportCollider.name : "无有效支撑"), step.StepId);
                Live.jump = new Jump { origin = step.PreviousWorldPosition, apex = step.PreviousWorldPosition, movingSupport = step.MovingSupport };
            }
            else if (step.GroundedBefore && !step.GroundedAfter)
                MarkEvent("离开支撑 · " + step.Driver, step.StepId);
            var jump = Live.jump;
            if (jump != null && !jump.finished)
            {
                bool hadHeadContact = jump.interruption == "碰顶";
                jump.Observe(step);
                if (step.HitHead && !hadHeadContact) MarkEvent("碰顶", step.StepId);
            }
            if (!step.GroundedBefore && step.GroundedAfter && step.LandingDownwardSpeed > 0f)
                MarkEvent($"落地 · {step.LandingDownwardSpeed:0.00} U/s · " +
                          (step.SupportCollider != null ? step.SupportCollider.name : "无支撑对象"), step.StepId);
        }

        private void ObserveRequest(string status, string reason)
            => MarkEvent(string.IsNullOrEmpty(reason) ? $"跳跃{status}" : $"跳跃{status} · {DescribeReason(reason)}", stepId);
        private void ObserveTraversal(SideTraversalEvent signal)
        {
            switch (signal.Kind)
            {
                case SideTraversalEventKind.Grab: EndJump("抓边"); break;
                case SideTraversalEventKind.WallJump: EndJump("墙跳"); break;
                case SideTraversalEventKind.ClimbComplete: EndJump("翻越"); break;
            }
            MarkEvent($"{DescribeTraversal(signal.Kind)} · {signal.ContactPosition ?? signal.Position:F2}", signal.StepId);
        }

        private static string DescribeTraversal(SideTraversalEventKind kind)
        {
            switch (kind)
            {
                case SideTraversalEventKind.Grab: return "抓到挂点";
                case SideTraversalEventKind.WallJump: return "墙跳";
                case SideTraversalEventKind.ClimbComplete: return "翻越完成";
                case SideTraversalEventKind.Drop: return "主动放手";
                default: return kind.ToString();
            }
        }

        private static string DescribeReason(string reason)
        {
            switch (reason)
            {
                case "ordinary": return "普通跳";
                case "wall": return "墙跳";
                case "jump-no-support": return "无支撑且已超离台宽容";
                case "jump-motion-not-ready": return "运动尚未获得许可";
                case "jump-headroom": return "头顶净空不足";
                case "jump-input-blocked": return "输入被当前状态阻止";
                case "ledge-action-blocked": return "挂边时不可普通跳";
                case "jump-buffer-expired": return "跳跃缓冲到期";
                case "jump-released": return "尚未受理即松开";
                default: return reason;
            }
        }

        private void MarkEvent(string what, long step)
        {
            if (Live == null) return;
            if (Live.marks.Count >= 60) Live.marks.RemoveAt(0);
            Live.marks.Add(new Mark { time = Time.unscaledTimeAsDouble, label = what, step = step });
        }

        private void EndJump(string reason)
        {
            var jump = Live?.jump;
            if (jump == null || jump.finished) return;
            if (jump.interruption == null) jump.interruption = reason;
            jump.finished = true;
        }

        public void Dispose()
        {
            if (motor != null)
            {
                motor.SideStepCompleted -= ObserveStep;
                motor.SideTraversalOccurred -= ObserveTraversal;
            }
            if (state != null) state.JumpInputObserved -= ObserveRequest;
            motor = null;
            state = null;
            generation = 0;
            Live = null;
        }
    }
}
