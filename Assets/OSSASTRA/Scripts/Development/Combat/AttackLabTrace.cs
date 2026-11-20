/* 横版攻击调试台只读记录。正式角色、Spine 和战斗网关提供事实；关闭会话即解除订阅。 */
using System;
using System.Collections.Generic;
using System.Text;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using Ossastra.CharacterAnimation;
using Ossastra.Combat;
using Spine;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Development
{
    [Serializable]
    public sealed class AttackLabEvent
    {
        public int trial, frame, life, control, stage, physicsStep, emission = -1, mask, candidates;
        public int hpBefore, hpAfter, animationTrack = -1, recoveryBlockers;
        public string track, label, detail, actor, action, parent, target, scene, version, queryType;
        public string triggers, outcome, contactQuality, traversing, clip, phase, semantic, source;
        public double realMs, simMs, deviceSeconds = -1, posture, postureMax;
        public Vector3 start, end, direction, facing, contact, foot;
        public float radius, angle, duration, elapsed, localTime, mix, playbackRate;
        public string command, device, controlPath;
        public int deviceId;
        public long gesture;
        public bool pressed, released, uiCaptured, grounded, allowsMove, allowsAttack;
        public double actionSeconds;
        public float visualFacing;
        public float effectiveMarkerTime, collectedTrackTime, consumedUnscaledTime;
        public int markerOccurrence, markerCycle, replay;
        public long markerSequence;
        public Vector3 aim, arrivalDirection, releaseOrigin, sectorOrigin;
        public bool hasSector;
    }

    [DisallowMultipleComponent]
    public sealed partial class AttackLabTrace : MonoBehaviour, ICombatTraceSink
    {
        private const int Capacity = 2048;
        private const int TrialLimit = 8;
        private readonly Dictionary<Guid, int> firstTrial = new Dictionary<Guid, int>();
        private readonly Queue<Guid> identities = new Queue<Guid>();
        private readonly Queue<int> trials = new Queue<int>();
        private readonly List<AttackLabEvent> events = new List<AttackLabEvent>(Capacity);
        private ActorRoot actor;
        private CharacterAbilities abilities;
        private CharacterAnimationController animationDriver;
        private Ossastra.CharacterAnimation.AnimationHandleSystem animationHandle;
        private Spine.AnimationState animationState;
        private CombatPosture posture;
        private int trial, step, lastCommit, lastBlockers;
        private string version, lastRecovery;
        private SideViewTraversalMode lastTraversal;
        private bool lastGrounded, initialState;
        private double realStart, simStart;

        public IReadOnlyList<AttackLabEvent> Events => events;
        public int Dropped { get; private set; }
        public bool Recording => actor != null && ReferenceEquals(CombatTraceProbe.Sink, this);
        public bool Detailed { get; set; } = true;
        public bool ShowGeometry { get; set; } = true;
        public bool ShowTrail { get; set; } = true;
        public bool ShowScale { get; set; } = true;
        public bool Frozen { get; set; }
        bool ICombatTraceSink.Capturing => Recording && !Frozen;
        bool ICombatTraceSink.CaptureQueries => Detailed;
        bool ICombatTraceSink.KnowsExecution(Guid id) => firstTrial.TryGetValue(id, out int ownerTrial) && ownerTrial == trial;

        public void BeginTrial(ActorRoot source, int generation, string snapshotVersion)
        {
            EndTrial();
            if (!source) return;
            if (trials.Count == 0 || generation != trial)
            {
                step = 0;
                realStart = Time.realtimeSinceStartupAsDouble;
                simStart = source.SimulationTime;
                trials.Enqueue(generation);
                if (trials.Count > TrialLimit)
                {
                    int old = trials.Dequeue();
                    Dropped += events.RemoveAll(e => e.trial == old);
                }
            }
            actor = source;
            trial = generation;
            version = snapshotVersion ?? "";
            lastCommit = -1;
            initialState = true;
            ClearOverlay();
            CombatTraceProbe.Owner = source;
            CombatTraceProbe.Sink = this;
            actor.ActionObserved += OnAction;
            CombatContactGateway.Resolved += OnBatch;
            posture = actor.GetComponent<CombatPosture>();
            abilities = actor.GetComponent<CharacterAbilities>();
            if (abilities) abilities.FeedbackObserved += OnAbility;
            animationDriver = actor.GetComponentInChildren<CharacterAnimationController>(true);
            animationHandle = actor.GetComponentInChildren<Ossastra.CharacterAnimation.AnimationHandleSystem>(true);
            if (animationDriver) animationDriver.SpineSignal += OnSignal;
            BindAnimationState();
            Add("状态", "开始试验", version);
        }

        public void EndTrial()
        {
            if (actor) actor.ActionObserved -= OnAction;
            if (abilities) abilities.FeedbackObserved -= OnAbility;
            if (animationDriver) animationDriver.SpineSignal -= OnSignal;
            UnbindAnimationState();
            CombatContactGateway.Resolved -= OnBatch;
            if (ReferenceEquals(CombatTraceProbe.Sink, this))
            {
                CombatTraceProbe.Sink = null;
                CombatTraceProbe.Owner = null;
            }
            actor = null;
            abilities = null;
            animationDriver = null;
            animationHandle = null;
            posture = null;
        }

        private void OnDisable() => EndTrial();
        private void FixedUpdate() { if (Recording) step++; }

        private AttackLabEvent Add(string lane, string label, string detail, Guid action = default,
            Guid parent = default, int emission = -1, string target = "")
        {
            if (!Recording || Frozen) return null;
            if (IsRetired(action) || IsRetired(parent)) return null;
            Remember(action); Remember(parent);
            if (events.Count == Capacity)
            {
                events.RemoveRange(0, Capacity / 8);
                Dropped += Capacity / 8;
            }
            var e = new AttackLabEvent
            {
                track = lane, label = label, detail = detail ?? "", action = Id(action), parent = Id(parent),
                emission = emission, target = target, actor = actor.ActorId, trial = trial, scene = actor.gameObject.scene.name,
                version = version, frame = Time.frameCount, physicsStep = step, life = actor.LifeGeneration,
                control = actor.ActionControlRevision, stage = actor.AttackStage, facing = actor.LogicalDirection,
                realMs = 1000 * (Time.realtimeSinceStartupAsDouble - realStart), simMs = 1000 * (actor.SimulationTime - simStart),
                foot = Foot(), posture = posture ? posture.Current : 0, postureMax = posture ? posture.Maximum : 0,
                recoveryBlockers = posture?.ActionLedger?.RecoveryBlockers ?? 0, traversing = actor.DataBus?.TraversalMode.ToString(),
                grounded = actor.DataBus?.IsGrounded ?? false, allowsMove = actor.DataBus?.AllowsMovement ?? false,
                allowsAttack = actor.DataBus?.AllowsAttack ?? false, aim = actor.DataBus?.AttackDirection ?? Vector3.zero,
                visualFacing = actor.DataBus?.VisualFacingX ?? 0
            };
            events.Add(e);
            return e;
        }

        private bool IsRetired(Guid id) => id != Guid.Empty && firstTrial.TryGetValue(id, out var owner) && owner != trial;

        private void Remember(Guid id)
        {
            if (id == Guid.Empty || firstTrial.ContainsKey(id)) return;
            if (identities.Count == Capacity * 2) firstTrial.Remove(identities.Dequeue());
            firstTrial.Add(id, trial);
            identities.Enqueue(id);
        }

        private static string Id(Guid value) => value == Guid.Empty ? "" : value.ToString("N");

        void ICombatTraceSink.InputEdge(string command, string device, int deviceId, string control, long gesture,
            bool pressed, bool released, bool captured, double deviceSeconds)
        {
            var e = Add("输入", command + (pressed ? "按下" : "释放"), $"{device} · {control} · 捕获 {captured}");
            if (e == null) return;
            e.command = command; e.device = device; e.deviceId = deviceId; e.controlPath = control;
            e.gesture = gesture; e.pressed = pressed; e.released = released; e.uiCaptured = captured; e.deviceSeconds = deviceSeconds;
        }

        void ICombatTraceSink.Note(string lane, string label, string detail, Guid action, Guid parent,
            int emission, string target, double deviceSeconds)
        {
            var e = Add(lane, label, detail, action, parent, emission, target);
            if (e != null) e.deviceSeconds = deviceSeconds;
        }

        void ICombatTraceSink.Query(AttackRelease release, string shape, Vector3 start, Vector3 end, Vector3 direction,
            float radius, float angle, int mask, int count, string triggers, float duration, float elapsed, Vector3? sectorOrigin)
        {
            var e = Add("判定", shape, $"粗筛 {count} 个碰撞体 · 半径 {radius:0.###} · {triggers}",
                release.ActionId, release.ParentExecutionId, release.EmissionIndex);
            if (e == null) return;
            e.queryType = shape; e.start = start; e.end = end; e.direction = direction;
            e.radius = radius; e.angle = angle; e.mask = mask; e.candidates = count; e.triggers = triggers;
            e.duration = duration; e.elapsed = elapsed; e.hasSector = sectorOrigin.HasValue;
            e.sectorOrigin = sectorOrigin.GetValueOrDefault();
            latestQuery = e;
        }

        private void OnAction(ActionFact fact)
        {
            var e = Add("状态", fact.Kind.ToString(), fact.Source + " " + fact.Reason, fact.ActionId,
                fact.Release?.ParentExecutionId ?? Guid.Empty, fact.Release?.EmissionIndex ?? -1);
            if (e != null) { e.source = fact.Source; e.actionSeconds = fact.At; }
        }

        private void OnAbility(AbilityFeedbackFact fact)
        {
            var e = Add("状态", fact.Kind, fact.DefinitionId + " · " + fact.Channel + " · " + fact.DeliveryMode,
                fact.ExecutionId, fact.ExecutionId, fact.EmissionIndex);
            if (e == null) return;
            e.start = fact.Position; e.direction = fact.ReleaseDirection ?? Vector3.zero; e.source = fact.RequestSource;
        }

        private void OnBatch(CombatContactBatch batch)
        {
            if (!Recording || Frozen || !ReferenceEquals(batch.Source, actor.Vitals) ||
                batch.SourceSceneHandle != actor.gameObject.scene.handle) return;
            var row = Add("判定", "批次结算", "回执 " + batch.Receipts.Count + " · " + batch.Cause,
                batch.ActionId, batch.ParentExecutionId, batch.EmissionIndex);
            if (row == null) return;
            row.candidates = batch.Receipts.Count;
            row.start = batch.Origin;
            row.direction = batch.Geometry?.ReleaseDirection ?? Vector3.zero;
            row.releaseOrigin = batch.Geometry?.ReleaseOrigin ?? batch.Origin;
            row.arrivalDirection = batch.Geometry?.ArrivalDirection ?? Vector3.zero;
            if (batch.EnvironmentContact.HasValue)
            {
                row.contact = batch.EnvironmentContact.Value.Position;
                row.contactQuality = batch.EnvironmentContact.Value.Quality.ToString();
            }
            foreach (var receipt in batch.Receipts)
            {
                var hit = receipt.Outcome;
                var e = Add("判定", hit.Reason.ToString(), $"HP {hit.HpBefore} → {hit.HpAfter}",
                    batch.ActionId, batch.ParentExecutionId, batch.EmissionIndex, receipt.TargetId);
                e.contact = receipt.Contact.Position;
                e.contactQuality = receipt.Contact.Quality.ToString();
                e.outcome = hit.Reason.ToString(); e.hpBefore = hit.HpBefore; e.hpAfter = hit.HpAfter;
                if (hit.Reason == HitReason.Applied && receipt.Target)
                {
                    var feedback = Add("反馈", "受击回执", $"HP {hit.HpBefore} → {hit.HpAfter} · 受击状态 {receipt.Target.IsHitReacting}",
                        batch.ActionId, batch.ParentExecutionId, batch.EmissionIndex, receipt.TargetId);
                    feedback.hpBefore = hit.HpBefore; feedback.hpAfter = hit.HpAfter;
                }
            }
        }

        private void OnSignal(AnimationSignal signal)
        {
            string detail = signal.Calibrated
                ? $"{signal.Animation} · {signal.Meaning}/{signal.Purpose} · 第 {signal.Occurrence + 1} 次 · 源 {signal.Time:F3}s → 校准 {signal.EffectiveTime:F3}s · 采集轨时 {signal.CollectedTime:F3}s"
                : signal.Target + " · " + signal.Source;
            var e = Add("动画", "Spine · " + signal.Name, detail, signal.ActionId);
            if (e == null) return;
            e.localTime = signal.Time; e.animationTrack = signal.Track; e.phase = Id(signal.PhaseId);
            e.source = signal.Source.ToString();
            e.clip = signal.Animation;
            e.semantic = signal.Calibrated ? signal.Meaning.ToString() : "";
            e.effectiveMarkerTime = signal.EffectiveTime;
            e.collectedTrackTime = signal.CollectedTime;
            e.consumedUnscaledTime = signal.ConsumedTime;
            e.markerOccurrence = signal.Occurrence;
            e.markerCycle = signal.Cycle;
            e.markerSequence = signal.Sequence;
            e.replay = signal.ReplayId;
        }

        private void BindAnimationState()
        {
            var next = animationDriver ? animationDriver.Skeleton?.AnimationState : null;
            if (ReferenceEquals(next, animationState)) return;
            UnbindAnimationState();
            animationState = next;
            if (next == null) return;
            next.Event += OnSourceEvent;
            next.Start += OnTrackStart;
            next.End += OnTrackEnd;
        }

        private void UnbindAnimationState()
        {
            if (animationState == null) return;
            animationState.Event -= OnSourceEvent;
            animationState.Start -= OnTrackStart;
            animationState.End -= OnTrackEnd;
            animationState = null;
        }

        private void OnSourceEvent(TrackEntry entry, Spine.Event source)
        {
            if (!Recording || Frozen || entry == null || source == null) return;
            var action = Guid.Empty;
            if (animationHandle && animationHandle.IsCurrent(entry, out var bound)) action = bound.runtimeActionId;
            var item = Add("动画", "Spine 源回调", $"{source.Data.Name} · {entry.Animation?.Name} · 轨道 {entry.TrackIndex}", action);
            if (item == null) return;
            item.animationTrack = entry.TrackIndex;
            item.clip = entry.Animation?.Name;
            item.localTime = source.Time;
            item.collectedTrackTime = entry.TrackTime;
            item.playbackRate = entry.TimeScale;
        }

        private void OnTrackStart(TrackEntry entry) => Track("轨道进入", entry);
        private void OnTrackEnd(TrackEntry entry) => Track("轨道退出", entry);

        private void Track(string label, TrackEntry entry)
        {
            if (!Recording || Frozen || entry == null) return;
            Guid action = Guid.Empty, phase = Guid.Empty;
            if (animationHandle && animationHandle.IsCurrent(entry, out var bound))
            {
                action = bound.runtimeActionId; phase = bound.runtimePhaseId;
            }
            var e = Add("动画", label, $"{entry.Animation?.Name} · 轨道 {entry.TrackIndex} · {animationDriver.FallbackReason}", action);
            if (e == null) return;
            e.animationTrack = entry.TrackIndex; e.clip = entry.Animation?.Name; e.phase = Id(phase);
            e.localTime = entry.TrackTime; e.mix = entry.MixDuration; e.playbackRate = entry.TimeScale;
            e.semantic = animationDriver.CurrentSemantic.ToString();
            e.source = animationDriver.CapturedSource.ToString();
        }

        private void LateUpdate()
        {
            if (!Recording || Frozen) return;
            BindAnimationState();
            if (animationDriver && animationState != null && lastCommit != animationDriver.CommitCount)
            {
                lastCommit = animationDriver.CommitCount;
                var tracks = animationState.Tracks;
                for (int i = 0; i < tracks.Count; i++) Track("实际轨道", tracks.Items[i]);
            }
            var bus = actor.DataBus;
            if (bus == null) return;
            int blockers = posture?.ActionLedger?.RecoveryBlockers ?? 0;
            string recovery = actor.LastPostureRecoveryEvent;
            if (initialState || bus.TraversalMode != lastTraversal || bus.IsGrounded != lastGrounded ||
                blockers != lastBlockers || recovery != lastRecovery)
            {
                Add("状态", "运动与架势", $"{bus.TraversalMode} · 接地 {bus.IsGrounded} · 恢复阻塞 {blockers} · {recovery}");
                initialState = false; lastTraversal = bus.TraversalMode; lastGrounded = bus.IsGrounded;
                lastBlockers = blockers; lastRecovery = recovery;
            }
            SampleFoot();
        }

        public void ObserveTestRequest(string requestedAction)
        {
            var e = Add("输入", "动作请求测试", requestedAction + " · 快捷按钮直接请求，未采样设备");
            if (e != null) e.source = "调试台按钮";
        }

        public string ExportText(int generation = -1)
        {
            var text = new StringBuilder();
            foreach (var e in events)
            {
                if (generation >= 0 && e.trial != generation) continue;
                text.AppendLine($"{e.trial}  {e.realMs:0.0}ms / {e.simMs:0.0}ms  {e.track}  {e.label}  {e.detail}  {e.action}  {e.target}");
            }
            return text.ToString();
        }
    }
}
