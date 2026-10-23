using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GalConcurrentDispatcher : IDisposable
{
    private readonly GalFlowStateMachine stateMachine;
    private readonly EventBus galEvents;
    private readonly int maxQueuedRequests;
    private readonly Queue<GalPlayRequest> queuedRequests = new();
    private readonly HashSet<string> queuedKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> cooldownEnds =
        new(StringComparer.Ordinal);

    private GalPlayRequest? activeConcurrentRequest;
    private string activeKey = string.Empty;
    private float unscaledClock;
    private float currentLineElapsed;
    private float currentLineDuration;
    private float currentLineProgress;
    private bool disposed;

    public int QueuedCount => queuedRequests.Count;
    public float CurrentLineProgress => currentLineProgress;

    public event Action<float> ProgressChanged;

    public GalConcurrentDispatcher(
        GalFlowStateMachine stateMachine,
        EventBus galEvents,
        int maxQueuedRequests)
    {
        this.stateMachine = stateMachine ??
            throw new ArgumentNullException(nameof(stateMachine));
        this.galEvents = galEvents ?? new EventBus();
        this.maxQueuedRequests = Mathf.Max(1, maxQueuedRequests);
        this.stateMachine.LinePresented += OnLinePresented;
    }

    public GalStartResult Request(GalPlayRequest request)
    {
        if (disposed)
            return GalStartResult.RejectedByFlow;

        if (request.Sequence == null || !request.Sequence.HasLines)
            return GalStartResult.InvalidSequence;

        if (request.PresentationMode == GalPresentationMode.ConcurrentOverlay)
            return RequestConcurrent(request);

        if (IsActiveConcurrent())
        {
            ClearQueue(GalStopReason.Preempted);
            StopActiveConcurrent(GalStopReason.Preempted);
        }

        return stateMachine.TryStart(request);
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (disposed || unscaledDeltaTime <= 0f ||
            float.IsNaN(unscaledDeltaTime) ||
            float.IsInfinity(unscaledDeltaTime))
        {
            return;
        }

        unscaledClock += unscaledDeltaTime;

        if (!IsActiveConcurrent() || currentLineDuration <= 0f)
            return;

        currentLineElapsed += unscaledDeltaTime;
        SetProgress(currentLineElapsed / currentLineDuration);
        if (currentLineProgress < 1f)
            return;

        stateMachine.Advance();
        if (IsActiveConcurrent())
            return;

        CompleteActiveConcurrent();
        StartNextQueued();
    }

    public void Stop(GalStopReason reason = GalStopReason.Requested)
    {
        if (disposed)
            return;

        ClearQueue(reason);
        if (IsActiveConcurrent())
        {
            StopActiveConcurrent(reason);
            return;
        }

        stateMachine.Stop(reason);
        ResetLineTiming();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        Stop(GalStopReason.Destroyed);
        stateMachine.LinePresented -= OnLinePresented;
        stateMachine.Dispose();
        disposed = true;
    }

    private GalStartResult RequestConcurrent(GalPlayRequest request)
    {
        if (stateMachine.IsActive && !IsActiveConcurrent())
            return GalStartResult.Busy;

        string key = ResolveKey(request);
        if (IsDuplicate(key))
            return GalStartResult.DuplicateIgnored;

        if (!stateMachine.IsActive)
            return StartConcurrent(request, key);

        if (queuedRequests.Count >= maxQueuedRequests)
            return GalStartResult.QueueFull;

        queuedRequests.Enqueue(request);
        queuedKeys.Add(key);
        return GalStartResult.Queued;
    }

    private GalStartResult StartConcurrent(GalPlayRequest request, string key)
    {
        activeConcurrentRequest = request;
        activeKey = key;
        GalStartResult result = stateMachine.TryStart(request);
        if (result == GalStartResult.Started)
            return result;

        activeConcurrentRequest = null;
        activeKey = string.Empty;
        ResetLineTiming();
        return result;
    }

    private void StartNextQueued()
    {
        while (!disposed && !stateMachine.IsActive && queuedRequests.Count > 0)
        {
            GalPlayRequest next = queuedRequests.Dequeue();
            string key = ResolveKey(next);
            queuedKeys.Remove(key);
            GalStartResult result = StartConcurrent(next, key);
            if (result == GalStartResult.Started)
                return;

            ClearFailedStartAndQueue(result);
            return;
        }
    }

    private void ClearFailedStartAndQueue(GalStartResult result)
    {
        int removedCount = 1 + queuedRequests.Count;
        queuedRequests.Clear();
        queuedKeys.Clear();
        GalStopReason reason = result == GalStartResult.ViewUnavailable
            ? GalStopReason.ViewLost
            : GalStopReason.Requested;
        galEvents.Publish(new GalConcurrentQueueClearedEvent(
            removedCount,
            reason));
    }

    private void StopActiveConcurrent(GalStopReason reason)
    {
        GalPlayRequest? closingRequest = activeConcurrentRequest;
        string closingKey = activeKey;
        stateMachine.Stop(reason);
        RegisterCooldown(closingRequest, closingKey);
        activeConcurrentRequest = null;
        activeKey = string.Empty;
        ResetLineTiming();
    }

    private void CompleteActiveConcurrent()
    {
        RegisterCooldown(activeConcurrentRequest, activeKey);
        activeConcurrentRequest = null;
        activeKey = string.Empty;
        ResetLineTiming();
    }

    private void OnLinePresented(GalLineViewModel line)
    {
        currentLineElapsed = 0f;
        currentLineDuration = GalAutoAdvanceTiming.Calculate(
            line.Text,
            line.AutoAdvanceSecondsOverride);
        SetProgress(0f, forceNotify: true);
    }

    private void RegisterCooldown(
        GalPlayRequest? request,
        string key)
    {
        if (!request.HasValue || string.IsNullOrEmpty(key))
            return;

        float seconds = request.Value.DuplicateCooldownSeconds;
        if (seconds <= 0f)
        {
            cooldownEnds.Remove(key);
            return;
        }

        cooldownEnds[key] = unscaledClock + seconds;
    }

    private bool IsDuplicate(string key)
    {
        if (string.Equals(activeKey, key, StringComparison.Ordinal) ||
            queuedKeys.Contains(key))
        {
            return true;
        }

        if (!cooldownEnds.TryGetValue(key, out float cooldownEnd))
            return false;

        if (cooldownEnd > unscaledClock)
            return true;

        cooldownEnds.Remove(key);
        return false;
    }

    private bool IsActiveConcurrent()
    {
        return stateMachine.ActiveRequest.HasValue &&
            stateMachine.ActiveRequest.Value.PresentationMode ==
            GalPresentationMode.ConcurrentOverlay;
    }

    private void ClearQueue(GalStopReason reason)
    {
        int removedCount = queuedRequests.Count;
        if (removedCount == 0)
            return;

        queuedRequests.Clear();
        queuedKeys.Clear();
        galEvents.Publish(new GalConcurrentQueueClearedEvent(
            removedCount,
            reason));
    }

    private void ResetLineTiming()
    {
        currentLineElapsed = 0f;
        currentLineDuration = 0f;
        SetProgress(0f);
    }

    private void SetProgress(float value, bool forceNotify = false)
    {
        float clamped = Mathf.Clamp01(value);
        if (!forceNotify && Mathf.Approximately(currentLineProgress, clamped))
            return;

        currentLineProgress = clamped;
        ProgressChanged?.Invoke(currentLineProgress);
    }

    private static string ResolveKey(GalPlayRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RequestKey))
            return request.RequestKey.Trim();

        string sequenceId = request.Sequence != null
            ? request.Sequence.SequenceId
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(sequenceId))
            return sequenceId.Trim();

        return request.Sequence.GetInstanceID().ToString();
    }
}
