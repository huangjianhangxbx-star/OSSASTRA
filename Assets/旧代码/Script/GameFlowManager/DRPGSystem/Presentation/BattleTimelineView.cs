using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleTimelineView : MonoBehaviour
{
    [SerializeField] private List<BattleTimelineEntryView> entries =
        new List<BattleTimelineEntryView>();
    [SerializeField] private HorizontalLayoutGroup layoutGroup;
    [SerializeField] private Graphic transitionPulse;

    private readonly List<int> boundRuntimeIds = new List<int>();
    private Sequence activeTransition;
    private BattlePresentationHandle activeHandle;
    private BattlePresentationOutcome? activeCancellationOutcome;
    private bool hasBound;

    public bool IsTransitionPlaying =>
        activeTransition != null && activeTransition.IsActive();

    public void BindImmediate(
        IReadOnlyList<DRPGBattleParticipantViewModel> models)
    {
        KillActiveTransition(BattlePresentationCancelReason.Replaced);
        List<int> runtimeIds = CollectRuntimeIds(models);
        ApplyOrderAndBind(models);
        ReplaceBoundIds(runtimeIds);
        hasBound = true;
        RestoreStableLayout();
    }

    public IBattlePresentationHandle PlayTransition(
        IReadOnlyList<DRPGBattleParticipantViewModel> models,
        float durationSeconds)
    {
        List<int> newRuntimeIds = CollectRuntimeIds(models);
        if (!hasBound)
        {
            BindImmediate(models);
            return Settled(BattlePresentationOutcome.Completed);
        }

        if (SameIds(boundRuntimeIds, newRuntimeIds))
        {
            RebindExisting(models);
            return Settled(BattlePresentationOutcome.Completed);
        }

        return StartTransition(models, newRuntimeIds, durationSeconds);
    }

    public IBattlePresentationHandle PlayTransition(
        IReadOnlyList<int> newRuntimeIds,
        float durationSeconds)
    {
        if (newRuntimeIds == null || !hasBound ||
            SameIds(boundRuntimeIds, newRuntimeIds))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        return StartTransition(null, newRuntimeIds, durationSeconds);
    }

    public void SetTargetFeedback(
        int runtimeId,
        DRPGBattleTargetFeedback feedback)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            BattleTimelineEntryView entry = entries[i];
            if (entry != null && entry.BoundRuntimeId == runtimeId)
                entry.SetTargetFeedback(feedback);
        }
    }

    public void ClearTargetFeedback()
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i]?.SetTargetFeedback(DRPGBattleTargetFeedback.None);
    }

    public void KillActiveTransition(
        BattlePresentationCancelReason reason =
            BattlePresentationCancelReason.Replaced)
    {
        if (activeHandle != null && !activeHandle.IsSettled)
        {
            activeHandle.Cancel(reason);
            return;
        }

        Sequence sequence = activeTransition;
        activeTransition = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
        RestoreStableLayout();
    }

    private IBattlePresentationHandle StartTransition(
        IReadOnlyList<DRPGBattleParticipantViewModel> models,
        IReadOnlyList<int> newRuntimeIds,
        float durationSeconds)
    {
        KillActiveTransition(BattlePresentationCancelReason.Replaced);
        var oldPositions = new Dictionary<int, Vector2>();
        for (int i = 0; i < entries.Count; i++)
        {
            BattleTimelineEntryView entry = entries[i];
            if (entry != null && entry.BoundRuntimeId != 0 &&
                entry.RectTransform != null)
            {
                oldPositions[entry.BoundRuntimeId] =
                    entry.RectTransform.anchoredPosition;
            }
        }

        IReadOnlyList<BattleTimelineTransitionMove> moves =
            BattleTimelineTransitionPresenter.BuildMoves(
                boundRuntimeIds,
                newRuntimeIds);
        Dictionary<int, BattleTimelineEntryView> ordered = models != null
            ? ApplyOrderAndBind(models)
            : ApplyOrderOnly(newRuntimeIds);
        if (ordered.Count != newRuntimeIds.Count)
        {
            RestoreStableLayout();
            return Settled(BattlePresentationOutcome.Skipped);
        }

        RectTransform root = transform as RectTransform;
        if (root != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        var targetPositions = new Dictionary<int, Vector2>();
        foreach (KeyValuePair<int, BattleTimelineEntryView> pair in ordered)
        {
            if (pair.Value != null && pair.Value.RectTransform != null)
                targetPositions[pair.Key] = pair.Value.RectTransform.anchoredPosition;
        }

        if (layoutGroup != null)
            layoutGroup.enabled = false;

        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(
            reason => CancelTransition(handle, reason));
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        activeTransition = sequence;
        activeHandle = handle;
        activeCancellationOutcome = null;
        float duration = Mathf.Max(0.01f, durationSeconds);
        for (int i = 0; i < moves.Count; i++)
        {
            BattleTimelineTransitionMove move = moves[i];
            if (!ordered.TryGetValue(
                    move.RuntimeId,
                    out BattleTimelineEntryView entry) ||
                entry == null || entry.RectTransform == null ||
                !oldPositions.TryGetValue(move.RuntimeId, out Vector2 oldPosition) ||
                !targetPositions.TryGetValue(move.RuntimeId, out Vector2 targetPosition))
            {
                continue;
            }

            RectTransform rect = entry.RectTransform;
            rect.anchoredPosition = oldPosition;
            if (move.IsCompletingActor)
            {
                Vector2 midpoint = Vector2.Lerp(oldPosition, targetPosition, 0.52f) +
                    Vector2.up * 12f;
                Sequence hop = DOTween.Sequence()
                    .Append(rect.DOAnchorPos(midpoint, duration * 0.45f)
                        .SetEase(Ease.OutQuad))
                    .Append(rect.DOAnchorPos(targetPosition, duration * 0.55f)
                        .SetEase(Ease.OutCubic));
                sequence.Insert(0f, hop);
                Vector3 baseScale = rect.localScale;
                sequence.Insert(0f, rect.DOScale(baseScale * 1.07f, duration * 0.35f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine));
            }
            else
            {
                sequence.Insert(0f, rect.DOAnchorPos(targetPosition, duration)
                    .SetEase(Ease.OutCubic));
            }
        }

        if (transitionPulse != null)
        {
            transitionPulse.gameObject.SetActive(true);
            Color pulseColor = transitionPulse.color;
            pulseColor.a = 0f;
            transitionPulse.color = pulseColor;
            sequence.Insert(0f, transitionPulse.DOFade(1f, 0.08f)
                .SetLoops(2, LoopType.Yoyo));
        }

        sequence.AppendInterval(Mathf.Max(0f, duration - sequence.Duration()));
        sequence.OnComplete(() => FinishTransition(
            sequence,
            handle,
            BattlePresentationOutcome.Completed));
        sequence.OnKill(() => FinishTransition(
            sequence,
            handle,
            activeCancellationOutcome ?? BattlePresentationOutcome.Cancelled));
        ReplaceBoundIds(newRuntimeIds);
        return handle;
    }

    private void CancelTransition(
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
        Sequence sequence = activeTransition;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
        if (!handle.IsSettled)
            FinishTransition(sequence, handle, outcome);
    }

    private Dictionary<int, BattleTimelineEntryView> ApplyOrderAndBind(
        IReadOnlyList<DRPGBattleParticipantViewModel> models)
    {
        var ordered = new Dictionary<int, BattleTimelineEntryView>();
        var used = new HashSet<BattleTimelineEntryView>();
        int modelCount = models != null ? models.Count : 0;

        for (int i = 0; i < modelCount && i < entries.Count; i++)
        {
            DRPGBattleParticipantViewModel model = models[i];
            BattleTimelineEntryView entry = FindEntry(model.RuntimeId, used) ??
                FindUnusedEntry(used);
            if (entry == null)
                continue;

            used.Add(entry);
            ordered[model.RuntimeId] = entry;
            entry.transform.SetSiblingIndex(i);
            entry.Bind(model);
        }

        int trailingIndex = modelCount;
        for (int i = 0; i < entries.Count; i++)
        {
            BattleTimelineEntryView entry = entries[i];
            if (entry == null || used.Contains(entry))
                continue;
            entry.Bind(null);
            entry.transform.SetSiblingIndex(trailingIndex++);
        }

        return ordered;
    }

    private Dictionary<int, BattleTimelineEntryView> ApplyOrderOnly(
        IReadOnlyList<int> runtimeIds)
    {
        var ordered = new Dictionary<int, BattleTimelineEntryView>();
        var used = new HashSet<BattleTimelineEntryView>();
        for (int i = 0; i < runtimeIds.Count; i++)
        {
            BattleTimelineEntryView entry = FindEntry(runtimeIds[i], used);
            if (entry == null)
                continue;
            used.Add(entry);
            ordered[runtimeIds[i]] = entry;
            entry.transform.SetSiblingIndex(i);
        }
        return ordered;
    }

    private void RebindExisting(
        IReadOnlyList<DRPGBattleParticipantViewModel> models)
    {
        int modelCount = models != null ? models.Count : 0;
        for (int i = 0; i < modelCount; i++)
        {
            DRPGBattleParticipantViewModel model = models[i];
            BattleTimelineEntryView entry = FindEntry(model.RuntimeId, null);
            entry?.Bind(model);
        }
    }

    private BattleTimelineEntryView FindEntry(
        int runtimeId,
        ISet<BattleTimelineEntryView> excluded)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            BattleTimelineEntryView entry = entries[i];
            if (entry != null && entry.BoundRuntimeId == runtimeId &&
                (excluded == null || !excluded.Contains(entry)))
            {
                return entry;
            }
        }

        return null;
    }

    private BattleTimelineEntryView FindUnusedEntry(
        ISet<BattleTimelineEntryView> used)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            BattleTimelineEntryView entry = entries[i];
            if (entry != null && !used.Contains(entry))
                return entry;
        }

        return null;
    }

    private void FinishTransition(
        Sequence sequence,
        BattlePresentationHandle handle,
        BattlePresentationOutcome outcome)
    {
        if (ReferenceEquals(activeTransition, sequence))
        {
            activeTransition = null;
            activeHandle = null;
            activeCancellationOutcome = null;
            RestoreStableLayout();
        }
        handle?.TrySettle(outcome);
    }

    private void RestoreStableLayout()
    {
        if (layoutGroup != null)
            layoutGroup.enabled = true;
        RectTransform root = transform as RectTransform;
        if (root != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        if (transitionPulse != null)
        {
            Color color = transitionPulse.color;
            color.a = 0f;
            transitionPulse.color = color;
        }
    }

    private static List<int> CollectRuntimeIds(
        IReadOnlyList<DRPGBattleParticipantViewModel> models)
    {
        var result = new List<int>();
        if (models == null)
            return result;
        for (int i = 0; i < models.Count; i++)
            result.Add(models[i].RuntimeId);
        return result;
    }

    private static bool SameIds(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }

    private void ReplaceBoundIds(IReadOnlyList<int> values)
    {
        boundRuntimeIds.Clear();
        for (int i = 0; i < values.Count; i++)
            boundRuntimeIds.Add(values[i]);
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
        KillActiveTransition(BattlePresentationCancelReason.ScopeEnded);
    }
}
