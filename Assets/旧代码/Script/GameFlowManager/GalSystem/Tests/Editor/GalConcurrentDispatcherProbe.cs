#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class GalConcurrentDispatcherProbe
{
    public static string Run()
    {
        QueuesDeduplicatesAndAdvancesInFifoOrder();
        UsesKeyFallbacksAndExpiresCooldowns();
        RejectsInvalidAndBusyRequests();
        ClearsQueueWhenDequeuedStartFails();
        PreemptsConcurrentBeforeStartingHigherMode();
        StopAndDisposeAreIdempotent();
        LargeTickAdvancesAtMostOneLine();
        return "PASS gal concurrent dispatcher";
    }

    private static void QueuesDeduplicatesAndAdvancesInFifoOrder()
    {
        GalSequenceSO sequenceA = CreateSequence("a", 2f, "A");
        GalSequenceSO sequenceB = CreateSequence("b", 5f, "B");
        GalSequenceSO sequenceC = CreateSequence("c", 5f, "C");
        GalSequenceSO sequenceD = CreateSequence("d", 5f, "D");
        var events = new EventBus();
        var machine = CreateMachine(events, out _, out _);
        var dispatcher = new GalConcurrentDispatcher(machine, events, 2);
        var started = new List<GalSequenceSO>();
        var progress = new List<float>();
        events.Subscribe<GalStartedEvent>(e => started.Add(e.Sequence));
        dispatcher.ProgressChanged += value => progress.Add(value);

        try
        {
            GalPlayRequest requestA = Concurrent(sequenceA, "probe.a", 1f);
            GalPlayRequest requestB = Concurrent(sequenceB, "probe.b", 1f);
            GalPlayRequest requestC = Concurrent(sequenceC, "probe.c", 1f);
            GalPlayRequest requestD = Concurrent(sequenceD, "probe.d", 1f);

            AssertEqual(GalStartResult.Started, dispatcher.Request(requestA),
                "An idle Concurrent request must start immediately");
            AssertEqual(GalStartResult.Queued, dispatcher.Request(requestB),
                "A second Concurrent request must enter the FIFO");
            AssertEqual(GalStartResult.DuplicateIgnored, dispatcher.Request(requestB),
                "A queued key must be deduplicated");
            AssertEqual(1, dispatcher.QueuedCount,
                "A duplicate must not grow the FIFO");
            AssertEqual(GalStartResult.Queued, dispatcher.Request(requestC),
                "A distinct request must fill the remaining queue slot");
            AssertEqual(GalStartResult.QueueFull, dispatcher.Request(requestD),
                "A distinct request beyond capacity must be rejected explicitly");
            AssertEqual(2, dispatcher.QueuedCount,
                "A full rejection must not change the queue");

            AssertApproximately(0f, dispatcher.CurrentLineProgress,
                "A newly presented line must begin at zero progress");
            dispatcher.Tick(-4f);
            AssertApproximately(0f, dispatcher.CurrentLineProgress,
                "A negative delta must not advance progress");
            dispatcher.Tick(1f);
            AssertApproximately(0.5f, dispatcher.CurrentLineProgress,
                "Half of the line duration must report half progress");
            dispatcher.Tick(1f);

            AssertSame(sequenceB, machine.ActiveRequest.Value.Sequence,
                "Completing A must start B before C");
            AssertEqual(1, dispatcher.QueuedCount,
                "Starting B must leave only C queued");
            AssertEqual(2, started.Count,
                "Only A and dequeued B may publish Started");
            AssertSame(sequenceA, started[0], "A must publish Started first");
            AssertSame(sequenceB, started[1], "B must publish Started second");
            AssertTrue(ContainsApproximately(progress, 1f),
                "Progress observers must see the completed value before advancement");
            AssertApproximately(0f, dispatcher.CurrentLineProgress,
                "The next line must reset progress to zero");
            AssertEqual(GalStartResult.DuplicateIgnored, dispatcher.Request(requestA),
                "A completed key must remain deduplicated during cooldown");
        }
        finally
        {
            dispatcher.Dispose();
            Destroy(sequenceA, sequenceB, sequenceC, sequenceD);
        }
    }

    private static void UsesKeyFallbacksAndExpiresCooldowns()
    {
        GalSequenceSO withId = CreateSequence("fallback.sequence", 0.5f, "ID");
        GalSequenceSO withoutId = CreateSequence(string.Empty, 5f, "Instance");
        var events = new EventBus();
        var machine = CreateMachine(events, out _, out _);
        var dispatcher = new GalConcurrentDispatcher(machine, events, 2);

        try
        {
            GalPlayRequest fallback = Concurrent(withId, string.Empty, 1f);
            AssertEqual(GalStartResult.Started, dispatcher.Request(fallback),
                "An empty explicit key must fall back to SequenceId");
            AssertEqual(GalStartResult.DuplicateIgnored, dispatcher.Request(fallback),
                "The active SequenceId fallback must deduplicate");

            dispatcher.Tick(0.5f);
            AssertEqual(false, machine.IsActive,
                "The single-line fallback sequence must complete on time");
            AssertEqual(GalStartResult.DuplicateIgnored, dispatcher.Request(fallback),
                "The SequenceId fallback must remain cooling down");
            dispatcher.Tick(0.99f);
            AssertEqual(GalStartResult.DuplicateIgnored, dispatcher.Request(fallback),
                "Cooldown must remain active before its deadline");
            dispatcher.Tick(0.02f);
            AssertEqual(GalStartResult.Started, dispatcher.Request(fallback),
                "Cooldown must expire using accumulated unscaled ticks");
            dispatcher.Stop();

            GalPlayRequest explicitKey = Concurrent(withId, "explicit.other", 0f);
            AssertEqual(GalStartResult.Started, dispatcher.Request(explicitKey),
                "An explicit key must take priority over a cooling SequenceId");
            dispatcher.Stop();

            GalPlayRequest instanceFallback =
                Concurrent(withoutId, string.Empty, 0f);
            AssertEqual(GalStartResult.Started, dispatcher.Request(instanceFallback),
                "Missing author keys must fall back to the asset instance id");
            AssertEqual(
                GalStartResult.DuplicateIgnored,
                dispatcher.Request(instanceFallback),
                "The same asset instance fallback must deduplicate while active");
        }
        finally
        {
            dispatcher.Dispose();
            Destroy(withId, withoutId);
        }
    }

    private static void RejectsInvalidAndBusyRequests()
    {
        GalSequenceSO empty = ScriptableObject.CreateInstance<GalSequenceSO>();
        GalSequenceSO concurrent = CreateSequence("valid.concurrent", 2f, "C");
        GalSequenceSO fullscreen = CreateSequence("valid.fullscreen", 2f, "F");
        var events = new EventBus();
        var machine = CreateMachine(events, out _, out _);
        var dispatcher = new GalConcurrentDispatcher(machine, events, 2);

        try
        {
            AssertEqual(
                GalStartResult.InvalidSequence,
                dispatcher.Request(Concurrent(null, "null", 0f)),
                "A null sequence must be rejected before scheduling");
            AssertEqual(
                GalStartResult.InvalidSequence,
                dispatcher.Request(Concurrent(empty, "empty", 0f)),
                "An empty sequence must be rejected before scheduling");
            AssertEqual(0, dispatcher.QueuedCount,
                "Invalid requests must never enter the FIFO");

            AssertEqual(
                GalStartResult.Started,
                dispatcher.Request(new GalPlayRequest(
                    fullscreen,
                    GalPresentationMode.FullscreenExclusive,
                    "Probe:Fullscreen")),
                "A valid higher mode must start while idle");
            AssertEqual(
                GalStartResult.Busy,
                dispatcher.Request(Concurrent(concurrent, "busy", 0f)),
                "Concurrent content must not queue behind an active higher mode");
            AssertEqual(0, dispatcher.QueuedCount,
                "Busy Concurrent content must not become stale queued work");
        }
        finally
        {
            dispatcher.Dispose();
            Destroy(empty, concurrent, fullscreen);
        }
    }

    private static void PreemptsConcurrentBeforeStartingHigherMode()
    {
        GalSequenceSO current = CreateSequence("current", 5f, "A");
        GalSequenceSO queued = CreateSequence("queued", 5f, "B");
        GalSequenceSO fullscreen = CreateSequence("fullscreen", 5f, "F");
        var events = new EventBus();
        var machine = CreateMachine(events, out _, out _);
        var dispatcher = new GalConcurrentDispatcher(machine, events, 2);
        var order = new List<string>();
        GalConcurrentQueueClearedEvent? cleared = null;
        GalStopReason? stopped = null;
        events.Subscribe<GalConcurrentQueueClearedEvent>(e =>
        {
            cleared = e;
            order.Add("queue");
        });
        events.Subscribe<GalStoppedEvent>(e =>
        {
            stopped = e.Reason;
            order.Add("stopped");
        });

        try
        {
            AssertEqual(GalStartResult.Started,
                dispatcher.Request(Concurrent(current, "current", 1f)),
                "Concurrent content must start");
            AssertEqual(GalStartResult.Queued,
                dispatcher.Request(Concurrent(queued, "queued", 1f)),
                "The second Concurrent request must queue");

            GalStartResult result = dispatcher.Request(new GalPlayRequest(
                fullscreen,
                GalPresentationMode.FullscreenExclusive,
                "Probe:Fullscreen"));

            AssertEqual(GalStartResult.Started, result,
                "A higher mode must start immediately after preemption");
            AssertEqual("queue,stopped", string.Join(",", order),
                "Queued work must clear before the active session stops");
            AssertEqual(1, cleared.Value.RemovedCount,
                "The aggregate clear event must report actual removed work");
            AssertEqual(GalStopReason.Preempted, cleared.Value.Reason,
                "The clear event must preserve the preemption reason");
            AssertEqual(GalStopReason.Preempted, stopped.Value,
                "The active Concurrent session must stop as Preempted");
            AssertEqual(0, dispatcher.QueuedCount,
                "Preemption must leave no stale Concurrent requests");
            AssertSame(fullscreen, machine.ActiveRequest.Value.Sequence,
                "The higher-mode request must become active");
            AssertEqual(
                GalStartResult.Busy,
                dispatcher.Request(Concurrent(queued, "after.preempt", 0f)),
                "Concurrent content must remain Busy during the higher mode");
        }
        finally
        {
            dispatcher.Dispose();
            Destroy(current, queued, fullscreen);
        }
    }

    private static void ClearsQueueWhenDequeuedStartFails()
    {
        VerifyDequeuedFailureClearsQueue(
            makeUnavailable: (view, _) => view.IsReady = false,
            GalStopReason.ViewLost,
            "A lost View must clear failed and remaining queued work");
        VerifyDequeuedFailureClearsQueue(
            makeUnavailable: (_, suspension) => suspension.AllowAcquire = false,
            GalStopReason.Requested,
            "A rejected flow must clear failed and remaining queued work");
    }

    private static void VerifyDequeuedFailureClearsQueue(
        Action<FakeView, FakeSuspension> makeUnavailable,
        GalStopReason expectedReason,
        string message)
    {
        GalSequenceSO current = CreateSequence("failure.current", 0.5f, "A");
        GalSequenceSO queuedB = CreateSequence("failure.b", 5f, "B");
        GalSequenceSO queuedC = CreateSequence("failure.c", 5f, "C");
        var events = new EventBus();
        var machine = CreateMachine(events, out FakeView view, out FakeSuspension suspension);
        var dispatcher = new GalConcurrentDispatcher(machine, events, 2);
        int clearedCount = 0;
        int stoppedCount = 0;
        GalConcurrentQueueClearedEvent observed = default;
        events.Subscribe<GalConcurrentQueueClearedEvent>(e =>
        {
            clearedCount++;
            observed = e;
        });
        events.Subscribe<GalStoppedEvent>(_ => stoppedCount++);

        try
        {
            dispatcher.Request(Concurrent(current, "failure.current", 0f));
            dispatcher.Request(Concurrent(queuedB, "failure.b", 0f));
            dispatcher.Request(Concurrent(queuedC, "failure.c", 0f));
            makeUnavailable(view, suspension);

            dispatcher.Tick(0.5f);

            AssertEqual(1, clearedCount, message);
            AssertEqual(2, observed.RemovedCount,
                "The failed dequeued item and remaining item must be aggregated");
            AssertEqual(expectedReason, observed.Reason,
                "Dequeued failure must publish its defined clear reason");
            AssertEqual(0, dispatcher.QueuedCount,
                "A failed dequeue must leave no stale work");
            AssertEqual(false, machine.IsActive,
                "A failed dequeue must leave the state machine inactive");
            AssertEqual(0, stoppedCount,
                "Queued requests that never started must not publish Stopped");
        }
        finally
        {
            dispatcher.Dispose();
            Destroy(current, queuedB, queuedC);
        }
    }

    private static void StopAndDisposeAreIdempotent()
    {
        GalSequenceSO current = CreateSequence("stop.current", 5f, "A");
        GalSequenceSO queued = CreateSequence("stop.queued", 5f, "B");
        GalSequenceSO afterDispose = CreateSequence("after.dispose", 5f, "C");
        var events = new EventBus();
        var machine = CreateMachine(events, out _, out _);
        var dispatcher = new GalConcurrentDispatcher(machine, events, 2);
        int clearedCount = 0;
        int stoppedCount = 0;
        int progressCount = 0;
        GalConcurrentQueueClearedEvent lastClear = default;
        events.Subscribe<GalConcurrentQueueClearedEvent>(e =>
        {
            clearedCount++;
            lastClear = e;
        });
        events.Subscribe<GalStoppedEvent>(_ => stoppedCount++);
        dispatcher.ProgressChanged += _ => progressCount++;

        try
        {
            dispatcher.Request(Concurrent(current, "stop.current", 0f));
            dispatcher.Request(Concurrent(queued, "stop.queued", 0f));
            dispatcher.Stop(GalStopReason.Requested);
            dispatcher.Stop(GalStopReason.Requested);

            AssertEqual(1, clearedCount,
                "Only a Stop that removes queued work may publish QueueCleared");
            AssertEqual(1, lastClear.RemovedCount,
                "Stop must report the one removed queued request");
            AssertEqual(GalStopReason.Requested, lastClear.Reason,
                "Stop must preserve its queue clear reason");
            AssertEqual(1, stoppedCount,
                "Repeated Stop must stop the active session only once");
            AssertEqual(false, machine.IsActive,
                "Stop must leave the state machine inactive");

            dispatcher.Dispose();
            dispatcher.Dispose();
            AssertEqual(1, clearedCount,
                "Dispose with an empty queue must not publish QueueCleared");
            int progressAfterDispose = progressCount;
            AssertEqual(
                GalStartResult.Started,
                machine.TryStart(Concurrent(afterDispose, "direct", 0f)),
                "The real machine must remain usable for the unsubscribe probe");
            AssertEqual(progressAfterDispose, progressCount,
                "Dispose must unsubscribe from LinePresented");
            machine.Stop(GalStopReason.Destroyed);
        }
        finally
        {
            dispatcher.Dispose();
            machine.Stop(GalStopReason.Destroyed);
            Destroy(current, queued, afterDispose);
        }
    }

    private static void LargeTickAdvancesAtMostOneLine()
    {
        GalSequenceSO sequence = CreateSequence(
            "one.advance",
            new GalLine(null, "first", autoAdvanceSecondsOverride: 1f),
            new GalLine(null, "second", autoAdvanceSecondsOverride: 1f));
        var events = new EventBus();
        var machine = CreateMachine(events, out FakeView view, out _);
        var dispatcher = new GalConcurrentDispatcher(machine, events, 1);

        try
        {
            dispatcher.Request(Concurrent(sequence, "one.advance", 0f));
            dispatcher.Tick(100f);
            AssertEqual(2, view.Presented.Count,
                "One Tick may advance by only one line even with a huge delta");
            AssertEqual(true, machine.IsActive,
                "A huge Tick must not consume the newly presented line");
            AssertApproximately(0f, dispatcher.CurrentLineProgress,
                "The newly presented line must reset progress");

            dispatcher.Tick(100f);
            AssertEqual(false, machine.IsActive,
                "A later Tick may complete the final line");
        }
        finally
        {
            dispatcher.Dispose();
            Destroy(sequence);
        }
    }

    private static GalFlowStateMachine CreateMachine(
        EventBus events,
        out FakeView view,
        out FakeSuspension suspension)
    {
        view = new FakeView();
        suspension = new FakeSuspension();
        return new GalFlowStateMachine(view, suspension, events);
    }

    private static GalPlayRequest Concurrent(
        GalSequenceSO sequence,
        string key,
        float cooldown)
    {
        return new GalPlayRequest(
            sequence,
            GalPresentationMode.ConcurrentOverlay,
            "DispatcherProbe",
            key,
            cooldown);
    }

    private static GalSequenceSO CreateSequence(
        string sequenceId,
        float duration,
        string text)
    {
        return CreateSequence(
            sequenceId,
            new GalLine(
                null,
                text,
                autoAdvanceSecondsOverride: duration));
    }

    private static GalSequenceSO CreateSequence(
        string sequenceId,
        params GalLine[] lines)
    {
        GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
        SetPrivateField(sequence, "sequenceId", sequenceId);
        SetPrivateField(sequence, "lines", new List<GalLine>(lines));
        return sequence;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new Exception($"Missing test setup field: {fieldName}");
        field.SetValue(target, value);
    }

    private static void Destroy(params UnityEngine.Object[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
                UnityEngine.Object.DestroyImmediate(values[i]);
        }
    }

    private static bool ContainsApproximately(List<float> values, float expected)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (Math.Abs(values[i] - expected) <= 0.0001f)
                return true;
        }

        return false;
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertApproximately(
        float expected,
        float actual,
        string message)
    {
        if (float.IsNaN(actual) ||
            float.IsInfinity(actual) ||
            Math.Abs(expected - actual) > 0.0001f)
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private sealed class FakeView : IGalDialogueView
    {
        public bool IsReady { get; set; } = true;
        public List<GalLineViewModel> Presented { get; } = new();
        public event Action Confirmed;

        public void Show(GalPresentationMode mode)
        {
        }

        public void Hide()
        {
        }

        public void Present(GalLineViewModel line)
        {
            Presented.Add(line);
        }
    }

    private sealed class FakeSuspension : IGameplaySuspensionService
    {
        private bool leased;
        public bool AllowAcquire { get; set; } = true;

        public bool TryAcquire(
            GalPresentationMode mode,
            out IDisposable suspensionLease)
        {
            if (leased || !AllowAcquire)
            {
                suspensionLease = null;
                return false;
            }

            leased = true;
            suspensionLease = new ReleaseAction(() => leased = false);
            return true;
        }
    }

    private sealed class ReleaseAction : IDisposable
    {
        private Action release;

        public ReleaseAction(Action release)
        {
            this.release = release;
        }

        public void Dispose()
        {
            Action action = release;
            release = null;
            action?.Invoke();
        }
    }
}
#endif
