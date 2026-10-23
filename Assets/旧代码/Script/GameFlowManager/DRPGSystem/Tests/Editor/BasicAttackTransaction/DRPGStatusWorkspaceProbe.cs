#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGStatusWorkspaceProbe
{
    internal static void RunAll()
    {
        StagedStatusAffectsDamage();
        RemovedStatusAffectsDamage();
        ForkedStatusStacksStayIndependent();
        DurationOverflowRejectsBeforeAnyLiveMutation();
        InvalidStatusDefinitionRejectsBeforeAnyLiveMutation();
        RefreshPoliciesAndHugeStackRequestsStayDeterministic();
        SameIdStatusDefinitionDriftRejectsCapturedJournal();
    }

    internal static void StagedStatusAffectsDamage()
    {
        using (var test = new Fixture())
        {
            var draft = test.Battle.CreateWorkspace();
            draft.For(test.Target).ApplyStatus(test.Status(2f));
            var result = test.Resolve(draft);
            Check(result.Status == DRPGWorkspaceResolutionStatus.Succeeded && result.Batch.Hits[0].HpAfter == 120f &&
                result.Batch.Hits[0].PostureAfter == 40f,
                "Staged vulnerability was ignored by detached damage.");
            Check(test.Battle.Enemy.CurrentHp == 120f && test.Battle.Enemy.Statuses.Count == 0,
                "Resolving a status draft mutated the live participant.");
        }
    }

    internal static void RemovedStatusAffectsDamage()
    {
        using (var test = new Fixture())
        {
            var seed = test.Battle.CreateWorkspace();
            seed.For(test.Target).ApplyStatus(test.Status(2f));
            Check(test.Battle.State.TryCommitJournal(test.Battle.Lease, seed).Succeeded, "Status seed failed.");
            var draft = test.Battle.CreateWorkspace();
            draft.For(test.Target).RemoveStatus("vulnerable");
            var removedResult = test.Resolve(draft);
            Check(removedResult.Batch.Hits[0].HpAfter == 120f &&
                removedResult.Batch.Hits[0].PostureAfter == 60f,
                "A removed status still modified detached damage.");
            Check(test.Battle.Enemy.CurrentHp == 120f && test.Battle.Enemy.Statuses.Count == 1,
                "Removing a draft status changed live state.");
        }
    }

    internal static void ForkedStatusStacksStayIndependent()
    {
        using (var test = new Fixture())
        {
            var spec = test.Status(1.5f);
            var draft = test.Battle.CreateWorkspace();
            draft.For(test.Target).ApplyStatus(spec);
            var stacked = draft.Fork();
            stacked.For(test.Target).ApplyStatus(spec, 2);
            var removed = stacked.Fork();
            removed.For(test.Target).RemoveStatus(spec.StatusId);
            var stackedResult = test.Resolve(stacked);
            var draftResult = test.Resolve(draft);
            var removedForkResult = test.Resolve(removed);
            Check(stackedResult.Batch.Hits[0].HpAfter == 120f &&
                stackedResult.Batch.Hits[0].PostureAfter == 30f &&
                draftResult.Batch.Hits[0].HpAfter == 120f &&
                draftResult.Batch.Hits[0].PostureAfter == 50f &&
                removedForkResult.Batch.Hits[0].HpAfter == 120f &&
                removedForkResult.Batch.Hits[0].PostureAfter == 60f,
                "Forked apply/remove/stack status drafts did not resolve independently.");
            Check(test.Battle.Enemy.Statuses.Count == 0 && test.Battle.State.BattleRevision == 0,
                "Forked status calculations changed live state.");
        }
    }

    internal static void DurationOverflowRejectsBeforeAnyLiveMutation()
    {
        using (var test = new Fixture())
        {
            var spec = test.Status(1f, DRPGStatusRefreshPolicy.AddDuration);
            var seed = test.Battle.CreateWorkspace();
            seed.For(test.Target).ApplyStatus(spec, 1, int.MaxValue);
            Check(test.Battle.State.TryCommitJournal(test.Battle.Lease, seed).Succeeded, "Long status seed failed.");
            var draft = test.Battle.CreateWorkspace();
            draft.For(test.Target).SetHitPoints(100f).ApplyStatus(spec, 1, 1);
            var result = test.Battle.State.TryCommitJournal(test.Battle.Lease, draft);
            Check(!result.Succeeded && test.Battle.Enemy.CurrentHp == 120f &&
                test.Battle.Enemy.Statuses.Snapshot()[0].RemainingTurns == int.MaxValue &&
                test.Battle.State.BattleRevision == 1,
                "Overflowing status duration committed HP or corrupted the live duration.");
        }
    }

    internal static void InvalidStatusDefinitionRejectsBeforeAnyLiveMutation()
    {
        using (var test = new Fixture())
        {
            var draft = test.Battle.CreateWorkspace();
            draft.For(test.Target).SetHitPoints(100f).ApplyStatus(test.Status(float.PositiveInfinity));
            Check(!test.Battle.State.TryCommitJournal(test.Battle.Lease, draft).Succeeded &&
                test.Battle.Enemy.CurrentHp == 120f && test.Battle.Enemy.Statuses.Count == 0 &&
                test.Battle.State.BattleRevision == 0,
                "A non-finite status definition was applied alongside live HP.");
        }
    }

    internal static void RefreshPoliciesAndHugeStackRequestsStayDeterministic()
    {
        foreach (DRPGStatusRefreshPolicy policy in Enum.GetValues(typeof(DRPGStatusRefreshPolicy)))
        using (var test = new Fixture())
        {
            var spec = test.Status(1.5f, policy);
            var seed = test.Battle.CreateWorkspace();
            seed.For(test.Target).ApplyStatus(spec, 2, 5);
            Check(test.Battle.State.TryCommitJournal(test.Battle.Lease, seed).Succeeded, "Refresh seed failed.");
            var draft = test.Battle.CreateWorkspace();
            draft.For(test.Target).ApplyStatus(test.Status(9f), int.MaxValue, 2);
            var result = test.Resolve(draft);
            Check(result.Status == DRPGWorkspaceResolutionStatus.Succeeded && result.Batch.Hits[0].HpAfter == 120f &&
                result.Batch.Hits[0].PostureAfter == 30f,
                "Huge stack request wrapped, or a same-ID incoming definition replaced the existing definition.");
            Check(test.Battle.State.TryCommitJournal(test.Battle.Lease, draft).Succeeded, "Refresh commit failed.");
            var actual = test.Battle.Enemy.Statuses.Snapshot()[0];
            int duration = policy == DRPGStatusRefreshPolicy.AddDuration ? 7 : policy == DRPGStatusRefreshPolicy.KeepLongerDuration ? 5 : 2;
            Check(actual.Stacks == 3 && actual.RemainingTurns == duration && actual.Spec.IncomingDamageMultiplier == 1.5f,
                "Prepared status replay diverged from frozen stack/refresh policy.");
        }
    }

    internal static void SameIdStatusDefinitionDriftRejectsCapturedJournal()
    {
        using (var test = new Fixture())
        {
            var seed = test.Battle.CreateWorkspace();
            seed.For(test.Target).ApplyStatus(test.Status(2f));
            Check(test.Battle.State.TryCommitJournal(test.Battle.Lease, seed).Succeeded, "Drift seed failed.");
            var draft = test.Battle.CreateWorkspace();
            Check(test.Resolve(draft).Status == DRPGWorkspaceResolutionStatus.Succeeded, "Drift control resolution failed.");
            using (test.Battle.State.OpenCompatibilityMutationScope(test.Battle.Lease))
            {
                test.Battle.Enemy.Statuses.Remove("vulnerable");
                test.Battle.Enemy.Statuses.Apply(test.Status(3f));
            }
            Check(!test.Battle.State.TryCommitJournal(test.Battle.Lease, draft).Succeeded && test.Battle.Enemy.CurrentHp == 120f &&
                test.Battle.Enemy.Statuses.Snapshot()[0].Spec.IncomingDamageMultiplier == 3f && test.Battle.State.BattleRevision == 1,
                "Same ID/stacks/duration concealed a changed live status definition.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal Fixture()
        {
            Battle = DRPGBattleStateMutationFixture.Create();
            var skill = Own<DRPGSkillSO>();
            Set(skill, "skillId", "status-workspace-basic");
            Set(skill, "power", 20f);
            Spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(
                Battle.Player.RuntimeId, Battle.Enemy.RuntimeId, skill), Battle.Player);
        }
        internal DRPGBattleStateMutationFixture Battle { get; }
        internal ParticipantId Target => new ParticipantId(Battle.Enemy.RuntimeId);
        internal DRPGResolvedActionSpec Spec { get; }
        internal DRPGResolvedStatusSpec Status(float incoming,
            DRPGStatusRefreshPolicy policy = DRPGStatusRefreshPolicy.RefreshDuration)
        {
            var definition = Own<DRPGStatusDefinitionSO>();
            Set(definition, "statusId", "vulnerable");
            Set(definition, "incomingDamageMultiplier", incoming);
            Set(definition, "maxStacks", 3);
            Set(definition, "durationTurns", 2);
            Set(definition, "refreshPolicy", policy);
            return DRPGResolvedStatusSpec.From(definition);
        }
        internal DRPGWorkspaceResolutionResult Resolve(DRPGBattleMutationWorkspace draft) =>
            new DRPGActionResolver().ResolveIntoWorkspace(Spec, Battle.Player, Battle.Enemy,
                new[] { Battle.Player, Battle.Enemy }, draft,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
        private T Own<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            owned.Add(value);
            return value;
        }
        public void Dispose()
        {
            Battle.Dispose();
            foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value);
        }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
    }
}
#endif
