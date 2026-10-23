using System;

public enum GalFlowState
{
    Inactive,
    Opening,
    PresentingLine,
    Closing
}

public enum GalStartResult
{
    Started,
    Queued,
    DuplicateIgnored,
    QueueFull,
    Busy,
    InvalidSequence,
    ViewUnavailable,
    RejectedByFlow
}

public enum GalStopReason
{
    Completed,
    Requested,
    Preempted,
    ViewLost,
    Destroyed
}

public readonly struct GalPlayRequest
{
    public GalSequenceSO Sequence { get; }
    public GalPresentationMode PresentationMode { get; }
    public string Source { get; }
    public string RequestKey { get; }
    public float DuplicateCooldownSeconds { get; }

    public GalPlayRequest(
        GalSequenceSO sequence,
        GalPresentationMode presentationMode = GalPresentationMode.PausedOverlay,
        string source = "",
        string requestKey = "",
        float duplicateCooldownSeconds = 1f)
    {
        Sequence = sequence;
        PresentationMode = presentationMode;
        Source = source ?? string.Empty;
        RequestKey = requestKey ?? string.Empty;
        DuplicateCooldownSeconds = float.IsNaN(duplicateCooldownSeconds) ||
            float.IsInfinity(duplicateCooldownSeconds)
                ? 0f
                : Math.Max(0f, duplicateCooldownSeconds);
    }
}

public readonly struct GalConcurrentQueueClearedEvent
{
    public int RemovedCount { get; }
    public GalStopReason Reason { get; }

    public GalConcurrentQueueClearedEvent(
        int removedCount,
        GalStopReason reason)
    {
        RemovedCount = Math.Max(0, removedCount);
        Reason = reason;
    }
}

public readonly struct GalStartedEvent
{
    public GalSequenceSO Sequence { get; }
    public GalPresentationMode PresentationMode { get; }
    public string Source { get; }

    public GalStartedEvent(GalPlayRequest request)
    {
        Sequence = request.Sequence;
        PresentationMode = request.PresentationMode;
        Source = request.Source;
    }
}

public readonly struct GalLineChangedEvent
{
    public GalSequenceSO Sequence { get; }
    public GalLineViewModel Line { get; }

    public GalLineChangedEvent(
        GalSequenceSO sequence,
        GalLineViewModel line)
    {
        Sequence = sequence;
        Line = line;
    }
}

public readonly struct GalCompletedEvent
{
    public GalSequenceSO Sequence { get; }

    public GalCompletedEvent(GalSequenceSO sequence)
    {
        Sequence = sequence;
    }
}

public readonly struct GalStoppedEvent
{
    public GalSequenceSO Sequence { get; }
    public GalStopReason Reason { get; }

    public GalStoppedEvent(GalSequenceSO sequence, GalStopReason reason)
    {
        Sequence = sequence;
        Reason = reason;
    }
}

public interface IGalDialogueView
{
    bool IsReady { get; }
    event Action Confirmed;
    void Show(GalPresentationMode mode);
    void Hide();
    void Present(GalLineViewModel line);
}

public interface IGameplaySuspensionService
{
    bool TryAcquire(
        GalPresentationMode mode,
        out IDisposable suspensionLease);
}
