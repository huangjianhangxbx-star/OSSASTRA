#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

internal static class DRPGBattleCloseupGeometryProbe
{
    internal static void Run()
    {
        Type geometry = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            geometry = assembly.GetType("BattleCloseupGeometry");
            if (geometry != null) break;
        }
        Check(geometry != null, "BattleCloseupGeometry production type is missing.");

        MethodInfo perspective = Require(geometry, "PerspectiveFov");
        MethodInfo orthographic = Require(geometry, "OrthographicSize");
        MethodInfo downward = Require(geometry, "DownwardWorldDistance");

        float fov = Invoke(perspective, 60f, 1.15f);
        float visualRatio = Mathf.Tan(30f * Mathf.Deg2Rad) /
            Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        Check(Mathf.Abs(visualRatio - 1.15f) < .0001f,
            "Perspective magnification must be expressed through the tangent of half-FOV.");
        Check(Mathf.Abs(Invoke(orthographic, 10f, 1.15f) - 10f / 1.15f) < .0001f,
            "Orthographic magnification must divide the frozen home size.");
        Check(Mathf.Abs(Invoke(downward, 20f, .7f, .03f) - .42f) < .0001f,
            "Downward step-out must be relative to the visible battle region.");

        Check(Mathf.Abs(Invoke(perspective, 47f, 1f) - 47f) < .0001f,
            "Unit perspective magnification must be an identity.");
        Check(Mathf.Abs(Invoke(orthographic, 8f, 1f) - 8f) < .0001f,
            "Unit orthographic magnification must be an identity.");
        Check(Invoke(downward, 20f, .7f, 0f) == 0f,
            "Zero downward fraction must be an identity.");

        float frozenHome = 10f;
        for (int i = 0; i < 20; i++)
            Check(Mathf.Abs(Invoke(orthographic, frozenHome, 1.15f) - frozenHome / 1.15f) < .0001f,
                "Repeated close-ups must calculate from the frozen home, never the previous close-up.");

        ExpectArgument(perspective, 60f, .99f);
        ExpectArgument(perspective, float.NaN, 1.15f);
        ExpectArgument(orthographic, 0f, 1.15f);
        ExpectArgument(downward, 20f, 1.01f, .03f);
        ExpectArgument(downward, 20f, .7f, float.PositiveInfinity);
    }

    private static MethodInfo Require(Type owner, string method)
    {
        MethodInfo result = owner.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
        Check(result != null, "Missing geometry method: " + method + ".");
        return result;
    }

    private static float Invoke(MethodInfo method, params object[] arguments)
    {
        try { return (float)method.Invoke(null, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static void ExpectArgument(MethodInfo method, params object[] arguments)
    {
        bool rejected = false;
        try { Invoke(method, arguments); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, method.Name + " accepted a non-finite or out-of-contract input.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
