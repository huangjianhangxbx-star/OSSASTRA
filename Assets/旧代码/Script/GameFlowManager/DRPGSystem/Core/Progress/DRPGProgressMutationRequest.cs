using System;

public sealed class DRPGProgressMutationRequest
{
    private DRPGProgressMutationRequest(
        string sourceId,
        string deduplicationKey,
        int priority,
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        DRPGProgressMutationKind kind,
        float amount,
        DRPGProgressResourceDefinition definition,
        bool includeReactionDepth)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source ID is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(deduplicationKey))
        {
            throw new ArgumentException(
                "A progress deduplication key is required.",
                nameof(deduplicationKey));
        }
        if (string.IsNullOrWhiteSpace(ownerStableId))
        {
            throw new ArgumentException(
                "A stable owner ID is required.",
                nameof(ownerStableId));
        }
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required.", nameof(resourceId));
        if ((kind == DRPGProgressMutationKind.Gain ||
             kind == DRPGProgressMutationKind.Spend) &&
            (!(amount > 0f) || float.IsNaN(amount) || float.IsInfinity(amount)))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
        if (kind == DRPGProgressMutationKind.Register && definition == null)
            throw new ArgumentNullException(nameof(definition));

        SourceId = sourceId.Trim();
        DeduplicationKey = deduplicationKey.Trim();
        Priority = priority;
        OwnerStableId = ownerStableId.Trim();
        ResourceId = resourceId.Trim();
        Scope = scope;
        Kind = kind;
        Amount = amount;
        Definition = definition;
        IncludeReactionDepth = includeReactionDepth;
    }

    public string SourceId { get; }
    public string DeduplicationKey { get; }
    public int Priority { get; }
    public string OwnerStableId { get; }
    public string ResourceId { get; }
    public DRPGProgressScope Scope { get; }
    public DRPGProgressMutationKind Kind { get; }
    public float Amount { get; }
    public DRPGProgressResourceDefinition Definition { get; }
    public bool IncludeReactionDepth { get; }

    public static DRPGProgressMutationRequest Register(
        string sourceId,
        string deduplicationKey,
        int priority,
        string ownerStableId,
        DRPGProgressResourceDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        return new DRPGProgressMutationRequest(
            sourceId,
            deduplicationKey,
            priority,
            ownerStableId,
            definition.ResourceId,
            definition.Scope,
            DRPGProgressMutationKind.Register,
            0f,
            definition,
            true);
    }

    public static DRPGProgressMutationRequest Gain(
        string sourceId,
        string deduplicationKey,
        int priority,
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        float amount,
        bool includeReactionDepth = false)
    {
        return CreateValueMutation(
            sourceId,
            deduplicationKey,
            priority,
            ownerStableId,
            resourceId,
            scope,
            DRPGProgressMutationKind.Gain,
            amount,
            includeReactionDepth);
    }

    public static DRPGProgressMutationRequest Spend(
        string sourceId,
        string deduplicationKey,
        int priority,
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        float amount,
        bool includeReactionDepth = false)
    {
        return CreateValueMutation(
            sourceId,
            deduplicationKey,
            priority,
            ownerStableId,
            resourceId,
            scope,
            DRPGProgressMutationKind.Spend,
            amount,
            includeReactionDepth);
    }

    public static DRPGProgressMutationRequest Reset(
        string sourceId,
        string deduplicationKey,
        int priority,
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope)
    {
        return new DRPGProgressMutationRequest(
            sourceId,
            deduplicationKey,
            priority,
            ownerStableId,
            resourceId,
            scope,
            DRPGProgressMutationKind.Reset,
            0f,
            null,
            true);
    }

    private static DRPGProgressMutationRequest CreateValueMutation(
        string sourceId,
        string deduplicationKey,
        int priority,
        string ownerStableId,
        string resourceId,
        DRPGProgressScope scope,
        DRPGProgressMutationKind kind,
        float amount,
        bool includeReactionDepth)
    {
        return new DRPGProgressMutationRequest(
            sourceId,
            deduplicationKey,
            priority,
            ownerStableId,
            resourceId,
            scope,
            kind,
            amount,
            null,
            includeReactionDepth);
    }
}

public sealed class DRPGProgressMutationResult
{
    public DRPGProgressMutationResult(
        DRPGProgressMutationRequest request,
        DRPGProgressMutationState state,
        float valueBefore,
        float valueAfter,
        float capacity,
        float appliedAmount,
        float overflowAmount,
        string message = "")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        State = state;
        ValueBefore = valueBefore;
        ValueAfter = valueAfter;
        Capacity = capacity;
        AppliedAmount = appliedAmount;
        OverflowAmount = overflowAmount;
        Message = message ?? string.Empty;
    }

    public DRPGProgressMutationRequest Request { get; }
    public DRPGProgressMutationState State { get; }
    public float ValueBefore { get; }
    public float ValueAfter { get; }
    public float Capacity { get; }
    public float AppliedAmount { get; }
    public float OverflowAmount { get; }
    public string Message { get; }
    public bool WasApplied => State == DRPGProgressMutationState.Applied;

    public static DRPGProgressMutationResult Reject(
        DRPGProgressMutationRequest request,
        DRPGProgressMutationState state,
        string message)
    {
        return new DRPGProgressMutationResult(
            request,
            state,
            0f,
            0f,
            0f,
            0f,
            0f,
            message);
    }
}
