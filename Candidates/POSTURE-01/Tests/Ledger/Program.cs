using System;
using Ossastra.CombatCore;
using static Ossastra.CombatCore.PostureActionGroup;

static void Equal(double expected, double actual)
{
    if (Math.Abs(expected - actual) > 0.0000001 || double.IsNaN(actual))
        throw new Exception($"expected {expected}, received {actual}");
}

static void Check(bool condition)
{
    if (!condition) throw new Exception("condition failed");
}

static void Test(string name, Action check)
{
    try { check(); Console.WriteLine("PASS " + name); }
    catch (Exception error)
    {
        Console.WriteLine("FAIL " + name + " " + error.Message);
        Environment.ExitCode = 1;
    }
}

static ActionPostureLedger Ledger() => new ActionPostureLedger(100, .20, 40, 10);

Test("same group holds the greatest actual exposure, independent groups accumulate", () =>
{
    var ledger = Ledger();
    for (int i = 0; i < 5; i++)
    {
        var attack = Guid.NewGuid();
        ledger.Begin(Guid.Empty, MainAttack, 100, true);
        ledger.Begin(attack, MainAttack, 20, true);
        Equal(80, ledger.Current);
        ledger.End(attack);
    }
    ledger.Begin(Guid.NewGuid(), MainAttack, 35, true);
    ledger.Begin(Guid.NewGuid(), MainAttack, 20, true);
    ledger.Begin(Guid.NewGuid(), Dodge, 25, true);
    Equal(40, ledger.Current);
});

Test("clipped action charges actual capacity and no debt follows one exposure", () =>
{
    var ledger = Ledger();
    var attack = Guid.NewGuid();
    ledger.Begin(attack, MainAttack, 95, false);
    var dodge = Guid.NewGuid();
    ledger.Begin(dodge, Dodge, 30, false);
    Equal(5, ledger.GetDeficit(Dodge));
    Equal(0, ledger.Current);
    ledger.End(dodge);
    ledger.End(attack);
    ledger.Tick(1);
    Equal(32, ledger.Current);
    ledger.Begin(dodge, Dodge, 30, true);
    Equal(32, ledger.Current);
    Equal(0, ledger.GlobalRecoveryBlockers);
    ledger.Begin(Guid.NewGuid(), Dodge, 30, false);
    Equal(5.4, ledger.Current);
});

Test("only the final same-group owner opens its own recovery wait", () =>
{
    var ledger = Ledger();
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();
    ledger.Begin(first, MainAttack, 20, true);
    ledger.Begin(second, MainAttack, 20, false);
    ledger.End(Guid.NewGuid());
    ledger.End(first);
    ledger.Tick(.8);
    Equal(80, ledger.Current);
    ledger.End(second);
    ledger.End(second);
    ledger.Tick(.3);
    Equal(84, ledger.Current);
});

Test("ordinary walks and stable hang isolate their own group and deadlines survive global holds", () =>
{
    var ledger = Ledger();
    var attack = Guid.NewGuid();
    var dodge = Guid.NewGuid();
    ledger.Begin(Guid.NewGuid(), Locomotion, 5, false);
    ledger.Begin(Guid.NewGuid(), VerticalTraversal, 15, false);
    ledger.Begin(attack, MainAttack, 20, true);
    ledger.Begin(dodge, Dodge, 25, true);
    ledger.End(attack);
    ledger.Tick(.4);
    Equal(35, ledger.Current);
    ledger.End(dodge);
    ledger.Tick(.1);
    Equal(39, ledger.Current);
    Equal(15, ledger.GetDeficit(VerticalTraversal));
    Equal(25, ledger.GetDeficit(Dodge));
    Equal(16, ledger.GetDeficit(MainAttack));
});

Test("a single recovery budget is proportional and splits at per-group deadlines", () =>
{
    var ledger = Ledger();
    var attack = Guid.NewGuid();
    var dodge = Guid.NewGuid();
    ledger.Begin(attack, MainAttack, 20, true);
    ledger.Begin(dodge, Dodge, 30, true);
    ledger.End(attack);
    ledger.Tick(.1);
    ledger.End(dodge);
    ledger.Tick(.3);
    Equal(58, ledger.Current);
    Equal(16 - 4 * 16.0 / 46.0, ledger.GetDeficit(MainAttack));
    Equal(30 - 4 * 30.0 / 46.0, ledger.GetDeficit(Dodge));
    ledger.SetRecoveryMultiplier(1.5);
    ledger.Tick(.1);
    Equal(64, ledger.Current);
});

Test("holding guard starts free, pauses recovery, and stays active at zero", () =>
{
    var ledger = Ledger();
    var attack = Guid.NewGuid();
    ledger.Begin(attack, MainAttack, 20, true);
    ledger.End(attack);
    ledger.SetGuardHold(true);
    Equal(80, ledger.Current);
    ledger.Tick(2);
    Equal(60, ledger.Current);
    ledger.Tick(30);
    Equal(0, ledger.Current);
    Check(ledger.GuardHeld);
    ledger.Begin(Guid.NewGuid(), Dodge, 25, true);
    Equal(0, ledger.GetDeficit(Dodge));
});

Test("zero itself is not a break, legal HP damage or blocked impact can break", () =>
{
    var ledger = Ledger();
    ledger.Begin(Guid.NewGuid(), MainAttack, 100, true);
    Check(!ledger.ApplyImpact(0, false, true, false));
    Check(!ledger.ApplyImpact(25, true, false, false));
    Check(ledger.ApplyImpact(0, true, true, false));
    Equal(0, ledger.Current);
    Equal(0, ledger.GlobalRecoveryBlockers);
    ledger.Tick(.1);
    Equal(4, ledger.Current);
    Check(!ledger.ApplyImpact(110, true, true, true));
    Equal(4, ledger.Current);
    Check(ledger.ApplyImpact(10, false, true, false));
});

Test("multiple groups under one identity suspend without clearing numeric deficits", () =>
{
    var ledger = Ledger();
    var owner = Guid.NewGuid();
    ledger.Begin(owner, MainAttack, 20, true);
    ledger.Begin(owner, MainAttack, 20, true);
    ledger.Begin(owner, VerticalTraversal, 15, true);
    Equal(65, ledger.Current);
    Equal(1, ledger.RecoveryBlockers);
    ledger.Suspend();
    Equal(65, ledger.Current);
    Equal(0, ledger.RecoveryBlockers);
    ledger.Begin(owner, MainAttack, 20, true);
    Equal(0, ledger.RecoveryBlockers);
    ledger.Tick(.5);
    Equal(77, ledger.Current);
    ledger.Reset();
    Equal(100, ledger.Current);
});

Test("a new phase may raise exposure while a repeated node cannot change its recovery permission", () =>
{
    var ledger = Ledger();
    var owner = Guid.NewGuid();
    ledger.Begin(owner, VerticalTraversal, 15, true);
    ledger.Begin(owner, VerticalTraversal, 15, false);
    Equal(1, ledger.GlobalRecoveryBlockers);
    ledger.Begin(owner, VerticalTraversal, 20, true);
    Equal(20, ledger.GetDeficit(VerticalTraversal));
});

Test("two independent actor ledgers never touch each other", () =>
{
    var first = Ledger();
    var second = Ledger();
    var owner = Guid.NewGuid();
    first.Begin(owner, MainAttack, 30, true);
    second.Begin(owner, MainAttack, 20, true);
    first.Suspend();
    first.Tick(.3);
    Equal(74, first.Current);
    Equal(80, second.Current);
    Equal(1, second.GlobalRecoveryBlockers);
});

Test("a recovered guard bucket and hit bucket only spend one budget", () =>
{
    var ledger = Ledger();
    ledger.SetGuardHold(true);
    ledger.Tick(2);
    ledger.SetGuardHold(false);
    Check(!ledger.ApplyImpact(20, false, true, false));
    ledger.Tick(.7);
    Equal(80, ledger.Current);
    Equal(10, ledger.GuardDeficit);
    Equal(10, ledger.HitDeficit);
});
