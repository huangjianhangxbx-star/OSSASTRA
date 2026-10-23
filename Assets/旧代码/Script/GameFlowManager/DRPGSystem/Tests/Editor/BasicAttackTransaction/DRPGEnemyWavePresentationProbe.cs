#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

internal static class DRPGEnemyWavePresentationProbe
{
    internal static void AnnouncementThenWholeReadinessThenCleanupThenCommit()
    {
        using (var f = new Fixture())
        {
            f.Run.Tick();
            Check(f.Port.Announcement != null && f.Port.Staging == null && f.Core.Scheduler.Count == 0,
                "Wave did not issue announcement before staging.");
            Complete(f.Port.Announcement);
            f.Step(.74); Check(f.Port.Staging == null, "Announcement was not readable for the full duration.");
            f.Step(.01); Check(f.Port.Staging != null && f.Core.Scheduler.Count == 0, "Staging did not follow the announcement barrier.");
            Complete(f.Port.Staging); f.Step(0);
            Check(f.Port.Ready != null && f.Port.Command.StaggerMaximum <= .5, "Wave readiness/stagger contract was lost.");
            Complete(f.Port.Ready); Complete(f.Port.Command.MemberReady[0]); f.Step(0);
            Check(f.Port.Close == null && f.Core.Scheduler.Count == 0, "Aggregate ready bypassed an unready member.");
            Complete(f.Port.Command.MemberReady[1]); f.Step(0);
            Check(f.Port.Close != null && !f.Port.Retract && f.Core.Scheduler.Count == 0 && f.Core.ContinuationInbox.PendingCount == 0,
                "Whole-ready wave committed before normal cleanup acknowledgement.");
            f.Port.Close.TryComplete(true); f.Step(0);
            Check(f.Run.CompletionSubmitted && f.Core.Scheduler.Count == 2 && f.Core.ContinuationInbox.PendingCount == 1 && f.Lease.IsActive,
                "Clean wave failed atomic deployment/one continuation submission or released its owner's lease.");
            f.Step(1); Check(f.Core.ContinuationInbox.PendingCount == 1, "Duplicate Tick resubmitted the wave.");
        }
    }
    internal static void ReadyTimeoutRetractsStagingWithoutDeployment()
    {
        using (var f = new Fixture())
        {
            f.Run.Tick(); Complete(f.Port.Announcement); f.Step(.4); f.Step(.4);
            Complete(f.Port.Staging); f.Step(0); Complete(f.Port.Ready);
            f.Step(6);
            Check(f.Run.Fault.Length != 0 && f.Port.Retract && f.Port.Close != null && !f.Run.FaultCleanupCompleted &&
                f.Core.Scheduler.Count == 0 && f.Lease.IsActive, "Missing member readiness did not fail closed and retract staging.");
            foreach (var r in f.Port.Command.MemberReady) Complete(r);
            f.Port.Close.TryComplete(true); f.Step(0);
            Check(f.Run.FaultCleanupCompleted && !f.Run.CompletionSubmitted && f.Core.Scheduler.Count == 0 &&
                f.Core.ContinuationInbox.PendingCount == 0 && f.Lease.IsActive, "Late ready callback escaped fault cleanup ownership.");
        }
    }
    internal static void AnnouncementTimeoutRetractsWithoutStaging()
    {
        using (var f = new Fixture())
        {
            f.Run.Tick();
            Check(f.Port.Announcement != null && f.Port.Staging == null,
                "Wave announcement was not isolated before staging.");
            f.Step(6);
            Check(f.Run.Fault.Length != 0 && f.Port.Staging == null &&
                f.Port.Retract && f.Port.Close != null &&
                !f.Run.FaultCleanupCompleted && f.Core.Scheduler.Count == 0 &&
                f.Core.ContinuationInbox.PendingCount == 0 && f.Lease.IsActive,
                "Announcement timeout staged or deployed actors instead of entering owned retract cleanup.");
            Complete(f.Port.Announcement);
            f.Port.Close.TryComplete(true); f.Step(0);
            Check(f.Run.FaultCleanupCompleted && !f.Run.CompletionSubmitted &&
                f.Port.Staging == null && f.Core.Scheduler.Count == 0 &&
                f.Core.ContinuationInbox.PendingCount == 0 && f.Lease.IsActive,
                "Late announcement callback revived a timed-out wave or released its outer lease.");
        }
    }
    internal static void CompletionArrivingAfterDeadlineCannotBeatRecovery()
    {
        using (var f = new Fixture())
        {
            f.Run.Tick(); Complete(f.Port.Announcement); f.Step(.4); f.Step(.4);
            Complete(f.Port.Staging); f.Step(0);
            f.Port.OnTick = () => { Complete(f.Port.Ready); foreach (var receipt in f.Port.Command.MemberReady) Complete(receipt); };
            f.Step(6);
            Check(f.Run.Fault.Length != 0 && f.Port.Retract && f.Core.Scheduler.Count == 0,
                "Late callback beat the expired readiness watchdog.");
        }
    }
    internal static void AnnouncementRequiresRenderedFramesNotOnlyElapsedTime()
    {
        using (var f = new Fixture())
        {
            f.Run.Tick(); Complete(f.Port.Announcement);
            f.Clock.Now = .8; f.Run.Tick();
            Check(f.Port.Staging == null, "Unrendered announcement elapsed directly into staging.");
            f.Step(0); Check(f.Port.Staging == null, "One frame was treated as a readable announcement.");
            f.Step(0); Check(f.Port.Staging != null, "Rendered announcement never advanced.");
        }
    }
    internal static void CancellationInsideNormalCloseRetractsBeforeAnyCommit()
    {
        using (var f = new Fixture())
        {
            f.Run.Tick(); Complete(f.Port.Announcement); f.Step(.4); f.Step(.4);
            Complete(f.Port.Staging); f.Step(0); Complete(f.Port.Ready);
            foreach (var r in f.Port.Command.MemberReady) Complete(r);
            f.Port.OnNormalClose = () => f.Run.Cancel("Scene disabled while closing");
            f.Step(0);
            Check(f.Port.Retract && f.Core.Scheduler.Count == 0 && f.Core.ContinuationInbox.PendingCount == 0,
                "Synchronous close cancellation committed the staged enemies.");
            f.Port.Close.TryComplete(true); f.Step(0);
            Check(f.Run.FaultCleanupCompleted && f.Lease.IsActive, "Fault cleanup released the outer owner's lease.");
        }
    }
    internal static void LateNormalCleanupCannotDeployAfterItsDeadline()
    {
        using (var f = new Fixture())
        {
            f.Run.Tick(); Complete(f.Port.Announcement); f.Step(.4); f.Step(.4);
            Complete(f.Port.Staging); f.Step(0); Complete(f.Port.Ready);
            foreach (var r in f.Port.Command.MemberReady) Complete(r);
            f.Step(0);
            var expired = f.Port.Close;
            f.Port.OnTick = () => expired.TryComplete(true);
            f.Step(6);
            Check(f.Run.Fault.Length != 0 && !f.Run.CompletionSubmitted && f.Port.Retract && f.Core.Scheduler.Count == 0,
                "Late normal cleanup acknowledgement committed after its deadline.");
        }
    }
    private static void Complete(PresentationReceipt r)
    { Check(r != null, "Expected receipt was not issued."); r.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "ready")); }
    private sealed class Clock : IDRPGBattlePresentationClock
    { public double Now { get; set; } public long RenderedFrame { get; set; } }
    private sealed class Port : IWaveTransitionPresentationPort
    {
        internal PresentationReceipt Announcement, Staging, Ready;
        internal PresentationCleanupReceipt Close;
        internal bool Retract;
        internal WaveBatchStagingCommand Command;
        internal Action OnTick, OnNormalClose;
        public PresentationReceipt AnnounceWave(in WaveAnnouncementCommand c) => Announcement = Receipt(c.Identity, DRPGBattleActionStage.Announcement);
        public WaveStagingReceipt StageBatch(in WaveBatchStagingCommand c)
        { Command = c; return new WaveStagingReceipt(Staging = Receipt(c.Identity, DRPGBattleActionStage.ConflictComposition)); }
        public PresentationReceipt AwaitBatchReady(in WaveBatchReadyCommand c) => Ready = Receipt(c.Identity, DRPGBattleActionStage.TurnBegin);
        public void Tick() { OnTick?.Invoke(); }
        public PresentationCleanupReceipt CloseWave(WaveTransitionIdentity identity, bool retractActors)
        { Retract = retractActors; if (!retractActors) OnNormalClose?.Invoke(); return Close = new PresentationCleanupReceipt(); }
        private static PresentationReceipt Receipt(WaveTransitionIdentity id, DRPGBattleActionStage stage)
            => new PresentationReceipt(BattleTransactionIdentity.ForWave(id), stage, 0);
    }
    private sealed class Fixture : IDisposable
    {
        private readonly DRPGCombatantProfileSO profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        internal readonly DRPGBattleSessionComposition Core;
        internal readonly BattleRuleMutationLease Lease;
        internal readonly Clock Clock = new Clock();
        internal readonly Port Port = new Port();
        internal readonly DRPGEnemyWavePresentationRun Run;
        internal Fixture()
        {
            var specs = Enumerable.Range(0, 2).Select(i => new BattleParticipantSpec(profile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, i, initiallyDeployed:false)).ToArray();
            var enemies = specs.Select((s, i) => new BattleParticipantRuntime(9201 + i, s)).ToArray();
            var player = new BattleParticipantRuntime(9001, new BattleParticipantSpec(profile, DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            var plan = DRPGEnemyWavePlan.Freeze(42, "presentation-wave", specs, 2, 2, 3);
            Core = DRPGBattleSessionComposition.Build(new[]{player}, enemies, new DRPGTurnScheduler(), DRPGProcessBattleGenerationSource.Instance,
                DRPGBattleTransactionPolicy.StandardBasicAttackTest, null, wavePlan:plan);
            Check(Core.Arbiter.TryAcquire(BattleTransactionIdentity.ForWave(new WaveTransitionIdentity(Core.BattleGeneration, 1, 1)), out Lease, out _), "Acquire failed.");
            Run = new DRPGEnemyWavePresentationRun(Core.WaveLifecycle, Lease, Port, Clock,
                new DRPGEnemyWavePolicySnapshot(2, 2, .75, .3, 3), 2, 12, 5);
        }
        internal void Step(double seconds) { Clock.Now += seconds; Clock.RenderedFrame++; Run.Tick(); }
        public void Dispose() { Run.Cancel("Fixture closed"); Lease.Dispose(); UnityEngine.Object.DestroyImmediate(profile); }
    }
    private static void Check(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
}
#endif
