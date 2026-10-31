/* 本轮纯状态断言：共享Health恢复、微事务计划与重复奖励；不代签实际空间/输入。 */
using System;using System.Linq;using Ossastra.CharacterCore;using Ossastra.WorldProps;
static class Test {
 static int passed;static void Check(bool b,string name){if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
 static void Reject(Action f,string name){bool rejected=false;try{f();}catch(ArgumentException){rejected=true;}Check(rejected,name);}
 static void Hit(WorldPropState p,int damage){var r=AttackRelease.ForEmission(Guid.NewGuid(),0,"temporary-a",damage,0);p.OnHit(r.Resolve(new[]{p.Health}).Single());}
 static void Main(){
 var h=Health.FromSnapshot("partial",60,19);Check(h.Current==19&&h.Maximum==60,"controlled partial Health restore");
 Reject(()=>Health.FromSnapshot("bad",60,-1),"negative HP rejected");Reject(()=>Health.FromSnapshot("bad",60,61),"overfull HP rejected");
 var d=new WorldPropDefinition("stone",1,60,new[]{new WorldPropDropRule("stone-item",2,2,1)});
 var a=new WorldPropState("map-a","stone-a",1,d);var b=new WorldPropState("map-a","stone-b",1,d);
 Hit(a,20);Check(a.Health.Current==40&&b.Health.Current==60,"same definition instance Health isolated");
 var partial=a.Capture();var restored=WorldPropState.Restore(partial,d);Check(restored.Health.Current==40&&restored.Phase==WorldPropPhase.Alive,"partial restore keeps true HP");
 Hit(a,50);Check(a.Phase==WorldPropPhase.BreakPending&&a.Health.Current==0&&a.Plan.Single().quantity==2,"terminal freezes pending drop plan");
 int grants=0;var plan=a.Capture();a.TryDeliver((id,drops,commit)=>false);Check(a.Phase==WorldPropPhase.BreakPending&&a.Capture().breakId==plan.breakId,"reward refusal preserves same pending");
 Check(a.TryDeliver((id,drops,commit)=>{grants+=drops.Sum(x=>x.quantity);commit();return true;}),"atomic receipt accepted");
 Check(!a.TryDeliver((id,drops,commit)=>{grants+=99;return true;})&&grants==2,"depleted callback never reissues reward");
 var dead=WorldPropState.Restore(a.Capture(),d);Check(dead.Phase==WorldPropPhase.Depleted&&!dead.TryDeliver((id,drops,commit)=>{grants+=99;return true;})&&grants==2,"picked/reloaded depletion never regrants");
 var pending=WorldPropState.Restore(plan,d);Check(pending.Plan.Single().quantity==2&&pending.BreakId==a.BreakId,"pending reload retains same plan and identity");
 var corrupt=a.Capture();corrupt.durability=20;Reject(()=>WorldPropState.Restore(corrupt,d),"depleted/live HP inconsistency rejected");
 var unknown=a.Capture();unknown.definitionId="other";Reject(()=>WorldPropState.Restore(unknown,d),"unknown definition cannot reseed");
 var deterministic=new WorldPropDefinition("random",1,10,new[]{new WorldPropDropRule("seed",1,8,1),new WorldPropDropRule("rare",1,1,.5)});
 var r1=new WorldPropState("map-a","random-a",1,deterministic);var r2=new WorldPropState("map-a","random-a",1,deterministic);Hit(r1,10);Hit(r2,10);
 Check(string.Join(";",r1.Plan.Select(x=>x.definitionId+":"+x.quantity))==string.Join(";",r2.Plan.Select(x=>x.definitionId+":"+x.quantity)),"stable life deterministic plan");
 var store=new WorldPropStore();store.Register(d);store.Attach("map-a","one",1,"stone");store.Attach("map-b","two",1,"stone");Hit(store.Get("map-a","one",1),20);var frozen=store.Capture();
 var candidate=WorldPropStore.Restore(frozen,new[]{d});Check(candidate.Get("map-a","one",1).Health.Current==40&&candidate.Get("map-b","two",1).Health.Current==60,"unloaded map record remains in same aggregate");
 Reject(()=>WorldPropStore.Restore(new[]{frozen[0],frozen[0]},new[]{d}),"duplicate stable IDs refused");
 Console.WriteLine("TOTAL "+passed);
 }
}
