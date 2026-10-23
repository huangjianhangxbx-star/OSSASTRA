using System;
using Ossastra.Combat;
class CombatStatusTests
{
    static int checks;
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static void Main()
    {
        var state=new CombatStatusState();var a=Guid.NewGuid();var b=Guid.NewGuid();
        Check(state.ApplySlow(a,.35,1)&&Math.Abs(state.SpeedScale-.65)<1e-8,"slow follows source ratio");
        state.Tick(0);Check(state.SpeedScale<1,"pause does not expire");
        state.ApplySlow(a,.35,1);state.Tick(.6);Check(state.SpeedScale<1,"same source refreshes");
        state.ApplySlow(b,.5,.2);Check(state.SpeedScale==.5,"strongest slow, no exponential stacking");
        state.Tick(.25);Check(Math.Abs(state.SpeedScale-.65)<1e-8,"weaker surviving slow restored");
        state.ApplyStun(a,1.5);state.Tick(.75);Check(state.IsStunned,"stun still active");
        state.ApplyStun(a,.1);state.Tick(.2);Check(state.IsStunned,"shorter refresh cannot shorten stun");
        state.Tick(1);Check(!state.IsStunned&&state.SpeedScale==1,"all timers expire");
        Check(!state.ApplySlow(a,double.NaN,1)&&!state.ApplyStun(Guid.Empty,2),"invalid effects rejected");
        state.ApplyStun(b,1);state.Clear();Check(!state.IsStunned&&state.SpeedScale==1,"death clear");
        Console.WriteLine("PASS "+checks+" combat-status contracts");
    }
}
