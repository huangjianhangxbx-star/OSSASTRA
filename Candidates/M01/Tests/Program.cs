using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;

internal static class Program
{
    private static int failures;
    private static int total;
    private static BasicAttackDefinition Attack(double release = .25, double move = .4,
        double ready = .6, double finish = .8, int damage = 12)
        => new BasicAttackDefinition(release, move, ready, finish, damage);

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void Test(string name, Action body)
    {
        total++;
        try { body(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }

    private static BasicActionRuntime Started()
    {
        var runtime = new BasicActionRuntime("player");
        Check(runtime.Request(Attack(), "player-input", new ControlAuthority()).Accepted,
            "A legal request must be accepted");
        return runtime;
    }

    private static AttackRelease Released()
        => Started().Advance(.25, false).Single(f => f.Kind == ActionFactKind.Released).Release;

    private static int Main()
    {
        Test("owner release cannot unlock another owner's block", () => {
            var gate = new ControlAuthority(); var menu = new object(); var stun = new object();
            gate.Block(menu, ControlChannel.Attack); gate.Block(stun, ControlChannel.Attack);
            gate.Release(menu);
            Check(!gate.Allows(ControlChannel.Attack), "Second owner's attack block lost");
            gate.Release(stun); Check(gate.Allows(ControlChannel.Attack), "Block never released");
        });
        Test("move and attack permissions remain independent", () => {
            var gate = new ControlAuthority(); gate.Block(new object(), ControlChannel.Move);
            Check(!gate.Allows(ControlChannel.Move) && gate.Allows(ControlChannel.Attack), "Channels conflated");
        });
        Test("owner changes only its own channels", () => {
            var gate = new ControlAuthority(); var owner = new object();
            gate.Block(owner, ControlChannel.Attack); gate.Block(owner, ControlChannel.Move);
            Check(gate.Allows(ControlChannel.Attack) && !gate.Allows(ControlChannel.Move), "Owner replacement failed");
        });
        Test("held input emits only a rising edge", () => {
            var input = new AttackIntent();
            Check(input.Observe(true) && !input.Observe(true), "Held repeated attack");
            Check(!input.Observe(false) && input.Observe(true), "Next press lost");
        });
        Test("focus reset requires release before a fresh press", () => {
            var input = new AttackIntent(); input.Observe(true); input.SuppressUntilRelease();
            Check(!input.Observe(true) && !input.Observe(true), "Stale held fired after focus reset");
            input.Observe(false); Check(input.Observe(true), "Fresh press not rearmed");
        });
        Test("request acceptance creates identity without releasing", () => {
            var runtime = new BasicActionRuntime("player");
            var result = runtime.Request(Attack(), "player-input", new ControlAuthority());
            Check(result.Accepted && result.Fact.Kind == ActionFactKind.Accepted &&
                result.Fact.ActionId != Guid.Empty && runtime.IsActive && !runtime.MoveReady,
                "Request/accept/release identity collapsed");
            Check(runtime.Advance(.24, false).Count == 0, "Attack released before its node");
        });
        Test("blocked and busy requests create no new action", () => {
            var runtime = new BasicActionRuntime("player"); var gate = new ControlAuthority();
            var owner = new object(); gate.Block(owner, ControlChannel.Attack);
            Check(runtime.Request(Attack(), "player-input", gate).Reason == "attack-blocked" && !runtime.IsActive,
                "Blocked input accepted");
            gate.Release(owner); var first = runtime.Request(Attack(), "player-input", gate);
            var denied = runtime.Request(Attack(), "player-input", gate);
            Check(!denied.Accepted && denied.Reason == "busy" && runtime.ActionId == first.Fact.ActionId,
                "Busy request replaced accepted action");
        });
        Test("large delta emits ordered nodes exactly once", () => {
            var runtime = Started(); var facts = runtime.Advance(2, false);
            Check(string.Join(",", facts.Select(f => f.Kind)) == "Released,MoveReady,AttackReady,Finished",
                "Crossed nodes missing, repeated or unordered");
            Check(facts[0].At == .25 && facts[3].At == .8, "Node stamped with overshot frame time");
            Check(runtime.Advance(2, false).Count == 0 && !runtime.IsActive, "Finished action ran again");
        });
        Test("paused clock does not consume elapsed time", () => {
            var runtime = Started(); Check(runtime.Advance(100, true).Count == 0, "Pause emitted nodes");
            Check(runtime.Advance(.24, false).Count == 0, "Paused time accumulated");
            Check(runtime.Advance(.01, false).Single().Kind == ActionFactKind.Released, "Unpause lost release");
        });
        Test("move and attack readiness remain separate from finish", () => {
            var runtime = Started(); runtime.Advance(.25, false);
            Check(!runtime.MoveReady && !runtime.AttackReady, "Release silently unlocked both permissions");
            runtime.Advance(.15, false); Check(runtime.MoveReady && !runtime.AttackReady, "Move node lost");
            runtime.Advance(.2, false); Check(runtime.AttackReady && runtime.IsActive, "Ready collapsed into finish");
        });
        Test("zero-time nodes wait for tick then follow stable tie order", () => {
            var runtime = new BasicActionRuntime("player");
            Check(runtime.Request(Attack(0, 0, 0, 0), "system", new ControlAuthority()).Accepted, "Zero-time request rejected");
            Check(string.Join(",", runtime.Advance(0, false).Select(f => f.Kind)) ==
                "Released,MoveReady,AttackReady,Finished", "Zero-time nodes wrong");
        });
        Test("invalid time never mutates a live action", () => {
            var runtime = Started(); Throws<ArgumentOutOfRangeException>(() => runtime.Advance(double.NaN, false));
            Throws<ArgumentOutOfRangeException>(() => runtime.Advance(-1, false));
            Throws<ArgumentOutOfRangeException>(() => runtime.Advance(double.PositiveInfinity, false));
            Check(runtime.Advance(.24, false).Count == 0, "Invalid delta changed clock");
        });
        Test("invalid configuration rejected before use", () => {
            Throws<ArgumentOutOfRangeException>(() => Attack(-.1));
            Throws<ArgumentOutOfRangeException>(() => Attack(.3, .2));
            Throws<ArgumentOutOfRangeException>(() => Attack(.2, .3, .4, .1));
            Throws<ArgumentOutOfRangeException>(() => Attack(damage: 0));
            Throws<ArgumentOutOfRangeException>(() => Attack(finish: double.NaN));
        });
        Test("pre-release cancel has one terminal and no damage credential", () => {
            var runtime = Started(); runtime.Advance(.1, false);
            var terminal = runtime.Cancel("focus-lost");
            Check(terminal != null && terminal.Kind == ActionFactKind.Cancelled && terminal.Reason == "focus-lost",
                "Cancellation fact or cause missing");
            Check(runtime.Cancel("disabled") == null && runtime.Advance(1, false).Count == 0,
                "Cancelled action terminated twice or released");
        });
        Test("empty cancel cause cannot erase an action", () => {
            var runtime = Started(); Throws<ArgumentException>(() => runtime.Cancel(" "));
            Check(runtime.IsActive, "Invalid cancellation mutated action");
        });
        Test("cancel after release preserves committed damage and never releases again", () => {
            var runtime = Started(); var release = runtime.Advance(.25, false).Single().Release;
            var target = new Health("enemy", 100); runtime.Cancel("disabled");
            var outcome = release.Resolve(new[] { target }).Single();
            Check(outcome.HpBefore == 100 && outcome.HpAfter == 88 && target.Current == 88,
                "Body cancellation erased committed effect");
            Check(runtime.Advance(10, false).Count == 0, "Cancelled action emitted another release");
        });
        Test("re-enable simulation cannot reuse cancelled action identity", () => {
            var runtime = Started(); var oldId = runtime.ActionId; runtime.Cancel("disabled");
            var next = runtime.Request(Attack(), "player-input", new ControlAuthority());
            Check(next.Accepted && next.Fact.ActionId != oldId, "New lifecycle reused old identity");
        });
        Test("duplicate collider candidates damage one target only once", () => {
            var target = new Health("enemy", 100);
            var hits = Released().Resolve(new[] { target, target });
            Check(target.Current == 88 && hits.Count(h => h.Reason == HitReason.Applied) == 1 &&
                hits.Count(h => h.Reason == HitReason.Duplicate) == 1, "Duplicate target damaged twice");
        });
        Test("one release may hit two independently identified targets", () => {
            var a = new Health("enemy-a", 100); var b = new Health("enemy-b", 100);
            Released().Resolve(new[] { a, b }); Check(a.Current == 88 && b.Current == 88, "Targets conflated");
        });
        Test("empty swing closes the release without later phantom hits", () => {
            var release = Released(); var target = new Health("enemy", 100);
            Check(release.Resolve(Array.Empty<Health>()).Count == 0, "Empty swing invented target");
            Throws<InvalidOperationException>(() => release.Resolve(new[] { target }));
            Check(target.Current == 100, "Closed empty swing hit later");
        });
        Test("same release cannot be resolved for new targets a second time", () => {
            var release = Released(); var a = new Health("a", 100); var b = new Health("b", 100);
            release.Resolve(new[] { a }); Throws<InvalidOperationException>(() => release.Resolve(new[] { b }));
            Check(a.Current == 88 && b.Current == 100, "Release window reopened");
        });
        Test("different actions can damage the same target", () => {
            var target = new Health("enemy", 100); Released().Resolve(new[] { target });
            Released().Resolve(new[] { target }); Check(target.Current == 76, "Target ledger leaked across actions");
        });
        Test("self and dead target do not receive damage", () => {
            var self = new Health("player", 100); var enemy = new Health("enemy", 5);
            Released().Resolve(new[] { enemy });
            var hits = Released().Resolve(new[] { self, enemy });
            Check(self.Current == 100 && enemy.Current == 0 && hits[0].Reason == HitReason.Self &&
                hits[1].Reason == HitReason.Dead, "Self/dead filtering failed or HP became negative");
        });
        Test("null target batch is validated before any HP changes", () => {
            var release = Released(); var target = new Health("enemy", 100);
            Throws<ArgumentException>(() => release.Resolve(new[] { target, null }));
            Check(target.Current == 100, "Invalid batch partially damaged target");
            release.Resolve(new[] { target }); Check(target.Current == 88, "Invalid batch consumed release");
        });
        Test("invalid identity and health cannot enter authority", () => {
            Throws<ArgumentException>(() => new Health(" ", 100));
            Throws<ArgumentOutOfRangeException>(() => new Health("enemy", 0));
            Throws<ArgumentException>(() => new BasicActionRuntime(" "));
        });
        Test("source identity follows accepted action into all facts and hit result", () => {
            var runtime = new BasicActionRuntime("player");
            var accepted = runtime.Request(Attack(), "player-input", new ControlAuthority());
            var facts = runtime.Advance(1, false); var release = facts[0].Release;
            var hit = release.Resolve(new[] { new Health("enemy", 100) }).Single();
            Check(facts.All(f => f.ActionId == accepted.Fact.ActionId && f.Source == "player-input") &&
                hit.ActionId == accepted.Fact.ActionId && hit.ActorId == "player" && hit.TargetId == "enemy",
                "Identity/source not preserved through result");
        });
        Test("sixty-Hz frame partition releases on frame fifteen, not sixteen", () => {
            var runtime = Started();
            for (int frame = 1; frame < 15; frame++)
                Check(runtime.Advance(1.0 / 60, false).Count == 0, "Release happened before frame fifteen");
            Check(runtime.Advance(1.0 / 60, false).Single().Kind == ActionFactKind.Released,
                "Floating-point accumulation delayed the release by one frame");
        });
        Test("AttackReady actually accepts a new press before old Finish", () => {
            var runtime = Started(); var oldId = runtime.ActionId;
            runtime.Advance(.6, false);
            var next = runtime.Request(Attack(), "player-input", new ControlAuthority());
            Check(next.Accepted && next.Fact.ActionId != oldId,
                "AttackReady advertised readiness but still rejected the next press");
            var terminal = next.PreviousTerminal;
            Check(terminal != null && terminal.ActionId == oldId && terminal.Kind == ActionFactKind.Cancelled &&
                terminal.Reason == "replaced-by-next-attack", "Prior action lost its named terminal");
            Check(runtime.Advance(.25, false).Single().ActionId == next.Fact.ActionId,
                "Previous runtime leaked into new action");
        });
        Test("pause rejects new requests without consuming the next valid press", () => {
            var runtime = new BasicActionRuntime("player"); var gate = new ControlAuthority();
            var denied = runtime.Request(Attack(), "player-input", gate, true);
            Check(!denied.Accepted && denied.Reason == "paused" && !runtime.IsActive,
                "Paused request created an active action");
            Check(runtime.Request(Attack(), "player-input", gate, false).Accepted,
                "Paused rejection prevented a later legal request");
        });
        Console.WriteLine($"RESULT {total - failures}/{total} passed");
        return failures == 0 ? 0 : 1;
    }
}
