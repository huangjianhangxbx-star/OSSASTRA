#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal static class DRPGTurnCandidateAuthorityProbe
{
    // Break caught: Session and the entry runner use separate reservations or
    // reserve mutates timeline/time before its transaction begins.
    private static void ReserveIsPureAndHasOneExactOwner()
    {
        using (var f = DRPGBattleStateMutationFixture.Create())
        {
            var source = f.State.TurnCandidates; long revision = f.State.BattleRevision;
            Check(source.TryReserve(out var r, out var error), error);
            Check(r.Candidate.Participant == f.Player && r.ReservedLogicalTime == 10 && source.HasReservation &&
                f.Scheduler.Count == 2 && f.Scheduler.CurrentTime == 0 && f.State.BattleRevision == revision,
                "Reservation changed rule state or selected the wrong candidate.");
            Check(!source.TryReserve(out _, out _) && source.Matches(r), "Second reservation replaced the first.");
            var near = new DRPGTurnCandidateReservation(r.ReservationId, r.SourceKind, r.Candidate, r.ReservedLogicalTime + .00001);
            Check(!source.Matches(near) && !source.TryDiscardLegacy(near, out _) && source.Matches(r),
                "Approximate numeric equality accepted a forged reservation.");
            Check(source.TryDiscardLegacy(r, out _) && source.TryReserve(out var next, out _) && next.ReservationId > r.ReservationId,
                "Discard reused identity or consumed the scheduler.");
        }
    }

    // Break caught: active TurnEntry can still be consumed through the old
    // synchronous Session bridge before its rendered final gates.
    private static void PinBlocksLegacyCommitDiscardAndReacquiredLease()
    {
        using (var f = DRPGBattleStateMutationFixture.Create())
        {
            var source = f.State.TurnCandidates;
            Check(source.TryReserve(out var r, out var error), error); f.Lease.Dispose();
            var owner = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(f.BattleGeneration, 1, new ParticipantId(f.Player.RuntimeId)));
            Check(f.Arbiter.TryAcquire(owner, out var lease, out error), error);
            using (lease)
            {
                Check(source.TryPin(lease, r, out error), error);
                Check(!source.TryCommitLegacy(r, out _, out _) && !source.TryDiscardLegacy(r, out _) && f.Scheduler.Count == 2,
                    "Legacy bridge consumed a transaction-owned candidate.");
            }
            Check(f.Arbiter.TryAcquire(owner, out var replacement, out error), error);
            using (replacement) Check(!source.TryPin(replacement, r, out _), "Reacquiring the same identity stole the physical candidate pin.");
        }
    }

    private static void LegacyBridgeStillConsumesExactlyOnce()
    {
        using (var f = DRPGBattleStateMutationFixture.Create())
        {
            var source = f.State.TurnCandidates;
            Check(source.TryReserve(out var r, out var error), error);
            using (f.State.OpenCompatibilityMutationScope(f.Lease))
                Check(source.TryCommitLegacy(r, out var plan, out error) && plan == null, error);
            Check(f.Scheduler.Count == 1 && f.Scheduler.CurrentTime == 10 && !source.HasReservation &&
                !source.TryCommitLegacy(r, out _, out _), "Legacy candidate consumption was repeated or lost.");
        }
    }

    private static void SessionAndStateShareTheSameReservation()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            var source = f.Session.TransactionState.TurnCandidates;
            Check(f.Session.TryReserveNextTurnCandidate(out var reservation, out var error), error);
            Check(source.Matches(reservation) && !source.TryReserve(out _, out _),
                "Session and State opened two independent candidate reservations.");
            Check(f.Session.TryDiscardTurnCandidate(reservation, out error) && !source.HasReservation, error);
            Check(source.TryReserve(out var next, out error) && f.Session.TryDiscardTurnCandidate(next, out error),
                "Session cannot consume the same State reservation: " + error);
        }
    }

    private static void LegacyTimelineShiftFeedsTheSharedCandidatePendingState()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            var actor = f.Session.CurrentActor;
            var source = f.Session.TransactionState.TurnCandidates;
            Check(f.Session.TransactionArbiter.TryAcquire(f.Owner, out var lease, out var error), error);
            using (lease)
            using (f.Session.TransactionState.OpenCompatibilityMutationScope(lease))
                Check(f.Session.TryApplyTimelineShift(actor, 7, 3, out _, out _, out error), error);
            Check(source.PendingTimelineDeltas.TryGetValue(actor.RuntimeId, out var delta) && delta == 7 &&
                source.PendingTimelinePriorities.TryGetValue(actor.RuntimeId, out var priority) && priority == 3,
                "Legacy Session kept pending timeline effects outside candidate state.");
        }
    }

    private static void CommittedEntryAutomaticallyPinsItsReservation()
    {
        using (var f = new DRPGTurnEntrySettlementProbe.Fixture())
        {
            var source = f.State.TurnCandidates;
            Check(source.TryReserve(out var reservation, out var error), error); f.Begin(f.Player);
            Check(f.State.TryCommitTurnEntry(f.Lease, reservation, f.Homes, out var ledger, out error), error);
            Check(!source.TryDiscardLegacy(reservation, out _) && source.Matches(reservation),
                "Committed TurnEntry left its candidate available to legacy discard.");
            Check(f.State.TryCommitTurnEntry(f.Lease, reservation, f.Homes, out var replay, out error) && ReferenceEquals(ledger, replay), error);
        }
    }

    private static void ExecutablePlanIsPinnedByReferenceNotReadyTime()
    {
        var skill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
        using (var f = DRPGBattleStateMutationFixture.Create())
        {
            var source = f.State.TurnCandidates;
            var plan = DRPGEnemyActionPlan.CreateAttack(1, f.Enemy.RuntimeId, skill, 0, 50, 0)
                .CommitTarget(f.Player.RuntimeId, 0).WithSatisfiedPlayerResponse(1);
            source.Plans.Add(f.Enemy.RuntimeId, plan);
            Check(source.TryReserve(out var r, out var error), error);
            Check(r.Candidate.Participant == f.Enemy && r.SourceKind == DRPGTurnCandidateSourceKind.ExecutableEnemyPlan &&
                r.ReservedLogicalTime == 0 && r.Candidate.ScheduledTime == 50, "Executable was incorrectly inferred from time.");
            source.Plans[f.Enemy.RuntimeId] = plan.MarkExecuting();
            using (f.State.OpenCompatibilityMutationScope(f.Lease))
                Check(!source.TryCommitLegacy(r, out _, out _), "A replaced plan silently supplied the reserved action.");
            source.Plans[f.Enemy.RuntimeId] = plan;
            using (f.State.OpenCompatibilityMutationScope(f.Lease))
                Check(source.TryCommitLegacy(r, out var actual, out error) && ReferenceEquals(plan, actual), error);
            Check(f.Scheduler.Count == 2 && f.Scheduler.CurrentTime == 0 && source.Plans.Count == 0,
                "Executable consumption changed unrelated scheduler time or entry.");
        }
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); }
    }

    private static void ReplacedPinnedPlanCannotCommitAnotherRuleJournal()
    {
        var skill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            using (var f = new DRPGTurnEntrySettlementProbe.Fixture())
            {
                var source = f.State.TurnCandidates;
                var plan = DRPGEnemyActionPlan.CreateAttack(1, f.Enemy.RuntimeId, skill, 0, 50, 0)
                    .CommitTarget(f.Player.RuntimeId, 0).WithSatisfiedPlayerResponse(1);
                source.Plans.Add(f.Enemy.RuntimeId, plan);
                Check(source.TryReserve(out var reservation, out var error), error); f.Begin(f.Enemy);
                Check(f.State.TryCommitTurnEntry(f.Lease, reservation, f.Homes, out _, out error), error);
                long revision = f.State.BattleRevision;
                var draft = f.State.CreateWorkspace(); draft.For(f.Entry.Actor).SetHitPoints(50);
                source.Plans[f.Enemy.RuntimeId] = plan.MarkExecuting();
                Check(!f.State.TryCommitJournal(f.Lease, draft).Succeeded && f.Enemy.CurrentHp == 120 && f.State.BattleRevision == revision,
                    "A replaced pinned plan could still mutate the candidate's rule state.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); }
    }
    private static void ReplacedPinnedPlanStopsPresentationAndKeepsOwnership()
    {
        var skill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            using (var f = new DRPGTurnEntrySettlementProbe.Fixture())
            {
                var source = f.State.TurnCandidates;
                var plan = DRPGEnemyActionPlan.CreateAttack(1, f.Enemy.RuntimeId, skill, 0, 50, 0)
                    .CommitTarget(f.Player.RuntimeId, 0).WithSatisfiedPlayerResponse(1);
                source.Plans.Add(f.Enemy.RuntimeId, plan);
                Check(source.TryReserve(out var reservation, out var error), error); f.Begin(f.Enemy);
                var clock = new DRPGSettlementPresentationProbe.FixtureClock();
                var port = new ObservedPort(f.Homes, clock);
                using (var module = new DRPGBattleTurnEntryModule(f.State, f.Lease, reservation, port, clock, () => true))
                {
                    module.Tick();
                    Check(module.Ledger != null && port.Issued == 1, "Entry did not start its real settlement sequence.");
                    long revision = f.State.BattleRevision;
                    source.Plans[f.Enemy.RuntimeId] = plan.MarkExecuting();
                    clock.Now = .31; clock.RenderedFrame++;
                    module.Tick();
                    Check(module.Fault.Length != 0 && port.Issued == 1 && port.Cancelled == 1 &&
                        !module.ReadyForFinalization && f.Lease.IsActive && source.Matches(reservation) && f.State.BattleRevision == revision,
                        "Stale plan continued presentation, lost owned cleanup, or released the candidate.");
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); }
    }

    // Only the external render receipt is controlled; candidate, module and
    // settlement sequence are the production implementations.
    private sealed class ObservedPort : IBattleTurnEntryPresentationPort
    {
        private readonly IReadOnlyDictionary<ParticipantId, int> homes;
        internal int Issued, Cancelled;
        internal ObservedPort(IReadOnlyDictionary<ParticipantId, int> homes, IDRPGBattlePresentationClock clock)
        { this.homes = homes; Clock = clock; }
        public IDRPGBattlePresentationClock Clock { get; }
        public bool TryCaptureHomes(BattleTransactionIdentity owner, out IReadOnlyDictionary<ParticipantId, int> value)
        { value = homes; return true; }
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command)
        { Issued++; return new PresentationReceipt(command.Owner, DRPGBattleActionStage.TurnBegin, command.StepOrdinal); }
        public PresentationReceipt PresentLifecycle(DRPGBattleSettlementLedger ledger, int ordinal)
        { Issued++; return new PresentationReceipt(ledger.Owner, DRPGBattleActionStage.TurnBegin, ordinal); }
        public DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal facts, int ordinal)
        { Issued++; return new DRPGBattleDeparturePresentation(facts, ordinal); }
        public PresentationReceipt ReconcileFormation(DRPGBattleSettlementLedger ledger, DRPGBattlePostReturnResult rules, int ordinal)
        { Issued++; return new PresentationReceipt(ledger.Owner, DRPGBattleActionStage.TurnBegin, ordinal); }
        public void Tick() { }
        public void CancelReceipt(PresentationReceipt receipt, string reason) { Cancelled++; }
    }
    private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
}
#endif
