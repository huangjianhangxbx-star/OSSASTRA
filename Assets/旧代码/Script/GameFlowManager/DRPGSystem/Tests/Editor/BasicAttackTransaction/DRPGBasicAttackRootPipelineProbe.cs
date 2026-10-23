#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGBasicAttackRootPipelineProbe
{
    // Break caught: the real root stops at ReadyForImpact, or resolves before
    // windup / more than once. No test calls Commit or PresentImpact for it.
    public static void RootCommitsAtItsOnlyHitGateThenReturnsWithoutAdvancingTurn()
    {
        foreach (bool enemy in new[] { false, true })
        using (var f = new Fixture(enemy))
        {
            var actor = f.Domain.Session.CurrentActor;
            float hp = f.Target.CurrentHp;
            long revision = f.Domain.Session.TransactionState.BattleRevision;
            f.Start();
            f.Step(.749);
            Check(f.Target.CurrentHp == hp && f.Source.Events == 0, "Announcement committed damage.");
            f.Step(.002); f.Step(.121); f.Step(.299);
            Check(f.Target.CurrentHp == hp && f.Source.Events == 0, "Conflict/windup committed damage early.");
            f.Step(.002);
            Check(f.Target.CurrentHp < hp && f.Source.Events == 1,
                "Root did not commit exactly one real action at the end of its windup. " + f.Status);
            for (int i = 0; i < 80; i++) f.Step(.03);
            Check(f.Source.Events == 1 && f.Domain.Session.TransactionState.BattleRevision == revision + 4,
                "Root did not perform one impact, post-return reconciliation, settlement and final reconciliation.");
            Check(f.ActorView.transform.position == f.ActorHome && f.TargetView.transform.position == f.TargetHome,
                "Root did not wait for both real actor return paths.");
            Check(f.Root.ReadyForFinalization && ReferenceEquals(actor, f.Domain.Session.CurrentActor) && f.Source.Lease.IsActive &&
                !f.StartResult.Receipt.Completion.IsCompleted && f.CompletedEvents == 0,
                "Root advanced/completed before final publication and continuation. " + f.Status);
        }
    }

    public static void CleanupFailureStillReleasesRootAndProjection()
    {
        using (var f = new Fixture())
        {
            f.Start(); f.ReachHit(); f.Port.ThrowOnCancel = true;
            try { f.Root.Dispose(); } catch (InvalidOperationException) { }
            Check(!f.Source.Lease.IsActive && !f.Domain.Surface.ProjectionArbiter.IsLeased &&
                f.StartResult.Receipt.Completion.IsCompleted,
                "A leaf cleanup exception leaked the rule/projection lease or root terminal.");
            Check(f.Source.Events == 1 && f.CompletedEvents == 0, "Cleanup replayed impact or published ActionCompleted.");
        }
    }

    public static void DuplicateRootStartDoesNotReplaceTheLockedAction()
    {
        using (var f = new Fixture())
        {
            f.Start(); var original = f.StartResult.Receipt;
            var duplicate = f.Root.TryStart(f.Frozen, f.Frozen.Identity, f.Source.Lease, "duplicate-root");
            Check(!duplicate.IsAccepted && duplicate.Rejection == BasicAttackRejection.Busy &&
                f.Source.Lease.IsActive && !original.Completion.IsCompleted,
                "Duplicate start replaced/faulted the original locked root.");
            f.ReachHit(); Check(f.Source.Events == 1, "Original root no longer reached its single impact.");
        }
    }

    public static void MismatchedIdentityCannotConsumeOrDisposeTheSuppliedLease()
    {
        using (var f = new Fixture())
        {
            var wrong = new ActionIdentity(f.Frozen.Identity.BattleGeneration, f.Frozen.Identity.TurnSerial,
                f.Frozen.Identity.ActionId + 1);
            var rejected = f.Root.TryStart(f.Frozen, wrong, f.Source.Lease, "wrong-root");
            Check(!rejected.IsAccepted && f.Source.Lease.IsActive && !f.Domain.Surface.ProjectionArbiter.IsLeased && f.Source.Events == 0,
                "Mismatched root identity consumed or disposed another action lease.");
        }
    }

    public static void FrozenRulesSurviveAssetChangesDuringAnnouncement()
    {
        using (var f = new Fixture())
        {
            f.Start(); float hp = f.Target.CurrentHp; float mp = f.Actor.CurrentMp;
            DRPGBasicAttackPreludeProbe.Set(f.Domain.Player.Build.BasicAttack, "power", 10000f);
            DRPGBasicAttackPreludeProbe.Set(f.Domain.Player.Build.BasicAttack, "manaCost", 99f);
            f.ReachHit();
            Check(f.Target.CurrentHp == hp - 1f && f.Actor.CurrentMp == mp && f.Source.Events == 1,
                "Root re-read mutable asset power/cost after lock. " + f.Status);
        }
    }

    public static void CommittedObserverCannotReenterOrContinueDisposedRoot()
    {
        foreach (bool dispose in new[] { false, true })
        using (var f = new Fixture())
        {
            f.Domain.Session.ActionLedgerCommitted += _ => { f.Root.Tick(); if (dispose) f.Root.Dispose(); };
            f.Start(); f.ReachHit();
            Check(f.Source.Events == 1 && f.CompletedEvents == 0, "Commit notification reentry repeated rules or completed action.");
            Check(dispose ? !f.Source.Lease.IsActive && f.Visuals.ActiveNumbers().Length == 0 :
                f.Source.Lease.IsActive && f.Visuals.ActiveNumbers().Length == 1,
                "Disposed/reentrant root issued stale impact presentation or lost its live lease.");
        }
    }

    public static void ForeignReturnReceiptCannotUnlockOrCancelAnotherAction()
    {
        using (var f = new Fixture())
        {
            f.Port.ForeignReturn = new PresentationReceipt(new ActionIdentity(f.Frozen.Identity.BattleGeneration,
                f.Frozen.Identity.TurnSerial, f.Frozen.Identity.ActionId + 1), DRPGBattleActionStage.Return);
            f.Start(); f.ReachHit(); f.Step(.46);
            Check(f.StartResult.Receipt.Completion.IsCompleted && !f.Source.Lease.IsActive &&
                !f.Port.ForeignReturn.Completion.IsCompleted && f.CompletedEvents == 0 && f.Source.Events == 1,
                "A foreign return advanced the root or was cancelled by a non-owner.");
        }
    }

    public static void MissingDepartureNeverCommitsFormationOrCompletesAction()
    {
        using (var f = new Fixture())
        {
            long before = f.Domain.Session.TransactionState.BattleRevision;
            f.Port.HoldDeparture = true;
            f.Start(); f.ReachHit();
            for (int i = 0; i < 120; i++) f.Step(.03);
            Check(f.StartResult.Receipt.Completion.IsCompleted && !f.Source.Lease.IsActive &&
                f.Source.Events == 1 && f.Domain.Session.TransactionState.BattleRevision == before + 1 &&
                f.CompletedEvents == 0, "Missing departure skipped the Stage8 gate or leaked ownership.");
        }
    }

    public static void BoundRootRequiresProjectionBeforeAnnouncement()
    {
        using (var f = new Fixture())
        {
            f.Port.SuppressProjection = true; f.Start();
            Check(f.StartResult.Receipt.Completion.IsCompleted && !f.Source.Lease.IsActive && f.Source.Events == 0,
                "Bound root accepted a missing full-battlefield projection lease.");
        }
    }

    public static void RemovedPresentationBindingCannotCommitThroughCapturedOldPort()
    {
        using (var f = new Fixture())
        {
            f.Start(); float hp = f.Target.CurrentHp;
            f.Slot.TryUnbind(f.Frozen.Identity.BattleGeneration, f.Port);
            f.ReachHit();
            Check(f.StartResult.Receipt.Completion.IsCompleted && !f.Source.Lease.IsActive &&
                f.Target.CurrentHp == hp && f.Source.Events == 0 && !f.Domain.Surface.ProjectionArbiter.IsLeased,
                "Root used its captured old presentation port after the generation binding was removed.");
        }
    }

    internal static System.Collections.IEnumerator ProductionRootPipeline()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        using (var f = new Fixture(enemy, lethal, true))
        {
            var actor = f.Domain.Session.CurrentActor;
            float hp = f.Target.CurrentHp;
            long revision = f.Domain.Session.TransactionState.BattleRevision;
            double start = f.Clock.Now, hitTime = -1;
            bool sawHome = false; long homeFrame = -1;
            f.Start();
            while (!f.Root.ReadyForSettlement && !f.StartResult.Receipt.Completion.IsCompleted && f.Clock.Now - start < 10d)
            {
                yield return new WaitForEndOfFrame();
                f.Root.Tick();
                Check(ReferenceEquals(actor, f.Domain.Session.CurrentActor) && f.CompletedEvents == 0,
                    "Production root advanced the actor inside Stage3-8.");
                if (f.Source.Events == 0)
                    Check(f.Target.CurrentHp == hp && f.Domain.Pool.ReleaseCount == 0, "Production root mutated before hit.");
                else if (hitTime < 0)
                {
                    hitTime = f.Clock.Now;
                    Check(hitTime - start >= 1.17d && f.Visuals.ActiveNumbers().Length == 1 && f.Domain.Pool.ReleaseCount == 0,
                        "Production root skipped announcement/composition/windup or did not batch its number with impact.");
                }
                if (lethal && hitTime >= 0)
                {
                    if (f.Domain.Pool.ReleaseCount == 0)
                    {
                        Check(f.TargetView.gameObject.activeInHierarchy && f.Visuals.ActiveDefeatMarkers().Length == 1 &&
                            f.TargetView.VisualRoot.GetComponent<SpriteRenderer>().color.r <= .41f,
                            "Production lethal root lost original actor/X/dim before home.");
                        if (!sawHome && f.TargetView.transform.position == f.TargetHome)
                        { sawHome = true; homeFrame = f.Clock.RenderedFrame; }
                    }
                    else Check(sawHome && f.Clock.RenderedFrame > homeFrame, "Production root released an unrendered home pose.");
                }
            }
            Check(f.Root.ReadyForSettlement && f.Source.Lease.IsActive && f.Domain.Surface.ProjectionArbiter.IsLeased &&
                f.Source.Events == 1 && f.Domain.Session.TransactionState.BattleRevision == revision + 3 &&
                f.Root.SettlementLedger != null && f.Root.SettlementLedger.BaseRevision == revision + 2 &&
                f.Root.SettlementLedger.CommittedRevision == revision + 3 && !f.Root.ReadyForFinalization &&
                f.Domain.Pool.ReleaseCount == (lethal ? 1 : 0) && f.Defeated.Count == (lethal && enemy ? 1 : 0) &&
                !f.StartResult.Receipt.Completion.IsCompleted, "Production root Stage3-8 failed: " + f.Status +
                "; sideEnemy=" + enemy + "; lethal=" + lethal + "; revisionDelta=" + (f.Domain.Session.TransactionState.BattleRevision - revision) +
                "; stage9=" + (f.Root.SettlementLedger != null) + "; finalReady=" + f.Root.ReadyForFinalization +
                "; ruleLease=" + f.Source.Lease.IsActive + "; projectionLease=" + f.Domain.Surface.ProjectionArbiter.IsLeased +
                "; impacts=" + f.Source.Events + "; releases=" + f.Domain.Pool.ReleaseCount + "; incap=" + f.Defeated.Count +
                "; rootTerminal=" + f.StartResult.Receipt.Completion.IsCompleted);
        }
    }

    public static void LethalRootRetainsSameVisibleActorThroughHomeBeforeDeparture()
    {
        foreach (bool enemy in new[] { false, true })
        using (var f = new Fixture(enemy, true))
        {
            f.Start(); f.ReachHit();
            Check(f.Target.CurrentHp == 0 && f.Domain.Pool.ReleaseCount == 0 && f.Visuals.ActiveDefeatMarkers().Length == 1,
                "Lethal root did not reveal X on the original retained actor.");
            f.Step(.46);
            bool sawHome = false;
            long homeFrame = -1;
            for (int i = 0; i < 80; i++)
            {
                if (f.Domain.Pool.ReleaseCount == 0)
                {
                    Check(f.TargetView.gameObject.activeInHierarchy && f.Visuals.ActiveDefeatMarkers().Length == 1 &&
                        f.TargetView.VisualRoot.GetComponent<SpriteRenderer>().color.r <= .41f && f.Defeated.Count == 0,
                        "Lethal root hid/undimmed/replaced the actor before return completed.");
                    if (f.TargetView.transform.position == f.TargetHome && !sawHome)
                    { sawHome = true; homeFrame = f.Source.Clock.RenderedFrame; }
                }
                f.Step(.03);
                if (f.Domain.Pool.ReleaseCount > 0)
                    Check(sawHome && f.Source.Clock.RenderedFrame > homeFrame,
                        "Root released the actor before a home-rendered frame.");
            }
            Check(f.Root.ReadyForSettlement && f.Domain.Pool.ReleaseCount == 1 && f.Defeated.Count == (enemy ? 1 : 0) &&
                f.Source.Events == 1 && f.Source.Lease.IsActive && f.CompletedEvents == 0,
                "Lethal root failed typed departure/formation, or advanced before Stage9. " + f.Status);
        }
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly DRPGBasicAttackImpactSequenceProbe.Fixture Source;
        internal DRPGBasicAttackProjectionProbe.Fixture Domain => Source.Domain;
        internal readonly DRPGBasicAttackHitOutcomeProbe.Visuals Visuals;
        private readonly DRPGBasicAttackVisualRestoreProbe.ActorArt art;
        internal readonly DRPGBattleTransactionPresentationAdapter Adapter;
        internal readonly IDRPGBattlePresentationClock Clock;
        internal readonly InterceptPort Port;
        internal readonly DRPGBattleTransactionPresentationPortSlot Slot;
        internal readonly BattleDefeatedActorRoster Defeated;
        internal readonly DRPGStandardBasicAttackTransactionModule Root;
        internal readonly DRPGBasicAttackFrozenSnapshot Frozen;
        internal readonly BattleParticipantRuntime Actor, Target;
        internal readonly BattleWorldActorView ActorView, TargetView;
        internal readonly Vector3 ActorHome, TargetHome;
        internal RootActionStartResult StartResult;
        internal int CompletedEvents;
        internal Fixture(bool enemy = false, bool lethal = false, bool production = false, bool statuses = false, bool settlementDeaths = false)
        {
            Source = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemy, false, lethal: lethal,
                configureRules: DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins, prepareRules: false,
                beforeStart: statuses ? (Action<DRPGBattleSession, BattleParticipantRuntime, BattleParticipantRuntime>)((s, p, e) =>
                    DRPGSettlementPresentationProbe.Fixture.SeedStatuses(s, p, e, settlementDeaths, settlementDeaths)) : null);
            art = new DRPGBasicAttackVisualRestoreProbe.ActorArt(Source);
            Actor = enemy ? Domain.Enemy : Domain.Player; Target = enemy ? Domain.Player : Domain.Enemy;
            Domain.Pool.TryGet(Actor.RuntimeId, out ActorView); Domain.Pool.TryGet(Target.RuntimeId, out TargetView);
            ActorHome = ActorView.transform.position; TargetHome = TargetView.transform.position;
            Clock = production ? (IDRPGBattlePresentationClock)Domain.Root.AddComponent<UnityRenderFrameFence>() : Source.Clock;
            Visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(Domain.Root, Clock);
            Defeated = Domain.Root.AddComponent<BattleDefeatedActorRoster>();
            var template = new GameObject("RootIncapSpriteTemplate", typeof(SpriteRenderer));
            template.transform.SetParent(Domain.Root.transform, false); template.GetComponent<SpriteRenderer>().sprite = art.Idle;
            template.SetActive(false);
            var anchor = new GameObject("RootIncapAnchor").transform; anchor.SetParent(Domain.Root.transform, false);
            Domain.Pool.TryGet(Domain.Player.RuntimeId, out var playerView); anchor.position = playerView.transform.position;
            Defeated.Configure(new[] { new BattleIncapacitationBinding(new ParticipantId(Domain.Player.RuntimeId), template, anchor) });
            Adapter = new DRPGBattleTransactionPresentationAdapter(Clock, Visuals.Announcement,
                new BattleAnimationPlaybackRouter(Clock, (id, command) =>
                    ActorView.VisualRoot.GetComponent<BattleWorldActorSpriteDriver>().PlayBoundIntent(id, command.Snapshot.AnimationIntent)),
                _ => { var handle = new BattlePresentationHandle(); handle.TrySettle(BattlePresentationOutcome.Completed); return handle; },
                projectionArbiter: Domain.Surface.ProjectionArbiter, impactFeedback: Visuals.Feedback, battleCamera: Visuals.Camera,
                defeatedRoster: Defeated);
            Port = new InterceptPort(Adapter);
            var original = Source.Frozen;
            var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(Actor.RuntimeId, Target.RuntimeId,
                Domain.Player.Build.BasicAttack), Actor);
            Frozen = new DRPGBasicAttackFrozenSnapshot(original.Turn, original.Selection, original.Target,
                Domain.Player.Build.BasicAttack, Actor.Side, Target.Side,
                Pos(ActorHome), new ActionTracePosition(enemy ? 1 : -1, 0, 0), Pos(TargetHome),
                new ActionTracePosition(enemy ? -1 : 1, 0, 0), enemy ? -1 : 1, enemy ? 1 : -1,
                original.ActorVisualBaseline, original.TargetVisualBaseline, original.Timing, original.Semantics,
                new[] { new ParticipantId(Actor.RuntimeId), new ParticipantId(Target.RuntimeId) }, true, true, true, true,
                spec, Actor.RuntimeId.ToString()).WithIdentity(original.Identity);
            Slot = new DRPGBattleTransactionPresentationPortSlot(Domain.Session.BattleGeneration);
            Slot.TryBind(Domain.Session.BattleGeneration, Port);
            Root = new DRPGStandardBasicAttackTransactionModule(Slot);
            Root.BindRules(Domain.Session.StandardBasicAttackRules);
            Domain.Session.ActionCompleted += _ => CompletedEvents++;
        }
        internal void Start() { StartResult = Root.TryStart(Frozen, Frozen.Identity, Source.Lease, "root-pipeline-locked"); }
        internal void ReachHit() { Step(.749); Step(.002); Step(.121); Step(.301); }
        internal void Step(double delta) { Source.Clock.Now += delta; Source.Clock.RenderedFrame++; Root.Tick(); }
        internal string Status => StartResult.Receipt.Completion.IsCompleted ? StartResult.Receipt.Completion.Result.Detail :
            "Prelude=" + Root.Prelude?.Stage + "; ready=" + Root.Prelude?.ReadyForImpact;
        public void Dispose() { Root.Dispose(); Source.Dispose(); art.Dispose(); }
        private static ActionTracePosition Pos(Vector3 v) => new ActionTracePosition(v.x, v.y, v.z);
    }

    // Only the external leaf boundary is intercepted; the Unity adapter,
    // ledger, rule kernel, motion and projection remain real.
    internal sealed class InterceptPort : IStandardBasicAttackPresentationPort, IDRPGBattlePresentationPump,
        IStandardBasicAttackProjectionScope, IStandardBasicAttackReturnPort, IStandardBasicAttackSettlementPort
    {
        private readonly DRPGBattleTransactionPresentationAdapter real;
        internal bool ThrowOnCancel, HoldDeparture, SuppressProjection;
        internal bool HoldSettlementDeparture;
        internal bool HoldEmergencyCleanup;
        internal int EmergencyCleanupCalls;
        internal PresentationCleanupReceipt HeldEmergencyCleanup;
        internal Action OnSettlementHomes;
        internal DRPGBattleDeparturePresentation HeldSettlementDeparture, ForeignSettlementDeparture;
        private int settlementFinalOrdinal = 1;
        internal PresentationReceipt ForeignReturn;
        internal InterceptPort(DRPGBattleTransactionPresentationAdapter real) { this.real = real; }
        public IDRPGBattlePresentationClock Clock => real.Clock;
        public void Tick() => real.Tick();
        public void CancelReceipt(PresentationReceipt r, string reason)
        { real.CancelReceipt(r, reason); if (ThrowOnCancel) throw new InvalidOperationException("Injected leaf cleanup failure"); }
        public IStandardBasicAttackProjectionLifetime OpenProjection(in DRPGBasicAttackFrozenSnapshot s) => SuppressProjection ? null : real.OpenProjection(s);
        public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest r, out DRPGBasicAttackPresentationFreezeInput s) => real.TryCaptureFrozenSnapshot(r, out s);
        public PresentationReceipt Announce(in AnnouncementCommand c) => real.Announce(c);
        public PresentationReceipt EnterConflict(in ConflictCompositionCommand c) => real.EnterConflict(c);
        public PresentationReceipt PlayWindup(in BasicAttackWindupCommand c) => real.PlayWindup(c);
        public PresentationReceipt PresentImpact(in ImpactPresentationCommand c) => real.PresentImpact(c);
        public PresentationReceipt PresentReaction(in ReactionPresentationCommand c) => real.PresentReaction(c);
        public PresentationReceipt Return(in ReturnPresentationCommand c) => ForeignReturn ?? real.Return(c);
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand c)
        { settlementFinalOrdinal = c.Plan == null ? 1 : Math.Max(1, c.Plan.Steps.Count); return real.PresentSettlement(c); }
        public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand c)
        {
            EmergencyCleanupCalls++;
            var actual = real.EmergencyCleanup(c);
            if (!HoldEmergencyCleanup) return actual;
            if (HeldEmergencyCleanup == null) HeldEmergencyCleanup = new PresentationCleanupReceipt();
            return HeldEmergencyCleanup;
        }
        public DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal d) => HoldDeparture ? new DRPGBattleDeparturePresentation(d) : real.CompleteDefeats(d);
        public PresentationReceipt ReconcileFormation(DRPGBattlePostReturnResult r) => real.ReconcileFormation(r);
        public bool TryCaptureSettlementHomes(ActionIdentity id, out System.Collections.Generic.IReadOnlyDictionary<ParticipantId, int> homes)
        { bool valid = real.TryCaptureSettlementHomes(id, out homes); OnSettlementHomes?.Invoke(); return valid; }
        public DRPGBattleDeparturePresentation CompleteSettlementDefeats(DRPGBattleDepartureJournal d)
        {
            if (ForeignSettlementDeparture != null) return ForeignSettlementDeparture;
            if (!HoldSettlementDeparture) return real.CompleteSettlementDefeats(d);
            if (HeldSettlementDeparture == null) HeldSettlementDeparture = new DRPGBattleDeparturePresentation(d, settlementFinalOrdinal);
            return HeldSettlementDeparture;
        }
        public PresentationReceipt ReconcileSettlementFormation(DRPGBattlePostReturnResult r) => real.ReconcileSettlementFormation(r);
    }
    internal static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
