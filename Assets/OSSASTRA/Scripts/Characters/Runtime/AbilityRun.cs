/* 脚本：AbilityRun
 * 功能：为专职执行器提供已接受来源、角色模拟钟和共享投递端口。
 * 边界：不持有另一套HP/体力/CD；可变状态属于每次Run，不属于共享执行器SO。
 * 协作：CharacterAbilities管理Run生命周期；身体阶段仍由Root原BasicActionRuntime推进。
 */
using System;
using UnityEngine;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Characters
{
    public sealed class AbilityRunContext
    {
        public readonly CharacterRoot owner;
        public readonly CompiledAbility definition;
        public readonly IAbilitySource source;
        public readonly Guid id;
        public readonly Guid releasePaymentId=Guid.NewGuid();
        public readonly int lifeGeneration;
        public readonly double acceptedAt;
        public readonly Vector3 direction,landing;
        public readonly Action<AbilityRunContext,AbilityEmission> emit;
        public readonly AbilityOperation operation;
        public readonly Sprite projectileSprite;
        public readonly float projectileScale;
        public bool SourceCommitted {get;private set;}
        internal void MarkSourceCommitted(){SourceCommitted=true;}
        public bool BeginCooldown()
        {
            var state=source.RuntimeState;
            return state.Cooldowns.Commit(source.InstanceId+":"+definition.cooldownIdentity,id,state.Clock(definition.cooldownPolicy),definition.cooldown);
        }
        public AbilityRunContext(CharacterRoot owner,CompiledAbility definition,IAbilitySource source,Guid id,Vector3 direction,Vector3 landing,Action<AbilityRunContext,AbilityEmission> emit,AbilityOperation operation,Sprite sprite,float scale)
        {
            this.owner=owner;this.definition=definition;this.source=source;this.id=id;this.direction=direction;this.landing=landing;this.emit=emit;
            lifeGeneration=owner.LifeGeneration;acceptedAt=owner.SimulationTime;
            this.operation=operation;
            projectileSprite=sprite;projectileScale=scale;
        }
    }
    public abstract class AbilityRun
    {
        public AbilityRunContext Context {get;}
        public bool Ended {get;protected set;}
        public virtual bool RequiresHeld=>false;
        public abstract void Tick(float delta,bool held);
        public virtual void Cancel(){Context.source.CancelReservation();Ended=true;}
        protected AbilityRun(AbilityRunContext context){Context=context;}
    }
}
