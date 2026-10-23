#if UNITY_EDITOR
using System;

public static class DRPGEncounterPointProbe
{
    public static string Run()
    {
        CrossingSamplesCatchThinAuthoredPointWithoutDuplicateEntry();

        var point = new DRPGEncounterPointState();
        if (!point.TryEnter(true))
            throw new InvalidOperationException("First valid entry must start.");
        point.Finish(false);
        if (point.TryEnter(true))
            throw new InvalidOperationException("Retry requires leaving the point.");
        point.Leave();
        if (!point.TryEnter(true))
            throw new InvalidOperationException("Re-entry after leaving must retry.");
        point.Finish(true);
        point.Leave();
        if (point.TryEnter(true))
            throw new InvalidOperationException("A victorious point stays complete.");

        var bounds = new DRPGNodeBattleBounds(-10f, 10f);
        if (!bounds.ContainsBattle(0f, 4f, 5f) ||
            bounds.ContainsBattle(8f, 4f, 5f))
        {
            throw new InvalidOperationException(
                "Battle bounds must include the entire frozen composition.");
        }
        return "PASS encounter retry and edge-safe bounds";
    }

    private static void CrossingSamplesCatchThinAuthoredPointWithoutDuplicateEntry()
    {
        var traversal = new DRPGEncounterPointTraversal();
        DRPGEncounterPointSample before = traversal.Sample(-2f, 0f, .25f);
        if (before.ShouldEnter || before.Left)
            throw new InvalidOperationException("Approaching a point must not trigger early.");

        DRPGEncounterPointSample crossed = traversal.Sample(2f, 0f, .25f);
        if (!crossed.ShouldEnter || crossed.Left)
        {
            throw new InvalidOperationException(
                "A party root crossing a thin point between frames must enter exactly once.");
        }

        DRPGEncounterPointSample clear = traversal.Sample(2.5f, 0f, .25f);
        if (clear.ShouldEnter || !clear.Left)
            throw new InvalidOperationException("The point must re-arm only after leaving.");

        DRPGEncounterPointSample stillClear = traversal.Sample(3f, 0f, .25f);
        if (stillClear.ShouldEnter || stillClear.Left)
            throw new InvalidOperationException("Remaining outside must not pulse leave repeatedly.");

        DRPGEncounterPointSample returned = traversal.Sample(0f, 0f, .25f);
        if (!returned.ShouldEnter || returned.Left)
            throw new InvalidOperationException("Returning to the authored band must enter again.");
    }
}
#endif
