using UnityEngine;

/// <summary>
/// Resolves whether planar movement is inside the rear sector of the aim direction.
/// Separate enter/release thresholds prevent animation chatter near the sector edge.
/// </summary>
public static class TopDownBackpedalPolicy
{
    private const float EnterAlignmentThreshold = -0.5f;
    private const float ReleaseAlignmentThreshold = -0.3f;
    private const float DirectionEpsilonSqr = 0.0001f;

    public static bool Resolve(
        Vector2 moveDirection,
        Vector2 aimDirection,
        bool wasBackpedaling)
    {
        if (moveDirection.sqrMagnitude <= DirectionEpsilonSqr ||
            aimDirection.sqrMagnitude <= DirectionEpsilonSqr)
        {
            return false;
        }

        float alignment = Vector2.Dot(
            moveDirection.normalized,
            aimDirection.normalized);

        return wasBackpedaling
            ? alignment < ReleaseAlignmentThreshold
            : alignment <= EnterAlignmentThreshold;
    }
}
