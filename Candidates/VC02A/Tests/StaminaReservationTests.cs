using System;
using System.Reflection;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;

// 独立纯合同：真实余额、预留/提交/撤销及长按边界；不依赖Unity或另一个并行夹具。
internal static class StaminaReservationTests
{
    private static int passed,failed;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    // RED时以断言明确报告缺失合同，而非因未实现API制造编译失败。
    private static object Call(StaminaState state,string name,params object[] arguments)
    {
        var method=typeof(StaminaState).GetMethod(name,BindingFlags.Instance|BindingFlags.Public);
        Check(method!=null,"Missing stamina contract: "+name);
        try{return method.Invoke(state,arguments);}catch(TargetInvocationException e){throw e.InnerException;}
    }
    private static bool Reserve(StaminaState s,Guid token,double amount)=>(bool)Call(s,"TryReserve",token,amount);
    private static bool Commit(StaminaState s,Guid token,double now,Func<bool> source)=>(bool)Call(s,"CommitReserved",token,now,source);
    private static bool Cancel(StaminaState s,Guid token)=>(bool)Call(s,"CancelReservation",token);
    private static int Main()
    {
        Test("Reserved release cost protects concurrent accepted action",()=>{
            var s=new StaminaState(100,.5,20);var token=Guid.NewGuid();Check(Reserve(s,token,60),"reserve");
            Check(s.Current==100,"reservation cannot pay early");Check(!s.CanSpend(41),"concurrent budget");
            Check(s.TrySpend(Guid.NewGuid(),40,0),"legal concurrent cost");Check(!s.TrySpend(Guid.NewGuid(),1,0),"overspend blocked");
            Check(Commit(s,token,0,()=>true),"reserved payment");Check(s.Current==0,"only actual payments affect balance");});
        Test("Failed source transaction does not charge stamina",()=>{
            var s=new StaminaState(100,0,0);var token=Guid.NewGuid();Reserve(s,token,30);int calls=0;
            Check(!Commit(s,token,0,()=>{calls++;return false;}),"failure propagated");Check(s.Current==100&&calls==1,"no stamina loss");
            Check(!s.CanSpend(71),"budget still held until caller releases");Cancel(s,token);Check(s.CanSpend(100),"cancellation unholds");});
        Test("Successful source callback and release payment occur once",()=>{
            var s=new StaminaState(100,0,0);var token=Guid.NewGuid();Reserve(s,token,25);int calls=0;
            Check(Commit(s,token,0,()=>{calls++;return true;}),"commit");
            Check(!Commit(s,token,0,()=>{calls++;return true;}),"duplicate blocked");Check(s.Current==75&&calls==1,"one charge and callback");
            Check(!Reserve(s,token,25),"spent token cannot reserve twice");});
        Test("Cancelled release cannot invoke source or debit",()=>{
            var s=new StaminaState(100,0,0);var token=Guid.NewGuid();Reserve(s,token,40);Check(Cancel(s,token),"cancel");
            int calls=0;Check(!Commit(s,token,0,()=>{calls++;return true;}),"old release refused");Check(calls==0&&s.Current==100,"no stale side effects");});
        Test("Sustained drain cannot consume a reserved release budget",()=>{
            var s=new StaminaState(100,0,0);var token=Guid.NewGuid();Reserve(s,token,60);
            Check(!s.Drain(80,0),"sustain ends at spendable exhaustion");Check(s.Current==60,"reserved balance protected");
            Check(Commit(s,token,0,()=>true)&&s.Current==0,"release can still pay");});
        Test("Life restore invalidates pending reservation without payment",()=>{
            var s=new StaminaState(100,0,0);var token=Guid.NewGuid();Reserve(s,token,80);s.Restore();
            int calls=0;Check(!Commit(s,token,0,()=>{calls++;return true;}),"old generation reservation gone");
            Check(calls==0&&s.Current==100&&s.CanSpend(100),"restore restores availability");});
        Test("Invalid or duplicate reservations leave balance unchanged",()=>{
            var s=new StaminaState(100,0,0);var token=Guid.NewGuid();Check(!Reserve(s,Guid.Empty,5),"empty identity");
            Check(!Reserve(s,token,double.NaN)&&!Reserve(s,token,-1)&&!Reserve(s,token,101),"invalid budgets");
            Check(Reserve(s,token,20)&&!Reserve(s,token,20),"same reservation once");Check(s.CanSpend(80)&&!s.CanSpend(81),"not doubled");});
        Test("Invalid release clock never calls source",()=>{
            var s=new StaminaState(100,0,0);var token=Guid.NewGuid();Reserve(s,token,20);s.Tick(1,false);int calls=0;
            Check(!Commit(s,token,.5,()=>{calls++;return true;})&&!Commit(s,token,double.NaN,()=>{calls++;return true;}),"clock rejection");
            Check(calls==0&&s.Current==100,"no invalid-time source transaction");});
        Test("Long hold released after skipped threshold sample stays long",()=>{
            var g=new GestureRecognizer(GestureMode.TapOrLongPress,.3);g.Sample(false,0,1);g.Sample(true,.01,1);
            Check(g.Sample(false,.5,1)==GestureSignal.LongPress,"long gesture misclassified as tap");Check(g.Sample(false,.6,1)==GestureSignal.None,"only one winner");});
        Test("Short tap and already emitted long press retain single winner",()=>{
            var g=new GestureRecognizer(GestureMode.TapOrLongPress,.3);g.Sample(false,0,1);g.Sample(true,.01,1);Check(g.Sample(false,.2,1)==GestureSignal.Tap,"tap");
            g.Sample(true,.3,1);Check(g.Sample(true,.7,1)==GestureSignal.LongPress,"held long");Check(g.Sample(false,.8,1)==GestureSignal.End,"release no duplicate");});
        Console.WriteLine($"RESULT passed={passed} failed={failed}");return failed==0?0:1;
    }
}
