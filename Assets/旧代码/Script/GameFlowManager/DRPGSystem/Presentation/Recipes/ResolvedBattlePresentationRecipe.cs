using System;
using System.Collections.Generic;

public sealed class ResolvedBattlePresentationCue
{
    public ResolvedBattlePresentationCue(
        BattlePresentationCueSO cue,
        BattlePresentationLaneKind lane,
        BattlePresentationConflictPolicy conflictPolicy,
        bool blocksBeat)
    {
        Cue = cue ?? throw new ArgumentNullException(nameof(cue));
        Lane = lane;
        ConflictPolicy = conflictPolicy;
        BlocksBeat = blocksBeat;
    }

    public BattlePresentationCueSO Cue { get; }
    public BattlePresentationLaneKind Lane { get; }
    public BattlePresentationConflictPolicy ConflictPolicy { get; }
    public bool BlocksBeat { get; }
}

public sealed class ResolvedBattlePresentationBeat
{
    public ResolvedBattlePresentationBeat(
        string beatId,
        float delayBeforeSeconds,
        float minimumDurationSeconds,
        bool releasesGate,
        IEnumerable<ResolvedBattlePresentationCue> cues)
    {
        BeatId = beatId ?? string.Empty;
        DelayBeforeSeconds = delayBeforeSeconds;
        MinimumDurationSeconds = minimumDurationSeconds;
        ReleasesGate = releasesGate;
        Cues = Array.AsReadOnly(
            new List<ResolvedBattlePresentationCue>(
                cues ?? throw new ArgumentNullException(nameof(cues))).ToArray());
    }

    public string BeatId { get; }
    public float DelayBeforeSeconds { get; }
    public float MinimumDurationSeconds { get; }
    public bool ReleasesGate { get; }
    public IReadOnlyList<ResolvedBattlePresentationCue> Cues { get; }
}

public sealed class ResolvedBattlePresentationRecipe
{
    public ResolvedBattlePresentationRecipe(
        IEnumerable<string> sourceRecipeIds,
        BattlePresentationGateKind gateKind,
        float maximumGateSeconds,
        IEnumerable<ResolvedBattlePresentationBeat> beats)
    {
        SourceRecipeIds = Array.AsReadOnly(
            new List<string>(
                sourceRecipeIds ??
                throw new ArgumentNullException(nameof(sourceRecipeIds))).ToArray());
        GateKind = gateKind;
        MaximumGateSeconds = maximumGateSeconds;
        Beats = Array.AsReadOnly(
            new List<ResolvedBattlePresentationBeat>(
                beats ?? throw new ArgumentNullException(nameof(beats))).ToArray());
    }

    public IReadOnlyList<string> SourceRecipeIds { get; }
    public BattlePresentationGateKind GateKind { get; }
    public float MaximumGateSeconds { get; }
    public IReadOnlyList<ResolvedBattlePresentationBeat> Beats { get; }
    public bool HasContent => SourceRecipeIds.Count > 0 && Beats.Count > 0;
}
