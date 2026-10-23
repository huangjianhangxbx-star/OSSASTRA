using System;

public readonly struct BattlePresentationLaneKey :
    IEquatable<BattlePresentationLaneKey>
{
    public BattlePresentationLaneKey(
        BattlePresentationLaneKind kind,
        int runtimeId = 0)
    {
        Kind = kind;
        RuntimeId = kind == BattlePresentationLaneKind.Participant
            ? runtimeId
            : 0;
    }

    public BattlePresentationLaneKind Kind { get; }
    public int RuntimeId { get; }

    public static BattlePresentationLaneKey From(
        BattlePresentationLaneKind kind,
        BattlePresentationRequest request)
    {
        if (kind != BattlePresentationLaneKind.Participant)
            return new BattlePresentationLaneKey(kind);

        int runtimeId = 0;
        if (request != null && request.Targets.Count > 0 &&
            request.Targets[0] != null)
        {
            runtimeId = request.Targets[0].RuntimeId;
        }
        else if (request != null && request.Hits.Count > 0 &&
            request.Hits[0] != null)
        {
            runtimeId = request.Hits[0].TargetRuntimeId;
        }
        else if (request?.Actor != null)
        {
            runtimeId = request.Actor.RuntimeId;
        }

        return new BattlePresentationLaneKey(kind, runtimeId);
    }

    public bool Equals(BattlePresentationLaneKey other)
    {
        return Kind == other.Kind && RuntimeId == other.RuntimeId;
    }

    public override bool Equals(object obj)
    {
        return obj is BattlePresentationLaneKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Kind * 397) ^ RuntimeId;
        }
    }

    public override string ToString()
    {
        return Kind == BattlePresentationLaneKind.Participant
            ? $"{Kind}:{RuntimeId}"
            : Kind.ToString();
    }
}
