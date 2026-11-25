/*
 * 脚本：SideViewEnemyBrain
 * 功能：感知场内队员、稳定保留目标，并将横版敌人的策略意图送入 EnemyRoot。
 * 职责：拥有横版 CharacterController 位移和表面查询，消费根发布的正式动作节点。
 * 边界：不直接结算 HP、不创建攻击回执，逐怪行为和招式选择由策略负责。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using Ossastra.EnemyCore;
using UnityEngine;

namespace Ossastra.Enemies
{
    public enum SideViewEnemyKind { Thumper, Fly, Crawler, Gang, Mimic, Mother }
    public enum SideViewEnemyPhase { Idle, Alert, Windup, Active, Recovery }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class SideViewEnemyBrain : MonoBehaviour, IEnemyBrain
    {
        [SerializeField] private SideViewEnemyKind kind;
        [SerializeField] private CombatVitals[] targets = Array.Empty<CombatVitals>();
        [SerializeField, Min(.1f)] private float wakeRange = 6, attackRange = 3, verticalRange = 1.6f;
        [SerializeField, Min(0)] private float walkSpeed = 1.8f, attackSpeed = 5.5f, retryDelay = 1.2f;
        [SerializeField, Min(0)] private float targetSwitchMargin = .45f;
        [SerializeField, Min(0), Tooltip("相对出生标记的根高度。0 使用该敌人策略的默认高度。")]
        private float spawnClearance;
        [SerializeField, Range(-180, 180), Tooltip("爬虫出生表面相对地面的平面角度。")]
        private float surfaceAngle;
        [SerializeField] private LayerMask terrainLayers = ~0;
        private readonly RaycastHit[] terrainHits = new RaycastHit[24];
        private CombatVitals owner;
        private ICombatTarget pursuit;
        private CharacterController body;
        private Tactics tactics;
        private float nextAttack;
        private float lastSeen;
        private float verticalVelocity;
        private float phaseStarted;
        private int facing = -1;
        private int previousHp;
        private Vector3 home;
        private EnemyRoot simulationOwner;
        private float SimulationTime => simulationOwner != null ? simulationOwner.SimulationTime : Time.time;

        public bool IsAvailable => isActiveAndEnabled;
        public bool HasTarget => pursuit != null;
        public SideViewEnemyKind Kind => kind;
        public SideViewEnemyPhase Phase { get; private set; }
        public Vector3 Direction => Vector3.right * facing;
        public float VisualAngle => tactics?.VisualAngle ?? 0;
        public Vector3 HitDirection => tactics?.HitDirection ?? Direction;
        public Vector3 MoveDirection { get; private set; }
        public bool DamageWindowOpen(float now) => Phase == SideViewEnemyPhase.Active && tactics.ContactEnabled(now);
        public bool IsAirborne => (tactics?.Clearance ?? 0) > 0;
        internal string AnimationKey => tactics?.AnimationKey ?? "idle";

        public bool Initialize(CombatVitals value)
        {
            if (value == null || value.gameObject != gameObject || value.Health == null) return false;
            owner = value;
            simulationOwner = GetComponent<EnemyRoot>();
            body = GetComponent<CharacterController>();
            tactics = CreateTactics();
            // 上浮的是实际碰撞和感知根。已有地图的地面标记也按这条合同恢复空中锚。
            var clearance = spawnClearance > 0 ? spawnClearance : tactics.Clearance;
            home = transform.position + Vector3.up * clearance;
            ReturnToSpawn();
            ResetDecision(SimulationTime);
            return body != null && targets != null && targets.Length > 0;
        }

        public void ResetDecision(float now)
        {
            pursuit = null;
            nextAttack = now + .35f;
            lastSeen = float.NegativeInfinity;
            verticalVelocity = 0;
            facing = -1;
            MoveDirection = Vector3.zero;
            previousHp = owner != null && owner.Health != null ? owner.Health.Current : 0;
            SetPhase(SideViewEnemyPhase.Idle, now);
            tactics?.Reset(now);
        }

        public void Tick(float now, float delta)
        {
            var damaged = owner.Health.Current < previousHp;
            previousHp = owner.Health.Current;
            if (Phase >= SideViewEnemyPhase.Windup) return;
            pursuit = SelectVisibleTarget();
            if (pursuit != null || damaged) lastSeen = now;
            SetPhase(now - lastSeen < tactics.MemorySeconds ? SideViewEnemyPhase.Alert : SideViewEnemyPhase.Idle, now);
            tactics.Decide(now, damaged);
        }

        public bool TrySelect(float now, out ICombatTarget candidate)
        {
            candidate = pursuit;
            return candidate != null && now >= nextAttack && tactics.TryAttack(now, TargetOffset);
        }

        private ICombatTarget SelectVisibleTarget()
        {
            ICombatTarget best = null;
            var bestDistance = float.PositiveInfinity;
            var retainedDistance = float.PositiveInfinity;
            foreach (var target in targets)
            {
                if (!IsEligible(target)) continue;
                var offset = target.WorldPosition - transform.position;
                if (!SideViewEnemyRules.Detects(offset.x, offset.y, offset.z, wakeRange, verticalRange, .8f) ||
                    !CombatSight.IsClear(transform, target.transform, .45f)) continue;

                var distance = offset.x * offset.x + offset.y * offset.y;
                if (ReferenceEquals(pursuit, target)) retainedDistance = distance;
                if (!SideViewEnemyRules.Beats(target.Health.Id, distance, best?.Health.Id, bestDistance)) continue;
                best = target;
                bestDistance = distance;
            }
            // 换主控和两个队员距离小幅交替不会改变承诺目标。
            if (!float.IsPositiveInfinity(retainedDistance) &&
                !SideViewEnemyRules.ShouldSwitchTarget(retainedDistance, bestDistance, targetSwitchMargin))
                return pursuit;
            return best;
        }

        public bool IsEligible(ICombatTarget candidate)
        {
            if (!IsAvailable || owner == null || !owner.IsAvailable || targets == null ||
                !CombatTargetValidity.IsAvailable(candidate) || candidate.TargetTransform == null) return false;
            var seen = false;
            foreach (var target in targets)
            {
                if (target == null) continue;
                if (ReferenceEquals(target, candidate)) seen = true;
                else if (target.Health != null && target.Health.Id == candidate.Health.Id) return false;
            }
            return seen && CombatTargetPolicy.CanHit(owner.Health.Id, owner.Faction, owner.IsAlive,
                candidate.Health.Id, candidate.Faction, candidate.IsAlive);
        }

        public BasicAttackDefinition ChooseAttack(BasicAttackDefinition primary) => tactics.Timing(primary);

        public void Commit(ICombatTarget selected)
        {
            var now = SimulationTime;
            Face(selected.WorldPosition.x - transform.position.x);
            MoveDirection = Vector3.zero;
            SetPhase(SideViewEnemyPhase.Windup, now);
            tactics.Commit(now);
        }

        public void OnAccepted(float now) { }

        public void Observe(ActionFact fact, float now)
        {
            switch (fact.Kind)
            {
                case ActionFactKind.Released:
                    SetPhase(SideViewEnemyPhase.Active, now);
                    break;
                case ActionFactKind.MoveReady:
                    SetPhase(SideViewEnemyPhase.Recovery, now);
                    break;
                case ActionFactKind.Finished:
                    SetPhase(SideViewEnemyPhase.Alert, now);
                    // 长动作的有效窗、后摇与再次决策是三段时钟。
                    nextAttack = now + tactics.Cooldown;
                    break;
            }
            tactics.Observe(fact.Kind, now);
        }

        internal Ossastra.Combat.ActionImpactMotor Impact { get; set; }
        private bool movedThisTick;

        public void Move(float delta)
        {
            if (body == null || !body.enabled || delta <= 0) return;
            movedThisTick = false;
            tactics.Move(SimulationTime, delta);
            // 个别蓄力阶段没有自愿运动，仍由同一个 Step 提交目标受击命令。
            if (!movedThisTick && Impact && Impact.HasMotion) Step(Vector3.zero, delta);
        }

        /// <summary>研究场触发原作爬虫对既成环境声音或演奏事件的反应。</summary>
        public bool NotifyAmbientSignal(Vector3 source, bool sustained)
        {
            if (!IsAvailable || owner == null || !owner.IsAlive || tactics is not CrawlerTactics crawler)
                return false;
            return crawler.HeardSignal(source, sustained, SimulationTime);
        }

        private Vector3 Position => transform.position;
        private Vector3 TargetOffset => pursuit == null ? Vector3.zero : pursuit.WorldPosition - Position;
        private float PhaseAge(float now) => now - phaseStarted;

        private void SetPhase(SideViewEnemyPhase value, float now)
        {
            if (Phase == value) return;
            Phase = value;
            phaseStarted = now;
        }

        private void Face(float x)
        {
            if (Mathf.Abs(x) > .07f) facing = SideViewEnemyRules.Facing(x, facing);
        }

        private bool GroundAhead(float sign)
        {
            var origin = body.bounds.center + Vector3.right * (Mathf.Sign(sign) * (body.radius + .22f));
            origin.y = body.bounds.min.y + .18f;
            return TryTerrain(origin, Vector3.down, .65f, out _);
        }

        private bool WallAhead(float sign, float travel)
            => TryTerrain(body.bounds.center, Vector3.right * Mathf.Sign(sign), body.radius + travel + .06f, out _);

        private float FloorY(float x)
        {
            var origin = new Vector3(x, body.bounds.center.y, Position.z);
            return TryTerrain(origin, Vector3.down, 12, out var floor) ? floor.point.y : home.y - tactics.Clearance;
        }

        private void Walk(float velocity, float delta)
        {
            if (velocity != 0 && !GroundAhead(velocity)) velocity = 0;
            verticalVelocity = body.isGrounded && verticalVelocity <= 0 ? -2 : Mathf.Max(-14, verticalVelocity - 22 * delta);
            Step(new Vector3(velocity, verticalVelocity, 0), delta);
        }

        private CollisionFlags Step(Vector3 velocity, float delta)
        {
            MoveDirection = velocity.sqrMagnitude > .001f ? velocity.normalized : Vector3.zero;
            movedThisTick = true;
            return ApplyStep(velocity, delta);
        }

        internal void StepImpactOnly(float delta)
        {
            if (body && body.enabled && Impact && Impact.HasMotion && delta > 0) ApplyStep(Vector3.zero, delta);
        }

        private CollisionFlags ApplyStep(Vector3 velocity, float delta)
        {
            if (!Impact || !Impact.HasMotion) return body.Move(velocity * delta);
            var before = transform.position;
            var requested = Impact.EnemyStep(velocity, delta, 22f);
            var collision = Impact.MoveBody(body, requested);
            Impact.ObserveEnemy(requested, transform.position - before, collision);
            return collision;
        }

        private void Glide(Vector3 goal, float speed, float delta)
        {
            var offset = goal - Position;
            offset.z = 0;
            Step(Vector3.ClampMagnitude(offset / Mathf.Max(.15f, delta), speed), delta);
        }

        private bool TryTerrain(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            var best = float.PositiveInfinity;
            var count = Physics.RaycastNonAlloc(origin, direction, terrainHits, distance, terrainLayers, QueryTriggerInteraction.Ignore);
            for (var index = 0; index < count; index++)
            {
                var hit = terrainHits[index];
                if (hit.distance >= best || hit.transform.IsChildOf(transform) || CombatSpatialTarget.Find(hit.collider) != null ||
                    Mathf.Abs(hit.normal.z) > .75f) continue;
                best = hit.distance;
                nearest = hit;
            }
            return best < float.PositiveInfinity;
        }

        internal void ReturnToSpawn() => transform.position = home;

        private Tactics CreateTactics()
        {
            return kind switch
            {
                SideViewEnemyKind.Thumper => new ThumperTactics(this),
                SideViewEnemyKind.Fly => new FlyTactics(this),
                SideViewEnemyKind.Crawler => new CrawlerTactics(this),
                SideViewEnemyKind.Gang => new GangTactics(this),
                SideViewEnemyKind.Mimic => new MimicTactics(this),
                SideViewEnemyKind.Mother => new MotherTactics(this),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }
    }
}
