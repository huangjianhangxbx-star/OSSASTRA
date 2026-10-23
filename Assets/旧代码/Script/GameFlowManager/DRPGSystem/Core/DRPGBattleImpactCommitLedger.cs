using System;
using System.Collections.Generic;

public sealed class DRPGBattleImpactCommitLedger
{
    private enum State : byte
    {
        Pending,
        Committing,
        Committed
    }

    private readonly State[] states;

    public DRPGBattleImpactCommitLedger(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        states = new State[count];
    }

    public bool IsCommitted(int index) =>
        IsValid(index) && states[index] == State.Committed;

    public bool TryBeginCommit(int index)
    {
        if (!IsValid(index) || states[index] != State.Pending)
            return false;
        for (int i = 0; i < index; i++)
        {
            if (states[i] != State.Committed)
                return false;
        }
        states[index] = State.Committing;
        return true;
    }

    public void CompleteCommit(int index)
    {
        RequireValid(index);
        if (states[index] != State.Committing)
        {
            throw new InvalidOperationException(
                "Only an in-flight impact can be completed.");
        }
        states[index] = State.Committed;
    }

    public void RollBackBegin(int index)
    {
        RequireValid(index);
        if (states[index] == State.Committing)
            states[index] = State.Pending;
    }

    public IReadOnlyList<int> PendingIndices
    {
        get
        {
            var result = new List<int>();
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != State.Committed)
                    result.Add(i);
            }
            return result.AsReadOnly();
        }
    }

    public bool IsComplete
    {
        get
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != State.Committed)
                    return false;
            }
            return true;
        }
    }

    private bool IsValid(int index) => index >= 0 && index < states.Length;

    private void RequireValid(int index)
    {
        if (!IsValid(index))
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
