#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class DRPGEnemyWaveContinuationProbe
{
    internal static void WaveCompletionIsSealedOnceWithoutInlineTurnEntry()
    {
        var arbiter=new DRPGBattleTransactionArbiter(901,new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
        var identity=new WaveTransitionIdentity(901,1,1);
        Check(arbiter.TryAcquire(BattleTransactionIdentity.ForWave(identity),out var lease,out _),"Wave lease rejected.");
        try
        {
            var inbox=new DRPGBattleContinuationInbox(arbiter);
            var members=new[]{new EnemyDeploymentId("encounter-a",0),new EnemyDeploymentId("encounter-a",1)};
            var completion=new WaveTransitionCompletion(identity,members,7,lease);
            members[0]=new EnemyDeploymentId("encounter-a",99);
            Check(inbox.Submit(completion).Status==ContinuationSubmitStatus.Accepted,"Wave completion was rejected by the shared Inbox.");
            Check(inbox.Submit(completion).Status==ContinuationSubmitStatus.Duplicate && inbox.PendingCount==1 &&
                arbiter.CurrentOwner==completion.Owner && !arbiter.IsCommandInputOpen && completion.ActivatedMembers[0].AuthoredOrdinal==0,
                "Wave completion mutated, duplicated or released input inline.");
            var conflict=new WaveTransitionCompletion(identity,new[]{new EnemyDeploymentId("encounter-a",2)},7,lease);
            Check(!inbox.Submit(conflict).IsAccepted && inbox.PendingCount==1,"Conflicting wave members replaced a sealed activation.");
        }
        finally {lease.Dispose();}
    }
    internal static void WavePumpTransfersOnceWithoutInputGap()
    {
        RunDelivery(0);
    }
    internal static void WavePreparationCannotSelectForeignBattleOrWrongKind()
    {
        RunDelivery(1); RunDelivery(2);
    }
    internal static void WavePreparationCannotReplacePhysicalLease()
    {
        RunDelivery(3);
    }
    internal static void WaveDefaultAndExpiredLeaseAreRejected()
    {
        var arbiter=new DRPGBattleTransactionArbiter(901,new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
        var inbox=new DRPGBattleContinuationInbox(arbiter);
        Check(!inbox.Submit(default(WaveTransitionCompletion)).IsAccepted,"Default wave was accepted.");
        var id=new WaveTransitionIdentity(901,1,1);
        Check(arbiter.TryAcquire(BattleTransactionIdentity.ForWave(id),out var lease,out _),"Acquire failed.");
        var value=new WaveTransitionCompletion(id,new[]{new EnemyDeploymentId("encounter-a",0)},1,lease);
        lease.Dispose();
        Check(arbiter.TryAcquire(value.Owner,out var replacement,out _),"Reacquire failed.");
        try {Check(!inbox.Submit(value).IsAccepted && inbox.PendingCount==0,"Expired physical lease was accepted.");}
        finally {replacement.Dispose();}
    }
    internal static void AtomicBatchSealsFrozenCompletionBeforeDeferredTurnEntry()
    {
        var profile=ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        BattleRuleMutationLease lease=null;
        WaveReceiver receiver=null;
        try
        {
            var enemies=Enumerable.Range(0,3).Select(i=>new BattleParticipantRuntime(9001+i,
                new BattleParticipantSpec(profile,DRPGBattleSide.Enemy,DRPGBattleRosterRole.Enemy,i,initiallyDeployed:false))).ToArray();
            var arbiter=new DRPGBattleTransactionArbiter(901,new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
            var scheduler=new DRPGTurnScheduler();
            var state=new DRPGBattleSessionState(901,enemies,scheduler,arbiter);
            Check(arbiter.TryAcquire(BattleTransactionIdentity.ForWave(new WaveTransitionIdentity(901,1,1)),out lease,out _),"Acquire failed.");
            var authored=Enumerable.Range(0,3).Select(i=>new EnemyDeploymentId("encounter-a",i)).ToArray();
            var batch=new DRPGEnemyWaveDeploymentBatch(state,lease,enemies.Select(e=>new ParticipantId(e.RuntimeId)).ToArray(),2,12,authored);
            authored[0]=new EnemyDeploymentId("encounter-a",99);
            Check(!batch.TryGetCompletion(out _),"Uncommitted wave produced completion.");
            var ready=batch.ReadyReceipts;
            ready[0].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed,"ready"));
            Check(!batch.TryCommit(out _) && !batch.TryGetCompletion(out _),"Partial batch produced completion.");
            foreach(var receipt in ready.Skip(1)) receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Degraded,"safe placeholder ready"));
            Check(batch.TryCommit(out var error),"Batch commit failed: "+error);
            Check(batch.TryGetCompletion(out var completion),"Committed batch did not seal typed completion.");
            Check(completion.FinalBattleRevision==1 && completion.ActivatedMembers.Count==3 && completion.ActivatedMembers[0].AuthoredOrdinal==0,
                "Completion reconstructed mutable caller membership or wrong revision.");
            var inbox=new DRPGBattleContinuationInbox(arbiter);
            receiver=new WaveReceiver(arbiter,lease,0);
            var router=new DRPGBattleContinuationRouter(inbox,null,waveRunner:receiver);receiver.Router=router;
            Check(inbox.Submit(completion).IsAccepted && receiver.Starts==0 && scheduler.GetTimelineSnapshot().Count==3,
                "Wave completion bypassed deferred delivery or omitted batch registration.");
            Check(router.Pump() && receiver.Starts==1 && batch.TryGetCompletion(out var same) && same.HasSameValues(completion),
                "Wave failed deferred delivery or its sealed facts changed after ownership transfer.");
        }
        finally {receiver?.HeldLease?.Dispose();lease?.Dispose();UnityEngine.Object.DestroyImmediate(profile);}
    }
    internal static void ExactWaveDepartureSetAccumulatesAcrossSealedActions()
    {
        var arbiter=new DRPGBattleTransactionArbiter(901,new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
        var waveId=new WaveTransitionIdentity(901,1,1);
        Check(arbiter.TryAcquire(BattleTransactionIdentity.ForWave(waveId),out var lease,out _),"Acquire failed.");
        var ids=new[]{new EnemyDeploymentId("encounter-a",0),new EnemyDeploymentId("encounter-a",1)};
        var activation=new WaveTransitionCompletion(waveId,ids,1,lease);
        var mapping=new Dictionary<EnemyDeploymentId,ParticipantId>{{ids[0],new ParticipantId(91)},{ids[1],new ParticipantId(92)}};
        var ledger=new DRPGEnemyWaveDepartureLedger(activation,mapping);
        mapping[ids[0]]=new ParticipantId(999);
        lease.Dispose();
        Check(!ledger.AllDeparted,"An active wave started already cleared.");
        var first=SealedDeparture(arbiter,1,2,new DRPGBattleDepartureMember(new ParticipantId(91),DRPGBattleSide.Enemy));
        Check(ledger.TryObserve(first,out _) && !ledger.AllDeparted,"First completed departure was rejected or cleared its living peer.");
        Check(ledger.TryObserve(first,out _) && !ledger.AllDeparted,"Duplicate departure counted twice.");
        var foreign=SealedDeparture(arbiter,2,3,new DRPGBattleDepartureMember(new ParticipantId(93),DRPGBattleSide.Enemy));
        Check(!ledger.TryObserve(foreign,out _) && !ledger.AllDeparted,"Another wave's member cleared this wave.");
        var old=SealedDeparture(arbiter,3,0,new DRPGBattleDepartureMember(new ParticipantId(92),DRPGBattleSide.Enemy));
        Check(!ledger.TryObserve(old,out _) && !ledger.AllDeparted,"Pre-activation completion cleared this wave.");
        var party=SealedDeparture(arbiter,4,4,new DRPGBattleDepartureMember(new ParticipantId(7),DRPGBattleSide.Player));
        Check(ledger.TryObserve(party,out _) && !ledger.AllDeparted,"Player departure counted as an enemy or was rejected.");
        var mixed=SealedDeparture(arbiter,6,4,new DRPGBattleDepartureMember(new ParticipantId(92),DRPGBattleSide.Enemy),
            new DRPGBattleDepartureMember(new ParticipantId(93),DRPGBattleSide.Enemy));
        Check(!ledger.TryObserve(mixed,out _) && !ledger.AllDeparted,"Rejected mixed journal partially cleared valid members.");
        var wrongSide=SealedDeparture(arbiter,7,4,new DRPGBattleDepartureMember(new ParticipantId(92),DRPGBattleSide.Player));
        Check(!ledger.TryObserve(wrongSide,out _) && !ledger.AllDeparted,"Known enemy accepted contradictory side metadata.");
        var last=SealedDeparture(arbiter,5,5,new DRPGBattleDepartureMember(new ParticipantId(92),DRPGBattleSide.Enemy));
        Check(ledger.TryObserve(last,out _) && ledger.AllDeparted,"Exact completed departure set did not open the next-wave gate.");
    }
    private static DRPGBattleContinuationInbox.Entry SealedDeparture(DRPGBattleTransactionArbiter arbiter,long serial,long revision,
        params DRPGBattleDepartureMember[] members)
    {
        var id=new ActionIdentity(901,serial,serial);
        var owner=BattleTransactionIdentity.ForRootAction(id);
        Check(arbiter.TryAcquire(owner,out var lease,out _),"Root fixture acquire failed.");
        try
        {
            var journal=new DRPGBattleDepartureJournal(owner,(ulong)serial,members);
            var value=new RootActionCompletion(id,new RootActionTerminal(RootActionTerminalKind.Completed,"complete"),
                DRPGBattleOutcomeCandidate.Continue,revision,journal,lease);
            var inbox=new DRPGBattleContinuationInbox(arbiter);
            Check(inbox.Submit(value).IsAccepted,"Root fixture sealing failed.");
            inbox.TryTake(out var entry);return entry;
        }
        finally {lease.Dispose();}
    }
    private static void RunDelivery(int mode)
    {
        var arbiter=new DRPGBattleTransactionArbiter(901,new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
        var id=new WaveTransitionIdentity(901,1,1);
        Check(arbiter.TryAcquire(BattleTransactionIdentity.ForWave(id),out var lease,out _),"Acquire failed.");
        var inbox=new DRPGBattleContinuationInbox(arbiter);
        var receiver=new WaveReceiver(arbiter,lease,mode);
        var router=new DRPGBattleContinuationRouter(inbox,null,waveRunner:receiver);
        receiver.Router=router;
        var value=new WaveTransitionCompletion(id,new[]{new EnemyDeploymentId("encounter-a",0)},1,lease);
        try
        {
            Check(inbox.Submit(value).IsAccepted && receiver.Starts==0,"Wave advanced inline.");
            bool pumped=router.Pump();
            Check(receiver.Prepares==1,"Wave payload did not reach its registered typed receiver.");
            if(mode==0)
            {
                Check(pumped && receiver.Starts==1 && router.Fault.Length==0 && arbiter.CurrentOwner==receiver.Next &&
                    !arbiter.IsCommandInputOpen,"Wave did not atomically continue to TurnEntry.");
                Check(inbox.Submit(value).Status==ContinuationSubmitStatus.Duplicate && !router.Pump() && receiver.Starts==1,
                    "Duplicate wave completion started another candidate.");
            }
            else Check(!pumped && receiver.Starts==0 && router.Fault.Length!=0 && arbiter.CurrentOwner==value.Owner &&
                !arbiter.IsCommandInputOpen,"Invalid preparation escaped its fail-closed owner.");
        }
        finally {receiver.HeldLease?.Dispose();lease.Dispose();}
    }
    private sealed class WaveReceiver : IBattleWaveContinuationRunner
    {
        private readonly DRPGBattleTransactionArbiter arbiter;
        private readonly BattleRuleMutationLease original;
        private readonly int mode;
        internal DRPGBattleContinuationRouter Router;
        internal BattleRuleMutationLease HeldLease;
        internal BattleTransactionIdentity Next;
        internal int Prepares,Starts;
        internal WaveReceiver(DRPGBattleTransactionArbiter arbiter,BattleRuleMutationLease original,int mode)
        {this.arbiter=arbiter;this.original=original;this.mode=mode;}
        public bool TryPrepare(in WaveTransitionCompletion completion,out BattleTransactionIdentity next,out string error)
        {
            Prepares++;
            Check(arbiter.Owns(original,completion.Owner) && !arbiter.IsCommandInputOpen,"Preparation lost original owner.");
            Check(!Router.Pump(),"Preparation reentered the router.");
            next=mode==2 ? completion.Owner : BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(mode==1?902:901,1,new ParticipantId(5)));
            Next=next;error=string.Empty;
            if(mode==3)
            {
                original.Dispose();
                Check(arbiter.TryAcquire(completion.Owner,out HeldLease,out _),"Test replacement acquire failed.");
            }
            return true;
        }
        public void Start(in WaveTransitionCompletion completion,BattleRuleMutationLease lease)
        {
            Starts++;HeldLease=lease;
            Check(arbiter.Owns(lease,Next) && !arbiter.IsCommandInputOpen && !ReferenceEquals(lease,original),
                "Start received the old lease or an unlocked candidate.");
            Check(!Router.Pump(),"Start reentered the router.");
        }
    }
    private static void Check(bool value,string error){if(!value)throw new InvalidOperationException(error);}
}
#endif
