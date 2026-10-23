using System;
using UnityEngine;

// Zero-version data is the legacy serialized default, not an authored v1 policy.
[Serializable]
public struct DRPGEnemyWavePolicy
{
    [SerializeField] private int schemaVersion;
    [SerializeField] private int minWaveSize;
    [SerializeField] private int maxWaveSize;
    [SerializeField] private double announcementDuration;
    [SerializeField] private double entryStaggerMaximum;

    public DRPGEnemyWavePolicy(int minimum, int maximum, double announcement, double stagger)
    { schemaVersion = 1; minWaveSize = minimum; maxWaveSize = maximum; announcementDuration = announcement; entryStaggerMaximum = stagger; }

    internal DRPGEnemyWavePolicySnapshot Freeze(int capacity, int count, double defaultAnnouncement, double defaultStagger)
    {
        if (capacity < 1 || count < 1) throw new ArgumentException("Wave policy requires capacity and authored enemies.");
        if (schemaVersion == 0 && minWaveSize == 0 && maxWaveSize == 0 && announcementDuration == 0 && entryStaggerMaximum == 0)
            return new DRPGEnemyWavePolicySnapshot(1, Math.Min(capacity, count), defaultAnnouncement, defaultStagger, capacity);
        if (schemaVersion != 1) throw new ArgumentException("Unsupported or partially authored wave policy version.");
        return new DRPGEnemyWavePolicySnapshot(minWaveSize, maxWaveSize, announcementDuration, entryStaggerMaximum, capacity);
    }
}

internal readonly struct DRPGEnemyWavePolicySnapshot
{
    public int MinWaveSize { get; }
    public int MaxWaveSize { get; }
    public double AnnouncementDuration { get; }
    public double EntryStaggerMaximum { get; }
    internal DRPGEnemyWavePolicySnapshot(int min, int max, double announcement, double stagger, int capacity)
    {
        if (min <= 0 || max < min || max > capacity) throw new ArgumentOutOfRangeException(nameof(min), "Wave size must be within 1..capacity.");
        if (double.IsNaN(announcement) || double.IsInfinity(announcement) || announcement <= 0 ||
            double.IsNaN(stagger) || double.IsInfinity(stagger) || stagger < 0 || stagger > .50)
            throw new ArgumentOutOfRangeException(nameof(announcement), "Wave announcement must be readable and entry stagger within 0..0.50 seconds.");
        MinWaveSize = min; MaxWaveSize = max; AnnouncementDuration = announcement; EntryStaggerMaximum = stagger;
    }
}
