using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class BattlePresentationTraceBuffer
{
    private readonly List<string> entries;
    private readonly ReadOnlyCollection<string> readOnlyEntries;

    public BattlePresentationTraceBuffer(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Trace capacity must be at least one.");
        }

        Capacity = capacity;
        entries = new List<string>(capacity);
        readOnlyEntries = entries.AsReadOnly();
    }

    public int Capacity { get; }
    public IReadOnlyList<string> Entries => readOnlyEntries;

    public void Record(string entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        if (entries.Count == Capacity)
            entries.RemoveAt(0);
        entries.Add(entry);
    }

    public void Clear()
    {
        entries.Clear();
    }
}
