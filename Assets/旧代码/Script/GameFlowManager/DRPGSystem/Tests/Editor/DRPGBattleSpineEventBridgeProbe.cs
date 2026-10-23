#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class DRPGBattleSpineEventBridgeProbe
{
    public static string Run()
    {
        VerifyMatchAndDuplicateDiscipline();
        VerifyCancellationAndTimeout();
        VerifyDisabledSourceFailsOpen();
        return "PASS DRPG Spine marker gateway";
    }

    private static void VerifyMatchAndDuplicateDiscipline()
    {
        float now = 10f;
        var source = new FakeSource();
        var trace = new BattlePresentationTraceBuffer(32);
        using (var bridge = new BattleSpineEventBridge(
            actorId => actorId == 7 ? source : null,
            trace,
            () => now))
        {
            int matched = 0;
            int timedOut = 0;
            IBattlePresentationHandle handle = bridge.WaitFor(
                new BattlePresentationScope(3, 4, 99),
                7,
                new BattleSpineMarker(BattleSpineMarkerNames.Impact, 2),
                1f,
                () => matched++,
                () => timedOut++);

            source.Emit(new BattleSpineMarker(BattleSpineMarkerNames.Impact, 1));
            AssertTrue(!handle.IsSettled, "An unrelated impact index must be ignored");
            source.Emit(new BattleSpineMarker(BattleSpineMarkerNames.Impact, 2));
            AssertEqual(BattlePresentationOutcome.Completed, handle.Outcome,
                "The expected marker must complete the wait");
            AssertEqual(1, matched, "Match callback must run exactly once");
            AssertEqual(0, timedOut, "A matched marker must not time out");

            source.Emit(new BattleSpineMarker(BattleSpineMarkerNames.Impact, 2));
            AssertEqual(1, matched, "A duplicate marker must not recommit");
            AssertTraceContains(trace, "duplicate/late",
                "Duplicate marker rejection must be traceable");
        }
    }

    private static void VerifyCancellationAndTimeout()
    {
        float now = 20f;
        var source = new FakeSource();
        var trace = new BattlePresentationTraceBuffer(32);
        using (var bridge = new BattleSpineEventBridge(
            _ => source,
            trace,
            () => now))
        {
            int matched = 0;
            int timedOut = 0;
            IBattlePresentationHandle cancelled = bridge.WaitFor(
                new BattlePresentationScope(1, 1, 1),
                1,
                new BattleSpineMarker(BattleSpineMarkerNames.PresentationEnd, 0),
                1f,
                () => matched++,
                () => timedOut++);
            cancelled.Cancel(BattlePresentationCancelReason.ScopeEnded);
            source.Emit(new BattleSpineMarker(BattleSpineMarkerNames.PresentationEnd, 0));
            AssertEqual(BattlePresentationOutcome.Cancelled, cancelled.Outcome,
                "Cancellation must settle deterministically");
            AssertEqual(0, matched, "A late marker after cancellation must do nothing");
            AssertEqual(0, timedOut, "Cancellation is not a timeout");

            IBattlePresentationHandle timeout = bridge.WaitFor(
                new BattlePresentationScope(1, 2, 2),
                1,
                new BattleSpineMarker(BattleSpineMarkerNames.WindupComplete, 0),
                0.5f,
                () => matched++,
                () => timedOut++);
            now = 20.49f;
            bridge.Tick();
            AssertTrue(!timeout.IsSettled, "Wait must remain pending before deadline");
            now = 20.5f;
            bridge.Tick();
            AssertEqual(BattlePresentationOutcome.TimedOut, timeout.Outcome,
                "Unscaled watchdog expiry must settle as TimedOut");
            AssertEqual(1, timedOut, "Timeout callback must run exactly once");
        }
    }

    private static void VerifyDisabledSourceFailsOpen()
    {
        var trace = new BattlePresentationTraceBuffer(8);
        using (var bridge = new BattleSpineEventBridge(
            _ => null,
            trace,
            () => 0f))
        {
            int timedOut = 0;
            IBattlePresentationHandle handle = bridge.WaitFor(
                new BattlePresentationScope(1, 1, 3),
                999,
                new BattleSpineMarker(BattleSpineMarkerNames.HitStop, 0),
                1f,
                null,
                () => timedOut++);
            AssertEqual(BattlePresentationOutcome.TimedOut, handle.Outcome,
                "A disabled source must fail open immediately");
            AssertEqual(1, timedOut, "Disabled source must invoke fallback once");
            AssertTraceContains(trace, "source missing",
                "Missing source must be traceable");
        }
    }

    private static void AssertTraceContains(
        BattlePresentationTraceBuffer trace,
        string expected,
        string message)
    {
        for (int i = 0; i < trace.Entries.Count; i++)
        {
            if (trace.Entries[i].IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0)
                return;
        }
        throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private sealed class FakeSource : IBattleSpineEventSource
    {
        private event Action<BattleSpineMarker> Marker;

        public IDisposable Subscribe(Action<BattleSpineMarker> onMarker)
        {
            Marker += onMarker;
            return new Subscription(() => Marker -= onMarker);
        }

        public void Emit(BattleSpineMarker marker)
        {
            Marker?.Invoke(marker);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Action dispose;
        public Subscription(Action dispose) { this.dispose = dispose; }
        public void Dispose()
        {
            Action action = dispose;
            dispose = null;
            action?.Invoke();
        }
    }
}
#endif
