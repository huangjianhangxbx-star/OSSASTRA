using System;using System.Linq;using Ossastra.CharacterCore;using Ossastra.CharacterCore.Abilities;
static class AbilityContractsTests
{
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static void Main()
 {
  var tapHold=new GestureRecognizer(GestureMode.TapOrLongPress,.3);
  Check(tapHold.Sample(false,0,1)==GestureSignal.None,"neutral arms");
  Check(tapHold.Sample(true,.1,1)==GestureSignal.None,"competing gesture waits");
  Check(tapHold.Sample(false,.2,1)==GestureSignal.Tap,"tap resolves on release");
  tapHold.Sample(true,.3,1);Check(tapHold.Sample(true,.61,1)==GestureSignal.LongPress,"long wins once");
  Check(tapHold.Sample(true,.7,1)==GestureSignal.None,"held cannot repay");
  Check(tapHold.Sample(false,.8,1)==GestureSignal.End,"long release not second tap");
  var hold=new GestureRecognizer(GestureMode.Hold,.3);hold.Sample(false,0,1);
  Check(hold.Sample(true,.1,1)==GestureSignal.Begin,"guard begins immediately");
  Check(hold.Sample(true,.2,2)==GestureSignal.Cancel,"epoch invalidates accepted gesture");
  Check(hold.Sample(true,.3,2)==GestureSignal.None,"old held key blocked after switch");
  hold.Sample(false,.4,2);Check(hold.Sample(true,.5,2)==GestureSignal.Begin,"real release rearms");
  var plan=new AbilityPlan("test",.8,.6,.7,new[]{new AbilityEmission(.1,10,0),new AbilityEmission(.2,12,1)},AbilityOccupancy.Body);
  var frozen=plan.Emissions;frozen[0]=new AbilityEmission(.1,999,0);Check(plan.Emissions[0].Damage==10,"caller cannot mutate frozen plan");
  var execution=new AbilityExecution(Guid.NewGuid(),"actor","item",3,1,plan,5);
  Check(execution.Advance(5.1,false).Length==1,"first actual node");
  Check(execution.Advance(5.1,false).Length==0,"same time no duplicate emission");
  Check(execution.Advance(5.3,true).Length==0,"pause emits nothing");
  Check(execution.Advance(5.3,false).Single().Index==1,"second pulse distinct index");
  execution.Cancel();Check(execution.Advance(6,false).Length==0,"cancel prevents late release");
  bool invalid=false;try{new AbilityPlan("bad",.2,0,0,new[]{new AbilityEmission(.3,1,0)},AbilityOccupancy.Body);}catch(ArgumentException){invalid=true;}Check(invalid,"out of range release rejected");
  invalid=false;try{new AbilityPlan("bad",1,0,0,new[]{new AbilityEmission(double.NaN,1,0)},AbilityOccupancy.Body);}catch(ArgumentException){invalid=true;}Check(invalid,"nonfinite config rejected");
  invalid=false;try{new AbilityPlan("bad",1,0,0,new[]{new AbilityEmission(.1,1,0),new AbilityEmission(.2,1,0)},AbilityOccupancy.Body);}catch(ArgumentException){invalid=true;}Check(invalid,"duplicate emission identity rejected");
  var paid=new ActionAdmissionLedger();var req=Guid.NewGuid();int payments=0;
  Check(paid.TryCommit(req,null,()=>{payments++;return true;}),"existing sole admission ledger commits");
  Check(!paid.TryCommit(req,null,()=>{payments++;return true;})&&payments==1,"existing ledger rejects duplicate payment");
    var gear=new EquipmentState("actor",100);
  gear.RegisterDefinition("shield-a",OffhandKind.Shield,100);gear.RegisterDefinition("shield-b",OffhandKind.Shield,75);
  Check(gear.TryEquipDefinition("shield-a",true),"definition A equips");var first=gear.LeftInstanceId;
  Check(gear.Wear(Guid.NewGuid())&&gear.Durability==99,"first shield wears once");
  Check(gear.TryEquipDefinition("shield-b",true)&&gear.LeftInstanceId!=first&&gear.Durability==75,"same category different stable instance");
  gear.TryEquipDefinition("shield-a",true);Check(gear.LeftInstanceId==first&&gear.Durability==99,"switch back preserves identity and durability");
  Check(!gear.TryEquipDefinition("unknown",true)&&gear.LeftInstanceId==first,"unknown source cannot create free item");
    var runtime=new BasicActionRuntime("actor");var started=runtime.Request(new BasicAttackDefinition(.2,.3,.4,2,10),"phase-test",new ControlAuthority());
  var phase=runtime.Rephase(started.Fact.ActionId,new BasicAttackDefinition(.1,.2,.3,.4,20));
  Check(phase!=null&&phase.Kind==ActionFactKind.PhaseChanged&&runtime.ActionId==started.Fact.ActionId,"phase change retains accepted identity");
  Check(runtime.Rephase(Guid.NewGuid(),new BasicAttackDefinition(.1,.2,.3,.4,20))==null,"foreign phase cannot hijack body");
  Check(runtime.Advance(.1,false).Single(f=>f.Release!=null).ActionId==started.Fact.ActionId,"phase releases under original action");
  var wave=AttackRelease.ForContactWindow(Guid.NewGuid(),0,"actor",20,0);var firstTarget=new Health("first-target",100);var lateTarget=new Health("late-target",100);
  wave.Resolve(new[]{firstTarget});wave.Resolve(new[]{firstTarget,lateTarget});
  Check(firstTarget.Current==80&&lateTarget.Current==80,"moving contact window hits late target but never repeats first target");
  var sourceSet=new EffectSourceSet();var roots=new[]{"rush"};
  var basicRune=new EffectSourceDefinition("basic","blue","frost-haste",phase:0,exclusivePoint:"haste",hastePerFrost:.08);
  var upgradedRune=new EffectSourceDefinition("upgraded","blue","frost-haste",phase:1,exclusivePoint:"haste",hastePerFrost:.12);
  sourceSet.Grant("a",basicRune);sourceSet.Grant("b",upgradedRune);
  var evaluation=sourceSet.Evaluate("blue",roots);
  Check(evaluation.Single(r=>r.InstanceId=="b").Status==EffectSourceStatus.Active&&evaluation.Single(r=>r.InstanceId=="a").Status==EffectSourceStatus.Superseded,"upgraded exclusive source wins by phase");
  sourceSet.Grant("c",new EffectSourceDefinition("branch","blue","rush-frost",requiredRoot:"rush"));
  Check(sourceSet.Evaluate("blue",Array.Empty<string>()).Single(r=>r.InstanceId=="c").Status==EffectSourceStatus.Dormant,"missing root leaves branch dormant");
  Check(sourceSet.Evaluate("yellow",roots).All(r=>r.Status==EffectSourceStatus.ImportedUnresolved),"foreign profession sources do not execute");
  var frozenSources=sourceSet.Snapshot("blue",roots,3,false);sourceSet.Remove("b");
  Check(Math.Abs(frozenSources.HastePerFrost-.12)<.00001&&frozenSources.ContainsSource("b")&&!sourceSet.Contains("b"),"accepted source snapshot survives registry removal without changing live ownership");
  Console.WriteLine(count+" ability contracts passed");
 }
}
