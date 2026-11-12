/*
 * 脚本：SideViewLocomotionAudioAdapter
 * 功能：消费横版角色碰撞结算后的运动步，按真实路程提交脚步并独立提交起跳／落地短音。
 * 边界：不读按键或可见位置，不查询或推算支撑，不创建音源，不改音量或内容档案。
 * 协作：CharacterMovement → 运动步累计器 → 现有 SFX 来源租约；本场组件也声明冷启动音频入口。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Audio
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class SideViewLocomotionAudioAdapter : MonoBehaviour
    {
        private sealed class Observation
        {
            public ActorRoot actor;
            public CharacterMovement movement;
            public Action<SideMovementStep> callback;
            public SideViewAudioStepSampler sampler;
            public SfxSourceHandle source;
            public SideMovementStep? footstep, launch, land;
            public SideViewAudioCue footstepKind;
            public void ClearPending() { footstep = launch = land = null; }
            public void Reset()
            {
                ClearPending();
                sampler.Reset();
                source?.Dispose();
                source = null;
            }
        }

        [Header("权威运动步距和模拟速度门槛")]
        [SerializeField, Min(.01f)] private float walkStride = .8f;
        [SerializeField, Min(.01f)] private float runStride = 1.2f;
        [SerializeField, Min(.01f)] private float runSpeed = 2.2f;
        [Header("本场普通运动短音（不是投送 land）")]
        [SerializeField] private SfxEventDefinitionSO jumpCue;
        [SerializeField] private SfxEventDefinitionSO landingCue;

        private readonly List<Observation> observations = new List<Observation>();
        private CharacterControlCoordinator coordinator;
        private GameAudioRoot audioRoot;
        private string world;
        private float nextDiscovery;

        public int ObservedMotionSteps { get; private set; }
        public int SubmittedFootsteps { get; private set; }
        public int SubmittedJumps { get; private set; }
        public int SubmittedLandings { get; private set; }
        public int CoalescedFootsteps { get; private set; }
        public string LastDecision { get; private set; } = "等待权威运动步";

        private void OnEnable() => Discover();
        private void Start() => Discover();

        internal void Discover()
        {
            nextDiscovery = Time.unscaledTime + .5f;
            if (coordinator == null || !coordinator.isActiveAndEnabled)
            {
                coordinator = null;
                foreach (var value in FindObjectsOfType<CharacterControlCoordinator>())
                    if (value.isActiveAndEnabled && value.gameObject.scene == gameObject.scene) { coordinator = value; break; }
            }
            foreach (var actor in FindObjectsOfType<ActorRoot>())
            {
                if (actor.gameObject.scene != gameObject.scene || observations.Exists(item => item.actor == actor)) continue;
                var movement = actor.GetComponent<CharacterMovement>();
                if (movement == null) continue;
                var item = new Observation { actor = actor, movement = movement, sampler = new SideViewAudioStepSampler(walkStride, runStride, runSpeed) };
                item.callback = step => Observe(item, step);
                movement.SideStepCompleted += item.callback;
                observations.Add(item);
            }
        }

        private void Observe(Observation item, SideMovementStep step)
        {
            if (!ConnectWorld() || !Eligible(item)) return;
            ObservedMotionSteps++;
            var cue = item.sampler.Observe(step.StepId, step.ContactEpoch, step.GroundedBefore, step.GroundedAfter,
                step.Launched, step.AllowsFootstep, step.SupportCollider != null, step.HorizontalDistance,
                step.SimulatedSeconds, step.LandingDownwardSpeed);
            if (cue == SideViewAudioCue.Jump) item.launch = step;
            if (cue == SideViewAudioCue.Land) item.land = step;
            if (!step.GroundedAfter) item.footstep = null;
            if (cue == SideViewAudioCue.Walk || cue == SideViewAudioCue.Run)
            {
                // 一帧补算多个权威步只发最后一个脚步；累计器已经消耗完整路程，下一帧不补播。
                if (item.footstep.HasValue) CoalescedFootsteps++;
                item.footstep = step;
                item.footstepKind = cue;
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextDiscovery) Discover();
            if (!ConnectWorld()) return;
            for (int i = observations.Count - 1; i >= 0; i--)
            {
                var item = observations[i];
                if (item.actor == null || item.movement == null) { Detach(item); observations.RemoveAt(i); continue; }
                if (!Eligible(item)) { item.Reset(); continue; }
                if (item.launch.HasValue) SubmitEdge(item, item.launch.Value, jumpCue, true);
                if (item.land.HasValue) SubmitEdge(item, item.land.Value, landingCue, false);
                if (item.footstep.HasValue) SubmitFootstep(item, item.footstep.Value);
                item.ClearPending();
            }
        }

        private bool ConnectWorld()
        {
            var root = GameAudioRoot.Instance;
            bool available = Application.isPlaying && isActiveAndEnabled && root != null && root.SfxAvailable &&
                gameObject.scene == SceneManager.GetActiveScene();
            string scope = available ? root.WorldScopeId : null;
            if (!available || audioRoot != root || world != scope)
            {
                if (audioRoot != null || world != null) foreach (var item in observations) item.Reset();
                audioRoot = available ? root : null;
                world = scope;
            }
            return available;
        }

        private static bool Eligible(Observation item) => item.actor != null && item.actor.isActiveAndEnabled &&
            !item.actor.IsSimulationSuspended && item.actor.IsAlive && item.movement != null && item.movement.isActiveAndEnabled;

        private static bool IsCurrent(Observation item, SideMovementStep step) => step.ContactEpoch == (long)item.movement.ContactRevision;

        private SfxSourceHandle Lease(Observation item)
        {
            if (item.source == null || item.source.IsDisposed)
                item.source = audioRoot.RegisterSfxSource("side-motion@" + item.actor.GetInstanceID());
            return item.source;
        }

        private void SubmitEdge(Observation item, SideMovementStep step, SfxEventDefinitionSO definition, bool jump)
        {
            if (!IsCurrent(item, step)) return;
            if (definition == null) { LastDecision = jump ? "ASSET_BLOCKED_ORDINARY_JUMP" : "ASSET_BLOCKED_ORDINARY_LANDING"; return; }
            var source = Lease(item);
            if (source == null) return;
            if (jump) SubmittedJumps++; else SubmittedLandings++;
            LastDecision = source.SubmitDefinition(definition, (jump ? "jump:" : "land:") + step.StepId, step.WorldPosition).Reason;
        }

        private void SubmitFootstep(Observation item, SideMovementStep step)
        {
            if (!IsCurrent(item, step) || !audioRoot.WorldAudioEnabled || step.SupportCollider == null) return;
            var source = Lease(item);
            if (source == null) return;
            var fact = step.SupportCollider.GetComponentInParent<AudioSurfaceFact>();
            string surface = fact == null || !fact.isActiveAndEnabled ? "generic" : (fact.surface ?? "generic").Trim().ToLowerInvariant();
            if (surface != "stone" && surface != "wood" && surface != "dirt" && surface != "metal") surface = "generic";
            string role = coordinator != null && coordinator.Current == item.actor ? "player" : "companion";
            string gait = item.footstepKind == SideViewAudioCue.Run ? ".run" : ".walk";
            string cue = role + ".footstep." + surface + gait;
            if (surface != "generic" && audioRoot.SfxCatalog != null && !audioRoot.SfxCatalog.TryResolve(cue, out _, out _))
                cue = role + ".footstep.generic" + gait;
            SubmittedFootsteps++;
            LastDecision = source.Submit(cue, "step:" + step.StepId, step.WorldPosition).Reason;
        }

        private static void Detach(Observation item)
        {
            if (item.movement != null) item.movement.SideStepCompleted -= item.callback;
            item.Reset();
        }

        private void OnDisable()
        {
            foreach (var item in observations) Detach(item);
            observations.Clear();
            audioRoot = null;
            world = null;
            coordinator = null;
        }
    }
}
