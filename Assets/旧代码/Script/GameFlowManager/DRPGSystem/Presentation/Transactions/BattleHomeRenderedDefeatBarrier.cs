using System;

// Reusable by RootAction and later settlement owners. The caller supplies a
// pinned-view visibility check, not a timer or a rule-death mutation callback.
internal sealed class BattleHomeRenderedDefeatBarrier : IDisposable
{
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly Func<bool> validateVisibleHome;
    private long observedFrame = -1;

    internal BattleHomeRenderedDefeatBarrier(BattleTransactionIdentity owner,
        DRPGBattleActionProjectionLease lease, IDRPGBattlePresentationClock clock, Func<bool> validateVisibleHome,
        DRPGBattleActionStage stage = DRPGBattleActionStage.Return)
    {
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.validateVisibleHome = validateVisibleHome ?? throw new ArgumentNullException(nameof(validateVisibleHome));
        Receipt = new PresentationReceipt(owner, stage);
        if (lease.Owner != owner || !lease.TryOwnPresentation(this))
            Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Home-rendered fence owner mismatch."));
    }
    internal PresentationReceipt Receipt { get; }
    internal void Tick()
    {
        if (Receipt.Completion.IsCompleted) return;
        bool valid;
        try { valid = lease.IsActive && validateVisibleHome(); }
        catch { valid = false; }
        if (!valid)
        {
            Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Pending defeat is not visibly retained at its pinned home."));
            return;
        }
        if (observedFrame < 0) { observedFrame = clock.RenderedFrame; return; }
        if (clock.RenderedFrame > observedFrame)
            Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Pinned home and pending defeat survived a rendered frame."));
    }
    public void Dispose() => Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Home-rendered fence owner closed."));
}
