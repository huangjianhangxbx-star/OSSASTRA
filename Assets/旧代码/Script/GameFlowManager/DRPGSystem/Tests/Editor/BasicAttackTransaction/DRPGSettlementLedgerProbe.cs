#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGSettlementLedgerProbe
{
    [UnityEditor.MenuItem("Tools/ANKALUTE/DRPG/Run Settlement Rule Probes")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("Settlement status-root rules: 14/14 PASS. Stage9 presentation and causal children are not covered by this group.");
    }
    internal static void RunAll()
    {
        BoundaryConsumptionCommitsWithVitalsOnce();
        LateInvalidMutationDoesNotConsumeStatus();
        SettlementOrdersOwnersBeforeSourcesAndCommitsOnce();
        SettlementDeathSkipsDeadSourcesWithoutRevivingOrDeparting();
        SettlementLateOverflowLeavesBothSidesUntouched();
        SettlementRejectsForeignOwnerAndConflictingReplay();
        SettlementUsesFrozenHomeSlotsNotRosterOrder();
        SettlementExcludesFreshStatusesAndOtherActorsTurnEnd();
        EmptySettlementIsSealedAndInvalidLayoutsAreRejected();
        AuthoredPersistenceSurvivesSourceDefeatAndIsFrozen();
        BoundaryIdentityChangesJournalFingerprint();
        PriorDefeatDoesNotRequireAnActiveHomeSlot();
        SettlementPreservesPostureBreakAndExpiryFacts();
        RootOwnershipBlocksInterveningMutations();
    }

    // Break caught: consuming only a detached projection loses expiry on State commit.
    internal static void BoundaryConsumptionCommitsWithVitalsOnce()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Enemy, Status("poison", -2f), 3);
            var id = new ParticipantId(test.Enemy.RuntimeId);
            var boundary = new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 1, new ParticipantId(test.Player.RuntimeId));
            var draft = test.CreateWorkspace();
            var target = draft.For(id);
            string instanceId = target.CreateStatusProjection().Snapshot()[0].StatusInstanceId;
            Check(Consume(target, boundary, instanceId, out var step), "Due status was not staged into the workspace journal.");
            Check(step.HpDelta == -6 && step.Before.Stacks == 3 && step.After.RemainingTurns == 1, "One three-stack step must tick -6 HP and consume one duration.");
            target.SetHitPoints(target.HitPoints + step.HpDelta);
            Check(!Consume(target, boundary, instanceId, out _), "Duplicate boundary consumed the same instance twice.");
            Check(test.Enemy.CurrentHp == 120 && test.Enemy.Statuses.Snapshot()[0].RemainingTurns == 2, "Detached staging leaked into live HP/status.");
            var fork = draft.Fork();
            Check(fork.For(id).CreateStatusProjection().Snapshot()[0].RemainingTurns == 1, "Fork lost the pending boundary mutation.");
            long revision = test.State.BattleRevision;
            Check(test.State.TryCommitJournal(test.Lease, draft).Succeeded, "Combined HP/status journal was rejected.");
            var committed = test.Enemy.Statuses.Snapshot();
            Check(test.State.BattleRevision == revision + 1 && test.Enemy.CurrentHp == 114 && committed[0].RemainingTurns == 1 && committed[0].LastConsumedBoundarySerial == 1,
                "HP and instance duration did not commit together exactly once.");
            Check(!test.State.TryCommitJournal(test.Lease, fork).Succeeded && test.Enemy.CurrentHp == 114, "Stale fork committed the tick twice.");
        }
    }

    // Break caught: writing duration eagerly before a later resource preflight fails.
    internal static void LateInvalidMutationDoesNotConsumeStatus()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Player, Status("expired", -7f, duration: 1));
            var draft = test.CreateWorkspace();
            var target = draft.For(new ParticipantId(test.Player.RuntimeId));
            var before = test.Player.Statuses.Snapshot();
            Check(Consume(target, new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 1, new ParticipantId(test.Player.RuntimeId)),
                before[0].StatusInstanceId, out var step) && step.IsExpired, "Last duration did not stage expiry.");
            target.SetHitPoints(93);
            draft.For(new ParticipantId(test.Enemy.RuntimeId)).SetMana(9999);
            long revision = test.State.BattleRevision;
            Check(!test.State.TryCommitJournal(test.Lease, draft).Succeeded, "Invalid late MP mutation was accepted.");
            Check(test.State.BattleRevision == revision && test.Player.CurrentHp == 100 && DRPGActionStatusChange.SameSnapshots(before, test.Player.Statuses.Snapshot()),
                "Late failure partially wrote HP, duration, expiry, or identity counters.");
        }
    }

    private static bool Consume(ParticipantMutationBuilder target, DRPGStatusBoundary boundary, string id, out DRPGStatusBoundaryStep step)
    {
        return target.TryConsumeStatusBoundary(boundary, id, out step);
    }

    // Break caught: caster-side/insertion sorting or per-step State commits change the ordered result.
    internal static void SettlementOrdersOwnersBeforeSourcesAndCommitsOnce()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Enemy, Status("enemy-first-insertion", -5), source: test.Player);
            Apply(test, test.Player, Status("actor-end", -3, DRPGStatusTriggerTiming.ActorTurnEnd, 99));
            Apply(test, test.Player, Status("lower-priority", -2, priority: 1));
            Apply(test, test.Player, Status("higher-priority", -4, priority: 10), 2, test.Enemy);
            Apply(test, test.Player, Status("turn-start-excluded", -50, DRPGStatusTriggerTiming.TurnStart));
            using (var lease = Root(test))
            {
                long revision = test.State.BattleRevision;
                var slots = Slots(test);
                Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), slots, out var ledger, out var error), error);
                var steps = (System.Collections.IList)Get(ledger, "Steps");
                Check(steps.Count == 4, "Settlement included TurnStart or split one multi-stack instance.");
                string[] ids = { "higher-priority", "lower-priority", "actor-end", "enemy-first-insertion" };
                float[] hp = { 92, 90, 87, 115 };
                for (int i = 0; i < steps.Count; i++)
                {
                    var status = (DRPGStatusBoundaryStep)Get(steps[i], "Status");
                    Check(status.Before.StatusId == ids[i] && (float)Get(steps[i], "HpAfter") == hp[i], "Settlement lane/timing/priority order or before/after HP chain is wrong at " + i);
                }
                Check(test.State.BattleRevision == revision + 1 && test.Player.CurrentHp == 87 && test.Enemy.CurrentHp == 115,
                    "Settlement did not atomically commit both lanes with one revision.");
                Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), slots, out var replay, out error) && ReferenceEquals(ledger, replay) && test.State.BattleRevision == revision + 1,
                    "Replaying settlement rebuilt or consumed it again.");
                slots[new ParticipantId(test.Player.RuntimeId)] = 7;
                Check((int)Get(steps[0], "HomeSlot") == 0, "Mutable layout input changed the frozen ledger.");
                Check(test.Arbiter.Owns(lease, lease.Owner), "Settlement released Root ownership before presentation.");
            }
        }
    }

    internal static void SettlementDeathSkipsDeadSourcesWithoutRevivingOrDeparting()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Player, Status("lethal", -100, priority: 50), source: test.Enemy);
            Apply(test, test.Player, Status("ordinary-heal", 30, priority: 1), source: test.Enemy);
            Apply(test, test.Enemy, Status("dead-caster", -70), source: test.Player);
            Apply(test, test.Enemy, Status("still-alive-caster", -5, priority: -1));
            using (var lease = Root(test))
            {
                Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), Slots(test), out var ledger, out var error), error);
                var steps = (System.Collections.IList)Get(ledger, "Steps");
                Check(steps.Count == 2 && (bool)Get(steps[0], "RuleDefeated") && test.Player.CurrentHp == 0 && test.Enemy.CurrentHp == 115,
                    "Death revived a target, let its ordinary source tick, or interrupted the remaining lane.");
                Check(test.Player.Statuses.Snapshot()[1].RemainingTurns == 2 && test.Enemy.Statuses.Snapshot()[0].RemainingTurns == 2,
                    "Skipped ordinary steps consumed duration.");
                Check(test.Player.IsDeployed && test.Scheduler.GetTimelineSnapshot().Count == 2 && test.Arbiter.Owns(lease, lease.Owner),
                    "Stage9 rule commit removed a dead actor or released the Root before presentation barriers.");
            }
        }
    }

    internal static void SettlementLateOverflowLeavesBothSidesUntouched()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Player, Status("valid-party", -5));
            Apply(test, test.Enemy, Status("overflow-enemy", -float.MaxValue), 2);
            var playerBefore = test.Player.Statuses.Snapshot(); var enemyBefore = test.Enemy.Statuses.Snapshot();
            using (var lease = Root(test))
            {
                long revision = test.State.BattleRevision;
                Check(!Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), Slots(test), out var ledger, out _) && ledger == null,
                    "An overflowing later enemy step was accepted or exposed a partial ledger.");
                Check(test.State.BattleRevision == revision && test.Player.CurrentHp == 100 && test.Enemy.CurrentHp == 120 &&
                    DRPGActionStatusChange.SameSnapshots(playerBefore, test.Player.Statuses.Snapshot()) && DRPGActionStatusChange.SameSnapshots(enemyBefore, test.Enemy.Statuses.Snapshot()),
                    "A late overflow partially applied the earlier lane.");
            }
        }
    }

    internal static void SettlementRejectsForeignOwnerAndConflictingReplay()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        using (var foreign = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Player, Status("poison", -5));
            Check(!Commit(test.State, test.Lease, new ParticipantId(test.Player.RuntimeId), Slots(test), out _, out _), "TestHarness ownership bypassed the settlement Root gate.");
            using (var lease = Root(test))
            using (var foreignLease = Root(foreign))
            {
                Check(!Commit(test.State, foreignLease, new ParticipantId(test.Player.RuntimeId), Slots(test), out _, out _), "Foreign Root ownership committed settlement.");
                Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), Slots(test), out _, out var error), error);
                long revision = test.State.BattleRevision;
                var changed = Slots(test); changed[new ParticipantId(test.Enemy.RuntimeId)] = 2;
                Check(!Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), changed, out _, out _), "Conflicting layout replay was silently accepted.");
                Check(!Commit(test.State, lease, new ParticipantId(test.Enemy.RuntimeId), Slots(test), out _, out _) && test.State.BattleRevision == revision,
                    "Conflicting actor replay was silently accepted or mutated State.");
            }
        }
    }

    private static BattleRuleMutationLease Root(DRPGBattleStateMutationFixture test)
    {
        test.Arbiter.Release(test.Lease);
        Check(test.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(new ActionIdentity(test.BattleGeneration, 1, 19)), out var lease, out string error), error);
        return lease;
    }

    internal static void SettlementUsesFrozenHomeSlotsNotRosterOrder()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var p1 = test.CreateDetachedParticipant(9911); var p2 = test.CreateDetachedParticipant(9912);
            var e1 = test.CreateDetachedParticipant(9913, DRPGBattleSide.Enemy); var e2 = test.CreateDetachedParticipant(9914, DRPGBattleSide.Enemy);
            var actors = new[] { e1, p1, e2, p2 };
            foreach (var actor in actors) actor.Statuses.Apply(Status("tick-" + actor.RuntimeId, -1));
            var arbiter = new DRPGBattleTransactionArbiter(test.BattleGeneration + 10000000, new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
            var state = new DRPGBattleSessionState(arbiter.BattleGeneration, actors, new DRPGTurnScheduler(), arbiter);
            Check(arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(new ActionIdentity(arbiter.BattleGeneration, 1, 20)), out var lease, out string error), error);
            using (lease)
            {
                var slots = new Dictionary<ParticipantId, int> { { new ParticipantId(9911), 1 }, { new ParticipantId(9912), 0 }, { new ParticipantId(9913), 1 }, { new ParticipantId(9914), 0 } };
                var duplicate = new Dictionary<ParticipantId, int>(slots); duplicate[new ParticipantId(9911)] = 0;
                Check(!Commit(state, lease, new ParticipantId(9911), duplicate, out _, out _) && state.BattleRevision == 0, "Overlapping Home Slots were accepted.");
                Check(Commit(state, lease, new ParticipantId(9911), slots, out var ledger, out error), error);
                var steps = (System.Collections.IList)Get(ledger, "Steps");
                int[] wanted = { 9912, 9911, 9914, 9913 };
                for (int i = 0; i < 4; i++) Check(((ParticipantId)Get(steps[i], "Owner")).Value == wanted[i], "Settlement used roster insertion/PartyOrder instead of frozen Home Slot.");
            }
        }
    }

    internal static void SettlementExcludesFreshStatusesAndOtherActorsTurnEnd()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Enemy, Status("enemy-own-end", -90, DRPGStatusTriggerTiming.ActorTurnEnd));
            Apply(test, test.Player, Status("old-status", -4));
            using (var lease = Root(test))
            {
                var draft = test.CreateWorkspace();
                draft.For(new ParticipantId(test.Player.RuntimeId)).ApplyStatus(Status("just-applied", -60));
                Check(test.State.TryCommitJournal(lease, draft).Succeeded, "Current Root status fixture failed.");
                Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), Slots(test), out var ledger, out var error), error);
                Check(((System.Collections.IList)Get(ledger, "Steps")).Count == 1 && test.Player.CurrentHp == 96 && test.Enemy.CurrentHp == 120,
                    "Fresh status or another actor's turn-end status ticked in the same Root.");
                Check(test.Player.Statuses.Snapshot()[1].RemainingTurns == 2 && test.Enemy.Statuses.Snapshot()[0].RemainingTurns == 2, "An excluded status lost duration.");
            }
        }
    }

    internal static void EmptySettlementIsSealedAndInvalidLayoutsAreRejected()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        using (var lease = Root(test))
        {
            var actor = new ParticipantId(test.Player.RuntimeId);
            var missing = Slots(test); missing.Remove(actor);
            var negative = Slots(test); negative[actor] = -1;
            var extra = Slots(test); extra.Add(new ParticipantId(98765), 4);
            foreach (var bad in new[] { missing, negative, extra })
                Check(!Commit(test.State, lease, actor, bad, out _, out _) && test.State.BattleRevision == 0, "Invalid layout partially committed an empty settlement.");
            Check(Commit(test.State, lease, actor, Slots(test), out var ledger, out var error), error);
            Check(((System.Collections.IList)Get(ledger, "Steps")).Count == 0 && test.State.BattleRevision == 1, "Empty settlement was not recorded as one boundary.");
            Check(Commit(test.State, lease, actor, Slots(test), out var replay, out _) && ReferenceEquals(ledger, replay) && test.State.BattleRevision == 1, "Empty replay advanced State.");
        }
    }

    internal static void AuthoredPersistenceSurvivesSourceDefeatAndIsFrozen()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                Set(definition, "statusId", "persistent"); Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
                Set(definition, "turnStartHpDelta", -9f);
                var field = definition.GetType().GetField("persistsAfterSourceDefeat", BindingFlags.NonPublic | BindingFlags.Instance);
                Check(field != null, "Missing persistence authoring.");
                field.SetValue(definition, true);
                var persistent = DRPGResolvedStatusSpec.From(definition);
                field.SetValue(definition, false);
                Apply(test, test.Player, Status("source-killer", -100, priority: 20), source: test.Enemy);
                Apply(test, test.Enemy, persistent, source: test.Player);
                using (var lease = Root(test))
                {
                    Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), Slots(test), out var ledger, out var error), error);
                    Check(test.Player.CurrentHp == 0 && test.Enemy.CurrentHp == 111 && ((System.Collections.IList)Get(ledger, "Steps")).Count == 2,
                        "An explicitly persistent frozen status was skipped after its source died.");
                    Check(!persistent.HasSameValues(DRPGResolvedStatusSpec.From(definition)), "Persistence is missing from frozen status equality.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
    }

    internal static void BoundaryIdentityChangesJournalFingerprint()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            Apply(test, test.Player, Status("journal-tick", -1));
            var id = new ParticipantId(test.Player.RuntimeId);
            var first = test.CreateWorkspace(); var second = test.CreateWorkspace();
            string instance = test.Player.Statuses.Snapshot()[0].StatusInstanceId;
            Check(Consume(first.For(id), new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 1, id), instance, out _), "First tick failed.");
            Check(Consume(second.For(id), new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 2, id), instance, out _), "Second tick failed.");
            // Exercise the actual fingerprint serializer, not a source-text assertion.
            var method = typeof(DRPGActionResolutionFingerprint).GetMethod("AppendJournal", BindingFlags.Static | BindingFlags.NonPublic);
            var a = new System.Text.StringBuilder(); var b = new System.Text.StringBuilder();
            method.Invoke(null, new object[] { a, first.BuildValidatedJournal() });
            method.Invoke(null, new object[] { b, second.BuildValidatedJournal() });
            Check(a.ToString() != b.ToString(), "Different consumed boundaries have the same journal fingerprint input.");
        }
    }

    internal static void PriorDefeatDoesNotRequireAnActiveHomeSlot()
    {
        using (var test = DRPGBattleStateMutationFixture.Create(enemyStartsDefeated: true))
        {
            Apply(test, test.Player, Status("survivor", -2));
            using (var lease = Root(test))
            {
                var homes = new Dictionary<ParticipantId, int> { { new ParticipantId(test.Player.RuntimeId), 0 } };
                Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), homes, out _, out var error),
                    "Stage8's already-defeated actor incorrectly still requires an active Home Slot: " + error);
                Check(test.Player.CurrentHp == 98 && test.Enemy.CurrentHp == 0, "Prior defeat interrupted survivor settlement.");
            }
        }
    }

    internal static void SettlementPreservesPostureBreakAndExpiryFacts()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction); Set(definition, "maxStacks", 3);
                Set(definition, "durationTurns", 1); Set(definition, "turnStartHpDelta", 3f); Set(definition, "turnStartPostureDelta", -5f);
                var draft = test.CreateWorkspace();
                draft.For(new ParticipantId(test.Player.RuntimeId)).SetHitPoints(80).SetPosture(10).ApplyStatus(DRPGResolvedStatusSpec.From(definition), 3);
                Check(test.State.TryCommitJournal(test.Lease, draft).Succeeded, "Posture fixture failed.");
                using (var lease = Root(test))
                {
                    Check(Commit(test.State, lease, new ParticipantId(test.Player.RuntimeId), Slots(test), out var ledger, out var error), error);
                    var step = ((System.Collections.IList)Get(ledger, "Steps"))[0];
                    var status = (DRPGStatusBoundaryStep)Get(step, "Status");
                    Check(test.Player.CurrentHp == 89 && test.Player.CurrentPosture == 0 &&
                        !test.Player.PostureBreak.CaptureRuleState().IsExposed && test.Player.Statuses.Count == 0,
                        "Stacked HP/posture or expiry was lost at atomic settlement commit.");
                    Check(status.IsExpired && status.PostureDelta == -15 && (float)Get(step, "PostureBefore") == 10 && (float)Get(step, "PostureAfter") == 0 &&
                        ((DRPGStatusCollectionSnapshot)Get(step, "StatusesBefore")).Count == 1 && ((DRPGStatusCollectionSnapshot)Get(step, "StatusesAfter")).Count == 0,
                        "Visible settlement facts do not preserve the single stacked expiry and clamped resource chain.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
    }

    internal static void RootOwnershipBlocksInterveningMutations()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        using (var lease = Root(test))
        {
            long revision = test.State.BattleRevision;
            Check(!test.Arbiter.TryAcquire(test.Owner, out _, out _), "Debug owner entered a locked Root.");
            var draft = test.CreateWorkspace(); draft.For(new ParticipantId(test.Player.RuntimeId)).SetFormationRow(DRPGFormationRow.Rear);
            Check(!test.State.TryCommitJournal(test.Lease, draft).Succeeded, "Released debug lease wrote formation during a Root.");
            bool hpRejected = false, timelineRejected = false;
            try { test.Enemy.ApplyDamage(10); } catch (InvalidOperationException) { hpRejected = true; }
            try { test.Scheduler.ScheduleAt(test.Enemy, 40, 0); } catch (InvalidOperationException) { timelineRejected = true; }
            Check(hpRejected && timelineRejected && test.State.BattleRevision == revision && test.Enemy.CurrentHp == 120,
                "External HP/timeline mutation crossed the Root lease boundary.");
        }
    }
    private static Dictionary<ParticipantId, int> Slots(DRPGBattleStateMutationFixture test) => new Dictionary<ParticipantId, int> {
        { new ParticipantId(test.Player.RuntimeId), 0 }, { new ParticipantId(test.Enemy.RuntimeId), 0 } };
    private static bool Commit(DRPGBattleSessionState state, BattleRuleMutationLease lease, ParticipantId actor,
        IReadOnlyDictionary<ParticipantId, int> slots, out DRPGBattleSettlementLedger ledger, out string error)
    {
        return state.TryCommitSettlement(lease, actor, slots, out ledger, out error);
    }
    private static object Get(object target, string name) => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

    private static DRPGResolvedStatusSpec Status(string id, float hp, DRPGStatusTriggerTiming timing = DRPGStatusTriggerTiming.AfterAction, int priority = 0, int duration = 2)
    {
        var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        try
        {
            Set(definition, "statusId", id); Set(definition, "turnStartHpDelta", hp); Set(definition, "maxStacks", 10);
            Set(definition, "durationTurns", duration); Set(definition, "triggerTiming", timing); Set(definition, "priority", priority);
            return DRPGResolvedStatusSpec.From(definition);
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    private static void Apply(DRPGBattleStateMutationFixture test, BattleParticipantRuntime owner, DRPGResolvedStatusSpec spec, int stacks = 1, BattleParticipantRuntime source = null)
    {
        var draft = test.CreateWorkspace();
        draft.For(new ParticipantId(owner.RuntimeId)).ApplyStatus(spec, stacks, 0, new ParticipantId((source ?? owner).RuntimeId));
        Check(test.State.TryCommitJournal(test.Lease, draft).Succeeded, "Fixture status application failed.");
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
#endif
