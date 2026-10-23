using System;
using System.Collections.Generic;

public sealed class BattleSpineEventBridge :
    IBattleSpineMarkerGateway,
    IDisposable
{
    private readonly Func<int, IBattleSpineEventSource> sourceResolver;
    private readonly BattlePresentationTraceBuffer trace;
    private readonly Func<float> realtimeProvider;
    private readonly List<PendingWait> pending = new List<PendingWait>();
    private readonly Dictionary<int, SourceBinding> bindings =
        new Dictionary<int, SourceBinding>();
    private readonly HashSet<MarkerIdentity> settledMarkers =
        new HashSet<MarkerIdentity>();
    private bool disposed;

    public BattleSpineEventBridge(
        Func<int, IBattleSpineEventSource> sourceResolver,
        BattlePresentationTraceBuffer trace,
        Func<float> realtimeProvider)
    {
        this.sourceResolver = sourceResolver ??
            throw new ArgumentNullException(nameof(sourceResolver));
        this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        this.realtimeProvider = realtimeProvider ??
            throw new ArgumentNullException(nameof(realtimeProvider));
    }

    public IBattlePresentationHandle WaitFor(
        BattlePresentationScope scope,
        int actorRuntimeId,
        BattleSpineMarker expected,
        float timeoutSeconds,
        Action onMatched,
        Action onTimedOut)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(BattleSpineEventBridge));
        if (actorRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorRuntimeId));
        if (string.IsNullOrWhiteSpace(expected.Name))
            throw new ArgumentException("Expected marker name is required.", nameof(expected));

        PendingWait record = null;
        var handle = new BattleSpineMarkerWaitHandle(reason =>
            Cancel(record, reason));
        record = new PendingWait(
            scope,
            actorRuntimeId,
            expected,
            realtimeProvider() + Math.Max(0f, timeoutSeconds),
            onMatched,
            onTimedOut,
            handle);

        if (!EnsureBinding(actorRuntimeId))
        {
            trace.Record(Describe(record) + " source missing; watchdog fallback");
            InvokeFallbackAndSettle(record);
            return handle;
        }

        pending.Add(record);
        trace.Record(Describe(record) + " marker wait registered");
        if (timeoutSeconds <= 0f)
            Tick();
        return handle;
    }

    public void Tick()
    {
        if (disposed || pending.Count == 0)
            return;
        float now = realtimeProvider();
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            PendingWait record = pending[i];
            if (record.Handle.IsSettled)
            {
                pending.RemoveAt(i);
                continue;
            }
            if (now < record.Deadline)
                continue;

            pending.RemoveAt(i);
            settledMarkers.Add(new MarkerIdentity(
                record.ActorRuntimeId,
                record.Expected));
            trace.Record(Describe(record) + " timed out");
            InvokeFallbackAndSettle(record);
        }
    }

    public void CancelScope(
        BattlePresentationScope scope,
        BattlePresentationCancelReason reason)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].Scope == scope)
                Cancel(pending[i], reason);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            PendingWait record = pending[i];
            record.Handle.TrySettle(BattlePresentationOutcome.Cancelled);
        }
        pending.Clear();
        foreach (KeyValuePair<int, SourceBinding> pair in bindings)
            pair.Value.Subscription?.Dispose();
        bindings.Clear();
        settledMarkers.Clear();
    }

    private bool EnsureBinding(int actorRuntimeId)
    {
        if (bindings.ContainsKey(actorRuntimeId))
            return true;
        IBattleSpineEventSource source = sourceResolver(actorRuntimeId);
        if (source == null)
            return false;
        IDisposable subscription = source.Subscribe(marker =>
            OnMarker(actorRuntimeId, marker));
        if (subscription == null)
            return false;
        bindings.Add(actorRuntimeId, new SourceBinding(source, subscription));
        return true;
    }

    private void OnMarker(int actorRuntimeId, BattleSpineMarker marker)
    {
        if (disposed)
            return;
        var identity = new MarkerIdentity(actorRuntimeId, marker);
        bool matchedAny = false;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            PendingWait record = pending[i];
            if (record.ActorRuntimeId != actorRuntimeId ||
                !record.Expected.Equals(marker))
            {
                continue;
            }

            pending.RemoveAt(i);
            matchedAny = true;
            settledMarkers.Add(identity);
            trace.Record(Describe(record) + " marker matched");
            try
            {
                record.OnMatched?.Invoke();
                record.Handle.TrySettle(BattlePresentationOutcome.Completed);
            }
            catch (Exception exception)
            {
                trace.Record(Describe(record) + " marker callback faulted: " + exception.Message);
                record.Handle.TrySettle(BattlePresentationOutcome.Faulted, exception);
            }
        }

        if (!matchedAny && settledMarkers.Contains(identity))
        {
            trace.Record(
                "Spine marker duplicate/late rejected for actor " +
                actorRuntimeId + ": " + marker);
        }
    }

    private void Cancel(PendingWait record, BattlePresentationCancelReason reason)
    {
        if (record == null || record.Handle.IsSettled)
            return;
        pending.Remove(record);
        settledMarkers.Add(new MarkerIdentity(
            record.ActorRuntimeId,
            record.Expected));
        trace.Record(Describe(record) + " cancelled: " + reason);
        record.Handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
    }

    private static void InvokeFallbackAndSettle(PendingWait record)
    {
        try
        {
            record.OnTimedOut?.Invoke();
            record.Handle.TrySettle(BattlePresentationOutcome.TimedOut);
        }
        catch (Exception exception)
        {
            record.Handle.TrySettle(BattlePresentationOutcome.Faulted, exception);
        }
    }

    private static string Describe(PendingWait record)
    {
        return record.Scope + " / Actor " + record.ActorRuntimeId +
            " / " + record.Expected;
    }

    private sealed class PendingWait
    {
        public PendingWait(
            BattlePresentationScope scope,
            int actorRuntimeId,
            BattleSpineMarker expected,
            float deadline,
            Action onMatched,
            Action onTimedOut,
            BattleSpineMarkerWaitHandle handle)
        {
            Scope = scope;
            ActorRuntimeId = actorRuntimeId;
            Expected = expected;
            Deadline = deadline;
            OnMatched = onMatched;
            OnTimedOut = onTimedOut;
            Handle = handle;
        }

        public BattlePresentationScope Scope { get; }
        public int ActorRuntimeId { get; }
        public BattleSpineMarker Expected { get; }
        public float Deadline { get; }
        public Action OnMatched { get; }
        public Action OnTimedOut { get; }
        public BattleSpineMarkerWaitHandle Handle { get; }
    }

    private sealed class SourceBinding
    {
        public SourceBinding(IBattleSpineEventSource source, IDisposable subscription)
        {
            Source = source;
            Subscription = subscription;
        }
        public IBattleSpineEventSource Source { get; }
        public IDisposable Subscription { get; }
    }

    private readonly struct MarkerIdentity : IEquatable<MarkerIdentity>
    {
        public MarkerIdentity(int actorRuntimeId, BattleSpineMarker marker)
        {
            ActorRuntimeId = actorRuntimeId;
            Marker = marker;
        }
        public int ActorRuntimeId { get; }
        public BattleSpineMarker Marker { get; }
        public bool Equals(MarkerIdentity other) =>
            ActorRuntimeId == other.ActorRuntimeId && Marker.Equals(other.Marker);
        public override bool Equals(object obj) =>
            obj is MarkerIdentity other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return (ActorRuntimeId * 397) ^ Marker.GetHashCode(); }
        }
    }
}
