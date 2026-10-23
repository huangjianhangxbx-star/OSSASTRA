/* 脚本：RegenerationAbilitySO
 * 功能：可配置持续恢复执行器，使用同一能力接受/释放事务，恢复交给目标本体组件。
 * 边界：不读物品名、不扣背包、不直接写HP；目标必须真实、存活且无互斥恢复。
 */
using System;
using UnityEngine;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Executors/Regeneration")]
    public sealed class RegenerationAbilitySO:AbilityExecutorSO
    {
        [Min(.01f)] public float perSecond=4,seconds=5;
        public override bool UsesBodyAction=>false;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(float.IsNaN(perSecond)||float.IsInfinity(perSecond)||float.IsNaN(seconds)||float.IsInfinity(seconds)||perSecond<=0||seconds<=0||definition.releases==null||definition.releases.Length!=1)throw new ArgumentException(name+" regeneration parameters/release invalid");
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor(perSecond,seconds);
        private sealed class Executor:CompiledAbilityExecutor
        {
            private readonly float rate,duration;
            private readonly CombatRegeneration target;
            public Executor(float rate,float duration,CombatRegeneration target=null){this.rate=rate;this.duration=duration;this.target=target;}
            public override bool UsesBodyAction=>false;
            public override CharacterActionKind ActionKind=>CharacterActionKind.Support;
            public override bool AllowsCompanionUse=>true;
            public override CompiledAbilityExecutor Prepare(CharacterRoot owner,IAbilitySource source)
            {
                var body=(source as IAbilityTargetSource)?.Target??owner.Vitals;var consumer=body?.GetComponent<CombatRegeneration>();
                if(consumer==null)throw new ArgumentException("Regeneration target missing CombatRegeneration: "+body?.name);
                return new Executor(rate,duration,consumer);
            }
            public override bool CanUseSource(IAbilitySource source)=>target!=null&&target.CanBegin(rate,duration);
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context,target,rate,duration);
        }
        private sealed class Run:AbilityRun
        {
            private readonly CombatRegeneration target;private readonly float rate,duration;private bool released;
            public Run(AbilityRunContext context,CombatRegeneration target,float rate,float duration):base(context){this.target=target;this.rate=rate;this.duration=duration;}
            public override void Tick(float delta,bool held)
            {
                if(Ended||Context.owner.IsSimulationSuspended)return;
                var elapsed=Context.owner.SimulationTime-Context.acceptedAt;
                if(!released&&elapsed>=Context.definition.plan.Emissions[0].At)
                {
                    released=true;
                    if(target==null||!target.CanBegin(rate,duration)||!Context.TryCommitSource()){Cancel();return;}
                    if(!target.TryBegin(Context.id,rate,duration))throw new InvalidOperationException("Accepted regeneration target changed during release");
                    Context.NotifyFeedback("release");
                }
                if(elapsed>=Context.definition.plan.FinishAt)Ended=true;
            }
        }
    }
}
