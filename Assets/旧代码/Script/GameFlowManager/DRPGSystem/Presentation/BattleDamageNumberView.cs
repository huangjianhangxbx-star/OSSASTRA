using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class BattleDamageNumberView : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private CanvasGroup canvasGroup;

    private Sequence sequence;
    private BattlePresentationHandle activeHandle;
    private IDRPGBattlePresentationClock outcomeClock;
    private double outcomeStarted;
    private float outcomeDuration;

    public bool IsPlaying => sequence != null && sequence.IsActive();

    // Standard feedback is visible in the issuing hit batch, not after a
    // separate fade-in. Its private tween shares the action's unscaled clock.
    internal IBattlePresentationHandle PlayOutcomeLabel(string label, IDRPGBattlePresentationClock clock,
        double issuedAt, float duration)
    {
        Cancel(BattlePresentationCancelReason.Replaced);
        if (rectTransform == null) rectTransform = transform as RectTransform;
        gameObject.SetActive(true);
        if (valueText != null) valueText.text = label;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        outcomeClock = clock; outcomeStarted = issuedAt; outcomeDuration = Mathf.Max(.45f, duration);
        var start = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
        var owned = DOTween.Sequence().SetAutoKill(false).Pause();
        sequence = owned;
        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(reason => CancelOwned(owned, handle, reason));
        activeHandle = handle;
        owned.Append(DOTween.To(() => 0f, t =>
        {
            if (!ReferenceEquals(sequence, owned)) return;
            if (rectTransform != null) rectTransform.anchoredPosition = start + Vector2.up * (36f * t);
            if (canvasGroup != null) canvasGroup.alpha = 1f - Mathf.Clamp01((t - .72f) / .28f);
        }, 1f, outcomeDuration).SetEase(Ease.Linear));
        return handle;
    }

    internal void TickOutcomeLabel()
    {
        var owned = sequence;
        if (outcomeClock == null || owned == null || !owned.IsActive()) return;
        float elapsed = (float)System.Math.Max(0d, outcomeClock.Now - outcomeStarted);
        owned.Goto(Mathf.Min(outcomeDuration, elapsed), false);
        if (elapsed < outcomeDuration) return;
        var handle = activeHandle;
        outcomeClock = null;
        Finish(owned, handle, BattlePresentationOutcome.Completed);
        owned.Kill(false);
    }

    public IBattlePresentationHandle Play(float damage, float duration)
    {
        Cancel(BattlePresentationCancelReason.Replaced);
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        gameObject.SetActive(true);
        if (valueText != null)
            valueText.text = "-" + Mathf.CeilToInt(Mathf.Max(0f, damage));
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        float safeDuration = Mathf.Max(0.35f, duration);
        float startY = rectTransform != null
            ? rectTransform.anchoredPosition.y
            : 0f;
        Sequence owned = DOTween.Sequence().SetUpdate(true);
        sequence = owned;
        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(
            reason => CancelOwned(owned, handle, reason));
        activeHandle = handle;

        if (canvasGroup != null)
            owned.Append(canvasGroup.DOFade(1f, 0.12f));
        if (rectTransform != null)
        {
            owned.Join(rectTransform
                .DOAnchorPosY(startY + 36f, safeDuration)
                .SetEase(Ease.OutCubic));
        }
        if (canvasGroup != null)
        {
            owned.Insert(
                Mathf.Max(0.12f, safeDuration - 0.30f),
                canvasGroup.DOFade(0f, 0.30f));
        }
        owned.OnComplete(() => Finish(
            owned,
            handle,
            BattlePresentationOutcome.Completed));
        owned.OnKill(() => Finish(
            owned,
            handle,
            BattlePresentationOutcome.Cancelled));
        return handle;
    }

    public void Cancel(BattlePresentationCancelReason reason)
    {
        outcomeClock = null;
        BattlePresentationHandle handle = activeHandle;
        if (handle != null && !handle.IsSettled)
        {
            handle.Cancel(reason);
            return;
        }

        Sequence owned = sequence;
        sequence = null;
        activeHandle = null;
        if (owned != null && owned.IsActive())
            owned.Kill(false);
    }

    private void CancelOwned(
        Sequence owned,
        BattlePresentationHandle handle,
        BattlePresentationCancelReason reason)
    {
        if (!ReferenceEquals(sequence, owned) ||
            !ReferenceEquals(activeHandle, handle))
        {
            handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
            return;
        }

        sequence = null;
        activeHandle = null;
        if (owned != null && owned.IsActive())
            owned.Kill(false);
        handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
    }

    private void Finish(
        Sequence owned,
        BattlePresentationHandle handle,
        BattlePresentationOutcome outcome)
    {
        if (!ReferenceEquals(sequence, owned) ||
            !ReferenceEquals(activeHandle, handle))
        {
            return;
        }

        sequence = null;
        activeHandle = null;
        handle.TrySettle(outcome);
    }

    private void OnDisable()
    {
        Cancel(BattlePresentationCancelReason.ScopeEnded);
    }
}
