#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class DRPGBattlePresentationRuntimeProbe
{
    public static string Run()
    {
        VerifySettleOnceHandle();
        VerifyCompositeHandle();
        VerifyInputGateLeases();
        VerifyScopeIdentity();
        VerifyBoundedTrace();
        return "PASS DRPG presentation runtime";
    }

    private static void VerifySettleOnceHandle()
    {
        var handle = new BattlePresentationHandle();
        int earlyCallbacks = 0;
        int lateCallbacks = 0;

        handle.WhenSettled(outcome =>
        {
            AssertEqual(
                BattlePresentationOutcome.Completed,
                outcome,
                "The first terminal outcome must be retained");
            earlyCallbacks++;
        });

        AssertTrue(
            handle.TrySettle(BattlePresentationOutcome.Completed),
            "The first settlement must succeed");
        AssertFalse(
            handle.TrySettle(BattlePresentationOutcome.Cancelled),
            "A second settlement must be ignored");
        handle.WhenSettled(outcome =>
        {
            AssertEqual(
                BattlePresentationOutcome.Completed,
                outcome,
                "Late subscribers must observe the retained outcome");
            lateCallbacks++;
        });

        AssertTrue(handle.IsSettled, "The handle must report settled");
        AssertEqual(
            BattlePresentationOutcome.Completed,
            handle.Outcome,
            "The handle must expose its terminal outcome");
        AssertEqual(1, earlyCallbacks, "Early subscribers must run once");
        AssertEqual(1, lateCallbacks, "Late subscribers must run once");
        AssertThrows<ArgumentException>(
            () => new BattlePresentationHandle().TrySettle(
                BattlePresentationOutcome.Pending),
            "Pending is not a terminal outcome");
    }

    private static void VerifyCompositeHandle()
    {
        var first = new BattlePresentationHandle();
        var second = new BattlePresentationHandle();
        var composite = new BattlePresentationCompositeHandle(
            new IBattlePresentationHandle[] { first, second });

        first.TrySettle(BattlePresentationOutcome.Skipped);
        AssertFalse(
            composite.IsSettled,
            "The composite must wait for every child");
        second.TrySettle(BattlePresentationOutcome.Completed);
        AssertTrue(
            composite.IsSettled,
            "The composite must settle when every child settles");
        AssertEqual(
            BattlePresentationOutcome.Completed,
            composite.Outcome,
            "A completed composite may contain skipped optional children");

        var cancellableChild = new BattlePresentationHandle();
        var cancelledComposite = new BattlePresentationCompositeHandle(
            new IBattlePresentationHandle[] { cancellableChild });
        cancelledComposite.Cancel(BattlePresentationCancelReason.ScopeEnded);
        AssertEqual(
            BattlePresentationOutcome.Cancelled,
            cancellableChild.Outcome,
            "Cancelling a composite must cancel unsettled children");
        AssertEqual(
            BattlePresentationOutcome.Cancelled,
            cancelledComposite.Outcome,
            "A cancelled composite must expose cancellation");
    }

    private static void VerifyInputGateLeases()
    {
        var gate = new BattleInputGate();
        var transitions = new List<bool>();
        gate.Changed += transitions.Add;

        IDisposable first = gate.Acquire("timeline");
        IDisposable second = gate.Acquire("turn-quote");
        AssertTrue(gate.IsBlocked, "Any live lease must block input");
        AssertEqual(1, transitions.Count, "Only the first lease blocks input");
        AssertEqual(true, transitions[0], "The first transition must block");

        first.Dispose();
        AssertTrue(gate.IsBlocked, "The second lease must keep input blocked");
        first.Dispose();
        AssertEqual(1, transitions.Count, "A lease must be idempotent");

        second.Dispose();
        AssertFalse(gate.IsBlocked, "Releasing every lease must unblock input");
        AssertEqual(2, transitions.Count, "Unblocking must fire one transition");
        AssertEqual(false, transitions[1], "The final transition must unblock");
    }

    private static void VerifyScopeIdentity()
    {
        var scope = new BattlePresentationScope(2, 3, 7);
        var same = new BattlePresentationScope(2, 3, 7);
        var previousBattle = new BattlePresentationScope(1, 3, 7);
        var previousTurn = new BattlePresentationScope(2, 2, 7);
        var previousAction = new BattlePresentationScope(2, 3, 6);

        AssertTrue(scope.Equals(same), "Equal scope identities must compare equal");
        AssertEqual(scope.GetHashCode(), same.GetHashCode(),
            "Equal scopes must have equal hash codes");
        AssertFalse(scope.Equals(previousBattle),
            "Battle generation must participate in scope identity");
        AssertFalse(scope.Equals(previousTurn),
            "Turn serial must participate in scope identity");
        AssertFalse(scope.Equals(previousAction),
            "Action id must participate in scope identity");
    }

    private static void VerifyBoundedTrace()
    {
        var trace = new BattlePresentationTraceBuffer(2);
        trace.Record("one");
        trace.Record("two");
        trace.Record("three");

        AssertEqual(2, trace.Entries.Count,
            "Trace capacity must remain bounded");
        AssertEqual("two", trace.Entries[0],
            "Trace must evict the oldest entry first");
        AssertEqual("three", trace.Entries[1],
            "Trace must retain the newest entry");
        AssertThrows<ArgumentOutOfRangeException>(
            () => new BattlePresentationTraceBuffer(0),
            "Trace capacity below one must be rejected");
    }

    private static void AssertThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
