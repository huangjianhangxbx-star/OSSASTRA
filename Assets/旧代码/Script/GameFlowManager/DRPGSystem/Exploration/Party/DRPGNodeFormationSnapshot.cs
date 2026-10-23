using System;
using System.Collections.Generic;

/// <summary>
/// Immutable node-local mapping from stable character identity to physical slot.
/// It deliberately does not encode protagonist identity.
/// </summary>
public sealed class DRPGNodeFormationSnapshot
{
    private readonly string[] order;
    private readonly IReadOnlyList<string> readOnlyOrder;

    internal DRPGNodeFormationSnapshot(IReadOnlyList<string> source, long revision)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (revision < 0L)
            throw new ArgumentOutOfRangeException(nameof(revision));

        order = new string[source.Count];
        for (int i = 0; i < source.Count; i++)
            order[i] = source[i];
        readOnlyOrder = Array.AsReadOnly(order);
        Revision = revision;
    }

    public IReadOnlyList<string> Order => readOnlyOrder;
    public int Count => order.Length;
    public long Revision { get; }

    public int IndexOf(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return -1;
        for (int i = 0; i < order.Length; i++)
        {
            if (string.Equals(order[i], stableId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }
}
