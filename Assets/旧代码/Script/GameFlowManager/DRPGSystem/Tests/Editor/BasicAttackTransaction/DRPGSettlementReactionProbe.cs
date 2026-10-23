#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGSettlementReactionProbe
{
    [UnityEditor.MenuItem("Tools/ANKALUTE/DRPG/Run Settlement Reaction Probes")]
    public static void RunFromMenu() { RunAll(); Debug.Log("Settlement causal-rule probes PASS; no Stage9 presentation coverage."); }
    internal static void RunAll()
    {
        StatusReactionFinishesBeforeNextLane(); OnDefeatRequiresThisStatusToKillItsOwner();
        PassiveAndEventIdentityContinueFromStage7(); ReactionBudgetIsSharedAcrossStages();
        LateReactionFailureDiscardsBothLanesAndCanRetry(); ReentrantSettlementCannotCommitInsideCollect();
        ReactionFactsChangeSettlementFingerprint(); ReactionSourceCannotImpersonateAnotherActor();
        PreparedKernelCarriesContinuationOnlyAfterReturn();
        CausalChildrenAreDepthFirstAndTypeRestricted(); ContinuationRejectsForeignRootAndChangedReplay();
        NewChildStatusWaitsForNextOrdinaryBoundary();
        LedgerFreezesActualChildSidesSeparatelyFromLane();
    }

    internal static void PreparedKernelCarriesContinuationOnlyAfterReturn() =>
        DRPGActionResourceJournalProbe.SettlementKernelCarriesContinuationAfterReturn();

    // Break caught: dropping non-hit roots or delaying children until after the enemy lane.
    internal static void StatusReactionFinishesBeforeNextLane()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "party-poison", -5);
            test.Status(test.Enemy, "enemy-poison", -3);
            var strike = test.Spec(test.Player, test.Enemy, 7);
            var source = new Source("status-response", test.Player, false, (context, requests) => {
                if (!IsSettlement(context) || context.TargetParticipant.Value != test.Player.RuntimeId) return;
                Check(!context.HasHit && context.Effects.Count == 0, "Status was disguised as an attack hit.");
                requests.Add(Request(context, "response", strike));
            });
            test.Begin(source);
            long revision = test.State.BattleRevision;
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 95 && test.Enemy.CurrentHp == 117,
                "Status reaction was omitted or primary HP was replayed. player=" + test.Player.CurrentHp +
                "; enemy=" + test.Enemy.CurrentHp + "; posture=" + test.Enemy.CurrentPosture);
            Check(ledger.Steps[1].HpBefore == 120 && ledger.Steps[1].HpAfter == 117,
                "Cross-side child did not finish before the next lane root.");
            Check(test.State.BattleRevision == revision + 1, "Causal groups committed more than once.");
            Check(test.Commit(out var replay, out error) && ReferenceEquals(ledger, replay) && test.State.BattleRevision == revision + 1,
                "Settlement replay rebuilt reactions or advanced State.");
        }
    }

    internal static void OnDefeatRequiresThisStatusToKillItsOwner()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "lethal", -100); test.Status(test.Enemy, "later", -3);
            var strike = test.Spec(test.Player, test.Enemy, 9);
            test.Begin(new Source("last-word", test.Player, false, (c, requests) => {
                if (IsSettlement(c)) requests.Add(Request(c, "last-word", strike, onDefeat: true));
            }));
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 0 && test.Enemy.CurrentHp == 117 &&
                test.Enemy.CurrentPosture == 51 && test.Player.IsDeployed,
                "Causal status death did not trigger its owner, repeated on unrelated status, or departed early. " +
                "player=" + test.Player.CurrentHp + "; enemy=" + test.Enemy.CurrentHp +
                "; posture=" + test.Enemy.CurrentPosture + "; deployed=" + test.Player.IsDeployed);
            Check(ledger.Groups[0].Children.Count == 1 && ledger.Groups[0].Children[0].State == DRPGImmediateReactionState.Resolved &&
                ledger.Groups[1].Children[0].State == DRPGImmediateReactionState.SkippedIneligible,
                "OnDefeat lost actual causal eligibility.");
        }
    }

    internal static void PassiveAndEventIdentityContinueFromStage7()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "poison", -5);
            var strike = test.Spec(test.Player, test.Enemy, 7);
            test.Begin(new Source("once-per-root", test.Player, true, (c, requests) => {
                if (c.Depth == 0) requests.Add(Request(c, "passive", strike));
            }));
            Check(test.Enemy.CurrentHp == 120, "Stage7 passive fixture failed.");
            Check(test.Commit(out var ledger, out var error), error);
            var child = ledger.Groups[0].Children[0]; var prior = test.PrimaryClosure.Entries[0];
            Check(test.Enemy.CurrentHp == 120 && child.State == DRPGImmediateReactionState.SkippedDuplicate,
                "Stage9 reset a passive already consumed by Stage7.");
            Check(child.EventId != prior.EventId && child.InsertionOrdinal > prior.InsertionOrdinal && child.Identity == prior.Identity,
                "Reaction sequence restarted or escaped its Root identity.");
        }
    }

    internal static void ReactionBudgetIsSharedAcrossStages()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "poison", -5); test.Status(test.Enemy, "poison", -3);
            var strike = test.Spec(test.Player, test.Enemy, 1);
            test.Begin(new Source("many", test.Player, false, (c, requests) => {
                if (c.Depth != 0) return;
                for (int i = 0; i < (IsSettlement(c) ? 2 : 31); i++)
                    requests.Add(new DRPGImmediateReactionRequest(c.Identity, c.EventId, "r" + i.ToString("D2"),
                        DRPGImmediateReactionCategory.OtherImmediate, 0, strike,
                        DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1), DRPGHitOutcome.Evaded));
            }));
            Check(test.PrimaryClosure.Entries.Count == 31, "Stage7 budget fixture failed.");
            Check(test.Commit(out var ledger, out var error), error);
            Check(ledger.Groups[0].Children[0].State == DRPGImmediateReactionState.Resolved &&
                ledger.Groups[0].Children[1].State == DRPGImmediateReactionState.SkippedBudget &&
                ledger.Groups[1].Children[0].State == DRPGImmediateReactionState.SkippedBudget && test.Enemy.CurrentHp == 117,
                "Stage9 or its next lane restarted the 32-step Root budget or damaged an evaded target.");
        }
    }

    internal static void LateReactionFailureDiscardsBothLanesAndCanRetry()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "first", -5); test.Status(test.Enemy, "last", -3);
            var strike = test.Spec(test.Player, test.Enemy, 7); bool fail = true;
            test.Begin(new Source("late-fault", test.Player, true, (c, requests) => {
                if (!IsSettlement(c)) return;
                if (c.TargetParticipant.Value == test.Enemy.RuntimeId && fail) throw new InvalidOperationException("Injected late child fault.");
                requests.Add(Request(c, "once", strike));
            }));
            long revision = test.State.BattleRevision;
            var partyStatuses = test.Player.Statuses.Snapshot(); var enemyStatuses = test.Enemy.Statuses.Snapshot();
            Check(!test.Commit(out var failed, out _) && failed == null && test.Player.CurrentHp == 100 && test.Enemy.CurrentHp == 120 &&
                test.State.BattleRevision == revision && DRPGActionStatusChange.SameSnapshots(partyStatuses, test.Player.Statuses.Snapshot()) &&
                DRPGActionStatusChange.SameSnapshots(enemyStatuses, test.Enemy.Statuses.Snapshot()), "Late reaction fault leaked partial HP, status duration or expiry.");
            fail = false;
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 95 && test.Enemy.CurrentHp == 117 &&
                ledger.Groups[0].Children[0].State == DRPGImmediateReactionState.Resolved,
                "Failed speculative attempt consumed the sealed passive budget.");
        }
    }

    internal static void ReentrantSettlementCannotCommitInsideCollect()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "poison", -5);
            bool attempted = false;
            test.Begin(new Source("reentry", test.Player, false, (c, requests) => {
                if (!IsSettlement(c) || attempted) return;
                attempted = true; long revision = test.State.BattleRevision;
                Check(!test.Commit(out var nested, out _) && nested == null && test.State.BattleRevision == revision && test.Player.CurrentHp == 100,
                    "A Collect callback committed a nested settlement.");
            }));
            long before = test.State.BattleRevision;
            Check(test.Commit(out _, out var error) && attempted && test.State.BattleRevision == before + 1 && test.Player.CurrentHp == 95, error);
        }
    }

    // Break caught: the fingerprint includes status roots but omits typed child facts.
    internal static void ReactionFactsChangeSettlementFingerprint()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "poison", -5);
            var firstSpec = test.Spec(test.Player, test.Enemy, 7); var secondSpec = test.Spec(test.Player, test.Enemy, 9);
            bool second = false;
            test.Begin(new Source("fingerprint", test.Player, false, (c, requests) => {
                if (IsSettlement(c)) requests.Add(Request(c, "child", second ? secondSpec : firstSpec));
            }));
            var first = test.BuildDetached(); second = true; var other = test.BuildDetached();
            Check(first.Steps[0].HpAfter == other.Steps[0].HpAfter && first.Fingerprint != other.Fingerprint,
                "Distinct child damage/skill facts collide in the settlement fingerprint.");
            Check(test.Enemy.CurrentHp == 120 && test.Player.CurrentHp == 100, "Fingerprint construction mutated live State.");
        }
    }

    // Break caught: collecting on one owner can submit a request impersonating another actor.
    internal static void ReactionSourceCannotImpersonateAnotherActor()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "poison", -5); var strike = test.Spec(test.Player, test.Enemy, 7);
            test.Begin(new Source("wrong-owner", test.Enemy, false, (c, requests) => {
                if (IsSettlement(c)) requests.Add(Request(c, "invalid", strike));
            }));
            long revision = test.State.BattleRevision;
            Check(!test.Commit(out _, out _) && test.Player.CurrentHp == 100 && test.Enemy.CurrentHp == 120 && test.State.BattleRevision == revision,
                "A reaction source impersonated a different actor and committed the status draft.");
        }
    }

    internal static void CausalChildrenAreDepthFirstAndTypeRestricted()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "root", -5);
            var playerStrike = test.Spec(test.Player, test.Enemy, 7); var enemyStrike = test.Spec(test.Enemy, test.Player, 3);
            test.Begin(new Source("player", test.Player, false, (c, requests) => {
                if (IsSettlement(c))
                {
                    requests.Add(new DRPGImmediateReactionRequest(c.Identity, c.EventId, "counter", DRPGImmediateReactionCategory.Counterattack, 0,
                        playerStrike, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1)));
                    requests.Add(Request(c, "sibling", playerStrike));
                }
            }), new Source("enemy", test.Enemy, false, (c, requests) => {
                if (c.Category != DRPGImmediateReactionCategory.Counterattack) return;
                requests.Add(new DRPGImmediateReactionRequest(c.Identity, c.EventId, "forbidden-counter", DRPGImmediateReactionCategory.Counterattack, 0,
                    enemyStrike, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1)));
                requests.Add(new DRPGImmediateReactionRequest(c.Identity, c.EventId, "reflect", DRPGImmediateReactionCategory.Reflected, 0,
                    enemyStrike, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1)));
            }));
            Check(test.Commit(out var ledger, out var error), error);
            var children = ledger.Groups[0].Children;
            Check(children.Count == 4 && children[0].ReactionId == "counter" && children[1].State == DRPGImmediateReactionState.SkippedTypeMatrix &&
                children[2].ReactionId == "reflect" && children[2].ParentEventId == children[0].EventId && children[3].ReactionId == "sibling" &&
                children[3].ParentEventId == ledger.Steps[0].EventId && test.Player.CurrentHp == 95 &&
                test.Enemy.CurrentHp == 120,
                "Settlement flattened the child tree, reordered siblings, or allowed counter-to-counter.");
            Check(ledger.Groups[0].Lane == DRPGBattleSide.Player, "Cross-side child changed its root lane.");
        }
    }

    internal static void ContinuationRejectsForeignRootAndChangedReplay()
    {
        using (var test = new Fixture())
        using (var other = new Fixture())
        {
            test.Status(test.Player, "root", -5); test.Begin(); other.Begin();
            long revision = test.State.BattleRevision;
            Check(!test.CommitUsing(other.PrimaryClosure.Continuation, out _, out _) && test.State.BattleRevision == revision && test.Player.CurrentHp == 100,
                "A foreign Root continuation was accepted.");
            Check(test.Commit(out _, out var error), error);
            Check(!test.CommitUsing(null, out _, out _) && test.State.BattleRevision == revision + 1,
                "A replay silently removed the committed reaction policy/continuation.");
        }
    }

    internal static void NewChildStatusWaitsForNextOrdinaryBoundary()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "root", -5);
            var childStatus = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                Set(childStatus, "statusId", "new-poison"); Set(childStatus, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
                Set(childStatus, "turnStartHpDelta", -30f); Set(childStatus, "durationTurns", 2);
                var spec = test.StatusSpec(test.Player, test.Enemy, childStatus);
                test.Begin(new Source("apply", test.Player, true, (c, requests) => {
                    if (IsSettlement(c)) requests.Add(Request(c, "apply-poison", spec));
                }));
                Check(test.Commit(out var ledger, out var error), error);
                var child = ledger.Groups[0].Children[0]; var applied = test.Enemy.Statuses.Snapshot()[0];
                Check(ledger.Steps.Count == 1 && child.Batch.Hits.Count == 0 && child.RequiresPresentation && test.Enemy.CurrentHp == 120 &&
                    applied.RemainingTurns == 2 && applied.AppliedBoundarySerial == 1 && applied.EligibleFromBoundarySerial == 2,
                    "A status applied by a causal child ticked immediately, lost its typed effect, or was faked as a hit.");
            }
            finally { UnityEngine.Object.DestroyImmediate(childStatus); }
        }
    }

    // Break caught: a Player-lane group incorrectly labels its enemy child target as Player.
    internal static void LedgerFreezesActualChildSidesSeparatelyFromLane()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "root", -5); var strike = test.Spec(test.Player, test.Enemy, 7);
            test.Begin(new Source("cross-side", test.Player, false, (c, requests) => {
                if (IsSettlement(c)) requests.Add(Request(c, "enemy-target", strike));
            }));
            Check(test.Commit(out var ledger, out var error), error);
            var sides = ledger.ParticipantSides;
            var child = ledger.Groups[0].Children[0];
            Check(ledger.Groups[0].Lane == DRPGBattleSide.Player && sides[child.TargetParticipant] == DRPGBattleSide.Enemy &&
                sides[child.SourceParticipant] == DRPGBattleSide.Player, "Causal lane replaced the child's actual affected side.");
            if (sides is IDictionary<ParticipantId, DRPGBattleSide> mutable)
            {
                bool rejected = false;
                try { mutable[child.TargetParticipant] = DRPGBattleSide.Player; } catch (NotSupportedException) { rejected = true; }
                Check(rejected && sides[child.TargetParticipant] == DRPGBattleSide.Enemy, "A reader mutated sealed participant sides.");
            }
        }
    }

    private static bool IsSettlement(DRPGImmediateReactionContext context) => context.Settlement != null;
    private static DRPGImmediateReactionRequest Request(DRPGImmediateReactionContext context, string id,
        DRPGResolvedActionSpec spec, bool onDefeat = false) => new DRPGImmediateReactionRequest(context.Identity,
        context.EventId, id, DRPGImmediateReactionCategory.OtherImmediate, 0, spec,
        DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f), onDefeat: onDefeat);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private sealed class Source : IDRPGImmediateReactionSource
    {
        private readonly Action<DRPGImmediateReactionContext, ICollection<DRPGImmediateReactionRequest>> collect;
        internal Source(string id, BattleParticipantRuntime owner, bool passive,
            Action<DRPGImmediateReactionContext, ICollection<DRPGImmediateReactionRequest>> collect)
        { SourceId = id; Participant = new ParticipantId(owner.RuntimeId); IsPassive = passive; this.collect = collect; }
        public string SourceId { get; }
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive { get; }
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests) => collect(context, requests);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DRPGBattleStateMutationFixture fixture = DRPGBattleStateMutationFixture.Create();
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private DRPGImmediateReactionLedgerBuilder.Continuation continuation;
        private readonly Dictionary<ParticipantId, int> homes = new Dictionary<ParticipantId, int>();
        internal BattleParticipantRuntime Player => fixture.Player;
        internal BattleParticipantRuntime Enemy => fixture.Enemy;
        internal DRPGBattleSessionState State => fixture.State;
        internal BattleRuleMutationLease Lease { get; private set; }
        internal DRPGImmediateReactionClosureResult PrimaryClosure { get; private set; }
        internal void Status(BattleParticipantRuntime owner, string id, float hp)
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>(); assets.Add(definition);
            Set(definition, "statusId", id); Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
            Set(definition, "durationTurns", 2); Set(definition, "turnStartHpDelta", hp);
            var draft = fixture.CreateWorkspace();
            draft.For(new ParticipantId(owner.RuntimeId)).ApplyStatus(DRPGResolvedStatusSpec.From(definition), 1, 0, new ParticipantId(owner.RuntimeId));
            Check(State.TryCommitJournal(fixture.Lease, draft).Succeeded, "Status fixture failed.");
        }
        internal DRPGResolvedActionSpec Spec(BattleParticipantRuntime actor, BattleParticipantRuntime target, float power)
        {
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>(); assets.Add(skill);
            Set(skill, "skillId", "settlement-response-" + assets.Count); Set(skill, "power", power);
            Set(skill, "effects", new List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash) });
            return DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill), actor);
        }
        internal DRPGResolvedActionSpec StatusSpec(BattleParticipantRuntime actor, BattleParticipantRuntime target, DRPGStatusDefinitionSO status)
        {
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>(); assets.Add(skill);
            Set(skill, "skillId", "apply-child-status");
            Set(skill, "effects", new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.ApplyStatus(DRPGActionEffectTarget.PrimaryTarget, status) });
            return DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill), actor);
        }
        internal void Begin(params IDRPGImmediateReactionSource[] sources)
        {
            fixture.Lease.Dispose();
            var identity = new ActionIdentity(fixture.BattleGeneration, 1, 19);
            Check(fixture.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(identity), out var lease, out var error), error);
            Lease = lease;
            var draft = fixture.CreateWorkspace(); var resolver = new DRPGActionResolver();
            var primary = resolver.ResolveIntoWorkspace(Spec(Player, Enemy, 20), Player, Enemy, new[] { Player, Enemy },
                draft, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            PrimaryClosure = new DRPGImmediateReactionLedgerBuilder().Build(identity, "primary-event", primary,
                draft, new[] { Player, Enemy }, sources, resolver);
            Check(PrimaryClosure.Succeeded, PrimaryClosure.Error);
            Check(State.TryCommitJournal(Lease, PrimaryClosure.Workspace).Succeeded, "Primary commit failed.");
            continuation = PrimaryClosure.Continuation;
            if (Player.CurrentHp > 0) homes.Add(new ParticipantId(Player.RuntimeId), 0);
            if (Enemy.CurrentHp > 0) homes.Add(new ParticipantId(Enemy.RuntimeId), 0);
        }
        internal bool Commit(out DRPGBattleSettlementLedger ledger, out string error)
        {
            return State.TryCommitSettlement(Lease, new ParticipantId(Player.RuntimeId), homes, out ledger, out error, continuation);
        }
        internal bool CommitUsing(DRPGImmediateReactionLedgerBuilder.Continuation selected, out DRPGBattleSettlementLedger ledger, out string error) =>
            State.TryCommitSettlement(Lease, new ParticipantId(Player.RuntimeId), homes, out ledger, out error, selected);
        internal DRPGBattleSettlementLedger BuildDetached() => DRPGBattleSettlementLedger.Build(Lease.Owner.Action,
            new ParticipantId(Player.RuntimeId), fixture.CreateWorkspace(), homes, continuation);
        public void Dispose()
        {
            Lease?.Dispose(); fixture.Dispose();
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void SettlementKernelCarriesContinuationAfterReturn()
    {
        using (var test = new ProductionRulePlan())
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            var skill = CreateDamageSkill("shared-kernel-passive", 7f);
            try
            {
                var state = test.Session.TransactionState; var arbiter = test.Session.TransactionArbiter;
                test.Lease.Dispose();
                Check(arbiter.TryAcquire(BattleTransactionIdentity.ForTestHarness(new BattleStartIdentity(test.Identity.BattleGeneration, 99)), out var setup, out var error), error);
                using (setup)
                {
                    StatusSet(definition, "statusId", "kernel-poison"); StatusSet(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
                    StatusSet(definition, "turnStartHpDelta", -5f); StatusSet(definition, "durationTurns", 2);
                    var draft = state.CreateWorkspace();
                    draft.For(new ParticipantId(test.Player.RuntimeId)).ApplyStatus(DRPGResolvedStatusSpec.From(definition), 1, 0, new ParticipantId(test.Player.RuntimeId));
                    Check(state.TryCommitJournal(setup, draft).Succeeded, "Kernel status fixture failed.");
                }
                Check(arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(test.Identity), out var root, out error), error);
                using (root)
                {
                    var reaction = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(test.Player.RuntimeId, test.Enemy.RuntimeId, skill), test.Player);
                    var source = new MutableProductionSource(new ParticipantId(test.Player.RuntimeId), reaction) { IsPassive = true };
                    Check(test.Session.StandardBasicAttackRules.TryPrepare(test.Frozen, test.Spec, root, out var action, out error, new[] { source }), error);
                    using (action)
                    {
                        var homes = new Dictionary<ParticipantId, int> {
                            { new ParticipantId(test.Player.RuntimeId), 0 }, { new ParticipantId(test.Friend.RuntimeId), 1 }, { new ParticipantId(test.Enemy.RuntimeId), 0 } };
                        Check(!CommitPreparedSettlement(action, homes, out _, out _), "Prepared action settled before primary impact.");
                        Check(action.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)).Status == DRPGPrimaryImpactCommitStatus.Committed,
                            "Kernel primary failed.");
                        Check(!CommitPreparedSettlement(action, homes, out _, out _), "Prepared action settled before post-return reconciliation.");
                        Check(action.TryReconcileFormationAfterReturn(out _, out error), error);
                        long revision = state.BattleRevision;
                        Check(CommitPreparedSettlement(action, homes, out var ledger, out error), "Missing prepared settlement handoff: " + error);
                        Check(test.Player.CurrentHp == 95 && test.Enemy.CurrentHp == 120 &&
                            test.Enemy.CurrentPosture == 53 && test.Player.CurrentMp == 28 &&
                            ledger.Groups[0].Children[0].State == DRPGImmediateReactionState.SkippedDuplicate && state.BattleRevision == revision + 1,
                            "Kernel lost its Stage7 continuation or replayed costs/primary damage at Settlement.");
                        Check(CommitPreparedSettlement(action, homes, out var replay, out error) && ReferenceEquals(ledger, replay) && state.BattleRevision == revision + 1 &&
                            test.LegacyEvents == 0 && test.LedgerEvents == 1 && arbiter.Owns(root, root.Owner),
                            "Settlement replay republished action events, completed the action or released its owner.");
                        homes[new ParticipantId(test.Player.RuntimeId)] = 3;
                        Check(!CommitPreparedSettlement(action, homes, out _, out _), "Prepared settlement accepted a changed replay layout.");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    private static bool CommitPreparedSettlement(DRPGPreparedBasicAttackRuleAction action, IReadOnlyDictionary<ParticipantId, int> homes,
        out DRPGBattleSettlementLedger ledger, out string error)
    {
        return action.TryCommitSettlement(homes, out ledger, out error);
    }
}
#endif
