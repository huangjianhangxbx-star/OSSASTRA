using System;using System.Linq;using Ossastra.CharacterCore;using Ossastra.CharacterCore.Abilities;
static class CombatResourcesTests
{
 static int checks;static void C(bool ok,string n){if(!ok)throw new Exception(n);Console.WriteLine("PASS "+n);checks++;}
 static void Main()
 {
  var h=new Health("a",100);var gray=new RecoverableHealthState(h);h.ApplyDamage(40);gray.RecordLoss(40);C(gray.Gray==40,"actual loss creates gray HP");
  var parent=Guid.NewGuid();C(gray.Reclaim(parent,20,.1,.5)==10&&h.Current==70,"skill gain uses existing health");C(gray.Reclaim(parent,100,.1,.5)==0&&h.Current==70,"second pulse shares parent budget");
  gray.Tick(4.5);C(Math.Abs(gray.Gray-25)<.001,"four second hold then ten percent decay");h.Heal(29);C(gray.Gray==1,"ordinary healing clamps gray");
  h.ApplyDamage(99);gray.RecordLoss(99);C(gray.Reclaim(Guid.NewGuid(),99,.1,.5)==0,"dead actor cannot reclaim");gray.Reset();C(gray.Gray==0,"death reset clears gray");
  var frost=new FrostReserveState();C(frost.Current==3&&frost.Capacity==3,"initial frost");C(frost.Spend(1,0)&&frost.Current==2,"frost commit");frost.Tick(1.9);C(frost.Current==2,"frost recovery delay");frost.Tick(2.5);C(Math.Abs(frost.Current-2.4)<.0001,"frost recovery crosses delay correctly");frost.SetModifiers(5,1.33);C(Math.Abs(frost.Current-4.4)<.0001,"capacity source adds exact delta");frost.SetModifiers(3,1);C(frost.Current==3,"source removal clamps capacity");
  var magazine=new MagazineState();C(magazine.Count==4&&magazine.Next==AmmoKind.Normal,"four actual bullets");var shot=Guid.NewGuid();C(magazine.Consume(shot)&&!magazine.Consume(shot)&&magazine.Count==3,"release only consumes once");
  var reload=Guid.NewGuid();C(magazine.BeginReload(reload,AmmoKind.Mortar,6,false,0,.8),"precision reserves reload");C(!magazine.CompleteReload(.7)&&magazine.Count==3,"reload not free before due");C(magazine.CompleteReload(.8)&&magazine.Count==6&&magazine.Next==AmmoKind.Mortar,"reload commits special sequence");C(!magazine.CompleteReload(1)&&magazine.Count==6,"no duplicate refill");
  magazine.BeginReload(Guid.NewGuid(),AmmoKind.Normal,4,false,2,.8);magazine.CancelReload();C(!magazine.CompleteReload(3)&&magazine.Count==6,"cancel preserves prior magazine without refill");
  C(magazine.BeginReload(Guid.NewGuid(),AmmoKind.Mortar,4,true,3,.8)&&magazine.Count==4&&magazine.Next==AmmoKind.Mortar,"instant reload and delayed path exclusive");
  Console.WriteLine(checks+" combat resource contracts passed");
 }
}
