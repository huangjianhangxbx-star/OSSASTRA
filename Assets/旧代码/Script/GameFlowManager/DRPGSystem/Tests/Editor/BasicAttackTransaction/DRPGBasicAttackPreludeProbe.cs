#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

internal static class DRPGBasicAttackPreludeProbe
{
    internal static void AnnouncementIsolation()
    {
        foreach (bool enemy in new[] { false, true })
        using (var test = new PreludeFixture(enemy))
        using (var prelude = test.Start())
        {
            Check(prelude.Stage == DRPGBattleActionStage.Announcement && test.Port.History.Count == 1,
                "Root did not issue isolated announcement after lock.");
            test.Port.History[0].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "early"));
            test.Clock.Now = .749; test.Clock.RenderedFrame++;
            prelude.Tick();
            Check(test.Port.History.Count == 1 && !prelude.ReadyForImpact, "Early announcement receipt skipped minimum hold.");
            test.Clock.Now = .75; prelude.Tick();
            Check(test.Port.History.Count == 2 && prelude.Stage == DRPGBattleActionStage.ConflictComposition,
                "Valid announcement did not release the conflict command.");
            Check(test.State.Player.CurrentHp == 100f && test.State.Enemy.CurrentHp == 120f && test.Lease.IsActive,
                "Prelude committed HP or released root ownership.");
        }
    }

    internal static void ConflictCompositionProtocol()
    {
        using (var test = new PreludeFixture())
        using (var prelude = test.Start())
        {
            Check(test.Port.History.Count == 1, "Announcement not issued.");
            test.Port.History[0].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "ok"));
            test.Clock.Now = 2; prelude.Tick();
            Check(test.Port.History.Count == 1, "Advancing time invented a rendered frame.");
            test.Clock.RenderedFrame++; prelude.Tick();
            Check(test.Port.History.Count == 2 && test.Port.Conflict.IssuedAt == 2 &&
                test.Port.Conflict.Snapshot.ActorConflictPosition.X == -.5f && test.Port.Conflict.Snapshot.TargetConflictPosition.X == .5f,
                "Composition lost simultaneous frozen conflict coordinates or issue time.");
            test.Port.History[1].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "ok"));
            test.Clock.Now = 2.119; test.Clock.RenderedFrame++; prelude.Tick();
            Check(test.Port.History.Count == 2, "Conflict minimum hold was skipped.");
            test.Clock.Now = 2.121; prelude.Tick();
            Check(test.Port.History.Count == 3 && prelude.Stage == DRPGBattleActionStage.Windup, "Conflict receipt did not release windup.");
        }
    }

    internal static void AnnouncementUsesRealLabelAndRenderFence()
    {
        foreach (bool enemy in new[] { false, true })
        using (var test = new PreludeFixture(enemy))
        {
            var root = new GameObject("AnnouncementProbe", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var labelObject = new GameObject("ActionName", typeof(RectTransform)); labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>(); var group = root.GetComponent<CanvasGroup>(); group.alpha = 0f;
            try
            {
                var presenter = new BattleActionAnnouncementPresenter(label, group, test.Clock);
                var receipt = presenter.Present(new AnnouncementCommand(test.Frozen, 0, "莱莎"));
                Check(group.alpha == 1 && label.text.Contains("莱莎") &&
                    label.text.Contains(test.Frozen.Semantics.ActionName) &&
                    !receipt.Completion.IsCompleted,
                    "Real announcement label was not shown independently.");
                test.Clock.Now = .749; test.Clock.RenderedFrame++; presenter.Tick();
                Check(!receipt.Completion.IsCompleted && group.alpha == 1, "Real label closed before .75s.");
                test.Clock.Now = .994; presenter.Tick();
                Check(!receipt.Completion.IsCompleted && group.alpha == 1,
                    "Elapsed time closed the real label after only one rendered frame.");
                test.Clock.RenderedFrame++; presenter.Tick();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && group.alpha == 0,
                    "Real label did not close at the readable terminal.");
                // More rendered frames cannot replace the independent wall-clock floor.
                var startedAt = test.Clock.Now;
                receipt = presenter.Present(new AnnouncementCommand(test.Frozen, startedAt, "莱莎"));
                test.Clock.RenderedFrame += 2; test.Clock.Now = startedAt + .749; presenter.Tick();
                Check(!receipt.Completion.IsCompleted && group.alpha == 1, "Two rendered frames skipped the .75s hold.");
                test.Clock.Now = startedAt + .751; presenter.Tick();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && group.alpha == 0,
                    "Real label did not close after both independent gates.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }

    internal static void Check(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }

    internal static void RealSpineEventKeepsPlaybackIdentity()
    {
        var host = new GameObject("RealSpineBindingProbe");
        try
        {
            var source = host.AddComponent<BattleSpineEventSourceAdapter>();
            var data = new Spine.SkeletonData();
            data.Bones.Add(new Spine.BoneData(0, "root", null));
            var timeline = new Spine.EventTimeline(1);
            timeline.SetFrame(0, new Spine.Event(.10f, new Spine.EventData("Impact")) { Int = 0 });
            var animation = new Spine.Animation("basic-probe") { Duration = 1f };
            animation.SetTimelines(new Spine.ExposedList<Spine.Timeline> { timeline }, new Spine.ExposedList<int>());
            data.Animations.Add(animation);
            var skeleton = new Spine.Skeleton(data);
            var state = new Spine.AnimationState(new Spine.AnimationStateData(data));
            var entry = state.SetAnimation(0, animation, false);
            var identity = new AnimationPlaybackIdentity(new ActionIdentity(777, 2, 3), DRPGBattleActionStage.Windup, 0, 7, 0);
            var received = new List<BattleAnimationMarker>();
            using (var binding = source.BindPlayback(entry, identity, received.Add))
            {
                Check(binding != null, "Real TrackEntry binding is absent.");
                source.BindRuntimeId(987); // Rebinding an actor must not rewrite an existing playback identity.
                state.Update(.11f); state.Apply(skeleton);
                Check(received.Count == 1 && received[0].Playback == identity && received[0].Name == "Impact" && received[0].Ordinal == 0,
                    "Real TrackEntry event lost its captured playback identity.");
                state.Update(.1f); state.Apply(skeleton);
                Check(received.Count == 1, "Spine event repeated.");
            }
            entry.TrackTime = 0; state.Apply(skeleton);
            state.Update(.11f); state.Apply(skeleton);
            Check(received.Count == 1, "Disposed TrackEntry binding still emitted events.");
            state.ClearTracks();
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    internal static void FrozenNameFlowsThroughRoot()
    {
        using (var test = new PreludeFixture())
        using (var prelude = test.Start())
        {
            Check(test.Port.Announcement.ActorName == "己方" &&
                test.Port.Announcement.Snapshot.ActorDisplayName == "己方",
                "Root announcement uses a runtime number/live lookup instead of the frozen actor name.");
        }
    }

    internal static void TimingProfileFreezesApprovedFiniteMinimums()
    {
        var profile = ScriptableObject.CreateInstance<DRPGBasicAttackTimingProfile>();
        try
        {
            Set(profile, "announcementSeconds", 1.2f);
            var frozen = profile.Freeze();
            Set(profile, "announcementSeconds", float.PositiveInfinity);
            Set(profile, "conflictHoldSeconds", -.5f);
            Set(profile, "windupMinimumSeconds", float.NaN);
            var corrected = profile.Freeze();
            Check(frozen.AnnouncementSeconds == 1.2f && corrected.AnnouncementSeconds == 0f &&
                corrected.ConflictHoldSeconds == 0f && corrected.WindupMinimumSeconds == 0f &&
                corrected.ReturnSeconds > 0f && corrected.SettlementItemSeconds >= .45f,
                "Timing profile is mutable after freeze or permits invalid/subminimum durations.");
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); }
    }

    internal static void WatchdogRecoveryResistsReentrantCompletion()
    {
        var clock = new ProbeClock(); var watchdog = new BattlePresentationWatchdog(clock);
        var receipt = new PresentationReceipt(new ActionIdentity(777, 1, 1), DRPGBattleActionStage.Windup);
        bool reentered = false;
        watchdog.Watch(receipt, 1, "ConflictCheckpoint", () => reentered =
            receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "reentrant")));
        clock.Now = 1; watchdog.Tick();
        Check(!reentered && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded,
            "Cleanup callback stole the recovering receipt's terminal.");
        var failed = new PresentationReceipt(new ActionIdentity(777, 1, 2), DRPGBattleActionStage.Windup);
        watchdog.Watch(failed, 2, "ConflictCheckpoint", () => throw new InvalidOperationException("cleanup-probe"));
        clock.Now = 2; watchdog.Tick();
        Check(failed.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted, "Failed cleanup pretended to recover.");
    }

    internal static void MissingPlaybackCannotClaimAuthoredFallback()
    {
        using (var test = new PreludeFixture())
        {
            var router = new BattleAnimationPlaybackRouter(test.Clock, (id, c) => null);
            var receipt = router.Play(new BasicAttackWindupCommand(test.Frozen, 0));
            test.Clock.Now = 1; test.Clock.RenderedFrame++; router.Tick();
            Check(!receipt.Completion.IsCompleted, "Missing playback was reported as a normal authored fallback.");
        }
    }

    internal static void DeadlineWinsOverUnpumpedLateMarker()
    {
        foreach (bool atDeadline in new[] { false, true })
        using (var test = new PreludeFixture(false, BasicAttackMarkerMode.ExpectedMarker))
        {
            var host = new GameObject("DeadlineAdapterProbe", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            try
            {
                var label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(host.transform, false);
                var presenter = new BattleActionAnnouncementPresenter(label.AddComponent<TextMeshProUGUI>(), host.GetComponent<CanvasGroup>(), test.Clock);
                var playback = new BattleAnimationPlaybackRouter(test.Clock, (id, c) => new CallbackDisposable(() => { }));
                var adapter = new DRPGBattleTransactionPresentationAdapter(test.Clock, presenter, playback, c => new BattlePresentationHandle());
                var receipt = adapter.PlayWindup(new BasicAttackWindupCommand(test.Frozen, 0));
                test.Clock.Now = 2d + test.Frozen.Timing.WindupMinimumSeconds - (atDeadline ? 0 : .000001d);
                test.Clock.RenderedFrame++;
                playback.ReportMarker(new BattleAnimationMarker(playback.Current, "Impact", 0));
                adapter.Tick();
                Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind ==
                    (atDeadline ? PresentationReceiptTerminalKind.Degraded : PresentationReceiptTerminalKind.Completed),
                    "An unpumped late marker won the exact deadline, or a timely marker was lost.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }

    internal static void RootModuleStartsPreludeWithoutCompletingAction()
    {
        using (var test = new PreludeFixture())
        {
            var slot = new DRPGBattleTransactionPresentationPortSlot(test.Frozen.Identity.BattleGeneration);
            slot.TryBind(test.Frozen.Identity.BattleGeneration, test.Port);
            using (var module = new DRPGStandardBasicAttackTransactionModule(slot))
            {
            var root = module.TryStart(test.Frozen, test.Frozen.Identity, test.Lease, "probe-locked");
            Check(test.Port.History.Count == 1 && test.Lease.IsActive,
                "Root module still faults/releases ownership instead of starting announcement.");
            foreach (var minimum in new[] { .75d, .121d, .301d })
            {
                module.Prelude.ExpectedReceipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "probe"));
                test.Clock.Now += minimum; test.Clock.RenderedFrame++; module.Tick();
            }
            Check(module.Prelude.ReadyForImpact && test.Lease.IsActive && !root.Receipt.Completion.IsCompleted &&
                test.Port.History.Count == 3 && test.State.Enemy.CurrentHp == 120,
                "Root module skipped a stage, committed HP or completed at the prelude boundary.");
            }
        }
    }

    internal static void ForeignReceiptFaultCannotCancelAnotherAction()
    {
        using (var test = new PreludeFixture())
        {
            var foreign = new PresentationReceipt(new ActionIdentity(test.Frozen.Identity.BattleGeneration, 1, 999),
                DRPGBattleActionStage.Announcement);
            test.Port.NextReceipt = foreign;
            using (var prelude = test.Start())
            {
                Check(!string.IsNullOrEmpty(prelude.Fault) && !prelude.ReadyForImpact,
                    "Foreign receipt was not rejected.");
                Check(test.Port.Cancelled.Count == 0 && !foreign.Completion.IsCompleted,
                    "Fail-closed cleanup cancelled/completed another action's receipt.");
            }
        }
    }

    internal static void WindupMarkerGate()
    {
        using (var test = new PreludeFixture())
        {
            AnimationPlaybackIdentity created = default;
            int disposed = 0;
            var router = new BattleAnimationPlaybackRouter(test.Clock, (identity, command) =>
            { created = identity; return new CallbackDisposable(() => disposed++); });
            var receipt = router.Play(new BasicAttackWindupCommand(test.Frozen, 0), BasicAttackMarkerMode.ExpectedMarker);
            Check(created.IsValid && created == router.Current && !receipt.Completion.IsCompleted,
                "Windup did not bind identity to the created playback.");
            router.ReportMarker(new BattleAnimationMarker(created, "Impact", 0));
            router.ReportMarker(new BattleAnimationMarker(created, "Impact", 0));
            Check(router.LastMarkerDisposition == "duplicate-marker", "Early duplicate marker was not classified.");
            test.Clock.Now = .299; test.Clock.RenderedFrame++; router.Tick();
            Check(!receipt.Completion.IsCompleted, "Early marker skipped minimum windup.");
            test.Clock.Now = .301; router.Tick();
            Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed && disposed == 1,
                "Valid retained marker did not complete and seal exactly once.");
            router.ReportMarker(new BattleAnimationMarker(created, "Impact", 0)); router.Tick();
            Check(router.LastMarkerDisposition.Contains("late") && disposed == 1, "Late marker revived sealed playback.");
            var firstPlayback = created;
            var second = router.Play(new BasicAttackWindupCommand(test.Frozen, test.Clock.Now), BasicAttackMarkerMode.ExpectedMarker);
            var valid = router.Current;
            Check(valid.PlaybackSerial != firstPlayback.PlaybackSerial, "Same-action playback serial was reused.");
            foreach (var wrong in new[] {
                firstPlayback,
                new AnimationPlaybackIdentity(new ActionIdentity(test.Frozen.Identity.BattleGeneration + 1, 1, 1), valid.Stage, 0, valid.PlaybackSerial, 0),
                new AnimationPlaybackIdentity(new ActionIdentity(valid.Action.BattleGeneration, valid.Action.TurnSerial + 1, valid.Action.ActionId), valid.Stage, 0, valid.PlaybackSerial, 0),
                new AnimationPlaybackIdentity(new ActionIdentity(valid.Action.BattleGeneration, valid.Action.TurnSerial, valid.Action.ActionId + 1), valid.Stage, 0, valid.PlaybackSerial, 0),
                new AnimationPlaybackIdentity(valid.Action, DRPGBattleActionStage.Return, 0, valid.PlaybackSerial, 0),
                new AnimationPlaybackIdentity(valid.Action, valid.Stage, 1, valid.PlaybackSerial, 0),
                new AnimationPlaybackIdentity(valid.Action, valid.Stage, 0, valid.PlaybackSerial + 1, 0),
                new AnimationPlaybackIdentity(valid.Action, valid.Stage, 0, valid.PlaybackSerial, 1) })
                router.ReportMarker(new BattleAnimationMarker(wrong, "Impact", 0));
            router.ReportMarker(new BattleAnimationMarker(valid, "Impact", 1));
            router.ReportMarker(new BattleAnimationMarker(valid, "PresentationEnd", 0));
            test.Clock.Now = 1; test.Clock.RenderedFrame++; router.Tick();
            Check(!second.Completion.IsCompleted, "Wrong playback identity/name/ordinal opened the hit gate.");
            router.ReportMarker(new BattleAnimationMarker(valid, "Impact", 0)); router.Tick();
            Check(second.Completion.IsCompleted, "Valid playback marker was rejected.");
            var fallback = router.Play(new BasicAttackWindupCommand(test.Frozen, 1));
            test.Clock.Now = 1.299; router.Tick(); Check(!fallback.Completion.IsCompleted, "Authored fallback too early.");
            test.Clock.Now = 1.301; test.Clock.RenderedFrame++; router.Tick();
            Check(fallback.Completion.IsCompleted && fallback.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                fallback.Completion.Result.Detail == "AuthoredFallback", "Authored fallback incorrectly degraded.");
        }
    }

    internal static void ReceiptTimeoutCleansOnlyOwnedCue()
    {
        var clock = new ProbeClock(); var watchdog = new BattlePresentationWatchdog(clock);
        var a = new PresentationReceipt(new ActionIdentity(777, 1, 1), DRPGBattleActionStage.Windup);
        var b = new PresentationReceipt(new ActionIdentity(777, 1, 2), DRPGBattleActionStage.Windup);
        int cleanedA = 0, cleanedB = 0;
        watchdog.Watch(a, 1, "ConflictCheckpoint", () => cleanedA++);
        watchdog.Watch(b, 2, "ConflictCheckpoint", () => cleanedB++);
        clock.Now = .999999; watchdog.Tick(); Check(!a.Completion.IsCompleted && cleanedA == 0, "Timeout fired before deadline.");
        clock.Now = 1; watchdog.Tick();
        Check(a.Completion.IsCompleted && a.Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded && cleanedA == 1 && cleanedB == 0 && !b.Completion.IsCompleted,
            "Deadline did not clean only its owned cue and issue Degraded.");
        watchdog.Tick(); Check(watchdog.RecoveryCount == 1 && cleanedA == 1, "Timeout repeated cleanup.");
        Check(!watchdog.TryAccept(a.Owner, a.Stage, 0, a.ReceiptId, new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "late")) &&
            watchdog.LastDroppedReason.Contains("late-after-recovery"), "Late receipt was not classified after recovery.");
        Check(!watchdog.TryAccept(a.Owner, b.Stage, 0, b.ReceiptId, new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "wrong")),
            "Wrong transaction completed another receipt.");
        Check(watchdog.TryAccept(b.Owner, b.Stage, 0, b.ReceiptId, new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "ok")),
            "Matching receipt was rejected.");
        clock.Now = 2; watchdog.Tick(); Check(cleanedB == 0 && b.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed, "Completed cue was timed out.");
    }

    internal sealed class CallbackDisposable : IDisposable
    { private Action callback; internal CallbackDisposable(Action callback) { this.callback = callback; } public void Dispose() { var c = callback; callback = null; c?.Invoke(); } }

    internal static void DirectorCannotAdvanceRulesOrRestore()
    {
        var set = ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        var recipe = ScriptableObject.CreateInstance<BattlePresentationRecipeSO>();
        var restore = ScriptableObject.CreateInstance<BattleConflictRestoreCueSO>();
        var marker = ScriptableObject.CreateInstance<BattleSpineMarkerCueSO>();
        var semantic = BattleSemanticPresentationCueSO.Create(BattleSemanticPresentationModuleKind.Exit, new BattleExitPresentationDefinition());
        try
        {
            var entries = new List<BattlePresentationCueEntry>();
            foreach (var cue in new BattlePresentationCueSO[] { restore, marker, semantic })
            { var e = new BattlePresentationCueEntry(); Set(e, "cue", cue); entries.Add(e); }
            var beat = new BattlePresentationBeat(); Set(beat, "cues", entries);
            Set(recipe, "moment", BattlePresentationMoment.ActionSettled); Set(recipe, "beats", new List<BattlePresentationBeat> { beat });
            Set(set, "baseRecipes", new List<BattlePresentationRecipeSO> { recipe });
            var restoreSpy = new ForbiddenExecutor(BattlePresentationCueKind.ConflictRestore);
            var ruleSpy = new ForbiddenExecutor(BattlePresentationCueKind.SpineMarker);
            var semanticSpy = new ForbiddenExecutor(BattlePresentationCueKind.SemanticModule);
            var director = new BattlePresentationDirector(new BattlePresentationRecipeResolver(), set, new[] { restoreSpy, ruleSpy, semanticSpy },
                new BattleInputGate(), new BattlePresentationTraceBuffer(32));
            var id = new ActionIdentity(777, 2, 3);
            var request = new BattlePresentationRequest(1234, BattlePresentationMoment.ActionSettled, new BattlePresentationScope(777, 2, 3),
                null, Array.Empty<BattleParticipantPresentationSnapshot>(), Array.Empty<int>(), "basic", EAnimationLogicState.None,
                Array.Empty<BattleHitPresentationSnapshot>(), BattlePresentationFacts.Empty, id, DRPGBattleActionStage.Return);
            var run = director.Play(request); director.Tick(10);
            Check(restoreSpy.Calls == 0 && ruleSpy.Calls == 0 && semanticSpy.Calls == 0 && run.IsFinished,
                "Standard leaf Director invoked semantic restore or legacy rule marker executor.");
            var legacy = new BattlePresentationRequest(1235, BattlePresentationMoment.ActionSettled, new BattlePresentationScope(777, 2, 4),
                null, Array.Empty<BattleParticipantPresentationSnapshot>(), Array.Empty<int>(), "basic", EAnimationLogicState.None,
                Array.Empty<BattleHitPresentationSnapshot>(), BattlePresentationFacts.Empty);
            director.Play(legacy); director.Tick(10);
            Check(restoreSpy.Calls == 1 && ruleSpy.Calls == 1 && semanticSpy.Calls == 1,
                "Legacy control did not exercise all forbidden executors; the guard test is inconclusive.");
        }
        finally { UnityEngine.Object.DestroyImmediate(semantic); UnityEngine.Object.DestroyImmediate(marker); UnityEngine.Object.DestroyImmediate(restore); UnityEngine.Object.DestroyImmediate(recipe); UnityEngine.Object.DestroyImmediate(set); }
    }

    internal static void MissingReceiptsRecoverWithinSameRoot()
    {
        using (var test = new PreludeFixture())
        using (var prelude = test.Start())
        {
            test.Clock.Now = 5; test.Clock.RenderedFrame++; prelude.Tick();
            Check(prelude.Stage == DRPGBattleActionStage.ConflictComposition && test.Port.Cancelled.Count == 1,
                "Missing announcement receipt did not clean and continue same root.");
            test.Clock.Now = 10; test.Clock.RenderedFrame++; prelude.Tick();
            test.Clock.Now = 15; test.Clock.RenderedFrame++; prelude.Tick();
            Check(prelude.ReadyForImpact && prelude.Degraded && string.IsNullOrEmpty(prelude.Fault) && test.Port.Cancelled.Count == 3 && test.Lease.IsActive,
                "Missing phase receipts deadlocked, changed root owner or falsely completed action.");
            Check(test.State.Enemy.CurrentHp == 120 && test.State.Player.CurrentHp == 100, "Watchdog resolved rules.");
        }
    }

    internal static void MonoDirectorAndDirectSemanticExitCannotRestoreStandardScope()
    {
        var host = new GameObject("StandardDirectorScopeProbe");
        var set = ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        var exit = BattleSemanticPresentationCueSO.Create(BattleSemanticPresentationModuleKind.Exit, new BattleExitPresentationDefinition());
        try
        {
            var hud = host.AddComponent<BattleHudPresenter>(); hud.Configure(Array.Empty<BattleHudGroupView>());
            var camera = host.AddComponent<Camera>(); camera.enabled = false;
            var cameraPresenter = host.AddComponent<BattleCameraPresenter>(); cameraPresenter.Configure(camera, host.transform);
            var stage = host.AddComponent<BattleStagePresenter>();
            var context = host.AddComponent<BattleConflictPresentationContext>(); context.Configure(hud, cameraPresenter, stage, null, null);
            var director = host.AddComponent<DRPGBattlePresentationDirector>(); director.ConfigureConflictContext(context); director.Bind(set);
            var executor = host.AddComponent<BattleSemanticModuleCueExecutor>(); executor.Configure(context, null);
            var identity = new ActionIdentity(778, 2, 3);
            var request = new BattlePresentationRequest(900, BattlePresentationMoment.ActionSettled, new BattlePresentationScope(778, 2, 3),
                null, Array.Empty<BattleParticipantPresentationSnapshot>(), Array.Empty<int>(), "basic", EAnimationLogicState.None,
                Array.Empty<BattleHitPresentationSnapshot>(), BattlePresentationFacts.Empty, identity, DRPGBattleActionStage.Return);
            Check(context.Ensure(request) != null && context.ActiveScopeCount == 1, "Real conflict scope was not created.");
            executor.Play(exit, request);
            Check(context.ActiveScopeCount == 1, "Direct semantic Exit restored a standard scope.");
            var run = director.Play(request);
            Check(run.IsFinished && context.ActiveScopeCount == 1, "Mono Director restored at a leaf terminal.");
            director.enabled = false;
            Check(context.ActiveScopeCount == 1, "Mono Director disable restored a retained standard scope.");
            // The transaction owner, not a leaf callback, performs the explicit release.
            context.Restore(request); director.ReleaseStandardScope(identity);
            Check(context.ActiveScopeCount == 0, "Explicit matching transaction cleanup could not release the scope.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(exit); UnityEngine.Object.DestroyImmediate(set); }
    }

    internal static void BoundSpritePoseDoesNotLungeOrRestoreOnSeal()
    {
        using (var test = new PreludeFixture())
        {
            var host = new GameObject("BoundSpriteMotionRoot");
            var visual = new GameObject("VisualRoot"); visual.transform.SetParent(host.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            var driver = visual.AddComponent<BattleWorldActorSpriteDriver>();
            var texture = new Texture2D(4, 6); var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, 0));
            var attack = Sprite.Create(texture, new Rect(0, 0, 4, 6), new Vector2(.4f, .6f));
            Set(test.State.Player.Profile, "fullPortrait", sprite);
            Set(test.State.Player.Profile, "attackCutInPortrait", attack);
            try
            {
                driver.Configure(renderer, 2); driver.Bind(new DRPGBattleParticipantViewModel(test.State.Player, 0, true));
                var pos = host.transform.localPosition; var scale = host.transform.localScale;
                var binding = driver.PlayBoundIntent(new AnimationPlaybackIdentity(test.Frozen.Identity, DRPGBattleActionStage.Windup, 0, 99, 0), EAnimationLogicState.Skill_1);
                Check(binding != null && host.transform.localPosition == pos && host.transform.localScale == scale,
                    "Bound sprite playback missing or moved/scaled the tactical root.");
                Check(Mathf.Abs(renderer.bounds.min.y - host.transform.position.y) < .001f,
                    "Different attack sprite pivot changed the authored floor.");
                binding.Dispose(); Check(DRPGBasicAttackVisualRestoreProbe.DisplaysFrame(renderer, attack) && host.transform.localPosition == pos,
                    "Sealing playback hid/restored the pose instead of only detaching its events.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(attack); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
    private sealed class ForbiddenExecutor : IBattlePresentationCueExecutor
    {
        internal int Calls;
        internal ForbiddenExecutor(BattlePresentationCueKind kind) { Kind = kind; }
        public BattlePresentationCueKind Kind { get; }
        public IBattlePresentationHandle Play(BattlePresentationCueSO cue, BattlePresentationRequest request)
        { Calls++; var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; }
    }
    internal static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f == null) throw new InvalidOperationException("Missing fixture field " + field);
        f.SetValue(target, value);
    }

    internal sealed class ProbeClock : IDRPGBattlePresentationClock
    { public double Now { get; set; } public long RenderedFrame { get; set; } }

    internal sealed class PreludeFixture : IDisposable
    {
        internal readonly DRPGBattleStateMutationFixture State = DRPGBattleStateMutationFixture.Create();
        internal readonly ProbeClock Clock = new ProbeClock();
        internal readonly RecordingPreludePort Port = new RecordingPreludePort();
        internal readonly DRPGSkillSO Skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        internal readonly DRPGBasicAttackFrozenSnapshot Frozen;
        internal readonly BattleRuleMutationLease Lease;
        internal PreludeFixture(bool enemy = false, BasicAttackMarkerMode markerMode = BasicAttackMarkerMode.AuthoredFallback)
        {
            Port.TestClock = Clock;
            State.Lease.Dispose();
            var id = new ActionIdentity(State.BattleGeneration, 1, 1);
            Check(State.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(id), out Lease, out var error), error);
            var actor = new ParticipantId(enemy ? State.Enemy.RuntimeId : State.Player.RuntimeId);
            var target = new ParticipantId(enemy ? State.Player.RuntimeId : State.Enemy.RuntimeId);
            Frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(id.BattleGeneration, 1, actor),
                new TargetSelectionToken(id.BattleGeneration, 1, 1, actor), target, Skill,
                enemy ? DRPGBattleSide.Enemy : DRPGBattleSide.Player, new ActionTracePosition(-3, 0, 0),
                new ActionTracePosition(enemy ? .5f : -.5f, 0, 0), enemy ? "敌方" : "己方", markerMode).WithIdentity(id);
        }
        internal DRPGBasicAttackPrelude Start() => new DRPGBasicAttackPrelude(Frozen, Port, Clock, Lease, "probe-locked");
        public void Dispose() { Lease.Dispose(); UnityEngine.Object.DestroyImmediate(Skill); State.Dispose(); }
    }

    internal sealed class RecordingPreludePort : IStandardBasicAttackPresentationPort, IDRPGBattlePresentationPump
    {
        internal readonly List<PresentationReceipt> History = new List<PresentationReceipt>();
        internal readonly List<PresentationReceipt> Cancelled = new List<PresentationReceipt>();
        internal ConflictCompositionCommand Conflict;
        internal AnnouncementCommand Announcement;
        internal PresentationReceipt NextReceipt;
        internal IDRPGBattlePresentationClock TestClock;
        public IDRPGBattlePresentationClock Clock => TestClock;
        public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest request, out DRPGBasicAttackPresentationFreezeInput snapshot) { snapshot = default; return false; }
        private PresentationReceipt Issue(ActionIdentity id, DRPGBattleActionStage stage)
        { var r = NextReceipt ?? new PresentationReceipt(id, stage); NextReceipt = null; History.Add(r); return r; }
        public PresentationReceipt Announce(in AnnouncementCommand c) { Announcement = c; return Issue(c.Identity, DRPGBattleActionStage.Announcement); }
        public PresentationReceipt EnterConflict(in ConflictCompositionCommand c) { Conflict = c; return Issue(c.Identity, DRPGBattleActionStage.ConflictComposition); }
        public PresentationReceipt PlayWindup(in BasicAttackWindupCommand c) => Issue(c.Identity, DRPGBattleActionStage.Windup);
        public PresentationReceipt PresentImpact(in ImpactPresentationCommand c) => throw new InvalidOperationException("Prelude requested impact.");
        public PresentationReceipt PresentReaction(in ReactionPresentationCommand c) => throw new InvalidOperationException("Prelude requested reaction.");
        public PresentationReceipt Return(in ReturnPresentationCommand c) => throw new InvalidOperationException("Prelude requested return.");
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand c) => throw new InvalidOperationException("Prelude requested settlement.");
        public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand c) => throw new InvalidOperationException("Prelude requested broad cleanup.");
        public void Tick() { }
        public void CancelReceipt(PresentationReceipt r, string reason) { Cancelled.Add(r); }
    }
}
#endif
