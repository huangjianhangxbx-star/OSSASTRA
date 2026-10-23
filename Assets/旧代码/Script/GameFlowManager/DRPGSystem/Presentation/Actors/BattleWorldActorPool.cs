using System.Collections.Generic;
using UnityEngine;

public sealed class BattleWorldActorPool : MonoBehaviour
{
    [SerializeField] private BattleWorldActorView viewPrefab;
    [SerializeField] private Transform poolRoot;

    private readonly Dictionary<int, BattleWorldActorView> active =
        new Dictionary<int, BattleWorldActorView>();
    private readonly Queue<BattleWorldActorView> available =
        new Queue<BattleWorldActorView>();
    private readonly Dictionary<int, BattleBorrowedActorLease> borrowed =
        new Dictionary<int, BattleBorrowedActorLease>();
    private DRPGNodePartyRoot borrowedParty;

    public int ActiveCount => active.Count;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    private BattleWaveActorStaging waveStaging;

    internal void RegisterWaveStaging(BattleWaveActorStaging staging, DRPGBattleActionProjectionLease lease)
    {
        if (staging == null || lease == null || !lease.IsActive ||
            lease.Owner.Kind != BattleTransactionKind.WaveTransition ||
            projectionGuard == null || !ReferenceEquals(projectionGuard.Active, lease) || waveStaging != null)
            throw new System.InvalidOperationException("Staging requires this pool's exclusive Wave projection.");
        waveStaging = staging;
    }

    internal BattleWorldActorView RentForWave(BattleWaveActorStaging staging)
    {
        if (!ReferenceEquals(waveStaging, staging))
            throw new System.InvalidOperationException("Foreign Wave staging capability.");
        BattleWorldActorView view = null;
        while (available.Count > 0 && view == null) view = available.Dequeue();
        if (view == null) view = CreateView();
        view.transform.SetParent(ResolveRoot(), false);
        return view;
    }

    internal bool CanAdoptWave(BattleWaveActorStaging staging, int runtimeId) =>
        ReferenceEquals(waveStaging, staging) && !active.ContainsKey(runtimeId);

    internal void AdoptWave(BattleWaveActorStaging staging, BattleWorldActorView view)
    {
        if (!CanAdoptWave(staging, view.RuntimeId))
            throw new System.InvalidOperationException("Wave adoption conflicts with the active roster.");
        active.Add(view.RuntimeId, view);
    }

    internal void GuardWaveView(BattleWorldActorView view) => view.SetProjectionGuard(projectionGuard);

    internal void ReturnWave(BattleWaveActorStaging staging, BattleWorldActorView view)
    {
        if (!ReferenceEquals(waveStaging, staging))
            throw new System.InvalidOperationException("Foreign Wave cleanup capability.");
        if (view == null) return;
        // This view has never belonged to the canonical roster. No survivor's
        // guard is relaxed, and the global projection remains exclusively held.
        view.SetProjectionGuard(null);
        view.ResetForPool();
        view.transform.SetParent(ResolveRoot(), false);
        available.Enqueue(view);
    }

    internal void EndWaveStaging(BattleWaveActorStaging staging)
    {
        if (ReferenceEquals(waveStaging, staging)) waveStaging = null;
    }
    internal bool RejectForeignProjectionWrite(string operation) =>
        projectionGuard != null && projectionGuard.DeferCanonical(operation);
    internal int ReleaseCount { get; private set; }
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard)
    {
        projectionGuard = guard;
        foreach (var view in active.Values)
            if (view != null) view.SetProjectionGuard(guard);
    }

    public void Configure(BattleWorldActorView prefab, Transform root)
    {
        viewPrefab = prefab;
        poolRoot = root != null ? root : transform;
    }

    public void ConfigureBorrowedParty(DRPGNodePartyRoot party) =>
        borrowedParty = party;

    public BattleWorldActorView Acquire(int runtimeId)
        => AcquireCore(runtimeId, null);

    private BattleWorldActorView AcquireCore(int runtimeId, BattleCanonicalProjectionPermit permit)
    {
        if (runtimeId <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(runtimeId));
        if (active.TryGetValue(runtimeId, out BattleWorldActorView existing) && existing != null)
            return existing;
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Pool.AcquireNew", runtimeId, permit))
            throw new System.InvalidOperationException("Projection lease forbids new actors.");

        // A destroyed Unity wrapper is not a reusable pool entry. Only the
        // unleased/canonical path may replace it after ownership loss.
        active.Remove(runtimeId);

        BattleWorldActorView view = available.Count > 0
            ? available.Dequeue()
            : CreateView();
        view.transform.SetParent(ResolveRoot(), false);
        view.SetProjectionGuard(projectionGuard);
        view.BindCanonical(runtimeId, null, permit);
        active.Add(runtimeId, view);
        return view;
    }

    public BattleWorldActorView Acquire(DRPGBattleParticipantViewModel actor)
        => AcquireCanonical(actor, null);

    internal BattleWorldActorView AcquireCanonical(DRPGBattleParticipantViewModel actor,
        BattleCanonicalProjectionPermit permit)
    {
        if (actor == null)
            throw new System.ArgumentNullException(nameof(actor));
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Pool.Rebind", actor.RuntimeId, permit))
        {
            if (active.TryGetValue(actor.RuntimeId, out var pinned)) return pinned;
            throw new System.InvalidOperationException("Projection lease forbids actor replacement.");
        }
        BattleWorldActorView view;
        if (!active.TryGetValue(actor.RuntimeId, out view) &&
            !actor.IsEnemy && borrowedParty != null &&
            borrowedParty.TryGetMember(actor.Profile.CombatantId, out DRPGScenePartyMember member) &&
            member != null && member.View != null)
        {
            if (projectionGuard != null &&
                projectionGuard.DeferCanonical("Pool.AdoptBorrowed", actor.RuntimeId, permit))
            {
                throw new System.InvalidOperationException(
                    "Projection lease forbids borrowed actor adoption.");
            }
            view = member.View;
            var lease = new BattleBorrowedActorLease(view);
            view.SetProjectionGuard(projectionGuard);
            active.Add(actor.RuntimeId, view);
            borrowed.Add(actor.RuntimeId, lease);
        }
        else
        {
            view = AcquireCore(actor.RuntimeId, permit);
        }
        view.BindCanonical(actor.RuntimeId, actor, permit);
        return view;
    }

    public bool TryGet(int runtimeId, out BattleWorldActorView view) =>
        active.TryGetValue(runtimeId, out view);

    internal void CommitBorrowedFormationHome(DRPGBattleActionProjectionLease projection, BattleWorldActorView view)
    {
        if (projection == null || projectionGuard == null || !ReferenceEquals(projectionGuard.Active, projection) ||
            view == null || !projection.IsAtCurrentHome(view.RuntimeId))
            throw new System.InvalidOperationException("Only the active projection may commit a rendered borrowed home.");
        if (borrowed.TryGetValue(view.RuntimeId, out var lease) && ReferenceEquals(lease.View, view))
            lease.CommitFormationHome();
    }

    public void Release(int runtimeId)
        => ReleaseCanonical(runtimeId, null);

    internal void ReleaseCanonical(int runtimeId, BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Pool.Release", runtimeId, permit)) return;
        if (!active.TryGetValue(runtimeId, out BattleWorldActorView view))
            return;
        active.Remove(runtimeId);
        ReleaseCount++;
        if (view != null)
        {
            if (borrowed.Remove(runtimeId, out BattleBorrowedActorLease lease))
            {
                view.UnbindBorrowedCanonical(permit);
                view.SetProjectionGuard(null);
                lease.Dispose();
                return;
            }
            view.ResetForPoolCanonical(permit);
            view.SetProjectionGuard(null);
            view.transform.SetParent(ResolveRoot(), false);
            available.Enqueue(view);
        }
    }

    internal void ReleaseForDeparture(BattleWorldActorView view, BattleDepartureProjectionPermit permit)
    {
        if (projectionGuard?.Active == null || !projectionGuard.Active.OwnsDeparturePermit(permit, view) ||
            !active.TryGetValue(permit.RuntimeId, out var current) || !ReferenceEquals(current, view))
            throw new System.InvalidOperationException("Pool departure requires the exact owned actor.");
        active.Remove(permit.RuntimeId); ReleaseCount++;
        if (borrowed.Remove(permit.RuntimeId, out BattleBorrowedActorLease lease))
        {
            view.UnbindBorrowedForDeparture(permit);
            view.SetProjectionGuard(null);
            lease.Dispose();
            // Normal lease disposal restores exploration visibility. A defeated
            // actor has instead transferred that visibility to the incap roster.
            view.gameObject.SetActive(false);
            return;
        }
        view.ResetForDeparture(permit);
        view.SetProjectionGuard(null);
        view.transform.SetParent(ResolveRoot(), false);
        available.Enqueue(view);
    }

    public void ReleaseAll()
    {
        int[] runtimeIds = new int[active.Count];
        active.Keys.CopyTo(runtimeIds, 0);
        for (int i = 0; i < runtimeIds.Length; i++)
            Release(runtimeIds[i]);
    }

    private BattleWorldActorView CreateView()
    {
        BattleWorldActorView view;
        if (viewPrefab != null)
        {
            view = Instantiate(viewPrefab, ResolveRoot());
        }
        else
        {
            var instance = new GameObject("BattleWorldActorView");
            instance.transform.SetParent(ResolveRoot(), false);
            view = instance.AddComponent<BattleWorldActorView>();
        }

        view.name = "BattleWorldActorView_Pooled";
        view.EnsureAnchors();
        return view;
    }

    private Transform ResolveRoot() => poolRoot != null ? poolRoot : transform;
}
