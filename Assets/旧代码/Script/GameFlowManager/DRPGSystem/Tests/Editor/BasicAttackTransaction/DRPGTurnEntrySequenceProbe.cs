#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal static class DRPGTurnEntrySequenceProbe
{
    // Break caught: the action-only sequence refuses candidate ownership or collapses synchronous leaves into one frame.
    internal static void CandidateUsesSerialReadableReceiptsWithoutAnAction()
    {
        using (var f = new Fixture(true))
        using (var sequence = f.Start())
        {
            Check(f.Port.Commands.Count == 1 && f.Port.Commands[0].Owner == f.Domain.Lease.Owner &&
                f.Port.Commands[0].Stage == DRPGBattleActionStage.TurnBegin && !f.Port.Commands[0].Identity.IsValid,
                "Candidate receipt borrowed an action or stage9 owner.");
            f.Port.Complete(0); f.Clock.Now = .449; f.Clock.RenderedFrame = 1; sequence.Tick();
            Check(f.Port.Commands.Count == 1 && !sequence.ReadyForDefeatFinalization, "Candidate skipped the first .45s hold.");
            f.Clock.Now = .45; sequence.Tick();
            Check(f.Port.Commands.Count == 2 && f.Port.Commands[1].IssuedAt == .45, "Next candidate item did not wait for its predecessor.");
            f.Port.Complete(1); f.Clock.Now = .90; sequence.Tick();
            Check(!sequence.ReadyForDefeatFinalization, "Second candidate item bypassed its fresh rendered frame.");
            f.Clock.RenderedFrame++; sequence.Tick();
            Check(sequence.ReadyForDefeatFinalization && f.Domain.Player.CurrentHp == 93 && f.Domain.State.BattleRevision == f.Revision &&
                f.Domain.Arbiter.Owns(f.Domain.Lease, f.Domain.Lease.Owner) && !f.Domain.Arbiter.IsCommandInputOpen,
                "Candidate presentation replayed rules, failed its terminal or released input early.");
        }
    }

    // Break caught: an empty candidate skips the shared .30s handoff or manufactures two lane waits.
    internal static void EmptyCandidateWaitsOnceAndRequiresRenderedEvidence()
    {
        using (var f = new Fixture(false))
        using (var sequence = f.Start())
        {
            f.Port.Complete(0); f.Clock.Now = .30; sequence.Tick();
            Check(!sequence.ReadyForDefeatFinalization, "No rendered frame was required.");
            f.Clock.RenderedFrame = 1; sequence.Tick();
            Check(sequence.ReadyForDefeatFinalization && f.Port.Commands.Count == 1 && f.Domain.State.BattleRevision == f.Revision,
                "Empty candidate added a second wait or another rule commit.");
        }
    }

    // Break caught: watchdog keys only default ActionIdentity, runs before its deadline, or accepts callbacks after cleanup.
    internal static void CandidateTimeoutCleansBeforeDegradingAndRejectsLateCallback()
    {
        using (var f = new Fixture(true))
        using (var sequence = f.Start())
        {
            f.Clock.Now = 2.449; sequence.Tick();
            Check(f.Port.Cancelled.Count == 0 && !f.Port.Receipts[0].Completion.IsCompleted, "Candidate timed out before deadline.");
            bool lateAccepted = true;
            f.Port.OnTick = () => lateAccepted = f.Port.Receipts[0].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "late"));
            f.Clock.Now = 2.45; sequence.Tick();
            Check(!lateAccepted && f.Port.Cancelled.Count == 1 && f.Port.CleanedBeforeTerminal &&
                f.Port.Receipts[0].Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded && f.Port.Commands.Count == 1,
                "Candidate recovery skipped owned cleanup, accepted a late leaf or skipped its frame fence.");
            f.Clock.RenderedFrame = 1; sequence.Tick();
            Check(sequence.Degraded && f.Port.Commands.Count == 2 && f.Port.Commands[1].Owner == f.Domain.Lease.Owner,
                "Recovered candidate did not continue exactly once under the same owner.");
        }
    }

    // Break caught: a different candidate with the same default ActionIdentity is adopted or cancelled.
    internal static void ForeignCandidateReceiptIsNeverAdoptedOrCancelled()
    {
        using (var f = new Fixture(false))
        {
            f.Port.Foreign = true;
            using (var sequence = f.Start())
                Check(sequence.Fault.Length != 0 && !sequence.ReadyForDefeatFinalization && f.Port.Cancelled.Count == 0 &&
                    !f.Port.Receipts[0].Completion.IsCompleted, "Foreign candidate receipt was accepted or mutated.");
        }
    }

    // Break caught: disposing an enclosing scene during step issuance leaks the receipt created by that callback.
    internal static void CandidateDisposalDuringIssueCleansItsReturnedLeaf()
    {
        using (var f = new Fixture(true))
        using (var sequence = f.Start())
        {
            f.Port.OnPresent = sequence.Dispose;
            f.Port.Complete(0); f.Clock.Now = .45; f.Clock.RenderedFrame = 1; sequence.Tick();
            Check(f.Port.Receipts.Count == 2 && f.Port.Cancelled.Count == 1 && f.Port.Receipts[1].Completion.IsCompleted &&
                !sequence.ReadyForDefeatFinalization && f.Domain.Lease.IsActive, "Candidate disposal leaked a leaf or released its domain owner.");
        }
    }

    // Break caught: unknown terminal enum values are silently treated as successful candidate completion.
    internal static void UnknownTerminalCannotReleaseTheCandidateBarrier()
    {
        using (var f = new Fixture(false))
        using (var sequence = f.Start())
        {
            f.Port.Receipts[0].TryComplete(new PresentationReceiptTerminal((PresentationReceiptTerminalKind)99, "invalid"));
            f.Clock.Now = .30; f.Clock.RenderedFrame = 1; sequence.Tick();
            Check(sequence.Fault.Length != 0 && !sequence.ReadyForDefeatFinalization && f.Domain.Lease.IsActive,
                "Unknown candidate terminal became success.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DRPGTurnEntrySettlementProbe.Fixture Domain = new DRPGTurnEntrySettlementProbe.Fixture();
        internal readonly DRPGSettlementPresentationProbe.FixtureClock Clock = new DRPGSettlementPresentationProbe.FixtureClock();
        internal readonly DRPGBattleSettlementLedger Ledger;
        internal readonly Port Port;
        internal readonly long Revision;
        internal Fixture(bool statuses)
        {
            Port = new Port(Clock);
            if (statuses) { Domain.Status(Domain.Player, "a-entry", -3); Domain.Status(Domain.Player, "z-entry", -4); }
            Domain.Begin(Domain.Player);
            Check(Domain.Commit(out Ledger, out var error), error); Revision = Domain.State.BattleRevision;
        }
        internal DRPGBattleSettlementSequence Start() => new DRPGBattleSettlementSequence(Ledger, Port, Clock, Domain.Lease);
        public void Dispose() => Domain.Dispose();
    }

    // Controls only the external presentation receipt; the shared domain ledger, sequence and watchdog remain real.
    private sealed class Port : IBattleSettlementPresentationPort, IDRPGBattlePresentationPump
    {
        internal Port(IDRPGBattlePresentationClock clock) { Clock = clock; }
        public IDRPGBattlePresentationClock Clock { get; }
        internal readonly List<SettlementPresentationCommand> Commands = new List<SettlementPresentationCommand>();
        internal readonly List<PresentationReceipt> Receipts = new List<PresentationReceipt>();
        internal readonly List<PresentationReceipt> Cancelled = new List<PresentationReceipt>();
        internal Action OnTick, OnPresent;
        internal bool Foreign, CleanedBeforeTerminal;
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command)
        {
            Commands.Add(command);
            var owner = command.Owner;
            if (Foreign) owner = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(owner.BattleGeneration,
                owner.TurnEntry.CandidateSerial + 1, owner.TurnEntry.Actor));
            var receipt = new PresentationReceipt(owner, command.Stage, command.StepOrdinal);
            Receipts.Add(receipt); OnPresent?.Invoke(); return receipt;
        }
        internal void Complete(int i) => Receipts[i].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "controlled leaf"));
        public void Tick() => OnTick?.Invoke();
        public void CancelReceipt(PresentationReceipt receipt, string reason)
        { Cancelled.Add(receipt); CleanedBeforeTerminal = !receipt.Completion.IsCompleted; }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
