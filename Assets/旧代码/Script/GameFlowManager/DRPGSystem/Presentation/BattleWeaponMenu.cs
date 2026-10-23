using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Dynamic battle-only view over the current actor's backpack candidates.
/// Eligibility and transactions remain domain-owned.
/// </summary>
public sealed class BattleWeaponMenu : MonoBehaviour
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private BattleWeaponMenuEntryView entryTemplate;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private TMP_Text feedbackText;

    private readonly List<BattleWeaponMenuEntryView> entries =
        new List<BattleWeaponMenuEntryView>();
    private DRPGBattleSession session;
    private bool inputEnabled;
    private Tween feedbackTween;
    private int visibleEntryCount;

    public event Action<DRPGBattleWeaponCandidate> WeaponRequested;
    public int VisibleEntryCount => visibleEntryCount;

    public void Bind(DRPGBattleSession battleSession)
    {
        session = battleSession;
        Refresh();
    }

    public void Refresh()
    {
        EnsurePool(session?.GetCurrentWeaponCandidates().Count ?? 0);
        IReadOnlyList<DRPGBattleWeaponCandidate> candidates =
            session?.GetCurrentWeaponCandidates() ??
            Array.Empty<DRPGBattleWeaponCandidate>();
        visibleEntryCount = candidates.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            bool visible = i < candidates.Count;
            entries[i].gameObject.SetActive(visible);
            if (visible)
            {
                entries[i].Bind(
                    candidates[i],
                    inputEnabled,
                    RequestWeapon,
                    SetFeedback);
            }
        }
        emptyText?.gameObject.SetActive(candidates.Count == 0);
        if (candidates.Count == 0)
            SetFeedback("背包中没有可切换武器");
        else
            SetFeedback(string.Empty);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        Refresh();
    }

    public void Unbind()
    {
        session = null;
        visibleEntryCount = 0;
        SetInputEnabled(false);
        SetFeedback(string.Empty);
    }

    public void SetFeedback(string value)
    {
        if (feedbackText == null)
            return;
        feedbackTween?.Kill(false);
        feedbackText.SetText(value ?? string.Empty);
        feedbackText.alpha = string.IsNullOrWhiteSpace(value) ? 0f : 1f;
        if (!string.IsNullOrWhiteSpace(value))
        {
            feedbackTween = feedbackText.DOFade(0.62f, 0.55f)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    private void EnsurePool(int required)
    {
        if (entryTemplate == null || contentRoot == null)
            return;
        if (entries.Count == 0)
        {
            entries.Add(entryTemplate);
            entryTemplate.gameObject.SetActive(false);
        }
        while (entries.Count < required)
        {
            BattleWeaponMenuEntryView entry = Instantiate(
                entryTemplate,
                contentRoot);
            entry.name = "WeaponEntry_" + entries.Count;
            entries.Add(entry);
        }
    }

    private void RequestWeapon(DRPGBattleWeaponCandidate candidate)
    {
        if (!inputEnabled || candidate == null || !candidate.IsEligible)
        {
            SetFeedback(candidate?.RejectionReason ?? "当前不可切换武器");
            return;
        }
        WeaponRequested?.Invoke(candidate);
    }

    private void OnDisable()
    {
        feedbackTween?.Kill(false);
    }
}
