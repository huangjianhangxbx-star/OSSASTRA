#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DRPGDamageBrokenPresentationTestRunner
{
    private const string PartyPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGParty_DebugBreak.asset";
    private const string EncounterPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGEncounter_DebugBreak.asset";
    private const string SessionPhaseKey =
        "ANKALUTE.DRPG.BreakPresentationTest.Phase.v1";
    private const string EnteringPhase = "entering";
    private const string RunningPhase = "running";
    private const string ReportRelativePath = "Temp/DRPGDamageBrokenPresentationTest.txt";
    private const string ScreenshotRelativePath = "Temp/DRPGDamageBrokenPresentation.png";

    private static int waitFrames;
    private static double testDeadline;
    private static double breakObservedAt;
    private static double captureAt;
    private static DRPGBattleFlowRoot battleFlow;
    private static DRPGBattleSession session;
    private static DRPGPartySetupSO party;
    private static DRPGEncounterDefinitionSO encounter;
    private static BattleParticipantRuntime damageBrokenTarget;
    private static bool breakHitObserved;
    private static bool damageBrokenCutInStarted;
    private static bool damageBrokenCutInVisible;
    private static int breakActionActorRuntimeId;

    static DRPGDamageBrokenPresentationTestRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += ResumeOrStart;
    }

    [MenuItem("Tools/ANKALUTE/DRPG/Run Break Presentation Test")]
    public static void RunFromMenu()
    {
        SessionState.EraseString(SessionPhaseKey);
        if (EditorApplication.isPlaying)
        {
            BeginInPlayMode();
            return;
        }

        BeginFromEditMode();
    }

    private static void ResumeOrStart()
    {
        string phase = SessionState.GetString(SessionPhaseKey, string.Empty);
        if (EditorApplication.isPlaying &&
            (phase == EnteringPhase || phase == RunningPhase))
        {
            BeginInPlayMode();
            return;
        }

        if (!EditorApplication.isPlaying && phase == EnteringPhase)
        {
            EditorApplication.delayCall += BeginFromEditMode;
        }
    }

    private static void BeginFromEditMode()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += BeginFromEditMode;
            return;
        }

        SessionState.SetString(SessionPhaseKey, EnteringPhase);
        WriteReport("RUNNING: entering Play Mode");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetString(SessionPhaseKey, string.Empty) == EnteringPhase)
        {
            BeginInPlayMode();
        }
    }

    private static void BeginInPlayMode()
    {
        if (!EditorApplication.isPlaying ||
            SessionState.GetString(SessionPhaseKey, string.Empty) == RunningPhase)
        {
            return;
        }

        SessionState.SetString(SessionPhaseKey, RunningPhase);
        waitFrames = 12;
        testDeadline = EditorApplication.timeSinceStartup + 30d;
        breakObservedAt = 0d;
        captureAt = 0d;
        battleFlow = null;
        session = null;
        party = null;
        encounter = null;
        damageBrokenTarget = null;
        breakHitObserved = false;
        damageBrokenCutInStarted = false;
        damageBrokenCutInVisible = false;
        breakActionActorRuntimeId = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;

        try
        {
            if (waitFrames-- > 0)
                return;

            if (session == null)
            {
                StartBattle();
                return;
            }

            if (breakHitObserved)
            {
                ObserveDamageBrokenCutIn();
                return;
            }

            if (EditorApplication.timeSinceStartup > testDeadline)
            {
                throw new InvalidOperationException(
                    "The debug encounter did not trigger an ally break within 30 seconds. " +
                    "target=" + (damageBrokenTarget != null ? damageBrokenTarget.Profile.DisplayName : "<none>"));
            }

            DriveCurrentTurn();
        }
        catch (Exception exception)
        {
            FailAndExit(exception);
        }
    }

    private static void StartBattle()
    {
        string probeResult = DRPGProbeRunner.RunAll();
        Assert(probeResult.StartsWith("[DRPGProbes] PASS", StringComparison.Ordinal),
            "Foundation probes did not pass: " + probeResult);

        GameFlowRoot root = GameFlowRoot.Instance;
        Assert(root != null, "GameFlowRoot.Instance is missing in Play Mode.");
        Assert(root.drpgBattleFlow != null, "DRPGBattleFlowRoot is not connected.");

        party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>(PartyPath);
        encounter = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>(EncounterPath);
        Assert(party != null, "Debug break party asset is missing.");
        Assert(encounter != null, "Debug break encounter asset is missing.");

        DRPGBattleStartResult result = root.drpgBattleFlow.RequestBattle(
            party,
            encounter);
        Assert(result == DRPGBattleStartResult.Started,
            "Debug break battle did not start: " + result);

        battleFlow = root.drpgBattleFlow;
        session = battleFlow.CurrentSession;
        Assert(session != null, "Debug break session is missing after start.");
        session.ActionResolved += OnActionResolved;
        session.BattleEnded += OnBattleEnded;

        DRPGBattlePresentationRoot presentation =
            root.GetComponentInChildren<DRPGBattlePresentationRoot>(true);
        Assert(presentation != null, "Debug break presentation is missing.");
        Assert(
            presentation.GetComponent<DRPGBattlePresentationDirector>() != null,
            "Presentation director is missing.");
        Assert(
            presentation.GetComponent<
                DRPGBattleActionTransactionCoordinator>() != null,
            "Transaction coordinator is missing.");
        AssertNoMissingScripts(presentation.gameObject);

        damageBrokenTarget = FindFirstPartyMember();
        Assert(damageBrokenTarget != null, "Debug break target teammate is missing.");
        Assert(damageBrokenTarget.IsOrdinaryEnemyTarget,
            "The debug target must be an ordinary enemy target.");
    }

    private static void DriveCurrentTurn()
    {
        if (session == null || !session.IsActive || session.CurrentActor == null ||
            session.Phase != DRPGBattlePhase.AwaitingCommand ||
            session.CurrentActor.Side == DRPGBattleSide.Enemy)
        {
            return;
        }

        BattleParticipantRuntime actor = session.CurrentActor;
        DRPGSkillSO skill = actor.Profile.Skills.Count > 0
            ? actor.Profile.Skills[0]
            : null;
        BattleParticipantRuntime target = FindLivingEnemy();
        Assert(skill != null && target != null,
            "Debug break player driver could not build a legal command.");

        var command = new DRPGBattleCommand(
            actor.RuntimeId,
            target.RuntimeId,
            skill);
        string error;
        bool accepted = session.HasStagedActionConsumer
            ? session.TryPrepareCommand(command, out _, out error)
            : session.TrySubmitCommand(command, out error);
        Assert(accepted, "Debug break player command was rejected: " + error);
    }

    private static void OnActionResolved(DRPGBattleActionResolution resolution)
    {
        if (resolution == null || damageBrokenTarget == null)
            return;

        for (int i = 0; i < resolution.Hits.Count; i++)
        {
            DRPGBattleHitResult hit = resolution.Hits[i];
            if (hit != null && hit.TargetRuntimeId == damageBrokenTarget.RuntimeId &&
                hit.TriggeredDamageBroken)
            {
                breakHitObserved = true;
                breakObservedAt = EditorApplication.timeSinceStartup;
                breakActionActorRuntimeId = resolution.PreparedAction.ActorRuntimeId;
                return;
            }
        }
    }

    private static void ObserveDamageBrokenCutIn()
    {
        if (damageBrokenTarget == null ||
            damageBrokenTarget.LowHpState != DRPGLowHpPresentationState.DamageBroken)
        {
            throw new InvalidOperationException(
                "Break hit was reported, but the teammate runtime is not Broken.");
        }

        GameFlowRoot root = GameFlowRoot.Instance;
        BattleDamageBrokenCutInPresenter cutIn = root != null
            ? root.GetComponentInChildren<BattleDamageBrokenCutInPresenter>(true)
            : null;
        if (!damageBrokenCutInStarted && cutIn != null && cutIn.IsPlaying &&
            CountActiveDamageBrokenViews(cutIn) > 0)
        {
            damageBrokenCutInStarted = true;
            captureAt = EditorApplication.timeSinceStartup + 0.45d;
            return;
        }

        if (damageBrokenCutInStarted && !damageBrokenCutInVisible &&
            EditorApplication.timeSinceStartup >= captureAt)
        {
            damageBrokenCutInVisible = true;
            captureAt = EditorApplication.timeSinceStartup;
            ScreenCapture.CaptureScreenshot(GetProjectPath(ScreenshotRelativePath));
            return;
        }

        if (!damageBrokenCutInStarted &&
            EditorApplication.timeSinceStartup > breakObservedAt + 3d)
        {
            throw new InvalidOperationException(
                "The ally break hit resolved, but BattleDamageBrokenCutInPresenter never entered IsPlaying.");
        }

        if (damageBrokenCutInVisible && cutIn != null && !cutIn.IsPlaying)
        {
            bool advanced = session == null || !session.IsActive ||
                session.Phase == DRPGBattlePhase.AwaitingCommand ||
                session.Phase == DRPGBattlePhase.Result ||
                (session.CurrentActor != null &&
                 session.CurrentActor.RuntimeId != breakActionActorRuntimeId);
            if (!advanced)
                return;
            DRPGBattlePresentationRoot presentation = root != null
                ? root.GetComponentInChildren<DRPGBattlePresentationRoot>(true)
                : null;
            DRPGBattlePresentationDirector director = presentation != null
                ? presentation.GetComponent<DRPGBattlePresentationDirector>()
                : null;
            DRPGBattleActionTransactionCoordinator coordinator =
                presentation != null
                    ? presentation.GetComponent<
                        DRPGBattleActionTransactionCoordinator>()
                    : null;
            if (director != null && !director.HasDanglingRuns &&
                director.InputGate.LeaseCount == 0 && coordinator != null &&
                !coordinator.HasActiveAction)
            {
                FinishAndExit();
                return;
            }
        }

        if (damageBrokenCutInVisible &&
            EditorApplication.timeSinceStartup > breakObservedAt + 10d)
        {
            throw new InvalidOperationException(
                "Break presentation did not settle before its realtime watchdog.");
        }
    }

    private static int CountActiveDamageBrokenViews(BattleDamageBrokenCutInPresenter cutIn)
    {
        BattleDamageBrokenCutInView[] views =
            cutIn.GetComponentsInChildren<BattleDamageBrokenCutInView>(true);
        int count = 0;
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].gameObject.activeSelf)
                count++;
        }

        return count;
    }

    private static BattleParticipantRuntime FindFirstPartyMember()
    {
        for (int i = 0; i < session.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = session.Participants[i];
            if (participant.RosterRole == DRPGBattleRosterRole.PartyMember)
                return participant;
        }

        return null;
    }

    private static BattleParticipantRuntime FindLivingEnemy()
    {
        for (int i = 0; i < session.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = session.Participants[i];
            if (participant.Side == DRPGBattleSide.Enemy && participant.IsAlive)
                return participant;
        }

        return null;
    }

    private static void FinishAndExit()
    {
        Assert(breakHitObserved, "No break hit was observed.");
        Assert(damageBrokenCutInVisible, "The break cut-in was never visible.");
        Assert(damageBrokenTarget != null &&
            damageBrokenTarget.LowHpState == DRPGLowHpPresentationState.DamageBroken,
            "The selected teammate did not remain in Broken state.");

        GameFlowRoot root = GameFlowRoot.Instance;
        DRPGBattlePresentationRoot presentation = root != null
            ? root.GetComponentInChildren<DRPGBattlePresentationRoot>(true)
            : null;
        DRPGBattlePresentationDirector director = presentation != null
            ? presentation.GetComponent<DRPGBattlePresentationDirector>()
            : null;
        DRPGBattleActionTransactionCoordinator coordinator =
            presentation != null
                ? presentation.GetComponent<
                    DRPGBattleActionTransactionCoordinator>()
                : null;
        Assert(director != null && !director.HasDanglingRuns,
            "Presentation director retained a run after break completion.");
        Assert(director.InputGate.LeaseCount == 0,
            "Presentation input gate retained a lease after break completion.");
        Assert(coordinator != null && !coordinator.HasActiveAction,
            "Transaction coordinator retained an action after break completion.");

        WriteReport(
            "PASS DRPG break presentation test\n" +
            "target=" + damageBrokenTarget.Profile.DisplayName + "\n" +
            "targetHp=" + damageBrokenTarget.CurrentHp + "/" + damageBrokenTarget.Profile.MaxHp + "\n" +
            "targetLowHpState=" + damageBrokenTarget.LowHpState + "\n" +
            "breakHitObserved=true\n" +
            "damageBrokenCutInVisible=true\n" +
            "actionFlowGateReleased=true\n" +
            "screenshot=" + ScreenshotRelativePath);
        CleanupAndExit();
    }

    private static void OnBattleEnded(DRPGBattleResult result)
    {
        if (!breakHitObserved && result.Outcome != DRPGBattleOutcome.None)
        {
            throw new InvalidOperationException(
                "Debug break battle ended before the target broke: " + result.Outcome);
        }
    }

    private static void FailAndExit(Exception exception)
    {
        WriteReport("FAILED DRPG break presentation test\n" + exception);
        Debug.LogException(exception);
        CleanupAndExit();
    }

    private static void CleanupAndExit()
    {
        EditorApplication.update -= Tick;
        if (session != null)
        {
            session.ActionResolved -= OnActionResolved;
            session.BattleEnded -= OnBattleEnded;
            if (session.IsActive)
                session.Abort();
        }

        session = null;
        battleFlow = null;
        party = null;
        encounter = null;
        damageBrokenTarget = null;
        SessionState.EraseString(SessionPhaseKey);
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertNoMissingScripts(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject current = transforms[i].gameObject;
            Assert(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current) == 0,
                "Missing MonoBehaviour script on " + current.name);
        }
    }

    private static void WriteReport(string contents)
    {
        File.WriteAllText(GetProjectPath(ReportRelativePath), contents ?? string.Empty);
    }

    private static string GetProjectPath(string relativePath)
    {
        return Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            relativePath);
    }
}
#endif
