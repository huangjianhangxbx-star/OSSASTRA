#if UNITY_EDITOR
using System;
using System.Reflection;

internal static class DRPGBasicAttackFinalizationProbe
{
    internal static void MissingFaultCleanupConfirmationHasBoundedRetriesAndNeverFakesCompletion()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            f.Start(); f.Port.HoldEmergencyCleanup = true; f.Root.Dispose();
            int initial = f.Port.EmergencyCleanupCalls;
            try
            {
                for (int i = 0; i < 8; i++) f.Step(5.1);
                Check(f.Port.EmergencyCleanupCalls == initial + 2 && !f.Root.FaultCleanupComplete && f.Source.Lease.IsActive &&
                    !f.Domain.Session.TransactionArbiter.IsCommandInputOpen && f.Source.Events == 0 && f.CompletedEvents == 0,
                    "Unconfirmed cleanup retried forever, fabricated completion, or released the failed transaction.");
            }
            finally { f.Port.HeldEmergencyCleanup.TryComplete(true); f.Root.Tick(); }
        }
    }

    internal static void LostFaultCleanupAcknowledgementCanBeRecoveredWithoutReplayingAction()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            f.Start();
            f.Port.HoldEmergencyCleanup = true;
            f.Root.Dispose();
            var lost = f.Port.HeldEmergencyCleanup;
            Check(lost != null && !lost.Completion.IsCompleted && f.Source.Lease.IsActive,
                "Lost acknowledgement fixture did not retain its original input/rule owner.");
            // The actual adapter already cleaned its handles. Only the outward
            // acknowledgement was lost; the next cleanup request can confirm it.
            f.Port.HoldEmergencyCleanup = false;
            f.Step(4.9);
            Check(f.Source.Lease.IsActive, "Fault cleanup retried before its bounded acknowledgement window.");
            f.Step(.2);
            Check(f.Root.FaultCleanupComplete && !f.Source.Lease.IsActive && !f.Domain.Surface.ProjectionArbiter.IsLeased &&
                f.Source.Events == 0 && f.CompletedEvents == 0,
                "A lost cleanup acknowledgement permanently stranded the Root, or recovery replayed its action.");
            lost.TryComplete(true); f.Step(.1);
            Check(f.Source.Events == 0 && f.CompletedEvents == 0 && !f.Source.Lease.IsActive,
                "Late cleanup acknowledgement resurrected its completed fault owner.");
        }
    }

    internal static void SealedRootFaultCleanupRetainsTheNotificationLease()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Session.TransactionArbiter);
            f.Start();
            for (int i = 0; i < 300 && !f.Root.ReadyForFinalization; i++) f.Step(.03);
            Check(Yield(f.Root, inbox), "Fixture did not seal a real completed Root.");
            var completed = f.StartResult.Receipt.Completion.Result;
            f.Port.HoldEmergencyCleanup = true;
            try
            {
                f.Root.CloseForContinuationFault(f.Source.Lease, "rejected continuation");
                Check(f.Port.HeldEmergencyCleanup != null && !f.Root.FaultCleanupComplete && f.Source.Lease.IsActive,
                    "Sealed Root cleanup did not wait for acknowledgement under its original lease.");
                for (int i = 0; i < 3; i++)
                { f.Root.CloseForContinuationFault(f.Source.Lease, "duplicate"); f.Step(.1); }
                Check(f.Source.Lease.IsActive && f.Source.Events == 1 && f.CompletedEvents == 0 &&
                    f.StartResult.Receipt.Completion.Result.Kind == completed.Kind,
                    "Repeated cleanup rewrote a sealed Root, replayed impact or released its notification lease.");
                f.Port.HeldEmergencyCleanup.TryComplete(true); f.Step(.1);
                Check(f.Root.FaultCleanupComplete && f.Source.Lease.IsActive && !f.Domain.Surface.ProjectionArbiter.IsLeased &&
                    !f.Domain.Session.TransactionArbiter.IsCommandInputOpen,
                    "Only Session sealing its technical terminal may release the cleaned notification lease.");
            }
            finally { f.Port.HeldEmergencyCleanup?.TryComplete(true); f.Root.Tick(); f.Source.Lease.Dispose(); }
        }
    }

    internal static void LockTimePresentationLossAlsoCompletesFaultCleanup()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            f.Slot.TryUnbind(f.Domain.Session.BattleGeneration, f.Port);
            f.Start();
            Check(f.StartResult.IsAccepted && f.StartResult.Receipt.Completion.IsCompleted &&
                f.StartResult.Receipt.Completion.Result.Kind == RootActionTerminalKind.Faulted &&
                f.Root.FaultCleanupComplete && !f.Source.Lease.IsActive,
                "Presentation loss just after lock bypassed the fault cleanup terminal.");
        }
    }

    internal static void FaultRetainsOwnershipUntilPresentationCleanupCompletes()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            f.Start();
            f.Port.HoldEmergencyCleanup = true;
            try
            {
                f.Root.Dispose();
                Check(f.StartResult.Receipt.Completion.IsCompleted &&
                    f.StartResult.Receipt.Completion.Result.Kind == RootActionTerminalKind.Faulted,
                    "Fault must seal the root receipt before waiting for cleanup.");
                Check(f.Port.HeldEmergencyCleanup != null && f.Source.Lease.IsActive &&
                    !f.Domain.Session.TransactionArbiter.IsCommandInputOpen,
                    "Fault released rule/input ownership before presentation cleanup acknowledged.");
                for (int i = 0; i < 4; i++) { f.Root.Dispose(); f.Step(.1); }
                Check(f.Source.Lease.IsActive && f.Source.Events == 0 && f.CompletedEvents == 0,
                    "Pending cleanup advanced the failed action or opened input.");
                f.Port.HeldEmergencyCleanup.TryComplete(true);
                f.Step(.1);
                Check(!f.Source.Lease.IsActive && !f.Domain.Surface.ProjectionArbiter.IsLeased,
                    "Acknowledged cleanup left projection or rule ownership behind.");
            }
            finally { f.Port.HeldEmergencyCleanup?.TryComplete(true); f.Root.Tick(); }
        }
    }

    internal static void FinalSettlementClosesProjectionAndYieldsExactlyOnce()
    {
        foreach (bool enemy in new[] { false, true })
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(enemy, statuses: true))
        {
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Session.TransactionArbiter);
            var actor = f.Domain.Session.CurrentActor; var token = f.Domain.Session.CurrentActorTurnToken;
            int flushes = 0;
            f.Domain.Surface.CanonicalProjectionClosing += () => flushes++;
            f.Start();
            for (int i = 0; i < 300 && !f.Root.ReadyForFinalization; i++)
            {
                Check(!Yield(f.Root, inbox), "Root yielded before the final Stage9 render receipt.");
                Check(inbox.PendingCount == 0 && f.Source.Lease.IsActive && f.CompletedEvents == 0 &&
                    f.Domain.Session.CurrentActor == actor && f.Domain.Session.CurrentActorTurnToken == token,
                    "Stage9 changed actor, owner or published completion early.");
                f.Step(.03);
            }
            Check(f.Root.ReadyForFinalization, "Root never reached its final barrier.");
            Check(Yield(f.Root, inbox), "Finished Root cannot yield its sealed final completion.");
            Check(inbox.PendingCount == 1 && f.StartResult.Receipt.Completion.IsCompleted && flushes == 1 &&
                !f.Domain.Surface.ProjectionArbiter.IsLeased && f.Source.Lease.IsActive &&
                !f.Domain.Session.TransactionArbiter.IsCommandInputOpen && f.CompletedEvents == 0,
                "Yield must seal/flush exactly once, retain rule/input ownership and not invoke continuation inline.");
            for (int i = 0; i < 4; i++) { Check(Yield(f.Root, inbox), "Same finalization was not idempotent."); f.Step(.1); }
            Check(inbox.PendingCount == 1 && flushes == 1 && f.Source.Events == 1 &&
                f.StartResult.Receipt.Completion.Result.Kind != RootActionTerminalKind.Faulted,
                "Late Tick/final callback replayed impact, canonical close or terminal.");
        }
    }

    internal static void CanonicalCloseReentryCannotYieldDisposedRoot()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Session.TransactionArbiter);
            f.Start(); for (int i = 0; i < 300 && !f.Root.ReadyForFinalization; i++) f.Step(.03);
            f.Domain.Surface.CanonicalProjectionClosing += () => f.Root.Dispose();
            Check(!Yield(f.Root, inbox) && inbox.PendingCount == 0, "Disposed Root escaped through canonical close reentry.");
            Check(f.StartResult.Receipt.Completion.IsCompleted &&
                f.StartResult.Receipt.Completion.Result.Kind == RootActionTerminalKind.Faulted && !f.Source.Lease.IsActive,
                "Canonical close cancellation did not fault and release the owned transaction.");
        }
    }

    private static bool Yield(DRPGStandardBasicAttackTransactionModule root, IBattleContinuationPort inbox)
    {
        var method = root.GetType().GetMethod("TryYieldCompletion", BindingFlags.Instance | BindingFlags.NonPublic);
        return method != null && (bool)method.Invoke(root, new object[] { inbox, null });
    }

    internal static void OutcomeAndDepartureMembershipComeFromFinalJournals()
    {
        foreach (bool mutual in new[] { false, true })
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(lethal: !mutual, statuses: mutual, settlementDeaths: mutual))
        {
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Session.TransactionArbiter);
            f.Start(); for (int i = 0; i < 300 && !f.Root.ReadyForFinalization; i++) f.Step(.03);
            Check(Yield(f.Root, inbox), "Lethal Root did not yield.");
            Check(inbox.TryTake(out var entry), "Missing lethal completion.");
            Check(entry.Root.Outcome == (mutual ? DRPGBattleOutcomeCandidate.Defeat : DRPGBattleOutcomeCandidate.Victory) &&
                entry.Root.Departures.Members.Count == (mutual ? 2 : 1) &&
                entry.Root.FinalBattleRevision == f.Domain.Session.TransactionState.BattleRevision,
                "Final outcome ignored mutual defeat or omitted a Stage7/9 departure.");
            Check(inbox.Submit(entry.Root).Status == ContinuationSubmitStatus.Duplicate && inbox.PendingCount == 0,
                "Delivered Root was enqueued again.");
        }
    }

    internal static void InboxRejectsChangedFactsAndReacquiredPhysicalLease()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Session.TransactionArbiter);
            f.Start(); for (int i = 0; i < 300 && !f.Root.ReadyForFinalization; i++) f.Step(.03);
            Check(Yield(f.Root, inbox), "Root did not yield.");
            Check(inbox.TryTake(out var entry), "Missing completion.");
            var value = entry.Root;
            var forged = new RootActionCompletion(value.Identity, value.Terminal, DRPGBattleOutcomeCandidate.Defeat,
                value.FinalBattleRevision, value.Departures, value.Lease);
            Check(!inbox.Submit(forged).IsAccepted && inbox.PendingCount == 0, "Conflicting outcome replaced a sealed completion.");
            value.Lease.Dispose();
            Check(f.Domain.Session.TransactionArbiter.TryAcquire(value.Owner, out var replacement, out var error), error);
            try
            {
                var fresh = new DRPGBattleContinuationInbox(f.Domain.Session.TransactionArbiter);
                Check(!fresh.Submit(value).IsAccepted, "Same owner key with a different physical lease accepted stale completion.");
            }
            finally { replacement.Dispose(); }
        }
    }

    internal static void NextPumpTransfersRootWithoutInputGapOrReentrantDelivery()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Session.TransactionArbiter);
            var next = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(f.Frozen.Identity.BattleGeneration,
                f.Frozen.Identity.TurnSerial + 1, new ParticipantId(f.Domain.Enemy.RuntimeId)));
            var receiver = new RecordingReceiver(next);
            var router = new DRPGBattleContinuationRouter(inbox, null, rootRunner: receiver);
            receiver.Pump = router.Pump;
            f.Start(); for (int i = 0; i < 300 && !f.Root.ReadyForFinalization; i++) f.Step(.03);
            Check(Yield(f.Root, inbox) && receiver.Starts == 0, "Root started next runner inline.");
            Check(router.Pump(), "Root completion did not dispatch: " + router.Fault);
            try
            {
                Check(receiver.Starts == 1 && receiver.Preparations == 1 && !f.Source.Lease.IsActive &&
                    receiver.Lease.Owner == next && receiver.Lease.IsActive &&
                    !receiver.Lease.Arbiter.IsCommandInputOpen && !router.Pump(), "Root transfer lost ownership or delivered twice.");
                f.Root.Dispose();
                Check(receiver.Lease.IsActive, "Disposing the old Root released the next runner's lease.");
            }
            finally { receiver.Lease?.Dispose(); }
        }
    }

    private sealed class RecordingReceiver : IBattleRootContinuationRunner
    {
        private readonly BattleTransactionIdentity next;
        internal Func<bool> Pump;
        internal int Preparations, Starts;
        internal BattleRuleMutationLease Lease;
        internal RecordingReceiver(BattleTransactionIdentity next) { this.next = next; }
        public bool TryPrepare(in RootActionCompletion value, out BattleTransactionIdentity owner, out string error)
        {
            Preparations++; owner = next; error = string.Empty;
            Check(value.Lease.IsActive && !value.Lease.Arbiter.IsCommandInputOpen && !Pump(), "Preparation opened input or reentered Pump.");
            return true;
        }
        public void Start(in RootActionCompletion value, BattleRuleMutationLease lease)
        { Starts++; Lease = lease; Check(!value.Lease.IsActive && lease.Owner == next && !Pump(), "Start did not hold the transferred owner."); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
