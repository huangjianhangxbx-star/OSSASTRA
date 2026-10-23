using System;

// Unity-facing stage adapter. Motion is supplied by the action's composition
// owner; this adapter never calls a Session, Resolver or scheduler.
internal sealed class DRPGBattleTransactionPresentationAdapter : IStandardBasicAttackPresentationPort,
    IDRPGBattlePresentationPump, IStandardBasicAttackProjectionScope, IStandardBasicAttackReturnPort, IStandardBasicAttackSettlementPort, IBattleStartPresentationPort, IBattleWavePresentationFactory
{
    private readonly BattleActionAnnouncementPresenter announcement;
    private readonly BattleAnimationPlaybackRouter playback;
    private readonly Func<ConflictCompositionCommand, IBattlePresentationHandle> compose;
    private readonly Func<DRPGBasicAttackPresentationFreezeRequest, DRPGBasicAttackPresentationFreezeInput> freeze;
    private readonly BattlePresentationWatchdog watchdog;
    private PresentationReceipt active;
    private IBattlePresentationHandle conflictHandle;
    private double conflictStarted, conflictDuration;
    private long conflictFrame;
    private readonly DRPGBattleProjectionLeaseArbiter projectionArbiter;
    private BattleActorMotionController motion;
    private PresentationReceipt conflictMotion;
    private readonly BattleImpactFeedbackPresenter impactFeedback;
    private readonly UnityEngine.Camera battleCamera;
    private BattleHitOutcomePresenter hitOutcomes;
    private BattleReturnBarrier returnBarrier;
    private BattleCameraStateSnapshot cameraHome;
    private DRPGBasicAttackFrozenSnapshot ownedSnapshot;
    private readonly BattleDefeatedActorRoster defeatedRoster;
    private DRPGBattleDeparturePresentation completedDeparture;
    private DRPGBattleActionResolutionLedger activeLedger;
    private BattleFormationReconcileBarrier formationBarrier;
    private DRPGBattlePostReturnResult formationRules;
    private BattleSettlementStepPresenter settlement;
    private DRPGSettlementPresentationPlan settlementPlan;
    private DRPGBattleDeparturePresentation settlementDeparture;
    private BattleFormationReconcileBarrier settlementFormation;
    private DRPGBattlePostReturnResult settlementFormationRules;
    private readonly Func<BattleStartEntryCommand, IBattlePresentationHandle> presentEntry;
    private readonly IBattleWavePresentationFactory waveFactory;
    private readonly IBattleAudioCueSink audioCueSink;
    private readonly Func<DRPGBasicAttackFrozenSnapshot, DRPGBattleActionProjectionLease, BattleCloseupScope> closeupFactory;
    private BattleCloseupScope closeup;
    private PresentationReceipt entryReceipt;
    private IBattlePresentationHandle entryHandle;
    private long entrySettledFrame = -1;
    internal DRPGBattleTransactionPresentationAdapter(IDRPGBattlePresentationClock clock,
        BattleActionAnnouncementPresenter announcement, BattleAnimationPlaybackRouter playback,
        Func<ConflictCompositionCommand, IBattlePresentationHandle> compose,
        Func<DRPGBasicAttackPresentationFreezeRequest, DRPGBasicAttackPresentationFreezeInput> freeze = null,
        DRPGBattleProjectionLeaseArbiter projectionArbiter = null,
        BattleImpactFeedbackPresenter impactFeedback = null, UnityEngine.Camera battleCamera = null,
        BattleDefeatedActorRoster defeatedRoster = null,
        Func<BattleStartEntryCommand, IBattlePresentationHandle> presentEntry = null,
        IBattleWavePresentationFactory waveFactory = null,
        IBattleAudioCueSink audioCueSink = null,
        Func<DRPGBasicAttackFrozenSnapshot, DRPGBattleActionProjectionLease, BattleCloseupScope> closeupFactory = null)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.announcement = announcement ?? throw new ArgumentNullException(nameof(announcement));
        this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
        this.compose = compose ?? throw new ArgumentNullException(nameof(compose));
        this.freeze = freeze;
        this.projectionArbiter = projectionArbiter;
        this.impactFeedback = impactFeedback; this.battleCamera = battleCamera;
        this.defeatedRoster = defeatedRoster;
        this.presentEntry = presentEntry;
        this.waveFactory = waveFactory;
        this.audioCueSink = audioCueSink ?? NullBattleAudioCueSink.Instance;
        this.closeupFactory = closeupFactory;
        watchdog = new BattlePresentationWatchdog(clock);
    }
    public IDRPGBattlePresentationClock Clock { get; }
    public bool TryOpen(BattleTransactionIdentity owner, System.Collections.Generic.IReadOnlyList<BattleParticipantRuntime> members,
        int perRowLimit, out IBattleWavePresentationScope scope, out string error)
    {
        scope = null; error = "Wave presentation factory is not bound.";
        return waveFactory != null && waveFactory.TryOpen(owner, members, perRowLimit, out scope, out error);
    }
    public PresentationReceipt PresentEntry(in BattleStartEntryCommand command)
    {
        if (entryReceipt != null && entryReceipt.Owner == command.Owner) return entryReceipt;
        var receipt = new PresentationReceipt(command.Owner, DRPGBattleActionStage.TurnBegin, 0);
        if (entryReceipt != null || active != null && !active.Completion.IsCompleted || presentEntry == null)
        {
            receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Entry binding unavailable or already owned."));
            return receipt;
        }
        entryReceipt = receipt; active = receipt; entrySettledFrame = -1;
        try
        {
            entryHandle = presentEntry(command);
            if (entryHandle == null) throw new InvalidOperationException("Entry coordinator returned no handle.");
        }
        catch (Exception error)
        { receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, error.Message)); }
        return receipt;
    }
    public IStandardBasicAttackProjectionLifetime OpenProjection(in DRPGBasicAttackFrozenSnapshot snapshot)
    {
        // Unbound is retained only for isolated Prelude fixtures. The complete
        // standard battle binding supplies its surface's arbiter.
        if (projectionArbiter == null) return null;
        var result = projectionArbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(snapshot.Identity));
        if (!result.Accepted) throw new InvalidOperationException(result.Reason);
        hitOutcomes?.Dispose(); hitOutcomes = null;
        closeup?.Dispose(); closeup = null;
        settlement?.Dispose(); settlement = null; settlementPlan = null;
        settlementFormation?.Dispose(); settlementFormation = null; settlementFormationRules = null; settlementDeparture = null;
        returnBarrier?.Dispose(); returnBarrier = null;
        formationBarrier?.Dispose(); formationBarrier = null; formationRules = null;
        completedDeparture = null; activeLedger = null;
        announcement.Hide();
        ownedSnapshot = snapshot;
        if (battleCamera != null) cameraHome = new BattleCameraStateSnapshot(battleCamera.transform.position, battleCamera.transform.rotation,
            battleCamera.fieldOfView, battleCamera.orthographic, battleCamera.orthographicSize, battleCamera.enabled);
        try
        {
            motion = new BattleActorMotionController(result.Lease, Clock);
            closeup = closeupFactory?.Invoke(snapshot, result.Lease);
        }
        catch { result.Lease.Dispose(); throw; }
        return result.Lease;
    }
    internal int RecoveryCount => watchdog.RecoveryCount;
    public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest request, out DRPGBasicAttackPresentationFreezeInput snapshot)
    { snapshot = freeze != null ? freeze(request) : default; return snapshot.IsValid; }
    private static PresentationReceipt Unavailable(ActionIdentity id, DRPGBattleActionStage stage)
    { var r = new PresentationReceipt(id, stage); r.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Later-stage adapter is not bound.")); return r; }
    public PresentationReceipt Announce(in AnnouncementCommand c)
    {
        if (active != null && !active.Completion.IsCompleted) return Unavailable(c.Identity, DRPGBattleActionStage.Announcement);
        active = announcement.Present(new AnnouncementCommand(c.Snapshot, c.IssuedAt, c.Snapshot.ActorDisplayName));
        Watch(active, Math.Max(0d, c.Snapshot.Timing.AnnouncementSeconds), "LockBaseline");
        return active;
    }
    public PresentationReceipt EnterConflict(in ConflictCompositionCommand c)
    {
        if (active != null && !active.Completion.IsCompleted) return Unavailable(c.Identity, DRPGBattleActionStage.ConflictComposition);
        active = new PresentationReceipt(c.Identity, DRPGBattleActionStage.ConflictComposition);
        conflictStarted = Clock.Now; conflictFrame = Clock.RenderedFrame;
        conflictDuration = Math.Max(0d, c.Snapshot.Timing.ConflictHoldSeconds);
        conflictMotion = null;
        closeup?.Enter(c.Identity);
        if (motion != null)
        {
            var actor = c.Snapshot.ActorConflictPosition;
            var target = c.Snapshot.TargetConflictPosition;
            conflictMotion = motion.SnapConflict(new ConflictMotionCommand(c.Identity,
                c.Snapshot.Turn.Actor, c.Snapshot.Target,
                new UnityEngine.Vector3(actor.X, actor.Y, actor.Z),
                new UnityEngine.Vector3(target.X, target.Y, target.Z), c.IssuedAt));
            if (conflictMotion.Completion.IsCompleted &&
                conflictMotion.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted)
            {
                active.TryComplete(conflictMotion.Completion.Result);
                return active;
            }
        }
        try { conflictHandle = compose(c); } catch { conflictHandle = null; }
        Watch(active, conflictDuration, "ConflictCheckpoint");
        return active;
    }
    public PresentationReceipt PlayWindup(in BasicAttackWindupCommand c)
    {
        if (active != null && !active.Completion.IsCompleted) return Unavailable(c.Identity, DRPGBattleActionStage.Windup);
        active = playback.Play(c, c.Snapshot.Semantics.MarkerMode, c.Snapshot.Semantics.ExpectedMarkerOrdinal);
        Watch(active, Math.Max(0d, c.Snapshot.Timing.WindupMinimumSeconds), "ConflictCheckpoint");
        return active;
    }
    private void Watch(PresentationReceipt receipt, double minimum, string checkpoint)
    { watchdog.Watch(receipt, Clock.Now + minimum + 2d, checkpoint, () => CancelReceipt(receipt, "ExpectedMarkerOrReceiptMissing")); }
    public PresentationReceipt PresentImpact(in ImpactPresentationCommand c) => PresentLedgerItem(c.Ledger, 0, c.IssuedAt, c.Identity);
    public PresentationReceipt PresentReaction(in ReactionPresentationCommand c) => PresentLedgerItem(c.Ledger, c.StepOrdinal, c.IssuedAt, c.Identity);
    private PresentationReceipt PresentLedgerItem(DRPGBattleActionResolutionLedger ledger, int step, double issuedAt, ActionIdentity identity)
    {
        if (ledger == null || motion == null || projectionArbiter?.Active == null ||
            active != null && !active.Completion.IsCompleted && active.Stage != DRPGBattleActionStage.ImpactAndReactions)
            return Unavailable(identity, DRPGBattleActionStage.ImpactAndReactions);
        if (hitOutcomes == null) activeLedger = ledger;
        if (hitOutcomes == null) hitOutcomes = new BattleHitOutcomePresenter(ledger, projectionArbiter.Active,
            motion, impactFeedback, battleCamera, Clock, audioCueSink, reaction =>
            {
                closeup?.IncludeReaction(ledger.Identity, reaction.SourceParticipant);
                closeup?.IncludeReaction(ledger.Identity, reaction.TargetParticipant);
            });
        var receipt = hitOutcomes.Present(ledger, step, issuedAt);
        if (!ReferenceEquals(active, receipt) && !receipt.Completion.IsCompleted)
        { active = receipt; Watch(active, .45d, "ConflictCheckpoint"); }
        return receipt;
    }
    public PresentationReceipt Return(in ReturnPresentationCommand c)
    {
        if (c.Identity != ownedSnapshot.Identity || hitOutcomes == null || !hitOutcomes.ReadyForReturn || projectionArbiter?.Active == null)
            return Unavailable(c.Identity, DRPGBattleActionStage.Return);
        if (returnBarrier != null) return returnBarrier.Receipt;
        announcement.Hide();
        closeup?.BeginReturn(c.Identity);
        returnBarrier = new BattleReturnBarrier(c.Identity, projectionArbiter.Active, motion, battleCamera, cameraHome, Clock,
            Math.Max(0f, ownedSnapshot.Timing.ReturnSeconds), hitOutcomes.DefeatsAreVisibleAtHome, closeup);
        active = returnBarrier.Receipt;
        Watch(active, Math.Max(0d, ownedSnapshot.Timing.ReturnSeconds) + .1d, "PostReturnImpactReconcileCheckpoint");
        return active;
    }
    public DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal departures)
    {
        if (hitOutcomes != null)
        {
            var receipt = hitOutcomes.CompleteDefeats(departures, returnBarrier, defeatedRoster);
            if (!receipt.Receipt.Completion.IsCompleted || receipt.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed)
                completedDeparture = receipt;
            return receipt;
        }
        var result = new DRPGBattleDeparturePresentation(departures);
        result.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted,
            "Defeat transfer is not bound."));
        return result;
    }
    public PresentationReceipt ReconcileFormation(DRPGBattlePostReturnResult rules)
    {
        if (rules == null || activeLedger == null || projectionArbiter?.Active == null || !projectionArbiter.Active.IsActive ||
            !rules.Departures.Owner.Equals(projectionArbiter.Active.Owner) || rules.Departures.SourceFingerprint != activeLedger.Fingerprint ||
            rules.CommittedRevision != activeLedger.MutationJournal.BaseRevision + 2 || completedDeparture == null ||
            !completedDeparture.Departures.HasSameValues(rules.Departures) || !completedDeparture.Receipt.Completion.IsCompleted ||
            completedDeparture.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed)
            return Unavailable(ownedSnapshot.Identity, DRPGBattleActionStage.Return);
        if (formationBarrier != null) return ReferenceEquals(formationRules, rules) ? formationBarrier.Receipt : Unavailable(ownedSnapshot.Identity, DRPGBattleActionStage.Return);
        formationRules = rules;
        formationBarrier = new BattleFormationReconcileBarrier(projectionArbiter.Active, motion, Clock, activeLedger, rules);
        active = formationBarrier.Receipt;
        Watch(active, .30d, "PostReturnImpactReconcileCheckpoint"); return active;
    }
    public bool TryCaptureSettlementHomes(ActionIdentity identity, out System.Collections.Generic.IReadOnlyDictionary<ParticipantId, int> homes)
    {
        homes = null;
        if (identity != ownedSnapshot.Identity || projectionArbiter?.Active == null || !projectionArbiter.Active.IsActive ||
            activeLedger == null || formationRules == null || formationBarrier == null || !formationBarrier.Receipt.Completion.IsCompleted ||
            formationBarrier.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed || settlementPlan != null) return false;
        try { homes = projectionArbiter.Active.FrozenFormation.CaptureHomeSlots(activeLedger, formationRules, projectionArbiter.Active); return true; }
        catch (InvalidOperationException) { return false; }
    }
    public PresentationReceipt PresentSettlement(in SettlementPresentationCommand c)
    {
        if (c.Plan == null || c.Identity != ownedSnapshot.Identity || projectionArbiter?.Active == null ||
            !projectionArbiter.Active.IsActive || formationRules == null || formationBarrier == null ||
            !formationBarrier.Receipt.Completion.IsCompleted || formationBarrier.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed ||
            c.Plan.Ledger.BaseRevision != formationRules.CommittedRevision ||
            active != null && !active.Completion.IsCompleted && active.Stage != DRPGBattleActionStage.Settlement ||
            settlementPlan != null && !ReferenceEquals(settlementPlan, c.Plan))
            return Unavailable(c.Identity, DRPGBattleActionStage.Settlement);
        if (settlement == null)
        {
            settlementPlan = c.Plan;
            settlement = new BattleSettlementStepPresenter(c.Plan, projectionArbiter.Active, impactFeedback, battleCamera, Clock);
        }
        var receipt = settlement.Present(c);
        if (!ReferenceEquals(active, receipt) && !receipt.Completion.IsCompleted)
        { active = receipt; Watch(active, c.Plan.IsEmpty ? .30d : .45d, "SettlementCheckpoint"); }
        return receipt;
    }
    public DRPGBattleDeparturePresentation CompleteSettlementDefeats(DRPGBattleDepartureJournal facts)
    {
        if (settlement == null)
        {
            var rejected = new DRPGBattleDeparturePresentation(facts);
            rejected.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Settlement presenter is not bound."));
            return rejected;
        }
        var result = settlement.CompleteDefeats(facts, defeatedRoster);
        if (!result.Receipt.Completion.IsCompleted || result.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed)
            settlementDeparture = result;
        if (!result.Receipt.Completion.IsCompleted && !ReferenceEquals(active, result.Receipt))
        { active = result.Receipt; Watch(active, 0d, "PostSettlementReconcileCheckpoint"); }
        return result;
    }
    public PresentationReceipt ReconcileSettlementFormation(DRPGBattlePostReturnResult rules)
    {
        if (rules == null || settlementPlan == null || settlement == null || !settlement.IsComplete ||
            projectionArbiter?.Active == null || !projectionArbiter.Active.IsActive ||
            rules.Departures.Stage != DRPGBattleActionStage.Settlement ||
            !rules.Departures.HasSameValues(DRPGSettlementDepartureFacts.Describe(settlementPlan.Ledger)) ||
            rules.CommittedRevision != settlementPlan.Ledger.CommittedRevision + 1 || settlementDeparture == null ||
            !settlementDeparture.Departures.HasSameValues(rules.Departures) || !settlementDeparture.Receipt.Completion.IsCompleted ||
            settlementDeparture.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed)
            return Unavailable(ownedSnapshot.Identity, DRPGBattleActionStage.Settlement);
        if (settlementFormation != null) return ReferenceEquals(settlementFormationRules, rules) ? settlementFormation.Receipt :
            Unavailable(ownedSnapshot.Identity, DRPGBattleActionStage.Settlement);
        settlementFormationRules = rules;
        settlementFormation = new BattleFormationReconcileBarrier(projectionArbiter.Active, motion, Clock, activeLedger,
            rules, formationRules, Math.Max(1, settlementPlan.Steps.Count) + 1);
        active = settlementFormation.Receipt; Watch(active, .30d, "PostSettlementReconcileCheckpoint"); return active;
    }
    public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand c)
    {
        var r = new PresentationCleanupReceipt();
        if (active == null || active.Owner != c.Owner) { r.TryComplete(false); return r; }
        CancelReceipt(active, c.Reason);
        active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, c.Reason));
        r.TryComplete(true); return r;
    }
    public void Tick()
    {
        if (entryReceipt != null && !entryReceipt.Completion.IsCompleted && entryHandle != null && entryHandle.IsSettled)
        {
            if (entrySettledFrame < 0) entrySettledFrame = Clock.RenderedFrame;
            else if (Clock.RenderedFrame > entrySettledFrame)
            {
                var kind = entryHandle.Outcome == BattlePresentationOutcome.Completed ? PresentationReceiptTerminalKind.Completed :
                    entryHandle.Outcome == BattlePresentationOutcome.Skipped ? PresentationReceiptTerminalKind.Degraded : PresentationReceiptTerminalKind.Faulted;
                entryReceipt.TryComplete(new PresentationReceiptTerminal(kind, "Entry handle settled and final composition rendered."));
            }
        }
        watchdog.Tick();
        closeup?.Tick();
        motion?.Tick();
        hitOutcomes?.Tick();
        returnBarrier?.Tick();
        hitOutcomes?.TickDefeatMarkers();
        hitOutcomes?.TickDepartures();
        formationBarrier?.Tick();
        settlement?.Tick();
        settlementFormation?.Tick();
        announcement.Tick(); playback.Tick();
        if (active != null && active.Stage == DRPGBattleActionStage.ConflictComposition && !active.Completion.IsCompleted &&
            conflictHandle != null && conflictHandle.IsSettled &&
            (conflictMotion == null || conflictMotion.Completion.IsCompleted) &&
            Clock.Now - conflictStarted >= conflictDuration && Clock.RenderedFrame > conflictFrame)
        {
            var kind = conflictHandle.Outcome == BattlePresentationOutcome.Completed ? PresentationReceiptTerminalKind.Completed : PresentationReceiptTerminalKind.Degraded;
            active.TryComplete(new PresentationReceiptTerminal(kind, "Conflict composition rendered."));
        }
    }
    public void CancelReceipt(PresentationReceipt receipt, string reason)
    {
        if (!ReferenceEquals(active, receipt)) return;
        if (ReferenceEquals(receipt, entryReceipt))
        {
            if (entryHandle != null && !entryHandle.IsSettled) entryHandle.Cancel(BattlePresentationCancelReason.BattleHidden);
            if (entryHandle != null && !entryHandle.IsSettled)
                throw new InvalidOperationException("Entry cleanup did not settle its owned handle.");
            entryHandle = null;
            return;
        }
        if (settlementFormation != null && ReferenceEquals(receipt, settlementFormation.Receipt))
        {
            settlementFormation.Dispose();
            if (!receipt.Completion.IsCompleted) throw new InvalidOperationException("Settlement cleanup cannot substitute for rendered formation.");
            return;
        }
        if (formationBarrier != null && ReferenceEquals(receipt, formationBarrier.Receipt))
        {
            formationBarrier.Dispose();
            if (!receipt.Completion.IsCompleted) throw new InvalidOperationException("Formation cleanup cannot substitute for rendered reconciliation.");
            return;
        }
        announcement.Hide();
        announcement.Cancel(receipt); playback.Cancel(receipt, reason);
        hitOutcomes?.Cancel(receipt);
        settlement?.Cancel(receipt);
        if (receipt.Stage == DRPGBattleActionStage.Return)
        {
            returnBarrier?.Dispose();
            // A watchdog owns terminal publication during recovery. If cleanup
            // cannot prove a rendered home, it must fault, not grant Degraded.
            if (!receipt.Completion.IsCompleted)
                throw new InvalidOperationException("Return cleanup cannot substitute for a rendered home barrier.");
        }
        if (receipt.Stage == DRPGBattleActionStage.ConflictComposition && conflictHandle != null && !conflictHandle.IsSettled)
            conflictHandle.Cancel(BattlePresentationCancelReason.TimedOut);
        conflictHandle = null;
    }
}
