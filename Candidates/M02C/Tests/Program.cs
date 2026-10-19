// M02-C纯合同：来源只能选一条；跟随滞回/受阻只产意图，不接触设备或Unity位移。
using System;
using Ossastra.CharacterCore;
internal static class Program
{
    private static int failed, passed;
    private static void Check(string name, Action body)
    {
        try { body(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }
    private static void Expect(bool condition) { if (!condition) throw new Exception("contract expectation not met"); }
    private static void Main()
    {
        Check("current selects player only", () => Expect(CharacterIntentPolicy.Select(true, true, true, true) == CharacterIntentKind.Player));
        Check("non-current selects companion only", () => Expect(CharacterIntentPolicy.Select(true, true, false, true) == CharacterIntentKind.Companion));
        Check("no current or transfer barrier is neutral", () => Expect(CharacterIntentPolicy.Select(true, false, false, true) == CharacterIntentKind.Neutral));
        Check("no session preserves player adapter semantics", () => Expect(CharacterIntentPolicy.Select(false, false, false, false) == CharacterIntentKind.Player));
        Check("no companion stays neutral in managed session", () => Expect(CharacterIntentPolicy.Select(true, true, false, false) == CharacterIntentKind.Neutral));
        Check("A-B identity exchange keeps one source", () => {
            Expect(CharacterIntentPolicy.Select(true,true,false,true)==CharacterIntentKind.Companion);
            Expect(CharacterIntentPolicy.Select(true,true,true,true)==CharacterIntentKind.Player);
        });
        Check("far target emits normalized movement", () => {
            var f=new CompanionFollowPolicy(2.4,1.6,.5,.5);
            var d=f.Sample(3,4,0,0,0,.1,true);
            Expect(d.State==CompanionFollowState.Following && Math.Abs(d.MoveX-.6)<.0001 && Math.Abs(d.MoveZ-.8)<.0001);
        });
        Check("start stop hysteresis does not chatter", () => {
            var f=new CompanionFollowPolicy(2.4,1.6,.5,.5);
            Expect(f.Sample(2,0,0,0,0,.1,true).MoveX==0);
            Expect(f.Sample(3,0,0,0,0,.1,true).MoveX>0);
            Expect(f.Sample(2,0,0,0,.1,.1,true).MoveX>0);
            Expect(f.Sample(1.5,0,0,0,.1,.1,true).MoveX==0);
            Expect(f.Sample(1.8,0,0,0,0,.1,true).MoveX==0);
        });
        Check("blocked requires prior commanded zero travel", () => {
            var f=new CompanionFollowPolicy(2.4,1.6,.5,.5);
            f.Sample(4,0,0,0,0,.3,true);
            Expect(f.Sample(4,0,0,0,0,.3,true).State==CompanionFollowState.Following);
            Expect(f.Sample(4,0,0,0,0,.3,true).State==CompanionFollowState.Blocked);
            Expect(f.Sample(4,0,0,0,0,.5,true).MoveX==0);
            Expect(f.Sample(4,0,.6,0,0,.1,true).State==CompanionFollowState.Following);
        });
        Check("actual travel prevents false blocking", () => {
            var f=new CompanionFollowPolicy(2.4,1.6,.5,.5);
            for(int i=0;i<20;i++)Expect(f.Sample(4,0,0,0,.1,.1,true).State==CompanionFollowState.Following);
        });
        Check("ineligible resets moving state", () => {
            var f=new CompanionFollowPolicy(2.4,1.6,.5,.5);f.Sample(4,0,0,0,0,.1,true);
            Expect(f.Sample(4,0,0,0,0,.1,false).State==CompanionFollowState.Inactive);
            Expect(f.Sample(2,0,0,0,0,.1,true).MoveX==0);
        });
        Check("invalid samples are neutral not NaN movement", () => {
            var f=new CompanionFollowPolicy(2.4,1.6,.5,.5);
            Expect(f.Sample(double.NaN,0,0,0,0,.1,true).State==CompanionFollowState.Inactive);
            Expect(f.Sample(4,0,0,0,0,-1,true).MoveX==0);
        });
        Check("threshold configuration fails before use", () => {
            bool rejected=false;try{new CompanionFollowPolicy(1,2,.5,.5);}catch(ArgumentException){rejected=true;}Expect(rejected);
        });
        Console.WriteLine("RESULT " + passed + "/" + (passed+failed) + " passed");
        Environment.ExitCode=failed==0?0:1;
    }
}
