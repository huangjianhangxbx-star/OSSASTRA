using System;
using System.Collections.Generic;

public sealed class DRPGBattleReactionParticipantSnapshot
{
    private DRPGBattleReactionParticipantSnapshot(
        BattleParticipantRuntime participant)
    {
        RuntimeId = participant.RuntimeId;
        Side = participant.Side;
        RosterRole = participant.RosterRole;
        IsAlive = participant.IsAlive;
        IsDeployed = participant.IsDeployed;
        CurrentHp = participant.CurrentHp;
        MaxHp = participant.EffectiveMaxHp;
        CurrentMp = participant.CurrentMp;
        MaxMp = participant.EffectiveMaxMp;
        CurrentPosture = participant.CurrentPosture;
        MaxPosture = participant.EffectiveMaxPosture;
        CurrentShield = participant.CurrentShield;
        FormationRow = participant.FormationRow;
    }

    public int RuntimeId { get; }
    public DRPGBattleSide Side { get; }
    public DRPGBattleRosterRole RosterRole { get; }
    public bool IsAlive { get; }
    public bool IsDeployed { get; }
    public float CurrentHp { get; }
    public float MaxHp { get; }
    public float CurrentMp { get; }
    public float MaxMp { get; }
    public float CurrentPosture { get; }
    public float MaxPosture { get; }
    public float CurrentShield { get; }
    public DRPGFormationRow FormationRow { get; }

    internal static DRPGBattleReactionParticipantSnapshot From(
        BattleParticipantRuntime participant)
    {
        if (participant == null)
            throw new ArgumentNullException(nameof(participant));
        return new DRPGBattleReactionParticipantSnapshot(participant);
    }
}

public interface IDRPGBattleReactionSource
{
    string SourceId { get; }

    void Collect(
        DRPGBattleReactionContext context,
        ICollection<DRPGBattleReactionRequest> requests);
}

public sealed class DRPGBattleReactionContext
{
    private readonly Dictionary<int, BattleParticipantRuntime> runtimes;

    internal DRPGBattleReactionContext(
        DRPGBattleEvent battleEvent,
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        Event = battleEvent ??
            throw new ArgumentNullException(nameof(battleEvent));
        runtimes = new Dictionary<int, BattleParticipantRuntime>();
        int count = participants != null ? participants.Count : 0;
        var snapshots = new DRPGBattleReactionParticipantSnapshot[count];
        for (int i = 0; i < count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant == null)
            {
                throw new ArgumentException(
                    "Reaction participants cannot contain null.",
                    nameof(participants));
            }
            runtimes.Add(participant.RuntimeId, participant);
            snapshots[i] =
                DRPGBattleReactionParticipantSnapshot.From(participant);
        }
        Participants = Array.AsReadOnly(snapshots);
    }

    public DRPGBattleEvent Event { get; }
    public IReadOnlyList<DRPGBattleReactionParticipantSnapshot> Participants
    {
        get;
    }

    public DRPGBattleReactionRequest CreateSkillRequest(
        string sourceId,
        string reactionId,
        string deduplicationKey,
        int priority,
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGSkillSO skill,
        DRPGBattleImpactContext impactContext)
    {
        if (!runtimes.TryGetValue(
                actorRuntimeId,
                out BattleParticipantRuntime actor))
        {
            throw new InvalidOperationException(
                "Reaction actor was not found: " + actorRuntimeId);
        }
        if (!runtimes.ContainsKey(targetRuntimeId))
        {
            throw new InvalidOperationException(
                "Reaction target was not found: " + targetRuntimeId);
        }
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));

        DRPGBattleCommand command = DRPGBattleCommand.UseSkill(
            actorRuntimeId,
            targetRuntimeId,
            skill);
        DRPGResolvedActionSpec spec =
            DRPGActionSpecResolver.Resolve(command, actor)
                .CreateReaction(command);
        return new DRPGBattleReactionRequest(
            Event.EventId,
            sourceId,
            reactionId,
            deduplicationKey,
            priority,
            spec,
            targetRuntimeId,
            impactContext);
    }
}

public sealed class DRPGBattleReactionRequest
{
    public DRPGBattleReactionRequest(
        long triggeringEventId,
        string sourceId,
        string reactionId,
        string deduplicationKey,
        int priority,
        DRPGResolvedActionSpec actionSpec,
        int targetRuntimeId,
        DRPGBattleImpactContext impactContext)
    {
        if (triggeringEventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(triggeringEventId));
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source ID is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(reactionId))
        {
            throw new ArgumentException(
                "Reaction ID is required.",
                nameof(reactionId));
        }
        if (string.IsNullOrWhiteSpace(deduplicationKey))
        {
            throw new ArgumentException(
                "A reaction deduplication key is required.",
                nameof(deduplicationKey));
        }
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));

        TriggeringEventId = triggeringEventId;
        SourceId = sourceId.Trim();
        ReactionId = reactionId.Trim();
        DeduplicationKey = deduplicationKey.Trim();
        Priority = priority;
        ActionSpec = actionSpec ??
            throw new ArgumentNullException(nameof(actionSpec));
        TargetRuntimeId = targetRuntimeId;
        ImpactContext = impactContext;
    }

    public long TriggeringEventId { get; }
    public string SourceId { get; }
    public string ReactionId { get; }
    public string DeduplicationKey { get; }
    public int Priority { get; }
    public DRPGResolvedActionSpec ActionSpec { get; }
    public int ActorRuntimeId => ActionSpec.ActorRuntimeId;
    public int TargetRuntimeId { get; }
    public DRPGBattleImpactContext ImpactContext { get; }
}

public sealed class DRPGBattleReactionOptions
{
    public DRPGBattleReactionOptions(
        int maxReactionCount = 32,
        int maxDepth = 8)
    {
        if (maxReactionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxReactionCount));
        }
        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));

        MaxReactionCount = maxReactionCount;
        MaxDepth = maxDepth;
    }

    public int MaxReactionCount { get; }
    public int MaxDepth { get; }
}

public enum DRPGBattleReactionTraceState
{
    Resolved = 0,
    SkippedDuplicate = 1,
    SkippedDepthLimit = 2,
    SkippedBudgetLimit = 3,
    SkippedInvalidParticipant = 4,
    SourceFaulted = 5,
    ResolutionFaulted = 6
}

public sealed class DRPGBattleReactionTraceEntry
{
    public DRPGBattleReactionTraceEntry(
        long eventId,
        int depth,
        string sourceId,
        string reactionId,
        DRPGBattleReactionTraceState state,
        string message = "")
    {
        EventId = Math.Max(0L, eventId);
        Depth = Math.Max(0, depth);
        SourceId = sourceId ?? string.Empty;
        ReactionId = reactionId ?? string.Empty;
        State = state;
        Message = message ?? string.Empty;
    }

    public long EventId { get; }
    public int Depth { get; }
    public string SourceId { get; }
    public string ReactionId { get; }
    public DRPGBattleReactionTraceState State { get; }
    public string Message { get; }
}

public sealed class DRPGBattleReactionResolution
{
    public DRPGBattleReactionResolution(
        DRPGBattleEvent triggeringEvent,
        DRPGBattleReactionRequest request,
        DRPGResolvedActionBatch batch)
    {
        TriggeringEvent = triggeringEvent ??
            throw new ArgumentNullException(nameof(triggeringEvent));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Batch = batch ?? throw new ArgumentNullException(nameof(batch));
    }

    public DRPGBattleEvent TriggeringEvent { get; }
    public DRPGBattleReactionRequest Request { get; }
    public DRPGResolvedActionBatch Batch { get; }
}

public sealed class DRPGBattleReactionRun
{
    public DRPGBattleReactionRun(
        DRPGBattleEventBatch rootBatch,
        IReadOnlyList<DRPGBattleEventBatch> batches,
        IReadOnlyList<DRPGBattleReactionResolution> resolutions,
        IReadOnlyList<DRPGBattleReactionTraceEntry> trace)
    {
        RootBatch = rootBatch ??
            throw new ArgumentNullException(nameof(rootBatch));
        Batches = CopyBatches(rootBatch, batches);

        RootActionId = rootBatch.RootActionId;
        Events = FlattenEvents(Batches);
        Resolutions = Copy(resolutions);
        Trace = Copy(trace);
    }

    public long RootActionId { get; }
    public DRPGBattleEventBatch RootBatch { get; }
    public IReadOnlyList<DRPGBattleEventBatch> Batches { get; }
    public IReadOnlyList<DRPGBattleEvent> Events { get; }
    public IReadOnlyList<DRPGBattleReactionResolution> Resolutions { get; }
    public IReadOnlyList<DRPGBattleReactionTraceEntry> Trace { get; }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<T>();
        var copy = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<DRPGBattleEventBatch> CopyBatches(
        DRPGBattleEventBatch rootBatch,
        IReadOnlyList<DRPGBattleEventBatch> source)
    {
        if (source == null || source.Count == 0)
            return Array.AsReadOnly(new[] { rootBatch });

        var copy = new DRPGBattleEventBatch[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            DRPGBattleEventBatch batch = source[i];
            if (batch == null || batch.RootActionId != rootBatch.RootActionId)
            {
                throw new ArgumentException(
                    "Reaction batches must share one root action.",
                    nameof(source));
            }
            copy[i] = batch;
        }
        if (!ReferenceEquals(copy[0], rootBatch))
        {
            throw new ArgumentException(
                "The exact root batch must be first.",
                nameof(source));
        }
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<DRPGBattleEvent> FlattenEvents(
        IReadOnlyList<DRPGBattleEventBatch> batches)
    {
        int count = 0;
        for (int i = 0; i < batches.Count; i++)
            count += batches[i].Events.Count;
        if (count == 0)
            return Array.Empty<DRPGBattleEvent>();

        var result = new DRPGBattleEvent[count];
        int cursor = 0;
        for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            IReadOnlyList<DRPGBattleEvent> events = batches[batchIndex].Events;
            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                result[cursor++] = events[eventIndex];
        }
        return Array.AsReadOnly(result);
    }
}
