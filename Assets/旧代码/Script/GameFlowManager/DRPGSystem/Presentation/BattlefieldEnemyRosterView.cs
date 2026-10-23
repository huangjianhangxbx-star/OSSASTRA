using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class BattlefieldEnemyRosterView : MonoBehaviour
{
    [SerializeField] private List<BattlefieldEnemyView> enemyViews =
        new List<BattlefieldEnemyView>();
    [SerializeField] private RectTransform layoutRoot;
    [SerializeField] private TMP_Text reinforcementWarning;
    [SerializeField] private float availableRowWidth = 1120f;
    [SerializeField] private float baseUnitWidth = 150f;
    [SerializeField] private float itemSpacing = 18f;
    [SerializeField] private float frontRowY = -118f;
    [SerializeField] private float middleRowY = 18f;
    [SerializeField] private float rearRowY = 142f;
    [SerializeField] private float rowStaggerX = 58f;
    [SerializeField] private float middleRowScale = 0.86f;
    [SerializeField] private float rearRowScale = 0.72f;

    private bool hasBound;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard)
    {
        projectionGuard = guard;
        foreach (var view in enemyViews) view?.SetProjectionGuard(guard);
    }
    internal void ApplyLeasedParticipant(BattleProjectedParticipant participant)
    {
        foreach (var view in enemyViews)
            if (view != null && view.BoundRuntimeId == participant.RuntimeId) view.ApplyLeasedParticipant(participant);
    }
    private bool formationOverlayVisible;
    private Func<BattleParticipantRuntime, bool> targetFilter;

    public event Action<BattleParticipantRuntime> TargetClicked;

    public IReadOnlyList<BattlefieldEnemyView> Views => enemyViews;
    public int ActiveEnemyCount { get; private set; }

    public void Bind(IReadOnlyList<DRPGBattleParticipantViewModel> enemies)
        => BindCanonical(enemies, null);

    internal void BindCanonical(IReadOnlyList<DRPGBattleParticipantViewModel> enemies,
        BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("EnemyRoster.Bind", 0, permit)) return;
        var ordered = new List<DRPGBattleParticipantViewModel>();
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null)
                    ordered.Add(enemies[i]);
            }
        }
        ordered.Sort(CompareModels);

        ActiveEnemyCount = 0;
        for (int i = 0; i < enemyViews.Count; i++)
        {
            DRPGBattleParticipantViewModel model =
                i < ordered.Count ? ordered[i] : null;
            enemyViews[i]?.BindCanonical(model, OnTargetClicked, permit);
            if (model != null)
                ActiveEnemyCount++;
        }
        ApplyDynamicLayout(hasBound);
        SetFormationOverlayVisible(formationOverlayVisible);
        hasBound = true;
    }

    public void SetTargetingEnabled(bool value)
    {
        SetTargetingEnabled(value, null);
    }

    public void SetTargetingEnabled(
        bool value,
        Func<BattleParticipantRuntime, bool> filter)
    {
        targetFilter = value ? filter : null;
        for (int i = 0; i < enemyViews.Count; i++)
        {
            BattlefieldEnemyView view = enemyViews[i];
            bool legal = value && view != null &&
                (targetFilter == null || TryPassFilter(view));
            view?.SetSelectable(legal);
        }
    }

    public void SetFormationOverlayVisible(bool visible)
    {
        formationOverlayVisible = visible;
        for (int i = 0; i < enemyViews.Count; i++)
            enemyViews[i]?.SetFormationOverlayVisible(visible);
    }

    public void SetWorldPresentationMode(bool enabled)
    {
        for (int i = 0; i < enemyViews.Count; i++)
            enemyViews[i]?.SetWorldPresentationMode(enabled);
    }

    public void SetReinforcementStatus(bool incoming, int reserveCount)
    {
        if (reinforcementWarning == null)
            return;
        bool visible = incoming && reserveCount > 0;
        reinforcementWarning.gameObject.SetActive(visible);
        reinforcementWarning.text = visible
            ? $"其余怪物即将来袭  ×{reserveCount}"
            : string.Empty;
    }

    public void SetTargetFeedback(
        int runtimeId,
        DRPGBattleTargetFeedback feedback)
    {
        for (int i = 0; i < enemyViews.Count; i++)
        {
            BattlefieldEnemyView view = enemyViews[i];
            if (view != null && view.BoundRuntimeId == runtimeId)
                view.SetTargetFeedback(feedback);
        }
    }

    public void ClearTargetFeedback()
    {
        for (int i = 0; i < enemyViews.Count; i++)
            enemyViews[i]?.SetTargetFeedback(DRPGBattleTargetFeedback.None);
    }

    public bool TryResolveImpactAnchor(
        int runtimeId,
        out BattleImpactAnchor anchor)
    {
        for (int i = 0; i < enemyViews.Count; i++)
        {
            BattlefieldEnemyView view = enemyViews[i];
            if (view != null && view.TryResolveImpactAnchor(runtimeId, out anchor))
                return true;
        }

        anchor = null;
        return false;
    }

    private void OnTargetClicked(BattleParticipantRuntime participant)
    {
        TargetClicked?.Invoke(participant);
    }

    private void ApplyDynamicLayout(bool animated)
    {
        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            DRPGFormationRow row = (DRPGFormationRow)rowIndex;
            var rowViews = new List<BattlefieldEnemyView>();
            var items = new List<DRPGFormationLayoutItem>();
            for (int i = 0; i < enemyViews.Count; i++)
            {
                BattlefieldEnemyView view = enemyViews[i];
                if (view == null || !view.gameObject.activeSelf ||
                    view.BoundFormationRow != row)
                {
                    continue;
                }
                rowViews.Add(view);
                items.Add(new DRPGFormationLayoutItem(
                    view.BoundRuntimeId,
                    view.BoundVisualWidth));
            }

            IReadOnlyList<DRPGFormationLayoutPlacement> placements =
                DRPGFormationLayoutCalculator.Calculate(
                    items,
                    availableRowWidth,
                    baseUnitWidth,
                    itemSpacing);
            float y = row == DRPGFormationRow.Front
                ? frontRowY
                : row == DRPGFormationRow.Middle ? middleRowY : rearRowY;
            float scale = row == DRPGFormationRow.Front
                ? 1f
                : row == DRPGFormationRow.Middle
                    ? middleRowScale
                    : rearRowScale;
            float stagger = row == DRPGFormationRow.Front
                ? rowStaggerX
                : row == DRPGFormationRow.Middle ? 0f : -rowStaggerX;
            for (int i = 0; i < rowViews.Count; i++)
            {
                rowViews[i].ApplyFormationLayout(
                    new Vector2(placements[i].CenterX + stagger, y),
                    placements[i].Width,
                    scale,
                    animated);
            }
        }

        Transform parent = layoutRoot != null ? layoutRoot : transform;
        for (int rowIndex = 2; rowIndex >= 0; rowIndex--)
        {
            for (int i = 0; i < enemyViews.Count; i++)
            {
                BattlefieldEnemyView view = enemyViews[i];
                if (view != null && view.gameObject.activeSelf &&
                    (int)view.BoundFormationRow == rowIndex &&
                    view.transform.parent == parent)
                {
                    view.transform.SetAsLastSibling();
                }
            }
        }
    }

    private bool TryPassFilter(BattlefieldEnemyView view)
    {
        BattleParticipantRuntime participant = null;
        for (int i = 0; i < enemyViews.Count; i++)
        {
            if (ReferenceEquals(enemyViews[i], view))
            {
                participant = view.BoundParticipant;
                break;
            }
        }
        return participant != null && targetFilter(participant);
    }

    private static int CompareModels(
        DRPGBattleParticipantViewModel left,
        DRPGBattleParticipantViewModel right)
    {
        int result = left.FormationRow.CompareTo(right.FormationRow);
        return result != 0
            ? result
            : left.FormationSlot.CompareTo(right.FormationSlot);
    }
}
