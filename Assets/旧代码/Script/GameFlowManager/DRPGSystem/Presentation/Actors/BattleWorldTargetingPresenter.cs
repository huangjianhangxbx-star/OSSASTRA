using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleWorldTargetingPresenter : MonoBehaviour
{
    [SerializeField] private Camera battleCamera;
    [SerializeField] private BattleWorldActorPool actorPool;
    [SerializeField] private RectTransform proxyRoot;

    private readonly Dictionary<int, BattleActorScreenHitProxy> proxies =
        new Dictionary<int, BattleActorScreenHitProxy>();
    private readonly Dictionary<int, BattleWorldActorStatusView> statusViews =
        new Dictionary<int, BattleWorldActorStatusView>();
    private readonly HashSet<int> enemyIds = new HashSet<int>();
    private Func<int, bool> targetFilter;
    private bool targetingEnabled;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard)
    {
        projectionGuard = guard;
        foreach (var view in statusViews.Values)
            if (view != null) view.SetProjectionGuard(guard);
    }

    public event Action<int> TargetClicked;
    public event Action<int, bool> HoverChanged;

    internal IReadOnlyList<CanvasGroup> CaptureFloatingGroups()
    {
        var result = new List<CanvasGroup>();
        if (proxyRoot == null) return result;
        foreach (var projector in proxyRoot.GetComponentsInChildren<BattleActorScreenProjector>(true))
        {
            CanvasGroup group = projector.GetComponent<CanvasGroup>();
            if (group != null && !result.Contains(group)) result.Add(group);
        }
        return result;
    }

    internal void ApplyLeasedParticipant(BattleProjectedParticipant participant)
    {
        if (statusViews.TryGetValue(participant.RuntimeId, out var view) && view != null)
            view.ApplyLeasedParticipant(participant);
    }

    public void Configure(
        Camera camera,
        BattleWorldActorPool pool,
        RectTransform root)
    {
        battleCamera = camera;
        actorPool = pool;
        proxyRoot = root;
    }

    public void ConfigureWorld(Camera camera, BattleWorldActorPool pool)
    {
        Clear();
        battleCamera = camera;
        actorPool = pool;
    }

    public void Bind(IReadOnlyList<DRPGBattleParticipantViewModel> enemies)
        => BindCanonical(enemies, null);

    internal void BindCanonical(IReadOnlyList<DRPGBattleParticipantViewModel> enemies,
        BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("WorldTargeting.Bind", 0, permit)) return;
        enemyIds.Clear();
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                DRPGBattleParticipantViewModel enemy = enemies[i];
                if (enemy != null && enemy.IsAlive)
                    enemyIds.Add(enemy.RuntimeId);
            }
        }

        int[] existing = new int[proxies.Count];
        proxies.Keys.CopyTo(existing, 0);
        for (int i = 0; i < existing.Length; i++)
        {
            if (!enemyIds.Contains(existing[i]))
                RemoveProxy(existing[i]);
        }

        foreach (int runtimeId in enemyIds)
        {
            if (actorPool != null &&
                actorPool.TryGet(runtimeId, out BattleWorldActorView actor))
            {
                EnsureProxy(actor, FindModel(enemies, runtimeId), permit);
            }
        }
        RefreshInteractivity();
    }

    public void SetTargetingEnabled(
        bool enabled,
        Func<int, bool> filter = null)
    {
        targetingEnabled = enabled;
        targetFilter = enabled ? filter : null;
        RefreshInteractivity();
    }

    public void Clear()
    {
        if (projectionGuard != null && projectionGuard.DeferCanonical("WorldTargeting.Clear")) return;
        int[] ids = new int[proxies.Count];
        proxies.Keys.CopyTo(ids, 0);
        for (int i = 0; i < ids.Length; i++)
            RemoveProxy(ids[i]);
        enemyIds.Clear();
        targetFilter = null;
        targetingEnabled = false;
    }

    public void SetTargetFeedback(
        int runtimeId,
        DRPGBattleTargetFeedback feedback)
    {
        if (statusViews.TryGetValue(runtimeId, out BattleWorldActorStatusView view))
            view?.SetFeedback(feedback);
    }

    public void ClearTargetFeedback()
    {
        foreach (BattleWorldActorStatusView view in statusViews.Values)
            view?.SetFeedback(DRPGBattleTargetFeedback.None);
    }

    public bool TryResolveImpactAnchor(
        int runtimeId,
        out BattleImpactAnchor anchor)
    {
        if (proxies.TryGetValue(runtimeId, out BattleActorScreenHitProxy proxy) &&
            proxy != null)
        {
            statusViews.TryGetValue(runtimeId, out BattleWorldActorStatusView status);
            anchor = new BattleImpactAnchor(
                runtimeId,
                BattleImpactAnchorKind.EnemyField,
                proxy.transform as RectTransform,
                status != null ? status.ImpactGraphic : proxy,
                null);
            return true;
        }
        anchor = null;
        return false;
    }

    private void EnsureProxy(
        BattleWorldActorView actor,
        DRPGBattleParticipantViewModel model, BattleCanonicalProjectionPermit permit)
    {
        if (actor == null || actor.RuntimeId <= 0 || proxyRoot == null ||
            battleCamera == null)
        {
            return;
        }
        if (proxies.ContainsKey(actor.RuntimeId))
        {
            if (statusViews.TryGetValue(
                    actor.RuntimeId,
                    out BattleWorldActorStatusView existingStatus))
            {
                existingStatus?.BindCanonical(model, permit);
            }
            return;
        }

        Renderer renderer = actor.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
            return;

        var proxyObject = new GameObject(
            "WorldTarget_" + actor.RuntimeId,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(BattleActorScreenHitProxy),
            typeof(BattleActorScreenProjector));
        RectTransform rect = proxyObject.GetComponent<RectTransform>();
        rect.SetParent(proxyRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        BattleActorScreenHitProxy proxy =
            proxyObject.GetComponent<BattleActorScreenHitProxy>();
        proxy.Bind(actor.RuntimeId, ResolveSorting(renderer), 0f);
        proxy.HoverChanged += HandleHoverChanged;
        proxy.Clicked += HandleClicked;
        proxyObject.GetComponent<BattleActorScreenProjector>().Configure(
            battleCamera,
            renderer,
            rect);
        BattleWorldActorStatusView status =
            BattleWorldActorStatusView.Create(rect);
        if (status != null)
        {
            status.SetProjectionGuard(projectionGuard);
            status.BindCanonical(model, permit);
        }
        proxies.Add(actor.RuntimeId, proxy);
        statusViews.Add(actor.RuntimeId, status);
    }

    private void RefreshInteractivity()
    {
        foreach (KeyValuePair<int, BattleActorScreenHitProxy> pair in proxies)
        {
            bool legal = targetingEnabled &&
                (targetFilter == null || targetFilter(pair.Key));
            pair.Value.SetInteractable(legal);
        }
    }

    private void HandleHoverChanged(int runtimeId, bool hovered)
    {
        SetTargetFeedback(
            runtimeId,
            hovered
                ? DRPGBattleTargetFeedback.Hovered
                : DRPGBattleTargetFeedback.None);
        HoverChanged?.Invoke(runtimeId, hovered);
    }

    private void HandleClicked(int runtimeId) => TargetClicked?.Invoke(runtimeId);

    private void RemoveProxy(int runtimeId)
    {
        if (!proxies.TryGetValue(runtimeId, out BattleActorScreenHitProxy proxy))
            return;
        proxies.Remove(runtimeId);
        statusViews.Remove(runtimeId);
        if (proxy != null)
        {
            proxy.HoverChanged -= HandleHoverChanged;
            proxy.Clicked -= HandleClicked;
            if (Application.isPlaying)
                Destroy(proxy.gameObject);
            else
                DestroyImmediate(proxy.gameObject);
        }
    }

    private static int ResolveSorting(Renderer renderer)
    {
        return renderer is SpriteRenderer spriteRenderer
            ? spriteRenderer.sortingOrder
            : 0;
    }

    private static DRPGBattleParticipantViewModel FindModel(
        IReadOnlyList<DRPGBattleParticipantViewModel> models,
        int runtimeId)
    {
        if (models == null)
            return null;
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null && models[i].RuntimeId == runtimeId)
                return models[i];
        }
        return null;
    }

    private void OnDisable() => Clear();
}
