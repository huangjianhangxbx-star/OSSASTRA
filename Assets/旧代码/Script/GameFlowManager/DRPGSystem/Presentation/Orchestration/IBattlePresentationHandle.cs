using System;

public interface IBattlePresentationHandle
{
    bool IsSettled { get; }
    BattlePresentationOutcome Outcome { get; }
    void WhenSettled(Action<BattlePresentationOutcome> callback);
    void Cancel(BattlePresentationCancelReason reason);
}
