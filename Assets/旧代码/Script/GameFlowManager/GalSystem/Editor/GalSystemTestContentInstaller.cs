#if UNITY_EDITOR
using System;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GalSystemTestContentInstaller
{
    private const string GalFolder =
        "Assets/Script/GameFlowManager/GalSystem";
    private const string TestContentFolder = GalFolder + "/Content/Test";
    private const string PortraitFolder = TestContentFolder + "/Portraits";
    private const string BackgroundFolder = TestContentFolder + "/Backgrounds";
    private const string AnimePortraitPath =
        PortraitFolder + "/SPR_GalPortrait_Test_Anime.png";
    private const string BackgroundPath =
        BackgroundFolder + "/BG_GalFullscreen_Test.png";
    private const string ChineseFontPath =
        "Assets/SiYuanHeiTi-ExtraLight/SourceHanSansSC-ExtraLight-22 SDF.asset";
    private const string ActorPath = TestContentFolder + "/GalActor_Test.asset";
    private const string PausedSequencePath =
        TestContentFolder + "/GalSequence_Test.asset";
    private const string FullscreenSequencePath =
        TestContentFolder + "/GalSequence_Test_Fullscreen.asset";
    private const string ConcurrentSequenceAPath =
        TestContentFolder + "/GalSequence_Test_Concurrent_A.asset";
    private const string ConcurrentSequenceBPath =
        TestContentFolder + "/GalSequence_Test_Concurrent_B.asset";
    private const string PrefabFolder = GalFolder + "/Prefabs";
    private const string ViewPrefabPath =
        PrefabFolder + "/PF_GalDialogueView.prefab";
    private const string RootPrefabPath =
        PrefabFolder + "/PF_GalFlowRoot.prefab";

    [MenuItem("Tools/Project ANOIR/Gal/Install Test Vertical Slice")]
    public static void InstallFromMenu()
    {
        Debug.Log(InstallTestVerticalSlice());
    }

    [MenuItem("Tools/Project ANOIR/Gal/Validate Concurrent Test Content")]
    public static void ValidateConcurrentFromMenu()
    {
        Debug.Log(ValidateConcurrentTestContent());
    }

    public static string ValidateConcurrentTestContent()
    {
        try
        {
            GalSequenceSO paused = LoadRequired<GalSequenceSO>(PausedSequencePath);
            GalSequenceSO fullscreen =
                LoadRequired<GalSequenceSO>(FullscreenSequencePath);
            GalSequenceSO concurrentA =
                LoadRequired<GalSequenceSO>(ConcurrentSequenceAPath);
            GalSequenceSO concurrentB =
                LoadRequired<GalSequenceSO>(ConcurrentSequenceBPath);
            TMP_FontAsset chineseFont =
                LoadRequired<TMP_FontAsset>(ChineseFontPath);
            GameObject viewPrefab = LoadRequired<GameObject>(ViewPrefabPath);
            GameObject rootPrefab = LoadRequired<GameObject>(RootPrefabPath);

            ValidateConcurrentSequence(
                concurrentA,
                "gal_test_concurrent_a",
                0f,
                "Concurrent A");
            ValidateConcurrentSequence(
                concurrentB,
                "gal_test_concurrent_b",
                1.2f,
                "Concurrent B");
            ValidateConcurrentViewPrefab(
                viewPrefab,
                chineseFont,
                concurrentA,
                concurrentB);
            ValidateConcurrentRootPrefab(
                rootPrefab,
                viewPrefab,
                paused,
                fullscreen,
                concurrentA,
                concurrentB);

            return "[GalInstallerValidation] PASS concurrent content, " +
                "view, root references and preserved local fileIDs.";
        }
        catch (Exception exception)
        {
            return "[GalInstallerValidation] FAILED: " + exception.Message;
        }
    }

    public static string InstallTestVerticalSlice()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "[GalInstaller] REJECTED: exit Play Mode before installing.";

        EnsureFolder(TestContentFolder);
        EnsureFolder(PortraitFolder);
        EnsureFolder(BackgroundFolder);
        EnsureFolder(PrefabFolder);

        ConfigureSpriteImporter(
            AnimePortraitPath,
            true,
            new Vector2(0.5f, 0f));
        ConfigureSpriteImporter(
            BackgroundPath,
            false,
            new Vector2(0.5f, 0.5f));

        Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(AnimePortraitPath);
        if (portrait == null)
        {
            return "[GalInstaller] FAILED: Sprite was not imported at " +
                AnimePortraitPath + ".";
        }

        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        if (background == null)
        {
            return "[GalInstaller] FAILED: Sprite was not imported at " +
                BackgroundPath + ".";
        }

        TMP_FontAsset chineseFont =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
        if (chineseFont == null)
        {
            return "[GalInstaller] FAILED: Chinese TMP font was not found at " +
                ChineseFontPath + ".";
        }

        Scene scene = SceneManager.GetActiveScene();
        bool sceneWasDirty = scene.isDirty;

        try
        {
            GalActorSO actor = CreateOrUpdateActor(portrait);
            GalSequenceSO pausedSequence =
                CreateOrUpdatePausedSequence(actor);
            GalSequenceSO fullscreenSequence =
                CreateOrUpdateFullscreenSequence(actor, background);
            GalSequenceSO concurrentSequenceA =
                CreateOrUpdateConcurrentSequence(
                    ConcurrentSequenceAPath,
                    "gal_test_concurrent_a",
                    actor,
                    "前方区域信号有点奇怪，小心一点。",
                    "我会继续监控，有变化马上告诉你。",
                    0f);
            GalSequenceSO concurrentSequenceB =
                CreateOrUpdateConcurrentSequence(
                    ConcurrentSequenceBPath,
                    "gal_test_concurrent_b",
                    actor,
                    "补充情报：右侧通道检测到移动目标。",
                    "先别停下，我正在确认它的身份。",
                    1.2f);
            GameObject viewPrefab =
                CreateOrUpdateDialogueViewPrefab(chineseFont);
            GameObject rootPrefab = CreateOrUpdateFlowRootPrefab(
                viewPrefab,
                pausedSequence,
                fullscreenSequence,
                concurrentSequenceA,
                concurrentSequenceB);

            if (!EnsureSceneConnection(
                    rootPrefab,
                    out GameFlowRoot gameFlowRoot,
                    out GalFlowRoot galFlowRoot,
                    out bool sceneChanged,
                    out string sceneError))
            {
                return "[GalInstaller] FAILED: " + sceneError;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            GalDialogueView sceneView =
                galFlowRoot.GetComponentInChildren<GalDialogueView>(true);
            var report = new StringBuilder();
            report.Append("[GalInstaller] PASS")
                .Append("; scene=").Append(scene.name)
                .Append("; gameFlowPath=")
                .Append(GetPath(gameFlowRoot.transform))
                .Append("; galPath=").Append(GetPath(galFlowRoot.transform))
                .Append("; portrait=").Append(portrait.name)
                .Append("; background=").Append(background.name)
                .Append("; actor=").Append(actor.name)
                .Append("; pausedSequence=").Append(pausedSequence.name)
                .Append("; fullscreenSequence=")
                .Append(fullscreenSequence.name)
                .Append("; concurrentSequenceA=")
                .Append(concurrentSequenceA.name)
                .Append("; concurrentSequenceB=")
                .Append(concurrentSequenceB.name)
                .Append("; viewReady=")
                .Append(sceneView != null && sceneView.IsReady)
                .Append("; sceneWasDirty=").Append(sceneWasDirty)
                .Append("; sceneChanged=").Append(sceneChanged)
                .Append("; sceneSaved=false");
            return report.ToString();
        }
        catch (Exception exception)
        {
            return "[GalInstaller] FAILED: " + exception.Message;
        }
    }

    private static void ValidateConcurrentSequence(
        GalSequenceSO sequence,
        string expectedId,
        float expectedOverride,
        string label)
    {
        if (!string.Equals(sequence.SequenceId, expectedId, StringComparison.Ordinal))
            throw new InvalidOperationException(label + " SequenceId is incorrect.");
        if (sequence.DefaultPresentationMode != GalPresentationMode.ConcurrentOverlay)
            throw new InvalidOperationException(label + " mode is not ConcurrentOverlay.");
        if (sequence.Lines == null || sequence.Lines.Count < 2)
            throw new InvalidOperationException(label + " must contain at least two lines.");

        for (int index = 0; index < 2; index++)
        {
            GalLine line = sequence.Lines[index];
            if (line == null || string.IsNullOrWhiteSpace(line.Text))
                throw new InvalidOperationException(label + $" line {index} is invalid.");
            if (!Mathf.Approximately(
                    line.AutoAdvanceSecondsOverride,
                    expectedOverride))
            {
                throw new InvalidOperationException(
                    label + $" line {index} auto-advance override is incorrect.");
            }
        }
    }

    private static void ValidateConcurrentViewPrefab(
        GameObject prefab,
        TMP_FontAsset chineseFont,
        GalSequenceSO concurrentA,
        GalSequenceSO concurrentB)
    {
        GalDialogueView view = prefab.GetComponent<GalDialogueView>();
        Transform visualRoot = prefab.transform.Find("VisualRoot");
        Transform portraitStage = visualRoot?.Find("PortraitStage");
        Transform dialoguePanel = visualRoot?.Find("DialoguePanel");
        Transform card = visualRoot?.Find("ConcurrentCard");
        Transform avatarMask = card?.Find("AvatarMask");
        Image avatarImage = avatarMask?.Find("AvatarImage")?.GetComponent<Image>();
        Transform textColumn = card?.Find("TextColumn");
        TMP_Text speaker =
            textColumn?.Find("SpeakerName")?.GetComponent<TMP_Text>();
        TMP_Text dialogue =
            textColumn?.Find("DialogueText")?.GetComponent<TMP_Text>();
        Image progress = card?.Find("AutoAdvanceProgress")?.GetComponent<Image>();
        CanvasGroup group = card?.GetComponent<CanvasGroup>();
        Image cardImage = card?.GetComponent<Image>();
        HorizontalLayoutGroup horizontal =
            card?.GetComponent<HorizontalLayoutGroup>();
        VerticalLayoutGroup vertical =
            textColumn?.GetComponent<VerticalLayoutGroup>();
        Mask mask = avatarMask?.GetComponent<Mask>();
        RectTransform cardRect = card as RectTransform;

        if (view == null || visualRoot == null || portraitStage == null ||
            dialoguePanel == null || card == null || avatarMask == null ||
            textColumn == null || avatarImage == null || speaker == null ||
            dialogue == null || progress == null || group == null ||
            cardImage == null || horizontal == null || vertical == null ||
            mask == null || cardRect == null)
        {
            throw new InvalidOperationException(
                "PF_GalDialogueView is missing ConcurrentCard hierarchy or components.");
        }

        if (!view.IsReady)
            throw new InvalidOperationException("PF_GalDialogueView IsReady is false.");
        if (cardRect.anchorMin != Vector2.zero || cardRect.anchorMax != Vector2.zero ||
            cardRect.pivot != Vector2.zero || cardRect.anchoredPosition.x < 36f ||
            cardRect.anchoredPosition.y < 36f)
        {
            throw new InvalidOperationException(
                "ConcurrentCard is not anchored inside the lower-left safe margin.");
        }
        if (group.blocksRaycasts || group.interactable ||
            !Mathf.Approximately(group.alpha, 0f))
        {
            throw new InvalidOperationException(
                "ConcurrentCard CanvasGroup must start transparent and ignore input.");
        }
        if (cardImage.raycastTarget || avatarImage.raycastTarget ||
            speaker.raycastTarget ||
            dialogue.raycastTarget || progress.raycastTarget)
        {
            throw new InvalidOperationException(
                "ConcurrentCard graphics must not receive Gameplay raycasts.");
        }
        if (speaker.font != chineseFont || dialogue.font != chineseFont)
        {
            throw new InvalidOperationException(
                "ConcurrentCard text must use the configured Chinese TMP font.");
        }

        string requiredCharacters = GetSequenceText(concurrentA) +
            GetSequenceText(concurrentB);
        if (!chineseFont.HasCharacters(requiredCharacters))
        {
            throw new InvalidOperationException(
                "The configured Chinese TMP font does not cover all Concurrent test text.");
        }

        var serialized = new SerializedObject(view);
        RequireExactReference(serialized, "portraitStage", portraitStage.gameObject);
        RequireExactReference(serialized, "dialoguePanel", dialoguePanel.gameObject);
        RequireExactReference(serialized, "concurrentCard", card.gameObject);
        RequireExactReference(serialized, "concurrentCanvasGroup", group);
        RequireExactReference(serialized, "concurrentAvatarImage", avatarImage);
        RequireExactReference(serialized, "concurrentSpeakerNameText", speaker);
        RequireExactReference(serialized, "concurrentDialogueText", dialogue);
        RequireExactReference(serialized, "concurrentProgressFill", progress);

        RequireLocalId(prefab, 1656234512499454681UL, "View root");
        RequireLocalId(prefab.transform, 8285376294978024591UL, "View RectTransform");
        RequireLocalId(view, 3684202772808574123UL, "GalDialogueView");
        RequireLocalId(visualRoot.gameObject, 8159857003766696700UL, "VisualRoot");
        RequireLocalId(
            visualRoot.Find("GameplayBackdropDimmer")?.GetComponent<Image>(),
            693959579365415466UL,
            "GameplayBackdropDimmer Image");
        RequireLocalId(
            visualRoot.Find("DialoguePanel/ConfirmButton")?.gameObject,
            5886834821050385327UL,
            "ConfirmButton");
    }

    private static string GetSequenceText(GalSequenceSO sequence)
    {
        var result = new StringBuilder();
        for (int index = 0; index < sequence.Lines.Count; index++)
        {
            GalLine line = sequence.Lines[index];
            if (line != null)
                result.Append(line.Text);
        }

        return result.ToString();
    }

    private static void ValidateConcurrentRootPrefab(
        GameObject prefab,
        GameObject viewPrefab,
        GalSequenceSO paused,
        GalSequenceSO fullscreen,
        GalSequenceSO concurrentA,
        GalSequenceSO concurrentB)
    {
        GalFlowRoot flow = prefab.GetComponent<GalFlowRoot>();
        GalDebugHotkeyTrigger trigger = prefab.GetComponent<GalDebugHotkeyTrigger>();
        GalDialogueView[] views =
            prefab.GetComponentsInChildren<GalDialogueView>(true);
        Canvas[] canvases = prefab.GetComponentsInChildren<Canvas>(true);
        if (flow == null || trigger == null || views.Length != 1 ||
            canvases.Length != 1 || prefab.transform.childCount != 1)
        {
            throw new InvalidOperationException(
                "PF_GalFlowRoot composition changed or contains duplicates.");
        }

        GalDialogueView sourceView =
            PrefabUtility.GetCorrespondingObjectFromSource(views[0]);
        if (sourceView != viewPrefab.GetComponent<GalDialogueView>())
            throw new InvalidOperationException("Nested PF_GalDialogueView source changed.");

        var flowSerialized = new SerializedObject(flow);
        SerializedProperty capacity =
            RequireProperty(flowSerialized, "maxQueuedConcurrentRequests");
        if (capacity.intValue != 3)
            throw new InvalidOperationException("Root Concurrent queue capacity is not 3.");

        var triggerSerialized = new SerializedObject(trigger);
        RequireExactReference(triggerSerialized, "gameFlowArbitrator", null);
        RequireExactReference(triggerSerialized, "testSequence", paused);
        RequireExactReference(triggerSerialized, "fullscreenTestSequence", fullscreen);
        RequireExactReference(
            triggerSerialized,
            "concurrentTestSequenceA",
            concurrentA);
        RequireExactReference(
            triggerSerialized,
            "concurrentTestSequenceB",
            concurrentB);

        RequireLocalId(prefab, 5435446752566921711UL, "Root prefab");
        RequireLocalId(prefab.transform, 8145877467556113015UL, "Root Transform");
        RequireLocalId(flow, 6515326013171384046UL, "GalFlowRoot");
        RequireLocalId(trigger, 7824773478115457074UL, "GalDebugHotkeyTrigger");
        RequireLocalId(views[0], 6903097798091595902UL, "Nested GalDialogueView");
    }

    private static T LoadRequired<T>(string assetPath)
        where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
            throw new InvalidOperationException("Missing asset: " + assetPath);
        return asset;
    }

    private static void RequireExactReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object expected)
    {
        SerializedProperty property = RequireProperty(serialized, propertyName);
        if (property.objectReferenceValue != expected)
        {
            throw new InvalidOperationException(
                serialized.targetObject.name + "." + propertyName +
                " is not wired to the expected object.");
        }
    }

    private static void RequireLocalId(
        UnityEngine.Object target,
        ulong expected,
        string label)
    {
        if (target == null)
            throw new InvalidOperationException(label + " is missing.");

        ulong actual = Unsupported.GetLocalIdentifierInFileForPersistentObject(target);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                label + $" local fileID changed: expected {expected}, actual {actual}.");
        }
    }

    private static void ConfigureSpriteImporter(
        string assetPath,
        bool hasAlpha,
        Vector2 pivot)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.sRGBTexture = true;
        importer.alphaSource = hasAlpha
            ? TextureImporterAlphaSource.FromInput
            : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = hasAlpha;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 100f;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static GalActorSO CreateOrUpdateActor(Sprite portrait)
    {
        GalActorSO actor = AssetDatabase.LoadAssetAtPath<GalActorSO>(ActorPath);
        if (actor == null)
        {
            actor = ScriptableObject.CreateInstance<GalActorSO>();
            AssetDatabase.CreateAsset(actor, ActorPath);
        }

        var serialized = new SerializedObject(actor);
        serialized.FindProperty("actorId").stringValue = "test_survivor";
        serialized.FindProperty("displayName").stringValue = "MARA";
        serialized.FindProperty("defaultPortrait").objectReferenceValue = portrait;
        serialized.FindProperty("portraitVariants").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actor);
        return actor;
    }

    private static GalSequenceSO CreateOrUpdatePausedSequence(GalActorSO actor)
    {
        GalSequenceSO sequence =
            AssetDatabase.LoadAssetAtPath<GalSequenceSO>(PausedSequencePath);
        if (sequence == null)
        {
            sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
            AssetDatabase.CreateAsset(sequence, PausedSequencePath);
        }

        var serialized = new SerializedObject(sequence);
        serialized.FindProperty("sequenceId").stringValue =
            "gal_test_vertical_slice";
        serialized.FindProperty("defaultPresentationMode").enumValueIndex =
            (int)GalPresentationMode.PausedOverlay;
        serialized.FindProperty("defaultBackground").objectReferenceValue = null;

        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = 3;
        ConfigureLine(
            lines.GetArrayElementAtIndex(0),
            actor,
            "The radio is finally quiet. We should move before the storm returns.",
            GalPortraitSlot.Left,
            null);
        ConfigureLine(
            lines.GetArrayElementAtIndex(1),
            actor,
            "This portrait and dialogue are temporary assets for the Gal system test.",
            GalPortraitSlot.Left,
            null);
        ConfigureLine(
            lines.GetArrayElementAtIndex(2),
            actor,
            "Press NEXT once more to close the dialogue and resume Gameplay.",
            GalPortraitSlot.Left,
            null);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static GalSequenceSO CreateOrUpdateFullscreenSequence(
        GalActorSO actor,
        Sprite background)
    {
        GalSequenceSO sequence =
            AssetDatabase.LoadAssetAtPath<GalSequenceSO>(FullscreenSequencePath);
        if (sequence == null)
        {
            sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
            AssetDatabase.CreateAsset(sequence, FullscreenSequencePath);
        }

        var serialized = new SerializedObject(sequence);
        serialized.FindProperty("sequenceId").stringValue = "gal_test_fullscreen";
        serialized.FindProperty("defaultPresentationMode").enumValueIndex =
            (int)GalPresentationMode.FullscreenExclusive;
        serialized.FindProperty("defaultBackground").objectReferenceValue = background;
        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = 3;
        ConfigureLine(
            lines.GetArrayElementAtIndex(0),
            actor,
            "This is not where we were a moment ago.",
            GalPortraitSlot.Left,
            null);
        ConfigureLine(
            lines.GetArrayElementAtIndex(1),
            actor,
            "The fullscreen backdrop has taken over while Gameplay remains safely paused.",
            GalPortraitSlot.Center,
            null);
        ConfigureLine(
            lines.GetArrayElementAtIndex(2),
            actor,
            "One more confirmation, and we return to the game.",
            GalPortraitSlot.Right,
            null);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static GalSequenceSO CreateOrUpdateConcurrentSequence(
        string assetPath,
        string sequenceId,
        GalActorSO actor,
        string firstLine,
        string secondLine,
        float autoAdvanceSecondsOverride)
    {
        GalSequenceSO sequence =
            AssetDatabase.LoadAssetAtPath<GalSequenceSO>(assetPath);
        if (sequence == null)
        {
            sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
            AssetDatabase.CreateAsset(sequence, assetPath);
        }

        var serialized = new SerializedObject(sequence);
        serialized.FindProperty("sequenceId").stringValue = sequenceId;
        serialized.FindProperty("defaultPresentationMode").enumValueIndex =
            (int)GalPresentationMode.ConcurrentOverlay;
        serialized.FindProperty("defaultBackground").objectReferenceValue = null;

        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = 2;
        ConfigureLine(
            lines.GetArrayElementAtIndex(0),
            actor,
            firstLine,
            GalPortraitSlot.Left,
            null,
            autoAdvanceSecondsOverride);
        ConfigureLine(
            lines.GetArrayElementAtIndex(1),
            actor,
            secondLine,
            GalPortraitSlot.Left,
            null,
            autoAdvanceSecondsOverride);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static void ConfigureLine(
        SerializedProperty line,
        GalActorSO actor,
        string text,
        GalPortraitSlot slot,
        Sprite backgroundOverride,
        float autoAdvanceSecondsOverride = 0f)
    {
        line.FindPropertyRelative("actor").objectReferenceValue = actor;
        line.FindPropertyRelative("portraitVariantKey").stringValue = string.Empty;
        line.FindPropertyRelative("portraitOverride").objectReferenceValue = null;
        line.FindPropertyRelative("speakerNameOverride").stringValue = string.Empty;
        line.FindPropertyRelative("text").stringValue = text;
        line.FindPropertyRelative("portraitSlot").enumValueIndex = (int)slot;
        line.FindPropertyRelative("backgroundOverride").objectReferenceValue =
            backgroundOverride;
        line.FindPropertyRelative("autoAdvanceSecondsOverride").floatValue =
            Mathf.Max(0f, autoAdvanceSecondsOverride);
    }

    private static GameObject CreateOrUpdateDialogueViewPrefab(
        TMP_FontAsset chineseFont)
    {
        GameObject existing =
            AssetDatabase.LoadAssetAtPath<GameObject>(ViewPrefabPath);
        if (existing == null)
            return CreateDialogueViewPrefab(chineseFont);

        GameObject contents = PrefabUtility.LoadPrefabContents(ViewPrefabPath);
        try
        {
            GalDialogueView view = contents.GetComponent<GalDialogueView>();
            Canvas canvas = contents.GetComponent<Canvas>();
            if (view == null || canvas == null)
            {
                throw new InvalidOperationException(
                    "PF_GalDialogueView is missing its root GalDialogueView or Canvas.");
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            Transform visualRoot = FindUniqueDirectChild(contents.transform, "VisualRoot");
            if (visualRoot == null)
                throw new InvalidOperationException("PF_GalDialogueView/VisualRoot is missing.");

            Transform dimmer =
                FindUniqueDirectChild(visualRoot, "GameplayBackdropDimmer");
            if (dimmer == null)
            {
                throw new InvalidOperationException(
                    "PF_GalDialogueView/VisualRoot/GameplayBackdropDimmer is missing.");
            }

            Transform portraitStage =
                FindUniqueDirectChild(visualRoot, "PortraitStage");
            Transform portraitTransform =
                portraitStage?.Find("ActivePortrait");
            Image portraitImage =
                portraitTransform != null
                    ? portraitTransform.GetComponent<Image>()
                    : null;
            if (portraitImage == null)
            {
                throw new InvalidOperationException(
                    "PF_GalDialogueView/VisualRoot/PortraitStage/ActivePortrait is missing.");
            }

            ConfigurePortraitImage(portraitImage);

            Transform dialoguePanel =
                FindUniqueDirectChild(visualRoot, "DialoguePanel");
            if (dialoguePanel == null)
            {
                throw new InvalidOperationException(
                    "PF_GalDialogueView/VisualRoot/DialoguePanel is missing.");
            }

            Transform stage =
                FindUniqueDirectChild(visualRoot, "FullscreenBackgroundStage");
            if (stage == null)
            {
                stage = CreateUIObject(
                    "FullscreenBackgroundStage",
                    visualRoot).transform;
            }

            RectTransform stageRect = stage.GetComponent<RectTransform>();
            if (stageRect == null)
                throw new InvalidOperationException("FullscreenBackgroundStage must use RectTransform.");
            Stretch(stageRect);
            stage.SetSiblingIndex(0);

            Transform backgroundTransform =
                FindUniqueDirectChild(stage, "BackgroundImage");
            Image backgroundImage;
            if (backgroundTransform == null)
            {
                backgroundImage = CreateImage("BackgroundImage", stage, Color.black);
            }
            else
            {
                backgroundImage = backgroundTransform.GetComponent<Image>();
                if (backgroundImage == null)
                    backgroundImage = backgroundTransform.gameObject.AddComponent<Image>();
            }

            ConfigureBackgroundImage(backgroundImage);
            AspectRatioFitter fitter =
                backgroundImage.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            ConfigureConcurrentCard(
                visualRoot,
                chineseFont,
                out GameObject concurrentCard,
                out CanvasGroup concurrentGroup,
                out Image concurrentAvatar,
                out TMP_Text concurrentSpeaker,
                out TMP_Text concurrentDialogue,
                out Image concurrentProgress);

            var serialized = new SerializedObject(view);
            SetRequiredReference(serialized, "fullscreenBackgroundStage", stage.gameObject);
            SetRequiredReference(serialized, "fullscreenBackgroundImage", backgroundImage);
            SetRequiredReference(serialized, "fullscreenBackgroundFitter", fitter);
            SetRequiredReference(serialized, "gameplayBackdropDimmer", dimmer.gameObject);
            SetRequiredReference(serialized, "portraitStage", portraitStage.gameObject);
            SetRequiredReference(serialized, "dialoguePanel", dialoguePanel.gameObject);
            SetRequiredReference(serialized, "concurrentCard", concurrentCard);
            SetRequiredReference(
                serialized,
                "concurrentCanvasGroup",
                concurrentGroup);
            SetRequiredReference(
                serialized,
                "concurrentAvatarImage",
                concurrentAvatar);
            SetRequiredReference(
                serialized,
                "concurrentSpeakerNameText",
                concurrentSpeaker);
            SetRequiredReference(
                serialized,
                "concurrentDialogueText",
                concurrentDialogue);
            SetRequiredReference(
                serialized,
                "concurrentProgressFill",
                concurrentProgress);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            stage.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(contents, ViewPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(ViewPrefabPath);
    }

    private static GameObject CreateDialogueViewPrefab(
        TMP_FontAsset chineseFont)
    {
        GameObject canvasRoot = new GameObject(
            "PF_GalDialogueView",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(GalDialogueView));

        try
        {
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject visualRoot = CreateUIObject("VisualRoot", canvasRoot.transform);
            Stretch(visualRoot.GetComponent<RectTransform>());

            GameObject fullscreenStage = CreateUIObject(
                "FullscreenBackgroundStage",
                visualRoot.transform);
            Stretch(fullscreenStage.GetComponent<RectTransform>());
            fullscreenStage.transform.SetSiblingIndex(0);
            Image backgroundImage = CreateImage(
                "BackgroundImage",
                fullscreenStage.transform,
                Color.black);
            ConfigureBackgroundImage(backgroundImage);
            AspectRatioFitter backgroundFitter =
                backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
            backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundFitter.aspectRatio = 16f / 9f;

            Image dimmer = CreateImage(
                "GameplayBackdropDimmer",
                visualRoot.transform,
                new Color(0.015f, 0.025f, 0.045f, 0.62f));
            Stretch(dimmer.rectTransform);
            dimmer.raycastTarget = true;

            GameObject portraitStage =
                CreateUIObject("PortraitStage", visualRoot.transform);
            RectTransform portraitStageRect =
                portraitStage.GetComponent<RectTransform>();
            portraitStageRect.anchorMin = new Vector2(0f, 0.27f);
            portraitStageRect.anchorMax = new Vector2(1f, 1f);
            portraitStageRect.offsetMin = Vector2.zero;
            portraitStageRect.offsetMax = Vector2.zero;

            Image portrait = CreateImage(
                "ActivePortrait",
                portraitStage.transform,
                Color.white);
            ConfigurePortraitImage(portrait);

            Image dialoguePanel = CreateImage(
                "DialoguePanel",
                visualRoot.transform,
                new Color(0.025f, 0.045f, 0.075f, 0.96f));
            RectTransform panelRect = dialoguePanel.rectTransform;
            panelRect.anchorMin = new Vector2(0.055f, 0.045f);
            panelRect.anchorMax = new Vector2(0.945f, 0.31f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            dialoguePanel.raycastTarget = true;

            var outline = dialoguePanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.31f, 0.62f, 0.78f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI speaker = CreateText(
                "SpeakerName",
                dialoguePanel.transform,
                "MARA",
                38f,
                new Color(0.96f, 0.69f, 0.25f, 1f));
            RectTransform speakerRect = speaker.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.offsetMin = new Vector2(48f, -78f);
            speakerRect.offsetMax = new Vector2(-220f, -20f);
            speaker.alignment = TextAlignmentOptions.Left;

            TextMeshProUGUI dialogue = CreateText(
                "DialogueText",
                dialoguePanel.transform,
                "Dialogue preview",
                30f,
                new Color(0.92f, 0.95f, 1f, 1f));
            RectTransform dialogueRect = dialogue.rectTransform;
            dialogueRect.anchorMin = Vector2.zero;
            dialogueRect.anchorMax = Vector2.one;
            dialogueRect.offsetMin = new Vector2(48f, 42f);
            dialogueRect.offsetMax = new Vector2(-250f, -88f);
            dialogue.alignment = TextAlignmentOptions.TopLeft;
            dialogue.enableWordWrapping = true;

            Image buttonImage = CreateImage(
                "ConfirmButton",
                dialoguePanel.transform,
                new Color(0.11f, 0.28f, 0.39f, 1f));
            RectTransform buttonRect = buttonImage.rectTransform;
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-36f, 32f);
            buttonRect.sizeDelta = new Vector2(180f, 72f);
            Button button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            TextMeshProUGUI label = CreateText(
                "Label",
                button.transform,
                "NEXT",
                28f,
                Color.white);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;

            ConfigureConcurrentCard(
                visualRoot.transform,
                chineseFont,
                out GameObject concurrentCard,
                out CanvasGroup concurrentGroup,
                out Image concurrentAvatar,
                out TMP_Text concurrentSpeaker,
                out TMP_Text concurrentDialogue,
                out Image concurrentProgress);

            var serialized = new SerializedObject(
                canvasRoot.GetComponent<GalDialogueView>());
            SetRequiredReference(serialized, "visualRoot", visualRoot);
            SetRequiredReference(
                serialized,
                "fullscreenBackgroundStage",
                fullscreenStage);
            SetRequiredReference(
                serialized,
                "fullscreenBackgroundImage",
                backgroundImage);
            SetRequiredReference(
                serialized,
                "fullscreenBackgroundFitter",
                backgroundFitter);
            SetRequiredReference(
                serialized,
                "gameplayBackdropDimmer",
                dimmer.gameObject);
            SetRequiredReference(serialized, "portraitStage", portraitStage);
            SetRequiredReference(serialized, "dialoguePanel", dialoguePanel.gameObject);
            SetRequiredReference(serialized, "portraitImage", portrait);
            SetRequiredReference(serialized, "speakerNameText", speaker);
            SetRequiredReference(serialized, "dialogueText", dialogue);
            SetRequiredReference(serialized, "confirmButton", button);
            SetRequiredReference(serialized, "concurrentCard", concurrentCard);
            SetRequiredReference(
                serialized,
                "concurrentCanvasGroup",
                concurrentGroup);
            SetRequiredReference(
                serialized,
                "concurrentAvatarImage",
                concurrentAvatar);
            SetRequiredReference(
                serialized,
                "concurrentSpeakerNameText",
                concurrentSpeaker);
            SetRequiredReference(
                serialized,
                "concurrentDialogueText",
                concurrentDialogue);
            SetRequiredReference(
                serialized,
                "concurrentProgressFill",
                concurrentProgress);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            fullscreenStage.SetActive(false);
            visualRoot.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(canvasRoot, ViewPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasRoot);
        }
    }

    private static void ConfigureBackgroundImage(Image backgroundImage)
    {
        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(1920f, 1080f);
        backgroundImage.color = Color.black;
        backgroundImage.preserveAspect = true;
        backgroundImage.raycastTarget = true;
    }

    private static void ConfigurePortraitImage(Image portrait)
    {
        RectTransform portraitRect = portrait.rectTransform;
        portraitRect.anchorMin = new Vector2(0.25f, 0f);
        portraitRect.anchorMax = new Vector2(0.25f, 0f);
        portraitRect.pivot = new Vector2(0.5f, 0f);
        portraitRect.anchoredPosition = new Vector2(0f, -300f);
        portraitRect.sizeDelta = new Vector2(760f, 1080f);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
    }

    private static void ConfigureConcurrentCard(
        Transform visualRoot,
        TMP_FontAsset chineseFont,
        out GameObject card,
        out CanvasGroup canvasGroup,
        out Image avatarImage,
        out TMP_Text speakerName,
        out TMP_Text dialogueText,
        out Image progressFill)
    {
        Transform cardTransform =
            FindUniqueDirectChild(visualRoot, "ConcurrentCard");
        if (cardTransform == null)
            cardTransform = CreateUIObject("ConcurrentCard", visualRoot).transform;

        card = cardTransform.gameObject;
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.zero;
        cardRect.pivot = Vector2.zero;
        cardRect.anchoredPosition = new Vector2(36f, 36f);
        cardRect.sizeDelta = new Vector2(560f, 150f);

        Image cardImage = GetOrAddComponent<Image>(card);
        cardImage.color = new Color(0.025f, 0.045f, 0.075f, 0.94f);
        cardImage.raycastTarget = false;

        canvasGroup = GetOrAddComponent<CanvasGroup>(card);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        HorizontalLayoutGroup horizontal =
            GetOrAddComponent<HorizontalLayoutGroup>(card);
        horizontal.padding = new RectOffset(14, 14, 14, 18);
        horizontal.spacing = 14f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        Transform avatarMaskTransform =
            FindUniqueDirectChild(cardTransform, "AvatarMask");
        if (avatarMaskTransform == null)
        {
            avatarMaskTransform =
                CreateUIObject("AvatarMask", cardTransform).transform;
        }

        avatarMaskTransform.SetSiblingIndex(0);
        Image maskImage = GetOrAddComponent<Image>(avatarMaskTransform.gameObject);
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        Mask mask = GetOrAddComponent<Mask>(avatarMaskTransform.gameObject);
        mask.showMaskGraphic = false;
        LayoutElement avatarLayout =
            GetOrAddComponent<LayoutElement>(avatarMaskTransform.gameObject);
        avatarLayout.ignoreLayout = false;
        avatarLayout.minWidth = 112f;
        avatarLayout.preferredWidth = 112f;
        avatarLayout.flexibleWidth = 0f;
        avatarLayout.minHeight = 112f;
        avatarLayout.preferredHeight = 112f;

        Transform avatarTransform =
            FindUniqueDirectChild(avatarMaskTransform, "AvatarImage");
        if (avatarTransform == null)
        {
            avatarTransform =
                CreateUIObject("AvatarImage", avatarMaskTransform).transform;
        }

        avatarImage = GetOrAddComponent<Image>(avatarTransform.gameObject);
        Stretch(avatarImage.rectTransform);
        avatarImage.color = Color.white;
        avatarImage.preserveAspect = true;
        avatarImage.raycastTarget = false;

        Transform textColumnTransform =
            FindUniqueDirectChild(cardTransform, "TextColumn");
        if (textColumnTransform == null)
        {
            textColumnTransform =
                CreateUIObject("TextColumn", cardTransform).transform;
        }

        textColumnTransform.SetSiblingIndex(1);
        LayoutElement textColumnLayout =
            GetOrAddComponent<LayoutElement>(textColumnTransform.gameObject);
        textColumnLayout.ignoreLayout = false;
        textColumnLayout.minWidth = 300f;
        textColumnLayout.preferredWidth = 406f;
        textColumnLayout.flexibleWidth = 1f;

        VerticalLayoutGroup vertical =
            GetOrAddComponent<VerticalLayoutGroup>(textColumnTransform.gameObject);
        vertical.padding = new RectOffset(0, 0, 0, 0);
        vertical.spacing = 4f;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        Transform speakerTransform =
            FindUniqueDirectChild(textColumnTransform, "SpeakerName");
        TextMeshProUGUI speaker;
        if (speakerTransform == null)
        {
            speaker = CreateText(
                "SpeakerName",
                textColumnTransform,
                "MARA",
                24f,
                new Color(0.96f, 0.69f, 0.25f, 1f));
        }
        else
        {
            speaker = GetOrAddComponent<TextMeshProUGUI>(
                speakerTransform.gameObject);
        }

        speakerName = speaker;
        speakerName.font = chineseFont;
        speakerName.fontSize = 24f;
        speakerName.color = new Color(0.96f, 0.69f, 0.25f, 1f);
        speakerName.alignment = TextAlignmentOptions.Left;
        speakerName.raycastTarget = false;
        LayoutElement speakerLayout =
            GetOrAddComponent<LayoutElement>(speakerName.gameObject);
        speakerLayout.ignoreLayout = false;
        speakerLayout.preferredHeight = 32f;

        Transform dialogueTransform =
            FindUniqueDirectChild(textColumnTransform, "DialogueText");
        TextMeshProUGUI dialogue;
        if (dialogueTransform == null)
        {
            dialogue = CreateText(
                "DialogueText",
                textColumnTransform,
                "Concurrent dialogue preview",
                22f,
                new Color(0.92f, 0.95f, 1f, 1f));
        }
        else
        {
            dialogue = GetOrAddComponent<TextMeshProUGUI>(
                dialogueTransform.gameObject);
        }

        dialogueText = dialogue;
        dialogueText.font = chineseFont;
        dialogueText.fontSize = 22f;
        dialogueText.color = new Color(0.92f, 0.95f, 1f, 1f);
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.enableWordWrapping = true;
        dialogueText.maxVisibleLines = 2;
        dialogueText.overflowMode = TextOverflowModes.Ellipsis;
        dialogueText.raycastTarget = false;
        LayoutElement dialogueLayout =
            GetOrAddComponent<LayoutElement>(dialogueText.gameObject);
        dialogueLayout.ignoreLayout = false;
        dialogueLayout.preferredHeight = 70f;
        dialogueLayout.flexibleHeight = 1f;

        Transform progressTransform =
            FindUniqueDirectChild(cardTransform, "AutoAdvanceProgress");
        if (progressTransform == null)
        {
            progressFill = CreateImage(
                "AutoAdvanceProgress",
                cardTransform,
                new Color(0.31f, 0.72f, 0.92f, 0.95f));
        }
        else
        {
            progressFill = GetOrAddComponent<Image>(progressTransform.gameObject);
        }

        progressFill.transform.SetSiblingIndex(2);
        RectTransform progressRect = progressFill.rectTransform;
        progressRect.anchorMin = Vector2.zero;
        progressRect.anchorMax = new Vector2(1f, 0f);
        progressRect.pivot = new Vector2(0.5f, 0f);
        progressRect.offsetMin = new Vector2(14f, 8f);
        progressRect.offsetMax = new Vector2(-14f, 12f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        progressFill.color = new Color(0.31f, 0.72f, 0.92f, 0.95f);
        progressFill.raycastTarget = false;
        LayoutElement progressLayout =
            GetOrAddComponent<LayoutElement>(progressFill.gameObject);
        progressLayout.ignoreLayout = true;

        avatarMaskTransform.gameObject.SetActive(true);
        card.SetActive(false);
    }

    private static GameObject CreateOrUpdateFlowRootPrefab(
        GameObject viewPrefab,
        GalSequenceSO pausedSequence,
        GalSequenceSO fullscreenSequence,
        GalSequenceSO concurrentSequenceA,
        GalSequenceSO concurrentSequenceB)
    {
        GameObject existing =
            AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
        if (existing == null)
        {
            return CreateFlowRootPrefab(
                viewPrefab,
                pausedSequence,
                fullscreenSequence,
                concurrentSequenceA,
                concurrentSequenceB);
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(RootPrefabPath);
        try
        {
            GalFlowRoot flowRoot = contents.GetComponent<GalFlowRoot>();
            GalDebugHotkeyTrigger trigger =
                contents.GetComponent<GalDebugHotkeyTrigger>();
            if (flowRoot == null || trigger == null)
            {
                throw new InvalidOperationException(
                    "PF_GalFlowRoot is missing GalFlowRoot or GalDebugHotkeyTrigger.");
            }

            GalDialogueView[] views =
                contents.GetComponentsInChildren<GalDialogueView>(true);
            if (views.Length != 1)
            {
                throw new InvalidOperationException(
                    "PF_GalFlowRoot must retain exactly one nested GalDialogueView.");
            }

            GameObject sourceView = PrefabUtility.GetCorrespondingObjectFromSource(
                views[0].gameObject);
            if (sourceView != viewPrefab)
            {
                throw new InvalidOperationException(
                    "PF_GalFlowRoot must retain the existing PF_GalDialogueView nested prefab.");
            }

            var flowSerialized = new SerializedObject(flowRoot);
            SetRequiredReference(flowSerialized, "dialogueView", views[0]);
            RequireProperty(flowSerialized, "maxQueuedConcurrentRequests")
                .intValue = 3;
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();
            ConfigureDebugTrigger(
                trigger,
                null,
                pausedSequence,
                fullscreenSequence,
                concurrentSequenceA,
                concurrentSequenceB);

            PrefabUtility.SaveAsPrefabAsset(contents, RootPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
    }

    private static GameObject CreateFlowRootPrefab(
        GameObject viewPrefab,
        GalSequenceSO pausedSequence,
        GalSequenceSO fullscreenSequence,
        GalSequenceSO concurrentSequenceA,
        GalSequenceSO concurrentSequenceB)
    {
        GameObject root = new GameObject("PF_GalFlowRoot");
        try
        {
            GalFlowRoot flowRoot = root.AddComponent<GalFlowRoot>();
            GalDebugHotkeyTrigger trigger =
                root.AddComponent<GalDebugHotkeyTrigger>();
            GameObject viewInstance = PrefabUtility.InstantiatePrefab(
                viewPrefab,
                root.transform) as GameObject;
            if (viewInstance == null)
                throw new InvalidOperationException("Could not instantiate PF_GalDialogueView.");

            GalDialogueView view = viewInstance.GetComponent<GalDialogueView>();
            var flowSerialized = new SerializedObject(flowRoot);
            SetRequiredReference(flowSerialized, "dialogueView", view);
            RequireProperty(flowSerialized, "maxQueuedConcurrentRequests")
                .intValue = 3;
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();
            ConfigureDebugTrigger(
                trigger,
                null,
                pausedSequence,
                fullscreenSequence,
                concurrentSequenceA,
                concurrentSequenceB);

            return PrefabUtility.SaveAsPrefabAsset(root, RootPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureDebugTrigger(
        GalDebugHotkeyTrigger trigger,
        GameFlowArbitrator arbitrator,
        GalSequenceSO pausedSequence,
        GalSequenceSO fullscreenSequence,
        GalSequenceSO concurrentSequenceA,
        GalSequenceSO concurrentSequenceB)
    {
        if (trigger == null)
            throw new InvalidOperationException("GalDebugHotkeyTrigger is missing.");

        var serialized = new SerializedObject(trigger);
        SetRequiredReference(
            serialized,
            "gameFlowArbitrator",
            arbitrator);
        SetRequiredReference(serialized, "testSequence", pausedSequence);
        SetRequiredReference(
            serialized,
            "fullscreenTestSequence",
            fullscreenSequence);
        SetRequiredReference(
            serialized,
            "concurrentTestSequenceA",
            concurrentSequenceA);
        SetRequiredReference(
            serialized,
            "concurrentTestSequenceB",
            concurrentSequenceB);
        SerializedProperty mode = RequireProperty(serialized, "presentationMode");
        mode.enumValueIndex = (int)GalPresentationMode.PausedOverlay;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
    }

    private static bool EnsureSceneConnection(
        GameObject rootPrefab,
        out GameFlowRoot gameFlowRoot,
        out GalFlowRoot galFlowRoot,
        out bool sceneChanged,
        out string error)
    {
        sceneChanged = false;
        error = string.Empty;
        gameFlowRoot = null;
        galFlowRoot = null;

        Scene scene = SceneManager.GetActiveScene();
        GameFlowRoot[] roots = FindInScene<GameFlowRoot>(scene);
        if (roots.Length != 1)
        {
            error = $"expected exactly one scene GameFlowRoot, found {roots.Length}.";
            return false;
        }

        gameFlowRoot = roots[0];
        if (gameFlowRoot.gameFlowArbitrator == null)
        {
            error = "the scene GameFlowRoot has no GameFlowArbitrator.";
            return false;
        }

        GalFlowRoot[] galRoots = FindInScene<GalFlowRoot>(scene);
        if (galRoots.Length > 1)
        {
            error = $"expected at most one scene GalFlowRoot, found {galRoots.Length}.";
            return false;
        }

        if (galRoots.Length == 0)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(
                rootPrefab,
                gameFlowRoot.transform) as GameObject;
            if (instance == null)
            {
                error = "could not instantiate PF_GalFlowRoot.";
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Install Gal test vertical slice");
            galFlowRoot = instance.GetComponent<GalFlowRoot>();
            sceneChanged = true;
        }
        else
        {
            galFlowRoot = galRoots[0];
        }

        if (galFlowRoot == null || !galFlowRoot.transform.IsChildOf(gameFlowRoot.transform))
        {
            error = "the existing GalFlowRoot is not a child of the unique GameFlowRoot.";
            return false;
        }

        GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(
            galFlowRoot.gameObject);
        if (sourceRoot != rootPrefab)
        {
            error = "the existing GalFlowRoot is not an instance of PF_GalFlowRoot.";
            return false;
        }

        var rootSerialized = new SerializedObject(gameFlowRoot);
        SerializedProperty galProperty =
            RequireProperty(rootSerialized, "<galFlow>k__BackingField");
        if (galProperty.objectReferenceValue != galFlowRoot)
        {
            Undo.RecordObject(gameFlowRoot, "Assign GalFlowRoot");
            galProperty.objectReferenceValue = galFlowRoot;
            rootSerialized.ApplyModifiedProperties();
            sceneChanged = true;
        }

        GalDebugHotkeyTrigger trigger =
            galFlowRoot.GetComponent<GalDebugHotkeyTrigger>();
        if (trigger == null)
        {
            error = "the scene GalFlowRoot is missing GalDebugHotkeyTrigger.";
            return false;
        }

        var triggerSerialized = new SerializedObject(trigger);
        SerializedProperty arbitratorProperty =
            RequireProperty(triggerSerialized, "gameFlowArbitrator");
        if (arbitratorProperty.objectReferenceValue != gameFlowRoot.gameFlowArbitrator)
        {
            Undo.RecordObject(trigger, "Connect Gal debug trigger");
            arbitratorProperty.objectReferenceValue = gameFlowRoot.gameFlowArbitrator;
            triggerSerialized.ApplyModifiedProperties();
            sceneChanged = true;
        }

        if (sceneChanged)
            EditorSceneManager.MarkSceneDirty(scene);

        return true;
    }

    private static T[] FindInScene<T>(Scene scene)
        where T : Component
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        int count = 0;
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
                count++;
        }

        var result = new T[count];
        int destination = 0;
        for (int index = 0; index < all.Length; index++)
        {
            T item = all[index];
            if (item == null || item.gameObject.scene != scene)
                continue;

            result[destination++] = item;
        }

        return result;
    }

    private static void SetRequiredReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        RequireProperty(serialized, propertyName).objectReferenceValue = value;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.name +
                " is missing serialized property '" + propertyName + "'.");
        }

        return property;
    }

    private static Transform FindUniqueDirectChild(Transform parent, string name)
    {
        Transform result = null;
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (!string.Equals(child.name, name, StringComparison.Ordinal))
                continue;

            if (result != null)
            {
                throw new InvalidOperationException(
                    GetPath(parent) + " contains multiple direct children named " + name + ".");
            }

            result = child;
        }

        return result;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject result = CreateUIObject(name, parent);
        Image image = result.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string text,
        float fontSize,
        Color color)
    {
        GameObject result = CreateUIObject(name, parent);
        TextMeshProUGUI label = result.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int split = folderPath.LastIndexOf('/');
        string parent = folderPath.Substring(0, split);
        string folderName = folderPath.Substring(split + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }
}
#endif
