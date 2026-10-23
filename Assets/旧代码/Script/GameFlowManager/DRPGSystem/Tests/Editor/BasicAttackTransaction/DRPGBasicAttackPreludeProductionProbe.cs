#if UNITY_EDITOR
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEditor;

internal static class DRPGBasicAttackPreludeProductionProbe
{
    internal static IEnumerator ProductionAnnouncementIsolation()
    {
        foreach (bool enemy in new[] { false, true })
        using (var test = new RealPreludeFixture(enemy))
        {
            var start = test.Clock.Now; var frame = test.Clock.RenderedFrame;
            var receipt = test.Adapter.Announce(new AnnouncementCommand(test.Domain.Frozen, start, "ignored"));
            Check(test.Group.alpha == 1 && test.Label.text.Contains(enemy ? "敌方" : "己方") && test.Label.text.Contains("普通攻击") && !receipt.Completion.IsCompleted,
                "Production adapter did not show the readable action announcement.");
            var position = test.Actor.transform.position; var scale = test.Actor.transform.localScale; var rotation = test.Actor.transform.rotation;
            var cameraPosition = test.Camera.transform.position; float fov = test.Camera.fieldOfView;
            int samples = 0;
            while (!receipt.Completion.IsCompleted && test.Clock.Now - start < 3)
            {
                yield return new WaitForEndOfFrame();
                Check(test.Actor.transform.position == position && test.Actor.transform.localScale == scale && test.Actor.transform.rotation == rotation &&
                    test.Camera.transform.position == cameraPosition && test.Camera.fieldOfView == fov && test.CompositionCalls == 0 && test.PlaybackCalls == 0 &&
                    test.Domain.State.Player.CurrentHp == 100 && test.Domain.State.Enemy.CurrentHp == 120,
                    "Announcement mutated actors, camera, HP or started composition/playback.");
                if (test.Clock.Now - start < .75) Check(test.Group.alpha == 1 && !receipt.Completion.IsCompleted, "Production announcement closed too early.");
                Check(test.Label.textInfo.characterCount > 0 && test.Label.rectTransform.rect.width > 0 && test.Label.canvasRenderer.GetAlpha() > 0,
                    "Announcement label did not reach the real Canvas renderer.");
                samples++; test.Adapter.Tick();
            }
            Check(samples >= 2 && test.Clock.RenderedFrame > frame && test.Clock.Now - start >= .75 && receipt.Completion.IsCompleted &&
                receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && test.Adapter.RecoveryCount == 0,
                "Production announcement lacks real time/render fence or degraded: enemy=" + enemy +
                "; samples=" + samples + "; elapsed=" + (test.Clock.Now - start).ToString("R") +
                "; rendered=" + (test.Clock.RenderedFrame - frame) + "; completed=" + receipt.Completion.IsCompleted +
                "; terminal=" + (receipt.Completion.IsCompleted ? receipt.Completion.Result.Kind + ": " + receipt.Completion.Result.Detail : "pending") +
                "; recoveries=" + test.Adapter.RecoveryCount);
        }
    }

    internal static IEnumerator ProductionWindupMarkerIdentity()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (bool authoredFallback in new[] { false, true })
        using (var test = new RealPreludeFixture(enemy, authoredFallback ? BasicAttackMarkerMode.AuthoredFallback : BasicAttackMarkerMode.ExpectedMarker))
        {
            double start = test.Clock.Now;
            var receipt = test.Adapter.PlayWindup(new BasicAttackWindupCommand(test.Domain.Frozen, start));
            var identity = test.Playback.Current;
            Check(identity.IsValid && test.PlaybackCalls == 1 && !receipt.Completion.IsCompleted, "Production windup did not start a bound frame playback.");
            var pos = test.Actor.transform.position; var scale = test.Actor.transform.localScale;
            while (!receipt.Completion.IsCompleted && test.Clock.Now - start < 2)
            {
                yield return new WaitForEndOfFrame();
                test.TickSpine();
                Check(test.Actor.transform.position == pos && test.Actor.transform.localScale == scale && test.Renderer.enabled,
                    "Bound static windup lunged, scaled or hid its actor.");
                if (test.Clock.Now - start < .30) Check(!receipt.Completion.IsCompleted, "Early production marker opened the hit gate.");
                test.Adapter.Tick();
            }
            Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && test.Clock.Now - start >= .30,
                "Production marker failed minimum windup.");
            Check(DRPGBasicAttackVisualRestoreProbe.DisplaysFrame(test.Renderer, test.AttackSprite) &&
                receipt.Completion.Result.Detail == (authoredFallback ? "AuthoredFallback" : "AnimationMarker"),
                "Production did not retain the distinct attack frame or distinguish authored fallback.");
            test.Playback.ReportMarker(new BattleAnimationMarker(identity, "Impact", 0));
            Check(test.Playback.LastMarkerDisposition.Contains("late") && test.Domain.State.Enemy.CurrentHp == 120 && test.Domain.Lease.IsActive,
                "Production late marker was accepted or advanced rules/root ownership.");
        }
    }

    internal static IEnumerator ProductionMarkerMissingRecovers()
    {
        using (var test = new RealPreludeFixture(false, BasicAttackMarkerMode.ExpectedMarker))
        {
            var slot = new DRPGBattleTransactionPresentationPortSlot(test.Domain.Frozen.Identity.BattleGeneration);
            slot.TryBind(test.Domain.Frozen.Identity.BattleGeneration, test.Adapter);
            using (var module = new DRPGStandardBasicAttackTransactionModule(slot))
            {
            double start = test.Clock.Now;
            var root = module.TryStart(test.Domain.Frozen, test.Domain.Frozen.Identity, test.Domain.Lease, "production-probe-locked");
            Check(root.IsAccepted && !root.Receipt.Completion.IsCompleted, "Root did not start the real production prelude.");
            while (module.Prelude != null && !module.Prelude.ReadyForImpact && test.Clock.Now - start < 6)
            { yield return new WaitForEndOfFrame(); module.Tick(); }
            var receipt = module.Prelude?.ExpectedReceipt;
            int recoveries = 0, completedActions = 0;
            foreach (var entry in test.Domain.State.Arbiter.Trace.SnapshotForAction(test.Domain.Frozen.Identity))
            {
                if (entry.EventKind == ActionTraceEventKind.WatchdogExpired) recoveries++;
                if (entry.EventKind == ActionTraceEventKind.Finalized) completedActions++;
            }
            Check(module.Prelude != null && module.Prelude.ReadyForImpact && module.Prelude.Degraded &&
                receipt != null && receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded && recoveries == 1 &&
                completedActions == 0 && !root.Receipt.Completion.IsCompleted && test.Clock.Now - start >= 3.17 &&
                test.Renderer.enabled && test.Domain.Lease.IsActive && test.Domain.State.Enemy.CurrentHp == 120,
                "Expected missing marker did not clean and degrade once without rules or restore.");
            var identity = test.Playback.Current;
            test.Playback.ReportMarker(new BattleAnimationMarker(identity, "Impact", 0)); test.Adapter.Tick();
            Check(test.Playback.LastMarkerDisposition.Contains("late") && !root.Receipt.Completion.IsCompleted, "Late-after-recovery revived playback.");
            }
        }
    }

    private static void Check(bool condition, string error) => DRPGBasicAttackPreludeProbe.Check(condition, error);
    private sealed class RealPreludeFixture : IDisposable
    {
        internal readonly DRPGBasicAttackPreludeProbe.PreludeFixture Domain;
        private readonly GameObject root;
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        internal readonly Sprite AttackSprite;
        private Spine.AnimationState spineState;
        private Spine.Skeleton spineSkeleton;
        private double spineTime;
        internal readonly UnityRenderFrameFence Clock;
        internal readonly CanvasGroup Group;
        internal readonly TMP_Text Label;
        internal readonly GameObject Actor;
        internal readonly Camera Camera;
        internal readonly SpriteRenderer Renderer;
        internal readonly BattleAnimationPlaybackRouter Playback;
        internal readonly DRPGBattleTransactionPresentationAdapter Adapter;
        internal int CompositionCalls, PlaybackCalls;
        internal RealPreludeFixture(bool enemy = false, BasicAttackMarkerMode markerMode = BasicAttackMarkerMode.AuthoredFallback)
        {
            Domain = new DRPGBasicAttackPreludeProbe.PreludeFixture(enemy, markerMode);
            root = new GameObject("BasicAttackProductionPrelude", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.hideFlags = HideFlags.HideAndDontSave;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440);
            Group = root.GetComponent<CanvasGroup>(); Group.alpha = 0;
            var textObject = new GameObject("ActionName", typeof(RectTransform)); textObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform)textObject.transform; rect.anchorMin = new Vector2(.25f, .8f); rect.anchorMax = new Vector2(.75f, .9f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            Label = textObject.AddComponent<TextMeshProUGUI>();
            Label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SiYuanHeiTi-ExtraLight/SourceHanSansSC-ExtraLight-22 SDF.asset");
            Check(Label.font != null, "Verified project CJK font missing."); Label.fontSize = 32; Label.color = Color.white; Label.alignment = TextAlignmentOptions.Center;
            Clock = root.AddComponent<UnityRenderFrameFence>();
            Actor = new GameObject("BoundActorMotionRoot"); Actor.hideFlags = HideFlags.HideAndDontSave;
            var visual = new GameObject("VisualRoot", typeof(SpriteRenderer)); visual.transform.SetParent(Actor.transform, false);
            Renderer = visual.GetComponent<SpriteRenderer>(); texture = new Texture2D(4, 6);
            var pixels = new Color[24]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white; texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, 0));
            AttackSprite = Sprite.Create(texture, new Rect(0, 0, 4, 6), new Vector2(.4f, .6f));
            var participant = enemy ? Domain.State.Enemy : Domain.State.Player;
            DRPGBasicAttackPreludeProbe.Set(participant.Profile, "fullPortrait", sprite);
            DRPGBasicAttackPreludeProbe.Set(participant.Profile, "attackCutInPortrait", AttackSprite);
            var driver = visual.AddComponent<BattleWorldActorSpriteDriver>(); driver.Configure(Renderer, 2); driver.Bind(new DRPGBattleParticipantViewModel(participant, 0, true));
            var cameraObject = new GameObject("ObservedCamera"); cameraObject.transform.SetParent(root.transform, false); Camera = cameraObject.AddComponent<Camera>(); Camera.enabled = false;
            var source = visual.AddComponent<BattleSpineEventSourceAdapter>();
            Playback = new BattleAnimationPlaybackRouter(Clock, (identity, command) =>
            {
                PlaybackCalls++;
                var pose = driver.PlayBoundIntent(identity, EAnimationLogicState.Skill_1);
                if (command.Snapshot.Semantics.MarkerMode == BasicAttackMarkerMode.AuthoredFallback) return pose;
                var data = new Spine.SkeletonData(); data.Bones.Add(new Spine.BoneData(0, "root", null));
                var timeline = new Spine.EventTimeline(1);
                timeline.SetFrame(0, new Spine.Event(.10f, new Spine.EventData("Impact")) { Int = 0 });
                var animation = new Spine.Animation("production-basic-probe") { Duration = 1f };
                animation.SetTimelines(new Spine.ExposedList<Spine.Timeline> { timeline }, new Spine.ExposedList<int>());
                data.Animations.Add(animation);
                spineSkeleton = new Spine.Skeleton(data);
                spineState = new Spine.AnimationState(new Spine.AnimationStateData(data));
                var entry = spineState.SetAnimation(0, animation, false); spineTime = Clock.Now;
                var binding = source.BindPlayback(entry, identity, marker => Playback.ReportMarker(marker));
                return new DRPGBasicAttackPreludeProbe.CallbackDisposable(() => { binding.Dispose(); pose.Dispose(); });
            });
            Adapter = new DRPGBattleTransactionPresentationAdapter(Clock, new BattleActionAnnouncementPresenter(Label, Group, Clock), Playback,
                command => { CompositionCalls++; var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; });
        }
        internal void TickSpine()
        {
            if (spineState == null) return;
            float delta = (float)(Clock.Now - spineTime); spineTime = Clock.Now;
            spineState.Update(delta); spineState.Apply(spineSkeleton);
        }
        public void Dispose() { spineState?.ClearTracks(); UnityEngine.Object.DestroyImmediate(Actor); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(AttackSprite); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); Domain.Dispose(); }
    }
}
#endif
