using System;
using System.Collections.Generic;

internal interface IBattleTurnEntryPresentationPort : IBattleSettlementPresentationPort, IDRPGBattlePresentationPump
{
    bool TryCaptureHomes(BattleTransactionIdentity owner, out IReadOnlyDictionary<ParticipantId, int> homes);
    PresentationReceipt PresentLifecycle(DRPGBattleSettlementLedger ledger, int ordinal);
    DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal departures, int ordinal);
    PresentationReceipt ReconcileFormation(DRPGBattleSettlementLedger ledger, DRPGBattlePostReturnResult rules, int ordinal);
}

// Candidate-owned entry, not a Root Action and not a Session facade.
internal sealed class DRPGBattleTurnEntryModule : IDisposable
{
    private enum Step { Pending, Items, Lifecycle, Departure, Formation }
    private readonly DRPGBattleSessionState state;
    private readonly BattleRuleMutationLease lease;
    private readonly DRPGTurnCandidateReservation reservation;
    private readonly IBattleTurnEntryPresentationPort port;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly Func<bool> scopeStillOwned;
    private readonly DRPGImmediateReactionLedgerBuilder.Continuation continuation;
    private readonly BattlePresentationWatchdog watchdog;
    private DRPGBattleSettlementSequence items;
    private DRPGBattleDeparturePresentation departure;
    private PresentationReceipt expected;
    private Step step;
    private int ordinal;
    private long expectedRevision, observedFrame, issuedFrame;
    private double observedAt, issuedAt, deadline;
    private bool disposed, ticking, cancelling;

    internal DRPGBattleTurnEntryModule(DRPGBattleSessionState state, BattleRuleMutationLease lease,
        DRPGTurnCandidateReservation reservation, IBattleTurnEntryPresentationPort port,
        IDRPGBattlePresentationClock clock, Func<bool> scopeStillOwned,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.reservation = reservation; this.port = port;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.scopeStillOwned = scopeStillOwned; this.continuation = continuation;
        expectedRevision = state.BattleRevision;
        observedAt = clock.Now; observedFrame = clock.RenderedFrame;
        watchdog = new BattlePresentationWatchdog(clock);
        // No leaf invocation or rule mutation on the continuation delivery stack.
    }
    internal DRPGBattleSettlementLedger Ledger { get; private set; }
    internal DRPGBattlePostReturnResult Reconciliation { get; private set; }
    internal bool ReadyForFinalization { get; private set; }
    internal bool CandidateStillEligible => ReadyForFinalization && Ledger.TurnEntryLifecycle.CandidateAlive &&
        !Ledger.TurnEntryLifecycle.SkipAction;
    internal bool Degraded { get; private set; }
    internal string Fault { get; private set; } = string.Empty;
    internal bool CanEnqueue(DRPGBattleSessionState expectedState, BattleRuleMutationLease expectedLease) =>
        !disposed && !ticking && step == Step.Pending && Ledger == null && Fault.Length == 0 &&
        ReferenceEquals(state, expectedState) && ReferenceEquals(lease, expectedLease) &&
        state.TurnCandidates.Matches(reservation) && state.OwnsRuleMutationLease(lease, lease.Owner);
    // Called by the State-owned candidate authority immediately before terminal
    // preparation. A cached ReadyForFinalization alone is never authorization:
    // scope teardown, rule drift or a reacquired lease may have occurred since.
    internal bool TryAuthorizeFinalization(DRPGBattleSessionState expectedState,
        BattleRuleMutationLease expectedLease, DRPGTurnCandidateReservation expectedReservation)
    {
        if (disposed || ticking || Fault.Length != 0 || !ReadyForFinalization ||
            !ReferenceEquals(state, expectedState) || !ReferenceEquals(lease, expectedLease) ||
            !state.TurnCandidates.Matches(expectedReservation) || !state.TurnCandidates.Matches(reservation) ||
            step != Step.Formation || Ledger == null || Reconciliation == null || expected == null ||
            !expected.Completion.IsCompleted || expected.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed)
            return false;
        try
        {
            if (!CheckOwner() || !Observe()) return false;
            return !disposed && state.BattleRevision == Reconciliation.CommittedRevision &&
                clock.Now - issuedAt >= .30d && clock.RenderedFrame > issuedFrame;
        }
        catch (Exception error) { Fail(error.Message); return false; }
    }
    internal void Tick()
    {
        if (disposed || ticking || ReadyForFinalization || Fault.Length != 0) return;
        ticking = true;
        try
        {
            if (!Observe() || !CheckOwner()) return;
            if (step == Step.Pending) { Begin(); return; }
            if (step == Step.Items)
            {
                items.Tick();
                if (disposed || !CheckOwner()) return;
                if (items.Fault.Length != 0) { Fail(items.Fault); return; }
                if (!items.ReadyForDefeatFinalization) return;
                Degraded |= items.Degraded;
                var life = Ledger.TurnEntryLifecycle;
                Issue(life.CandidateAlive && (life.ClearedDefend || life.SkipAction || !life.Before.Same(life.After) ||
                    life.PostureBefore != life.PostureAfter) ? Step.Lifecycle : Step.Departure);
                return;
            }
            if (step == Step.Lifecycle) watchdog.Tick();
            else if (!expected.Completion.IsCompleted && clock.Now >= deadline)
            { Fail("TurnEntry final gate timed out without rendered proof."); return; }
            if (disposed || !CheckOwner()) return;
            port.Tick();
            if (disposed || !Observe() || !CheckOwner() || !expected.Completion.IsCompleted) return;
            var terminal = expected.Completion.Result;
            if (terminal.Kind != PresentationReceiptTerminalKind.Completed &&
                !(step == Step.Lifecycle && terminal.Kind == PresentationReceiptTerminalKind.Degraded))
            { Fail("TurnEntry gate lacks a valid terminal: " + terminal.Detail); return; }
            double minimum = step == Step.Lifecycle ? .45d : step == Step.Formation ? .30d : 0d;
            if (clock.Now - issuedAt < minimum || clock.RenderedFrame <= issuedFrame) return;
            Degraded |= terminal.Kind == PresentationReceiptTerminalKind.Degraded;
            if (step == Step.Formation) ReadyForFinalization = true;
            else { ordinal++; Issue(step == Step.Lifecycle ? Step.Departure : Step.Formation); }
        }
        catch (Exception error) { Fail(error.Message); }
        finally { ticking = false; }
    }

    private void Begin()
    {
        if (port == null || !ReferenceEquals(port.Clock, clock) ||
            !port.TryCaptureHomes(lease.Owner, out var homes))
        { Fail("TurnEntry requires an explicit matching presentation binding and frozen homes."); return; }
        if (disposed || !CheckOwner()) return;
        if (!state.TryCommitTurnEntry(lease, reservation, homes, out var ledger, out var error, continuation))
        { Fail(error); return; }
        Ledger = ledger; expectedRevision = ledger.CommittedRevision;
        if (disposed || !CheckOwner()) return;
        var plan = new DRPGSettlementPresentationPlan(ledger);
        ordinal = Math.Max(1, plan.Steps.Count); step = Step.Items;
        items = new DRPGBattleSettlementSequence(plan, port, clock, lease);
        if (disposed) { items.Dispose(); return; }
        if (!CheckOwner()) return;
        if (items.Fault.Length != 0) Fail(items.Fault);
    }

    private void Issue(Step next)
    {
        step = next; expected = null; issuedAt = clock.Now; issuedFrame = clock.RenderedFrame;
        PresentationReceipt receipt;
        if (step == Step.Lifecycle) receipt = port.PresentLifecycle(Ledger, ordinal);
        else if (step == Step.Departure)
        {
            var facts = DRPGSettlementDepartureFacts.Describe(Ledger);
            departure = port.CompleteDefeats(facts, ordinal);
            if (departure != null && departure.Receipt.Owner == lease.Owner) expected = departure.Receipt;
            if (departure == null || !facts.HasSameValues(departure.Departures))
            { Fail("TurnEntry departure provenance changed."); return; }
            receipt = departure.Receipt;
        }
        else
        {
            if (!state.TryReconcileSettlement(lease, Ledger, departure, out var rules, out var error))
            { Fail(error); return; }
            Reconciliation = rules; expectedRevision = rules.CommittedRevision;
            receipt = port.ReconcileFormation(Ledger, rules, ordinal);
        }
        if (receipt != null && receipt.Owner == lease.Owner) expected = receipt;
        if (receipt == null || receipt.Owner != lease.Owner || receipt.Stage != DRPGBattleActionStage.TurnBegin || receipt.StepOrdinal != ordinal)
        { Fail("TurnEntry receipt owner/stage/ordinal conflict."); return; }
        expected = receipt;
        if (disposed) { CancelExpected(); return; }
        if (!Observe() || !CheckOwner()) return;
        deadline = issuedAt + (step == Step.Formation ? 2.30d : step == Step.Lifecycle ? 2.45d : 2d);
        if (step == Step.Lifecycle)
            watchdog.Watch(receipt, deadline, "TurnEntryLifecycle", () => CancelLeaf(receipt, "TurnEntry lifecycle timeout"));
    }

    private bool CheckOwner()
    {
        if (disposed) return false;
        if (!OwnsState() || scopeStillOwned == null || !scopeStillOwned() || !OwnsState())
        { Fail("TurnEntry lost its exact owner, candidate binding or rule revision."); return false; }
        return !disposed;
    }
    private bool OwnsState() => lease.Owner.Kind == BattleTransactionKind.TurnEntry &&
        state.OwnsRuleMutationLease(lease, lease.Owner) && state.TurnCandidates.AllowsPinnedWork(lease) &&
        state.BattleRevision == expectedRevision;
    private bool Observe()
    {
        double now = clock.Now; long frame = clock.RenderedFrame;
        if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 || now < observedAt || frame < observedFrame)
        { Fail("TurnEntry requires a monotonic finite presentation clock."); return false; }
        observedAt = now; observedFrame = frame; return true;
    }
    private void Fail(string error)
    { if (Fault.Length == 0) Fault = string.IsNullOrEmpty(error) ? "TurnEntry faulted." : error; Dispose(); }
    private void CancelLeaf(PresentationReceipt receipt, string reason)
    {
        if (cancelling) return;
        cancelling = true;
        try { port?.CancelReceipt(receipt, reason); }
        finally { cancelling = false; }
    }
    private void CancelExpected()
    {
        if (expected == null || expected.Completion.IsCompleted) return;
        try { CancelLeaf(expected, "TurnEntry disposed"); }
        catch (Exception error) { Fault += " | Cleanup: " + error.Message; }
        finally { expected.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "TurnEntry disposed")); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { items?.Dispose(); } catch (Exception error) { Fault += " | Item cleanup: " + error.Message; }
        CancelExpected();
        // The continuation/fault owner, not a leaf or this module, releases or
        // transfers the domain lease after its outcome/publication decision.
    }
}
