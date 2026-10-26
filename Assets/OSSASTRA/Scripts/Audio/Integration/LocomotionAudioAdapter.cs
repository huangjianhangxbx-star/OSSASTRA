/* 脚本：LocomotionAudioAdapter
 * 功能：读取本场实体碰撞后的真实XZ行走，按距离产生材质脚步事实。
 * 边界：不读取按键、不推进角色、不补造落脚动画事件；闪避/强制位移/传送不算行走。
 * 协作：角色根/敌人根仍拥有运动；本适配器持有独立来源租约，统一音频根准入和播放。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.Enemies;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;
using EnemyEntityRoot = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.Audio
{
    /// <summary>只累计实际水平距离；站立、失去资格和传送都会切断步长累计。</summary>
    public sealed class DistanceFootstepSampler
    {
        private readonly float walkDistance, runDistance, runSpeed, teleportDistance;
        private Vector3 previous;
        private bool hasPrevious;
        private float accumulated;
        public bool IsRunning { get; private set; }

        public DistanceFootstepSampler(float walkDistance = .8f, float runDistance = 1.2f,
            float runSpeed = 2.2f, float teleportDistance = 3f)
        {
            if (!Finite(walkDistance) || walkDistance <= 0 || !Finite(runDistance) || runDistance <= 0 ||
                !Finite(runSpeed) || runSpeed <= 0 || !Finite(teleportDistance) || teleportDistance <= 0)
                throw new ArgumentOutOfRangeException(nameof(walkDistance), "步长、跑速和传送阈值必须为有限正数。");
            this.walkDistance = walkDistance;
            this.runDistance = runDistance;
            this.runSpeed = runSpeed;
            this.teleportDistance = teleportDistance;
        }

        public void Reset() { hasPrevious = false; accumulated = 0; IsRunning = false; }

        /// <summary>每次最多一个候选；不补发低帧率期间的历史脚步。</summary>
        public bool Advance(Vector3 position, bool eligible, float delta)
        {
            if (!eligible || !Finite(delta) || delta <= 0 || !Finite(position.x) ||
                !Finite(position.y) || !Finite(position.z))
            { Reset(); return false; }
            if (!hasPrevious) { previous = position; hasPrevious = true; return false; }
            var displacement = position - previous;
            previous = position;
            displacement.y = 0;
            float distance = displacement.magnitude;
            if (distance > teleportDistance || distance <= .001f)
            { accumulated = 0; IsRunning = false; return false; }
            IsRunning = distance / delta >= runSpeed;
            accumulated += distance;
            float stride = IsRunning ? runDistance : walkDistance;
            if (accumulated < stride) return false;
            // 丢弃多步积压而保留不足一步的余量，避免下一帧爆发追赶。
            accumulated %= stride;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class LocomotionAudioAdapter : MonoBehaviour
    {
        private sealed class Observation
        {
            public ActorRoot actor;
            public EnemyEntityRoot enemy;
            public EnemyMovement enemyMovement;
            public CharacterDodge dodge;
            public CombatStatusEffects statusEffects;
            public DistanceFootstepSampler sampler;
            public SfxSourceHandle source;
            public long sequence;
            public Component Entity => actor != null ? (Component)actor : enemy;
        }

        [Header("真实行走距离；不按输入或时间触发")]
        [SerializeField, Min(.01f)] private float walkDistance = .8f;
        [SerializeField, Min(.01f)] private float runDistance = 1.2f;
        [SerializeField, Min(.01f)] private float runSpeed = 2.2f;
        [SerializeField, Min(.01f)] private float teleportDistance = 3f;
        [Header("向下读取真实碰撞地表")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(.01f)] private float groundProbeHeight = .5f;
        [SerializeField, Min(.01f)] private float groundProbeDistance = 2f;
        private readonly List<Observation> observed = new List<Observation>();
        private readonly RaycastHit[] groundHits = new RaycastHit[32];
        private GameAudioRoot audioRoot;
        private CharacterControlCoordinator coordinator;
        private string world;
        private float nextDiscovery;
        public int SubmittedSteps { get; private set; }
        public int AcceptedSteps { get; private set; }
        public int ActiveSources { get; private set; }
        public string LastEvent { get; private set; } = "无";
        public string Status { get; private set; } = "等待本场行走事实";

        private bool InOwnScene(Component value) => value != null && value.gameObject.scene == gameObject.scene;

        private void LateUpdate()
        {
            var currentRoot = GameAudioRoot.Instance;
            if (!Application.isPlaying || currentRoot == null || !currentRoot.SfxAvailable || !currentRoot.WorldAudioEnabled)
            { ReleaseAll(); Status = "当前内容档案未启用世界声音"; return; }
            if (audioRoot != currentRoot || world != currentRoot.WorldScopeId)
            {
                ReleaseAll(); audioRoot = currentRoot; world = audioRoot.WorldScopeId; nextDiscovery = 0;
            }
            if (Time.unscaledTime >= nextDiscovery) Discover();
            ActiveSources = 0;
            for (int i = observed.Count - 1; i >= 0; i--)
            {
                var item = observed[i];
                if (item.Entity == null) { Release(item); observed.RemoveAt(i); continue; }
                bool eligible = Eligible(item) && Time.timeScale > 0;
                if (!eligible) { Release(item); continue; }
                if (item.source == null || item.source.IsDisposed)
                {
                    item.sampler.Reset();
                    item.source = audioRoot.RegisterSfxSource("locomotion@" + item.Entity.GetInstanceID());
                }
                if (item.source == null) continue;
                ActiveSources++;
                if (!item.sampler.Advance(item.Entity.transform.position, true, Time.deltaTime)) continue;
                string role = item.enemy != null ? "enemy" : coordinator != null && coordinator.Current == item.actor ? "player" : "companion";
                string surface = ReadSurface(item.Entity.transform);
                // 无物理地面时不把空中/越界位移伪装成generic地表。
                if (surface == null) continue;
                string cue = role + ".footstep." + surface + (item.sampler.IsRunning ? ".run" : ".walk");
                LastEvent = cue; SubmittedSteps++;
                var decision = item.source.Submit(cue, "step:" + (++item.sequence), item.Entity.transform.position);
                if (decision.Accepted) AcceptedSteps++;
                Status = decision.Reason;
            }
        }

        private void Discover()
        {
            nextDiscovery = Time.unscaledTime + .5f;
            coordinator = null;
            foreach (var value in FindObjectsOfType<CharacterControlCoordinator>())
                if (value.isActiveAndEnabled && InOwnScene(value)) { coordinator = value; break; }
            foreach (var actor in FindObjectsOfType<ActorRoot>())
                if (InOwnScene(actor) && !observed.Exists(value => value.actor == actor))
                    observed.Add(NewObservation(actor, null));
            foreach (var enemy in FindObjectsOfType<EnemyEntityRoot>())
                if (InOwnScene(enemy) && !observed.Exists(value => value.enemy == enemy))
                    observed.Add(NewObservation(null, enemy));
        }

        private Observation NewObservation(ActorRoot actor, EnemyEntityRoot enemy)
        {
            Component entity = actor != null ? (Component)actor : enemy;
            return new Observation
            {
                actor = actor, enemy = enemy,
                enemyMovement = entity.GetComponent<EnemyMovement>(),
                dodge = entity.GetComponent<CharacterDodge>(),
                statusEffects = entity.GetComponent<CombatStatusEffects>(),
                sampler = new DistanceFootstepSampler(walkDistance, runDistance, runSpeed, teleportDistance)
            };
        }

        private static bool Eligible(Observation item)
        {
            if (item.actor != null)
            {
                var actor = item.actor;
                var bus = actor.DataBus;
                return actor.CanSimulateIntent && !actor.IsSimulationSuspended && bus != null && bus.IsMoving &&
                    (item.dodge == null || !item.dodge.IsActive) &&
                    (bus.MotionOverride == null || !bus.MotionOverride.IsActive);
            }
            return item.enemy != null && item.enemy.isActiveAndEnabled && !item.enemy.IsSimulationSuspended &&
                item.enemy.Vitals != null && item.enemy.Vitals.IsAvailable &&
                item.enemyMovement != null && item.enemyMovement.isActiveAndEnabled && item.enemyMovement.HasMovementRequest &&
                item.enemyMovement.Velocity.sqrMagnitude > .000001f &&
                (item.statusEffects == null || !item.statusEffects.BlocksIntent);
        }

        private string ReadSurface(Transform entity)
        {
            int count = Physics.RaycastNonAlloc(entity.position + Vector3.up * groundProbeHeight,
                Vector3.down, groundHits, groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore);
            Collider nearest = null; float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider == null || hit.collider.transform == entity || hit.collider.transform.IsChildOf(entity) || hit.distance >= distance) continue;
                nearest = hit.collider; distance = hit.distance;
            }
            if (nearest == null) return null;
            var fact = nearest.GetComponentInParent<AudioSurfaceFact>();
            string surface = fact == null || !fact.isActiveAndEnabled ? "generic" : (fact.surface ?? "generic").Trim().ToLowerInvariant();
            return surface == "stone" || surface == "wood" || surface == "dirt" || surface == "metal" ? surface : "generic";
        }

        private static void Release(Observation item)
        { item.source?.Dispose(); item.source = null; item.sampler.Reset(); }
        private void ReleaseAll()
        {
            foreach (var item in observed) Release(item);
            observed.Clear(); ActiveSources = 0; audioRoot = null; world = null; coordinator = null;
        }
        private void OnDisable() => ReleaseAll();
        private void OnDestroy() => ReleaseAll();
    }
}
