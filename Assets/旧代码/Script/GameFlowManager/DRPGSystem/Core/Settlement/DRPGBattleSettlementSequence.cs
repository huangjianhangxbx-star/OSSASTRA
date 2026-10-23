using System;

// Shared Stage9 / TurnEntry presentation. It cannot resolve rules, release its
// transaction, select an actor or publish completion. Visible items precede defeat finalization.
internal sealed class DRPGBattleSettlementSequence : IDisposable
{
    private readonly DRPGSettlementPresentationPlan plan;
    private readonly IBattleSettlementPresentationPort port;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleRuleMutationLease lease;
    private readonly BattlePresentationWatchdog watchdog;
    private PresentationReceipt expected;
    private double startedAt;
    private long startedFrame;
    private int step;
    private bool disposed;
    private bool ticking;
    private bool cancelling;
    private double observedAt;
    private long observedFrame;

    internal DRPGBattleSettlementSequence(DRPGBattleSettlementLedger ledger,
        IBattleSettlementPresentationPort port, IDRPGBattlePresentationClock clock,
        BattleRuleMutationLease lease)
        : this(new DRPGSettlementPresentationPlan(ledger), port, clock, lease) { }

    internal DRPGBattleSettlementSequence(DRPGSettlementPresentationPlan plan,
        IBattleSettlementPresentationPort port, IDRPGBattlePresentationClock clock,
        BattleRuleMutationLease lease)
    {
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        this.port = port ?? throw new ArgumentNullException(nameof(port));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        if (!OwnsTransaction()) throw new ArgumentException("Settlement requires the matching active transaction lease.");
        watchdog = new BattlePresentationWatchdog(clock);
        Issue();
    }

    internal bool ReadyForDefeatFinalization { get; private set; }
    internal string Fault { get; private set; } = string.Empty;
    internal bool Degraded { get; private set; }
    private double Minimum => plan.IsEmpty ? 0d : .45d;
    private DRPGBattleActionStage Stage => plan.Ledger.Owner.Kind == BattleTransactionKind.TurnEntry
        ? DRPGBattleActionStage.TurnBegin : DRPGBattleActionStage.Settlement;
    private bool OwnsTransaction() => lease.Arbiter.Owns(lease, plan.Ledger.Owner);

    internal void Tick()
    {
        if (disposed || ticking || ReadyForDefeatFinalization || Fault.Length != 0) return;
        if (!OwnsTransaction()) { Fail("Settlement presentation ownership lost."); return; }
        ticking = true;
        try
        {
            if (!ObserveClock()) return;
            // Reserve timeout recovery before calling a leaf that may complete late.
            watchdog.Tick();
            if (disposed) return;
            if (!OwnsTransaction()) { Fail("Settlement ownership lost during recovery."); return; }
            (port as IDRPGBattlePresentationPump)?.Tick();
            if (disposed) return;
            if (!OwnsTransaction()) { Fail("Settlement ownership lost during callback."); return; }
            if (!ObserveClock()) return;
            if (!expected.Completion.IsCompleted) return;
            var terminal = expected.Completion.Result;
            if (terminal.Kind == PresentationReceiptTerminalKind.Faulted) { Fail(terminal.Detail); return; }
            if (terminal.Kind != PresentationReceiptTerminalKind.Completed && terminal.Kind != PresentationReceiptTerminalKind.Degraded)
            { Fail("Settlement received an unrecognized terminal kind."); return; }
            if (clock.Now - startedAt < Minimum || clock.RenderedFrame <= startedFrame) return;
            Degraded |= terminal.Kind == PresentationReceiptTerminalKind.Degraded;
            Trace(ActionTraceEventKind.ReceiptCompleted, terminal.Detail,
                terminal.Kind == PresentationReceiptTerminalKind.Degraded ? ActionTraceReceiptStatus.Degraded : ActionTraceReceiptStatus.Completed);
            step++;
            if (step >= Math.Max(1, plan.Steps.Count)) ReadyForDefeatFinalization = true;
            else Issue();
        }
        catch (Exception exception) { Fail(exception.Message); }
        finally { ticking = false; }
    }

    private void Issue()
    {
        expected = null;
        if (!ObserveClock()) return;
        startedAt = clock.Now; startedFrame = clock.RenderedFrame;
        try
        {
            var receipt = port.PresentSettlement(new SettlementPresentationCommand(plan, step, startedAt));
            if (receipt == null || receipt.Owner != lease.Owner || receipt.Identity != plan.Ledger.Identity ||
                receipt.Stage != Stage || receipt.StepOrdinal != step)
            { Fail("Settlement receipt owner/identity/stage/step conflict."); return; }
            expected = receipt;
            // A leaf may synchronously tear down its enclosing projection while
            // issuing. Adopt only our receipt, then clean it even if already disposed.
            if (disposed) { CancelExpected(); return; }
            if (!OwnsTransaction()) { Fail("Settlement ownership lost during issuance."); return; }
            Trace(ActionTraceEventKind.ReceiptIssued, plan.IsEmpty ? "Empty stage handoff" : "Immutable settlement item", ActionTraceReceiptStatus.Issued);
            watchdog.Watch(receipt, startedAt + Minimum + 2d, "SettlementCheckpoint", () =>
            {
                CancelLeaf(receipt, "Expected settlement receipt missing");
                Trace(ActionTraceEventKind.WatchdogExpired, "Owned settlement leaf recovered; rules not replayed.", ActionTraceReceiptStatus.None);
            });
        }
        catch (Exception exception) { Fail(exception.Message); }
    }

    private bool ObserveClock()
    {
        double now = clock.Now; long frame = clock.RenderedFrame;
        if (double.IsNaN(now) || double.IsInfinity(now) || now < observedAt || frame < observedFrame)
        { Fail("Settlement requires a finite monotonic presentation clock."); return false; }
        observedAt = now; observedFrame = frame; return true;
    }

    private void Trace(ActionTraceEventKind kind, string detail, ActionTraceReceiptStatus status)
    {
        string eventId = plan.IsEmpty ? "settlement-empty" : plan.Steps[step].EventId;
        string prefix = "settlement-presentation-" + expected.ReceiptId.Value;
        lease.Arbiter.Trace.Record(0d, clock.Now, ActionTraceIdentity.ForTransaction(lease.Owner), kind,
            Stage, step, prefix + "-" + kind,
            kind == ActionTraceEventKind.ReceiptCompleted ? prefix + "-ReceiptIssued" : eventId,
            new ActionTraceContext(receiptId: expected.ReceiptId, receiptStatus: status, detail: detail));
    }

    private void Fail(string detail)
    {
        if (Fault.Length == 0) Fault = string.IsNullOrEmpty(detail) ? "Settlement presentation faulted." : detail;
        Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CancelExpected();
    }

    private void CancelExpected()
    {
        if (expected == null || expected.Completion.IsCompleted) return;
        try { CancelLeaf(expected, "Settlement sequence disposed"); }
        catch (Exception exception) { Fault += " Settlement cleanup failed: " + exception.Message; }
        finally { expected.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Settlement sequence disposed")); }
    }

    private void CancelLeaf(PresentationReceipt receipt, string reason)
    {
        if (cancelling) return;
        cancelling = true;
        try { (port as IDRPGBattlePresentationPump)?.CancelReceipt(receipt, reason); }
        finally { cancelling = false; }
    }
}
