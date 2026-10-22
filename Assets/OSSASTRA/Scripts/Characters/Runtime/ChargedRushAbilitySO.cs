/* 脚本：ChargedRushAbilitySO
 * 功能：准备后退→松开/超时→释放姿势→冲锋的专职执行策略。
 * 边界：不依赖左手盾类别，不移动Transform；阶段共享受理身份、费用和夺回预算。
 * 协作：Root管理阶段事实，Movement执行带墙体碰撞的位移，持续接触由共享投递器去重。
 */
using System;
using UnityEngine;
using Ossastra.Combat;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Executors/Charged Rush")]
    public sealed class ChargedRushAbilitySO:AbilityExecutorSO
    {
        public ActionDefinitionSO releasePose,chargePose;
        public override bool UsesBodyAction=>true;
        public override void Validate(AbilityDefinitionSO definition)
        {
            if(definition.action==null||releasePose==null||chargePose==null||definition.releases.Length!=1)
                throw new ArgumentException("rush requires prepare/release/charge and one contact wave");
            definition.action.Freeze();releasePose.Freeze();chargePose.Freeze();
            if(definition.action.Freeze().FinishAt<=2||releasePose.Freeze().FinishAt<=1.0/6||chargePose.Freeze().FinishAt<.6)
                throw new ArgumentException("rush phase ends before required transition");
        }
        public override CompiledAbilityExecutor CompileExecutor()=>new Executor(releasePose,chargePose);
        private sealed class Executor:CompiledAbilityExecutor
        {
            private readonly ActionDefinitionSO release,charge;
            private readonly ConfiguredActionSnapshot frozenRelease,frozenCharge;
            public Executor(ActionDefinitionSO release,ActionDefinitionSO charge,ConfiguredActionSnapshot a=null,ConfiguredActionSnapshot b=null)
            {this.release=release;this.charge=charge;frozenRelease=a;frozenCharge=b;}
            public override bool UsesBodyAction=>true;
            public override bool StartsCooldownOnAccept=>false;
            public override CompiledAbilityExecutor Prepare(CharacterRoot owner)=>new Executor(null,null,owner.FreezeAbilityPhase(release),owner.FreezeAbilityPhase(charge));
            public override AbilityRun CreateRun(AbilityRunContext context)=>new Run(context,frozenRelease,frozenCharge);
        }
        private sealed class Run:AbilityRun,ICharacterMotionOverride,ICombatEvasion
        {
            private readonly ConfiguredActionSnapshot release,charge;
            private int phase;
            private double phaseAt;
            private Vector3 velocity;
            public Run(AbilityRunContext context,ConfiguredActionSnapshot release,ConfiguredActionSnapshot charge):base(context)
            {this.release=release;this.charge=charge;phaseAt=context.acceptedAt;}
            public bool IsActive=>!Ended&&velocity.sqrMagnitude>0;
            public Vector3 MotionVelocity=>velocity;
            public bool IsEvading=>!Ended&&(phase==0&&Context.owner.SimulationTime-phaseAt<2||phase==2&&Context.owner.SimulationTime-phaseAt<.6);
            public override void Tick(float delta,bool held)
            {
                if(Ended)return;
                var t=Context.owner.SimulationTime-phaseAt;velocity=Vector3.zero;
                if(phase==0)
                {
                    velocity=Context.direction*DistanceStep(t,delta,.15f,-1.7f);
                    if(t>=.15&&(!held||t>=2))Transition(1,release);
                }
                else if(phase==1&&t>=1.0/6)
                {
                    if(!Context.BeginCooldown()){Cancel();Context.owner.CancelOwnedMainAction(Context.id,"rush-cooldown-conflict");return;}
                    Transition(2,charge);
                    if(!Ended)Context.emit(Context,new AbilityEmission(0,Context.definition.plan.Emissions[0].Damage,0));
                }
                else if(phase==2)
                {
                    velocity=Context.direction*DistanceStep(t,delta,.2f,6);
                    if(t>=.6){velocity=Vector3.zero;Ended=true;}
                }
            }
            private void Transition(int next,ConfiguredActionSnapshot snapshot)
            {if(!Context.owner.ChangeAbilityPhase(Context.id,snapshot)){Cancel();return;}phase=next;phaseAt=Context.owner.SimulationTime;velocity=Vector3.zero;}
            internal static float DistanceStep(double elapsed,float delta,float duration,float distance)
            {
                if(delta<=0)return 0;
                float Curve(double time){var p=Mathf.Clamp01((float)(time/duration));return 1-(1-p)*(1-p);}
                return distance*(Curve(elapsed)-Curve(elapsed-delta))/delta;
            }
        }
    }
}
