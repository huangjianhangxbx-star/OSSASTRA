#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGFreeStrikeGeometryProbe
{
    public static string Run()
    {
        Rect body = new Rect(0f, 0f, 1f, 1f);

        VerifyHit(
            new DRPGFreeStrikeSample(
                DRPGFreeStrikeShape.Point,
                new Vector2(0.72f, 0.50f),
                Vector2.right,
                0f,
                0f),
            body,
            true,
            "Point input inside the full body must hit");
        VerifyHit(
            new DRPGFreeStrikeSample(
                DRPGFreeStrikeShape.Point,
                new Vector2(1.20f, 0.50f),
                Vector2.right,
                0f,
                0f),
            body,
            false,
            "Point input outside the full body must miss");
        VerifyHit(
            new DRPGFreeStrikeSample(
                DRPGFreeStrikeShape.Line,
                new Vector2(0.50f, 0.50f),
                Vector2.right,
                0.60f,
                0.08f),
            body,
            true,
            "Line input crossing the full body must hit");
        VerifyHit(
            new DRPGFreeStrikeSample(
                DRPGFreeStrikeShape.Line,
                new Vector2(1.40f, 1.40f),
                Vector2.right,
                0.30f,
                0.04f),
            body,
            false,
            "Line input outside the full body must miss");

        var centerPoint = new DRPGFreeStrikeSample(
            DRPGFreeStrikeShape.Point,
            body.center,
            Vector2.right,
            0f,
            0f);
        Require(DRPGFreeStrikeGeometry.TryIntersectBody(
                centerPoint,
                body,
                out float traceDistance,
                out Vector2 hitPoint,
                out float accuracy),
            "Center point did not produce body contact metadata");
        Require(Mathf.Approximately(traceDistance, 0f),
            "Point input trace distance must remain zero");
        Require(hitPoint == body.center && Mathf.Approximately(accuracy, 1f),
            "Center point must preserve its hit point and maximum accuracy");

        return "PASS body-only DRPG free-strike geometry";
    }

    private static void VerifyHit(
        DRPGFreeStrikeSample sample,
        Rect body,
        bool expected,
        string message)
    {
        bool actual = DRPGFreeStrikeGeometry.IntersectsBody(sample, body);
        Require(actual == expected, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
