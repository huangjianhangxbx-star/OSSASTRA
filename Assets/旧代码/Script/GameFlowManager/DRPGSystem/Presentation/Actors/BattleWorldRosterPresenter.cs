using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleWorldRosterPresenter : MonoBehaviour
{
    [SerializeField] private BattleWorldActorPool actorPool;
    internal BattleWorldActorPool ActorPool => actorPool;
    [SerializeField] private Transform playerFrontOrigin;
    [SerializeField] private Transform enemyFrontOrigin;
    [SerializeField, Min(1f)] private float availableWidth = 8f;
    [SerializeField, Min(0f)] private float actorSpacing = 0.75f;
    [SerializeField, Min(0f)] private float rowDepthStep = 1.8f;
    [SerializeField, Min(0f)] private float rowHorizontalStep = 0.42f;
    [SerializeField, Range(0.1f, 1f)] private float middleRowScale = 0.94f;
    [SerializeField, Range(0.1f, 1f)] private float rearRowScale = 0.88f;
    [SerializeField, Min(0f)] private float transitionSeconds = 0.2f;

    private readonly HashSet<int> boundIds = new HashSet<int>();
    private readonly List<BattleWorldActorView> activeViews =
        new List<BattleWorldActorView>();
    private readonly Dictionary<int, FormationAssignment> formationAssignments =
        new Dictionary<int, FormationAssignment>();
    private bool inPlaceSorting;
    private Transform nodePlayerOrigin;
    private Transform nodeEnemyOrigin;
    private Transform PlayerOrigin => nodePlayerOrigin != null ? nodePlayerOrigin : playerFrontOrigin;
    private Transform EnemyOrigin => nodeEnemyOrigin != null ? nodeEnemyOrigin : enemyFrontOrigin;
    internal bool PreservesNodeHomes => inPlaceSorting;

    public IReadOnlyList<BattleWorldActorView> ActiveViews => activeViews;
    internal BattleFrozenFormationLayout CaptureFrozenLayout() => new BattleFrozenFormationLayout(activeViews,
        PlayerOrigin != null ? PlayerOrigin.position : transform.position,
        EnemyOrigin != null ? EnemyOrigin.position : transform.position,
        availableWidth, actorSpacing, inPlaceSorting ? 0f : rowDepthStep, rowHorizontalStep,
        inPlaceSorting ? 1f : middleRowScale, inPlaceSorting ? 1f : rearRowScale, inPlaceSorting);
    internal bool HasConsistentPool
    {
        get
        {
            if (actorPool == null || actorPool.ActiveCount != activeViews.Count) return false;
            foreach (var view in activeViews)
                if (view == null || !actorPool.TryGet(view.RuntimeId, out var pooled) ||
                    !ReferenceEquals(view, pooled)) return false;
            return true;
        }
    }
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard)
    {
        projectionGuard = guard;
        actorPool?.SetProjectionGuard(guard);
    }

    public void Configure(
        BattleWorldActorPool pool,
        Transform playerOrigin,
        Transform enemyOrigin)
    {
        actorPool = pool;
        playerFrontOrigin = playerOrigin;
        enemyFrontOrigin = enemyOrigin;
    }

    public void SetInPlaceSorting(bool enabled)
    {
        inPlaceSorting = enabled;
        if (!enabled) { nodePlayerOrigin = null; nodeEnemyOrigin = null; }
    }

    public void ConfigureNodeOrigins(Transform partyOrigin, Transform enemyOrigin)
    {
        nodePlayerOrigin = partyOrigin;
        nodeEnemyOrigin = enemyOrigin;
    }

    public void Bind(
        IReadOnlyList<DRPGBattleParticipantViewModel> party,
        IReadOnlyList<DRPGBattleParticipantViewModel> enemies,
        bool immediate = false)
        => BindCanonical(party, enemies, immediate, null);

    internal void BindCanonical(
        IReadOnlyList<DRPGBattleParticipantViewModel> party,
        IReadOnlyList<DRPGBattleParticipantViewModel> enemies,
        bool immediate, BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Roster.Bind", 0, permit)) return;
        if (actorPool == null)
            return;

        var nextIds = new HashSet<int>();
        BindSide(party, PlayerOrigin, nextIds, immediate, true, permit);
        BindSide(enemies, EnemyOrigin, nextIds, immediate, false, permit);

        int[] oldIds = new int[boundIds.Count];
        boundIds.CopyTo(oldIds);
        for (int i = 0; i < oldIds.Length; i++)
        {
            if (!nextIds.Contains(oldIds[i]))
            {
                formationAssignments.Remove(oldIds[i]);
                actorPool.ReleaseCanonical(oldIds[i], permit);
            }
        }

        boundIds.Clear();
        foreach (int runtimeId in nextIds)
            boundIds.Add(runtimeId);
        RebuildActiveViewList();
    }

    internal void DepartView(BattleWorldActorView view, BattleDepartureProjectionPermit permit)
    {
        if (projectionGuard?.Active == null || !projectionGuard.Active.OwnsDeparturePermit(permit, view))
            throw new InvalidOperationException("Roster departure requires the exact owned view capability.");
        boundIds.Remove(permit.RuntimeId);
        formationAssignments.Remove(permit.RuntimeId);
        activeViews.Remove(view);
        actorPool.ReleaseForDeparture(view, permit);
    }

    public void Clear()
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Roster.Clear")) return;
        if (actorPool != null)
        {
            foreach (int runtimeId in boundIds)
                actorPool.Release(runtimeId);
        }
        boundIds.Clear();
        formationAssignments.Clear();
        activeViews.Clear();
    }

    private void BindSide(
        IReadOnlyList<DRPGBattleParticipantViewModel> actors,
        Transform origin,
        ISet<int> nextIds,
        bool immediate,
        bool mirror, BattleCanonicalProjectionPermit permit)
    {
        if (actors == null || actors.Count == 0)
            return;

        var items = new List<DRPGWorldFormationItem>(actors.Count);
        for (int i = 0; i < actors.Count; i++)
        {
            DRPGBattleParticipantViewModel actor = actors[i];
            if (actor == null || !actor.IsAlive)
                continue;
            items.Add(new DRPGWorldFormationItem(
                actor.RuntimeId,
                actor.FormationRow,
                actor.FormationSlot,
                actor.FormationVisualWidth));
        }

        Vector3 originPosition = origin != null
            ? origin.position
            : transform.position;
        IReadOnlyList<DRPGWorldFormationPlacement> placements =
            DRPGWorldFormationLayoutCalculator.Calculate(
                items,
                originPosition,
                availableWidth,
                1f,
                actorSpacing,
                inPlaceSorting ? 0f : rowDepthStep,
                rowHorizontalStep,
                inPlaceSorting ? 1f : middleRowScale,
                inPlaceSorting ? 1f : rearRowScale);

        var actorById = new Dictionary<int, DRPGBattleParticipantViewModel>();
        for (int i = 0; i < actors.Count; i++)
        {
            if (actors[i] != null)
                actorById[actors[i].RuntimeId] = actors[i];
        }

        for (int i = 0; i < placements.Count; i++)
        {
            DRPGWorldFormationPlacement placement = placements[i];
            if (!actorById.TryGetValue(
                    placement.RuntimeId,
                    out DRPGBattleParticipantViewModel actor))
            {
                continue;
            }
            BattleWorldActorView view = actorPool.AcquireCanonical(actor, permit);
            nextIds.Add(actor.RuntimeId);
            Vector3 position = placement.WorldPosition;
            if (mirror)
                position.x = originPosition.x - (position.x - originPosition.x);
            float duration = immediate ? 0f : transitionSeconds;
            Vector3 targetScale = Vector3.one * placement.UniformScale;
            if (mirror && inPlaceSorting)
            {
                // Node-owned allies already occupy their authoritative homes.
                // Binding a battle must not apply the former stage layout.
                formationAssignments[actor.RuntimeId] =
                    new FormationAssignment(view.transform.position, view.transform.localScale);
                ApplySorting(view, placement.SortingOrder, true);
                continue;
            }
            bool sameAssignment = formationAssignments.TryGetValue(
                    actor.RuntimeId,
                    out FormationAssignment assignment) &&
                assignment.Matches(position, targetScale);
            bool atAssignedPose =
                (view.transform.position - position).sqrMagnitude <= .000001f &&
                (view.transform.localScale - targetScale).sqrMagnitude <= .000001f;
            // A canonical refresh may arrive while a transaction-owned recoil
            // or return tween is deliberately displacing this same actor. Keep
            // that motion intact when its logical slot did not change. Outside
            // live motion, repair any drift back to the assigned formation.
            bool alreadyPlaced = sameAssignment &&
                (atAssignedPose ||
                 (!immediate && DOTween.IsTweening(view.transform)));
            formationAssignments[actor.RuntimeId] =
                new FormationAssignment(position, targetScale);
            if (!alreadyPlaced)
            {
                view.transform.DOKill(false);
                if (duration <= 0f)
                {
                    view.transform.position = position;
                    view.transform.localScale = targetScale;
                }
                else
                {
                    view.transform.DOMove(position, duration).SetEase(Ease.OutCubic);
                    view.transform.DOScale(
                        placement.UniformScale,
                        duration).SetEase(Ease.OutBack);
                }
            }
            ApplySorting(view, placement.SortingOrder, inPlaceSorting);
        }
    }

    private void RebuildActiveViewList()
    {
        activeViews.Clear();
        foreach (int runtimeId in boundIds)
        {
            if (actorPool.TryGet(runtimeId, out BattleWorldActorView view))
                activeViews.Add(view);
        }
    }

    private static void ApplySorting(
        BattleWorldActorView view,
        int order,
        bool useSceneCharacterLayer)
    {
        SpriteRenderer[] renderers =
            view.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (useSceneCharacterLayer)
                renderers[i].sortingLayerName = "Character1";
            renderers[i].sortingOrder = order + i;
        }
    }

    private readonly struct FormationAssignment
    {
        internal FormationAssignment(Vector3 position, Vector3 scale)
        {
            Position = position;
            Scale = scale;
        }

        internal Vector3 Position { get; }
        internal Vector3 Scale { get; }

        internal bool Matches(Vector3 position, Vector3 scale)
        {
            return (Position - position).sqrMagnitude <= .000001f &&
                (Scale - scale).sqrMagnitude <= .000001f;
        }
    }

    private void OnDisable() => Clear();
}
