using System;

// Created only after the entire step presenter completes. The new home fence
// cannot inherit the already-consumed Stage8 permission, even for the same Root.
internal sealed class BattleSettlementDefeatBarrier : IDisposable
{
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly BattleDefeatPresentationLayer defeats;
    private readonly BattleDefeatedActorRoster roster;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleHomeRenderedDefeatBarrier home;
    private readonly double startedAt;
    private double observedAt;
    private long observedFrame;
    private DRPGBattleDeparturePresentation transfer;
    private bool disposed;
    internal BattleSettlementDefeatBarrier(DRPGSettlementPresentationPlan plan, DRPGBattleDepartureJournal facts,
        DRPGBattleActionProjectionLease lease, BattleDefeatPresentationLayer defeats,
        BattleDefeatedActorRoster roster, IDRPGBattlePresentationClock clock, int? stepOrdinal = null)
    {
        this.lease = lease; this.defeats = defeats; this.roster = roster; this.clock = clock;
        int baseline = Math.Max(1, plan.Steps.Count), ordinal = stepOrdinal ?? baseline;
        Result = new DRPGBattleDeparturePresentation(facts, ordinal); startedAt = clock.Now;
        observedAt = startedAt; observedFrame = clock.RenderedFrame;
        if (facts.Owner != lease.Owner || !facts.HasSameValues(DRPGSettlementDepartureFacts.Describe(plan.Ledger)) ||
            ordinal != baseline && !(facts.Owner.Kind == BattleTransactionKind.TurnEntry && ordinal == baseline + 1) || !lease.TryOwnPresentation(this))
        { Fail("Settlement death provenance or projection owner conflict."); return; }
        home = new BattleHomeRenderedDefeatBarrier(lease.Owner, lease, clock, defeats.IsVisibleAtHome, facts.Stage);
    }
    internal DRPGBattleDeparturePresentation Result { get; }
    internal bool HasRenderedHome => !disposed && lease.IsActive && home != null && home.Receipt.Completion.IsCompleted &&
        home.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed;
    internal bool CanAuthorize(BattleTransactionIdentity owner, int id)
    {
        if (!HasRenderedHome || Result.Departures.Owner != owner || Result.Receipt.Completion.IsCompleted) return false;
        foreach (var member in Result.Departures.Members) if (member.Participant.Value == id) return true;
        return false;
    }
    internal void Tick()
    {
        if (disposed || Result.Receipt.Completion.IsCompleted) return;
        if (!lease.IsActive || double.IsNaN(clock.Now) || double.IsInfinity(clock.Now) || clock.Now < observedAt ||
            clock.RenderedFrame < observedFrame || clock.Now - startedAt >= 2d)
        { Fail("Settlement death owner, clock or rendered-home deadline failed."); return; }
        observedAt = clock.Now; observedFrame = clock.RenderedFrame;
        try
        {
            if (transfer == null)
            {
                home.Tick();
                if (!home.Receipt.Completion.IsCompleted) return;
                if (!HasRenderedHome) { Fail(home.Receipt.Completion.Result.Detail); return; }
                transfer = defeats.Transfer(Result.Departures, this, roster, clock);
            }
            else defeats.TickDeparture(clock);
            if (!transfer.Receipt.Completion.IsCompleted) return;
            if (transfer.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed)
            { Fail(transfer.Receipt.Completion.Result.Detail); return; }
            foreach (var view in transfer.Views) Result.RecordView(view);
            Result.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Settlement deaths transferred and rendered."));
        }
        catch (Exception error) { Fail(error.Message); }
    }
    private void Fail(string detail) => Result.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, detail));
    public void Dispose() { if (disposed) return; disposed = true; home?.Dispose(); Fail("Settlement death barrier closed."); }
}
