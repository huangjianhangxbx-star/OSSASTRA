#if UNITY_EDITOR
using System;
using UnityEngine;

internal static class DRPGTurnEntryFormationProbe
{
    // Break caught: after a candidate death the remaining actors teleport,
    // start in different batches, reuse a render fence, or lose their full owner.
    private static void CandidateDeathUsesOneSmoothWholeRosterBarrier()
    {
        foreach (bool enemy in new[] { false, true })
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(enemy, true, fullBattlefield: true))
        using (var sequence = f.Start())
        using (var motion = new BattleActorMotionController(f.Projection, f.Clock))
        {
            var origins = new System.Collections.Generic.Dictionary<int, Vector3>();
            foreach (var view in f.Domain.Roster.ActiveViews) origins.Add(view.RuntimeId, view.transform.position);
            f.Step(sequence, .451);
            var departure = f.Presenter.CompleteDefeats(DRPGSettlementDepartureFacts.Describe(f.Ledger), f.Defeated);
            f.Presenter.Tick(); Advance(f); Advance(f);
            Check(departure.Receipt.Completion.IsCompleted, "Actual candidate departure not rendered.");
            Check(f.Domain.Session.TransactionState.TryReconcileSettlement(f.RuleLease, f.Ledger, departure, out var rules, out var error), error);
            Check(rules.FormationChanges.Count == 2, "Candidate death did not compact its two surviving rows.");
            using (var barrier = new BattleFormationReconcileBarrier(f.Projection, motion, f.Clock, f.Ledger, rules))
            {
                Check(!barrier.Receipt.Completion.IsCompleted, "Whole formation jumped to terminal on issuance.");
                foreach (var view in f.Domain.Roster.ActiveViews) Check(view.transform.position == origins[view.RuntimeId], "Formation teleported on issuance.");
                f.Clock.Now += .15; f.Clock.RenderedFrame++; motion.Tick(); barrier.Tick();
                foreach (var change in rules.FormationChanges)
                {
                    f.Projection.TryGetView(change.Participant.Value, out var view);
                    Check(view.transform.position != origins[view.RuntimeId], "A survivor missed the common movement start.");
                }
                Check(!barrier.Receipt.Completion.IsCompleted, "Formation skipped minimum duration.");
                f.Clock.Now += .151; f.Clock.RenderedFrame++; motion.Tick(); barrier.Tick();
                Check(!barrier.Receipt.Completion.IsCompleted, "Endpoint update reused the old render fence.");
                f.Clock.Now += .01; f.Clock.RenderedFrame++; motion.Tick(); barrier.Tick();
                Check(barrier.Receipt.Completion.IsCompleted && barrier.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    barrier.Receipt.Owner == f.RuleLease.Owner && barrier.Receipt.Stage == DRPGBattleActionStage.TurnBegin &&
                    f.RuleLease.IsActive && f.Completed == 0, "Formation completion released the candidate or borrowed an ActionId.");
                foreach (var write in motion.Journal)
                    Check(write.Transaction == f.RuleLease.Owner && !write.Owner.IsValid && write.Stage == DRPGBattleActionStage.TurnBegin,
                        "Motion write lost its complete TurnEntry identity.");
                var homes = f.Projection.Capture(DRPGBattleProjectionCheckpointKind.PostSettlementReconcileCheckpoint);
                Check(homes.Actors.Count == 5, "Departure remained in the active Home checkpoint.");
                for (int i = 0; i < homes.Actors.Count; i++)
                for (int j = i + 1; j < homes.Actors.Count; j++) Check(homes.Actors[i].Home.Position != homes.Actors[j].Home.Position, "Shared Home overlap.");
            }
        }
    }

    // Break caught: two candidates both have default ActionIdentity; old motion
    // checks must not let an empty legacy command move either candidate's view.
    private static void EmptyActionAndForeignCandidateCannotMovePinnedViews()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(statuses: false))
        using (var motion = new BattleActorMotionController(f.Projection, f.Clock))
        {
            var target = Target(f); var before = f.CandidateView.transform.position;
            bool rejected = false;
            try { rejected = Faulted(motion.MoveFormation(default(ActionIdentity), target, f.Clock.Now)); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Empty ActionIdentity acquired TurnEntry formation motion.");
            var foreign = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(f.Entry.BattleGeneration,
                f.Entry.CandidateSerial + 1, f.Entry.Actor));
            Check(Faulted(motion.MoveFormation(foreign, target, f.Clock.Now, DRPGBattleActionStage.TurnBegin)), "Foreign candidate moved a pinned view.");
            f.Clock.Now += .31; f.Clock.RenderedFrame++; motion.Tick();
            Check(f.CandidateView.transform.position == before, "Rejected command left an active tween.");
        }
    }

    private static void ClosingProjectionCancelsCandidateMotionWithoutLateWrites()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(statuses: false))
        using (var motion = new BattleActorMotionController(f.Projection, f.Clock))
        {
            var receipt = motion.MoveFormation(f.RuleLease.Owner, Target(f), f.Clock.Now, DRPGBattleActionStage.TurnBegin);
            Check(!receipt.Completion.IsCompleted, "Candidate motion did not start.");
            f.Clock.Now += .1; f.Clock.RenderedFrame++; motion.Tick();
            f.Projection.Dispose(); var afterCleanup = f.CandidateView.transform.position;
            f.Clock.Now += 1; f.Clock.RenderedFrame++; motion.Tick();
            Check(Faulted(receipt) && f.CandidateView.transform.position == afterCleanup && f.RuleLease.IsActive,
                "Late candidate motion survived projection cleanup or released rule ownership.");
        }
    }

    private static void SettlementJournalFreezesTheFinalWorkspaceRows()
    {
        using (var f = new DRPGTurnEntrySettlementProbe.Fixture())
        {
            f.Begin(f.Player); var draft = f.State.CreateWorkspace();
            draft.For(f.Entry.Actor).SetFormationRow(DRPGFormationRow.Rear);
            var ledger = DRPGBattleSettlementLedger.Build(f.Lease.Owner, f.Entry.Actor, draft, f.Homes);
            Check(ledger.MutationJournal != null, "No frozen settlement journal for formation planning.");
            draft.For(f.Entry.Actor).SetFormationRow(DRPGFormationRow.Middle);
            Check(ledger.MutationJournal.ParticipantMutations.Count == 1 &&
                ledger.MutationJournal.ParticipantMutations[0].FormationRow == DRPGFormationRow.Rear &&
                f.Player.FormationRow == DRPGFormationRow.Front, "Frozen settlement rows alias a later workspace or mutate live rules.");
        }
    }

    // Layout consumes the frozen row, not the mutable draft or the initial row.
    private static void FormationPlanConsumesSettlementRowsAndRejectsAnotherOwner()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(statuses: false))
        {
            var draft = f.Domain.Session.TransactionState.CreateWorkspace();
            draft.For(f.Entry.Actor).SetFormationRow(DRPGFormationRow.Rear);
            var homes = new System.Collections.Generic.Dictionary<ParticipantId, int> {
                { new ParticipantId(f.Domain.Player.RuntimeId), 0 }, { new ParticipantId(f.Domain.Enemy.RuntimeId), 0 } };
            var ledger = DRPGBattleSettlementLedger.Build(f.RuleLease.Owner, f.Entry.Actor, draft, homes);
            var rules = new DRPGBattlePostReturnResult(DRPGSettlementDepartureFacts.Describe(ledger), ledger.CommittedRevision + 1,
                Array.Empty<DRPGBattleFormationChange>());
            draft.For(f.Entry.Actor).SetFormationRow(DRPGFormationRow.Middle);
            var placements = f.Projection.FrozenFormation.Plan(ledger.Owner, ledger.MutationJournal, rules, f.Projection);
            foreach (var placement in placements)
                Check(placement.Row == (placement.RuntimeId == f.Candidate.RuntimeId ? DRPGFormationRow.Rear : DRPGFormationRow.Front),
                    "Layout ignored frozen settlement rows or read the later draft.");
            var other = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(f.Entry.BattleGeneration, f.Entry.CandidateSerial + 1, f.Entry.Actor));
            bool rejected = false;
            try { f.Projection.FrozenFormation.Plan(other, ledger.MutationJournal, rules, f.Projection); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Layout accepted another candidate's row journal.");
        }
    }

    private static void EmptyFormationCannotHideAReplacedDepartureStage()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(statuses: false))
        using (var motion = new BattleActorMotionController(f.Projection, f.Clock))
        {
            var fakeFacts = new DRPGBattleDepartureJournal(f.Ledger.Owner, f.Ledger.Fingerprint,
                Array.Empty<DRPGBattleDepartureMember>(), DRPGBattleActionStage.Return);
            var rules = new DRPGBattlePostReturnResult(fakeFacts, f.Ledger.CommittedRevision + 1,
                Array.Empty<DRPGBattleFormationChange>());
            using (var barrier = new BattleFormationReconcileBarrier(f.Projection, motion, f.Clock, f.Ledger, rules))
                Check(Faulted(barrier.Receipt) && motion.Journal.Count == 0 && f.RuleLease.IsActive,
                    "Empty formation accepted a replaced TurnEntry departure stage.");
        }
    }

    private static DRPGWorldFormationPlacement Target(DRPGTurnEntryPresentationProbe.Fixture f) =>
        new DRPGWorldFormationPlacement(f.Candidate.RuntimeId, DRPGFormationRow.Front, f.Home + Vector3.right,
            f.CandidateView.transform.localScale.x, 1, 0);
    private static void Advance(DRPGTurnEntryPresentationProbe.Fixture f)
    { f.Clock.Now += .01; f.Clock.RenderedFrame++; f.Presenter.Tick(); }
    private static bool Faulted(PresentationReceipt receipt) => receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
