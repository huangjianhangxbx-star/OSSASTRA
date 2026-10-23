#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBasicAttackReactionMotionProbe
{
    internal static System.Collections.IEnumerator ProductionReactionReturn()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = Create(enemyFirst))
        using (var art = new DRPGBasicAttackReturnBarrierProbe.VisibleActorArt(f))
        {
            var clock = f.Domain.Root.AddComponent<UnityRenderFrameFence>();
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, clock);
            var adapter = new DRPGBattleTransactionPresentationAdapter(clock, visuals.Announcement,
                new BattleAnimationPlaybackRouter(clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback, battleCamera: visuals.Camera);
            using (var projection = adapter.OpenProjection(f.Frozen))
            {
                var homes = Positions(f);
                var identities = new Dictionary<int, int>();
                foreach (var view in f.Domain.Roster.ActiveViews) identities.Add(view.RuntimeId, view.GetInstanceID());
                var cameraHome = visuals.Camera.transform.position;
                var composition = adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, clock.Now));
                visuals.Camera.transform.position = new Vector3(1, 1, -5);
                double deadline = clock.Now + 8d;
                while (!composition.Completion.IsCompleted && clock.Now < deadline) { yield return null; adapter.Tick(); }
                Check(composition.Completion.IsCompleted, "Real reaction composition did not finish.");
                f.Commit();
                using (var queue = new DRPGBasicAttackImpactSequence(f.Ledger, adapter, clock, f.Lease))
                {
                    while (!queue.ReadyForReturn && queue.Fault.Length == 0 && clock.Now < deadline)
                    {
                        yield return null; queue.Tick();
                        AssertIdentityAndBystanders(f, homes, identities);
                        Check(visuals.ActiveNumbers().Length <= 1, "Real reaction names and damage numbers overlapped.");
                    }
                    var motion = Motion(adapter);
                    Check(queue.ReadyForReturn && queue.Fault.Length == 0 && motion.DisplacedParticipants.Count == 4,
                        "Real reaction participants were not staged: " + queue.Fault);
                    Check(f.Prepared.TryDescribeDepartures(out var departures, out var error), error);
                    var early = adapter.CompleteDefeats(departures);
                    Check(early.Receipt.Completion.IsCompleted && early.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted,
                        "Departure bypassed the reaction participants' return.");
                    var returning = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                    Check(!returning.Completion.IsCompleted, "Real reaction return snapped on entry.");
                    while (!returning.Completion.IsCompleted && clock.Now < deadline) { yield return null; adapter.Tick(); }
                    Check(returning.Completion.IsCompleted && returning.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                        "Real reaction return failed its rendered barrier.");
                    long firstFrame = -1; double firstTimestamp = -1;
                    foreach (int id in new[] { 7101, 7102, 7111, 7121 })
                    {
                        bool found = false;
                        foreach (var write in motion.Journal)
                            if (write.Stage == DRPGBattleActionStage.Return && write.Participant.Value == id)
                            {
                                Check(firstFrame < 0 || firstFrame == write.Frame && firstTimestamp == write.Timestamp,
                                    "Real reaction actors started return in different batches.");
                                firstFrame = write.Frame; firstTimestamp = write.Timestamp; found = true; break;
                            }
                        Check(found && View(f, id).transform.position == homes[id], "Real return lost a third-party frozen home.");
                    }
                    Check(visuals.Camera.transform.position == cameraHome && f.Lease.IsActive && f.Events == 1 &&
                        f.Domain.Session.CurrentActor.RuntimeId == f.Frozen.Turn.Actor.Value, "Real return released the actor or camera early.");
                    var transferred = adapter.CompleteDefeats(departures);
                    Check(transferred.Receipt.Completion.IsCompleted && transferred.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                        "Empty departure failed after the four-participant barrier.");
                }
            }
        }
    }

    public static void EvadedHasHorizontalDodgeWithoutDamageRecoil()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, false))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, f.Clock.Now));
                Advance(f, adapter, .13d); f.Commit(DRPGHitOutcome.Evaded);
                var hit = f.Ledger.PrimaryImpact.Hit;
                var target = View(f, hit.TargetRuntimeId);
                Vector3 origin = target.transform.position, scale = target.transform.lossyScale;
                var receipt = adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                Advance(f, adapter, .09d);
                float first = Mathf.Abs(target.transform.position.x - origin.x);
                Check(first > .45f && first < .65f, "Evaded is static or reused the .45 hit recoil instead of its dodge trajectory.");
                Advance(f, adapter, .03d);
                Check(Mathf.Abs(Mathf.Abs(target.transform.position.x - origin.x) - .65f) < .0001f,
                    "Dodge did not reach its one horizontal reversal point.");
                Check(target.transform.lossyScale == scale && target.transform.position.y == origin.y && target.transform.position.z == origin.z,
                    "Dodge altered scale or drifted off the horizontal axis.");
                Check(visuals.ActiveNumbers().Length == 1 && visuals.ActiveNumbers()[0].GetComponent<TMPro.TMP_Text>().text == "闪避" &&
                    hit.HpAfter == hit.HpBefore && ((DRPGBattleActionProjectionLease)scope).GetPresented(hit.TargetRuntimeId).Hp == hit.HpBefore,
                    "Evaded emitted a damage number or changed HP.");
                Advance(f, adapter, .18d);
                Check(target.transform.position == origin && !receipt.Completion.IsCompleted, "Dodge missed conflict or consumed the result hold.");
                Advance(f, adapter, .16d);
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && f.Events == 1,
                    "Dodge did not complete one outcome without rule replay.");
            }
        }
    }

    public static void ReactionPreparationRejectsEarlyReturnAndSkippedSteps()
    {
        using (var f = Create(false))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, f.Clock.Now));
                Advance(f, adapter, .13d); f.Commit();
                adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                Advance(f, adapter, .46d);
                var before = Positions(f);
                var skipped = adapter.PresentReaction(new ReactionPresentationCommand(f.Ledger, 2, f.Clock.Now));
                Check(skipped.Completion.IsCompleted && skipped.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted,
                    "A future reaction bypassed the ledger cursor.");
                foreach (var pair in before) Check(View(f, pair.Key).transform.position == pair.Value, "Rejected reaction moved an actor.");
                var command = new ReactionPresentationCommand(f.Ledger, 1, f.Clock.Now);
                var reaction = adapter.PresentReaction(command);
                var joined = Positions(f);
                Check(ReferenceEquals(reaction, adapter.PresentReaction(command)) && visuals.ActiveNumbers().Length == 1,
                    "Repeated reaction preparation replayed its motion or label.");
                f.Clock.Now += .13d; adapter.Tick();
                var hit = f.Ledger.Reactions[0].Batch.Hits[0];
                Check(projection.GetPresented(hit.TargetRuntimeId).Hp == hit.HpBefore,
                    "Elapsed time substituted for a rendered preparation frame.");
                var earlyReturn = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Check(earlyReturn.Completion.IsCompleted && earlyReturn.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted,
                    "Return began during an unfinished reaction.");
                foreach (var pair in joined) Check(View(f, pair.Key).transform.position == pair.Value, "Early return or replay moved a participant.");
                scope.Dispose();
                Check(visuals.ActiveNumbers().Length == 0 && reaction.Completion.IsCompleted && f.Lease.IsActive && f.Events == 1,
                    "Closing a preparation leaked its label, rule lease or callback.");
            }
        }
    }

    public static void TimedOutPreparationPreservesTheNextLedgerCursor()
    {
        using (var f = Create(false))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, f.Clock.Now));
                Advance(f, adapter, .13d); f.Commit();
                using (var queue = new DRPGBasicAttackImpactSequence(f.Ledger, adapter, f.Clock, f.Lease))
                {
                    f.Clock.Now += .46d; f.Clock.RenderedFrame++; queue.Tick();
                    f.Clock.Now += 2.5d; f.Clock.RenderedFrame++; queue.Tick();
                    Check(queue.Fault.Length == 0 && queue.Degraded && !queue.ReadyForReturn &&
                        projection.GetPresented(f.Ledger.Reactions[0].TargetParticipant.Value).Hp == f.Ledger.Reactions[0].Batch.Hits[0].HpAfter &&
                        visuals.ActiveNumbers().Length == 1 && visuals.ActiveNumbers()[0].GetComponent<TMPro.TMP_Text>().text == "追击",
                        "Preparation timeout lost its semantic cursor or overlapped/replayed outcomes: " + queue.Fault);
                    f.Clock.Now += .13d; f.Clock.RenderedFrame++; queue.Tick();
                    f.Clock.Now += .46d; f.Clock.RenderedFrame++; queue.Tick();
                    Check(queue.ReadyForReturn && f.Lease.IsActive && f.Events == 1, "Recovery did not finish without re-running damage.");
                }
            }
        }
    }

    public static void ReactionHasReadablePreparationBeforeOneOutcome()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = Create(enemyFirst))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, f.Clock.Now));
                Advance(f, adapter, .13d); f.Commit();
                adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                Advance(f, adapter, .46d);
                var hit = f.Ledger.Reactions[0].Batch.Hits[0];
                float before = projection.GetPresented(hit.TargetRuntimeId).Hp;
                var receipt = adapter.PresentReaction(new ReactionPresentationCommand(f.Ledger, 1, f.Clock.Now));
                Check(projection.GetPresented(hit.TargetRuntimeId).Hp == before && visuals.ActiveNumbers().Length == 1 &&
                    visuals.ActiveNumbers()[0].GetComponent<TMPro.TMP_Text>().text == "反击",
                    "Reaction skipped its compact name/preparation and revealed damage immediately.");
                Advance(f, adapter, .11d);
                Check(projection.GetPresented(hit.TargetRuntimeId).Hp == before && !receipt.Completion.IsCompleted,
                    "Reaction preparation ended before its minimum.");
                Advance(f, adapter, .02d);
                Check(projection.GetPresented(hit.TargetRuntimeId).Hp == hit.HpAfter && visuals.ActiveNumbers().Length == 1 &&
                    visuals.ActiveNumbers()[0].GetComponent<TMPro.TMP_Text>().text.StartsWith("-"),
                    "Reaction outcome did not replace its name with one synchronous damage result.");
                Advance(f, adapter, .44d);
                Check(!receipt.Completion.IsCompleted, "Reaction's name window consumed its outcome-readable window.");
                Advance(f, adapter, .02d);
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && f.Events == 1,
                    "Reaction outcome did not settle independently without a second rules commit.");
            }
        }
    }

    // Catches staging only the primary pair, forgetting a moved reaction source,
    // restarting a prior actor, or returning any member before the common batch.
    public static void DisplacedReactionActorsJoinTheSameReturnBatch()
    {
        foreach (bool enemyFirst in new[] { false, true })
        foreach (double delta in new[] { 1d / 60d, 1d / 30d, .12d })
        using (var f = Create(enemyFirst))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var projection = adapter.OpenProjection(f.Frozen))
            {
                var homes = Positions(f);
                var identities = new Dictionary<int, int>();
                foreach (var view in f.Domain.Roster.ActiveViews) identities.Add(view.RuntimeId, view.GetInstanceID());
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, f.Clock.Now));
                Advance(f, adapter, .13d); f.Commit();
                var motion = Motion(adapter);
                using (var queue = new DRPGBasicAttackImpactSequence(f.Ledger, adapter, f.Clock, f.Lease))
                {
                    Check(motion.DisplacedParticipants.Count == 2, "A future reaction moved before its ledger cursor.");
                    double deadline = f.Clock.Now + 3d;
                    while (!queue.ReadyForReturn && queue.Fault.Length == 0 && f.Clock.Now < deadline)
                    {
                        f.Clock.Now += delta; f.Clock.RenderedFrame++; queue.Tick();
                        AssertIdentityAndBystanders(f, homes, identities);
                    }
                    Check(queue.ReadyForReturn && queue.Fault.Length == 0, "Reaction queue failed: " + queue.Fault);
                    Check(motion.DisplacedParticipants.Count == 4 &&
                        View(f, 7111).transform.position != homes[7111] && View(f, 7121).transform.position != homes[7121],
                        "Third-party Counter/Pursuit sources never joined the conflict or were omitted from displaced set.");
                    var beforeReturn = Positions(f);
                    double returnAt = f.Clock.Now;
                    var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                    Check(!receipt.Completion.IsCompleted, "Return completed before the displaced participants moved.");
                    foreach (int id in new[] { 7101, 7102, 7111, 7121 })
                        Check(View(f, id).transform.position == beforeReturn[id], "An actor snapped home when return began.");
                    Advance(f, adapter, delta);
                    foreach (int id in new[] { 7101, 7102, 7111, 7121 })
                    {
                        var p = View(f, id).transform.position;
                        Check(p != beforeReturn[id] && Vector3.Distance(p, homes[id]) < Vector3.Distance(beforeReturn[id], homes[id]) && p != homes[id],
                            "One reaction participant did not begin a smooth return in the common frame: " + id);
                    }
                    deadline = f.Clock.Now + 2d;
                    while (!receipt.Completion.IsCompleted && f.Clock.Now < deadline) Advance(f, adapter, delta);
                    Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                        "Return barrier failed or lost a reaction participant.");
                    foreach (var pair in homes) Check(View(f, pair.Key).transform.position == pair.Value, "Return did not reach each frozen home.");
                    long batchFrame = -1;
                    foreach (int id in new[] { 7101, 7102, 7111, 7121 })
                    {
                        bool found = false;
                        foreach (var write in motion.Journal)
                            if (write.Stage == DRPGBattleActionStage.Return && write.Participant.Value == id)
                            {
                                Check(batchFrame < 0 || batchFrame == write.Frame, "Participants started return in different rendered frames.");
                                Check(Math.Abs(write.Timestamp - (returnAt + delta)) < .000001d, "Return start batches differ.");
                                batchFrame = write.Frame; found = true; break;
                            }
                        Check(found, "Return journal omitted a displaced participant.");
                    }
                    Check(f.Lease.IsActive && f.Events == 1 && f.Domain.Session.CurrentActor.RuntimeId == f.Frozen.Turn.Actor.Value,
                        "Reaction return changed actor, released the root, or recommitted damage.");
                }
            }
        }
    }

    internal static DRPGBasicAttackImpactSequenceProbe.Fixture Create(bool enemyFirst) =>
        new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, fullBattlefield: true,
            configureRules: DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins, thirdPartyReactions: true);
    internal static BattleActorMotionController Motion(DRPGBattleTransactionPresentationAdapter adapter) =>
        (BattleActorMotionController)typeof(DRPGBattleTransactionPresentationAdapter).GetField("motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter);
    internal static BattleWorldActorView View(DRPGBasicAttackImpactSequenceProbe.Fixture f, int id)
    { Check(f.Domain.Pool.TryGet(id, out var view), "Missing actor: " + id); return view; }
    internal static Dictionary<int, Vector3> Positions(DRPGBasicAttackImpactSequenceProbe.Fixture f)
    { var values = new Dictionary<int, Vector3>(); foreach (var view in f.Domain.Roster.ActiveViews) values.Add(view.RuntimeId, view.transform.position); return values; }
    internal static void Advance(DRPGBasicAttackImpactSequenceProbe.Fixture f, DRPGBattleTransactionPresentationAdapter adapter, double seconds)
    { f.Clock.Now += seconds; f.Clock.RenderedFrame++; adapter.Tick(); }
    private static void AssertIdentityAndBystanders(DRPGBasicAttackImpactSequenceProbe.Fixture f,
        Dictionary<int, Vector3> homes, Dictionary<int, int> identities)
    {
        foreach (var pair in identities) Check(View(f, pair.Key).GetInstanceID() == pair.Value, "Reaction replaced a leased actor.");
        foreach (int id in new[] { 7112, 7122 })
        {
            Vector3 current = View(f, id).transform.position;
            Check(current == homes[id],
                "Reaction moved an unrelated bystander " + id +
                " from " + homes[id] + " to " + current + ".");
        }
        Check(f.Domain.Pool.ReleaseCount == 0, "Reaction released an actor before return.");
    }
    internal static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
