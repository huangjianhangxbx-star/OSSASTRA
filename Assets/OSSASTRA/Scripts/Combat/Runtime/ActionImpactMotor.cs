/*
 * 招式运动接收器：保存当前本体的短程曲线、输入窗口与击飞冲量。没有 Update，也不写 Transform。
 * 正式玩家或敌人运动器在自己的模拟步取样，结合既有重力、碰撞和平台，完成唯一的物理提交。
 * 主动、命中反冲、受击推移各占一条曲线。通道内按请求平均速率取强，等强按最近请求刷新。
 * 带停靠点的目标请求持有独立句柄。在本体既有移动提交后按实际位置报告结果，后续取消只影响原请求。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using Ossastra.Enemies;
using UnityEngine;

namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class ActionImpactMotor : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float pushResistance;
        [Tooltip("启用后为招式运动单独指定抗性；默认沿用本体已有的受推倍率")]
        [SerializeField] private bool overrideResistance;
        public float PushResistance
        {
            get => !overrideResistance && TryGetComponent<CombatStatusEffects>(out var status) ? 1f - status.ImpactWeight : pushResistance;
            set { overrideResistance = true; pushResistance = Mathf.Clamp01(value); }
        }
        public Vector2 LastRequestedStep { get; private set; }
        public Vector2 LastActualStep { get; private set; }

        public enum TargetMotionStatus { Pending, Arrived, Failed, Cancelled }

        /// <summary>确认本体实际到位的单次目标运动句柄，终结后的结果可继续供来源查询。</summary>
        public sealed class TargetMotion
        {
            private ActionImpactMotor motor;
            internal readonly Vector2 destination;
            internal readonly float speed, timeout, horizontalTolerance, verticalTolerance;
            internal float elapsed, stepSeconds, beforeDistance, stalled;

            internal TargetMotion(ActionImpactMotor motor, Guid actionId, Vector2 destination, float speed,
                float timeout, float horizontalTolerance, float verticalTolerance)
            {
                this.motor = motor;
                ActionId = actionId;
                this.destination = destination;
                this.speed = speed;
                this.timeout = timeout;
                this.horizontalTolerance = horizontalTolerance;
                this.verticalTolerance = verticalTolerance;
            }

            public Guid ActionId { get; }
            public TargetMotionStatus Status { get; private set; }
            public string Reason { get; private set; }
            public Vector2 RequestedStep { get; internal set; }
            public Vector2 ActualStep { get; internal set; }

            public void Cancel()
            {
                if (motor && ReferenceEquals(motor.targetMotion, this))
                    motor.EndTargetMotion(TargetMotionStatus.Cancelled, "来源取消");
            }

            internal void Finish(TargetMotionStatus status, string reason)
            {
                Status = status;
                Reason = reason;
                motor = null;
            }
        }

        private sealed class Travel
        {
            internal Guid owner;
            internal Vector2 distance;
            internal float elapsed, duration, progress;
            internal AnimationCurve curve;
            internal bool replaceInput;
            internal Vector2 Step(float dt)
            {
                elapsed = Mathf.Min(duration, elapsed + dt);
                float fraction = elapsed / duration;
                float next = elapsed >= duration ? 1f : Mathf.Max(progress, Mathf.Clamp01(curve == null ? fraction : curve.Evaluate(fraction)));
                Vector2 step = distance * (next - progress);
                progress = next;
                return step;
            }
        }
        private struct InputWindow
        {
            public Guid action;
            public string entry;
            public float scale, facing;
            public int direction;
            public bool fullBody;
        }

        private Travel self, recoil, pushed;
        private TargetMotion targetMotion;
        private readonly List<InputWindow> windows = new List<InputWindow>();
        private Vector2 launch;
        private float enemyVerticalSpeed;
        private Guid launchOwner;
        private bool pendingLaunch, flying;
        public bool HasMotion => self != null || recoil != null || pushed != null || pendingLaunch || flying || targetMotion != null;
        public bool HasInputWindow => windows.Count > 0;
        public float LockedFacing
        {
            get
            {
                foreach (var window in windows) if (window.fullBody) return window.facing;
                return 0;
            }
        }

        private static bool Resolve(Component actor, bool target, out ActionImpactMotor motor, out string reason)
        {
            motor = null;
            reason = "无可动横版目标";
            if (!actor) return false;
            var player = actor.GetComponentInParent<CharacterMovement>();
            var enemy = actor.GetComponentInParent<Ossastra.Enemies.EnemyRoot>();
            GameObject owner;
            if (player && player.isActiveAndEnabled && player.IsSideView) owner = player.gameObject;
            else if (enemy && enemy.isActiveAndEnabled && enemy.Vitals && enemy.Vitals.SpatialPlane == CombatPlane.SideXY)
            {
                var walker = enemy.GetComponent<EnemyMovement>();
                var tactics = enemy.GetComponent<SideViewEnemyBrain>();
                if (!(walker && walker.isActiveAndEnabled || tactics && tactics.isActiveAndEnabled)) return false;
                owner = enemy.gameObject;
            }
            else return false;
            var body = owner.GetComponent<CharacterController>();
            var vitals = owner.GetComponent<CombatVitals>();
            if (!body || !body.enabled || !vitals || !vitals.IsAvailable) return false;
            if (target && owner.TryGetComponent<CombatPosture>(out var posture) && posture.IsStaggered)
            { reason = "破防优先"; return false; }
            if (owner.TryGetComponent<CombatStatusEffects>(out var status) && (status.HasForcedMotion || status.BlocksIntent))
            { reason = "已有强制运动或控制优先"; return false; }
            if (!owner.TryGetComponent(out motor)) motor = owner.AddComponent<ActionImpactMotor>();
            if (!motor.isActiveAndEnabled) return false;
            if (target && motor.PushResistance >= 1) { reason = "免疫推移"; return false; }
            reason = null;
            return true;
        }

        public static bool TryMoveTarget(Component actor, Guid actionId, Vector2 destination, float horizontalSpeed,
            float timeout, float horizontalTolerance, float verticalTolerance, out TargetMotion motion, out string reason)
        {
            motion = null;
            reason = "停靠、速度或期限参数无效";
            if (actionId == Guid.Empty || !Finite(destination) || !PositiveFinite(horizontalSpeed) ||
                !PositiveFinite(timeout) || !PositiveFinite(horizontalTolerance) || !PositiveFinite(verticalTolerance)) return false;
            if (!Resolve(actor, true, out var motor, out reason)) return false;
            if (motor.HasMotion)
            { reason = "已有招式运动"; return false; }
            var position = motor.transform.position;
            if (Mathf.Abs(position.y - destination.y) > verticalTolerance)
            { reason = "目标与停靠点高差超限"; return false; }

            motion = new TargetMotion(motor, actionId, destination, horizontalSpeed * (1 - motor.PushResistance),
                timeout, horizontalTolerance, verticalTolerance);
            motor.targetMotion = motion;
            if (Mathf.Abs(position.x - destination.x) <= horizontalTolerance)
                motor.EndTargetMotion(TargetMotionStatus.Arrived, "目标已在停靠范围");
            return true;
        }

        private static bool PositiveFinite(float value) => value > 0 && !float.IsInfinity(value);

        public static bool TryDisplace(Component actor, Guid actionId, Vector2 distance, float seconds,
            AnimationCurve curve, bool target, bool replaceInput, out string reason, bool isRecoil = false)
        {
            if (!Resolve(actor, target, out var motor, out reason)) return false;
            if (!(seconds > 0) || float.IsInfinity(seconds) || !Finite(distance))
            { reason = "距离或时间无效"; return false; }
            if (target) distance *= 1 - motor.PushResistance;
            var old = target ? motor.pushed : isRecoil ? motor.recoil : motor.self;
            if (old != null && old.distance.magnitude / old.duration > distance.magnitude / seconds)
            { reason = "已有较强同类运动"; return false; }
            motor.EndTargetMotion(TargetMotionStatus.Failed, "其他招式位移接管");
            var travel = new Travel { owner = actionId, distance = distance, duration = seconds, curve = curve, replaceInput = replaceInput };
            if (target) motor.pushed = travel;
            else if (isRecoil) motor.recoil = travel;
            else motor.self = travel;
            return true;
        }

        public static bool TryLaunch(Component actor, Guid actionId, Vector2 velocity, bool target, out string reason)
        {
            if (!Resolve(actor, target, out var motor, out reason)) return false;
            if (!Finite(velocity) || velocity.y <= 0) { reason = "击飞初速无效"; return false; }
            if (target) velocity *= 1 - motor.PushResistance;
            motor.EndTargetMotion(TargetMotionStatus.Failed, "击飞接管");
            motor.launch = velocity;
            motor.launchOwner = target ? Guid.Empty : actionId;
            motor.pendingLaunch = true;
            return true;
        }
        private static bool Finite(Vector2 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);

        public static bool SetInput(Component actor, Guid actionId, string entryId, float scale, int allowedDirection,
            bool fullBody, out string reason, float acceptedFacing = 0)
        {
            if (!Resolve(actor, false, out var motor, out reason)) return false;
            if (!(scale >= 0 && scale <= 1)) { reason = "倍率越界"; return false; }
            if (!motor.TryGetComponent<CharacterMovement>(out _)) { reason = "输入窗口仅适用于玩家运动器"; return false; }
            ClearInput(actor, actionId, entryId);
            var orientation = motor.GetComponent<CharacterOrientation>();
            float facing = orientation ? orientation.AttackDirection.x : 1;
            if (Mathf.Abs(acceptedFacing) > .001f) facing = acceptedFacing;
            else if (orientation && Mathf.Abs(facing) < .01f) facing = orientation.VisualFacingX;
            motor.windows.Add(new InputWindow { action = actionId, entry = entryId, scale = scale,
                direction = allowedDirection, fullBody = fullBody, facing = Mathf.Sign(facing) });
            return true;
        }

        internal float InputScale(float signedInput, float previousScale)
        {
            if (targetMotion != null || self != null && self.replaceInput) return 0;
            foreach (var window in windows)
            {
                if (window.direction * signedInput < 0) return 0;
                previousScale = Mathf.Min(previousScale, window.scale);
            }
            return previousScale;
        }

        public static void ClearInput(Component actor, Guid actionId, string entryId)
        {
            var motor = actor ? actor.GetComponentInParent<ActionImpactMotor>() : null;
            if (motor) motor.windows.RemoveAll(w => w.action == actionId && w.entry == entryId);
        }
        public static void Release(Component actor, Guid actionId)
        {
            var motor = actor ? actor.GetComponentInParent<ActionImpactMotor>() : null;
            if (!motor || actionId == Guid.Empty) return;
            motor.windows.RemoveAll(w => w.action == actionId);
            if (motor.self?.owner == actionId) motor.self = null;
            if (motor.recoil?.owner == actionId) motor.recoil = null;
            if (motor.launchOwner == actionId && motor.pendingLaunch) motor.pendingLaunch = false;
        }

        internal Vector2 Step(float dt, out float? verticalLaunch)
        {
            verticalLaunch = pendingLaunch ? launch.y : (float?)null;
            flying |= pendingLaunch;
            pendingLaunch = false;
            Vector2 step = flying ? new Vector2(launch.x * dt, 0) : Vector2.zero;
            if (self != null)
            {
                step += self.Step(dt);
                if (self.elapsed >= self.duration) self = null;
            }
            if (recoil != null)
            {
                step += recoil.Step(dt);
                if (recoil.elapsed >= recoil.duration) recoil = null;
            }
            if (pushed != null)
            {
                step += pushed.Step(dt);
                if (pushed.elapsed >= pushed.duration) pushed = null;
            }
            if (targetMotion != null) step += StepTargetMotion(dt);
            return step;
        }

        // 只在目标本体的有效模拟步前进。局部卡肉期间运动器不会来取样，命令时钟随本体暂停。
        private Vector2 StepTargetMotion(float dt)
        {
            if (!Resolve(this, true, out _, out var reason))
            {
                EndTargetMotion(TargetMotionStatus.Failed, reason);
                return Vector2.zero;
            }
            var motion = targetMotion;
            var position = transform.position;
            if (Mathf.Abs(position.y - motion.destination.y) > motion.verticalTolerance)
            {
                EndTargetMotion(TargetMotionStatus.Failed, "停靠高度超限");
                return Vector2.zero;
            }
            motion.stepSeconds = Mathf.Min(dt, motion.timeout - motion.elapsed);
            motion.elapsed += motion.stepSeconds;
            float offset = motion.destination.x - position.x;
            motion.beforeDistance = Mathf.Abs(offset);
            float distance = motion.speed * motion.stepSeconds;
            motion.RequestedStep = new Vector2(Mathf.Clamp(offset, -distance, distance), 0);
            return motion.RequestedStep;
        }

        internal void Observe(Vector2 requested, Vector2 actual, bool landed)
        {
            LastRequestedStep = requested;
            LastActualStep = actual;
            if (landed) flying = false;
            var motion = targetMotion;
            if (motion == null || motion.stepSeconds <= 0) return;
            motion.ActualStep = actual;
            float remaining = Mathf.Abs(transform.position.x - motion.destination.x);
            if (Mathf.Abs(transform.position.y - motion.destination.y) > motion.verticalTolerance)
                EndTargetMotion(TargetMotionStatus.Failed, "停靠高度超限");
            else if (remaining <= motion.horizontalTolerance)
                EndTargetMotion(TargetMotionStatus.Arrived, "实际到位");
            else
            {
                // 斜面与台阶可以吃掉部分行程。只有连续没有向目标靠近才判阻挡。
                bool progressing = motion.beforeDistance - remaining > Mathf.Abs(motion.RequestedStep.x) * .05f;
                motion.stalled = progressing ? 0 : motion.stalled + motion.stepSeconds;
                if (motion.stalled >= .12f) EndTargetMotion(TargetMotionStatus.Failed, "目标运动受阻");
                else if (motion.elapsed >= motion.timeout) EndTargetMotion(TargetMotionStatus.Failed, "目标运动超时");
            }
        }

        private void EndTargetMotion(TargetMotionStatus status, string reason)
        {
            var motion = targetMotion;
            if (motion == null) return;
            targetMotion = null;
            motion.Finish(status, reason);
        }
        // 敌人的飞行、爬墙和走路策略共用同一胶囊，冲量期间只接管垂直速度。
        // PhysX 贴地滑动会再次用 minMoveDistance 筛切线余量。高帧率下合法的持续轻推小于默认 1 mm。
        // 保留一次原生 Move 与全部接触处理，只有本次招式驱动的提交允许亚毫米位移。
        internal CollisionFlags MoveBody(CharacterController body, Vector3 displacement)
        {
            var previousMinimum = body.minMoveDistance;
            if (previousMinimum <= 0) return body.Move(displacement);
            body.minMoveDistance = 0;
            try { return body.Move(displacement); }
            finally { body.minMoveDistance = previousMinimum; }
        }

        internal Vector3 EnemyStep(Vector3 velocity, float delta, float gravity)
        {
            var offset = Step(delta, out var initial);
            if (targetMotion != null) velocity.x = 0;
            if (initial.HasValue) enemyVerticalSpeed = initial.Value;
            if (flying)
            {
                enemyVerticalSpeed = Mathf.Max(-30f, enemyVerticalSpeed - gravity * delta);
                velocity.y = enemyVerticalSpeed;
            }
            return velocity * delta + (Vector3)offset;
        }
        internal void ObserveEnemy(Vector3 requested, Vector3 actual, CollisionFlags collision)
        {
            if ((collision & CollisionFlags.Above) != 0 && enemyVerticalSpeed > 0) enemyVerticalSpeed = 0;
            Observe(requested, actual, (collision & CollisionFlags.Below) != 0 && enemyVerticalSpeed <= 0);
        }

        internal void ClearMotion()
        {
            EndTargetMotion(TargetMotionStatus.Failed, "运动仲裁清退");
            self = recoil = pushed = null;
            pendingLaunch = flying = false;
            LastRequestedStep = LastActualStep = Vector2.zero;
        }
        public void ResetState()
        {
            ClearMotion();
            windows.Clear();
        }
        private void OnEnable()
        {
            if (TryGetComponent<CharacterMovement>(out var player)) player.Impact = this;
            if (TryGetComponent<EnemyMovement>(out var enemy)) enemy.Impact = this;
            if (TryGetComponent<SideViewEnemyBrain>(out var side)) side.Impact = this;
        }
        private void OnDisable()
        {
            ResetState();
            if (TryGetComponent<CharacterMovement>(out var player)) player.Impact = null;
            if (TryGetComponent<EnemyMovement>(out var enemy)) enemy.Impact = null;
            if (TryGetComponent<SideViewEnemyBrain>(out var side)) side.Impact = null;
        }
    }
}
