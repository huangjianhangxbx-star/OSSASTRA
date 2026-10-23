using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleDamageBrokenCutInPresenter : MonoBehaviour
{
    [SerializeField] private GameObject visibleRoot;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private CanvasGroup inputBlocker;
    [SerializeField] private List<BattleDamageBrokenCutInView> views =
        new List<BattleDamageBrokenCutInView>();
    [SerializeField] private float baseWidth = 440f;
    [SerializeField] private float minimumWidth = 240f;
    [SerializeField] private float spacing = 20f;
    [SerializeField] private float enterSeconds = 0.42f;
    [SerializeField] private float holdSeconds = 0.85f;
    [SerializeField] private float exitSeconds = 0.30f;

    private Sequence activeSequence;
    private BattlePresentationHandle activeHandle;

    public bool IsPlaying => activeSequence != null && activeSequence.IsActive();

    public static IReadOnlyList<DRPGBattleHitResult> FilterEligible(
        IReadOnlyList<DRPGBattleHitResult> hits)
    {
        var result = new List<DRPGBattleHitResult>();
        var seenTargets = new HashSet<int>();
        if (hits == null)
            return result;

        for (int i = 0; i < hits.Count; i++)
        {
            DRPGBattleHitResult hit = hits[i];
            if (hit == null || !hit.TriggeredDamageBroken ||
                hit.TargetRosterRole != DRPGBattleRosterRole.PartyMember ||
                !seenTargets.Add(hit.TargetRuntimeId))
            {
                continue;
            }
            result.Add(hit);
        }
        return result;
    }

    public static IReadOnlyList<float> BuildCenteredPositions(
        int count,
        float safeWidth,
        float preferredWidth,
        float gap)
    {
        var result = new List<float>();
        if (count <= 0)
            return result;

        float safeGap = Mathf.Max(0f, gap);
        float availableWidth = Mathf.Max(1f, safeWidth - safeGap * (count - 1));
        float width = Mathf.Min(Mathf.Max(1f, preferredWidth), availableWidth / count);
        float total = width * count + safeGap * (count - 1);
        float firstCenter = -total * 0.5f + width * 0.5f;
        for (int i = 0; i < count; i++)
            result.Add(firstCenter + i * (width + safeGap));
        return result;
    }

    public IBattlePresentationHandle Play(
        IReadOnlyList<BattleParticipantPresentationSnapshot> targets)
    {
        IReadOnlyList<BattleParticipantPresentationSnapshot> eligible =
            FilterEligible(targets);
        int count = Mathf.Min(eligible.Count, views.Count);
        if (count <= 0)
            return Settled(BattlePresentationOutcome.Skipped);

        Cancel(BattlePresentationCancelReason.Replaced);
        SetVisible(true);
        RectTransform area = safeArea != null ? safeArea : transform as RectTransform;
        float safeWidth = area != null && area.rect.width > 0f
            ? area.rect.width
            : 1600f;
        float safeHeight = area != null && area.rect.height > 0f
            ? area.rect.height
            : 1000f;
        float gap = Mathf.Max(0f, spacing);
        float availableWidth = Mathf.Max(1f, safeWidth - gap * (count - 1));
        float width = count <= 2
            ? Mathf.Min(baseWidth, availableWidth / count)
            : Mathf.Clamp(availableWidth / count, minimumWidth, baseWidth);
        float height = Mathf.Min(900f, safeHeight * 0.86f);
        IReadOnlyList<float> centers = BuildCenteredPositions(
            count,
            safeWidth,
            width,
            gap);

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        activeSequence = sequence;
        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(
            reason => CancelOwned(sequence, handle, reason));
        activeHandle = handle;

        float enter = Mathf.Max(0.01f, enterSeconds);
        float hold = Mathf.Max(0f, holdSeconds);
        float exit = Mathf.Max(0.01f, exitSeconds);
        for (int i = 0; i < views.Count; i++)
        {
            BattleDamageBrokenCutInView view = views[i];
            if (view == null)
                continue;
            if (i >= count)
            {
                view.Hide();
                continue;
            }

            view.Bind(eligible[i], width, height);
            RectTransform rect = view.Panel;
            if (rect == null)
                continue;
            Vector2 target = new Vector2(centers[i], 0f);
            rect.anchoredPosition = new Vector2(
                target.x,
                safeHeight + height * 0.5f);
            sequence.Insert(0f, rect.DOAnchorPos(target, enter)
                .SetEase(Ease.OutExpo));
            sequence.Insert(enter + hold, rect
                .DOAnchorPosY(safeHeight + height * 0.5f, exit)
                .SetEase(Ease.InExpo));
        }

        sequence.AppendInterval(Mathf.Max(
            0f,
            enter + hold + exit - sequence.Duration()));
        sequence.OnComplete(() => Finish(
            sequence,
            handle,
            BattlePresentationOutcome.Completed));
        sequence.OnKill(() => Finish(
            sequence,
            handle,
            BattlePresentationOutcome.Cancelled));
        return handle;
    }

    public void Cancel(BattlePresentationCancelReason reason)
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
        SetVisible(false);
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
        SetVisible(false);
        handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
    }

    private void Finish(
        Sequence sequence,
        BattlePresentationHandle handle,
        BattlePresentationOutcome outcome)
    {
        if (!ReferenceEquals(activeSequence, sequence) ||
            !ReferenceEquals(activeHandle, handle))
        {
            return;
        }

        activeSequence = null;
        activeHandle = null;
        SetVisible(false);
        handle.TrySettle(outcome);
    }

    private static IReadOnlyList<BattleParticipantPresentationSnapshot>
        FilterEligible(IReadOnlyList<BattleParticipantPresentationSnapshot> targets)
    {
        var result = new List<BattleParticipantPresentationSnapshot>();
        var seen = new HashSet<int>();
        if (targets == null)
            return result;

        for (int i = 0; i < targets.Count; i++)
        {
            BattleParticipantPresentationSnapshot target = targets[i];
            if (target == null ||
                target.RosterRole != DRPGBattleRosterRole.PartyMember ||
                target.LowHpState != DRPGLowHpPresentationState.DamageBroken ||
                !seen.Add(target.RuntimeId))
            {
                continue;
            }
            result.Add(target);
        }
        return result;
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }

    private void SetVisible(bool value)
    {
        if (visibleRoot != null)
            visibleRoot.SetActive(value);
        else if (gameObject.activeSelf != value)
            gameObject.SetActive(value);
        if (inputBlocker != null)
        {
            inputBlocker.blocksRaycasts = value;
            inputBlocker.interactable = value;
        }
    }

    private void OnDisable()
    {
        Cancel(BattlePresentationCancelReason.ScopeEnded);
    }
}
