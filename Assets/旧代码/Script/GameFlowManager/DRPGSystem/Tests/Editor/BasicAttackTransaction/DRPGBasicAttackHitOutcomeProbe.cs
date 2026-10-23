#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;

public static class DRPGBasicAttackHitOutcomeProbe
{
    internal static IEnumerator ProductionGuardedAndEvadedAreSingleOutcomeBeats()
    {
        // Cross a real rendered frame before exercising the production adapter;
        // the assertions below cover both sides and all authoritative outcomes.
        yield return null;
        AdapterProjectsOneSideSymmetricOutcomeBeat();
        ResultAudioSharesTheSingleOutcomeTimestampOnBothSides();
        yield return null;
    }

    public static void WorldFlashMaterialsArePreparedBeforeImpactAndReused()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(
            false,
            withReactions: false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(
                    f.Frozen,
                    0d));
                ((DRPGBattleActionProjectionLease)scope).TryGetView(
                    f.Domain.Enemy.RuntimeId,
                    out var target);
                var renderer = target.VisualRoot.GetComponent<SpriteRenderer>();
                if (renderer == null)
                    renderer = target.VisualRoot.gameObject.AddComponent<SpriteRenderer>();

                MethodInfo prepare = typeof(BattleWorldHitFlash).GetMethod(
                    "Prepare",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Check(prepare != null,
                    "World hit-flash has no pre-impact preparation seam.");
                prepare.Invoke(null, new object[] { target });

                using (var first = new BattleWorldHitFlash(target, 0d))
                {
                    Material firstPulse = renderer.sharedMaterial;
                    first.Dispose();
                    using (var second = new BattleWorldHitFlash(target, 1d))
                    {
                        Material secondPulse = renderer.sharedMaterial;
                        Check(firstPulse != null &&
                            ReferenceEquals(firstPulse, secondPulse),
                            "A later hit allocated a fresh flash material instead of reusing the prepared one.");
                    }
                }
            }
        }
    }

    public static void WorldFlashIsSingleMonotonicPulseAndNeverRunsForEvade()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (var outcome in new[] { DRPGHitOutcome.Hit, DRPGHitOutcome.Guarded, DRPGHitOutcome.Evaded })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemy, false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0));
                f.Clock.Now = .13; f.Clock.RenderedFrame = 1; adapter.Tick(); f.Commit(outcome);
                ((DRPGBattleActionProjectionLease)scope).TryGetView(f.Ledger.PrimaryImpact.Hit.TargetRuntimeId, out var target);
                var renderer = target.GetComponentInChildren<SpriteRenderer>();
                if (renderer == null) renderer = target.VisualRoot.gameObject.AddComponent<SpriteRenderer>();
                var original = renderer.sharedMaterial;
                var color = renderer.color = new Color(.6f, .7f, .8f, .35f);
                var command = new ImpactPresentationCommand(f.Ledger, f.Clock.Now);
                adapter.PresentImpact(command);
                if (outcome == DRPGHitOutcome.Evaded)
                { Check(renderer.sharedMaterial == original && renderer.color == color, "Evade emitted hit flash."); continue; }
                var flash = renderer.sharedMaterial;
                Check(flash != original && flash.HasProperty("_AnkaluteHitFlash") && flash.GetFloat("_AnkaluteHitFlash") > .9f &&
                    visuals.ActiveNumbers().Length == 1, "Hit flash did not start with the unique visible result.");
                float previous = 1;
                for (int i = 1; i <= 5; i++)
                {
                    f.Clock.Now = .13 + i * .02; f.Clock.RenderedFrame++; adapter.Tick();
                    float blend = renderer.sharedMaterial == original ? 0 : renderer.sharedMaterial.GetFloat("_AnkaluteHitFlash");
                    Check(blend <= previous && blend >= 0 && renderer.color == color, "Flash repeated, brightened again, or changed semantic tint/alpha.");
                    previous = blend;
                }
                Check(renderer.sharedMaterial == original, "Flash did not restore the original shared material.");
                adapter.PresentImpact(command);
                Check(renderer.sharedMaterial == original && f.Events == 1, "Duplicate result flashed twice or replayed damage.");
            }
        }
    }

    public static void FlashCancellationRestoresMaterialButNeverTouchesShadowOrDeathTint()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, withReactions: false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0));
                f.Clock.Now = .13; f.Clock.RenderedFrame = 1; adapter.Tick(); f.Commit();
                ((DRPGBattleActionProjectionLease)scope).TryGetView(f.Ledger.PrimaryImpact.Hit.TargetRuntimeId, out var target);
                var renderer = target.VisualRoot.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = target.VisualRoot.gameObject.AddComponent<SpriteRenderer>();
                var shadow = target.ShadowAnchor.gameObject.AddComponent<SpriteRenderer>();
                var original = renderer.sharedMaterial; var shadowMaterial = shadow.sharedMaterial;
                var receipt = adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                var pulse = renderer.sharedMaterial;
                Check(pulse != original && shadow.sharedMaterial == shadowMaterial, "Hit flash also changed the separate shadow layer.");
                var deathTint = renderer.color = new Color(.24f, .28f, .32f, .35f);
                adapter.CancelReceipt(receipt, "Probe cancellation");
                Check(renderer.sharedMaterial == original && renderer.color == deathTint && pulse != null,
                    "Cancelling the result left its prepared material assigned or erased semantic death tint.");
                adapter.Tick();
                Check(renderer.sharedMaterial == original && f.Events == 1, "Cancelled flash was restarted by Tick.");
            }
        }
    }

    public static void ResultAudioSharesTheSingleOutcomeTimestampOnBothSides()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (var outcome in new[] { DRPGHitOutcome.Hit, DRPGHitOutcome.Guarded, DRPGHitOutcome.Evaded })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemy, false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var audio = new AudioBoundary(f.Clock);
            var adapter = new DRPGBattleTransactionPresentationAdapter(f.Clock, visuals.Announcement,
                new BattleAnimationPlaybackRouter(f.Clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback, battleCamera: visuals.Camera,
                audioCueSink: audio);
            using (adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0));
                f.Clock.Now = .13; f.Clock.RenderedFrame = 1; adapter.Tick(); f.Commit(outcome);
                Check(audio.Requests.Count == 0, "Result audio played before the unique hit point.");
                var command = new ImpactPresentationCommand(f.Ledger, f.Clock.Now);
                var receipt = adapter.PresentImpact(command);
                int count = outcome == DRPGHitOutcome.Evaded ? 1 : 2;
                Check(audio.Requests.Count == count, "Standard outcome omitted or duplicated its audio: " + enemy + "/" + outcome);
                var expected = outcome == DRPGHitOutcome.Evaded ? BattleAudioCue.Miss :
                    outcome == DRPGHitOutcome.Guarded ? BattleAudioCue.Guard : BattleAudioCue.Hit;
                Check(audio.Requests[count - 1].Cue == expected && (count == 1 || audio.Requests[0].Cue == BattleAudioCue.Attack),
                    "Evade/guard was rendered as a generic hit before its actual result.");
                foreach (var request in audio.Requests)
                    Check(request.ActorRuntimeId == f.Ledger.PrimaryImpact.Hit.ActorRuntimeId && request.TargetRuntimeId == f.Ledger.PrimaryImpact.Hit.TargetRuntimeId &&
                        request.SkillId == f.Ledger.PrimaryImpact.Hit.SkillId,
                        "Result sound addressed another action's actor or target.");
                Check(audio.Time == .13 && audio.Frame == 1 && visuals.ActiveNumbers().Length == 1,
                    "Audio and the visible result label were split across timestamps/frames.");
                adapter.PresentImpact(command); f.Clock.Now = .60; f.Clock.RenderedFrame++; adapter.Tick();
                Check(audio.Requests.Count == count && receipt.Completion.IsCompleted && f.Events == 1,
                    "Duplicate/late feedback replayed audio or rule mutation.");
            }
        }
    }

    public static void AudioPlaybackFailureDegradesOnlyItsResultWithoutReplayingDamage()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, withReactions: false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var audio = new AudioBoundary(f.Clock) { Throw = true };
            var adapter = new DRPGBattleTransactionPresentationAdapter(f.Clock, visuals.Announcement,
                new BattleAnimationPlaybackRouter(f.Clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback, battleCamera: visuals.Camera, audioCueSink: audio);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0));
                f.Clock.Now = .13; f.Clock.RenderedFrame = 1; adapter.Tick(); f.Commit();
                PresentationReceipt receipt;
                try { receipt = adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now)); }
                catch (Exception failure) { throw new InvalidOperationException("Cue-local audio error escaped the outcome owner.", failure); }
                Check(visuals.ActiveNumbers().Length == 1 && !receipt.Completion.IsCompleted && f.Events == 1,
                    "Audio failure hid the result, skipped the readable hold or replayed damage.");
                f.Clock.Now = .60; f.Clock.RenderedFrame++; adapter.Tick();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded &&
                    f.Events == 1 && f.Lease.IsActive && ((DRPGBattleActionProjectionLease)scope).IsActive,
                    "Audio failure became a rule fault, normal success, or abandoned the conflict projection.");
            }
        }
    }

    public static void AudioFailureDoesNotContaminateFollowingReactionOrItsPreparation()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var audio = new AudioBoundary(f.Clock) { Throw = true };
            var adapter = new DRPGBattleTransactionPresentationAdapter(f.Clock, visuals.Announcement,
                new BattleAnimationPlaybackRouter(f.Clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback, battleCamera: visuals.Camera, audioCueSink: audio);
            using (adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0));
                f.Clock.Now = .13; f.Clock.RenderedFrame = 1; adapter.Tick(); f.Commit();
                var primary = adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                f.Clock.Now += .46; f.Clock.RenderedFrame++; adapter.Tick();
                Check(primary.Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded, "Injected audio failure was not reported.");
                audio.Throw = false;
                int before = audio.Requests.Count;
                var command = new ReactionPresentationCommand(f.Ledger, 1, f.Clock.Now);
                var reaction = adapter.PresentReaction(command);
                Check(audio.Requests.Count == before, "Reaction preparation played the result audio early.");
                f.Clock.Now += .13; f.Clock.RenderedFrame++; adapter.Tick();
                var hit = f.Ledger.Reactions[0].Batch.Hits[0];
                Check(audio.Requests.Count > before, "Reaction omitted its own result audio.");
                for (int i = before; i < audio.Requests.Count; i++)
                    Check(audio.Requests[i].ActorRuntimeId == hit.ActorRuntimeId && audio.Requests[i].TargetRuntimeId == hit.TargetRuntimeId &&
                        audio.Requests[i].SkillId == hit.SkillId && audio.Requests[i].SemanticCueId != audio.Requests[0].SemanticCueId,
                        "Reaction reused the primary sound identity or participants.");
                int after = audio.Requests.Count;
                adapter.PresentReaction(command);
                f.Clock.Now += .46; f.Clock.RenderedFrame++; adapter.Tick();
                Check(reaction.Completion.IsCompleted && reaction.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    audio.Requests.Count == after && f.Events == 1 && f.Lease.IsActive,
                    "Primary degradation leaked into the next result or reaction replayed damage/audio.");
            }
        }
    }

    // External playback boundary only; HP, projection, number and motion above
    // are the real production components, not replaced by this recorder.
    private sealed class AudioBoundary : IBattleAudioCueSink
    {
        private readonly IDRPGBattlePresentationClock clock;
        internal readonly System.Collections.Generic.List<BattleAudioCueRequest> Requests = new System.Collections.Generic.List<BattleAudioCueRequest>();
        internal double Time; internal long Frame;
        internal bool Throw;
        internal AudioBoundary(IDRPGBattlePresentationClock clock) { this.clock = clock; }
        public void Begin(DRPGBattleAudioPaletteSO palette) { }
        public void End() { }
        public void Play(BattleAudioCueRequest request) { Requests.Add(request); Time = clock.Now; Frame = clock.RenderedFrame; if (Throw) throw new InvalidOperationException("Injected external audio failure."); }
    }

    public static void RealReactionQueueUsesIntermediateHpCursors()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                using (var queue = new DRPGBasicAttackImpactSequence(f.Ledger, adapter, f.Clock, f.Lease))
                {
                    Check(projection.GetPresented(f.Domain.Enemy.RuntimeId).Posture ==
                            f.Ledger.PrimaryImpact.Hit.PostureAfter &&
                        projection.GetPresented(f.Domain.Enemy.RuntimeId).Posture !=
                            f.Ledger.Reactions[f.Ledger.Reactions.Count - 1].Batch.Hits[0].PostureAfter,
                        "Primary feedback projected a later reaction's final layered-health cursor early.");
                    for (int beat = 0; beat < 3; beat++)
                    {
                        Check(visuals.ActiveNumbers().Length == 1, "Reaction outcomes overlapped or vanished.");
                        if (beat > 0)
                        { f.Clock.Now += .13d; f.Clock.RenderedFrame++; queue.Tick(); }
                        f.Clock.Now += .46d; f.Clock.RenderedFrame++; queue.Tick();
                        Check(queue.Fault.Length == 0, queue.Fault);
                    }
                    Check(queue.ReadyForReturn && projection.GetPresented(f.Domain.Player.RuntimeId).Hp == f.PlayerHp &&
                        projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp == f.EnemyHp && f.Events == 1 && f.Lease.IsActive,
                        "Real reaction presentation omitted an intermediate HP cursor or released rule ownership.");
                }
            }
        }
    }

    public static void ClosingProjectionCancelsItsOutcomeLabel()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, withReactions: false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                var receipt = adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                scope.Dispose();
                Check(visuals.ActiveNumbers().Length == 0 && receipt.Completion.IsCompleted,
                    "A closed projection left its outcome label/receipt alive until another Tick.");
            }
        }
    }

    public static void NonHitHealingReactionHasItsOwnVisibleHpBeat()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, healingReaction: true))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                using (var queue = new DRPGBasicAttackImpactSequence(f.Ledger, adapter, f.Clock, f.Lease))
                {
                    f.Clock.Now += .46d; f.Clock.RenderedFrame++; queue.Tick();
                    Check(queue.Fault.Length == 0, "Non-hit healing reaction was rejected: " + queue.Fault);
                    f.Clock.Now += .13d; f.Clock.RenderedFrame++; queue.Tick();
                    var effect = f.Ledger.Reactions[0].Batch.Effects[0];
                    Check(effect.VitalChange != null && projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp == effect.VitalChange.After &&
                        visuals.ActiveNumbers().Length == 1 && visuals.ActiveNumbers()[0].GetComponent<TMP_Text>().text.Contains("治疗"),
                        "Healing feedback omitted its immutable HP cursor or its separate readable label.");
                    var position = f.Domain.Target.transform.position;
                    f.Clock.Now += .05d; f.Clock.RenderedFrame++; queue.Tick();
                    Check(f.Domain.Target.transform.position == position, "A non-hit healing effect emitted damage recoil.");
                    f.Clock.Now += .41d; f.Clock.RenderedFrame++; queue.Tick();
                    f.Clock.Now += .13d; f.Clock.RenderedFrame++; queue.Tick();
                    f.Clock.Now += .46d; f.Clock.RenderedFrame++; queue.Tick();
                    Check(queue.ReadyForReturn && f.Events == 1 && projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp == f.EnemyHp,
                        "Healing presentation re-ran rules or blocked the next ledger reaction.");
                }
            }
        }
    }

    public static void CompletedLedgerRejectsExtraReactionWithoutThrowing()
    {
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, withReactions: false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            using (adapter.OpenProjection(f.Frozen))
            {
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                f.Clock.Now = .59d; f.Clock.RenderedFrame++; adapter.Tick();
                var extra = adapter.PresentReaction(new ReactionPresentationCommand(f.Ledger, 1, f.Clock.Now));
                Check(extra.Completion.IsCompleted && extra.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted && f.Events == 1,
                    "A reaction beyond the immutable ledger was accepted.");
            }
        }
    }

    public static void AdapterProjectsOneSideSymmetricOutcomeBeat()
    {
        foreach (bool enemyFirst in new[] { false, true })
        foreach (DRPGHitOutcome outcome in new[] { DRPGHitOutcome.Hit, DRPGHitOutcome.Guarded, DRPGHitOutcome.Evaded })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, false))
        {
            var visuals = new Visuals(f.Domain.Root, f.Clock);
            var adapter = new DRPGBattleTransactionPresentationAdapter(f.Clock,
                visuals.Announcement, new BattleAnimationPlaybackRouter(f.Clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback, battleCamera: visuals.Camera);
            using (var scope = adapter.OpenProjection(f.Frozen))
            {
                var projection = (DRPGBattleActionProjectionLease)scope;
                adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, f.Clock.Now));
                f.Clock.Now = .13d; f.Clock.RenderedFrame = 1; adapter.Tick();
                f.Commit(outcome);
                var hit = f.Ledger.PrimaryImpact.Hit;
                projection.TryGetView(hit.TargetRuntimeId, out var target);
                Vector3 position = target.transform.position, scale = target.transform.localScale;
                var command = new ImpactPresentationCommand(f.Ledger, f.Clock.Now);
                var receipt = adapter.PresentImpact(command);
                Check(!receipt.Completion.IsCompleted && projection.GetPresented(hit.TargetRuntimeId).Hp == hit.HpAfter &&
                    projection.GetPresented(hit.TargetRuntimeId).Posture == hit.PostureAfter,
                    "Primary hit did not update its leased layered-health cursor and open one visible outcome receipt: " + enemyFirst + "/" + outcome);
                var numbers = visuals.ActiveNumbers();
                Check(numbers.Length == 1 && numbers[0].GetComponent<CanvasGroup>().alpha == 1f,
                    "Outcome number was missing, duplicated, or appeared after the hit timestamp.");
                string expected = hit.Outcome == DRPGHitOutcome.Evaded ? "闪避" :
                    (hit.Outcome == DRPGHitOutcome.Guarded ? "防御 " : "") + "-" +
                    Mathf.CeilToInt(hit.DamageBreakdown != null
                        ? hit.DamageBreakdown.RequestedDamage
                        : hit.AppliedDamage);
                Check(numbers[0].GetComponent<TMP_Text>().text == expected, "Hit/Guard/Evade was split into different outcome labels.");
                Check(ReferenceEquals(receipt, adapter.PresentImpact(command)) && visuals.ActiveNumbers().Length == 1,
                    "An identical beat replayed feedback or returned a new receipt.");
                f.Clock.Now = .18d; f.Clock.RenderedFrame++; adapter.Tick();
                float delta = target.transform.position.x - position.x;
                Check(enemyFirst ? delta < 0f : delta > 0f,
                    "The selected hit/dodge motion did not begin with its single outcome label.");
                Check(target.transform.localScale == scale && target.transform.position.y == position.y,
                    "Outcome feedback changed actor scale or vertical position.");
                f.Clock.Now = .57d; f.Clock.RenderedFrame++; adapter.Tick();
                Check(!receipt.Completion.IsCompleted, "Outcome skipped its 0.45s readable minimum.");
                f.Clock.Now = .59d; f.Clock.RenderedFrame++; adapter.Tick();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                    target.transform.position == position && f.Events == 1,
                    "Outcome did not finish at conflict, or presentation changed the rule commit count.");
            }
        }
    }

    internal sealed class Visuals
    {
        internal readonly BattleImpactFeedbackPresenter Feedback;
        internal readonly BattleActionAnnouncementPresenter Announcement;
        internal readonly Camera Camera;
        internal readonly TMP_Text DefeatTemplate;
        private readonly Transform layer;
        internal Visuals(GameObject parent, IDRPGBattlePresentationClock clock)
        {
            var canvas = new GameObject("HitProbeCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(parent.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Numbers", typeof(RectTransform)); go.transform.SetParent(canvas.transform, false);
            layer = go.transform;
            Feedback = go.AddComponent<BattleImpactFeedbackPresenter>();
            var template = new GameObject("OutcomeTemplate", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup), typeof(BattleDamageNumberView));
            template.transform.SetParent(layer, false);
            var number = template.GetComponent<BattleDamageNumberView>();
            Set(number, "rectTransform", template.GetComponent<RectTransform>());
            Set(number, "valueText", template.GetComponent<TMP_Text>());
            Set(number, "canvasGroup", template.GetComponent<CanvasGroup>());
            Set(Feedback, "feedbackLayer", go.GetComponent<RectTransform>());
            Set(Feedback, "damageNumberTemplate", number); template.SetActive(false);
            var defeat = new GameObject("DefeatMarkerTemplate", typeof(RectTransform), typeof(TextMeshProUGUI));
            defeat.transform.SetParent(layer, false);
            DefeatTemplate = defeat.GetComponent<TMP_Text>(); DefeatTemplate.text = "X";
            DefeatTemplate.fontSize = 40; DefeatTemplate.alignment = TextAlignmentOptions.Center;
            DefeatTemplate.rectTransform.sizeDelta = new Vector2(64, 64); DefeatTemplate.raycastTarget = false;
            Set(Feedback, "defeatMarkerTemplate", DefeatTemplate); defeat.SetActive(false);
            var announcement = new GameObject("Announcement", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            announcement.transform.SetParent(canvas.transform, false);
            Announcement = new BattleActionAnnouncementPresenter(announcement.GetComponent<TMP_Text>(), announcement.GetComponent<CanvasGroup>(), clock);
            var camera = new GameObject("HitProbeCamera", typeof(Camera)); camera.transform.SetParent(parent.transform, false);
            Camera = camera.GetComponent<Camera>(); Camera.orthographic = true; Camera.orthographicSize = 6;
            Camera.transform.position = new Vector3(0, 0, -10);
        }
        internal BattleDamageNumberView[] ActiveNumbers() => layer.GetComponentsInChildren<BattleDamageNumberView>();
        internal TMP_Text[] ActiveDefeatMarkers()
        {
            var markers = new System.Collections.Generic.List<TMP_Text>();
            foreach (var text in layer.GetComponentsInChildren<TMP_Text>()) if (text.text == "X") markers.Add(text);
            return markers.ToArray();
        }
        internal DRPGBattleTransactionPresentationAdapter CreateAdapter(DRPGBasicAttackImpactSequenceProbe.Fixture f,
            BattleDefeatedActorRoster defeatedRoster = null) =>
            new DRPGBattleTransactionPresentationAdapter(f.Clock, Announcement, new BattleAnimationPlaybackRouter(f.Clock, (_, __) => null),
                _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                projectionArbiter: f.Domain.Surface.ProjectionArbiter, impactFeedback: Feedback, battleCamera: Camera, defeatedRoster: defeatedRoster);
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
