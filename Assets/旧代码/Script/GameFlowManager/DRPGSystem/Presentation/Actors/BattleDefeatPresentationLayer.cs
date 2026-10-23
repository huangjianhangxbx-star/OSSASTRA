using System;
using System.Collections.Generic;
using UnityEngine;

// Pending defeat is an action-owned projection, not a second rule death.
// It retains the original actor and pose; a later explicit transfer owns the
// incapacitated layer or enemy departure after the rendered-home barrier.
internal sealed class BattleDefeatPresentationLayer : IDisposable
{
    private sealed class Pending
    {
        internal BattleWorldActorView View;
        internal int RuntimeId, ViewId;
        internal UnityEngine.UI.Graphic Marker;
        internal SpriteRenderer[] Renderers;
        internal Color[] Colors;
    }
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly BattleImpactFeedbackPresenter feedback;
    private readonly Camera camera;
    private readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
    private bool disposed;
    private DRPGBattleDeparturePresentation departure;
    private long transferredFrame;
    private BattleDefeatedActorRoster transferredRoster;
    private bool retiredSpriteBodies;

    internal BattleDefeatPresentationLayer(DRPGBattleActionProjectionLease lease,
        BattleImpactFeedbackPresenter feedback, Camera camera)
    {
        this.lease = lease; this.feedback = feedback; this.camera = camera;
        if (lease == null || !lease.TryOwnPresentation(this))
            throw new InvalidOperationException("Pending defeat requires a live projection owner.");
    }

    internal bool Reveal(ParticipantId participant)
    {
        if (disposed || !lease.IsActive || !lease.TryGetView(participant.Value, out var view) ||
            !lease.GetPresented(participant.Value).DefeatPending) return false;
        if (pending.ContainsKey(participant.Value)) return true;
        var renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
        // An unbound renderer/marker is reported, never silently accepted as a
        // rendered defeat. Spine tint binding has a separate authored adapter.
        if (feedback == null || renderers.Length == 0) return false;
        var entry = new Pending { View = view, RuntimeId = participant.Value, ViewId = view.GetInstanceID(),
            Renderers = renderers, Colors = new Color[renderers.Length] };
        entry.Marker = feedback.CreateDefeatMarker(MarkerPosition(entry), camera);
        if (entry.Marker == null) return false;
        for (int i = 0; i < renderers.Length; i++)
        {
            entry.Colors[i] = renderers[i].color;
            var color = entry.Colors[i];
            renderers[i].color = new Color(color.r * .4f, color.g * .4f, color.b * .4f, color.a);
        }
        pending.Add(participant.Value, entry);
        return true;
    }

    internal void Tick()
    {
        if (disposed || !lease.IsActive) return;
        foreach (var entry in pending.Values)
            if (entry.View != null && entry.View.RuntimeId == entry.RuntimeId && entry.View.GetInstanceID() == entry.ViewId)
                BattleImpactFeedbackPresenter.PositionWorldGraphic(entry.Marker, MarkerPosition(entry), camera);
    }

    internal DRPGBattleDeparturePresentation Transfer(DRPGBattleDepartureJournal facts, BattleReturnBarrier barrier,
        BattleDefeatedActorRoster roster, IDRPGBattlePresentationClock clock)
        => TransferCore(facts, roster, clock, barrier != null && barrier.Receipt.Completion.IsCompleted &&
            barrier.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
            id => lease.CanDepart(barrier, id), id => lease.AuthorizeDeparture(barrier, id));

    internal DRPGBattleDeparturePresentation Transfer(DRPGBattleDepartureJournal facts, BattleSettlementDefeatBarrier barrier,
        BattleDefeatedActorRoster roster, IDRPGBattlePresentationClock clock)
        => TransferCore(facts, roster, clock, barrier != null && barrier.HasRenderedHome,
            id => lease.CanDepart(barrier, id), id => lease.AuthorizeDeparture(barrier, id));

    private DRPGBattleDeparturePresentation TransferCore(DRPGBattleDepartureJournal facts,
        BattleDefeatedActorRoster roster, IDRPGBattlePresentationClock clock, bool homeReady,
        Func<int, bool> canDepart, Func<int, BattleDepartureProjectionPermit> authorize)
    {
        if (!disposed && lease.IsActive && departure != null && departure.Departures.HasSameValues(facts)) return departure;
        var result = new DRPGBattleDeparturePresentation(facts);
        if (disposed || !lease.IsActive || !facts.Owner.Equals(lease.Owner) || facts.Members.Count != pending.Count ||
            !homeReady)
            return RejectTransfer(result, "Defeat transfer requires the complete rendered-home member set.");
        foreach (var member in facts.Members)
        {
            if (!pending.TryGetValue(member.Participant.Value, out var entry) || !canDepart(member.Participant.Value) ||
                entry.View.BoundActor.IsEnemy != (member.Side == DRPGBattleSide.Enemy) ||
                member.Side == DRPGBattleSide.Player && (roster == null || !roster.CanTransfer(member.Participant) ||
                    !roster.HasIndependentAnchor(member.Participant, lease)))
                return RejectTransfer(result, "Defeat member, view or authored incap binding is unavailable.");
        }
        departure = result; transferredFrame = clock.RenderedFrame; transferredRoster = roster;
        var createdIncap = new List<ParticipantId>();
        // Stable two-lane transfer: allies first, then enemies. No rule writes.
        try
        {
            foreach (var side in new[] { DRPGBattleSide.Player, DRPGBattleSide.Enemy })
            foreach (var member in facts.Members)
            {
                if (member.Side != side) continue;
                var entry = pending[member.Participant.Value];
                var permit = authorize(member.Participant.Value);
                int modelId = 0;
                if (side == DRPGBattleSide.Player)
                {
                    var model = roster.Transfer(member.Participant, entry.Marker, camera,
                        () => !disposed && lease.IsActive && roster.HasIndependentAnchor(member.Participant, lease));
                    createdIncap.Add(member.Participant); modelId = model.GetInstanceID();
                }
                else BattleImpactFeedbackPresenter.DestroyOwnedMarker(entry.Marker);
                // Restore pooled material values only in this unrendered removal
                // batch; no normal-color frame or second flash is presented.
                for (int i = 0; i < entry.Renderers.Length; i++)
                    if (entry.Renderers[i] != null) entry.Renderers[i].color = entry.Colors[i];
                pending.Remove(member.Participant.Value);
                lease.DepartView(permit);
                if (disposed || !lease.IsActive) throw new InvalidOperationException("Departure callback lost its projection owner.");
                result.RecordView(new DRPGDepartureViewFact(member.Participant, entry.ViewId, modelId));
            }
        }
        catch (Exception error)
        {
            foreach (var participant in createdIncap) roster.RollbackTransfer(participant);
            return RejectTransfer(result, error.Message);
        }
        if (facts.Members.Count == 0) result.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Explicit empty departure."));
        return result;
    }

    internal void TickDeparture(IDRPGBattlePresentationClock clock)
    {
        if (departure == null || departure.Receipt.Completion.IsCompleted) return;
        if (disposed || !lease.IsActive) { RejectTransfer(departure, "Departure projection owner lost."); return; }
        if (retiredSpriteBodies)
        {
            if (clock.RenderedFrame > transferredFrame)
                departure.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Vacated homes rendered."));
            return;
        }
        foreach (var view in departure.Views)
            if (view.IncapacitatedModelInstanceId != 0 && (transferredRoster == null ||
                !transferredRoster.IsVisible(view.Participant, view.IncapacitatedModelInstanceId)))
            { RejectTransfer(departure, "Transferred incapacitated model or marker is not visible."); return; }
        if (clock.RenderedFrame > transferredFrame)
        {
            if (transferredRoster != null && transferredRoster.RetireAfterRenderedTransfer && departure.Views.Count > 0)
            {
                foreach (var view in departure.Views)
                    if (view.IncapacitatedModelInstanceId != 0) transferredRoster.RollbackTransfer(view.Participant);
                retiredSpriteBodies = true;
                transferredFrame = clock.RenderedFrame;
                return;
            }
            departure.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Typed departure state rendered."));
        }
    }

    private static DRPGBattleDeparturePresentation RejectTransfer(DRPGBattleDeparturePresentation result, string error)
    { result.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, error)); return result; }

    internal bool IsVisibleAtHome()
    {
        if (disposed || !lease.IsActive) return false;
        foreach (var entry in pending.Values)
        {
            if (entry.View == null || !entry.View.gameObject.activeInHierarchy || entry.View.RuntimeId != entry.RuntimeId ||
                entry.View.GetInstanceID() != entry.ViewId || entry.Marker == null || !entry.Marker.enabled ||
                !entry.Marker.gameObject.activeInHierarchy || entry.Marker.color.a < .95f) return false;
            float alpha = entry.Marker.color.a;
            foreach (var group in entry.Marker.GetComponentsInParent<CanvasGroup>())
            {
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
            if (alpha < .95f) return false;
            bool visibleRenderer = false;
            if (!lease.IsAtCurrentHome(entry.RuntimeId)) return false;
            for (int i = 0; i < entry.Renderers.Length; i++)
            {
                var renderer = entry.Renderers[i]; var baseline = entry.Colors[i];
                if (renderer == null || renderer.color.r > baseline.r * .4f + .001f ||
                    renderer.color.g > baseline.g * .4f + .001f || renderer.color.b > baseline.b * .4f + .001f) return false;
                visibleRenderer |= renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null && renderer.color.a > .01f;
            }
            if (!visibleRenderer) return false;
        }
        return true;
    }

    private static Vector3 MarkerPosition(Pending entry)
    {
        Vector3 point = entry.View.transform.position;
        foreach (var renderer in entry.Renderers)
            if (renderer != null && renderer.enabled) point.y = Mathf.Max(point.y, renderer.bounds.max.y);
        return point + Vector3.up * .25f;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (departure != null) RejectTransfer(departure, "Departure owner closed.");
        foreach (var entry in pending.Values)
        {
            BattleImpactFeedbackPresenter.DestroyOwnedMarker(entry.Marker);
            // Cleanup has no tween/flash. Do not leak a previous actor's dim
            // into the next use of a pooled renderer or write to a recycled ID.
            if (entry.View == null || entry.View.RuntimeId != entry.RuntimeId || entry.View.GetInstanceID() != entry.ViewId) continue;
            for (int i = 0; i < entry.Renderers.Length; i++)
                if (entry.Renderers[i] != null) entry.Renderers[i].color = entry.Colors[i];
        }
        pending.Clear();
    }
}
