#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class GalFlowLifecycleProbe
{
    public static string Run()
    {
        RejectsInvalidRequestsWithoutSuspending();
        NormalizesInvalidDuplicateCooldowns();
        PlaysToCompletionAndReleasesExactlyOnce();
        PassesFullscreenExclusiveToViewAndSuspension();
        ExposesConcurrentSessionAndIgnoresConfirm();
        StopIsIdempotentAndPublishesStoppedOnlyOnce();
        PreemptedStopIsIdempotentAndPublishesStoppedOnlyOnce();
        QueueClearedEventClampsInvalidCounts();
        return "PASS gal flow lifecycle";
    }

    private static void RejectsInvalidRequestsWithoutSuspending()
    {
        var view = new FakeView();
        var suspension = new FakeSuspensionService();
        var machine = new GalFlowStateMachine(view, suspension, new EventBus());

        AssertEqual(
            GalStartResult.InvalidSequence,
            machine.TryStart(new GalPlayRequest(null)),
            "Null content must be rejected explicitly");
        AssertEqual(0, suspension.AcquireCount, "Invalid content must not pause Gameplay");
        AssertEqual(false, machine.ActiveRequest.HasValue,
            "Invalid content must not leave an active request");

        GalSequenceSO sequence = CreateSequence("唯一一句");
        try
        {
            view.IsReady = false;
            AssertEqual(
                GalStartResult.ViewUnavailable,
                machine.TryStart(new GalPlayRequest(sequence)),
                "An unavailable View must reject the request");
            AssertEqual(0, suspension.AcquireCount, "Missing View must not pause Gameplay");
            AssertEqual(false, machine.ActiveRequest.HasValue,
                "Missing View must not leave an active request");

            view.IsReady = true;
            suspension.AllowAcquire = false;
            AssertEqual(
                GalStartResult.RejectedByFlow,
                machine.TryStart(new GalPlayRequest(sequence)),
                "A rejected suspension request must not start Gal");
            AssertEqual(GalFlowState.Inactive, machine.State, "Rejected flow must stay inactive");
            AssertEqual(false, machine.ActiveRequest.HasValue,
                "Rejected flow must not leave an active request");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void NormalizesInvalidDuplicateCooldowns()
    {
        AssertApproximately(
            0f,
            new GalPlayRequest(null, duplicateCooldownSeconds: -2f)
                .DuplicateCooldownSeconds,
            "Negative cooldown must clamp to zero");
        AssertApproximately(
            0f,
            new GalPlayRequest(null, duplicateCooldownSeconds: float.NaN)
                .DuplicateCooldownSeconds,
            "NaN cooldown must normalize to zero");
        AssertApproximately(
            0f,
            new GalPlayRequest(
                null,
                duplicateCooldownSeconds: float.PositiveInfinity)
                .DuplicateCooldownSeconds,
            "Positive infinity cooldown must normalize to zero");
        AssertApproximately(
            0f,
            new GalPlayRequest(
                null,
                duplicateCooldownSeconds: float.NegativeInfinity)
                .DuplicateCooldownSeconds,
            "Negative infinity cooldown must normalize to zero");
    }

    private static void PlaysToCompletionAndReleasesExactlyOnce()
    {
        GalSequenceSO sequence = CreateSequence("第一句", "第二句");
        var view = new FakeView();
        var suspension = new FakeSuspensionService();
        var events = new EventBus();
        var machine = new GalFlowStateMachine(view, suspension, events);
        int startedCount = 0;
        int changedCount = 0;
        int completedCount = 0;
        int stoppedCount = 0;

        events.Subscribe<GalStartedEvent>(_ => startedCount++);
        events.Subscribe<GalLineChangedEvent>(_ => changedCount++);
        events.Subscribe<GalCompletedEvent>(_ => completedCount++);
        events.Subscribe<GalStoppedEvent>(_ => stoppedCount++);

        try
        {
            AssertEqual(
                GalStartResult.Started,
                machine.TryStart(
                    new GalPlayRequest(
                        sequence,
                        GalPresentationMode.PausedOverlay,
                        "Probe")),
                "A valid request must start");
            AssertEqual(GalFlowState.PresentingLine, machine.State, "Start must present the first line");
            AssertEqual(1, suspension.AcquireCount, "Gameplay suspension must be acquired once");
            AssertEqual(
                GalPresentationMode.PausedOverlay,
                suspension.LastRequestedMode,
                "PausedOverlay must be forwarded to Gameplay suspension");
            AssertEqual(1, view.ShowCount, "The View must open once");
            AssertEqual(
                GalPresentationMode.PausedOverlay,
                view.LastShownMode,
                "PausedOverlay must be forwarded to the View");
            AssertEqual(1, view.Presented.Count, "The first line must be presented immediately");
            AssertEqual("第一句", view.Presented[0].Text, "The first line must be correct");
            AssertEqual(1, startedCount, "Started must be published once");
            AssertEqual(1, changedCount, "The first line must publish LineChanged");

            AssertEqual(
                GalStartResult.Busy,
                machine.TryStart(new GalPlayRequest(sequence)),
                "An active session must reject replacement");
            AssertEqual(1, suspension.AcquireCount, "Busy requests must not acquire another pause");

            view.RaiseConfirmed();
            AssertEqual(2, view.Presented.Count, "One confirmation must advance one line");
            AssertEqual("第二句", view.Presented[1].Text, "The second line must be correct");
            AssertEqual(2, changedCount, "Each presented line must publish LineChanged");

            view.RaiseConfirmed();
            AssertEqual(GalFlowState.Inactive, machine.State, "Final confirmation must close the session");
            AssertEqual(1, view.HideCount, "Natural completion must hide once");
            AssertEqual(1, suspension.ReleaseCount, "Natural completion must release once");
            AssertEqual(1, completedCount, "Natural completion must publish Completed once");
            AssertEqual(0, stoppedCount, "Natural completion must not also publish Stopped");
        }
        finally
        {
            machine.Dispose();
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void PassesFullscreenExclusiveToViewAndSuspension()
    {
        GalSequenceSO sequence = CreateSequence("全屏测试");
        var view = new FakeView();
        var suspension = new FakeSuspensionService();
        var machine = new GalFlowStateMachine(view, suspension, new EventBus());

        try
        {
            AssertEqual(
                GalStartResult.Started,
                machine.TryStart(new GalPlayRequest(
                    sequence,
                    GalPresentationMode.FullscreenExclusive,
                    "FullscreenProbe")),
                "FullscreenExclusive must start");
            AssertEqual(
                GalPresentationMode.FullscreenExclusive,
                view.LastShownMode,
                "View must receive the requested mode");
            AssertEqual(
                GalPresentationMode.FullscreenExclusive,
                suspension.LastRequestedMode,
                "Suspension must receive the requested mode");

            view.RaiseConfirmed();
            AssertEqual(
                GalFlowState.Inactive,
                machine.State,
                "Fullscreen confirmation must complete the single-line sequence");
            machine.Stop(GalStopReason.Requested);
            machine.Dispose();
            AssertEqual(1, suspension.ReleaseCount,
                "Fullscreen suspension must release exactly once");
            AssertEqual(1, view.HideCount,
                "Fullscreen view must hide exactly once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void ExposesConcurrentSessionAndIgnoresConfirm()
    {
        GalSequenceSO sequence = CreateSequence("并行第一句", "并行第二句");
        var view = new FakeView();
        var suspension = new FakeSuspensionService();
        var events = new EventBus();
        var machine = new GalFlowStateMachine(
            view,
            suspension,
            events);
        var request = new GalPlayRequest(
            sequence,
            GalPresentationMode.ConcurrentOverlay,
            "Probe",
            "probe.concurrent",
            1.25f);
        int linePresentedCount = 0;
        GalLineViewModel lastPresented = default;
        var notificationOrder = new List<string>();
        view.OnPresent = _ => notificationOrder.Add("view");
        events.Subscribe<GalLineChangedEvent>(_ => notificationOrder.Add("bus"));
        machine.LinePresented += line =>
        {
            notificationOrder.Add("local");
            linePresentedCount++;
            lastPresented = line;
        };

        try
        {
            AssertEqual(
                "probe.concurrent",
                request.RequestKey,
                "Concurrent request key must be preserved");
            AssertApproximately(
                1.25f,
                request.DuplicateCooldownSeconds,
                "Concurrent cooldown must be preserved");
            AssertEqual(
                GalStartResult.Started,
                machine.TryStart(request),
                "A valid Concurrent request must start");
            AssertEqual(
                GalPresentationMode.ConcurrentOverlay,
                machine.ActiveRequest.Value.PresentationMode,
                "The active request must expose the runtime mode");
            AssertEqual(
                "probe.concurrent",
                machine.ActiveRequest.Value.RequestKey,
                "The active request must preserve its deduplication key");
            AssertEqual(
                1,
                linePresentedCount,
                "Starting must publish the first local line notification");
            AssertEqual(
                "并行第一句",
                lastPresented.Text,
                "The first local line notification must carry the presented line");
            AssertEqual(
                "view,bus,local",
                string.Join(",", notificationOrder),
                "Line notifications must follow View, EventBus, local event order");

            int presentCountBeforeConfirm = view.PresentCount;
            view.RaiseConfirmed();
            AssertEqual(
                presentCountBeforeConfirm,
                view.PresentCount,
                "Concurrent mode must ignore confirm input");
            AssertEqual(
                1,
                linePresentedCount,
                "Ignored confirm input must not publish another line");

            notificationOrder.Clear();
            machine.Advance();
            AssertEqual(
                2,
                view.PresentCount,
                "Explicit automatic advancement must present the next line");
            AssertEqual(
                2,
                linePresentedCount,
                "Every presented line must publish the local notification");
            AssertEqual(
                "view,bus,local",
                string.Join(",", notificationOrder),
                "Advanced line notifications must preserve ordering");

            machine.Advance();
            AssertEqual(
                GalFlowState.Inactive,
                machine.State,
                "Concurrent completion must close the session");
            AssertEqual(
                false,
                machine.ActiveRequest.HasValue,
                "Closing must clear the active request");
        }
        finally
        {
            machine.Dispose();
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void StopIsIdempotentAndPublishesStoppedOnlyOnce()
    {
        GalSequenceSO sequence = CreateSequence("不会自然结束");
        var view = new FakeView();
        var suspension = new FakeSuspensionService();
        var events = new EventBus();
        var machine = new GalFlowStateMachine(view, suspension, events);
        int stoppedCount = 0;
        GalStopReason observedReason = GalStopReason.Completed;

        events.Subscribe<GalStoppedEvent>(e =>
        {
            stoppedCount++;
            observedReason = e.Reason;
        });

        try
        {
            machine.TryStart(new GalPlayRequest(sequence));
            machine.Stop(GalStopReason.Requested);
            machine.Stop(GalStopReason.Requested);
            machine.Dispose();

            AssertEqual(GalFlowState.Inactive, machine.State, "Stop must return to Inactive");
            AssertEqual(1, suspension.ReleaseCount, "Repeated Stop must release only once");
            AssertEqual(1, view.HideCount, "Repeated Stop must hide only once");
            AssertEqual(1, stoppedCount, "Repeated Stop must publish only once");
            AssertEqual(GalStopReason.Requested, observedReason, "Stop reason must be preserved");
            AssertEqual(false, machine.ActiveRequest.HasValue,
                "Requested stop must clear the active request");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void PreemptedStopIsIdempotentAndPublishesStoppedOnlyOnce()
    {
        GalSequenceSO sequence = CreateSequence("将被高级模式抢占");
        var view = new FakeView();
        var suspension = new FakeSuspensionService();
        var events = new EventBus();
        var machine = new GalFlowStateMachine(view, suspension, events);
        int stoppedCount = 0;
        GalStopReason observedReason = GalStopReason.Completed;

        events.Subscribe<GalStoppedEvent>(e =>
        {
            stoppedCount++;
            observedReason = e.Reason;
        });

        try
        {
            machine.TryStart(new GalPlayRequest(
                sequence,
                GalPresentationMode.ConcurrentOverlay,
                requestKey: "probe.preempted"));
            machine.Stop(GalStopReason.Preempted);
            machine.Stop(GalStopReason.Preempted);
            machine.Dispose();

            AssertEqual(
                GalFlowState.Inactive,
                machine.State,
                "Preemption must return the session to Inactive");
            AssertEqual(
                false,
                machine.ActiveRequest.HasValue,
                "Preemption must clear the active request");
            AssertEqual(
                1,
                suspension.ReleaseCount,
                "Repeated preemption must release only once");
            AssertEqual(
                1,
                view.HideCount,
                "Repeated preemption must hide only once");
            AssertEqual(
                1,
                stoppedCount,
                "Repeated preemption must publish Stopped only once");
            AssertEqual(
                GalStopReason.Preempted,
                observedReason,
                "Stopped must preserve the Preempted reason");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void QueueClearedEventClampsInvalidCounts()
    {
        var invalid = new GalConcurrentQueueClearedEvent(
            -4,
            GalStopReason.Preempted);
        var valid = new GalConcurrentQueueClearedEvent(
            3,
            GalStopReason.Requested);

        AssertEqual(
            0,
            invalid.RemovedCount,
            "A negative removed count must clamp to zero");
        AssertEqual(
            GalStopReason.Preempted,
            invalid.Reason,
            "Queue clear reason must be preserved");
        AssertEqual(
            3,
            valid.RemovedCount,
            "A valid removed count must be preserved");
        AssertEqual(
            GalStopReason.Requested,
            valid.Reason,
            "A valid queue clear reason must be preserved");
    }

    private static GalSequenceSO CreateSequence(params string[] texts)
    {
        GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
        var lines = new List<GalLine>();
        for (int i = 0; i < texts.Length; i++)
            lines.Add(new GalLine(null, texts[i]));

        SetPrivateField(sequence, "lines", lines);
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

    private sealed class FakeView : IGalDialogueView
    {
        public bool IsReady { get; set; } = true;
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public GalPresentationMode LastShownMode { get; private set; }
        public List<GalLineViewModel> Presented { get; } = new();
        public int PresentCount => Presented.Count;
        public Action<GalLineViewModel> OnPresent { get; set; }

        public event Action Confirmed;

        public void Show(GalPresentationMode mode)
        {
            ShowCount++;
            LastShownMode = mode;
        }

        public void Hide()
        {
            HideCount++;
        }

        public void Present(GalLineViewModel line)
        {
            Presented.Add(line);
            OnPresent?.Invoke(line);
        }

        public void RaiseConfirmed()
        {
            Confirmed?.Invoke();
        }
    }

    private sealed class FakeSuspensionService : IGameplaySuspensionService
    {
        public bool AllowAcquire { get; set; } = true;
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public GalPresentationMode LastRequestedMode { get; private set; }

        public bool TryAcquire(
            GalPresentationMode mode,
            out IDisposable suspensionLease)
        {
            LastRequestedMode = mode;

            if (!AllowAcquire)
            {
                suspensionLease = null;
                return false;
            }

            AcquireCount++;
            suspensionLease = new ReleaseAction(() => ReleaseCount++);
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

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
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
}
#endif
