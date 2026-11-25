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
    [DefaultExecutionOrder(-99)]
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
        public Vector3 AttackDirection
        {
            get
            {
                if (sideView != null) return sideView.HitDirection;
                var direction = target == null ? Vector3.forward : target.WorldPosition - transform.position;
                direction.y = 0;
                return direction.normalized;
            }
        }
        public Vector3 WarningCenter => transform.position+AttackDirection*(hitShape==null?0:hitShape.Reach);
        public float WarningRadius => hitShape==null?0:hitShape.Radius;
        private IEnemyBrain brain;
        private IEnemyMovement movement;
        private CombatStatusEffects statusEffects;
        private BasicActionRuntime runtime;
        private BasicAttackDefinition attack, activeAttack;
        private SideViewEnemyBrain sideView;
        private AttackRelease pendingRelease;
        private readonly ControlAuthority authority=new ControlAuthority();
        private ICombatTarget target;
        private Health targetHealth;
        private bool focused=true, applicationPaused, paused;
        public event Action<ActionFact> ActionObserved;
        public bool HasCombatTarget => sideView != null ? sideView.HasTarget : brain is ChaseEnemyBrain chase && chase.TargetId!="none";
        public string ActorId => actorId;
        public CombatVitals Vitals => vitals;
        public string LastFact { get; private set; }="Idle";
        public string LastHit => melee==null?"未初始化":melee.LastResult;
        public bool ActionActive => runtime!=null && runtime.IsActive;
        public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if(!value) Cancel("development-attacks-disabled"); } }
        public int PresentationGeneration { get; private set; }
        public bool IsSimulationSuspended => !focused || applicationPaused || paused || (vitals != null && vitals.InHitStop);
        private float hitStopElapsed;
        internal float SimulationTime => Time.time - hitStopElapsed;
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
            sideView=GetComponent<SideViewEnemyBrain>();
            if (sideView != null)
            {
                brain = sideView;
                vitals.SpatialPlane = CombatPlane.SideXY;
            }
            else brain=chase!=null&&chase.enabled?(IEnemyBrain)chase:GetComponent<StationaryEnemyBrain>();
            movement=GetComponent<EnemyMovement>();
            viewMovement=movement as EnemyMovement;
            statusEffects=GetComponent<CombatStatusEffects>();if(statusEffects!=null)statusEffects.ControlChanged+=OnStatusControlChanged;
            try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage,postureImpact); runtime=new BasicActionRuntime(actorId); }
            catch(ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
            if(!vitals.Initialize(actorId) || brain==null || !brain.Initialize(vitals))
            { Debug.LogError("Enemy identity/brain invalid",this); enabled=false; return; }
            vitals.RegisterHitStopConsumer(this);
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
            (presentation as EnemySpineView)?.SynchronizeClock();
            if(vitals.InHitStop)
            {
                hitStopElapsed += Time.deltaTime;
                return;
            }
            float now = SimulationTime;
            // 1. 根独占架势时钟，禁用开发攻击不冻结恢复；失焦/暂停停钟。
            if (posture != null) posture.Tick(!focused || applicationPaused || paused ? 0 : Time.deltaTime, runtime.IsActive);
            statusEffects?.Tick(IsSimulationSuspended?0:Time.deltaTime);
            if(statusEffects!=null&&statusEffects.BlocksIntent)
            {movement?.Tick(Vector3.zero,IsSimulationSuspended?0:Time.deltaTime);return;}
            if (!focused || applicationPaused || paused || !authority.Allows(ControlChannel.Attack))
            { movement?.Stop(); return; }
            if (!attacksEnabled)
            {
                // 训练场停用攻击 AI 的可动靶仍沿原运动所有者消费受击请求。
                if (sideView != null) sideView.StepImpactOnly(Time.deltaTime);
                else if (viewMovement && viewMovement.Impact && viewMovement.Impact.HasMotion)
                    viewMovement.Tick(Vector3.zero, Time.deltaTime);
                else movement?.Stop();
                return;
            }
            // 2. 决策资格先于动作请求；Brain不掌握架势数值或第二时钟。
            if(brain==null || !brain.IsAvailable) { Cancel("brain-disabled"); return; }
            brain.Tick(now,Time.deltaTime);
            var targetStillValid = brain.IsEligible(target) && ReferenceEquals(target.Health, targetHealth);
            if (runtime.IsActive &&
                !(sideView == null ? targetStillValid : Ossastra.EnemyCore.SideViewEnemyRules.HoldsCommit(targetStillValid, warning.Released)))
                Cancel("target-dead-out-of-range-or-occluded-or-stale");
            if(!runtime.IsActive && brain.TrySelect(now,out var selected))
            {
                activeAttack = sideView == null ? attack : sideView.ChooseAttack(attack);
                var request=runtime.Request(activeAttack,"enemy-brain",authority);
                if(request.Accepted)
                {
                    target=selected; targetHealth=selected.Health; brain.OnAccepted(now);
                    sideView?.Commit(selected);
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
                sideView?.Observe(fact, now);
                ActionObserved?.Invoke(fact);
                if(fact.Release!=null)
                {
                    if (sideView != null) pendingRelease = fact.Release;
                    else melee.Resolve(fact.Release, AttackDirection);
                }
            }
            if (sideView == null) movement?.Tick(runtime.IsActive?Vector3.zero:brain.MoveDirection,Time.deltaTime);
            else
            {
                sideView.Move(Time.deltaTime);
                if (pendingRelease != null)
                {
                    if (sideView.Phase != SideViewEnemyPhase.Active)
                    {
                        var missed = pendingRelease;
                        pendingRelease = null;
                        hitShape.CompleteMiss(missed);
                    }
                    else if (sideView.DamageWindowOpen(now) && hitShape.ResolveOverlap(pendingRelease, sideView.HitDirection, 0, true))
                        pendingRelease = null;
                }
            }
        }
        public void Cancel(string reason)
        {
            var fact=runtime==null?null:runtime.Cancel(reason);
            if(fact!=null) { PresentationGeneration++; warning.Clear(fact.ActionId);
                ActionObserved?.Invoke(fact); }
            if(fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N");
            pendingRelease?.Resolve(Array.Empty<Health>());
            pendingRelease = null;
            target=null; targetHealth=null;
            movement?.Stop();
            brain?.ResetDecision(SimulationTime);
        }
        public void SetPaused(bool value) { paused=value; if(value) { vitals?.ResetHitStop(); Cancel("enemy-paused"); } }
        private void OnStatusControlChanged(){if(statusEffects!=null&&statusEffects.BlocksIntent)Cancel("combat-status");}
        private void OnPostureBroken(string cause)
        {
            GetComponent<ActionImpactMotor>()?.ClearMotion();
            authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
            Cancel("posture-broken:" + cause);
        }
        private void OnPostureRecovered() { authority.Release(posture); }
        private void OnDied(HitOutcome outcome) { GetComponent<ActionImpactMotor>()?.ResetState();statusEffects?.Clear();Cancel("death:"+outcome.ActorId); }
        private void OnDisable() { GetComponent<ActionImpactMotor>()?.ResetState();vitals?.ResetHitStop(); Cancel("enemy-disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); }
        private void OnApplicationFocus(bool value) { focused=value; if(!value) { vitals?.ResetHitStop(); Cancel("focus-lost"); } }
        private void OnApplicationPause(bool value) { applicationPaused=value; if(value) { vitals?.ResetHitStop(); Cancel("application-paused"); } }
        private void OnDestroy()
        {
            if(statusEffects!=null)statusEffects.ControlChanged-=OnStatusControlChanged;
            if(vitals!=null) vitals.Died-=OnDied;
            if(posture!=null) { posture.Broken-=OnPostureBroken; posture.Recovered-=OnPostureRecovered; }
            authority.Release(posture);
            Cancel("destroyed");
        }
        public void RestoreForDevelopment()
        { GetComponent<ActionImpactMotor>()?.ResetState();PresentationGeneration++; Cancel("development-reset"); vitals.RestoreForDevelopment(); if(posture!=null) posture.ResetState(); brain.ResetDecision(SimulationTime); }
        public bool RespawnOnRest()
        {
            if (sideView == null || sideView.Kind == SideViewEnemyKind.Mother || vitals == null || vitals.IsAlive) return false;
            Cancel("rest-respawn");
            sideView.ReturnToSpawn();
            return vitals.RestoreAfterDeath();
        }
    }
}
