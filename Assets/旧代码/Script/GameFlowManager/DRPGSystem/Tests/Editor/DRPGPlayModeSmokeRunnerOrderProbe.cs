#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGPlayModeSmokeRunnerOrderProbe
{
    private const string EnteringPhase = "entering";
    private const string FoundationPassedKey =
        "ANKALUTE.DRPG.FoundationPreflightPassed";
    private const string FoundationSummaryKey =
        "ANKALUTE.DRPG.FoundationPreflightSummary";
    private const string SessionPhaseKey =
        "ANKALUTE.DRPG.PlayModeSmoke.Phase.v11";
    private const string BatchExitKey =
        "ANKALUTE.DRPG.PlayModeSmoke.BatchExit.v1";
    private const string FailureExitKey =
        "ANKALUTE.DRPG.PlayModeSmoke.Failed.v1";
    private const string ProbeArmedKey =
        "ANKALUTE.DRPG.PlayModeSmokeRunnerOrderProbe.Armed.v1";
    private const string ProbeTerminalKey =
        "ANKALUTE.DRPG.PlayModeSmokeRunnerOrderProbe.Terminal.v1";
    private const string ProbeResultKey =
        "ANKALUTE.DRPG.PlayModeSmokeRunnerOrderProbe.Result.v1";
    private const string PassingSummaryPrefix = "[DRPGProbes] PASS (";
    private const string RuntimeHostProbeName =
        "battle world runtime host uniqueness";
    private const double WatchdogTimeoutSeconds = 30d;

    private static double watchdogDeadline;

    private readonly struct BoundarySnapshot
    {
        public BoundarySnapshot(
            string phase,
            bool transitionRequested,
            bool foundationPassed,
            string foundationSummary)
        {
            Phase = phase;
            TransitionRequested = transitionRequested;
            FoundationPassed = foundationPassed;
            FoundationSummary = foundationSummary;
        }

        public string Phase { get; }
        public bool TransitionRequested { get; }
        public bool FoundationPassed { get; }
        public string FoundationSummary { get; }
    }

    private readonly struct ValidatorNegativeCase
    {
        public ValidatorNegativeCase(
            string name,
            BoundarySnapshot snapshot,
            string expectedFailure)
        {
            Name = name;
            Snapshot = snapshot;
            ExpectedFailure = expectedFailure;
        }

        public string Name { get; }
        public BoundarySnapshot Snapshot { get; }
        public string ExpectedFailure { get; }
    }

    private readonly struct WatchdogSnapshot
    {
        public WatchdogSnapshot(
            double now,
            double deadline,
            bool transitionRequested,
            bool productionFailure)
        {
            Now = now;
            Deadline = deadline;
            TransitionRequested = transitionRequested;
            ProductionFailure = productionFailure;
        }

        public double Now { get; }
        public double Deadline { get; }
        public bool TransitionRequested { get; }
        public bool ProductionFailure { get; }
    }

    [MenuItem("Tools/ANKALUTE/DRPG/Run PlayMode Smoke Runner Order Probe")]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Runner-order probe must start from a stable EditMode state.");
        }

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= OnEditorUpdate;
        ClearSmokeSessionState();
        SessionState.EraseBool(ProbeArmedKey);
        SessionState.EraseBool(ProbeTerminalKey);
        SessionState.SetString(ProbeResultKey, "RUNNING");
        SessionState.SetBool(ProbeArmedKey, true);
        watchdogDeadline = EditorApplication.timeSinceStartup +
            WatchdogTimeoutSeconds;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnEditorUpdate;

        try
        {
            DRPGPlayModeSmokeTestRunner.RunFromMenu();
        }
        catch (Exception exception)
        {
            CompleteProbe(
                "FAIL PlayMode smoke threw before ExitingEditMode: " +
                exception.Message);
            Debug.LogException(exception);
        }

        if (!SessionState.GetBool(ProbeTerminalKey, false))
        {
            Debug.Log(
                "[DRPGPlayModeSmokeRunnerOrderProbe] ARMED; " +
                "poll PollResult().");
        }
    }

    public static string PollResult()
    {
        string result = SessionState.GetString(
            ProbeResultKey,
            "NOT_STARTED");
        return SessionState.GetBool(ProbeTerminalKey, false)
            ? result
            : "PENDING " + result;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode ||
            !SessionState.GetBool(ProbeArmedKey, false))
        {
            return;
        }

        BoundarySnapshot snapshot = new BoundarySnapshot(
            SessionState.GetString(SessionPhaseKey, string.Empty),
            EditorApplication.isPlayingOrWillChangePlaymode,
            SessionState.GetBool(FoundationPassedKey, false),
            SessionState.GetString(FoundationSummaryKey, string.Empty));
        string failures = ValidateBoundary(snapshot);
        string validatorFailure = ValidateValidatorNegativeTable();
        if (!string.IsNullOrEmpty(validatorFailure))
        {
            failures = AppendFailure(
                failures,
                "validator negative table: " + validatorFailure);
        }
        string watchdogFailure = ValidateWatchdogNegativeTable();
        if (!string.IsNullOrEmpty(watchdogFailure))
        {
            failures = AppendFailure(
                failures,
                "watchdog negative table: " + watchdogFailure);
        }

        string result = string.IsNullOrEmpty(failures)
            ? "PASS PlayMode smoke foundation receipt observed at " +
                "ExitingEditMode"
            : "FAIL PlayMode smoke foundation receipt at ExitingEditMode: " +
                failures;
        CompleteProbe(result);

        if (string.IsNullOrEmpty(failures))
            Debug.Log("[DRPGPlayModeSmokeRunnerOrderProbe] " + result);
        else
            Debug.LogError("[DRPGPlayModeSmokeRunnerOrderProbe] " + result);
    }

    private static void OnEditorUpdate()
    {
        if (!SessionState.GetBool(ProbeArmedKey, false))
        {
            EditorApplication.update -= OnEditorUpdate;
            return;
        }

        WatchdogSnapshot snapshot = new WatchdogSnapshot(
            EditorApplication.timeSinceStartup,
            watchdogDeadline,
            EditorApplication.isPlayingOrWillChangePlaymode,
            SessionState.GetBool(FailureExitKey, false));
        string failure = EvaluateWatchdog(snapshot);
        if (string.IsNullOrEmpty(failure))
            return;

        string result = "FAIL PlayMode smoke did not reach ExitingEditMode: " +
            failure;
        CompleteProbe(result);
        Debug.LogError("[DRPGPlayModeSmokeRunnerOrderProbe] " + result);
    }

    private static string AppendFailure(string current, string failure)
    {
        return string.IsNullOrEmpty(current)
            ? failure
            : current + " | " + failure;
    }

    private static string ValidateValidatorNegativeTable()
    {
        const string validSummary =
            "[DRPGProbes] PASS (1/1)\n" +
            "PASS battle world runtime host uniqueness";
        ValidatorNegativeCase[] cases =
        {
            new ValidatorNegativeCase(
                "arbitrary summary",
                new BoundarySnapshot(
                    EnteringPhase,
                    true,
                    true,
                    "arbitrary summary"),
                "foundation summary must start with"),
            new ValidatorNegativeCase(
                "play was not requested",
                new BoundarySnapshot(
                    EnteringPhase,
                    false,
                    true,
                    validSummary),
                "isPlayingOrWillChangePlaymode must be true"),
            new ValidatorNegativeCase(
                "wrong phase",
                new BoundarySnapshot(
                    "running",
                    true,
                    true,
                    validSummary),
                "phase must be entering")
        };

        for (int index = 0; index < cases.Length; index++)
        {
            ValidatorNegativeCase current = cases[index];
            string failure = ValidateBoundary(current.Snapshot);
            if (string.IsNullOrEmpty(failure) || failure.IndexOf(
                    current.ExpectedFailure,
                    StringComparison.Ordinal) < 0)
            {
                return current.Name + " was accepted";
            }
        }

        return string.Empty;
    }

    private static string ValidateBoundary(BoundarySnapshot snapshot)
    {
        string failures = string.Empty;
        if (!string.Equals(
                snapshot.Phase,
                EnteringPhase,
                StringComparison.Ordinal))
        {
            failures = AppendFailure(
                failures,
                "phase must be entering, actual=" +
                (string.IsNullOrEmpty(snapshot.Phase)
                    ? "<empty>"
                    : snapshot.Phase));
        }
        if (!snapshot.TransitionRequested)
        {
            failures = AppendFailure(
                failures,
                "isPlayingOrWillChangePlaymode must be true");
        }
        if (!snapshot.FoundationPassed)
        {
            failures = AppendFailure(
                failures,
                "FoundationPreflightPassed must be true");
        }

        string summary = snapshot.FoundationSummary ?? string.Empty;
        if (!summary.StartsWith(
                PassingSummaryPrefix,
                StringComparison.Ordinal))
        {
            failures = AppendFailure(
                failures,
                "foundation summary must start with " +
                PassingSummaryPrefix);
        }
        if (summary.IndexOf(
                RuntimeHostProbeName,
                StringComparison.Ordinal) < 0)
        {
            failures = AppendFailure(
                failures,
                "foundation summary must include " + RuntimeHostProbeName);
        }

        return failures;
    }

    private static string ValidateWatchdogNegativeTable()
    {
        WatchdogSnapshot[] shouldFail =
        {
            new WatchdogSnapshot(1d, 10d, false, true),
            new WatchdogSnapshot(10d, 10d, false, false)
        };
        for (int index = 0; index < shouldFail.Length; index++)
        {
            if (string.IsNullOrEmpty(EvaluateWatchdog(shouldFail[index])))
                return "terminal no-transition case was accepted at " + index;
        }

        WatchdogSnapshot[] shouldWait =
        {
            new WatchdogSnapshot(1d, 10d, false, false),
            new WatchdogSnapshot(10d, 10d, true, true)
        };
        for (int index = 0; index < shouldWait.Length; index++)
        {
            if (!string.IsNullOrEmpty(EvaluateWatchdog(shouldWait[index])))
                return "non-terminal case was rejected at " + index;
        }

        return string.Empty;
    }

    private static string EvaluateWatchdog(WatchdogSnapshot snapshot)
    {
        if (snapshot.TransitionRequested)
            return string.Empty;
        if (snapshot.ProductionFailure)
            return "production preflight terminated in EditMode";
        if (snapshot.Now >= snapshot.Deadline)
        {
            return "no PlayMode transition was requested within " +
                WatchdogTimeoutSeconds + " seconds";
        }
        return string.Empty;
    }

    private static void CompleteProbe(string result)
    {
        SessionState.SetString(ProbeResultKey, result ?? string.Empty);
        SessionState.SetBool(ProbeTerminalKey, true);
        SessionState.EraseBool(ProbeArmedKey);
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= OnEditorUpdate;
        watchdogDeadline = 0d;
        ClearSmokeSessionState();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.ExitPlaymode();
    }

    private static void ClearSmokeSessionState()
    {
        SessionState.EraseBool(FoundationPassedKey);
        SessionState.EraseString(FoundationSummaryKey);
        SessionState.EraseString(SessionPhaseKey);
        SessionState.EraseBool(BatchExitKey);
        SessionState.EraseBool(FailureExitKey);
    }
}
#endif
