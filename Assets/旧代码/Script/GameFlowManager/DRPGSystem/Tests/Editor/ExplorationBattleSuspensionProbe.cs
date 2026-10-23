#if UNITY_EDITOR
using System;
using UnityEngine;

public static class ExplorationBattleSuspensionProbe
{
    public static string Run()
    {
        var authority = new CharacterControlAuthority();
        bool paused = false;
        var pauseTarget = new FakePauseTarget();
        var suspension = new ExplorationBattleSuspension(
            authority,
            () => paused,
            value => paused = value,
            new IDRPGExplorationPauseTarget[] { pauseTarget });

        float timeScaleBefore = Time.timeScale;
        AssertTrue(suspension.TryAcquire(out IDisposable lease),
            "The first battle suspension lease must be acquired");
        AssertTrue(paused, "Exploration flow must be marked paused");
        AssertTrue(pauseTarget.IsPaused,
            "Optional exploration pause targets must be suspended");
        AssertFalse(authority.IsAllowed(CharacterControlChannel.Move),
            "Movement must be blocked during battle");
        AssertFalse(authority.IsAllowed(CharacterControlChannel.PrimaryAttack),
            "Exploration attacks must be blocked during battle");
        AssertEqual(timeScaleBefore, Time.timeScale,
            "DRPG battle suspension must not change Time.timeScale");
        AssertFalse(suspension.TryAcquire(out _),
            "A second concurrent suspension lease must be rejected");

        lease.Dispose();
        lease.Dispose();
        AssertFalse(paused, "Exploration pause state must be restored once");
        AssertFalse(pauseTarget.IsPaused,
            "Optional pause targets must be restored once");
        AssertTrue(authority.IsAllowed(CharacterControlChannel.Move),
            "Movement authority must be restored after battle");

        return "PASS exploration battle suspension";
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

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private sealed class FakePauseTarget : IDRPGExplorationPauseTarget
    {
        public bool IsPaused { get; private set; }

        public void SetBattlePaused(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
#endif
