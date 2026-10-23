using UnityEngine;

public static class DRPGFreeStrikeGeometry
{
    public static bool TryIntersectBody(
        DRPGFreeStrikeSample sample,
        Rect normalizedBody,
        out float traceDistance,
        out Vector2 hitPoint,
        out float accuracy)
    {
        if (sample.Shape != DRPGFreeStrikeShape.Point &&
            sample.Shape != DRPGFreeStrikeShape.Line)
        {
            throw new System.NotSupportedException(
                "Free-strike footprint is not implemented: " + sample.Shape);
        }

        if (sample.Shape == DRPGFreeStrikeShape.Point)
        {
            traceDistance = 0f;
            hitPoint = sample.NormalizedPoint;
            Vector2 halfSize = normalizedBody.size * 0.5f;
            float halfDiagonal = halfSize.magnitude;
            float centerDistance = Vector2.Distance(
                sample.NormalizedPoint,
                normalizedBody.center);
            accuracy = halfDiagonal <= 0.000001f
                ? (centerDistance <= 0.000001f ? 1f : 0f)
                : 1f - Mathf.Clamp01(centerDistance / halfDiagonal);
            return normalizedBody.Contains(sample.NormalizedPoint);
        }

        float halfLength = sample.LineLength * 0.5f;
        float halfWidth = sample.LineWidth * 0.5f;
        Vector2 offset = sample.NormalizedDirection * halfLength;
        Vector2 start = sample.NormalizedPoint - offset;
        Vector2 end = sample.NormalizedPoint + offset;
        Rect expanded = new Rect(
            normalizedBody.xMin - halfWidth,
            normalizedBody.yMin - halfWidth,
            normalizedBody.width + halfWidth * 2f,
            normalizedBody.height + halfWidth * 2f);
        if (!TrySegmentRectEntry(start, end, expanded, out float entry))
        {
            traceDistance = 0f;
            hitPoint = default;
            accuracy = 0f;
            return false;
        }

        float segmentLength = Vector2.Distance(start, end);
        traceDistance = entry * segmentLength;
        hitPoint = start + (end - start) * entry;
        float halfDiagonalBody = (normalizedBody.size * 0.5f).magnitude;
        float distanceToCenter = Vector2.Distance(hitPoint, normalizedBody.center);
        accuracy = halfDiagonalBody <= 0.000001f
            ? (distanceToCenter <= 0.000001f ? 1f : 0f)
            : 1f - Mathf.Clamp01(distanceToCenter / halfDiagonalBody);
        return true;
    }

    public static bool IntersectsBody(
        DRPGFreeStrikeSample sample,
        Rect normalizedBody)
    {
        return TryIntersectBody(sample, normalizedBody, out _, out _, out _);
    }

    private static bool TrySegmentRectEntry(
        Vector2 start,
        Vector2 end,
        Rect rect,
        out float entry)
    {
        Vector2 direction = end - start;
        float tMin = 0f;
        float tMax = 1f;
        bool hit = Clip(-direction.x, start.x - rect.xMin, ref tMin, ref tMax) &&
            Clip(direction.x, rect.xMax - start.x, ref tMin, ref tMax) &&
            Clip(-direction.y, start.y - rect.yMin, ref tMin, ref tMax) &&
            Clip(direction.y, rect.yMax - start.y, ref tMin, ref tMax);
        entry = hit ? tMin : 0f;
        return hit;
    }

    private static bool Clip(
        float denominator,
        float numerator,
        ref float tMin,
        ref float tMax)
    {
        if (Mathf.Abs(denominator) <= 0.000001f)
            return numerator >= 0f;

        float t = numerator / denominator;
        if (denominator < 0f)
        {
            if (t > tMax)
                return false;
            if (t > tMin)
                tMin = t;
        }
        else
        {
            if (t < tMin)
                return false;
            if (t < tMax)
                tMax = t;
        }
        return true;
    }
}
