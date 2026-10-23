using System;
using UnityEngine;

internal static class BattleCloseupGeometry
{
    internal static float PerspectiveFov(float baseFovDegrees, float magnification)
    {
        RequireFiniteRange(baseFovDegrees, 0f, 179f, nameof(baseFovDegrees), false);
        RequireFiniteRange(magnification, 1f, float.MaxValue, nameof(magnification), true);
        float halfRadians = baseFovDegrees * .5f * Mathf.Deg2Rad;
        return 2f * Mathf.Atan(Mathf.Tan(halfRadians) / magnification) * Mathf.Rad2Deg;
    }

    internal static float OrthographicSize(float baseSize, float magnification)
    {
        RequireFiniteRange(baseSize, 0f, float.MaxValue, nameof(baseSize), false);
        RequireFiniteRange(magnification, 1f, float.MaxValue, nameof(magnification), true);
        return baseSize / magnification;
    }

    internal static float DownwardWorldDistance(float worldViewportHeight,
        float battleAreaHeightFraction, float downFraction)
    {
        RequireFiniteRange(worldViewportHeight, 0f, float.MaxValue, nameof(worldViewportHeight), false);
        RequireFiniteRange(battleAreaHeightFraction, 0f, 1f, nameof(battleAreaHeightFraction), true);
        RequireFiniteRange(downFraction, 0f, 1f, nameof(downFraction), true);
        return worldViewportHeight * battleAreaHeightFraction * downFraction;
    }

    private static void RequireFiniteRange(float value, float minimum, float maximum,
        string parameterName, bool includeMinimum)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value > maximum ||
            (includeMinimum ? value < minimum : value <= minimum))
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
