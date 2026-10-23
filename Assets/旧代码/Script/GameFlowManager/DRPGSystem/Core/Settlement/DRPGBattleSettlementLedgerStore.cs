using System;
using System.Collections.Generic;

// One store per authoritative State, not per presenter or per invocation.
internal sealed class DRPGBattleSettlementLedgerStore
{
    private readonly DRPGBattleSessionState state;
    private readonly Dictionary<BattleTransactionIdentity, Entry> committed = new Dictionary<BattleTransactionIdentity, Entry>();
    private bool building;
    internal DRPGBattleSettlementLedgerStore(DRPGBattleSessionState state) { this.state = state; }

    internal bool TryCommit(BattleRuleMutationLease lease, ParticipantId actor,
        IReadOnlyDictionary<ParticipantId, int> homes, out DRPGBattleSettlementLedger ledger, out string error,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation = null, DRPGTurnCandidateReservation? turnEntry = null)
    {
        ledger = null; error = string.Empty;
        if (building) { error = "Settlement is already resolving."; return false; }
        if (lease == null || (lease.Owner.Kind != BattleTransactionKind.RootAction && lease.Owner.Kind != BattleTransactionKind.TurnEntry) ||
            !state.OwnsRuleMutationLease(lease, lease.Owner))
        { error = "Settlement requires this State's active Root or TurnEntry mutation lease."; return false; }
        var identity = lease.Owner;
        if (committed.TryGetValue(identity, out var previous))
        {
            if (!ReferenceEquals(previous.Lease, lease) || state.BattleRevision != previous.Ledger.CommittedRevision)
            { error = "Settlement replay cannot conceal an intervening rule revision."; return false; }
            if (!previous.Ledger.MatchesRequest(actor, homes, continuation) || !SameReservation(previous.Reservation, turnEntry))
            { error = "Settlement replay conflicts with its frozen actor or Home Slots."; return false; }
            ledger = previous.Ledger; return true;
        }
        try
        {
            building = true;
            if (turnEntry.HasValue && !state.ValidateTurnEntryReservation(turnEntry.Value, out error)) return false;
            var draft = state.CreateWorkspace();
            var prepared = DRPGBattleSettlementLedger.Build(identity, actor, draft, homes, continuation, turnEntry);
            var entry = new Entry(prepared, lease, turnEntry);
            // All ledger allocation, fingerprinting and cache growth precede the
            // single State commit. Source collection has already finished.
            committed.EnsureCapacity(committed.Count + 1);
            var result = state.TryCommitJournal(lease, draft);
            if (!result.Succeeded) { error = result.Error; return false; }
            committed.Add(identity, entry);
            ledger = prepared; return true;
        }
        catch (Exception exception) { error = exception.Message; return false; }
        finally { building = false; }
    }

    private static bool SameReservation(DRPGTurnCandidateReservation? a, DRPGTurnCandidateReservation? b) =>
        a.HasValue == b.HasValue && (!a.HasValue || a.Value.ReservationId == b.Value.ReservationId &&
            a.Value.SourceKind == b.Value.SourceKind && ReferenceEquals(a.Value.Candidate.Participant, b.Value.Candidate.Participant) &&
            a.Value.Candidate.ScheduledTime == b.Value.Candidate.ScheduledTime && a.Value.Candidate.Priority == b.Value.Candidate.Priority &&
            a.Value.ReservedLogicalTime == b.Value.ReservedLogicalTime);

    // Same post-settlement rule barrier for Root Stage9 and TurnEntry. The
    // committed ledger and physical lease are State-owned provenance, not a
    // caller assertion; a matching-looking uncommitted ledger earns no authority.
    internal bool TryReconcile(BattleRuleMutationLease lease, DRPGBattleSettlementLedger ledger,
        DRPGBattleDeparturePresentation departure, out DRPGBattlePostReturnResult result, out string error)
    {
        result = null; error = string.Empty;
        if (building || ledger == null || lease == null || !state.OwnsRuleMutationLease(lease, ledger.Owner) ||
            !committed.TryGetValue(ledger.Owner, out var entry) || !ReferenceEquals(entry.Ledger, ledger) ||
            !ReferenceEquals(entry.Lease, lease))
        { error = "Reconciliation requires this State's committed settlement and its original active lease."; return false; }
        var facts = entry.Departures;
        if (departure == null || !departure.Departures.HasSameValues(facts) || departure.Receipt.Owner != facts.Owner ||
            departure.Receipt.Stage != facts.Stage || !departure.Receipt.Completion.IsCompleted ||
            departure.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed || departure.Views.Count != facts.Members.Count)
        { error = "Settlement reconciliation requires the exact rendered typed departure receipt."; return false; }
        var views = new HashSet<ParticipantId>(); var instances = new HashSet<int>();
        foreach (var view in departure.Views)
        {
            bool matched = false;
            foreach (var member in facts.Members)
                if (member.Participant == view.Participant && (member.Side == DRPGBattleSide.Player ?
                    view.IncapacitatedModelInstanceId != 0 : view.IncapacitatedModelInstanceId == 0)) matched = true;
            if (!matched || view.OriginalViewInstanceId == 0 || !views.Add(view.Participant) || !instances.Add(view.OriginalViewInstanceId))
            { error = "Settlement departure view identities do not cover the frozen member set."; return false; }
        }
        long expectedRevision = entry.Reconciled != null ? entry.Reconciled.CommittedRevision : ledger.CommittedRevision;
        if (state.BattleRevision != expectedRevision || entry.Reconciled != null && !ReferenceEquals(entry.Receipt, departure))
        { error = "Settlement reconciliation cannot consume an intervening revision or replaced receipt."; return false; }
        if (entry.Reconciled != null) { result = entry.Reconciled; return true; }
        try
        {
            building = true;
            var draft = state.CreateWorkspace();
            foreach (var member in facts.Members)
            {
                draft.RemoveFromTimeline(member.Participant);
                if (member.Side == DRPGBattleSide.Enemy) draft.For(member.Participant).SetDeployed(false);
            }
            var candidate = new DRPGBattlePostReturnResult(facts, checked(expectedRevision + 1), DRPGPostReturnFormationDraft.Stage(draft));
            var commit = state.TryCommitJournal(lease, draft);
            if (!commit.Succeeded) { error = commit.Error; return false; }
            entry.Receipt = departure; entry.Reconciled = candidate; result = candidate; return true;
        }
        catch (Exception exception) { error = exception.Message; return false; }
        finally { building = false; }
    }

    private sealed class Entry
    {
        internal Entry(DRPGBattleSettlementLedger ledger, BattleRuleMutationLease lease, DRPGTurnCandidateReservation? reservation)
        { Ledger = ledger; Lease = lease; Reservation = reservation; Departures = DRPGSettlementDepartureFacts.Describe(ledger); }
        internal readonly DRPGTurnCandidateReservation? Reservation;
        internal readonly DRPGBattleSettlementLedger Ledger;
        internal readonly BattleRuleMutationLease Lease;
        internal readonly DRPGBattleDepartureJournal Departures;
        internal DRPGBattleDeparturePresentation Receipt;
        internal DRPGBattlePostReturnResult Reconciled;
    }
}
