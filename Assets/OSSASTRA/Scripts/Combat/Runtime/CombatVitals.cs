/*
 * 脚本：CombatVitals
 * 功能：持有本体唯一Health/阵营，接收正式结算并通知可选虚血模块，单次死亡时关闭表现和碰撞。
 * 边界：IsAlive只读Health；不另存isDead，不选Current、不计算攻击、不自动复活。
 * 协作：Root/敌人显式Initialize；Melee反馈已Applied结果；受击时钟驱动独立材质闪白；返场仍恢复同一本体。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class CombatVitals : MonoBehaviour, ICombatTarget
    {
        [SerializeField, Min(1)] private int maximumHp=100;
        [SerializeField] private CombatFaction faction=CombatFaction.Allied;
        [SerializeField, Tooltip("横版不透明实体。仅在碰撞体贴合外观时开启，让接触特效落在朝相机的实体表面。")]
        private bool sideViewOpaqueImpactSurface;
        internal bool SideViewOpaqueImpactSurface => sideViewOpaqueImpactSurface;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool[] rendererEnabled, colliderEnabled;
        private const float HitReactionSeconds = .15f;
        private CombatHitFlash hitFlash;
        private bool deathNotified, hitReacting;
        private CombatRecovery recovery;
        private CombatRegeneration regeneration;
        public CombatRecovery Recovery=>recovery;
        private float hitReactUntil;
        [Header("旧版调试 · 首段普攻停顿")]
        public bool enableAttackHitStop;
        [Range(0f,.12f)] public float hitStopSeconds = .045f;
        public bool stopAttacker = true, stopVictim = true;
        private readonly LocalHitStopClock hitStop = new LocalHitStopClock();
        private Behaviour hitStopConsumer;
        private Guid stopRelease, stopParent;
        private int stopEmission;
        public bool InHitStop => hitStop.Frozen;
        public bool SupportsLocalHitStop => hitStopConsumer != null && hitStopConsumer.isActiveAndEnabled;
        public string LastHitStop { get; private set; } = "尚未请求";
#if UNITY_EDITOR
        public AttackHitStop? LaboratoryHitStop { get; set; }
#endif
        internal void RegisterHitStopConsumer(Behaviour owner) => hitStopConsumer = owner;
        internal AttackHitStop CaptureHitStop(AttackHitStop authored, bool supportsLegacyPreview)
        {
#if UNITY_EDITOR
            if (LaboratoryHitStop.HasValue) return LaboratoryHitStop.Value;
#endif
            if (authored.configured || !enableAttackHitStop || !supportsLegacyPreview) return authored;
            return new AttackHitStop
            {
                configured = true,
                attackerSeconds = stopAttacker ? hitStopSeconds : 0,
                receiverSeconds = stopVictim ? hitStopSeconds : 0
            };
        }

        internal void ApplyHitStop(AttackRelease release, CombatVitals receiver, HitOutcome outcome)
        {
            var config = release.HitStop;
            if (!config.Accepts(outcome.Reason)) return;
            if (release.TryClaimAttackerStop()) BeginHitStop(release, config.attackerSeconds);
            if (config.receiverSeconds <= 0 || outcome.HpAfter <= 0) return;
            if (receiver != null && receiver.SupportsLocalHitStop)
                receiver.BeginHitStop(release, config.receiverSeconds);
            else
                CombatTraceProbe.For(this)?.Note("反馈", "目标未接局部卡肉", outcome.TargetId,
                    release.ActionId, release.ParentExecutionId, release.EmissionIndex, outcome.TargetId);
        }

        private void BeginHitStop(AttackRelease release, float seconds)
        {
            if (!SupportsLocalHitStop || !IsAvailable ||
                !hitStop.Request(release.ParentExecutionId, seconds, Time.frameCount, Time.unscaledTimeAsDouble))
                return;
            stopRelease = release.ActionId;
            stopParent = release.ParentExecutionId;
            stopEmission = release.EmissionIndex;
            TraceHitStop("卡肉请求", $"{hitStop.RequestedSeconds * 1000:0.#} ms");
        }

        private void AdvanceHitStop()
        {
            if (!SupportsLocalHitStop)
            {
                ResetHitStop();
                return;
            }
            var edge = hitStop.Advance(Time.frameCount, Time.unscaledTimeAsDouble);
            if (edge == HitStopEdge.Started)
                TraceHitStop("卡肉开始", $"未缩放时钟 {hitStop.StartedAt:F3} s");
            if (edge == HitStopEdge.Ended)
                TraceHitStop("卡肉结束", $"实测 {(hitStop.FinishedAt - hitStop.StartedAt) * 1000:0.#} ms / {hitStop.Frames} 帧");
        }

        internal void CancelHitStopForParent(Guid parent)
        {
            if (hitStop.Cancel(parent)) TraceHitStop("卡肉取消", "动作撤销");
        }
        internal void ResetHitStop()
        {
            bool active = hitStop.Parent != Guid.Empty;
            hitStop.Reset();
            if (active) TraceHitStop("卡肉取消", "本体状态变更");
        }

        private void TraceHitStop(string label, string detail)
        {
            LastHitStop = label + " · " + detail;
            var trace = CombatTraceProbe.For(this) ?? CombatTraceProbe.ForExecution(this, stopParent);
            trace?.Note("反馈", label, detail, stopRelease, stopParent, stopEmission, Health?.Id);
        }
        public Health Health { get; private set; }
        public bool IsAlive => Health != null && Health.Current>0;
        public bool IsHitReacting => hitReacting && IsAlive;
        public bool IsAvailable => isActiveAndEnabled && IsAlive;
        public Vector3 WorldPosition => transform.position;
        public Transform TargetTransform => transform;
        public CombatFaction Faction => faction;
        internal CombatPlane SpatialPlane { get; set; }
        public int DeathCount { get; private set; }
        public string LastResult { get; private set; }="尚未受击";
        public event Action<HitOutcome> Died;

        public bool Initialize(string actorId)
        {
            if (Health != null) return Health.Id==actorId;
            if (string.IsNullOrWhiteSpace(actorId) || maximumHp<=0) return false;
            Health=new Health(actorId,maximumHp);
            recovery=GetComponent<CombatRecovery>();recovery?.Initialize(this);
            regeneration=GetComponent<CombatRegeneration>();regeneration?.Initialize(this);
            renderers=GetComponentsInChildren<Renderer>(true);
            hitFlash = new CombatHitFlash(renderers);
            colliders=GetComponentsInChildren<Collider>(true);
            rendererEnabled=new bool[renderers.Length]; colliderEnabled=new bool[colliders.Length];
            for (var i=0;i<renderers.Length;i++) rendererEnabled[i]=renderers[i].enabled;
            for (var i=0;i<colliders.Length;i++) colliderEnabled[i]=colliders[i].enabled;
            return true;
        }

        public void OnHitResolved(HitOutcome outcome)
        {
            if (Health==null || outcome==null || outcome.TargetId!=Health.Id || outcome.Reason!=HitReason.Applied) return;
            LastResult=outcome.ActorId+" → "+outcome.TargetId+" "+outcome.HpBefore+"→"+outcome.HpAfter+" #"+outcome.ActionId.ToString("N");
            recovery?.RecordLoss(outcome);
            if (IsAlive) { ShowHit(); return; }
            if (deathNotified) return;
            ResetHitStop(); deathNotified=true; DeathCount++;
            EndHitReaction();
            recovery?.ResetState();
            regeneration?.Clear();
            // 只退本体表现/碰撞，保留Root/身份/Health供会话与调试读取；死亡不靠禁用全组Root。
            foreach (var r in renderers) if (r!=null) r.enabled=false;
            foreach (var c in colliders) if (c!=null) c.enabled=false;
            Died?.Invoke(outcome);
        }

        public void ShowHit()
        {
            if (!IsAlive) return;
            hitReactUntil = Time.time + HitReactionSeconds;
            hitReacting = true;
            hitFlash.Apply();
        }

        private void Update()
        {
            AdvanceHitStop();
            if (InHitStop && hitReacting) hitReactUntil += Time.deltaTime;
            if (hitReacting && Time.time >= hitReactUntil) EndHitReaction();
        }

        // 当前帧换装或切灯光方案时，同一脉冲继续作用于新材质。
        private void LateUpdate()
        {
            if (hitReacting) hitFlash.Apply();
        }

        private void OnDisable() { ResetHitStop(); EndHitReaction(); }

        private void EndHitReaction()
        {
            if (!hitReacting) return;
            hitReacting = false;
            hitFlash.Clear();
        }

        /// <summary>开发回放恢复同一身份/Health，仅还原本组件关掉的Renderer/Collider；无生产计时返场。</summary>
        public void RestoreForDevelopment()
        {
            if (Health==null) return;
            Health.RestoreForDevelopment(); RestoreBody("Development重置");
        }
        public void TickResources(double delta){recovery?.Tick(delta);regeneration?.Tick(delta);}
        internal bool RestoreAfterDeath()
        {
            if(Health==null || !isActiveAndEnabled || !Health.RestoreAfterDeath()) return false;
            RestoreBody("场景内返场"); return true;
        }
        private void RestoreBody(string reason)
        {
            ResetHitStop(); deathNotified=false; LastResult=reason;
            EndHitReaction();
            recovery?.ResetState();
            for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].enabled=rendererEnabled[i];
            for (var i=0;i<colliders.Length;i++) if (colliders[i]!=null) colliders[i].enabled=colliderEnabled[i];
        }
    }
}
