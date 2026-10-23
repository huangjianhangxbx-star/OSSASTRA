#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

internal static class GameRenderParameterProbe
{
    [MenuItem("Tools/ANKALUTE/GameRender/Run Parameter Probe")]
    internal static void Run()
    {
        RequireNear(GameRenderParameters.Resolve(0.3f, false, 0.8f), 0.3f, "inherit global value");
        RequireNear(GameRenderParameters.Resolve(0.3f, true, 0.8f), 0.8f, "override one value");
        RequireNear(GameRenderParameters.Resolve(0.3f, true, float.NaN), 0.3f, "invalid override falls back");
        RequireNear(GameRenderParameters.Clamp(3.0f, 0f, 1f), 1f, "limit excess strength");
        RequireNear(GameRenderParameters.Clamp(float.PositiveInfinity, 0f, 1f), 0f, "invalid strength is neutral");
        var style = ScriptableObject.CreateInstance<GameRenderProfile>();
        try
        {
            style.inkStrength = 0f;
            style.highlightLift = 0f;
            style.grainStrength = 0f;
            if (GameRenderParameters.HasWorldEffect(style) || GameRenderParameters.HasGrainEffect(style))
                throw new Exception("Zero-strength style should not enqueue a pass");
            style.highlightLift = 0.02f;
            if (!GameRenderParameters.HasWorldEffect(style))
                throw new Exception("Highlight alone should enqueue the world pass");
            style.grainStrength = 0.006f;
            if (!GameRenderParameters.HasGrainEffect(style))
                throw new Exception("Grain alone should enqueue the grain pass");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(style);
        }
        Debug.Log("PASS GameRender parameter probe (8/8)");
    }

    private static void RequireNear(float actual, float expected, string caseName)
    {
        if (Math.Abs(actual - expected) > 0.0001f)
            throw new Exception(caseName + ": expected " + expected + ", got " + actual);
    }
}
#endif
