using System;
using System.Collections.Generic;
using UnityEngine;

// Uses the bound battlefield and its announcement surface, never a parallel UI.
internal sealed class BattleWavePresentationFactory : IBattleWavePresentationFactory
{
    private readonly DRPGBattleUGUISurface surface;
    private readonly BattleActionAnnouncementPresenter announcement;
    private readonly IDRPGBattlePresentationClock clock;
    internal BattleWavePresentationFactory(DRPGBattleUGUISurface surface,
        BattleActionAnnouncementPresenter announcement, IDRPGBattlePresentationClock clock)
    { this.surface = surface; this.announcement = announcement; this.clock = clock; }

    public bool TryOpen(BattleTransactionIdentity owner, IReadOnlyList<BattleParticipantRuntime> members,
        int perRowLimit, out IBattleWavePresentationScope scope, out string error)
    {
        scope = null; error = string.Empty;
        if (surface == null || surface.WorldRosterPresenter == null || surface.WorldRosterPresenter.ActorPool == null ||
            !owner.IsValid || owner.Kind != BattleTransactionKind.WaveTransition)
        { error = "Wave requires the bound world actor pool and its exact owner."; return false; }
        var acquired = surface.ProjectionArbiter.TryAcquire(owner);
        if (!acquired.Accepted) { error = acquired.Reason; return false; }
        var owned = new Scope(acquired.Lease, clock);
        scope = owned; // Retain ownership even if construction/cleanup throws.
        try
        {
            var homes = acquired.Lease.FrozenFormation.PlanWave(members, perRowLimit, acquired.Lease);
            var byId = new Dictionary<int, DRPGWorldFormationPlacement>();
            foreach (var home in homes) byId.Add(home.RuntimeId, home);
            var entries = new List<BattleWaveSpriteEntry>();
            foreach (var member in members)
            {
                var home = byId[member.RuntimeId];
                entries.Add(new BattleWaveSpriteEntry(new DRPGBattleParticipantViewModel(member, 0, false),
                    home.WorldPosition, Vector3.one * home.UniformScale, home.SortingOrder));
            }
            owned.Attach(new BattleWaveSpritePresentationPort(acquired.Lease, surface.WorldRosterPresenter.ActorPool,
                announcement, clock, entries, .45f, 3f));
            return true;
        }
        catch (Exception failure) { error = failure.ToString(); return false; }
    }

    private sealed class Scope : IBattleWavePresentationScope
    {
        private readonly DRPGBattleActionProjectionLease projection;
        private BattleWaveSpritePresentationPort port;
        private bool disposed;
        internal Scope(DRPGBattleActionProjectionLease projection, IDRPGBattlePresentationClock clock)
        { this.projection = projection; Clock = clock; }
        internal void Attach(BattleWaveSpritePresentationPort value) { port = value; }
        public IWaveTransitionPresentationPort Port => port;
        public IDRPGBattlePresentationClock Clock { get; }
        public bool IsActive => !disposed && projection.IsActive;
        public bool TryAdoptDeployed() => IsActive && port != null && port.TryAdoptDeployed();
        public void Dispose()
        {
            if (disposed) return;
            port?.Dispose();
            projection.Dispose();
            disposed = true;
        }
    }
}
