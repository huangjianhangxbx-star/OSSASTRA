/* 脚本：PrecisionReloadAbilitySO
 * 功能：以同一装备实例弹仓执行精确装填；配置和分支在接受时冻结。
 * 边界：不保存共享可变状态，不新造弹仓；撤销只取消本次装填，不补弹、不回退已完成提交。
 * 协作：Root负责身体动作/付款/CD，Run只驱动实例弹仓的唯一装填事务。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Executors/Precision Reload")]
    public sealed class PrecisionReloadAbilitySO:AbilityExecutorSO
    {
        [Min(.01f)] public float reloadDuration=.8f;
        [Range(1,64)] public int rounds=4;
        public override bool UsesBodyAction=>true;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(float.IsNaN(reloadDuration)||float.IsInfinity(reloadDuration)||reloadDuration<=0||rounds<1||rounds>64||definition.action==null||
                definition.action.Freeze().FinishAt+0.00001<reloadDuration)
                throw new ArgumentException("precision reload requires a valid body duration and magazine size");
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor(reloadDuration,rounds);
        private sealed class Executor:CompiledAbilityExecutor
        {
            private readonly float duration;
            private readonly int rounds;
            public Executor(float duration,int rounds){this.duration=duration;this.rounds=rounds;}
            public override bool UsesBodyAction=>true;
            public override bool CanUseSource(IAbilitySource source)=>source?.RuntimeState!=null&&!source.RuntimeState.Magazine.IsReloading;
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context,duration,rounds);
        }
        private sealed class Run:AbilityRun
        {
            private readonly MagazineState magazine;
            public Run(AbilityRunContext context,float duration,int rounds):base(context)
            {
                magazine=context.source.RuntimeState.Magazine;
                var modifiers=context.effects.Modifiers(context.definition.effectRoot);
                var count=modifiers.Where(m=>m.AmmoCount>0).Select(m=>m.AmmoCount).DefaultIfEmpty(rounds).Max();
                var instant=modifiers.Any(m=>m.InstantReload);
                var state=context.source.RuntimeState;state.ShotEnhancements.ReloadStarted();
                if(!magazine.BeginReload(context.id,AmmoKind.Mortar,count,instant,context.acceptedAt,duration))
                    throw new InvalidOperationException("Accepted reload reservation changed before execution");
                if(instant){state.ShotEnhancements.ReloadCompleted();context.NotifyFeedback("reload");Ended=true;}
                state.ShotEnhancements.PrecisionAccepted(context.effects,state.EquippedTime);
            }
            public override void Tick(float delta,bool held)
            {
                if(Ended||Context.owner.IsSimulationSuspended)return;
                if(magazine.CompleteReload(Context.id,Context.owner.SimulationTime)){Context.source.RuntimeState.ShotEnhancements.ReloadCompleted();Context.NotifyFeedback("reload");Ended=true;}
            }
            public override void Cancel(){magazine.CancelReload(Context.id);Context.source.RuntimeState.ShotEnhancements.ReloadCancelled();base.Cancel();}
        }
    }
}
