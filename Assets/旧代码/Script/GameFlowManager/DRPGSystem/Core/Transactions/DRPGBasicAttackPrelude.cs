using System;

public interface IDRPGBattlePresentationClock
{
    double Now { get; }
    long RenderedFrame { get; }
}

internal interface IDRPGBattlePresentationPump
{
    IDRPGBattlePresentationClock Clock { get; }
    void Tick();
    void CancelReceipt(PresentationReceipt receipt, string reason);
}

// Phase 4-6 protocol owned by the root module. It never commits rules or
// completes the root action; the later impact runner consumes ReadyForImpact.
internal sealed class DRPGBasicAttackPrelude : IDisposable
{
    private readonly DRPGBasicAttackFrozenSnapshot frozen;
    private readonly IStandardBasicAttackPresentationPort port;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleRuleMutationLease lease;
    private readonly string parentEventId;
    private readonly BattlePresentationWatchdog watchdog;
    private double startedAt;
    private long startedFrame;
    private bool disposed;
    internal DRPGBasicAttackPrelude(in DRPGBasicAttackFrozenSnapshot snapshot,
        IStandardBasicAttackPresentationPort port, IDRPGBattlePresentationClock clock,
        BattleRuleMutationLease lease, string parentEventId)
    {
        if (!snapshot.Identity.IsValid) throw new ArgumentException("Locked snapshot required.");
        if (port == null || clock == null || lease == null) throw new ArgumentNullException();
        if (!lease.Arbiter.Owns(lease, BattleTransactionIdentity.ForRootAction(snapshot.Identity)))
            throw new ArgumentException("Prelude requires the matching root lease.");
        frozen = snapshot; this.port = port; this.clock = clock; this.lease = lease;
        this.parentEventId = parentEventId;
        watchdog = new BattlePresentationWatchdog(clock);
        Issue(DRPGBattleActionStage.Announcement);
    }
    internal DRPGBattleActionStage Stage { get; private set; } = DRPGBattleActionStage.Locked;
    internal PresentationReceipt ExpectedReceipt { get; private set; }
    internal bool ReadyForImpact { get; private set; }
    internal bool Degraded { get; private set; }
    internal string Fault { get; private set; } = string.Empty;
    internal void Tick()
    {
        if (disposed || ReadyForImpact || !string.IsNullOrEmpty(Fault)) return;
        if (!lease.Arbiter.Owns(lease, BattleTransactionIdentity.ForRootAction(frozen.Identity)))
        { Fault = "Root presentation ownership lost."; Dispose(); return; }
        watchdog.Tick();
        (port as IDRPGBattlePresentationPump)?.Tick();
        if (!ExpectedReceipt.Completion.IsCompleted) return;
        double minimum = Stage == DRPGBattleActionStage.Announcement ? frozen.Timing.AnnouncementSeconds :
            Stage == DRPGBattleActionStage.ConflictComposition ? frozen.Timing.ConflictHoldSeconds : frozen.Timing.WindupMinimumSeconds;
        var result = ExpectedReceipt.Completion.Result;
        if (clock.Now - startedAt < minimum ||
            result.Kind == PresentationReceiptTerminalKind.Completed && clock.RenderedFrame <= startedFrame) return;
        if (result.Kind == PresentationReceiptTerminalKind.Faulted) { Fault = result.Detail; Dispose(); return; }
        Degraded |= result.Kind == PresentationReceiptTerminalKind.Degraded;
        Trace(ActionTraceEventKind.ReceiptCompleted, result.Detail,
            result.Kind == PresentationReceiptTerminalKind.Degraded ? ActionTraceReceiptStatus.Degraded : ActionTraceReceiptStatus.Completed);
        if (Stage == DRPGBattleActionStage.Announcement) Issue(DRPGBattleActionStage.ConflictComposition);
        else if (Stage == DRPGBattleActionStage.ConflictComposition) Issue(DRPGBattleActionStage.Windup);
        else ReadyForImpact = true;
    }

    private void Issue(DRPGBattleActionStage stage)
    {
        Stage = stage; startedAt = clock.Now; startedFrame = clock.RenderedFrame;
        try
        {
            ExpectedReceipt = stage == DRPGBattleActionStage.Announcement
                ? port.Announce(new AnnouncementCommand(frozen, startedAt, frozen.ActorDisplayName))
                : stage == DRPGBattleActionStage.ConflictComposition
                    ? port.EnterConflict(new ConflictCompositionCommand(frozen, startedAt))
                    : port.PlayWindup(new BasicAttackWindupCommand(frozen, startedAt));
            if (ExpectedReceipt == null || ExpectedReceipt.Identity != frozen.Identity || ExpectedReceipt.Stage != stage || ExpectedReceipt.StepOrdinal != 0)
            { Fault = "Presentation receipt identity/stage/step conflict."; Dispose(); return; }
            Trace(ActionTraceEventKind.PhaseEntered, stage.ToString(), ActionTraceReceiptStatus.None);
            Trace(ActionTraceEventKind.ReceiptIssued, string.Empty, ActionTraceReceiptStatus.Issued);
            var watched = ExpectedReceipt;
            double minimum = stage == DRPGBattleActionStage.Announcement ? frozen.Timing.AnnouncementSeconds :
                stage == DRPGBattleActionStage.ConflictComposition ? frozen.Timing.ConflictHoldSeconds : frozen.Timing.WindupMinimumSeconds;
            watchdog.Watch(watched, startedAt + minimum + 2d,
                stage == DRPGBattleActionStage.Announcement ? "LockBaseline" : "ConflictCheckpoint", () =>
                {
                    (port as IDRPGBattlePresentationPump)?.CancelReceipt(watched, "Expected receipt missing");
                    Trace(ActionTraceEventKind.WatchdogExpired, "Owned cue cleaned; no marker or rule commit synthesized.", ActionTraceReceiptStatus.None);
                });
        }
        catch (Exception exception) { Fault = exception.Message; Dispose(); }
    }

    private void Trace(ActionTraceEventKind kind, string detail, ActionTraceReceiptStatus status)
    {
        string prefix = "prelude-" + ExpectedReceipt.ReceiptId.Value;
        lease.Arbiter.Trace.Record(0d, clock.Now, ActionTraceIdentity.ForTransaction(lease.Owner), kind, Stage, 0,
            prefix + "-" + kind, kind == ActionTraceEventKind.ReceiptCompleted ? prefix + "-ReceiptIssued" : parentEventId,
            new ActionTraceContext(receiptId: ExpectedReceipt.ReceiptId, receiptStatus: status,
                actor: frozen.Turn.Actor, target: frozen.Target, side: frozen.ActorSide, detail: detail));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (ExpectedReceipt != null &&
            ExpectedReceipt.Owner == BattleTransactionIdentity.ForRootAction(frozen.Identity) &&
            !ExpectedReceipt.Completion.IsCompleted)
        {
            try { (port as IDRPGBattlePresentationPump)?.CancelReceipt(ExpectedReceipt, "Prelude disposed"); }
            finally { ExpectedReceipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Prelude disposed")); }
        }
    }
}
