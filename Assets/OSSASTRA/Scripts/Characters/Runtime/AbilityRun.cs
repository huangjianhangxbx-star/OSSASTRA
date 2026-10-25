/* 脚本：AbilityRun
 * 功能：为专职执行器提供已接受来源、角色模拟钟和共享投递端口。
 * 边界：不持有另一套HP/体力/CD；可变状态属于每次Run，不属于共享执行器SO。
 * 协作：CharacterAbilities管理Run生命周期；身体阶段仍由Root原BasicActionRuntime推进。
 */
using System;
using UnityEngine;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
using Ossastra.CharacterCore;
using System.Collections.Generic;
namespace Ossastra.Characters
{
    public sealed class AbilityRunContext
    {
        public readonly CharacterRoot owner;
        public readonly CompiledAbility definition;
        public readonly IAbilitySource source;
        public readonly Guid id;
        public readonly Guid releasePaymentId;
        public readonly int lifeGeneration;
        public readonly double acceptedAt;
        public readonly Vector3 direction,landing,acceptedPosition;
        public readonly Action<AbilityRunContext,AbilityEmission> emit;
        public readonly AbilityOperation operation;
        public readonly CharacterActionSource requestSource;
        // 操作/表现所属手别与资源来源分离：副手可消耗主手弹药，仍是副手动作。
        public readonly bool? presentationMainHand;
        public readonly Sprite projectileSprite;
        public readonly float projectileScale;
        public readonly EffectSnapshot effects;
        public readonly ShotEnhancementSnapshot shot;
        internal Action<AbilityRunContext,AbilityEmission,DeliveryParameters,string,float> deliveryPort;
        internal Action<int,string,IReadOnlyList<HitOutcome>> onResolved;
        internal Action<Guid,string,Vector3> feedback;
        internal Func<AbilityRunContext,bool> commitSource;
        public bool TryCommitSource()=>commitSource!=null&&commitSource(this);
        public void NotifyFeedback(string kind)=>feedback?.Invoke(id,kind,owner.transform.position);
        /// <summary>同一动作可提交不同投递节点；统一来源支付、命中和父预算仍归共享端口。</summary>
        public void Deliver(AbilityEmission emission,DeliveryParameters delivery,string channel,float posture=0)
            =>deliveryPort(this,emission,delivery,channel,posture);
        public double ModifiedCooldown=>effects.Cooldown(definition.effectRoot,definition.cooldown);
        public bool SourceCommitted {get;private set;}
        internal void MarkSourceCommitted(){SourceCommitted=true;}
        public bool BeginCooldown()
        {
            var state=source.RuntimeState;
            return state.Cooldowns.Commit(source.InstanceId+":"+definition.cooldownIdentity,id,state.Clock(definition.cooldownPolicy),ModifiedCooldown);
        }
        public AbilityRunContext(CharacterRoot owner,CompiledAbility definition,IAbilitySource source,Guid id,Vector3 direction,Vector3 landing,Action<AbilityRunContext,AbilityEmission> emit,AbilityOperation operation,Sprite sprite,float scale,EffectSnapshot effects=null,Guid releasePaymentId=default,CharacterActionSource requestSource=CharacterActionSource.Player,bool? presentationMainHand=null)
        {
            this.owner=owner;this.definition=definition;this.source=source;this.id=id;this.direction=direction;this.landing=landing;this.emit=emit;
            lifeGeneration=owner.LifeGeneration;acceptedAt=owner.SimulationTime;acceptedPosition=owner.transform.position;
            this.operation=operation;this.requestSource=requestSource;
            this.presentationMainHand=presentationMainHand;
            projectileSprite=sprite;projectileScale=scale;
            this.effects=effects??new EffectSnapshot(0,Array.Empty<EffectSourceRow>(),0,false);
            this.releasePaymentId=releasePaymentId;shot=source.RuntimeState.ShotEnhancements.Snapshot();
        }
    }
    public abstract class AbilityRun
    {
        public AbilityRunContext Context {get;}
        public bool Ended {get;protected set;}
        public virtual bool RequiresHeld=>false;
        public abstract void Tick(float delta,bool held);
        public virtual void OnResolved(int index,string channel,IReadOnlyList<HitOutcome> receipts) { }
        public virtual void Cancel(){Context.source.CancelReservation();Ended=true;}
        protected AbilityRun(AbilityRunContext context){Context=context;}
    }
}
