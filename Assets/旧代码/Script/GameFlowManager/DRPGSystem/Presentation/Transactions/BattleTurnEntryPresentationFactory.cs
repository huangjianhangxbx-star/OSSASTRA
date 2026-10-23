using System;
using UnityEngine;

// Creates one candidate scope from the already-bound battlefield. It never
// generates replacement actors, UI templates, or fallback combat assets.
internal sealed class BattleTurnEntryPresentationFactory : IBattleTurnEntryPresentationFactory
{
    private readonly IDRPGBattleProjectionLeaseArbiter projections;
    private readonly BattleImpactFeedbackPresenter feedback;
    private readonly Camera camera;
    private readonly BattleDefeatedActorRoster defeated;
    private readonly IDRPGBattlePresentationClock clock;

    internal BattleTurnEntryPresentationFactory(IDRPGBattleProjectionLeaseArbiter projections,
        BattleImpactFeedbackPresenter feedback, Camera camera, BattleDefeatedActorRoster defeated,
        IDRPGBattlePresentationClock clock)
    {
        this.projections = projections ?? throw new ArgumentNullException(nameof(projections));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.feedback = feedback;
        this.camera = camera;
        this.defeated = defeated;
    }

    public bool TryOpen(BattleTransactionIdentity owner, out IBattleTurnEntryPresentationScope scope, out string error)
    {
        scope = null;
        error = string.Empty;
        if (!owner.IsValid || owner.Kind != BattleTransactionKind.TurnEntry)
        { error = "A valid TurnEntry owner is required for candidate presentation."; return false; }
        var acquired = projections.TryAcquire(owner);
        if (!acquired.Accepted) { error = acquired.Reason; return false; }
        try
        {
            var adapter = new BattleTurnEntryPresentationAdapter(acquired.Lease, feedback, camera, defeated, clock);
            scope = new Scope(acquired.Lease, adapter);
            return true;
        }
        catch (Exception failure)
        {
            acquired.Lease.Dispose();
            error = failure.Message;
            return false;
        }
    }

    private sealed class Scope : IBattleTurnEntryPresentationScope
    {
        private readonly DRPGBattleActionProjectionLease projection;
        private readonly BattleTurnEntryPresentationAdapter adapter;
        private bool disposed;
        internal Scope(DRPGBattleActionProjectionLease projection, BattleTurnEntryPresentationAdapter adapter)
        { this.projection = projection; this.adapter = adapter; }
        public IBattleTurnEntryPresentationPort Port => adapter;
        public bool IsActive => !disposed && projection.IsActive;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { adapter.Dispose(); }
            finally { projection.Dispose(); }
        }
    }
}
