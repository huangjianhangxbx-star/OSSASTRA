/*
 * 脚本：EnemyRoot
 * 功能：敌方唯一组合根，组织身份、生命、决策与单一动作推进/释放。
 * 边界：无玩家Root/Input/Bus/Current，不选最近目标、不直接扣HP。
 * 协作：Brain提交并验证目标；根独占BasicActionRuntime，生命周期撤销未释放动作。
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
            try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage); runtime=new BasicActionRuntime(actorId); }
            catch(ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
            if(!vitals.Initialize(actorId) || brain==null || !brain.Initialize(vitals))
            { Debug.LogError("Enemy identity/brain invalid",this); enabled=false; return; }
            vitals.Died+=OnDied;
        }
        private void Update()
        {
            if(runtime==null) return;
            if(vitals==null || !vitals.IsAvailable) { Cancel("source-unavailable"); return; }
            if(!attacksEnabled || !focused || applicationPaused || paused) return;
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
            // 唯一推进者；Brain没有Update/Advance，不生成第二套释放事实。
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
        private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
        private void OnDisable() { Cancel("enemy-disabled"); }
        private void OnApplicationFocus(bool value) { focused=value; if(!value) Cancel("focus-lost"); }
        private void OnApplicationPause(bool value) { applicationPaused=value; if(value) Cancel("application-paused"); }
        private void OnDestroy() { if(vitals!=null) vitals.Died-=OnDied; Cancel("destroyed"); }
        public void RestoreForDevelopment()
        { Cancel("development-reset"); vitals.RestoreForDevelopment(); brain.ResetDecision(Time.time); }
    }
}
