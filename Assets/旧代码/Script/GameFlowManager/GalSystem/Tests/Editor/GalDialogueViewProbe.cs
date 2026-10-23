#if UNITY_EDITOR
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class GalDialogueViewProbe
{
    public static string Run()
    {
        GameObject gameFlowObject = null;
        GameObject root = null;
        Texture2D backgroundTexture = null;
        Sprite backgroundSprite = null;
        Texture2D portraitTexture = null;
        Sprite portraitSprite = null;

        try
        {
            gameFlowObject = new GameObject(
                "GameFlowRootProbe",
                typeof(GameFlowRoot));
            root = new GameObject(
                "GalDialogueViewProbe",
                typeof(RectTransform));
            root.transform.SetParent(gameFlowObject.transform, false);
            GalDialogueView view = root.AddComponent<GalDialogueView>();
            GalFlowRoot galFlowRoot = root.AddComponent<GalFlowRoot>();

            GameObject visualRoot = CreateUIObject("VisualRoot", root.transform);
            GameObject fullscreenStage = CreateUIObject(
                "FullscreenBackgroundStage",
                visualRoot.transform);
            Image backgroundImage = CreateImage(
                "FullscreenBackgroundImage",
                fullscreenStage.transform);
            AspectRatioFitter backgroundFitter =
                backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
            GameObject dimmer = CreateUIObject(
                "GameplayBackdropDimmer",
                visualRoot.transform);
            GameObject portraitStage = CreateUIObject(
                "PortraitStage",
                visualRoot.transform);
            Image portraitImage = CreateImage(
                "PortraitImage",
                portraitStage.transform);
            GameObject dialoguePanel = CreateUIObject(
                "DialoguePanel",
                visualRoot.transform);
            TMP_Text speakerNameText = CreateText(
                "SpeakerNameText",
                dialoguePanel.transform);
            TMP_Text dialogueText = CreateText(
                "DialogueText",
                dialoguePanel.transform);
            Button confirmButton = CreateUIObject(
                "ConfirmButton",
                dialoguePanel.transform).AddComponent<Button>();

            GameObject concurrentCard = CreateUIObject(
                "ConcurrentCard",
                visualRoot.transform);
            CanvasGroup concurrentCanvasGroup =
                concurrentCard.AddComponent<CanvasGroup>();
            RectTransform concurrentCardRect =
                concurrentCard.GetComponent<RectTransform>();
            concurrentCardRect.sizeDelta = new Vector2(560f, 150f);
            var concurrentLayout =
                concurrentCard.AddComponent<HorizontalLayoutGroup>();
            concurrentLayout.padding = new RectOffset(16, 16, 16, 16);
            concurrentLayout.spacing = 16f;
            concurrentLayout.childControlWidth = true;
            concurrentLayout.childForceExpandWidth = false;
            GameObject avatarMask = CreateUIObject(
                "AvatarMask",
                concurrentCard.transform);
            var avatarLayout = avatarMask.AddComponent<LayoutElement>();
            avatarLayout.preferredWidth = 112f;
            avatarLayout.flexibleWidth = 0f;
            Image concurrentAvatarImage = CreateImage(
                "AvatarImage",
                avatarMask.transform);
            GameObject textColumn = CreateUIObject(
                "TextColumn",
                concurrentCard.transform);
            var textLayout = textColumn.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
            TMP_Text concurrentSpeakerNameText = CreateText(
                "SpeakerName",
                textColumn.transform);
            TMP_Text concurrentDialogueText = CreateText(
                "DialogueText",
                textColumn.transform);
            Image concurrentProgressFill = CreateImage(
                "AutoAdvanceProgress",
                concurrentCard.transform);
            var progressLayout =
                concurrentProgressFill.gameObject.AddComponent<LayoutElement>();
            progressLayout.ignoreLayout = true;

            SetPrivateField(view, "visualRoot", visualRoot);
            SetPrivateField(view, "fullscreenBackgroundStage", fullscreenStage);
            SetPrivateField(view, "fullscreenBackgroundImage", backgroundImage);
            SetPrivateField(view, "fullscreenBackgroundFitter", backgroundFitter);
            SetPrivateField(view, "gameplayBackdropDimmer", dimmer);
            SetPrivateField(view, "portraitStage", portraitStage);
            SetPrivateField(view, "dialoguePanel", dialoguePanel);
            SetPrivateField(view, "portraitImage", portraitImage);
            SetPrivateField(view, "speakerNameText", speakerNameText);
            SetPrivateField(view, "dialogueText", dialogueText);
            SetPrivateField(view, "confirmButton", confirmButton);
            SetPrivateField(view, "concurrentCard", concurrentCard);
            SetPrivateField(
                view,
                "concurrentCanvasGroup",
                concurrentCanvasGroup);
            SetPrivateField(
                view,
                "concurrentAvatarImage",
                concurrentAvatarImage);
            SetPrivateField(
                view,
                "concurrentSpeakerNameText",
                concurrentSpeakerNameText);
            SetPrivateField(
                view,
                "concurrentDialogueText",
                concurrentDialogueText);
            SetPrivateField(
                view,
                "concurrentProgressFill",
                concurrentProgressFill);
            SetPrivateField(galFlowRoot, "dialogueView", view);

            RequiresAllModeVisualReferences(view);

            backgroundTexture = new Texture2D(8, 4);
            backgroundSprite = Sprite.Create(
                backgroundTexture,
                new Rect(0f, 0f, 8f, 4f),
                new Vector2(0.5f, 0.5f));
            portraitTexture = new Texture2D(4, 8);
            portraitSprite = Sprite.Create(
                portraitTexture,
                new Rect(0f, 0f, 4f, 8f),
                new Vector2(0.5f, 0f));

            VerifiesModeVisibilityAndBackgroundFallback(
                view,
                visualRoot,
                fullscreenStage,
                backgroundImage,
                backgroundFitter,
                dimmer,
                portraitStage,
                dialoguePanel,
                confirmButton,
                concurrentCard,
                concurrentCanvasGroup,
                avatarMask,
                textColumn.GetComponent<RectTransform>(),
                concurrentAvatarImage,
                concurrentSpeakerNameText,
                concurrentDialogueText,
                concurrentProgressFill,
                portraitSprite,
                backgroundSprite);

            RejectsMissingConcurrentReferenceBeforeSuspending(view);
            VerifiesRootProgressWiring(
                gameFlowObject.GetComponent<GameFlowRoot>(),
                galFlowRoot,
                concurrentProgressFill);

            return "PASS gal dialogue view";
        }
        finally
        {
            if (backgroundSprite != null)
                UnityEngine.Object.DestroyImmediate(backgroundSprite);
            if (backgroundTexture != null)
                UnityEngine.Object.DestroyImmediate(backgroundTexture);
            if (portraitSprite != null)
                UnityEngine.Object.DestroyImmediate(portraitSprite);
            if (portraitTexture != null)
                UnityEngine.Object.DestroyImmediate(portraitTexture);
            if (gameFlowObject != null)
                UnityEngine.Object.DestroyImmediate(gameFlowObject);
            else if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void RequiresAllModeVisualReferences(GalDialogueView view)
    {
        AssertTrue(view.IsReady, "A fully wired View must be ready");

        string[] requiredFields =
        {
            "fullscreenBackgroundStage",
            "fullscreenBackgroundImage",
            "fullscreenBackgroundFitter",
            "gameplayBackdropDimmer",
            "portraitStage",
            "dialoguePanel",
            "concurrentCard",
            "concurrentCanvasGroup",
            "concurrentAvatarImage",
            "concurrentSpeakerNameText",
            "concurrentDialogueText",
            "concurrentProgressFill"
        };

        for (int i = 0; i < requiredFields.Length; i++)
        {
            FieldInfo field = FindPrivateField(view, requiredFields[i]);
            object value = field.GetValue(view);
            field.SetValue(view, null);
            AssertFalse(
                view.IsReady,
                $"Missing {requiredFields[i]} must make the View unavailable");
            field.SetValue(view, value);
        }
    }

    private static void VerifiesModeVisibilityAndBackgroundFallback(
        GalDialogueView view,
        GameObject visualRoot,
        GameObject fullscreenStage,
        Image backgroundImage,
        AspectRatioFitter backgroundFitter,
        GameObject dimmer,
        GameObject portraitStage,
        GameObject dialoguePanel,
        Button confirmButton,
        GameObject concurrentCard,
        CanvasGroup concurrentCanvasGroup,
        GameObject avatarMask,
        RectTransform concurrentTextColumn,
        Image concurrentAvatarImage,
        TMP_Text concurrentSpeakerNameText,
        TMP_Text concurrentDialogueText,
        Image concurrentProgressFill,
        Sprite portraitSprite,
        Sprite backgroundSprite)
    {
        view.Show(GalPresentationMode.PausedOverlay);
        AssertTrue(visualRoot.activeSelf, "Paused view must open");
        AssertFalse(
            fullscreenStage.activeSelf,
            "Paused view must hide fullscreen stage");
        AssertTrue(dimmer.activeSelf, "Paused view must show dimmer");
        AssertTrue(portraitStage.activeSelf, "Paused view must show portrait stage");
        AssertTrue(dialoguePanel.activeSelf, "Paused view must show dialogue panel");
        AssertTrue(confirmButton.gameObject.activeSelf, "Paused view must show Confirm");
        AssertFalse(concurrentCard.activeSelf, "Paused view must hide Concurrent card");

        view.Show(GalPresentationMode.FullscreenExclusive);
        AssertTrue(
            fullscreenStage.activeSelf,
            "Fullscreen view must show background stage");
        AssertFalse(
            dimmer.activeSelf,
            "Fullscreen view must hide gameplay dimmer");
        AssertTrue(portraitStage.activeSelf, "Fullscreen view must show portrait stage");
        AssertTrue(dialoguePanel.activeSelf, "Fullscreen view must show dialogue panel");
        AssertTrue(confirmButton.gameObject.activeSelf, "Fullscreen view must show Confirm");
        AssertFalse(concurrentCard.activeSelf, "Fullscreen view must hide Concurrent card");
        view.Present(new GalLineViewModel(
            0,
            "测试角色",
            "有背景",
            null,
            GalPortraitSlot.Left,
            backgroundSprite));
        AssertSame(
            backgroundSprite,
            backgroundImage.sprite,
            "Fullscreen background Sprite must be presented");
        AssertEqual(
            Color.white,
            backgroundImage.color,
            "A real background must render without tint");
        AssertTrue(
            backgroundImage.enabled,
            "A real fullscreen background must be enabled");
        AssertTrue(
            backgroundImage.preserveAspect,
            "Fullscreen background must preserve aspect ratio");
        AssertEqual(
            2f,
            backgroundFitter.aspectRatio,
            "Background fitter must use the Sprite aspect ratio");
        AssertEqual(
            AspectRatioFitter.AspectMode.EnvelopeParent,
            backgroundFitter.aspectMode,
            "Fullscreen background must cover the entire stage");

        view.Present(new GalLineViewModel(
            1,
            "测试角色",
            "黑底兜底",
            null,
            GalPortraitSlot.Left,
            null));
        AssertSame(
            null,
            backgroundImage.sprite,
            "Null content must clear stale background Sprite");
        AssertEqual(
            Color.black,
            backgroundImage.color,
            "Null fullscreen content must become opaque black");
        AssertTrue(
            backgroundImage.enabled,
            "Black fallback Image must remain enabled");
        AssertEqual(
            16f / 9f,
            backgroundFitter.aspectRatio,
            "Black fallback must use the default widescreen ratio");

        view.Show(GalPresentationMode.ConcurrentOverlay);
        AssertFalse(
            fullscreenStage.activeSelf,
            "Concurrent view must hide fullscreen stage");
        AssertFalse(
            dimmer.activeSelf,
            "Concurrent view must not dim Gameplay");
        AssertFalse(portraitStage.activeSelf, "Concurrent view must hide portrait stage");
        AssertFalse(dialoguePanel.activeSelf, "Concurrent view must hide dialogue panel");
        AssertFalse(confirmButton.gameObject.activeSelf, "Concurrent view must hide Confirm");
        AssertTrue(concurrentCard.activeSelf, "Concurrent view must show its card");
        AssertApproximately(1f, concurrentCanvasGroup.alpha,
            "Concurrent Show must finish visible in the Editor probe");
        AssertFalse(concurrentCanvasGroup.interactable,
            "Concurrent card must not be interactable");
        AssertFalse(concurrentCanvasGroup.blocksRaycasts,
            "Concurrent card must not block Gameplay raycasts");

        view.Present(new GalLineViewModel(
            2,
            "通讯角色",
            "继续移动，我会保持联络。",
            portraitSprite,
            GalPortraitSlot.Right,
            backgroundSprite));
        AssertSame(portraitSprite, concurrentAvatarImage.sprite,
            "Concurrent avatar must use the resolved portrait");
        AssertTrue(concurrentAvatarImage.enabled,
            "A present Concurrent avatar must be enabled");
        AssertTrue(avatarMask.activeSelf,
            "A present Concurrent avatar must keep its mask visible");
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            concurrentCard.GetComponent<RectTransform>());
        float textWidthWithAvatar = concurrentTextColumn.rect.width;
        AssertEqual("通讯角色", concurrentSpeakerNameText.text,
            "Concurrent speaker name must be presented");
        AssertEqual("继续移动，我会保持联络。", concurrentDialogueText.text,
            "Concurrent dialogue text must be presented");
        AssertFalse(concurrentAvatarImage.raycastTarget,
            "Concurrent avatar must not receive raycasts");
        AssertFalse(concurrentSpeakerNameText.raycastTarget,
            "Concurrent speaker text must not receive raycasts");
        AssertFalse(concurrentDialogueText.raycastTarget,
            "Concurrent dialogue text must not receive raycasts");
        AssertFalse(concurrentProgressFill.raycastTarget,
            "Concurrent progress must not receive raycasts");

        view.SetConcurrentProgress(0.4f);
        AssertApproximately(0.4f, concurrentProgressFill.fillAmount,
            "Concurrent progress must display the supplied fraction");
        view.SetConcurrentProgress(-2f);
        AssertApproximately(0f, concurrentProgressFill.fillAmount,
            "Concurrent progress must clamp below zero");
        view.SetConcurrentProgress(4f);
        AssertApproximately(1f, concurrentProgressFill.fillAmount,
            "Concurrent progress must clamp above one");

        view.Present(new GalLineViewModel(
            3,
            "无线电",
            "头像暂时不可用。",
            null,
            GalPortraitSlot.Left));
        AssertFalse(concurrentAvatarImage.enabled,
            "A missing Concurrent portrait must disable the avatar safely");
        AssertFalse(avatarMask.activeSelf,
            "A missing Concurrent portrait must hide AvatarMask");
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            concurrentCard.GetComponent<RectTransform>());
        AssertTrue(
            concurrentTextColumn.rect.width > textWidthWithAvatar,
            "A missing Concurrent portrait must expand the text area");

        view.Present(new GalLineViewModel(
            4,
            "通讯角色",
            "头像恢复。",
            portraitSprite,
            GalPortraitSlot.Left));
        AssertTrue(avatarMask.activeSelf,
            "A later portrait must restore AvatarMask");

        view.Hide();
        AssertApproximately(0f, concurrentCanvasGroup.alpha,
            "Concurrent Hide must finish transparent in the Editor probe");
        AssertFalse(concurrentCard.activeSelf,
            "Concurrent Hide must close the card after fading");
        AssertFalse(visualRoot.activeSelf,
            "Concurrent Hide must close the visual root after fading");

        view.Show(GalPresentationMode.ConcurrentOverlay);
        AssertTrue(visualRoot.activeSelf,
            "A new Concurrent Show must cancel stale closing state");
        AssertTrue(concurrentCard.activeSelf,
            "A new Concurrent Show must reopen the card without a stale close");
        AssertApproximately(1f, concurrentCanvasGroup.alpha,
            "A reopened Concurrent card must become visible");
    }

    private static void RejectsMissingConcurrentReferenceBeforeSuspending(
        GalDialogueView view)
    {
        GalSequenceSO sequence = CreateSequence("缺失引用拒绝", 2f);
        var suspension = new ProbeSuspension();
        var machine = new GalFlowStateMachine(view, suspension, new EventBus());
        FieldInfo field = FindPrivateField(view, "concurrentCard");
        object value = field.GetValue(view);

        try
        {
            field.SetValue(view, null);
            AssertFalse(view.IsReady,
                "A missing Concurrent reference must make the View unavailable");
            AssertEqual(
                GalStartResult.ViewUnavailable,
                machine.TryStart(new GalPlayRequest(
                    sequence,
                    GalPresentationMode.ConcurrentOverlay)),
                "An incomplete Concurrent View must reject the session");
            AssertEqual(0, suspension.AcquireCount,
                "An incomplete View must reject before acquiring Gameplay flow");
        }
        finally
        {
            field.SetValue(view, value);
            machine.Dispose();
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void VerifiesRootProgressWiring(
        GameFlowRoot gameFlowRoot,
        GalFlowRoot galFlowRoot,
        Image progressFill)
    {
        GalSequenceSO sequence = CreateSequence("Root进度", 2f);
        SetPrivateField(
            gameFlowRoot,
            "<EventHub>k__BackingField",
            new GameEventHub());

        try
        {
            galFlowRoot.Initialize(gameFlowRoot);
            AssertEqual(
                GalStartResult.Started,
                galFlowRoot.RequestGal(new GalPlayRequest(
                    sequence,
                    GalPresentationMode.ConcurrentOverlay,
                    requestKey: "probe.root.progress",
                    duplicateCooldownSeconds: 0f)),
                "Root must start Concurrent content through its Dispatcher");

            GalConcurrentDispatcher dispatcher =
                (GalConcurrentDispatcher)FindPrivateField(
                    galFlowRoot,
                    "dispatcher").GetValue(galFlowRoot);
            dispatcher.Tick(1f);
            AssertApproximately(0.5f, progressFill.fillAmount,
                "Root must forward Dispatcher progress to the View");
            AssertEqual(1, GetProgressSubscriberCount(dispatcher),
                "Root must subscribe the View exactly once");

            InvokePrivate(galFlowRoot, "OnDestroy");
            AssertEqual(0, GetProgressSubscriberCount(dispatcher),
                "Root destruction must unsubscribe the View progress callback");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static GalSequenceSO CreateSequence(string text, float duration)
    {
        GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
        SetPrivateField(
            sequence,
            "lines",
            new System.Collections.Generic.List<GalLine>
            {
                new GalLine(
                    null,
                    text,
                    autoAdvanceSecondsOverride: duration)
            });
        return sequence;
    }

    private static int GetProgressSubscriberCount(
        GalConcurrentDispatcher dispatcher)
    {
        FieldInfo field = FindPrivateField(dispatcher, "ProgressChanged");
        Delegate callback = field.GetValue(dispatcher) as Delegate;
        return callback?.GetInvocationList().Length ?? 0;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new Exception($"Missing test setup method: {methodName}");
        method.Invoke(target, null);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        return CreateUIObject(name, parent).AddComponent<Image>();
    }

    private static TMP_Text CreateText(string name, Transform parent)
    {
        return CreateUIObject(name, parent).AddComponent<TextMeshProUGUI>();
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FindPrivateField(target, fieldName).SetValue(target, value);
    }

    private static FieldInfo FindPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new Exception($"Missing test setup field: {fieldName}");
        return field;
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

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertApproximately(
        float expected,
        float actual,
        string message)
    {
        if (float.IsNaN(actual) ||
            float.IsInfinity(actual) ||
            Math.Abs(expected - actual) > 0.0001f)
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private sealed class ProbeSuspension : IGameplaySuspensionService
    {
        public int AcquireCount { get; private set; }

        public bool TryAcquire(
            GalPresentationMode mode,
            out IDisposable suspensionLease)
        {
            AcquireCount++;
            suspensionLease = new ReleaseAction();
            return true;
        }
    }

    private sealed class ReleaseAction : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
#endif
