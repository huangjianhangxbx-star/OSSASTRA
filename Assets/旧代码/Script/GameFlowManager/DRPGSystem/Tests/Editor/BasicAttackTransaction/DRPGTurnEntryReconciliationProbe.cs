#if UNITY_EDITOR
using System;
using UnityEditor;

internal static class DRPGTurnEntryReconciliationProbe
{
    [MenuItem("Tools/ANKALUTE/DRPG/Run Turn Entry Reconciliation Probes")]
    public static void RunFromMenu()
    {
        LethalCandidateLeavesTimelineOnlyAfterTypedDeparture();
        NonterminalOrDegradedDepartureCannotCommit();
        WrongCandidateOrViewCoverageCannotCommit();
        UncommittedLedgerCannotAuthorizeReconciliation();
        ReplayIsOnceAndRequiresTheOriginalReceipt();
        InterveningRevisionAndReacquiredLeaseCannotReplay();
        EmptyDepartureStillCompactsBothSidesOnce();
        UnityEngine.Debug.Log("PASS Turn Entry reconciliation: 7 checks");
    }

    // Break caught: a TurnEntry death is never removed, or is removed before
    // its typed rendered departure; no ActionId is available to distinguish it.
    private static void LethalCandidateLeavesTimelineOnlyAfterTypedDeparture()
    {
        foreach (bool enemy in new[] { false, true })
        using (var f = new DRPGTurnEntrySettlementProbe.Fixture())
        {
            var candidate = enemy ? f.Enemy : f.Player;
            f.Status(candidate, "fatal", -200); f.Begin(candidate);
            Check(f.Commit(out var ledger, out var error), error);
            long revision = f.State.BattleRevision;
            Check(HasCandidate(f, candidate.RuntimeId) && !candidate.IsAlive, "Rule death prematurely removed its timeline entry.");
            var departure = Completed(ledger);
            Check(f.State.TryReconcileSettlement(f.Lease, ledger, departure, out var result, out error), error);
            Check(!HasCandidate(f, candidate.RuntimeId) && result.CommittedRevision == revision + 1 &&
                result.Departures.Owner == f.Lease.Owner && result.Departures.Stage == DRPGBattleActionStage.TurnBegin &&
                f.Lease.IsActive && !ledger.Identity.IsValid && f.Core.Scheduler.CurrentTime == 0,
                "Shared reconciliation lost TurnEntry ownership, replayed time, or released the command lock.");
        }
    }

    // Break caught: a skipped/failed presentation is treated as proof of departure.
    private static void NonterminalOrDegradedDepartureCannotCommit()
    {
        using (var f = Fatal())
        {
            Check(f.Commit(out var ledger, out var error), error); long revision = f.State.BattleRevision;
            var pending = new DRPGBattleDeparturePresentation(DRPGSettlementDepartureFacts.Describe(ledger));
            Check(!f.State.TryReconcileSettlement(f.Lease, ledger, pending, out _, out _), "Pending departure accepted.");
            foreach (var kind in new[] { PresentationReceiptTerminalKind.Degraded, PresentationReceiptTerminalKind.Faulted })
            {
                var bad = Completed(ledger, kind);
                Check(!f.State.TryReconcileSettlement(f.Lease, ledger, bad, out _, out _), "Non-rendered terminal accepted.");
            }
            Check(f.State.BattleRevision == revision && HasCandidate(f, f.Player.RuntimeId), "Rejected departure partially mutated rules.");
        }
    }

    private static void WrongCandidateOrViewCoverageCannotCommit()
    {
        using (var f = Fatal())
        {
            Check(f.Commit(out var ledger, out var error), error); long revision = f.State.BattleRevision;
            var facts = DRPGSettlementDepartureFacts.Describe(ledger);
            var foreign = new DRPGBattleDepartureJournal(BattleTransactionIdentity.ForTurnEntry(
                new TurnEntryIdentity(f.Entry.BattleGeneration, 2, f.Entry.Actor)), facts.SourceFingerprint, facts.Members, facts.Stage);
            var badOwner = new DRPGBattleDeparturePresentation(foreign);
            badOwner.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "probe"));
            Check(!f.State.TryReconcileSettlement(f.Lease, ledger, badOwner, out _, out _), "Different candidate with empty ActionId was accepted.");
            foreach (int variant in new[] { 0, 1, 2 })
            {
                var bad = new DRPGBattleDeparturePresentation(facts);
                if (variant != 0) bad.RecordView(new DRPGDepartureViewFact(f.Entry.Actor, variant == 1 ? 0 : 123, 0));
                bad.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "probe"));
                Check(!f.State.TryReconcileSettlement(f.Lease, ledger, bad, out _, out _), "Missing original view or ally incap model was accepted.");
            }
            Check(f.State.BattleRevision == revision, "Malformed departure changed state.");
        }
    }

    private static void UncommittedLedgerCannotAuthorizeReconciliation()
    {
        using (var f = Fatal())
        {
            var draft = f.State.CreateWorkspace();
            var uncommitted = DRPGBattleSettlementLedger.Build(f.Lease.Owner, f.Entry.Actor, draft, f.Homes);
            Check(!f.State.TryReconcileSettlement(f.Lease, uncommitted, Completed(uncommitted), out _, out _) && f.Player.IsAlive,
                "Uncommitted settlement authorized death reconciliation.");
        }
    }

    private static void ReplayIsOnceAndRequiresTheOriginalReceipt()
    {
        using (var f = Fatal())
        {
            Check(f.Commit(out var ledger, out var error), error); var departure = Completed(ledger);
            Check(f.State.TryReconcileSettlement(f.Lease, ledger, departure, out var first, out error), error);
            Check(f.State.TryReconcileSettlement(f.Lease, ledger, departure, out var replay, out error) && ReferenceEquals(first, replay), error);
            Check(!f.State.TryReconcileSettlement(f.Lease, ledger, Completed(ledger), out _, out _) &&
                f.State.BattleRevision == first.CommittedRevision, "Replaced receipt replayed a rule mutation.");
        }
    }

    private static void InterveningRevisionAndReacquiredLeaseCannotReplay()
    {
        using (var f = Fatal())
        {
            Check(f.Commit(out var ledger, out var error), error); var departure = Completed(ledger);
            Check(f.State.TryReconcileSettlement(f.Lease, ledger, departure, out var first, out error), error);
            Check(f.State.TryCommitJournal(f.Lease, f.State.CreateWorkspace()).Succeeded, "Cannot commit intervening mutation.");
            Check(!f.State.TryReconcileSettlement(f.Lease, ledger, departure, out _, out _), "Replay concealed intervening revision.");
        }
        using (var f = Fatal())
        {
            Check(f.Commit(out var ledger, out var error), error); var owner = f.Lease.Owner;
            f.Lease.Dispose(); Check(f.Arbiter.TryAcquire(owner, out var replacement, out error), error);
            using (replacement)
                Check(!f.State.TryReconcileSettlement(replacement, ledger, Completed(ledger), out _, out _) && HasCandidate(f, f.Player.RuntimeId),
                    "A new physical lease reused an old settlement's authority.");
        }
    }

    private static void EmptyDepartureStillCompactsBothSidesOnce()
    {
        using (var f = new DRPGTurnEntrySettlementProbe.Fixture())
        {
            var draft = f.State.CreateWorkspace();
            draft.For(new ParticipantId(f.Player.RuntimeId)).SetFormationRow(DRPGFormationRow.Middle);
            draft.For(new ParticipantId(f.Enemy.RuntimeId)).SetFormationRow(DRPGFormationRow.Rear);
            Check(f.State.TryCommitJournal(f.Core.Lease, draft).Succeeded, "Cannot seed sparse rows.");
            f.Begin(f.Player); Check(f.Commit(out var ledger, out var error), error);
            var departure = Completed(ledger);
            Check(f.State.TryReconcileSettlement(f.Lease, ledger, departure, out var rules, out error), error);
            Check(rules.Departures.Members.Count == 0 && rules.FormationChanges.Count == 2 &&
                f.Player.FormationRow == DRPGFormationRow.Front && f.Enemy.FormationRow == DRPGFormationRow.Front &&
                HasCandidate(f, f.Player.RuntimeId) && HasCandidate(f, f.Enemy.RuntimeId) && f.Lease.IsActive,
                "Empty departure bypassed whole-roster compaction or changed candidates.");
        }
    }

    private static DRPGTurnEntrySettlementProbe.Fixture Fatal()
    {
        var f = new DRPGTurnEntrySettlementProbe.Fixture();
        f.Status(f.Player, "fatal", -200); f.Begin(f.Player); return f;
    }
    internal static DRPGBattleDeparturePresentation Completed(DRPGBattleSettlementLedger ledger,
        PresentationReceiptTerminalKind kind = PresentationReceiptTerminalKind.Completed)
    {
        var facts = DRPGSettlementDepartureFacts.Describe(ledger);
        var receipt = new DRPGBattleDeparturePresentation(facts); int view = 100;
        foreach (var member in facts.Members)
            receipt.RecordView(new DRPGDepartureViewFact(member.Participant, ++view, member.Side == DRPGBattleSide.Player ? view + 1000 : 0));
        receipt.Receipt.TryComplete(new PresentationReceiptTerminal(kind, "probe")); return receipt;
    }
    private static bool HasCandidate(DRPGTurnEntrySettlementProbe.Fixture f, int id)
    {
        foreach (var entry in f.State.CaptureRuleSnapshot().Timeline) if (entry.Participant.Value == id) return true;
        return false;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
