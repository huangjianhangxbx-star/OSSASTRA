using System;
using System.Collections.Generic;

public sealed class DRPGBattleReactionPipeline
{
    private readonly IDRPGBattleReactionSource[] sources;
    private readonly DRPGBattleReactionOptions options;
    internal bool HasLegacySources => sources.Length != 0;

    public DRPGBattleReactionPipeline(
        IEnumerable<IDRPGBattleReactionSource> sources = null,
        DRPGBattleReactionOptions options = null)
    {
        this.options = options ?? new DRPGBattleReactionOptions();
        var collected = new List<IDRPGBattleReactionSource>();
        if (sources != null)
        {
            foreach (IDRPGBattleReactionSource source in sources)
            {
                if (source == null)
                {
                    throw new ArgumentException(
                        "Reaction sources cannot contain null.",
                        nameof(sources));
                }
                if (string.IsNullOrWhiteSpace(source.SourceId))
                {
                    throw new ArgumentException(
                        "Every reaction source requires a stable ID.",
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
                    "Duplicate reaction source ID: " +
                    collected[i].SourceId,
                    nameof(sources));
            }
        }
        this.sources = collected.ToArray();
    }

    public DRPGBattleReactionRun ProcessBatch(
        DRPGBattleEventBatch rootBatch,
        IDRPGBattleEffectEnvironment environment,
        DRPGActionResolver resolver,
        DRPGBattleEventFactory eventFactory)
    {
        if (rootBatch == null)
            throw new ArgumentNullException(nameof(rootBatch));
        if (environment == null)
            throw new ArgumentNullException(nameof(environment));
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));
        if (eventFactory == null)
            throw new ArgumentNullException(nameof(eventFactory));
        if (rootBatch.Depth != 0)
        {
            throw new ArgumentException(
                "The reaction run must begin with a depth-zero root batch.",
                nameof(rootBatch));
        }

        long rootActionId = rootBatch.RootActionId;
        var batches = new List<DRPGBattleEventBatch> { rootBatch };
        var events = new List<DRPGBattleEvent>();
        var eventQueue = new Queue<DRPGBattleEvent>();
        var resolutions = new List<DRPGBattleReactionResolution>();
        var trace = new List<DRPGBattleReactionTraceEntry>();
        var deduplicationKeys = new HashSet<string>(StringComparer.Ordinal);

        AppendBatch(rootBatch, events, eventQueue);

        int acceptedCount = 0;
        while (eventQueue.Count > 0)
        {
            DRPGBattleEvent battleEvent = eventQueue.Dequeue();
            var requests = new List<DRPGBattleReactionRequest>();
            DRPGBattleReactionContext context =
                new DRPGBattleReactionContext(
                    battleEvent,
                    environment.Participants);

            for (int sourceIndex = 0;
                 sourceIndex < sources.Length;
                 sourceIndex++)
            {
                IDRPGBattleReactionSource source = sources[sourceIndex];
                var sourceRequests =
                    new List<DRPGBattleReactionRequest>();
                try
                {
                    source.Collect(context, sourceRequests);
                }
                catch (Exception exception)
                {
                    trace.Add(new DRPGBattleReactionTraceEntry(
                        battleEvent.EventId,
                        battleEvent.Depth,
                        source.SourceId,
                        string.Empty,
                        DRPGBattleReactionTraceState.SourceFaulted,
                        exception.Message));
                    continue;
                }

                for (int requestIndex = 0;
                     requestIndex < sourceRequests.Count;
                     requestIndex++)
                {
                    DRPGBattleReactionRequest request =
                        sourceRequests[requestIndex];
                    if (request == null ||
                        !string.Equals(
                            request.SourceId,
                            source.SourceId,
                            StringComparison.Ordinal) ||
                        request.TriggeringEventId != battleEvent.EventId)
                    {
                        trace.Add(new DRPGBattleReactionTraceEntry(
                            battleEvent.EventId,
                            battleEvent.Depth,
                            source.SourceId,
                            request != null
                                ? request.ReactionId
                                : string.Empty,
                            DRPGBattleReactionTraceState.SourceFaulted,
                            "The source returned a null, foreign, or stale request."));
                        continue;
                    }
                    requests.Add(request);
                }
            }

            requests.Sort(CompareRequests);
            for (int requestIndex = 0;
                 requestIndex < requests.Count;
                 requestIndex++)
            {
                DRPGBattleReactionRequest request = requests[requestIndex];
                int reactionDepth = battleEvent.Depth + 1;
                if (reactionDepth > options.MaxDepth)
                {
                    trace.Add(Trace(
                        battleEvent,
                        request,
                        reactionDepth,
                        DRPGBattleReactionTraceState.SkippedDepthLimit,
                        "Reaction depth limit reached."));
                    continue;
                }
                if (acceptedCount >= options.MaxReactionCount)
                {
                    trace.Add(Trace(
                        battleEvent,
                        request,
                        reactionDepth,
                        DRPGBattleReactionTraceState.SkippedBudgetLimit,
                        "Reaction count budget reached."));
                    continue;
                }
                if (!deduplicationKeys.Add(request.DeduplicationKey))
                {
                    trace.Add(Trace(
                        battleEvent,
                        request,
                        reactionDepth,
                        DRPGBattleReactionTraceState.SkippedDuplicate,
                        "Reaction deduplication key already resolved."));
                    continue;
                }

                BattleParticipantRuntime actor = FindParticipant(
                    environment.Participants,
                    request.ActorRuntimeId);
                BattleParticipantRuntime target = FindParticipant(
                    environment.Participants,
                    request.TargetRuntimeId);
                if (actor == null || target == null ||
                    !actor.IsAlive || !actor.IsDeployed ||
                    !target.IsAlive || !target.IsDeployed ||
                    request.ActionSpec.ActorRuntimeId != actor.RuntimeId ||
                    request.ActionSpec.TargetRuntimeId != target.RuntimeId)
                {
                    trace.Add(Trace(
                        battleEvent,
                        request,
                        reactionDepth,
                        DRPGBattleReactionTraceState.SkippedInvalidParticipant,
                        "Reaction actor or target is no longer valid."));
                    continue;
                }

                acceptedCount++;
                try
                {
                    DRPGResolvedActionBatch batch = resolver.Resolve(
                        request.ActionSpec,
                        actor,
                        target,
                        environment,
                        request.ImpactContext);
                    resolutions.Add(new DRPGBattleReactionResolution(
                        battleEvent,
                        request,
                        batch));
                    trace.Add(Trace(
                        battleEvent,
                        request,
                        reactionDepth,
                        DRPGBattleReactionTraceState.Resolved,
                        string.Empty));
                    DRPGBattleEventBatch childBatch =
                        eventFactory.CreateChild(
                        rootActionId,
                        reactionDepth,
                        request.ActorRuntimeId,
                        request.TargetRuntimeId,
                        request.ActionSpec.SkillId,
                        batch);
                    batches.Add(childBatch);
                    AppendBatch(childBatch, events, eventQueue);
                }
                catch (Exception exception)
                {
                    trace.Add(Trace(
                        battleEvent,
                        request,
                        reactionDepth,
                        DRPGBattleReactionTraceState.ResolutionFaulted,
                        exception.Message));
                }
            }
        }

        return new DRPGBattleReactionRun(
            rootBatch,
            batches,
            resolutions,
            trace);
    }

    private static int CompareRequests(
        DRPGBattleReactionRequest left,
        DRPGBattleReactionRequest right)
    {
        int priority = right.Priority.CompareTo(left.Priority);
        if (priority != 0)
            return priority;
        int source = string.CompareOrdinal(left.SourceId, right.SourceId);
        if (source != 0)
            return source;
        int reaction = string.CompareOrdinal(
            left.ReactionId,
            right.ReactionId);
        if (reaction != 0)
            return reaction;
        return string.CompareOrdinal(
            left.DeduplicationKey,
            right.DeduplicationKey);
    }

    private static void AppendBatch(
        DRPGBattleEventBatch batch,
        ICollection<DRPGBattleEvent> events,
        Queue<DRPGBattleEvent> queue)
    {
        for (int i = 0; i < batch.Events.Count; i++)
        {
            DRPGBattleEvent battleEvent = batch.Events[i];
            events.Add(battleEvent);
            queue.Enqueue(battleEvent);
        }
    }

    private static BattleParticipantRuntime FindParticipant(
        IReadOnlyList<BattleParticipantRuntime> participants,
        int runtimeId)
    {
        if (participants == null)
            return null;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant != null && participant.RuntimeId == runtimeId)
                return participant;
        }
        return null;
    }

    private static DRPGBattleReactionTraceEntry Trace(
        DRPGBattleEvent battleEvent,
        DRPGBattleReactionRequest request,
        int depth,
        DRPGBattleReactionTraceState state,
        string message)
    {
        return new DRPGBattleReactionTraceEntry(
            battleEvent.EventId,
            depth,
            request.SourceId,
            request.ReactionId,
            state,
            message);
    }
}
