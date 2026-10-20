/*
 * 脚本：StationaryMeleeEnemy
 * 功能：固定站位的真实验证敌人，近距可见目标通过单一动作时钟释放近战。
 * 边界：无寻路/追击/行为树，不直接扣HP、不用开发靶充敌人，不自动返场。
 * 协作：独立Vitals持有生命，复用BasicActionRuntime→CharacterMeleeHit；目标失效/出圈/遮挡具名取消。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using UnityEngine;
using Root = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Enemies
{
    [RequireComponent(typeof(CharacterVitals),typeof(CharacterMeleeHit))]
    public sealed class StationaryMeleeEnemy : MonoBehaviour
    {
        [SerializeField] private string actorId="enemy-stationary-01";
        [SerializeField] private Root[] targets=Array.Empty<Root>();
        [SerializeField, Min(.1f)] private float triggerDistance=1.7f, attackInterval=1.8f;
        [SerializeField] private float releaseAt=.5f, moveReadyAt=.6f, attackReadyAt=.9f, finishAt=1.1f;
        [SerializeField, Min(1)] private int damage=20;
        [SerializeField] private bool attacksEnabled=true;
        private CharacterVitals vitals;
        private CharacterMeleeHit melee;
        private BasicActionRuntime runtime;
        private BasicAttackDefinition attack;
        private readonly ControlAuthority authority=new ControlAuthority();
        private Root target;
        private float nextAttack;
        private bool focused=true, applicationPaused;
        public CharacterVitals Vitals => vitals;
        public string LastFact { get; private set; }="Idle";
        public string LastHit => melee==null?"未初始化":melee.LastResult;
        public bool ActionActive => runtime!=null && runtime.IsActive;
        public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if (!value) Cancel("development-attacks-disabled"); } }

        private void Awake()
        {
            vitals=GetComponent<CharacterVitals>(); melee=GetComponent<CharacterMeleeHit>();
            try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage); runtime=new BasicActionRuntime(actorId); }
            catch (ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
            if (!vitals.Initialize(actorId)) { Debug.LogError("Enemy identity invalid",this); enabled=false; return; }
            vitals.Died+=OnDied; nextAttack=Time.time+attackInterval;
        }

        private bool Eligible(Root candidate)
        {
            if (candidate==null || !candidate.isActiveAndEnabled || !candidate.IsAlive || candidate.Vitals==null || !candidate.Vitals.IsAlive ||
                !CombatTargetPolicy.CanHit(vitals.Health.Id,vitals.Faction,vitals.IsAlive,candidate.ActorId,candidate.Vitals.Faction,candidate.IsAlive)) return false;
            var offset=candidate.transform.position-transform.position; offset.y=0;
            if (offset.magnitude>triggerDistance) return false;
            var start=transform.position+Vector3.up*.8f;
            var end=candidate.transform.position+Vector3.up*.8f;
            // 不借目标半径隔墙感知；忽略自身/该目标的碰撞体，其余实体为遮挡。
            foreach (var hit in Physics.RaycastAll(start,end-start,Vector3.Distance(start,end),~0,QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(candidate.transform)) return false;
            return true;
        }

        private void Update()
        {
            if (vitals==null || !vitals.IsAlive || runtime==null || !attacksEnabled || !focused || applicationPaused) return;
            if (runtime.IsActive && !Eligible(target)) Cancel("target-dead-out-of-range-or-occluded");
            if (!runtime.IsActive && Time.time>=nextAttack)
            {
                target=null; var nearest=float.MaxValue;
                foreach (var candidate in targets)
                    if (Eligible(candidate))
                    { var distance=(candidate.transform.position-transform.position).sqrMagnitude; if (distance<nearest) { nearest=distance; target=candidate; } }
                if (target!=null)
                {
                    var request=runtime.Request(attack,"stationary-enemy",authority);
                    if (request.Accepted) { LastFact="Accepted #"+request.Fact.ActionId.ToString("N"); nextAttack=Time.time+attackInterval; }
                }
            }
            foreach (var fact in runtime.Advance(Time.deltaTime,false))
            {
                LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
                if (fact.Release!=null)
                {
                    var direction=target.transform.position-transform.position; direction.y=0;
                    melee.Resolve(fact.Release,direction.normalized);
                }
            }
        }
        private void Cancel(string reason)
        { var fact=runtime==null?null:runtime.Cancel(reason); if (fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N"); target=null; }
        private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
        private void OnDisable() { Cancel("enemy-disabled"); }
        private void OnApplicationFocus(bool value) { focused=value; if (!value) Cancel("focus-lost"); }
        private void OnApplicationPause(bool value) { applicationPaused=value; if (value) Cancel("application-paused"); }
        private void OnDestroy() { if (vitals!=null) vitals.Died-=OnDied; }
        public void RestoreForDevelopment() { Cancel("development-reset"); vitals.RestoreForDevelopment(); nextAttack=Time.time+attackInterval; }
        private void OnDrawGizmosSelected() { Gizmos.color=Color.red; Gizmos.DrawWireSphere(transform.position+Vector3.up*.8f,triggerDistance); }
    }
}
