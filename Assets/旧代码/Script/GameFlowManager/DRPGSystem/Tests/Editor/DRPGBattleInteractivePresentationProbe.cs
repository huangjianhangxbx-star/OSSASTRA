#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleInteractivePresentationProbe
{
    public static string Run()
    {
        Type viewType = Type.GetType(
            "BattleInteractiveCombatView, Assembly-CSharp");
        AssertNotNull(viewType,
            "Removing the shared interactive view would split defense and free-strike UI lifecycles");
        AssertMethod(viewType, "ShowDefense");
        AssertMethod(viewType, "ShowFreeStrike");
        AssertMethod(viewType, "UpdateWindow");
        MethodInfo strikeSample = viewType.GetMethod(
            "TryCreateStrikeSample",
            new[]
            {
                typeof(Vector2),
                typeof(DRPGResolvedFreeStrikeSpec),
                typeof(DRPGFreeStrikeSample).MakeByRefType(),
                typeof(DRPGFreeStrikeBodyContact[]).MakeByRefType()
            });
        AssertNotNull(strikeSample,
            "The interaction view must consume one resolved spec and return domain contacts");
        AssertMethod(viewType, "ShowGuardResult");
        AssertMethod(viewType, "ShowStrikeResult");
        AssertMethod(viewType, "HideImmediate");

        MethodInfo configure = typeof(DRPGBattleActionTransactionCoordinator)
            .GetMethod("ConfigureInteractiveView");
        AssertNotNull(configure,
            "The transaction owner must receive the interaction view explicitly");
        PropertyInfo surfaceView = typeof(DRPGBattleUGUISurface)
            .GetProperty("InteractiveCombatView");
        AssertNotNull(surfaceView,
            "The UGUI surface must expose its single interaction view to composition");

        return "PASS DRPG interactive presentation interface";
    }

    private static void AssertMethod(Type type, string name)
    {
        AssertNotNull(type.GetMethod(name),
            $"The shared interaction view is missing {name}");
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new Exception(message);
    }
}
#endif
