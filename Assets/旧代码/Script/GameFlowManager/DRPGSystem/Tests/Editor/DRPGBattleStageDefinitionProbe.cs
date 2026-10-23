#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleStageDefinitionProbe
{
    public static string Run()
    {
        VerifyCameraDefinitions();
        VerifyStageAnchors();
        return "PASS DRPG perspective stage definitions";
    }

    private static void VerifyCameraDefinitions()
    {
        var clamped = new BattleCameraShotDefinition(
            "close",
            new Vector3(1f, 2f, -4f),
            new Vector3(5f, 10f, 0f),
            200f,
            -2f);
        AssertNear(BattleCameraShotDefinition.MaximumFieldOfView,
            clamped.FieldOfView,
            "FOV must clamp to a safe perspective range");
        AssertNear(0f, clamped.DurationSeconds,
            "Negative shot timing must deterministically clamp to zero");

        BattleCameraSequenceDefinition valid =
            new BattleCameraSequenceDefinition(
                "default",
                new[]
                {
                    new BattleCameraShotDefinition(
                        "default",
                        Vector3.zero,
                        Vector3.zero,
                        45f,
                        0f),
                    clamped
                });
        AssertTrue(valid.TryGetDefault(out BattleCameraShotDefinition shot),
            "A valid sequence must resolve its default shot");
        AssertEqual("default", shot.ShotId,
            "Default-shot lookup must be deterministic");

        AssertThrows(() => new BattleCameraSequenceDefinition(
                "missing",
                new[] { clamped }),
            "A missing default shot must be rejected");
        AssertThrows(() => new BattleCameraSequenceDefinition(
                "same",
                new[]
                {
                    new BattleCameraShotDefinition(
                        "same", Vector3.zero, Vector3.zero, 45f, 0f),
                    new BattleCameraShotDefinition(
                        "same", Vector3.one, Vector3.zero, 50f, 0.2f)
                }),
            "Duplicate camera-shot IDs must be rejected");
    }

    private static void VerifyStageAnchors()
    {
        BattleStageProfileSO profile =
            ScriptableObject.CreateInstance<BattleStageProfileSO>();
        try
        {
            SetField(profile, "anchors", new List<BattleStageAnchorDefinition>
            {
                new BattleStageAnchorDefinition(
                    BattleStageAnchorIds.EnemyFront,
                    new Vector3(0f, 0f, 0f),
                    Vector3.zero),
                new BattleStageAnchorDefinition(
                    BattleStageAnchorIds.EnemyMiddle,
                    new Vector3(0f, 0f, 2f),
                    Vector3.zero),
                new BattleStageAnchorDefinition(
                    BattleStageAnchorIds.EnemyRear,
                    new Vector3(0f, 0f, 4f),
                    Vector3.zero)
            });
            AssertTrue(profile.TryGetAnchor(
                    BattleStageAnchorIds.EnemyRear,
                    out BattleStageAnchorDefinition rear),
                "The rear formation anchor must be authorable independently");
            AssertNear(4f, rear.LocalPosition.z,
                "Stage anchors must preserve authored world depth");

            SetField(profile, "anchors", new List<BattleStageAnchorDefinition>
            {
                new BattleStageAnchorDefinition(
                    "duplicate", Vector3.zero, Vector3.zero),
                new BattleStageAnchorDefinition(
                    "duplicate", Vector3.one, Vector3.zero)
            });
            AssertFalse(profile.TryValidate(out string error),
                "Duplicate anchor IDs must be reported before activation");
            AssertTrue(error.Contains("duplicate"),
                "Stage validation must identify the conflicting anchor");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message);
    }
}
#endif
