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
        [Header("事件普攻首段 · 命中停顿")]
        public bool enableAttackHitStop;
        [Range(0f,.12f)] public float hitStopSeconds = .045f;
        public bool stopAttacker = true, stopVictim = true;
        private Guid hitStopAttack, stoppedEmission;
        private int hitFrame;
        private double stopUntil;
        public bool InHitStop => Time.frameCount > hitFrame && Time.unscaledTimeAsDouble < stopUntil;
        internal void ArmHitStop(Guid parent) { hitStopAttack = parent; stoppedEmission = Guid.Empty; }
        internal void DisarmHitStop(Guid parent) { if(hitStopAttack == parent) hitStopAttack = Guid.Empty; }
        internal void ApplyHitStop(AttackRelease release, CombatVitals victim)
        {
            if (!enableAttackHitStop || hitStopAttack != release.ParentExecutionId || hitStopSeconds <= 0) return;
            if (stopAttacker && stoppedEmission != release.ActionId) BeginHitStop(hitStopSeconds);
            stoppedEmission = release.ActionId;
            if (stopVictim && victim != null && victim.IsAlive) victim.BeginHitStop(hitStopSeconds);
        }
        private void BeginHitStop(float seconds)
        {
            if (stopUntil > Time.unscaledTimeAsDouble) return;
            hitFrame = Time.frameCount;
            stopUntil = Time.unscaledTimeAsDouble + Mathf.Clamp(seconds, 0, .12f);
        }
        private void ResetHitStop() { hitStopAttack = stoppedEmission = Guid.Empty; stopUntil = 0; }
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
