using System;
using System.Collections.Generic;

/// <summary>
/// Owns physical party order for one node. Identity and role remain elsewhere.
/// </summary>
public sealed class DRPGNodePartyFormation
{
    private readonly List<string> entryOrder = new List<string>();
    private readonly List<string> currentOrder = new List<string>();
    private readonly IReadOnlyList<string> readOnlyOrder;

    public DRPGNodePartyFormation(IEnumerable<string> stableIds)
    {
        if (stableIds == null)
            throw new ArgumentNullException(nameof(stableIds));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string stableId in stableIds)
        {
            string normalized = Normalize(stableId);
            if (!seen.Add(normalized))
                throw new ArgumentException(
                    "Party formation contains a duplicate stable id: " + normalized,
                    nameof(stableIds));
            entryOrder.Add(normalized);
            currentOrder.Add(normalized);
        }
        readOnlyOrder = currentOrder.AsReadOnly();
    }

    public IReadOnlyList<string> Order => readOnlyOrder;
    public int Count => currentOrder.Count;
    public long Revision { get; private set; }

    public bool TrySwap(string firstStableId, string secondStableId)
    {
        int first = IndexOf(firstStableId);
        int second = IndexOf(secondStableId);
        if (first < 0 || second < 0 || first == second)
            return false;
        string value = currentOrder[first];
        currentOrder[first] = currentOrder[second];
        currentOrder[second] = value;
        Revision++;
        return true;
    }

    public bool Remove(string stableId)
    {
        int index = IndexOf(stableId);
        if (index < 0)
            return false;
        currentOrder.RemoveAt(index);
        Revision++;
        return true;
    }

    public bool Add(string stableId)
    {
        string normalized = Normalize(stableId);
        if (IndexOf(normalized) >= 0)
            return false;
        currentOrder.Add(normalized);
        Revision++;
        return true;
    }

    public void RestoreDefault()
    {
        var present = new HashSet<string>(currentOrder, StringComparer.Ordinal);
        var replacement = new List<string>(currentOrder.Count);
        for (int i = 0; i < entryOrder.Count; i++)
        {
            if (present.Contains(entryOrder[i]))
                replacement.Add(entryOrder[i]);
        }
        for (int i = 0; i < currentOrder.Count; i++)
        {
            if (!entryOrder.Contains(currentOrder[i]))
                replacement.Add(currentOrder[i]);
        }
        if (SameOrder(replacement))
            return;
        currentOrder.Clear();
        currentOrder.AddRange(replacement);
        Revision++;
    }

    public DRPGNodeFormationSnapshot Capture() =>
        new DRPGNodeFormationSnapshot(currentOrder, Revision);

    private int IndexOf(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return -1;
        for (int i = 0; i < currentOrder.Count; i++)
        {
            if (string.Equals(currentOrder[i], stableId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private bool SameOrder(IReadOnlyList<string> candidate)
    {
        if (candidate.Count != currentOrder.Count)
            return false;
        for (int i = 0; i < candidate.Count; i++)
        {
            if (!string.Equals(candidate[i], currentOrder[i], StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static string Normalize(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            throw new ArgumentException("A party member requires a stable id.", nameof(stableId));
        return stableId.Trim();
    }
}
