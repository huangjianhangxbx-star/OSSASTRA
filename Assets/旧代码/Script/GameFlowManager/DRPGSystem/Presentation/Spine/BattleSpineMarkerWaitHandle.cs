using System;

public sealed class BattleSpineMarkerWaitHandle : IBattlePresentationHandle
{
    private readonly BattlePresentationHandle inner;

    internal BattleSpineMarkerWaitHandle(
        Action<BattlePresentationCancelReason> cancelRequested)
    {
        inner = new BattlePresentationHandle(cancelRequested);
    }

    public bool IsSettled => inner.IsSettled;
    public BattlePresentationOutcome Outcome => inner.Outcome;

    public void WhenSettled(Action<BattlePresentationOutcome> callback)
    {
        inner.WhenSettled(callback);
    }

    public void Cancel(BattlePresentationCancelReason reason)
    {
        inner.Cancel(reason);
    }

    internal bool TrySettle(BattlePresentationOutcome outcome, Exception fault = null)
    {
        return inner.TrySettle(outcome, fault);
    }
}
