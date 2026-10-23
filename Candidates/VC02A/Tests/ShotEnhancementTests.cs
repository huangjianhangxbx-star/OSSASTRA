using System;using Ossastra.CharacterCore.Abilities;
class ShotEnhancementTests
{
 static int checks;static void C(bool ok){checks++;if(!ok)throw new Exception("check "+checks);}
 static void Main()
 {
  var sources=new EffectSourceSet();sources.Grant("s",new EffectSourceDefinition("precision","yellow","reload",modifiers:new[]{new EffectModifier("precision",reloadShotRate:.4,reloadShotDamage:.35,guaranteedCrit:true)}));
  var snapshot=sources.Snapshot("yellow",new[]{"precision"},0,false);var state=new ShotEnhancementState();
  state.ReloadStarted();state.PrecisionAccepted(snapshot,0);C(state.Snapshot().DamageMultiplier==2.25);
  state.Tick(.04,sources);C(state.Snapshot().Rate==1);state.Tick(.06,sources);C(state.Snapshot().Rate==1.4);
  state.ReloadCompleted();C(Math.Abs(state.Snapshot().DamageMultiplier-1.35*2.25)<1e-8);
  var shot=state.Snapshot();state.Tick(100,sources);C(state.Snapshot().CriticalSource=="s");state.Applied(shot);C(state.Snapshot().CriticalSource==null);
  state.ReloadStarted();C(state.Snapshot().Rate==1);C(state.Snapshot().DamageMultiplier==1.35);
  state.ReloadCompleted();C(state.Snapshot().DamageMultiplier==1);sources.Remove("s");state.Tick(101,sources);C(state.Snapshot().Rate==1);
  C(shot.CriticalSource=="s"&&shot.DamageMultiplier>3);state.Clear();C(state.Snapshot().DamageMultiplier==1);
  Console.WriteLine("PASS "+checks+" shot-enhancement contracts");
 }
}
