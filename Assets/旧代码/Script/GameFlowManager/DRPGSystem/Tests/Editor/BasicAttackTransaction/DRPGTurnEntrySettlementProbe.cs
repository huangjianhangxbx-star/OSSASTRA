#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGTurnEntrySettlementProbe
{
    [UnityEditor.MenuItem("Tools/ANKALUTE/DRPG/Run Turn Entry Shared Settlement Probes")]
    public static void RunFromMenu()
    {
        int count = 0;
        foreach (var type in new[] { typeof(DRPGTurnEntrySettlementProbe), typeof(DRPGTurnEntrySequenceProbe), typeof(DRPGTurnEntryPresentationProbe) })
        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0) continue;
            try { method.Invoke(null, null); count++; }
            catch (TargetInvocationException error) { throw new InvalidOperationException(type.Name + "." + method.Name, error.InnerException); }
        }
        Debug.Log("TurnEntry shared settlement PASS: " + count + " checks; Session/FlowRoot candidate integration remains pending.");
    }

    // Break caught: the shared ledger rejects TurnEntry or ticks everyone/AfterAction before publishing the candidate.
    internal static void OnlyCandidateTurnStartIsCommittedOnce()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "entry", -3, 2);
            test.Status(test.Player, "after", -40, timing: DRPGStatusTriggerTiming.AfterAction);
            test.Status(test.Enemy, "other-entry", -20);
            test.Begin(test.Player);
            long revision = test.State.BattleRevision;
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 94 && test.Enemy.CurrentHp == 120 && ledger.Steps.Count == 1,
                "TurnEntry must tick only the candidate's TurnStart instance, merging its two stacks.");
            Check(ledger.Steps[0].HpBefore == 100 && ledger.Steps[0].HpAfter == 94 &&
                ledger.Steps[0].Status.Before.RemainingTurns == 2 && ledger.Steps[0].Status.After.RemainingTurns == 1,
                "The frozen status step did not retain one duration and exact before/after values.");
            Check(!ledger.Identity.IsValid && !ledger.Steps[0].Identity.IsValid,
                "TurnEntry was disguised as an ActionIdentity.");
            Check(test.Commit(out var replay, out error) && ReferenceEquals(ledger, replay) &&
                test.State.BattleRevision == revision + 1 && test.Player.CurrentHp == 94,
                "Replaying TurnEntry consumed another tick or committed twice.");
            Check(!test.Arbiter.IsCommandInputOpen && test.Arbiter.Owns(test.Lease, test.Lease.Owner),
                "Ledger completion released the candidate owner before presentation.");
        }
    }

    // Break caught: a lethal TurnStart removes the still-visible participant or ticks its later ordinary healing.
    internal static void LethalCandidateStaysDeployedForItsPresentationBarrier()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "a-lethal", -100);
            test.Status(test.Player, "z-heal", 40);
            test.Begin(test.Player);
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 0 && test.Player.IsDeployed && ledger.Steps.Count == 1 && ledger.Steps[0].RuleDefeated,
                "TurnStart death revived, departed early or lost the pending-death fact.");
            Check(test.Player.Statuses.Snapshot()[1].RemainingTurns == 2 && test.Core.Scheduler.CurrentTime == 0,
                "A skipped effect consumed duration or the ledger advanced the scheduler on its own.");
        }
    }

    // Break caught: an empty TurnEntry has no immutable result, preventing an explicit 0.30s presentation handoff.
    internal static void EmptyCandidateStillProducesOneOwnedLedger()
    {
        using (var test = new Fixture())
        {
            test.Begin(test.Enemy);
            long revision = test.State.BattleRevision;
            Check(test.Commit(out var ledger, out var error), error);
            Check(ledger.Steps.Count == 0 && new DRPGSettlementPresentationPlan(ledger).IsEmpty &&
                test.State.BattleRevision == revision + 1 && ledger.Fingerprint != 0 && !ledger.Identity.IsValid,
                "Empty TurnEntry omitted its explicit, action-free ledger.");
        }
    }

    // Break caught: partial HP/duration writes survive a later overflow inside the same candidate workspace.
    internal static void LateInvalidTickRollsBackTheWholeCandidate()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "a-poison", -3);
            test.Status(test.Player, "z-overflow", float.MaxValue, 2);
            test.Begin(test.Player);
            var before = test.Player.Statuses.Snapshot();
            long revision = test.State.BattleRevision;
            Check(!test.Commit(out var ledger, out _) && ledger == null && test.Player.CurrentHp == 100 &&
                test.State.BattleRevision == revision && DRPGActionStatusChange.SameSnapshots(before, test.Player.Statuses.Snapshot()),
                "A rejected candidate ledger leaked earlier HP or duration writes.");
        }
    }

    // Break caught: same-generation but wrong candidate, stale lease or altered replay can tick another participant.
    internal static void CandidateMismatchAndStaleLeaseCannotCommit()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "entry", -3);
            test.Status(test.Enemy, "entry", -5);
            test.Begin(test.Player);
            long revision = test.State.BattleRevision;
            Check(!test.State.TryCommitSettlement(test.Lease, new ParticipantId(test.Enemy.RuntimeId), test.Homes, out _, out _) &&
                test.State.BattleRevision == revision && test.Enemy.CurrentHp == 120,
                "TurnEntry owner ticked a different actor.");
            Check(test.Commit(out var ledger, out var error), error);
            var changed = new Dictionary<ParticipantId, int>(test.Homes) { [new ParticipantId(test.Player.RuntimeId)] = 1 };
            Check(!test.State.TryCommitSettlement(test.Lease, test.Entry.Actor, changed, out _, out _),
                "A replay replaced frozen Home Slots.");
            test.Lease.Dispose();
            Check(!test.Commit(out _, out _) && test.Player.CurrentHp == 97 && test.State.BattleRevision == revision + 1,
                "Stale lease returned a cached ledger or repeated its mutation.");
        }
    }

    // Break caught: a status has no hit, so the attack-only closure drops its child or allocates a fake action.
    internal static void CandidateCausalChildrenUseTheFullTurnEntryOwner()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "entry", -3);
            var response = test.Spec(test.Player, test.Enemy, 7);
            bool childObserved = false;
            test.Begin(test.Player);
            test.Capture(new Source(test.Player, (context, requests) => {
                Check(context.Owner == test.Lease.Owner && !context.Identity.IsValid, "Reaction lost its full candidate owner.");
                if (context.Depth == 0)
                {
                    Check(!context.HasHit && context.Settlement != null, "TurnStart was faked as a primary hit.");
                    requests.Add(Request(context, response));
                }
                else childObserved = true;
            }));
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 97 && test.Enemy.CurrentHp == 113 && childObserved &&
                ledger.Owner == test.Lease.Owner && ledger.Steps[0].Transaction == test.Lease.Owner &&
                ledger.Groups[0].Children.Count == 1 && !ledger.Groups[0].Children[0].Identity.IsValid,
                "Candidate status/child did not commit together or escaped its owner.");
        }
    }

    // Break caught: OnDefeat is suppressed because its candidate is rule-dead, or death causes immediate release.
    internal static void CandidateOnDefeatRunsBeforeAnyDeparture()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "lethal", -100);
            var response = test.Spec(test.Player, test.Enemy, 7);
            test.Begin(test.Player);
            test.Capture(new Source(test.Player, (context, requests) => {
                if (context.Settlement != null) requests.Add(Request(context, response, onDefeat: true));
            }));
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 0 && test.Player.IsDeployed && test.Enemy.CurrentHp == 113 &&
                ledger.Groups[0].Children[0].State == DRPGImmediateReactionState.Resolved && !test.Arbiter.IsCommandInputOpen,
                "Candidate OnDefeat was skipped, departed early, or opened input.");
        }
    }

    // Break caught: the first failed attempt spends passive dedupe or publishes a partial status/child workspace.
    internal static void CausalFailureRollsBackAndRetriesWithoutSpendingPassiveBudget()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "a-entry", -3); test.Status(test.Player, "z-entry", -4);
            var response = test.Spec(test.Player, test.Enemy, 7); bool fail = true;
            test.Begin(test.Player);
            test.Capture(new Source(test.Player, (context, requests) => {
                if (context.Settlement == null) return;
                if (context.Settlement.HpBefore == 97 && fail) throw new InvalidOperationException("Injected late TurnEntry source fault.");
                requests.Add(Request(context, response));
            }, passive: true));
            long revision = test.State.BattleRevision;
            Check(!test.Commit(out var rejected, out _) && rejected == null && test.Player.CurrentHp == 100 &&
                test.Enemy.CurrentHp == 120 && test.State.BattleRevision == revision && test.Player.Statuses.Snapshot()[0].RemainingTurns == 2,
                "Failed TurnEntry leaked a partial root or child.");
            fail = false;
            Check(test.Commit(out var ledger, out var error), error);
            Check(test.Player.CurrentHp == 93 && test.Enemy.CurrentHp == 113 &&
                ledger.Groups[0].Children[0].State == DRPGImmediateReactionState.Resolved &&
                ledger.Groups[1].Children[0].State == DRPGImmediateReactionState.SkippedDuplicate,
                "Retry lost its passive or reset once-per-transaction dedupe between status roots.");
        }
    }

    // Break caught: a reentrant source commits another candidate ledger while the outer workspace is uncommitted.
    internal static void CandidateCollectionCannotReenterTheSharedStore()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "entry", -3); bool rejected = false;
            test.Begin(test.Player);
            test.Capture(new Source(test.Player, (context, requests) => {
                rejected = !test.Commit(out _, out _) && test.Player.CurrentHp == 100;
            }));
            Check(test.Commit(out _, out var error) && rejected && test.Player.CurrentHp == 97, error);
        }
    }

    // Break caught: all TurnEntry actions have default ActionIdentity, letting a foreign candidate spoof its parent.
    internal static void AnotherCandidateCannotSpoofAReactionWithTheSameEmptyActionId()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "entry", -3); var response = test.Spec(test.Player, test.Enemy, 7);
            test.Begin(test.Player);
            var foreign = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(test.Core.BattleGeneration, 2, test.Entry.Actor));
            test.Capture(new Source(test.Player, (context, requests) => {
                if (context.Depth == 0) requests.Add(new DRPGImmediateReactionRequest(foreign, context.EventId, "spoof",
                    DRPGImmediateReactionCategory.OtherImmediate, 0, response, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1)));
            }));
            Check(!test.Commit(out _, out _) && test.Player.CurrentHp == 100 && test.Enemy.CurrentHp == 120,
                "A foreign candidate request passed the default-action identity comparison.");
        }
    }

    // Break caught: returning the cached ledger masks later rule writes and presents an obsolete candidate snapshot.
    internal static void CandidateReplayCannotHideAnInterveningRuleMutation()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "entry", -3); test.Begin(test.Player);
            Check(test.Commit(out _, out var error), error);
            var later = test.State.CreateWorkspace(); later.For(test.Entry.Actor).SetHitPoints(90);
            Check(test.State.TryCommitJournal(test.Lease, later).Succeeded, "Intervening revision fixture failed.");
            long revision = test.State.BattleRevision;
            Check(!test.Commit(out var stale, out _) && stale == null && test.Player.CurrentHp == 90 && test.State.BattleRevision == revision,
                "Candidate replay concealed an intervening rule revision.");
        }
    }

    // Break caught: each ordinary status root starts a fresh 32-step reaction budget, allowing an unbounded candidate transaction.
    internal static void CandidateReactionBudgetIsSharedAcrossAllStatusRoots()
    {
        using (var test = new Fixture())
        {
            test.Status(test.Player, "a-entry", -3); test.Status(test.Player, "z-entry", -4);
            var response = test.Spec(test.Player, test.Enemy, 1); test.Begin(test.Player);
            test.Capture(new Source(test.Player, (context, requests) => {
                if (context.Settlement == null) return;
                for (int i = 0; i < 33; i++) requests.Add(new DRPGImmediateReactionRequest(context.Owner, context.EventId, "bounded-" + i.ToString("D2"),
                    DRPGImmediateReactionCategory.OtherImmediate, 0, response, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1), DRPGHitOutcome.Evaded));
            }));
            Check(test.Commit(out var ledger, out var error), error);
            Check(ledger.Groups[0].Children.Count == 33 && ledger.Groups[0].Children[31].State == DRPGImmediateReactionState.Resolved &&
                ledger.Groups[0].Children[32].State == DRPGImmediateReactionState.SkippedBudget &&
                ledger.Groups[1].Children[0].State == DRPGImmediateReactionState.SkippedBudget &&
                ledger.Groups[1].Children[32].State == DRPGImmediateReactionState.SkippedBudget && test.Enemy.CurrentHp == 120 && test.Player.CurrentHp == 93,
                "TurnEntry causal budget reset at the next ordinary status root.");
        }
    }

    private static DRPGImmediateReactionRequest Request(DRPGImmediateReactionContext context, DRPGResolvedActionSpec spec,
        bool onDefeat = false) => new DRPGImmediateReactionRequest(context.Owner, context.EventId, "response",
            DRPGImmediateReactionCategory.OtherImmediate, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1), onDefeat: onDefeat);

    private sealed class Source : IDRPGImmediateReactionSource
    {
        private readonly Action<DRPGImmediateReactionContext, ICollection<DRPGImmediateReactionRequest>> collect;
        internal Source(BattleParticipantRuntime actor, Action<DRPGImmediateReactionContext, ICollection<DRPGImmediateReactionRequest>> collect,
            bool passive = false) { Participant = new ParticipantId(actor.RuntimeId); this.collect = collect; IsPassive = passive; }
        public string SourceId => "turn-entry-response";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive { get; }
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests) => collect(context, requests);
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        internal readonly DRPGBattleStateMutationFixture Core = DRPGBattleStateMutationFixture.Create();
        internal DRPGBattleSessionState State => Core.State;
        internal DRPGBattleTransactionArbiter Arbiter => Core.Arbiter;
        internal BattleParticipantRuntime Player => Core.Player;
        internal BattleParticipantRuntime Enemy => Core.Enemy;
        internal TurnEntryIdentity Entry { get; private set; }
        internal BattleRuleMutationLease Lease { get; private set; }
        internal DRPGImmediateReactionLedgerBuilder.Continuation Continuation { get; private set; }
        internal Dictionary<ParticipantId, int> Homes => new Dictionary<ParticipantId, int> {
            { new ParticipantId(Player.RuntimeId), 0 }, { new ParticipantId(Enemy.RuntimeId), 0 } };
        internal void Status(BattleParticipantRuntime owner, string id, float hp, int stacks = 1,
            DRPGStatusTriggerTiming timing = DRPGStatusTriggerTiming.TurnStart)
        {
            var asset = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>(); assets.Add(asset);
            foreach (var field in new Dictionary<string, object> { { "statusId", id }, { "triggerTiming", timing },
                { "turnStartHpDelta", hp }, { "durationTurns", 2 }, { "maxStacks", Math.Max(2, stacks) } })
                typeof(DRPGStatusDefinitionSO).GetField(field.Key, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, field.Value);
            var draft = State.CreateWorkspace(); var participant = new ParticipantId(owner.RuntimeId);
            draft.For(participant).ApplyStatus(DRPGResolvedStatusSpec.From(asset), stacks, 0, participant);
            Check(State.TryCommitJournal(Core.Lease, draft).Succeeded, "Cannot seed actual candidate status.");
        }
        internal void Begin(BattleParticipantRuntime actor)
        {
            Core.Lease.Dispose();
            Entry = new TurnEntryIdentity(Core.BattleGeneration, 1, new ParticipantId(actor.RuntimeId));
            Check(Arbiter.TryAcquire(BattleTransactionIdentity.ForTurnEntry(Entry), out var lease, out var error), error);
            Lease = lease;
        }
        internal DRPGResolvedActionSpec Spec(BattleParticipantRuntime actor, BattleParticipantRuntime target, float power)
        {
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>(); assets.Add(skill);
            foreach (var field in new Dictionary<string, object> { { "skillId", "turn-entry-child-" + assets.Count }, { "power", power },
                { "effects", new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1, DRPGDamageTag.Slash) } } })
                typeof(DRPGSkillSO).GetField(field.Key, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, field.Value);
            return DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill), actor);
        }
        internal void Capture(params IDRPGImmediateReactionSource[] sources) =>
            Continuation = DRPGImmediateReactionLedgerBuilder.CaptureTurnEntry(Entry, State.CreateWorkspace(),
                new[] { Player, Enemy }, sources, new DRPGActionResolver());
        internal bool Commit(out DRPGBattleSettlementLedger ledger, out string error) =>
            State.TryCommitSettlement(Lease, Entry.Actor, Homes, out ledger, out error, Continuation);
        public void Dispose()
        {
            Lease?.Dispose(); Core.Dispose();
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
        }
    }
    internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
