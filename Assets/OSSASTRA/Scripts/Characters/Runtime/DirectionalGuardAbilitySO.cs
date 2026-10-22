/* 脚本：DirectionalGuardAbilitySO
 * 功能：可组合方向防御执行器；任何绑定该能力的装备可使用，不要求Shield类别。
 * 边界：按下即开始维持，不等待长按阈值；只交原资源与防御回执，不写HP。
 * 协作：绑定选择Hold手势；每次Run冻结角度/架势参数，真实格挡后才磨损来源实例。
 */
using System;
using UnityEngine;
using Ossastra.Combat;
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Executors/Directional Guard")]
    public sealed class DirectionalGuardAbilitySO:AbilityExecutorSO
    {
        [Range(0,180)] public float frontAngle=120;
        [Range(0,1)] public float postureScale=.5f;
        public override bool UsesBodyAction=>false;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(float.IsNaN(frontAngle)||frontAngle<0||frontAngle>180||float.IsNaN(postureScale)||postureScale<0||postureScale>1)
                throw new ArgumentException("guard angle/posture invalid");
            if(definition.releases.Length!=0)throw new ArgumentException("guard has no attack release nodes");
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor(frontAngle,postureScale);
        private sealed class Executor:CompiledAbilityExecutor
        {
            private readonly float angle,scale;
            public Executor(float angle,float scale){this.angle=angle;this.scale=scale;}
            public override bool UsesBodyAction=>false;
            public override CharacterActionKind ActionKind=>CharacterActionKind.Guard;
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context,angle,scale);
        }
        private sealed class Run:AbilityRun,ICombatGuard
        {
            public override bool RequiresHeld=>true;
            private readonly double minimumDot,scale;
            public Run(AbilityRunContext context,float angle,float scale):base(context){minimumDot=Math.Cos(angle*.5*Math.PI/180);this.scale=scale;}
            public bool IsGuarding=>!Ended&&Context.owner.CanSimulateIntent&&Context.source.IsAvailable;
            public Vector3 GuardDirection=>Context.owner.DataBus.AttackDirection;
            public double MinimumDot=>minimumDot;
            public double PostureScale=>scale;
            public override void Tick(float delta,bool held)
            {
                if(!held||!IsGuarding){Cancel();return;}
                var stamina=Context.owner.GetComponent<CharacterStamina>();
                if(Context.definition.holdCost>0&&(stamina==null||!stamina.Drain(Context.definition.holdCost*delta,Context.owner.SimulationTime)))Cancel();
            }
            public void OnGuarded(HitOutcome outcome)
            {if(IsGuarding&&outcome.TargetId==Context.owner.ActorId)Context.source.CommitRelease(outcome.ActionId);}
        }
    }
}
