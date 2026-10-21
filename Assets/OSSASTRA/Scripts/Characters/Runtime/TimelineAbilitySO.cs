/* 脚本：TimelineAbilitySO
 * 功能：把不可变有限释放计划交给共享投递器，供近战、射线、飞行弹和投掷内容复用。
 * 边界：不按装备类别或技能名字判断；身体动作/付款仍由根，Run只拥有释放游标。
 * 协作：收到原Runtime终结即撤未释放节点；已经离手的投递由场景效果队列继续管理。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Executors/Timeline")]
    public sealed class TimelineAbilitySO:AbilityExecutorSO
    {
        [SerializeField] private bool usesBodyAction=true;
        public override bool UsesBodyAction=>usesBodyAction;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(usesBodyAction&&definition.action==null)throw new ArgumentException("action animation required for body ability");
            if(definition.releases==null||definition.releases.Length==0)throw new ArgumentException("timeline requires releases");
            if(usesBodyAction)
            {
                var action=definition.action.Freeze();
                if(Math.Abs(action.FinishAt-definition.finishAt)>.00001||Math.Abs(action.MoveReadyAt-definition.moveReadyAt)>.00001||
                    Math.Abs(action.AttackReadyAt-definition.attackReadyAt)>.00001||Math.Abs(action.ReleaseAt-definition.releases.Min(n=>n.at))>.00001)
                    throw new ArgumentException("action stage times differ from ability plan");
            }
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor(usesBodyAction);
        private sealed class Executor:CompiledAbilityExecutor
        {
            private readonly bool usesBody;
            public Executor(bool usesBody){this.usesBody=usesBody;}
            public override bool UsesBodyAction=>usesBody;
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context);
        }
        private sealed class Run:AbilityRun
        {
            private readonly AbilityExecution execution;
            public Run(AbilityRunContext context):base(context)
            {execution=new AbilityExecution(context.id,context.owner.ActorId,context.source.InstanceId,context.lifeGeneration,0,context.definition.plan,context.acceptedAt);}
            public override void Tick(float delta,bool held)
            {
                if(Ended)return;
                foreach(var emission in execution.Advance(Context.owner.SimulationTime,Context.owner.IsSimulationSuspended))Context.emit(Context,emission);
                if(execution.Finished(Context.owner.SimulationTime))Ended=true;
            }
            public override void Cancel(){execution.Cancel();base.Cancel();}
        }
    }
}
