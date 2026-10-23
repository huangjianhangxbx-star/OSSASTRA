using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleAttackCutInPresenter : MonoBehaviour
{
    [SerializeField] private GameObject visibleRoot;
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private Image dimmer;
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform panelWedge;
    [SerializeField] private Image portrait;
    [SerializeField] private RectTransform portraitTransform;
    [SerializeField] private BattleActorPresenter idleActorPresenter;
    [SerializeField] private TMP_Text actorName;
    [SerializeField] private Image impactFlash;
    [SerializeField] private CanvasGroup slashGroup;
    [SerializeField] private RectTransform[] slashStreaks =
        Array.Empty<RectTransform>();
    [SerializeField] private Vector2 panelEnterOffset =
        new Vector2(980f, 24f);
    [SerializeField] private Vector2 portraitEnterOffset =
        new Vector2(760f, -36f);
    [SerializeField] private Vector2 exitOffset =
        new Vector2(1100f, 40f);
    [SerializeField] private float openSeconds = 0.14f;
    [SerializeField] private float impactSeconds = 0.16f;
    [SerializeField] private float closeSeconds = 0.18f;

    private Sequence activeSequence;
    private BattlePresentationHandle activeHandle;
    private Vector2 panelRestPosition;
    private Vector2 panelWedgeRestPosition;
    private Vector2 portraitRestPosition;
    private Vector3 panelRestScale = Vector3.one;
    private Vector3 portraitRestScale = Vector3.one;
    private Color dimmerRestColor = new Color(0f, 0f, 0f, 0.72f);
    private Color portraitRestColor = Color.white;
    private Color flashRestColor = Color.white;
    private Vector2[] slashRestPositions = Array.Empty<Vector2>();
    private float boundDirection = 1f;
    private bool layoutCaptured;

    public int BoundActorRuntimeId { get; private set; }
    public bool IsVisible => visibleRoot != null && visibleRoot.activeSelf;
    public event Action<bool> VisibilityChanged;

    public IBattlePresentationHandle Play(
        BattleAttackCutInStage stage,
        BattleParticipantPresentationSnapshot actor,
        float durationSeconds)
    {
        if (actor == null)
            return Settled(BattlePresentationOutcome.Skipped);

        CaptureLayout();
        switch (stage)
        {
            case BattleAttackCutInStage.Open:
                return PlayOpen(actor, ResolveDuration(
                    durationSeconds,
                    openSeconds));
            case BattleAttackCutInStage.Impact:
                return PlayImpact(actor, ResolveDuration(
                    durationSeconds,
                    impactSeconds));
            case BattleAttackCutInStage.Close:
                return PlayClose(ResolveDuration(
                    durationSeconds,
                    closeSeconds));
            default:
                return Settled(BattlePresentationOutcome.Skipped);
        }
    }

    public void Cancel(
        BattlePresentationCancelReason reason =
            BattlePresentationCancelReason.ScopeEnded)
    {
        if (activeHandle != null && !activeHandle.IsSettled)
        {
            activeHandle.Cancel(reason);
            return;
        }

        Sequence sequence = activeSequence;
        activeSequence = null;
        activeHandle = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
        HideImmediate();
    }

    private IBattlePresentationHandle PlayOpen(
        BattleParticipantPresentationSnapshot actor,
        float duration)
    {
        Cancel(BattlePresentationCancelReason.Replaced);
        BindActor(actor);
        SetVisible(true);
        float direction = actor.Side == DRPGBattleSide.Player ? 1f : -1f;

        if (overlayGroup != null)
            overlayGroup.alpha = 1f;
        if (dimmer != null)
        {
            Color transparent = dimmerRestColor;
            transparent.a = 0f;
            dimmer.color = transparent;
        }
        if (panel != null)
        {
            panel.anchoredPosition = panelRestPosition +
                MultiplyX(panelEnterOffset, direction);
            panel.localScale = panelRestScale;
        }
        if (panelWedge != null)
        {
            panelWedge.anchoredPosition = panelWedgeRestPosition +
                MultiplyX(panelEnterOffset, direction);
        }
        if (portraitTransform != null)
        {
            portraitTransform.anchoredPosition = portraitRestPosition +
                MultiplyX(portraitEnterOffset, direction);
            portraitTransform.localScale = portraitRestScale * 1.08f;
        }
        if (portrait != null)
        {
            Color transparent = portraitRestColor;
            transparent.a = 0f;
            portrait.color = transparent;
        }
        ResetImpactLayers();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        BattlePresentationHandle handle = Own(sequence);
        if (dimmer != null)
            sequence.Insert(0f, dimmer.DOFade(dimmerRestColor.a, duration * 0.55f));
        if (panel != null)
        {
            sequence.Insert(0f, panel
                .DOAnchorPos(panelRestPosition, duration)
                .SetEase(Ease.OutExpo));
        }
        if (panelWedge != null)
        {
            sequence.Insert(0.025f, panelWedge
                .DOAnchorPos(panelWedgeRestPosition, duration)
                .SetEase(Ease.OutExpo));
        }
        if (portraitTransform != null)
        {
            sequence.Insert(0.035f, portraitTransform
                .DOAnchorPos(portraitRestPosition, duration * 0.92f)
                .SetEase(Ease.OutBack, 1.12f));
            sequence.Insert(0.035f, portraitTransform
                .DOScale(portraitRestScale, duration * 0.92f)
                .SetEase(Ease.OutCubic));
        }
        if (portrait != null)
            sequence.Insert(0.035f, portrait.DOFade(portraitRestColor.a, duration * 0.5f));
        CompleteOwnership(sequence, handle, true);
        return handle;
    }

    private IBattlePresentationHandle PlayImpact(
        BattleParticipantPresentationSnapshot actor,
        float duration)
    {
        CancelActiveOnly(BattlePresentationCancelReason.Replaced);
        if (!IsVisible)
        {
            BindActor(actor);
            RestoreOpenState();
            SetVisible(true);
        }

        float direction = actor.Side == DRPGBattleSide.Player ? 1f : -1f;
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        BattlePresentationHandle handle = Own(sequence);
        if (impactFlash != null)
        {
            Color transparent = flashRestColor;
            transparent.a = 0f;
            impactFlash.color = transparent;
            sequence.Insert(0f, impactFlash
                .DOFade(Mathf.Max(0.1f, flashRestColor.a), duration * 0.28f)
                .SetEase(Ease.OutQuad));
            sequence.Insert(duration * 0.28f, impactFlash
                .DOFade(0f, duration * 0.58f)
                .SetEase(Ease.InQuad));
        }
        if (panel != null)
        {
            sequence.Insert(0f, panel.DOPunchAnchorPos(
                new Vector2(-38f * direction, 9f),
                duration,
                8,
                0.42f));
        }
        if (portraitTransform != null)
        {
            sequence.Insert(0f, portraitTransform.DOPunchAnchorPos(
                new Vector2(-46f * direction, 12f),
                duration,
                9,
                0.38f));
            sequence.Insert(0f, portraitTransform.DOPunchScale(
                Vector3.one * 0.045f,
                duration,
                7,
                0.38f));
        }
        InsertSlashMotion(sequence, duration, direction);
        CompleteOwnership(sequence, handle, true);
        return handle;
    }

    private IBattlePresentationHandle PlayClose(float duration)
    {
        CancelActiveOnly(BattlePresentationCancelReason.Replaced);
        if (!IsVisible)
            return Settled(BattlePresentationOutcome.Skipped);

        float direction = boundDirection;
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        BattlePresentationHandle handle = Own(sequence);
        ResetImpactLayers();
        if (dimmer != null)
            sequence.Insert(0f, dimmer.DOFade(0f, duration));
        if (panel != null)
        {
            sequence.Insert(0f, panel
                .DOAnchorPos(
                    panelRestPosition + MultiplyX(exitOffset, direction),
                    duration)
                .SetEase(Ease.InExpo));
        }
        if (panelWedge != null)
        {
            sequence.Insert(0f, panelWedge
                .DOAnchorPos(
                    panelWedgeRestPosition + MultiplyX(exitOffset, direction),
                    duration)
                .SetEase(Ease.InExpo));
        }
        if (portraitTransform != null)
        {
            sequence.Insert(0f, portraitTransform
                .DOAnchorPos(
                    portraitRestPosition + MultiplyX(exitOffset, direction),
                    duration * 0.92f)
                .SetEase(Ease.InBack, 1.08f));
        }
        if (portrait != null)
            sequence.Insert(duration * 0.35f, portrait.DOFade(0f, duration * 0.55f));
        CompleteOwnership(sequence, handle, false);
        return handle;
    }

    private void InsertSlashMotion(
        Sequence sequence,
        float duration,
        float direction)
    {
        if (slashGroup != null)
        {
            slashGroup.alpha = 0f;
            sequence.Insert(0f, slashGroup.DOFade(1f, duration * 0.18f));
            sequence.Insert(duration * 0.42f, slashGroup.DOFade(0f, duration * 0.5f));
        }

        if (slashStreaks == null)
            return;
        for (int i = 0; i < slashStreaks.Length; i++)
        {
            RectTransform streak = slashStreaks[i];
            if (streak == null)
                continue;
            Vector2 rest = i < slashRestPositions.Length
                ? slashRestPositions[i]
                : streak.anchoredPosition;
            streak.anchoredPosition = rest +
                new Vector2(300f * direction, -160f);
            sequence.Insert(i * 0.018f, streak
                .DOAnchorPos(
                    rest + new Vector2(-180f * direction, 120f),
                    duration * 0.78f)
                .SetEase(Ease.OutExpo));
        }
    }

    private BattlePresentationHandle Own(Sequence sequence)
    {
        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(
            reason => CancelOwned(sequence, handle, reason));
        activeSequence = sequence;
        activeHandle = handle;
        return handle;
    }

    private void CompleteOwnership(
        Sequence sequence,
        BattlePresentationHandle handle,
        bool keepVisible)
    {
        sequence.OnComplete(() => FinishOwned(
            sequence,
            handle,
            BattlePresentationOutcome.Completed,
            keepVisible));
        sequence.OnKill(() => FinishOwned(
            sequence,
            handle,
            BattlePresentationOutcome.Cancelled,
            keepVisible));
    }

    private void CancelOwned(
        Sequence sequence,
        BattlePresentationHandle handle,
        BattlePresentationCancelReason reason)
    {
        if (!ReferenceEquals(activeSequence, sequence) ||
            !ReferenceEquals(activeHandle, handle))
        {
            handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
            return;
        }

        activeSequence = null;
        activeHandle = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
        HideImmediate();
        handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
    }

    private void FinishOwned(
        Sequence sequence,
        BattlePresentationHandle handle,
        BattlePresentationOutcome outcome,
        bool keepVisible)
    {
        if (!ReferenceEquals(activeSequence, sequence) ||
            !ReferenceEquals(activeHandle, handle))
        {
            return;
        }

        activeSequence = null;
        activeHandle = null;
        if (keepVisible)
            RestoreOpenState();
        else
            HideImmediate();
        handle.TrySettle(outcome);
    }

    private void CancelActiveOnly(BattlePresentationCancelReason reason)
    {
        BattlePresentationHandle handle = activeHandle;
        if (handle != null && !handle.IsSettled)
        {
            handle.Cancel(reason);
            return;
        }

        Sequence sequence = activeSequence;
        activeSequence = null;
        activeHandle = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
    }

    private void BindActor(BattleParticipantPresentationSnapshot actor)
    {
        BoundActorRuntimeId = actor.RuntimeId;
        boundDirection = actor.Side == DRPGBattleSide.Player ? 1f : -1f;
        if (portrait != null)
        {
            Sprite sprite = actor.AttackCutInPortrait != null
                ? actor.AttackCutInPortrait
                : actor.FullPortrait;
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            portrait.preserveAspect = true;
        }
        if (actorName != null)
            actorName.text = actor.DisplayName;
    }

    private void CaptureLayout()
    {
        if (layoutCaptured)
            return;
        if (portraitTransform == null && portrait != null)
            portraitTransform = portrait.rectTransform;
        if (panel != null)
        {
            panelRestPosition = panel.anchoredPosition;
            panelRestScale = panel.localScale;
        }
        if (panelWedge != null)
            panelWedgeRestPosition = panelWedge.anchoredPosition;
        if (portraitTransform != null)
        {
            portraitRestPosition = portraitTransform.anchoredPosition;
            portraitRestScale = portraitTransform.localScale;
        }
        if (dimmer != null)
            dimmerRestColor = dimmer.color;
        if (portrait != null)
            portraitRestColor = portrait.color;
        if (impactFlash != null)
            flashRestColor = impactFlash.color;
        slashRestPositions = new Vector2[
            slashStreaks != null ? slashStreaks.Length : 0];
        for (int i = 0; i < slashRestPositions.Length; i++)
        {
            if (slashStreaks[i] != null)
                slashRestPositions[i] = slashStreaks[i].anchoredPosition;
        }
        layoutCaptured = true;
    }

    private void RestoreOpenState()
    {
        if (overlayGroup != null)
            overlayGroup.alpha = 1f;
        if (dimmer != null)
            dimmer.color = dimmerRestColor;
        if (panel != null)
        {
            panel.anchoredPosition = panelRestPosition;
            panel.localScale = panelRestScale;
        }
        if (panelWedge != null)
            panelWedge.anchoredPosition = panelWedgeRestPosition;
        if (portraitTransform != null)
        {
            portraitTransform.anchoredPosition = portraitRestPosition;
            portraitTransform.localScale = portraitRestScale;
        }
        if (portrait != null)
            portrait.color = portraitRestColor;
        ResetImpactLayers();
    }

    private void ResetImpactLayers()
    {
        if (impactFlash != null)
        {
            Color color = flashRestColor;
            color.a = 0f;
            impactFlash.color = color;
        }
        if (slashGroup != null)
            slashGroup.alpha = 0f;
        for (int i = 0; i < slashRestPositions.Length; i++)
        {
            if (slashStreaks[i] != null)
                slashStreaks[i].anchoredPosition = slashRestPositions[i];
        }
    }

    private void HideImmediate()
    {
        RestoreOpenState();
        BoundActorRuntimeId = 0;
        boundDirection = 1f;
        SetVisible(false);
    }

    private void SetVisible(bool value)
    {
        if (visibleRoot == null)
            return;

        bool changed = visibleRoot.activeSelf != value;
        visibleRoot.SetActive(value);
        idleActorPresenter?.SetPresentationSuppressed(value);
        if (changed)
            VisibilityChanged?.Invoke(value);
    }

    private static Vector2 MultiplyX(Vector2 value, float direction)
    {
        return new Vector2(value.x * direction, value.y);
    }

    private static float ResolveDuration(float requested, float fallback)
    {
        return Mathf.Max(0.01f, requested > 0f ? requested : fallback);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }

    private void OnDisable()
    {
        Cancel(BattlePresentationCancelReason.ScopeEnded);
    }
}
