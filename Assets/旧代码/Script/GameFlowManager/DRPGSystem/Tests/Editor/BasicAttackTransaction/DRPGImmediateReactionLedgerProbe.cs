#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGImmediateReactionLedgerProbe
{
    internal static void ReactionCategoryOrder()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("order", test.Player, 0, false, (context, requests) =>
            {
                if (context.Depth != 0) return;
                requests.Add(Request(context, "other", DRPGImmediateReactionCategory.OtherImmediate, 100, one));
                requests.Add(Request(context, "pursuit", DRPGImmediateReactionCategory.Pursuit, 90, one));
                requests.Add(Request(context, "reflect", DRPGImmediateReactionCategory.Reflected, 80, one));
                requests.Add(Request(context, "counter", DRPGImmediateReactionCategory.Counterattack, 70, one));
                requests.Add(Request(context, "defense", DRPGImmediateReactionCategory.DefenseOrEvade, 0, one,
                    outcome: DRPGHitOutcome.Evaded));
            });
            var result = test.Build(source);
            Check(result.Succeeded, result.Error);
            Check(Names(result) == "defense,counter,reflect,pursuit,other",
                "Reaction categories did not precede authored priority in the resolved ledger.");
            Check(Math.Abs(test.Enemy.CurrentHp - 120f) < 0.0001f &&
                test.State.BattleRevision == 0L,
                "Building a reaction ledger mutated live HP or revision.");
            Check(result.Entries[0].Batch.Hits[0].Outcome == DRPGHitOutcome.Evaded &&
                Math.Abs(result.Entries[0].Batch.Hits[0].HpBefore - 120f) < 0.0001f &&
                Math.Abs(result.Entries[0].Batch.Hits[0].HpAfter - 120f) < 0.0001f &&
                result.Entries[0].Batch.Effects.Count == 0,
                "An evade after primary damage read stale pre-action HP or added damage effects. " +
                "Outcome=" + result.Entries[0].Batch.Hits[0].Outcome +
                "; Hp=" + result.Entries[0].Batch.Hits[0].HpBefore + "->" +
                result.Entries[0].Batch.Hits[0].HpAfter +
                "; Effects=" + result.Entries[0].Batch.Effects.Count);
            Check(Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 56f) < 0.0001f,
                "Reaction outcomes were not resolved sequentially in one detached workspace.");
            Check(Math.Abs(test.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(test.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 60f) < 0.0001f,
                "The closure builder modified its caller's primary workspace.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in result.Entries)
                Check(entry.Identity == test.Identity && entry.ParentEventId == "primary-event" &&
                    entry.Depth == 1 && entry.RequiresPresentation && ids.Add(entry.EventId),
                    "A resolved reaction lost full root identity, causal parent, or unique event ID.");
            Check(test.State.TryCommitJournal(test.Lease, result.Workspace).Succeeded &&
                Math.Abs(test.Enemy.CurrentHp - 120f) < 0.0001f &&
                Math.Abs(test.Enemy.CurrentPosture - 56f) < 0.0001f && test.State.BattleRevision == 1L,
                "The primary and reactions did not apply through one real State journal commit.");
            Check(!test.State.TryCommitJournal(test.Lease, result.Workspace).Succeeded &&
                test.State.BattleRevision == 1L, "The closed reaction workspace was applied twice.");
        }
        StableTieOrder();
    }

    internal static void StableTieOrder()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            Source Create(string id, BattleParticipantRuntime participant, int slot, string name, int priority) =>
                new Source(id, participant, slot, false, (context, requests) =>
                {
                    if (context.Depth == 0)
                        requests.Add(Request(context, name, DRPGImmediateReactionCategory.OtherImmediate, priority, one));
                });
            // Intentionally reverse source, slot and priority order. Source IDs must
            // not take precedence over the approved combat ordering keys.
            var sources = new[] {
                Create("a", test.Enemy, 1, "later-slot", 0),
                Create("z", test.Player, 0, "earlier-slot", 0),
                Create("middle", test.Enemy, 2, "higher-priority", 1)
            };
            var result = test.Build(sources);
            Check(result.Succeeded && Names(result) == "higher-priority,earlier-slot,later-slot",
                "Same-category reactions did not use priority then source Home Slot.");
            Array.Reverse(sources);
            var reversed = test.Build(sources);
            Check(reversed.Succeeded && Names(reversed) == "higher-priority,earlier-slot,later-slot",
                "Changing source registration order changed the resolved reaction order.");
            var ties = new Source("ties", test.Player, 0, false, (context, requests) =>
            {
                if (context.Depth != 0) return;
                requests.Add(Request(context, "z", DRPGImmediateReactionCategory.OtherImmediate, 0, one));
                requests.Add(Request(context, "a", DRPGImmediateReactionCategory.OtherImmediate, 0, one));
            });
            Check(Names(test.Build(ties)) == "a,z", "Definition identity was not used as a stable tie-breaker.");
        }
    }

    internal static void ReactionBudgetAndEligibility()
    {
        CountBudget();
        DepthBudget();
        TypeMatrix();
        PassiveOncePerRoot();
        DeathCancelsOrdinaryButAllowsOnDefeat();
        OnDefeatRequiresLethalSourceEvent();
        DuplicateParentDoesNotApplyTwice();
        FaultDiscardsClosure();
        ForeignIdentityIsRejected();
        WrongParentIsRejected();
    }

    internal static void CountBudget()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("count", test.Player, 0, false, (context, requests) =>
            {
                if (context.Depth != 0) return;
                for (int i = 0; i < 33; i++)
                    requests.Add(Request(context, i.ToString("D2"), DRPGImmediateReactionCategory.OtherImmediate, 0, one));
            });
            var result = test.Build(source);
            Check(result.Succeeded && result.Entries.Count == 33 && PresentableCount(result) == 32,
                "The reaction count boundary must accept step 32 and diagnose step 33.");
            Check(result.Entries[32].State == DRPGImmediateReactionState.SkippedBudget &&
                !result.Entries[32].RequiresPresentation && result.Entries[32].Batch == null &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 28f) < 0.0001f,
                "The over-budget reaction mutated HP or requested a presentation receipt. " +
                "HP=" + result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints +
                "; Posture=" + result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture +
                "; Present=" + result.Entries[32].RequiresPresentation);
        }
    }

    internal static void DepthBudget()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("depth", test.Player, 0, false, (context, requests) =>
                requests.Add(Request(context, "chain", DRPGImmediateReactionCategory.OtherImmediate, 0, one)));
            var result = test.Build(source);
            Check(result.Succeeded && result.Entries.Count == 9 && PresentableCount(result) == 8 &&
                result.Entries[7].Depth == 8 && result.Entries[8].Depth == 9 &&
                result.Entries[8].State == DRPGImmediateReactionState.SkippedBudget &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 52f) < 0.0001f,
                "Reaction depth eight/nine was not bounded without a ninth mutation.");
            for (int i = 1; i < result.Entries.Count; i++)
                Check(result.Entries[i].ParentEventId == result.Entries[i - 1].EventId,
                    "Nested reaction parent identity was reconstructed from the root.");
        }
    }

    internal static void TypeMatrix()
    {
        foreach (var category in new[] { DRPGImmediateReactionCategory.Counterattack, DRPGImmediateReactionCategory.Reflected })
        {
            using (var test = new Fixture())
            {
                var one = test.Spec(test.Player, test.Enemy, 1f);
                var source = new Source("matrix", test.Player, 0, false, (context, requests) =>
                    requests.Add(Request(context, "recursive", category, 0, one)));
                var result = test.Build(source);
                Check(result.Succeeded && result.Entries.Count == 2 && PresentableCount(result) == 1 &&
                    result.Entries[1].State == DRPGImmediateReactionState.SkippedTypeMatrix &&
                    Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                    Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 59f) < 0.0001f,
                    "A counter generated another counter, or reflected damage reflected itself.");
            }
        }
    }

    internal static void PassiveOncePerRoot()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("passive", test.Player, 0, true, (context, requests) =>
                requests.Add(Request(context, "pursue", DRPGImmediateReactionCategory.Pursuit, 0, one)));
            var result = test.Build(source);
            Check(result.Succeeded && result.Entries.Count == 2 && PresentableCount(result) == 1 &&
                result.Entries[1].State == DRPGImmediateReactionState.SkippedDuplicate,
                "One passive source triggered again under a different parent in the same root action.");
        }
    }

    internal static void DeathCancelsOrdinaryButAllowsOnDefeat()
    {
        using (var test = new Fixture())
        {
            var lethal = test.Spec(test.Enemy, test.Player, 200f);
            var seven = test.Spec(test.Player, test.Enemy, 7f);
            var counter = new Source("counter", test.Enemy, 0, true, (context, requests) =>
            {
                if (context.Depth == 0)
                    requests.Add(Request(context, "lethal-counter", DRPGImmediateReactionCategory.Counterattack, 0, lethal));
            });
            var pursuit = new Source("pursuit", test.Player, 0, true, (context, requests) =>
            {
                if (context.Depth == 0)
                    requests.Add(Request(context, "cancelled-pursuit", DRPGImmediateReactionCategory.Pursuit, 0, seven));
            });
            var death = new Source("death", test.Player, 0, true, (context, requests) =>
            {
                if (context.Hit.WasDefeated && context.Hit.TargetRuntimeId == test.Player.RuntimeId)
                    requests.Add(Request(context, "on-defeat", DRPGImmediateReactionCategory.OtherImmediate, 0, seven, onDefeat: true));
            });
            var result = test.Build(pursuit, death, counter);
            Check(result.Succeeded && Names(result) == "lethal-counter,on-defeat,cancelled-pursuit" &&
                result.Entries[2].State == DRPGImmediateReactionState.SkippedIneligible &&
                !result.Entries[2].RequiresPresentation && PresentableCount(result) == 2,
                "Rule defeat did not cancel pending ordinary pursuit while retaining explicit OnDefeat.");
            Check(result.Entries[1].ParentEventId == result.Entries[0].EventId &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Player.RuntimeId)).HitPoints) < 0.0001f &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 53f) < 0.0001f &&
                Math.Abs(test.Player.CurrentHp - 100f) < 0.0001f && Math.Abs(test.Enemy.CurrentHp - 120f) < 0.0001f,
                "Death reaction causality or detached sequential HP was incorrect.");
        }
    }

    internal static void FaultDiscardsClosure()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("fault", test.Player, 0, false, (context, requests) =>
            {
                if (context.Depth > 0) throw new InvalidOperationException("injected reaction rule fault");
                requests.Add(Request(context, "before-fault", DRPGImmediateReactionCategory.OtherImmediate, 0, one));
            });
            var result = test.Build(source);
            Check(!result.Succeeded && result.Workspace == null && result.Entries.Count == 0 &&
                result.Error.Contains("injected reaction rule fault") && test.State.BattleRevision == 0L &&
                Math.Abs(test.Enemy.CurrentHp - 120f) < 0.0001f &&
                Math.Abs(test.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(test.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 60f) < 0.0001f,
                "A fault after an earlier reaction exposed a partial closure or mutated caller/live state.");
        }
    }

    internal static void OnDefeatRequiresLethalSourceEvent()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("premature-death", test.Player, 0, true, (context, requests) =>
            {
                if (context.Depth == 0)
                    requests.Add(Request(context, "on-defeat", DRPGImmediateReactionCategory.OtherImmediate,
                        0, one, onDefeat: true));
            });
            var result = test.Build(source);
            Check(result.Succeeded && result.Entries.Count == 1 &&
                result.Entries[0].State == DRPGImmediateReactionState.SkippedIneligible &&
                !result.Entries[0].RequiresPresentation &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 60f) < 0.0001f,
                "An OnDefeat reaction fired without the triggering event defeating its owner.");
        }
    }

    internal static void DuplicateParentDoesNotApplyTwice()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("duplicate", test.Player, 0, false, (context, requests) =>
            {
                if (context.Depth != 0) return;
                var request = Request(context, "same-definition", DRPGImmediateReactionCategory.OtherImmediate, 0, one);
                requests.Add(request);
                requests.Add(request);
            });
            var result = test.Build(source);
            Check(result.Succeeded && result.Entries.Count == 2 && PresentableCount(result) == 1 &&
                result.Entries[1].State == DRPGImmediateReactionState.SkippedDuplicate &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).HitPoints - 120f) < 0.0001f &&
                Math.Abs(result.Workspace.For(new ParticipantId(test.Enemy.RuntimeId)).Posture - 59f) < 0.0001f,
                "A duplicate source/definition/parent produced another HP mutation or receipt.");
        }
    }

    internal static void WrongParentIsRejected()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("orphan", test.Player, 0, true, (context, requests) =>
                requests.Add(new DRPGImmediateReactionRequest(context.Identity, "unrelated-parent",
                    "orphan", DRPGImmediateReactionCategory.OtherImmediate, 0, one,
                    DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f))));
            var result = test.Build(source);
            Check(!result.Succeeded && result.Workspace == null && result.Entries.Count == 0 &&
                test.State.BattleRevision == 0L && Math.Abs(test.Enemy.CurrentHp - 120f) < 0.0001f,
                "An orphan reaction entered a valid root action under the wrong parent event.");
        }
    }

    internal static void ForeignIdentityIsRejected()
    {
        using (var test = new Fixture())
        {
            var one = test.Spec(test.Player, test.Enemy, 1f);
            var source = new Source("foreign", test.Player, 0, true, (context, requests) =>
                requests.Add(new DRPGImmediateReactionRequest(
                    new ActionIdentity(test.Identity.BattleGeneration + 1L, 1L, 1L), context.EventId,
                    "foreign", DRPGImmediateReactionCategory.OtherImmediate, 0, one,
                    DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f))));
            var result = test.Build(source);
            Check(!result.Succeeded && result.Workspace == null && test.State.BattleRevision == 0L &&
                Math.Abs(test.Enemy.CurrentHp - 120f) < 0.0001f,
                "A reaction from another BattleGeneration entered the root closure.");
        }
    }

    private static DRPGImmediateReactionRequest Request(DRPGImmediateReactionContext context,
        string id, DRPGImmediateReactionCategory category, int priority, DRPGResolvedActionSpec spec,
        DRPGHitOutcome outcome = DRPGHitOutcome.Hit, bool onDefeat = false) =>
        new DRPGImmediateReactionRequest(context.Identity, context.EventId, id, category, priority,
            spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f), outcome, onDefeat);

    private static string Names(DRPGImmediateReactionClosureResult result)
    {
        var names = new List<string>();
        foreach (var entry in result.Entries) names.Add(entry.ReactionId);
        return string.Join(",", names);
    }

    private static int PresentableCount(DRPGImmediateReactionClosureResult result)
    {
        int count = 0;
        foreach (var entry in result.Entries) if (entry.RequiresPresentation) count++;
        return count;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Source : IDRPGImmediateReactionSource
    {
        private readonly Action<DRPGImmediateReactionContext, ICollection<DRPGImmediateReactionRequest>> collect;
        internal Source(string id, BattleParticipantRuntime owner, int slot, bool passive,
            Action<DRPGImmediateReactionContext, ICollection<DRPGImmediateReactionRequest>> collect)
        {
            SourceId = id; Participant = new ParticipantId(owner.RuntimeId);
            HomeSlot = slot; IsPassive = passive; this.collect = collect;
        }
        public string SourceId { get; }
        public ParticipantId Participant { get; }
        public int HomeSlot { get; }
        public bool IsPassive { get; }
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests) => collect(context, requests);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DRPGBattleStateMutationFixture fixture = DRPGBattleStateMutationFixture.Create();
        private readonly List<DRPGSkillSO> skills = new List<DRPGSkillSO>();
        internal Fixture()
        {
            Identity = new ActionIdentity(fixture.BattleGeneration, 1L, 1L);
            fixture.Lease.Dispose();
            Check(fixture.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(Identity),
                out var lease, out string error), error);
            Lease = lease;
            Workspace = fixture.CreateWorkspace();
            Primary = new DRPGActionResolver().ResolveIntoWorkspace(Spec(Player, Enemy, 20f), Player, Enemy,
                new[] { Player, Enemy }, Workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            Check(Primary.Status == DRPGWorkspaceResolutionStatus.Succeeded, Primary.Error);
        }
        internal BattleParticipantRuntime Player => fixture.Player;
        internal BattleParticipantRuntime Enemy => fixture.Enemy;
        internal DRPGBattleSessionState State => fixture.State;
        internal ActionIdentity Identity { get; }
        internal BattleRuleMutationLease Lease { get; }
        internal DRPGBattleMutationWorkspace Workspace { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; }
        internal DRPGResolvedActionSpec Spec(BattleParticipantRuntime actor, BattleParticipantRuntime target, float power)
        {
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>(); skills.Add(skill);
            Set(skill, "skillId", "reaction_" + skills.Count);
            Set(skill, "power", power);
            Set(skill, "effects", new List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash) });
            var command = DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill);
            return DRPGActionSpecResolver.Resolve(command, actor);
        }
        internal DRPGImmediateReactionClosureResult Build(params IDRPGImmediateReactionSource[] sources) =>
            new DRPGImmediateReactionLedgerBuilder().Build(Identity, "primary-event", Primary,
                Workspace, new[] { Player, Enemy }, sources, new DRPGActionResolver());
        public void Dispose()
        {
            Lease.Dispose(); fixture.Dispose();
            foreach (var skill in skills) UnityEngine.Object.DestroyImmediate(skill);
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif
