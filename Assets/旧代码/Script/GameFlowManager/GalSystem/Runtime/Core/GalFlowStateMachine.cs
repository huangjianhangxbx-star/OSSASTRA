using System;

public sealed class GalFlowStateMachine : IDisposable
{
    private readonly IGalDialogueView view;
    private readonly IGameplaySuspensionService suspensionService;
    private readonly EventBus galEvents;
    private readonly GalSequencePlayer sequencePlayer = new();

    private IDisposable suspensionLease;
    private GalSequenceSO activeSequence;
    private bool confirmSubscribed;

    public GalFlowState State { get; private set; } = GalFlowState.Inactive;
    public bool IsActive => State != GalFlowState.Inactive;
    public GalPlayRequest? ActiveRequest { get; private set; }

    public event Action<GalLineViewModel> LinePresented;

    public GalFlowStateMachine(
        IGalDialogueView view,
        IGameplaySuspensionService suspensionService,
        EventBus galEvents)
    {
        this.view = view;
        this.suspensionService = suspensionService;
        this.galEvents = galEvents ?? new EventBus();
    }

    public GalStartResult TryStart(GalPlayRequest request)
    {
        if (IsActive)
            return GalStartResult.Busy;

        if (request.Sequence == null || !request.Sequence.HasLines)
            return GalStartResult.InvalidSequence;

        if (view == null || !view.IsReady)
            return GalStartResult.ViewUnavailable;

        if (suspensionService == null ||
            !suspensionService.TryAcquire(
                request.PresentationMode,
                out IDisposable lease) ||
            lease == null)
        {
            return GalStartResult.RejectedByFlow;
        }

        State = GalFlowState.Opening;
        activeSequence = request.Sequence;
        ActiveRequest = request;
        suspensionLease = lease;

        if (!sequencePlayer.TryBegin(activeSequence, out GalLineViewModel firstLine))
        {
            Close(GalStopReason.Requested, publishEvent: false);
            return GalStartResult.InvalidSequence;
        }

        if (request.PresentationMode != GalPresentationMode.ConcurrentOverlay)
            SubscribeConfirm();

        view.Show(request.PresentationMode);
        State = GalFlowState.PresentingLine;
        galEvents.Publish(new GalStartedEvent(request));
        Present(firstLine);
        return GalStartResult.Started;
    }

    public void Advance()
    {
        if (State != GalFlowState.PresentingLine)
            return;

        if (sequencePlayer.TryAdvance(out GalLineViewModel nextLine))
        {
            Present(nextLine);
            return;
        }

        Close(GalStopReason.Completed, publishEvent: true);
    }

    public void Stop(GalStopReason reason = GalStopReason.Requested)
    {
        if (!IsActive)
            return;

        Close(reason, publishEvent: true);
    }

    public void Dispose()
    {
        if (IsActive)
            Close(GalStopReason.Destroyed, publishEvent: true);
        else
            UnsubscribeConfirm();
    }

    private void Present(GalLineViewModel line)
    {
        view.Present(line);
        galEvents.Publish(new GalLineChangedEvent(activeSequence, line));
        LinePresented?.Invoke(line);
    }

    private void Close(GalStopReason reason, bool publishEvent)
    {
        if (!IsActive)
            return;

        State = GalFlowState.Closing;
        GalPlayRequest? closingRequest = ActiveRequest;
        GalSequenceSO closingSequence = closingRequest.HasValue
            ? closingRequest.Value.Sequence
            : activeSequence;
        UnsubscribeConfirm();
        sequencePlayer.Clear();
        view?.Hide();

        IDisposable lease = suspensionLease;
        suspensionLease = null;
        lease?.Dispose();

        activeSequence = null;
        ActiveRequest = null;
        State = GalFlowState.Inactive;

        if (!publishEvent)
            return;

        if (reason == GalStopReason.Completed)
            galEvents.Publish(new GalCompletedEvent(closingSequence));
        else
            galEvents.Publish(new GalStoppedEvent(closingSequence, reason));
    }

    private void SubscribeConfirm()
    {
        if (confirmSubscribed)
            return;

        view.Confirmed += Advance;
        confirmSubscribed = true;
    }

    private void UnsubscribeConfirm()
    {
        if (!confirmSubscribed || view == null)
            return;

        view.Confirmed -= Advance;
        confirmSubscribed = false;
    }
}
