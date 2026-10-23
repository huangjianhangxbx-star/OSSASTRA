#if UNITY_EDITOR
using System;
using System.Collections.Generic;

// Editor-test contract substitute only. Production probes must not use this type.
public enum ScriptedPresentationOutcome
{
    Pending,
    Completed,
    Cancelled,
    TimedOut,
    Faulted
}

// Editor-test contract substitute only. Production probes must not use this type.
public sealed class ScriptedPresentationReceipt
{
    internal ScriptedPresentationReceipt(int id, string name)
    {
        Id = id;
        Name = name;
        Issued = true;
        Outcome = ScriptedPresentationOutcome.Pending;
    }

    public int Id { get; }
    public string Name { get; }
    public bool Issued { get; }
    public bool Completed { get; private set; }
    public ScriptedPresentationOutcome Outcome { get; private set; }

    public bool TryComplete(ScriptedPresentationOutcome outcome)
    {
        if (outcome == ScriptedPresentationOutcome.Pending)
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (Completed)
            return false;

        Completed = true;
        Outcome = outcome;
        return true;
    }
}

// Editor-test contract substitute only. Production probes must not use this type.
public sealed class ScriptedBattlePresentationAdapter
{
    private readonly List<ScriptedPresentationReceipt> history =
        new List<ScriptedPresentationReceipt>();
    private int nextId = 1;

    public IReadOnlyList<ScriptedPresentationReceipt> History =>
        history.AsReadOnly();

    public IReadOnlyList<ScriptedPresentationReceipt> Pending
    {
        get
        {
            var pending = new List<ScriptedPresentationReceipt>();
            for (int i = 0; i < history.Count; i++)
            {
                if (!history[i].Completed)
                    pending.Add(history[i]);
            }

            return pending.AsReadOnly();
        }
    }

    public ScriptedPresentationReceipt Issue(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A scripted presentation needs a name.", nameof(name));

        var receipt = new ScriptedPresentationReceipt(nextId++, name);
        history.Add(receipt);
        return receipt;
    }

    public bool Complete(int id, ScriptedPresentationOutcome outcome)
    {
        for (int i = 0; i < history.Count; i++)
        {
            if (history[i].Id == id)
                return history[i].TryComplete(outcome);
        }

        return false;
    }

    public bool Complete(
        ScriptedPresentationReceipt receipt,
        ScriptedPresentationOutcome outcome)
    {
        if (receipt == null)
            throw new ArgumentNullException(nameof(receipt));

        return history.Contains(receipt) && receipt.TryComplete(outcome);
    }
}
#endif
