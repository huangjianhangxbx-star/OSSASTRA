using System;
using System.Collections.Generic;

internal sealed class BattlePresentationWatchdog
{
    private readonly IDRPGBattlePresentationClock clock;
    private readonly Dictionary<PresentationReceiptId, Entry> entries = new Dictionary<PresentationReceiptId, Entry>();
    internal BattlePresentationWatchdog(IDRPGBattlePresentationClock clock)
    { this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }
    internal int RecoveryCount { get; private set; }
    internal string LastDroppedReason { get; private set; }
    internal void Watch(PresentationReceipt receipt, double deadline, string checkpoint, Action cleanup)
    {
        if (receipt == null || cleanup == null || string.IsNullOrEmpty(checkpoint) || double.IsNaN(deadline) || double.IsInfinity(deadline))
            throw new ArgumentException("Receipt, finite deadline, checkpoint and cleanup required.");
        if (entries.ContainsKey(receipt.ReceiptId)) throw new InvalidOperationException("Receipt already watched.");
        entries.Add(receipt.ReceiptId, new Entry(receipt, deadline, checkpoint, cleanup));
    }
    internal void Tick()
    {
        // Snapshot permits cleanup to enqueue another owned leaf without modifying this iteration.
        foreach (var e in new List<Entry>(entries.Values))
        {
            if (e.Recovered || e.Receipt.Completion.IsCompleted || clock.Now < e.Deadline) continue;
            if (!e.Receipt.TryBeginRecovery()) continue;
            e.Recovered = true; RecoveryCount++;
            var kind = PresentationReceiptTerminalKind.Degraded;
            string detail = "Timeout at " + e.Checkpoint;
            try { e.Cleanup(); } catch (Exception exception) { kind = PresentationReceiptTerminalKind.Faulted; detail += ": cleanup failed: " + exception.Message; }
            e.Receipt.CompleteRecovery(new PresentationReceiptTerminal(kind, detail));
        }
    }
    internal bool TryAccept(BattleTransactionIdentity owner, DRPGBattleActionStage stage, int step,
        PresentationReceiptId receiptId, PresentationReceiptTerminal terminal)
    {
        if (!entries.TryGetValue(receiptId, out var e) || owner != e.Receipt.Owner || stage != e.Receipt.Stage || step != e.Receipt.StepOrdinal)
        { LastDroppedReason = "identity-stage-step-receipt-conflict"; return false; }
        if (!e.Receipt.Completion.IsCompleted && clock.Now >= e.Deadline) Tick();
        if (e.Recovered) { LastDroppedReason = "late-after-recovery"; return false; }
        if (e.Receipt.Completion.IsCompleted)
        {
            var prior = e.Receipt.Completion.Result;
            if (prior.Kind == terminal.Kind && prior.Detail == terminal.Detail) return true;
            LastDroppedReason = "terminal-conflict"; return false;
        }
        return e.Receipt.TryComplete(terminal);
    }
    private sealed class Entry
    {
        internal readonly PresentationReceipt Receipt;
        internal readonly double Deadline;
        internal readonly string Checkpoint;
        internal readonly Action Cleanup;
        internal bool Recovered;
        internal Entry(PresentationReceipt receipt, double deadline, string checkpoint, Action cleanup)
        { Receipt = receipt; Deadline = deadline; Checkpoint = checkpoint; Cleanup = cleanup; }
    }
}
