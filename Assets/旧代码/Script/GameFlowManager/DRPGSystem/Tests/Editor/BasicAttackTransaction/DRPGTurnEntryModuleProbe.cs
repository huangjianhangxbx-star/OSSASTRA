#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using static DRPGTurnEntrySettlementProbe;

internal static class DRPGTurnEntryModuleProbe
{
    // Break caught: Session.Start publishes a turn before the entry receipt,
    // or startup and TurnEntry use separate schedulers/identity authorities.
    private static void SessionStartupDefersTheFirstActorThroughItsOwnEntry() => CheckStartup(0);
    private static void SessionAbortWaitsForStartupCleanup() => CheckStartup(1);
    private static void SessionAbortSealsQueuedStartupCompletion() => CheckStartup(2);
    private static void SessionAbortClosesCandidateProjectionBeforeResult() => CheckStartup(3);
    private static void SessionAbortDuringEntryIssueCannotLeakAHandle() => CheckStartup(4);
    private static void StartupFaultWaitsForCleanupAndNeverBecomesBattleEnded() => CheckStartup(5);
    private static void CandidateFaultClosesScopeBeforePublishingTechnicalTerminal() => CheckStartup(6);
    private static void ContinuationOpenFailureCleansTheTransferredOwner() => CheckStartup(7);
    private static void CandidateCleanupExceptionRetainsOwnershipUntilRetrySucceeds() => CheckStartup(8);
    private static void RejectedPartialCandidateRetainsScopeUntilCleanupSucceeds() => CheckStartup(9);
    private static void InvalidPartialCandidateRetainsScopeUntilCleanupSucceeds() => CheckStartup(10);
    private static void CheckStartup(int abortAt)
    {
        var profile = UnityEngine.ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            var player = new BattleParticipantRuntime(8101, new BattleParticipantSpec(profile,
                DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            var enemy = new BattleParticipantRuntime(8102, new BattleParticipantSpec(profile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0));
            var session = DRPGBattleHeadlessSessionFactory.CreateUnbound(new[] { player, enemy });
            var clock = new DRPGSettlementPresentationProbe.FixtureClock();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var prepare = typeof(DRPGBattleSession).GetMethod("PrepareStart", flags);
            var enqueue = typeof(DRPGBattleSession).GetMethod("EnqueuePreparedStartup", flags);
            Check(prepare != null && enqueue != null, "Session has no two-phase startup entry.");
            Check((BattleStartupPrepareStatus)prepare.Invoke(session, new object[] { clock }) == BattleStartupPrepareStatus.PresentationUnavailable &&
                session.CurrentActor == null && !session.TransactionArbiter.HasOwner && session.Timeline.Count == 0,
                "Unbound startup allocated or initialized a turn.");
            var startup = new StartupPort(clock);
            session.TransactionPresentationPortSlot.TryBind(session.BattleGeneration, startup);
            Check((BattleStartupPrepareStatus)prepare.Invoke(session, new object[] { clock }) == BattleStartupPrepareStatus.PresentationUnavailable,
                "Startup prepared without its candidate presentation factory.");
            var factory = new EntryFactory(null, clock);
            Check(session.TryBindTurnEntryPresentationFactory(factory), "Factory bind failed.");
            int actors = 0, completed = 0; bool inputInEvent = false;
            session.CurrentActorChanged += _ => { actors++; inputInEvent |= session.TransactionArbiter.IsCommandInputOpen; };
            session.ActionCompleted += _ => completed++;
            Check((BattleStartupPrepareStatus)prepare.Invoke(session, new object[] { clock }) == BattleStartupPrepareStatus.Prepared,
                "Bound Session could not prepare startup.");
            var owner = session.TransactionArbiter.CurrentOwner;
            Check(owner.Kind == BattleTransactionKind.BattleStart && !session.TransactionArbiter.IsCommandInputOpen &&
                session.CurrentActor == null && actors == 0 && startup.Issues == 0 && !session.TransactionState.TurnCandidates.HasReservation,
                "Preparation ran entry or published/reserved a candidate inline.");
            var driver = new DRPGBattleFlowStateMachine();
            typeof(DRPGBattleFlowStateMachine).GetField("session", flags).SetValue(driver, session);
            typeof(DRPGBattleFlowStateMachine).GetField("isRunning", flags).SetValue(driver, true);
            driver.Update(0);
            Check(startup.Issues == 0, "Prepared but not enqueued startup ran.");
            Check((bool)enqueue.Invoke(session, new object[] { owner.BattleStart }), "Matching startup could not enqueue.");
            Check(startup.Issues == 0 && actors == 0, "Enqueue issued entry inline.");
            if (abortAt == 4) startup.OnIssue = session.Abort;
            driver.Update(0);
            Check(startup.Issues == 1 && factory.Opened == 0 && session.TransactionArbiter.CurrentOwner == owner,
                "Startup pump skipped receipt or selected a candidate early.");
            if (abortAt == 5) { CheckTechnicalFault(session, startup, factory, clock, owner, true); return; }
            if (abortAt == 1 || abortAt == 4)
            { CheckStartupAbort(session, driver, startup, factory, clock, abortAt == 4); return; }
            startup.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "entry-rendered"));
            driver.Update(0);
            Check(factory.Opened == 0 && actors == 0 && session.TransactionArbiter.CurrentOwner == owner,
                "Receipt terminal transferred inline.");
            if (abortAt == 2)
            { CheckStartupAbort(session, driver, startup, factory, clock, false); return; }
            if (abortAt == 7) factory.RejectOpen = true;
            if (abortAt == 9 || abortAt == 10)
            { factory.RejectPartial = abortAt == 9; factory.InvalidPort = abortAt == 10; factory.ThrowOnClose = true; }
            driver.Update(0);
            if (abortAt == 7 || abortAt == 9 || abortAt == 10)
            { CheckTechnicalFault(session, startup, factory, clock, session.TransactionArbiter.CurrentOwner, false, true); return; }
            Check(factory.Opened == 1 && factory.Latest.Receipts.Count == 0 && actors == 0 &&
                session.TransactionArbiter.CurrentOwner.Kind == BattleTransactionKind.TurnEntry && !session.TransactionArbiter.IsCommandInputOpen,
                "Startup did not transfer to one deferred real candidate: " + driver.LastError);
            driver.Update(0);
            if (abortAt == 6 || abortAt == 8)
            {
                factory.ThrowOnClose = abortAt == 8;
                CheckTechnicalFault(session, startup, factory, clock, session.TransactionArbiter.CurrentOwner, false);
                return;
            }
            if (abortAt == 3)
            { CheckStartupAbort(session, driver, startup, factory, clock, false); return; }
            for (int i = 0; i < 3; i++)
            { factory.Latest.Complete(); clock.Now += .451; clock.RenderedFrame++; driver.Update(.451f); }
            Check(actors == 0 && !session.TransactionArbiter.IsCommandInputOpen, "Candidate published inline with its last render.");
            driver.Update(0);
            // No authored skills: the real planner schedules an early enemy
            // Wait, which must still pass through the same TurnEntry module.
            Check(actors == 1 && completed == 0 && !inputInEvent && session.CurrentActor == enemy &&
                session.CurrentActorTurnToken.IsValid && session.TransactionArbiter.IsCommandInputOpen && factory.Disposed == 1 &&
                driver.LastError.Length == 0, "Startup actor=" + session.CurrentActor?.RuntimeId + " events=" + actors +
                " completed=" + completed + " inputInEvent=" + inputInEvent + " disposed=" + factory.Disposed +
                " open=" + session.TransactionArbiter.IsCommandInputOpen + " error=" + driver.LastError);
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); }
    }

    private static void CheckTechnicalFault(DRPGBattleSession session, StartupPort startup, EntryFactory factory,
        DRPGSettlementPresentationProbe.FixtureClock clock, BattleTransactionIdentity owner, bool atStartup, bool alreadyFaulted = false)
    {
        int faults = 0, ended = 0, actions = 0, actors = 0;
        session.BattleEnded += _ => ended++;
        session.ActionCompleted += _ => actions++;
        session.CurrentActorChanged += _ => actors++;
        session.BattleFaulted += _ => throw new InvalidOperationException("isolated fault observer");
        session.BattleFaulted += terminal =>
        {
            faults++;
            Check(terminal.Owner == owner && terminal.Kind.ToString() == (atStartup ? "Startup" : "TurnEntry") &&
                session.LastBattleFault.HasValue && !session.IsActive && !session.TransactionArbiter.HasOwner &&
                factory.Opened == factory.Disposed, "Technical terminal was published before exact-owner cleanup.");
            session.Abort(); session.TickTurnEntry();
        };
        if (atStartup)
        {
            startup.DelayedCleanup = true;
            startup.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "injected entry failure"));
        }
        else if (!alreadyFaulted)
            factory.Latest.Receipts[factory.Latest.Receipts.Count - 1].TryComplete(
                new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "injected candidate failure"));
        session.TickTurnEntry(); session.TickTurnEntry();
        if (atStartup || factory.ThrowOnClose)
        {
            Check(faults == 0 && session.TransactionArbiter.CurrentOwner == owner && !session.TransactionArbiter.IsCommandInputOpen,
                "Fault released ownership before cleanup acknowledgement.");
            session.Abort(); session.TickTurnEntry();
            Check(ended == 0 && session.Outcome == DRPGBattleOutcome.None, "Abort converted a pending technical fault into BattleEnded.");
            if (atStartup)
            {
                Check(startup.Cleanups == 1 && startup.Cleanup != null, "Startup fault never initiated owned cleanup.");
                startup.Cleanup.TryComplete(true);
            }
            factory.ThrowOnClose = false;
        }
        for (int i = 0; i < 4; i++) { clock.Now += .1; clock.RenderedFrame++; session.TickTurnEntry(); }
        Check(faults == 1 && ended == 0 && actions == 0 && actors == 0 && session.LastBattleFault.HasValue &&
            !session.IsActive && !session.TransactionArbiter.HasOwner && session.CurrentActor == null &&
            factory.Opened == factory.Disposed && session.Outcome == DRPGBattleOutcome.None && session.ObserverDiagnostics.Count == 1,
            "Fault did not cleanly seal once: faults=" + faults + " ended=" + ended + " owner=" + session.TransactionArbiter.HasOwner +
            " scopes=" + factory.Opened + "/" + factory.Disposed + " diagnostic=" + session.TurnEntryFault);
        session.Start(); session.TickTurnEntry();
        Check(!session.IsActive && !session.TransactionArbiter.HasOwner && actors == 0 && faults == 1,
            "A faulted Session could restart its scheduler or publish another actor.");
    }

    private static void CheckStartupAbort(DRPGBattleSession session, DRPGBattleFlowStateMachine driver,
        StartupPort startup, EntryFactory factory, DRPGSettlementPresentationProbe.FixtureClock clock, bool alreadyRequested)
    {
        int ended = 0; bool inputInEvent = false; bool scopeOpenInEvent = false;
        int opened = factory.Opened;
        session.BattleEnded += _ => { ended++; inputInEvent |= session.TransactionArbiter.IsCommandInputOpen;
            scopeOpenInEvent |= factory.Opened != factory.Disposed; };
        if (!alreadyRequested) session.Abort();
        Check(session.Outcome == DRPGBattleOutcome.None && ended == 0 && session.TransactionArbiter.HasOwner,
            "Abort completed inline before owned cleanup.");
        startup.DelayedCleanup = true;
        driver.Update(0);
        if (opened == 0)
        {
            Check(startup.Cleanups == 1 && session.TransactionArbiter.HasOwner && ended == 0,
                "Startup abort failed to wait for its cleanup receipt.");
            startup.Cleanup.TryComplete(true); driver.Update(0);
        }
        Check(ended == 1 && !inputInEvent && !scopeOpenInEvent && session.Outcome == DRPGBattleOutcome.Aborted &&
            !session.TransactionArbiter.HasOwner && factory.Opened == factory.Disposed && session.CurrentActor == null,
            "Abort failed cleanup-before-result ordering: " + driver.LastError);
        startup.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "late"));
        clock.Now += 1; clock.RenderedFrame++; driver.Update(1);
        Check(ended == 1 && factory.Opened == opened && session.CurrentActor == null, "Cancelled startup continued on a late receipt.");
    }

    private sealed class StartupPort : IStandardBasicAttackPresentationPort, IBattleStartPresentationPort
    {
        internal StartupPort(IDRPGBattlePresentationClock clock) { }
        internal int Issues;
        internal int Cleanups;
        internal bool DelayedCleanup;
        internal Action OnIssue;
        internal PresentationCleanupReceipt Cleanup;
        internal PresentationReceipt Receipt;
        public PresentationReceipt PresentEntry(in BattleStartEntryCommand command)
        { Issues++; OnIssue?.Invoke(); return Receipt = new PresentationReceipt(command.Owner, DRPGBattleActionStage.TurnBegin); }
        public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand command)
        { Cleanups++; Cleanup = new PresentationCleanupReceipt(); if (!DelayedCleanup) Cleanup.TryComplete(true); return Cleanup; }
        public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest request, out DRPGBasicAttackPresentationFreezeInput snapshot)
        { snapshot = default; throw new NotSupportedException(); }
        public PresentationReceipt Announce(in AnnouncementCommand command) => throw new NotSupportedException();
        public PresentationReceipt EnterConflict(in ConflictCompositionCommand command) => throw new NotSupportedException();
        public PresentationReceipt PlayWindup(in BasicAttackWindupCommand command) => throw new NotSupportedException();
        public PresentationReceipt PresentImpact(in ImpactPresentationCommand command) => throw new NotSupportedException();
        public PresentationReceipt PresentReaction(in ReactionPresentationCommand command) => throw new NotSupportedException();
        public PresentationReceipt Return(in ReturnPresentationCommand command) => throw new NotSupportedException();
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command) => throw new NotSupportedException();
    }

    // Break caught: the real Session never receives the settled candidate, or
    // releases input before CurrentActorChanged observers finish.
    private static void SettledCandidatePublishesToTheRealSessionExactlyOnce() => CheckSessionPublication(false);
    private static void RealFlowAutomaticallyPumpsEntryAndDefersPublication() => CheckSessionPublication(true);
    private static void EnemyWarningPreparationDoesNotBecomeAnActorTurn() => CheckSessionPublication(false, 1);
    private static void ScheduledEnemyWaitKeepsItsExecutablePlan() => CheckSessionPublication(false, 2);
    private static void SameTimeEnemyPreparationPrecedesPlayerPublication() => CheckSessionPublication(false, 3);
    private static void RealFlowSkipsThenPublishesOnlyTheNextEligibleCandidate() => CheckSessionPublication(true, 4);
    private static void LethalEntryPublishesOneResultAfterTheFinalRenderedGate() => CheckSessionPublication(true, 6);
    private static void ProductionFactoryOwnsAndReleasesTheActualProjection()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(false, false, false, false, false, true))
        {
            f.Projection.Dispose();
            var type = typeof(BattleTurnEntryPresentationAdapter).Assembly.GetType("BattleTurnEntryPresentationFactory");
            Check(type != null, "No production candidate scope factory is available.");
            var factory = (IBattleTurnEntryPresentationFactory)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { f.Domain.Surface.ProjectionArbiter, f.Visuals.Feedback, f.Visuals.Camera, f.Defeated, f.Clock }, null);
            Check(!factory.TryOpen(f.Domain.Owner, out var invalid, out _) && invalid == null && !f.Domain.Surface.ProjectionArbiter.IsLeased,
                "Root identity acquired a candidate scope.");
            Check(factory.TryOpen(f.RuleLease.Owner, out var scope, out var error), error);
            using (scope)
            {
                Check(scope.IsActive && scope.Port is BattleTurnEntryPresentationAdapter &&
                    scope.Port.TryCaptureHomes(f.RuleLease.Owner, out var homes) && homes.Count == 2 &&
                    f.Domain.Surface.ProjectionArbiter.IsLeased && f.RuleLease.IsActive, "Factory did not bind real candidate views/Home slots.");
                Check(!factory.TryOpen(f.RuleLease.Owner, out var duplicate, out _) && duplicate == null && scope.IsActive,
                    "A second candidate factory replaced the live projection.");
            }
            Check(!scope.IsActive && !f.Domain.Surface.ProjectionArbiter.IsLeased && f.RuleLease.IsActive,
                "Candidate scope cleanup leaked projection or released domain ownership.");
        }
    }

    private static void CheckSessionPublication(bool automatic, int preparation = 0)
    {
        var profile = UnityEngine.ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        var basicSkill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
        var extraSkill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(DRPGSkillSO).GetField("skillId", fields).SetValue(basicSkill, "entry-basic");
            typeof(DRPGSkillSO).GetField("skillId", fields).SetValue(extraSkill, "entry-other");
            typeof(DRPGCombatantProfileSO).GetField("skills", fields).SetValue(profile, new List<DRPGSkillSO> { basicSkill, extraSkill });
            var player = new BattleParticipantRuntime(8101, new BattleParticipantSpec(profile,
                DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            var enemy = new BattleParticipantRuntime(8102, new BattleParticipantSpec(profile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0));
            var scheduler = new DRPGTurnScheduler();
            var session = DRPGBattleHeadlessSessionFactory.CreateUnbound(new[] { player, enemy }, scheduler);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var identities = (DRPGBattleIdentityAuthority)typeof(DRPGBattleSession).GetField("transactionIdentityAuthority", flags).GetValue(session);
            var flow = (DRPGBattleFlowOrchestrator)typeof(DRPGBattleSession).GetField("flow", flags).GetValue(session);
            var start = BattleTransactionIdentity.ForBattleStart(identities.AllocateBattleStart());
            Check(session.TransactionArbiter.TryAcquire(start, out var setup, out var error), error);
            using (setup)
            using (session.TransactionState.OpenCompatibilityMutationScope(setup))
            {
                scheduler.Initialize(new[] { player, enemy });
                Check(flow.TryStartBattle(out error) && flow.TryBeginTurn(out error), error);
                if (preparation == 4) player.ReducePosture(player.CurrentPosture);
                if (preparation == 6)
                {
                    var status = UnityEngine.ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
                    try
                    {
                        DRPGBasicAttackPreludeProbe.Set(status, "statusId", "entry-lethal-result");
                        DRPGBasicAttackPreludeProbe.Set(status, "triggerTiming", DRPGStatusTriggerTiming.TurnStart);
                        DRPGBasicAttackPreludeProbe.Set(status, "turnStartHpDelta", -10000f);
                        DRPGBasicAttackPreludeProbe.Set(status, "durationTurns", 2);
                        var draft = session.TransactionState.CreateWorkspace();
                        draft.For(new ParticipantId(player.RuntimeId)).ApplyStatus(DRPGResolvedStatusSpec.From(status), 1, 0, new ParticipantId(player.RuntimeId));
                        Check(session.TransactionState.TryCommitJournal(setup, draft).Succeeded, "Could not seed lethal entry status.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(status); }
                }
                if (preparation > 0 && preparation < 4)
                {
                    scheduler.ScheduleAt(player, preparation == 3 ? 10 : 20, -1);
                    scheduler.ScheduleAt(enemy, 10, 0);
                    enemy.ReducePosture(20);
                    float postureBefore = enemy.CurrentPosture;
                    var plan = preparation == 2 ? DRPGEnemyActionPlan.CreateWait(101, enemy.RuntimeId, 0, 10) :
                        DRPGEnemyActionPlan.CreateAttack(101, enemy.RuntimeId, basicSkill, 0, 10, 2);
                    session.TransactionState.TurnCandidates.Plans[enemy.RuntimeId] = plan;
                    var reserve = typeof(DRPGBattleSession).GetMethod("TryReserveActionableTurnCandidate", flags);
                    object[] call = { setup, default(DRPGTurnCandidateReservation), null };
                    Check(reserve != null && (bool)reserve.Invoke(session, call),
                        "The formal candidate path cannot process enemy preparation: " + call[2]);
                    var candidate = (DRPGTurnCandidateReservation)call[1];
                    var after = session.TransactionState.TurnCandidates.Plans[enemy.RuntimeId];
                    Check(candidate.Candidate.Participant == (preparation == 2 ? enemy : player) &&
                        candidate.SourceKind == (preparation == 2 ? DRPGTurnCandidateSourceKind.ExecutableEnemyPlan : DRPGTurnCandidateSourceKind.SchedulerTimeline) &&
                        after.PlanId == 101 && after.State == (preparation == 2 ? DRPGEnemyPlanState.Executable : DRPGEnemyPlanState.AwaitingPlayerResponse) &&
                        enemy.CurrentPosture == postureBefore && session.CurrentActor == null && !session.CurrentActorTurnToken.IsValid &&
                        setup.IsActive && !session.TransactionArbiter.IsCommandInputOpen,
                        "Enemy preparation became a turn, lost its plan, or skipped the player's warning-zero response.");
                    Check(session.TransactionState.TurnCandidates.TryDiscardLegacy(candidate, out error), error);
                    return;
                }
            }
            var source = session.TransactionState.TurnCandidates;
            Check(source.TryReserve(out var reservation, out error), error);
            var entry = identities.AllocateTurnEntry(new ParticipantId(reservation.Candidate.Participant.RuntimeId));
            Check(session.TransactionArbiter.TryAcquire(BattleTransactionIdentity.ForTurnEntry(entry), out var lease, out error), error);
            using (lease)
            {
                Check(identities.TryBeginTurnEntry(entry, lease), "Entry identity not activated.");
                var clock = new DRPGSettlementPresentationProbe.FixtureClock();
                var port = new Port(new Dictionary<ParticipantId, int> {
                    { new ParticipantId(player.RuntimeId), 0 }, { new ParticipantId(enemy.RuntimeId), 0 } }, clock);
                using (var module = new DRPGBattleTurnEntryModule(session.TransactionState, lease, reservation, port, clock, () => true))
                {
                    if (automatic)
                    {
                        var enqueue = typeof(DRPGBattleSession).GetMethod("TryEnqueueTurnEntry", flags);
                        using (var foreign = new Fixture(false))
                            Check(enqueue != null && !(bool)enqueue.Invoke(session, new object[] { foreign.Module, lease, null, null }),
                                "Runner accepted a foreign module using a local lease.");
                        Check(enqueue != null && (bool)enqueue.Invoke(session, new object[] { module, lease, null, null }),
                            "Formal Session cannot enqueue its TurnEntry runner.");
                        var driver = new DRPGBattleFlowStateMachine();
                        typeof(DRPGBattleFlowStateMachine).GetField("session", flags).SetValue(driver, session);
                        typeof(DRPGBattleFlowStateMachine).GetField("isRunning", flags).SetValue(driver, true);
                        int actorEvents = 0;
                        session.CurrentActorChanged += _ => actorEvents++;
                        if (preparation == 6)
                        {
                            int ended = 0, completed = 0; bool openDuringResult = false;
                            session.ActionCompleted += _ => completed++;
                            session.BattleEnded += _ => { ended++; openDuringResult |= session.TransactionArbiter.IsCommandInputOpen; };
                            var endHandler = (Action<DRPGBattleResult>)Delegate.CreateDelegate(typeof(Action<DRPGBattleResult>), driver,
                                typeof(DRPGBattleFlowStateMachine).GetMethod("OnBattleEnded", flags));
                            session.BattleEnded += endHandler;
                            driver.Update(0);
                            port.Complete(); clock.Now += .451; clock.RenderedFrame++; driver.Update(.451f);
                            Check(player.CurrentHp == 0 && ended == 0 && actorEvents == 0 && lease.IsActive && port.Departure != null,
                                "Lethal candidate ended battle before its departure presentation.");
                            port.Departure.RecordView(new DRPGDepartureViewFact(entry.Actor, 101, 201));
                            port.Complete(); clock.RenderedFrame++; driver.Update(0);
                            port.Complete(); clock.Now += .301; clock.RenderedFrame++; driver.Update(.301f);
                            Check(ended == 0 && lease.IsActive && session.Outcome == DRPGBattleOutcome.None,
                                "Battle result was delivered inline with final render completion.");
                            driver.Update(0);
                            Check(ended == 1 && !openDuringResult && completed == 0 && actorEvents == 0 && !lease.IsActive &&
                                session.Outcome == DRPGBattleOutcome.Defeat && session.CurrentActor == null && !session.CurrentActorTurnToken.IsValid &&
                                session.Phase == DRPGBattlePhase.Result && driver.Session == null && driver.LastError.Length == 0,
                                "Real Flow did not finish exactly one delayed defeat result: " + driver.LastError);
                            driver.Update(0);
                            Check(ended == 1 && completed == 0, "Result replayed or invented an ActionCompleted.");
                            return;
                        }
                        if (preparation == 4)
                        {
                            var factory = new EntryFactory(port, clock);
                            var bind = typeof(DRPGBattleSession).GetMethod("TryBindTurnEntryPresentationFactory", flags);
                            Check(bind != null && (bool)bind.Invoke(session, new object[] { factory }), "Real Session has no candidate presentation factory binding.");
                            driver.Update(0);
                            for (int i = 0; i < 4; i++)
                            { factory.Latest.Complete(); clock.Now += .451; clock.RenderedFrame++; driver.Update(.451f); }
                            Check(actorEvents == 0 && lease.IsActive, "Skipped actor was published or released before queued continuation.");
                            driver.Update(0);
                            Check(actorEvents == 0 && !lease.IsActive && !session.TransactionArbiter.IsCommandInputOpen &&
                                factory.Opened == 1 && factory.Latest.Receipts.Count == 0, "Skip did not atomically transfer to one deferred candidate.");
                            driver.Update(0);
                            for (int i = 0; i < 3; i++)
                            { factory.Latest.Complete(); clock.Now += .451; clock.RenderedFrame++; driver.Update(.451f); }
                            Check(actorEvents == 0 && !session.TransactionArbiter.IsCommandInputOpen, "Next candidate published inline with finalization.");
                            driver.Update(0);
                            Check(actorEvents == 1 && session.CurrentActor == enemy && session.CurrentActorTurnToken.Actor.Value == enemy.RuntimeId &&
                                session.TransactionArbiter.IsCommandInputOpen && factory.Disposed == 1,
                                "Skip continuation failed to publish the next actual candidate or clean its scope: " + driver.LastError);
                            return;
                        }
                        driver.Update(0);
                        Check(module.Ledger != null && port.Receipts.Count == 1, "Real flow did not tick the candidate.");
                        for (int i = 0; i < 3; i++)
                        {
                            port.Complete(); clock.Now += .451; clock.RenderedFrame++; driver.Update(.451f);
                            Check(session.CurrentActor == null && actorEvents == 0 && lease.IsActive,
                                "Entry terminal published inline instead of through the next pump.");
                        }
                        long revision = session.TransactionState.BattleRevision;
                        driver.Update(0);
                        Check(session.CurrentActor == reservation.Candidate.Participant && actorEvents == 1 &&
                            session.CurrentActorTurnToken.IsValid && !lease.IsActive && session.TransactionArbiter.IsCommandInputOpen,
                            "Queued candidate never reached real Session publication.");
                        driver.Update(0);
                        Check(actorEvents == 1 && session.TransactionState.BattleRevision == revision,
                            "A later frame replayed TurnStart or published twice.");
                        return;
                    }
                    module.Tick();
                    for (int i = 0; i < 3; i++)
                    {
                        Check(session.CurrentActor == null && !session.CurrentActorTurnToken.IsValid && !session.TransactionArbiter.IsCommandInputOpen,
                            "Real Session published during entry presentation.");
                        port.Complete(); clock.Now += .451; clock.RenderedFrame++; module.Tick();
                    }
                    Check(source.TryFinalize(module, identities, out var completion, out error), error);
                    int events = 0; bool sawOpen = false; bool nestedAccepted = false; bool otherSkillAccepted = false;
                    session.CurrentActorChanged += actor => {
                        events++; sawOpen = session.TransactionArbiter.IsCommandInputOpen;
                        nestedAccepted = PublishSettled(session, completion, lease);
                        otherSkillAccepted = session.TryBeginTargetSelection(extraSkill, out _);
                    };
                    Check(PublishSettled(session, completion, lease), "Settled candidate has no working real Session publication path.");
                    Check(session.CurrentActor == reservation.Candidate.Participant && session.CurrentActorTurnToken == completion.Turn &&
                        session.Phase == DRPGBattlePhase.AwaitingCommand && session.TransactionArbiter.IsCommandInputOpen &&
                        events == 1 && !sawOpen && !nestedAccepted && !otherSkillAccepted, "Actor/token publication leaked input, duplicated, or remained disconnected.");
                    Check(!PublishSettled(session, completion, lease) && events == 1, "Late delivery published twice.");
                    Check(session.TryBeginTargetSelection(extraSkill, out error), "The legal skill remained blocked after terminal delivery: " + error);
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(basicSkill); UnityEngine.Object.DestroyImmediate(extraSkill); }
    }

    private static bool PublishSettled(DRPGBattleSession session, DRPGTurnEntrySettlementCompletion completion, BattleRuleMutationLease lease)
    {
        var method = typeof(DRPGBattleSession).GetMethod("TryPublishSettledTurnEntry",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return method != null && (bool)method.Invoke(session, new object[] { completion, lease, null });
    }

    // Break caught: candidate finalization is available before serial status/departure/formation have rendered.
    private static void SerialItemsThenBothFinalGatesKeepTheCandidateLocked()
    {
        using (var f = new Fixture(true))
        {
            Check(f.Module.Ledger == null && f.Domain.Core.Scheduler.CurrentTime == 0, "Construction started TurnEntry inline.");
            f.Step(0); Check(f.Module.Ledger != null && f.Port.Receipts.Count == 1 && f.Domain.Player.CurrentHp == 93, "First pump did not commit and issue one item.");
            f.Port.Complete(); f.Step(.449); Check(f.Port.Receipts.Count == 1, "Item dwell skipped.");
            f.Step(.002); Check(f.Port.Receipts.Count == 2 && !f.Module.ReadyForFinalization, "Second status did not remain serial.");
            f.Port.Complete(); f.Step(.451); Check(f.Port.Departure != null && !f.Module.ReadyForFinalization, "Departure was skipped.");
            long revision = f.Domain.State.BattleRevision;
            f.Step(.2); Check(f.Domain.State.BattleRevision == revision, "Rules reconciled before departure terminal.");
            f.Port.Complete(); f.Step(0); Check(f.Module.Reconciliation != null && !f.Module.ReadyForFinalization, "Formation receipt did not hold finalization.");
            f.Port.Complete(); f.Step(.301);
            Check(f.Module.ReadyForFinalization && f.Module.CandidateStillEligible && f.Domain.Lease.IsActive &&
                f.Domain.Core.Scheduler.CurrentTime == 10 && f.Domain.Core.Scheduler.Count == 2, "Module consumed actor/input/time before outer finalization.");
            revision = f.Domain.State.BattleRevision; f.Step(10);
            Check(f.Domain.State.BattleRevision == revision, "Completed entry replayed rules.");
        }
    }

    // Break caught: rule-dead candidate disappears/advances before shared rendered departure and formation.
    private static void LethalEntryUsesSharedDepartureAndCannotBecomeAnActor()
    {
        using (var f = new Fixture(true, true))
        {
            f.Step(0); Check(f.Domain.Player.CurrentHp == 0 && f.Domain.Core.Scheduler.Count == 2, "Death missing or timeline consumed too soon.");
            f.Port.Complete(); f.Step(.451);
            Check(f.Port.Departure != null && f.Domain.Core.Scheduler.Count == 2 && !f.Module.ReadyForFinalization, "Missing pending death gate.");
            f.Port.Departure.RecordView(new DRPGDepartureViewFact(f.Domain.Entry.Actor, 101, 201));
            f.Port.Complete(); f.Step(0);
            Check(f.Domain.Core.Scheduler.Count == 1 && f.Module.Reconciliation != null && !f.Module.ReadyForFinalization, "Shared death reconcile did not precede visual gate.");
            f.Port.Complete(); f.Step(.301);
            Check(f.Module.ReadyForFinalization && !f.Module.CandidateStillEligible && f.Domain.Lease.IsActive,
                "Lethal candidate published eligibility or released input.");
        }
    }

    // Break caught: PostureBreak skip is treated as eligible or its lifecycle feedback receives no dwell.
    private static void SkipLifecycleHasItsOwnReadableReceipt()
    {
        using (var f = new Fixture(false, broken: true))
        {
            f.Step(0); f.Port.Complete(); f.Step(.301);
            Check(f.Port.Lifecycle != null && f.Port.Departure == null, "Skip lifecycle feedback was omitted.");
            f.Port.Complete(); f.Step(.449); Check(f.Port.Departure == null, "Skip lifecycle flashed without dwell.");
            f.Step(.002); f.Port.Complete(); f.Step(0); f.Port.Complete(); f.Step(.301);
            Check(f.Module.ReadyForFinalization && !f.Module.CandidateStillEligible && f.Domain.Player.CurrentPosture == 0,
                "Skipped candidate became command-ready.");
        }
    }

    // Break caught: absent explicit projection binding quietly commits rules using an immediate fallback.
    private static void MissingHomesRejectsBeforeRulesAndTime()
    {
        using (var f = new Fixture(false))
        {
            f.Port.HomesAvailable = false; long revision = f.Domain.State.BattleRevision; f.Step(0, false);
            Check(f.Module.Fault.Length != 0 && f.Module.Ledger == null && f.Domain.State.BattleRevision == revision &&
                f.Domain.Core.Scheduler.CurrentTime == 0 && f.Domain.Lease.IsActive, "Missing presentation mutated candidate state.");
        }
    }

    // Break caught: final-gate timeout is accepted as rendered proof, opening the candidate early.
    private static void FinalGateTimeoutCannotMasqueradeAsRenderedCompletion()
    {
        using (var f = new Fixture(false))
        {
            f.Step(0); f.Port.Complete(); f.Step(.301); f.Step(1.999);
            Check(f.Module.Fault.Length == 0, "Departure watchdog fired early.");
            f.Step(.002, false);
            Check(f.Module.Fault.Length != 0 && !f.Module.ReadyForFinalization && f.Domain.Lease.IsActive &&
                f.Port.Cancelled.Count == 1 && f.Port.Cancelled[0] == f.Port.Departure.Receipt, "Timeout leaked or finalized the wrong receipt.");
        }
    }

    // Break caught: a scope teardown inside final-gate issuance loses the newly returned owned receipt.
    private static void DisposalDuringFinalIssueCleansOnlyItsReturnedReceipt()
    {
        using (var f = new Fixture(false))
        {
            f.Step(0); f.Port.OnDeparture = f.Module.Dispose; f.Port.Complete(); f.Step(.301, false);
            Check(f.Port.Departure != null && f.Port.Cancelled.Count == 1 &&
                f.Port.Departure.Receipt.Completion.IsCompleted && f.Domain.Lease.IsActive && !f.Module.ReadyForFinalization,
                "Disposal leaked the final leaf or released the candidate.");
        }
    }

    // Break caught: scope validation itself mutates State after the revision check and the module commits on top of it.
    private static void ScopeCallbackCannotHideAnInterveningRevision()
    {
        using (var f = new Fixture(true))
        {
            int calls = 0;
            f.OnScope = () =>
            {
                if (++calls != 2) return;
                var draft = f.Domain.State.CreateWorkspace(); draft.For(f.Domain.Entry.Actor).SetHitPoints(99);
                Check(f.Domain.State.TryCommitJournal(f.Domain.Lease, draft).Succeeded, "Intervening fixture mutation failed.");
            };
            f.Step(0, false);
            Check(f.Module.Fault.Length != 0 && f.Module.Ledger == null && f.Domain.Player.CurrentHp == 99 &&
                f.Domain.Core.Scheduler.CurrentTime == 0, "Scope callback hid rule drift before atomic TurnEntry.");
        }
    }

    // Break caught: a malformed but owned receipt is rejected without cleaning its active leaf.
    private static void WrongStageOwnedLifecycleReceiptIsCleaned()
    {
        using (var f = new Fixture(false, broken: true))
        {
            f.Port.WrongLifecycleStage = true; f.Step(0); f.Port.Complete(); f.Step(.301, false);
            var owned = f.Port.Receipts[f.Port.Receipts.Count - 1];
            Check(f.Module.Fault.Length != 0 && f.Port.Cancelled.Count == 1 && f.Port.Cancelled[0] == owned &&
                owned.Completion.IsCompleted && f.Domain.Lease.IsActive, "Rejected owned receipt leaked its leaf.");
        }
    }

    // Break caught: timed-out lifecycle feedback is permanently stuck or late completion bypasses its cleanup.
    private static void LifecycleTimeoutCleansBeforeDegradingAndContinuesSameCandidate()
    {
        using (var f = new Fixture(false, broken: true))
        {
            f.Step(0); f.Port.Complete(); f.Step(.301);
            var receipt = f.Port.Receipts[f.Port.Receipts.Count - 1];
            f.Step(2.449); Check(f.Port.Cancelled.Count == 0 && f.Port.Departure == null, "Lifecycle watchdog fired early.");
            f.Step(.002);
            Check(f.Module.Degraded && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded &&
                f.Port.Cancelled.Count == 1 && f.Port.Departure != null && f.Domain.Lease.IsActive &&
                !receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "late")),
                "Lifecycle recovery lost the candidate owner or accepted a late terminal.");
        }
    }

    // Break caught: disposal during bounded causal rule collection still issues new presentation after the commit returns.
    private static void DisposalDuringRuleCollectionDoesNotIssueFutureLeaves()
    {
        using (var domain = new DRPGTurnEntrySettlementProbe.Fixture())
        {
            domain.Status(domain.Player, "entry", -3);
            var reservation = DRPGTurnEntryLifecycleProbe.Reserve(domain); domain.Begin(domain.Player);
            var clock = new DRPGSettlementPresentationProbe.FixtureClock(); var port = new Port(domain.Homes, clock);
            DRPGBattleTurnEntryModule module = null;
            domain.Capture(new DisposalSource(domain.Entry.Actor, () => module.Dispose()));
            using (module = new DRPGBattleTurnEntryModule(domain.State, domain.Lease, reservation, port, clock, () => true, domain.Continuation))
            {
                module.Tick();
                Check(module.Ledger != null && domain.Player.CurrentHp == 97 && port.Receipts.Count == 0 && domain.Lease.IsActive &&
                    !module.ReadyForFinalization, "A disposed candidate issued a future presentation leaf after rule collection.");
            }
        }
    }

    // Break caught: caller readiness skips serial/final render gates, or the
    // preflight consumes time/candidate instead of freezing terminal input.
    private static void FinalizationPreflightRequiresEveryRenderedGateAndIsPure()
    {
        using (var f = new Fixture(false, owned: true))
        {
            var source = f.Domain.State.TurnCandidates;
            Check(!source.TryPrepareFinalization(f.Module, out _, out _), "Unstarted module authorized finalization.");
            f.Step(0);
            Check(!source.TryPrepareFinalization(f.Module, out _, out _), "Status gate was skipped.");
            f.Port.Complete(); f.Step(.301);
            Check(!source.TryPrepareFinalization(f.Module, out _, out _), "Departure gate was skipped.");
            f.Port.Complete(); f.Step(0);
            f.Port.Complete(); f.Step(.299);
            Check(!source.TryPrepareFinalization(f.Module, out _, out _), "Formation dwell was skipped.");
            f.Step(.002); long revision = f.Domain.State.BattleRevision;
            Check(source.TryPrepareFinalization(f.Module, out var facts, out var error), error);
            Check(source.TryPrepareFinalization(f.Module, out var replay, out error) && ReferenceEquals(facts, replay), error);
            Check(facts.Owner == f.Domain.Lease.Owner && facts.Reservation.Actor == f.Domain.Entry.Actor &&
                facts.Snapshot.Revision == revision && facts.Snapshot.CurrentLogicalTime == 10 &&
                facts.Lifecycle.CandidateAlive && !facts.Lifecycle.SkipAction && f.Domain.Core.Scheduler.Count == 2 &&
                source.HasReservation && f.Domain.Lease.IsActive && f.Domain.State.BattleRevision == revision,
                "Preflight changed candidate/time/ownership or froze the wrong State.");
        }
    }

    // Break caught: a rendered, still-eligible candidate remains reserved or
    // can be published twice instead of becoming the one current actor token.
    private static void EligibleTerminalConsumesOriginalCandidateAndPublishesExactlyOnce()
    {
        using (var f = new Fixture(false, owned: true))
        {
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(entry == f.Domain.Entry && identities.TryBeginTurnEntry(entry, f.Domain.Lease),
                "Fixture did not bind the exact candidate identity and physical lease.");
            FinishEmpty(f);

            var source = f.Domain.State.TurnCandidates;
            Check(source.TryFinalize(f.Module, identities, out var completion, out var error), error);
            Check(completion.Kind == DRPGTurnEntrySettlementCompletionKind.ActorStillEligible &&
                completion.Owner == f.Domain.Lease.Owner && completion.Turn.IsValid &&
                completion.Turn.Actor == f.Domain.Entry.Actor && f.Domain.Core.Scheduler.Count == 1 &&
                f.Domain.Core.Scheduler.CurrentTime == 10 && !source.HasReservation,
                "Eligible terminal did not atomically consume and publish the original candidate.");
            Check(!source.TryFinalize(f.Module, identities, out _, out _),
                "Eligible candidate finalized or published more than once.");
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Arbiter);
            Check(inbox.Submit(completion).Status == ContinuationSubmitStatus.Accepted && inbox.PendingCount == 1 &&
                f.Domain.Lease.IsActive, "Typed entry completion was not queued with its original lease.");
            Check(inbox.Submit(completion).Status == ContinuationSubmitStatus.Duplicate && inbox.PendingCount == 1,
                "Repeated entry completion created another continuation.");
            var receiver = new EntryContinuationObserver(f.Domain.Arbiter);
            var router = new DRPGBattleContinuationRouter(inbox, receiver, receiver);
            Check(receiver.Starts == 0 && router.Pump() && receiver.Starts == 1 &&
                ReferenceEquals(receiver.Lease, f.Domain.Lease) && !receiver.SawOpenInput && !router.Pump(),
                "Actor publication did not wait for its single owned continuation pump: " + router.Fault);
        }
    }

    // Break caught: the finalizer goes back to the old queue head and loses
    // the candidate identity after an entry effect has shifted its schedule.
    private static void FinalizationFreezesPostSettlementTimelineNotTheOldHead()
    {
        var skill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            using (var f = new Fixture(true, owned: true, singleStatus: true))
            {
                typeof(DRPGSkillSO).GetField("effects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(skill, new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.TimelineShift(DRPGActionEffectTarget.PrimaryTarget, 25) });
                var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(f.Domain.Enemy.RuntimeId, f.Domain.Player.RuntimeId, skill), f.Domain.Enemy);
                // The real TurnStart causal source changes this candidate to time 35.
                // Install before the module's atomic collection, not after its gate.
                f.Domain.Capture(new TimelineSource(new ParticipantId(f.Domain.Enemy.RuntimeId), spec));
                f.Module.Dispose();
                using (var module = new DRPGBattleTurnEntryModule(f.Domain.State, f.Domain.Lease, f.Reservation,
                    f.Port, f.Clock, () => true, f.Domain.Continuation))
                {
                    module.Tick(); f.Port.Complete(); f.Clock.Now = .451; f.Clock.RenderedFrame++; module.Tick();
                    f.Port.Complete(); f.Clock.Now += .451; f.Clock.RenderedFrame++; module.Tick();
                    f.Port.Complete(); f.Clock.RenderedFrame++; module.Tick();
                    f.Port.Complete(); f.Clock.Now += .301; f.Clock.RenderedFrame++; module.Tick();
                    Check(f.Domain.State.TurnCandidates.TryPrepareFinalization(module, out var facts, out var error), error);
                    float candidateTime = -1;
                    foreach (var row in facts.Snapshot.Timeline) if (row.Participant == f.Domain.Entry.Actor) candidateTime = row.ScheduledTime;
                    Check(facts.Reservation.ScheduledTime == 10 && candidateTime == 35 && facts.Snapshot.CurrentLogicalTime == 10 &&
                        f.Domain.Core.Scheduler.PeekNextEntry().Value.Participant == f.Domain.Enemy,
                        "Terminal preflight retargeted the new queue head or lost the causal timeline shift.");
                    var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
                    var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
                    Check(identities.TryBeginTurnEntry(entry, f.Domain.Lease), "Candidate identity was not bound.");
                    long revision = f.Domain.State.BattleRevision;
                    Check(f.Domain.State.TurnCandidates.TryFinalize(module, identities, out var completed, out error), error);
                    Check(completed.Turn.Actor == f.Domain.Entry.Actor && f.Domain.Core.Scheduler.CurrentTime == 10 &&
                        f.Domain.Core.Scheduler.Count == 1 && f.Domain.Core.Scheduler.PeekNextEntry().Value.Participant == f.Domain.Enemy &&
                        f.Domain.Core.Scheduler.PeekNextEntry().Value.ScheduledTime == 20 && f.Domain.State.BattleRevision == revision + 1,
                        "Terminal consumed the new head, advanced logical time, or bypassed the revision journal.");
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); }
    }

    private static void FinalizationRejectsUnreservedAndForeignModules()
    {
        using (var f = new Fixture(false))
        using (var other = new Fixture(false, owned: true))
        {
            FinishEmpty(f); FinishEmpty(other);
            Check(!f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out _), "Manual candidate became formal authority.");
            Check(!other.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out _), "Foreign module authorized a candidate.");
            Check(other.Domain.State.TurnCandidates.TryPrepareFinalization(other.Module, out _, out var error), error);
        }
    }

    private static void FinalizationRejectsLateRevisionAndClosedScope()
    {
        using (var f = new Fixture(false, owned: true))
        {
            FinishEmpty(f);
            Check(f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out var error), error);
            var draft = f.Domain.State.CreateWorkspace(); draft.For(f.Domain.Entry.Actor).SetHitPoints(99);
            Check(f.Domain.State.TryCommitJournal(f.Domain.Lease, draft).Succeeded, "Late mutation fixture failed.");
            Check(!f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out _) && f.Domain.Lease.IsActive,
                "Cached readiness hid an intervening rule revision.");
        }
        using (var f = new Fixture(false, owned: true))
        {
            FinishEmpty(f);
            Check(f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out var error), error);
            f.OnScope = f.Module.Dispose;
            Check(!f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out _) && f.Domain.Lease.IsActive,
                "Scope disposal during finalization still yielded a candidate.");
        }
    }

    private static void FinalizationRetainsSkipAndDeathWithoutPublishingAnActor()
    {
        using (var f = new Fixture(false, broken: true, owned: true))
        {
            f.Step(0); f.Port.Complete(); f.Step(.301); f.Port.Complete(); f.Step(.451);
            f.Port.Complete(); f.Step(0); f.Port.Complete(); f.Step(.301);
            Check(f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out var facts, out var error), error);
            Check(facts.Lifecycle.SkipAction && facts.Lifecycle.CandidateAlive && f.Domain.Lease.IsActive,
                "Skipped candidate was confused with an eligible actor.");
        }
        using (var f = new Fixture(true, true, owned: true))
        {
            f.Step(0); f.Port.Complete(); f.Step(.451);
            f.Port.Departure.RecordView(new DRPGDepartureViewFact(f.Domain.Entry.Actor, 101, 201));
            f.Port.Complete(); f.Step(0); f.Port.Complete(); f.Step(.301);
            Check(f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out var facts, out var error), error);
            Check(!facts.Lifecycle.CandidateAlive && facts.Snapshot.Timeline.Count == 1 &&
                f.Domain.State.TurnCandidates.HasReservation && f.Domain.Lease.IsActive,
                "Dead candidate was consumed/published before outcome continuation.");
        }
    }

    // Break caught: a scope callback recursively prepares a second terminal
    // snapshot before the outer preparation has validated its candidate.
    private static void FinalizationScopeReentryCannotPrepareAnotherSnapshot()
    {
        using (var f = new Fixture(false, owned: true))
        {
            FinishEmpty(f);
            var source = f.Domain.State.TurnCandidates;
            bool inside = false, nestedSucceeded = false; int callbacks = 0;
            f.OnScope = () =>
            {
                callbacks++;
                if (inside) return;
                inside = true;
                nestedSucceeded = source.TryPrepareFinalization(f.Module, out _, out _);
                inside = false;
            };
            Check(source.TryPrepareFinalization(f.Module, out var facts, out var error), error);
            Check(!nestedSucceeded && callbacks == 1 && facts.Snapshot.Revision == f.Domain.State.BattleRevision &&
                f.Domain.Lease.IsActive && source.HasReservation, "Reentrant scope prepared another terminal or lost ownership.");
        }
    }

    // Break caught: cached terminal facts survive a physical lease replacement
    // or a presentation clock rollback after the original final frame.
    private static void FinalizationRejectsReacquiredLeaseAndClockRollback()
    {
        using (var f = new Fixture(false, owned: true))
        {
            FinishEmpty(f);
            Check(f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out var error), error);
            var owner = f.Domain.Lease.Owner; f.Domain.Lease.Dispose();
            Check(f.Domain.Arbiter.TryAcquire(owner, out var replacement, out error), error);
            using (replacement)
                Check(!f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out _) && replacement.IsActive,
                    "Reacquiring the same identity reused stale finalization facts or released the new owner.");
        }
        using (var f = new Fixture(false, owned: true))
        {
            FinishEmpty(f);
            Check(f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out var error), error);
            f.Clock.Now -= .1;
            Check(!f.Domain.State.TurnCandidates.TryPrepareFinalization(f.Module, out _, out _) &&
                f.Module.Fault.Length != 0 && f.Domain.Lease.IsActive, "Finalization ignored a rolled-back presentation clock.");
        }
    }

    private static void FinishEmpty(Fixture f)
    { f.Step(0); f.Port.Complete(); f.Step(.301); f.Port.Complete(); f.Step(0); f.Port.Complete(); f.Step(.301); }

    internal static void EligibleCompletionFreezesTheFinalConsumptionRevision()
    {
        using (var f = new Fixture(false, owned: true))
        {
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(identities.TryBeginTurnEntry(entry, f.Domain.Lease), "Identity not bound.");
            FinishEmpty(f);
            long before = f.Domain.State.BattleRevision;
            Check(f.Domain.State.TurnCandidates.TryFinalize(f.Module, identities, out var completion, out var error), error);
            Check(completion.FinalBattleRevision == before + 1 && completion.FinalBattleRevision == f.Domain.State.BattleRevision,
                "Eligible completion omitted its final scheduler consumption revision.");
            var draft = f.Domain.State.CreateWorkspace(); draft.For(f.Domain.Entry.Actor).SetHitPoints(1);
            Check(f.Domain.State.TryCommitJournal(f.Domain.Lease, draft).Succeeded, "Late mutation fixture failed.");
            Check(completion.FinalBattleRevision == before + 1, "Sealed completion changed with live State.");
        }
    }

    private static void TurnEntryCompletionCannotBorrowAReacquiredLease()
    {
        foreach (bool queuedBefore in new[] { false, true })
        using (var f = new Fixture(false, owned: true))
        {
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(identities.TryBeginTurnEntry(entry,f.Domain.Lease), "Identity not bound.");
            FinishEmpty(f);
            Check(f.Domain.State.TurnCandidates.TryFinalize(f.Module,identities,out var completion,out var error),error);
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Arbiter);
            Check(!inbox.Submit(default(DRPGTurnEntrySettlementCompletion)).IsAccepted, "Default completion entered inbox.");
            if (queuedBefore) Check(inbox.Submit(completion).IsAccepted, "Owned completion was rejected.");
            f.Domain.Lease.Dispose();
            Check(f.Domain.Arbiter.TryAcquire(completion.Owner,out var replacement,out error),error);
            using (replacement)
            {
                var receiver = new EntryContinuationObserver(f.Domain.Arbiter);
                var router = new DRPGBattleContinuationRouter(inbox,receiver,receiver);
                if (!queuedBefore) Check(!inbox.Submit(completion).IsAccepted, "Old completion borrowed replacement lease.");
                Check(!router.Pump() && receiver.Starts == 0 && replacement.IsActive &&
                    (queuedBefore ? router.Fault.Length > 0 : inbox.PendingCount == 0),
                    "Expired completion dispatched or released the replacement owner.");
            }
        }
    }

    private static void OrdinaryPlayerSkipReschedulesOnceAndPreservesPendingShift()
    {
        float baseline = 0;
        foreach (int delta in new[] { 0, 7 })
        using (var f = new Fixture(false, broken: true, owned: true))
        {
            var source = f.Domain.State.TurnCandidates;
            source.PendingTimelineDeltas[f.Domain.Player.RuntimeId] = delta;
            source.PendingTimelinePriorities[f.Domain.Player.RuntimeId] = 3;
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(identities.TryBeginTurnEntry(entry, f.Domain.Lease), "Identity not bound.");
            f.Step(0); f.Port.Complete(); f.Step(.301); f.Port.Complete(); f.Step(.451);
            f.Port.Complete(); f.Step(0); f.Port.Complete(); f.Step(.301);
            long revision = f.Domain.State.BattleRevision;
            Check(source.TryFinalize(f.Module, identities, out var result, out var error), error);
            float ready = -1; int priority = 0;
            Check(result.FinalBattleRevision == revision + 1,
                "Skip completion omitted the final rescheduling revision.");
            foreach (var row in f.Domain.Core.Scheduler.GetTimelineSnapshot())
                if (row.Participant == f.Domain.Player) { ready = row.ScheduledTime; priority = row.Priority; }
            if (delta == 0) baseline = ready;
            Check(ready > 10 && Math.Abs(ready - baseline - delta) < .001f && priority == 3 &&
                result.Kind == DRPGTurnEntrySettlementCompletionKind.SelectAgain && !result.Turn.IsValid &&
                f.Domain.State.BattleRevision == revision + 1 && f.Domain.Core.Scheduler.CurrentTime == 10 &&
                f.Domain.Core.Scheduler.Count == 2 && f.Domain.Lease.IsActive && !source.HasReservation &&
                !source.PendingTimelineDeltas.ContainsKey(f.Domain.Player.RuntimeId) &&
                !source.PendingTimelinePriorities.ContainsKey(f.Domain.Player.RuntimeId),
                "Ordinary skip lost delay/priority, changed time, or opened input.");
            Check(!source.TryFinalize(f.Module, identities, out _, out _) &&
                f.Domain.State.BattleRevision == revision + 1, "Skip was rescheduled twice.");
        }
    }

    private static void OrdinaryEnemySkipUsesTheSessionPlannerAndConsumesPendingEffects()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            var state = f.Session.TransactionState; var source = state.TurnCandidates;
            Check(f.Session.TransactionArbiter.TryAcquire(f.Owner, out var seedLease, out var error), error);
            using (seedLease)
            using (state.OpenCompatibilityMutationScope(seedLease)) f.Enemy.ReducePosture(f.Enemy.CurrentPosture);
            // This actor has no authored ordinary attack: existing AI must
            // select a scheduled Wait instead of inventing a fallback attack.
            var old = DRPGEnemyActionPlan.CreateWait(9001, f.Enemy.RuntimeId, 0, 0).MarkReady();
            source.Plans[f.Enemy.RuntimeId] = old;
            source.PendingTimelineDeltas[f.Enemy.RuntimeId] = 7;
            source.PendingTimelinePriorities[f.Enemy.RuntimeId] = 3;
            Check(source.TryReserve(out var reservation, out error), error);
            var identities = new DRPGBattleIdentityAuthority(f.Session.BattleGeneration);
            var entry = identities.AllocateTurnEntry(new ParticipantId(f.Enemy.RuntimeId));
            Check(f.Session.TransactionArbiter.TryAcquire(BattleTransactionIdentity.ForTurnEntry(entry), out var lease, out error), error);
            using (lease)
            {
                Check(identities.TryBeginTurnEntry(entry, lease), "Enemy identity not bound.");
                var clock = new DRPGSettlementPresentationProbe.FixtureClock();
                var port = new Port(new Dictionary<ParticipantId,int> {
                    {new ParticipantId(f.Player.RuntimeId),0},{entry.Actor,0}}, clock);
                using (var module = new DRPGBattleTurnEntryModule(state, lease, reservation, port, clock, () => true))
                {
                    module.Tick();
                    for (int i = 0; i < 4; i++) { port.Complete(); clock.Now += .451; clock.RenderedFrame++; module.Tick(); }
                    Check(module.ReadyForFinalization && module.Ledger.TurnEntryLifecycle.SkipAction, module.Fault);
                    float now = state.CaptureRuleSnapshot().CurrentLogicalTime;
                    Check(source.TryFinalize(module, identities, out var completed, out error), error);
                    Check(completed.Kind == DRPGTurnEntrySettlementCompletionKind.SelectAgain && !completed.Turn.IsValid &&
                        source.Plans.TryGetValue(f.Enemy.RuntimeId, out var next) && next.PlanId != old.PlanId &&
                        next.ActionKind == DRPGEnemyPlannedActionKind.Wait && next.State == DRPGEnemyPlanState.Scheduled &&
                        next.ReadyAtTime > now && !source.PendingTimelineDeltas.ContainsKey(f.Enemy.RuntimeId) &&
                        !source.PendingTimelinePriorities.ContainsKey(f.Enemy.RuntimeId) && lease.IsActive &&
                        f.Session.CurrentActor == f.Player, "Enemy skip lost ordinary Wait planning, pending effects, or actor ownership.");
                    int rows = 0;
                    foreach (var row in state.CaptureRuleSnapshot().Timeline)
                        if (row.Participant == entry.Actor) { rows++; Check(row.Priority == 3 && row.ScheduledTime == source.Plans[f.Enemy.RuntimeId].ReadyAtTime,
                            "Enemy schedule disagrees with the prepared plan."); }
                    Check(rows == 1, "Enemy skip created duplicate or missing schedule rows.");
                }
            }
        }
    }

    private static void DefeatTerminalPrecedesPublicationIncludingMutualAnnihilation()
    {
        foreach (bool enemyDefeated in new[] { false, true })
        using (var f = new Fixture(true, true, owned: true, enemyDefeated: enemyDefeated))
        {
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(identities.TryBeginTurnEntry(entry, f.Domain.Lease), "Identity not bound.");
            Check(!f.Domain.State.TurnCandidates.TryFinalize(f.Module, identities, out _, out _), "Defeat skipped render gates.");
            f.Step(0); f.Port.Complete(); f.Step(.451);
            f.Port.Departure.RecordView(new DRPGDepartureViewFact(f.Domain.Entry.Actor, 101, 201));
            f.Port.Complete(); f.Step(0); f.Port.Complete(); f.Step(.301);
            Check(f.Domain.State.TurnCandidates.TryFinalize(f.Module, identities, out var result, out var error), error);
            Check(result.Kind == DRPGTurnEntrySettlementCompletionKind.BattleResult && result.Outcome == DRPGBattleOutcome.Defeat &&
                !result.Turn.IsValid && !f.Domain.State.TurnCandidates.HasReservation && f.Domain.Lease.IsActive &&
                !identities.TryPublishTurnEntry(entry, out _) && !f.Domain.State.TurnCandidates.TryFinalize(f.Module, identities, out _, out _),
                "Defeat published an actor, released continuation ownership, replayed, or lost precedence.");
        }
    }

    private static void EmptyEnemyFieldCannotPublishWithoutWaveOutcomeContext()
    {
        using (var f = new Fixture(false, owned: true, enemyDefeated: true))
        {
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(identities.TryBeginTurnEntry(entry, f.Domain.Lease), "Identity not bound.");
            FinishEmpty(f); long revision = f.Domain.State.BattleRevision;
            Check(!f.Domain.State.TurnCandidates.TryFinalize(f.Module, identities, out var result, out _) &&
                !result.Turn.IsValid && f.Domain.State.TurnCandidates.HasReservation &&
                f.Domain.State.BattleRevision == revision && f.Domain.Lease.IsActive,
                "Empty battlefield incorrectly opened a turn before wave/outcome resolution.");
        }
    }

    private static void EnemyTerminalRetainsTheLockedActionAndTarget()
    {
        using (var f = new Fixture(false, owned: true, enemyCandidate: true))
        {
            var source = f.Domain.State.TurnCandidates;
            var before = source.Plans[f.Domain.Enemy.RuntimeId];
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(identities.TryBeginTurnEntry(entry, f.Domain.Lease), "Enemy identity not bound.");
            FinishEmpty(f);
            Check(source.TryFinalize(f.Module, identities, out var result, out var error), error);
            Check(result.Turn.Actor == new ParticipantId(f.Domain.Enemy.RuntimeId) &&
                source.Plans.TryGetValue(f.Domain.Enemy.RuntimeId, out var executing) &&
                executing.State == DRPGEnemyPlanState.Executing && executing.PlanId == before.PlanId &&
                executing.CommittedTargetRuntimeId == f.Domain.Player.RuntimeId && ReferenceEquals(executing.Skill, before.Skill),
                "Enemy terminal dropped or regenerated its already locked action/target.");
        }
    }

    private static void DeadCandidateWithSurvivingTeamDiscardsOnlyAfterDepartureAndFormation()
    {
        var status = UnityEngine.ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        try
        {
            foreach (var field in new Dictionary<string, object> { {"statusId", "candidate-lethal"},
                {"triggerTiming", DRPGStatusTriggerTiming.TurnStart}, {"turnStartHpDelta", -10000f}, {"durationTurns", 2} })
                typeof(DRPGStatusDefinitionSO).GetField(field.Key, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(status, field.Value);
            using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false, fullBattlefield: true))
            {
                var state = f.Session.TransactionState; var source = state.TurnCandidates;
                var actor = new ParticipantId(f.Enemy.RuntimeId);
                var waveIdentity = new WaveTransitionIdentity(f.Session.BattleGeneration, 1, 1);
                Check(f.Session.TransactionArbiter.TryAcquire(BattleTransactionIdentity.ForWave(waveIdentity), out var waveLease, out _),
                    "Cannot acquire fixture activation owner.");
                DRPGEnemyWaveDepartureLedger waveDepartures;
                DRPGEnemyWaveDepartureLedger newerWaveDepartures;
                using (waveLease)
                {
                    var deployedIds = new List<EnemyDeploymentId>();
                    var runtimeIds = new Dictionary<EnemyDeploymentId, ParticipantId>();
                    foreach (var member in state.CaptureRuleSnapshot().Participants)
                        if (member.Side == DRPGBattleSide.Enemy && member.IsDeployed)
                        {
                            var deployment = new EnemyDeploymentId("turn-entry-wave", deployedIds.Count);
                            deployedIds.Add(deployment); runtimeIds.Add(deployment, member.Participant);
                        }
                    Check(deployedIds.Count > 1, "Fixture needs surviving wave peers.");
                    var activation = new WaveTransitionCompletion(waveIdentity, deployedIds, state.BattleRevision, waveLease);
                    waveDepartures = new DRPGEnemyWaveDepartureLedger(activation, runtimeIds);
                    var newerActivation = new WaveTransitionCompletion(waveIdentity, deployedIds, state.BattleRevision + 1000, waveLease);
                    newerWaveDepartures = new DRPGEnemyWaveDepartureLedger(newerActivation, runtimeIds);
                }
                Check(f.Session.TransactionArbiter.TryAcquire(f.Owner, out var seed, out var error), error);
                using (seed)
                {
                    var draft = state.CreateWorkspace(); draft.For(actor).ApplyStatus(DRPGResolvedStatusSpec.From(status),1,0,actor);
                    Check(state.TryCommitJournal(seed, draft).Succeeded, "Cannot seed actual TurnStart death.");
                }
                var homes = new Dictionary<ParticipantId,int>();
                int playerSlot = 0, enemySlot = 0;
                foreach (var row in state.CaptureRuleSnapshot().Participants) if (row.IsDeployed && row.CurrentHp > 0)
                    homes.Add(row.Participant, row.Side == DRPGBattleSide.Player ? playerSlot++ : enemySlot++);
                source.Plans[f.Enemy.RuntimeId] = DRPGEnemyActionPlan.CreateWait(9001,f.Enemy.RuntimeId,0,0).MarkReady();
                source.PendingTimelineDeltas[f.Enemy.RuntimeId] = 7;
                source.PendingTimelinePriorities[f.Enemy.RuntimeId] = 3;
                Check(source.TryReserve(out var reservation, out error), error);
                var identities = (DRPGBattleIdentityAuthority)typeof(DRPGBattleSession)
                    .GetField("transactionIdentityAuthority", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(f.Session);
                var entry = identities.AllocateTurnEntry(actor);
                Check(f.Session.TransactionArbiter.TryAcquire(BattleTransactionIdentity.ForTurnEntry(entry), out var lease, out error), error);
                using (lease)
                {
                    Check(identities.TryBeginTurnEntry(entry,lease), "Identity not bound.");
                    var clock = new DRPGSettlementPresentationProbe.FixtureClock(); var port = new Port(homes,clock);
                    using (var module = new DRPGBattleTurnEntryModule(state,lease,reservation,port,clock,()=>true))
                    {
                        module.Tick(); Check(port.Receipts.Count > 0, module.Fault); port.Complete(); clock.Now += .451; clock.RenderedFrame++; module.Tick();
                        Check(f.Enemy.CurrentHp == 0 && port.Departure != null && !source.TryFinalize(module,identities,out _,out _),
                            "Death gate: hp=" + f.Enemy.CurrentHp + "; departure=" + (port.Departure != null) + "; fault=" + module.Fault);
                        port.Departure.RecordView(new DRPGDepartureViewFact(actor,101,0));
                        port.Complete(); clock.RenderedFrame++; module.Tick();
                        Check(!source.TryFinalize(module,identities,out _,out _), "Death skipped formation barrier.");
                        port.Complete(); clock.Now += .301; clock.RenderedFrame++; module.Tick();
                        Check(module.ReadyForFinalization, "Dead candidate final gate: " + module.Fault);
                        Check(source.TryFinalize(module,identities,out var result,out error),error);
                        Check(result.FinalBattleRevision == state.BattleRevision, "Death completion lost its committed revision.");
                        var inbox = new DRPGBattleContinuationInbox(f.Session.TransactionArbiter);
                        Check(inbox.Submit(result).IsAccepted, "Actual death completion was not sealed.");
                        Check(inbox.TryTake(out var delivered), "Actual death completion was not queued.");
                        Check(waveDepartures.TryObserve(delivered, out error) && !waveDepartures.AllDeparted,
                            "TurnEntry departure did not reach its wave, or cleared living peers: " + error);
                        Check(waveDepartures.TryObserve(delivered, out _) && !waveDepartures.AllDeparted,
                            "Duplicate TurnEntry departure retired a peer.");
                        Check(!newerWaveDepartures.TryObserve(delivered, out _) && !newerWaveDepartures.AllDeparted,
                            "Stale TurnEntry departure cleared a newer activation.");
                        Check(result.Kind == DRPGTurnEntrySettlementCompletionKind.SelectAgain && !result.Turn.IsValid &&
                            result.Outcome == DRPGBattleOutcome.None && !source.HasReservation && lease.IsActive &&
                            !source.Plans.ContainsKey(actor.Value) && !source.PendingTimelineDeltas.ContainsKey(actor.Value) &&
                            !source.PendingTimelinePriorities.ContainsKey(actor.Value) && f.Session.CurrentActor == f.Player &&
                            !identities.TryPublishTurnEntry(entry,out _), "Dead candidate retained future work or opened input.");
                    }
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(status); }
    }

    private static void ForcedSkipRetainsUninterruptiblePlanWithoutPublishingTurn()
    {
        using (var f = new Fixture(false, broken: true, owned: true, enemyCandidate: true, uninterruptible: true))
        {
            var source = f.Domain.State.TurnCandidates;
            var before = source.Plans[f.Domain.Enemy.RuntimeId];
            var identities = new DRPGBattleIdentityAuthority(f.Domain.Core.BattleGeneration);
            var entry = identities.AllocateTurnEntry(f.Domain.Entry.Actor);
            Check(identities.TryBeginTurnEntry(entry, f.Domain.Lease), "Enemy identity not bound.");
            f.Step(0); f.Port.Complete(); f.Step(.301); f.Port.Complete(); f.Step(.451);
            f.Port.Complete(); f.Step(0); f.Port.Complete(); f.Step(.301);
            Check(f.Module.Ledger.TurnEntryLifecycle.SkipAction, "Fixture did not produce a forced skip.");
            Check(source.TryFinalize(f.Module, identities, out var result, out var error), error);
            Check(result.Kind == DRPGTurnEntrySettlementCompletionKind.SelectAgain && !result.Turn.IsValid &&
                f.Domain.Lease.IsActive && !source.HasReservation && !identities.TryPublishTurnEntry(entry, out _) &&
                source.Plans.TryGetValue(f.Domain.Enemy.RuntimeId, out var retry) && retry.State == DRPGEnemyPlanState.Executable &&
                retry.PlanId == before.PlanId && retry.CommittedTargetRuntimeId == before.CommittedTargetRuntimeId &&
                ReferenceEquals(retry.Skill, before.Skill) && retry.ReadyAtTime == before.ReadyAtTime &&
                f.Domain.Core.Scheduler.CurrentTime == 0 && f.Domain.Core.Scheduler.Count == 2,
                "Forced skip lost the locked enemy plan or opened input/rescheduled its time.");
            var inbox = new DRPGBattleContinuationInbox(f.Domain.Arbiter);
            var receiver = new EntryContinuationObserver(f.Domain.Arbiter);
            var router = new DRPGBattleContinuationRouter(inbox, receiver, receiver);
            Check(inbox.Submit(result).IsAccepted && receiver.Starts == 0 && router.Pump(), router.Fault);
            using (receiver.Lease)
                Check(receiver.Starts == 1 && !receiver.SawOpenInput && !f.Domain.Lease.IsActive &&
                    receiver.Lease.Owner != result.Owner && receiver.Lease.Owner.Kind == BattleTransactionKind.TurnEntry &&
                    inbox.Submit(result).Status == ContinuationSubmitStatus.Duplicate && !router.Pump(),
                    "Skipped candidate did not transfer atomically to exactly one subsequent entry.");
        }
    }

    private sealed class EntryContinuationObserver : IBattleStartContinuationRunner, IBattleTurnEntryContinuationRunner
    {
        private readonly DRPGBattleTransactionArbiter arbiter;
        internal EntryContinuationObserver(DRPGBattleTransactionArbiter arbiter) { this.arbiter = arbiter; }
        internal int Starts;
        internal bool SawOpenInput;
        internal BattleRuleMutationLease Lease;
        public bool TryPrepare(in DRPGTurnEntrySettlementCompletion completion, out BattleTransactionIdentity next, out string error)
        {
            next = completion.Kind == DRPGTurnEntrySettlementCompletionKind.SelectAgain
                ? BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(completion.Owner.BattleGeneration,
                    completion.Owner.TurnEntry.CandidateSerial + 1, completion.Owner.TurnEntry.Actor)) : completion.Owner;
            error = string.Empty; return true;
        }
        public void Start(in DRPGTurnEntrySettlementCompletion completion, BattleRuleMutationLease lease)
        { Starts++; Lease = lease; SawOpenInput = arbiter.IsCommandInputOpen; }
        public bool TryPrepare(in BattleStartCompletion completion, out BattleTransactionIdentity next, out string error)
        { throw new InvalidOperationException("TurnEntry was routed as startup."); }
        public void Start(in BattleStartCompletion completion, BattleRuleMutationLease lease)
        { throw new InvalidOperationException("TurnEntry was delivered as startup."); }
    }

    private sealed class TimelineSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        internal TimelineSource(ParticipantId actor, DRPGResolvedActionSpec spec) { Participant = actor; this.spec = spec; }
        public string SourceId => "entry-finalization-timeline";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth == 0) requests.Add(new DRPGImmediateReactionRequest(context.Owner, context.EventId, "delay-candidate",
                DRPGImmediateReactionCategory.OtherImmediate, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1)));
        }
    }

    private sealed class DisposalSource : IDRPGImmediateReactionSource
    {
        private readonly Action callback;
        internal DisposalSource(ParticipantId actor, Action callback) { Participant = actor; this.callback = callback; }
        public string SourceId => "entry-disposal";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests) => callback();
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DRPGTurnEntrySettlementProbe.Fixture Domain = new DRPGTurnEntrySettlementProbe.Fixture();
        internal readonly DRPGSettlementPresentationProbe.FixtureClock Clock = new DRPGSettlementPresentationProbe.FixtureClock();
        internal readonly Port Port;
        internal readonly DRPGBattleTurnEntryModule Module;
        internal readonly DRPGTurnCandidateReservation Reservation;
        internal Action OnScope;
        private UnityEngine.Object enemySkill;
        internal Fixture(bool status, bool lethal = false, bool broken = false, bool owned = false, bool singleStatus = false, bool enemyDefeated = false, bool enemyCandidate = false, bool uninterruptible = false)
        {
            if (enemyDefeated)
            {
                var draft = Domain.State.CreateWorkspace(); draft.For(new ParticipantId(Domain.Enemy.RuntimeId)).SetHitPoints(0);
                Check(Domain.State.TryCommitJournal(Domain.Core.Lease, draft).Succeeded, "Cannot seed enemy defeat.");
            }
            if (broken) using (Domain.State.OpenCompatibilityMutationScope(Domain.Core.Lease))
                (enemyCandidate ? Domain.Enemy : Domain.Player).ReducePosture(100);
            if (status) Domain.Status(Domain.Player, "a-entry", lethal ? -200 : -3);
            if (status && !lethal && !singleStatus) Domain.Status(Domain.Player, "z-entry", -4);
            if (enemyCandidate)
            {
                var skill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>(); enemySkill = skill;
                if (uninterruptible) typeof(DRPGSkillSO).GetField("postureBreakInterruptionPolicy",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(skill, DRPGPostureBreakInterruptionPolicy.Uninterruptible);
                Domain.State.TurnCandidates.Plans.Add(Domain.Enemy.RuntimeId,
                    DRPGEnemyActionPlan.CreateAttack(1, Domain.Enemy.RuntimeId, skill, 0, 50, 0)
                        .CommitTarget(Domain.Player.RuntimeId, 0).WithSatisfiedPlayerResponse(1));
            }
            var reservation = DRPGTurnEntryLifecycleProbe.Reserve(Domain);
            if (owned) Check(Domain.State.TurnCandidates.TryReserve(out reservation, out var error), error);
            Reservation = reservation; Domain.Begin(reservation.Candidate.Participant);
            var homes = Domain.Homes;
            if (enemyDefeated) homes.Remove(new ParticipantId(Domain.Enemy.RuntimeId));
            Port = new Port(homes, Clock);
            Module = new DRPGBattleTurnEntryModule(Domain.State, Domain.Lease, reservation, Port, Clock, () => { OnScope?.Invoke(); return true; });
        }
        internal void Step(double delta, bool check = true)
        {
            Clock.Now += delta; Clock.RenderedFrame++; Module.Tick();
            if (check)
            {
                Check(Module.Fault.Length == 0, Module.Fault);
                Check(Module.Ledger != null && Port.Receipts.Count > 0, "TurnEntry pump did not start its committed presentation.");
            }
        }
        public void Dispose() { Module.Dispose(); Domain.Dispose(); if (enemySkill != null) UnityEngine.Object.DestroyImmediate(enemySkill); }
    }

    // Only external render receipts are controlled. State/store/status/sequence,
    // lifecycle, clock and rule reconciliation are production components.
    private sealed class EntryFactory : IBattleTurnEntryPresentationFactory
    {
        private readonly DRPGSettlementPresentationProbe.FixtureClock clock;
        internal Port Latest;
        internal int Opened, Disposed;
        internal bool RejectOpen, ThrowOnClose, RejectPartial, InvalidPort;
        internal EntryFactory(Port first, DRPGSettlementPresentationProbe.FixtureClock clock) { Latest = first; this.clock = clock; }
        public bool TryOpen(BattleTransactionIdentity owner, out IBattleTurnEntryPresentationScope scope, out string error)
        {
            if (RejectOpen) { scope = null; error = "injected candidate open failure"; return false; }
            Opened++;
            Latest = new Port(new Dictionary<ParticipantId, int> { { new ParticipantId(8101), 0 }, { new ParticipantId(8102), 0 } }, clock);
            scope = new EntryScope(this, InvalidPort ? null : Latest);
            error = RejectPartial ? "injected partially created candidate" : string.Empty;
            return !RejectPartial;
        }
        private sealed class EntryScope : IBattleTurnEntryPresentationScope
        {
            private readonly EntryFactory factory;
            internal EntryScope(EntryFactory factory, Port port) { this.factory = factory; Port = port; }
            public IBattleTurnEntryPresentationPort Port { get; }
            public bool IsActive { get; private set; } = true;
            public void Dispose()
            {
                if (!IsActive) return;
                if (factory.ThrowOnClose) throw new InvalidOperationException("injected candidate close failure");
                IsActive = false; factory.Disposed++;
            }
        }
    }

    private sealed class Port : IBattleTurnEntryPresentationPort
    {
        private readonly IReadOnlyDictionary<ParticipantId, int> homes;
        internal Port(IReadOnlyDictionary<ParticipantId, int> homes, IDRPGBattlePresentationClock clock) { this.homes = homes; Clock = clock; }
        public IDRPGBattlePresentationClock Clock { get; }
        internal bool HomesAvailable = true;
        internal readonly List<PresentationReceipt> Receipts = new List<PresentationReceipt>();
        internal readonly List<PresentationReceipt> Cancelled = new List<PresentationReceipt>();
        internal DRPGBattleDeparturePresentation Departure;
        internal DRPGTurnEntryLifecycle Lifecycle;
        internal Action OnDeparture;
        internal bool WrongLifecycleStage;
        public bool TryCaptureHomes(BattleTransactionIdentity owner, out IReadOnlyDictionary<ParticipantId, int> value)
        { value = homes; return HomesAvailable; }
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command) => Add(command.Owner, command.StepOrdinal);
        public PresentationReceipt PresentLifecycle(DRPGBattleSettlementLedger ledger, int ordinal)
        {
            Lifecycle = ledger.TurnEntryLifecycle;
            if (!WrongLifecycleStage) return Add(ledger.Owner, ordinal);
            var r = new PresentationReceipt(ledger.Owner, DRPGBattleActionStage.Settlement, ordinal); Receipts.Add(r); return r;
        }
        public DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal facts, int ordinal)
        { Departure = new DRPGBattleDeparturePresentation(facts, ordinal); Receipts.Add(Departure.Receipt); OnDeparture?.Invoke(); return Departure; }
        public PresentationReceipt ReconcileFormation(DRPGBattleSettlementLedger ledger, DRPGBattlePostReturnResult rules, int ordinal) => Add(ledger.Owner, ordinal);
        private PresentationReceipt Add(BattleTransactionIdentity owner, int ordinal)
        { var r = new PresentationReceipt(owner, DRPGBattleActionStage.TurnBegin, ordinal); Receipts.Add(r); return r; }
        internal void Complete() => Receipts[Receipts.Count - 1].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "controlled render"));
        public void Tick() { }
        public void CancelReceipt(PresentationReceipt receipt, string reason) => Cancelled.Add(receipt);
    }
}
#endif
