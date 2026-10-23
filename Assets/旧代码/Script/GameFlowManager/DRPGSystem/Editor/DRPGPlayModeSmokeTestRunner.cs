#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DRPGPlayModeSmokeTestRunner
{
    private const string EncounterPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset";
    private const string SessionPhaseKey =
        "ANKALUTE.DRPG.PlayModeSmoke.Phase.v11";
    private const string BatchExitKey =
        "ANKALUTE.DRPG.PlayModeSmoke.BatchExit.v1";
    private const string FailureExitKey =
        "ANKALUTE.DRPG.PlayModeSmoke.Failed.v1";
    private const string FoundationPassedKey =
        "ANKALUTE.DRPG.FoundationPreflightPassed";
    private const string FoundationSummaryKey =
        "ANKALUTE.DRPG.FoundationPreflightSummary";
    private const string EnteringPhase = "entering";
    private const string RunningPhase = "running";
    private const string SmokeScenePath =
        "Assets/Scenes/测试场3_电车45.unity";
    private const string ReportRelativePath = "Logs/DRPGPlayModeSmokeTest.txt";
    private const string ScreenshotRelativePath = "Logs/DRPGBattleSmoke.png";

    private static int waitFrames;
    private static DRPGBattleFlowRoot battleFlow;
    private static DRPGBattleSession session;
    private static DRPGEncounterDefinitionSO encounter;
    private static DRPGBattleOutcome observedOutcome;
    private static bool secondLegStarted;
    private static bool forcedOutcomePending;
    private static DRPGBattleOutcome forcedOutcome;
    private static bool seenVictory;
    private static bool seenDefeat;
    private static CharacterRoot expeditionPlayer;
    private static bool huntPauseBefore;
    private static bool hasHuntData;
    private static bool hasAuthority;
    private static bool moveAllowedBefore;
    private static bool verifyFirstPlayerUiResolution;
    private static float firstUiTargetHpBefore;
    private static BattleParticipantRuntime firstUiTarget;
    private static double firstUiResolutionDeadline;
    private static int legacyEnemyPreviewCount;
    private static int hiddenOrdinaryPreviewCount;
    private static int pendingLegacyEnemyPreviewTargetId;
    private static bool legacyEnemyPreviewFeedbackVerified;
    private static int enemyPlanChangeCount;
    private static int enemyTargetDisclosureCount;
    private static bool initialPresentationInspected;
    private static double initialPresentationDeadline;
    private static string foundationProbeSummary = string.Empty;
    private static string smokeResonancePartnerId = string.Empty;
    private static string smokeResonanceSkillId = string.Empty;
    private static int smokeResonanceProgress;
    private static bool smokeResonancePreserved;
    private static bool smokeResonanceCleared;
    private static BattleCameraPresenter smokeBattleCamera;
    private static int cameraPlayCountBeforeFirstAttack;
    private static int cameraRestoreCountBeforeFirstAttack;
    private static bool conflictCameraObserved;
    private static bool firstUiDamageObserved;

    static DRPGPlayModeSmokeTestRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += ResumeOrStart;
    }

    [MenuItem("Tools/ANKALUTE/DRPG/Run PlayMode Smoke Test")]
    public static void RunFromMenu()
    {
        SessionState.EraseString(SessionPhaseKey);
        SessionState.EraseBool(FoundationPassedKey);
        SessionState.EraseString(FoundationSummaryKey);
        SessionState.SetBool(BatchExitKey, Application.isBatchMode);
        SessionState.SetBool(FailureExitKey, false);
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
            return;
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

        if (Application.isBatchMode &&
            !string.Equals(
                EditorSceneManager.GetActiveScene().path,
                SmokeScenePath,
                StringComparison.Ordinal))
        {
            EditorSceneManager.OpenScene(SmokeScenePath, OpenSceneMode.Single);
        }

        try
        {
            RunFoundationPreflight();
        }
        catch (Exception exception)
        {
            FailAndExit(exception);
            return;
        }

        SessionState.SetString(SessionPhaseKey, EnteringPhase);
        WriteReport("RUNNING: entering Play Mode");
        EditorApplication.EnterPlaymode();
    }

    private static void RunFoundationPreflight()
    {
        string savedSummary = SessionState.GetString(
            FoundationSummaryKey,
            string.Empty);
        if (SessionState.GetBool(FoundationPassedKey, false) &&
            !string.IsNullOrWhiteSpace(savedSummary))
        {
            return;
        }

        SessionState.EraseBool(FoundationPassedKey);
        SessionState.EraseString(FoundationSummaryKey);
        string probeResult = DRPGProbeRunner.RunAll();
        Assert(probeResult.StartsWith(
                "[DRPGProbes] PASS",
                StringComparison.Ordinal),
            "Foundation probes did not pass: " + probeResult);
        SessionState.SetString(FoundationSummaryKey, probeResult);
        SessionState.SetBool(FoundationPassedKey, true);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetString(SessionPhaseKey, string.Empty) == EnteringPhase)
        {
            BeginInPlayMode();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode &&
            SessionState.GetBool(BatchExitKey, false))
        {
            bool failed = SessionState.GetBool(FailureExitKey, false);
            SessionState.EraseBool(BatchExitKey);
            SessionState.EraseBool(FailureExitKey);
            EditorApplication.delayCall += () =>
                EditorApplication.Exit(failed ? 1 : 0);
        }
    }

    private static void BeginInPlayMode()
    {
        if (!EditorApplication.isPlaying ||
            SessionState.GetString(SessionPhaseKey, string.Empty) == RunningPhase)
        {
            return;
        }

        string savedFoundationSummary = SessionState.GetString(
            FoundationSummaryKey,
            string.Empty);
        if (!SessionState.GetBool(FoundationPassedKey, false) ||
            string.IsNullOrWhiteSpace(savedFoundationSummary))
        {
            FailAndExit(new InvalidOperationException(
                "PlayMode smoke started without a passing EditMode foundation " +
                "preflight receipt."));
            return;
        }

        SessionState.SetString(SessionPhaseKey, RunningPhase);
        waitFrames = 12;
        observedOutcome = DRPGBattleOutcome.None;
        secondLegStarted = false;
        forcedOutcomePending = false;
        forcedOutcome = DRPGBattleOutcome.None;
        seenVictory = false;
        seenDefeat = false;
        expeditionPlayer = null;
        verifyFirstPlayerUiResolution = false;
        firstUiTargetHpBefore = 0f;
        firstUiTarget = null;
        firstUiResolutionDeadline = 0d;
        legacyEnemyPreviewCount = 0;
        hiddenOrdinaryPreviewCount = 0;
        pendingLegacyEnemyPreviewTargetId = 0;
        legacyEnemyPreviewFeedbackVerified = false;
        enemyPlanChangeCount = 0;
        enemyTargetDisclosureCount = 0;
        initialPresentationInspected = false;
        initialPresentationDeadline = 0d;
        foundationProbeSummary = ExtractProbeCount(savedFoundationSummary);
        smokeResonancePartnerId = string.Empty;
        smokeResonanceSkillId = string.Empty;
        smokeResonanceProgress = 0;
        smokeResonancePreserved = false;
        smokeResonanceCleared = false;
        smokeBattleCamera = null;
        cameraPlayCountBeforeFirstAttack = 0;
        cameraRestoreCountBeforeFirstAttack = 0;
        conflictCameraObserved = false;
        firstUiDamageObserved = false;
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
                StartAndInspectBattle();
                return;
            }

            if (observedOutcome != DRPGBattleOutcome.None)
            {
                HandleCompletedBattle();
                return;
            }

            if (forcedOutcomePending)
            {
                forcedOutcomePending = false;
                Assert(session.TryForceDevelopmentOutcome(
                        forcedOutcome,
                        out string forceError),
                    "Could not force the second smoke outcome: " + forceError);
                return;
            }

            if (!initialPresentationInspected)
            {
                InspectStartedBattle();
                return;
            }

            if (pendingLegacyEnemyPreviewTargetId > 0)
            {
                VerifyEnemyTargetFeedback(pendingLegacyEnemyPreviewTargetId);
                pendingLegacyEnemyPreviewTargetId = 0;
                legacyEnemyPreviewFeedbackVerified = true;
            }

            if (verifyFirstPlayerUiResolution)
            {
                ObserveFirstAttackCamera();
                if (firstUiTarget != null &&
                    firstUiTarget.CurrentHp < firstUiTargetHpBefore)
                {
                    firstUiDamageObserved = true;
                    Assert(conflictCameraObserved,
                        "The first ordinary attack resolved without playing the " +
                        "configured perspective conflict shot.");
                }

                if (firstUiDamageObserved &&
                    smokeBattleCamera != null &&
                    smokeBattleCamera.RestoreCount >
                        cameraRestoreCountBeforeFirstAttack)
                {
                    verifyFirstPlayerUiResolution = false;
                    firstUiTarget = null;
                }
                else if (EditorApplication.timeSinceStartup <
                    firstUiResolutionDeadline)
                {
                    return;
                }
                else
                {
                    throw new InvalidOperationException(
                        "The first ordinary attack did not complete and restore its " +
                        "camera scope before the real-time deadline. phase=" +
                        session.Phase + "; activeAction=" +
                        (session.ActiveAction != null) + "; hp=" +
                        (firstUiTarget != null ? firstUiTarget.CurrentHp : -1f) +
                        "; cameraPlayed=" + conflictCameraObserved +
                        "; cameraRestores=" +
                        (smokeBattleCamera != null
                            ? smokeBattleCamera.RestoreCount
                            : -1));
                }
            }

            DriveCurrentTurn();
        }
        catch (Exception exception)
        {
            FailAndExit(exception);
        }
    }

    private static void StartAndInspectBattle()
    {
        Assert(SessionState.GetBool(FoundationPassedKey, false),
            "PlayMode smoke lost its EditMode foundation preflight receipt.");
        Assert(!string.IsNullOrWhiteSpace(SessionState.GetString(
                FoundationSummaryKey,
                string.Empty)),
            "PlayMode smoke lost its EditMode foundation preflight summary.");

        GameFlowRoot root = GameFlowRoot.Instance;
        Assert(root != null, "GameFlowRoot.Instance is missing in Play Mode.");
        Assert(root.drpgBattleFlow != null, "DRPGBattleFlowRoot is not connected.");
        Assert(root.drpgGameLoop != null,
            "DRPGGameLoopRuntimeRoot is not connected.");
        HuntFlowDataBus titleHuntData = root.huntFlow != null
            ? root.huntFlow.huntFlowDataBus
            : null;
        Assert(titleHuntData != null,
            "Title phase requires an initialized Hunt data bus.");
        Assert(titleHuntData.player == null,
            "Title phase must not spawn the Hunt player before an expedition launch.");
        Assert(!titleHuntData.isRunActive,
            "Title phase must keep the Hunt state machine inactive.");
        Assert(!root.drpgGameLoop.CanBeginBattle,
            "Title must not expose exploration battle authority.");

        DRPGGameLoopView gameLoopView =
            root.GetComponentInChildren<DRPGGameLoopView>(true);
        Assert(gameLoopView != null,
            "DRPGGameLoopView is not connected to the runtime root.");
        SerializedObject gameLoopViewSo = new SerializedObject(gameLoopView);
        GameObject titleRoot = gameLoopViewSo.FindProperty("titleRoot")
            .objectReferenceValue as GameObject;
        GameObject strategicHubRoot = gameLoopViewSo.FindProperty(
            "strategicHubRoot").objectReferenceValue as GameObject;
        GameObject hubHomeRoot = gameLoopViewSo.FindProperty("hubHomeRoot")
            .objectReferenceValue as GameObject;
        GameObject dungeonSelectionRoot = gameLoopViewSo.FindProperty(
            "dungeonSelectionRoot").objectReferenceValue as GameObject;
        Button startButton = gameLoopViewSo.FindProperty("startButton")
            .objectReferenceValue as Button;
        Button openDungeonButton = gameLoopViewSo.FindProperty(
            "openDungeonSelectionButton").objectReferenceValue as Button;
        Button testExpeditionButton = gameLoopViewSo.FindProperty(
            "testExpeditionButton").objectReferenceValue as Button;
        Button hubBackButton = gameLoopViewSo.FindProperty("hubBackButton")
            .objectReferenceValue as Button;
        Button returnToTitleButton = gameLoopViewSo.FindProperty(
            "returnToTitleButton").objectReferenceValue as Button;
        Assert(titleRoot != null && titleRoot.activeInHierarchy &&
            strategicHubRoot != null && !strategicHubRoot.activeSelf,
            "Title view must be the only visible application page at boot.");
        Assert(startButton != null && openDungeonButton != null &&
            testExpeditionButton != null && hubBackButton != null &&
            returnToTitleButton != null,
            "Strategic Hub runtime button references are incomplete.");

        startButton.onClick.Invoke();
        Assert(root.drpgGameLoop.AnkalutePhase ==
            AnkaluteGameLoopPhase.StrategicHub,
            "Title Start button must enter StrategicHub instead of exploration.");
        Assert(root.drpgGameLoop.HubSnapshot != null &&
            root.drpgGameLoop.HubSnapshot.Route.Equals(StrategicHubRoute.Home),
            "StrategicHub entry must land on HubHome.");
        Assert(!root.drpgGameLoop.CanBeginBattle,
            "StrategicHub must not expose exploration battle authority.");
        Assert(root.huntFlow.huntFlowDataBus.isPaused,
            "StrategicHub must keep Hunt paused.");
        Assert(strategicHubRoot.activeInHierarchy &&
            hubHomeRoot != null && hubHomeRoot.activeInHierarchy &&
            dungeonSelectionRoot != null && !dungeonSelectionRoot.activeSelf,
            "Strategic Hub must render HubHome after Start.");

        GameUIRoot applicationUiRoot = GameUIRoot.Instance;
        Assert(applicationUiRoot != null,
            "GameUIRoot is missing in StrategicHub.");
        GameMenuInputRouter blockedMenuRouter =
            applicationUiRoot.GetComponent<GameMenuInputRouter>();
        Assert(blockedMenuRouter != null,
            "GameMenuInputRouter is missing in StrategicHub.");
        blockedMenuRouter.Open();
        Assert(applicationUiRoot.ModalCoordinator.ActiveOwner == null &&
            (applicationUiRoot.GameMenu == null ||
             !applicationUiRoot.GameMenu.IsOpen),
            "Game Menu must not open outside ExpeditionExploration.");
        DRPGPartyBackpackInputRouter blockedBackpackRouter =
            applicationUiRoot.GetComponent<DRPGPartyBackpackInputRouter>();
        Assert(blockedBackpackRouter != null,
            "DRPGPartyBackpackInputRouter is missing in StrategicHub.");
        blockedBackpackRouter.Open();
        Assert(applicationUiRoot.ModalCoordinator.ActiveOwner == null,
            "Party Backpack must not open outside ExpeditionExploration.");

        encounter = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>(
            EncounterPath);
        Assert(encounter != null, "Test encounter asset is missing.");
        DRPGBattleDebugLauncher launcher =
            root.GetComponent<DRPGBattleDebugLauncher>();
        Assert(launcher != null, "DRPGBattleDebugLauncher is not connected.");
        DRPGPartySetupSO smokeParty = new SerializedObject(launcher)
            .FindProperty("party").objectReferenceValue as DRPGPartySetupSO;
        Assert(smokeParty != null, "Smoke battle party is not configured.");
        bool illegalDirectStart = root.drpgBattleFlow.TryBeginBattle(
            new DRPGBattleStartRequest(smokeParty, encounter, default, 42),
            out DRPGBattleSession illegalSession,
            out _);
        Assert(!illegalDirectStart && illegalSession == null &&
            root.drpgBattleFlow.CurrentSession == null,
            "Direct TryBeginBattle must not bypass the StrategicHub phase gate.");

        returnToTitleButton.onClick.Invoke();
        Assert(root.drpgGameLoop.AnkalutePhase == AnkaluteGameLoopPhase.Title &&
            titleRoot.activeInHierarchy && !strategicHubRoot.activeSelf,
            "HubHome ReturnToTitle button must restore the title page.");
        startButton.onClick.Invoke();
        Assert(root.drpgGameLoop.AnkalutePhase ==
            AnkaluteGameLoopPhase.StrategicHub,
            "Title must be able to re-enter StrategicHub.");

        openDungeonButton.onClick.Invoke();
        Assert(root.drpgGameLoop.HubSnapshot.Route.Equals(
                StrategicHubRoute.DungeonSelection),
            "DungeonSelection route was not committed.");
        Assert(!hubHomeRoot.activeSelf &&
            dungeonSelectionRoot.activeInHierarchy,
            "DungeonSelection page did not replace HubHome.");
        hubBackButton.onClick.Invoke();
        Assert(root.drpgGameLoop.HubSnapshot.Route.Equals(
                StrategicHubRoute.Home) && hubHomeRoot.activeInHierarchy,
            "DungeonSelection Back button did not restore HubHome.");
        openDungeonButton.onClick.Invoke();

        HuntPlayerSpawner huntSpawner = root.huntFlow.huntPlayerSpawner;
        GameObject availableActorPrefab = huntSpawner != null
            ? huntSpawner.actorPrefab
            : null;
        Assert(huntSpawner != null && availableActorPrefab != null,
            "Fault-injection fixture requires the configured Hunt spawner.");
        bool loggingWasEnabled = Debug.unityLogger.logEnabled;
        huntSpawner.actorPrefab = null;
        Debug.unityLogger.logEnabled = false;
        try
        {
            testExpeditionButton.onClick.Invoke();
            Assert(root.drpgGameLoop.AnkalutePhase ==
                    AnkaluteGameLoopPhase.StrategicHub &&
                root.drpgGameLoop.HubSnapshot.Route.Equals(
                    StrategicHubRoute.Home),
                "Failed Hunt activation must compensate to HubHome.");
            Assert(!root.drpgGameLoop.CanUseExplorationUI &&
                !root.drpgGameLoop.CanBeginBattle,
                "Failed Hunt activation must keep exploration gates closed.");
            Assert(root.huntFlow.huntFlowDataBus.isPaused,
                "Failed Hunt activation must keep Hunt paused.");
            Assert(hubHomeRoot.activeInHierarchy &&
                !dungeonSelectionRoot.activeSelf,
                "Failed Hunt activation must render HubHome.");
        }
        finally
        {
            huntSpawner.actorPrefab = availableActorPrefab;
            Debug.unityLogger.logEnabled = loggingWasEnabled;
        }

        openDungeonButton.onClick.Invoke();
        Assert(root.drpgGameLoop.HubSnapshot.Route.Equals(
                StrategicHubRoute.DungeonSelection),
            "Hub must remain usable after compensated activation failure.");
        testExpeditionButton.onClick.Invoke();
        Assert(root.drpgGameLoop.AnkalutePhase ==
            AnkaluteGameLoopPhase.ExpeditionExploration,
            "Test expedition button must enter ExpeditionExploration.");
        Assert(!strategicHubRoot.activeSelf,
            "Launching an expedition must hide the Strategic Hub canvas root.");
        Assert(root.drpgGameLoop.CanBeginBattle,
            "The launched expedition did not expose battle authority.");
        Assert(root.huntFlow.huntFlowDataBus.player != null,
            "Expedition launch must activate Hunt and produce a player.");
        expeditionPlayer = root.huntFlow.huntFlowDataBus.player;
        Assert(!root.huntFlow.huntFlowDataBus.isPaused,
            "Expedition launch must release the application pause gate.");
        ConfigureSmokeResonance(root);

        HuntFlowDataBus huntData = root.huntFlow != null
            ? root.huntFlow.huntFlowDataBus
            : null;
        hasHuntData = huntData != null;
        huntPauseBefore = hasHuntData && huntData.isPaused;
        CharacterControlAuthority authority =
            huntData != null && huntData.player != null
                ? huntData.player.controlAuthority
                : null;
        hasAuthority = authority != null;
        moveAllowedBefore = !hasAuthority ||
            authority.IsAllowed(CharacterControlChannel.Move);

        DRPGEncounterDefinitionSO invalidEncounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();
        try
        {
            SerializedObject invalidEncounterSo =
                new SerializedObject(invalidEncounter);
            invalidEncounterSo.FindProperty("encounterId").stringValue = " ";
            invalidEncounterSo.ApplyModifiedPropertiesWithoutUndo();
            bool invalidEncounterStarted = root.drpgBattleFlow.TryBeginBattle(
                new DRPGBattleStartRequest(
                    smokeParty,
                    invalidEncounter,
                    default, 42),
                out DRPGBattleSession invalidEncounterSession,
                out string invalidEncounterError);
            Assert(!invalidEncounterStarted && invalidEncounterSession == null &&
                root.drpgBattleFlow.CurrentSession == null,
                "A blank EncounterId must be rejected before session creation.");
            Assert(root.drpgBattleFlow.LastStartResult ==
                    DRPGBattleStartResult.InvalidConfiguration &&
                invalidEncounterError.Contains("EncounterId"),
                "Blank EncounterId rejection must expose its configuration error.");
            Assert(root.drpgGameLoop.CanBeginBattle,
                "Rejected EncounterId must leave expedition exploration intact.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(invalidEncounter);
        }

        DRPGBattleStartResult result = launcher.StartTestBattle();
        Assert(result == DRPGBattleStartResult.Started,
            "Test battle did not start: " + result);

        battleFlow = root.drpgBattleFlow;
        session = battleFlow.CurrentSession;
        Assert(session != null, "Battle session is missing after a Started result.");
        session.BattleEnded += OnBattleEnded;
        session.CommandPreviewed += OnCommandPreviewed;
        session.EnemyPlansChanged += OnEnemyPlansChanged;
        session.EnemyTargetDisclosureChanged += OnEnemyTargetDisclosureChanged;
        initialPresentationDeadline = EditorApplication.timeSinceStartup + 6d;
        waitFrames = 1;
    }

    private static void InspectStartedBattle()
    {
        GameFlowRoot root = GameFlowRoot.Instance;
        Assert(root != null, "GameFlowRoot.Instance disappeared in Play Mode.");
        HuntFlowDataBus huntData = root.huntFlow != null
            ? root.huntFlow.huntFlowDataBus
            : null;
        CharacterControlAuthority authority =
            huntData != null && huntData.player != null
                ? huntData.player.controlAuthority
                : null;

        DRPGBattlePresentationRoot presentation =
            root.GetComponentInChildren<DRPGBattlePresentationRoot>(true);
        Assert(presentation != null, "Battle presentation is missing.");
        DRPGBattlePresentationEventBridge eventBridge =
            presentation.GetComponent<DRPGBattlePresentationEventBridge>();
        Assert(eventBridge != null, "Presentation event bridge is missing.");
        DRPGBattlePresentationDirector director =
            presentation.GetComponent<DRPGBattlePresentationDirector>();
        Assert(director != null, "Presentation director is missing.");
        if ((eventBridge.PendingSignalCount > 0 ||
             director.InputGate.IsBlocked) &&
            EditorApplication.timeSinceStartup < initialPresentationDeadline)
        {
            waitFrames = 1;
            return;
        }
        Assert(eventBridge.PendingSignalCount == 0,
            "Presentation event bridge did not flush its initial signals.");
        Assert(!director.InputGate.IsBlocked,
            "Initial turn presentation did not release its input gate.");

        BattleWorldRuntimeHost worldHost =
            presentation.GetComponent<BattleWorldRuntimeHost>();
        Assert(worldHost != null && worldHost.WorldInstance != null,
            "Scene-space battle world was not created.");
        Assert(worldHost.WorldInstance.transform.parent == null,
            "Battle world must remain a top-level scene object outside UGUI.");
        smokeBattleCamera = worldHost.CameraPresenter;
        Assert(smokeBattleCamera != null &&
            smokeBattleCamera.BattleCamera != null,
            "Perspective battle camera presenter is missing.");
        Assert(!smokeBattleCamera.BattleCamera.orthographic,
            "The isolated battle camera must remain perspective after entry.");

        Assert(session.Participants.Count == 9,
            "Expected protagonist + 2 party + 6 initially deployed enemies.");
        Assert(session.ReserveEnemyCount == 4,
            "Expected four configured enemies to remain in the reserve queue.");
        Assert(CountRole(DRPGBattleRosterRole.Protagonist) == 1,
            "Expected exactly one protagonist.");
        Assert(CountRole(DRPGBattleRosterRole.PartyMember) == 2,
            "Expected exactly two ordinary party members.");
        Assert(CountRole(DRPGBattleRosterRole.Enemy) == 6,
            "Expected exactly six initially deployed enemies.");
        Assert(session.ResonanceAccess != null,
            "The battle session is missing its resonance access seam.");
        Assert(string.Equals(
                session.ResonanceAccess.SelectedPartnerCharacterId,
                smokeResonancePartnerId,
                StringComparison.Ordinal),
            "The battle session did not receive the selected resonance partner.");
        IReadOnlyList<DRPGBattleParticipantViewModel> orderedParty =
            new DRPGBattleViewModel(session).PartyMembers;
        Assert(orderedParty.Count >= 2,
            "The battle HUD requires protagonist and resonance partner entries.");
        Assert(orderedParty[0].Participant.RosterRole ==
                DRPGBattleRosterRole.Protagonist,
            "The protagonist must remain first in the battle HUD.");
        Assert(string.Equals(
                orderedParty[1].Participant.Profile.CombatantId,
                smokeResonancePartnerId,
                StringComparison.Ordinal),
            "The selected resonance partner must be second in the battle HUD.");
        Assert(Mathf.Approximately(Time.timeScale, 1f),
            "Battle overlay must not freeze Time.timeScale.");
        if (hasHuntData)
            Assert(root.huntFlow.huntFlowDataBus.isPaused,
                "Exploration Hunt flow was not paused.");
        if (hasAuthority)
            Assert(!authority.IsAllowed(CharacterControlChannel.Move),
                "Exploration movement authority was not blocked.");

        Assert(director != null, "Presentation director is missing.");
        Assert(
            presentation.GetComponent<
                DRPGBattleActionTransactionCoordinator>() != null,
            "Transaction coordinator is missing.");
        AssertNoMissingScripts(presentation.gameObject);
        SerializedObject presentationSo = new SerializedObject(presentation);
        GameObject visibleRoot = presentationSo.FindProperty("visibleRoot")
            .objectReferenceValue as GameObject;
        Assert(visibleRoot != null && visibleRoot.activeInHierarchy,
            "Battle visual root is not visible.");

        BattleActorPresenter actorPresenter =
            presentation.GetComponentInChildren<BattleActorPresenter>(true);
        Assert(actorPresenter != null && actorPresenter.gameObject.activeInHierarchy,
            "Current actor panel is not active.");
        Image actorPortrait = new SerializedObject(actorPresenter)
            .FindProperty("portrait").objectReferenceValue as Image;
        DRPGBattleUGUISurface surface =
            presentation.GetComponent<DRPGBattleUGUISurface>();
        Assert(actorPortrait != null &&
            actorPortrait.sprite == session.CurrentActor.Profile.FullPortrait,
            "Right-side portrait is not bound to the current acting character. " +
            "currentRuntime=" + session.CurrentActor.RuntimeId +
            "; boundRuntime=" + actorPresenter.BoundRuntimeId +
            "; expectedSprite=" +
            (session.CurrentActor.Profile.FullPortrait != null
                ? session.CurrentActor.Profile.FullPortrait.name
                : "<null>") +
            "; actualSprite=" +
            (actorPortrait != null && actorPortrait.sprite != null
                ? actorPortrait.sprite.name
                : "<null>") +
            "; bridgeEnabled=" +
            (eventBridge != null && eventBridge.isActiveAndEnabled) +
            "; pendingSignals=" +
            (eventBridge != null ? eventBridge.PendingSignalCount : -1) +
            "; snapshotRuntime=" +
            (surface != null && surface.LastSnapshot != null &&
             surface.LastSnapshot.CurrentActor != null
                ? surface.LastSnapshot.CurrentActor.RuntimeId
                : 0));
        Assert(actorPresenter.GetComponentInChildren<SpriteBattleActorAnimationDriver>(true) != null,
            "Current actor animation-driver seam is missing.");

        BattleWorldBackgroundPresenter worldBackground =
            worldHost.Resolve<BattleWorldBackgroundPresenter>();
        Assert(worldBackground != null,
            "Scene-space battle background presenter is missing.");
        SpriteRenderer background = new SerializedObject(worldBackground)
            .FindProperty("backgroundRenderer")
            .objectReferenceValue as SpriteRenderer;
        Assert(background != null &&
            background.sprite == encounter.BattleBackground,
            "Encounter-specific battle background was not bound to the world stage.");

        BattlePartyMemberView[] partyViews =
            presentation.GetComponentsInChildren<BattlePartyMemberView>(true);
        BattleTimelineEntryView[] timelineViews =
            presentation.GetComponentsInChildren<BattleTimelineEntryView>(true);
        Assert(CountActiveSelf(partyViews) == 3,
            "Bottom HUD must show the battlefield protagonist and two party members.");
        int hybridTimelineCount = new DRPGBattleViewModel(session).Timeline.Count;
        Assert(CountActiveSelf(timelineViews) == hybridTimelineCount,
            "Timeline view must match the hybrid player/ready-enemy timeline.");

        BattlefieldEnemyRosterView enemyRoster =
            presentation.GetComponentInChildren<BattlefieldEnemyRosterView>(true);
        Assert(enemyRoster != null,
            "The central battlefield enemy roster is missing.");
        Assert(enemyRoster.ActiveEnemyCount == 6,
            "The central battlefield must show all six initially deployed enemies.");
        Assert(CountVisibleEnemyWarnings(presentation) > 0,
            "At least one ordinary enemy intent warning must be visible in battle.");

        BattleCommandMenu commandMenu =
            presentation.GetComponentInChildren<BattleCommandMenu>(true);
        Assert(commandMenu != null, "The five-category battle command menu is missing.");
        Button attackButton = new SerializedObject(commandMenu)
            .FindProperty("basicAttackButton").objectReferenceValue as Button;
        Assert(attackButton != null && attackButton.interactable,
            "The ordinary-attack command must be clickable on the first player turn.");

        firstUiTarget = FirstLivingEnemy();
        Assert(firstUiTarget != null, "The first UI target is missing.");
        firstUiTargetHpBefore = firstUiTarget.CurrentHp;
        attackButton.onClick.Invoke();
        Assert(session.Phase == DRPGBattlePhase.TargetSelection,
            "Ordinary attack must enter target selection.");
        Assert(Mathf.Approximately(firstUiTarget.CurrentHp, firstUiTargetHpBefore),
            "Ordinary attack must not auto-hit the first enemy.");

        BattlefieldEnemyView firstEnemyView = FindEnemyView(
            enemyRoster,
            firstUiTarget.RuntimeId);
        Assert(firstEnemyView != null && firstEnemyView.IsSelectable,
            "The chosen enemy target must be interactable.");
        Button enemyButton = new SerializedObject(firstEnemyView)
            .FindProperty("targetButton").objectReferenceValue as Button;
        Assert(enemyButton != null, "The enemy target button is missing.");
        cameraPlayCountBeforeFirstAttack = smokeBattleCamera.PlayCount;
        cameraRestoreCountBeforeFirstAttack = smokeBattleCamera.RestoreCount;
        conflictCameraObserved = false;
        firstUiDamageObserved = false;
        enemyButton.onClick.Invoke();
        Assert(firstEnemyView.CurrentFeedback == DRPGBattleTargetFeedback.Selected,
            "Clicking an enemy must show selected-target feedback before damage.");
        Assert(Mathf.Approximately(firstUiTarget.CurrentHp, firstUiTargetHpBefore),
            "Selected-target feedback must appear before command resolution.");
        verifyFirstPlayerUiResolution = true;
        firstUiResolutionDeadline = EditorApplication.timeSinceStartup + 2.5d;
        waitFrames = 1;
        initialPresentationInspected = true;

        ScreenCapture.CaptureScreenshot(GetProjectPath(ScreenshotRelativePath));
    }

    private static void DriveCurrentTurn()
    {
        if (session == null || !session.IsActive || session.CurrentActor == null)
            return;

        if (session.Phase != DRPGBattlePhase.AwaitingCommand)
            return;

        if (session.CurrentActor.Side == DRPGBattleSide.Enemy)
            return;

        BattleParticipantRuntime actor = session.CurrentActor;
        DRPGSkillSO skill = actor.Build.BasicAttack;
        BattleParticipantRuntime target = FirstLivingLegalEnemy(actor, skill);
        if (skill == null || target == null)
        {
            DRPGBattleCommand waitCommand =
                DRPGBattleCommand.Wait(actor.RuntimeId);
            string waitError;
            bool waitAccepted = session.HasStagedActionConsumer
                ? session.TryPrepareCommand(
                    waitCommand,
                    out _,
                    out waitError)
                : session.TrySubmitCommand(waitCommand, out waitError);
            Assert(waitAccepted,
                "Player-side wait command was rejected: " + waitError);
            return;
        }
        DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
            actor.RuntimeId,
            target.RuntimeId,
            skill);
        string error;
        bool accepted = session.HasStagedActionConsumer
            ? session.TryPrepareCommand(command, out _, out error)
            : session.TrySubmitCommand(command, out error);
        Assert(accepted,
            "Player-side command was rejected: " + error);
    }

    private static void HandleCompletedBattle()
    {
        DRPGBattleOutcome completedOutcome = observedOutcome;
        observedOutcome = DRPGBattleOutcome.None;
        bool firstLeg = !secondLegStarted;

        Assert(
            completedOutcome == DRPGBattleOutcome.Victory ||
            completedOutcome == DRPGBattleOutcome.Defeat,
            "Automated formation battle ended without a legal combat result: " +
            completedOutcome);
        Assert(battleFlow != null && battleFlow.CurrentSession == null,
            "Battle flow did not release the completed session.");
        if (firstLeg)
            VerifyPrimaryBattleCoverage();

        GameFlowRoot root = GameFlowRoot.Instance;
        Assert(root != null && root.drpgGameLoop != null,
            "The game loop disappeared after battle completion.");
        HandleApplicationReturn(root, completedOutcome, firstLeg);
        seenVictory |= completedOutcome == DRPGBattleOutcome.Victory;
        seenDefeat |= completedOutcome == DRPGBattleOutcome.Defeat;
        VerifyPresentationReleased(root);
        UnsubscribeCurrentSession();

        if (firstLeg)
        {
            secondLegStarted = true;
            StartForcedSecondLeg(root);
            return;
        }

        Assert(seenVictory && seenDefeat,
            "The deterministic two-leg smoke must cover Victory and Defeat.");
        Assert(smokeResonancePreserved,
            "Resonance progress was not preserved across a same-expedition battle boundary.");
        Assert(smokeResonanceCleared,
            "Resonance progress was not cleared when the expedition ended.");
        VerifyExplorationRestored(root);
        VerifyGameMenuRuntime();

        WriteReport(
            "PASS DRPG PlayMode smoke test\n" +
            "foundationProbes=" + foundationProbeSummary + "\n" +
            "topLoop=Title>StrategicHub>DungeonSelection>Expedition\n" +
            "outcomes=Victory+Defeat\n" +
            "defeatReturn=StrategicHub/HubHome\n" +
            "secondExpedition=same_player_resumed\n" +
            "activationFailure=compensated_to_hub_with_gate_closed\n" +
            "invalidEncounterGate=rejected_pre_session\n" +
            "roster=protagonist+2_party+6_initial_enemies+4_reserve\n" +
            "timeline=hybrid_player_plus_warning0_enemy\npartyHud=3\n" +
            "background=encounter_SO\n" +
            "currentActorPortrait=dynamic\n" +
            "centralEnemies=2\n" +
            "playerTargeting=basic_command_then_enemy_click\n" +
            "battleCamera=perspective_conflict_framed_and_restored\n" +
            "enemyIntent=warning_badge_and_hidden_target_validated\n" +
            "gameMenu=status_tree_loadout_character_switch\n" +
            "gameMenuModal=released\n" +
            "resonance=runtime_connected_cross_battle_preserved_exit_cleared\n" +
            "explorationSuspension=restored\n" +
            "timeScale=1\n" +
            "screenshot=" + ScreenshotRelativePath);
        CleanupAndExit();
    }

    private static void ObserveFirstAttackCamera()
    {
        if (smokeBattleCamera == null || conflictCameraObserved)
            return;
        if (smokeBattleCamera.PlayCount <= cameraPlayCountBeforeFirstAttack)
            return;
        if (string.IsNullOrEmpty(smokeBattleCamera.LastPlayedShotId) ||
            smokeBattleCamera.LastPlayedShotId.IndexOf(
                "conflict.default.framed",
                StringComparison.Ordinal) < 0)
        {
            return;
        }

        Assert(!smokeBattleCamera.BattleCamera.orthographic,
            "Conflict camera playback must not switch back to orthographic.");
        conflictCameraObserved = true;
    }

    private static void VerifyPrimaryBattleCoverage()
    {
        Assert(enemyPlanChangeCount > 0,
            "Ordinary enemy intent plans never advanced during the smoke battle.");
        Assert(hiddenOrdinaryPreviewCount == 0,
            "Hidden ordinary intents must not leak through legacy command previews.");
        Assert(legacyEnemyPreviewCount > 0,
            "The unchanged telegraphed-special flow never previewed its target.");
        Assert(legacyEnemyPreviewFeedbackVerified,
            "The unchanged telegraphed-special target feedback was not visible.");
        Assert(enemyTargetDisclosureCount == 0,
            "The hidden basic attack must not disclose its committed target.");
    }

    private static void HandleApplicationReturn(
        GameFlowRoot root,
        DRPGBattleOutcome outcome,
        bool firstLeg)
    {
        if (outcome == DRPGBattleOutcome.Victory)
        {
            Assert(GetSmokeResonanceProgress(root) >= smokeResonanceProgress,
                "Victory must preserve resonance progress inside the same expedition.");
            smokeResonancePreserved = true;
            Assert(root.drpgGameLoop.AnkalutePhase ==
                AnkaluteGameLoopPhase.BattleSettlement,
                "Victory must enter BattleSettlement before exploration resumes.");
            Assert(root.drpgGameLoop.ActiveSettlement != null,
                "Victory must expose the runtime-owned settlement payload.");
            Assert(root.drpgGameLoop.CompleteSettlement(),
                "The smoke test could not complete the victory settlement.");
            Assert(root.drpgGameLoop.ActiveSettlement == null,
                "Continuing must clear the settlement payload.");
        }
        else
        {
            Assert(GetSmokeResonanceProgress(root) == 0,
                "Defeat must clear expedition-scoped resonance progress.");
            smokeResonanceCleared = true;
            Assert(root.drpgGameLoop.AnkalutePhase ==
                AnkaluteGameLoopPhase.StrategicHub,
                "Defeat must return to StrategicHub instead of Title.");
            Assert(root.drpgGameLoop.HubSnapshot != null &&
                root.drpgGameLoop.HubSnapshot.Route.Equals(
                    StrategicHubRoute.Home),
                "Defeat must reset StrategicHub navigation to HubHome.");
            Assert(!root.drpgGameLoop.CanBeginBattle,
                "Defeat Hub must not expose battle authority.");
            Assert(root.huntFlow.huntFlowDataBus.isPaused,
                "Defeat Hub must keep the existing exploration paused.");
            Assert(root.gameFlowArbitrator.huntFlowSM != null &&
                !root.gameFlowArbitrator.huntFlowSM.IsActive,
                "Defeat must deactivate Hunt until the next expedition.");
            Assert(ReferenceEquals(
                    expeditionPlayer,
                    root.huntFlow.huntFlowDataBus.player),
                "Defeat Hub must retain, not duplicate, the current player.");

            CharacterUIInputRouter packageRouter =
                expeditionPlayer != null
                    ? expeditionPlayer.characterUIInputRouter
                    : null;
            Assert(packageRouter != null,
                "Defeat Hub player is missing CharacterUIInputRouter.");
            packageRouter.OpenPackageUI();
            Assert(packageRouter.CurrentContext ==
                    CharacterUIInputRouter.PlayerInputContext.Gameplay &&
                GameUIRoot.Instance.ModalCoordinator.ActiveOwner == null,
                "Legacy Package UI must not open in StrategicHub.");

            Assert(root.drpgGameLoop.OpenDungeonSelection(),
                "Defeat Hub could not reopen DungeonSelection.");
            Assert(root.drpgGameLoop.LaunchExpedition(
                    new ExpeditionStartRequest(
                        new ExpeditionId("smoke-reentry"))),
                "Defeat Hub could not launch the second expedition.");
            Assert(ReferenceEquals(
                    expeditionPlayer,
                    root.huntFlow.huntFlowDataBus.player),
                "Second launch must resume the existing player without respawn.");
            if (firstLeg)
                ConfigureSmokeResonance(root);
        }

        VerifyExplorationRestored(root);
    }

    private static void StartForcedSecondLeg(GameFlowRoot root)
    {
        Assert(GetSmokeResonanceProgress(root) >= smokeResonanceProgress,
            "The second battle must inherit resonance progress from its expedition.");
        DRPGBattleDebugLauncher launcher =
            root.GetComponent<DRPGBattleDebugLauncher>();
        Assert(launcher != null,
            "Second-leg DRPGBattleDebugLauncher is missing.");
        DRPGBattleStartResult result = launcher.StartTestBattle();
        Assert(result == DRPGBattleStartResult.Started,
            "Second smoke battle did not start: " + result);
        battleFlow = root.drpgBattleFlow;
        session = battleFlow.CurrentSession;
        Assert(session != null, "Second smoke battle session is missing.");
        session.BattleEnded += OnBattleEnded;
        forcedOutcome = seenVictory
            ? DRPGBattleOutcome.Defeat
            : DRPGBattleOutcome.Victory;
        forcedOutcomePending = true;
        waitFrames = 2;
    }

    private static void VerifyExplorationRestored(GameFlowRoot root)
    {
        Assert(root.drpgGameLoop.AnkalutePhase ==
            AnkaluteGameLoopPhase.ExpeditionExploration,
            "Result handling must return to ExpeditionExploration.");
        Assert(root.drpgGameLoop.CanBeginBattle,
            "Restored expedition must expose battle authority.");
        HuntFlowDataBus huntData = root.huntFlow != null
            ? root.huntFlow.huntFlowDataBus
            : null;
        if (hasHuntData)
            Assert(huntData != null && huntData.isPaused == huntPauseBefore,
                "Exploration pause state was not restored.");
        if (hasAuthority && huntData != null && huntData.player != null)
        {
            Assert(
                huntData.player.controlAuthority.IsAllowed(
                    CharacterControlChannel.Move) == moveAllowedBefore,
                "Character control authority was not restored.");
        }
    }

    private static void VerifyPresentationReleased(GameFlowRoot root)
    {
        DRPGBattlePresentationRoot presentation = root != null
            ? root.GetComponentInChildren<DRPGBattlePresentationRoot>(true)
            : null;
        if (presentation == null)
            return;

        DRPGBattlePresentationDirector director =
            presentation.GetComponent<DRPGBattlePresentationDirector>();
        DRPGBattleActionTransactionCoordinator transaction =
            presentation.GetComponent<DRPGBattleActionTransactionCoordinator>();
        Assert(director != null && !director.HasDanglingRuns,
            "Presentation director retained a run after battle completion.");
        Assert(director.InputGate.LeaseCount == 0,
            "Presentation input gate retained a lease after battle completion.");
        Assert(transaction != null && !transaction.HasActiveAction,
            "Transaction coordinator retained an action after battle completion.");
        GameObject visibleRoot = new SerializedObject(presentation)
            .FindProperty("visibleRoot").objectReferenceValue as GameObject;
        Assert(visibleRoot == null || !visibleRoot.activeSelf,
            "Battle overlay did not hide after result.");
    }

    private static void UnsubscribeCurrentSession()
    {
        if (session == null)
            return;
        session.BattleEnded -= OnBattleEnded;
        session.CommandPreviewed -= OnCommandPreviewed;
        session.EnemyPlansChanged -= OnEnemyPlansChanged;
        session.EnemyTargetDisclosureChanged -=
            OnEnemyTargetDisclosureChanged;
        session = null;
    }

    private static void VerifyGameMenuRuntime()
    {
        GameUIRoot uiRoot = GameUIRoot.Instance;
        Assert(uiRoot != null, "GameUIRoot is missing after battle completion.");
        GameMenuInputRouter router = uiRoot.GetComponent<GameMenuInputRouter>();
        Assert(router != null, "GameMenuInputRouter is missing from GameUIRoot.");

        router.Open();
        GameMenuController menu = uiRoot.GameMenu;
        Assert(menu != null && menu.IsOpen && menu.gameObject.activeInHierarchy,
            "ESC menu did not open through its runtime router.");
        Assert(uiRoot.ModalCoordinator.IsOwnedBy(router),
            "ESC menu did not own the shared modal lease.");
        Assert(menu.SelectableProfiles.Count == 2,
            "ESC menu must expose the two configured test characters.");
        Assert(menu.CurrentProfile != null &&
               menu.CurrentProfile.CombatantId == "ankalute_protagonist",
            "The gun profile (conceptual 安卡露忒) must be the default menu page.");

        SerializedObject menuSo = new SerializedObject(menu);
        Button treeButton = menuSo.FindProperty("skillTreeButton")
            .objectReferenceValue as Button;
        Button loadoutButton = menuSo.FindProperty("loadoutButton")
            .objectReferenceValue as Button;
        Button nextCharacterButton = menuSo.FindProperty("nextCharacterButton")
            .objectReferenceValue as Button;
        SkillTreePageView treePage = menuSo.FindProperty("skillTreePage")
            .objectReferenceValue as SkillTreePageView;
        SkillLoadoutPageView loadoutPage = menuSo.FindProperty("loadoutPage")
            .objectReferenceValue as SkillLoadoutPageView;
        Assert(treeButton != null && loadoutButton != null &&
               nextCharacterButton != null && treePage != null &&
               loadoutPage != null,
            "ESC menu navigation references are incomplete.");

        treeButton.onClick.Invoke();
        Assert(treePage.gameObject.activeSelf,
            "Skill-tree page did not become active.");
        Assert(treePage.GetComponentsInChildren<SkillTreeNodeView>(true).Length >= 17,
            "Skill-tree page did not render its 16 authored nodes.");

        loadoutButton.onClick.Invoke();
        Assert(loadoutPage.gameObject.activeSelf,
            "Four-slot skill-loadout page did not become active.");
        SerializedProperty slots = new SerializedObject(loadoutPage)
            .FindProperty("slots");
        Assert(slots != null && slots.arraySize == DRPGSkillLoadout.SlotCount,
            "Skill-loadout page does not expose exactly four slots.");

        nextCharacterButton.onClick.Invoke();
        Assert(menu.CurrentProfile != null &&
               menu.CurrentProfile.CombatantId == "ankalute_party_vanguard",
            "Character switch did not reach the melee profile (conceptual 守).");

        router.enabled = false;
        Assert(!menu.IsOpen && uiRoot.ModalCoordinator.ActiveOwner == null,
            "Disabling the menu router must close the menu and release its lease.");
        Assert(Mathf.Approximately(Time.timeScale, 1f),
            "ESC menu must not modify Time.timeScale.");
        router.enabled = true;
    }

    private static void OnBattleEnded(DRPGBattleResult result)
    {
        observedOutcome = result.Outcome;
    }

    private static void OnCommandPreviewed(DRPGBattleCommand command)
    {
        if (command == null || session == null)
            return;

        BattleParticipantRuntime actor = FindParticipant(command.ActorRuntimeId);
        if (actor == null || actor.Side != DRPGBattleSide.Enemy)
            return;

        if (command.Skill != null &&
            command.Skill.ExecutionMode == DRPGSkillExecutionMode.Standard &&
            command.Skill.EnemyTargetDisclosureMode ==
                DRPGEnemyTargetDisclosureMode.Hidden)
        {
            hiddenOrdinaryPreviewCount++;
            return;
        }

        legacyEnemyPreviewCount++;
        pendingLegacyEnemyPreviewTargetId = command.TargetRuntimeId;
    }

    private static void VerifyEnemyTargetFeedback(int runtimeId)
    {
        DRPGBattlePresentationRoot presentation = GameFlowRoot.Instance != null
            ? GameFlowRoot.Instance.GetComponentInChildren<
                DRPGBattlePresentationRoot>(true)
            : null;
        Assert(presentation != null,
            "Battle presentation disappeared during enemy preview.");

        BattleTimelineEntryView[] entries =
            presentation.GetComponentsInChildren<BattleTimelineEntryView>(true);
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].BoundRuntimeId != runtimeId)
                continue;
            Outline outline = new SerializedObject(entries[i])
                .FindProperty("targetOutline").objectReferenceValue as Outline;
            Assert(outline != null && outline.enabled,
                "Legacy enemy target intent must be visible on the timeline entry.");
            return;
        }

        BattlePartyMemberView[] partyViews =
            presentation.GetComponentsInChildren<BattlePartyMemberView>(true);
        for (int i = 0; i < partyViews.Length; i++)
        {
            if (partyViews[i].BoundRuntimeId != runtimeId)
                continue;
            Outline outline = new SerializedObject(partyViews[i])
                .FindProperty("targetOutline").objectReferenceValue as Outline;
            Assert(outline != null && outline.enabled,
                "Legacy enemy target intent must be visible on the party HUD entry.");
            return;
        }

        throw new InvalidOperationException(
            "Legacy enemy target is missing from target-feedback surfaces.");
    }

    private static void OnEnemyPlansChanged()
    {
        enemyPlanChangeCount++;
    }

    private static void OnEnemyTargetDisclosureChanged(
        DRPGEnemyActionPlanSnapshot snapshot)
    {
        enemyTargetDisclosureCount++;
    }

    private static int CountVisibleEnemyWarnings(
        DRPGBattlePresentationRoot presentation)
    {
        BattlefieldEnemyView[] views =
            presentation.GetComponentsInChildren<BattlefieldEnemyView>(true);
        int count = 0;
        for (int i = 0; i < views.Length; i++)
        {
            SerializedProperty warningRoot = new SerializedObject(views[i])
                .FindProperty("warningRoot");
            GameObject warning = warningRoot != null
                ? warningRoot.objectReferenceValue as GameObject
                : null;
            if (views[i].gameObject.activeInHierarchy &&
                warning != null && warning.activeInHierarchy)
            {
                count++;
            }
        }
        return count;
    }

    private static void ConfigureSmokeResonance(GameFlowRoot root)
    {
        Assert(root != null && root.drpgProgression != null,
            "The progression root is missing for resonance smoke setup.");
        DRPGProgressionRoot progression = root.drpgProgression;
        progression.Initialize();
        DRPGResonanceService service = progression.ResonanceService;
        DRPGPartyCompositionState party = progression.PartyComposition;
        DRPGResonanceExpeditionState expedition =
            root.drpgGameLoop != null
                ? root.drpgGameLoop.ResonanceExpedition
                : null;
        Assert(service != null && party != null && expedition != null,
            "The runtime resonance composition is incomplete.");
        Assert(root.drpgGameLoop.ResonanceAccess != null,
            "The game loop did not compose battle resonance access.");

        DRPGResonanceBranchSO branch = null;
        DRPGResonanceNodeDefinition activeNode = null;
        IReadOnlyList<DRPGResonanceBranchSO> branches =
            service.Definition.Branches;
        for (int branchIndex = 0;
             branchIndex < branches.Count && activeNode == null;
             branchIndex++)
        {
            DRPGResonanceBranchSO candidate = branches[branchIndex];
            if (candidate == null || candidate.FriendProfile == null ||
                !party.ContainsCharacterId(candidate.FriendCharacterId))
            {
                continue;
            }
            for (int nodeIndex = 0; nodeIndex < candidate.Nodes.Count; nodeIndex++)
            {
                DRPGResonanceNodeDefinition node = candidate.Nodes[nodeIndex];
                DRPGResonanceOperationResult unlocked =
                    service.TryUnlockNode(candidate.FriendCharacterId, node.NodeId);
                Assert(unlocked.Success,
                    "Could not unlock resonance smoke node " + node.NodeId +
                    ": " + unlocked.Message);
                if (node.RewardKind == DRPGResonanceNodeRewardKind.ActiveSkill &&
                    node.ActiveSkill != null)
                {
                    branch = candidate;
                    activeNode = node;
                }
            }
        }

        Assert(branch != null && activeNode != null,
            "No party-compatible active resonance node is configured.");
        DRPGResonanceOperationResult selected =
            service.TrySelectPartner(branch.FriendProfile, party);
        Assert(selected.Success,
            "Could not select the resonance smoke partner: " + selected.Message);
        DRPGResonanceOperationResult equipped = service.TryEquipActiveSkill(
            branch.FriendCharacterId,
            activeNode.ActiveSkill);
        Assert(equipped.Success,
            "Could not equip the resonance smoke skill: " + equipped.Message);

        smokeResonancePartnerId = branch.FriendCharacterId;
        smokeResonanceSkillId = activeNode.ActiveSkill.SkillId;
        smokeResonanceProgress = expedition.AddProgress(
            smokeResonancePartnerId,
            smokeResonanceSkillId,
            activeNode.RequiredBattleProgress,
            activeNode.RequiredBattleProgress);
        Assert(smokeResonanceProgress == activeNode.RequiredBattleProgress,
            "The resonance smoke skill did not reach its configured requirement.");
    }

    private static int GetSmokeResonanceProgress(GameFlowRoot root)
    {
        DRPGResonanceExpeditionState expedition = root != null &&
            root.drpgGameLoop != null
                ? root.drpgGameLoop.ResonanceExpedition
                : null;
        return expedition != null
            ? expedition.GetProgress(
                smokeResonancePartnerId,
                smokeResonanceSkillId)
            : 0;
    }

    private static string ExtractProbeCount(string probeResult)
    {
        if (string.IsNullOrWhiteSpace(probeResult))
            return "unknown";
        int open = probeResult.LastIndexOf('(');
        int close = probeResult.LastIndexOf(')');
        return open >= 0 && close > open
            ? probeResult.Substring(open + 1, close - open - 1)
            : "PASS";
    }

    private static int CountRole(DRPGBattleRosterRole role)
    {
        int count = 0;
        for (int i = 0; i < session.Participants.Count; i++)
        {
            if (session.Participants[i].RosterRole == role)
                count++;
        }
        return count;
    }

    private static int CountActiveSelf<T>(T[] views) where T : Component
    {
        int count = 0;
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].gameObject.activeSelf)
                count++;
        }
        return count;
    }

    private static BattleParticipantRuntime FirstLivingEnemy()
    {
        for (int i = 0; i < session.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = session.Participants[i];
            if (participant.Side == DRPGBattleSide.Enemy && participant.IsAlive)
                return participant;
        }
        return null;
    }

    private static BattleParticipantRuntime FirstLivingLegalEnemy(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill)
    {
        if (actor == null || skill == null)
            return null;
        for (int i = 0; i < session.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = session.Participants[i];
            if (participant.Side == DRPGBattleSide.Enemy &&
                session.CanTargetWithSkill(actor, participant, skill))
            {
                return participant;
            }
        }
        return null;
    }

    private static BattleParticipantRuntime FindParticipant(int runtimeId)
    {
        for (int i = 0; i < session.Participants.Count; i++)
        {
            if (session.Participants[i].RuntimeId == runtimeId)
                return session.Participants[i];
        }
        return null;
    }

    private static BattlefieldEnemyView FindEnemyView(
        BattlefieldEnemyRosterView roster,
        int runtimeId)
    {
        IReadOnlyList<BattlefieldEnemyView> views = roster.Views;
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null && views[i].BoundRuntimeId == runtimeId)
                return views[i];
        }
        return null;
    }

    private static void FailAndExit(Exception exception)
    {
        bool exitBatchFromEditMode = !EditorApplication.isPlaying &&
            SessionState.GetBool(BatchExitKey, false);
        SessionState.SetBool(FailureExitKey, true);
        WriteReport("FAILED DRPG PlayMode smoke test\n" + exception);
        Debug.LogException(exception);
        CleanupAndExit();
        if (exitBatchFromEditMode)
        {
            SessionState.EraseBool(BatchExitKey);
            SessionState.EraseBool(FailureExitKey);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    private static void CleanupAndExit()
    {
        EditorApplication.update -= Tick;
        if (session != null)
        {
            session.BattleEnded -= OnBattleEnded;
            session.CommandPreviewed -= OnCommandPreviewed;
        }
        session = null;
        battleFlow = null;
        encounter = null;
        firstUiTarget = null;
        SessionState.EraseString(SessionPhaseKey);
        SessionState.EraseBool(FoundationPassedKey);
        SessionState.EraseString(FoundationSummaryKey);
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
        string path = GetProjectPath(ReportRelativePath);
        File.WriteAllText(path, contents ?? string.Empty);
    }

    private static string GetProjectPath(string relativePath)
    {
        return Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            relativePath);
    }
}
#endif
