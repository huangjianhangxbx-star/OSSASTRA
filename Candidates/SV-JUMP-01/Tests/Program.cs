using System;
using Ossastra.CharacterCore;

static void Check(bool pass, string name) { if(!pass) throw new Exception(name); Console.WriteLine(name); }
var cycle = new OrdinaryJumpCycle();
Check(!cycle.CanLaunch(4), "air spawn grants no ground grace");
cycle.Observe(false, true, false, false);
Check(cycle.CanLaunch(4), "collision landing grants launch");
cycle.Observe(false, false, false, false);
for (int i=1;i<4;i++) { Check(cycle.CanLaunch(4), "natural edge grace "+i); cycle.Observe(false,false,false,false); }
Check(cycle.CanLaunch(4), "last natural edge step");
cycle.Observe(false,false,false,false);
Check(!cycle.CanLaunch(4), "grace expires");
cycle.Observe(false,true,false,false);
cycle.Launch();
Check(!cycle.CanLaunch(4), "no double jump via grace");
Check(cycle.Step(9,2)==JumpVerticalCommand.Sustain,"initial sustain");
cycle.Release();
Check(cycle.Step(9,2)==JumpVerticalCommand.Sustain,"short jump has two full sustain steps");
Check(cycle.Step(9,2)==JumpVerticalCommand.Cut,"release cuts once");
Check(cycle.Step(9,2)==JumpVerticalCommand.Cut,"released rise continues cutting on the simulation clock");
cycle.Observe(true,false,true,false);
Check(cycle.Phase==OrdinaryJumpPhase.Falling && !cycle.CanLaunch(4), "ceiling cancels rise");
cycle.Observe(false,true,false,false);
Check(cycle.CanLaunch(4), "new jump after landing");
cycle.Launch();
for(int i=0;i<9;i++) Check(cycle.Step(9,2)==JumpVerticalCommand.Sustain,"long sustain "+i);
Check(cycle.Step(9,2)==JumpVerticalCommand.Gravity,"hold budget ends");
cycle.Release();
Check(cycle.Step(9,2)==JumpVerticalCommand.Cut,"release after sustain while rising");
cycle.Reset();
Check(!cycle.CanLaunch(4)&&cycle.Phase==OrdinaryJumpPhase.Uninitialized,"lifecycle clears contact");
cycle.Observe(false,true,false,false);
cycle.Launch();
cycle.CancelHold();
Check(cycle.Step(9,2)==JumpVerticalCommand.Gravity,"invalidated input continues gravity without a cut");
cycle.Observe(false,false,false,false);
Check(Math.Abs(cycle.GravityScale(.02f,true,.1f,5f)-.2f)<.00001f,"first held falling tick reduces gravity");
Check(cycle.GravityScale(.02f,false,.1f,5f)==1f,"release restores falling gravity");
