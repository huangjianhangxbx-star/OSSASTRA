#if UNITY_EDITOR
using System;

public static class DRPGGameLoopProbe
{
    public static string Run()
    {
        var loop = new DRPGGameLoopState();
        AssertEqual(DRPGGameLoopPhase.Title, loop.Phase,
            "The application must open on the title phase");
        AssertFalse(loop.TryBeginBattle(),
            "A battle cannot begin before exploration starts");

        AssertTrue(loop.TryStartExploration(),
            "Start must enter exploration");
        AssertTrue(loop.TryBeginBattle(),
            "Exploration must be allowed to enter battle");
        AssertTrue(loop.TryResolveBattle(
                new DRPGBattleResult(DRPGBattleOutcome.Escaped, 1f),
                null),
            "Escape must resolve the battle");
        AssertEqual(DRPGGameLoopPhase.Exploration, loop.Phase,
            "Escape must return directly to exploration");
        AssertTrue(loop.ActiveSettlement == null,
            "Escape must not create a settlement");

        AssertTrue(loop.TryBeginBattle(),
            "A second encounter must be possible");
        AssertTrue(loop.TryResolveBattle(
                new DRPGBattleResult(DRPGBattleOutcome.Defeat, 2f),
                null),
            "Defeat must resolve the battle");
        AssertEqual(DRPGGameLoopPhase.Title, loop.Phase,
            "Total defeat must return to title");

        AssertTrue(loop.TryStartExploration(),
            "The title start button must work after defeat");
        AssertTrue(loop.TryBeginBattle(),
            "A new battle must be possible after restarting");
        var settlement = new DRPGVictorySettlement(
            Array.Empty<DRPGExperienceAward>(),
            new DRPGLootContainerState());
        AssertTrue(loop.TryResolveBattle(
                new DRPGBattleResult(DRPGBattleOutcome.Victory, 3f),
                settlement),
            "Victory must open settlement");
        AssertEqual(DRPGGameLoopPhase.VictorySettlement, loop.Phase,
            "Victory must remain outside exploration until settlement closes");
        AssertSame(settlement, loop.ActiveSettlement,
            "The exact one-shot settlement must be retained");
        AssertFalse(loop.TryBeginBattle(),
            "Another encounter cannot begin behind settlement UI");
        AssertTrue(loop.TryCompleteSettlement(),
            "Closing settlement must return to exploration");
        AssertEqual(DRPGGameLoopPhase.Exploration, loop.Phase,
            "Settlement completion must restore exploration");

        return "PASS DRPG title exploration battle settlement loop";
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }
}
#endif
