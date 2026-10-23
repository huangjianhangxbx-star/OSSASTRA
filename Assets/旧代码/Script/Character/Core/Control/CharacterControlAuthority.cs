using System;
using System.Collections.Generic;

public sealed class CharacterControlAuthority
{
    private readonly Dictionary<object, Entry> entries =
        new Dictionary<object, Entry>();

    private long sequence;

    public void SetOverride(object owner, CharacterControlOverride value)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        entries[owner] = new Entry(value, ++sequence);
    }

    public void ReleaseOverride(object owner)
    {
        if (owner == null)
            return;

        entries.Remove(owner);
    }

    public bool IsAllowed(CharacterControlChannel channel)
    {
        foreach (Entry entry in entries.Values)
        {
            if ((entry.Value.BlockedChannels & channel) != 0)
                return false;
        }

        return true;
    }

    public CharacterFacingMode ResolveFacingMode(CharacterFacingMode fallback)
    {
        bool found = false;
        Entry selected = default;

        foreach (Entry entry in entries.Values)
        {
            if (!entry.Value.HasFacingMode)
                continue;

            if (!found || IsHigherPriority(entry, selected))
            {
                selected = entry;
                found = true;
            }
        }

        return found ? selected.Value.FacingMode : fallback;
    }

    public AimTrackingMode ResolveAimTrackingMode(AimTrackingMode fallback)
    {
        bool found = false;
        Entry selected = default;

        foreach (Entry entry in entries.Values)
        {
            if (!entry.Value.HasAimTrackingMode)
                continue;

            if (!found || IsHigherPriority(entry, selected))
            {
                selected = entry;
                found = true;
            }
        }

        return found ? selected.Value.AimTrackingMode : fallback;
    }

    public void Clear()
    {
        entries.Clear();
    }

    private static bool IsHigherPriority(Entry candidate, Entry selected)
    {
        if (candidate.Value.Priority != selected.Value.Priority)
            return candidate.Value.Priority > selected.Value.Priority;

        return candidate.Sequence > selected.Sequence;
    }

    private readonly struct Entry
    {
        public CharacterControlOverride Value { get; }
        public long Sequence { get; }

        public Entry(CharacterControlOverride value, long sequence)
        {
            Value = value;
            Sequence = sequence;
        }
    }
}
