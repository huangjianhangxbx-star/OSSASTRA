using System;
using System.Collections.Generic;

public sealed class BattlePresentationRun
{
    private readonly BattlePresentationHandle gate =
        new BattlePresentationHandle();
    private readonly BattlePresentationHandle finished =
        new BattlePresentationHandle();
    private readonly List<IBattlePresentationHandle> cueHandles =
        new List<IBattlePresentationHandle>();
    private readonly Action<BattlePresentationRun, BattlePresentationCancelReason>
        cancelRequested;
    private IDisposable inputLease;
    private bool recipeComplete;
    private BattlePresentationOutcome recipeOutcome =
        BattlePresentationOutcome.Completed;

    internal BattlePresentationRun(
        BattlePresentationRequest request,
        ResolvedBattlePresentationRecipe recipe,
        Action<BattlePresentationRun, BattlePresentationCancelReason>
            cancelRequested)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        this.cancelRequested = cancelRequested;
    }

    public BattlePresentationRequest Request { get; }
    public ResolvedBattlePresentationRecipe Recipe { get; }
    public bool IsGateReleased => gate.IsSettled;
    public bool IsFinished => finished.IsSettled;
    public BattlePresentationOutcome GateOutcome => gate.Outcome;
    public BattlePresentationOutcome FinishedOutcome => finished.Outcome;
    public int GateReleaseCount { get; private set; }

    public void WhenGateReleased(
        Action<BattlePresentationOutcome> callback)
    {
        gate.WhenSettled(callback);
    }

    public void WhenFinished(
        Action<BattlePresentationOutcome> callback)
    {
        finished.WhenSettled(callback);
    }

    public void Cancel(BattlePresentationCancelReason reason)
    {
        if (IsFinished)
            return;

        if (cancelRequested != null)
        {
            cancelRequested(this, reason);
            return;
        }

        CancelDirect(reason);
    }

    internal void SetInputLease(IDisposable lease)
    {
        inputLease = lease;
        if (IsGateReleased)
            ReleaseInputLease();
    }

    internal void RegisterCue(IBattlePresentationHandle handle)
    {
        if (handle == null)
            throw new ArgumentNullException(nameof(handle));

        cueHandles.Add(handle);
        handle.WhenSettled(_ => TryFinish());
    }

    internal void ReleaseGate(BattlePresentationOutcome outcome)
    {
        if (gate.TrySettle(outcome))
        {
            GateReleaseCount++;
            ReleaseInputLease();
        }
    }

    internal void MarkRecipeComplete(BattlePresentationOutcome outcome)
    {
        recipeComplete = true;
        recipeOutcome = outcome;
        TryFinish();
    }

    internal void CancelDirect(BattlePresentationCancelReason reason)
    {
        if (IsFinished)
            return;

        BattlePresentationOutcome outcome = OutcomeFrom(reason);
        recipeOutcome = outcome;
        recipeComplete = true;

        for (int i = 0; i < cueHandles.Count; i++)
        {
            if (!cueHandles[i].IsSettled)
                cueHandles[i].Cancel(reason);
        }

        ReleaseGate(outcome);
        if (!finished.IsSettled)
            finished.TrySettle(outcome);
        ReleaseInputLease();
    }

    internal void CancelUnsettledCues(BattlePresentationCancelReason reason)
    {
        for (int i = 0; i < cueHandles.Count; i++)
        {
            if (!cueHandles[i].IsSettled)
                cueHandles[i].Cancel(reason);
        }
    }

    private void TryFinish()
    {
        if (!recipeComplete || finished.IsSettled)
            return;

        for (int i = 0; i < cueHandles.Count; i++)
        {
            if (!cueHandles[i].IsSettled)
                return;
        }

        BattlePresentationOutcome outcome = MergeCueOutcomes(recipeOutcome);
        if (!gate.IsSettled)
            ReleaseGate(outcome);
        finished.TrySettle(outcome);
        ReleaseInputLease();
    }

    private BattlePresentationOutcome MergeCueOutcomes(
        BattlePresentationOutcome initial)
    {
        BattlePresentationOutcome result = initial;
        bool anyCompleted = initial == BattlePresentationOutcome.Completed;
        for (int i = 0; i < cueHandles.Count; i++)
        {
            BattlePresentationOutcome cueOutcome = cueHandles[i].Outcome;
            if (cueOutcome == BattlePresentationOutcome.Faulted)
                return BattlePresentationOutcome.Faulted;
            if (cueOutcome == BattlePresentationOutcome.TimedOut)
                result = BattlePresentationOutcome.TimedOut;
            else if (cueOutcome == BattlePresentationOutcome.Cancelled &&
                result != BattlePresentationOutcome.TimedOut)
                result = BattlePresentationOutcome.Cancelled;
            else if (cueOutcome == BattlePresentationOutcome.Completed)
                anyCompleted = true;
        }

        if (result == BattlePresentationOutcome.Skipped && anyCompleted)
            return BattlePresentationOutcome.Completed;
        return result;
    }

    private void ReleaseInputLease()
    {
        IDisposable lease = inputLease;
        inputLease = null;
        lease?.Dispose();
    }

    private static BattlePresentationOutcome OutcomeFrom(
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
