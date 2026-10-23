#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGPostureBreakLifecycleProbe
{
    public static string Run()
    {
        DRPGPostureBreakRules rules = DRPGPostureBreakRules.Default;
        AssertNear(0.5f, rules.RecoveryPostureRatio,
            "Default break recovery must restore half posture");
        AssertEqual(3, rules.StabilityOwnActionCount,
            "Default recovery stability must last three completed own actions");

        var runtime = new DRPGPostureBreakRuntime(rules);
        AssertEqual(DRPGPostureBreakState.Stable, runtime.State,
            "A new participant must begin stable");
        AssertFalse(runtime.IsExposed,
            "Stable participants must not count as exposed");

        DRPGPostureBreakTransition entered = runtime.EnterBreak();
        AssertTrue(entered.EnteredBreak,
            "The first zero-posture transition must enter break exactly once");
        AssertEqual(
            DRPGPostureBreakState.ExposedPendingSkip,
            runtime.State,
            "New breaks must wait for the actor's skipped scheduling opportunity");
        AssertTrue(runtime.IsExposed,
            "A participant waiting to skip must expose its weak state");
        AssertNear(1.25f, runtime.IncomingDamageMultiplier,
            "Exposed participants must use the configured damage multiplier");
        AssertNear(1.25f, runtime.IncomingAilmentBuildupMultiplier,
            "Exposed participants must use the configured ailment multiplier");
        AssertFalse(runtime.EnterBreak().EnteredBreak,
            "Repeated zero-posture notifications must not duplicate break entry");

        DRPGPostureBreakTransition skipped = runtime.ResolveOwnTurnStart();
        AssertTrue(skipped.SkipOwnAction,
            "The next own scheduling opportunity after break must be skipped");
        AssertNear(0f, skipped.RestorePostureRatio,
            "Skipping must not recover posture immediately");
        AssertEqual(DRPGPostureBreakState.ExposedAfterSkip, runtime.State,
            "The participant must remain exposed after the skipped opportunity");

        DRPGPostureBreakTransition recovered = runtime.ResolveOwnTurnStart();
        AssertFalse(recovered.SkipOwnAction,
            "The opportunity after the forced skip must remain actionable");
        AssertNear(0.5f, recovered.RestorePostureRatio,
            "The first actionable turn after break must restore configured posture");
        AssertEqual(DRPGPostureBreakState.RecoveryStability, runtime.State,
            "Post-break recovery must enter the temporary stability state");
        AssertEqual(3, runtime.RemainingStabilityOwnActions,
            "Recovery stability must count completed own actions, not turns sampled");
        AssertNear(0.5f, runtime.IncomingPostureDamageMultiplier,
            "Recovery stability must reduce incoming posture damage");

        runtime.CompleteOwnAction();
        AssertEqual(2, runtime.RemainingStabilityOwnActions,
            "Only completed own actions may consume recovery stability");
        runtime.CompleteOwnAction();
        runtime.CompleteOwnAction();
        AssertEqual(DRPGPostureBreakState.Stable, runtime.State,
            "Recovery stability must return to stable after its final action");
        AssertNear(1f, runtime.IncomingPostureDamageMultiplier,
            "Stable participants must receive normal posture damage");

        runtime.EnterBreak();
        runtime.ResolveOwnTurnStart();
        runtime.ResolveOwnTurnStart();
        AssertTrue(runtime.EnterBreak().EnteredBreak,
            "Posture reaching zero during stability must start a fresh break");

        AssertThrows<ArgumentOutOfRangeException>(
            () => new DRPGPostureBreakRules(float.NaN, 3, 0.5f, 1.25f, 1.25f),
            "Non-finite break configuration must be rejected");
        var clamped = new DRPGPostureBreakRules(4f, -2, -1f, -3f, -5f);
        AssertNear(1f, clamped.RecoveryPostureRatio,
            "Recovery ratios must be clamped to a valid ratio");
        AssertEqual(0, clamped.StabilityOwnActionCount,
            "Negative stability durations must clamp to zero");
        AssertNear(0f, clamped.StabilityIncomingPostureDamageMultiplier,
            "Negative damage multipliers must clamp to zero");

        return "PASS DRPG posture-break lifecycle";
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Mathf.Abs(expected - actual) > 0.001f)
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
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

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new Exception(message);
    }
}
#endif
