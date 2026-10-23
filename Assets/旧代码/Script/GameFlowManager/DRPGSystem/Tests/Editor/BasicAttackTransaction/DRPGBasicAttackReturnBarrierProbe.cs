#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBasicAttackReturnBarrierProbe
{
    public static string ProductionStatus { get; private set; } = "NotRun";
    public static string StartProduction()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("This probe requires real Play Mode frames.");
        if (ProductionStatus == "Running") throw new InvalidOperationException("Production probe already running.");
        ProductionStatus = "Running";
        var host = new GameObject("Task9RealFrameProbeHost").AddComponent<ProductionHost>();
        host.Begin(); return ProductionStatus;
    }

    internal static System.Collections.IEnumerator ProductionLethalLeaseMatrix()
    {
        System.Collections.IEnumerator stageSeven =
            DRPGBasicAttackRootPipelineProbe.ProductionRootPipeline();
        try
        {
            while (stageSeven.MoveNext())
                yield return stageSeven.Current;
        }
        finally
        {
            (stageSeven as IDisposable)?.Dispose();
        }

        // The full Root settlement fixture kills one ally and one enemy on
        // separate party/enemy cursors and proves that neither transfer can
        // cross the second readable result or advance the current actor.
        DRPGBasicAttackRootSettlementProbe
            .RootSettlementDeathsFinalizeOnlyAfterBothResults();
        yield return null;
    }

    internal static System.Collections.IEnumerator ProductionSimultaneousReturn()
    {
        System.Collections.IEnumerator reactions =
            DRPGBasicAttackReactionMotionProbe.ProductionReactionReturn();
        try
        {
            while (reactions.MoveNext())
                yield return reactions.Current;
        }
        finally
        {
            (reactions as IDisposable)?.Dispose();
        }

        System.Collections.IEnumerator visualReturn =
            DRPGBasicAttackVisualRestoreProbe.ProductionVisualReturn();
        try
        {
            while (visualReturn.MoveNext())
                yield return visualReturn.Current;
        }
        finally
        {
            (visualReturn as IDisposable)?.Dispose();
        }

        AdapterReturnsBothActorsAndCameraBeforeRenderedTerminal();
        yield return null;
    }
    private sealed class ProductionHost : MonoBehaviour
    {
        private System.Collections.IEnumerator test;
        internal void Begin() { test = ProductionHitAndReturn(); StartCoroutine(Drive()); }
        private System.Collections.IEnumerator Drive()
        {
            while (true)
            {
                bool next;
                try { next = test.MoveNext(); }
                catch (Exception error) { ProductionStatus = "FAIL: " + error; break; }
                if (!next) { ProductionStatus = "PASS: 34 real-frame cases: 24 hit/return/departure/formation + 2 third-party reaction returns + 4 full sprite/environment returns + 4 root Stage3-8 pipelines"; break; }
                yield return test.Current;
            }
            (test as IDisposable)?.Dispose(); test = null; Destroy(gameObject);
        }
        private void OnDestroy() { (test as IDisposable)?.Dispose(); test = null; }
    }
    private static System.Collections.IEnumerator ProductionHitAndReturn()
    {
        foreach (bool enemyFirst in new[] { false, true })
        foreach (bool fullBattlefield in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        foreach (var outcome in new[] { DRPGHitOutcome.Hit, DRPGHitOutcome.Guarded, DRPGHitOutcome.Evaded })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, false, lethal: lethal, fullBattlefield: fullBattlefield,
            configureRules: fullBattlefield ?
                (Action<DRPGBasicAttackProjectionProbe.Fixture, BattleRuleMutationLease>)DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldRows :
                DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins))
        using (var art = new VisibleActorArt(f))
        {
            var clock = f.Domain.Root.AddComponent<UnityRenderFrameFence>();
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, clock);
            var defeatedRoster = f.Domain.Root.AddComponent<BattleDefeatedActorRoster>();
            var incapTemplate = new GameObject("IncapProductionTemplate", typeof(SpriteRenderer));
            incapTemplate.transform.SetParent(f.Domain.Root.transform, false);
            incapTemplate.GetComponent<SpriteRenderer>().sprite = art.Renderer.sprite; incapTemplate.SetActive(false);
            var incapAnchor = new GameObject("IncapProductionAnchor").transform;
            incapAnchor.SetParent(f.Domain.Root.transform, false); incapAnchor.position = new Vector3(0, -3, 0);
            defeatedRoster.Configure(new[] { new BattleIncapacitationBinding(f.Frozen.Target, incapTemplate, incapAnchor) });
            var adapter = new DRPGBattleTransactionPresentationAdapter(clock, visuals.Announcement,
                new BattleAnimationPlaybackRouter(clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback, battleCamera: visuals.Camera, defeatedRoster: defeatedRoster);
            var cameraHome = visuals.Camera.transform.position;
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                projection.TryGetView(f.Frozen.Turn.Actor.Value, out var actor);
                projection.TryGetView(f.Frozen.Target.Value, out var target);
                var actorHome = actor.transform.position; var targetHome = target.transform.position;
                var composition = adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, clock.Now));
                visuals.Camera.transform.position = new Vector3(1, 1, -5);
                double deadline = clock.Now + 5d;
                while (!composition.Completion.IsCompleted && clock.Now < deadline) { yield return null; adapter.Tick(); }
                Check(composition.Completion.IsCompleted, "Production composition did not render.");
                f.Commit(outcome);
                var hit = adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, clock.Now));
                Check(!hit.Completion.IsCompleted && visuals.ActiveNumbers().Length == 1,
                    "Production hit did not issue a synchronous world label: " + enemyFirst + "/" + outcome + "; " + (hit.Completion.IsCompleted ? hit.Completion.Result.Detail : "pending"));
                bool defeated = f.Ledger.PrimaryImpact.Hit.WasDefeated;
                Check(!defeated || visuals.ActiveDefeatMarkers().Length == 1 && art.Renderer.color.r <= .4f,
                    "Production lethal hit did not reveal X and dim.");
                while (!hit.Completion.IsCompleted && clock.Now < deadline) { yield return null; adapter.Tick(); }
                Check(hit.Completion.IsCompleted && hit.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed, "Production hit failed.");
                var returning = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                while (!returning.Completion.IsCompleted && clock.Now < deadline)
                {
                    yield return null; adapter.Tick();
                    Check(!defeated || visuals.ActiveDefeatMarkers().Length == 1 && target.gameObject.activeInHierarchy &&
                        art.Renderer.enabled && art.Renderer.color.r <= .4f && f.Domain.Pool.ReleaseCount == 0,
                        "Production lethal actor/marker vanished or flashed during real return frames.");
                }
                Check(returning.Completion.IsCompleted && returning.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    actor.transform.position == actorHome && target.transform.position == targetHome && visuals.Camera.transform.position == cameraHome &&
                    f.Domain.Pool.ReleaseCount == 0 && f.Events == 1,
                    "Production rendered return failed or advanced rules.");
                var currentActor = f.Domain.Session.CurrentActor;
                Check(f.Prepared.TryDescribeDepartures(out var departures, out var error), error);
                var transferred = adapter.CompleteDefeats(departures);
                while (!transferred.Receipt.Completion.IsCompleted && clock.Now < deadline) { yield return null; adapter.Tick(); }
                Check(transferred.Receipt.Completion.IsCompleted && transferred.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    transferred.Views.Count == (defeated ? 1 : 0) && f.Domain.Pool.ReleaseCount == (defeated ? 1 : 0) &&
                    projection.IsActive && f.Lease.IsActive, "Production typed departure failed or closed Root ownership.");
                Check(!defeated || !enemyFirst || defeatedRoster.TryGet(f.Frozen.Target, out var incap) && incap.activeInHierarchy &&
                    visuals.ActiveDefeatMarkers().Length == 1, "Production ally incap model/X did not survive transfer.");
                Check(f.Prepared.TryReconcileFormationAfterReturn(out var reconciled, out error) && ReferenceEquals(reconciled.Departures, departures) &&
                    ReferenceEquals(f.Domain.Session.CurrentActor, currentActor) && f.Events == 1 && f.Lease.IsActive,
                    "Production post-transfer rule reconciliation advanced the actor or repeated impact: " + error);
                var formation = adapter.ReconcileFormation(reconciled);
                while (!formation.Completion.IsCompleted && clock.Now < deadline)
                {
                    yield return null; adapter.Tick();
                    var views = f.Domain.Roster.ActiveViews;
                    for (int i = 0; i < views.Count; i++)
                    for (int j = i + 1; j < views.Count; j++)
                        Check(Vector3.Distance(views[i].transform.position, views[j].transform.position) > .001f,
                            "Production formation overlapped two active actors.");
                }
                Check(formation.Completion.IsCompleted && formation.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    projection.IsActive && f.Lease.IsActive && ReferenceEquals(currentActor, f.Domain.Session.CurrentActor),
                    "Production whole-formation barrier failed or advanced the turn.");
            }
        }
        var reactions = DRPGBasicAttackReactionMotionProbe.ProductionReactionReturn();
        try { while (reactions.MoveNext()) yield return reactions.Current; }
        finally { (reactions as IDisposable)?.Dispose(); }
        var visualReturn = DRPGBasicAttackVisualRestoreProbe.ProductionVisualReturn();
        try { while (visualReturn.MoveNext()) yield return visualReturn.Current; }
        finally { (visualReturn as IDisposable)?.Dispose(); }
        var rootPipeline = DRPGBasicAttackRootPipelineProbe.ProductionRootPipeline();
        try { while (rootPipeline.MoveNext()) yield return rootPipeline.Current; }
        finally { (rootPipeline as IDisposable)?.Dispose(); }
    }
    internal sealed class VisibleActorArt : IDisposable
    {
        private readonly Sprite sprite;
        internal readonly SpriteRenderer Renderer;
        internal VisibleActorArt(DRPGBasicAttackImpactSequenceProbe.Fixture f)
        {
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), 1f);
            foreach (var view in f.Domain.Roster.ActiveViews)
            {
                var renderer = view.VisualRoot.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                if (view.RuntimeId == f.Frozen.Target.Value) Renderer = renderer;
            }
            var snapshot = DRPGBattlePresentationSnapshot.Capture(f.Domain.Session);
            f.Domain.Roster.Bind(snapshot.PartyMembers, snapshot.Enemies, true);
        }
        public void Dispose() { if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite); }
    }
    public static void AdapterReturnsBothActorsAndCameraBeforeRenderedTerminal()
    {
        foreach (double delta in new[] { 1d / 60d, 1d / 30d, .12d })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, withReactions: false))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var cameraHome = visuals.Camera.transform.position; float fovHome = visuals.Camera.fieldOfView;
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                projection.TryGetView(f.Domain.Player.RuntimeId, out var actor);
                Vector3 actorHome = actor.transform.position, targetHome = f.Domain.Target.transform.position;
                var visualHome = actor.VisualRoot.localPosition;
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                visuals.Camera.transform.position = new Vector3(1, 1, -5); visuals.Camera.fieldOfView = 42f;
                f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                f.Clock.Now = .59d; f.Clock.RenderedFrame++; adapter.Tick();
                actor.VisualRoot.localPosition += new Vector3(.25f, .15f, 0);
                Vector3 actorStart = actor.transform.position, targetStart = f.Domain.Target.transform.position;
                Vector3 cameraStart = visuals.Camera.transform.position, visualStart = actor.VisualRoot.localPosition;
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Check(!receipt.Completion.IsCompleted && actor.transform.position == actorStart &&
                    f.Domain.Target.transform.position == targetStart && visuals.Camera.transform.position == cameraStart &&
                    actor.VisualRoot.localPosition == visualStart, "Return snapped or completed on issue.");
                double start = f.Clock.Now;
                float previousActor = Vector3.Distance(actorStart, actorHome), previousCamera = Vector3.Distance(cameraStart, cameraHome);
                while (f.Clock.Now - start < .38d)
                {
                    f.Clock.Now += delta; f.Clock.RenderedFrame++; adapter.Tick();
                    float remaining = Vector3.Distance(actor.transform.position, actorHome);
                    float cameraRemaining = Vector3.Distance(visuals.Camera.transform.position, cameraHome);
                    Check(remaining < previousActor && cameraRemaining < previousCamera &&
                        actor.VisualRoot.localPosition != visualStart,
                        "Actor, VisualRoot or camera stalled instead of starting in the same return batch.");
                    Check(!receipt.Completion.IsCompleted, "Return skipped the home rendered fence.");
                    previousActor = remaining; previousCamera = cameraRemaining;
                }
                Check(actor.transform.position == actorHome && f.Domain.Target.transform.position == targetHome &&
                    actor.VisualRoot.localPosition == visualHome && visuals.Camera.transform.position == cameraHome &&
                    visuals.Camera.fieldOfView == fovHome && f.Domain.Pool.ReleaseCount == 0,
                    "Return did not reach the exact frozen homes and camera, or released an actor early.");
                adapter.Tick(); Check(!receipt.Completion.IsCompleted, "An extra Tick substituted for a rendered frame.");
                f.Clock.RenderedFrame++; adapter.Tick();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && f.Lease.IsActive && f.Events == 1,
                    "Return fence did not complete after rendering or advanced rules.");
                Check(ReferenceEquals(receipt, adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity))), "Duplicate return started another batch.");
            }
        }
    }

    public static void ReturnCannotSkipPendingReaction()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                f.Clock.Now = .59d; f.Clock.RenderedFrame++; adapter.Tick();
                var position = f.Domain.Target.transform.position;
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted && f.Domain.Target.transform.position == position,
                    "Return bypassed unpresented reactions or moved actors before queue completion.");
            }
        }
    }

    public static void ReturnTimeoutCannotPretendHomeWasRendered()
    {
        RunReturnTimeout(false);
    }

    internal static System.Collections.IEnumerator ProductionReturnTimeoutSurvivor()
    {
        yield return null;
        RunReturnTimeout(false);
        yield return null;
    }

    internal static System.Collections.IEnumerator ProductionReturnTimeoutLethal()
    {
        yield return null;
        RunReturnTimeout(true);
        yield return null;
    }

    private static void RunReturnTimeout(bool lethal)
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, withReactions: false, lethal: lethal))
        using (var art = new DRPGBasicAttackVisualRestoreProbe.ActorArt(f))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                f.Clock.Now = .59d; f.Clock.RenderedFrame++; adapter.Tick();
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                f.Clock.Now = 3.5d; adapter.Tick();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted && f.Lease.IsActive,
                    "Timed-out unrendered return was degraded into success and could cross the defeat barrier.");
                Check(!lethal || (f.Domain.Target.gameObject.activeInHierarchy &&
                    visuals.ActiveDefeatMarkers().Length == 1 && f.Domain.Pool.ReleaseCount == 0),
                    "Lethal return timeout removed the target or X marker before a rendered home barrier.");
            }
        }
    }
    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
