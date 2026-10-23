using System;

// Stage 7 consumes an already committed immutable ledger. It has no resolver,
// mutation workspace, Session facade, scheduler or continuation authority.
internal sealed class DRPGBasicAttackImpactSequence : IDisposable
{
    private const double ReadableMinimum = .45d;
    private readonly DRPGBattleActionResolutionLedger ledger;
    private readonly IStandardBasicAttackPresentationPort port;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleRuleMutationLease lease;
    private readonly BattlePresentationWatchdog watchdog;
    private PresentationReceipt expected;
    private double startedAt;
    private long startedFrame;
    private int step;
    private bool disposed;

    internal DRPGBasicAttackImpactSequence(DRPGBattleActionResolutionLedger ledger,
        IStandardBasicAttackPresentationPort port, IDRPGBattlePresentationClock clock,
        BattleRuleMutationLease lease)
    {
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.port = port ?? throw new ArgumentNullException(nameof(port));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        if (!OwnsRoot()) throw new ArgumentException("Stage7 requires the matching root lease.");
        watchdog = new BattlePresentationWatchdog(clock);
        Issue();
    }

    internal bool ReadyForReturn { get; private set; }
    internal string Fault { get; private set; } = string.Empty;
    internal bool Degraded { get; private set; }

    internal void Tick()
    {
        if (disposed || ReadyForReturn || Fault.Length != 0) return;
        if (!OwnsRoot()) { Fail("Root impact presentation ownership lost."); return; }
        try
        {
            // Recovery wins over a callback arriving at or after its deadline.
            watchdog.Tick();
            (port as IDRPGBattlePresentationPump)?.Tick();
            if (!OwnsRoot()) { Fail("Root impact presentation ownership lost during callback."); return; }
            if (!expected.Completion.IsCompleted) return;
            var terminal = expected.Completion.Result;
            if (terminal.Kind == PresentationReceiptTerminalKind.Faulted)
            { Fail(terminal.Detail); return; }
            if (clock.Now - startedAt < ReadableMinimum ||
                terminal.Kind == PresentationReceiptTerminalKind.Completed && clock.RenderedFrame <= startedFrame) return;
            Degraded |= terminal.Kind == PresentationReceiptTerminalKind.Degraded;
            Trace(ActionTraceEventKind.ReceiptCompleted, terminal.Detail,
                terminal.Kind == PresentationReceiptTerminalKind.Degraded ? ActionTraceReceiptStatus.Degraded : ActionTraceReceiptStatus.Completed);
            // Step 0 is primary; reaction steps retain their original ledger index + 1.
            // No loop issues more than one visible item in a rendered frame.
            do { step++; }
            while (step <= ledger.Reactions.Count && !ledger.Reactions[step - 1].RequiresPresentation);
            if (step > ledger.Reactions.Count) ReadyForReturn = true;
            else Issue();
        }
        catch (Exception exception) { Fail(exception.Message); }
    }

    private bool OwnsRoot() => lease.Arbiter.Owns(lease, BattleTransactionIdentity.ForRootAction(ledger.Identity));

    private void Issue()
    {
        startedAt = clock.Now; startedFrame = clock.RenderedFrame;
        expected = null;
        try
        {
            var receipt = step == 0
                ? port.PresentImpact(new ImpactPresentationCommand(ledger, startedAt))
                : port.PresentReaction(new ReactionPresentationCommand(ledger, step, startedAt));
            if (receipt == null || receipt.Identity != ledger.Identity ||
                receipt.Stage != DRPGBattleActionStage.ImpactAndReactions || receipt.StepOrdinal != step)
            { Fail("Impact receipt identity/stage/step conflict."); return; }
            expected = receipt;
            Trace(ActionTraceEventKind.ReceiptIssued, "Immutable ledger item", ActionTraceReceiptStatus.Issued);
            watchdog.Watch(receipt, startedAt + ReadableMinimum + 2d, "ConflictCheckpoint", () =>
            {
                (port as IDRPGBattlePresentationPump)?.CancelReceipt(receipt, "Expected impact receipt missing");
                Trace(ActionTraceEventKind.WatchdogExpired, "Owned feedback cleaned; ledger not resolved again.", ActionTraceReceiptStatus.None);
            });
        }
        catch (Exception exception) { Fail(exception.Message); }
    }

    private void Trace(ActionTraceEventKind kind, string detail, ActionTraceReceiptStatus status)
    {
        string eventId = step == 0 ? ledger.PrimaryImpact.EventId : ledger.Reactions[step - 1].EventId;
        string prefix = "impact-presentation-" + expected.ReceiptId.Value;
        lease.Arbiter.Trace.Record(0d, clock.Now, ActionTraceIdentity.ForTransaction(lease.Owner), kind,
            DRPGBattleActionStage.ImpactAndReactions, step, prefix + "-" + kind,
            kind == ActionTraceEventKind.ReceiptCompleted ? prefix + "-ReceiptIssued" : eventId,
            new ActionTraceContext(receiptId: expected.ReceiptId, receiptStatus: status, detail: detail));
    }

    private void Fail(string detail)
    {
        Fault = string.IsNullOrEmpty(detail) ? "Impact presentation faulted." : detail;
        Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (expected == null || expected.Completion.IsCompleted) return;
        try { (port as IDRPGBattlePresentationPump)?.CancelReceipt(expected, "Impact sequence disposed"); }
        finally { expected.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Impact sequence disposed")); }
    }
}
