using UnityEngine;

public static class GameRenderParameters
{
    public static bool HasWorldEffect(GameRenderProfile profile)
    {
        return profile != null && profile.enableWorldStyle &&
               (profile.debugClassification ||
                Clamp(profile.inkStrength, 0f, 1f) > 0f ||
                Clamp(profile.highlightLift, 0f, 1f) > 0f);
    }

    public static bool HasGrainEffect(GameRenderProfile profile)
    {
        return profile != null && profile.enableGrain &&
               Clamp(profile.grainStrength, 0f, 0.12f) > 0f;
    }

    public static float Resolve(float globalValue, bool useOverride, float localValue)
    {
        return useOverride && IsFinite(localValue) ? localValue : globalValue;
    }

    public static float Clamp(float value, float minimum, float maximum)
    {
        return IsFinite(value) ? Mathf.Clamp(value, minimum, maximum) : minimum;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
