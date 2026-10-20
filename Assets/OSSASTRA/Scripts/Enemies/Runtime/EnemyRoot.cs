/*
 * 脚本：EnemyRoot
 * 功能：敌方唯一组合根，组织身份、生命、决策与单一动作推进/释放。
 * 边界：无玩家Root/Input/Bus/Current，不选最近目标、不直接扣HP。
 * 协作：Brain提交并验证目标；根独占动作和可选架势推进，破势/生命周期撤销未释放动作。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Enemies
{
    [RequireComponent(typeof(CombatVitals),typeof(CombatMeleeHit),typeof(StationaryEnemyBrain))]
    public sealed class EnemyRoot : MonoBehaviour
    {
        [SerializeField] private string actorId="enemy-stationary-01";
        [SerializeField] private float releaseAt=.5f, moveReadyAt=.6f, attackReadyAt=.9f, finishAt=1.1f;
        [SerializeField, Min(1)] private int damage=20;
        [SerializeField, Min(0)] private float postureImpact;
        [SerializeField] private CombatPosture posture;
        [SerializeField] private bool attacksEnabled=true;
        private CombatVitals vitals;
        private ICombatHitResolver melee;
        private StationaryEnemyBrain brain;
        private BasicActionRuntime runtime;
        private BasicAttackDefinition attack;
        private readonly ControlAuthority authority=new ControlAuthority();
        private ICombatTarget target;
        private Health targetHealth;
        private bool focused=true, applicationPaused, paused;
        public string ActorId => actorId;
        public CombatVitals Vitals => vitals;
        public string LastFact { get; private set; }="Idle";
        public string LastHit => melee==null?"未初始化":melee.LastResult;
        public bool ActionActive => runtime!=null && runtime.IsActive;
        public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if(!value) Cancel("development-attacks-disabled"); } }
        private void Awake()
        {
            vitals=GetComponent<CombatVitals>(); melee=GetComponent<CombatMeleeHit>(); brain=GetComponent<StationaryEnemyBrain>();
            try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage,postureImpact); runtime=new BasicActionRuntime(actorId); }
            catch(ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
            if(!vitals.Initialize(actorId) || brain==null || !brain.Initialize(vitals))
            { Debug.LogError("Enemy identity/brain invalid",this); enabled=false; return; }
            vitals.Died+=OnDied;
            if (posture != null)
            {
                if (!posture.Initialize(vitals, this)) { Debug.LogError("Enemy posture configuration invalid", this); enabled=false; return; }
                posture.Broken += OnPostureBroken;
                posture.Recovered += OnPostureRecovered;
            }
        }
        private void Update()
        {
            if(runtime==null) return;
            if(vitals==null || !vitals.IsAvailable) { Cancel("source-unavailable"); return; }
            // 1. 根独占架势时钟，禁用开发攻击不冻结恢复；失焦/暂停停钟。
            if (posture != null) posture.Tick(!focused || applicationPaused || paused ? 0 : Time.deltaTime, runtime.IsActive);
            if(!attacksEnabled || !focused || applicationPaused || paused || !authority.Allows(ControlChannel.Attack)) return;
            // 2. 决策资格先于动作请求；Brain不掌握架势数值或第二时钟。
            if(brain==null || !brain.isActiveAndEnabled) { Cancel("brain-disabled"); return; }
            if(runtime.IsActive && (!brain.IsEligible(target) || !ReferenceEquals(target.Health,targetHealth)))
                Cancel("target-dead-out-of-range-or-occluded-or-stale");
            if(!runtime.IsActive && brain.TrySelect(Time.time,out var selected))
            {
                var request=runtime.Request(attack,"stationary-enemy",authority);
                if(request.Accepted)
                {
                    target=selected; targetHealth=selected.Health; brain.OnAccepted(Time.time);
                    LastFact="Accepted #"+request.Fact.ActionId.ToString("N");
                }
            }
            // 3. 唯一动作推进/命中阶段；破势回调立即Cancel，不保留冻结的待释放动作。
            foreach(var fact in runtime.Advance(Time.deltaTime,false))
            {
                LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
                if(fact.Release!=null)
                {
                    var direction=target.WorldPosition-transform.position; direction.y=0;
                    melee.Resolve(fact.Release,direction.normalized);
                }
            }
        }
        public void Cancel(string reason)
        {
            var fact=runtime==null?null:runtime.Cancel(reason);
            if(fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N");
            target=null; targetHealth=null;
        }
        public void SetPaused(bool value) { paused=value; if(value) Cancel("enemy-paused"); }
        private void OnPostureBroken(string cause)
        {
            authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
            Cancel("posture-broken:" + cause);
        }
        private void OnPostureRecovered() { authority.Release(posture); }
        private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
        private void OnDisable() { Cancel("enemy-disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); }
        private void OnApplicationFocus(bool value) { focused=value; if(!value) Cancel("focus-lost"); }
        private void OnApplicationPause(bool value) { applicationPaused=value; if(value) Cancel("application-paused"); }
        private void OnDestroy()
        {
            if(vitals!=null) vitals.Died-=OnDied;
            if(posture!=null) { posture.Broken-=OnPostureBroken; posture.Recovered-=OnPostureRecovered; }
            authority.Release(posture);
            Cancel("destroyed");
        }
        public void RestoreForDevelopment()
        { Cancel("development-reset"); vitals.RestoreForDevelopment(); if(posture!=null) posture.ResetState(); brain.ResetDecision(Time.time); }
    }
}
