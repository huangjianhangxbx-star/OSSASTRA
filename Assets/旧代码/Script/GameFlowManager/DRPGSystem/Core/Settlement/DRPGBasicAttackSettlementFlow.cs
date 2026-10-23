using System;

// Root-owned Stage9 coordinator. Rules commit once, visible steps are serial,
// then typed rendered departures precede one rule/visual roster reconciliation.
// Final publication/continuation intentionally belongs to the next Root gate.
internal sealed class DRPGBasicAttackSettlementFlow : IDisposable
{
    private readonly DRPGPreparedBasicAttackRuleAction rules;
    private readonly IStandardBasicAttackSettlementPort settlementPort;
    private readonly IDRPGBattlePresentationPump pump;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleRuleMutationLease lease;
    private readonly Func<bool> scopeStillOwned;
    private DRPGBattleSettlementSequence items;
    private DRPGBattleDeparturePresentation departure;
    private PresentationReceipt expected;
    private double deadline, observedAt;
    private long observedFrame;
    private int finalOrdinal, step;
    private bool disposed, ticking;
    internal DRPGBattleSettlementLedger Ledger { get; private set; }
    internal bool ReadyForFinalization { get; private set; }
    internal bool Degraded => items != null && items.Degraded;
    internal string Fault { get; private set; } = string.Empty;

    internal DRPGBasicAttackSettlementFlow(DRPGPreparedBasicAttackRuleAction rules, IStandardBasicAttackPresentationPort port,
        IDRPGBattlePresentationClock clock, BattleRuleMutationLease lease, Func<bool> scopeStillOwned)
    {
        this.rules = rules; this.clock = clock; this.lease = lease; this.scopeStillOwned = scopeStillOwned;
        settlementPort = port as IStandardBasicAttackSettlementPort; pump = port as IDRPGBattlePresentationPump;
        observedAt = clock.Now; observedFrame = clock.RenderedFrame;
        try
        {
            if (!Observe() || !OwnsRoot() || settlementPort == null || pump == null ||
                !settlementPort.TryCaptureSettlementHomes(lease.Owner.Action, out var homes))
            { Fail("Stage9 requires its matching Root, clock and published homes."); return; }
            if (!OwnsRoot()) { Fail("Stage9 lost its binding while freezing homes."); return; }
            if (!rules.TryCommitSettlement(homes, out var ledger, out var error)) { Fail(error); return; }
            Ledger = ledger; finalOrdinal = Math.Max(1, new DRPGSettlementPresentationPlan(ledger).Steps.Count);
            Trace(ActionTraceEventKind.PhaseEntered, "Settlement", ActionTraceReceiptStatus.None);
            items = new DRPGBattleSettlementSequence(ledger, port, clock, lease);
            if (!OwnsRoot()) { Fail("Stage9 lost ownership while issuing its first item."); return; }
            if (items.Fault.Length != 0) Fail(items.Fault);
        }
        catch (Exception error) { Fail(error.Message); }
    }

    internal void Tick()
    {
        if (disposed || ticking || ReadyForFinalization || Fault.Length != 0) return;
        ticking = true;
        try
        {
            if (!Observe()) return;
            if (!OwnsRoot()) { Fail("Stage9 Root or presentation binding lost."); return; }
            if (step == 0)
            {
                items.Tick();
                if (!OwnsRoot()) { Fail("Stage9 ownership lost during an item callback."); return; }
                if (items.Fault.Length != 0) { Fail(items.Fault); return; }
                if (items.ReadyForDefeatFinalization) { step = 1; IssueFinalGate(); }
                return;
            }
            if (!expected.Completion.IsCompleted && clock.Now >= deadline)
            { Fail("Stage9 final gate timed out without rendered proof."); return; }
            pump.Tick();
            if (disposed || !OwnsRoot()) { Fail("Stage9 ownership lost during final gate callback."); return; }
            if (!Observe() || !expected.Completion.IsCompleted) return;
            if (expected.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed)
            { Fail(expected.Completion.Result.Detail); return; }
            Trace(ActionTraceEventKind.ReceiptCompleted, "Settlement final gate rendered", ActionTraceReceiptStatus.Completed);
            if (step == 2) ReadyForFinalization = true;
            else { step = 2; IssueFinalGate(); }
        }
        catch (Exception error) { Fail(error.Message); }
        finally { ticking = false; }
    }

    private void IssueFinalGate()
    {
        expected = null;
        PresentationReceipt candidate;
        if (step == 1)
        {
            if (!rules.TryDescribeSettlementDepartures(out var facts, out var error)) { Fail(error); return; }
            departure = settlementPort.CompleteSettlementDefeats(facts);
            if (departure == null || !facts.HasSameValues(departure.Departures)) { Fail("Settlement departure provenance changed."); return; }
            candidate = departure.Receipt;
        }
        else
        {
            if (!rules.TryReconcileFormationAfterSettlement(departure, out var reconciled, out var error)) { Fail(error); return; }
            candidate = settlementPort.ReconcileSettlementFormation(reconciled);
        }
        if (candidate == null || candidate.Owner != lease.Owner || candidate.Identity != Ledger.Identity ||
            candidate.Stage != DRPGBattleActionStage.Settlement || candidate.StepOrdinal != finalOrdinal + step - 1)
        { Fail("Settlement final receipt identity/stage/ordinal conflict."); return; }
        expected = candidate;
        if (disposed || !OwnsRoot()) { CancelExpected(); Fail("Stage9 lost ownership during final gate issuance."); return; }
        deadline = clock.Now + (step == 1 ? 2d : 2.30d);
        Trace(ActionTraceEventKind.ReceiptIssued, "Settlement sequential final gate", ActionTraceReceiptStatus.Issued);
    }
    private bool OwnsRoot() => lease != null && lease.Arbiter.Owns(lease, lease.Owner) &&
        lease.Owner.Kind == BattleTransactionKind.RootAction && scopeStillOwned != null && scopeStillOwned();
    private bool Observe()
    {
        double now = clock.Now; long frame = clock.RenderedFrame;
        if (double.IsNaN(now) || double.IsInfinity(now) || now < observedAt || frame < observedFrame)
        { Fail("Stage9 requires a monotonic finite clock."); return false; }
        observedAt = now; observedFrame = frame; return true;
    }
    private void Trace(ActionTraceEventKind kind, string detail, ActionTraceReceiptStatus status)
    {
        var action = lease.Owner.Action;
        string prefix = expected == null
            ? "settlement-root-" + action.BattleGeneration + "-" + action.TurnSerial + "-" + action.ActionId
            : "settlement-final-" + expected.ReceiptId.Value;
        lease.Arbiter.Trace.Record(0d, clock.Now, ActionTraceIdentity.ForTransaction(lease.Owner), kind,
            DRPGBattleActionStage.Settlement, step == 0 ? 0 : finalOrdinal + step - 1, prefix + "-" + kind,
            kind == ActionTraceEventKind.ReceiptCompleted ? prefix + "-ReceiptIssued" : string.Empty,
            new ActionTraceContext(receiptId: expected != null ? expected.ReceiptId : default, receiptStatus: status, detail: detail));
    }
    private void Fail(string detail)
    { if (Fault.Length == 0) Fault = string.IsNullOrEmpty(detail) ? "Stage9 failed." : detail; Dispose(); }
    private void CancelExpected()
    {
        if (expected == null || expected.Completion.IsCompleted) return;
        try { pump.CancelReceipt(expected, "Stage9 flow disposed"); }
        catch (Exception error) { Fault += " | Final gate cleanup: " + error.Message; }
        finally { expected.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Stage9 flow disposed")); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { items?.Dispose(); } catch (Exception error) { Fault += " | Item cleanup: " + error.Message; }
        CancelExpected();
    }
}
