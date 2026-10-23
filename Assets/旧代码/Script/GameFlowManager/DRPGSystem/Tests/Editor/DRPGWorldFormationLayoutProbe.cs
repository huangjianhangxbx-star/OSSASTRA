#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class DRPGWorldFormationLayoutProbe
{
    public static string Run()
    {
        VerifyCenteringAndMixedWidths();
        VerifySixSlotSideOnLayout();
        VerifyLogicalRowsShareOneUniqueVisualSlotSequence();
        return "PASS DRPG world formation layout";
    }

    private static void VerifyCenteringAndMixedWidths()
    {
        IReadOnlyList<DRPGWorldFormationPlacement> single =
            DRPGWorldFormationLayoutCalculator.Calculate(
                new[]
                {
                    new DRPGWorldFormationItem(
                        1, DRPGFormationRow.Front, 0, 1f)
                },
                new Vector3(3f, 0f, 0f));
        AssertNear(3f, single[0].WorldPosition.x,
            "One actor must be centered on its row anchor");

        IReadOnlyList<DRPGWorldFormationPlacement> mixed =
            DRPGWorldFormationLayoutCalculator.Calculate(
                new[]
                {
                    new DRPGWorldFormationItem(
                        2, DRPGFormationRow.Front, 0, 5f),
                    new DRPGWorldFormationItem(
                        3, DRPGFormationRow.Front, 1, 1f)
                },
                Vector3.zero,
                availableWidth: 20f,
                baseWidth: 1f,
                spacing: 0.25f);
        float left = mixed[0].WorldPosition.x - mixed[0].ResolvedWidth * 0.5f;
        float right = mixed[1].WorldPosition.x + mixed[1].ResolvedWidth * 0.5f;
        AssertNear(0f, left + right,
            "Mixed-width actors must remain centered as a group");
        AssertTrue(mixed[1].WorldPosition.x > mixed[0].WorldPosition.x,
            "Authored slot order must remain left-to-right");
    }

    private static void VerifySixSlotSideOnLayout()
    {
        var items = new List<DRPGWorldFormationItem>();
        for (int row = 0; row < 3; row++)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                items.Add(new DRPGWorldFormationItem(
                    row * 2 + slot + 10,
                    (DRPGFormationRow)row,
                    slot,
                    1f));
            }
        }

        IReadOnlyList<DRPGWorldFormationPlacement> result =
            DRPGWorldFormationLayoutCalculator.Calculate(
                items,
                Vector3.zero,
                rowDepthStep: 2f);
        AssertEqual(6, result.Count,
            "The DD1-style active stage must expose exactly six enemy slots");
        DRPGWorldFormationPlacement front = Find(result, 10);
        DRPGWorldFormationPlacement middle = Find(result, 12);
        DRPGWorldFormationPlacement rear = Find(result, 14);
        AssertTrue(front.WorldPosition.z < middle.WorldPosition.z &&
                   middle.WorldPosition.z < rear.WorldPosition.z,
            "Formation rows must occupy distinct world depth");
        AssertTrue(front.UniformScale > middle.UniformScale &&
                   middle.UniformScale > rear.UniformScale,
            "Rear actors must be visually smaller than front actors");
        AssertTrue(front.SortingOrder > middle.SortingOrder &&
                   middle.SortingOrder > rear.SortingOrder,
            "Front-row art must sort over rear-row art");
        AssertTrue(Mathf.Abs(front.WorldPosition.z - rear.WorldPosition.z) <= 4.01f,
            "The three rows must use shallow perspective rather than a deep scattered field");
        AssertNoHorizontalOverlap(result,
            "Six enemy actors must remain individually readable in the shared side-on stage");
    }

    private static void VerifyLogicalRowsShareOneUniqueVisualSlotSequence()
    {
        IReadOnlyList<DRPGWorldFormationPlacement> result =
            DRPGWorldFormationLayoutCalculator.Calculate(
                new[]
                {
                    new DRPGWorldFormationItem(
                        31, DRPGFormationRow.Front, 0, 1f),
                    new DRPGWorldFormationItem(
                        32, DRPGFormationRow.Middle, 0, 1f),
                    new DRPGWorldFormationItem(
                        33, DRPGFormationRow.Rear, 0, 1f)
                },
                Vector3.zero,
                availableWidth: 8f,
                spacing: 0.75f,
                rowHorizontalStep: 0.42f);

        AssertEqual(3, result.Count,
            "All logical rows must project into the visual stage");
        AssertNoHorizontalOverlap(result,
            "Logical rows are combat data and must not reuse overlapping visual slots");
    }

    private static void AssertNoHorizontalOverlap(
        IReadOnlyList<DRPGWorldFormationPlacement> placements,
        string message)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            for (int j = i + 1; j < placements.Count; j++)
            {
                float minimumGap =
                    (placements[i].ResolvedWidth +
                     placements[j].ResolvedWidth) * 0.5f;
                float actualGap = Mathf.Abs(
                    placements[i].WorldPosition.x -
                    placements[j].WorldPosition.x);
                if (actualGap + 0.001f < minimumGap)
                {
                    throw new InvalidOperationException(
                        $"{message}. Pair=({placements[i].RuntimeId}," +
                        $"{placements[j].RuntimeId}), MinimumGap={minimumGap}, " +
                        $"ActualGap={actualGap}");
                }
            }
        }
    }

    private static DRPGWorldFormationPlacement Find(
        IReadOnlyList<DRPGWorldFormationPlacement> placements,
        int runtimeId)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            if (placements[i].RuntimeId == runtimeId)
                return placements[i];
        }
        throw new InvalidOperationException($"Missing placement {runtimeId}");
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(message);
    }
}
#endif
