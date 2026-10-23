#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGSettlementProjectionProbe
{
    internal static void RunAll()
    {
        LeaseFreezesStatusAndPostureAtModelBoundary(); SemanticCursorChangesOnlyItsParticipant();
        OldHpCursorPreservesFrozenSettlementState(); StaleSemanticBeforeIsRejected();
        PartialBeforeCannotReplaceStatusState(); StatusMetadataAndOwnerRemainDistinct();
    }

    // Break caught: acquiring the lease rereads a live runtime behind an older view model.
    internal static void LeaseFreezesStatusAndPostureAtModelBoundary()
    {
        using (var test = new Fixture())
        {
            test.Mutate(false);
            using (var lease = test.View.Surface.AcquireProjectionLease(test.View.Owner))
            {
                test.View.Bridge.FlushQueuedSignals();
                var before = lease.GetPresented(test.View.Enemy.RuntimeId);
                Check(Posture(before) == 75 && Statuses(before).Count == 1 && Statuses(before)[0].RemainingTurns == 2,
                    "Lease captured canonical future posture/status instead of the bound model.");
                Check(test.View.Enemy.CurrentPosture == 20 && test.View.Enemy.Statuses.Snapshot().Count == 0,
                    "Future canonical fixture was not applied.");
            }
        }
    }

    // Break caught: applying the party step exposes a future enemy death or expires its status.
    internal static void SemanticCursorChangesOnlyItsParticipant()
    {
        using (var test = new Fixture())
        using (var lease = test.View.Surface.AcquireProjectionLease(test.View.Owner))
        {
            test.Mutate(true); test.View.Bridge.FlushQueuedSignals();
            var party = lease.GetPresented(test.View.Player.RuntimeId); var enemy = lease.GetPresented(test.View.Enemy.RuntimeId);
            var afterParty = Semantic(party, 90, 20, test.View.Player.Statuses.Snapshot());
            Check(lease.Apply(new BattleProjectionCursor(test.View.Owner, 0, party, afterParty)).Accepted, "Party semantic step rejected.");
            var visibleParty = lease.GetPresented(party.RuntimeId); var visibleEnemy = lease.GetPresented(enemy.RuntimeId);
            Check(visibleParty.Hp == 90 && Posture(visibleParty) == 20 && Statuses(visibleParty).Count == 0 &&
                visibleEnemy.Hp == enemy.Hp && !visibleEnemy.DefeatPending && Posture(visibleEnemy) == 75 && Statuses(visibleEnemy).Count == 1,
                "The first semantic cursor exposed a later participant outcome.");
            var afterEnemy = Semantic(enemy, 0, 20, test.View.Enemy.Statuses.Snapshot());
            Check(lease.Apply(new BattleProjectionCursor(test.View.Owner, 1, enemy, afterEnemy)).Accepted &&
                lease.GetPresented(enemy.RuntimeId).DefeatPending && test.View.Target.gameObject.activeInHierarchy && test.View.Pool.ReleaseCount == 0,
                "The lethal semantic step released its actor before finalization.");
        }
    }

    internal static void OldHpCursorPreservesFrozenSettlementState()
    {
        using (var test = new Fixture())
        using (var lease = test.View.Surface.AcquireProjectionLease(test.View.Owner))
        {
            var before = lease.GetPresented(test.View.Enemy.RuntimeId);
            Check(lease.Apply(new BattleProjectionCursor(test.View.Owner, 0, before,
                new BattleProjectedParticipant(before.RuntimeId, before.Hp - 1, before.MaxHp))).Accepted, "Existing HP cursor rejected.");
            var after = lease.GetPresented(before.RuntimeId);
            Check(Posture(after) == 75 && DRPGActionStatusChange.SameSnapshots(Statuses(before), Statuses(after)),
                "Existing Stage7 HP cursor erased frozen posture/status metadata.");
        }
    }

    internal static void StaleSemanticBeforeIsRejected()
    {
        using (var test = new Fixture())
        using (var lease = test.View.Surface.AcquireProjectionLease(test.View.Owner))
        {
            var before = lease.GetPresented(test.View.Enemy.RuntimeId);
            var stale = Semantic(before, before.Hp, 74, Statuses(before));
            Check(!lease.Apply(new BattleProjectionCursor(test.View.Owner, 0, stale, before)).Accepted && lease.NextCursorOrdinal == 0,
                "A stale posture snapshot with matching HP was accepted.");
            var expired = DRPGStatusRuntimeCollection.FromSnapshot(Statuses(before)); expired.Remove("projection-status");
            stale = Semantic(before, before.Hp, 75, expired.Snapshot());
            Check(!lease.Apply(new BattleProjectionCursor(test.View.Owner, 0, stale, before)).Accepted && lease.NextCursorOrdinal == 0,
                "A stale status snapshot with matching HP was accepted.");
        }
    }

    internal static void PartialBeforeCannotReplaceStatusState()
    {
        using (var test = new Fixture())
        using (var lease = test.View.Surface.AcquireProjectionLease(test.View.Owner))
        {
            var full = lease.GetPresented(test.View.Enemy.RuntimeId);
            var hpOnly = new BattleProjectedParticipant(full.RuntimeId, full.Hp, full.MaxHp);
            var changed = full.WithSettlement(full.Hp, 20, full.Statuses);
            Check(!lease.Apply(new BattleProjectionCursor(test.View.Owner, 0, hpOnly, changed)).Accepted &&
                lease.GetPresented(full.RuntimeId).Posture == 75 && lease.NextCursorOrdinal == 0,
                "A partial HP-before bypassed the semantic comparison.");
        }
    }

    internal static void StatusMetadataAndOwnerRemainDistinct()
    {
        using (var test = new Fixture())
        using (var lease = test.View.Surface.AcquireProjectionLease(test.View.Owner))
        {
            var before = lease.GetPresented(test.View.Enemy.RuntimeId); var statuses = before.Statuses;
            var changedMetadata = new DRPGStatusCollectionSnapshot(statuses.OwnerParticipantId, statuses.NextInsertionOrdinal + 1,
                statuses, statuses.ObservedBoundarySerial);
            var stale = before.WithSettlement(before.Hp, before.Posture, changedMetadata);
            Check(!lease.Apply(new BattleProjectionCursor(test.View.Owner, 0, stale, before)).Accepted,
                "Identical icons with different instance high-water marks were treated as the same state.");
            bool rejected = false;
            try { before.WithSettlement(before.Hp, before.Posture, lease.GetPresented(test.View.Player.RuntimeId).Statuses); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && lease.NextCursorOrdinal == 0, "Another participant's status collection was accepted.");
        }
    }

    private static float Posture(BattleProjectedParticipant state)
    {
        return state.Posture;
    }
    private static DRPGStatusCollectionSnapshot Statuses(BattleProjectedParticipant state)
    {
        var statuses = state.Statuses;
        Check(statuses != null, "Projection lost status identity metadata."); return statuses;
    }
    private static BattleProjectedParticipant Semantic(BattleProjectedParticipant before, float hp, float posture, DRPGStatusCollectionSnapshot statuses)
    {
        return before.WithSettlement(hp, posture, statuses);
    }
    private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private sealed class Fixture : IDisposable
    {
        internal readonly DRPGBasicAttackProjectionProbe.Fixture View = new DRPGBasicAttackProjectionProbe.Fixture(false);
        internal Fixture()
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                Set(definition, "statusId", "projection-status"); Set(definition, "durationTurns", 2);
                Write(draft => {
                    foreach (var participant in new[] { View.Player, View.Enemy })
                    {
                        var target = draft.For(new ParticipantId(participant.RuntimeId)); target.SetPosture(75);
                        target.ApplyStatus(DRPGResolvedStatusSpec.From(definition), 1, 0, new ParticipantId(participant.RuntimeId));
                    }
                });
                View.Bridge.FlushQueuedSignals(); View.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(View.Session));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
        internal void Mutate(bool lethal) => Write(draft => {
            foreach (var participant in new[] { View.Player, View.Enemy })
            {
                var target = draft.For(new ParticipantId(participant.RuntimeId)); target.SetPosture(20); target.RemoveStatus("projection-status");
                if (lethal) target.SetHitPoints(participant == View.Enemy ? 0 : 90);
            }
        });
        private void Write(Action<DRPGBattleMutationWorkspace> change)
        {
            var arbiter = View.Session.TransactionArbiter;
            var owner = BattleTransactionIdentity.ForTestHarness(new BattleStartIdentity(View.Session.BattleGeneration, 200));
            Check(arbiter.TryAcquire(owner, out var lease, out var error), error);
            using (lease)
            {
                var draft = View.Session.TransactionState.CreateWorkspace(); change(draft);
                Check(View.Session.TransactionState.TryCommitJournal(lease, draft).Succeeded, "Semantic fixture did not commit.");
            }
        }
        public void Dispose() => View.Dispose();
    }
}
#endif
