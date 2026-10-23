using System;
using System.Collections.Generic;

public sealed class BattlePresentationCompositeHandle :
    IBattlePresentationHandle
{
    private readonly BattlePresentationHandle completion =
        new BattlePresentationHandle();
    private readonly List<IBattlePresentationHandle> children;
    private readonly BattlePresentationOutcome[] childOutcomes;
    private int remaining;

    public BattlePresentationCompositeHandle(
        IReadOnlyList<IBattlePresentationHandle> children)
    {
        if (children == null)
            throw new ArgumentNullException(nameof(children));

        this.children = new List<IBattlePresentationHandle>(children.Count);
        childOutcomes = new BattlePresentationOutcome[children.Count];
        remaining = children.Count;

        for (int i = 0; i < children.Count; i++)
        {
            IBattlePresentationHandle child = children[i] ??
                throw new ArgumentException(
                    "Composite children cannot contain null.",
                    nameof(children));
            this.children.Add(child);
            int childIndex = i;
            child.WhenSettled(outcome => OnChildSettled(childIndex, outcome));
        }

        if (remaining == 0)
            completion.TrySettle(BattlePresentationOutcome.Completed);
    }

    public bool IsSettled => completion.IsSettled;
    public BattlePresentationOutcome Outcome => completion.Outcome;

    public void WhenSettled(Action<BattlePresentationOutcome> callback)
    {
        completion.WhenSettled(callback);
    }

    public void Cancel(BattlePresentationCancelReason reason)
    {
        if (IsSettled)
            return;

        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].IsSettled)
                children[i].Cancel(reason);
        }

        if (!completion.IsSettled)
            completion.Cancel(reason);
    }

    private void OnChildSettled(
        int childIndex,
        BattlePresentationOutcome outcome)
    {
        if (completion.IsSettled ||
            childOutcomes[childIndex] != BattlePresentationOutcome.Pending)
        {
            return;
        }

        childOutcomes[childIndex] = outcome;
        remaining--;
        if (remaining > 0)
            return;

        completion.TrySettle(CombineOutcomes());
    }

    private BattlePresentationOutcome CombineOutcomes()
    {
        bool anyCompleted = false;
        bool anyCancelled = false;
        bool anyTimedOut = false;
        bool anyFaulted = false;

        for (int i = 0; i < childOutcomes.Length; i++)
        {
            switch (childOutcomes[i])
            {
                case BattlePresentationOutcome.Completed:
                    anyCompleted = true;
                    break;
                case BattlePresentationOutcome.Cancelled:
                    anyCancelled = true;
                    break;
                case BattlePresentationOutcome.TimedOut:
                    anyTimedOut = true;
                    break;
                case BattlePresentationOutcome.Faulted:
                    anyFaulted = true;
                    break;
            }
        }

        if (anyFaulted)
            return BattlePresentationOutcome.Faulted;
        if (anyTimedOut)
            return BattlePresentationOutcome.TimedOut;
        if (anyCancelled)
            return BattlePresentationOutcome.Cancelled;
        if (anyCompleted)
            return BattlePresentationOutcome.Completed;
        return BattlePresentationOutcome.Skipped;
    }
}
