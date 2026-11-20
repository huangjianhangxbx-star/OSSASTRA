/*
 * 脚本：SideViewLocomotionAudioAdapter
 * 功能：消费横版角色的实际运动步和墙边转换，提交脚步、起跳、落地及攀越短音。
 * 边界：不读按键或可见位置，不查询或推算支撑，不创建音源，不改音量或内容档案。
 * 协作：CharacterMovement → 运动步累计器 → 现有 SFX 来源租约；本场组件也声明冷启动音频入口。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using Ossastra.CharacterAnimation;
using Ossastra.Combat;
using Ossastra.UserInterface;
using UnityEngine;
using UnityEngine.SceneManagement;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Audio
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class SideViewLocomotionAudioAdapter : MonoBehaviour
    {
        private sealed class PendingMotionSound
        {
            public SideViewMotionKind kind;
            public SfxEventDefinitionSO sound;
            public SfxPlaybackSettings? playback;
            public long epoch, step;
            public int life, revision;
            public string material, profile;
            public Vector3 position;
            public double occurredAt, dueAt;
        }

        private sealed class Observation
        {
            public ActorRoot actor;
            public CharacterMovement movement;
            public Action<SideMovementStep> callback;
            public Action<SideTraversalEvent> traversalCallback;
            public SideViewAudioStepSampler sampler;
            public SfxSourceHandle source;
            public SideMovementStep? footstep, launch, land;
            public readonly List<SideTraversalEvent> traversal = new List<SideTraversalEvent>(4);
            public SideViewAudioCue footstepKind;
            public CharacterAnimationController animation;
            public Action<AnimationSignal> markerCallback;
            public SideMovementStep? latestMotion;
            public readonly List<PendingMotionSound> delayed = new List<PendingMotionSound>();
            public int feedbackRevision;
            public void ClearPending() { footstep = launch = land = null; traversal.Clear(); }
            public void Reset()
            {
                ClearPending();
                delayed.Clear();
                feedbackRevision++;
                latestMotion = null;
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
        [Header("墙边动作短音")]
        [SerializeField] private SfxEventDefinitionSO grabCue;
        [SerializeField] private SfxEventDefinitionSO wallJumpCue;
        [SerializeField] private SfxEventDefinitionSO climbCue;
        [SerializeField] private SfxEventDefinitionSO dropCue;

        private readonly List<Observation> observations = new List<Observation>();
        private CharacterControlCoordinator coordinator;
        private GameAudioRoot audioRoot;
        private string world;
        private float nextDiscovery;
        private ActorRoot previousCurrent;
        private CombatFeedbackView feedbackView;

        public int ObservedMotionSteps { get; private set; }
        public int SubmittedFootsteps { get; private set; }
        public int SubmittedJumps { get; private set; }
        public int SubmittedLandings { get; private set; }
        public int SubmittedTraversals { get; private set; }
        public int CoalescedFootsteps { get; private set; }
        public string LastDecision { get; private set; } = "等待权威运动步";

        private void OnEnable()
        {
            SideViewFeedbackRuntime.TrialReset += OnTrialReset;
            Discover();
        }
        private void Start() => Discover();

        internal void Discover()
        {
            nextDiscovery = Time.unscaledTime + .5f;
            if (feedbackView == null)
                foreach (var view in FindObjectsOfType<CombatFeedbackView>())
                    if (view.gameObject.scene == gameObject.scene) { feedbackView = view; break; }
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
                item.traversalCallback = fact =>
                {
                    ConnectWorld();
                    if (Eligible(item)) item.traversal.Add(fact);
                };
                movement.SideStepCompleted += item.callback;
                movement.SideTraversalOccurred += item.traversalCallback;
                item.markerCallback = signal => OnMarker(item, signal);
                BindAnimation(item);
                observations.Add(item);
            }
        }

        private void Observe(Observation item, SideMovementStep step)
        {
            ConnectWorld();
            if (!Eligible(item)) return;
            BindAnimation(item);
            item.latestMotion = step;
            ObservedMotionSteps++;
            var cue = item.sampler.Observe(step.StepId, step.ContactEpoch, step.GroundedBefore, step.GroundedAfter,
                step.Launched, step.AllowsFootstep, step.SupportCollider != null, step.HorizontalDistance,
                step.SimulatedSeconds, step.LandingDownwardSpeed, item.movement.IsWalking);
            if (cue == SideViewAudioCue.Jump) item.launch = step;
            if (cue == SideViewAudioCue.Land) item.land = step;
            if (!step.GroundedAfter) item.footstep = null;
            if ((cue == SideViewAudioCue.Walk || cue == SideViewAudioCue.Run) && item.animation?.HasCalibratedFootstep != true)
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
            ConnectWorld();
            var controlled = coordinator == null ? null : coordinator.Current;
            if (controlled != previousCurrent)
            {
                var previous = observations.Find(item => item.actor == previousCurrent);
                if (previous != null) previous.Reset();
                previousCurrent = controlled;
            }
            for (int i = observations.Count - 1; i >= 0; i--)
            {
                var item = observations[i];
                if (item.actor == null || item.movement == null) { Detach(item); observations.RemoveAt(i); continue; }
                if (!Eligible(item)) { item.Reset(); continue; }
                foreach (var fact in item.traversal) SubmitTraversal(item, fact);
                // 同一模拟步已经使用墙跳或攀越收尾配方时，不再发一份普通起落声。
                if (item.launch.HasValue && !item.traversal.Exists(f => f.StepId == item.launch.Value.StepId && f.Kind == SideTraversalEventKind.WallJump))
                    SubmitEdge(item, item.launch.Value, jumpCue, true);
                if (item.land.HasValue && !item.traversal.Exists(f => f.StepId == item.land.Value.StepId && f.Kind == SideTraversalEventKind.ClimbComplete))
                    SubmitEdge(item, item.land.Value, landingCue, false);
                if (item.footstep.HasValue && item.animation?.HasCalibratedFootstep != true) SubmitFootstep(item, item.footstep.Value);
                FlushSounds(item);
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
            if (audioRoot == null) return null;
            if (item.source == null || item.source.IsDisposed)
                item.source = audioRoot.RegisterSfxSource("side-motion@" + item.actor.GetInstanceID());
            return item.source;
        }

        private void SubmitEdge(Observation item, SideMovementStep step, SfxEventDefinitionSO definition, bool jump)
        {
            if (!IsCurrent(item, step)) return;
            var contact = jump ? step.LaunchSupportCollider : step.SupportCollider;
            var position = (jump ? step.LaunchSupportPosition : step.SupportPosition) ?? step.WorldPosition;
            var travel = StepTravel(step);
            if (!jump) travel.y = -(float)step.LandingDownwardSpeed * (float)step.SimulatedSeconds;
            OfferContact(item, jump ? SideViewMotionKind.Jump : SideViewMotionKind.Land,
                definition, contact, position, step.StepId, step.ContactEpoch, (float)step.LandingDownwardSpeed,
                jump ? Vector3.up : Vector3.down, travel, Vector3.up);
        }

        private static void BindAnimation(Observation item)
        {
            var active = item.actor.StatePresentation as CharacterAnimationController;
            if (ReferenceEquals(active, item.animation)) return;
            if (item.animation != null) item.animation.SpineSignal -= item.markerCallback;
            item.animation = active;
            if (active != null) active.SpineSignal += item.markerCallback;
            item.latestMotion = null;
        }

        private void OnMarker(Observation item, AnimationSignal marker)
        {
            if (!marker.Calibrated || marker.Meaning != AnimationEventMeaning.Footstep ||
                !ConnectWorld() || !Eligible(item) || !item.latestMotion.HasValue) return;
            var step = item.latestMotion.Value;
            if (!CanEmitFootstep(item, step)) return;
            var custom = marker.Binding.sound;
            if (marker.Purpose != (custom ? AnimationSignalPurpose.Sound : AnimationSignalPurpose.Marker)) return;
            item.footstep = null;
            item.footstepKind = item.movement.IsWalking ? SideViewAudioCue.Walk : SideViewAudioCue.Run;
            string factKey = $"anim-step:{marker.Generation}:{marker.ReplayId}:{marker.Cycle}:{marker.Occurrence}:{marker.Sequence}";
            if (custom)
            {
                var source = Lease(item);
                if (source == null || !audioRoot.WorldAudioEnabled) return;
                SubmittedFootsteps++;
                LastDecision = source.SubmitClip(custom, factKey, step.WorldPosition, marker.Binding.volume).Reason;
            }
            else SubmitFootstep(item, step, factKey);
        }

        internal bool CanEmitFootstep(ActorRoot actor)
        {
            var item = observations.Find(o => o.actor == actor);
            return item != null && item.latestMotion.HasValue && CanEmitFootstep(item, item.latestMotion.Value);
        }

        private static bool CanEmitFootstep(Observation item, SideMovementStep step) =>
            Eligible(item) && IsCurrent(item, step) && step.GroundedBefore && step.GroundedAfter &&
            !step.Launched && step.AllowsFootstep && step.HorizontalDistance > .0001f && step.SupportCollider != null;

        private void SubmitFootstep(Observation item, SideMovementStep step, string factKey = null)
        {
            if (!IsCurrent(item, step) || audioRoot == null || !audioRoot.WorldAudioEnabled || step.SupportCollider == null) return;
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
            LastDecision = source.Submit(cue, factKey ?? "step:" + step.StepId, step.WorldPosition).Reason;
        }

        private void SubmitTraversal(Observation item, SideTraversalEvent fact)
        {
            if (fact.ContactEpoch != item.movement.ContactRevision) return;
            SfxEventDefinitionSO definition;
            SideViewMotionKind kind;
            var towardWall = Vector3.right * fact.WallSide;
            var surfaceOut = -towardWall;
            Vector3 release;
            switch (fact.Kind)
            {
                case SideTraversalEventKind.Grab:
                    definition = grabCue; kind = SideViewMotionKind.Grab; release = towardWall; break;
                case SideTraversalEventKind.WallJump:
                    definition = wallJumpCue; kind = SideViewMotionKind.WallJump; release = surfaceOut + Vector3.up; break;
                case SideTraversalEventKind.ClimbComplete:
                    definition = climbCue; kind = SideViewMotionKind.Climb; release = Vector3.up; surfaceOut = Vector3.up; break;
                case SideTraversalEventKind.Drop:
                    definition = dropCue; kind = SideViewMotionKind.Drop; release = Vector3.down; break;
                default: return;
            }
            var travel = item.latestMotion is SideMovementStep step && step.StepId == fact.StepId
                ? StepTravel(step) : release;
            OfferContact(item, kind, definition, fact.ContactCollider, fact.ContactPosition ?? fact.Position,
                fact.StepId, fact.ContactEpoch, 0, release, travel, surfaceOut);
        }

        private static Vector3 StepTravel(SideMovementStep step) =>
            CombatContactGeometry.Project(step.WorldPosition - step.PreviousWorldPosition, CombatPlane.SideXY);

        private static Vector3 ContactDirection(CombatEffectDirection mode, Vector3 release, Vector3 travel, Vector3 surfaceOut)
        {
            if (mode == CombatEffectDirection.Fixed) return Vector3.right;
            if (mode == CombatEffectDirection.Surface && surfaceOut.sqrMagnitude > .000001f) return surfaceOut;
            if (mode != CombatEffectDirection.Released && travel.sqrMagnitude > .000001f) return travel;
            return release.sqrMagnitude > .000001f ? release : Vector3.right;
        }

        private void OfferContact(Observation item, SideViewMotionKind kind, SfxEventDefinitionSO fallback,
            Collider contact, Vector3 position, long step, long epoch, float downwardSpeed,
            Vector3 release, Vector3 travel, Vector3 surfaceOut)
        {
            string material = SideViewFeedbackRuntime.MaterialOf(contact, false);
            if (string.IsNullOrEmpty(material)) material = SideViewFeedbackRuntime.MaterialOf(contact, true);
            var profile = SideViewFeedbackRuntime.ProfileFor(item.actor);
            var row = profile == null ? null : profile.ResolveMovement(kind, material);
            var settings = row?.settings;
            bool customSound = settings?.overrideSound == true;
            var sound = customSound ? settings.sound : fallback;
            int life = item.actor.LifeGeneration;
            int revision = item.feedbackRevision;
            float intensity = kind == SideViewMotionKind.Land && settings != null && downwardSpeed >= settings.strongLandingSpeed
                ? settings.strongLandingMultiplier : 1f;
            string label = row == null ? "共用运动反馈" : profile.name + " / " + (row.material ?? "通用");
            double now = Time.realtimeSinceStartupAsDouble;
            CombatTraceProbe.For(item.actor)?.Note("运动反馈", kind.ToString(),
                $"事实 · 第 {step} 步 / 支撑代 {epoch} / {material ?? "无明确材质"} / 下落 {downwardSpeed:F2} · {label}");
            if (settings?.overrideEffect == true && settings.effect != null && feedbackView != null)
            {
                var visual = settings.Copy();
                visual.effectScale *= intensity;
                var direction = ContactDirection(settings.direction, release, travel, surfaceOut);
                feedbackView.QueueContactEffect(visual, position, direction, item.actor,
                    follow: settings.followTarget ? item.actor.Vitals : null,
                    stillCurrent: () => IsCurrent(item, epoch, life, revision));
            }
            if (sound == null)
            {
                LastDecision = customSound ? "AUTHORED_MUTED" : "ASSET_BLOCKED_MOTION_" + kind;
                CombatTraceProbe.For(item.actor)?.Note("运动反馈", kind.ToString(), LastDecision);
                return;
            }
            if (item.delayed.Count >= 32) return;
            SfxPlaybackSettings? playback = null;
            if (customSound)
                playback = new SfxPlaybackSettings(Mathf.Clamp01(sound.volume * settings.volume * intensity),
                    settings.pitchMin, settings.pitchMax, settings.soundStartMs);
            else if (!Mathf.Approximately(intensity, 1f))
                playback = new SfxPlaybackSettings(Mathf.Clamp01(sound.volume * intensity), sound.pitchMin, sound.pitchMax, 0);
            item.delayed.Add(new PendingMotionSound
            {
                kind = kind, sound = sound, position = position, step = step, epoch = epoch, life = life, revision = revision,
                occurredAt = now, dueAt = now + (customSound ? Math.Max(0, settings.soundDelayMs) / 1000.0 : 0),
                material = material, profile = label,
                playback = playback
            });
        }

        private static bool IsCurrent(Observation item, long epoch, int life, int revision)
            => Eligible(item) && item.actor.LifeGeneration == life && item.feedbackRevision == revision &&
                item.movement.ContactRevision == epoch;

        private void FlushSounds(Observation item)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < item.delayed.Count;)
            {
                var cue = item.delayed[i];
                if (!IsCurrent(item, cue.epoch, cue.life, cue.revision))
                {
                    item.delayed.RemoveAt(i);
                    continue;
                }
                if (now < cue.dueAt) { i++; continue; }
                item.delayed.RemoveAt(i);
                var source = Lease(item);
                if (source == null) continue;
                if (cue.kind == SideViewMotionKind.Jump) SubmittedJumps++;
                else if (cue.kind == SideViewMotionKind.Land) SubmittedLandings++;
                else SubmittedTraversals++;
                var decision = source.SubmitConfirmed(cue.sound.eventId, cue.sound,
                    "motion:"+cue.kind+":"+cue.step+":"+cue.epoch, cue.position, cue.occurredAt, cue.playback);
                LastDecision = decision.Reason;
                CombatTraceProbe.For(item.actor)?.Note("运动反馈", cue.kind.ToString(),
                    $"声音提交 {cue.sound.eventId} / {cue.material ?? "通用"} / {cue.profile} · {decision.Reason}");
            }
        }

        private void OnTrialReset(int scene)
        {
            if (scene != gameObject.scene.handle) return;
            foreach (var item in observations) item.Reset();
        }

        private static void Detach(Observation item)
        {
            if (item.animation != null) item.animation.SpineSignal -= item.markerCallback;
            if (item.movement != null)
            {
                item.movement.SideStepCompleted -= item.callback;
                item.movement.SideTraversalOccurred -= item.traversalCallback;
            }
            item.Reset();
        }

        private void OnDisable()
        {
            SideViewFeedbackRuntime.TrialReset -= OnTrialReset;
            foreach (var item in observations) Detach(item);
            observations.Clear();
            audioRoot = null;
            world = null;
            coordinator = null;
            previousCurrent = null;
        }
    }
}
