/* 脚本：RocketLeapAbilitySO
 * 功能：释放后沿合法目标方向弹射，运动后在实际落点投递伤害。
 * 边界：不以延时圆伤害冒充运动；不写Transform，不产生第二份体力或冷却。
 * 协作：Root推进唯一模拟钟；Movement处理墙体，释放时与落地时分别产生冻结节点。
 */
using System;
using UnityEngine;
using Ossastra.Combat;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Executors/Rocket Leap")]
    public sealed class RocketLeapAbilitySO:AbilityExecutorSO
    {
        public override bool UsesBodyAction=>true;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(definition.action==null||definition.releases.Length!=1||definition.action.Freeze().FinishAt<.9f)
                throw new ArgumentException("rocket requires full body action and landing release");
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor();
        private sealed class Executor:CompiledAbilityExecutor
        {
            public override bool UsesBodyAction=>true;
            public override bool StartsCooldownOnAccept=>false;
            public override float AimRange=>6;
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context);
        }
        private sealed class Run:AbilityRun,ICharacterMotionOverride,ICombatEvasion
        {
            private readonly Vector3 displacement;
            private bool launched,landed;
            private Vector3 velocity;
            public Run(AbilityRunContext context):base(context)
            {var d=context.landing-context.owner.transform.position;d.y=0;displacement=Vector3.ClampMagnitude(d,6);}
            public bool IsActive=>!Ended&&velocity.sqrMagnitude>0;
            public Vector3 MotionVelocity=>velocity;
            public bool IsEvading=>!Ended&&launched&&Context.owner.SimulationTime<Context.acceptedAt+.4666667;
            public override void Tick(float delta,bool held)
            {
                if(Ended)return;var t=Context.owner.SimulationTime-Context.acceptedAt;velocity=Vector3.zero;
                if(!launched&&t>=.1)
                {
                    if(!Context.BeginCooldown()){Cancel();Context.owner.CancelOwnedMainAction(Context.id,"rocket-cooldown-conflict");return;}
                    launched=true;
                }
                if(launched&&delta>0)
                {
                    var p=Mathf.Clamp01((float)((t-.1)/.33));var previous=Mathf.Clamp01((float)((t-delta-.1)/.33));
                    velocity=displacement*((p-previous)/delta);
                }
                // 最后一段运动由Movement在本帧执行，下一帧取真实落点；墙体截断也不会隔墙炸预期落点。
                if(!landed&&t-delta>=.43)
                {landed=true;Context.emit(Context,new AbilityEmission(.43,Context.definition.plan.Emissions[0].Damage,0));}
                if(t>=.9){velocity=Vector3.zero;Ended=true;}
            }
        }
    }
}
