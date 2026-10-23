#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBattleCameraFramingProbe
{
    public static string Run()
    {
        var baseShot = new BattleCameraShotDefinition(
            "conflict.default",
            new Vector3(0.8f, 0.95f, -8.2f),
            new Vector3(1f, -4f, 0f),
            36f,
            0.16f);
        Vector3 authoredFocus = new Vector3(0f, -0.7f, 0.5f);

        BattleCameraShotDefinition singleTarget =
            BattleCameraFramingCalculator.CreateFramedShot(
                baseShot,
                authoredFocus,
                new[]
                {
                    new Vector3(-3.1f, -1.55f, 0.8f),
                    new Vector3(2.05f, -1.35f, 0f)
                },
                16f / 9f,
                0.6f,
                0.8f,
                31f,
                0.12f);

        AssertNear(0.275f, singleTarget.LocalPosition.x,
            "Single-target framing must center the attacker and target horizontally");
        AssertNear(0.2f, singleTarget.LocalPosition.y,
            "Single-target framing must preserve the authored focus offset");
        AssertNear(-8.2f, singleTarget.LocalPosition.z,
            "Dynamic framing must preserve the authored camera depth");
        AssertNear(31f, singleTarget.FieldOfView,
            "A compact single-target conflict must use the requested push-in FOV");

        BattleCameraShotDefinition multiTarget =
            BattleCameraFramingCalculator.CreateFramedShot(
                baseShot,
                authoredFocus,
                new[]
                {
                    new Vector3(-3.1f, -1.55f, 0.8f),
                    new Vector3(2.05f, -1.35f, 0f),
                    new Vector3(5f, -1.3f, 0f)
                },
                16f / 9f,
                0.6f,
                0.8f,
                31f,
                0.12f);
        AssertTrue(multiTarget.FieldOfView > singleTarget.FieldOfView,
            "Multi-target framing must widen enough to keep every target visible");
        AssertTrue(multiTarget.FieldOfView <= BattleCameraShotDefinition.MaximumFieldOfView,
            "Dynamic framing must respect the camera FOV safety bound");

        BattleCameraShotDefinition fallback =
            BattleCameraFramingCalculator.CreateFramedShot(
                baseShot,
                authoredFocus,
                Array.Empty<Vector3>(),
                16f / 9f,
                0.6f,
                0.8f,
                31f,
                0.12f);
        AssertNear(baseShot.LocalPosition.x, fallback.LocalPosition.x,
            "Missing actor views must retain the authored static shot");
        AssertNear(31f, fallback.FieldOfView,
            "Missing actor views still honor the recipe's configured FOV");

        return "PASS DRPG battle camera framing";
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
        {
            throw new InvalidOperationException(
                message + $". Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}
#endif
