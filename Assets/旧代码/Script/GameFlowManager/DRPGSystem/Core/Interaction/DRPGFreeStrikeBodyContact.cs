using System;
using UnityEngine;

/// <summary>
/// A normalized full-body contact produced by a free-strike input.
/// Spatial weak points and breakable parts intentionally do not belong here.
/// </summary>
public readonly struct DRPGFreeStrikeBodyContact
{
    public DRPGFreeStrikeBodyContact(
        int targetRuntimeId,
        float traceDistance,
        Vector2 normalizedHitPoint,
        float accuracy,
        int contactOrder,
        DRPGFreeStrikeImpactRole role)
    {
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));
        if (traceDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(traceDistance));
        if (contactOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(contactOrder));

        TargetRuntimeId = targetRuntimeId;
        TraceDistance = traceDistance;
        NormalizedHitPoint = normalizedHitPoint;
        Accuracy = Mathf.Clamp01(accuracy);
        ContactOrder = contactOrder;
        Role = role;
    }

    public int TargetRuntimeId { get; }
    public float TraceDistance { get; }
    public Vector2 NormalizedHitPoint { get; }
    public float Accuracy { get; }
    public int ContactOrder { get; }
    public DRPGFreeStrikeImpactRole Role { get; }

    // Kept as an explicit invariant so diagnostics can assert that the legacy
    // spatial weak-point protocol never leaks back into body contacts.
    public bool HasSpatialWeakPointMetadata => false;

    public DRPGFreeStrikeBodyContact WithContactOrder(int value)
    {
        return new DRPGFreeStrikeBodyContact(
            TargetRuntimeId,
            TraceDistance,
            NormalizedHitPoint,
            Accuracy,
            value,
            Role);
    }

    public DRPGFreeStrikeBodyContact AsSecondary(int order)
    {
        return new DRPGFreeStrikeBodyContact(
            TargetRuntimeId,
            TraceDistance,
            NormalizedHitPoint,
            Accuracy,
            order,
            DRPGFreeStrikeImpactRole.Secondary);
    }
}
