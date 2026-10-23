using System;
using System.Collections.Generic;
using UnityEngine;

// Visible actors are not active pool members until the entire rule batch has
// committed. The Wave runner adopts them before closing its projection lease.
internal sealed class BattleWaveActorStaging : IDisposable
{
    private readonly BattleWorldActorPool pool;
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly Dictionary<int, BattleWorldActorView> staged = new Dictionary<int, BattleWorldActorView>();
    private readonly Dictionary<int, ColliderState> colliders = new Dictionary<int, ColliderState>();
    private bool disposed, adopted, busy;

    private sealed class ColliderState
    {
        private readonly Collider2D[] targets;
        private readonly bool[] enabled;
        internal ColliderState(BattleWorldActorView view)
        {
            targets = view.GetComponentsInChildren<Collider2D>(true); enabled = new bool[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            { enabled[i] = targets[i].enabled; targets[i].enabled = false; }
        }
        internal void Restore()
        {
            for (int i = 0; i < targets.Length; i++) if (targets[i] != null) targets[i].enabled = enabled[i];
        }
    }

    internal BattleWaveActorStaging(BattleWorldActorPool pool, DRPGBattleActionProjectionLease lease)
    {
        this.pool = pool != null ? pool : throw new ArgumentNullException(nameof(pool));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        pool.RegisterWaveStaging(this, lease);
        if (!lease.TryOwnPresentation(this))
        { pool.EndWaveStaging(this); throw new InvalidOperationException("Wave projection ownership lost."); }
    }

    internal BattleWorldActorView Stage(DRPGBattleParticipantViewModel actor)
    {
        if (disposed || adopted || busy || !lease.OwnsPresentation(this))
            throw new InvalidOperationException("Wave staging is no longer writable.");
        if (actor == null || !actor.IsEnemy || !actor.Participant.IsAlive || actor.Participant.IsDeployed ||
            pool.TryGet(actor.RuntimeId, out _))
            throw new InvalidOperationException("Only living reserve enemies can be staged.");
        if (staged.TryGetValue(actor.RuntimeId, out var existing))
        {
            if (existing == null || !ReferenceEquals(existing.BoundActor.Participant, actor.Participant))
                throw new InvalidOperationException("Staged actor identity changed.");
            return existing;
        }
        busy = true;
        try
        {
            var view = pool.RentForWave(this);
            staged.Add(actor.RuntimeId, view);
            colliders.Add(actor.RuntimeId, new ColliderState(view));
            view.Bind(actor.RuntimeId, actor);
            pool.GuardWaveView(view);
            if (disposed || !lease.OwnsPresentation(this))
                throw new InvalidOperationException("Wave staging cancelled during actor binding.");
            return view;
        }
        catch { Dispose(); throw; }
        finally { busy = false; if (disposed) Dispose(); }
    }

    internal bool TryAdoptDeployed()
    {
        if (disposed || busy || !lease.OwnsPresentation(this)) return false;
        if (adopted) return true;
        if (staged.Count == 0) return false;
        foreach (var pair in staged)
        {
            var view = pair.Value;
            if (view == null || !view.gameObject.activeSelf || view.RuntimeId != pair.Key ||
                view.BoundActor == null || !view.BoundActor.Participant.IsAlive ||
                !view.BoundActor.Participant.IsDeployed || !pool.CanAdoptWave(this, pair.Key)) return false;
        }
        // Membership changes have no Unity callbacks. Validate the whole set
        // first so a missing member cannot partially expose the wave.
        foreach (var view in staged.Values) pool.AdoptWave(this, view);
        staged.Clear(); adopted = true;
        foreach (var state in colliders.Values) state.Restore();
        colliders.Clear();
        return true;
    }

    public void Dispose()
    {
        disposed = true;
        // A visual driver's Bind can synchronously cancel its owner. Returning
        // the view before that call unwinds would let Bind reactivate the queue.
        if (busy) return;
        List<Exception> faults = null;
        foreach (int id in new List<int>(staged.Keys))
        {
            try
            {
                pool.ReturnWave(this, staged[id]); staged.Remove(id);
                if (colliders.TryGetValue(id, out var state)) state.Restore();
                colliders.Remove(id);
            }
            catch (Exception error) { if (faults == null) faults = new List<Exception>(); faults.Add(error); }
        }
        if (staged.Count == 0 && pool != null) pool.EndWaveStaging(this);
        if (faults != null) throw new AggregateException("Wave staging cleanup failed.", faults);
    }
}
