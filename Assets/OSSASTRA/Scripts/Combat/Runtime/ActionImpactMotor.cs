/*
 * 招式运动接收器：保存当前本体的短程曲线、输入窗口与击飞冲量。没有 Update，也不写 Transform。
 * 正式玩家或敌人运动器在自己的模拟步取样，结合既有重力、碰撞和平台，完成唯一的物理提交。
 * 主动、命中反冲、受击推移各占一条曲线。通道内按请求平均速率取强，等强按最近请求刷新。
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
        private readonly List<InputWindow> windows = new List<InputWindow>();
        private Vector2 launch;
        private float enemyVerticalSpeed;
        private Guid launchOwner;
        private bool pendingLaunch, flying;
        public bool HasMotion => self != null || recoil != null || pushed != null || pendingLaunch || flying;
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
            if (self != null && self.replaceInput) return 0;
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
            return step;
        }

        internal void Observe(Vector2 requested, Vector2 actual, bool landed)
        {
            LastRequestedStep = requested;
            LastActualStep = actual;
            if (landed) flying = false;
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
