// 架势纯模型合同：精确delta、同次归零、不可变释放、非法参数；不模拟Unity世界。
using System;
using System.Linq;
using Ossastra.CombatCore;
using Ossastra.CharacterCore;
class Program {
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
 static void Near(double a,double b,string name)=>Check(Math.Abs(a-b)<1e-8,name);
 static void Main(){
 var p=new PostureState(100,.6,.5,1.5,.25);
 Near(p.Current,100,"initial full");
 p.ApplyImpact(60);Near(p.Current,40,"first hit");Check(!p.IsStaggered,"first no stagger");
 p.ApplyImpact(60);Check(p.IsStaggered,"zero immediately staggers");Near(p.StaggerRemaining,.6,"duration");
 p.Tick(.2,false);p.ApplyImpact(90);Near(p.StaggerRemaining,.4,"no refresh");
 p.Tick(.4,false);Near(p.Current,50,"recover fifty");Check(!p.IsStaggered,"end once");
 p.Tick(1.5,false);Near(p.Current,50,"exact delay no regen");p.Tick(.4,false);Near(p.Current,60,"25 percent per second");
 p.Tick(1,true);Near(p.Current,60,"action pauses regeneration");
 p.Tick(double.NaN,false);p.Tick(-1,false);p.ApplyImpact(double.PositiveInfinity);Near(p.Current,60,"invalid no mutation");
 p.Tick(10,false);Near(p.Current,100,"clamped full");p.ApplyImpact(0);Near(p.Current,100,"zero inert");
 p.ApplyImpact(100);p.Tick(2.5,false);Near(p.Current,60,"overshoot phases partitioned");
 p.Reset();Near(p.Current,100,"reset same model");
 bool rejected=false;try{new PostureState(0,.6,.5,1.5,.25);}catch(ArgumentException){rejected=true;}Check(rejected,"bad configuration rejected");
 var q=new PostureState(100,.6,.5,1.5,.25);q.ApplyImpact(55);Near(q.Current,45,"player impact");Near(p.Current,100,"instances independent");
 var authority=new ControlAuthority(); var runtime=new BasicActionRuntime("source");var def=new BasicAttackDefinition(.3,.5,.8,1.1,12,55);
 var hp=new Health("target",72);runtime.Request(def,"test",authority);Near(hp.Current,72,"accepted no HP");
 var release=runtime.Advance(.3,false).Single(f=>f.Release!=null).Release;Near(release.PostureImpact,55,"immutable release payload");
 release.Resolve(new[]{hp});Near(hp.Current,60,"single original HP write");
 bool duplicate=false;try{release.Resolve(new[]{hp});}catch(InvalidOperationException){duplicate=true;}Check(duplicate,"release duplicate rejected");
 Console.WriteLine("TOTAL "+passed);
 }
}
