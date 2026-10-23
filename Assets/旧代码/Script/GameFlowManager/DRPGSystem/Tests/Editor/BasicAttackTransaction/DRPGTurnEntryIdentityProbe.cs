#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGTurnEntryIdentityProbe
{
    // Break caught: a previous actor can still reserve an action while a candidate is settling.
    internal static void PendingEntryInvalidatesPreviousCommandToken()
    {
        var authority = new DRPGBattleIdentityAuthority(701);
        var oldTurn = authority.AllocateTurn(new ParticipantId(1));
        var entry = authority.AllocateTurnEntry(new ParticipantId(2));
        using (var lease = Lease(entry))
            Check(authority.TryBeginTurnEntry(entry, lease), "The owned candidate did not activate.");
        bool rejected = false;
        try { using (authority.ReserveAction(oldTurn)) { } }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "A prior actor token still accepts commands during TurnEntry.");
    }

    // Break caught: independent candidate and actor counters roll status time back after discarded candidates.
    internal static void PublishedCandidateKeepsItsReservedBoundary()
    {
        var authority = new DRPGBattleIdentityAuthority(702);
        authority.AllocateTurn(new ParticipantId(1));
        var discarded = authority.AllocateTurnEntry(new ParticipantId(2));
        var entry = authority.AllocateTurnEntry(new ParticipantId(3));
        Check(discarded.CandidateSerial == 2 && entry.CandidateSerial == 3,
            "Candidate boundaries did not advance after the previously published actor.");
        using (var lease = Lease(entry))
        {
            Check(authority.TryBeginTurnEntry(entry, lease), "The owned candidate did not activate.");
            Check(authority.TryPublishTurnEntry(entry, out var turn), "The terminal candidate could not publish.");
            Check(turn.TurnSerial == 3 && turn.Actor == new ParticipantId(3), "Publication allocated a new or older status boundary.");
            var action = authority.AllocateAction(turn);
            Check(action.ActionId == 1 && action.TurnSerial == 3, "Candidate selection consumed an ActionId or changed its boundary.");
            Check(authority.AllocateTurn(new ParticipantId(1)).TurnSerial == 4, "Legacy handoff reused an earlier boundary.");
        }
    }

    // Break caught: foreign, actor-spoofed, superseded, or replayed entry publishes a new turn.
    internal static void OnlyThePendingExactCandidatePublishesOnce()
    {
        var authority = new DRPGBattleIdentityAuthority(703);
        var stale = authority.AllocateTurnEntry(new ParticipantId(1));
        var entry = authority.AllocateTurnEntry(new ParticipantId(2));
        using (var lease = Lease(entry))
        {
            Check(authority.TryBeginTurnEntry(entry, lease), "The owned candidate did not activate.");
            foreach (var invalid in new[] { default(TurnEntryIdentity), stale,
                new TurnEntryIdentity(704, entry.CandidateSerial, entry.Actor),
                new TurnEntryIdentity(703, entry.CandidateSerial, new ParticipantId(3)) })
                Check(!authority.TryPublishTurnEntry(invalid, out var rejected) && !rejected.IsValid,
                    "An unrelated candidate was published.");
            Check(authority.TryPublishTurnEntry(entry, out var turn), "A rejected forgery consumed the valid candidate.");
            Check(!authority.TryPublishTurnEntry(entry, out _), "Candidate publication replay created another turn.");
            Check(authority.AllocateSelection(turn).Actor == entry.Actor, "Failed replay invalidated the real published actor.");
        }
    }

    // Break caught: TurnStart raises the collection high-water mark above the following Root boundary, skipping AfterAction.
    internal static void SkippedCandidateDoesNotSuppressAfterActionStatus()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var start = Status("entry", DRPGStatusTriggerTiming.TurnStart, -3);
            var after = Status("after", DRPGStatusTriggerTiming.AfterAction, -5);
            try
            {
                var actor = new ParticipantId(test.Player.RuntimeId);
                var initial = test.CreateWorkspace();
                initial.For(actor).ApplyStatus(DRPGResolvedStatusSpec.From(start), 1, 0, actor);
                initial.For(actor).ApplyStatus(DRPGResolvedStatusSpec.From(after), 1, 0, actor);
                Check(test.State.TryCommitJournal(test.Lease, initial).Succeeded, "Could not seed actual status state.");
                test.Lease.Dispose();
                var authority = new DRPGBattleIdentityAuthority(test.BattleGeneration);
                authority.AllocateTurnEntry(new ParticipantId(test.Enemy.RuntimeId));
                var entry = authority.AllocateTurnEntry(actor);
                Check(test.Arbiter.TryAcquire(BattleTransactionIdentity.ForTurnEntry(entry), out var lease, out string error), error);
                try
                {
                    Check(authority.TryBeginTurnEntry(entry, lease), "The owned candidate did not activate.");
                    var draft = test.CreateWorkspace();
                    var target = draft.For(actor);
                    var instance = target.CreateStatusProjection().Snapshot()[0];
                    Check(target.TryConsumeStatusBoundary(new DRPGStatusBoundary(DRPGStatusTriggerTiming.TurnStart,
                        entry.CandidateSerial, actor), instance.StatusInstanceId, out var step), "TurnStart was not due.");
                    target.SetHitPoints(target.HitPoints + step.HpDelta);
                    Check(test.State.TryCommitJournal(lease, draft).Succeeded && test.Player.CurrentHp == 97,
                        "TurnStart did not atomically commit one duration and -3 HP.");
                    Check(authority.TryPublishTurnEntry(entry, out var turn), "Candidate could not publish after its actual status commit.");
                    var action = authority.AllocateAction(turn);
                    Check(test.Arbiter.TransferToContinuation(lease, BattleTransactionIdentity.ForRootAction(action),
                        out var root, out error), error);
                    using (root)
                    {
                        var homes = new Dictionary<ParticipantId, int> { { actor, 0 }, { new ParticipantId(test.Enemy.RuntimeId), 0 } };
                        Check(test.State.TryCommitSettlement(root, actor, homes, out var ledger, out error), error);
                        Check(test.Player.CurrentHp == 92 && ledger.Steps.Count == 1,
                            "AfterAction was skipped because its serial moved behind the candidate's consumed boundary.");
                        Check(test.Player.Statuses.Snapshot()[1].RemainingTurns == 1,
                            "AfterAction did not consume exactly one duration after TurnEntry.");
                    }
                }
                finally { lease.Dispose(); }
            }
            finally { UnityEngine.Object.DestroyImmediate(start); UnityEngine.Object.DestroyImmediate(after); }
        }
    }

    // Break caught: speculative allocation or legacy timeline maintenance invalidates the live actor before ownership.
    internal static void AllocationWithoutOwnershipCannotReplaceTheActor()
    {
        var authority = new DRPGBattleIdentityAuthority(705);
        var turn = authority.AllocateTurn(new ParticipantId(1));
        var entry = authority.AllocateTurnEntry(new ParticipantId(2));
        Check(authority.AllocateSelection(turn).Actor == turn.Actor, "Allocating a candidate invalidated the current actor before ownership.");
        Check(!authority.TryPublishTurnEntry(entry, out _), "An unowned candidate published without activation.");
        Check(!authority.TryBeginTurnEntry(entry, null), "A candidate activated without a mutation lease.");
        using (var foreign = Lease(new TurnEntryIdentity(706, entry.CandidateSerial, entry.Actor)))
            Check(!authority.TryBeginTurnEntry(entry, foreign), "A foreign lease activated the candidate.");
        Check(authority.AllocateSelection(turn).Actor == turn.Actor, "Rejected activation revoked the current actor.");
    }

    // Break caught: terminal callback publishes after its candidate lease has expired.
    internal static void ExpiredCandidateLeaseCannotPublish()
    {
        var authority = new DRPGBattleIdentityAuthority(707);
        var entry = authority.AllocateTurnEntry(new ParticipantId(1));
        var lease = Lease(entry);
        Check(authority.TryBeginTurnEntry(entry, lease), "Candidate did not activate.");
        lease.Dispose();
        Check(!authority.TryPublishTurnEntry(entry, out _), "Expired candidate lease published an actor.");
    }

    private static BattleRuleMutationLease Lease(TurnEntryIdentity entry)
    {
        var arbiter = new DRPGBattleTransactionArbiter(entry.BattleGeneration,
            new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(16)));
        Check(arbiter.TryAcquire(BattleTransactionIdentity.ForTurnEntry(entry), out var lease, out string error), error);
        return lease;
    }

    private static DRPGStatusDefinitionSO Status(string id, DRPGStatusTriggerTiming timing, float hp)
    {
        var value = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        foreach (var field in new Dictionary<string, object> { { "statusId", id }, { "triggerTiming", timing },
            { "turnStartHpDelta", hp }, { "durationTurns", 2 } })
            typeof(DRPGStatusDefinitionSO).GetField(field.Key, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, field.Value);
        return value;
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
