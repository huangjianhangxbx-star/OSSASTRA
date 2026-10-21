using System; using System.Linq; using System.Collections; using System.Collections.Generic; using Ossastra.CharacterCore;
class Tests {
 static int passed; static void Check(bool v,string s){if(!v)throw new Exception(s);passed++;}
 static AttackRelease Release(){var r=new BasicActionRuntime("attacker");r.Request(new BasicAttackDefinition(.1,.2,.3,.4,10,20),"test",new ControlAuthority());return r.Advance(.1,false).Single(f=>f.Release!=null).Release;}
 static void Main(){
 var h=new Health("target",100);var release=Release();var asm=typeof(AttackRelease).Assembly;
 var snapshot=asm.GetType("Ossastra.CharacterCore.DefenseSnapshot");
 // 新接缝尚未存在时走原真实Resolve，记录“无法表达合法零伤格挡”的产品缺口。
 if(snapshot==null){release.Resolve(new[]{h});Check(h.Current==100,"Guarded contact must preserve HP; actual="+h.Current);return;}
 var contactType=asm.GetType("Ossastra.CharacterCore.AttackContact");
 object Defense(bool evade,bool guard,double dot=.9)=>Activator.CreateInstance(snapshot,new object[]{evade,guard,dot,.5,.5});
 IReadOnlyList<HitOutcome> Resolve(AttackRelease r, Health health,object defense,bool duplicate=false){var c=Activator.CreateInstance(contactType,new object[]{health,defense});var list=Array.CreateInstance(contactType,duplicate?2:1);list.SetValue(c,0);if(duplicate)list.SetValue(c,1);return (IReadOnlyList<HitOutcome>)typeof(AttackRelease).GetMethod("ResolveContacts").Invoke(r,new object[]{list});}
 var result=Resolve(release,h,Defense(false,true),true);Check(h.Current==100&&result[0].Reason.ToString()=="Guarded","guard 0HP");Check(result[1].Reason==HitReason.Duplicate,"guard duplicate");Check((double)typeof(HitOutcome).GetProperty("PostureScale").GetValue(result[0])==.5,"guard posture scale");
 var evade=Resolve(Release(),h,Defense(true,true))[0];Check(h.Current==100&&evade.Reason.ToString()=="Evaded","evade precedence");
 Check((double)typeof(HitOutcome).GetProperty("PostureScale").GetValue(evade)==0,"evade no posture");
 Check(Resolve(Release(),h,Defense(false,true,-1))[0].Reason==HitReason.Applied&&h.Current==90,"rear hit");
 Release().Resolve(new[]{h});Check(h.Current==80,"legacy normal");
 bool threw=false;try{Resolve(release,h,Defense(false,true));}catch(System.Reflection.TargetInvocationException x){threw=x.InnerException is InvalidOperationException;}Check(threw,"one batch across APIs");
 var self=new Health("attacker",100);Check(Resolve(Release(),self,Defense(false,true))[0].Reason==HitReason.Self,"self before defense");
 var dead=new Health("dead",1);Release().Resolve(new[]{dead});Check(Resolve(Release(),dead,Defense(true,true))[0].Reason==HitReason.Dead,"dead before defense");
 Console.WriteLine(passed+" defense contracts passed");}
}

