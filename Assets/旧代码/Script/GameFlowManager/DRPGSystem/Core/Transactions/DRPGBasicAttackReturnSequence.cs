using System;

// Root-owned Stage8. Only this sequence grants the post-return rule primitive
// after the typed departure terminal. No scheduler, outcome or continuation.
internal sealed class DRPGBasicAttackReturnSequence : IDisposable
{
    private readonly DRPGBasicAttackFrozenSnapshot frozen;
    private readonly DRPGPreparedBasicAttackRuleAction rules;
    private readonly IStandardBasicAttackPresentationPort port;
    private readonly IStandardBasicAttackReturnPort returnPort;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleRuleMutationLease lease;
    private PresentationReceipt expected;
    private DRPGBattleDepartureJournal departures;
    private double deadline;
    private int step;
    private bool disposed;
    internal bool ReadyForSettlement { get; private set; }
    internal string Fault { get; private set; } = string.Empty;

    internal DRPGBasicAttackReturnSequence(in DRPGBasicAttackFrozenSnapshot frozen,
        DRPGPreparedBasicAttackRuleAction rules, IStandardBasicAttackPresentationPort port,
        IDRPGBattlePresentationClock clock, BattleRuleMutationLease lease)
    {
        this.frozen = frozen; this.rules = rules; this.port = port;
        this.clock = clock; this.lease = lease;
        returnPort = port as IStandardBasicAttackReturnPort;
        Issue();
    }

    internal void Tick()
    {
        if (disposed || ReadyForSettlement || Fault.Length != 0) return;
        try
        {
            if (!OwnsRoot()) { Fail("Stage8 ownership lost."); return; }
            if (clock.Now >= deadline && !expected.Completion.IsCompleted)
            { Fail("Stage8 receipt timed out; rendered home/departure/formation was not proven."); return; }
            (port as IDRPGBattlePresentationPump)?.Tick();
            if (disposed || !OwnsRoot()) { Fail("Stage8 ownership lost during callback."); return; }
            if (!expected.Completion.IsCompleted) return;
            var terminal = expected.Completion.Result;
            // Degraded cue cleanup is not proof of the home/rendered gates.
            if (terminal.Kind != PresentationReceiptTerminalKind.Completed)
            { Fail(terminal.Detail); return; }
            Trace(ActionTraceEventKind.ReceiptCompleted, "Stage8 barrier completed", ActionTraceReceiptStatus.Completed);
            if (step == 2) { ReadyForSettlement = true; return; }
            step++; Issue();
        }
        catch (Exception exception) { Fail(exception.Message); }
    }

    private bool OwnsRoot() => lease != null && lease.Arbiter.Owns(lease,
        BattleTransactionIdentity.ForRootAction(frozen.Identity));

    private void Issue()
    {
        expected = null;
        try
        {
            if (!OwnsRoot() || returnPort == null) { Fail("Stage8 ports/owner missing."); return; }
            PresentationReceipt candidate;
            if (step == 0) candidate = port.Return(new ReturnPresentationCommand(frozen.Identity));
            else if (step == 1)
            {
                if (!rules.TryDescribeDepartures(out departures, out var error)) { Fail(error); return; }
                var transfer = returnPort.CompleteDefeats(departures);
                if (transfer == null || !departures.HasSameValues(transfer.Departures))
                { Fail("Departure receipt changed its owner/fingerprint/member set."); return; }
                candidate = transfer.Receipt;
            }
            else
            {
                if (!rules.TryReconcileFormationAfterReturn(out var reconciled, out var error)) { Fail(error); return; }
                candidate = returnPort.ReconcileFormation(reconciled);
            }
            if (disposed || !OwnsRoot()) { Fail("Stage8 ownership lost while issuing receipt."); return; }
            if (candidate == null || candidate.Owner != lease.Owner || candidate.Stage != DRPGBattleActionStage.Return || candidate.StepOrdinal != step)
            { Fail("Stage8 receipt identity/stage/step conflict."); return; }
            expected = candidate;
            deadline = clock.Now + (step == 0 ? Math.Max(.38d, frozen.Timing.ReturnSeconds) : step == 2 ? .30d : .1d) + 2d;
            if (step == 0) Trace(ActionTraceEventKind.PhaseEntered, "Return", ActionTraceReceiptStatus.None);
            Trace(ActionTraceEventKind.ReceiptIssued, "Stage8 sequential gate", ActionTraceReceiptStatus.Issued);
        }
        catch (Exception exception) { Fail(exception.Message); }
    }

    private void Trace(ActionTraceEventKind kind, string detail, ActionTraceReceiptStatus status)
    {
        string prefix = "return-sequence-" + expected.ReceiptId.Value;
        lease.Arbiter.Trace.Record(0d, clock.Now, ActionTraceIdentity.ForTransaction(lease.Owner), kind,
            DRPGBattleActionStage.Return, step, prefix + "-" + kind,
            kind == ActionTraceEventKind.ReceiptCompleted ? prefix + "-ReceiptIssued" : string.Empty,
            new ActionTraceContext(receiptId: expected.ReceiptId, receiptStatus: status, detail: detail));
    }

    private void Fail(string reason)
    { if (Fault.Length == 0) Fault = string.IsNullOrEmpty(reason) ? "Stage8 failed." : reason; Dispose(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (expected == null || expected.Completion.IsCompleted) return;
        try { (port as IDRPGBattlePresentationPump)?.CancelReceipt(expected, "Stage8 sequence disposed"); }
        finally { expected.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Stage8 sequence disposed")); }
    }
}
