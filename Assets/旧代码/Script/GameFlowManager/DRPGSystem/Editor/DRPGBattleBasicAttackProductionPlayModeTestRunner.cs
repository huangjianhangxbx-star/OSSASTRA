#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal enum DRPGBasicAttackProductionRunStatus
{
    Pending,
    Running,
    Passed,
    Failed,
    WatchdogTimedOut,
    RunnerIncomplete,
    InfrastructureFailed
}

internal sealed class DRPGBasicAttackProductionScenarioReport
{
    internal DRPGBasicAttackProductionScenarioReport(DRPGBasicAttackProductionScenario scenario)
    {
        Scenario = scenario;
        Status = DRPGBasicAttackProductionRunStatus.Pending;
    }

    internal DRPGBasicAttackProductionScenario Scenario { get; private set; }
    internal DRPGBasicAttackProductionRunStatus Status { get; private set; }
    internal string Detail { get; private set; }
    internal bool IsTerminal { get { return Status != DRPGBasicAttackProductionRunStatus.Running; } }
    internal bool Passed { get { return Status == DRPGBasicAttackProductionRunStatus.Passed; } }

    internal void Begin()
    {
        Status = DRPGBasicAttackProductionRunStatus.Running;
        Detail = null;
    }

    internal void Complete(DRPGBasicAttackProductionRunStatus status, string detail)
    {
        Status = status;
        Detail = detail;
    }
}

internal sealed class DRPGBasicAttackProductionScenarioReceipt
{
    internal DRPGBasicAttackProductionScenarioReceipt(DRPGBasicAttackProductionScenarioReport report)
    {
        Report = report;
    }

    internal DRPGBasicAttackProductionScenarioReport Report { get; private set; }
    internal bool IsComplete { get { return Report.IsTerminal; } }
}

internal sealed class DRPGBasicAttackProductionMatrixReport
{
    internal DRPGBasicAttackProductionMatrixReport(IReadOnlyList<DRPGBasicAttackProductionScenarioReport> scenarios)
    {
        Scenarios = scenarios;
    }

    internal IReadOnlyList<DRPGBasicAttackProductionScenarioReport> Scenarios { get; private set; }
    internal int CompletedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Scenarios.Count; i++)
            {
                if (Scenarios[i].Passed)
                    count++;
            }
            return count;
        }
    }

    internal int FailedCount { get { return Count(DRPGBasicAttackProductionRunStatus.Failed); } }
    internal int WatchdogCount { get { return Count(DRPGBasicAttackProductionRunStatus.WatchdogTimedOut); } }
    internal int RunnerIncompleteCount { get { return Count(DRPGBasicAttackProductionRunStatus.RunnerIncomplete); } }
    internal int InfrastructureFailureCount { get { return Count(DRPGBasicAttackProductionRunStatus.InfrastructureFailed); } }
    internal int PendingCount { get { return Count(DRPGBasicAttackProductionRunStatus.Pending); } }
    internal bool AllExpectedScenariosPassed
    {
        get { return Scenarios.Count == DRPGBasicAttackProductionScenarioManifest.FinalExpectedScenarios.Count && CompletedCount == Scenarios.Count; }
    }

    private int Count(DRPGBasicAttackProductionRunStatus status)
    {
        int count = 0;
        for (int i = 0; i < Scenarios.Count; i++)
        {
            if (Scenarios[i].Status == status)
                count++;
        }
        return count;
    }
}

internal sealed class DRPGBasicAttackLegacyRedCaseReceipt
{
    internal DRPGBasicAttackLegacyRedCaseReceipt(string caseId)
    {
        CaseId = caseId;
        Status = DRPGBasicAttackProductionRunStatus.Pending;
    }

    internal string CaseId { get; private set; }
    internal DRPGBasicAttackProductionRunStatus Status { get; private set; }
    internal string Detail { get; private set; }

    internal void Begin() { Status = DRPGBasicAttackProductionRunStatus.Running; Detail = null; }
    internal void Complete(DRPGBasicAttackProductionRunStatus status, string detail) { Status = status; Detail = detail; }
}

[InitializeOnLoad]
public static class DRPGBattleBasicAttackProductionPlayModeTestRunner
{
    private const string SessionPhaseKey = "ANKALUTE.DRPG.BasicAttackProduction.Phase.v1";
    private const string SessionCommandLineKey = "ANKALUTE.DRPG.BasicAttackProduction.CommandLine.v1";
    private const string SessionExitCodeKey = "ANKALUTE.DRPG.BasicAttackProduction.ExitCode.v1";
    private const string SessionSummaryKey = "ANKALUTE.DRPG.BasicAttackProduction.Summary.v1";
    private const string LegacyRedSummaryKey = "ANKALUTE.DRPG.BasicAttackLegacyRed.Summary.v1";
    private const string LegacyRedCapturedKey =
        "ANKALUTE.DRPG.BasicAttackLegacyRed.Captured.v1";
    private const string EnteringPhase = "entering";
    private const string RunningPhase = "running";
    private const string LegacyEnteringPhase = "legacy-entering";
    private const string LegacyRunningPhase = "legacy-running";
    private const string LegacyExitingPhase = "legacy-exiting";
    private const string MatrixMenuExitingPhase = "matrix-menu-exiting";
    private const double ScenarioWatchdogSeconds = 30d;
    private const double ContinuousBattleScenarioWatchdogSeconds = 180d;

    // Task 1 deliberately registers no production scenarios. A scenario can
    // become runnable only by adding its real PlayMode assertion factory here;
    // an enum/string alone can never count as completed.
    private static readonly IDictionary<
        DRPGBasicAttackProductionScenario,
        Func<IEnumerator>> ProductionScenarioRegistry =
        new Dictionary<
            DRPGBasicAttackProductionScenario,
            Func<IEnumerator>>
        {
            { DRPGBasicAttackProductionScenario.ProductionAnnouncementIsolation, DRPGBasicAttackPreludeProductionProbe.ProductionAnnouncementIsolation },
            { DRPGBasicAttackProductionScenario.ProductionWindupMarkerIdentity, DRPGBasicAttackPreludeProductionProbe.ProductionWindupMarkerIdentity },
            { DRPGBasicAttackProductionScenario.ProductionLethalLeaseMatrix, DRPGBasicAttackReturnBarrierProbe.ProductionLethalLeaseMatrix },
            { DRPGBasicAttackProductionScenario.ProductionMarkerMissingRecovers, DRPGBasicAttackPreludeProductionProbe.ProductionMarkerMissingRecovers },
            { DRPGBasicAttackProductionScenario.ProductionLeaseBlocksRebind, DRPGBasicAttackProjectionProbe.ProductionLeaseBlocksRebind },
            { DRPGBasicAttackProductionScenario.ProductionSettlementProjectionIsSerial, DRPGSettlementWidgetsProbe.ProductionWidgets },
            { DRPGBasicAttackProductionScenario.ProductionSettlementNoChainFire, DRPGProductionStartupProbe.ProductionSettlementNoChainFire },
            { DRPGBasicAttackProductionScenario.ProductionParityThroughRealEntrances, DRPGFormalPresentationBindingProbe.ProductionParityThroughRealEntrances },
            { DRPGBasicAttackProductionScenario.ProductionGuardedAndEvadedAreSingleOutcomeBeats, DRPGBasicAttackHitOutcomeProbe.ProductionGuardedAndEvadedAreSingleOutcomeBeats },
            { DRPGBasicAttackProductionScenario.ProductionRecoilContinuityThroughLateUpdate, () => DRPGBattleBasicAttackProductionPresentationProbe.RunCoroutine("RecoilIsPureHorizontalAndRebindSafe") },
            { DRPGBasicAttackProductionScenario.ProductionAttackerNoSnapAtRestoreBoundary, () => DRPGBattleBasicAttackProductionPresentationProbe.RunCoroutine("AttackerRestoresWithoutVisualRootSnap") },
            { DRPGBasicAttackProductionScenario.ProductionSimultaneousReturn, DRPGBasicAttackReturnBarrierProbe.ProductionSimultaneousReturn },
            { DRPGBasicAttackProductionScenario.ProductionImpactFeedbackTimeoutDoesNotRemutate, DRPGBasicAttackProductionWatchdogProbe.ProductionImpactFeedbackTimeoutDoesNotRemutate },
            { DRPGBasicAttackProductionScenario.ProductionReactionTimeoutContinuesSerially, DRPGBasicAttackProductionWatchdogProbe.ProductionReactionTimeoutContinuesSerially },
            { DRPGBasicAttackProductionScenario.ProductionReturnTimeoutSurvivor, DRPGBasicAttackReturnBarrierProbe.ProductionReturnTimeoutSurvivor },
            { DRPGBasicAttackProductionScenario.ProductionReturnTimeoutLethal, DRPGBasicAttackReturnBarrierProbe.ProductionReturnTimeoutLethal },
            { DRPGBasicAttackProductionScenario.ProductionRootSettlementTimeoutStaysSerial, DRPGBasicAttackProductionWatchdogProbe.ProductionRootSettlementTimeoutStaysSerial },
            { DRPGBasicAttackProductionScenario.ProductionTurnEntryTimeoutStaysSerial, DRPGBasicAttackProductionWatchdogProbe.ProductionTurnEntryTimeoutStaysSerial },
            { DRPGBasicAttackProductionScenario.ProductionActorAnchorsFollowMotionRoot, DRPGProductionStartupProbe.ProductionActorAnchorsFollowMotionRoot },
            { DRPGBasicAttackProductionScenario.ProductionWaveEntryAnnouncementTimeout, DRPGBasicAttackProductionWatchdogProbe.ProductionWaveEntryAnnouncementTimeout },
            { DRPGBasicAttackProductionScenario.ProductionWavePartialReadyTimeout, DRPGBasicAttackProductionWatchdogProbe.ProductionWavePartialReadyTimeout },
            { DRPGBasicAttackProductionScenario.ProductionWaveReceiptIdentityMatrix, DRPGBasicAttackProductionWatchdogProbe.ProductionWaveReceiptIdentityMatrix },
            { DRPGBasicAttackProductionScenario.ProductionWaveSceneDisableCleansHandles, DRPGBasicAttackProductionWatchdogProbe.ProductionWaveSceneDisableCleansHandles },
            { DRPGBasicAttackProductionScenario.ProductionWavePlaceholderBatchDeploysAtomically, DRPGFormalPresentationBindingProbe.ProductionWavePlaceholderBatchDeploysAtomically },
            { DRPGBasicAttackProductionScenario.ProductionWaveRegistrationFailureFaultsCleanly, DRPGFormalPresentationBindingProbe.ProductionWaveRegistrationFailureFaultsCleanly }
        };

    private static readonly List<DRPGBasicAttackProductionScenarioReport> Reports =
        new List<DRPGBasicAttackProductionScenarioReport>();
    private static int nextScenarioIndex;
    private static IEnumerator activeCoroutine;
    private static LegacyCoroutineHost activeHost;
    private static DRPGBasicAttackProductionScenarioReport activeReport;
    private static double activeDeadline;
    private static bool observingConsole;
    private static string consoleFailure;
    private static bool singleScenarioRun;
    private static DRPGBasicAttackProductionScenarioReport lastScenarioReport;

    public static string GetLastProductionScenarioReport() => lastScenarioReport == null
        ? "No scenario requested." : lastScenarioReport.Scenario + ": " + lastScenarioReport.Status + " - " + lastScenarioReport.Detail;
    private static readonly string[] LegacyRedCaseIds =
    {
        "LethalTargetRemainsLeasedUntilReturn",
        "RecoilIsPureHorizontalAndRebindSafe",
        "PlayerAndEnemyImpactFeedbackAreSymmetric",
        "AttackerRestoresWithoutVisualRootSnap",
        "PostActionSettlementIsSerialAndBlocksContinuation"
    };
    private static readonly List<DRPGBasicAttackLegacyRedCaseReceipt> LegacyRedReports =
        new List<DRPGBasicAttackLegacyRedCaseReceipt>();
    private static int nextLegacyRedIndex;
    private static DRPGBasicAttackLegacyRedCaseReceipt activeLegacyRedReport;

    static DRPGBattleBasicAttackProductionPlayModeTestRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += ResumeAfterDomainReload;
    }

    [MenuItem("Tools/ANKALUTE/DRPG/Basic Attack/Run Production Adapter Matrix")]
    public static void RunFromMenu()
    {
        StartMatrix(false);
    }

    // Unity accepts this as -executeMethod.  It exits only after the final
    // report has been logged and the editor has received one more update.
    public static void RunFromCommandLine()
    {
        StartMatrix(true);
    }

    // Called by the existing "Capture Legacy Behavior REDs" menu runner.
    public static void RunLegacyBehaviorRedsFromMenu()
    {
        StopTicking();
        DestroyActiveHost();
        LegacyRedReports.Clear();
        nextLegacyRedIndex = 0;
        activeLegacyRedReport = null;
        SessionState.EraseString(LegacyRedSummaryKey);
        SessionState.EraseBool(LegacyRedCapturedKey);
        SessionState.SetString(SessionPhaseKey, LegacyEnteringPhase);
        if (EditorApplication.isPlaying)
        {
            BeginLegacyRedsInPlayMode();
            return;
        }
        EditorApplication.delayCall += EnterPlayModeWhenReady;
    }

    public static string GetLastLegacyBehaviorRedSummary()
    {
        return SessionState.GetString(LegacyRedSummaryKey, string.Empty);
    }

    internal static DRPGBasicAttackProductionScenarioReceipt RunProductionScenario(DRPGBasicAttackProductionScenario scenario)
    {
        DRPGBasicAttackProductionScenarioReport report = new DRPGBasicAttackProductionScenarioReport(scenario);
        lastScenarioReport = report;
        if (!EditorApplication.isPlaying)
        {
            report.Complete(DRPGBasicAttackProductionRunStatus.Failed, "RunProductionScenario requires Play Mode so the adapter advances on real frames.");
            return new DRPGBasicAttackProductionScenarioReceipt(report);
        }
        if (activeReport != null || activeLegacyRedReport != null ||
            !string.IsNullOrEmpty(SessionState.GetString(
                SessionPhaseKey,
                string.Empty)))
        {
            report.Complete(
                DRPGBasicAttackProductionRunStatus.InfrastructureFailed,
                "Another basic-attack runner already owns the PlayMode session.");
            return new DRPGBasicAttackProductionScenarioReceipt(report);
        }

        Func<IEnumerator> factory;
        if (!ProductionScenarioRegistry.TryGetValue(
                scenario,
                out factory) || factory == null)
        {
            report.Complete(
                DRPGBasicAttackProductionRunStatus.Pending,
                "No production PlayMode assertion is registered for this " +
                "scenario.");
            return new DRPGBasicAttackProductionScenarioReceipt(report);
        }

        StopTicking();
        DestroyActiveHost();
        activeReport = null;
        activeCoroutine = null;
        singleScenarioRun = true;
        StartConsoleObservation();
        StartScenario(report, factory);
        if (report.IsTerminal)
        {
            singleScenarioRun = false;
            StopTicking();
            return new DRPGBasicAttackProductionScenarioReceipt(report);
        }
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return new DRPGBasicAttackProductionScenarioReceipt(report);
    }

    internal static int EvaluateExitContract(DRPGBasicAttackProductionMatrixReport report)
    {
        if (report == null || report.WatchdogCount > 0 ||
            report.RunnerIncompleteCount > 0)
            return 2;
        return report.AllExpectedScenariosPassed ? 0 : 1;
    }

    // Small pure-contract check for callers that inject RecordingEditorProcessExit.
    internal static void VerifyExitContract()
    {
        VerifyExitContractCase(0, MakeContractReport(DRPGBasicAttackProductionRunStatus.Passed));
        VerifyExitContractCase(1, MakeContractReport(DRPGBasicAttackProductionRunStatus.Failed));
        VerifyExitContractCase(2, MakeContractReport(DRPGBasicAttackProductionRunStatus.WatchdogTimedOut));
        VerifyExitContractCase(2, MakeContractReport(DRPGBasicAttackProductionRunStatus.RunnerIncomplete));
    }

    internal static string VerifyLegacyRedClassificationContract()
    {
        Exception matching = new DRPGBattleLegacyInvariantException(
            LegacyRedCaseIds[0],
            "known invariant");
        Exception wrongCase = new DRPGBattleLegacyInvariantException(
            LegacyRedCaseIds[1],
            "wrong invariant");
        if (ClassifyLegacyFailure(LegacyRedCaseIds[0], matching) !=
                DRPGBasicAttackProductionRunStatus.Failed ||
            ClassifyLegacyFailure(LegacyRedCaseIds[0], wrongCase) !=
                DRPGBasicAttackProductionRunStatus.InfrastructureFailed ||
            ClassifyLegacyFailure(
                LegacyRedCaseIds[0],
                new InvalidOperationException("fixture failure")) !=
                DRPGBasicAttackProductionRunStatus.InfrastructureFailed)
        {
            throw new InvalidOperationException(
                "Legacy RED failures are not classified by exact invariant id.");
        }

        var exact = CreateLegacyClassificationFixture(
            DRPGBasicAttackProductionRunStatus.Failed);
        var infrastructure = CreateLegacyClassificationFixture(
            DRPGBasicAttackProductionRunStatus.Failed);
        infrastructure[2].Complete(
            DRPGBasicAttackProductionRunStatus.InfrastructureFailed,
            "fixture failure");
        if (!LegacyRedsMatchExpected(exact) ||
            LegacyRedsMatchExpected(infrastructure))
        {
            throw new InvalidOperationException(
                "Legacy RED aggregate accepts an infrastructure failure.");
        }
        return "PASS legacy RED classification and strict 0/5 gate";
    }

    private static void VerifyExitContractCase(int expectedCode, DRPGBasicAttackProductionMatrixReport report)
    {
        List<string> order = new List<string>();
        OrderedRecordingEditorProcessExit exit = new OrderedRecordingEditorProcessExit(order);
        ExitAfterLogFlush(report, exit, delegate { order.Add("flush"); });
        if (exit.ExitCode != expectedCode || order.Count != 2 || order[0] != "flush" || order[1] != "exit")
            throw new InvalidOperationException("Basic attack production exit contract verification failed.");
    }

    private static DRPGBasicAttackProductionMatrixReport MakeContractReport(DRPGBasicAttackProductionRunStatus status)
    {
        List<DRPGBasicAttackProductionScenarioReport> reports = new List<DRPGBasicAttackProductionScenarioReport>();
        foreach (DRPGBasicAttackProductionScenario scenario in DRPGBasicAttackProductionScenarioManifest.FinalExpectedScenarios)
        {
            DRPGBasicAttackProductionScenarioReport report = new DRPGBasicAttackProductionScenarioReport(scenario);
            report.Complete(status, "contract verification");
            reports.Add(report);
        }
        return new DRPGBasicAttackProductionMatrixReport(reports);
    }

    private static void StartMatrix(bool commandLine)
    {
        StopTicking();
        Reports.Clear();
        nextScenarioIndex = 0;
        activeCoroutine = null;
        DestroyActiveHost();
        activeReport = null;
        consoleFailure = null;
        singleScenarioRun = false;
        SessionState.EraseInt(SessionExitCodeKey);
        SessionState.EraseString(SessionSummaryKey);
        SessionState.SetBool(SessionCommandLineKey, commandLine);
        SessionState.SetString(SessionPhaseKey, EnteringPhase);
        if (EditorApplication.isPlaying)
        {
            BeginInPlayMode();
            return;
        }
        EditorApplication.delayCall += EnterPlayModeWhenReady;
    }

    private static void ResumeAfterDomainReload()
    {
        string phase = SessionState.GetString(SessionPhaseKey, string.Empty);
        if (EditorApplication.isPlaying &&
            (phase == EnteringPhase || phase == RunningPhase))
            BeginInPlayMode();
        else if (EditorApplication.isPlaying &&
            (phase == LegacyEnteringPhase || phase == LegacyRunningPhase))
            BeginLegacyRedsInPlayMode();
        else if (!EditorApplication.isPlaying && phase == EnteringPhase)
            EditorApplication.delayCall += EnterPlayModeWhenReady;
    }

    private static void EnterPlayModeWhenReady()
    {
        string phase = SessionState.GetString(SessionPhaseKey, string.Empty);
        if (phase != EnteringPhase && phase != LegacyEnteringPhase)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += EnterPlayModeWhenReady;
            return;
        }
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        string phase = SessionState.GetString(SessionPhaseKey, string.Empty);
        if (state == PlayModeStateChange.EnteredPlayMode && phase == EnteringPhase)
            BeginInPlayMode();
        else if (state == PlayModeStateChange.EnteredPlayMode && phase == LegacyEnteringPhase)
            BeginLegacyRedsInPlayMode();
        else if (state == PlayModeStateChange.EnteredEditMode && phase == "exiting")
            FinishInEditMode();
        else if (state == PlayModeStateChange.EnteredEditMode && phase == MatrixMenuExitingPhase)
            FinishInteractiveMatrixInEditMode();
        else if (state == PlayModeStateChange.EnteredEditMode && phase == LegacyExitingPhase)
            FinishLegacyRedsInEditMode();
    }

    private static void BeginInPlayMode()
    {
        if (!EditorApplication.isPlaying || SessionState.GetString(SessionPhaseKey, string.Empty) == RunningPhase)
            return;
        SessionState.SetString(SessionPhaseKey, RunningPhase);
        Reports.Clear();
        foreach (DRPGBasicAttackProductionScenario scenario in DRPGBasicAttackProductionScenarioManifest.FinalExpectedScenarios)
            Reports.Add(new DRPGBasicAttackProductionScenarioReport(scenario));
        nextScenarioIndex = 0;
        StartConsoleObservation();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void BeginLegacyRedsInPlayMode()
    {
        if (!EditorApplication.isPlaying || SessionState.GetString(SessionPhaseKey, string.Empty) == LegacyRunningPhase)
            return;
        SessionState.SetString(SessionPhaseKey, LegacyRunningPhase);
        LegacyRedReports.Clear();
        for (int i = 0; i < LegacyRedCaseIds.Length; i++)
            LegacyRedReports.Add(new DRPGBasicAttackLegacyRedCaseReceipt(LegacyRedCaseIds[i]));
        nextLegacyRedIndex = 0;
        activeLegacyRedReport = null;
        StartConsoleObservation();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;
        if (SessionState.GetString(SessionPhaseKey, string.Empty) == LegacyRunningPhase)
        {
            TickLegacyReds();
            return;
        }
        if (activeReport != null)
        {
            AdvanceActiveScenario();
            return;
        }
        if (singleScenarioRun)
        {
            singleScenarioRun = false;
            StopTicking();
            return;
        }
        if (nextScenarioIndex >= Reports.Count)
        {
            CompleteMatrix();
            return;
        }
        DRPGBasicAttackProductionScenarioReport report = Reports[nextScenarioIndex++];
        Func<IEnumerator> factory;
        if (!ProductionScenarioRegistry.TryGetValue(
                report.Scenario,
                out factory) || factory == null)
        {
            report.Complete(DRPGBasicAttackProductionRunStatus.Pending, "No completed production adapter is registered for this Task 1 scenario.");
            return;
        }
        StartScenario(report, factory);
    }

    private static void TickLegacyReds()
    {
        if (activeLegacyRedReport != null)
        {
            AdvanceLegacyRed();
            return;
        }
        if (nextLegacyRedIndex >= LegacyRedReports.Count)
        {
            CompleteLegacyReds();
            return;
        }
        StartLegacyRed(LegacyRedReports[nextLegacyRedIndex++]);
    }

    private static void StartLegacyRed(DRPGBasicAttackLegacyRedCaseReceipt report)
    {
        try
        {
            consoleFailure = null;
            report.Begin();
            activeLegacyRedReport = report;
            activeDeadline = EditorApplication.timeSinceStartup +
                ScenarioWatchdogSeconds;
            activeCoroutine = CreateLegacyRedCoroutine(report.CaseId);
            if (activeCoroutine == null)
                throw new MissingMethodException("DRPGBattleBasicAttackLegacyBehaviorProbe must expose public static IEnumerator RunCoroutine(string legacyCaseId).");
            GameObject hostObject = new GameObject("DRPGBasicAttackLegacyRedCoroutineHost");
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            activeHost = hostObject.AddComponent<LegacyCoroutineHost>();
            activeHost.Begin(activeCoroutine);
        }
        catch (Exception exception)
        {
            report.Complete(
                DRPGBasicAttackProductionRunStatus.InfrastructureFailed,
                exception.ToString());
            activeLegacyRedReport = null;
            activeCoroutine = null;
            DestroyActiveHost();
        }
    }

    private static void AdvanceLegacyRed()
    {
        if (EditorApplication.timeSinceStartup > activeDeadline)
        {
            activeLegacyRedReport.Complete(
                DRPGBasicAttackProductionRunStatus.WatchdogTimedOut,
                "Scenario exceeded the PlayMode watchdog.");
        }
        else if (!string.IsNullOrEmpty(consoleFailure))
        {
            activeLegacyRedReport.Complete(
                DRPGBasicAttackProductionRunStatus.InfrastructureFailed,
                consoleFailure);
        }
        else if (activeHost == null)
        {
            activeLegacyRedReport.Complete(
                DRPGBasicAttackProductionRunStatus.InfrastructureFailed,
                "Legacy RED coroutine host was destroyed before completion.");
        }
        else if (activeHost.Failure != null)
        {
            activeLegacyRedReport.Complete(
                ClassifyLegacyFailure(
                    activeLegacyRedReport.CaseId,
                    activeHost.Failure),
                activeHost.Failure.Message);
        }
        else if (activeHost.IsComplete)
        {
            activeLegacyRedReport.Complete(DRPGBasicAttackProductionRunStatus.Passed, "Probe completed without the expected RED failure.");
        }
        else
        {
            return;
        }
        activeLegacyRedReport = null;
        activeCoroutine = null;
        DestroyActiveHost();
    }

    private static void StartScenario(
        DRPGBasicAttackProductionScenarioReport report,
        Func<IEnumerator> factory)
    {
        try
        {
            consoleFailure = null;
            report.Begin();
            activeReport = report;
            activeDeadline = EditorApplication.timeSinceStartup +
                ResolveScenarioWatchdogSeconds(report.Scenario);
            activeCoroutine = factory();
            if (activeCoroutine == null)
                throw new RunnerIncompleteException(
                    "The production scenario factory returned no coroutine.");
            GameObject hostObject = new GameObject("DRPGBasicAttackProductionCoroutineHost");
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            activeHost = hostObject.AddComponent<LegacyCoroutineHost>();
            activeHost.Begin(activeCoroutine);
        }
        catch (RunnerIncompleteException exception)
        {
            report.Complete(
                DRPGBasicAttackProductionRunStatus.RunnerIncomplete,
                exception.ToString());
            activeReport = null;
            activeCoroutine = null;
            DestroyActiveHost();
        }
        catch (Exception exception)
        {
            report.Complete(DRPGBasicAttackProductionRunStatus.Failed, exception.ToString());
            activeReport = null;
            activeCoroutine = null;
            DestroyActiveHost();
        }
    }

    private static void AdvanceActiveScenario()
    {
        if (EditorApplication.timeSinceStartup > activeDeadline)
        {
            activeReport.Complete(DRPGBasicAttackProductionRunStatus.WatchdogTimedOut, "Scenario exceeded the PlayMode watchdog.");
            activeReport = null;
            activeCoroutine = null;
            DestroyActiveHost();
            return;
        }
        if (!string.IsNullOrEmpty(consoleFailure))
        {
            activeReport.Complete(DRPGBasicAttackProductionRunStatus.Failed, consoleFailure);
            activeReport = null;
            activeCoroutine = null;
            DestroyActiveHost();
            return;
        }
        if (activeHost == null)
        {
            activeReport.Complete(
                DRPGBasicAttackProductionRunStatus.RunnerIncomplete,
                "Production coroutine host was destroyed before completion.");
            activeReport = null;
            activeCoroutine = null;
            return;
        }
        if (activeHost.Failure != null)
        {
            activeReport.Complete(DRPGBasicAttackProductionRunStatus.Failed, activeHost.Failure.ToString());
            activeReport = null;
            activeCoroutine = null;
            DestroyActiveHost();
            return;
        }
        if (activeHost.IsComplete)
        {
            activeReport.Complete(DRPGBasicAttackProductionRunStatus.Passed, "Production adapter completed on real PlayMode frames.");
            activeReport = null;
            activeCoroutine = null;
            DestroyActiveHost();
        }
    }

    private static double ResolveScenarioWatchdogSeconds(
        DRPGBasicAttackProductionScenario scenario)
    {
        // This adapter intentionally drives six complete, readable production
        // actions through the real FlowRoot. Its own coroutine still has a
        // tighter bounded-runtime assertion; the runner must not pre-empt it
        // with the short single-beat watchdog used by the other scenarios.
        switch (scenario)
        {
            case DRPGBasicAttackProductionScenario.ProductionActorAnchorsFollowMotionRoot:
            case DRPGBasicAttackProductionScenario.ProductionSettlementNoChainFire:
            case DRPGBasicAttackProductionScenario.ProductionMarkerMissingRecovers:
            case DRPGBasicAttackProductionScenario.ProductionWavePlaceholderBatchDeploysAtomically:
            case DRPGBasicAttackProductionScenario.ProductionWaveRegistrationFailureFaultsCleanly:
                return ContinuousBattleScenarioWatchdogSeconds;
            default:
                return ScenarioWatchdogSeconds;
        }
    }

    private static IEnumerator CreateLegacyRedCoroutine(string caseId)
    {
        Type type = FindType("DRPGBattleBasicAttackLegacyBehaviorProbe");
        if (type == null)
            return null;
        MethodInfo method = type.GetMethod("RunCoroutine", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
        if (method == null)
            return null;
        IEnumerator coroutine = method.Invoke(null, new object[] { caseId }) as IEnumerator;
        if (coroutine == null)
            throw new InvalidOperationException("Legacy RunCoroutine(string) must return IEnumerator.");
        return coroutine;
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null)
                return type;
        }
        return null;
    }

    private static void StartConsoleObservation()
    {
        consoleFailure = null;
        if (!observingConsole)
        {
            Application.logMessageReceived += OnLogMessageReceived;
            observingConsole = true;
        }
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Assert || type == LogType.Error || type == LogType.Exception)
            consoleFailure = type + ": " + condition + "\n" + stackTrace;
    }

    private static void CompleteMatrix()
    {
        StopTicking();
        DRPGBasicAttackProductionMatrixReport report = new DRPGBasicAttackProductionMatrixReport(Reports);
        string summary = FormatReport(report);
        if (SessionState.GetBool(SessionCommandLineKey, false))
        {
            Debug.Log(summary);
            SessionState.SetInt(SessionExitCodeKey, EvaluateExitContract(report));
            SessionState.SetString(SessionSummaryKey, summary);
            SessionState.SetString(SessionPhaseKey, "exiting");
            EditorApplication.ExitPlaymode();
            return;
        }
        Debug.Log(summary);
        SessionState.SetString(SessionSummaryKey, summary);
        SessionState.SetString(SessionPhaseKey, MatrixMenuExitingPhase);
        EditorApplication.ExitPlaymode();
    }

    private static void CompleteLegacyReds()
    {
        StopTicking();
        string summary = FormatLegacyRedSummary();
        SessionState.SetString(LegacyRedSummaryKey, summary);
        SessionState.SetBool(
            LegacyRedCapturedKey,
            LegacyRedsMatchExpected(LegacyRedReports));
        SessionState.SetString(SessionPhaseKey, LegacyExitingPhase);
        EditorApplication.ExitPlaymode();
    }

    private static void FinishInteractiveMatrixInEditMode()
    {
        string summary = SessionState.GetString(SessionSummaryKey, "[DRPGBasicAttackProduction] summary was not persisted.");
        SessionState.EraseString(SessionPhaseKey);
        SessionState.EraseBool(SessionCommandLineKey);
        SessionState.EraseString(SessionSummaryKey);
        Debug.Log(summary);
    }

    private static void FinishLegacyRedsInEditMode()
    {
        string summary = SessionState.GetString(LegacyRedSummaryKey, "[DRPGBasicAttackLegacyRED] summary was not persisted.");
        bool expectedRedCaptured = SessionState.GetBool(
            LegacyRedCapturedKey,
            false);
        SessionState.EraseString(SessionPhaseKey);
        SessionState.EraseBool(LegacyRedCapturedKey);
        if (expectedRedCaptured)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }

    private static void FinishInEditMode()
    {
        DRPGBasicAttackProductionMatrixReport report = new DRPGBasicAttackProductionMatrixReport(Reports);
        int exitCode = SessionState.GetInt(SessionExitCodeKey, EvaluateExitContract(report));
        string summary = SessionState.GetString(SessionSummaryKey, FormatReport(report));
        SessionState.EraseString(SessionPhaseKey);
        SessionState.EraseBool(SessionCommandLineKey);
        SessionState.EraseInt(SessionExitCodeKey);
        SessionState.EraseString(SessionSummaryKey);
        EditorApplication.delayCall += delegate
        {
            ExitAfterLogFlush(exitCode, new UnityEditorProcessExit(), delegate { Debug.Log(summary); });
        };
    }

    private static void ExitAfterLogFlush(DRPGBasicAttackProductionMatrixReport report, IEditorProcessExit exit, Action flushLog)
    {
        ExitAfterLogFlush(EvaluateExitContract(report), exit, flushLog);
    }

    private static void ExitAfterLogFlush(int exitCode, IEditorProcessExit exit, Action flushLog)
    {
        if (flushLog != null)
            flushLog();
        exit.Exit(exitCode);
    }

    private static void StopTicking()
    {
        EditorApplication.update -= Tick;
        if (observingConsole)
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            observingConsole = false;
        }
    }

    private static void DestroyActiveHost()
    {
        if (activeHost != null)
            UnityEngine.Object.Destroy(activeHost.gameObject);
        activeHost = null;
    }

    private static string FormatReport(DRPGBasicAttackProductionMatrixReport report)
    {
        string headline = string.Format(
            "[DRPGBasicAttackProduction] expected={0}, completed={1}, " +
            "pending={2}, failed={3}, watchdog={4}, incomplete={5}, " +
            "infrastructure={6}",
            report.Scenarios.Count,
            report.CompletedCount,
            report.PendingCount,
            report.FailedCount,
            report.WatchdogCount,
            report.RunnerIncompleteCount,
            report.InfrastructureFailureCount);
        System.Text.StringBuilder details = new System.Text.StringBuilder(headline);
        for (int i = 0; i < report.Scenarios.Count; i++)
        {
            DRPGBasicAttackProductionScenarioReport scenario = report.Scenarios[i];
            details.Append('\n')
                .Append(scenario.Scenario)
                .Append(": ")
                .Append(scenario.Status)
                .Append(" - ")
                .Append(scenario.Detail);
        }
        return details.ToString();
    }

    private static string FormatLegacyRedSummary()
    {
        int passed = 0;
        int failed = 0;
        int infrastructure = 0;
        int watchdog = 0;
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < LegacyRedReports.Count; i++)
        {
            DRPGBasicAttackLegacyRedCaseReceipt report = LegacyRedReports[i];
            if (report.Status == DRPGBasicAttackProductionRunStatus.Passed)
                passed++;
            else if (report.Status == DRPGBasicAttackProductionRunStatus.Failed)
                failed++;
            else if (report.Status ==
                DRPGBasicAttackProductionRunStatus.WatchdogTimedOut)
                watchdog++;
            else
                infrastructure++;
            builder.Append('\n').Append(report.CaseId).Append(": ").Append(report.Status).Append(" - ").Append(report.Detail);
        }
        bool expectedRedCaptured = LegacyRedsMatchExpected(LegacyRedReports);
        return string.Format(
            "[DRPGBasicAttackLegacyRED] {0} passed / {1} failed; " +
            "infrastructure={2}; watchdog={3}; expected-red-captured={4}{5}",
            passed,
            failed,
            infrastructure,
            watchdog,
            expectedRedCaptured,
            builder);
    }

    private static DRPGBasicAttackProductionRunStatus ClassifyLegacyFailure(
        string caseId,
        Exception failure)
    {
        DRPGBattleLegacyInvariantException expected =
            failure as DRPGBattleLegacyInvariantException;
        return expected != null && string.Equals(
                expected.CaseId,
                caseId,
                StringComparison.Ordinal)
            ? DRPGBasicAttackProductionRunStatus.Failed
            : DRPGBasicAttackProductionRunStatus.InfrastructureFailed;
    }

    private static List<DRPGBasicAttackLegacyRedCaseReceipt>
        CreateLegacyClassificationFixture(
            DRPGBasicAttackProductionRunStatus status)
    {
        var result = new List<DRPGBasicAttackLegacyRedCaseReceipt>(
            LegacyRedCaseIds.Length);
        for (int i = 0; i < LegacyRedCaseIds.Length; i++)
        {
            var receipt = new DRPGBasicAttackLegacyRedCaseReceipt(
                LegacyRedCaseIds[i]);
            receipt.Complete(status, "classification fixture");
            result.Add(receipt);
        }
        return result;
    }

    private static bool LegacyRedsMatchExpected(
        IReadOnlyList<DRPGBasicAttackLegacyRedCaseReceipt> reports)
    {
        if (reports == null || reports.Count != LegacyRedCaseIds.Length)
            return false;
        for (int i = 0; i < reports.Count; i++)
        {
            DRPGBasicAttackLegacyRedCaseReceipt report =
                reports[i];
            if (!string.Equals(
                    report.CaseId,
                    LegacyRedCaseIds[i],
                    StringComparison.Ordinal) ||
                report.Status != DRPGBasicAttackProductionRunStatus.Failed)
            {
                return false;
            }
        }
        return true;
    }

    private sealed class UnityEditorProcessExit : IEditorProcessExit
    {
        public void Exit(int exitCode)
        {
            EditorApplication.Exit(exitCode);
        }
    }

    private sealed class OrderedRecordingEditorProcessExit : IEditorProcessExit
    {
        private readonly IList<string> order;

        internal OrderedRecordingEditorProcessExit(IList<string> order)
        {
            this.order = order;
        }

        internal int? ExitCode { get; private set; }

        public void Exit(int exitCode)
        {
            order.Add("exit");
            ExitCode = exitCode;
        }
    }

    private sealed class RunnerIncompleteException : Exception
    {
        internal RunnerIncompleteException(string message) : base(message)
        {
        }
    }

    private sealed class LegacyCoroutineHost : MonoBehaviour
    {
        internal bool IsComplete { get; private set; }
        internal Exception Failure { get; private set; }

        internal void Begin(IEnumerator coroutine)
        {
            StartCoroutine(Run(coroutine));
        }

        private IEnumerator Run(IEnumerator coroutine)
        {
            while (true)
            {
                bool advanced;
                try
                {
                    advanced = coroutine.MoveNext();
                }
                catch (Exception exception)
                {
                    Failure = exception;
                    yield break;
                }
                if (!advanced)
                {
                    IsComplete = true;
                    yield break;
                }
                yield return coroutine.Current;
            }
        }
    }
}
#endif
