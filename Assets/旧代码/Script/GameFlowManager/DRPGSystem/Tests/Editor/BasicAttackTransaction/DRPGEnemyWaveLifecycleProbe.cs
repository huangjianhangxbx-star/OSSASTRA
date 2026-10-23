#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

internal static class DRPGEnemyWaveLifecycleProbe
{
    internal static void CompositionOwnsWholeWaveDeploymentAndSealsOnce()
    {
        using (var f = new Fixture())
        {
            var wave = f.Core.WaveLifecycle;
            Check(wave != null && ReferenceEquals(wave, f.Core.WaveLifecycle), "Composition did not create its single Wave lifecycle.");
            Check(f.Enemies.All(e => !e.IsDeployed) && f.Core.Scheduler.Count == 0, "Enemies appeared before the first wave.");
            using (var lease = f.Lease(1))
            {
                Check(wave.TryStage(lease, 2, 12, out var batch, out var error), error);
                Check(!wave.TryStage(lease, 2, 12, out _, out _), "Repeated staging replaced the pending batch.");
                batch.ReadyReceipts[0].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "ready"));
                Check(!wave.TryCommitReady(out _, out _) && f.Core.Scheduler.Count == 0 && f.Core.ContinuationInbox.PendingCount == 0,
                    "Partial readiness crossed the lifecycle barrier.");
                Ready(batch);
                Check(wave.TryCommitReady(out var completion, out error), error);
                Check(completion.ActivatedMembers.Count == 2 && completion.ActivatedMembers[0].AuthoredOrdinal == 0 &&
                    completion.ActivatedMembers[1].AuthoredOrdinal == 1 && wave.CompletedWaveCount == 1 &&
                    f.Core.Scheduler.Count == 2 && f.Enemies[0].IsDeployed && f.Enemies[1].IsDeployed && !f.Enemies[2].IsDeployed &&
                    f.Core.ContinuationInbox.PendingCount == 1 && f.Core.Arbiter.Owns(lease, completion.Owner),
                    "Lifecycle did not commit the planned batch with one sealed completion and retained ownership.");
                Check(wave.TryCommitReady(out var duplicate, out _) && duplicate.HasSameValues(completion) &&
                    f.Core.ContinuationInbox.PendingCount == 1 && f.Core.Scheduler.Count == 2, "Completion was published twice.");
            }
        }
    }
    internal static void EmptyRuleRosterCannotBypassTheExactDepartureBarrier()
    {
        using (var f = new Fixture())
        {
            var wave = f.Core.WaveLifecycle;
            using (var first = f.Lease(1))
            {
                Check(wave.TryStage(first, 2, 12, out var batch, out var error), error);
                Ready(batch); Check(wave.TryCommitReady(out _, out error), error);
                Check(f.Core.ContinuationInbox.TryTake(out _), "First completion missing.");
            }
            var action = new ActionIdentity(f.Core.BattleGeneration, 1, 1);
            Check(f.Core.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(action), out var root, out _), "Root acquire failed.");
            DRPGBattleContinuationInbox.Entry firstDeparture;
            using (root)
            {
                var draft = f.Core.State.CreateWorkspace();
                foreach (var enemy in f.Enemies.Take(2))
                {
                    draft.For(new ParticipantId(enemy.RuntimeId)).SetHitPoints(0).SetDeployed(false);
                    draft.RemoveFromTimeline(new ParticipantId(enemy.RuntimeId));
                }
                Check(f.Core.State.TryCommitJournal(root, draft).Succeeded, "Cannot create rule-cleared fixture.");
                firstDeparture = Departure(f, action, root, 0);
            }
            using (var second = f.Lease(2))
            {
                Check(!wave.TryStage(second, 2, 13, out _, out _), "Rule-cleared roster bypassed missing typed departures.");
                Check(wave.TryObserveDeparture(firstDeparture, out var error), error);
                Check(!wave.TryStage(second, 2, 13, out _, out _), "One departure cleared the entire wave.");
            }
            var nextAction = new ActionIdentity(f.Core.BattleGeneration, 2, 2);
            Check(f.Core.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(nextAction), out var last, out _), "Second Root acquire failed.");
            using (last) Check(wave.TryObserveDeparture(Departure(f, nextAction, last, 1), out var error), error);
            using (var second = f.Lease(2))
            {
                Check(wave.TryStage(second, 2, 13, out var batch, out var error), error);
                Ready(batch); Check(wave.TryCommitReady(out var completed, out error), error);
                Check(completed.ActivatedMembers.Count == 1 && completed.ActivatedMembers[0].AuthoredOrdinal == 2 &&
                    wave.CompletedWaveCount == 2 && f.Core.Scheduler.Count == 1 && f.Enemies[2].IsDeployed && !f.Enemies[0].IsDeployed && !f.Enemies[1].IsDeployed,
                    "Second wave did not use the remaining planned member exactly once.");
            }
            using (var exhausted = f.Lease(3))
                Check(!wave.TryStage(exhausted, 2, 14, out _, out _), "Exhausted plan spawned an extra wave.");
        }
    }
    internal static void CancelledStagingCannotPublishLateReadiness()
    {
        using (var f = new Fixture())
        using (var lease = f.Lease(1))
        {
            var wave = f.Core.WaveLifecycle;
            Check(wave.TryStage(lease, 2, 12, out var batch, out var error), error);
            wave.CancelPending(); Ready(batch);
            Check(!wave.TryCommitReady(out _, out _) && !wave.TryStage(lease, 2, 12, out _, out _) &&
                f.Core.Scheduler.Count == 0 && f.Enemies.All(e => !e.IsDeployed) && f.Core.ContinuationInbox.PendingCount == 0,
                "Cancelled staging accepted late callbacks or restarted.");
        }
    }
    internal static void PartyDefeatPreventsWaveStaging()
    {
        using (var f = new Fixture())
        using (var lease = f.Lease(1))
        {
            var draft = f.Core.State.CreateWorkspace(); draft.For(new ParticipantId(9001)).SetHitPoints(0);
            Check(f.Core.State.TryCommitJournal(lease, draft).Succeeded, "Cannot seed party defeat.");
            Check(!f.Core.WaveLifecycle.TryStage(lease, 2, 12, out _, out _) && f.Core.Scheduler.Count == 0,
                "A defeated party triggered enemy reinforcements.");
        }
    }
    private static DRPGBattleContinuationInbox.Entry Departure(Fixture f, ActionIdentity identity, BattleRuleMutationLease lease, int enemyIndex)
    {
        var owner = BattleTransactionIdentity.ForRootAction(identity);
        var journal = new DRPGBattleDepartureJournal(owner, (ulong)identity.ActionId,
            new[]{new DRPGBattleDepartureMember(new ParticipantId(f.Enemies[enemyIndex].RuntimeId), DRPGBattleSide.Enemy)});
        var value = new RootActionCompletion(identity, new RootActionTerminal(RootActionTerminalKind.Completed, "complete"),
            DRPGBattleOutcomeCandidate.WavePending, f.Core.State.BattleRevision, journal, lease);
        Check(f.Core.ContinuationInbox.Submit(value).IsAccepted, "Root completion not sealed.");
        Check(f.Core.ContinuationInbox.TryTake(out var entry), "Root completion not queued."); return entry;
    }
    private static void Ready(DRPGEnemyWaveDeploymentBatch batch)
    {
        foreach (var receipt in batch.ReadyReceipts)
            receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "ready"));
    }
    private sealed class Fixture : IDisposable
    {
        private readonly DRPGCombatantProfileSO profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        internal readonly DRPGBattleSessionComposition Core;
        internal readonly BattleParticipantRuntime[] Enemies;
        internal Fixture()
        {
            var specs = Enumerable.Range(0, 3).Select(i => new BattleParticipantSpec(profile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy, i, initiallyDeployed:false)).ToArray();
            Enemies = specs.Select((s, i) => new BattleParticipantRuntime(9101 + i, s)).ToArray();
            var player = new BattleParticipantRuntime(9001, new BattleParticipantSpec(profile, DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            var plan = DRPGEnemyWavePlan.Freeze(42, "lifecycle-test", specs, 2, 2, 3);
            Core = DRPGBattleSessionComposition.Build(new[]{player}, Enemies, new DRPGTurnScheduler(),
                DRPGProcessBattleGenerationSource.Instance, DRPGBattleTransactionPolicy.StandardBasicAttackTest, null, wavePlan:plan);
        }
        internal BattleRuleMutationLease Lease(int wave)
        {
            Check(Core.Arbiter.TryAcquire(BattleTransactionIdentity.ForWave(new WaveTransitionIdentity(Core.BattleGeneration, wave, wave)),
                out var lease, out var error), error); return lease;
        }
        public void Dispose() { UnityEngine.Object.DestroyImmediate(profile); }
    }
    private static void Check(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
}
#endif
