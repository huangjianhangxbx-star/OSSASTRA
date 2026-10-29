/*
 * 脚本：EnemyRoot
 * 功能：敌方唯一组合根，组织身份、生命、决策与单一动作推进/释放。
 * 边界：无玩家Root/Input/Bus/Current，不选最近目标、不直接扣HP。
 * 协作：Brain提交并验证目标；根独占动作和可选架势推进，破势/生命周期撤销未释放动作；LateUpdate组织可选观察后再呈现。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Enemies
{
    [RequireComponent(typeof(CombatVitals),typeof(CombatMeleeHit))]
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
        private CombatMeleeHit hitShape;
        private readonly Ossastra.EnemyCore.AttackWarningState warning = new Ossastra.EnemyCore.AttackWarningState();
        public Guid ActionId => runtime == null ? Guid.Empty : runtime.ActionId;
        public bool WarningVisible => isActiveAndEnabled && focused && !applicationPaused && !paused && warning.Tracking && vitals!=null && vitals.IsAvailable && target!=null && target.IsAvailable;
        public bool AttackReleased => warning.Released;
        public Vector3 AttackDirection { get { var d=target==null?Vector3.forward:target.WorldPosition-transform.position; d.y=0; return d.normalized; } }
        public Vector3 WarningCenter => transform.position+AttackDirection*(hitShape==null?0:hitShape.Reach);
        public float WarningRadius => hitShape==null?0:hitShape.Radius;
        private IEnemyBrain brain;
        private IEnemyMovement movement;
        private CombatStatusEffects statusEffects;
        private BasicActionRuntime runtime;
        private BasicAttackDefinition attack;
        private readonly ControlAuthority authority=new ControlAuthority();
        private ICombatTarget target;
        private Health targetHealth;
        private bool focused=true, applicationPaused, paused;
        public event Action<ActionFact> ActionObserved;
        public bool HasCombatTarget => brain is ChaseEnemyBrain chase && chase.TargetId!="none";
        public string ActorId => actorId;
        public CombatVitals Vitals => vitals;
        public string LastFact { get; private set; }="Idle";
        public string LastHit => melee==null?"未初始化":melee.LastResult;
        public bool ActionActive => runtime!=null && runtime.IsActive;
        public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if(!value) Cancel("development-attacks-disabled"); } }
        public int PresentationGeneration { get; private set; }
        public bool IsSimulationSuspended => !focused || applicationPaused || paused;
        private Ossastra.AnimationPresentation.IEntityPresentation presentation;
        private EnemyViewSpaceAdapter viewSpace;
        private EnemyMovement viewMovement;
        private void LateUpdate()
        {
            // 可选观察仅更新纯视觉，原决策/动作/命中仍在唯一Update中推进。
            if(viewSpace!=null)viewSpace.TickView(ActionActive?AttackDirection:viewMovement==null?Vector3.zero:viewMovement.Velocity);
            presentation?.Present();
        }
        private void Awake()
        {
            presentation=GetComponent<EnemySpineView>();
            viewSpace=GetComponent<EnemyViewSpaceAdapter>();
            if(viewSpace!=null&&!viewSpace.Initialize(transform))
            {Debug.LogError("敌人观察适配缺少合法视觉子层。",this);viewSpace.enabled=false;}
            vitals=GetComponent<CombatVitals>(); melee=GetComponent<CombatMeleeHit>(); hitShape=GetComponent<CombatMeleeHit>();
            var chase=GetComponent<ChaseEnemyBrain>();
            brain=chase!=null&&chase.enabled?(IEnemyBrain)chase:GetComponent<StationaryEnemyBrain>();
            movement=GetComponent<EnemyMovement>();
            viewMovement=movement as EnemyMovement;
            statusEffects=GetComponent<CombatStatusEffects>();if(statusEffects!=null)statusEffects.ControlChanged+=OnStatusControlChanged;
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
            statusEffects?.Tick(IsSimulationSuspended?0:Time.deltaTime);
            if(statusEffects!=null&&statusEffects.BlocksIntent)
            {movement?.Tick(Vector3.zero,IsSimulationSuspended?0:Time.deltaTime);return;}
            if(!attacksEnabled || !focused || applicationPaused || paused || !authority.Allows(ControlChannel.Attack)) {movement?.Stop();return;}
            // 2. 决策资格先于动作请求；Brain不掌握架势数值或第二时钟。
            if(brain==null || !brain.IsAvailable) { Cancel("brain-disabled"); return; }
            brain.Tick(Time.time,Time.deltaTime);
            if(runtime.IsActive && (!brain.IsEligible(target) || !ReferenceEquals(target.Health,targetHealth)))
                Cancel("target-dead-out-of-range-or-occluded-or-stale");
            if(!runtime.IsActive && brain.TrySelect(Time.time,out var selected))
            {
                var request=runtime.Request(attack,"enemy-brain",authority);
                if(request.Accepted)
                {
                    target=selected; targetHealth=selected.Health; brain.OnAccepted(Time.time);
                    LastFact="Accepted #"+request.Fact.ActionId.ToString("N");
                    warning.Accept(request.Fact.ActionId);
                    ActionObserved?.Invoke(request.Fact);
                }
            }
            // 3. 唯一动作推进/命中阶段；破势回调立即Cancel，不保留冻结的待释放动作。
            foreach(var fact in runtime.Advance(Time.deltaTime,false))
            {
                LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
                if(fact.Kind==ActionFactKind.Released)warning.Release(fact.ActionId);
                if(fact.Kind==ActionFactKind.Finished || fact.Kind==ActionFactKind.Cancelled)warning.Clear(fact.ActionId);
                ActionObserved?.Invoke(fact);
                if(fact.Release!=null)
                {
                    var direction=target.WorldPosition-transform.position; direction.y=0;
                    melee.Resolve(fact.Release,direction.normalized);
                }
            }
            // 根的最后物理阶段：只消费Brain方向；已接受攻击全段停步，避免贴人重叠攻击。
            movement?.Tick(runtime.IsActive?Vector3.zero:brain.MoveDirection,Time.deltaTime);
        }
        public void Cancel(string reason)
        {
            var fact=runtime==null?null:runtime.Cancel(reason);
            if(fact!=null) { PresentationGeneration++; warning.Clear(fact.ActionId);
                ActionObserved?.Invoke(fact); }
            if(fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N");
            target=null; targetHealth=null;
            movement?.Stop();
            brain?.ResetDecision(Time.time);
        }
        public void SetPaused(bool value) { paused=value; if(value) Cancel("enemy-paused"); }
        private void OnStatusControlChanged(){if(statusEffects!=null&&statusEffects.BlocksIntent)Cancel("combat-status");}
        private void OnPostureBroken(string cause)
        {
            authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
            Cancel("posture-broken:" + cause);
        }
        private void OnPostureRecovered() { authority.Release(posture); }
        private void OnDied(HitOutcome outcome) { statusEffects?.Clear();Cancel("death:"+outcome.ActorId); }
        private void OnDisable() { Cancel("enemy-disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); }
        private void OnApplicationFocus(bool value) { focused=value; if(!value) Cancel("focus-lost"); }
        private void OnApplicationPause(bool value) { applicationPaused=value; if(value) Cancel("application-paused"); }
        private void OnDestroy()
        {
            if(statusEffects!=null)statusEffects.ControlChanged-=OnStatusControlChanged;
            if(vitals!=null) vitals.Died-=OnDied;
            if(posture!=null) { posture.Broken-=OnPostureBroken; posture.Recovered-=OnPostureRecovered; }
            authority.Release(posture);
            Cancel("destroyed");
        }
        public void RestoreForDevelopment()
        { PresentationGeneration++; Cancel("development-reset"); vitals.RestoreForDevelopment(); if(posture!=null) posture.ResetState(); brain.ResetDecision(Time.time); }
    }
}
