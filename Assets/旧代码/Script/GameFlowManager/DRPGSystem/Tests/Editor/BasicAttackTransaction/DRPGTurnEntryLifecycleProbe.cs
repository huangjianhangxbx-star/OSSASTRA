#if UNITY_EDITOR
using System;
using static DRPGTurnEntrySettlementProbe;

internal static class DRPGTurnEntryLifecycleProbe
{
    // Break caught: actor lifecycle is still mutated synchronously outside the status commit.
    private static void DefendStatusAndClockCommitTogetherWithoutConsumingCandidate()
    {
        using (var t = new Fixture())
        {
            using (t.State.OpenCompatibilityMutationScope(t.Core.Lease)) t.Player.BeginDefend();
            t.Status(t.Player, "entry", -3); var reservation = Reserve(t); t.Begin(t.Player);
            long revision = t.State.BattleRevision;
            Check(t.Player.IsDefending && t.Core.Scheduler.CurrentTime == 0, "Reservation changed live rules.");
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out var ledger, out var error), error);
            Check(!t.Player.IsDefending && t.Player.CurrentHp == 97 && t.State.BattleRevision == revision + 1 &&
                t.Core.Scheduler.CurrentTime == 10 && t.Core.Scheduler.Count == 2 && ledger.Steps.Count == 1 &&
                t.Arbiter.Owns(t.Lease, ledger.Owner), "TurnEntry was not one atomic, owner-held commit with an unconsumed candidate.");
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out var replay, out error) &&
                ReferenceEquals(ledger, replay) && t.Core.Scheduler.CurrentTime == 10 && t.Player.CurrentHp == 97 &&
                t.State.BattleRevision == revision + 1, "Replay consumed clock or status twice: " + error);
        }
    }

    // Break caught: PostureBreak skip remains pending, or ordinary regeneration cancels exposure.
    private static void BrokenCandidateSkipsBeforeAnyPublication()
    {
        using (var t = new Fixture())
        {
            using (t.State.OpenCompatibilityMutationScope(t.Core.Lease)) t.Player.ReducePosture(100);
            var reservation = Reserve(t); t.Begin(t.Player);
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out _, out var error), error);
            Check(t.Player.PostureBreak.State == DRPGPostureBreakState.ExposedAfterSkip && t.Player.CurrentPosture == 0 &&
                t.Core.Scheduler.CurrentTime == 10 && t.Core.Scheduler.Count == 2, "Skip was lost or candidate consumed before its terminal.");
        }
    }

    // Break caught: recovery adds normal regeneration to the configured 50% posture restoration.
    private static void RecoveryIsExactlyHalfAndDoesNotConsumeStability()
    {
        using (var t = new Fixture())
        {
            using (t.State.OpenCompatibilityMutationScope(t.Core.Lease))
            { t.Player.ReducePosture(100); t.Player.ResolvePostureBreakOwnTurnStart(); }
            var reservation = Reserve(t); t.Begin(t.Player);
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out _, out var error), error);
            Check(t.Player.CurrentPosture == 50 && t.Player.PostureBreak.State == DRPGPostureBreakState.RecoveryStability &&
                t.Player.PostureBreak.RemainingStabilityOwnActions == 3, "Recovery amount/stability advanced outside own-action completion.");
        }
    }

    // Break caught: lethal TurnStart still recovers posture or removes the candidate before the death receipt.
    private static void LethalCandidateAdvancesTimeButKeepsItsDeathPendingViewSlot()
    {
        using (var t = new Fixture())
        {
            using (t.State.OpenCompatibilityMutationScope(t.Core.Lease))
            { t.Player.ReducePosture(100); t.Player.ResolvePostureBreakOwnTurnStart(); }
            t.Status(t.Player, "lethal", -200); var reservation = Reserve(t); t.Begin(t.Player);
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out var ledger, out var error), error);
            Check(t.Player.CurrentHp == 0 && t.Player.CurrentPosture == 0 &&
                t.Player.PostureBreak.State == DRPGPostureBreakState.ExposedAfterSkip && t.Core.Scheduler.CurrentTime == 10 &&
                t.Core.Scheduler.Count == 2 && ledger.Steps[0].RuleDefeated, "Death advanced posture or removed the candidate before presentation.");
        }
    }

    // Break caught: invalid frozen homes commit clock/defend before validation fails.
    private static void RejectedLedgerLeavesAllRulesUntouched()
    {
        using (var t = new Fixture())
        {
            using (t.State.OpenCompatibilityMutationScope(t.Core.Lease)) t.Player.BeginDefend();
            t.Status(t.Player, "entry", -3); var reservation = Reserve(t); t.Begin(t.Player);
            long revision = t.State.BattleRevision; var homes = t.Homes; homes.Clear();
            Check(!t.State.TryCommitTurnEntry(t.Lease, reservation, homes, out _, out _) && t.Player.CurrentHp == 100 &&
                t.Player.IsDefending && t.Core.Scheduler.CurrentTime == 0 && t.State.BattleRevision == revision,
                "Rejected TurnEntry leaked partial mutation.");
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out _, out var error) && t.Player.CurrentHp == 97,
                "Rejected attempt poisoned the valid retry: " + error);
        }
    }

    // Break caught: a different timeline candidate or fabricated logical time is accepted by matching ActionIdentity=default.
    private static void ForeignCandidateAndNonfiniteTimeAreRejected()
    {
        using (var t = new Fixture())
        {
            var right = Reserve(t);
            var foreign = new DRPGTurnCandidateReservation(2, DRPGTurnCandidateSourceKind.SchedulerTimeline,
                new DRPGTurnScheduleEntry(t.Enemy, 20, 0), 20);
            var invalid = new DRPGTurnCandidateReservation(3, right.SourceKind, right.Candidate, double.NaN);
            t.Begin(t.Player); long revision = t.State.BattleRevision;
            Check(!t.State.TryCommitTurnEntry(t.Lease, foreign, t.Homes, out _, out _) &&
                !t.State.TryCommitTurnEntry(t.Lease, invalid, t.Homes, out _, out _) &&
                t.State.BattleRevision == revision && t.Core.Scheduler.CurrentTime == 0, "Invalid reservation mutated State.");
        }
    }

    // Break caught: ordinary posture regeneration is omitted or applied to spectators.
    private static void OrdinaryRecoveryIsCandidateOnlyAndFrozen()
    {
        using (var t = new Fixture())
        {
            using (t.State.OpenCompatibilityMutationScope(t.Core.Lease))
            { t.Player.ReducePosture(60); t.Enemy.ReducePosture(40); }
            var reservation = Reserve(t); t.Begin(t.Player);
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out var ledger, out var error), error);
            Check(t.Player.CurrentPosture == 55 && t.Enemy.CurrentPosture == 40 &&
                ledger.TurnEntryLifecycle.PostureBefore == 40 && ledger.TurnEntryLifecycle.PostureAfter == 55,
                "Normal recovery was omitted, doubled, or applied to a noncandidate.");
            var later = t.State.CreateWorkspace(); later.For(t.Entry.Actor).SetPosture(60);
            Check(t.State.TryCommitJournal(t.Lease, later).Succeeded && ledger.TurnEntryLifecycle.PostureAfter == 55,
                "Lifecycle presentation values followed the live participant.");
        }
    }

    // Break caught: causal timeline effects clamp against pre-entry time rather than the once-advanced boundary.
    private static void TimelineEffectsUseThePendingLogicalTimeIncludingForks()
    {
        using (var t = new Fixture())
        {
            t.Begin(t.Player); var draft = t.State.CreateWorkspace(); draft.AdvanceTurnEntryLogicalTime(10);
            var fork = draft.Fork();
            var change = DRPGActionTimelineChange.Stage(fork, new ParticipantId(t.Enemy.RuntimeId), -100, 0);
            Check(change.After.Value.ScheduledTime == 10 && t.Core.Scheduler.CurrentTime == 0,
                "Detached causal effect used an old boundary or prematurely changed live time.");
            Check(t.State.TryCommitJournal(t.Lease, fork).Succeeded && t.Core.Scheduler.CurrentTime == 10,
                "Pending logical time was lost by the causal workspace fork.");
        }
    }

    // Break caught: cached status-only or differently reserved requests masquerade as the committed full lifecycle.
    private static void ReservationAndLifecycleModeArePartOfReplayIdentity()
    {
        using (var t = new Fixture())
        {
            var reservation = Reserve(t); t.Begin(t.Player);
            Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out var ledger, out var error), error);
            var changed = new DRPGTurnCandidateReservation(2, reservation.SourceKind, reservation.Candidate, 10);
            Check(!t.State.TryCommitTurnEntry(t.Lease, changed, t.Homes, out _, out _) && !t.Commit(out _, out _) &&
                t.Core.Scheduler.CurrentTime == 10 && t.State.BattleRevision == ledger.CommittedRevision,
                "A changed reservation or status-only replay concealed different lifecycle facts.");
        }
    }

    // Break caught: readiness is recomputed from ReadyAtTime, overriding an already-executable zero-warning plan.
    private static void ExecutablePlanDoesNotAdvanceTimeToItsAuthoredReadyAt()
    {
        using (var t = new Fixture())
        {
            var skill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
            try
            {
                var plan = DRPGEnemyActionPlan.CreateAttack(1, t.Enemy.RuntimeId, skill, 0, 50, 0)
                    .CommitTarget(t.Player.RuntimeId, 0).WithSatisfiedPlayerResponse(1);
                Check(plan.State == DRPGEnemyPlanState.Executable, "Zero-warning plan fixture is not executable.");
                var reservation = new DRPGTurnCandidateReservation(1, DRPGTurnCandidateSourceKind.ExecutableEnemyPlan,
                    new DRPGTurnScheduleEntry(t.Enemy, plan.ReadyAtTime, 0), 0);
                t.Status(t.Enemy, "entry", -3); t.Begin(t.Enemy);
                Check(t.State.TryCommitTurnEntry(t.Lease, reservation, t.Homes, out var ledger, out var error), error);
                Check(t.Enemy.CurrentHp == 117 && t.Core.Scheduler.CurrentTime == 0 &&
                    ledger.TurnEntryReservation.Value.ScheduledTime == 50, "Executable plan changed the scheduler clock or lost its authored ready time.");
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    internal static DRPGTurnCandidateReservation Reserve(Fixture t) => new DRPGTurnCandidateReservation(1,
        DRPGTurnCandidateSourceKind.SchedulerTimeline, t.Core.Scheduler.PeekNextEntry().Value, 10);
}
#endif
