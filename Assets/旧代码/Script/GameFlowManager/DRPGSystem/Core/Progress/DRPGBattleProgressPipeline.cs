using System;
using System.Collections.Generic;

public sealed class DRPGBattleProgressPipeline
{
    private readonly IDRPGBattleProgressSource[] sources;
    private readonly DRPGProgressStoreRouter router;
    private long manualMutationSerial = 1L;

    public DRPGBattleProgressPipeline(
        IEnumerable<IDRPGBattleProgressSource> sources = null,
        DRPGProgressStoreRouter router = null)
    {
        this.router = router ?? new DRPGProgressStoreRouter(
            new IDRPGProgressStore[] { new DRPGBattleProgressLedger() });
        var collected = new List<IDRPGBattleProgressSource>();
        if (sources != null)
        {
            foreach (IDRPGBattleProgressSource source in sources)
            {
                if (source == null)
                {
                    throw new ArgumentException(
                        "Progress sources cannot contain null.",
                        nameof(sources));
                }
                if (string.IsNullOrWhiteSpace(source.SourceId))
                {
                    throw new ArgumentException(
                        "Every progress source requires a stable ID.",
                        nameof(sources));
                }
                collected.Add(source);
            }
        }
        collected.Sort((left, right) => string.CompareOrdinal(
            left.SourceId,
            right.SourceId));
        for (int i = 1; i < collected.Count; i++)
        {
            if (string.Equals(
                    collected[i - 1].SourceId,
                    collected[i].SourceId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Duplicate progress source ID: " +
                    collected[i].SourceId,
                    nameof(sources));
            }
        }
        this.sources = collected.ToArray();
    }

    public DRPGProgressStoreRouter Router => router;

    internal IReadOnlyList<IDRPGBattleProgressSource> CaptureActionSources()
    {
        var frozen = new IDRPGBattleProgressSource[sources.Length];
        for (int i = 0; i < frozen.Length; i++)
        {
            if (!(sources[i] is DRPGConfiguredProgressSource configured))
                throw new InvalidOperationException("Progress source requires an explicit frozen action adapter: " + sources[i].SourceId);
            frozen[i] = configured.FreezeForAction();
        }
        return Array.AsReadOnly(frozen);
    }

    public bool TryGetSnapshot(
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        out DRPGProgressSnapshot snapshot)
    {
        snapshot = null;
        return router.TryGetStore(scope, out IDRPGProgressStore store) &&
            store.TryGetSnapshot(ownerStableId, resourceId, out snapshot);
    }

    public bool CanSpend(
        string ownerStableId,
        DRPGResolvedProgressCost cost,
        DRPGProgressScope scope,
        out string error)
    {
        error = string.Empty;
        if (cost == null || !cost.IsConfigured)
            return true;
        if (!TryGetSnapshot(
                ownerStableId,
                cost.ResourceId,
                scope,
                out DRPGProgressSnapshot snapshot))
        {
            error = "The required battle progress resource is not registered.";
            return false;
        }
        if (snapshot.CurrentValue + 0.0001f < cost.Amount)
        {
            error = "The actor does not have enough " + cost.ResourceId + ".";
            return false;
        }
        return true;
    }

    public bool TryRegister(
        string ownerStableId,
        DRPGProgressResourceDefinition definition,
        out DRPGProgressMutationResult result,
        out string error)
    {
        if (definition == null)
        {
            result = null;
            error = "A progress resource definition is required.";
            return false;
        }

        DRPGProgressMutationRequest request =
            DRPGProgressMutationRequest.Register(
                "progress.manual",
                NextManualKey("register", ownerStableId, definition.ResourceId),
                int.MaxValue,
                ownerStableId,
                definition);
        result = router.Apply(request);
        error = result.WasApplied ? string.Empty : result.Message;
        return result.WasApplied;
    }

    public bool TryGain(
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        float amount,
        out DRPGProgressMutationResult result,
        out string error)
    {
        return TryApplyManual(
            DRPGProgressMutationRequest.Gain(
                "progress.manual",
                NextManualKey("gain", ownerStableId, resourceId),
                int.MaxValue,
                ownerStableId,
                resourceId,
                scope,
                amount,
                true),
            out result,
            out error);
    }

    public bool TrySpend(
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        float amount,
        out DRPGProgressMutationResult result,
        out string error)
    {
        return TryApplyManual(
            DRPGProgressMutationRequest.Spend(
                "progress.manual",
                NextManualKey("spend", ownerStableId, resourceId),
                int.MaxValue,
                ownerStableId,
                resourceId,
                scope,
                amount,
                true),
            out result,
            out error);
    }

    public bool TryReset(
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        out DRPGProgressMutationResult result,
        out string error)
    {
        return TryApplyManual(
            DRPGProgressMutationRequest.Reset(
                "progress.manual",
                NextManualKey("reset", ownerStableId, resourceId),
                int.MaxValue,
                ownerStableId,
                resourceId,
                scope),
            out result,
            out error);
    }

    public DRPGBattleProgressRun Process(
        IReadOnlyList<DRPGBattleEventBatch> batches,
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        if (batches == null)
            throw new ArgumentNullException(nameof(batches));
        if (sources.Length == 0 || batches.Count == 0)
            return DRPGBattleProgressRun.Empty(batches);

        var collected = new List<CollectedRequest>();
        var trace = new List<DRPGProgressTraceEntry>();
        for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            DRPGBattleEventBatch batch = batches[batchIndex];
            if (batch == null)
            {
                throw new ArgumentException(
                    "Progress batches cannot contain null.",
                    nameof(batches));
            }
            var context = new DRPGBattleProgressContext(batch, participants);
            for (int sourceIndex = 0;
                 sourceIndex < sources.Length;
                 sourceIndex++)
            {
                IDRPGBattleProgressSource source = sources[sourceIndex];
                var requests = new List<DRPGProgressMutationRequest>();
                try
                {
                    source.Collect(context, requests);
                }
                catch (Exception exception)
                {
                    trace.Add(new DRPGProgressTraceEntry(
                        source.SourceId,
                        string.Empty,
                        string.Empty,
                        batch.Depth,
                        DRPGProgressTraceState.SourceFaulted,
                        exception.Message));
                    continue;
                }

                for (int requestIndex = 0;
                     requestIndex < requests.Count;
                     requestIndex++)
                {
                    DRPGProgressMutationRequest request = requests[requestIndex];
                    if (request == null ||
                        !string.Equals(
                            request.SourceId,
                            source.SourceId,
                            StringComparison.Ordinal))
                    {
                        trace.Add(new DRPGProgressTraceEntry(
                            source.SourceId,
                            request != null ? request.ResourceId : string.Empty,
                            request != null
                                ? request.DeduplicationKey
                                : string.Empty,
                            batch.Depth,
                            DRPGProgressTraceState.SourceFaulted,
                            "The source returned a null or foreign request."));
                        continue;
                    }
                    collected.Add(new CollectedRequest(context, request));
                }
            }
        }

        collected.Sort(Compare);
        var mutations = new List<DRPGProgressMutationResult>();
        var deduplicationKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < collected.Count; i++)
        {
            CollectedRequest item = collected[i];
            DRPGProgressMutationRequest request = item.Request;
            if (item.Context.Batch.Depth > 0 &&
                !request.IncludeReactionDepth)
            {
                trace.Add(Trace(
                    item,
                    DRPGProgressTraceState.SkippedReactionDepth,
                    "Reaction-depth progress is disabled by default."));
                continue;
            }

            string dedupe = request.SourceId + "\n" +
                request.ResourceId + "\n" + request.DeduplicationKey;
            if (!deduplicationKeys.Add(dedupe))
            {
                trace.Add(Trace(
                    item,
                    DRPGProgressTraceState.SkippedDuplicate,
                    "Progress deduplication key already resolved."));
                continue;
            }
            if (!item.Context.IsKnownOwner(request.OwnerStableId))
            {
                trace.Add(Trace(
                    item,
                    DRPGProgressTraceState.RejectedInvalidOwner,
                    "Progress owner is not part of this battle context."));
                continue;
            }

            DRPGProgressMutationResult result = router.Apply(request);
            if (result.WasApplied)
                mutations.Add(result);
            trace.Add(Trace(item, ToTraceState(result.State), result.Message));
        }

        return new DRPGBattleProgressRun(batches, mutations, trace);
    }

    private bool TryApplyManual(
        DRPGProgressMutationRequest request,
        out DRPGProgressMutationResult result,
        out string error)
    {
        result = router.Apply(request);
        error = result.WasApplied ? string.Empty : result.Message;
        return result.WasApplied;
    }

    private string NextManualKey(
        string operation,
        string ownerStableId,
        string resourceId)
    {
        return operation + ":" + (ownerStableId ?? string.Empty) + ":" +
            (resourceId ?? string.Empty) + ":" + manualMutationSerial++;
    }

    private static int Compare(CollectedRequest left, CollectedRequest right)
    {
        DRPGProgressMutationRequest a = left.Request;
        DRPGProgressMutationRequest b = right.Request;
        int priority = b.Priority.CompareTo(a.Priority);
        if (priority != 0)
            return priority;
        int source = string.CompareOrdinal(a.SourceId, b.SourceId);
        if (source != 0)
            return source;
        int resource = string.CompareOrdinal(a.ResourceId, b.ResourceId);
        if (resource != 0)
            return resource;
        int dedupe = string.CompareOrdinal(
            a.DeduplicationKey,
            b.DeduplicationKey);
        if (dedupe != 0)
            return dedupe;
        return left.Context.Batch.Depth.CompareTo(right.Context.Batch.Depth);
    }

    private static DRPGProgressTraceEntry Trace(
        CollectedRequest item,
        DRPGProgressTraceState state,
        string message)
    {
        return new DRPGProgressTraceEntry(
            item.Request.SourceId,
            item.Request.ResourceId,
            item.Request.DeduplicationKey,
            item.Context.Batch.Depth,
            state,
            message);
    }

    private static DRPGProgressTraceState ToTraceState(
        DRPGProgressMutationState state)
    {
        switch (state)
        {
            case DRPGProgressMutationState.Applied:
                return DRPGProgressTraceState.Applied;
            case DRPGProgressMutationState.RejectedInvalidOwner:
                return DRPGProgressTraceState.RejectedInvalidOwner;
            case DRPGProgressMutationState.RejectedInvalidResource:
                return DRPGProgressTraceState.RejectedInvalidResource;
            case DRPGProgressMutationState.RejectedUnsupportedScope:
                return DRPGProgressTraceState.RejectedUnsupportedScope;
            case DRPGProgressMutationState.RejectedInsufficientValue:
                return DRPGProgressTraceState.RejectedInsufficientValue;
            default:
                return DRPGProgressTraceState.RejectedInvalidRequest;
        }
    }

    private sealed class CollectedRequest
    {
        public CollectedRequest(
            DRPGBattleProgressContext context,
            DRPGProgressMutationRequest request)
        {
            Context = context;
            Request = request;
        }

        public DRPGBattleProgressContext Context { get; }
        public DRPGProgressMutationRequest Request { get; }
    }
}
