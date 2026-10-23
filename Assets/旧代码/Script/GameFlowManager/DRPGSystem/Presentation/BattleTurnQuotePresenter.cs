using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class BattleTurnQuotePresenter : MonoBehaviour
{
    [SerializeField] private TMP_Text quoteText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInSeconds = 0.14f;
    [SerializeField] private float holdSeconds = 1.25f;
    [SerializeField] private float fadeOutSeconds = 0.26f;

    private Sequence activeSequence;
    private BattlePresentationHandle activeHandle;
    private BattlePresentationOutcome? activeCancellationOutcome;

    public event Action<int, string> VoiceCueRequested;

    public bool IsPlaying => activeSequence != null && activeSequence.IsActive();

    public IBattlePresentationHandle Show(
        BattleParticipantRuntime participant,
        DRPGBattleQuoteContext context)
    {
        return Show(
            participant != null
                ? BattleParticipantPresentationSnapshot.From(participant)
                : null,
            context);
    }

    public IBattlePresentationHandle Show(
        BattleParticipantPresentationSnapshot participant,
        DRPGBattleQuoteContext context)
    {
        DRPGBattleQuoteSetSO quoteSet = participant?.BattleQuotes;
        if (quoteSet == null ||
            !quoteSet.TryGetRandom(context, out DRPGBattleQuoteLine line))
        {
            Hide(BattlePresentationCancelReason.Replaced);
            return Settled(BattlePresentationOutcome.Skipped);
        }

        Hide(BattlePresentationCancelReason.Replaced);
        if (quoteText != null)
        {
            quoteText.text = participant.DisplayName +
                "　" + line.Text + "　";
            quoteText.raycastTarget = false;
            quoteText.gameObject.SetActive(true);
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(true);

        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(
            reason => CancelPlayback(handle, reason));
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        activeSequence = sequence;
        activeHandle = handle;
        activeCancellationOutcome = null;
        if (canvasGroup != null)
            sequence.Append(canvasGroup.DOFade(1f, Mathf.Max(0.01f, fadeInSeconds)));
        sequence.AppendInterval(Mathf.Max(0f, holdSeconds));
        if (canvasGroup != null)
            sequence.Append(canvasGroup.DOFade(0f, Mathf.Max(0.01f, fadeOutSeconds)));
        sequence.OnComplete(() => Finish(
            sequence,
            handle,
            BattlePresentationOutcome.Completed));
        sequence.OnKill(() => Finish(
            sequence,
            handle,
            activeCancellationOutcome ?? BattlePresentationOutcome.Cancelled));

        if (!string.IsNullOrWhiteSpace(line.VoiceCueId))
            VoiceCueRequested?.Invoke(participant.RuntimeId, line.VoiceCueId);
        return handle;
    }

    public void Hide()
    {
        Hide(BattlePresentationCancelReason.Replaced);
    }

    public void Hide(BattlePresentationCancelReason reason)
    {
        if (activeHandle != null && !activeHandle.IsSettled)
            activeHandle.Cancel(reason);
        else
            RestoreStableState();
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
        Sequence sequence = activeSequence;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
        if (!handle.IsSettled)
            Finish(sequence, handle, outcome);
    }

    private void Finish(
        Sequence sequence,
        BattlePresentationHandle handle,
        BattlePresentationOutcome outcome)
    {
        if (ReferenceEquals(activeSequence, sequence))
        {
            activeSequence = null;
            activeHandle = null;
            activeCancellationOutcome = null;
            RestoreStableState();
        }
        handle?.TrySettle(outcome);
    }

    private void RestoreStableState()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (quoteText != null)
            quoteText.gameObject.SetActive(false);
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
        Hide(BattlePresentationCancelReason.ScopeEnded);
    }
}
