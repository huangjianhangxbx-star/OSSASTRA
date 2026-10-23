#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GalPlayModeVerificationRunner
{
    private const string ArmedKey = "ProjectANOIR.GalPlayModeVerifier.Armed.V4";
    private const string ArtifactFolder =
        "Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts";
    private const string PausedScreenshotPath =
        ArtifactFolder + "/GalPausedOverlay.png";
    private const string FullscreenScreenshotPath =
        ArtifactFolder + "/GalFullscreenExclusive.png";
    private const string ConcurrentScreenshotPath =
        ArtifactFolder + "/GalConcurrentOverlay.png";

    private const string ActorPath =
        "Assets/Script/GameFlowManager/GalSystem/Content/Test/GalActor_Test.asset";
    private const string PausedSequencePath =
        "Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test.asset";
    private const string FullscreenSequencePath =
        "Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Fullscreen.asset";
    private const string ConcurrentSequenceAPath =
        "Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_A.asset";
    private const string ConcurrentSequenceBPath =
        "Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_B.asset";
    private const string AnimePortraitPath =
        "Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png";
    private const string BackgroundPath =
        "Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png";
    private const string ViewPrefabPath =
        "Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalDialogueView.prefab";
    private const string RootPrefabPath =
        "Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalFlowRoot.prefab";

    private static VerificationPhase phase;
    private static int ticksInPhase;
    private static int verifiedRoundsStarted;
    private static int verifiedRoundsRestored;
    private static double phaseStartedAt;
    private static float previousTimeScale;
    private static bool previousHuntPaused;
    private static bool previousMoveAllowed;
    private static string previousLineText;
    private static GalFlowRoot galRoot;
    private static GameFlowArbitrator gameFlowArbitrator;
    private static HuntFlowDataBus huntData;
    private static Button confirmButton;
    private static GalSequenceSO concurrentA;
    private static GalSequenceSO concurrentB;
    private static int concurrentStartedCount;
    private static bool concurrentACompleted;
    private static bool concurrentBPreempted;
    private static Vector3 playerStartPosition;

    static GalPlayModeVerificationRunner()
    {
        if (SessionState.GetBool(ArmedKey, false))
            ArmUpdateLoop();
    }

    [MenuItem("Tools/Project ANOIR/Gal/Verify Test Vertical Slice")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GalPlayModeVerifier] Exit Play Mode before starting verification.");
            return;
        }

        VerifyEditModeComposition();
        SessionState.SetBool(ArmedKey, true);
        phase = VerificationPhase.WaitingForPlayMode;
        ticksInPhase = 0;
        phaseStartedAt = EditorApplication.timeSinceStartup;
        ArmUpdateLoop();
        EditorApplication.isPlaying = true;
    }

    public static string RunEditModeCompositionProbe()
    {
        VerifyEditModeComposition();
        return "PASS gal edit mode composition";
    }

    private static void ArmUpdateLoop()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= Tick;
            return;
        }

        try
        {
            if (phaseStartedAt <= 0d)
                phaseStartedAt = EditorApplication.timeSinceStartup;
            ticksInPhase++;
            if (EditorApplication.timeSinceStartup - phaseStartedAt > 20d)
                throw new Exception($"Timed out in phase {phase}.");

            switch (phase)
            {
                case VerificationPhase.WaitingForPlayMode:
                    if (!EditorApplication.isPlaying)
                        return;
                    MoveTo(VerificationPhase.WaitingForInitialization);
                    break;

                case VerificationPhase.WaitingForInitialization:
                    PrepareRuntimeAndQueueConcurrentA();
                    break;

                case VerificationPhase.WaitingForConcurrentA:
                    if (!VerifyConcurrentStarted(concurrentA, 1))
                        return;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.O));
                    MoveTo(VerificationPhase.WaitingForConcurrentDuplicate);
                    break;

                case VerificationPhase.WaitingForConcurrentDuplicate:
                    if (ticksInPhase < 3)
                        return;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    VerifyConcurrentGameplayInvariant();
                    if (concurrentStartedCount != 1 || galRoot.QueuedConcurrentCount != 0)
                        throw new Exception("Repeated O was not ignored as a duplicate.");
                    GalStartResult duplicateResult = gameFlowArbitrator.RequestGal(
                        new GalPlayRequest(
                            concurrentA,
                            GalPresentationMode.ConcurrentOverlay,
                            "PlayModeVerifier:DuplicateProof",
                            "debug.concurrent.a",
                            1f));
                    if (duplicateResult != GalStartResult.DuplicateIgnored)
                    {
                        throw new Exception(
                            "The active GameFlowArbitrator returned " +
                            $"{duplicateResult}, expected DuplicateIgnored.");
                    }
                    InputSystem.QueueStateEvent(
                        Keyboard.current,
                        new KeyboardState(Key.LeftShift, Key.O));
                    MoveTo(VerificationPhase.WaitingForConcurrentQueueB);
                    break;

                case VerificationPhase.WaitingForConcurrentQueueB:
                    if (galRoot.QueuedConcurrentCount != 1)
                        return;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.D));
                    MoveTo(VerificationPhase.WaitingForConcurrentMovement);
                    break;

                case VerificationPhase.WaitingForConcurrentMovement:
                    VerifyConcurrentGameplayInvariant();
                    if (Vector3.Distance(
                            playerStartPosition,
                            huntData.player.transform.position) < 0.03f)
                    {
                        return;
                    }
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    MoveTo(VerificationPhase.WaitingForConcurrentB);
                    break;

                case VerificationPhase.WaitingForConcurrentB:
                    VerifyConcurrentGameplayInvariant();
                    if (!concurrentACompleted ||
                        !VerifyConcurrentStarted(concurrentB, 2))
                    {
                        return;
                    }
                    if (galRoot.QueuedConcurrentCount != 0)
                        throw new Exception("FIFO promotion did not dequeue Concurrent B.");
                    EnsureArtifactFolder();
                    ScreenCapture.CaptureScreenshot(ConcurrentScreenshotPath, 1);
                    MoveTo(VerificationPhase.WaitingForConcurrentScreenshot);
                    break;

                case VerificationPhase.WaitingForConcurrentScreenshot:
                    if (ticksInPhase < 12)
                        return;
                    InputSystem.QueueStateEvent(
                        Keyboard.current,
                        new KeyboardState(Key.LeftShift, Key.P));
                    MoveTo(VerificationPhase.WaitingForPreemptFullscreen);
                    break;

                case VerificationPhase.WaitingForPreemptFullscreen:
                    if (!galRoot.IsActive ||
                        galRoot.ActiveMode != GalPresentationMode.FullscreenExclusive)
                    {
                        return;
                    }
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    if (!concurrentBPreempted || galRoot.QueuedConcurrentCount != 0)
                        throw new Exception("Fullscreen preemption did not stop B and clear the queue.");
                    if (!VerifyDialogueStarted(
                            GalPresentationMode.FullscreenExclusive,
                            null,
                            false))
                    {
                        return;
                    }
                    MoveTo(VerificationPhase.WaitingForPreemptFullscreenAdvance);
                    break;

                case VerificationPhase.WaitingForPreemptFullscreenAdvance:
                    if (ticksInPhase < 2)
                        return;
                    InvokeConfirmAndExpectLine(1);
                    VerifyCurrentLine(1);
                    InvokeConfirmAndExpectLine(2);
                    VerifyCurrentLine(2);
                    confirmButton.onClick.Invoke();
                    MoveTo(VerificationPhase.WaitingForNoConcurrentResume);
                    break;

                case VerificationPhase.WaitingForNoConcurrentResume:
                    if (galRoot.IsActive)
                        return;
                    if (EditorApplication.timeSinceStartup - phaseStartedAt < 1.5d)
                        return;
                    if (concurrentStartedCount != 2 || galRoot.QueuedConcurrentCount != 0)
                        throw new Exception("A preempted Concurrent request resumed unexpectedly.");
                    VerifyConcurrentGameplayInvariant();
                    CaptureRuntimeBaseline();
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.P));
                    MoveTo(VerificationPhase.WaitingForPausedDialogue);
                    break;

                case VerificationPhase.WaitingForPausedDialogue:
                    if (!VerifyDialogueStarted(
                            GalPresentationMode.PausedOverlay,
                            null,
                            true))
                    {
                        return;
                    }
                    MoveTo(VerificationPhase.WaitingForPausedScreenshot);
                    break;

                case VerificationPhase.WaitingForPausedScreenshot:
                    if (ticksInPhase < 12)
                        return;
                    InvokeConfirmAndExpectLine(1);
                    MoveTo(VerificationPhase.WaitingForPausedSecondLine);
                    break;

                case VerificationPhase.WaitingForPausedSecondLine:
                    if (ticksInPhase < 2)
                        return;
                    VerifyCurrentLine(1);
                    InvokeConfirmAndExpectLine(2);
                    MoveTo(VerificationPhase.WaitingForPausedThirdLine);
                    break;

                case VerificationPhase.WaitingForPausedThirdLine:
                    if (ticksInPhase < 2)
                        return;
                    VerifyCurrentLine(2);
                    confirmButton.onClick.Invoke();
                    MoveTo(VerificationPhase.WaitingForPausedClose);
                    break;

                case VerificationPhase.WaitingForPausedClose:
                    if (ticksInPhase < 2)
                        return;
                    VerifyClosedAndRestored(GalPresentationMode.PausedOverlay);
                    MoveTo(VerificationPhase.WaitingToQueueFullscreen);
                    break;

                case VerificationPhase.WaitingToQueueFullscreen:
                    if (ticksInPhase < 3)
                        return;
                    CaptureRuntimeBaseline();
                    InputSystem.QueueStateEvent(
                        Keyboard.current,
                        new KeyboardState(Key.LeftShift, Key.P));
                    MoveTo(VerificationPhase.WaitingForFullscreenDialogue);
                    break;

                case VerificationPhase.WaitingForFullscreenDialogue:
                    if (!VerifyDialogueStarted(
                            GalPresentationMode.FullscreenExclusive,
                            null,
                            true))
                    {
                        return;
                    }
                    MoveTo(VerificationPhase.WaitingForFullscreenScreenshot);
                    break;

                case VerificationPhase.WaitingForFullscreenScreenshot:
                    if (ticksInPhase < 12)
                        return;
                    InvokeConfirmAndExpectLine(1);
                    MoveTo(VerificationPhase.WaitingForFullscreenSecondLine);
                    break;

                case VerificationPhase.WaitingForFullscreenSecondLine:
                    if (ticksInPhase < 2)
                        return;
                    VerifyCurrentLine(1);
                    InvokeConfirmAndExpectLine(2);
                    MoveTo(VerificationPhase.WaitingForFullscreenThirdLine);
                    break;

                case VerificationPhase.WaitingForFullscreenThirdLine:
                    if (ticksInPhase < 2)
                        return;
                    VerifyCurrentLine(2);
                    confirmButton.onClick.Invoke();
                    MoveTo(VerificationPhase.WaitingForFullscreenClose);
                    break;

                case VerificationPhase.WaitingForFullscreenClose:
                    if (ticksInPhase < 2)
                        return;
                    VerifyClosedAndRestored(
                        GalPresentationMode.FullscreenExclusive);
                    VerifyBothRoundsCompleted();
                    CompleteSuccess();
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            CompleteFailure();
        }
    }

    private static void VerifyEditModeComposition()
    {
        GalActorSO actor = LoadRequiredAsset<GalActorSO>(
            ActorPath,
            "Test Actor asset");
        GalSequenceSO pausedSequence = LoadRequiredAsset<GalSequenceSO>(
            PausedSequencePath,
            "Paused test Sequence asset");
        GameObject rootPrefab = LoadRequiredAsset<GameObject>(
            RootPrefabPath,
            "GalFlowRoot prefab");

        GalSequenceSO fullscreenSequence = LoadRequiredAsset<GalSequenceSO>(
            FullscreenSequencePath,
            "Fullscreen test Sequence asset");
        Sprite animePortrait = LoadRequiredAsset<Sprite>(
            AnimePortraitPath,
            "Anime portrait Sprite");
        Sprite background = LoadRequiredAsset<Sprite>(
            BackgroundPath,
            "Fullscreen background Sprite");
        LoadRequiredAsset<GameObject>(ViewPrefabPath, "GalDialogueView prefab");

        VerifyConcurrentEditModeComposition(rootPrefab);

        if (actor.DefaultPortrait != animePortrait)
            throw new Exception("Test Actor does not use the anime portrait Sprite.");
        if (pausedSequence.DefaultPresentationMode !=
            GalPresentationMode.PausedOverlay)
        {
            throw new Exception("Paused test Sequence changed presentation mode.");
        }
        if (fullscreenSequence.DefaultPresentationMode !=
            GalPresentationMode.FullscreenExclusive)
        {
            throw new Exception("Fullscreen Sequence is not FullscreenExclusive.");
        }
        if (fullscreenSequence.DefaultBackground != background)
            throw new Exception("Fullscreen Sequence does not use the test background.");
        if (fullscreenSequence.Lines == null || fullscreenSequence.Lines.Count < 3)
            throw new Exception("Fullscreen Sequence must contain at least three lines.");

        VerifyViewPrefabComposition();
        VerifyTriggerReferences(
            rootPrefab.GetComponent<GalDebugHotkeyTrigger>(),
            pausedSequence,
            fullscreenSequence,
            "GalFlowRoot prefab");

        GameFlowRoot[] roots = UnityEngine.Object.FindObjectsByType<GameFlowRoot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (roots.Length != 1)
            throw new Exception("The active scene must contain exactly one GameFlowRoot.");

        GalFlowRoot[] galRoots = UnityEngine.Object.FindObjectsByType<GalFlowRoot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (galRoots.Length != 1)
            throw new Exception("The active scene must contain exactly one GalFlowRoot.");
        if (roots[0].galFlow != galRoots[0])
            throw new Exception("GameFlowRoot is not connected to the unique GalFlowRoot.");

        GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(
            galRoots[0].gameObject);
        if (sourceRoot != rootPrefab)
        {
            throw new Exception(
                "The connected GalFlowRoot is not an instance of PF_GalFlowRoot.");
        }

        GalDialogueView[] views =
            UnityEngine.Object.FindObjectsByType<GalDialogueView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        if (views.Length != 1)
            throw new Exception("The active scene must contain exactly one GalDialogueView.");
        if (views[0].GetComponentInParent<GalFlowRoot>(true) != galRoots[0])
            throw new Exception("The unique GalDialogueView is not owned by GalFlowRoot.");

        Canvas[] galCanvases = galRoots[0].GetComponentsInChildren<Canvas>(true);
        if (galCanvases.Length != 1 || galCanvases[0] != views[0].GetComponent<Canvas>())
        {
            throw new Exception(
                "The unique GalFlowRoot must contain exactly the GalDialogueView Canvas.");
        }

        VerifyTriggerReferences(
            galRoots[0].GetComponent<GalDebugHotkeyTrigger>(),
            pausedSequence,
            fullscreenSequence,
            "Active scene GalFlowRoot");
        GalDebugHotkeyTrigger sceneTrigger =
            galRoots[0].GetComponent<GalDebugHotkeyTrigger>();
        GameFlowArbitrator arbitrator =
            roots[0].GetComponent<GameFlowArbitrator>();
        if (arbitrator == null)
            throw new Exception("The active GameFlowRoot is missing GameFlowArbitrator.");
        RequireExactReference(
            new SerializedObject(sceneTrigger),
            "gameFlowArbitrator",
            arbitrator,
            "Active scene GalFlowRoot");
    }

    private static void VerifyConcurrentEditModeComposition(GameObject rootPrefab)
    {
        GalSequenceSO sequenceA = LoadRequiredAsset<GalSequenceSO>(
            ConcurrentSequenceAPath,
            "Concurrent A Sequence asset");
        GalSequenceSO sequenceB = LoadRequiredAsset<GalSequenceSO>(
            ConcurrentSequenceBPath,
            "Concurrent B Sequence asset");
        VerifyConcurrentSequence(sequenceA, "gal_test_concurrent_a", 0f, "A");
        VerifyConcurrentSequence(sequenceB, "gal_test_concurrent_b", 1.2f, "B");

        GalDialogueView[] nestedViews =
            rootPrefab.GetComponentsInChildren<GalDialogueView>(true);
        GameObject expectedViewPrefab = LoadRequiredAsset<GameObject>(
            ViewPrefabPath,
            "GalDialogueView prefab");
        if (nestedViews.Length != 1 ||
            PrefabUtility.GetCorrespondingObjectFromSource(
                nestedViews[0].gameObject) != expectedViewPrefab)
        {
            throw new Exception(
                "PF_GalFlowRoot must retain exactly one nested PF_GalDialogueView instance.");
        }

        var rootSerialized = new SerializedObject(rootPrefab.GetComponent<GalFlowRoot>());
        SerializedProperty capacity = rootSerialized.FindProperty(
            "maxQueuedConcurrentRequests");
        if (capacity == null || capacity.intValue != 3)
            throw new Exception("PF_GalFlowRoot Concurrent queue capacity must be exactly 3.");

        VerifyConcurrentTriggerReferences(
            rootPrefab.GetComponent<GalDebugHotkeyTrigger>(),
            sequenceA,
            sequenceB,
            "GalFlowRoot prefab");

        GalFlowRoot[] sceneRoots = UnityEngine.Object.FindObjectsByType<GalFlowRoot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (sceneRoots.Length != 1)
            throw new Exception("The active scene must contain exactly one GalFlowRoot.");
        VerifyConcurrentTriggerReferences(
            sceneRoots[0].GetComponent<GalDebugHotkeyTrigger>(),
            sequenceA,
            sequenceB,
            "Active scene GalFlowRoot");
    }

    private static void VerifyConcurrentSequence(
        GalSequenceSO sequence,
        string expectedId,
        float expectedDuration,
        string label)
    {
        if (sequence.SequenceId != expectedId ||
            sequence.DefaultPresentationMode != GalPresentationMode.ConcurrentOverlay ||
            sequence.Lines == null || sequence.Lines.Count != 2)
        {
            throw new Exception($"Concurrent {label} Sequence contract is invalid.");
        }

        for (int i = 0; i < sequence.Lines.Count; i++)
        {
            GalLine line = sequence.Lines[i];
            if (line == null || string.IsNullOrWhiteSpace(line.Text) ||
                !Mathf.Approximately(line.AutoAdvanceSecondsOverride, expectedDuration))
            {
                throw new Exception($"Concurrent {label} line {i} is invalid.");
            }
        }
    }

    private static T LoadRequiredAsset<T>(string assetPath, string label)
        where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
            throw new Exception($"{label} is missing or not imported at {assetPath}.");
        return asset;
    }

    private static void VerifyViewPrefabComposition()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(ViewPrefabPath);
        try
        {
            GalDialogueView view = contents.GetComponent<GalDialogueView>();
            if (view == null || !view.IsReady)
                throw new Exception("GalDialogueView prefab references are incomplete.");

            Transform visualRoot = contents.transform.Find("VisualRoot");
            Transform dimmer = visualRoot?.Find("GameplayBackdropDimmer");
            Transform stage = visualRoot?.Find("FullscreenBackgroundStage");
            Transform backgroundTransform = stage?.Find("BackgroundImage");
            Image backgroundImage = backgroundTransform?.GetComponent<Image>();
            AspectRatioFitter backgroundFitter =
                backgroundTransform?.GetComponent<AspectRatioFitter>();
            if (visualRoot == null || dimmer == null || stage == null ||
                backgroundTransform == null || backgroundImage == null ||
                backgroundFitter == null)
            {
                throw new Exception(
                    "GalDialogueView prefab must contain " +
                    "VisualRoot/FullscreenBackgroundStage/BackgroundImage.");
            }
            if (stage.parent != visualRoot || backgroundTransform.parent != stage)
                throw new Exception("Fullscreen background hierarchy is not exact.");
            if (stage.GetSiblingIndex() >= dimmer.GetSiblingIndex())
                throw new Exception("Fullscreen background stage must render behind the dimmer.");

            var serialized = new SerializedObject(view);
            RequireExactReference(
                serialized,
                "fullscreenBackgroundStage",
                stage.gameObject,
                "GalDialogueView prefab");
            RequireExactReference(
                serialized,
                "fullscreenBackgroundImage",
                backgroundImage,
                "GalDialogueView prefab");
            RequireExactReference(
                serialized,
                "fullscreenBackgroundFitter",
                backgroundFitter,
                "GalDialogueView prefab");
            RequireExactReference(
                serialized,
                "gameplayBackdropDimmer",
                dimmer.gameObject,
                "GalDialogueView prefab");

            Transform portraitStage = visualRoot.Find("PortraitStage");
            Transform dialoguePanel = visualRoot.Find("DialoguePanel");
            Transform card = visualRoot.Find("ConcurrentCard");
            Transform avatarMask = card?.Find("AvatarMask");
            Image avatar = avatarMask?.Find("AvatarImage")?.GetComponent<Image>();
            Transform textColumn = card?.Find("TextColumn");
            TMP_Text cardSpeaker = textColumn?.Find("SpeakerName")?.GetComponent<TMP_Text>();
            TMP_Text cardDialogue = textColumn?.Find("DialogueText")?.GetComponent<TMP_Text>();
            Image progress = card?.Find("AutoAdvanceProgress")?.GetComponent<Image>();
            CanvasGroup group = card?.GetComponent<CanvasGroup>();
            RectTransform cardRect = card as RectTransform;
            if (portraitStage == null || dialoguePanel == null || card == null ||
                avatar == null || cardSpeaker == null || cardDialogue == null ||
                progress == null || group == null || cardRect == null)
            {
                throw new Exception("GalDialogueView ConcurrentCard hierarchy is incomplete.");
            }
            if (!Mathf.Approximately(group.alpha, 0f) || group.interactable ||
                group.blocksRaycasts || avatar.raycastTarget ||
                cardSpeaker.raycastTarget || cardDialogue.raycastTarget ||
                progress.raycastTarget)
            {
                throw new Exception("ConcurrentCard must be initially hidden and input transparent.");
            }
            if (cardDialogue.overflowMode != TextOverflowModes.Ellipsis ||
                progress.type != Image.Type.Filled ||
                progress.fillMethod != Image.FillMethod.Horizontal ||
                !Mathf.Approximately(progress.fillAmount, 0f) ||
                cardRect.anchorMin != Vector2.zero ||
                cardRect.anchorMax != Vector2.zero ||
                cardRect.pivot != Vector2.zero ||
                cardRect.anchoredPosition.x < 36f || cardRect.anchoredPosition.y < 36f)
            {
                throw new Exception("ConcurrentCard layout/progress contract is invalid.");
            }
            RequireExactReference(serialized, "portraitStage", portraitStage.gameObject,
                "GalDialogueView prefab");
            RequireExactReference(serialized, "dialoguePanel", dialoguePanel.gameObject,
                "GalDialogueView prefab");
            RequireExactReference(serialized, "concurrentCard", card.gameObject,
                "GalDialogueView prefab");
            RequireExactReference(serialized, "concurrentCanvasGroup", group,
                "GalDialogueView prefab");
            RequireExactReference(serialized, "concurrentAvatarImage", avatar,
                "GalDialogueView prefab");
            RequireExactReference(serialized, "concurrentSpeakerNameText", cardSpeaker,
                "GalDialogueView prefab");
            RequireExactReference(serialized, "concurrentDialogueText", cardDialogue,
                "GalDialogueView prefab");
            RequireExactReference(serialized, "concurrentProgressFill", progress,
                "GalDialogueView prefab");

            Canvas canvas = contents.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                canvas.sortingOrder < 5000)
            {
                throw new Exception(
                    "GalDialogueView Canvas must be ScreenSpaceOverlay with order >= 5000.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void VerifyTriggerReferences(
        GalDebugHotkeyTrigger trigger,
        GalSequenceSO pausedSequence,
        GalSequenceSO fullscreenSequence,
        string ownerLabel)
    {
        if (trigger == null)
            throw new Exception($"{ownerLabel} is missing GalDebugHotkeyTrigger.");

        var serialized = new SerializedObject(trigger);
        RequireExactReference(
            serialized,
            "testSequence",
            pausedSequence,
            ownerLabel);
        RequireExactReference(
            serialized,
            "fullscreenTestSequence",
            fullscreenSequence,
            ownerLabel);
    }

    private static void VerifyConcurrentTriggerReferences(
        GalDebugHotkeyTrigger trigger,
        GalSequenceSO sequenceA,
        GalSequenceSO sequenceB,
        string ownerLabel)
    {
        if (trigger == null)
            throw new Exception($"{ownerLabel} is missing GalDebugHotkeyTrigger.");
        var serialized = new SerializedObject(trigger);
        RequireExactReference(serialized, "concurrentTestSequenceA", sequenceA, ownerLabel);
        RequireExactReference(serialized, "concurrentTestSequenceB", sequenceB, ownerLabel);
    }

    private static void RequireExactReference(
        SerializedObject serialized,
        string fieldName,
        UnityEngine.Object expected,
        string ownerLabel)
    {
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
            throw new Exception($"{ownerLabel} is missing serialized field '{fieldName}'.");
        if (property.objectReferenceValue != expected)
            throw new Exception($"{ownerLabel}.{fieldName} is not wired to the expected asset.");
    }

    private static void PrepareRuntimeAndQueueConcurrentA()
    {
        GameFlowRoot gameFlowRoot = GameFlowRoot.Instance;
        if (gameFlowRoot == null ||
            gameFlowRoot.galFlow == null ||
            !gameFlowRoot.galFlow.IsInitialized ||
            Keyboard.current == null)
        {
            return;
        }

        galRoot = gameFlowRoot.galFlow;
        gameFlowArbitrator = gameFlowRoot.GetComponent<GameFlowArbitrator>();
        if (gameFlowArbitrator == null)
            throw new Exception("GameFlowArbitrator is unavailable in Play Mode.");
        huntData = gameFlowRoot.huntFlow != null
            ? gameFlowRoot.huntFlow.huntFlowDataBus
            : null;
        if (huntData == null)
            throw new Exception("HuntFlowDataBus is unavailable in Play Mode.");

        if (huntData.player == null)
            return;

        verifiedRoundsStarted = 0;
        verifiedRoundsRestored = 0;
        concurrentStartedCount = 0;
        concurrentACompleted = false;
        concurrentBPreempted = false;
        concurrentA = LoadRequiredAsset<GalSequenceSO>(
            ConcurrentSequenceAPath,
            "Concurrent A Sequence asset");
        concurrentB = LoadRequiredAsset<GalSequenceSO>(
            ConcurrentSequenceBPath,
            "Concurrent B Sequence asset");
        gameFlowRoot.EventHub.Gal.Subscribe<GalStartedEvent>(OnGalStarted);
        gameFlowRoot.EventHub.Gal.Subscribe<GalCompletedEvent>(OnGalCompleted);
        gameFlowRoot.EventHub.Gal.Subscribe<GalStoppedEvent>(OnGalStopped);
        CaptureRuntimeBaseline();
        if (!previousMoveAllowed)
            throw new Exception("Move authority must be available before Concurrent verification.");
        playerStartPosition = huntData.player.transform.position;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.O));
        MoveTo(VerificationPhase.WaitingForConcurrentA);
    }

    private static void OnGalStarted(GalStartedEvent data)
    {
        if (data.PresentationMode != GalPresentationMode.ConcurrentOverlay)
            return;
        if (data.Sequence != concurrentA && data.Sequence != concurrentB)
            throw new Exception("An unexpected Concurrent Sequence started.");
        concurrentStartedCount++;
        if (concurrentStartedCount == 1 && data.Sequence != concurrentA ||
            concurrentStartedCount == 2 && data.Sequence != concurrentB)
        {
            throw new Exception("Concurrent FIFO order was not A then B.");
        }
    }

    private static void OnGalCompleted(GalCompletedEvent data)
    {
        if (data.Sequence == concurrentA)
            concurrentACompleted = true;
    }

    private static void OnGalStopped(GalStoppedEvent data)
    {
        if (data.Sequence == concurrentB && data.Reason == GalStopReason.Preempted)
            concurrentBPreempted = true;
    }

    private static bool VerifyConcurrentStarted(
        GalSequenceSO expectedSequence,
        int expectedStartCount)
    {
        if (!galRoot.IsActive ||
            galRoot.ActiveMode != GalPresentationMode.ConcurrentOverlay ||
            concurrentStartedCount < expectedStartCount)
        {
            return false;
        }
        if (concurrentStartedCount != expectedStartCount)
            throw new Exception("Concurrent Sequence start count changed unexpectedly.");
        VerifyConcurrentGameplayInvariant();
        VerifyConcurrentPresentation();
        return true;
    }

    private static void VerifyConcurrentGameplayInvariant()
    {
        if (!Mathf.Approximately(Time.timeScale, previousTimeScale) ||
            huntData.isPaused != previousHuntPaused ||
            huntData.player == null ||
            huntData.player.controlAuthority.IsAllowed(CharacterControlChannel.Move) !=
                previousMoveAllowed)
        {
            throw new Exception(
                "ConcurrentOverlay changed timeScale, Hunt pause, or Move authority.");
        }
    }

    private static void VerifyConcurrentPresentation()
    {
        GalDialogueView view = galRoot.GetComponentInChildren<GalDialogueView>(true);
        Transform visualRoot = view?.transform.Find("VisualRoot");
        Transform card = visualRoot?.Find("ConcurrentCard");
        CanvasGroup group = card?.GetComponent<CanvasGroup>();
        Image avatar = card?.Find("AvatarMask/AvatarImage")?.GetComponent<Image>();
        TMP_Text speaker = card?.Find("TextColumn/SpeakerName")?.GetComponent<TMP_Text>();
        TMP_Text dialogue = card?.Find("TextColumn/DialogueText")?.GetComponent<TMP_Text>();
        if (visualRoot == null || !visualRoot.gameObject.activeSelf ||
            card == null || !card.gameObject.activeSelf || group == null ||
            group.blocksRaycasts || group.interactable ||
            avatar == null || !avatar.enabled || avatar.sprite == null ||
            speaker == null || string.IsNullOrWhiteSpace(speaker.text) ||
            dialogue == null || string.IsNullOrWhiteSpace(dialogue.text))
        {
            throw new Exception("Concurrent communication card is not presented correctly.");
        }
        if (visualRoot.Find("PortraitStage").gameObject.activeSelf ||
            visualRoot.Find("DialoguePanel").gameObject.activeSelf ||
            visualRoot.Find("FullscreenBackgroundStage").gameObject.activeSelf ||
            visualRoot.Find("GameplayBackdropDimmer").gameObject.activeSelf)
        {
            throw new Exception("ConcurrentOverlay leaked traditional presentation layers.");
        }
    }

    private static void CaptureRuntimeBaseline()
    {
        if (Keyboard.current == null)
            throw new Exception("Keyboard is unavailable in Play Mode.");
        if (galRoot.IsActive || galRoot.State != GalFlowState.Inactive)
            throw new Exception("A Gal session was active before the next verification round.");
        if (huntData.player == null)
            throw new Exception("Player is unavailable before the verification round.");

        previousTimeScale = Time.timeScale;
        previousHuntPaused = huntData.isPaused;
        previousMoveAllowed =
            huntData.player.controlAuthority.IsAllowed(CharacterControlChannel.Move);
    }

    private static bool VerifyDialogueStarted(
        GalPresentationMode expectedMode,
        string screenshotPath,
        bool registerRound)
    {
        if (!galRoot.IsActive)
            return false;

        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());

        if (galRoot.State != GalFlowState.PresentingLine)
            throw new Exception($"Unexpected Gal state: {galRoot.State}.");
        if (!Mathf.Approximately(Time.timeScale, 0f))
            throw new Exception($"{expectedMode} did not set Time.timeScale to zero.");
        if (!huntData.isPaused)
            throw new Exception($"{expectedMode} did not pause HuntFlowDataBus.");
        if (huntData.player == null)
            throw new Exception($"{expectedMode} lost the player reference.");
        if (huntData.player.controlAuthority.IsAllowed(CharacterControlChannel.Move))
            throw new Exception($"{expectedMode} did not block player movement authority.");

        GalDialogueView view = galRoot.GetComponentInChildren<GalDialogueView>(true);
        if (view == null)
            throw new Exception("GalDialogueView is unavailable in Play Mode.");
        Transform visualRoot = view.transform.Find("VisualRoot");
        if (visualRoot == null || !visualRoot.gameObject.activeSelf)
            throw new Exception("Gal visual root is not visible.");

        VerifyModePresentation(view, visualRoot, expectedMode);

        Image portrait = visualRoot.Find("PortraitStage/ActivePortrait")
            ?.GetComponent<Image>();
        if (portrait == null || !portrait.enabled || portrait.sprite == null)
            throw new Exception($"{expectedMode} portrait is not visible.");

        confirmButton = visualRoot.Find("DialoguePanel/ConfirmButton")
            ?.GetComponent<Button>();
        if (confirmButton == null || !confirmButton.interactable)
            throw new Exception("Confirm button is not interactable.");

        if (registerRound)
            RegisterRoundStarted(expectedMode);
        previousLineText = null;
        VerifyCurrentLine(0);
        if (!string.IsNullOrEmpty(screenshotPath))
        {
            EnsureArtifactFolder();
            ScreenCapture.CaptureScreenshot(screenshotPath, 1);
        }
        return true;
    }

    private static void VerifyModePresentation(
        GalDialogueView view,
        Transform visualRoot,
        GalPresentationMode expectedMode)
    {
        GameObject fullscreenStage = visualRoot
            .Find("FullscreenBackgroundStage")?.gameObject;
        GameObject dimmer = visualRoot
            .Find("GameplayBackdropDimmer")?.gameObject;
        if (fullscreenStage == null || dimmer == null)
            throw new Exception("Gal mode presentation layers are missing.");

        if (expectedMode == GalPresentationMode.PausedOverlay)
        {
            if (fullscreenStage.activeSelf)
                throw new Exception("PausedOverlay left fullscreen stage visible.");
            if (!dimmer.activeSelf)
                throw new Exception("PausedOverlay did not enable the dimmer.");
            return;
        }

        if (!fullscreenStage.activeSelf)
            throw new Exception("FullscreenExclusive did not enable its background stage.");
        if (dimmer.activeSelf)
            throw new Exception("FullscreenExclusive left the gameplay dimmer visible.");

        Image backgroundImage = fullscreenStage.transform.Find("BackgroundImage")
            ?.GetComponent<Image>();
        if (backgroundImage == null || !backgroundImage.enabled ||
            backgroundImage.sprite == null ||
            !Mathf.Approximately(backgroundImage.color.a, 1f))
        {
            throw new Exception(
                "FullscreenExclusive background must be enabled, opaque and use a Sprite.");
        }

        Canvas canvas = view.GetComponent<Canvas>() ??
            view.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
            canvas.sortingOrder < 5000)
        {
            throw new Exception(
                "FullscreenExclusive Canvas must be ScreenSpaceOverlay with order >= 5000.");
        }
    }

    private static void RegisterRoundStarted(GalPresentationMode mode)
    {
        int expectedPreviousCount = mode == GalPresentationMode.PausedOverlay ? 0 : 1;
        if (verifiedRoundsStarted != expectedPreviousCount)
            throw new Exception($"{mode} verification round started out of sequence.");
        verifiedRoundsStarted++;
    }

    private static void InvokeConfirmAndExpectLine(int expectedLineIndex)
    {
        confirmButton.onClick.Invoke();
        if (!galRoot.IsActive)
            throw new Exception($"Dialogue closed before line {expectedLineIndex}.");
    }

    private static void VerifyCurrentLine(int expectedLineIndex)
    {
        GalDialogueView view = galRoot.GetComponentInChildren<GalDialogueView>(true);
        TMP_Text dialogue = view.transform
            .Find("VisualRoot/DialoguePanel/DialogueText")
            ?.GetComponent<TMP_Text>();
        if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.text))
            throw new Exception($"Dialogue text is empty on line {expectedLineIndex}.");
        if (expectedLineIndex > 0 &&
            string.Equals(previousLineText, dialogue.text, StringComparison.Ordinal))
        {
            throw new Exception($"Dialogue did not advance to line {expectedLineIndex}.");
        }

        previousLineText = dialogue.text;
    }

    private static void VerifyClosedAndRestored(GalPresentationMode mode)
    {
        if (galRoot.IsActive || galRoot.State != GalFlowState.Inactive)
            throw new Exception($"{mode} final confirmation did not close Gal.");
        if (!Mathf.Approximately(Time.timeScale, previousTimeScale))
            throw new Exception($"{mode} did not restore Time.timeScale.");
        if (huntData.isPaused != previousHuntPaused)
            throw new Exception($"{mode} did not restore Hunt pause state.");
        if (huntData.player == null)
            throw new Exception($"{mode} lost the player reference before restore check.");
        if (huntData.player.controlAuthority.IsAllowed(CharacterControlChannel.Move) !=
            previousMoveAllowed)
        {
            throw new Exception($"{mode} did not restore player movement authority.");
        }

        GalDialogueView view = galRoot.GetComponentInChildren<GalDialogueView>(true);
        Transform visualRoot = view.transform.Find("VisualRoot");
        if (visualRoot != null && visualRoot.gameObject.activeSelf)
            throw new Exception($"{mode} visual root remained visible after completion.");

        int expectedPreviousCount = mode == GalPresentationMode.PausedOverlay ? 0 : 1;
        if (verifiedRoundsRestored != expectedPreviousCount)
            throw new Exception($"{mode} verification round restored out of sequence.");
        verifiedRoundsRestored++;
    }

    private static void VerifyBothRoundsCompleted()
    {
        if (verifiedRoundsStarted != 2 || verifiedRoundsRestored != 2)
        {
            throw new Exception(
                "PausedOverlay and FullscreenExclusive verification rounds did not both complete.");
        }
    }

    private static void EnsureArtifactFolder()
    {
        if (!AssetDatabase.IsValidFolder(ArtifactFolder))
        {
            AssetDatabase.CreateFolder(
                "Assets/Script/GameFlowManager/GalSystem/Tests",
                "Artifacts");
        }
    }

    private static void MoveTo(VerificationPhase next)
    {
        phase = next;
        ticksInPhase = 0;
        phaseStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void CompleteSuccess()
    {
        Debug.Log(
            "[GalPlayModeVerifier] PASS Gal ConcurrentOverlay + PausedOverlay + FullscreenExclusive PlayMode verification");
        Finish();
    }

    private static void CompleteFailure()
    {
        Debug.LogError("[GalPlayModeVerifier] FAILED.");
        Finish();
    }

    private static void Finish()
    {
        GameFlowRoot root = GameFlowRoot.Instance;
        if (root != null && root.EventHub != null)
        {
            root.EventHub.Gal.Unsubscribe<GalStartedEvent>(OnGalStarted);
            root.EventHub.Gal.Unsubscribe<GalCompletedEvent>(OnGalCompleted);
            root.EventHub.Gal.Unsubscribe<GalStoppedEvent>(OnGalStopped);
        }
        SessionState.SetBool(ArmedKey, false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
    }

    private enum VerificationPhase
    {
        WaitingForPlayMode,
        WaitingForInitialization,
        WaitingForConcurrentA,
        WaitingForConcurrentDuplicate,
        WaitingForConcurrentQueueB,
        WaitingForConcurrentMovement,
        WaitingForConcurrentB,
        WaitingForConcurrentScreenshot,
        WaitingForPreemptFullscreen,
        WaitingForPreemptFullscreenAdvance,
        WaitingForNoConcurrentResume,
        WaitingForPausedDialogue,
        WaitingForPausedScreenshot,
        WaitingForPausedSecondLine,
        WaitingForPausedThirdLine,
        WaitingForPausedClose,
        WaitingToQueueFullscreen,
        WaitingForFullscreenDialogue,
        WaitingForFullscreenScreenshot,
        WaitingForFullscreenSecondLine,
        WaitingForFullscreenThirdLine,
        WaitingForFullscreenClose
    }
}
#endif
