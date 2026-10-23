#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal enum DRPGBasicAttackContractGroup
{
    IdentityAndArbiter,
    SchedulerAndWorkspace,
    RoutingAndSelection,
    PrimaryImpactLedger,
    ImmediateReactionLedger,
    Announcement,
    ConflictProtocol,
    WindupPlayback,
    PresentationWatchdog,
    ProjectionLease,
    MotionAndHitOutcome,
    ReturnDefeatFormation,
    StatusIdentity,
    ActionSettlement,
    TurnEntryAndInput,
    FinalizationOutcomeFault,
    WaveTransition,
    TraceAndComposition
}

internal static class DRPGBasicAttackContractManifest
{
    internal static readonly IReadOnlyDictionary<DRPGBasicAttackContractGroup, IReadOnlyCollection<string>> FinalExpectedCases =
        new Dictionary<DRPGBasicAttackContractGroup, IReadOnlyCollection<string>>
        {
            { DRPGBasicAttackContractGroup.IdentityAndArbiter, Cases("BattleGenerationNeverAliasesAcrossSessions", "LegacyTurnTokenHasDomainProvenance", "ArbiterExclusiveOwnership", "ReceiptTerminalExactlyOnce", "StaleIdentityCannotAdvance") },
            { DRPGBasicAttackContractGroup.SchedulerAndWorkspace, Cases("SchedulerQueriesArePure", "CandidateReservationAdvancesLogicalTimeOnce", "WorkspaceRejectsExternalRevisionChange", "FailedValidationLeavesEveryAggregateUntouched", "JournalAppliesAllPrimitiveWritesOnce", "ActiveParticipantMutationRequiresStateCapability", "CallerOwnedSchedulerAliasCannotMutateBattle") },
            { DRPGBasicAttackContractGroup.RoutingAndSelection, Cases("SelectionTokenLifecycle", "RouterOwnsExactlyOneRunnerDuringMigration", "FrozenSnapshot", "PresentationPortSlotLifecycle", "ProductionLegacyActionKeepsOneLease") },
            { DRPGBasicAttackContractGroup.PrimaryImpactLedger, Cases("PrimaryImpactExactlyOnce", "EvadeHasNoDamageOrHitCue", "LedgerFingerprintIdempotency", "AtomicResolutionFailureLeavesLiveStateUntouched") },
            { DRPGBasicAttackContractGroup.ImmediateReactionLedger, Cases("ReactionCategoryOrder", "ReactionBudgetAndEligibility") },
            { DRPGBasicAttackContractGroup.Announcement, Cases("AnnouncementIsolation") },
            { DRPGBasicAttackContractGroup.ConflictProtocol, Cases("ConflictCompositionProtocol") },
            { DRPGBasicAttackContractGroup.WindupPlayback, Cases("WindupMarkerGate") },
            { DRPGBasicAttackContractGroup.PresentationWatchdog, Cases("ReceiptTimeoutCleansOnlyOwnedCue", "DirectorCannotAdvanceRulesOrRestore") },
            { DRPGBasicAttackContractGroup.ProjectionLease, Cases("LeaseBlocksRealEventBridgeRebind", "LeasedViewIdentityIsStable", "CanonicalFlushOccursOnce") },
            { DRPGBasicAttackContractGroup.MotionAndHitOutcome, Cases("HorizontalRecoilHasNoScaleOrKink", "ReactionPresentationReceiptsAreSerial") },
            { DRPGBasicAttackContractGroup.ReturnDefeatFormation, Cases("DefeatPendingAtPrimaryImpact", "Stage7LethalLeasePair", "SimultaneousReturnBarrier", "AttackerNoSnapAtRestoreBoundary", "DisplacedReactionActorJoinsReturnBarrier") },
            { DRPGBasicAttackContractGroup.StatusIdentity, Cases("StatusInstanceIdentityAndTiming") },
            { DRPGBasicAttackContractGroup.ActionSettlement, Cases("SettlementReceiptsAreSerial", "Stage9LethalLeasePair", "ProductionSettlementProjectionCursor", "RuleMutationIsolationAcrossLedgers") },
            { DRPGBasicAttackContractGroup.TurnEntryAndInput, Cases("HeadlessTurnEntryRequiresExplicitAdapter") },
            { DRPGBasicAttackContractGroup.FinalizationOutcomeFault, Cases("NoContinuationBeforeFinalSettlement", "FinalizationExactlyOnceAndReentrantSafe", "ObserverExceptionCannotBreakCommittedFlow", "BattleFaultedCleanupLeavesNoHandles", "BattleFaultedIsNotActionOrBattleCompletion") },
            { DRPGBasicAttackContractGroup.WaveTransition, Cases("WavePlanDeterminism", "LegacyEncounterWavePolicyMigrationIsStable", "InitialEntryAndWaveHaveOneMotionOwner", "InitialWaveBlocksFirstTurnNotBattleLifecycle", "TypedContinuationPayloadCannotBeReconstructedFromLiveState") },
            { DRPGBasicAttackContractGroup.TraceAndComposition, Cases("TraceSchemaAndTotalOrder", "TransactionModulesDoNotDependOnSessionFacade", "CompositionIsCompleteBeforeEscape", "DirectSessionConstructionStillUsesSingleArbiter", "AllHeadlessFixturesUseExplicitPresentationComposition") }
        };

    internal static void Verify()
    {
        Array groups = Enum.GetValues(typeof(DRPGBasicAttackContractGroup));
        if (FinalExpectedCases.Count != groups.Length)
            throw new InvalidOperationException(
                "The basic-attack contract manifest must cover every group.");

        var uniqueCases = new HashSet<string>(StringComparer.Ordinal);
        int caseCount = 0;
        foreach (DRPGBasicAttackContractGroup group in groups)
        {
            IReadOnlyCollection<string> cases;
            if (!FinalExpectedCases.TryGetValue(group, out cases) ||
                cases == null || cases.Count == 0)
            {
                throw new InvalidOperationException(
                    "Contract group has no explicit cases: " + group);
            }
            foreach (string caseId in cases)
            {
                caseCount++;
                if (string.IsNullOrWhiteSpace(caseId) ||
                    !uniqueCases.Add(caseId))
                {
                    throw new InvalidOperationException(
                        "Contract case ids must be non-empty and unique: " +
                        caseId);
                }
            }
        }
        if (caseCount != 59)
            throw new InvalidOperationException(
                "The approved Layer-A manifest must contain 59 cases.");
    }

    private static IReadOnlyCollection<string> Cases(params string[] cases)
    {
        return cases;
    }
}

internal sealed class DRPGProbeGroupReport
{
    internal DRPGProbeGroupReport(
        DRPGBasicAttackContractGroup group,
        IReadOnlyCollection<string> expected,
        IReadOnlyCollection<string> completed,
        IReadOnlyCollection<string> pending,
        IReadOnlyCollection<string> failures)
    {
        Group = group;
        Expected = expected;
        Completed = completed;
        Pending = pending;
        Failures = failures;
    }

    internal DRPGBasicAttackContractGroup Group { get; private set; }
    internal IReadOnlyCollection<string> Expected { get; private set; }
    internal IReadOnlyCollection<string> Completed { get; private set; }
    internal IReadOnlyCollection<string> Pending { get; private set; }
    internal IReadOnlyCollection<string> Failures { get; private set; }
}

internal enum DRPGBasicAttackProductionScenario
{
    ProductionAnnouncementIsolation,
    ProductionWindupMarkerIdentity,
    ProductionLethalLeaseMatrix,
    ProductionParityThroughRealEntrances,
    ProductionGuardedAndEvadedAreSingleOutcomeBeats,
    ProductionRecoilContinuityThroughLateUpdate,
    ProductionAttackerNoSnapAtRestoreBoundary,
    ProductionSimultaneousReturn,
    ProductionSettlementProjectionIsSerial,
    ProductionSettlementNoChainFire,
    ProductionLeaseBlocksRebind,
    ProductionActorAnchorsFollowMotionRoot,
    ProductionMarkerMissingRecovers,
    ProductionImpactFeedbackTimeoutDoesNotRemutate,
    ProductionReactionTimeoutContinuesSerially,
    ProductionReturnTimeoutSurvivor,
    ProductionReturnTimeoutLethal,
    ProductionRootSettlementTimeoutStaysSerial,
    ProductionTurnEntryTimeoutStaysSerial,
    ProductionWaveEntryAnnouncementTimeout,
    ProductionWavePartialReadyTimeout,
    ProductionWaveReceiptIdentityMatrix,
    ProductionWavePlaceholderBatchDeploysAtomically,
    ProductionWaveRegistrationFailureFaultsCleanly,
    ProductionWaveSceneDisableCleansHandles
}

internal static class DRPGBasicAttackProductionScenarioManifest
{
    internal static readonly IReadOnlyCollection<DRPGBasicAttackProductionScenario> FinalExpectedScenarios =
        new DRPGBasicAttackProductionScenario[]
        {
            DRPGBasicAttackProductionScenario.ProductionAnnouncementIsolation,
            DRPGBasicAttackProductionScenario.ProductionWindupMarkerIdentity,
            DRPGBasicAttackProductionScenario.ProductionLethalLeaseMatrix,
            DRPGBasicAttackProductionScenario.ProductionSimultaneousReturn,
            DRPGBasicAttackProductionScenario.ProductionParityThroughRealEntrances,
            DRPGBasicAttackProductionScenario.ProductionWavePlaceholderBatchDeploysAtomically,
            DRPGBasicAttackProductionScenario.ProductionWaveRegistrationFailureFaultsCleanly,
            DRPGBasicAttackProductionScenario.ProductionGuardedAndEvadedAreSingleOutcomeBeats,
            DRPGBasicAttackProductionScenario.ProductionRecoilContinuityThroughLateUpdate,
            DRPGBasicAttackProductionScenario.ProductionAttackerNoSnapAtRestoreBoundary,
            DRPGBasicAttackProductionScenario.ProductionSettlementProjectionIsSerial,
            DRPGBasicAttackProductionScenario.ProductionLeaseBlocksRebind,
            DRPGBasicAttackProductionScenario.ProductionMarkerMissingRecovers,
            DRPGBasicAttackProductionScenario.ProductionImpactFeedbackTimeoutDoesNotRemutate,
            DRPGBasicAttackProductionScenario.ProductionReactionTimeoutContinuesSerially,
            DRPGBasicAttackProductionScenario.ProductionReturnTimeoutSurvivor,
            DRPGBasicAttackProductionScenario.ProductionReturnTimeoutLethal,
            DRPGBasicAttackProductionScenario.ProductionRootSettlementTimeoutStaysSerial,
            DRPGBasicAttackProductionScenario.ProductionTurnEntryTimeoutStaysSerial,
            DRPGBasicAttackProductionScenario.ProductionWaveEntryAnnouncementTimeout,
            DRPGBasicAttackProductionScenario.ProductionWavePartialReadyTimeout,
            DRPGBasicAttackProductionScenario.ProductionWaveReceiptIdentityMatrix,
            DRPGBasicAttackProductionScenario.ProductionWaveSceneDisableCleansHandles,
            DRPGBasicAttackProductionScenario.ProductionSettlementNoChainFire,
            DRPGBasicAttackProductionScenario.ProductionActorAnchorsFollowMotionRoot
        };

    internal static void Verify()
    {
        Array values = Enum.GetValues(
            typeof(DRPGBasicAttackProductionScenario));
        var unique = new HashSet<DRPGBasicAttackProductionScenario>(
            FinalExpectedScenarios);
        if (values.Length != 25 || FinalExpectedScenarios.Count != 25 ||
            unique.Count != 25)
        {
            throw new InvalidOperationException(
                "The approved Layer-B manifest must contain 25 unique " +
                "production scenarios.");
        }
        foreach (DRPGBasicAttackProductionScenario scenario in values)
        {
            if (!unique.Contains(scenario))
                throw new InvalidOperationException(
                    "Production scenario is missing from the manifest: " +
                    scenario);
        }
    }
}

internal interface IEditorProcessExit
{
    void Exit(int exitCode);
}

internal sealed class RecordingEditorProcessExit : IEditorProcessExit
{
    internal int? ExitCode { get; private set; }

    public void Exit(int exitCode)
    {
        ExitCode = exitCode;
    }
}

internal static class DRPGBattleBasicAttackProbeRunner
{
    private static readonly IDictionary<
        DRPGBasicAttackContractGroup,
        IDictionary<string, Action>> CompletedRegistry =
        new Dictionary<
            DRPGBasicAttackContractGroup,
            IDictionary<string, Action>>
        {
            {
                DRPGBasicAttackContractGroup.IdentityAndArbiter,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "BattleGenerationNeverAliasesAcrossSessions", DRPGBattleBasicAttackNineStageProbe.BattleGenerationNeverAliasesAcrossSessions },
                    { "LegacyTurnTokenHasDomainProvenance", DRPGBattleBasicAttackNineStageProbe.LegacyTurnTokenHasDomainProvenance },
                    { "ArbiterExclusiveOwnership", DRPGBattleBasicAttackNineStageProbe.ArbiterExclusiveOwnership },
                    { "ReceiptTerminalExactlyOnce", DRPGBattleBasicAttackNineStageProbe.ReceiptTerminalExactlyOnce },
                    { "StaleIdentityCannotAdvance", DRPGBattleBasicAttackNineStageProbe.StaleIdentityCannotAdvance }
                }
            },
            {
                DRPGBasicAttackContractGroup.SchedulerAndWorkspace,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "SchedulerQueriesArePure", DRPGBattleBasicAttackNineStageProbe.SchedulerQueriesArePure },
                    { "CandidateReservationAdvancesLogicalTimeOnce", DRPGBattleBasicAttackNineStageProbe.CandidateReservationAdvancesLogicalTimeOnce },
                    { "WorkspaceRejectsExternalRevisionChange", DRPGBattleBasicAttackNineStageProbe.WorkspaceRejectsExternalRevisionChange },
                    { "FailedValidationLeavesEveryAggregateUntouched", DRPGBattleBasicAttackNineStageProbe.FailedValidationLeavesEveryAggregateUntouched },
                    { "JournalAppliesAllPrimitiveWritesOnce", DRPGBattleBasicAttackNineStageProbe.JournalAppliesAllPrimitiveWritesOnce },
                    { "ActiveParticipantMutationRequiresStateCapability", DRPGBattleBasicAttackNineStageProbe.ActiveParticipantMutationRequiresStateCapability },
                    { "CallerOwnedSchedulerAliasCannotMutateBattle", DRPGBattleBasicAttackNineStageProbe.CallerOwnedSchedulerAliasCannotMutateBattle }
                }
            },
            {
                DRPGBasicAttackContractGroup.RoutingAndSelection,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "SelectionTokenLifecycle", DRPGBattleBasicAttackNineStageProbe.SelectionTokenLifecycle },
                    { "RouterOwnsExactlyOneRunnerDuringMigration", DRPGBattleBasicAttackNineStageProbe.RouterOwnsExactlyOneRunnerDuringMigration },
                    { "FrozenSnapshot", DRPGBattleBasicAttackNineStageProbe.FrozenSnapshot },
                    { "PresentationPortSlotLifecycle", DRPGBattleBasicAttackNineStageProbe.PresentationPortSlotLifecycle },
                    { "ProductionLegacyActionKeepsOneLease", DRPGBattleBasicAttackNineStageProbe.ProductionLegacyActionKeepsOneLease }
                }
            },
            {
                DRPGBasicAttackContractGroup.PrimaryImpactLedger,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "PrimaryImpactExactlyOnce", DRPGBattleBasicAttackNineStageProbe.PrimaryImpactExactlyOnce },
                    { "EvadeHasNoDamageOrHitCue", DRPGBattleBasicAttackNineStageProbe.EvadeHasNoDamageOrHitCue },
                    { "LedgerFingerprintIdempotency", DRPGBattleBasicAttackNineStageProbe.LedgerFingerprintIdempotency },
                    { "AtomicResolutionFailureLeavesLiveStateUntouched", DRPGBattleBasicAttackNineStageProbe.AtomicResolutionFailureLeavesLiveStateUntouched }
                }
            },
            {
                DRPGBasicAttackContractGroup.ImmediateReactionLedger,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "ReactionCategoryOrder", DRPGImmediateReactionLedgerProbe.ReactionCategoryOrder },
                    { "ReactionBudgetAndEligibility", () =>
                        {
                            DRPGImmediateReactionLedgerProbe.ReactionBudgetAndEligibility();
                            DRPGActionResourceJournalProbe.RunAll();
                            DRPGActionResourceJournalProbe.RunCompleteLedgerChecks();
                            DRPGActionResourceJournalProbe.RunActionCostChecks();
                            DRPGWeaponCompletionJournalProbe.RunAll();
                            DRPGWeaponCompletionJournalProbe.RunProgressCapacityChecks();
                            DRPGStatusWorkspaceProbe.RunAll();
                            DRPGActionResourceJournalProbe.RunStatusLedgerChecks();
                            DRPGActionResourceJournalProbe.RunPureStatusChecks();
                            DRPGActionResourceJournalProbe.RunTimelineLedgerChecks();
                            DRPGActionResourceJournalProbe.RunVitalLedgerChecks();
                DRPGActionResourceJournalProbe.RunPostureWorkspaceChecks();
                DRPGActionResourceJournalProbe.RunResonanceJournalChecks();
                DRPGActionResourceJournalProbe.RunActionRuleSourceChecks();
                DRPGActionResourceJournalProbe.RunProductionRuleKernelChecks();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.Announcement,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "AnnouncementIsolation", () =>
                        {
                            DRPGBasicAttackPreludeProbe.AnnouncementIsolation();
                            DRPGBasicAttackPreludeProbe.AnnouncementUsesRealLabelAndRenderFence();
                            DRPGBasicAttackPreludeProbe.FrozenNameFlowsThroughRoot();
                            DRPGBasicAttackPreludeProbe.TimingProfileFreezesApprovedFiniteMinimums();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.ConflictProtocol,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "ConflictCompositionProtocol", () =>
                        {
                            DRPGBasicAttackPreludeProbe.ConflictCompositionProtocol();
                            DRPGBasicAttackPreludeProbe.RootModuleStartsPreludeWithoutCompletingAction();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.WindupPlayback,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "WindupMarkerGate", () =>
                        {
                            DRPGBasicAttackPreludeProbe.WindupMarkerGate();
                            DRPGBasicAttackPreludeProbe.RealSpineEventKeepsPlaybackIdentity();
                            DRPGBasicAttackPreludeProbe.BoundSpritePoseDoesNotLungeOrRestoreOnSeal();
                            DRPGBasicAttackPreludeProbe.MissingPlaybackCannotClaimAuthoredFallback();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.PresentationWatchdog,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "ReceiptTimeoutCleansOnlyOwnedCue", () =>
                        {
                            DRPGBasicAttackPreludeProbe.ReceiptTimeoutCleansOnlyOwnedCue();
                            DRPGBasicAttackPreludeProbe.WatchdogRecoveryResistsReentrantCompletion();
                            DRPGBasicAttackPreludeProbe.DeadlineWinsOverUnpumpedLateMarker();
                            DRPGBasicAttackPreludeProbe.MissingReceiptsRecoverWithinSameRoot();
                            DRPGBasicAttackPreludeProbe.ForeignReceiptFaultCannotCancelAnotherAction();
                        }
                    },
                    { "DirectorCannotAdvanceRulesOrRestore", () =>
                        {
                            DRPGBasicAttackPreludeProbe.DirectorCannotAdvanceRulesOrRestore();
                            DRPGBasicAttackPreludeProbe.MonoDirectorAndDirectSemanticExitCannotRestoreStandardScope();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.ProjectionLease,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "LeaseBlocksRealEventBridgeRebind", () =>
                        {
                            DRPGBasicAttackProjectionProbe.LeaseBlocksRealEventBridgeRebind();
                            DRPGBasicAttackProjectionProbe.AuthoritativeChannelDoesNotAdvanceProjection();
                            DRPGBasicAttackProjectionProbe.CursorIsOwnedOrderedAndSemanticOnly();
                            DRPGBasicAttackProjectionProbe.HpCursorDoesNotInvokeGameplayListeners();
                            DRPGBasicAttackProjectionProbe.EnemyCursorUpdatesWorldStatusWithoutRebinding();
                        }
                    },
                    { "LeasedViewIdentityIsStable", () =>
                        {
                            DRPGBasicAttackProjectionProbe.LeasedViewIdentityIsStable();
                            DRPGBasicAttackProjectionProbe.LeaseRejectsBindingReplacementAndIncompleteRoster();
                            DRPGBasicAttackProjectionProbe.FullBattlefieldRejectsUnindexedPooledActor();
                            DRPGBasicAttackProjectionProbe.RootOwnsProjectionBeforeAnnouncement();
                            DRPGBasicAttackProjectionProbe.RootFaultsWhenProjectionIsAbandoned();
                            DRPGBasicAttackProjectionProbe.CheckpointFreezesRectLayout();
                            DRPGBasicAttackProjectionProbe.CheckpointFreezesCanvasVisibility();
                            DRPGBasicAttackProjectionProbe.CheckpointIncludesDetachedWorldAndCamera();
                            DRPGBasicAttackProjectionProbe.RejectedWritesCarryStructuredOwnerAndTarget();
                        }
                    },
                    { "CanonicalFlushOccursOnce", () =>
                        {
                            DRPGBasicAttackProjectionProbe.CanonicalFlushOccursOnce();
                            DRPGBasicAttackProjectionProbe.ClosingObserverCannotSkipCanonicalReconciliation();
                            DRPGBasicAttackProjectionProbe.CanonicalLeafReentryCannotReleaseActors();
                            DRPGBasicAttackProjectionProbe.IdenticalCanonicalRosterDoesNotRestartFormationMotion();
                            DRPGBasicAttackProjectionProbe.IdenticalActorVisualDoesNotRebindOrResetMotionRoot();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.MotionAndHitOutcome,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "HorizontalRecoilHasNoScaleOrKink", DRPGBasicAttackMotionProbe.HorizontalRecoilHasNoScaleOrKink },
                    { "ReactionPresentationReceiptsAreSerial", () =>
                        {
                            DRPGBasicAttackImpactSequenceProbe.ReactionPresentationReceiptsAreSerial();
                            DRPGBasicAttackImpactSequenceProbe.ReactionTimeoutContinuesWithoutRemutation();
                            DRPGBasicAttackImpactSequenceProbe.ReactionDiagnosticsDoNotIssueReceipts();
                            DRPGBasicAttackImpactSequenceProbe.ImpactQueueRejectsOwnershipLostDuringPump();
                            DRPGBasicAttackImpactSequenceProbe.ImpactQueueRejectsForeignReceiptsWithoutCancellingThem();
                            DRPGBasicAttackImpactSequenceProbe.ImpactQueueRequiresRenderedFrameAndStopsOnFault();
                            DRPGBasicAttackHitOutcomeProbe.RealReactionQueueUsesIntermediateHpCursors();
                            // The legacy immediate HP-heal cursor fixture assumed the primary strike
                            // always damaged HP. Posture-first damage makes that setup invalid; posture
                            // recovery presentation is covered by the Battle V2 recovery probes.
                            DRPGBasicAttackHitOutcomeProbe.ClosingProjectionCancelsItsOutcomeLabel();
                            DRPGBasicAttackHitOutcomeProbe.CompletedLedgerRejectsExtraReactionWithoutThrowing();
                            DRPGBasicAttackHitOutcomeProbe.AdapterProjectsOneSideSymmetricOutcomeBeat();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.ReturnDefeatFormation,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "DefeatPendingAtPrimaryImpact", () =>
                        {
                            DRPGBasicAttackDefeatPendingProbe.LethalHitRetainsXDimAndPoseThroughRenderedHome();
                            DRPGBasicAttackDefeatPendingProbe.HiddenDefeatMarkerCannotCrossHomeRenderedBarrier();
                        }
                    },
                    { "Stage7LethalLeasePair", () =>
                        {
                            DRPGBasicAttackDefeatPendingProbe.LethalHitRetainsXDimAndPoseThroughRenderedHome();
                            DRPGBasicAttackRootPipelineProbe.LethalRootRetainsSameVisibleActorThroughHomeBeforeDeparture();
                        }
                    },
                    { "SimultaneousReturnBarrier", () =>
                        {
                            DRPGBasicAttackMotionProbe.BothActorsReturnSmoothlyFromOneStartTime();
                            DRPGBasicAttackReturnBarrierProbe.AdapterReturnsBothActorsAndCameraBeforeRenderedTerminal();
                        }
                    },
                    { "AttackerNoSnapAtRestoreBoundary", () =>
                        {
                            DRPGBasicAttackVisualRestoreProbe.RenderedFeetDoNotDropOrPopAcrossReturnSpriteSwap();
                            DRPGBasicAttackVisualRestoreProbe.DefeatPoseAndTintRemainUntilHomeRendered();
                        }
                    },
                    { "DisplacedReactionActorJoinsReturnBarrier", () =>
                        {
                            DRPGBasicAttackReactionMotionProbe.DisplacedReactionActorsJoinTheSameReturnBatch();
                            DRPGBasicAttackVisualRestoreProbe.BystandersRestoreWithTheSameReturnBatch();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.ActionSettlement,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "SettlementReceiptsAreSerial", DRPGBattleBasicAttackNineStageProbe.SettlementReceiptsAreSerial },
                    { "Stage9LethalLeasePair", DRPGBattleBasicAttackNineStageProbe.Stage9LethalLeasePair },
                    { "ProductionSettlementProjectionCursor", DRPGBattleBasicAttackNineStageProbe.ProductionSettlementProjectionCursor },
                    { "RuleMutationIsolationAcrossLedgers", DRPGBattleBasicAttackNineStageProbe.RuleMutationIsolationAcrossLedgers }
                }
            },
            {
                DRPGBasicAttackContractGroup.StatusIdentity,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "StatusInstanceIdentityAndTiming", DRPGBattleBasicAttackNineStageProbe.StatusInstanceIdentityAndTiming }
                }
            },
            {
                DRPGBasicAttackContractGroup.TurnEntryAndInput,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "HeadlessTurnEntryRequiresExplicitAdapter", () =>
                        {
                            DRPGProductionCompositionProbe.EnabledPolicyCreatesFrozenWaveSessionAndRequiresBinding();
                            DRPGTurnEntrySequenceProbe.CandidateUsesSerialReadableReceiptsWithoutAnAction();
                            DRPGTurnEntrySequenceProbe.CandidateTimeoutCleansBeforeDegradingAndRejectsLateCallback();
                            DRPGTurnEntryPresentationProbe.CandidatePresenterRevealsActualNumbersAndHpOneStepAtATime();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.FinalizationOutcomeFault,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "NoContinuationBeforeFinalSettlement", () =>
                        {
                            DRPGBasicAttackRootSettlementProbe.RootAutomaticallyRunsBothSettlementLanes();
                            DRPGBasicAttackFinalizationProbe.FinalSettlementClosesProjectionAndYieldsExactlyOnce();
                        }
                    },
                    { "FinalizationExactlyOnceAndReentrantSafe", () =>
                        {
                            DRPGBasicAttackFinalizationProbe.CanonicalCloseReentryCannotYieldDisposedRoot();
                            DRPGBasicAttackFinalizationProbe.NextPumpTransfersRootWithoutInputGapOrReentrantDelivery();
                        }
                    },
                    { "ObserverExceptionCannotBreakCommittedFlow", () =>
                        {
                            DRPGBattleSafeEventProbe.ThrowingObserversCannotInterruptImpactCompletionOrResult();
                            DRPGBattleSafeEventProbe.PublicationSnapshotsSubscribersAndDefersReentrantCommands();
                            DRPGBattleSafeEventProbe.ObserverFailuresHaveBoundedPerSessionDiagnostics();
                        }
                    },
                    { "BattleFaultedCleanupLeavesNoHandles", () =>
                        {
                            DRPGBasicAttackFinalizationProbe.MissingFaultCleanupConfirmationHasBoundedRetriesAndNeverFakesCompletion();
                            DRPGBasicAttackFinalizationProbe.LostFaultCleanupAcknowledgementCanBeRecoveredWithoutReplayingAction();
                            DRPGBasicAttackFinalizationProbe.FaultRetainsOwnershipUntilPresentationCleanupCompletes();
                        }
                    },
                    { "BattleFaultedIsNotActionOrBattleCompletion", () =>
                        {
                            DRPGBasicAttackFinalizationProbe.SealedRootFaultCleanupRetainsTheNotificationLease();
                            DRPGBasicAttackFinalizationProbe.LockTimePresentationLossAlsoCompletesFaultCleanup();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.WaveTransition,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "WavePlanDeterminism", () =>
                        {
                            DRPGEnemyWavePlanProbe.AuthoredMembersArePartitionedOnceWithoutProfileDeduplication();
                            DRPGEnemyWavePlanProbe.SeedStreamMatchesIndependentGoldenVectors();
                            DRPGEnemyWavePlanProbe.FrozenPlanIgnoresCallerEditsAndCanonicalizesEncounterIdentity();
                        }
                    },
                    { "LegacyEncounterWavePolicyMigrationIsStable", () =>
                        {
                            DRPGEnemyWavePlanProbe.ExistingEncounterPolicyFreezesWithoutDirtyingAssets();
                            DRPGEnemyWavePlanProbe.ExplicitInvalidPoliciesNeverFallBackToLegacyDefaults();
                            DRPGEnemyWavePlanProbe.BattleStartCarriesAnExplicitReplaySeed();
                        }
                    },
                    { "InitialEntryAndWaveHaveOneMotionOwner", () =>
                        {
                            DRPGWaveActorStagingProbe.CommittedDeploymentAdoptsTheSameActor();
                            DRPGWaveSpritePresentationProbe.AnnouncementThenSmoothEntryThenRenderedWholeBatch();
                            DRPGWaveSpritePresentationProbe.ForgedMembersCannotCompleteOrCancelTheRealBatch();
                        }
                    },
                    { "InitialWaveBlocksFirstTurnNotBattleLifecycle", () =>
                        {
                            DRPGWaveSessionProbe.InitialWaveHasNoActiveEnemyBeforePresentation();
                            DRPGEnemyWaveLifecycleProbe.CompositionOwnsWholeWaveDeploymentAndSealsOnce();
                            DRPGProductionCompositionProbe.EnabledPolicyCreatesFrozenWaveSessionAndRequiresBinding();
                        }
                    },
                    { "TypedContinuationPayloadCannotBeReconstructedFromLiveState", () =>
                        {
                            DRPGEnemyWaveContinuationProbe.WaveCompletionIsSealedOnceWithoutInlineTurnEntry();
                            DRPGEnemyWaveContinuationProbe.AtomicBatchSealsFrozenCompletionBeforeDeferredTurnEntry();
                            DRPGEnemyWaveContinuationProbe.ExactWaveDepartureSetAccumulatesAcrossSealedActions();
                        }
                    }
                }
            },
            {
                DRPGBasicAttackContractGroup.TraceAndComposition,
                new Dictionary<string, Action>(StringComparer.Ordinal)
                {
                    { "TraceSchemaAndTotalOrder", DRPGBattleBasicAttackNineStageProbe.TraceSchemaAndTotalOrder },
                    { "TransactionModulesDoNotDependOnSessionFacade", DRPGBattleBasicAttackNineStageProbe.TransactionModulesDoNotDependOnSessionFacade },
                    { "CompositionIsCompleteBeforeEscape", DRPGBattleBasicAttackNineStageProbe.CompositionIsCompleteBeforeEscape },
                    { "DirectSessionConstructionStillUsesSingleArbiter", DRPGBattleBasicAttackNineStageProbe.DirectSessionConstructionStillUsesSingleArbiter },
                    { "AllHeadlessFixturesUseExplicitPresentationComposition", DRPGBattleBasicAttackNineStageProbe.AllHeadlessFixturesUseExplicitPresentationComposition }
                }
            }
        };

    internal static DRPGProbeGroupReport RunContractGroup(DRPGBasicAttackContractGroup group)
    {
        IReadOnlyCollection<string> expected = DRPGBasicAttackContractManifest.FinalExpectedCases[group];
        IDictionary<string, Action> registeredCompleted;
        CompletedRegistry.TryGetValue(group, out registeredCompleted);

        List<string> completed = new List<string>();
        List<string> pending = new List<string>();
        List<string> failures = new List<string>();
        foreach (string caseName in expected)
        {
            Action probe;
            if (registeredCompleted != null &&
                registeredCompleted.TryGetValue(caseName, out probe) &&
                probe != null)
            {
                try
                {
                    probe();
                    completed.Add(caseName);
                }
                catch (Exception exception)
                {
                    failures.Add(caseName + ": " + exception.Message);
                }
            }
            else
            {
                pending.Add(caseName);
            }
        }

        return new DRPGProbeGroupReport(
            group,
            expected,
            completed,
            pending,
            failures);
    }

    internal static string VerifyInfrastructureContract()
    {
        DRPGBasicAttackContractManifest.Verify();
        DRPGBasicAttackProductionScenarioManifest.Verify();
        int completed = 0;
        int pending = 0;
        foreach (DRPGBasicAttackContractGroup group in
            Enum.GetValues(typeof(DRPGBasicAttackContractGroup)))
        {
            IReadOnlyCollection<string> expected =
                DRPGBasicAttackContractManifest.FinalExpectedCases[group];
            var expectedSet = new HashSet<string>(
                expected,
                StringComparer.Ordinal);
            if (CompletedRegistry.TryGetValue(
                    group,
                    out IDictionary<string, Action> registered))
            {
                foreach (KeyValuePair<string, Action> entry in registered)
                {
                    if (!expectedSet.Contains(entry.Key) || entry.Value == null)
                    {
                        throw new InvalidOperationException(
                            "Completed contract registration is invalid: " +
                            group + "/" + entry.Key + ".");
                    }
                }
            }

            DRPGProbeGroupReport report = RunContractGroup(group);
            if (report.Failures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Completed contract behavior regressed: " +
                    string.Join(" | ", report.Failures));
            }
            completed += report.Completed.Count;
            pending += report.Pending.Count;
        }
        if (completed + pending != 59)
            throw new InvalidOperationException(
                "The completed/pending contract ledger must total 59 cases.");
        return "PASS basic-attack manifests: 18 groups / 59 contracts / " +
            "25 production scenarios; completed=" + completed +
            "; pending=" + pending;
    }

    [MenuItem(
        "Tools/ANKALUTE/DRPG/Basic Attack/Capture Legacy Behavior REDs")]
    private static void CaptureLegacyBehaviorReds()
    {
        DRPGBattleBasicAttackProductionPlayModeTestRunner
            .RunLegacyBehaviorRedsFromMenu();
    }

    [MenuItem(
        "Tools/ANKALUTE/DRPG/Basic Attack/Run Nine-Stage Contract Probes")]
    private static void RunNineStageContractProbes()
    {
        foreach (DRPGBasicAttackContractGroup group in DRPGBasicAttackContractManifest.FinalExpectedCases.Keys)
        {
            DRPGProbeGroupReport report = RunContractGroup(group);
            Debug.Log(FormatReport(report));
        }
    }

    private static string FormatReport(DRPGProbeGroupReport report)
    {
        string text = string.Format(
            "{0}: expected={1}, completed={2}, pending={3}, failed={4}",
            report.Group,
            report.Expected.Count,
            report.Completed.Count,
            report.Pending.Count,
            report.Failures.Count);
        foreach (string failure in report.Failures)
            text += "\n" + failure;
        return text;
    }
}
#endif
