#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBasicAttackMotionProbe
{
    public static void ConflictSnapsBothWithoutChangingHomes()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock { Now = 3d, RenderedFrame = 7 };
            using (var motion = new BattleActorMotionController(lease, clock))
            {
                f.Pool.TryGet(f.Player.RuntimeId, out var actor);
                var homeActor = actor.transform.position; var homeTarget = f.Target.transform.position;
                var receipt = motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action,
                    new ParticipantId(f.Player.RuntimeId), new ParticipantId(f.Enemy.RuntimeId),
                    new Vector3(-1, 2, 3), new Vector3(1, 2, 3), clock.Now));
                Check(actor.transform.position == new Vector3(-1, 2, 3) &&
                    f.Target.transform.position == new Vector3(1, 2, 3),
                    "Both participants must enter explicit conflict positions in the issuing microstep.");
                Check(receipt.Completion.IsCompleted && motion.Journal.Count == 2 &&
                    motion.Journal[0].Timestamp == motion.Journal[1].Timestamp,
                    "Conflict motion was not one owned batch.");
                var checkpoint = lease.Capture(DRPGBattleProjectionCheckpointKind.ConflictCheckpoint);
                Check(checkpoint.Actors[0].Home.Position == homeActor && checkpoint.Actors[1].Home.Position == homeTarget,
                    "Conflict entry rewrote permanent homes.");
            }
        }
    }

    public static void HorizontalRecoilHasNoScaleOrKink()
    {
        foreach (double delta in new[] { 1d / 60d, 1d / 30d, .12d })
        foreach (int direction in new[] { -1, 1 })
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            using (var motion = new BattleActorMotionController(lease, clock))
            {
                motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action,
                    new ParticipantId(f.Player.RuntimeId), new ParticipantId(f.Enemy.RuntimeId),
                    new Vector3(-1, 2, 3), new Vector3(1, 2, 3), 0d));
                var scale = f.Target.transform.localScale;
                var lossy = f.Target.transform.lossyScale;
                var visualPosition = f.Target.VisualRoot.localPosition;
                var visualScale = f.Target.VisualRoot.localScale;
                int offset = motion.Journal.Count;
                var receipt = motion.Recoil(new RecoilMotionCommand(new HitOutcomeBeatId(f.Owner.Action, 0),
                    new ParticipantId(f.Enemy.RuntimeId), direction, .45f, 0d));
                for (int step = 1; step <= 40 && !receipt.Completion.IsCompleted; step++)
                {
                    clock.Now = Math.Min(step * delta, .26d); clock.RenderedFrame++;
                    motion.Tick();
                    Check(f.Target.transform.localScale == scale && f.Target.transform.lossyScale == lossy &&
                        f.Target.VisualRoot.localScale == visualScale && f.Target.VisualRoot.localPosition == visualPosition,
                        "Recoil changed actor or visual scale/pose.");
                }
                Check(motion.Journal.Count - offset >= 3,
                    "Recoil did not record an outward and return trajectory.");
                bool reversed = false; float last = 0f; float peak = 0f;
                for (int index = offset; index < motion.Journal.Count; index++)
                {
                    var row = motion.Journal[index];
                    float progress = direction * (row.After.x - 1f);
                    Check(Math.Abs(row.After.y - 2f) < .00001f && Math.Abs(row.After.z - 3f) < .00001f &&
                        progress >= -.00001f && progress <= .45001f,
                        "Recoil left its horizontal conflict-to-peak interval.");
                    if (progress < last - .00001f) reversed = true;
                    Check(!reversed || progress <= last + .00001f, "Recoil changed direction twice.");
                    peak = Math.Max(peak, progress); last = progress;
                }
                Check(reversed && Math.Abs(peak - .45f) < .00001f && Math.Abs(last) < .00001f &&
                    receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                    "Recoil must visit its exact peak once and finish back at conflict, not home.");
            }
        }
    }

    public static void TacticalMotionHasOneLeaseOwner()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        using (var first = new BattleActorMotionController(lease, new DRPGBasicAttackPreludeProbe.ProbeClock()))
        {
            bool rejected = false;
            try
            {
                using (var second = new BattleActorMotionController(lease, new DRPGBasicAttackPreludeProbe.ProbeClock())) { }
            }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Two independent motion writers acquired the same action projection.");
        }
    }

    public static void ProjectionCloseCancelsOnlyItsMotion()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            using (var motion = new BattleActorMotionController(lease, clock))
            {
                motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action,
                    new ParticipantId(f.Player.RuntimeId), new ParticipantId(f.Enemy.RuntimeId),
                    new Vector3(-1, 2, 3), new Vector3(1, 2, 3), 0d));
                var receipt = motion.Recoil(new RecoilMotionCommand(new HitOutcomeBeatId(f.Owner.Action, 0),
                    new ParticipantId(f.Enemy.RuntimeId), 1, .45f, 0d));
                lease.Dispose();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted,
                    "Projection closed while its private motion sequence and receipt remained alive.");
                Vector3 canonical = f.Target.transform.position;
                int count = motion.Journal.Count;
                clock.Now = 4d; clock.RenderedFrame++;
                motion.Tick();
                Check(f.Target.transform.position == canonical && motion.Journal.Count == count,
                    "An abandoned tween wrote over the closed canonical projection.");
            }
        }
    }

    public static void ProductionAdapterUsesLeasedConflictMotion()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            var obj = new GameObject("MotionAnnouncement", typeof(RectTransform));
            obj.transform.SetParent(f.Root.transform, false);
            var label = obj.AddComponent<TMPro.TextMeshProUGUI>();
            var group = obj.AddComponent<CanvasGroup>();
            var adapter = new DRPGBattleTransactionPresentationAdapter(clock,
                new BattleActionAnnouncementPresenter(label, group, clock),
                new BattleAnimationPlaybackRouter(clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Surface.ProjectionArbiter);
            var turn = f.Session.CurrentActorTurnToken;
            var frozen = new DRPGBasicAttackFrozenSnapshot(turn,
                new TargetSelectionToken(turn.BattleGeneration, turn.TurnSerial, 1, turn.Actor),
                new ParticipantId(f.Enemy.RuntimeId), f.Player.Build.BasicAttack).WithIdentity(f.Owner.Action);
            using (adapter.OpenProjection(frozen))
            {
                f.Pool.TryGet(f.Player.RuntimeId, out var actor);
                var home = actor.transform.position;
                adapter.Announce(new AnnouncementCommand(frozen, clock.Now, "Actor"));
                Check(actor.transform.position == home, "Announcement moved the attacker.");
                clock.Now = .75d; clock.RenderedFrame = 2;
                adapter.Tick();
                var receipt = adapter.EnterConflict(new ConflictCompositionCommand(frozen, clock.Now));
                Check(actor.transform.position == new Vector3(-.5f, 0, 0) &&
                    f.Target.transform.position == new Vector3(.5f, 0, 0),
                    "Production adapter did not route both actors through the leased motion owner.");
                Check(!receipt.Completion.IsCompleted, "Conflict skipped its readable hold.");
                clock.Now = .88d; clock.RenderedFrame = 3;
                adapter.Tick();
                Check(receipt.Completion.IsCompleted, "Conflict did not finish after its rendered hold.");
            }
        }
    }

    public static void LegacyConflictCannotBecomeASecondWriter()
    {
        foreach (int operation in new[] { 0, 1, 2, 3, 4, 5 })
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            var cameraObject = new GameObject("ContextCamera", typeof(Camera), typeof(BattleCameraPresenter));
            cameraObject.transform.SetParent(f.Root.transform, false);
            var camera = cameraObject.GetComponent<BattleCameraPresenter>();
            camera.Configure(cameraObject.GetComponent<Camera>(), null);
            var context = f.Root.AddComponent<BattleConflictPresentationContext>();
            context.Configure(f.Root.AddComponent<BattleHudPresenter>(), camera,
                f.Root.AddComponent<BattleStagePresenter>(), f.Pool, null);
            var scope = new BattlePresentationScope(f.Owner.Action.BattleGeneration,
                f.Owner.Action.TurnSerial, f.Owner.Action.ActionId);
            var request = new BattlePresentationRequest(718,
                BattlePresentationMoment.ActionWindup, scope,
                BattleParticipantPresentationSnapshot.From(f.Player),
                new[] { BattleParticipantPresentationSnapshot.From(f.Enemy) }, Array.Empty<int>(),
                "basic", EAnimationLogicState.Idle, Array.Empty<BattleHitPresentationSnapshot>(),
                BattlePresentationFacts.Empty, f.Owner.Action, DRPGBattleActionStage.ConflictComposition);
            context.ApplyActors(request, 1f, 1f, 0f, BattleOtherTargetPresentationPolicy.Keep);
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            using (var motion = new BattleActorMotionController(lease, new DRPGBasicAttackPreludeProbe.ProbeClock()))
            {
                motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action,
                    new ParticipantId(f.Player.RuntimeId), new ParticipantId(f.Enemy.RuntimeId),
                    new Vector3(-1, 2, 3), new Vector3(1, 2, 3), 0d));
                Vector3 scale = f.Target.transform.localScale;
                f.Pool.TryGet(f.Player.RuntimeId, out var actor);
                Vector3 actorScale = actor.transform.localScale;
                cameraObject.transform.position = new Vector3(0, 0, -7);
                if (operation == 0) context.ApplyActors(request, 2f, 3f, .4f, BattleOtherTargetPresentationPolicy.Keep);
                else if (operation == 1) context.Restore(scope);
                else if (operation == 2) context.ApplyAttacker(request, true, 2f);
                else if (operation == 3) context.ApplyTargets(request, true, 3f, .4f, BattleOtherTargetPresentationPolicy.Keep);
                else
                {
                    var handle = operation == 4 ? context.PlayImpactFeedback(request, new BattleFeedbackPresentationDefinition()) :
                        context.PlayRestore(request, new BattleExitPresentationDefinition());
                    bool rejected = handle.IsSettled;
                    if (!handle.IsSettled) handle.Cancel(BattlePresentationCancelReason.TimedOut);
                    Check(rejected, "Legacy feedback/return scheduled a second motion sequence (operation " + operation + ").");
                }
                Check(f.Target.transform.position == new Vector3(1, 2, 3) && f.Target.transform.localScale == scale &&
                    actor.transform.position == new Vector3(-1, 2, 3) && actor.transform.localScale == actorScale &&
                    cameraObject.transform.position == new Vector3(0, 0, -7),
                    "Legacy operation " + operation + " overwrote the action-owned conflict checkpoint.");
            }
            context.RestoreAll();
        }
    }

    public static void BothActorsReturnSmoothlyFromOneStartTime()
    {
        foreach (double delta in new[] { 1d / 60d, 1d / 30d, .12d })
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            using (var motion = new BattleActorMotionController(lease, clock))
            {
                f.Pool.TryGet(f.Player.RuntimeId, out var actor);
                Vector3 actorHome = actor.transform.position, targetHome = f.Target.transform.position;
                Vector3 actorConflict = new Vector3(-1, 2, 3), targetConflict = new Vector3(1, 2, 3);
                motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action,
                    new ParticipantId(f.Player.RuntimeId), new ParticipantId(f.Enemy.RuntimeId), actorConflict, targetConflict, 0d));
                clock.Now = 1d;
                var actorReceipt = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Player.RuntimeId), 1d));
                var targetReceipt = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Enemy.RuntimeId), 1d));
                Check(!actorReceipt.Completion.IsCompleted && !targetReceipt.Completion.IsCompleted &&
                    actor.transform.position == actorConflict && f.Target.transform.position == targetConflict,
                    "Return snapped/completed on issue instead of beginning a shared smooth return.");
                float priorActor = Vector3.Distance(actorConflict, actorHome), priorTarget = Vector3.Distance(targetConflict, targetHome);
                for (int step = 1; step <= 40 && !actorReceipt.Completion.IsCompleted; step++)
                {
                    clock.Now = 1d + step * delta; clock.RenderedFrame++;
                    motion.Tick();
                    float nextActor = Vector3.Distance(actor.transform.position, actorHome);
                    float nextTarget = Vector3.Distance(f.Target.transform.position, targetHome);
                    Check(nextActor < priorActor && nextTarget < priorTarget,
                        "Both actors must make simultaneous positive return progress on every nonterminal frame.");
                    if (clock.Now < 1.38d)
                        Check(!actorReceipt.Completion.IsCompleted && !targetReceipt.Completion.IsCompleted,
                            "Return completed below the approved minimum.");
                    priorActor = nextActor; priorTarget = nextTarget;
                }
                Check(actorReceipt.Completion.IsCompleted && targetReceipt.Completion.IsCompleted &&
                    actor.transform.position.Equals(actorHome) && f.Target.transform.position.Equals(targetHome),
                    "Return receipts did not terminate at the exact frozen homes.");
                int writes = motion.Journal.Count;
                motion.Tick();
                Check(motion.Journal.Count == writes, "Return completion emitted an extra restore write.");
            }
        }
    }

    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
