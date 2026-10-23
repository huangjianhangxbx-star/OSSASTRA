#if UNITY_EDITOR
using System;

public static class DRPGBasicAttackDepartureProbe
{
    // Removing the separate post-return journal, advancing the scheduler, or
    // treating protagonist incapacitation as undeployment breaks this test.
    public static void PostReturnRemovesCandidateWithoutAdvancingActor()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(true, enemyFirst, false, lethal: true))
        {
            var state = f.Domain.Session.TransactionState;
            var actor = f.Domain.Session.CurrentActor;
            var turn = f.Domain.Session.CurrentActorTurnToken;
            long revision = state.BattleRevision;
            Check(f.Prepared.TryReconcileFormationAfterReturn(out var result, out var error),
                "Post-return reconciliation was not produced: " + error);
            Check(result.Departures.Owner.Equals(f.Domain.Owner) && result.Departures.SourceFingerprint == f.Ledger.Fingerprint &&
                result.Departures.Members.Count == 1 && result.Departures.Members[0].Participant == f.Frozen.Target &&
                result.Departures.Members[0].Side == (enemyFirst ? DRPGBattleSide.Player : DRPGBattleSide.Enemy),
                "Departure facts lost their exact owner, ledger or member side.");
            foreach (var entry in state.CaptureRuleSnapshot().Timeline)
                Check(entry.Participant != f.Frozen.Target, "Defeated target remained a future scheduler candidate.");
            Check(state.BattleRevision == revision + 1 && result.CommittedRevision == state.BattleRevision &&
                ReferenceEquals(actor, f.Domain.Session.CurrentActor) && turn.Equals(f.Domain.Session.CurrentActorTurnToken) &&
                f.Lease.IsActive && f.Events == 1 && f.Domain.Player.IsDeployed && f.Domain.Pool.ReleaseCount == 0,
                "Reconciliation advanced the turn, undeployed the protagonist, released a view or recommitted the hit.");
            Check(f.Prepared.TryReconcileFormationAfterReturn(out var replay, out error) && ReferenceEquals(result, replay) &&
                state.BattleRevision == revision + 1, "Repeated reconciliation was not idempotent.");
        }
    }

    public static void NonLethalReturnHasExplicitEmptyDepartureJournal()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(true, false, false))
        {
            Check(f.Prepared.TryReconcileFormationAfterReturn(out var result, out var error), error);
            Check(result.Departures != null && result.Departures.Members.Count == 0 && result.FormationChanges.Count == 0 &&
                result.Departures.Owner.Equals(f.Domain.Owner) && result.Departures.SourceFingerprint == f.Ledger.Fingerprint &&
                f.Lease.IsActive && f.Events == 1, "Empty reconciliation omitted its explicit owner-bound result.");
        }
    }

    public static void DepartureDescriptionPrecedesTransferWithoutRuleWrites()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, false, false, lethal: true))
        {
            Check(!f.Prepared.TryDescribeDepartures(out _, out _), "Uncommitted action supplied departure facts.");
            f.Commit();
            var state = f.Domain.Session.TransactionState; var before = state.CaptureRuleSnapshot();
            Check(f.Prepared.TryDescribeDepartures(out var departures, out var error), "No immutable pre-transfer departure plan: " + error);
            Check(departures.Members.Count == 1 && departures.Members[0].Participant == f.Frozen.Target &&
                departures.Owner.Equals(f.Domain.Owner) && departures.SourceFingerprint == f.Ledger.Fingerprint &&
                state.BattleRevision == before.Revision && state.CaptureRuleSnapshot().Timeline.Count == before.Timeline.Count &&
                f.Domain.Pool.ReleaseCount == 0 && f.Lease.IsActive,
                "Describing departure prematurely reconciled rules, released a view or lost provenance.");
            Check(f.Prepared.TryDescribeDepartures(out var replay, out error) && ReferenceEquals(departures, replay),
                "Repeated departure description changed the frozen member set.");
            Check(f.Prepared.TryReconcileFormationAfterReturn(out var reconciled, out error) && ReferenceEquals(departures, reconciled.Departures),
                "Post-transfer rule reconciliation reconstructed a different departure journal.");
            f.Lease.Dispose();
            Check(!f.Prepared.TryDescribeDepartures(out _, out _), "Closed owner could issue a new departure handoff.");
        }
    }

    public static void ReconciliationCompactsAllSurvivorsOnce()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(true, enemyFirst, false,
            lethal: true, fullBattlefield: true, configureRules: SetThreeRows))
        {
            long revision = f.Domain.Session.TransactionState.BattleRevision;
            Check(f.Prepared.TryReconcileFormationAfterReturn(out var result, out var error), error);
            Check(result.FormationChanges.Count == 2, "Post-return reconciliation did not compact both surviving occupied rows.");
            int first = enemyFirst ? 7111 : 7121, second = enemyFirst ? 7112 : 7122;
            foreach (var change in result.FormationChanges)
                Check((change.Participant.Value == first && change.Before == DRPGFormationRow.Middle && change.After == DRPGFormationRow.Front) ||
                    (change.Participant.Value == second && change.Before == DRPGFormationRow.Rear && change.After == DRPGFormationRow.Middle),
                    "Formation result changed the other side or lost original row provenance.");
            var snapshot = f.Domain.Session.TransactionState.CaptureRuleSnapshot();
            foreach (var participant in snapshot.Participants)
            {
                if (participant.Participant.Value == first) Check(participant.FormationRow == DRPGFormationRow.Front, "First survivor did not compact.");
                if (participant.Participant.Value == second) Check(participant.FormationRow == DRPGFormationRow.Middle, "Second survivor did not compact.");
            }
            Check(f.Prepared.TryReconcileFormationAfterReturn(out var replay, out error) && ReferenceEquals(result, replay) &&
                f.Domain.Session.TransactionState.BattleRevision == revision + 1 && f.Lease.IsActive && f.Events == 1,
                "Formation reconciliation ran twice or advanced the action.");
        }
    }

    public static void DepartureFactsRejectMalformedIdentityAndCopyMembers()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(true, false, false, lethal: true))
        {
            var member = new DRPGBattleDepartureMember(f.Frozen.Target, DRPGBattleSide.Enemy);
            var array = new[] { member };
            var facts = new DRPGBattleDepartureJournal(f.Domain.Owner, f.Ledger.Fingerprint, array);
            array[0] = new DRPGBattleDepartureMember(f.Frozen.Turn.Actor, DRPGBattleSide.Player);
            Check(facts.Members.Count == 1 && facts.Members[0].Participant == f.Frozen.Target,
                "Departure facts retain a mutable caller member array.");
            RejectArgument(() => new DRPGBattleDepartureJournal(default, 1, new[] { member }));
            RejectArgument(() => new DRPGBattleDepartureJournal(f.Domain.Owner, 0, new[] { member }));
            RejectArgument(() => new DRPGBattleDepartureJournal(f.Domain.Owner, 1, null));
            RejectArgument(() => new DRPGBattleDepartureJournal(f.Domain.Owner, 1, new[] { member, member }));
            RejectArgument(() => new DRPGBattleDepartureJournal(f.Domain.Owner, 1, new[] { default(DRPGBattleDepartureMember) }));
            RejectArgument(() => new DRPGBattleDepartureJournal(f.Domain.Owner, 1,
                new[] { new DRPGBattleDepartureMember(f.Frozen.Target, (DRPGBattleSide)999) }));
        }
    }

    public static void ReconciliationRejectsUncommittedStaleAndClosedOwners()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, false, false, lethal: true))
        {
            long revision = f.Domain.Session.TransactionState.BattleRevision;
            Check(!f.Prepared.TryReconcileFormationAfterReturn(out _, out _) && f.Domain.Session.TransactionState.BattleRevision == revision,
                "An uncommitted action reconciled departure.");
            f.Commit();
            var state = f.Domain.Session.TransactionState;
            Check(state.TryCommitJournal(f.Lease, state.CreateWorkspace()).Succeeded, "Could not stage the intervening rule revision.");
            revision = state.BattleRevision;
            Check(!f.Prepared.TryReconcileFormationAfterReturn(out _, out _) && state.BattleRevision == revision,
                "A stale action reconciled a later rule revision.");
        }
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(true, false, false, lethal: true))
        {
            Check(f.Prepared.TryReconcileFormationAfterReturn(out _, out var error), error);
            long revision = f.Domain.Session.TransactionState.BattleRevision;
            f.Lease.Dispose();
            Check(!f.Prepared.TryReconcileFormationAfterReturn(out _, out _) && f.Domain.Session.TransactionState.BattleRevision == revision,
                "Cached reconciliation escaped its closed owner.");
        }
    }

    public static void LivingTimelineRemovalRejectsTheWholeJournal()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, false, false))
        {
            var state = f.Domain.Session.TransactionState; var before = state.CaptureRuleSnapshot();
            var draft = state.CreateWorkspace();
            draft.For(f.Frozen.Turn.Actor).SetFormationRow(DRPGFormationRow.Rear);
            draft.RemoveFromTimeline(f.Frozen.Target);
            Check(!draft.TryGetScheduledProjection(f.Frozen.Target, out _), "Removed draft still exposes a scheduled candidate.");
            Check(!state.TryCommitJournal(f.Lease, draft).Succeeded && state.BattleRevision == before.Revision &&
                f.Domain.Player.FormationRow == DRPGFormationRow.Front && state.CaptureRuleSnapshot().Timeline.Count == before.Timeline.Count,
                "Invalid departure partially applied formation or removed a living candidate.");
        }
    }

    internal static void SetThreeRows(DRPGBasicAttackProjectionProbe.Fixture domain, BattleRuleMutationLease lease)
    {
        var draft = domain.Session.TransactionState.CreateWorkspace();
        foreach (var participant in domain.Session.Participants)
        {
            var row = participant.RuntimeId == 7111 || participant.RuntimeId == 7121 ? DRPGFormationRow.Middle :
                participant.RuntimeId == 7112 || participant.RuntimeId == 7122 ? DRPGFormationRow.Rear : DRPGFormationRow.Front;
            draft.For(new ParticipantId(participant.RuntimeId)).SetFormationRow(row);
        }
        Check(domain.Session.TransactionState.TryCommitJournal(lease, draft).Succeeded, "Could not arrange real rule rows.");
    }
    private static void RejectArgument(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("Malformed departure identity or member set was accepted.");
    }
    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
