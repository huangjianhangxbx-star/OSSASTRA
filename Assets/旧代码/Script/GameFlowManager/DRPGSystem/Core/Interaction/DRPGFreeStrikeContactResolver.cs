using System;
using System.Collections.Generic;
using UnityEngine;

public enum DRPGFreeStrikeImpactRole
{
    Primary = 0,
    Secondary = 1
}

public sealed class DRPGFreeStrikeTargetSurface
{
    public DRPGFreeStrikeTargetSurface(int targetRuntimeId, Rect bodyBounds)
    {
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));

        TargetRuntimeId = targetRuntimeId;
        BodyBounds = bodyBounds;
    }

    public int TargetRuntimeId { get; }
    public Rect BodyBounds { get; }
}

public static class DRPGFreeStrikeContactResolver
{
    public static DRPGFreeStrikeBodyContact[] Resolve(
        DRPGFreeStrikeSample sample,
        DRPGResolvedFreeStrikeSpec spec,
        IReadOnlyList<DRPGFreeStrikeTargetSurface> targets)
    {
        if (targets == null)
            throw new ArgumentNullException(nameof(targets));
        if (sample.Shape != DRPGFreeStrikeShape.Point &&
            sample.Shape != DRPGFreeStrikeShape.Line)
        {
            throw new NotSupportedException(
                "Free-strike footprint is not implemented: " + sample.Shape);
        }

        var contacts = new List<DRPGFreeStrikeBodyContact>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            DRPGFreeStrikeTargetSurface target = targets[i];
            if (target == null ||
                !DRPGFreeStrikeGeometry.TryIntersectBody(
                    sample,
                    target.BodyBounds,
                    out float traceDistance,
                    out Vector2 hitPoint,
                    out float accuracy))
            {
                continue;
            }

            contacts.Add(new DRPGFreeStrikeBodyContact(
                target.TargetRuntimeId,
                traceDistance,
                hitPoint,
                accuracy,
                0,
                DRPGFreeStrikeImpactRole.Primary));
        }

        contacts.Sort(CompareContacts);
        int count = spec.ContactPolicy == DRPGFreeStrikeContactPolicy.FirstContact
            ? Math.Min(1, contacts.Count)
            : spec.ContactPolicy == DRPGFreeStrikeContactPolicy.LimitedContacts
                ? Math.Min(spec.MaxPrimaryContacts, contacts.Count)
                : contacts.Count;
        var result = new List<DRPGFreeStrikeBodyContact>(count);
        for (int i = 0; i < count; i++)
            result.Add(contacts[i].WithContactOrder(i));

        AppendRadialSplashContacts(result, spec, targets);
        return result.ToArray();
    }

    private static void AppendRadialSplashContacts(
        List<DRPGFreeStrikeBodyContact> accepted,
        DRPGResolvedFreeStrikeSpec spec,
        IReadOnlyList<DRPGFreeStrikeTargetSurface> targets)
    {
        if (accepted.Count == 0 ||
            spec.SecondaryImpactKind != DRPGSecondaryImpactKind.RadialSplash ||
            spec.SecondaryRadius <= 0f ||
            spec.SecondaryDamageMultiplier <= 0f)
        {
            return;
        }

        var primaryIds = new HashSet<int>();
        for (int i = 0; i < accepted.Count; i++)
            primaryIds.Add(accepted[i].TargetRuntimeId);

        var bestByTarget = new Dictionary<int, SecondaryCandidate>();
        for (int sourceIndex = 0; sourceIndex < accepted.Count; sourceIndex++)
        {
            DRPGFreeStrikeBodyContact source = accepted[sourceIndex];
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                DRPGFreeStrikeTargetSurface target = targets[targetIndex];
                if (target == null || primaryIds.Contains(target.TargetRuntimeId))
                    continue;

                Vector2 nearest = new Vector2(
                    Mathf.Clamp(
                        source.NormalizedHitPoint.x,
                        target.BodyBounds.xMin,
                        target.BodyBounds.xMax),
                    Mathf.Clamp(
                        source.NormalizedHitPoint.y,
                        target.BodyBounds.yMin,
                        target.BodyBounds.yMax));
                float distance = Vector2.Distance(
                    source.NormalizedHitPoint,
                    nearest);
                if (distance > spec.SecondaryRadius)
                    continue;

                float halfDiagonal = (target.BodyBounds.size * 0.5f).magnitude;
                float centerDistance = Vector2.Distance(
                    nearest,
                    target.BodyBounds.center);
                float accuracy = halfDiagonal <= 0.000001f
                    ? (centerDistance <= 0.000001f ? 1f : 0f)
                    : 1f - Mathf.Clamp01(centerDistance / halfDiagonal);
                var candidate = new SecondaryCandidate(
                    source.ContactOrder,
                    target.TargetRuntimeId,
                    source.TraceDistance + distance,
                    distance,
                    nearest,
                    accuracy);
                if (!bestByTarget.TryGetValue(
                        target.TargetRuntimeId,
                        out SecondaryCandidate previous) ||
                    CompareSecondaryCandidates(candidate, previous) < 0)
                {
                    bestByTarget[target.TargetRuntimeId] = candidate;
                }
            }
        }

        var secondary = new List<SecondaryCandidate>(bestByTarget.Values);
        secondary.Sort(CompareSecondaryCandidates);
        for (int i = 0; i < secondary.Count; i++)
        {
            SecondaryCandidate candidate = secondary[i];
            accepted.Add(new DRPGFreeStrikeBodyContact(
                candidate.TargetRuntimeId,
                candidate.TraceDistance,
                candidate.HitPoint,
                candidate.Accuracy,
                accepted.Count,
                DRPGFreeStrikeImpactRole.Secondary));
        }
    }

    private static int CompareContacts(
        DRPGFreeStrikeBodyContact left,
        DRPGFreeStrikeBodyContact right)
    {
        int result = left.TraceDistance.CompareTo(right.TraceDistance);
        return result != 0
            ? result
            : left.TargetRuntimeId.CompareTo(right.TargetRuntimeId);
    }

    private static int CompareSecondaryCandidates(
        SecondaryCandidate left,
        SecondaryCandidate right)
    {
        int result = left.SplashDistance.CompareTo(right.SplashDistance);
        if (result != 0)
            return result;
        result = left.SourceOrder.CompareTo(right.SourceOrder);
        return result != 0
            ? result
            : left.TargetRuntimeId.CompareTo(right.TargetRuntimeId);
    }

    private readonly struct SecondaryCandidate
    {
        public SecondaryCandidate(
            int sourceOrder,
            int targetRuntimeId,
            float traceDistance,
            float splashDistance,
            Vector2 hitPoint,
            float accuracy)
        {
            SourceOrder = sourceOrder;
            TargetRuntimeId = targetRuntimeId;
            TraceDistance = traceDistance;
            SplashDistance = splashDistance;
            HitPoint = hitPoint;
            Accuracy = accuracy;
        }

        public int SourceOrder { get; }
        public int TargetRuntimeId { get; }
        public float TraceDistance { get; }
        public float SplashDistance { get; }
        public Vector2 HitPoint { get; }
        public float Accuracy { get; }
    }
}
