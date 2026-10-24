/* 脚本：MagazineShotAbilitySO
 * 功能：按同一来源实例的首弹冻结动作/投递，释放时只扣一发，支持普通直射和特殊落点炮弹。
 * 边界：不拥有第二份弹药，不按装备名字分支；身体动作由根，伤害由共享投递/命中网关。
 * 协作：精确装填写同一Magazine；射击接受前验证弹仓，换弹不追改已经接受的动作。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using Ossastra.CharacterCore;
using UnityEngine;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Executors/Magazine Shot")]
    public sealed class MagazineShotAbilitySO:AbilityExecutorSO
    {
        public ActionDefinitionSO normalAction,mortarAction;
        public DeliveryParameters normalDelivery=new DeliveryParameters{mode=DeliveryMode.Projectile,range=5.6f,speed=30,collisionRadius=.18f};
        public DeliveryParameters mortarDelivery=new DeliveryParameters{mode=DeliveryMode.RayLandingArc,range=7,radius=2,flightTime=.45f};
        [Min(1)] public int normalDamage=32,mortarDamage=80;
        public override bool UsesBodyAction=>true;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(normalAction==null||mortarAction==null||normalDelivery==null||mortarDelivery==null||normalDamage<1||mortarDamage<1)
                throw new ArgumentException("shot action/delivery/damage required");
            normalAction.Freeze();mortarAction.Freeze();normalDelivery.Freeze();mortarDelivery.Freeze();
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor(normalAction,mortarAction,normalDelivery.Freeze(),mortarDelivery.Freeze(),normalDamage,mortarDamage);
        private sealed class Executor:CompiledAbilityExecutor
        {
            private readonly ActionDefinitionSO normal,mortar;
            private readonly DeliveryParameters normalDelivery,mortarDelivery;
            private readonly int normalDamage,mortarDamage;
            private readonly ConfiguredActionSnapshot phase;
            private readonly AmmoKind kind;
            public Executor(ActionDefinitionSO normal,ActionDefinitionSO mortar,DeliveryParameters normalDelivery,DeliveryParameters mortarDelivery,int normalDamage,int mortarDamage,ConfiguredActionSnapshot phase=null,AmmoKind kind=AmmoKind.Normal)
            {this.normal=normal;this.mortar=mortar;this.normalDelivery=normalDelivery;this.mortarDelivery=mortarDelivery;this.normalDamage=normalDamage;this.mortarDamage=mortarDamage;this.phase=phase;this.kind=kind;}
            public override bool UsesBodyAction=>true;
            public override bool RepeatWhileHeld=>true;
            public override bool UsesMagazine=>true;
            public override void TickSource(CharacterRoot owner,IAbilitySource source)
            {
                var state=source.RuntimeState;bool before=state.Magazine.IsReloading;
                state.Magazine.TickAutomatic(state.EquippedTime,owner.CanSimulateIntent&&!owner.ActionActive);
                if(!before&&state.Magazine.IsReloading)state.ShotEnhancements.ReloadStarted();
                else if(before&&!state.Magazine.IsReloading){if(state.Magazine.Count==4){state.ShotEnhancements.ReloadCompleted();owner.GetComponent<CharacterAbilities>()?.NotifyResourceFeedback("reload");}else state.ShotEnhancements.ReloadCancelled();}
            }
            public override void StopSource(IAbilitySource source){var state=source.RuntimeState;bool loading=state.Magazine.IsReloading;state.Magazine.CancelAutomaticReload();if(loading&&!state.Magazine.IsReloading)state.ShotEnhancements.ReloadCancelled();}
            public override ConfiguredActionSnapshot InitialPhase=>phase;
            public override DeliveryParameters AcceptanceDelivery=>kind==AmmoKind.Mortar?mortarDelivery:normalDelivery;
            public override float AimRange=>kind==AmmoKind.Mortar?mortarDelivery.range:0;
            public override bool CanUseSource(IAbilitySource source)=>source?.RuntimeState!=null&&!source.RuntimeState.Magazine.IsReloading&&source.RuntimeState.Magazine.Count>0;
            public override CompiledAbilityExecutor Prepare(CharacterRoot owner,IAbilitySource source)
                =>Prepare(owner,source,new CharacterAnimationPreparation(true));
            public override CompiledAbilityExecutor Prepare(CharacterRoot owner,IAbilitySource source,CharacterAnimationPreparation preparation)
            {
                var kind=source.RuntimeState.Magazine.Next??AmmoKind.Normal;
                var frozen=owner.FreezeAbilityPhase(kind==AmmoKind.Mortar?mortar:normal,preparation);
                var rate=source.RuntimeState.ShotEnhancements.Snapshot().Rate;var t=frozen.Timeline;
                frozen=frozen.WithTimeline(new BasicAttackDefinition(t.ReleaseAt/rate,t.MoveReadyAt/rate,t.AttackReadyAt/rate,t.FinishAt/rate,t.Damage,t.PostureImpact));
                return new Executor(normal,mortar,normalDelivery,mortarDelivery,normalDamage,mortarDamage,frozen,kind);
            }
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context,phase,kind==AmmoKind.Mortar?mortarDelivery:normalDelivery,kind==AmmoKind.Mortar?mortarDamage:normalDamage);
        }
        private sealed class Run:AbilityRun
        {
            private readonly ConfiguredActionSnapshot phase;
            private readonly DeliveryParameters delivery;
            private readonly int damage;
            private readonly ShotEnhancementSnapshot shot;
            private bool released;
            public Run(AbilityRunContext context,ConfiguredActionSnapshot phase,DeliveryParameters delivery,int damage):base(context)
            {this.phase=phase;this.delivery=delivery;shot=context.shot;this.damage=damage;}
            public override void OnResolved(int index,string channel,IReadOnlyList<HitOutcome> receipts)
            {if(receipts.Any(r=>r.Reason==HitReason.Applied))Context.source.RuntimeState.ShotEnhancements.Applied(shot);}
            public override void Tick(float delta,bool held)
            {
                if(Ended||Context.owner.IsSimulationSuspended)return;
                var now=Context.owner.SimulationTime;
                if(!released&&now>=Context.acceptedAt+phase.Timeline.ReleaseAt)
                {
                    released=true;
                    if(Context.source.RuntimeState.Magazine.Consume(Context.id,Context.source.RuntimeState.EquippedTime))Context.Deliver(new AbilityEmission(phase.Timeline.ReleaseAt,damage,0),delivery,"ranged-shot");
                }
                if(now>=Context.acceptedAt+phase.Timeline.FinishAt)Ended=true;
            }
        }
    }
}
