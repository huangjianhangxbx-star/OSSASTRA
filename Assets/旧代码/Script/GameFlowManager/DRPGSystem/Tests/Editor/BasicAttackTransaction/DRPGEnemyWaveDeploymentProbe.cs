#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

internal static class DRPGEnemyWaveDeploymentProbe
{
    internal static void PartialReadinessCannotRegisterAnyEnemy()
        => CheckBatch(0);
    internal static void CancelledBatchRejectsLateReadyReceipts() => CheckBatch(1);
    internal static void LostPhysicalWaveLeaseCannotCommit() => CheckBatch(2);
    internal static void RevisionDriftRejectsTheEntireBatch() => CheckBatch(3);
    internal static void AFailedMemberCannotPartiallyDeployItsPeers() => CheckBatch(4);
    private static void CheckBatch(int mode)
    {
        var profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        BattleRuleMutationLease lease = null;
        try
        {
            var enemies = Enumerable.Range(0,3).Select(i => new BattleParticipantRuntime(9001+i,
                new BattleParticipantSpec(profile,DRPGBattleSide.Enemy,DRPGBattleRosterRole.Enemy,i, initiallyDeployed:false))).ToArray();
            var arbiter = new DRPGBattleTransactionArbiter(900,new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
            var scheduler = new DRPGTurnScheduler();
            var state = new DRPGBattleSessionState(900,enemies,scheduler,arbiter);
            var owner = BattleTransactionIdentity.ForWave(new WaveTransitionIdentity(900,1,1));
            Check(arbiter.TryAcquire(owner,out lease,out _), "Wave lease failed.");
            var ids = enemies.Select(e=>new ParticipantId(e.RuntimeId)).ToArray();
            var batch = new DRPGEnemyWaveDeploymentBatch(state,lease,ids,2,12f);
            var receipts = batch.ReadyReceipts;
            receipts[0].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed,"ready"));
            Check(!batch.TryCommit(out _) && enemies.All(e=>!e.IsDeployed) && scheduler.GetTimelineSnapshot().Count==0 && state.BattleRevision==0,
                "One ready member caused partial batch registration.");
            if (mode==1) batch.Cancel();
            if (mode==2) lease.Dispose();
            if (mode==3)
            {
                var draft=state.CreateWorkspace(); draft.For(ids[0]).SetHitPoints(1);
                Check(state.TryCommitJournal(lease,draft).Succeeded,"Revision drift fixture failed.");
            }
            if (mode==4) receipts[1].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted,"missing actor"));
            foreach(var receipt in receipts.Skip(1)) receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed,"ready"));
            if (mode!=0)
            {
                long before=state.BattleRevision;
                for(int i=0;i<3;i++) Check(!batch.TryCommit(out _) && state.BattleRevision==before &&
                    enemies.All(e=>!e.IsDeployed) && scheduler.GetTimelineSnapshot().Count==0,"Rejected batch partially registered or accepted a late receipt.");
                return;
            }
            Check(batch.TryCommit(out var error), "Complete batch rejected: "+error);
            Check(enemies.All(e=>e.IsDeployed) && scheduler.GetTimelineSnapshot().Count==3 && state.BattleRevision==1 &&
                scheduler.GetTimelineSnapshot().All(e=>e.ScheduledTime==12f), "Wave did not register atomically at the shared deployment time.");
            Check(batch.TryCommit(out _) && state.BattleRevision==1 && scheduler.GetTimelineSnapshot().Count==3,
                "Duplicate ready/commit registered the wave twice.");
        }
        finally { lease?.Dispose(); UnityEngine.Object.DestroyImmediate(profile); }
    }
    private static void Check(bool value,string error){if(!value)throw new InvalidOperationException(error);}
}
#endif
