#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGBattleBasicAttackNineStageProbe
{
    internal static void SettlementReceiptsAreSerial()
    {
        DRPGSettlementLedgerProbe.RunAll();
        DRPGSettlementReactionProbe.RunAll();
        DRPGSettlementSequenceProbe.RunAll();
        DRPGSettlementPresentationProbe.RunAll();
        DRPGBasicAttackRootSettlementProbe.RootAutomaticallyRunsBothSettlementLanes();
        DRPGBasicAttackRootSettlementProbe.EmptyRootSettlementStillWaitsBeforeFinalization();
        DRPGBasicAttackRootSettlementProbe.SettlementTracePhaseIdsAreUniqueAcrossRoots();
    }

    internal static void Stage9LethalLeasePair()
    {
        DRPGSettlementDefeatProbe.LethalCursorRetainsOriginalAcrossBothLanes();
        DRPGSettlementDefeatProbe.OldReturnCannotAuthorizeSettlementDeath();
        DRPGSettlementDefeatProbe.FinalizationWaitsForAllLanesAndOwnRenderedHome();
        DRPGSettlementDefeatProbe.HiddenHomeOrMissingIncapCannotReleaseAnyMember();
        DRPGSettlementDefeatProbe.SettlementReconcilesAllSurvivorsOnceAfterTypedDeparture();
        DRPGSettlementDefeatProbe.SettlementDefeatUsesLatestPublishedHome();
        DRPGSettlementDefeatProbe.ChildReactionDeathStaysInItsParentLaneUntilFinalization();
        DRPGSettlementDefeatProbe.FinalizationRejectsBackwardClockAndLateRelease();
        DRPGSettlementDefeatProbe.FormationTimeoutCannotBecomeDegradedCompletion();
        DRPGSettlementDefeatProbe.EmptyFinalizationHasExplicitTypedNoOpWithoutAdvancingRoot();
        DRPGSettlementDefeatProbe.IncapActivationLosingProjectionCannotLeakAModel();
        DRPGBasicAttackRootSettlementProbe.RootSettlementDeathsFinalizeOnlyAfterBothResults();
    }

    internal static void ProductionSettlementProjectionCursor()
    {
        DRPGBasicAttackSemanticOutcomeProbe.RunAll();
        DRPGSettlementProjectionProbe.RunAll();
        DRPGSettlementWidgetsProbe.SettlementCursorsUpdateAllThreeWidgetSurfaces();
        DRPGSettlementWidgetsProbe.ExpiredStatusesLeaveOnlyOnTheirOwnCursor();
        DRPGSettlementWidgetsProbe.DirectEnemyBindCannotEraseALeasedWidget();
        DRPGSettlementWidgetsProbe.PostureCursorUpdatesTheBoundWidgetsWithoutChangingTheirPose();
        DRPGSettlementWidgetsProbe.StatusInstancesStaySeparateAndPooledItemsAreCleared();
        DRPGWeaponCompletionProjectionProbe.WeaponClampWaitsForTheLastReactionOutcome();
        DRPGWeaponCompletionProjectionProbe.WeaponCompletionProjectsItsCommittedClampsBeforeReturn();
        DRPGWeaponCompletionProjectionProbe.StaleWeaponCompletionRejectsTheEntireOutcomeBeforeWriting();
    }

    internal static void RuleMutationIsolationAcrossLedgers()
    {
        DRPGSettlementLedgerProbe.RootOwnershipBlocksInterveningMutations();
        DRPGSettlementDefeatProbe.ForeignFactsAndUnrenderedReplayCannotMutateSettlement();
        DRPGBasicAttackRootSettlementProbe.MissingSettlementDepartureCannotCommitFinalFormation();
        DRPGBasicAttackRootSettlementProbe.HomeCaptureReentryCannotCommitSettlementAfterScopeLoss();
        DRPGBasicAttackRootSettlementProbe.ForeignSettlementReceiptCannotAdvanceOrCancelAnotherOwner();
        DRPGBasicAttackRootSettlementProbe.SettlementHomeSnapshotUsesSurvivorsPublishedSlotOrder();
    }

    internal static void StatusInstanceIdentityAndTiming() => DRPGStatusInstanceBoundaryProbe.RunAll();

    internal static void AllHeadlessFixturesUseExplicitPresentationComposition()
    {
        HeadlessFixtureConstructorAudit.RequireExplicitComposition();
        // Exercise the actual shared fixture, not a source-string assertion.
        WithSessionPair((first, second) =>
        {
            Require(first.TransactionPresentationPortSlot.TryGet(first.BattleGeneration, out var firstPort),
                "Headless fixture returned a Session without an explicit presentation port.");
            Require(second.TransactionPresentationPortSlot.TryGet(second.BattleGeneration, out var secondPort) &&
                    !ReferenceEquals(firstPort, secondPort),
                "Independent headless sessions must not share presentation ownership.");
            Require(!first.TransactionPresentationPortSlot.TryGet(second.BattleGeneration, out _),
                "A foreign generation obtained the fixture presentation port.");
            Require(first.CurrentActor == null && second.CurrentActor == null,
                "Headless composition must not secretly start a battle.");
            var identity = new ActionIdentity(first.BattleGeneration, 1, 1);
            var owner = BattleTransactionIdentity.ForRootAction(identity);
            long revision = first.TransactionState.BattleRevision;
            var receipts = new[]
            {
                firstPort.Announce(new AnnouncementCommand(identity)),
                firstPort.EnterConflict(new ConflictCompositionCommand(identity)),
                firstPort.PlayWindup(new BasicAttackWindupCommand(identity)),
                firstPort.PresentImpact(new ImpactPresentationCommand(identity)),
                firstPort.PresentReaction(new ReactionPresentationCommand(identity, 2)),
                firstPort.Return(new ReturnPresentationCommand(identity)),
                firstPort.PresentSettlement(new SettlementPresentationCommand(identity, 3))
            };
            var stages = new[] { DRPGBattleActionStage.Announcement, DRPGBattleActionStage.ConflictComposition,
                DRPGBattleActionStage.Windup, DRPGBattleActionStage.ImpactAndReactions, DRPGBattleActionStage.ImpactAndReactions,
                DRPGBattleActionStage.Return, DRPGBattleActionStage.Settlement };
            var trace = first.TransactionTrace;
            Require(trace.Count == 14, "Explicit test commands must produce one issued/terminal pair each.");
            for (int i = 0; i < receipts.Length; i++)
            {
                var receipt = receipts[i];
                int ordinal = i == 4 ? 2 : i == 6 ? 3 : 0;
                Require(receipt.Owner == owner && receipt.Stage == stages[i] && receipt.StepOrdinal == ordinal &&
                        receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                    "The headless command lost its typed identity, ordinal or terminal.");
                Require(trace[i * 2].EventKind == ActionTraceEventKind.ReceiptIssued &&
                        trace[i * 2 + 1].EventKind == ActionTraceEventKind.ReceiptCompleted &&
                        trace[i * 2].ReceiptId == receipt.ReceiptId && trace[i * 2 + 1].ReceiptId == receipt.ReceiptId &&
                        trace[i * 2 + 1].ParentEventId == trace[i * 2].EventId,
                    "Headless completion must follow its own issuance in the real Session trace.");
                Require(!receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "late")),
                    "A terminal headless receipt was completed twice.");
            }
            RequireThrows<InvalidOperationException>(() => firstPort.Announce(
                new AnnouncementCommand(new ActionIdentity(second.BattleGeneration, 1, 1))),
                "A headless port accepted a foreign battle.");
            Require(first.TransactionTrace.Count == 14 && first.TransactionState.BattleRevision == revision &&
                    first.CurrentActor == null && second.TransactionTrace.Count == 0,
                "Headless cues must not mutate rules, advance an actor or pollute another Session trace.");
            Require(first.TransactionPresentationPortSlot.TryUnbind(first.BattleGeneration, firstPort) == PresentationPortUnbindResult.Unbound,
                "The explicit test port could not be detached.");
            RequireThrows<InvalidOperationException>(() => firstPort.Announce(new AnnouncementCommand(identity)),
                "A captured unbound test port remained usable.");
        });
    }

    internal static void SchedulerQueriesArePure()
    {
        DRPGCombatantProfileSO firstProfile = CreateProfile(30f);
        DRPGCombatantProfileSO secondProfile = CreateProfile(10f);
        try
        {
            BattleParticipantRuntime first = CreateParticipant(
                9001,
                firstProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime second = CreateParticipant(
                9002,
                secondProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var scheduler = new DRPGTurnScheduler();
            scheduler.Initialize(new[] { first, second });

            IReadOnlyList<DRPGTurnScheduleEntry> before =
                scheduler.GetTimelineSnapshot();
            Require(before.Count == 2,
                "The scheduler fixture must begin with two entries.");
            float logicalTimeBefore = scheduler.CurrentTime;
            BattleParticipantRuntime scheduledFirst = before[0].Participant;
            float savedHp = scheduledFirst.CurrentHp;
            scheduledFirst.ApplyDamage(savedHp);
            Require(!scheduledFirst.IsAlive,
                "The scheduler fixture did not make the first entry rule-dead.");

            int countWhileDead = scheduler.Count;
            DRPGTurnScheduleEntry? peekWhileDead = scheduler.PeekNextEntry();
            IReadOnlyList<DRPGTurnScheduleEntry> snapshotWhileDead =
                scheduler.GetTimelineSnapshot();

            scheduledFirst.RestoreHp(savedHp);
            IReadOnlyList<DRPGTurnScheduleEntry> restored =
                scheduler.GetTimelineSnapshot();
            Require(countWhileDead == before.Count,
                "Count mutated the scheduler by deleting a rule-dead entry.");
            Require(peekWhileDead.HasValue &&
                    ReferenceEquals(
                        peekWhileDead.Value.Participant,
                        scheduledFirst),
                "PeekNextEntry mutated or skipped the scheduled rule-dead entry.");
            Require(snapshotWhileDead.Count == before.Count &&
                    ReferenceEquals(
                        snapshotWhileDead[0].Participant,
                        scheduledFirst),
                "GetTimelineSnapshot mutated or filtered the scheduler.");
            Require(restored.Count == before.Count &&
                    ReferenceEquals(restored[0].Participant, scheduledFirst),
                "Read-only scheduler queries deleted an entry that the fixture restored.");
            Require(Math.Abs(scheduler.CurrentTime - logicalTimeBefore) < 0.0001f,
                "Read-only scheduler queries advanced logical time.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstProfile);
            UnityEngine.Object.DestroyImmediate(secondProfile);
        }
    }

    internal static void CandidateReservationAdvancesLogicalTimeOnce()
    {
        DRPGCombatantProfileSO firstProfile = CreateProfile(30f);
        DRPGCombatantProfileSO secondProfile = CreateProfile(10f);
        try
        {
            BattleParticipantRuntime first = CreateParticipant(
                9011,
                firstProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime second = CreateParticipant(
                9012,
                secondProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var timeline = new DRPGTurnScheduler();
            timeline.ScheduleAt(first, 5f, 0);
            timeline.ScheduleAt(second, 6f, 0);
            var timelineSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { first, second },
                scheduler: timeline);

            Require(timelineSession.TryReserveNextTurnCandidate(
                    out DRPGTurnCandidateReservation firstReservation,
                    out string error),
                error);
            Require(firstReservation.SourceKind ==
                    DRPGTurnCandidateSourceKind.SchedulerTimeline &&
                    ReferenceEquals(firstReservation.Candidate.Participant, first),
                "The scheduler reservation did not freeze its first candidate.");
            Require(Math.Abs(timeline.CurrentTime) < 0.0001f &&
                    timeline.Count == 2,
                "Reserving a timeline candidate consumed it before commit.");
            Require(!timelineSession.TryReserveNextTurnCandidate(
                    out _,
                    out _),
                "A Session accepted a second active turn reservation.");

            var forged = new DRPGTurnCandidateReservation(
                firstReservation.ReservationId + 100L,
                firstReservation.SourceKind,
                firstReservation.Candidate,
                firstReservation.ReservedLogicalTime);
            Require(!TryCommitTurnCandidateWithTestHarnessLease(
                    timelineSession,
                    forged,
                    out _,
                    out _),
                "A forged reservation consumed the timeline candidate.");
            Require(Math.Abs(timeline.CurrentTime) < 0.0001f &&
                    timeline.Count == 2,
                "Rejecting a forged reservation changed logical time or entries.");
            Require(TryCommitTurnCandidateWithTestHarnessLease(
                    timelineSession,
                    firstReservation,
                    out DRPGEnemyActionPlan timelinePlan,
                    out error),
                error);
            Require(timelinePlan == null &&
                    Math.Abs(timeline.CurrentTime - 5f) < 0.0001f &&
                    timeline.Count == 1,
                "Committing a timeline reservation must consume one entry and " +
                "advance logical time exactly once.");
            Require(!TryCommitTurnCandidateWithTestHarnessLease(
                    timelineSession,
                    firstReservation,
                    out _,
                    out _) &&
                    Math.Abs(timeline.CurrentTime - 5f) < 0.0001f &&
                    timeline.Count == 1,
                "A duplicate timeline commit consumed or advanced twice.");

            Require(timelineSession.TryCommitTestHarnessMutation(
                    workspace => workspace
                        .For(new ParticipantId(first.RuntimeId))
                        .SetHitPoints(0f),
                    out BattleJournalCommitResult defeatCommit,
                    out error),
                error);
            Require(defeatCommit.Succeeded && !first.IsAlive,
                "The Session-owned test mutation did not defeat the committed actor.");
            Require(timelineSession.TryReserveNextTurnCandidate(
                    out DRPGTurnCandidateReservation secondReservation,
                    out error),
                error);
            Require(ReferenceEquals(
                    secondReservation.Candidate.Participant,
                    second) &&
                    Math.Abs(timeline.CurrentTime - 5f) < 0.0001f,
                "A TurnStart defeat did not preserve the previously committed " +
                "logical position for the next candidate.");
            Require(timelineSession.TryDiscardTurnCandidate(
                    secondReservation,
                    out error),
                error);
            Require(timeline.Count == 1 &&
                    Math.Abs(timeline.CurrentTime - 5f) < 0.0001f,
                "Discarding a reservation consumed an entry or changed time.");
            Require(!timelineSession.TryDiscardTurnCandidate(
                    secondReservation,
                    out _),
                "A duplicate discard was accepted.");
            Require(timelineSession.TryReserveNextTurnCandidate(
                    out DRPGTurnCandidateReservation retriedReservation,
                    out error),
                error);
            Require(ReferenceEquals(
                    retriedReservation.Candidate.Participant,
                    second),
                "Discard did not leave the same timeline candidate available.");
            Require(TryCommitTurnCandidateWithTestHarnessLease(
                    timelineSession,
                    retriedReservation,
                    out _,
                    out error),
                error);
            Require(Math.Abs(timeline.CurrentTime - 6f) < 0.0001f &&
                    timeline.Count == 0,
                "The retried timeline candidate was not consumed exactly once.");

            BattleParticipantRuntime planFirst = CreateParticipant(
                9013,
                firstProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime planSecond = CreateParticipant(
                9014,
                secondProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var planScheduler = new DRPGTurnScheduler();
            planScheduler.ScheduleAt(planSecond, 20f, 0);
            var planSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { planFirst, planSecond },
                scheduler: planScheduler);
            DRPGEnemyActionPlan executablePlan =
                DRPGEnemyActionPlan.CreateWait(
                    77L,
                    planSecond.RuntimeId,
                    0f,
                    0f)
                .MarkReady();
            SeedEnemyPlan(planSession, executablePlan);

            Require(planSession.TryReserveNextTurnCandidate(
                    out DRPGTurnCandidateReservation planReservation,
                    out error),
                error);
            Require(planReservation.SourceKind ==
                    DRPGTurnCandidateSourceKind.ExecutableEnemyPlan &&
                    ReferenceEquals(
                        planReservation.Candidate.Participant,
                        planSecond),
                "An executable enemy plan did not take reservation precedence.");
            Require(Math.Abs(planScheduler.CurrentTime) < 0.0001f &&
                    planScheduler.Count == 1,
                "Reserving an executable enemy plan changed scheduler time or entries.");
            Require(TryCommitTurnCandidateWithTestHarnessLease(
                    planSession,
                    planReservation,
                    out DRPGEnemyActionPlan committedPlan,
                    out error),
                error);
            Require(ReferenceEquals(committedPlan, executablePlan) &&
                    Math.Abs(planScheduler.CurrentTime) < 0.0001f &&
                    planScheduler.Count == 1,
                "Committing an executable plan must not advance scheduler time.");
            Require(!TryCommitTurnCandidateWithTestHarnessLease(
                    planSession,
                    planReservation,
                    out _,
                    out _),
                "A duplicate executable-plan commit was accepted.");

            Require(planSession.TryReserveNextTurnCandidate(
                    out DRPGTurnCandidateReservation afterPlanCommit,
                    out error),
                error);
            Require(afterPlanCommit.SourceKind ==
                    DRPGTurnCandidateSourceKind.SchedulerTimeline,
                "A committed executable plan remained available for a " +
                "second reservation.");
            Require(planSession.TryDiscardTurnCandidate(
                    afterPlanCommit,
                    out error),
                error);
            Require(!planSession.TryDiscardTurnCandidate(
                    afterPlanCommit,
                    out _) &&
                    Math.Abs(planScheduler.CurrentTime) < 0.0001f &&
                    planScheduler.Count == 1,
                "Timeline discard after an executable-plan commit was not " +
                "idempotent and side-effect free.");

            DRPGEnemyActionPlan discardablePlan =
                DRPGEnemyActionPlan.CreateWait(
                    78L,
                    planSecond.RuntimeId,
                    0f,
                    0f)
                .MarkReady();
            SeedEnemyPlan(planSession, discardablePlan);
            Require(planSession.TryReserveNextTurnCandidate(
                    out DRPGTurnCandidateReservation planDiscard,
                    out error),
                error);
            Require(planDiscard.SourceKind ==
                    DRPGTurnCandidateSourceKind.ExecutableEnemyPlan,
                "The discard fixture did not reserve its executable plan.");
            Require(planSession.TryDiscardTurnCandidate(planDiscard, out error),
                error);
            Require(planSession.TryReserveNextTurnCandidate(
                    out DRPGTurnCandidateReservation planRetry,
                    out error),
                error);
            Require(planRetry.SourceKind ==
                    DRPGTurnCandidateSourceKind.ExecutableEnemyPlan,
                "Discarding an executable plan consumed it instead of " +
                "leaving it available.");
            Require(TryCommitTurnCandidateWithTestHarnessLease(
                    planSession,
                    planRetry,
                    out DRPGEnemyActionPlan committedAfterDiscard,
                    out error),
                error);
            Require(ReferenceEquals(committedAfterDiscard, discardablePlan),
                "The retried executable-plan reservation changed identity.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstProfile);
            UnityEngine.Object.DestroyImmediate(secondProfile);
        }
    }

    internal static void WorkspaceRejectsExternalRevisionChange()
    {
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            DRPGBattleMutationWorkspace stale = fixture.CreateWorkspace();
            stale.For(new ParticipantId(fixture.Player.RuntimeId))
                .SetHitPoints(40f);

            DRPGBattleMutationWorkspace winner = fixture.CreateWorkspace();
            winner.SetResource("focus", 4);
            BattleJournalCommitResult first = fixture.State.TryCommitJournal(
                fixture.Lease,
                winner);
            Require(first.Succeeded && first.CommittedRevision == 1L,
                "The competing workspace did not advance battle revision once.");

            BattleJournalCommitResult rejected =
                fixture.State.TryCommitJournal(fixture.Lease, stale);
            Require(!rejected.Succeeded &&
                    rejected.CommittedRevision == 1L &&
                    rejected.Error.IndexOf(
                        "revision",
                        StringComparison.OrdinalIgnoreCase) >= 0,
                "A stale workspace was not rejected by revision.");
            Require(Math.Abs(
                    fixture.Player.CurrentHp -
                    fixture.Player.EffectiveMaxHp) < 0.0001f,
                "A rejected stale workspace changed participant HP.");
            Require(fixture.State.TryGetResource("focus", out int focus) &&
                    focus == 4 && fixture.State.BattleRevision == 1L,
                "A rejected stale workspace disturbed the winning commit.");
        }
    }

    internal static void FailedValidationLeavesEveryAggregateUntouched()
    {
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            DRPGResolvedStatusSpec status = fixture.CreateStatus(
                "atomic_failure_status");
            IReadOnlyList<DRPGTurnScheduleEntry> timelineBefore =
                fixture.Scheduler.GetTimelineSnapshot();
            float hpBefore = fixture.Player.CurrentHp;
            float mpBefore = fixture.Player.CurrentMp;
            float postureBefore = fixture.Player.CurrentPosture;
            float shieldBefore = fixture.Player.CurrentShield;
            DRPGFormationRow rowBefore = fixture.Player.FormationRow;
            int statusCountBefore = fixture.Player.Statuses.Count;
            Require(fixture.State.TryGetResource("focus", out int resourceBefore),
                "The fixture resource is missing.");

            DRPGBattleMutationWorkspace workspace = fixture.CreateWorkspace();
            workspace.For(new ParticipantId(fixture.Player.RuntimeId))
                .SetHitPoints(55f)
                .SetMana(11f)
                .SetPosture(33f)
                .SetShield(7f)
                .SetFormationRow(DRPGFormationRow.Rear)
                .ApplyStatus(status, 2, 3);
            workspace.For(new ParticipantId(fixture.Enemy.RuntimeId))
                .SetHitPoints(fixture.Enemy.EffectiveMaxHp + 1f);
            workspace.ScheduleAt(
                new ParticipantId(fixture.Player.RuntimeId),
                15f,
                -3);
            workspace.SetResource("focus", 9);

            BattleJournalCommitResult rejected =
                fixture.State.TryCommitJournal(fixture.Lease, workspace);
            Require(!rejected.Succeeded && fixture.State.BattleRevision == 0L,
                "An invalid journal was committed or advanced revision.");
            Require(Math.Abs(fixture.Player.CurrentHp - hpBefore) < 0.0001f &&
                    Math.Abs(fixture.Player.CurrentMp - mpBefore) < 0.0001f &&
                    Math.Abs(
                        fixture.Player.CurrentPosture - postureBefore) < 0.0001f &&
                    Math.Abs(
                        fixture.Player.CurrentShield - shieldBefore) < 0.0001f,
                "Failed validation partially changed participant resources.");
            Require(fixture.Player.FormationRow == rowBefore &&
                    fixture.Player.Statuses.Count == statusCountBefore,
                "Failed validation partially changed formation or statuses.");
            IReadOnlyList<DRPGTurnScheduleEntry> timelineAfter =
                fixture.Scheduler.GetTimelineSnapshot();
            Require(TimelineEquals(timelineBefore, timelineAfter),
                "Failed validation partially changed the scheduler timeline.");
            Require(fixture.State.TryGetResource("focus", out int resourceAfter) &&
                    resourceAfter == resourceBefore,
                "Failed validation partially changed battle resources.");
        }

        using (DRPGBattleStateMutationFixture defeatedFixture =
            DRPGBattleStateMutationFixture.Create(enemyStartsDefeated: true))
        {
            IReadOnlyList<DRPGTurnScheduleEntry> timelineBefore =
                defeatedFixture.Scheduler.GetTimelineSnapshot();
            DRPGBattleMutationWorkspace workspace =
                defeatedFixture.CreateWorkspace();
            workspace.ScheduleAt(
                new ParticipantId(defeatedFixture.Enemy.RuntimeId),
                25f,
                0);

            BattleJournalCommitResult rejected =
                defeatedFixture.State.TryCommitJournal(
                    defeatedFixture.Lease,
                    workspace);
            Require(!rejected.Succeeded &&
                    defeatedFixture.State.BattleRevision == 0L &&
                    TimelineEquals(
                        timelineBefore,
                        defeatedFixture.Scheduler.GetTimelineSnapshot()),
                "A timeline-only journal for an already defeated participant " +
                "was accepted or partially applied.");
        }
    }

    internal static void JournalAppliesAllPrimitiveWritesOnce()
    {
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            DRPGResolvedStatusSpec status = fixture.CreateStatus(
                "journal_apply_status",
                maxStacks: 4,
                durationTurns: 2);
            DRPGBattleMutationWorkspace workspace = fixture.CreateWorkspace();
            workspace.For(new ParticipantId(fixture.Player.RuntimeId))
                .SetHitPoints(61f)
                .SetMana(12f)
                .SetPosture(44f)
                .SetShield(8f)
                .SetFormationRow(DRPGFormationRow.Rear)
                .ApplyStatus(status, 2, 3);
            workspace.ScheduleAt(
                new ParticipantId(fixture.Player.RuntimeId),
                15f,
                -2);
            workspace.SetResource("focus", 9);

            BattleJournalCommitResult committed =
                fixture.State.TryCommitJournal(fixture.Lease, workspace);
            DRPGBattleMutationDirtySet expectedDirty =
                DRPGBattleMutationDirtySet.HitPoints |
                DRPGBattleMutationDirtySet.Mana |
                DRPGBattleMutationDirtySet.Posture |
                DRPGBattleMutationDirtySet.Shield |
                DRPGBattleMutationDirtySet.Status |
                DRPGBattleMutationDirtySet.Formation |
                DRPGBattleMutationDirtySet.Timeline |
                DRPGBattleMutationDirtySet.Resource;
            Require(committed.Succeeded &&
                    committed.CommittedRevision == 1L &&
                    (committed.DirtySet & expectedDirty) == expectedDirty,
                "The complete primitive journal was not committed once.");
            Require(Math.Abs(fixture.Player.CurrentHp - 61f) < 0.0001f &&
                    Math.Abs(fixture.Player.CurrentMp - 12f) < 0.0001f &&
                    Math.Abs(fixture.Player.CurrentPosture - 44f) < 0.0001f &&
                    Math.Abs(fixture.Player.CurrentShield - 8f) < 0.0001f,
                "The journal did not apply all participant resource values.");
            Require(fixture.Player.FormationRow == DRPGFormationRow.Rear &&
                    fixture.Player.Statuses.Contains(status.StatusId),
                "The journal did not apply formation and status writes.");
            IReadOnlyList<DRPGStatusRuntimeSnapshot> statusAfter =
                fixture.Player.Statuses.Snapshot();
            Require(statusAfter.Count == 1 &&
                    statusAfter[0].Stacks == 2 &&
                    statusAfter[0].RemainingTurns == 3,
                "The status write did not preserve its immutable values.");
            Require(fixture.State.TryGetResource("focus", out int focus) &&
                    focus == 9,
                "The journal did not apply its battle resource write.");
            DRPGTurnScheduleEntry playerEntry = FindTimelineEntry(
                fixture.Scheduler.GetTimelineSnapshot(),
                fixture.Player.RuntimeId);
            Require(Math.Abs(playerEntry.ScheduledTime - 15f) < 0.0001f &&
                    playerEntry.Priority == -2,
                "The journal did not apply its timeline write.");

            BattleJournalCommitResult duplicate =
                fixture.State.TryCommitJournal(fixture.Lease, workspace);
            Require(!duplicate.Succeeded &&
                    fixture.State.BattleRevision == 1L,
                "The same journal workspace committed more than once.");
            IReadOnlyList<DRPGStatusRuntimeSnapshot> statusAfterDuplicate =
                fixture.Player.Statuses.Snapshot();
            DRPGTurnScheduleEntry playerAfterDuplicate = FindTimelineEntry(
                fixture.Scheduler.GetTimelineSnapshot(),
                fixture.Player.RuntimeId);
            Require(fixture.State.TryGetResource(
                    "focus",
                    out int focusAfterDuplicate),
                "The resource disappeared after a duplicate commit.");
            Require(statusAfterDuplicate.Count == 1 &&
                    statusAfterDuplicate[0].Stacks == 2 &&
                    Math.Abs(playerAfterDuplicate.ScheduledTime - 15f) < 0.0001f &&
                    playerAfterDuplicate.Priority == -2 &&
                    focusAfterDuplicate == 9,
                "A duplicate commit repeated a primitive mutation.");
        }
    }

    internal static void ActiveParticipantMutationRequiresStateCapability()
    {
        RequireStatePortHasNoSessionFacadeDependency();
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
            SetField(attack, "skillId", "workspace_resolver_attack");
            SetField(attack, "power", 12f);
            SetField(
                attack,
                "effects",
                new List<DRPGActionEffectDefinition>
                {
                    DRPGActionEffectDefinition.Damage(
                        DRPGActionEffectTarget.PrimaryTarget,
                        1f,
                        DRPGDamageTag.Slash)
                });
            DRPGResolvedStatusSpec status = fixture.CreateStatus(
                "capability_status");
            float hpBefore = fixture.Player.CurrentHp;
            DRPGFormationRow rowBefore = fixture.Player.FormationRow;
            DRPGPostureBreakState postureBreakBefore =
                fixture.Player.PostureBreak.State;
            try
            {
                RequireThrows<InvalidOperationException>(
                    () => fixture.Player.ApplyDamage(5f),
                    "An attached participant accepted direct HP mutation.");
                RequireThrows<InvalidOperationException>(
                    () => fixture.Player.TrySetFormationRow(DRPGFormationRow.Rear),
                    "An attached participant accepted direct formation mutation.");
                RequireThrows<InvalidOperationException>(
                    () => fixture.Player.Statuses.Apply(status),
                    "An attached status collection accepted direct mutation.");
                RequireThrows<InvalidOperationException>(
                    () => fixture.Player.PostureBreak.Enter(),
                    "An attached posture-break runtime accepted direct mutation.");
                RequireThrows<InvalidOperationException>(
                    () => fixture.Player.ApplyCommittedRuleValues(
                        new DRPGBattleStateMutationCapability(
                            fixture.BattleGeneration),
                        1f,
                        fixture.Player.CurrentMp,
                        fixture.Player.CurrentPosture,
                        fixture.Player.CurrentShield,
                        fixture.Player.FormationRow,
                        fixture.Player.IsDeployed),
                    "A forged State capability mutated an attached participant.");

                var resolver = new DRPGActionResolver();
                DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                    fixture.Player.RuntimeId,
                    fixture.Enemy.RuntimeId,
                    attack);
                RequireThrows<InvalidOperationException>(
                    () => resolver.Resolve(command, fixture.Player, fixture.Enemy),
                    "The live Resolver overload bypassed the attached State.");
                Require(Math.Abs(fixture.Enemy.CurrentHp -
                                 fixture.Enemy.EffectiveMaxHp) < 0.0001f &&
                        Math.Abs(fixture.Enemy.CurrentPosture -
                                 fixture.Enemy.EffectiveMaxPosture) < 0.0001f &&
                        fixture.State.BattleRevision == 0L,
                    "A rejected live Resolver call changed HP, posture, or revision.");

                DRPGBattleMutationWorkspace resolverWorkspace =
                    fixture.CreateWorkspace();
                DRPGWorkspaceResolutionResult resolution =
                    resolver.ResolveIntoWorkspace(
                        DRPGActionSpecResolver.Resolve(command, fixture.Player),
                        fixture.Player,
                        fixture.Enemy,
                        new[] { fixture.Player, fixture.Enemy },
                        resolverWorkspace,
                        DRPGBattleImpactContext.ForStrike(
                            DRPGStrikeGrade.Body,
                            1f));
                Require(resolution.Status ==
                            DRPGWorkspaceResolutionStatus.Succeeded &&
                        resolution.Batch != null &&
                        resolution.Batch.Hits.Count == 1,
                    "The workspace Resolver did not stage one damage result.");
                Require(Math.Abs(fixture.Enemy.CurrentHp -
                                 fixture.Enemy.EffectiveMaxHp) < 0.0001f &&
                        fixture.State.BattleRevision == 0L,
                    "Workspace resolution mutated live state before commit.");

                using (DRPGBattleStateMutationFixture foreign =
                    DRPGBattleStateMutationFixture.Create())
                {
                    BattleJournalCommitResult wrongOwner =
                        fixture.State.TryCommitJournal(
                            foreign.Lease,
                            resolverWorkspace);
                    Require(!wrongOwner.Succeeded &&
                            Math.Abs(fixture.Enemy.CurrentHp -
                                     fixture.Enemy.EffectiveMaxHp) < 0.0001f &&
                            Math.Abs(fixture.Enemy.CurrentPosture -
                                     fixture.Enemy.EffectiveMaxPosture) < 0.0001f &&
                            fixture.State.BattleRevision == 0L,
                        "A foreign lease changed staged HP, posture, or revision.");
                }

                BattleJournalCommitResult resolverCommit =
                    fixture.State.TryCommitJournal(
                        fixture.Lease,
                        resolverWorkspace);
                Require(resolverCommit.Succeeded &&
                        fixture.State.BattleRevision == 1L &&
                        Math.Abs(fixture.Enemy.CurrentHp - 120f) < 0.0001f &&
                        Math.Abs(fixture.Enemy.CurrentPosture - 68f) < 0.0001f,
                    "The matching lease did not commit staged Resolver damage.");

                DRPGBattleMutationWorkspace workspace = fixture.CreateWorkspace();
                workspace.For(new ParticipantId(fixture.Player.RuntimeId))
                    .SetHitPoints(70f)
                    .SetFormationRow(DRPGFormationRow.Rear)
                    .ApplyStatus(status);
                BattleJournalCommitResult committed =
                    fixture.State.TryCommitJournal(fixture.Lease, workspace);
                Require(committed.Succeeded &&
                        fixture.State.BattleRevision == 2L &&
                        Math.Abs(fixture.Player.CurrentHp - 70f) < 0.0001f &&
                        fixture.Player.FormationRow == DRPGFormationRow.Rear &&
                        fixture.Player.Statuses.Contains(status.StatusId),
                    "The matching State capability did not apply the workspace.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(attack);
            }
        }


        using (DRPGBattleStateMutationFixture occupied =
            DRPGBattleStateMutationFixture.Create())
        {
            BattleParticipantRuntime fresh =
                occupied.CreateDetachedParticipant(9811);
            var freshScheduler = new DRPGTurnScheduler();
            freshScheduler.ScheduleAt(fresh, 4f, 0);
            long generation = occupied.BattleGeneration + 1L;
            var arbiter = new DRPGBattleTransactionArbiter(
                generation,
                new DRPGBattleActionTraceWriter(
                    new DRPGBoundedActionTraceSink(32)));

            RequireThrows<InvalidOperationException>(
                () => new DRPGBattleSessionState(
                    generation,
                    new[] { fresh, occupied.Player },
                    freshScheduler,
                    arbiter),
                "A State constructor accepted a participant owned by another State.");

            float freshHpBefore = fresh.CurrentHp;
            try
            {
                fresh.ApplyDamage(1f);
                freshScheduler.Clear();
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "A failed State construction partially attached an earlier " +
                    "participant or scheduler.");
            }
            Require(Math.Abs(fresh.CurrentHp - (freshHpBefore - 1f)) < 0.0001f,
                "A failed State construction did not preserve the earlier " +
                "participant's original mutation owner.");

            BattleParticipantRuntime schedulerFresh =
                occupied.CreateDetachedParticipant(9812);
            long schedulerGeneration = generation + 1L;
            var schedulerArbiter = new DRPGBattleTransactionArbiter(
                schedulerGeneration,
                new DRPGBattleActionTraceWriter(
                    new DRPGBoundedActionTraceSink(32)));
            RequireThrows<InvalidOperationException>(
                () => new DRPGBattleSessionState(
                    schedulerGeneration,
                    new[] { schedulerFresh },
                    occupied.Scheduler,
                    schedulerArbiter),
                "A State constructor accepted a scheduler owned by another State.");
            float schedulerFreshHpBefore = schedulerFresh.CurrentHp;
            try
            {
                schedulerFresh.ApplyDamage(1f);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "A scheduler attach failure partially attached its " +
                    "participant.");
            }
            Require(Math.Abs(
                    schedulerFresh.CurrentHp -
                    (schedulerFreshHpBefore - 1f)) < 0.0001f,
                "A scheduler attach failure changed participant ownership.");
        }
    }

    internal static void CallerOwnedSchedulerAliasCannotMutateBattle()
    {
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            DRPGTurnScheduler retainedAlias = fixture.Scheduler;
            IReadOnlyList<DRPGTurnScheduleEntry> before =
                retainedAlias.GetTimelineSnapshot();
            float timeBefore = retainedAlias.CurrentTime;

            RequireThrows<InvalidOperationException>(
                retainedAlias.Clear,
                "A caller-retained scheduler alias cleared an active timeline.");
            RequireThrows<InvalidOperationException>(
                () => retainedAlias.ScheduleAt(fixture.Player, 11f, 0),
                "A caller-retained scheduler alias scheduled into active state.");
            RequireThrows<InvalidOperationException>(
                () => retainedAlias.TakeNextEntry(),
                "A caller-retained scheduler alias consumed an active entry.");
            RequireThrows<InvalidOperationException>(
                () => retainedAlias.TryShiftParticipant(
                    fixture.Player.RuntimeId,
                    1f,
                    0,
                    out _,
                    out _),
                "A caller-retained scheduler alias shifted an active entry.");
            RequireThrows<InvalidOperationException>(
                () => retainedAlias.RemoveParticipant(
                    fixture.Player.RuntimeId),
                "A caller-retained scheduler alias removed an active entry.");
            Require(TimelineEquals(
                    before,
                    retainedAlias.GetTimelineSnapshot()) &&
                    Math.Abs(retainedAlias.CurrentTime - timeBefore) < 0.0001f &&
                    fixture.State.BattleRevision == 0L,
                "Rejected scheduler alias writes changed timeline or revision.");

            DRPGBattleMutationWorkspace workspace = fixture.CreateWorkspace();
            workspace.ScheduleAt(
                new ParticipantId(fixture.Player.RuntimeId),
                13f,
                -4);
            BattleJournalCommitResult committed =
                fixture.State.TryCommitJournal(fixture.Lease, workspace);
            DRPGTurnScheduleEntry applied = FindTimelineEntry(
                retainedAlias.GetTimelineSnapshot(),
                fixture.Player.RuntimeId);
            Require(committed.Succeeded &&
                    fixture.State.BattleRevision == 1L &&
                    Math.Abs(applied.ScheduledTime - 13f) < 0.0001f &&
                    applied.Priority == -4,
                "State capability did not apply the protected timeline mutation.");
        }
    }

    internal static void PrimaryImpactExactlyOnce()
    {
        RootActionCannotCommitASecondPrimaryOrdinal();
        PrimaryResourcesCannotCrossLegacyBoundary();
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            ActionIdentity identity = new ActionIdentity(
                fixture.BattleGeneration,
                1L,
                1L);
            using (BattleRuleMutationLease lease =
                AcquireRootActionLease(fixture, identity))
            {
                DRPGSkillSO skill = CreateWorkspaceDamageSkill(
                    "primary_impact_once",
                    18f,
                    5f,
                    7f);
                try
                {
                    var resolver = new DRPGActionResolver();
                    DRPGBattleMutationWorkspace workspace =
                        fixture.CreateWorkspace();
                    DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                        fixture.Player.RuntimeId,
                        fixture.Enemy.RuntimeId,
                        skill);
                    DRPGResolvedActionSpec spec =
                        DRPGActionSpecResolver.Resolve(command, fixture.Player);
                    DRPGBasicAttackFrozenSnapshot frozen =
                        CreatePrimaryImpactSnapshot(
                            fixture,
                            identity,
                            skill);
                    Require(DRPGActionResourceTransaction.TryReserve(
                            spec,
                            fixture.Player,
                            out DRPGActionResourceTransaction resources,
                            out string reserveError),
                        reserveError);
                    DRPGWorkspaceResolutionResult resolved =
                        resolver.ResolveIntoWorkspace(
                            spec,
                            fixture.Player,
                            fixture.Enemy,
                            new[] { fixture.Player, fixture.Enemy },
                            workspace,
                            DRPGBattleImpactContext.ForStrike(
                                DRPGStrikeGrade.Body,
                                1f),
                            DRPGHitOutcome.Hit);
                    var store = new DRPGBattleActionResolutionLedgerStore(
                        fixture.State,
                        fixture.Arbiter.Trace);

                    DRPGPrimaryImpactCommitResult first =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-primary-once",
                            frozen,
                            resolved,
                            workspace,
                            lease,
                            resources);
                    float hpAfterFirst = fixture.Enemy.CurrentHp;
                    float manaAfterFirst = fixture.Player.CurrentMp;
                    float postureAfterFirst = fixture.Player.CurrentPosture;
                    long revisionAfterFirst = fixture.State.BattleRevision;
                    DRPGBattleMutationWorkspace replayWorkspace =
                        fixture.CreateWorkspace();
                    DRPGPrimaryImpactCommitResult duplicate =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-primary-once",
                            frozen,
                            resolved,
                            replayWorkspace,
                            lease,
                            resources);

                    Require(first.Status ==
                                DRPGPrimaryImpactCommitStatus.Committed &&
                            duplicate.Status ==
                                DRPGPrimaryImpactCommitStatus.AlreadyCommitted,
                        "The primary impact did not expose committed and " +
                        "idempotent duplicate outcomes.");
                    Require(ReferenceEquals(first.Receipt, duplicate.Receipt) &&
                            first.Receipt != null &&
                            first.Receipt.Ledger.PrimaryImpact.ImpactOrdinal == 0,
                        "A duplicate primary impact did not return the original receipt.");
                    var sameJournalReplay = store.TryCommitPrimaryImpact(identity, 0,
                        "action-locked-primary-once", frozen, resolved, workspace,
                        lease, resources);
                    Require(sameJournalReplay.Status ==
                            DRPGPrimaryImpactCommitStatus.AlreadyCommitted &&
                        ReferenceEquals(first.Receipt, sameJournalReplay.Receipt),
                        "Replaying the complete original journal did not return its receipt.");
                    Require(Math.Abs(hpAfterFirst - 120f) < 0.0001f &&
                            Math.Abs(fixture.Enemy.CurrentPosture - 62f) < 0.0001f &&
                            Math.Abs(fixture.Enemy.CurrentHp - hpAfterFirst) < 0.0001f &&
                            Math.Abs(manaAfterFirst - 25f) < 0.0001f &&
                            Math.Abs(postureAfterFirst - 93f) < 0.0001f &&
                            Math.Abs(fixture.Player.CurrentMp - manaAfterFirst) < 0.0001f &&
                            Math.Abs(fixture.Player.CurrentPosture - postureAfterFirst) < 0.0001f &&
                            resources.State ==
                                DRPGActionResourceTransactionState.Committed &&
                            replayWorkspace.BuildValidatedJournal().DirtySet ==
                                DRPGBattleMutationDirtySet.None &&
                            revisionAfterFirst == 1L &&
                            fixture.State.BattleRevision == revisionAfterFirst,
                        "A duplicate primary impact repeated damage or revision mutation.");

                    lease.Dispose();
                    ActionIdentity otherIdentity = new ActionIdentity(
                        fixture.BattleGeneration,
                        2L,
                        2L);
                    using (BattleRuleMutationLease otherLease =
                        AcquireRootActionLease(fixture, otherIdentity))
                    {
                        DRPGBattleMutationWorkspace otherWorkspace =
                            fixture.CreateWorkspace();
                        DRPGWorkspaceResolutionResult otherResolution =
                            ResolvePrimaryImpact(
                                resolver,
                                fixture,
                                skill,
                                otherWorkspace,
                                DRPGHitOutcome.Hit);
                        DRPGBasicAttackFrozenSnapshot otherFrozen =
                            CreatePrimaryImpactSnapshot(
                                fixture,
                                otherIdentity,
                                skill);
                        DRPGPrimaryImpactCommitResult crossActionReuse =
                            store.TryCommitPrimaryImpact(
                                otherIdentity,
                                0,
                                "action-locked-resource-reuse",
                                otherFrozen,
                                otherResolution,
                                otherWorkspace,
                                otherLease,
                                resources);
                        Require(crossActionReuse.Status ==
                                    DRPGPrimaryImpactCommitStatus.Rejected &&
                                Math.Abs(fixture.Enemy.CurrentHp - hpAfterFirst) <
                                    0.0001f &&
                                fixture.State.BattleRevision == revisionAfterFirst,
                            "A committed resource reservation was reused by another action.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(skill);
                }
            }
        }
    }

    internal static void EvadeHasNoDamageOrHitCue()
    {
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            ActionIdentity identity = new ActionIdentity(
                fixture.BattleGeneration,
                1L,
                2L);
            using (BattleRuleMutationLease lease =
                AcquireRootActionLease(fixture, identity))
            {
                DRPGSkillSO skill = CreateWorkspaceDamageSkill(
                    "primary_impact_evaded",
                    99f);
                try
                {
                    float hpBefore = fixture.Enemy.CurrentHp;
                    var resolver = new DRPGActionResolver();
                    DRPGBattleMutationWorkspace workspace =
                        fixture.CreateWorkspace();
                    DRPGWorkspaceResolutionResult resolved =
                        ResolvePrimaryImpact(
                            resolver,
                            fixture,
                            skill,
                            workspace,
                            DRPGHitOutcome.Evaded);
                    DRPGBasicAttackFrozenSnapshot frozen =
                        CreatePrimaryImpactSnapshot(
                            fixture,
                            identity,
                            skill);
                    var store = new DRPGBattleActionResolutionLedgerStore(
                        fixture.State,
                        fixture.Arbiter.Trace);
                    DRPGPrimaryImpactCommitResult committed =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-evaded",
                            frozen,
                            resolved,
                            workspace,
                            lease);

                    DRPGPrimaryImpactEntry impact =
                        committed.Receipt != null
                            ? committed.Receipt.Ledger.PrimaryImpact
                            : null;
                    Require(committed.Status ==
                                DRPGPrimaryImpactCommitStatus.Committed &&
                            impact != null &&
                            impact.Outcome == DRPGHitOutcome.Evaded,
                        "An evaded primary impact did not commit one Evaded outcome.");
                    Require(Math.Abs(fixture.Enemy.CurrentHp - hpBefore) < 0.0001f &&
                            Math.Abs(impact.Hit.AppliedDamage) < 0.0001f &&
                            impact.DamageNumberCount == 0 &&
                            impact.GenericHitCueCount == 0 &&
                            !impact.RequiresRecoil &&
                            !impact.RequiresFlash &&
                            !impact.RequiresHitStop,
                        "Evaded produced damage or a generic hit feedback cue.");

                    lease.Dispose();
                    ActionIdentity forgedIdentity = new ActionIdentity(
                        fixture.BattleGeneration,
                        2L,
                        22L);
                    using (BattleRuleMutationLease forgedLease =
                        AcquireRootActionLease(fixture, forgedIdentity))
                    {
                        DRPGBattleMutationWorkspace forgedWorkspace =
                            fixture.CreateWorkspace();
                        forgedWorkspace.For(
                                new ParticipantId(fixture.Enemy.RuntimeId))
                            .SetShield(2f);
                        DRPGBattleHitResult validEvade = resolved.Batch.Hits[0];
                        var forgedHit = new DRPGBattleHitResult(
                            validEvade.ActorRuntimeId,
                            validEvade.TargetRuntimeId,
                            validEvade.SkillId,
                            validEvade.HpBefore,
                            0f,
                            validEvade.HpAfter,
                            true,
                            true,
                            validEvade.TargetRosterRole,
                            validEvade.RedirectReason,
                            validEvade.PostureBefore,
                            0f,
                            validEvade.PostureAfter,
                            validEvade.GuardGrade,
                            validEvade.StrikeGrade,
                            1f,
                            validEvade.DamageBreakdown,
                            validEvade.WasPostureBreakExposed,
                            DRPGHitOutcome.Evaded);
                        var forgedEffect = new DRPGActionEffectResult(
                            DRPGActionEffectKind.Damage,
                            DRPGActionEffectResultState.Applied,
                            validEvade.ActorRuntimeId,
                            validEvade.TargetRuntimeId,
                            0f,
                            0f,
                            1f,
                            validEvade.SkillId);
                        DRPGWorkspaceResolutionResult forgedResolution =
                            DRPGWorkspaceResolutionResult.Succeeded(
                                new DRPGResolvedActionBatch(
                                    Array.AsReadOnly(new[] { forgedHit }),
                                    Array.AsReadOnly(new[] { forgedEffect })));
                        float shieldBefore = fixture.Enemy.CurrentShield;
                        long revisionBefore = fixture.State.BattleRevision;
                        DRPGPrimaryImpactCommitResult forged =
                            store.TryCommitPrimaryImpact(
                                forgedIdentity,
                                0,
                                "action-locked-forged-evaded",
                                CreatePrimaryImpactSnapshot(
                                    fixture,
                                    forgedIdentity,
                                    skill),
                                forgedResolution,
                                forgedWorkspace,
                                forgedLease);
                        Require(forged.Status ==
                                    DRPGPrimaryImpactCommitStatus.Rejected &&
                                Math.Abs(fixture.Enemy.CurrentShield - shieldBefore) <
                                    0.0001f &&
                                fixture.State.BattleRevision == revisionBefore,
                            "A forged Evaded outcome carried shield, defeat, break, " +
                            "or effect mutations into live state.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(skill);
                }
            }
        }
    }

    internal static void LedgerFingerprintIdempotency()
    {
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            ActionIdentity identity = new ActionIdentity(
                fixture.BattleGeneration,
                1L,
                3L);
            using (BattleRuleMutationLease lease =
                AcquireRootActionLease(fixture, identity))
            {
                DRPGSkillSO firstSkill = CreateWorkspaceDamageSkill(
                    "primary_impact_fingerprint",
                    10f);
                DRPGSkillSO conflictingSkill = CreateWorkspaceDamageSkill(
                    "primary_impact_fingerprint",
                    25f);
                DRPGStatusDefinitionSO firstStatus = CreateFingerprintStatus(
                    "fingerprint_status",
                    1.10f);
                DRPGStatusDefinitionSO secondStatus = CreateFingerprintStatus(
                    "fingerprint_status",
                    1.35f);
                try
                {
                    var resolver = new DRPGActionResolver();
                    DRPGBattleMutationWorkspace firstWorkspace =
                        fixture.CreateWorkspace();
                    DRPGBattleMutationWorkspace guardConflictWorkspace =
                        fixture.CreateWorkspace();
                    DRPGBattleMutationWorkspace conflictWorkspace =
                        fixture.CreateWorkspace();
                    DRPGWorkspaceResolutionResult firstResolution =
                        ResolvePrimaryImpact(
                            resolver,
                            fixture,
                            firstSkill,
                            firstWorkspace,
                            DRPGHitOutcome.Hit);
                    DRPGWorkspaceResolutionResult sameDamageResolution =
                        ResolvePrimaryImpact(
                            resolver,
                            fixture,
                            firstSkill,
                            guardConflictWorkspace,
                            DRPGHitOutcome.Hit);
                    DRPGWorkspaceResolutionResult conflictResolution =
                        ResolvePrimaryImpact(
                            resolver,
                            fixture,
                            conflictingSkill,
                            conflictWorkspace,
                            DRPGHitOutcome.Hit);
                    DRPGBattleHitResult sameDamageHit =
                        sameDamageResolution.Batch.Hits[0];
                    var guardConflictHit = new DRPGBattleHitResult(
                        sameDamageHit.ActorRuntimeId,
                        sameDamageHit.TargetRuntimeId,
                        sameDamageHit.SkillId,
                        sameDamageHit.HpBefore,
                        sameDamageHit.AppliedDamage,
                        sameDamageHit.HpAfter,
                        sameDamageHit.WasDefeated,
                        sameDamageHit.TriggeredDamageBroken,
                        sameDamageHit.TargetRosterRole,
                        sameDamageHit.RedirectReason,
                        sameDamageHit.PostureBefore,
                        sameDamageHit.AppliedPostureDamage,
                        sameDamageHit.PostureAfter,
                        sameDamageHit.GuardGrade,
                        DRPGStrikeGrade.Weak,
                        sameDamageHit.ShieldAbsorbed,
                        sameDamageHit.DamageBreakdown,
                        sameDamageHit.WasPostureBreakExposed,
                        DRPGHitOutcome.Hit);
                    DRPGWorkspaceResolutionResult guardConflictResolution =
                        DRPGWorkspaceResolutionResult.Succeeded(
                            new DRPGResolvedActionBatch(
                                Array.AsReadOnly(new[] { guardConflictHit }),
                                sameDamageResolution.Batch.Effects));
                    var store = new DRPGBattleActionResolutionLedgerStore(
                        fixture.State,
                        fixture.Arbiter.Trace);
                    DRPGBasicAttackFrozenSnapshot frozen =
                        CreatePrimaryImpactSnapshot(
                            fixture,
                            identity,
                            firstSkill);

                    DRPGPrimaryImpactCommitResult first =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-fingerprint",
                            frozen,
                            firstResolution,
                            firstWorkspace,
                            lease);
                    float hpAfterFirst = fixture.Enemy.CurrentHp;
                    long revisionAfterFirst = fixture.State.BattleRevision;
                    DRPGPrimaryImpactCommitResult guardConflict =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-fingerprint",
                            frozen,
                            guardConflictResolution,
                            guardConflictWorkspace,
                            lease);
                    DRPGPrimaryImpactCommitResult conflict =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-fingerprint",
                            frozen,
                            conflictResolution,
                            conflictWorkspace,
                            lease);

                    Require(first.Status ==
                                DRPGPrimaryImpactCommitStatus.Committed &&
                            first.Receipt.Ledger.Fingerprint != 0UL,
                        "The first primary impact did not produce an immutable fingerprint.");
                    Require(conflict.Status ==
                                DRPGPrimaryImpactCommitStatus.FingerprintConflict &&
                            conflict.Receipt == null &&
                            conflict.Error.IndexOf(
                                "fingerprint",
                                StringComparison.OrdinalIgnoreCase) >= 0,
                        "The same impact key accepted a different ledger fingerprint.");
                    Require(guardConflict.Status ==
                                DRPGPrimaryImpactCommitStatus.FingerprintConflict,
                        "The same damage with a different strike grade aliased the " +
                        "committed primary-impact fingerprint.");

                    DRPGBattleMutationWorkspace firstStatusWorkspace =
                        fixture.CreateWorkspace();
                    DRPGBattleMutationWorkspace secondStatusWorkspace =
                        fixture.CreateWorkspace();
                    DRPGWorkspaceResolutionResult firstStatusResolution =
                        ResolvePrimaryImpact(
                            resolver,
                            fixture,
                            firstSkill,
                            firstStatusWorkspace,
                            DRPGHitOutcome.Hit);
                    DRPGWorkspaceResolutionResult secondStatusResolution =
                        ResolvePrimaryImpact(
                            resolver,
                            fixture,
                            firstSkill,
                            secondStatusWorkspace,
                            DRPGHitOutcome.Hit);
                    firstStatusWorkspace.For(
                            new ParticipantId(fixture.Enemy.RuntimeId))
                        .ApplyStatus(DRPGResolvedStatusSpec.From(firstStatus));
                    secondStatusWorkspace.For(
                            new ParticipantId(fixture.Enemy.RuntimeId))
                        .ApplyStatus(DRPGResolvedStatusSpec.From(secondStatus));
                    var key = new DRPGPrimaryImpactKey(identity, 9);
                    ulong firstStatusFingerprint =
                        DRPGActionResolutionFingerprint.ComputeLedger(
                            key,
                            "action-locked-status-fingerprint",
                            frozen,
                            firstStatusResolution.Batch.Hits[0],
                            firstStatusResolution.Batch.Effects,
                            firstStatusWorkspace.BuildValidatedJournal(),
                            null);
                    ulong secondStatusFingerprint =
                        DRPGActionResolutionFingerprint.ComputeLedger(
                            key,
                            "action-locked-status-fingerprint",
                            frozen,
                            secondStatusResolution.Batch.Hits[0],
                            secondStatusResolution.Batch.Effects,
                            secondStatusWorkspace.BuildValidatedJournal(),
                            null);
                    Require(firstStatusFingerprint != secondStatusFingerprint,
                        "Different immutable status payloads aliased one ledger fingerprint.");
                    Require(Math.Abs(fixture.Enemy.CurrentHp - hpAfterFirst) < 0.0001f &&
                            fixture.State.BattleRevision == revisionAfterFirst,
                        "A fingerprint conflict changed live state or revision.");
                    IReadOnlyList<ActionTraceEntry> trace =
                        fixture.Arbiter.Trace.Snapshot();
                    Require(ContainsTraceDetail(trace, "fingerprint conflict"),
                        "A primary-impact fingerprint conflict was not traced.");
                    var eventIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < trace.Count; i++)
                        Require(eventIds.Add(trace[i].EventId),
                            "Repeated fingerprint conflicts reused a trace event identity.");

                    guardConflictWorkspace.For(new ParticipantId(fixture.Enemy.RuntimeId))
                        .SetHitPoints(100f);
                    var journalConflict = store.TryCommitPrimaryImpact(identity, 0,
                        "action-locked-fingerprint", frozen, firstResolution,
                        guardConflictWorkspace, lease);
                    Require(journalConflict.Status ==
                            DRPGPrimaryImpactCommitStatus.FingerprintConflict &&
                        fixture.State.BattleRevision == revisionAfterFirst &&
                        Math.Abs(fixture.Enemy.CurrentHp - 120f) < 0.0001f,
                        "A replay with a different journal was accepted as the original ledger.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(firstSkill);
                    UnityEngine.Object.DestroyImmediate(conflictingSkill);
                    UnityEngine.Object.DestroyImmediate(firstStatus);
                    UnityEngine.Object.DestroyImmediate(secondStatus);
                }
            }
        }
    }

    internal static void AtomicResolutionFailureLeavesLiveStateUntouched()
    {
        ZeroCostImpactRejectsUnreservedActorMutation();
        PrimaryImpactRejectsDifferentFrozenPower();
        using (DRPGBattleStateMutationFixture fixture =
            DRPGBattleStateMutationFixture.Create())
        {
            ActionIdentity identity = new ActionIdentity(
                fixture.BattleGeneration,
                1L,
                4L);
            using (BattleRuleMutationLease lease =
                AcquireRootActionLease(fixture, identity))
            {
                DRPGSkillSO skill = CreateWorkspaceDamageSkill(
                    "primary_impact_atomic_failure",
                    20f,
                    5f,
                    7f);
                DRPGSkillSO wrongSkill = CreateWorkspaceDamageSkill(
                    "primary_impact_wrong_snapshot",
                    20f,
                    5f,
                    7f);
                try
                {
                    DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                        fixture.Player.RuntimeId,
                        fixture.Enemy.RuntimeId,
                        skill);
                    DRPGResolvedActionSpec spec =
                        DRPGActionSpecResolver.Resolve(command, fixture.Player);
                    DRPGBasicAttackFrozenSnapshot frozen =
                        CreatePrimaryImpactSnapshot(
                            fixture,
                            identity,
                            skill);
                    Require(DRPGActionResourceTransaction.TryReserve(
                            spec,
                            fixture.Player,
                            out DRPGActionResourceTransaction resources,
                            out string reserveError),
                        reserveError);
                    var resolver = new DRPGActionResolver();
                    DRPGBattleMutationWorkspace workspace =
                        fixture.CreateWorkspace();
                    DRPGWorkspaceResolutionResult resolved =
                        resolver.ResolveIntoWorkspace(
                            spec,
                            fixture.Player,
                            fixture.Enemy,
                            new[] { fixture.Player, fixture.Enemy },
                            workspace,
                            DRPGBattleImpactContext.ForStrike(
                                DRPGStrikeGrade.Body,
                                1f),
                            DRPGHitOutcome.Hit);
                    float hpBefore = fixture.Enemy.CurrentHp;
                    float manaBefore = fixture.Player.CurrentMp;
                    float postureBefore = fixture.Player.CurrentPosture;
                    long revisionBefore = fixture.State.BattleRevision;
                    var store = new DRPGBattleActionResolutionLedgerStore(
                        fixture.State,
                        fixture.Arbiter.Trace);
                    DRPGPrimaryImpactCommitResult wrongSnapshot =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-atomic-failure",
                            CreatePrimaryImpactSnapshot(
                                fixture,
                                identity,
                                wrongSkill),
                            resolved,
                            workspace,
                            lease,
                            resources);
                    Require(wrongSnapshot.Status ==
                                DRPGPrimaryImpactCommitStatus.Rejected &&
                            fixture.State.BattleRevision == revisionBefore &&
                            resources.State ==
                                DRPGActionResourceTransactionState.Reserved,
                        "A primary impact accepted a frozen snapshot for another skill.");
                    workspace.For(new ParticipantId(fixture.Enemy.RuntimeId))
                        .SetHitPoints(float.NaN);
                    DRPGPrimaryImpactCommitResult rejected =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-atomic-failure",
                            frozen,
                            resolved,
                            workspace,
                            lease,
                            resources);

                    Require(rejected.Status ==
                                DRPGPrimaryImpactCommitStatus.Rejected &&
                            rejected.Receipt == null &&
                            rejected.Error.IndexOf(
                                "HP mutation",
                                StringComparison.OrdinalIgnoreCase) >= 0,
                        "The primary-impact commit did not validate and reject " +
                        "the concrete invalid journal before live apply.");
                    Require(Math.Abs(fixture.Enemy.CurrentHp - hpBefore) < 0.0001f &&
                            Math.Abs(fixture.Player.CurrentMp - manaBefore) < 0.0001f &&
                            Math.Abs(fixture.Player.CurrentPosture - postureBefore) < 0.0001f &&
                            fixture.State.BattleRevision == revisionBefore &&
                            resources.State ==
                                DRPGActionResourceTransactionState.Reserved,
                        "A failed atomic resolution changed live HP, resources, " +
                        "revision, or reservation state.");
                    ParticipantMutationBuilder rolledBackActor = workspace.For(
                        new ParticipantId(fixture.Player.RuntimeId));
                    Require(Math.Abs(rolledBackActor.Mana - manaBefore) < 0.0001f &&
                            Math.Abs(rolledBackActor.Posture - postureBefore) < 0.0001f,
                        "A failed resource stage was not rolled back inside the workspace.");

                    workspace.For(new ParticipantId(fixture.Enemy.RuntimeId))
                        .SetHitPoints(resolved.Batch.Hits[0].HpAfter);
                    DRPGPrimaryImpactCommitResult repaired =
                        store.TryCommitPrimaryImpact(
                            identity,
                            0,
                            "action-locked-atomic-failure",
                            frozen,
                            resolved,
                            workspace,
                            lease,
                            resources);
                    Require(repaired.Status ==
                                DRPGPrimaryImpactCommitStatus.Committed &&
                            Math.Abs(fixture.Player.CurrentMp - (manaBefore - 5f)) <
                                0.0001f &&
                            Math.Abs(fixture.Player.CurrentPosture -
                                (postureBefore - 7f)) < 0.0001f,
                        "Repairing and retrying a failed impact deducted resources " +
                        "more than once.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(skill);
                    UnityEngine.Object.DestroyImmediate(wrongSkill);
                }
            }
        }
    }

    internal static void ZeroCostImpactRejectsUnreservedActorMutation()
    {
        using (var fixture = DRPGBattleStateMutationFixture.Create())
        {
            var identity = new ActionIdentity(fixture.BattleGeneration, 1L, 5L);
            using (var lease = AcquireRootActionLease(fixture, identity))
            {
                var skill = CreateWorkspaceDamageSkill("zero_cost_actor_write", 20f);
                try
                {
                    var spec = DRPGActionSpecResolver.Resolve(
                        DRPGBattleCommand.BasicAttack(fixture.Player.RuntimeId,
                            fixture.Enemy.RuntimeId, skill), fixture.Player);
                    Require(DRPGActionResourceTransaction.TryReserve(spec,
                        fixture.Player, out var resources, out string error), error);
                    var frozen = CreatePrimaryImpactSnapshot(fixture, identity, skill);
                    var workspace = fixture.CreateWorkspace();
                    var resolved = ResolvePrimaryImpact(new DRPGActionResolver(),
                        fixture, skill, workspace, DRPGHitOutcome.Hit);
                    float actorHp = fixture.Player.CurrentHp;
                    workspace.For(new ParticipantId(fixture.Player.RuntimeId))
                        .SetHitPoints(actorHp - 3f);
                    var store = new DRPGBattleActionResolutionLedgerStore(
                        fixture.State, fixture.Arbiter.Trace);
                    var rejected = store.TryCommitPrimaryImpact(identity, 0,
                        "locked-zero-cost", frozen, resolved, workspace, lease, resources);
                    Require(rejected.Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                        rejected.Receipt == null && fixture.State.BattleRevision == 0L &&
                        Math.Abs(fixture.Player.CurrentHp - actorHp) < 0.0001f &&
                        Math.Abs(fixture.Enemy.CurrentHp - 120f) < 0.0001f &&
                        resources.State == DRPGActionResourceTransactionState.Reserved,
                        "A zero-cost reservation allowed an unreserved actor HP write.");
                    var clean = fixture.CreateWorkspace();
                    var valid = ResolvePrimaryImpact(new DRPGActionResolver(),
                        fixture, skill, clean, DRPGHitOutcome.Hit);
                    var committed = store.TryCommitPrimaryImpact(identity, 0,
                        "locked-zero-cost", frozen, valid, clean, lease, resources);
                    Require(committed.Status == DRPGPrimaryImpactCommitStatus.Committed &&
                        fixture.State.BattleRevision == 1L &&
                        Math.Abs(fixture.Enemy.CurrentHp - 120f) < 0.0001f &&
                        Math.Abs(fixture.Enemy.CurrentPosture - 60f) < 0.0001f &&
                        Math.Abs(fixture.Player.CurrentHp - actorHp) < 0.0001f,
                        "Rejecting an unreserved actor write poisoned the valid zero-cost retry.");
                }
                finally { UnityEngine.Object.DestroyImmediate(skill); }
            }
        }
    }

    internal static void PrimaryResourcesCannotCrossLegacyBoundary()
    {
        using (var fixture = DRPGBattleStateMutationFixture.Create())
        {
            // Detached actor makes an illicit Legacy write observable without the
            // separate active-participant capability guard masking this boundary.
            var actor = fixture.CreateDetachedParticipant(9899);
            var skill = CreateWorkspaceDamageSkill("resource_owner", 20f, 5f);
            try
            {
                var spec = DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(actor.RuntimeId,
                        fixture.Enemy.RuntimeId, skill), actor);
                var key = new DRPGPrimaryImpactKey(
                    new ActionIdentity(fixture.BattleGeneration, 1L, 8L), 0);
                Require(DRPGActionResourceTransaction.TryReserve(spec, actor,
                    out var bound, out string error), error);
                Require(bound.TryBindPrimaryImpact(key, out error), error);
                Require(!bound.TryCommit(actor, out _, out _) &&
                    Math.Abs(actor.CurrentMp - 20f) < 0.0001f &&
                    bound.State == DRPGActionResourceTransactionState.Reserved,
                    "A primary-bound reservation was spent through the Legacy commit path.");
                Require(!bound.CanCommit(actor, out _),
                    "Legacy preflight accepted a primary-owned resource reservation.");

                Require(DRPGActionResourceTransaction.TryReserve(spec, actor,
                    out var legacy, out error), error);
                Require(legacy.TryCommit(actor, out _, out error), error);
                Require(!legacy.TryBindPrimaryImpact(key, out _) &&
                    Math.Abs(actor.CurrentMp - 15f) < 0.0001f,
                    "An already-spent Legacy reservation was rebound to a primary impact.");
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    internal static void RootActionCannotCommitASecondPrimaryOrdinal()
    {
        using (var fixture = DRPGBattleStateMutationFixture.Create())
        {
            var identity = new ActionIdentity(fixture.BattleGeneration, 1L, 7L);
            using (var lease = AcquireRootActionLease(fixture, identity))
            {
                var skill = CreateWorkspaceDamageSkill("one_root_primary", 20f);
                try
                {
                    var frozen = CreatePrimaryImpactSnapshot(fixture, identity, skill);
                    var store = new DRPGBattleActionResolutionLedgerStore(
                        fixture.State, fixture.Arbiter.Trace);
                    var firstWorkspace = fixture.CreateWorkspace();
                    var first = ResolvePrimaryImpact(new DRPGActionResolver(),
                        fixture, skill, firstWorkspace, DRPGHitOutcome.Hit);
                    Require(store.TryCommitPrimaryImpact(identity, 0, "locked-one-primary",
                        frozen, first, firstWorkspace, lease).Status ==
                        DRPGPrimaryImpactCommitStatus.Committed,
                        "The first root primary impact did not commit.");
                    var secondWorkspace = fixture.CreateWorkspace();
                    var second = ResolvePrimaryImpact(new DRPGActionResolver(),
                        fixture, skill, secondWorkspace, DRPGHitOutcome.Hit);
                    var rejected = store.TryCommitPrimaryImpact(identity, 1,
                        "locked-one-primary", frozen, second, secondWorkspace, lease);
                    Require(rejected.Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                        fixture.State.BattleRevision == 1L &&
                        Math.Abs(fixture.Enemy.CurrentHp - 120f) < 0.0001f &&
                        Math.Abs(fixture.Enemy.CurrentPosture - 60f) < 0.0001f,
                        "A second primary ordinal damaged the target twice in one root action.");
                }
                finally { UnityEngine.Object.DestroyImmediate(skill); }
            }
        }
    }

    internal static void PrimaryImpactRejectsDifferentFrozenPower()
    {
        using (var fixture = DRPGBattleStateMutationFixture.Create())
        {
            var identity = new ActionIdentity(fixture.BattleGeneration, 1L, 6L);
            using (var lease = AcquireRootActionLease(fixture, identity))
            {
                var skill = CreateWorkspaceDamageSkill("frozen_power", 20f);
                try
                {
                    var frozen = CreatePrimaryImpactSnapshot(fixture, identity, skill);
                    SetField(skill, "power", 60f);
                    var workspace = fixture.CreateWorkspace();
                    var resolved = ResolvePrimaryImpact(new DRPGActionResolver(),
                        fixture, skill, workspace, DRPGHitOutcome.Hit);
                    Require(resolved.Status == DRPGWorkspaceResolutionStatus.Succeeded &&
                        resolved.Batch.Hits[0].DamageBreakdown != null &&
                        Math.Abs(resolved.Batch.Hits[0].DamageBreakdown.RequestedDamage - 60f) < 0.0001f &&
                        Math.Abs(resolved.Batch.Hits[0].AppliedPostureDamage - 60f) < 0.0001f,
                        "The wrong-power fixture did not produce a valid 60-damage request.");
                    var store = new DRPGBattleActionResolutionLedgerStore(
                        fixture.State, fixture.Arbiter.Trace);
                    var rejected = store.TryCommitPrimaryImpact(identity, 0,
                        "locked-frozen-power", frozen, resolved, workspace, lease);
                    Require(rejected.Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                        rejected.Receipt == null && fixture.State.BattleRevision == 0L &&
                        Math.Abs(fixture.Enemy.CurrentHp - 120f) < 0.0001f,
                        "A valid same-skill result with different frozen power was committed.");
                }
                finally { UnityEngine.Object.DestroyImmediate(skill); }
            }
        }
    }

    internal static void BattleGenerationNeverAliasesAcrossSessions()
    {
        long firstPublicGeneration = 0L;
        long secondPublicGeneration = 0L;
        WithSessionPair((first, second) =>
        {
            Require(first.BattleGeneration > 0L,
                "A public Session must receive a positive domain battle generation.");
            Require(second.BattleGeneration > 0L,
                "Every public Session must receive a positive domain battle generation.");
            Require(first.BattleGeneration != second.BattleGeneration,
                "Two public Sessions must never alias the same battle generation.");
            firstPublicGeneration = first.BattleGeneration;
            secondPublicGeneration = second.BattleGeneration;

            BattleTransactionIdentity firstOwner =
                BattleTransactionIdentity.ForRootAction(
                    new ActionIdentity(first.BattleGeneration, 1L, 1L));
            Require(first.TransactionArbiter.TryAcquire(
                    firstOwner,
                    out BattleRuleMutationLease firstLease,
                    out string firstError),
                firstError);
            Require(!second.TransactionArbiter.TryAcquire(
                    firstOwner,
                    out BattleRuleMutationLease foreignLease,
                    out _),
                "A complete identity from the first Session acquired the " +
                "second Session's mutation arbiter.");
            Require(foreignLease == null &&
                    !second.TransactionArbiter.HasOwner &&
                    second.TransactionArbiter.IsCommandInputOpen,
                "A rejected cross-Session callback changed the second " +
                "Session's owner or input gate.");

            BattleTransactionIdentity secondOwner =
                BattleTransactionIdentity.ForRootAction(
                    new ActionIdentity(second.BattleGeneration, 1L, 1L));
            Require(second.TransactionArbiter.TryAcquire(
                    secondOwner,
                    out BattleRuleMutationLease secondLease,
                    out string error),
                error);
            BattleTransactionIdentity foreignContinuation =
                BattleTransactionIdentity.ForTurnEntry(
                    new TurnEntryIdentity(
                        first.BattleGeneration,
                        1L,
                        new ParticipantId(9101)));
            Require(!second.TransactionArbiter.TransferToContinuation(
                    secondLease,
                    foreignContinuation,
                    out BattleRuleMutationLease foreignContinuationLease,
                    out _),
                "A continuation from the first Session advanced the second Session.");
            Require(foreignContinuationLease == null &&
                    second.TransactionArbiter.CurrentOwner == secondOwner &&
                    secondLease.IsActive,
                "A rejected cross-Session continuation changed the second " +
                "Session's stage owner.");
            secondLease.Dispose();
            firstLease.Dispose();
        });

        DRPGCombatantProfileSO factoryProfile = CreateProfile();
        try
        {
            var firstRoster = DRPGBattleSessionCreationResult.Success(new[]
            {
                CreateParticipant(
                    9103,
                    factoryProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist)
            });
            var secondRoster = DRPGBattleSessionCreationResult.Success(new[]
            {
                CreateParticipant(
                    9104,
                    factoryProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist)
            });
            DRPGBattleSession firstFactory =
                DRPGBattleSessionFactory.CreateSession(firstRoster);
            DRPGBattleSession secondFactory =
                DRPGBattleSessionFactory.CreateSession(secondRoster);
            Require(firstFactory.BattleGeneration > secondPublicGeneration &&
                    secondFactory.BattleGeneration > firstFactory.BattleGeneration &&
                    firstPublicGeneration != firstFactory.BattleGeneration,
                "Factory-created Sessions must share the process monotonic generation source.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(factoryProfile);
        }
    }

    internal static void LegacyTurnTokenHasDomainProvenance()
    {
        DRPGCombatantProfileSO playerProfile = CreateProfile(30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(10f);
        try
        {
            BattleParticipantRuntime player = CreateParticipant(
                9201, playerProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(
                9202, enemyProfile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            session.Start();

            ActorTurnToken token = session.CurrentActorTurnToken;
            Require(ReferenceEquals(session.CurrentActor, player),
                "The fixture did not publish the intended current actor.");
            Require(token.IsValid,
                "A published legacy current actor must carry a valid ActorTurnToken.");
            Require(token.BattleGeneration == session.BattleGeneration &&
                    token.Actor == new ParticipantId(player.RuntimeId),
                "The legacy turn token must come from the Session identity authority.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }
    }

    internal static void ArbiterExclusiveOwnership()
    {
        var sink = new LosslessActionTraceSink();
        var trace = new DRPGBattleActionTraceWriter(sink);
        var arbiter = new DRPGBattleTransactionArbiter(7L, trace);
        BattleTransactionIdentity root = BattleTransactionIdentity.ForRootAction(
            new ActionIdentity(7L, 11L, 13L));
        BattleTransactionIdentity turnEntry = BattleTransactionIdentity.ForTurnEntry(
            new TurnEntryIdentity(7L, 17L, new ParticipantId(23)));
        BattleTransactionIdentity secondRoot = BattleTransactionIdentity.ForRootAction(
            new ActionIdentity(7L, 11L, 19L));
        BattleTransactionIdentity legacy = BattleTransactionIdentity.ForLegacyAction(
            new ActionIdentity(7L, 11L, 29L));
        BattleTransactionIdentity wave = BattleTransactionIdentity.ForWave(
            new WaveTransitionIdentity(7L, 31L, 2L));

        Require(arbiter.TryAcquire(root, out BattleRuleMutationLease first,
                out string error), error);
        Require(first != null && first.IsActive && arbiter.Owns(first, root),
            "The first owner did not receive the exclusive rule lease.");
        BattleTransactionIdentity[] contenders =
            { secondRoot, turnEntry, legacy, wave };
        for (int i = 0; i < contenders.Length; i++)
        {
            Require(!arbiter.TryAcquire(contenders[i], out _, out _),
                "A second transaction acquired the battle while the root action owned it: " +
                contenders[i].Kind + ".");
        }
        Require(arbiter.CurrentOwner == root && !arbiter.IsCommandInputOpen,
            "A rejected contender changed the active owner or opened input.");

        Require(arbiter.TransferToContinuation(
                first,
                turnEntry,
                out BattleRuleMutationLease continuation,
                out error),
            error);
        Require(continuation != null &&
                arbiter.CurrentOwner == turnEntry &&
                !arbiter.IsCommandInputOpen,
            "Continuation transfer introduced an owner or input gap.");

        int traceCountBeforeStaleDispose = sink.Snapshot().Count;
        first.Dispose();
        Require(arbiter.CurrentOwner == turnEntry && continuation.IsActive,
            "A stale dispose released or replaced the continuation owner.");
        IReadOnlyList<ActionTraceEntry> staleTrace = sink.Snapshot();
        Require(staleTrace.Count == traceCountBeforeStaleDispose + 1 &&
                staleTrace[staleTrace.Count - 1].EventKind ==
                    ActionTraceEventKind.StaleCallbackDropped,
            "A stale dispose was not traced against the active continuation owner.");

        continuation.Dispose();
        Require(!arbiter.HasOwner && arbiter.IsCommandInputOpen,
            "Releasing the matching owner did not reopen the idle battle gate.");
    }

    internal static void ReceiptTerminalExactlyOnce()
    {
        var trace = new DRPGBattleActionTraceWriter(
            new LosslessActionTraceSink());
        ActionIdentity identity = new ActionIdentity(31L, 37L, 41L);
        var receipt = new RootActionReceipt(
            identity,
            trace,
            AppendReceiptLockPrelude(trace, identity, 111, 211));
        var terminal = new RootActionTerminal(
            RootActionTerminalKind.Completed, "done");

        Require(receipt.TryComplete(identity, terminal, "terminal-a", out string error),
            error);
        Require(receipt.TryComplete(identity, terminal, "terminal-a", out error),
            "The same receipt terminal and fingerprint must be idempotent: " + error);
        Require(!receipt.TryComplete(
                identity,
                new RootActionTerminal(
                    RootActionTerminalKind.Faulted,
                    "different-payload"),
                "terminal-a",
                out _),
            "The same fingerprint accepted a different terminal payload.");
        Require(!receipt.TryComplete(identity, terminal, "terminal-b", out _),
            "A different fingerprint completed an already terminal receipt.");
        Require(receipt.Completion.IsCompleted &&
                receipt.Completion.Result.Kind == RootActionTerminalKind.Completed,
            "The root receipt did not expose its one authoritative terminal.");

        IReadOnlyList<ActionTraceEntry> entries = receipt.SnapshotTrace();
        int issued = 0;
        int terminalCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].EventKind == ActionTraceEventKind.ReceiptIssued)
                issued++;
            if (entries[i].EventKind == ActionTraceEventKind.ReceiptCompleted)
                terminalCount++;
        }
        Require(issued == 1 && terminalCount == 1,
            "The root receipt trace must contain exactly one issuance and " +
            "one terminal receipt event.");
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            Require(eventIds.Add(entries[i].EventId),
                "Repeated receipt callbacks reused a trace EventId.");
        }

        var faultTrace = new DRPGBattleActionTraceWriter(
            new LosslessActionTraceSink());
        ActionIdentity faultIdentity = new ActionIdentity(31L, 37L, 43L);
        var faultReceipt = new RootActionReceipt(
            faultIdentity,
            faultTrace,
            AppendReceiptLockPrelude(faultTrace, faultIdentity, 113, 213));
        Require(faultReceipt.TryComplete(
                faultIdentity,
                new RootActionTerminal(RootActionTerminalKind.Faulted, "fault"),
                "terminal-fault",
                out error),
            error);
        IReadOnlyList<ActionTraceEntry> faultEntries =
            faultReceipt.SnapshotTrace();
        ActionTraceReceiptStatus faultStatus = ActionTraceReceiptStatus.None;
        for (int i = 0; i < faultEntries.Count; i++)
        {
            if (faultEntries[i].EventKind ==
                ActionTraceEventKind.ReceiptCompleted)
            {
                faultStatus = faultEntries[i].ReceiptStatus;
            }
        }
        Require(faultStatus == ActionTraceReceiptStatus.Faulted,
            "A faulted root terminal was traced as an ordinary completion.");

        var sharedTrace = new DRPGBattleActionTraceWriter(
            new LosslessActionTraceSink());
        ActorTurnToken firstTurn = new ActorTurnToken(
            91L, 1L, new ParticipantId(201));
        TargetSelectionToken firstSelection = new TargetSelectionToken(
            91L, 1L, 1L, firstTurn.Actor);
        ActionIdentity firstAction = new ActionIdentity(91L, 1L, 1L);
        RootActionReceipt firstScopedReceipt = AppendCompleteReceiptTrace(
            sharedTrace,
            firstTurn,
            firstSelection,
            firstAction,
            new ParticipantId(301));
        ActorTurnToken secondTurn = new ActorTurnToken(
            91L, 2L, new ParticipantId(203));
        TargetSelectionToken secondSelection = new TargetSelectionToken(
            91L, 2L, 2L, secondTurn.Actor);
        ActionIdentity secondAction = new ActionIdentity(91L, 2L, 2L);
        RootActionReceipt secondScopedReceipt = AppendCompleteReceiptTrace(
            sharedTrace,
            secondTurn,
            secondSelection,
            secondAction,
            new ParticipantId(303));

        VerifyReceiptTraceScope(
            firstScopedReceipt,
            firstTurn,
            firstSelection,
            firstAction);
        VerifyReceiptTraceScope(
            secondScopedReceipt,
            secondTurn,
            secondSelection,
            secondAction);
    }

    internal static void StaleIdentityCannotAdvance()
    {
        RequireThrows<ArgumentException>(
            () => BattleTransactionIdentity.ForBattleStart(default),
            "An invalid battle-start identity entered the transaction union.");
        RequireThrows<ArgumentException>(
            () => BattleTransactionIdentity.ForRootAction(default),
            "An invalid root-action identity entered the transaction union.");
        RequireThrows<ArgumentException>(
            () => BattleTransactionIdentity.ForLegacyAction(default),
            "An invalid legacy-action identity entered the transaction union.");
        RequireThrows<ArgumentException>(
            () => BattleTransactionIdentity.ForTurnEntry(default),
            "An invalid turn-entry identity entered the transaction union.");
        RequireThrows<ArgumentException>(
            () => BattleTransactionIdentity.ForWave(default),
            "An invalid wave identity entered the transaction union.");

        var authority = new DRPGBattleIdentityAuthority(47L);
        ActorTurnToken supersededTurn = authority.AllocateTurn(
            new ParticipantId(101));
        ActorTurnToken currentTurn = authority.AllocateTurn(
            new ParticipantId(103));
        RequireThrows<ArgumentException>(
            () => authority.AllocateAction(supersededTurn),
            "A superseded turn token allocated a new ActionIdentity.");
        Require(authority.AllocateAction(currentTurn).TurnSerial ==
                currentTurn.TurnSerial,
            "The current turn token did not allocate an action identity.");
        ActorTurnToken forgedActor = new ActorTurnToken(
            currentTurn.BattleGeneration,
            currentTurn.TurnSerial,
            new ParticipantId(107));
        RequireThrows<ArgumentException>(
            () => authority.AllocateSelection(forgedActor),
            "A forged actor reused the current turn serial to allocate a selection.");
        RequireThrows<ArgumentException>(
            () => authority.AllocateAction(forgedActor),
            "A forged actor reused the current turn serial to allocate an action.");

        var lockAuthority = new DRPGBattleIdentityAuthority(48L);
        ActorTurnToken lockTurn = lockAuthority.AllocateTurn(
            new ParticipantId(109));
        long rejectedCandidateActionId;
        using (DRPGBattleActionIdentityReservation rejected =
            lockAuthority.ReserveAction(lockTurn))
        {
            rejectedCandidateActionId = rejected.CandidateActionId;
        }
        using (DRPGBattleActionIdentityReservation accepted =
            lockAuthority.ReserveAction(lockTurn))
        {
            Require(accepted.CandidateActionId == rejectedCandidateActionId,
                "A pre-lock rejection consumed an ActionId.");
            Require(accepted.Commit().ActionId == rejectedCandidateActionId,
                "The successful lock did not commit its reserved identity.");
        }
        Require(lockAuthority.AllocateAction(lockTurn).ActionId ==
                rejectedCandidateActionId + 1L,
            "A committed action identity did not advance exactly once.");

        var sink = new LosslessActionTraceSink();
        var trace = new DRPGBattleActionTraceWriter(sink);
        ActionIdentity active = new ActionIdentity(51L, 53L, 59L);
        ActionIdentity stale = new ActionIdentity(50L, 53L, 59L);
        var receipt = new RootActionReceipt(
            active,
            trace,
            AppendReceiptLockPrelude(trace, active, 117, 217));

        Require(!receipt.TryComplete(
                stale,
                new RootActionTerminal(RootActionTerminalKind.Completed, "stale"),
                "stale-fingerprint",
                out _),
            "A stale battle generation completed the active root receipt.");
        Require(!receipt.Completion.IsCompleted,
            "A stale callback advanced the active root receipt terminal.");
        IReadOnlyList<ActionTraceEntry> entries = receipt.SnapshotTrace();
        Require(entries.Count >= 2 &&
                entries[entries.Count - 1].EventKind ==
                    ActionTraceEventKind.StaleCallbackDropped &&
                entries[entries.Count - 1].Owner ==
                    BattleTransactionIdentity.ForRootAction(active),
            "A stale callback was not recorded against the active full identity.");

        VerifyDomainBridgeDoesNotForgeTurnSerial();
    }

    internal static void TraceSchemaAndTotalOrder()
    {
        string[] requiredProperties =
        {
            "TraceIdentity", "PrecursorTurn", "PrecursorSelection",
            "Actor", "Target", "Source", "Side", "HomePosition",
            "ConflictPosition", "HitOutcome", "BeforeProjection",
            "AfterProjection", "ReceiptStatus", "LeaseId", "BarrierId",
            "WatchdogId", "StaleReason"
        };
        for (int i = 0; i < requiredProperties.Length; i++)
        {
            Require(typeof(ActionTraceEntry).GetProperty(requiredProperties[i]) != null,
                "ActionTraceEntry cannot carry required spec field: " +
                requiredProperties[i] + ".");
        }

        var sink = new LosslessActionTraceSink();
        var trace = new DRPGBattleActionTraceWriter(sink);
        ParticipantId actor = new ParticipantId(73);
        ParticipantId target = new ParticipantId(79);
        ActorTurnToken turn = new ActorTurnToken(61L, 67L, actor);
        TargetSelectionToken selection = new TargetSelectionToken(
            61L, 67L, 69L, actor);
        ActionIdentity action = new ActionIdentity(61L, 67L, 71L);
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForRootAction(action);
        var receiptId = new PresentationReceiptId(1L);

        trace.Record(
            0.9d,
            1.9d,
            ActionTraceIdentity.ForTurn(turn),
            ActionTraceEventKind.PhaseEntered,
            DRPGBattleActionStage.TurnBegin,
            0,
            "turn-begin",
            null,
            new ActionTraceContext(
                actor: actor,
                side: DRPGBattleSide.Player,
                homePosition: new ActionTracePosition(-4f, 0f, 0f)));
        trace.Record(
            1d,
            2d,
            ActionTraceIdentity.ForSelection(selection),
            ActionTraceEventKind.SelectionOpened,
            DRPGBattleActionStage.Selection,
            0,
            "selection",
            "turn-begin",
            new ActionTraceContext(
                precursorTurn: turn,
                actor: actor,
                target: target,
                side: DRPGBattleSide.Player));
        trace.Record(
            1.1d,
            2.1d,
            ActionTraceIdentity.ForSelection(selection),
            ActionTraceEventKind.SelectionConfirmed,
            DRPGBattleActionStage.Selection,
            1,
            "selection-confirmed",
            "selection",
            new ActionTraceContext(
                precursorTurn: turn,
                actor: actor,
                target: target,
                side: DRPGBattleSide.Player));
        trace.Record(
            1.15d,
            2.15d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ActionLocked,
            DRPGBattleActionStage.Locked,
            0,
            "lock",
            "selection-confirmed",
            new ActionTraceContext(
                precursorTurn: turn,
                precursorSelection: selection,
                actor: actor,
                target: target,
                source: actor,
                side: DRPGBattleSide.Player,
                homePosition: new ActionTracePosition(-4f, 0f, 0f),
                conflictPosition: new ActionTracePosition(-1f, 0f, 0f),
                leaseId: "rule:71"));
        trace.Record(
            1.2d,
            2.2d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ReceiptIssued,
            DRPGBattleActionStage.Announcement,
            1,
            "announce-issued",
            "lock",
            new ActionTraceContext(
                actor: actor,
                target: target,
                side: DRPGBattleSide.Player,
                receiptId: receiptId,
                receiptStatus: ActionTraceReceiptStatus.Issued,
                leaseId: "rule:71"));
        trace.Record(
            1.3d,
            2.3d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ReceiptCompleted,
            DRPGBattleActionStage.Announcement,
            1,
            "announce-completed",
            "announce-issued",
            new ActionTraceContext(
                actor: actor,
                target: target,
                side: DRPGBattleSide.Player,
                receiptId: receiptId,
                receiptStatus: ActionTraceReceiptStatus.Completed,
                fingerprint: "root:71",
                leaseId: "rule:71"));
        trace.Record(
            1.4d,
            2.4d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ReturnStarted,
            DRPGBattleActionStage.Return,
            0,
            "return-started",
            "announce-completed",
            new ActionTraceContext(
                actor: actor,
                target: target,
                barrierId: "return:71",
                leaseId: "rule:71"));
        trace.Record(
            1.5d,
            2.5d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ReturnBarrierCompleted,
            DRPGBattleActionStage.Return,
            0,
            "return-completed",
            "return-started",
            new ActionTraceContext(
                actor: actor,
                target: target,
                barrierId: "return:71",
                leaseId: "rule:71"));
        trace.Record(
            1.6d,
            2.6d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.SettlementItem,
            DRPGBattleActionStage.Settlement,
            1,
            "settlement-1",
            "return-completed",
            new ActionTraceContext(
                actor: actor,
                target: target,
                source: actor,
                side: DRPGBattleSide.Player,
                fingerprint: "settlement:1",
                beforeProjection: new ActionTraceProjection(10, 5, true, true),
                afterProjection: new ActionTraceProjection(9, 5, true, true),
                leaseId: "rule:71"));
        trace.Record(
            1.7d,
            2.7d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.Finalized,
            DRPGBattleActionStage.Finalized,
            0,
            "finalized",
            "announce-completed",
            new ActionTraceContext(
                actor: actor,
                target: target,
                receiptId: receiptId,
                fingerprint: "root:71",
                leaseId: "rule:71"));

        IReadOnlyList<ActionTraceEntry> entries = trace.Snapshot();
        Require(entries.Count == 10,
            "The lossless trace did not retain the full transaction sequence.");
        for (int i = 0; i < entries.Count; i++)
        {
            Require(entries[i].Sequence == i + 1L,
                "Action trace total order is incomplete.");
        }
        Require(DRPGActionTraceContractValidator.TryValidate(
                entries,
                out string error),
            error);

        var missingTerminal = new List<ActionTraceEntry>(entries);
        missingTerminal.RemoveAt(5);
        Require(!DRPGActionTraceContractValidator.TryValidate(
                missingTerminal,
                out _),
            "A receipt issuance without one terminal passed trace validation.");

        var wrongIssuedParent = new List<ActionTraceEntry>(entries);
        wrongIssuedParent[4] = WithParent(
            wrongIssuedParent[4],
            "selection-confirmed");
        Require(!DRPGActionTraceContractValidator.TryValidate(
                wrongIssuedParent,
                out _),
            "ReceiptIssued accepted a parent other than its ActionLocked event.");

        var wrongTerminalParent = new List<ActionTraceEntry>(entries);
        wrongTerminalParent[5] = WithParent(
            wrongTerminalParent[5],
            "lock");
        Require(!DRPGActionTraceContractValidator.TryValidate(
                wrongTerminalParent,
                out _),
            "ReceiptCompleted accepted a parent other than its issuance.");

        var wrongFinalParent = new List<ActionTraceEntry>(entries);
        wrongFinalParent[9] = WithParent(
            wrongFinalParent[9],
            "settlement-1");
        Require(!DRPGActionTraceContractValidator.TryValidate(
                wrongFinalParent,
                out _),
            "Finalized accepted a parent other than its matching receipt terminal.");

        var forgedPrecursor = new List<ActionTraceEntry>(entries);
        ActionTraceEntry locked = forgedPrecursor[3];
        forgedPrecursor[3] = new ActionTraceEntry(
            locked.Sequence,
            locked.LogicalTimestamp,
            locked.PresentationTimestamp,
            locked.TraceIdentity,
            locked.EventKind,
            locked.Stage,
            locked.StepOrdinal,
            locked.EventId,
            locked.ParentEventId,
            new ActionTraceContext(
                precursorTurn: turn,
                precursorSelection: new TargetSelectionToken(
                    selection.BattleGeneration,
                    selection.TurnSerial,
                    selection.SelectionId,
                    new ParticipantId(83)),
                actor: actor,
                target: target,
                source: actor,
                side: DRPGBattleSide.Player,
                homePosition: locked.HomePosition,
                conflictPosition: locked.ConflictPosition,
                leaseId: locked.LeaseId));
        Require(!DRPGActionTraceContractValidator.TryValidate(
                forgedPrecursor,
                out _),
            "An ActionIdentity accepted an unrelated selection precursor.");

        var forgedSelectionIdentity = new List<ActionTraceEntry>(entries);
        locked = forgedSelectionIdentity[3];
        forgedSelectionIdentity[3] = new ActionTraceEntry(
            locked.Sequence,
            locked.LogicalTimestamp,
            locked.PresentationTimestamp,
            locked.TraceIdentity,
            locked.EventKind,
            locked.Stage,
            locked.StepOrdinal,
            locked.EventId,
            locked.ParentEventId,
            new ActionTraceContext(
                precursorTurn: turn,
                precursorSelection: new TargetSelectionToken(
                    selection.BattleGeneration,
                    selection.TurnSerial,
                    selection.SelectionId + 1L,
                    actor),
                actor: actor,
                target: new ParticipantId(83),
                source: actor,
                side: DRPGBattleSide.Player,
                homePosition: locked.HomePosition,
                conflictPosition: locked.ConflictPosition,
                leaseId: locked.LeaseId));
        Require(!DRPGActionTraceContractValidator.TryValidate(
                forgedSelectionIdentity,
                out _),
            "ActionLocked accepted a different SelectionId and target than " +
            "its parent SelectionOpened event.");

        var emptySettlement = new List<ActionTraceEntry>(entries);
        ActionTraceEntry settlement = emptySettlement[8];
        emptySettlement[8] = new ActionTraceEntry(
            settlement.Sequence,
            settlement.LogicalTimestamp,
            settlement.PresentationTimestamp,
            settlement.TraceIdentity,
            ActionTraceEventKind.PhaseEntered,
            DRPGBattleActionStage.Settlement,
            0,
            "settlement-empty",
            "return-completed",
            new ActionTraceContext(leaseId: settlement.LeaseId));
        Require(DRPGActionTraceContractValidator.TryValidate(
                emptySettlement,
                out error),
            "An explicit empty Settlement phase was not reconstructable: " + error);

        var outOfOrder = new List<ActionTraceEntry>(entries);
        ActionTraceEntry returnStarted = outOfOrder[6];
        outOfOrder[6] = outOfOrder[8];
        outOfOrder[8] = returnStarted;
        Require(!DRPGActionTraceContractValidator.TryValidate(
                outOfOrder,
                out _),
            "Settlement before ReturnBarrier passed trace validation.");
    }

    private static void VerifyDomainBridgeDoesNotForgeTurnSerial()
    {
        DRPGCombatantProfileSO playerProfile = CreateProfile(30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(10f);
        DRPGSkillSO basicAttack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        GameObject bridgeObject = new GameObject("Task2DomainIdentityBridge");
        try
        {
            BattleParticipantRuntime player = CreateParticipant(
                9301, playerProfile, DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(
                9302, enemyProfile, DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            DRPGBattlePresentationEventBridge bridge =
                bridgeObject.AddComponent<DRPGBattlePresentationEventBridge>();
            DRPGBattlePresentationDirector director =
                bridgeObject.AddComponent<DRPGBattlePresentationDirector>();
            DRPGBattleUGUISurface surface =
                bridgeObject.AddComponent<DRPGBattleUGUISurface>();
            bridge.Configure(director, surface, NullBattleAudioCueSink.Instance);
            bridge.Bind(session);

            Require(!session.CurrentActorTurnToken.IsValid &&
                    bridge.CurrentTurnSerial == 0L,
                "The no-turn domain fixture unexpectedly had a turn token.");
            InvokePrivate(
                bridge,
                "OnCommandPreviewed",
                DRPGBattleCommand.BasicAttack(
                    enemy.RuntimeId,
                    player.RuntimeId,
                    basicAttack));
            Require(bridge.PendingSignalCount == 0 &&
                    bridge.CurrentTurnSerial == 0L,
                "Domain presentation forged TurnSerial=1 before a valid " +
                "ActorTurnToken existed.");
            bridge.Unbind();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(bridgeObject);
            UnityEngine.Object.DestroyImmediate(basicAttack);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }
    }

    private static string AppendReceiptLockPrelude(
        DRPGBattleActionTraceWriter trace,
        ActionIdentity action,
        int actorId,
        int targetId)
    {
        var actor = new ParticipantId(actorId);
        var target = new ParticipantId(targetId);
        var turn = new ActorTurnToken(
            action.BattleGeneration,
            action.TurnSerial,
            actor);
        var selection = new TargetSelectionToken(
            action.BattleGeneration,
            action.TurnSerial,
            action.ActionId,
            actor);
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForRootAction(action);
        string prefix = "receipt-prelude-" + action.ActionId + "-";
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTurn(turn),
            ActionTraceEventKind.PhaseEntered,
            DRPGBattleActionStage.TurnBegin,
            0,
            prefix + "turn",
            null,
            new ActionTraceContext(
                actor: actor,
                side: DRPGBattleSide.Player,
                homePosition: new ActionTracePosition(-4f, 0f, 0f)));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForSelection(selection),
            ActionTraceEventKind.SelectionOpened,
            DRPGBattleActionStage.Selection,
            0,
            prefix + "selection",
            prefix + "turn",
            new ActionTraceContext(
                precursorTurn: turn,
                actor: actor,
                side: DRPGBattleSide.Player));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForSelection(selection),
            ActionTraceEventKind.SelectionConfirmed,
            DRPGBattleActionStage.Selection,
            1,
            prefix + "selection-confirmed",
            prefix + "selection",
            new ActionTraceContext(
                precursorTurn: turn,
                actor: actor,
                target: target,
                side: DRPGBattleSide.Player));
        string lockEventId = prefix + "lock";
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ActionLocked,
            DRPGBattleActionStage.Locked,
            0,
            lockEventId,
            prefix + "selection-confirmed",
            new ActionTraceContext(
                precursorTurn: turn,
                precursorSelection: selection,
                actor: actor,
                target: target,
                source: actor,
                side: DRPGBattleSide.Player,
                homePosition: new ActionTracePosition(-4f, 0f, 0f),
                conflictPosition: new ActionTracePosition(-1f, 0f, 0f),
                leaseId: prefix + "lease"));
        return lockEventId;
    }

    private static ActionTraceEntry WithParent(
        ActionTraceEntry entry,
        string parentEventId)
    {
        return new ActionTraceEntry(
            entry.Sequence,
            entry.LogicalTimestamp,
            entry.PresentationTimestamp,
            entry.TraceIdentity,
            entry.EventKind,
            entry.Stage,
            entry.StepOrdinal,
            entry.EventId,
            parentEventId,
            new ActionTraceContext(
                precursorTurn: entry.PrecursorTurn,
                precursorSelection: entry.PrecursorSelection,
                actor: entry.Actor,
                target: entry.Target,
                source: entry.Source,
                side: entry.Side,
                homePosition: entry.HomePosition,
                conflictPosition: entry.ConflictPosition,
                hitOutcome: entry.HitOutcome,
                beforeProjection: entry.BeforeProjection,
                afterProjection: entry.AfterProjection,
                receiptId: entry.ReceiptId,
                receiptStatus: entry.ReceiptStatus,
                leaseId: entry.LeaseId,
                barrierId: entry.BarrierId,
                watchdogId: entry.WatchdogId,
                staleReason: entry.StaleReason,
                fingerprint: entry.Fingerprint,
                detail: entry.Detail));
    }

    private static RootActionReceipt AppendCompleteReceiptTrace(
        DRPGBattleActionTraceWriter trace,
        ActorTurnToken turn,
        TargetSelectionToken selection,
        ActionIdentity action,
        ParticipantId target)
    {
        string prefix = "scoped-" + action.ActionId + "-";
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForRootAction(action);
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTurn(turn),
            ActionTraceEventKind.PhaseEntered,
            DRPGBattleActionStage.TurnBegin,
            0,
            prefix + "turn",
            null,
            new ActionTraceContext(
                actor: turn.Actor,
                side: DRPGBattleSide.Player,
                homePosition: new ActionTracePosition(-4f, 0f, 0f)));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForSelection(selection),
            ActionTraceEventKind.SelectionOpened,
            DRPGBattleActionStage.Selection,
            0,
            prefix + "selection",
            prefix + "turn",
            new ActionTraceContext(
                precursorTurn: turn,
                actor: turn.Actor,
                target: target,
                side: DRPGBattleSide.Player));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForSelection(selection),
            ActionTraceEventKind.SelectionConfirmed,
            DRPGBattleActionStage.Selection,
            1,
            prefix + "selection-confirmed",
            prefix + "selection",
            new ActionTraceContext(
                precursorTurn: turn,
                actor: turn.Actor,
                target: target,
                side: DRPGBattleSide.Player));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ActionLocked,
            DRPGBattleActionStage.Locked,
            0,
            prefix + "lock",
            prefix + "selection-confirmed",
            new ActionTraceContext(
                precursorTurn: turn,
                precursorSelection: selection,
                actor: turn.Actor,
                target: target,
                source: turn.Actor,
                side: DRPGBattleSide.Player,
                homePosition: new ActionTracePosition(-4f, 0f, 0f),
                conflictPosition: new ActionTracePosition(-1f, 0f, 0f),
                leaseId: prefix + "lease"));
        var receipt = new RootActionReceipt(
            action,
            trace,
            prefix + "lock");
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ReturnStarted,
            DRPGBattleActionStage.Return,
            0,
            prefix + "return-started",
            prefix + "lock",
            new ActionTraceContext(
                actor: turn.Actor,
                target: target,
                barrierId: prefix + "barrier",
                leaseId: prefix + "lease"));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ReturnBarrierCompleted,
            DRPGBattleActionStage.Return,
            0,
            prefix + "return-completed",
            prefix + "return-started",
            new ActionTraceContext(
                actor: turn.Actor,
                target: target,
                barrierId: prefix + "barrier",
                leaseId: prefix + "lease"));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.PhaseEntered,
            DRPGBattleActionStage.Settlement,
            0,
            prefix + "settlement",
            prefix + "return-completed",
            new ActionTraceContext(leaseId: prefix + "lease"));
        Require(receipt.TryComplete(
                action,
                new RootActionTerminal(RootActionTerminalKind.Completed, "done"),
                prefix + "terminal",
                out string error),
            error);
        return receipt;
    }

    private static void VerifyReceiptTraceScope(
        RootActionReceipt receipt,
        ActorTurnToken turn,
        TargetSelectionToken selection,
        ActionIdentity action)
    {
        IReadOnlyList<ActionTraceEntry> entries = receipt.SnapshotTrace();
        Require(entries.Count == 10,
            "A root receipt snapshot mixed events from another action.");
        for (int i = 0; i < entries.Count; i++)
        {
            ActionTraceEntry entry = entries[i];
            bool belongsToAction =
                entry.TraceIdentity.Kind == ActionTraceIdentityKind.ActorTurn &&
                    entry.TraceIdentity.Turn == turn ||
                entry.TraceIdentity.Kind ==
                    ActionTraceIdentityKind.TargetSelection &&
                    entry.TraceIdentity.Selection == selection ||
                entry.TraceIdentity.Kind == ActionTraceIdentityKind.Transaction &&
                    entry.Owner == BattleTransactionIdentity.ForRootAction(action);
            Require(belongsToAction,
                "A receipt exposed an event owned by another action.");
        }
        Require(DRPGActionTraceContractValidator.TryValidate(
                entries,
                out string error),
            "The action-scoped receipt trace is not independently valid: " +
            error);
    }

    internal static void SelectionTokenLifecycle()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            SetField(skill, "skillId", "task4_selection_basic");
            SetField(skill, "targetRule", DRPGSkillTargetRule.SingleEnemy);
            var identities = new DRPGBattleIdentityAuthority(9401L);
            ActorTurnToken turn = identities.AllocateTurn(new ParticipantId(41));
            var state = new SelectionStateStub(
                turn,
                new ParticipantId(42),
                skill);
            var selectionTrace = new DRPGBattleActionTraceWriter(
                new LosslessActionTraceSink());
            DRPGBasicAttackTrace.RecordTurnBegin(
                selectionTrace,
                turn,
                DRPGBattleSide.Player,
                new ActionTracePosition(0f, 0f, 0f));
            var authority = new DRPGBasicAttackSelectionAuthority(
                identities,
                state,
                selectionTrace);

            TargetSelectionOpenResult first =
                authority.TryOpen(turn);
            Require(first.IsAccepted && first.Selection.IsValid,
                "A valid current actor could not open Basic Attack targeting.");
            TargetSelectionCancelResult cancelled =
                authority.TryCancel(first.Selection);
            Require(cancelled.IsCancelled,
                "A pre-lock Basic Attack selection could not be cancelled.");
            Require(!authority.TryCancel(first.Selection).IsCancelled,
                "The same Basic Attack selection was cancelled twice.");

            TargetSelectionOpenResult second = authority.TryOpen(turn);
            Require(second.IsAccepted &&
                    second.Selection.SelectionId != first.Selection.SelectionId,
                "Reopening targeting did not allocate a fresh selection identity.");
            Require(!authority.TryOpen(new ActorTurnToken(
                        turn.BattleGeneration + 1L,
                        turn.TurnSerial,
                        turn.Actor)).IsAccepted,
                "A target selection opened for another battle generation.");
            Require(!authority.TryOpen(new ActorTurnToken(
                        turn.BattleGeneration,
                        turn.TurnSerial,
                        new ParticipantId(99))).IsAccepted,
                "A target selection opened for a forged current actor.");

            Require(!authority.TryFreeze(
                    new BasicAttackSelection(
                        second.Selection,
                        new ParticipantId(99)),
                    out _,
                    out BasicAttackRejection illegalTarget) &&
                    illegalTarget == BasicAttackRejection.IllegalTarget,
                "An illegal Basic Attack target was not rejected before lock.");
            Require(authority.TryCancel(second.Selection).IsCancelled,
                "A rejected target consumed the still-cancellable selection.");

            TargetSelectionOpenResult third = authority.TryOpen(turn);
            Require(third.IsAccepted,
                "The valid Basic Attack selection did not reopen.");
            bool froze = authority.TryFreeze(
                    new BasicAttackSelection(
                        third.Selection,
                        new ParticipantId(42)),
                    out DRPGBasicAttackFrozenSnapshot snapshot,
                    out BasicAttackRejection rejection);
            Require(froze,
                "A legal Basic Attack selection could not freeze: " + rejection);
            Require(snapshot.Turn == turn &&
                    snapshot.Selection == third.Selection &&
                    snapshot.Target == new ParticipantId(42),
                "The frozen Basic Attack snapshot lost its precursor identity.");
            authority.Consume(third.Selection);
            Require(!authority.TryCancel(third.Selection).IsCancelled,
                "A locked Basic Attack remained cancellable.");

            TargetSelectionOpenResult fourth = authority.TryOpen(turn);
            Require(fourth.IsAccepted,
                "The atomic promotion fixture could not open targeting.");
            var promotionTrace = new DRPGBattleActionTraceWriter(
                new DRPGBoundedActionTraceSink(32));
            var promotionArbiter = new DRPGBattleTransactionArbiter(
                turn.BattleGeneration,
                promotionTrace);
            bool cancelledDuringPromotion = false;
            Require(authority.TryPromote(
                    new BasicAttackSelection(
                        fourth.Selection,
                        new ParticipantId(42)),
                    frozen =>
                    {
                        cancelledDuringPromotion = authority.TryCancel(
                            fourth.Selection).IsCancelled;
                        return promotionArbiter.TryPromoteSelection(
                            fourth.Selection,
                            () => identities.AllocateAction(frozen.Turn));
                    },
                    out _,
                    out RootActionOwnershipResult promoted,
                    out _,
                    out rejection) &&
                    promoted.IsAccepted &&
                    !cancelledDuringPromotion,
                "Selection cancellation crossed the atomic lock boundary.");
            promoted.Lease.Dispose();

            state.IsActionBusy = true;
            Require(authority.TryOpen(turn).Rejection == BasicAttackRejection.Busy,
                "Targeting opened while another action owned the battle.");
            state.IsActionBusy = false;
            state.IsBattleEnded = true;
            Require(authority.TryOpen(turn).Rejection ==
                    BasicAttackRejection.BattleEnded,
                "Targeting opened after the battle ended.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    internal static void RouterOwnsExactlyOneRunnerDuringMigration()
    {
        DRPGSkillSO basic = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            SetField(basic, "skillId", "task4_router_basic");
            SetField(skill, "skillId", "task4_router_skill");
            var trace = new DRPGBattleActionTraceWriter(
                new DRPGBoundedActionTraceSink(64));

            var productionIds = new DRPGBattleIdentityAuthority(9402L);
            ActorTurnToken productionTurn = productionIds.AllocateTurn(
                new ParticipantId(51));
            TargetSelectionToken productionSelection =
                productionIds.AllocateSelection(productionTurn);
            var productionSelectionAuthority = new SelectionAuthorityStub(
                new DRPGBasicAttackFrozenSnapshot(
                    productionTurn,
                    productionSelection,
                    new ParticipantId(52),
                    basic));
            var productionArbiter = new DRPGBattleTransactionArbiter(
                9402L,
                trace);
            var productionLegacy = new LegacyRunnerStub();
            var productionStandard = new StandardRunnerStub(trace);
            var productionRouter = new DRPGBattleActionExecutionRouter(
                DRPGBattleTransactionPolicy.ProductionLegacy,
                productionSelectionAuthority,
                productionLegacy,
                productionStandard,
                productionIds,
                productionArbiter);

            productionRouter.TryStartLegacy(DRPGBattleCommand.BasicAttack(
                51,
                52,
                basic));
            productionRouter.TryStartLegacy(DRPGBattleCommand.UseSkill(
                51,
                52,
                skill));
            Require(productionLegacy.StartCount == 2 &&
                    productionStandard.StartCount == 0,
                "Production policy did not route both commands exactly once " +
                "through Legacy.");

            var testIds = new DRPGBattleIdentityAuthority(9403L);
            ActorTurnToken testTurn = testIds.AllocateTurn(new ParticipantId(61));
            TargetSelectionToken testSelection = testIds.AllocateSelection(testTurn);
            var testSelectionAuthority = new SelectionAuthorityStub(
                new DRPGBasicAttackFrozenSnapshot(
                    testTurn,
                    testSelection,
                    new ParticipantId(62),
                    basic),
                trace);
            var testArbiter = new DRPGBattleTransactionArbiter(9403L, trace);
            var testLegacy = new LegacyRunnerStub();
            var testStandard = new StandardRunnerStub(trace, holdLease: true);
            var testRouter = new DRPGBattleActionExecutionRouter(
                DRPGBattleTransactionPolicy.StandardBasicAttackTest,
                testSelectionAuthority,
                testLegacy,
                testStandard,
                testIds,
                testArbiter);

            RootActionStartResult started = testRouter.TryStart(
                new BasicAttackSelection(testSelection, new ParticipantId(62)));
            Require(started.IsAccepted && testStandard.StartCount == 1 &&
                    testLegacy.StartCount == 0 && testArbiter.HasOwner &&
                    testStandard.LastSnapshot.Identity ==
                        started.Receipt.Identity,
                "Test policy did not lock exactly one Standard Basic Attack runner.");
            IReadOnlyList<ActionTraceEntry> routedTrace =
                started.Receipt.SnapshotTrace();
            Require(routedTrace.Count == 5 &&
                    routedTrace[0].EventKind == ActionTraceEventKind.PhaseEntered &&
                    routedTrace[0].Stage == DRPGBattleActionStage.TurnBegin &&
                    routedTrace[1].EventKind == ActionTraceEventKind.SelectionOpened &&
                    routedTrace[1].ParentEventId == routedTrace[0].EventId &&
                    routedTrace[2].EventKind ==
                        ActionTraceEventKind.SelectionConfirmed &&
                    routedTrace[2].ParentEventId == routedTrace[1].EventId &&
                    routedTrace[3].EventKind == ActionTraceEventKind.ActionLocked &&
                    routedTrace[3].ParentEventId == routedTrace[2].EventId &&
                    routedTrace[3].PrecursorSelection == testSelection &&
                    routedTrace[3].Owner == BattleTransactionIdentity.ForRootAction(
                        started.Receipt.Identity) &&
                    routedTrace[4].EventKind == ActionTraceEventKind.ReceiptIssued &&
                    routedTrace[4].ParentEventId == routedTrace[3].EventId,
                "The production Router did not write the real turn-selection-lock trace prelude.");
            LegacyActionStartResult concurrent = testRouter.TryStartLegacy(
                DRPGBattleCommand.UseSkill(61, 62, skill));
            Require(!concurrent.IsAccepted && testLegacy.StartCount == 0,
                "Legacy ran concurrently with the Standard transaction owner.");
            testStandard.ReleaseHeldLease();

            var faultTrace = new DRPGBattleActionTraceWriter(
                new LosslessActionTraceSink());
            var faultIds = new DRPGBattleIdentityAuthority(9407L);
            ActorTurnToken faultTurn = faultIds.AllocateTurn(
                new ParticipantId(65));
            TargetSelectionToken faultSelection = faultIds.AllocateSelection(
                faultTurn);
            var faultSnapshot = new DRPGBasicAttackFrozenSnapshot(
                faultTurn,
                faultSelection,
                new ParticipantId(66),
                basic);
            var faultSelectionAuthority = new SelectionAuthorityStub(
                faultSnapshot,
                faultTrace);
            var faultArbiter = new DRPGBattleTransactionArbiter(
                9407L,
                faultTrace);
            var faultSlot = new DRPGBattleTransactionPresentationPortSlot(
                9407L);
            var faultPort = new PresentationPortStub();
            Require(faultSlot.TryBind(9407L, faultPort) ==
                    PresentationPortBindResult.Bound,
                "The dormant Standard trace fixture could not bind presentation.");
            var faultRouter = new DRPGBattleActionExecutionRouter(
                DRPGBattleTransactionPolicy.StandardBasicAttackTest,
                faultSelectionAuthority,
                new LegacyRunnerStub(),
                new DRPGStandardBasicAttackTransactionModule(faultSlot),
                faultIds,
                faultArbiter);
            RootActionStartResult faulted = faultRouter.TryStart(
                new BasicAttackSelection(
                    faultSelection,
                    new ParticipantId(66)));
            string faultTraceError;
            bool faultTraceValid = DRPGActionTraceContractValidator.TryValidate(
                faulted.Receipt.SnapshotTrace(),
                out faultTraceError);
            Require(faulted.IsAccepted &&
                    faulted.Receipt.Completion.IsCompleted &&
                    faulted.Receipt.Completion.Result.Kind ==
                        RootActionTerminalKind.Faulted &&
                    faultTraceValid,
                "The real dormant Standard fault trace is not causal/valid: " +
                faultTraceError);

            var atomicIds = new DRPGBattleIdentityAuthority(9406L);
            ActorTurnToken atomicTurn = atomicIds.AllocateTurn(
                new ParticipantId(63));
            TargetSelectionToken atomicSelection =
                atomicIds.AllocateSelection(atomicTurn);
            var atomicArbiter = new DRPGBattleTransactionArbiter(
                9406L,
                trace);
            BattleRuleMutationLease competingLease = null;
            bool competingAcquired = false;
            RootActionOwnershipResult promotion =
                atomicArbiter.TryPromoteSelection(
                    atomicSelection,
                    () =>
                    {
                        competingAcquired = atomicArbiter.TryAcquire(
                            BattleTransactionIdentity.ForTestHarness(
                                new BattleStartIdentity(9406L, 1L)),
                            out competingLease,
                            out _);
                        return atomicIds.AllocateAction(atomicTurn);
                    });
            Require(promotion.IsAccepted &&
                    !competingAcquired &&
                    competingLease == null &&
                    atomicArbiter.Owns(
                        promotion.Lease,
                        BattleTransactionIdentity.ForRootAction(
                            promotion.Identity)),
                "Selection promotion did not atomically allocate identity and ownership.");
            promotion.Lease.Dispose();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(basic);
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    internal static void FrozenSnapshot()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO runtimeSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGPassiveModifierSO runtimePassive =
            ScriptableObject.CreateInstance<DRPGPassiveModifierSO>();
        DRPGStatusDefinitionSO status =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(20f);
        try
        {
            SetField(status, "statusId", "frozen_status");
            SetField(status, "displayName", "Frozen Status");
            SetField(status, "durationTurns", 3);
            SetField(status, "maxStacks", 4);
            SetField(status, "outgoingDamageMultiplier", 1.25f);
            SetField(status, "turnStartHpDelta", -2f);
            SetField(skill, "skillId", "task4_frozen_basic");
            SetField(skill, "displayName", "Frozen Before");
            SetField(skill, "presentationKey", "frozen.before");
            SetField(skill, "power", 17f);
            SetField(skill, "prepareDelay", 0.25f);
            SetField(skill, "recoveryDelay", 0.5f);
            SetField(skill, "sourceFormationRows", DRPGFormationRowMask.Front);
            SetField(skill, "targetFormationRows", DRPGFormationRowMask.Rear);
            SetField(skill, "targetRule", DRPGSkillTargetRule.SingleEnemy);
            SetField(skill, "manaCost", 3f);
            SetField(skill, "postureCost", 4f);
            SetField(skill, "impactDamageMultipliers",
                new List<float> { 0.4f, 0.6f });
            SetField(skill, "effects",
                new List<DRPGActionEffectDefinition>
                {
                    DRPGActionEffectDefinition.Damage(
                        DRPGActionEffectTarget.PrimaryTarget,
                        0.75f,
                        DRPGDamageTag.Slash),
                    DRPGActionEffectDefinition.ApplyStatus(
                        DRPGActionEffectTarget.PrimaryTarget,
                        status,
                        2,
                        5)
                });
            var identities = new DRPGBattleIdentityAuthority(9404L);
            ActorTurnToken turn = identities.AllocateTurn(new ParticipantId(71));
            TargetSelectionToken selection = identities.AllocateSelection(turn);
            var snapshot = new DRPGBasicAttackFrozenSnapshot(
                turn,
                selection,
                new ParticipantId(72),
                skill);

            SetField(skill, "displayName", "Mutated After Lock");
            SetField(skill, "presentationKey", "mutated.after");
            SetField(skill, "power", 999f);
            SetField(skill, "prepareDelay", 9f);
            SetField(skill, "recoveryDelay", 10f);
            SetField(skill, "sourceFormationRows", DRPGFormationRowMask.All);
            SetField(skill, "targetFormationRows", DRPGFormationRowMask.All);
            SetField(skill, "targetRule", DRPGSkillTargetRule.Self);
            SetField(skill, "manaCost", 30f);
            SetField(skill, "postureCost", 40f);
            SetField(skill, "impactDamageMultipliers",
                new List<float> { 1f });
            SetField(skill, "effects",
                new List<DRPGActionEffectDefinition>
                {
                    DRPGActionEffectDefinition.Heal(
                        DRPGActionEffectTarget.Actor,
                        99f)
                });
            SetField(status, "displayName", "Mutated Status");
            SetField(status, "durationTurns", 99);
            SetField(status, "maxStacks", 99);
            SetField(status, "outgoingDamageMultiplier", 9f);
            SetField(status, "turnStartHpDelta", -99f);

            Require(snapshot.SkillId == "task4_frozen_basic" &&
                    snapshot.DisplayName == "Frozen Before" &&
                    snapshot.PresentationKey == "frozen.before" &&
                    Math.Abs(snapshot.SkillPower - 17f) < 0.0001f &&
                    Math.Abs(snapshot.PrepareDelay - 0.25f) < 0.0001f &&
                    Math.Abs(snapshot.RecoveryDelay - 0.5f) < 0.0001f &&
                    snapshot.SourceFormationRows == DRPGFormationRowMask.Front &&
                    snapshot.TargetFormationRows == DRPGFormationRowMask.Rear &&
                    snapshot.TargetRule == DRPGSkillTargetRule.SingleEnemy &&
                    Math.Abs(snapshot.ManaCost - 3f) < 0.0001f &&
                    Math.Abs(snapshot.PostureCost - 4f) < 0.0001f &&
                    snapshot.ImpactDamageMultipliers.Count == 2 &&
                    Math.Abs(snapshot.ImpactDamageMultipliers[0] - 0.4f) < 0.0001f &&
                    snapshot.Effects.Count == 2 &&
                    snapshot.Effects[0].Kind == DRPGActionEffectKind.Damage &&
                    Math.Abs(snapshot.Effects[0].Magnitude - 0.75f) < 0.0001f &&
                    snapshot.Effects[1].Kind ==
                        DRPGActionEffectKind.ApplyStatus &&
                    snapshot.Effects[1].Status.StatusId == "frozen_status" &&
                    snapshot.Effects[1].Status.DisplayName == "Frozen Status" &&
                    snapshot.Effects[1].Status.DurationTurns == 3 &&
                    snapshot.Effects[1].Status.MaxStacks == 4 &&
                    Math.Abs(
                        snapshot.Effects[1].Status.OutgoingDamageMultiplier -
                        1.25f) < 0.0001f &&
                    Math.Abs(snapshot.Effects[1].Status.TurnStartHpDelta + 2f) <
                        0.0001f &&
                    snapshot.ActorSide == DRPGBattleSide.Player &&
                    snapshot.TargetSide == DRPGBattleSide.Enemy &&
                    snapshot.ActorFacingSign == 1 &&
                    snapshot.TargetFacingSign == -1 &&
                    snapshot.ActorHomePosition.IsSpecified &&
                    snapshot.ActorConflictPosition.IsSpecified &&
                    snapshot.TargetHomePosition.IsSpecified &&
                    snapshot.TargetConflictPosition.IsSpecified &&
                    snapshot.ActorVisualBaseline.LocalScale.IsSpecified &&
                    snapshot.TargetVisualBaseline.LocalScale.IsSpecified &&
                    Math.Abs(snapshot.Timing.AnnouncementSeconds - 0.75f) < 0.0001f &&
                    Math.Abs(snapshot.Timing.WindupMinimumSeconds - 0.30f) < 0.0001f &&
                    Math.Abs(snapshot.Timing.ReturnSeconds - 0.38f) < 0.0001f &&
                    snapshot.Semantics.ActionName == "Frozen Before" &&
                    snapshot.Semantics.PresentationKey == "frozen.before" &&
                    snapshot.ProjectionCoverage.Count == 2 &&
                    snapshot.ActorWasAlive && snapshot.TargetWasAlive &&
                    snapshot.TargetWasDeployed && snapshot.TargetWasLegal,
                "The locked Basic Attack snapshot still reads mutable skill data.");
            Require(typeof(DRPGBasicAttackFrozenSnapshot).GetProperty(
                    "Skill",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic) == null,
                "The locked Basic Attack snapshot retained a mutable skill alias.");
            string[] requiredPresentationMembers =
            {
                "Identity",
                "TargetSide",
                "TargetHomePosition",
                "TargetConflictPosition",
                "ActorFacingSign",
                "TargetFacingSign",
                "ActorVisualBaseline",
                "TargetVisualBaseline",
                "Timing",
                "Semantics",
                "ProjectionCoverage"
            };
            for (int i = 0; i < requiredPresentationMembers.Length; i++)
            {
                Require(typeof(DRPGBasicAttackFrozenSnapshot).GetProperty(
                        requiredPresentationMembers[i],
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic) != null,
                    "The locked Basic Attack snapshot is missing " +
                    requiredPresentationMembers[i] + ".");
            }

            SetField(runtimeSkill, "skillId", "task4_runtime_frozen_basic");
            SetField(runtimeSkill, "displayName", "Runtime Frozen");
            SetField(runtimeSkill, "presentationKey", "runtime.frozen");
            SetField(runtimeSkill, "targetRule", DRPGSkillTargetRule.SingleEnemy);
            SetField(runtimeSkill, "sourceFormationRows", DRPGFormationRowMask.All);
            SetField(runtimeSkill, "targetFormationRows", DRPGFormationRowMask.All);
            SetField(runtimeSkill, "power", 10f);
            SetField(runtimeSkill, "manaCost", 10f);
            SetField(runtimeSkill, "effects",
                new List<DRPGActionEffectDefinition>
                {
                    DRPGActionEffectDefinition.Damage(
                        DRPGActionEffectTarget.PrimaryTarget,
                        1f,
                        DRPGDamageTag.Slash)
                });
            var resolvedModifier = new DRPGActionSpecModifierDefinition();
            SetField(resolvedModifier, "modifierId", "task4_runtime_contribution");
            SetField(resolvedModifier, "skillPowerAdditive", 5f);
            SetField(resolvedModifier, "skillPowerMultiplier", 2f);
            SetField(resolvedModifier, "outgoingDamageMultiplier", 1.5f);
            SetField(resolvedModifier, "manaCostAdditive", 2f);
            SetField(resolvedModifier, "manaCostMultiplier", 0.5f);
            SetField(resolvedModifier, "addDamageTags", DRPGDamageTag.Frost);
            SetField(runtimePassive, "modifierId", "task4_runtime_passive");
            SetField(runtimePassive, "actionModifiers",
                new List<DRPGActionSpecModifierDefinition> { resolvedModifier });
            var runtimeBuild = new DRPGResolvedCombatantBuild(
                playerProfile,
                null,
                runtimeSkill,
                new DRPGSkillSO[DRPGSkillLoadout.SlotCount],
                Array.Empty<DRPGSkillSO>(),
                new[] { runtimePassive });
            BattleParticipantRuntime player = new BattleParticipantRuntime(
                9471,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0,
                    runtimeBuild));
            BattleParticipantRuntime reinforcement = CreateParticipantWithBasic(
                9472,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                runtimeSkill);
            DRPGBattleSessionComposition composition =
                DRPGBattleSessionComposition.Build(
                    new[] { player },
                    new[] { reinforcement },
                    new DRPGTurnScheduler(),
                    DRPGProcessBattleGenerationSource.Instance,
                    DRPGBattleTransactionPolicy.StandardBasicAttackTest,
                    new DRPGBattleProgressPipeline());
            composition.State.InitializeParticipantDeployment(
                new ParticipantId(reinforcement.RuntimeId),
                true);
            var port = new PresentationPortStub
            {
                FrozenInput = CreatePresentationFreezeInput(
                    true,
                    "runtime.frozen.before",
                    2f)
            };
            Require(composition.PresentationPortSlot.TryBind(
                    composition.BattleGeneration,
                    port) == PresentationPortBindResult.Bound,
                "The real runtime freeze fixture could not bind presentation input.");
            composition.SelectionState.SetBattleAvailable(true);
            ActorTurnToken playerTurn = composition.IdentityAuthority.AllocateTurn(
                new ParticipantId(player.RuntimeId));
            composition.SelectionState.PublishTurn(playerTurn);
            TargetSelectionOpenResult playerOpen =
                composition.ActionExecutionRouter.TryOpenBasicAttackSelection(
                    playerTurn);
            Require(playerOpen.IsAccepted,
                "The player could not open selection for a deployed reinforcement.");
            Require(composition.SelectionState.TryResolveBasicAttackSpec(
                    playerTurn,
                    new ParticipantId(reinforcement.RuntimeId),
                    runtimeSkill,
                    out DRPGResolvedActionSpec playerSpec,
                    out BasicAttackRejection playerSpecRejection),
                "The player Basic Attack spec did not resolve: " +
                playerSpecRejection);
            Require(composition.SelectionState.TryFreezeBasicAttackSnapshot(
                    playerTurn,
                    playerOpen.Selection,
                    new ParticipantId(reinforcement.RuntimeId),
                    runtimeSkill,
                    playerSpec,
                    out DRPGBasicAttackFrozenSnapshot runtimeSnapshot,
                    out BasicAttackRejection runtimeRejection),
                "The real runtime snapshot could not freeze configured input: " +
                runtimeRejection);
            ActionIdentity runtimeIdentity =
                composition.IdentityAuthority.AllocateAction(playerTurn);
            runtimeSnapshot = runtimeSnapshot.WithIdentity(runtimeIdentity);
            port.FrozenInput = CreatePresentationFreezeInput(
                true,
                "runtime.frozen.after",
                99f);
            SetField(runtimeSkill, "presentationKey", "runtime.mutated.after");
            Require(runtimeSnapshot.Identity == runtimeIdentity &&
                    Math.Abs(runtimeSnapshot.ActorHomePosition.X + 6f) <
                        0.0001f &&
                    Math.Abs(runtimeSnapshot.ActorHomePosition.Y - 2f) <
                        0.0001f &&
                    Math.Abs(runtimeSnapshot.TargetHomePosition.X - 6f) <
                        0.0001f &&
                    Math.Abs(runtimeSnapshot.TargetHomePosition.Y - 2f) <
                        0.0001f &&
                    Math.Abs(runtimeSnapshot.ActorVisualBaseline.Red - 0.8f) <
                        0.0001f &&
                    Math.Abs(runtimeSnapshot.Timing.ReturnSeconds - 0.38f) <
                        0.0001f &&
                    runtimeSnapshot.Semantics.PresentationKey ==
                        "runtime.frozen.before" &&
                    Math.Abs(runtimeSnapshot.SkillPower - 30f) < 0.0001f &&
                    Math.Abs(runtimeSnapshot.ActorDamageMultiplier - 1.5f) <
                        0.0001f &&
                    Math.Abs(runtimeSnapshot.ManaCost - 6f) < 0.0001f &&
                    runtimeSnapshot.Effects.Count == 1 &&
                    Math.Abs(runtimeSnapshot.Effects[0].Magnitude - 30f) <
                        0.0001f &&
                    runtimeSnapshot.Effects[0].DamageTags ==
                        (DRPGDamageTag.Slash | DRPGDamageTag.Frost) &&
                    runtimeSnapshot.ContributionTrace.Count == 1 &&
                    runtimeSnapshot.ProjectionCoverage.Count == 2,
                "The runtime lock did not freeze the authoritative resolved action spec.");
            composition.ActionExecutionRouter.TryCancel(playerOpen.Selection);

            port.FrozenInput = CreatePresentationFreezeInput(
                false,
                "runtime.enemy.before",
                3f);
            ActorTurnToken enemyTurn = composition.IdentityAuthority.AllocateTurn(
                new ParticipantId(reinforcement.RuntimeId));
            composition.SelectionState.PublishTurn(enemyTurn);
            TargetSelectionOpenResult enemyOpen =
                composition.ActionExecutionRouter.TryOpenBasicAttackSelection(
                    enemyTurn);
            DRPGBasicAttackFrozenSnapshot enemySnapshot = default;
            BasicAttackRejection enemyRejection = BasicAttackRejection.None;
            bool enemyFrozen = enemyOpen.IsAccepted &&
                composition.SelectionState.TryResolveBasicAttackSpec(
                        enemyTurn,
                        new ParticipantId(player.RuntimeId),
                        runtimeSkill,
                        out DRPGResolvedActionSpec enemySpec,
                        out enemyRejection) &&
                composition.SelectionState.TryFreezeBasicAttackSnapshot(
                        enemyTurn,
                        enemyOpen.Selection,
                        new ParticipantId(player.RuntimeId),
                        runtimeSkill,
                        enemySpec,
                        out enemySnapshot,
                        out enemyRejection);
            Require(enemyFrozen &&
                    enemySnapshot.ActorSide == DRPGBattleSide.Enemy &&
                    enemySnapshot.TargetSide == DRPGBattleSide.Player &&
                    enemySnapshot.ProjectionCoverage.Count == 2,
                "A deployed reserve could not freeze the same Basic Attack " +
                "coverage as an initial participant: " + enemyRejection);
            port.FrozenInput = CreatePresentationFreezeInput(
                false,
                "runtime.invalid-duration",
                4f,
                0f);
            Require(composition.SelectionState.TryResolveBasicAttackSpec(
                    enemyTurn,
                    new ParticipantId(player.RuntimeId),
                    runtimeSkill,
                    out DRPGResolvedActionSpec shortTimingSpec,
                    out _),
                "The short-timing fixture could not resolve its action spec.");
            Require(!composition.SelectionState.TryFreezeBasicAttackSnapshot(
                    enemyTurn,
                    enemyOpen.Selection,
                    new ParticipantId(player.RuntimeId),
                    runtimeSkill,
                    shortTimingSpec,
                    out _,
                    out BasicAttackRejection shortTimingRejection) &&
                    shortTimingRejection ==
                        BasicAttackRejection.PresentationUnavailable,
                "A presentation snapshot with a nonpositive Return " +
                "duration was accepted.");
            composition.ActionExecutionRouter.TryCancel(enemyOpen.Selection);

            port.FrozenInput = CreatePresentationFreezeInput(
                true,
                "runtime.reentrant",
                5f);
            ActorTurnToken reentrantTurn = composition.IdentityAuthority.AllocateTurn(
                new ParticipantId(player.RuntimeId));
            composition.SelectionState.PublishTurn(reentrantTurn);
            TargetSelectionOpenResult reentrantOpen =
                composition.ActionExecutionRouter.TryOpenBasicAttackSelection(
                    reentrantTurn);
            bool captureCancelledSelection = false;
            port.OnCapture = () =>
            {
                captureCancelledSelection = composition.ActionExecutionRouter
                    .TryCancel(reentrantOpen.Selection).IsCancelled;
            };
            RootActionStartResult reentrantStart =
                composition.ActionExecutionRouter.TryStart(
                    new BasicAttackSelection(
                        reentrantOpen.Selection,
                        new ParticipantId(reinforcement.RuntimeId)));
            Require(reentrantStart.IsAccepted && !captureCancelledSelection,
                "Presentation capture reentrantly cancelled a selection while " +
                "its promotion was locking the action.");
            Require(!composition.Arbiter.HasOwner,
                "The dormant Standard action did not release its mutation lease.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
            UnityEngine.Object.DestroyImmediate(runtimeSkill);
            UnityEngine.Object.DestroyImmediate(runtimePassive);
            UnityEngine.Object.DestroyImmediate(status);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }
    }

    internal static void PresentationPortSlotLifecycle()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            SetField(skill, "skillId", "task4_slot_basic");
            var slot = new DRPGBattleTransactionPresentationPortSlot(9405L);
            var firstPort = new PresentationPortStub();
            var secondPort = new PresentationPortStub();
            Require(slot.TryBind(9404L, firstPort) ==
                    PresentationPortBindResult.InvalidGeneration,
                "A presentation port bound to the wrong battle generation.");
            Require(slot.TryBind(9405L, firstPort) ==
                    PresentationPortBindResult.Bound &&
                    slot.TryBind(9405L, firstPort) ==
                    PresentationPortBindResult.AlreadyBoundToSamePort &&
                    slot.TryBind(9405L, secondPort) ==
                    PresentationPortBindResult.Busy,
                "The presentation slot did not enforce one bound instance.");
            Require(slot.TryUnbind(9405L, secondPort) ==
                    PresentationPortUnbindResult.StalePort &&
                    slot.TryGet(9405L, out IStandardBasicAttackPresentationPort bound) &&
                    ReferenceEquals(bound, firstPort),
                "A stale presentation port unbound the active adapter.");
            Require(slot.TryUnbind(9404L, firstPort) ==
                    PresentationPortUnbindResult.InvalidGeneration &&
                    slot.TryUnbind(9405L, firstPort) ==
                    PresentationPortUnbindResult.Unbound &&
                    !slot.TryGet(9405L, out _),
                "The matching generation and port did not own unbind.");

            var identities = new DRPGBattleIdentityAuthority(9405L);
            ActorTurnToken turn = identities.AllocateTurn(new ParticipantId(81));
            TargetSelectionToken selection = identities.AllocateSelection(turn);
            var selectionAuthority = new SelectionAuthorityStub(
                new DRPGBasicAttackFrozenSnapshot(
                    turn,
                    selection,
                    new ParticipantId(82),
                    skill));
            var trace = new DRPGBattleActionTraceWriter(
                new DRPGBoundedActionTraceSink(64));
            var arbiter = new DRPGBattleTransactionArbiter(9405L, trace);
            var module = new DRPGStandardBasicAttackTransactionModule(slot);
            var router = new DRPGBattleActionExecutionRouter(
                DRPGBattleTransactionPolicy.StandardBasicAttackTest,
                selectionAuthority,
                new LegacyRunnerStub(),
                module,
                identities,
                arbiter);

            RootActionStartResult unavailable = router.TryStart(
                new BasicAttackSelection(selection, new ParticipantId(82)));
            Require(!unavailable.IsAccepted &&
                    unavailable.Rejection ==
                        BasicAttackRejection.PresentationUnavailable &&
                    !arbiter.HasOwner &&
                    selectionAuthority.ConsumeCount == 0,
                "Missing presentation consumed selection, ActionId or ownership.");
            Require(slot.TryBind(9405L, firstPort) ==
                    PresentationPortBindResult.Bound,
                "The production adapter could not bind after a rejected start.");
            RootActionStartResult available = router.TryStart(
                new BasicAttackSelection(selection, new ParticipantId(82)));
            Require(available.IsAccepted && available.Receipt.Identity.ActionId == 1L,
                "A rejected presentation start consumed ActionId or selection state.");

            var lostIds = new DRPGBattleIdentityAuthority(9415L);
            ActorTurnToken lostTurn = lostIds.AllocateTurn(new ParticipantId(91));
            TargetSelectionToken lostSelection = lostIds.AllocateSelection(lostTurn);
            var lostTrace = new DRPGBattleActionTraceWriter(
                new LosslessActionTraceSink());
            var lostArbiter = new DRPGBattleTransactionArbiter(9415L, lostTrace);
            var lostSlot = new DRPGBattleTransactionPresentationPortSlot(9415L);
            var lostPort = new PresentationPortStub();
            lostSlot.TryBind(9415L, lostPort);
            var lostAuthority = new SelectionAuthorityStub(
                new DRPGBasicAttackFrozenSnapshot(
                    lostTurn,
                    lostSelection,
                    new ParticipantId(92),
                    skill),
                lostTrace);
            var lostRouter = new DRPGBattleActionExecutionRouter(
                DRPGBattleTransactionPolicy.StandardBasicAttackTest,
                lostAuthority,
                new LegacyRunnerStub(),
                new UnbindingStandardRunner(
                    new DRPGStandardBasicAttackTransactionModule(lostSlot),
                    lostSlot,
                    lostPort,
                    9415L),
                lostIds,
                lostArbiter);
            RootActionStartResult lostAfterLock = lostRouter.TryStart(
                new BasicAttackSelection(lostSelection, new ParticipantId(92)));
            Require(lostAfterLock.IsAccepted &&
                    lostAfterLock.Receipt.Completion.IsCompleted &&
                    lostAfterLock.Receipt.Completion.Result.Kind ==
                        RootActionTerminalKind.Faulted &&
                    lostAuthority.ConsumeCount == 1 &&
                    !lostArbiter.HasOwner,
                "A presentation port lost after lock returned a pre-lock rejection " +
                "or leaked its mutation lease.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    internal static void ProductionLegacyActionKeepsOneLease()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(100f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(1f);
        DRPGCombatantProfileSO otherProfile = CreateProfile(1f);
        try
        {
            SetField(attack, "skillId", "task4_legacy_lease_basic");
            SetField(attack, "power", 1f);
            SetField(attack, "targetRule", DRPGSkillTargetRule.SingleEnemy);
            BattleParticipantRuntime player = CreateParticipantWithBasic(
                9411,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                attack);
            BattleParticipantRuntime enemy = CreateParticipantWithBasic(
                9412,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                attack);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            session.Start();
            Require(ReferenceEquals(session.CurrentActor, player),
                "The Legacy lease fixture did not begin on the player actor.");
            Require(session.TryPrepareCommand(
                    DRPGBattleCommand.BasicAttack(
                        player.RuntimeId,
                        enemy.RuntimeId,
                        attack),
                    out DRPGBattlePreparedAction action,
                    out string error),
                error);
            Require(action.Identity == session.CurrentActionIdentity &&
                    action.LegacyMutationPort != null &&
                    action.LegacyMutationPort.IsActive &&
                    action.BasicAttackSnapshot.HasValue &&
                    action.BasicAttackSnapshot.Value.Identity == action.Identity &&
                    session.TransactionArbiter.CurrentOwner ==
                        BattleTransactionIdentity.ForLegacyAction(action.Identity),
                "Legacy preparation did not freeze one action identity and owner.");
            long leaseSerial = action.LegacyMutationPort.LeaseSerial;
            long revisionBefore = session.TransactionState.BattleRevision;
            Require(!session.TryCommitTestHarnessMutation(
                    workspace => workspace.For(
                            new ParticipantId(enemy.RuntimeId))
                        .SetHitPoints(0f),
                    out _,
                    out _ ) &&
                    session.TransactionState.BattleRevision == revisionBefore,
                "A TestHarness mutation ran while the Legacy action owned rules.");
            Require(!session.TryPrepareCommand(
                    DRPGBattleCommand.Wait(player.RuntimeId),
                    out _,
                    out _),
                "A second action prepared while the Legacy owner was active.");

            BattleParticipantRuntime other = CreateParticipantWithBasic(
                9413,
                otherProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                attack);
            var wrongSession = DRPGBattleHeadlessSessionFactory.Create(new[] { other });
            Require(!wrongSession.TryResolveImpact(action, out _, out _),
                "A different Session advanced the prepared Legacy action.");
            Require(!action.ImpactLedger.IsComplete,
                "The wrong-Session facade mutated the original action ledger.");

            bool observerMutationAttempted = false;
            bool observerMutationRejected = false;
            float playerHpBeforeObserver = player.CurrentHp;
            session.ParticipantChanged += changed =>
            {
                if (observerMutationAttempted || !ReferenceEquals(changed, enemy))
                    return;
                observerMutationAttempted = true;
                try
                {
                    player.ApplyDamage(1f);
                }
                catch (InvalidOperationException)
                {
                    observerMutationRejected = true;
                }
            };
            Require(session.TryResolveImpact(action, out _, out error), error);
            Require(observerMutationAttempted && observerMutationRejected &&
                    Math.Abs(player.CurrentHp - playerHpBeforeObserver) < 0.0001f &&
                    session.TransactionState.BattleRevision == revisionBefore,
                "A synchronous observer borrowed the Legacy compatibility scope.");
            Require(action.LegacyMutationPort.IsActive &&
                    action.LegacyMutationPort.LeaseSerial == leaseSerial &&
                    session.TransactionArbiter.CurrentOwner ==
                        BattleTransactionIdentity.ForLegacyAction(action.Identity),
                "Resolve released or replaced the Legacy action lease.");
            Require(session.TryCommitAction(action, out error), error);
            Require(action.LegacyMutationPort.IsActive &&
                    action.LegacyMutationPort.LeaseSerial == leaseSerial,
                "Commit introduced an owner gap before finalization.");

            int completed = 0;
            session.ActionCompleted += _ => completed++;
            Require(session.TryFinalizeCommittedAction(action, out error), error);
            Require(completed == 1 && !action.LegacyMutationPort.IsActive &&
                    !session.TransactionArbiter.HasOwner,
                "Finalization did not release the one Legacy lease exactly once.");
            Require(!session.TryFinalizeCommittedAction(action, out _ ) &&
                    completed == 1,
                "Duplicate Legacy finalization was accepted.");

            BattleParticipantRuntime abortPlayer = CreateParticipantWithBasic(
                9414,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                attack);
            BattleParticipantRuntime abortEnemy = CreateParticipantWithBasic(
                9415,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                attack);
            var abortSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { abortPlayer, abortEnemy });
            abortSession.Start();
            Require(abortSession.TryPrepareCommand(
                    DRPGBattleCommand.BasicAttack(
                        abortPlayer.RuntimeId,
                        abortEnemy.RuntimeId,
                        attack),
                    out DRPGBattlePreparedAction abortedAction,
                    out error),
                error);
            Require(abortedAction.LegacyMutationPort.IsActive,
                "The abort fixture did not acquire its Legacy lease.");
            abortSession.Abort();
            Require(!abortedAction.LegacyMutationPort.IsActive &&
                    !abortSession.TransactionArbiter.HasOwner &&
                    abortSession.TransactionArbiter.IsCommandInputOpen,
                "Battle abort leaked the prepared Legacy action lease.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(otherProfile);
        }
    }

    internal static void TransactionModulesDoNotDependOnSessionFacade()
    {
        Type[] modules =
        {
            typeof(DRPGBattleActionExecutionRouter),
            typeof(DRPGStandardBasicAttackTransactionModule),
            typeof(DRPGBasicAttackSelectionRuntimeState),
            typeof(DRPGLegacyActionRunnerAdapter),
            typeof(DRPGLegacyActionMutationPort),
            typeof(DRPGBattleSessionComposition)
        };
        for (int i = 0; i < modules.Length; i++)
            RequireTypeSurfaceDoesNotReferenceSession(modules[i]);
    }

    internal static void CompositionIsCompleteBeforeEscape()
    {
        DRPGCombatantProfileSO firstProfile = CreateProfile(20f);
        DRPGCombatantProfileSO secondProfile = CreateProfile(10f);
        try
        {
            BattleParticipantRuntime directPlayer = CreateParticipant(
                9421,
                firstProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime directEnemy = CreateParticipant(
                9422,
                secondProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var direct = new DRPGBattleSession(
                new[] { directPlayer, directEnemy });
            RequireCompleteComposition(direct);
            Require(!direct.TransactionPresentationPortSlot.TryGet(direct.BattleGeneration, out _),
                "The production constructor must not install a headless immediate fallback.");

            BattleParticipantRuntime factoryPlayer = CreateParticipant(
                9423,
                firstProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime factoryEnemy = CreateParticipant(
                9424,
                secondProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            DRPGBattleSession factory = DRPGBattleSessionFactory.CreateSession(
                DRPGBattleSessionCreationResult.Success(
                    Array.AsReadOnly(new[] { factoryPlayer, factoryEnemy })));
            RequireCompleteComposition(factory);

            BattleParticipantRuntime first = CreateParticipant(
                9425,
                firstProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime duplicate = CreateParticipant(
                9425,
                secondProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            RequireThrows<ArgumentException>(
                () => new DRPGBattleSession(new[] { first, duplicate }),
                "An invalid constructor escaped a partial Session.");
            float before = first.CurrentHp;
            first.ApplyDamage(1f);
            Require(Math.Abs(first.CurrentHp - (before - 1f)) < 0.0001f,
                "A failed Session construction partially attached a participant.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstProfile);
            UnityEngine.Object.DestroyImmediate(secondProfile);
        }
    }

    internal static void DirectSessionConstructionStillUsesSingleArbiter()
    {
        DRPGCombatantProfileSO playerProfile = CreateProfile(20f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(10f);
        try
        {
            var session = new DRPGBattleSession(new[]
            {
                CreateParticipant(9431, playerProfile, DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist),
                CreateParticipant(9432, enemyProfile, DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy)
            });
            object stateArbiter = GetPrivateField(
                session.TransactionState,
                "arbiter");
            object routerArbiter = GetPrivateField(
                session.ActionExecutionRouter,
                "arbiter");
            Require(ReferenceEquals(stateArbiter, session.TransactionArbiter) &&
                    ReferenceEquals(routerArbiter, session.TransactionArbiter),
                "Direct Session construction created more than one arbiter.");
            Require(session.TransactionState.BattleGeneration ==
                    session.BattleGeneration,
                "The State and Session do not share one battle generation.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }
    }

    private static BattleParticipantRuntime CreateParticipantWithBasic(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        DRPGSkillSO basicAttack)
    {
        var loadout = new DRPGSkillSO[DRPGSkillLoadout.SlotCount];
        var build = new DRPGResolvedCombatantBuild(
            profile,
            null,
            basicAttack,
            loadout,
            Array.Empty<DRPGSkillSO>(),
            Array.Empty<DRPGPassiveModifierSO>());
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, 0, build));
    }

    private static DRPGBasicAttackPresentationFreezeInput
        CreatePresentationFreezeInput(
            bool playerActor,
            string presentationKey,
            float verticalOffset,
            float returnSeconds = 0.38f)
    {
        float actorX = playerActor ? -6f : 6f;
        float targetX = -actorX;
        float actorConflictX = playerActor ? -1.2f : 1.2f;
        float targetConflictX = -actorConflictX;
        float baselineRed = verticalOffset < 10f ? 0.8f : 0.2f;
        return new DRPGBasicAttackPresentationFreezeInput(
            new ActionTracePosition(actorX, verticalOffset, 0f),
            new ActionTracePosition(actorConflictX, 0.4f, 0f),
            new ActionTracePosition(targetX, verticalOffset, 0f),
            new ActionTracePosition(targetConflictX, 0.4f, 0f),
            playerActor ? 1 : -1,
            playerActor ? -1 : 1,
            new DRPGBasicAttackVisualBaseline(
                new ActionTracePosition(1f, 1f, 1f),
                baselineRed,
                0.7f,
                0.6f,
                1f,
                "ActorIdle"),
            new DRPGBasicAttackVisualBaseline(
                new ActionTracePosition(1f, 1f, 1f),
                0.6f,
                0.7f,
                baselineRed,
                1f,
                "TargetIdle"),
            new DRPGBasicAttackTimingSnapshot(
                0.80f,
                0.14f,
                0.32f,
                0.10f,
                0.18f,
                returnSeconds,
                0.48f,
                0.31f),
            new DRPGBasicAttackSemanticSnapshot(
                "普通攻击",
                EAnimationLogicState.Skill_1,
                presentationKey,
                presentationKey + ".audio",
                presentationKey + ".vfx"));
    }

    private static void RequireCompleteComposition(DRPGBattleSession session)
    {
        Require(session != null && session.TransactionState != null &&
                session.TransactionArbiter != null &&
                session.ActionExecutionRouter != null &&
                session.TransactionPresentationPortSlot != null,
            "A Session escaped before its transaction composition was complete.");
        object selectionState = GetPrivateField(
            session,
            "basicAttackSelectionState");
        object selectionAuthority = GetPrivateField(
            session.ActionExecutionRouter,
            "selectionAuthority");
        object routerSelectionState = GetPrivateField(
            selectionAuthority,
            "state");
        object legacyRunner = GetPrivateField(
            session.ActionExecutionRouter,
            "legacyRunner");
        Require(ReferenceEquals(selectionState, routerSelectionState) &&
                legacyRunner is DRPGLegacyActionRunnerAdapter,
            "The composition did not build the shared selection and Legacy runners.");
        System.Reflection.FieldInfo[] legacyFields = legacyRunner.GetType()
            .GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public);
        for (int i = 0; i < legacyFields.Length; i++)
        {
            Require(!typeof(Delegate).IsAssignableFrom(legacyFields[i].FieldType) &&
                    legacyFields[i].FieldType != typeof(DRPGBattleSession),
                "The composition retained a Session/delegate capture in Legacy runner.");
        }
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException("Missing field " + fieldName + ".");
        return field.GetValue(target);
    }

    private static void RequireTypeSurfaceDoesNotReferenceSession(Type type)
    {
        var referenced = new List<Type>();
        foreach (System.Reflection.FieldInfo field in type.GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public))
        {
            referenced.Add(field.FieldType);
        }
        foreach (System.Reflection.ConstructorInfo constructor in
            type.GetConstructors(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public))
        {
            System.Reflection.ParameterInfo[] parameters =
                constructor.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
                referenced.Add(parameters[i].ParameterType);
        }
        foreach (System.Reflection.MethodInfo method in type.GetMethods(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.DeclaredOnly))
        {
            referenced.Add(method.ReturnType);
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
                referenced.Add(parameters[i].ParameterType);
        }
        for (int i = 0; i < referenced.Count; i++)
        {
            Require(referenced[i] != typeof(DRPGBattleSession),
                type.Name + " exposes a Session-facade dependency.");
        }
    }

    private sealed class SelectionStateStub : IDRPGBasicAttackSelectionState
    {
        internal SelectionStateStub(
            ActorTurnToken currentTurn,
            ParticipantId validTarget,
            DRPGSkillSO basicAttack)
        {
            CurrentTurn = currentTurn;
            ValidTarget = validTarget;
            BasicAttack = basicAttack;
        }

        public ActorTurnToken CurrentTurn { get; set; }
        public bool IsBattleEnded { get; set; }
        public bool IsActionBusy { get; set; }
        internal ParticipantId ValidTarget { get; set; }
        internal DRPGSkillSO BasicAttack { get; set; }
        internal bool IsPlayerActor { get; set; } = true;

        public bool TryValidateBasicAttackActor(
            in ActorTurnToken turn,
            out DRPGSkillSO basicAttack,
            out BasicAttackRejection rejection)
        {
            basicAttack = null;
            if (IsBattleEnded)
            {
                rejection = BasicAttackRejection.BattleEnded;
                return false;
            }
            if (IsActionBusy)
            {
                rejection = BasicAttackRejection.Busy;
                return false;
            }
            if (turn != CurrentTurn)
            {
                rejection = turn.Actor == CurrentTurn.Actor
                    ? BasicAttackRejection.StaleTurnToken
                    : BasicAttackRejection.NotCurrentActor;
                return false;
            }
            if (!IsPlayerActor)
            {
                rejection = BasicAttackRejection.WrongSide;
                return false;
            }
            if (BasicAttack == null)
            {
                rejection = BasicAttackRejection.BasicAttackUnavailable;
                return false;
            }
            basicAttack = BasicAttack;
            rejection = BasicAttackRejection.None;
            return true;
        }

        public bool TryResolveBasicAttackSpec(
            in ActorTurnToken turn,
            ParticipantId target,
            DRPGSkillSO basicAttack,
            out DRPGResolvedActionSpec resolvedSpec,
            out BasicAttackRejection rejection)
        {
            resolvedSpec = null;
            bool valid = turn == CurrentTurn && target == ValidTarget &&
                ReferenceEquals(basicAttack, BasicAttack);
            rejection = valid
                ? BasicAttackRejection.None
                : BasicAttackRejection.IllegalTarget;
            return valid;
        }

        public bool TryFreezeBasicAttackSnapshot(
            in ActorTurnToken turn,
            in TargetSelectionToken selection,
            ParticipantId target,
            DRPGSkillSO basicAttack,
            DRPGResolvedActionSpec resolvedSpec,
            out DRPGBasicAttackFrozenSnapshot snapshot,
            out BasicAttackRejection rejection)
        {
            if (!TryResolveBasicAttackSpec(
                    turn,
                    target,
                    basicAttack,
                    out _,
                    out rejection))
            {
                snapshot = default;
                return false;
            }
            DRPGBattleSide actorSide = IsPlayerActor
                ? DRPGBattleSide.Player
                : DRPGBattleSide.Enemy;
            DRPGBattleSide targetSide = IsPlayerActor
                ? DRPGBattleSide.Enemy
                : DRPGBattleSide.Player;
            snapshot = new DRPGBasicAttackFrozenSnapshot(
                turn,
                selection,
                target,
                basicAttack,
                actorSide,
                targetSide,
                new ActionTracePosition(1f, 2f, 0f),
                new ActionTracePosition(
                    IsPlayerActor ? -0.5f : 0.5f,
                    0f,
                    0f),
                new ActionTracePosition(3f, 4f, 0f),
                new ActionTracePosition(
                    IsPlayerActor ? 0.5f : -0.5f,
                    0f,
                    0f),
                IsPlayerActor ? 1 : -1,
                IsPlayerActor ? -1 : 1,
                DRPGBasicAttackVisualBaseline.DefaultIdle,
                DRPGBasicAttackVisualBaseline.DefaultIdle,
                DRPGBasicAttackTimingSnapshot.ApprovedDefaults,
                new DRPGBasicAttackSemanticSnapshot(
                    basicAttack.DisplayName,
                    basicAttack.AnimationIntent,
                    basicAttack.PresentationKey,
                    "basic.impact.audio",
                    "basic.impact.vfx"),
                new[] { turn.Actor, target },
                true,
                true,
                true,
                true);
            rejection = BasicAttackRejection.None;
            return true;
        }

        public bool TryGetBasicAttackTraceContext(
            in ActorTurnToken turn,
            out DRPGBattleSide side,
            out ActionTracePosition homePosition)
        {
            if (turn != CurrentTurn)
            {
                side = default;
                homePosition = default;
                return false;
            }
            side = IsPlayerActor
                ? DRPGBattleSide.Player
                : DRPGBattleSide.Enemy;
            homePosition = new ActionTracePosition(1f, 2f, 0f);
            return true;
        }
    }

    private sealed class SelectionAuthorityStub :
        IDRPGBasicAttackSelectionAuthority
    {
        private readonly DRPGBasicAttackFrozenSnapshot snapshot;
        private readonly DRPGBattleActionTraceWriter trace;

        internal SelectionAuthorityStub(
            DRPGBasicAttackFrozenSnapshot snapshot,
            DRPGBattleActionTraceWriter trace = null)
        {
            this.snapshot = snapshot;
            this.trace = trace;
        }

        internal int ConsumeCount { get; private set; }
        internal int RestoreCount { get; private set; }

        public TargetSelectionOpenResult TryOpen(in ActorTurnToken turn)
        {
            return TargetSelectionOpenResult.Accepted(snapshot.Selection);
        }

        public TargetSelectionCancelResult TryCancel(
            in TargetSelectionToken selection)
        {
            return TargetSelectionCancelResult.Cancelled();
        }

        public bool TryFreeze(
            in BasicAttackSelection selection,
            out DRPGBasicAttackFrozenSnapshot frozen,
            out BasicAttackRejection rejection)
        {
            frozen = snapshot;
            rejection = BasicAttackRejection.None;
            return true;
        }

        public bool TryPromote(
            in BasicAttackSelection selection,
            Func<DRPGBasicAttackFrozenSnapshot, RootActionOwnershipResult> promote,
            out DRPGBasicAttackFrozenSnapshot frozen,
            out RootActionOwnershipResult ownership,
            out DRPGBasicAttackTraceCorrelation traceCorrelation,
            out BasicAttackRejection rejection)
        {
            frozen = snapshot;
            ownership = promote(snapshot);
            traceCorrelation = ownership.IsAccepted
                ? RecordTrace()
                : default;
            rejection = ownership.IsAccepted
                ? BasicAttackRejection.None
                : BasicAttackRejection.Busy;
            if (ownership.IsAccepted)
                Consume(selection.Selection);
            return ownership.IsAccepted;
        }

        public bool TryConfirmLegacy(
            in BasicAttackSelection selection,
            out DRPGBasicAttackFrozenSnapshot frozen,
            out DRPGBasicAttackTraceCorrelation traceCorrelation,
            out BasicAttackRejection rejection)
        {
            frozen = snapshot;
            traceCorrelation = RecordTrace();
            rejection = BasicAttackRejection.None;
            Consume(selection.Selection);
            return true;
        }

        public void Restore(in TargetSelectionToken selection)
        {
            RestoreCount++;
        }

        public void Consume(in TargetSelectionToken selection)
        {
            ConsumeCount++;
        }

        private DRPGBasicAttackTraceCorrelation RecordTrace()
        {
            string turnEventId = DRPGBasicAttackTrace.TurnEventId(snapshot.Turn);
            string openedEventId = "stub-selection-opened-" +
                snapshot.Selection.BattleGeneration + "-" +
                snapshot.Selection.SelectionId;
            string confirmedEventId = "stub-selection-confirmed-" +
                snapshot.Selection.BattleGeneration + "-" +
                snapshot.Selection.SelectionId;
            if (trace != null)
            {
                DRPGBasicAttackTrace.RecordTurnBegin(
                    trace,
                    snapshot.Turn,
                    snapshot.ActorSide,
                    snapshot.ActorHomePosition);
                openedEventId = DRPGBasicAttackTrace.RecordSelectionOpened(
                    trace,
                    snapshot.Turn,
                    snapshot.Selection,
                    snapshot.ActorSide,
                    snapshot.ActorHomePosition);
                confirmedEventId = DRPGBasicAttackTrace.RecordSelectionConfirmed(
                    trace,
                    snapshot,
                    openedEventId);
            }
            return new DRPGBasicAttackTraceCorrelation(
                turnEventId,
                openedEventId,
                confirmedEventId);
        }
    }

    private sealed class LegacyRunnerStub : IDRPGLegacyActionRunner
    {
        internal int StartCount { get; private set; }

        public LegacyActionStartResult TryStartLegacy(DRPGBattleCommand command)
        {
            StartCount++;
            return LegacyActionStartResult.Rejected("probe legacy runner");
        }
    }

    private sealed class StandardRunnerStub : IDRPGStandardBasicAttackRunner
    {
        private readonly DRPGBattleActionTraceWriter trace;
        private readonly bool holdLease;
        private BattleRuleMutationLease heldLease;

        internal StandardRunnerStub(
            DRPGBattleActionTraceWriter trace,
            bool holdLease = false)
        {
            this.trace = trace;
            this.holdLease = holdLease;
        }

        internal int StartCount { get; private set; }
        internal DRPGBasicAttackFrozenSnapshot LastSnapshot { get; private set; }

        public BasicAttackRejection CheckAvailability(long battleGeneration)
        {
            return BasicAttackRejection.None;
        }

        public RootActionStartResult TryStart(
            in DRPGBasicAttackFrozenSnapshot snapshot,
            ActionIdentity identity,
            BattleRuleMutationLease lease,
            string actionLockedEventId)
        {
            StartCount++;
            LastSnapshot = snapshot;
            var receipt = new RootActionReceipt(
                identity,
                trace,
                actionLockedEventId);
            if (holdLease)
                heldLease = lease;
            else
                lease.Dispose();
            return RootActionStartResult.Accepted(receipt);
        }

        internal void ReleaseHeldLease()
        {
            heldLease?.Dispose();
            heldLease = null;
        }
    }

    private sealed class UnbindingStandardRunner : IDRPGStandardBasicAttackRunner
    {
        private readonly DRPGStandardBasicAttackTransactionModule inner;
        private readonly DRPGBattleTransactionPresentationPortSlot slot;
        private readonly IStandardBasicAttackPresentationPort port;
        private readonly long battleGeneration;

        internal UnbindingStandardRunner(
            DRPGStandardBasicAttackTransactionModule inner,
            DRPGBattleTransactionPresentationPortSlot slot,
            IStandardBasicAttackPresentationPort port,
            long battleGeneration)
        {
            this.inner = inner;
            this.slot = slot;
            this.port = port;
            this.battleGeneration = battleGeneration;
        }

        public BasicAttackRejection CheckAvailability(long generation)
        {
            BasicAttackRejection result = inner.CheckAvailability(generation);
            if (result == BasicAttackRejection.None)
                slot.TryUnbind(battleGeneration, port);
            return result;
        }

        public RootActionStartResult TryStart(
            in DRPGBasicAttackFrozenSnapshot snapshot,
            ActionIdentity identity,
            BattleRuleMutationLease lease,
            string actionLockedEventId)
        {
            return inner.TryStart(snapshot, identity, lease, actionLockedEventId);
        }
    }

    private sealed class PresentationPortStub :
        IStandardBasicAttackPresentationPort
    {
        internal DRPGBasicAttackPresentationFreezeInput FrozenInput { get; set; }
        internal Action OnCapture { get; set; }

        public bool TryCaptureFrozenSnapshot(
            in DRPGBasicAttackPresentationFreezeRequest request,
            out DRPGBasicAttackPresentationFreezeInput snapshot)
        {
            OnCapture?.Invoke();
            snapshot = FrozenInput;
            return snapshot.IsValid;
        }

        public PresentationReceipt Announce(in AnnouncementCommand command) =>
            new PresentationReceipt(command.Identity, DRPGBattleActionStage.Announcement);
        public PresentationReceipt EnterConflict(
            in ConflictCompositionCommand command) =>
            new PresentationReceipt(
                command.Identity,
                DRPGBattleActionStage.ConflictComposition);
        public PresentationReceipt PlayWindup(in BasicAttackWindupCommand command) =>
            new PresentationReceipt(command.Identity, DRPGBattleActionStage.Windup);
        public PresentationReceipt PresentImpact(
            in ImpactPresentationCommand command) =>
            new PresentationReceipt(
                command.Identity,
                DRPGBattleActionStage.ImpactAndReactions);
        public PresentationReceipt PresentReaction(
            in ReactionPresentationCommand command) =>
            new PresentationReceipt(
                command.Identity,
                DRPGBattleActionStage.ImpactAndReactions,
                command.StepOrdinal);
        public PresentationReceipt Return(in ReturnPresentationCommand command) =>
            new PresentationReceipt(command.Identity, DRPGBattleActionStage.Return);
        public PresentationReceipt PresentSettlement(
            in SettlementPresentationCommand command) =>
            new PresentationReceipt(
                command.Identity,
                DRPGBattleActionStage.Settlement,
                command.StepOrdinal);
        public PresentationCleanupReceipt EmergencyCleanup(
            in BattlePresentationCleanupCommand command) =>
            new PresentationCleanupReceipt();
    }

    private static bool TryCommitTurnCandidateWithTestHarnessLease(
        DRPGBattleSession session,
        DRPGTurnCandidateReservation reservation,
        out DRPGEnemyActionPlan enemyPlan,
        out string error)
    {
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForTestHarness(
                new BattleStartIdentity(
                    session.BattleGeneration,
                    1000000L + reservation.ReservationId));
        if (!session.TransactionArbiter.TryAcquire(
                owner,
                out BattleRuleMutationLease lease,
                out error))
        {
            enemyPlan = null;
            return false;
        }

        try
        {
            using (session.TransactionState.OpenCompatibilityMutationScope(lease))
            {
                return session.TryCommitTurnCandidate(
                    reservation,
                    out enemyPlan,
                    out error);
            }
        }
        finally
        {
            lease.Dispose();
        }
    }

    private static void WithSessionPair(
        Action<DRPGBattleSession, DRPGBattleSession> assertion)
    {
        DRPGCombatantProfileSO firstProfile = CreateProfile();
        DRPGCombatantProfileSO secondProfile = CreateProfile();
        try
        {
            var first = DRPGBattleHeadlessSessionFactory.Create(new[]
            {
                CreateParticipant(9101, firstProfile, DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist)
            });
            var second = DRPGBattleHeadlessSessionFactory.Create(new[]
            {
                CreateParticipant(9102, secondProfile, DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist)
            });
            assertion(first, second);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstProfile);
            UnityEngine.Object.DestroyImmediate(secondProfile);
        }
    }

    private static DRPGCombatantProfileSO CreateProfile(float agility = 10f)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        return profile;
    }

    private static BattleParticipantRuntime CreateParticipant(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, 0));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException("Missing field " + fieldName + ".");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(
        object target,
        string methodName,
        object argument)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        if (method == null)
            throw new InvalidOperationException("Missing method " + methodName + ".");
        method.Invoke(target, new[] { argument });
    }

    private static void SeedEnemyPlan(
        DRPGBattleSession session,
        DRPGEnemyActionPlan plan)
    {
        var field = typeof(DRPGBattleSession).GetField(
            "enemyPlans",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException("Missing enemyPlans field.");
        var plans = (Dictionary<int, DRPGEnemyActionPlan>)field.GetValue(session);
        plans.Add(plan.ActorRuntimeId, plan);
    }

    private static DRPGTurnScheduleEntry FindTimelineEntry(
        IReadOnlyList<DRPGTurnScheduleEntry> entries,
        int runtimeId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Participant.RuntimeId == runtimeId)
                return entries[i];
        }
        throw new InvalidOperationException(
            "Missing timeline entry for participant " + runtimeId + ".");
    }

    private static bool TimelineEquals(
        IReadOnlyList<DRPGTurnScheduleEntry> left,
        IReadOnlyList<DRPGTurnScheduleEntry> right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (!ReferenceEquals(left[i].Participant, right[i].Participant) ||
                Math.Abs(left[i].ScheduledTime - right[i].ScheduledTime) >
                    0.0001f ||
                left[i].Priority != right[i].Priority)
            {
                return false;
            }
        }
        return true;
    }

    private static BattleRuleMutationLease AcquireRootActionLease(
        DRPGBattleStateMutationFixture fixture,
        ActionIdentity identity)
    {
        fixture.Lease.Dispose();
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForRootAction(identity);
        Require(fixture.Arbiter.TryAcquire(
                owner,
                out BattleRuleMutationLease lease,
                out string error),
            error);
        return lease;
    }

    private static DRPGSkillSO CreateWorkspaceDamageSkill(
        string skillId,
        float power,
        float manaCost = 0f,
        float postureCost = 0f)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", skillId);
        SetField(skill, "power", power);
        SetField(skill, "manaCost", manaCost);
        SetField(skill, "postureCost", postureCost);
        SetField(
            skill,
            "effects",
            new List<DRPGActionEffectDefinition>
            {
                DRPGActionEffectDefinition.Damage(
                    DRPGActionEffectTarget.PrimaryTarget,
                    1f,
                    DRPGDamageTag.Slash)
            });
        return skill;
    }

    private static DRPGWorkspaceResolutionResult ResolvePrimaryImpact(
        DRPGActionResolver resolver,
        DRPGBattleStateMutationFixture fixture,
        DRPGSkillSO skill,
        DRPGBattleMutationWorkspace workspace,
        DRPGHitOutcome outcome)
    {
        DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
            fixture.Player.RuntimeId,
            fixture.Enemy.RuntimeId,
            skill);
        return resolver.ResolveIntoWorkspace(
            DRPGActionSpecResolver.Resolve(command, fixture.Player),
            fixture.Player,
            fixture.Enemy,
            new[] { fixture.Player, fixture.Enemy },
            workspace,
            DRPGBattleImpactContext.ForStrike(
                DRPGStrikeGrade.Body,
                1f),
            outcome);
    }

    private static DRPGBasicAttackFrozenSnapshot CreatePrimaryImpactSnapshot(
        DRPGBattleStateMutationFixture fixture,
        ActionIdentity identity,
        DRPGSkillSO skill,
        int targetRuntimeId = 0)
    {
        var actor = new ParticipantId(fixture.Player.RuntimeId);
        var target = new ParticipantId(
            targetRuntimeId > 0 ? targetRuntimeId : fixture.Enemy.RuntimeId);
        var turn = new ActorTurnToken(
            identity.BattleGeneration,
            identity.TurnSerial,
            actor);
        var selection = new TargetSelectionToken(
            identity.BattleGeneration,
            identity.TurnSerial,
            identity.ActionId,
            actor);
        return new DRPGBasicAttackFrozenSnapshot(
            turn,
            selection,
            target,
            skill).WithIdentity(identity);
    }

    private static DRPGStatusDefinitionSO CreateFingerprintStatus(
        string statusId,
        float incomingDamageMultiplier)
    {
        DRPGStatusDefinitionSO status =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        SetField(status, "statusId", statusId);
        SetField(status, "displayName", statusId);
        SetField(status, "durationTurns", 2);
        SetField(status, "maxStacks", 3);
        SetField(status, "incomingDamageMultiplier", incomingDamageMultiplier);
        return status;
    }

    private static bool ContainsTraceDetail(
        IReadOnlyList<ActionTraceEntry> trace,
        string fragment)
    {
        for (int i = 0; i < trace.Count; i++)
        {
            if (trace[i].Detail.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static void RequireStatePortHasNoSessionFacadeDependency()
    {
        Type stateType = typeof(DRPGBattleSessionState);
        var referencedTypes = new List<Type>();
        foreach (System.Reflection.FieldInfo field in stateType.GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public))
        {
            referencedTypes.Add(field.FieldType);
        }
        foreach (System.Reflection.MethodInfo method in stateType.GetMethods(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.DeclaredOnly))
        {
            referencedTypes.Add(method.ReturnType);
            System.Reflection.ParameterInfo[] parameters =
                method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
                referencedTypes.Add(parameters[i].ParameterType);
        }

        for (int i = 0; i < referencedTypes.Count; i++)
        {
            string name = referencedTypes[i] != null
                ? referencedTypes[i].FullName ?? referencedTypes[i].Name
                : string.Empty;
            Require(name.IndexOf(
                        nameof(DRPGBattleSession),
                        StringComparison.Ordinal) < 0 &&
                    name.IndexOf("Router", StringComparison.Ordinal) < 0,
                "The transaction state port depends on the Session facade " +
                "or a Router: " + name + ".");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
#endif
