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
        [Min(0)] public float startup,frostPerBlock;
        [Range(0,1)] public float movementScale=1;
        public override bool UsesBodyAction=>false;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(float.IsNaN(frontAngle)||frontAngle<0||frontAngle>180||float.IsNaN(postureScale)||postureScale<0||postureScale>1)
                throw new ArgumentException("guard angle/posture invalid");
            if(definition.releases.Length!=0)throw new ArgumentException("guard has no attack release nodes");
            if((float.IsNaN(startup)||float.IsInfinity(startup))||startup<0||(float.IsNaN(frostPerBlock)||float.IsInfinity(frostPerBlock))||frostPerBlock<0||(float.IsNaN(movementScale)||float.IsInfinity(movementScale))||movementScale<0||movementScale>1)throw new ArgumentException("guard startup/resource/movement invalid");
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor(frontAngle,postureScale,startup,frostPerBlock,movementScale);
        private sealed class Executor:CompiledAbilityExecutor
        {
            private readonly float angle,scale;
            private readonly float startup,frostCost,movement;
            private readonly Ossastra.CharacterCore.Abilities.EquipmentAbilityState mainState;
            public Executor(float angle,float scale,float startup,float frostCost,float movement,Ossastra.CharacterCore.Abilities.EquipmentAbilityState mainState=null){this.angle=angle;this.scale=scale;this.startup=startup;this.frostCost=frostCost;this.movement=movement;this.mainState=mainState;}
            public override bool UsesBodyAction=>false;
            public override CharacterActionKind ActionKind=>CharacterActionKind.Guard;
            public override CompiledAbilityExecutor Prepare(CharacterRoot owner)=>new Executor(angle,scale,startup,frostCost,movement,owner.GetComponent<CharacterEquipment>()?.State.RightRuntime);
            public override bool CanUseSource(IAbilitySource source)=>frostCost<=0||mainState!=null&&mainState.Frost.Current>=frostCost;
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context,angle,scale,startup,frostCost,movement);
        }
        private sealed class Run:AbilityRun,ICombatGuard,ICharacterLocomotionScale
        {
            public override bool RequiresHeld=>true;
            private readonly double minimumDot,scale;
            private readonly float startup,frostCost,movement;
            private readonly Ossastra.CharacterCore.Abilities.EquipmentAbilityState mainState;
            public Run(AbilityRunContext context,float angle,float scale,float startup,float frostCost,float movement):base(context)
            {minimumDot=Math.Cos(angle*.5*Math.PI/180);this.scale=scale;this.startup=startup;this.frostCost=frostCost;this.movement=movement;mainState=context.owner.GetComponent<CharacterEquipment>()?.State.RightRuntime;}
            private bool CanMaintain=>!Ended&&Context.owner.CanSimulateIntent&&Context.source.IsAvailable&&(frostCost<=0||mainState!=null&&mainState.Frost.Current>=frostCost);
            public bool IsGuarding=>CanMaintain&&Context.owner.SimulationTime>=Context.acceptedAt+startup;
            public float LocomotionScale=>CanMaintain?movement:1;
            public Vector3 GuardDirection=>Context.owner.DataBus.AttackDirection;
            public double MinimumDot=>minimumDot;
            public double PostureScale=>scale;
            public override void Tick(float delta,bool held)
            {
                if(!held||!CanMaintain){Cancel();return;}
                var stamina=Context.owner.GetComponent<CharacterStamina>();
                if(Context.definition.holdCost>0&&(stamina==null||!stamina.Drain(Context.definition.holdCost*delta,Context.owner.SimulationTime)))Cancel();
            }
            public void OnGuarded(HitOutcome outcome)
            {if(IsGuarding&&outcome.TargetId==Context.owner.ActorId){if(frostCost>0)mainState.Frost.Spend(frostCost,mainState.EquippedTime);Context.source.CommitRelease(outcome.ActionId);Context.NotifyFeedback("guard");}}
        }
    }
}
