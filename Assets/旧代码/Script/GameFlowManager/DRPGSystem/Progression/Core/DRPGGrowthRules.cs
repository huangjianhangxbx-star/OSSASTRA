using System;
using System.Collections.Generic;
using UnityEngine;

public static class DRPGGrowthRules
{
    public const int NormalLevelCap = 80;
    public const int BreakthroughLevelCap = 100;
    public const int AttributeCap = 80;

    private static readonly int[] AttributeKnots = { 0, 5, 10, 20, 40, 60, 80 };
    private static readonly float[] AttributeValues =
        { 0f, 0.04f, 0.10f, 0.25f, 0.53f, 0.78f, 1f };

    public static int ExperienceToNextLevel(int currentLevel)
    {
        if (currentLevel < 1 || currentLevel >= BreakthroughLevelCap)
            return 0;
        if (currentLevel < NormalLevelCap)
        {
            return RoundToTen(
                120f + 30f * currentLevel +
                5f * currentLevel * currentLevel);
        }

        int level79Cost = RoundToTen(120f + 30f * 79f + 5f * 79f * 79f);
        return RoundToTen(
            level79Cost * Mathf.Pow(1.12f, currentLevel - 79));
    }

    public static float EvaluateAttribute(int attributeValue)
    {
        int value = Mathf.Clamp(attributeValue, 0, AttributeCap);
        for (int i = 1; i < AttributeKnots.Length; i++)
        {
            if (value > AttributeKnots[i])
                continue;
            float t = Mathf.InverseLerp(AttributeKnots[i - 1], AttributeKnots[i], value);
            return Mathf.Lerp(AttributeValues[i - 1], AttributeValues[i], t);
        }
        return AttributeValues[AttributeValues.Length - 1];
    }

    public static float EvaluateGrowthDelta(int initialValue, int currentValue)
    {
        return Mathf.Max(0f, EvaluateAttribute(currentValue) - EvaluateAttribute(initialValue));
    }

    public static float BreakthroughDurabilityMultiplier(int level)
    {
        return 1f + Mathf.Max(0, level - NormalLevelCap) * 0.0025f;
    }

    public static float BreakthroughPotencyMultiplier(int level)
    {
        return 1f + Mathf.Max(0, level - NormalLevelCap) * 0.0015f;
    }

    private static int RoundToTen(float value)
    {
        return Mathf.Max(10, Mathf.RoundToInt(value / 10f) * 10);
    }
}

public static class DRPGRelativeSpeedPolicy
{
    public static float ResolveDelayMultiplier(float actorSpeed, float opposingMedian)
    {
        float delta = actorSpeed - opposingMedian;
        if (delta <= -10f) return 1.15f;
        if (delta <= -5f) return 1.08f;
        if (delta < 5f) return 1f;
        if (delta < 10f) return 0.94f;
        return 0.88f;
    }

    public static float Median(IReadOnlyList<float> values, float fallback)
    {
        if (values == null || values.Count == 0)
            return fallback;
        var copy = new List<float>(values);
        copy.Sort();
        int middle = copy.Count / 2;
        return copy.Count % 2 == 0
            ? (copy[middle - 1] + copy[middle]) * 0.5f
            : copy[middle];
    }
}
