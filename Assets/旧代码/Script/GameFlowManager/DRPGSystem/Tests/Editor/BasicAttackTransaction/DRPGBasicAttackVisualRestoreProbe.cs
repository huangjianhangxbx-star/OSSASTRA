#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBasicAttackVisualRestoreProbe
{
    public static void ReturningHiddenVisualHonorsActivationReentry()
    {
        foreach (bool closeOnEnable in new[] { false, true })
        using (var f = Create(false))
        using (var art = new ActorArt(f))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            var scope = adapter.OpenProjection(f.Frozen);
            try
            {
                var lease = (DRPGBattleActionProjectionLease)scope; lease.TryGetView(f.Frozen.Turn.Actor.Value, out var actor);
                var body = actor.VisualRoot.GetComponent<SpriteRenderer>(); var accessory = actor.VisualRoot.GetChild(0);
                StartImpact(f, adapter, art); var privateMesh = body.sprite;
                accessory.gameObject.SetActive(false);
                var callback = accessory.gameObject.AddComponent<ActivationCallback>();
                bool activated = false;
                callback.Activated = () => { activated = true; if (closeOnEnable) { scope.Dispose(); body.color = Color.blue; } };
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Advance(f, adapter, .4d); Advance(f, adapter, .1d);
                Check(activated && accessory.gameObject.activeSelf, "Frozen visual ActiveSelf was not restored: callback=" + activated + " active=" + accessory.gameObject.activeSelf);
                if (closeOnEnable) Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted &&
                    body.color == Color.blue && f.Lease.IsActive, "Activation reentry allowed a stale restore write or closed rule ownership.");
                else Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && privateMesh == null,
                    "Completed return retained its private normalized Sprite resource.");
            }
            finally { scope.Dispose(); }
        }
    }
    [ExecuteAlways]
    private sealed class ActivationCallback : MonoBehaviour
    {
        internal Action Activated;
        private void OnEnable() { Activated?.Invoke(); }
    }

    public static void RenderedFeetDoNotDropOrPopAcrossReturnSpriteSwap()
    {
        foreach (bool enemyFirst in new[] { false, true })
        foreach (double delta in new[] { 1d / 60d, .12d })
        using (var f = Create(enemyFirst))
        using (var art = new ActorArt(f))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var lease = (DRPGBattleActionProjectionLease)scope; lease.TryGetView(f.Frozen.Turn.Actor.Value, out var actor);
                var body = actor.VisualRoot.GetComponent<SpriteRenderer>(); float height = body.bounds.size.y;
                var originalBounds = art.Attack.bounds; var originalVertices = art.Attack.vertices;
                StartImpact(f, adapter, art);
                Check(Mathf.Abs(body.bounds.min.y - actor.transform.position.y) < .0001f, "Attack frame was not initially floor aligned.");
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                for (int i = 0; i < 60 && !receipt.Completion.IsCompleted; i++)
                {
                    Advance(f, adapter, delta);
                    Check(Mathf.Abs(body.bounds.min.y - actor.transform.position.y) < .0001f && Mathf.Abs(body.bounds.size.y - height) < .0001f,
                        "Rendered attack art fell below its motion-root floor or inflated while returning: " + body.bounds);
                }
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && body.sprite == art.Idle,
                    "Continuous floor-aligned return did not seal the idle art.");
                Check(art.Attack.bounds == originalBounds && art.Attack.vertices.Length == originalVertices.Length,
                    "Bound frame normalization modified the authored Sprite asset.");
                for (int i = 0; i < originalVertices.Length; i++) Check(art.Attack.vertices[i] == originalVertices[i], "Authored sprite geometry was mutated.");
            }
        }
    }

    public static void InvalidPoseBindingRejectsTheWholeReturnBeforeMotion()
    {
        foreach (bool foreignPlayback in new[] { false, true })
        using (var f = Create(false))
        using (var art = new ActorArt(f))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var lease = (DRPGBattleActionProjectionLease)scope; lease.TryGetView(f.Frozen.Turn.Actor.Value, out var actor);
                StartImpact(f, adapter, art);
                if (foreignPlayback)
                {
                    var wrong = new ActionIdentity(f.Frozen.Identity.BattleGeneration, f.Frozen.Identity.TurnSerial, f.Frozen.Identity.ActionId + 1);
                    actor.VisualRoot.GetComponent<BattleWorldActorSpriteDriver>().PlayBoundIntent(
                        new AnimationPlaybackIdentity(wrong, DRPGBattleActionStage.Windup, 0, 2, 0), EAnimationLogicState.Skill_1).Dispose();
                }
                else actor.VisualRoot.GetChild(0).SetParent(actor.ShadowAnchor, false);
                var actorPosition = actor.transform.position; var targetPosition = f.Domain.Target.transform.position;
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted,
                    "Return accepted a reparented visual or foreign playback identity: " + foreignPlayback);
                Advance(f, adapter, .2d);
                Check(actor.transform.position == actorPosition && f.Domain.Target.transform.position == targetPosition,
                    "Rejected return left part of the batch moving.");
            }
        }
    }

    public static void LostEnvironmentCancelsOnlyTheOwnedReturn()
    {
        using (var f = Create(false))
        using (var art = new ActorArt(f))
        {
            var background = new GameObject("LostBackground", typeof(SpriteRenderer)); background.transform.SetParent(f.Domain.Root.transform, false);
            background.GetComponent<SpriteRenderer>().sprite = art.Idle;
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var lease = (DRPGBattleActionProjectionLease)scope; lease.TryGetView(f.Frozen.Turn.Actor.Value, out var actor);
                StartImpact(f, adapter, art);
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Advance(f, adapter, .1d);
                UnityEngine.Object.DestroyImmediate(background);
                Advance(f, adapter, .01d);
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted,
                    "Lost environment binding falsely completed the restore.");
                var position = actor.transform.position; var pose = actor.VisualRoot.localPosition;
                Advance(f, adapter, .1d);
                Check(actor.transform.position == position && actor.VisualRoot.localPosition == pose && lease.IsActive && f.Lease.IsActive,
                    "Faulted return left an owned actor tween running or closed unrelated rule ownership.");
            }
        }
    }

    public static void BystandersRestoreWithTheSameReturnBatch()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, false, false, fullBattlefield: true,
            configureRules: DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins))
        using (var art = new ActorArt(f))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var lease = (DRPGBattleActionProjectionLease)scope;
                lease.TryGetView(7112, out var bystander); var body = bystander.VisualRoot.GetComponent<SpriteRenderer>();
                var home = bystander.transform.position; var color = body.color;
                StartImpact(f, adapter, art);
                body.color = color * .3f;
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Advance(f, adapter, .1d);
                Check(body.color.r > color.r * .3f && body.color.r < color.r && bystander.transform.position == home,
                    "Undisplaced bystander failed to restore alongside the tactical participants.");
                for (int i = 0; i < 5; i++) Advance(f, adapter, .1d);
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    body.sprite == art.Idle && body.color == color && bystander.transform.position == home,
                    "Bystander restoration remained incomplete or moved its fixed home.");
            }
        }
    }

    public static void EnvironmentRestoresWithoutRewindingSemanticUi()
    {
        using (var f = Create(false))
        using (var art = new ActorArt(f))
        {
            var hud = new GameObject("OwnedHud", typeof(RectTransform), typeof(CanvasGroup));
            hud.transform.SetParent(f.Domain.Root.transform, false);
            var group = hud.GetComponent<CanvasGroup>(); group.alpha = .8f; group.interactable = false; group.blocksRaycasts = false;
            var rect = (RectTransform)hud.transform; rect.anchoredPosition = new Vector2(20, 30); rect.sizeDelta = new Vector2(300, 80);
            var hp = new GameObject("SemanticHp", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            hp.transform.SetParent(hud.transform, false); var fill = hp.GetComponent<UnityEngine.UI.Image>(); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillAmount = 1f;
            var background = new GameObject("BoundBackground", typeof(SpriteRenderer)); background.transform.SetParent(f.Domain.Root.transform, false);
            var renderer = background.GetComponent<SpriteRenderer>(); renderer.sprite = art.Idle; renderer.color = Color.white;
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                StartImpact(f, adapter, art);
                group.alpha = .05f; rect.anchoredPosition += new Vector2(60, 0); renderer.color = new Color(.2f, .2f, .2f, .4f);
                fill.fillAmount = .37f; fill.color = Color.red;
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Check(group.alpha == .05f && rect.anchoredPosition.x == 80f, "Environment snapped on return issue.");
                Advance(f, adapter, .1d);
                Check(group.alpha > .05f && group.alpha < .8f && renderer.color.r > .2f && renderer.color.r < 1f &&
                    rect.anchoredPosition.x > 20f && rect.anchoredPosition.x < 80f,
                    "HUD/background did not start a smooth restoration in the return batch.");
                for (int i = 0; i < 5; i++) Advance(f, adapter, .1d);
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    group.alpha == .8f && !group.interactable && !group.blocksRaycasts && rect.anchoredPosition == new Vector2(20, 30) &&
                    renderer.color == Color.white && fill.fillAmount == .37f && fill.color == Color.red,
                    "Environment return rewound semantic HP/UI or failed to restore presentation state.");
            }
        }
    }

    // Catches returning only VisualRoot while leaving nested pose/tint and the
    // real driver's intent latch active across canonical rebind.
    public static void SurvivorRestoresFrozenArtWithoutCanonicalSnap()
    {
        foreach (bool enemyFirst in new[] { false, true })
        foreach (double delta in new[] { 1d / 60d, 1d / 30d, .12d })
        using (var f = Create(enemyFirst))
        using (var art = new ActorArt(f))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            var scope = adapter.OpenProjection(f.Frozen);
            try
            {
                var lease = (DRPGBattleActionProjectionLease)scope;
                lease.TryGetView(f.Frozen.Turn.Actor.Value, out var actor);
                var body = actor.VisualRoot.GetComponent<SpriteRenderer>();
                var accessory = actor.VisualRoot.GetChild(0);
                var tint = body.color;
                int sortingHome = body.sortingOrder;
                var visualHome = new BattleProjectionPose(actor.VisualRoot);
                var accessoryHome = new BattleProjectionPose(accessory);
                StartImpact(f, adapter, art);
                actor.VisualRoot.localRotation = Quaternion.Euler(0, 0, 12);
                accessory.localPosition += new Vector3(.3f, .2f, 0);
                accessory.localScale *= .6f;
                body.color = new Color(.1f, .2f, .3f, .25f);
                body.flipX = !body.flipX; body.sortingOrder += 40;
                var startColor = body.color;
                var startPose = actor.VisualRoot.localPosition;
                var returning = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Check(DisplaysFrame(body, art.Attack) && body.color == startColor && actor.VisualRoot.localPosition == startPose,
                    "Return issue snapped the visual before its first sample.");
                Advance(f, adapter, delta);
                Check(body.color.r > startColor.r && body.color.r < tint.r && body.color.a > startColor.a && body.color.a < tint.a,
                    "Survivor tint/alpha did not smoothly join the return batch.");
                double deadline = f.Clock.Now + 2;
                while (!returning.Completion.IsCompleted && f.Clock.Now < deadline) Advance(f, adapter, delta);
                Check(returning.Completion.IsCompleted && returning.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                    "Visual restoration did not finish through the owned return barrier.");
                Check(body.sprite == art.Idle && body.color == tint && !body.flipX && body.sortingOrder == sortingHome,
                    "Frozen sprite/tint/renderer properties were not restored: idle=" + (body.sprite == art.Idle) +
                    " color=" + body.color + " expected=" + tint + " flip=" + body.flipX + " sorting=" + body.sortingOrder);
                Check(PoseEquals(actor.VisualRoot, visualHome) && PoseEquals(accessory, accessoryHome),
                    "Nested visual transform was not restored to the frozen pose.");
                // Closing flushes the real Session snapshot through the real
                // SpriteDriver.Bind, not a fake restore callback.
                scope.Dispose();
                Check(body.sprite == art.Idle && PoseEquals(actor.VisualRoot, visualHome) && PoseEquals(accessory, accessoryHome),
                    "Canonical rebind resurrected the attack pose or snapped its transform.");
            }
            finally { scope.Dispose(); }
        }
    }

    public static void DefeatPoseAndTintRemainUntilHomeRendered()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = Create(enemyFirst, true))
        using (var art = new ActorArt(f))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var lease = (DRPGBattleActionProjectionLease)scope;
                lease.TryGetView(f.Frozen.Target.Value, out var target);
                var body = target.VisualRoot.GetComponent<SpriteRenderer>();
                StartImpact(f, adapter, art);
                var pose = new BattleProjectionPose(target.VisualRoot); var color = body.color;
                var receipt = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                for (int i = 0; i < 6; i++)
                {
                    Advance(f, adapter, .1d);
                    Check(DisplaysFrame(body, art.Attack) && body.color == color && PoseEquals(target.VisualRoot, pose) &&
                        target.gameObject.activeInHierarchy && visuals.ActiveDefeatMarkers().Length == 1 && f.Domain.Pool.ReleaseCount == 0,
                        "Defeat return restored alive art/tint or released the pinned view before transfer.");
                }
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                    "Preserved defeat art did not pass HomeRendered.");
            }
        }
    }

    internal static System.Collections.IEnumerator ProductionVisualReturn()
    {
        foreach (bool enemyFirst in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, false, lethal: lethal, fullBattlefield: true,
            configureRules: DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins))
        using (var art = new ActorArt(f))
        {
            var clock = f.Domain.Root.AddComponent<UnityRenderFrameFence>();
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, clock);
            var hud = new GameObject("RealFrameHud", typeof(RectTransform), typeof(CanvasGroup)); hud.transform.SetParent(f.Domain.Root.transform, false);
            var group = hud.GetComponent<CanvasGroup>(); group.alpha = .8f; group.interactable = false; group.blocksRaycasts = false;
            var background = new GameObject("RealFrameBackground", typeof(SpriteRenderer)); background.transform.SetParent(f.Domain.Root.transform, false);
            var backgroundRenderer = background.GetComponent<SpriteRenderer>(); backgroundRenderer.sprite = art.Idle; backgroundRenderer.color = Color.white;
            var playback = new BattleAnimationPlaybackRouter(clock, (identity, command) =>
            {
                Check(f.Domain.Pool.TryGet(command.Snapshot.Turn.Actor.Value, out var bound), "Missing real-frame windup actor.");
                return bound.VisualRoot.GetComponent<BattleWorldActorSpriteDriver>().PlayBoundIntent(identity, EAnimationLogicState.Skill_1);
            });
            var adapter = new DRPGBattleTransactionPresentationAdapter(clock, visuals.Announcement, playback,
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback, battleCamera: visuals.Camera);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var lease = (DRPGBattleActionProjectionLease)scope;
                lease.TryGetView(f.Frozen.Turn.Actor.Value, out var actor); lease.TryGetView(f.Frozen.Target.Value, out var target);
                var home = new BattleProjectionPose(actor.VisualRoot); var actorTint = actor.VisualRoot.GetComponent<SpriteRenderer>().color;
                float renderedHeight = actor.VisualRoot.GetComponent<SpriteRenderer>().bounds.size.y;
                var actorIdentity = actor.GetInstanceID(); var targetIdentity = target.GetInstanceID();
                var cameraHome = visuals.Camera.transform.position;
                double deadline = clock.Now + 8;
                var composition = adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, clock.Now));
                while (!composition.Completion.IsCompleted && clock.Now < deadline) { yield return null; adapter.Tick(); }
                var windup = adapter.PlayWindup(new BasicAttackWindupCommand(f.Frozen, clock.Now));
                while (!windup.Completion.IsCompleted && clock.Now < deadline)
                {
                    yield return new WaitForEndOfFrame();
                    if (f.Frozen.Semantics.MarkerMode == BasicAttackMarkerMode.ExpectedMarker)
                        playback.ReportMarker(new BattleAnimationMarker(playback.Current, "Impact", 0));
                    adapter.Tick();
                }
                Check(windup.Completion.IsCompleted && windup.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    DisplaysFrame(actor.VisualRoot.GetComponent<SpriteRenderer>(), art.Attack), "Real bound windup did not retain its attack art.");
                target.VisualRoot.GetComponent<BattleWorldActorSpriteDriver>().PlayBoundIntent(
                    new AnimationPlaybackIdentity(f.Frozen.Identity, DRPGBattleActionStage.ImpactAndReactions, 0, 3, 0), EAnimationLogicState.Skill_1).Dispose();
                f.Commit(); var impact = adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, clock.Now));
                while (!impact.Completion.IsCompleted && clock.Now < deadline) { yield return new WaitForEndOfFrame(); adapter.Tick(); }
                Check(impact.Completion.IsCompleted && impact.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed, "Real impact did not finish.");
                var targetPose = new BattleProjectionPose(target.VisualRoot); var defeatTint = target.VisualRoot.GetComponent<SpriteRenderer>().color;
                foreach (var view in f.Domain.Roster.ActiveViews)
                    if (!lease.GetPresented(view.RuntimeId).DefeatPending) view.VisualRoot.GetComponent<SpriteRenderer>().color *= .3f;
                group.alpha = .05f; backgroundRenderer.color = Color.black;
                visuals.Camera.transform.position += new Vector3(1, 1, 2);
                Vector3 conflictPosition = actor.transform.position;
                var returning = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                Check(actor.transform.position == conflictPosition, "Real return snapped on issue.");
                int samples = 0; float lastAlpha = group.alpha; float lastTint = actor.VisualRoot.GetComponent<SpriteRenderer>().color.r;
                while (!returning.Completion.IsCompleted && clock.Now < deadline)
                {
                    yield return new WaitForEndOfFrame(); adapter.Tick(); samples++;
                    Check(group.alpha >= lastAlpha && actor.VisualRoot.GetComponent<SpriteRenderer>().color.r >= lastTint,
                        "Return tint/alpha reversed or flashed a second time.");
                    lastAlpha = group.alpha; lastTint = actor.VisualRoot.GetComponent<SpriteRenderer>().color.r;
                    var actorBounds = actor.VisualRoot.GetComponent<SpriteRenderer>().bounds;
                    Check(Mathf.Abs(actorBounds.min.y - actor.transform.position.y) < .0001f && Mathf.Abs(actorBounds.size.y - renderedHeight) < .0001f,
                        "Real rendered sprite feet/height popped during return or the idle swap.");
                    Check(actor.GetInstanceID() == actorIdentity && target.GetInstanceID() == targetIdentity && target.gameObject.activeInHierarchy &&
                        f.Domain.Pool.ReleaseCount == 0 && f.Events == 1 && f.Lease.IsActive,
                        "Visual return released a pinned actor or advanced/repeated rules.");
                    if (lethal) Check(target.VisualRoot.GetComponent<SpriteRenderer>().color == defeatTint &&
                        DisplaysFrame(target.VisualRoot.GetComponent<SpriteRenderer>(), art.Attack) && PoseEquals(target.VisualRoot, targetPose) &&
                        visuals.ActiveDefeatMarkers().Length == 1, "Real lethal return flashed alive art or lost X/dim.");
                }
                Check(samples >= 2 && returning.Completion.IsCompleted && returning.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    group.alpha == .8f && backgroundRenderer.color == Color.white && visuals.Camera.transform.position == cameraHome &&
                    actor.VisualRoot.GetComponent<SpriteRenderer>().sprite == art.Idle && actor.VisualRoot.GetComponent<SpriteRenderer>().color == actorTint &&
                    PoseEquals(actor.VisualRoot, home), "Real-frame full visual/environment return did not reach exact baseline.");
                scope.Dispose();
                Check(PoseEquals(actor.VisualRoot, home) && actor.VisualRoot.GetComponent<SpriteRenderer>().sprite == art.Idle,
                    "Real canonical close snapped or revived a surviving attack pose.");
            }
        }
    }

    internal static DRPGBasicAttackImpactSequenceProbe.Fixture Create(bool enemyFirst, bool lethal = false) =>
        new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, false, lethal: lethal,
            configureRules: DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins);
    internal static void StartImpact(DRPGBasicAttackImpactSequenceProbe.Fixture f,
        DRPGBattleTransactionPresentationAdapter adapter, ActorArt art)
    {
        adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, f.Clock.Now));
        foreach (var view in f.Domain.Roster.ActiveViews)
        {
            var identity = new AnimationPlaybackIdentity(f.Frozen.Identity, DRPGBattleActionStage.Windup, 0, 1, 0);
            view.VisualRoot.GetComponent<BattleWorldActorSpriteDriver>().PlayBoundIntent(identity, EAnimationLogicState.Skill_1).Dispose();
        }
        Advance(f, adapter, .13d); f.Commit();
        adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
        Advance(f, adapter, .46d);
    }
    internal static void Advance(DRPGBasicAttackImpactSequenceProbe.Fixture f, DRPGBattleTransactionPresentationAdapter adapter, double seconds)
    { f.Clock.Now += seconds; f.Clock.RenderedFrame++; adapter.Tick(); }
    private static bool PoseEquals(Transform t, BattleProjectionPose pose) => t.localPosition == pose.LocalPosition &&
        t.localScale == pose.LocalScale && t.localRotation == pose.LocalRotation;
    internal static bool DisplaysFrame(SpriteRenderer renderer, Sprite authored)
    {
        var visible = renderer.sprite;
        if (visible == null || visible.texture != authored.texture || visible.rect != authored.rect || visible.uv.Length != authored.uv.Length) return false;
        var uv = visible.uv; var authoredUv = authored.uv;
        for (int i = 0; i < uv.Length; i++) if (uv[i] != authoredUv[i]) return false;
        return renderer.enabled;
    }
    internal static void Check(bool condition, string message) => DRPGBasicAttackProjectionProbe.Check(condition, message);

    internal sealed class ActorArt : IDisposable
    {
        private readonly Texture2D texture;
        internal readonly Sprite Idle, Attack;
        internal ActorArt(DRPGBasicAttackImpactSequenceProbe.Fixture f)
        {
            texture = new Texture2D(4, 6);
            var pixels = new Color[24]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            Idle = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, 0), 1);
            Attack = Sprite.Create(texture, new Rect(0, 0, 4, 6), new Vector2(.4f, .6f), 1);
            foreach (var participant in f.Domain.Session.Participants)
            {
                DRPGBasicAttackPreludeProbe.Set(participant.Profile, "fullPortrait", Idle);
                DRPGBasicAttackPreludeProbe.Set(participant.Profile, "attackCutInPortrait", Attack);
            }
            foreach (var view in f.Domain.Roster.ActiveViews)
            {
                var renderer = view.VisualRoot.gameObject.AddComponent<SpriteRenderer>();
                renderer.color = new Color(.8f, .7f, .6f, .9f);
                var driver = view.VisualRoot.gameObject.AddComponent<BattleWorldActorSpriteDriver>();
                driver.Configure(renderer, 2); view.ConfigureVisualDriver(driver);
                var accessory = new GameObject("AuthoredAccessory").transform;
                accessory.SetParent(view.VisualRoot, false); accessory.localPosition = new Vector3(.1f, .3f, 0);
            }
            var snapshot = DRPGBattlePresentationSnapshot.Capture(f.Domain.Session);
            f.Domain.Roster.Bind(snapshot.PartyMembers, snapshot.Enemies, true);
        }
        public void Dispose()
        { UnityEngine.Object.DestroyImmediate(Attack); UnityEngine.Object.DestroyImmediate(Idle); UnityEngine.Object.DestroyImmediate(texture); }
    }
}
#endif
