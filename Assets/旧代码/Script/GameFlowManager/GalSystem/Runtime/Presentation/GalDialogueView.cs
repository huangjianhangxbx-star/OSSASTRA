using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GalDialogueView : MonoBehaviour, IGalDialogueView
{
    private const float ConcurrentFadeDuration = 0.15f;

    [SerializeField] private GameObject visualRoot;
    [SerializeField] private GameObject fullscreenBackgroundStage;
    [SerializeField] private Image fullscreenBackgroundImage;
    [SerializeField] private AspectRatioFitter fullscreenBackgroundFitter;
    [SerializeField] private GameObject gameplayBackdropDimmer;
    [SerializeField] private GameObject portraitStage;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private GameObject concurrentCard;
    [SerializeField] private CanvasGroup concurrentCanvasGroup;
    [SerializeField] private Image concurrentAvatarImage;
    [SerializeField] private TMP_Text concurrentSpeakerNameText;
    [SerializeField] private TMP_Text concurrentDialogueText;
    [SerializeField] private Image concurrentProgressFill;

    private bool listenerBound;
    private GalPresentationMode activeMode;
    private Coroutine concurrentFadeCoroutine;

    public bool IsReady =>
        visualRoot != null &&
        fullscreenBackgroundStage != null &&
        fullscreenBackgroundImage != null &&
        fullscreenBackgroundFitter != null &&
        gameplayBackdropDimmer != null &&
        portraitStage != null &&
        dialoguePanel != null &&
        portraitImage != null &&
        speakerNameText != null &&
        dialogueText != null &&
        confirmButton != null &&
        concurrentCard != null &&
        concurrentCanvasGroup != null &&
        concurrentAvatarImage != null &&
        concurrentSpeakerNameText != null &&
        concurrentDialogueText != null &&
        concurrentProgressFill != null;

    public event Action Confirmed;

    private void Awake()
    {
        BindButton();
        Hide();
    }

    private void OnDestroy()
    {
        CancelConcurrentFade();
        UnbindButton();
    }

    public void Show(GalPresentationMode mode)
    {
        CancelConcurrentFade();
        activeMode = mode;
        bool concurrent = mode == GalPresentationMode.ConcurrentOverlay;
        bool fullscreen = mode == GalPresentationMode.FullscreenExclusive;
        bool paused = mode == GalPresentationMode.PausedOverlay;

        fullscreenBackgroundStage.SetActive(fullscreen);
        gameplayBackdropDimmer.SetActive(paused);
        portraitStage.SetActive(!concurrent);
        dialoguePanel.SetActive(!concurrent);
        confirmButton.gameObject.SetActive(!concurrent);
        concurrentCard.SetActive(concurrent);

        if (fullscreen)
            ApplyBackground(null);
        else
            fullscreenBackgroundImage.enabled = false;

        visualRoot.SetActive(true);

        if (concurrent)
        {
            ConfigureConcurrentInputIsolation();
            BeginConcurrentFade(1f, deactivateAfterFade: false);
        }
        else
        {
            concurrentCanvasGroup.alpha = 0f;
        }
    }

    public void Hide()
    {
        if (visualRoot == null)
            return;

        if (activeMode == GalPresentationMode.ConcurrentOverlay &&
            concurrentCard != null &&
            concurrentCanvasGroup != null)
        {
            BeginConcurrentFade(0f, deactivateAfterFade: true);
            return;
        }

        CancelConcurrentFade();
        visualRoot.SetActive(false);
    }

    public void Present(GalLineViewModel line)
    {
        if (!IsReady)
            return;

        if (activeMode == GalPresentationMode.ConcurrentOverlay)
        {
            concurrentSpeakerNameText.text = line.SpeakerName;
            concurrentDialogueText.text = line.Text;
            concurrentAvatarImage.sprite = line.Portrait;
            concurrentAvatarImage.preserveAspect = true;
            bool hasPortrait = line.Portrait != null;
            concurrentAvatarImage.enabled = hasPortrait;

            Transform avatarMask = concurrentAvatarImage.transform.parent;
            if (avatarMask != null)
                avatarMask.gameObject.SetActive(hasPortrait);
            return;
        }

        speakerNameText.text = line.SpeakerName;
        dialogueText.text = line.Text;

        portraitImage.sprite = line.Portrait;
        portraitImage.preserveAspect = true;
        portraitImage.enabled = line.Portrait != null;
        ApplyPortraitSlot(line.PortraitSlot);
        ApplyBackground(line.Background);
    }

    public void SetConcurrentProgress(float progress)
    {
        if (concurrentProgressFill != null)
            concurrentProgressFill.fillAmount = Mathf.Clamp01(progress);
    }

    public void BindButton()
    {
        if (listenerBound || confirmButton == null)
            return;

        confirmButton.onClick.AddListener(NotifyConfirmed);
        listenerBound = true;
    }

    private void UnbindButton()
    {
        if (!listenerBound || confirmButton == null)
            return;

        confirmButton.onClick.RemoveListener(NotifyConfirmed);
        listenerBound = false;
    }

    private void NotifyConfirmed()
    {
        Confirmed?.Invoke();
    }

    private void ApplyPortraitSlot(GalPortraitSlot slot)
    {
        if (!(portraitImage.transform is RectTransform rect))
            return;

        float normalizedX = slot switch
        {
            GalPortraitSlot.Left => 0.25f,
            GalPortraitSlot.Center => 0.5f,
            GalPortraitSlot.Right => 0.75f,
            _ => 0.25f
        };

        rect.anchorMin = new Vector2(normalizedX, 0f);
        rect.anchorMax = new Vector2(normalizedX, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
    }

    private void ApplyBackground(Sprite background)
    {
        if (activeMode != GalPresentationMode.FullscreenExclusive)
            return;

        fullscreenBackgroundImage.enabled = true;
        fullscreenBackgroundImage.sprite = background;
        fullscreenBackgroundImage.preserveAspect = true;
        fullscreenBackgroundImage.color = background != null
            ? Color.white
            : Color.black;
        fullscreenBackgroundFitter.aspectMode =
            AspectRatioFitter.AspectMode.EnvelopeParent;
        fullscreenBackgroundFitter.aspectRatio =
            background != null && background.rect.height > 0f
                ? background.rect.width / background.rect.height
                : 16f / 9f;
    }

    private void ConfigureConcurrentInputIsolation()
    {
        concurrentCanvasGroup.interactable = false;
        concurrentCanvasGroup.blocksRaycasts = false;
        concurrentAvatarImage.raycastTarget = false;
        concurrentSpeakerNameText.raycastTarget = false;
        concurrentDialogueText.raycastTarget = false;
        concurrentProgressFill.raycastTarget = false;
    }

    private void BeginConcurrentFade(
        float targetAlpha,
        bool deactivateAfterFade)
    {
        CancelConcurrentFade();

        if (!Application.isPlaying)
        {
            concurrentCanvasGroup.alpha = targetAlpha;
            if (deactivateAfterFade)
                DeactivateConcurrentVisuals();
            return;
        }

        concurrentFadeCoroutine = StartCoroutine(
            FadeConcurrent(targetAlpha, deactivateAfterFade));
    }

    private IEnumerator FadeConcurrent(
        float targetAlpha,
        bool deactivateAfterFade)
    {
        float startAlpha = concurrentCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < ConcurrentFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            concurrentCanvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsed / ConcurrentFadeDuration));
            yield return null;
        }

        concurrentCanvasGroup.alpha = targetAlpha;
        concurrentFadeCoroutine = null;

        if (deactivateAfterFade)
            DeactivateConcurrentVisuals();
    }

    private void CancelConcurrentFade()
    {
        if (concurrentFadeCoroutine == null)
            return;

        StopCoroutine(concurrentFadeCoroutine);
        concurrentFadeCoroutine = null;
    }

    private void DeactivateConcurrentVisuals()
    {
        concurrentCard.SetActive(false);
        visualRoot.SetActive(false);
    }
}
