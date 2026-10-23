using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BattlePresentationBeat
{
    [SerializeField] private string beatId = "beat";
    [Min(0f)]
    [SerializeField] private float delayBeforeSeconds;
    [Min(0f)]
    [SerializeField] private float minimumDurationSeconds;
    [SerializeField] private bool releasesGate;
    [SerializeField] private List<BattlePresentationCueEntry> cues =
        new List<BattlePresentationCueEntry>();

    public string BeatId => beatId ?? string.Empty;
    public float DelayBeforeSeconds => delayBeforeSeconds;
    public float MinimumDurationSeconds => minimumDurationSeconds;
    public bool ReleasesGate => releasesGate;
    public IReadOnlyList<BattlePresentationCueEntry> Cues => cues;
}
