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
    public sealed class SideViewEnemyBrain : MonoBehaviour, IEnemyBrain
    {
        [SerializeField] private SideViewEnemyKind kind;
        [SerializeField] private CombatVitals[] targets = Array.Empty<CombatVitals>();
        [SerializeField, Min(.1f)] private float wakeRange = 6, attackRange = 3, verticalRange = 1.6f;
        [SerializeField, Min(0)] private float walkSpeed = 1.8f, attackSpeed = 5.5f, retryDelay = 1.2f;
        [SerializeField] private LayerMask terrainLayers = ~0;
        private CombatVitals owner;
        private ICombatTarget pursuit;
        private CharacterController body;
        private float nextAttack;
        private float lastSeen;
        private float verticalVelocity;
        private float pendingAttackUntil;
        private int facing = -1;
        private int actionNumber;
        private bool interruptedRoll;
        private bool secondMove;
        private Vector3 home;
        private Vector3 crawlerSurface = Vector3.up;
        private int crawlSign = -1;

        public bool IsAvailable => isActiveAndEnabled;
        public bool HasTarget => pursuit != null;
        public bool SecondMove => secondMove;
        public bool Bumped => interruptedRoll && kind == SideViewEnemyKind.Thumper && Phase == SideViewEnemyPhase.Active;
        public SideViewEnemyKind Kind => kind;
        public SideViewEnemyPhase Phase { get; private set; }
        public Vector3 Direction => Vector3.right * facing;
        public float VisualAngle => kind == SideViewEnemyKind.Crawler
            ? Vector3.SignedAngle(Vector3.up, crawlerSurface, Vector3.forward) : 0;
        public Vector3 HitDirection => kind == SideViewEnemyKind.Crawler ? Vector3.zero
            : kind == SideViewEnemyKind.Fly || (kind == SideViewEnemyKind.Mother && secondMove) ? Vector3.down : Direction;
        public Vector3 MoveDirection { get; private set; }
        public bool DamageWindowOpen(float now) => Phase == SideViewEnemyPhase.Active && now <= pendingAttackUntil;
        public bool IsAirborne => kind == SideViewEnemyKind.Fly || kind == SideViewEnemyKind.Mother;

        public bool Initialize(CombatVitals value)
        {
            if (value == null || value.gameObject != gameObject || value.Health == null) return false;
            owner = value;
            body = GetComponent<CharacterController>();
            home = transform.position;
            ResetDecision(Time.time);
            return body != null && targets.Length > 0;
        }

        public void ResetDecision(float now)
        {
            pursuit = null;
            nextAttack = now + .35f;
            lastSeen = float.NegativeInfinity;
            verticalVelocity = 0;
            MoveDirection = Vector3.zero;
            Phase = SideViewEnemyPhase.Idle;
            crawlerSurface = Vector3.up;
        }

        public void Tick(float now, float delta)
        {
            if (Phase >= SideViewEnemyPhase.Windup) return;
            pursuit = Select(wakeRange);
            if (pursuit != null)
            {
                lastSeen = now;
                Phase = SideViewEnemyPhase.Alert;
                var offset = pursuit.WorldPosition - transform.position;
                facing = SideViewEnemyRules.Facing(offset.x, facing);
                if (IsAirborne)
                    MoveDirection = Vector3.ClampMagnitude(offset + Vector3.up * (kind == SideViewEnemyKind.Fly ? 1.5f : .8f), 1);
                else
                    MoveDirection = Mathf.Abs(offset.x) > attackRange * .78f ? Direction : Vector3.zero;
                return;
            }

            var memory = kind == SideViewEnemyKind.Fly ? 8 : 6;
            Phase = now - lastSeen < memory ? SideViewEnemyPhase.Alert : SideViewEnemyPhase.Idle;
            if (kind == SideViewEnemyKind.Crawler)
                MoveDirection = Direction;
            else
            {
                var offset = home - transform.position;
                MoveDirection = Mathf.Abs(offset.x) > .18f ? Vector3.right * Mathf.Sign(offset.x) : Vector3.zero;
                if (IsAirborne)
                {
                    var direction = MoveDirection;
                    direction.y = Mathf.Clamp(offset.y, -1, 1);
                    MoveDirection = direction;
                }
            }
        }

        public bool TrySelect(float now, out ICombatTarget candidate)
        {
            candidate = now >= nextAttack ? Select(attackRange) : null;
            return candidate != null;
        }

        private ICombatTarget Select(float range)
        {
            ICombatTarget best = null;
            var distance = float.PositiveInfinity;
            foreach (var target in targets)
            {
                if (!IsEligible(target)) continue;
                var offset = target.WorldPosition - transform.position;
                if (!SideViewEnemyRules.Detects(offset.x, offset.y, offset.z, range, verticalRange, .8f) ||
                    !CombatSight.IsClear(transform, target.transform, .45f)) continue;
                if (kind == SideViewEnemyKind.Fly && range == attackRange && (offset.y > -.2f || Mathf.Abs(offset.x) > .8f))
                    continue;
                var score = offset.x * offset.x + offset.y * offset.y;
                if (!SideViewEnemyRules.Beats(target.Health.Id, score, best?.Health.Id, distance)) continue;
                best = target;
                distance = score;
            }
            return best;
        }

        public bool IsEligible(ICombatTarget candidate)
        {
            if (!IsAvailable || owner == null || !owner.IsAvailable ||
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

        public BasicAttackDefinition ChooseAttack(BasicAttackDefinition primary)
        {
            secondMove = (kind == SideViewEnemyKind.Gang || kind == SideViewEnemyKind.Mimic ||
                kind == SideViewEnemyKind.Mother) && actionNumber % 2 == 1;
            if (!secondMove) return primary;
            if (kind == SideViewEnemyKind.Gang)
                return new BasicAttackDefinition(1.5, 3.167, 5.167, 5.167, primary.Damage, primary.PostureImpact);
            if (kind == SideViewEnemyKind.Mimic)
                return new BasicAttackDefinition(.58, 2, 3.333, 3.333, primary.Damage, primary.PostureImpact);
            var release = primary.ReleaseAt + .18;
            return new BasicAttackDefinition(release, primary.MoveReadyAt + .3, primary.AttackReadyAt + .3,
                primary.FinishAt + .3, primary.Damage, primary.PostureImpact);
        }

        public void Commit(ICombatTarget selected)
        {
            facing = SideViewEnemyRules.Facing(selected.WorldPosition.x - transform.position.x, facing);
            MoveDirection = Vector3.zero;
            interruptedRoll = false;
            Phase = SideViewEnemyPhase.Windup;
            actionNumber++;
        }

        public void OnAccepted(float now) => nextAttack = now + retryDelay;

        public void Observe(ActionFact fact, float now, double activeDuration)
        {
            switch (fact.Kind)
            {
                case ActionFactKind.Released:
                    Phase = SideViewEnemyPhase.Active;
                    pendingAttackUntil = now + (float)activeDuration;
                    break;
                case ActionFactKind.MoveReady:
                    Phase = SideViewEnemyPhase.Recovery;
                    break;
                case ActionFactKind.Finished:
                    Phase = SideViewEnemyPhase.Alert;
                    break;
            }
        }

        public void Move(float delta)
        {
            if (body == null || !body.enabled || delta <= 0) return;
            if (kind == SideViewEnemyKind.Crawler)
            {
                Crawl(delta);
                return;
            }
            var direction = MoveDirection;
            var speed = walkSpeed;
            if (Phase >= SideViewEnemyPhase.Windup)
            {
                direction = Vector3.zero;
                if (Phase == SideViewEnemyPhase.Active && !interruptedRoll)
                    direction = kind == SideViewEnemyKind.Mother && !secondMove
                        ? Direction + Vector3.down * .35f
                        : kind == SideViewEnemyKind.Fly || (kind == SideViewEnemyKind.Mother && secondMove)
                            ? Vector3.down : kind == SideViewEnemyKind.Gang ? Vector3.zero : Direction;
                speed = kind == SideViewEnemyKind.Crawler ? walkSpeed : attackSpeed;
                if (kind == SideViewEnemyKind.Gang && !secondMove && !interruptedRoll)
                {
                    direction = Direction;
                    speed = Phase == SideViewEnemyPhase.Windup ? .85f
                        : Phase == SideViewEnemyPhase.Active ? 2.03f : .42f;
                }
                else if (kind == SideViewEnemyKind.Mimic)
                {
                    if (Phase == SideViewEnemyPhase.Recovery) direction = -Direction;
                    else if (!secondMove) direction = Vector3.zero;
                }
            }

            if (!IsAirborne && direction.x != 0 && !HasGround(direction.x))
            {
                if (kind == SideViewEnemyKind.Crawler && Phase < SideViewEnemyPhase.Windup) facing *= -1;
                if (Phase == SideViewEnemyPhase.Active) interruptedRoll = true;
                direction.x = 0;
            }
            direction.z = 0;
            if (!IsAirborne)
                verticalVelocity = body.isGrounded && verticalVelocity <= 0 ? -2 : Mathf.Max(-14, verticalVelocity - 22 * delta);
            var translation = direction * speed;
            translation.y += verticalVelocity;
            var collision = body.Move(translation * delta);
            if ((collision & CollisionFlags.Sides) != 0)
            {
                if (kind == SideViewEnemyKind.Crawler && Phase < SideViewEnemyPhase.Windup) facing *= -1;
                if (Phase == SideViewEnemyPhase.Active) interruptedRoll = true;
            }
        }

        private void Crawl(float delta)
        {
            var center = body.bounds.center;
            var tangent = new Vector3(crawlerSurface.y, -crawlerSurface.x, 0) * crawlSign;
            var probe = body.radius + .23f;
            if (TryTerrain(center, tangent, probe, out var insideCorner))
            {
                crawlerSurface = PlanarCardinal(insideCorner.normal);
                tangent = new Vector3(crawlerSurface.y, -crawlerSurface.x, 0) * crawlSign;
            }
            else if (!TryTerrain(center + tangent * .2f, -crawlerSurface, body.height * .5f + .32f, out _))
            {
                if (crawlerSurface != Vector3.up)
                    crawlerSurface = Vector3.up;
                crawlSign *= -1;
                tangent = -tangent;
            }
            facing = crawlSign;
            body.Move((tangent * walkSpeed - crawlerSurface * 1.7f) * delta);
        }

        private static Vector3 PlanarCardinal(Vector3 normal)
            => Mathf.Abs(normal.y) >= Mathf.Abs(normal.x)
                ? Vector3.up * Mathf.Sign(normal.y) : Vector3.right * Mathf.Sign(normal.x);

        private bool TryTerrain(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            var best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, terrainLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= best || hit.transform.IsChildOf(transform) || CombatSpatialTarget.Find(hit.collider) != null ||
                    Mathf.Abs(hit.normal.z) > .75f) continue;
                best = hit.distance;
                nearest = hit;
            }
            return best < float.PositiveInfinity;
        }

        private bool HasGround(float sign)
        {
            var origin = body.bounds.center + Vector3.right * (Mathf.Sign(sign) * (body.radius + .22f));
            origin.y = body.bounds.min.y + .18f;
            return TryTerrain(origin, Vector3.down, .65f, out _);
        }

        internal void ReturnToSpawn() => transform.position = home;
    }
}
