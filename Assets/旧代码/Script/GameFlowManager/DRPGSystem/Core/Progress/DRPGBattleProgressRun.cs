using System;
using System.Collections.Generic;

public sealed class DRPGProgressTraceEntry
{
    public DRPGProgressTraceEntry(
        string sourceId,
        string resourceId,
        string deduplicationKey,
        int depth,
        DRPGProgressTraceState state,
        string message = "")
    {
        SourceId = sourceId ?? string.Empty;
        ResourceId = resourceId ?? string.Empty;
        DeduplicationKey = deduplicationKey ?? string.Empty;
        Depth = Math.Max(0, depth);
        State = state;
        Message = message ?? string.Empty;
    }

    public string SourceId { get; }
    public string ResourceId { get; }
    public string DeduplicationKey { get; }
    public int Depth { get; }
    public DRPGProgressTraceState State { get; }
    public string Message { get; }
}

public sealed class DRPGBattleProgressRun
{
    public DRPGBattleProgressRun(
        IReadOnlyList<DRPGBattleEventBatch> observedBatches,
        IReadOnlyList<DRPGProgressMutationResult> mutations,
        IReadOnlyList<DRPGProgressTraceEntry> trace)
    {
        ObservedBatches = Copy(observedBatches);
        Mutations = Copy(mutations);
        Trace = Copy(trace);
    }

    public IReadOnlyList<DRPGBattleEventBatch> ObservedBatches { get; }
    public IReadOnlyList<DRPGProgressMutationResult> Mutations { get; }
    public IReadOnlyList<DRPGProgressTraceEntry> Trace { get; }

    public static DRPGBattleProgressRun Empty(
        IReadOnlyList<DRPGBattleEventBatch> observedBatches = null)
    {
        return new DRPGBattleProgressRun(
            observedBatches,
            Array.Empty<DRPGProgressMutationResult>(),
            Array.Empty<DRPGProgressTraceEntry>());
    }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<T>();
        var copy = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];
        return Array.AsReadOnly(copy);
    }
}
