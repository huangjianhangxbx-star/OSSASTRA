using System;
using System.Collections.Generic;

public sealed class BattlePresentationHandle : IBattlePresentationHandle
{
    private readonly List<Action<BattlePresentationOutcome>> callbacks =
        new List<Action<BattlePresentationOutcome>>();
    private readonly Action<BattlePresentationCancelReason> cancelRequested;

    public BattlePresentationHandle()
    {
    }

    public BattlePresentationHandle(
        Action<BattlePresentationCancelReason> cancelRequested)
    {
        this.cancelRequested = cancelRequested;
    }

    public bool IsSettled { get; private set; }
    public BattlePresentationOutcome Outcome { get; private set; } =
        BattlePresentationOutcome.Pending;
    public Exception Fault { get; private set; }

    public bool TrySettle(
        BattlePresentationOutcome outcome,
        Exception fault = null)
    {
        if (outcome == BattlePresentationOutcome.Pending)
        {
            throw new ArgumentException(
                "Pending is not a terminal presentation outcome.",
                nameof(outcome));
        }

        if (IsSettled)
            return false;

        IsSettled = true;
        Outcome = outcome;
        Fault = fault;

        Action<BattlePresentationOutcome>[] pendingCallbacks =
            callbacks.ToArray();
        callbacks.Clear();
        for (int i = 0; i < pendingCallbacks.Length; i++)
            pendingCallbacks[i](outcome);

        return true;
    }

    public void WhenSettled(Action<BattlePresentationOutcome> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        if (IsSettled)
        {
            callback(Outcome);
            return;
        }

        callbacks.Add(callback);
    }

    public void Cancel(BattlePresentationCancelReason reason)
    {
        if (IsSettled)
            return;
        if (cancelRequested != null)
        {
            cancelRequested(reason);
            return;
        }
        TrySettle(MapCancellation(reason));
    }

    public static BattlePresentationOutcome MapCancellation(
        BattlePresentationCancelReason reason)
    {
        switch (reason)
        {
            case BattlePresentationCancelReason.TimedOut:
                return BattlePresentationOutcome.TimedOut;
            case BattlePresentationCancelReason.Faulted:
                return BattlePresentationOutcome.Faulted;
            default:
                return BattlePresentationOutcome.Cancelled;
        }
    }
}
