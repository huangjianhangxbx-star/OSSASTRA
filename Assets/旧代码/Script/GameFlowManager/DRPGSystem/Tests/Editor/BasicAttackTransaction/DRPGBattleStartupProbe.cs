#if UNITY_EDITOR
using System;

internal static class DRPGBattleStartupProbe
{
    [UnityEditor.MenuItem("Tools/ANKALUTE/DRPG/Run Turn Entry Foundation Probes")]
    public static void RunFromMenu()
    {
        DRPGTurnEntryIdentityProbe.PendingEntryInvalidatesPreviousCommandToken();
        DRPGTurnEntryIdentityProbe.PublishedCandidateKeepsItsReservedBoundary();
        DRPGTurnEntryIdentityProbe.OnlyThePendingExactCandidatePublishesOnce();
        DRPGTurnEntryIdentityProbe.SkippedCandidateDoesNotSuppressAfterActionStatus();
        DRPGTurnEntryIdentityProbe.AllocationWithoutOwnershipCannotReplaceTheActor();
        DRPGTurnEntryIdentityProbe.ExpiredCandidateLeaseCannotPublish();
        DRPGBattleContinuationProbe.SubmissionWaitsForPumpAndTransfersWithoutInputGap();
        DRPGBattleContinuationProbe.DuplicateIsIdempotentAndConflictingFingerprintIsRejected();
        DRPGBattleContinuationProbe.UnknownTypeMissingPayloadAndForeignOwnerAreRejected();
        DRPGBattleContinuationProbe.ReacquiredOwnerCannotConsumeAnOlderQueuedLease();
        DRPGBattleContinuationProbe.RunnerCallbackCannotReenterThePump();
        DRPGBattleContinuationProbe.OwnershipIsRecheckedAfterPreparingTheNextRunner();
        DRPGBattleContinuationProbe.BadRouteAndStartFailureRemainFailClosed();
        DRPGBattleContinuationProbe.DepartureMetadataCannotLieAboutStartupCompletion();
        MissingPortLeavesStartupUnallocatedAndRetryable();
        PreparedStartupWaitsForEnqueueAndTheNextPump();
        ImmediateEntryStillCannotRunBeforeTheEnqueueBoundary();
        WrongAndRepeatedStartupCannotCreateAnotherEntry();
        CancellationWaitsForOwnedCleanupAndNeverSubmitsCompletion();
        ForeignReceiptAndOwnershipLossCannotContinue();
        ReentrantCancelWaitsUntilEntryIssueReturns();
        EntryCallbackCannotContinueAfterUnbindingItsPort();
        UnrecognizedTerminalCannotBecomeStartupSuccess();
        UnityEngine.Debug.Log("Turn Entry foundations: 23/23 PASS. Session/FlowRoot integration and actual TurnStart presentation are not covered.");
    }

    // Break caught: production startup silently bypasses its unbound/mismatched presentation port.
    internal static void MissingPortLeavesStartupUnallocatedAndRetryable()
    {
        using (var test = new Fixture(false))
        {
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.PresentationUnavailable &&
                !test.Module.Identity.IsValid && !test.Arbiter.HasOwner && test.Inbox.PendingCount == 0,
                "Unbound startup changed owner or allocated an identity.");
            Check(!test.Module.EnqueuePreparedStartup(default), "Unprepared startup enqueued.");
            Check(test.Ports.TryBind(902, test.Port) == PresentationPortBindResult.InvalidGeneration,
                "Wrong-battle presentation bound.");
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.PresentationUnavailable, "Wrong-battle binding became available.");
            test.Ports.TryBind(901, test.Port);
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared && test.Module.Identity.StartSerial == 1,
                "Explicit binding did not permit the first startup identity.");
        }
    }

    // Break caught: Prepare or Enqueue issues entry inline, before FlowRoot can publish BattleStarted.
    internal static void PreparedStartupWaitsForEnqueueAndTheNextPump()
    {
        using (var test = new Fixture())
        {
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Startup was not prepared.");
            var identity = test.Module.Identity;
            Check(test.Arbiter.CurrentOwner == BattleTransactionIdentity.ForBattleStart(identity) && !test.Arbiter.IsCommandInputOpen,
                "Prepared startup did not retain domain ownership.");
            test.Module.Tick();
            Check(test.Port.Issues == 0, "Prepare started entry before the battle-start notification.");
            Check(test.Module.EnqueuePreparedStartup(identity), "Matching startup could not enqueue.");
            Check(test.Port.Issues == 0 && test.Inbox.PendingCount == 0, "Enqueue dispatched inline.");
            test.Module.Tick();
            Check(test.Port.Issues == 1 && test.Port.Receipt.Owner == test.Arbiter.CurrentOwner &&
                !test.Port.Receipt.Identity.IsValid && test.Port.Command.IssuedAt == 2,
                "The entry receipt lost startup ownership or fabricated an ActionId.");
            Check(!test.Module.CompletionSubmitted && test.Inbox.PendingCount == 0, "Pending entry completed startup.");
            test.Port.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "entry-ready"));
            test.Module.Tick(); test.Module.Tick();
            Check(test.Module.CompletionSubmitted && test.Inbox.PendingCount == 1 && test.Port.Issues == 1 &&
                !test.Arbiter.IsCommandInputOpen, "Entry completion did not enqueue exactly once while retaining startup ownership.");
        }
    }

    // Break caught: immediate headless completion bypasses the external event/pump ordering.
    internal static void ImmediateEntryStillCannotRunBeforeTheEnqueueBoundary()
    {
        using (var test = new Fixture())
        {
            test.Port.Immediate = true;
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            Check(test.Module.EnqueuePreparedStartup(test.Module.Identity), "Enqueue failed.");
            Check(!test.Module.CompletionSubmitted && test.Port.Issues == 0, "Immediate port dispatched before the next pump.");
            test.Module.Tick();
            Check(test.Port.Issues == 1 && test.Module.CompletionSubmitted && test.Inbox.PendingCount == 1,
                "Explicit immediate entry did not finish on the scheduled pump.");
        }
    }

    // Break caught: duplicate/mismatched preparation or enqueue creates another owner/entry.
    internal static void WrongAndRepeatedStartupCannotCreateAnotherEntry()
    {
        using (var test = new Fixture())
        {
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            var identity = test.Module.Identity;
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared && test.Module.Identity == identity,
                "Repeated prepare allocated another startup identity.");
            Check(!test.Module.EnqueuePreparedStartup(new BattleStartIdentity(902, 1)), "Foreign startup enqueued.");
            Check(test.Module.EnqueuePreparedStartup(identity) && !test.Module.EnqueuePreparedStartup(identity), "Repeated enqueue duplicated an entry.");
            test.Module.Tick(); test.Module.Tick();
            Check(test.Port.Issues == 1 && test.Inbox.PendingCount == 0, "Entry was dispatched twice or completed prematurely.");
        }
    }

    // Break caught: cancelled entry releases input before its owned presentation handles have actually cleaned.
    internal static void CancellationWaitsForOwnedCleanupAndNeverSubmitsCompletion()
    {
        using (var test = new Fixture())
        {
            test.Port.DelayedCleanup = true;
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            test.Module.EnqueuePreparedStartup(test.Module.Identity); test.Module.Tick();
            test.Module.Cancel("scene-disabled");
            Check(test.Port.Cleanups == 1 && test.Arbiter.HasOwner && !test.Module.IsClosed,
                "Cancel released input before cleanup, or missed its owned handles.");
            test.Module.Tick();
            Check(test.Arbiter.HasOwner, "Pending cleanup was treated as completed.");
            test.Port.Cleanup.TryComplete(true); test.Module.Tick();
            Check(test.Module.IsClosed && !test.Arbiter.HasOwner && test.Inbox.PendingCount == 0, "Completed cleanup did not close startup safely.");
            test.Port.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "late"));
            test.Module.Tick(); test.Module.Cancel("again");
            Check(test.Port.Cleanups == 1 && test.Inbox.PendingCount == 0, "Late cancelled callback submitted completion or cleaned twice.");
        }
    }

    // Break caught: foreign receipt or owner-loss callback advances a startup it does not own.
    internal static void ForeignReceiptAndOwnershipLossCannotContinue()
    {
        using (var test = new Fixture())
        {
            test.Port.ForeignReceipt = true;
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            test.Module.EnqueuePreparedStartup(test.Module.Identity); test.Module.Tick();
            Check(test.Module.Fault.Length > 0 && !test.Module.CompletionSubmitted && test.Inbox.PendingCount == 0,
                "Foreign receipt completed startup.");
        }
        using (var test = new Fixture())
        {
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            test.Port.OnIssue = () =>
            {
                Check(test.Arbiter.TryCaptureCurrentLease(test.Arbiter.CurrentOwner, out var lease), "No active startup lease.");
                lease.Dispose();
            };
            test.Module.EnqueuePreparedStartup(test.Module.Identity); test.Module.Tick();
            Check(test.Module.Fault.Length > 0 && test.Inbox.PendingCount == 0, "Entry callback lost ownership but startup advanced.");
        }
    }

    // Break caught: a synchronous scene-disable callback closes startup before PresentEntry returns its new handles.
    internal static void ReentrantCancelWaitsUntilEntryIssueReturns()
    {
        using (var test = new Fixture())
        {
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            bool heldDuringCallback = false;
            test.Port.OnIssue = () =>
            {
                test.Module.Cancel("disable-during-entry");
                heldDuringCallback = test.Arbiter.HasOwner && !test.Module.IsClosed;
            };
            test.Module.EnqueuePreparedStartup(test.Module.Identity); test.Module.Tick();
            Check(heldDuringCallback && test.Module.IsClosed && !test.Port.HasEntryHandle && test.Port.Cleanups == 1 && test.Inbox.PendingCount == 0,
                "A handle returned after cancel escaped the cleanup barrier.");
        }
    }

    // Break caught: an immediate callback unbinds the captured port, but its completed receipt still enters continuation.
    internal static void EntryCallbackCannotContinueAfterUnbindingItsPort()
    {
        using (var test = new Fixture())
        {
            test.Port.Immediate = true;
            test.Port.OnIssue = () => test.Ports.TryUnbind(901, test.Port);
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            test.Module.EnqueuePreparedStartup(test.Module.Identity); test.Module.Tick();
            Check(test.Module.Fault.Length > 0 && !test.Module.CompletionSubmitted && test.Inbox.PendingCount == 0,
                "An unbound entry callback submitted completion.");
        }
    }

    internal static void UnrecognizedTerminalCannotBecomeStartupSuccess()
    {
        using (var test = new Fixture())
        {
            Check(test.Module.PrepareStart() == BattleStartupPrepareStatus.Prepared, "Prepare failed.");
            test.Module.EnqueuePreparedStartup(test.Module.Identity); test.Module.Tick();
            test.Port.Receipt.TryComplete(new PresentationReceiptTerminal((PresentationReceiptTerminalKind)41, "invalid-kind"));
            test.Module.Tick();
            Check(test.Module.Fault.Length > 0 && test.Inbox.PendingCount == 0, "Unknown receipt terminal was interpreted as success.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture(bool bind = true)
        {
            var identities = new DRPGBattleIdentityAuthority(901);
            Arbiter = new DRPGBattleTransactionArbiter(901, new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(32)));
            Ports = new DRPGBattleTransactionPresentationPortSlot(901); Port = new Port();
            if (bind) Ports.TryBind(901, Port);
            Inbox = new DRPGBattleContinuationInbox(Arbiter);
            Module = new DRPGBattleStartupModule(identities, Arbiter, Ports, Inbox, new Clock());
        }
        internal readonly DRPGBattleTransactionArbiter Arbiter;
        internal readonly DRPGBattleTransactionPresentationPortSlot Ports;
        internal readonly DRPGBattleContinuationInbox Inbox;
        internal readonly DRPGBattleStartupModule Module;
        internal readonly Port Port;
        public void Dispose() { Module.Cancel("fixture-disposed"); Port.Cleanup?.TryComplete(true); Module.Tick(); }
    }
    private sealed class Clock : IDRPGBattlePresentationClock { public double Now => 2; public long RenderedFrame => 1; }
    private sealed class Port : IStandardBasicAttackPresentationPort, IBattleStartPresentationPort
    {
        internal int Issues, Cleanups;
        internal bool Immediate, DelayedCleanup, ForeignReceipt;
        internal bool HasEntryHandle;
        internal Action OnIssue;
        internal PresentationReceipt Receipt;
        internal PresentationCleanupReceipt Cleanup;
        internal BattleStartEntryCommand Command;
        public PresentationReceipt PresentEntry(in BattleStartEntryCommand command)
        {
            Issues++; Command = command;
            Receipt = new PresentationReceipt(ForeignReceipt ? BattleTransactionIdentity.ForBattleStart(new BattleStartIdentity(999, 1)) : command.Owner,
                DRPGBattleActionStage.TurnBegin);
            OnIssue?.Invoke();
            HasEntryHandle = true;
            if (Immediate || ForeignReceipt) Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "explicit-test-port"));
            return Receipt;
        }
        public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand command)
        { Cleanups++; HasEntryHandle = false; Cleanup = new PresentationCleanupReceipt(); if (!DelayedCleanup) Cleanup.TryComplete(true); return Cleanup; }
        public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest request, out DRPGBasicAttackPresentationFreezeInput snapshot)
        { snapshot = default; throw new InvalidOperationException("Startup must not freeze an attack."); }
        public PresentationReceipt Announce(in AnnouncementCommand command) => throw new NotSupportedException();
        public PresentationReceipt EnterConflict(in ConflictCompositionCommand command) => throw new NotSupportedException();
        public PresentationReceipt PlayWindup(in BasicAttackWindupCommand command) => throw new NotSupportedException();
        public PresentationReceipt PresentImpact(in ImpactPresentationCommand command) => throw new NotSupportedException();
        public PresentationReceipt PresentReaction(in ReactionPresentationCommand command) => throw new NotSupportedException();
        public PresentationReceipt Return(in ReturnPresentationCommand command) => throw new NotSupportedException();
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command) => throw new NotSupportedException();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
