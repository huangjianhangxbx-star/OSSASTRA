using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class SpriteBattleActorAnimationDriver : MonoBehaviour,
    IBattleActorAnimationDriver
{
    [SerializeField] private RectTransform portraitTransform;
    [SerializeField] private Image portraitImage;
    [SerializeField] private float pulseScale = 1.04f;
    [SerializeField] private float pulseDuration = 0.16f;

    private Tween activeTween;
    private BattlePresentationHandle activeHandle;
    private BattlePresentationOutcome? activeCancellationOutcome;
    private Vector3 boundScale = Vector3.one;
    private DRPGCombatantProfileSO boundProfile;

    public void BindActor(DRPGCombatantProfileSO profile)
    {
        CancelActiveTween(BattlePresentationCancelReason.Replaced);
        boundProfile = profile;
        if (portraitTransform == null)
            portraitTransform = transform as RectTransform;
        if (portraitImage == null)
            portraitImage = GetComponent<Image>();

        if (portraitTransform != null)
        {
            boundScale = portraitTransform.localScale;
            portraitTransform.localScale = boundScale;
        }
    }

    public IBattlePresentationHandle PlayIntent(
        EAnimationLogicState animationIntent)
    {
        if (!isActiveAndEnabled || portraitTransform == null ||
            portraitImage == null || boundProfile == null)
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        CancelActiveTween(BattlePresentationCancelReason.Replaced);
        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(
            reason => CancelPlayback(handle, reason));
        activeHandle = handle;
        activeCancellationOutcome = null;
        Tween tween = null;
        tween = portraitTransform
            .DOPunchScale(
                boundScale * Mathf.Max(0f, pulseScale - 1f),
                Mathf.Max(0.01f, pulseDuration),
                4,
                0.35f)
            .SetUpdate(true)
            .OnComplete(() => FinishPlayback(
                tween,
                handle,
                BattlePresentationOutcome.Completed))
            .OnKill(() => FinishPlayback(
                tween,
                handle,
                activeCancellationOutcome ??
                    BattlePresentationOutcome.Cancelled));
        activeTween = tween;
        return handle;
    }

    public void SetLowHpState(DRPGLowHpPresentationState state)
    {
        if (portraitImage == null)
            portraitImage = GetComponent<Image>();
        if (portraitImage == null || boundProfile == null)
            return;

        DRPGBattlePortraitState portraitState =
            state == DRPGLowHpPresentationState.DamageBroken
                ? DRPGBattlePortraitState.Broken
                : DRPGBattlePortraitState.Normal;
        Sprite sprite = boundProfile.ResolvePortrait(portraitState);
        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;
    }

    private void CancelPlayback(
        BattlePresentationHandle handle,
        BattlePresentationCancelReason reason)
    {
        if (handle == null || handle.IsSettled)
            return;
        if (!ReferenceEquals(activeHandle, handle))
        {
            handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
            return;
        }

        activeCancellationOutcome =
            BattlePresentationHandle.MapCancellation(reason);
        BattlePresentationOutcome outcome = activeCancellationOutcome.Value;
        Tween tween = activeTween;
        if (tween != null && tween.IsActive())
            tween.Kill(false);
        if (!handle.IsSettled)
            FinishPlayback(tween, handle, outcome);
    }

    private void CancelActiveTween(BattlePresentationCancelReason reason)
    {
        if (activeHandle != null && !activeHandle.IsSettled)
        {
            activeHandle.Cancel(reason);
            return;
        }

        Tween tween = activeTween;
        activeTween = null;
        if (tween != null && tween.IsActive())
            tween.Kill(false);
        RestoreStableState();
    }

    private void FinishPlayback(
        Tween tween,
        BattlePresentationHandle handle,
        BattlePresentationOutcome outcome)
    {
        if (ReferenceEquals(activeTween, tween))
        {
            activeTween = null;
            activeHandle = null;
            activeCancellationOutcome = null;
            RestoreStableState();
        }
        handle?.TrySettle(outcome);
    }

    private void RestoreStableState()
    {
        if (portraitTransform != null)
            portraitTransform.localScale = boundScale;
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
        CancelActiveTween(BattlePresentationCancelReason.ScopeEnded);
    }
}
