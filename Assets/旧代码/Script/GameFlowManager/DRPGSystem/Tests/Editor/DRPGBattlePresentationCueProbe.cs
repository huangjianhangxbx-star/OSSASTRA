#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattlePresentationCueProbe
{
    public static string Run()
    {
        var created = new List<UnityEngine.Object>();
        try
        {
            VerifyMissingPresentersFailOpen(created);
            VerifyAttackCutInCueContract(created);
            VerifyAttackCutInOpenOwnsVisibleState(created);
            VerifyBreakExecutorFiltersImmutableFacts(created);
            VerifySemanticCueKindsFailOpen(created);
            VerifyImmediateSemanticCueExecution(created);
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG tracked presentation cue executors";
    }

    private static void VerifyAttackCutInCueContract(
        ICollection<UnityEngine.Object> created)
    {
        var host = new GameObject("Attack Cut-In Cue Probe");
        created.Add(host);
        BattleAttackCutInCueExecutor executor =
            host.AddComponent<BattleAttackCutInCueExecutor>();
        BattleAttackCutInCueSO cue =
            ScriptableObject.CreateInstance<BattleAttackCutInCueSO>();
        created.Add(cue);

        AssertEqual(BattlePresentationCueKind.AttackCutIn, cue.Kind,
            "Attack cut-in assets must expose their dedicated cue kind");
        AssertEqual(BattlePresentationCueKind.AttackCutIn, executor.Kind,
            "Attack cut-in executors must register the dedicated cue kind");
        AssertEqual(
            BattlePresentationOutcome.Skipped,
            executor.Play(cue, CreateRequest(false)).Outcome,
            "A missing attack cut-in presenter must fail open");
    }

    private static void VerifyAttackCutInOpenOwnsVisibleState(
        ICollection<UnityEngine.Object> created)
    {
        var host = new GameObject("Attack Cut-In Presenter Probe");
        created.Add(host);
        var overlay = new GameObject("Overlay", typeof(RectTransform));
        overlay.transform.SetParent(host.transform, false);
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(overlay.transform, false);
        var portraitObject = new GameObject(
            "Portrait",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        portraitObject.transform.SetParent(overlay.transform, false);
        Image portrait = portraitObject.GetComponent<Image>();
        overlay.SetActive(false);

        var texture = new Texture2D(2, 2);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            Vector2.one * 0.5f);
        created.Add(sprite);
        created.Add(texture);

        BattleAttackCutInPresenter presenter =
            host.AddComponent<BattleAttackCutInPresenter>();
        SetField(presenter, "visibleRoot", overlay);
        SetField(presenter, "panel", panel.GetComponent<RectTransform>());
        SetField(presenter, "portrait", portrait);
        SetField(
            presenter,
            "portraitTransform",
            portraitObject.GetComponent<RectTransform>());

        BattleParticipantPresentationSnapshot actor =
            new BattleParticipantPresentationSnapshot(
                7,
                "Cut-In Actor",
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember,
                100f,
                100f,
                DRPGLowHpPresentationState.Normal,
                sprite,
                null,
                null);
        IBattlePresentationHandle playback = presenter.Play(
            BattleAttackCutInStage.Open,
            actor,
            0.2f);

        AssertFalse(playback.IsSettled,
            "Opening the cut-in must own a live DOTween presentation handle");
        AssertEqual(true, overlay.activeSelf,
            "Opening the cut-in must reveal its isolated overlay root");
        AssertEqual(sprite, portrait.sprite,
            "Opening the cut-in must bind the acting participant Sprite");
        AssertEqual(7, presenter.BoundActorRuntimeId,
            "The visible cut-in must retain the immutable actor identity");

        playback.Cancel(BattlePresentationCancelReason.ScopeEnded);
        AssertEqual(BattlePresentationOutcome.Cancelled, playback.Outcome,
            "Cancelling the open stage must settle its handle");
        AssertEqual(false, overlay.activeSelf,
            "Cancelling the open stage must not leave a stale overlay");
    }

    private static void VerifyImmediateSemanticCueExecution(
        ICollection<UnityEngine.Object> created)
    {
        var host = new GameObject("Immediate Semantic Cue Probe");
        created.Add(host);
        ProbeSemanticSurface surface =
            host.AddComponent<ProbeSemanticSurface>();
        BattleTargetFeedbackCueExecutor targetExecutor =
            host.AddComponent<BattleTargetFeedbackCueExecutor>();
        BattleAudioPresentationCueExecutor audioExecutor =
            host.AddComponent<BattleAudioPresentationCueExecutor>();
        SetField(targetExecutor, "surfaceComponent", surface);
        SetField(audioExecutor, "audioCueSinkComponent", surface);

        BattleTargetFeedbackCueSO targetCue =
            ScriptableObject.CreateInstance<BattleTargetFeedbackCueSO>();
        BattleAudioPresentationCueSO audioCue =
            ScriptableObject.CreateInstance<BattleAudioPresentationCueSO>();
        created.Add(targetCue);
        created.Add(audioCue);
        BattlePresentationRequest request = CreateRequest(false);

        AssertEqual(
            BattlePresentationOutcome.Completed,
            targetExecutor.Play(targetCue, request).Outcome,
            "Target feedback must complete immediately after updating the surface");
        AssertEqual(2, surface.LastTargetRuntimeId,
            "Target feedback must use the immutable request target");
        AssertEqual(DRPGBattleTargetFeedback.Selected, surface.LastFeedback,
            "The target cue must preserve its configured semantic feedback");

        AssertEqual(
            BattlePresentationOutcome.Completed,
            audioExecutor.Play(audioCue, request).Outcome,
            "Semantic audio dispatch must complete without owning audio lifetime");
        AssertEqual(BattleAudioCue.Attack, surface.LastAudioRequest.Cue,
            "The audio executor must map cue asset data to the existing sink");
        AssertEqual(2, surface.LastAudioRequest.TargetRuntimeId,
            "The audio request must preserve its immutable target identity");
    }

    private static void VerifySemanticCueKindsFailOpen(
        ICollection<UnityEngine.Object> created)
    {
        var host = new GameObject("Semantic Cue Probe");
        created.Add(host);
        var executors = new IBattlePresentationCueExecutor[]
        {
            host.AddComponent<BattleTimelineCueExecutor>(),
            host.AddComponent<BattleActorMotionCueExecutor>(),
            host.AddComponent<BattleQuoteCueExecutor>(),
            host.AddComponent<BattleTargetFeedbackCueExecutor>(),
            host.AddComponent<BattleAudioPresentationCueExecutor>()
        };
        BattlePresentationCueSO[] cues =
        {
            ScriptableObject.CreateInstance<BattleTimelineCueSO>(),
            ScriptableObject.CreateInstance<BattleActorMotionCueSO>(),
            ScriptableObject.CreateInstance<BattleQuoteCueSO>(),
            ScriptableObject.CreateInstance<BattleTargetFeedbackCueSO>(),
            ScriptableObject.CreateInstance<BattleAudioPresentationCueSO>()
        };
        BattlePresentationCueKind[] expectedKinds =
        {
            BattlePresentationCueKind.TimelineTransition,
            BattlePresentationCueKind.ActorMotion,
            BattlePresentationCueKind.TurnQuote,
            BattlePresentationCueKind.TargetFeedback,
            BattlePresentationCueKind.Audio
        };

        for (int i = 0; i < cues.Length; i++)
        {
            created.Add(cues[i]);
            AssertEqual(expectedKinds[i], cues[i].Kind,
                "Each semantic cue asset must expose its fixed kind");
            AssertEqual(expectedKinds[i], executors[i].Kind,
                "Each semantic executor must register its cue kind");
            AssertEqual(
                BattlePresentationOutcome.Skipped,
                executors[i].Play(cues[i], CreateRequest(false)).Outcome,
                "A missing semantic presentation dependency must fail open");
        }
    }

    private static void VerifyMissingPresentersFailOpen(
        ICollection<UnityEngine.Object> created)
    {
        var host = new GameObject("Cue Executor Probe");
        created.Add(host);
        BattleImpactCueExecutor impact = host.AddComponent<BattleImpactCueExecutor>();
        BattleDamageBrokenCutInCueExecutor cutIn =
            host.AddComponent<BattleDamageBrokenCutInCueExecutor>();
        BattleImpactCueSO impactCue =
            ScriptableObject.CreateInstance<BattleImpactCueSO>();
        BattleDamageBrokenCutInCueSO breakCue =
            ScriptableObject.CreateInstance<BattleDamageBrokenCutInCueSO>();
        created.Add(impactCue);
        created.Add(breakCue);

        AssertEqual(BattlePresentationCueKind.ImpactFeedback, impact.Kind,
            "Impact executor must register the impact cue kind");
        AssertEqual(BattlePresentationCueKind.DamageBrokenCutIn, cutIn.Kind,
            "Break executor must register the break cue kind");
        AssertEqual(BattlePresentationOutcome.Skipped,
            impact.Play(impactCue, CreateRequest(false)).Outcome,
            "A missing impact presenter must fail open as skipped");
        AssertEqual(BattlePresentationOutcome.Skipped,
            cutIn.Play(breakCue, CreateRequest(true)).Outcome,
            "A missing break presenter must fail open as skipped");
    }

    private static void VerifyBreakExecutorFiltersImmutableFacts(
        ICollection<UnityEngine.Object> created)
    {
        var host = new GameObject("Break Cue Probe", typeof(RectTransform));
        created.Add(host);
        BattleDamageBrokenCutInPresenter presenter =
            host.AddComponent<BattleDamageBrokenCutInPresenter>();
        GameObject overlay = new GameObject(
            "Overlay",
            typeof(RectTransform),
            typeof(CanvasGroup));
        overlay.transform.SetParent(host.transform, false);
        GameObject viewObject = new GameObject("View", typeof(RectTransform));
        viewObject.transform.SetParent(overlay.transform, false);
        BattleDamageBrokenCutInView view = viewObject.AddComponent<BattleDamageBrokenCutInView>();
        SetField(presenter, "visibleRoot", overlay);
        SetField(presenter, "safeArea", overlay.GetComponent<RectTransform>());
        SetField(presenter, "views", new List<BattleDamageBrokenCutInView> { view });

        BattleDamageBrokenCutInCueExecutor executor =
            host.AddComponent<BattleDamageBrokenCutInCueExecutor>();
        SetField(executor, "presenter", presenter);
        BattleDamageBrokenCutInCueSO cue =
            ScriptableObject.CreateInstance<BattleDamageBrokenCutInCueSO>();
        created.Add(cue);

        IBattlePresentationHandle playback = executor.Play(
            cue,
            CreateRequest(true));
        AssertFalse(playback.IsSettled,
            "A matching immutable break target must start the cut-in");
        playback.Cancel(BattlePresentationCancelReason.ScopeEnded);
        AssertEqual(BattlePresentationOutcome.Cancelled, playback.Outcome,
            "Cancelling the executor handle must cancel the presenter tween");
        AssertFalse(presenter.IsPlaying,
            "Executor cancellation must not leave a dangling cut-in tween");

        IBattlePresentationHandle skipped = executor.Play(
            cue,
            CreateRequest(false));
        AssertEqual(BattlePresentationOutcome.Skipped, skipped.Outcome,
            "Targets absent from immutable break facts must be skipped");
    }

    private static BattlePresentationRequest CreateRequest(bool includeBreak)
    {
        var target = new BattleParticipantPresentationSnapshot(
            2,
            "Party",
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.PartyMember,
            30f,
            100f,
            DRPGLowHpPresentationState.DamageBroken,
            null,
            null,
            null);
        var hit = new BattleHitPresentationSnapshot(
            1,
            2,
            "probe",
            60f,
            30f,
            30f,
            true,
            false,
            false,
            DRPGLowHpPresentationState.DamageBroken,
            DRPGBattleRosterRole.PartyMember);
        return new BattlePresentationRequest(
            includeBreak ? 2L : 1L,
            BattlePresentationMoment.ImpactResolved,
            new BattlePresentationScope(1L, 1L, 1L),
            null,
            new[] { target },
            new[] { 1, 2 },
            "probe",
            EAnimationLogicState.Skill_1,
            new[] { hit },
            new BattlePresentationFacts(
                includeBreak ? new[] { 2 } : Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>()));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, fieldName);
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}

public sealed class ProbeSemanticSurface :
    MonoBehaviour,
    IDRPGBattleTargetFeedbackSurface,
    IBattleAudioCueSink
{
    public int LastTargetRuntimeId { get; private set; }
    public DRPGBattleTargetFeedback LastFeedback { get; private set; }
    public BattleAudioCueRequest LastAudioRequest { get; private set; }

    public void ApplyTargetFeedback(
        int runtimeId,
        DRPGBattleTargetFeedback feedback)
    {
        LastTargetRuntimeId = runtimeId;
        LastFeedback = feedback;
    }

    public void ClearTargetFeedback()
    {
        LastTargetRuntimeId = 0;
        LastFeedback = DRPGBattleTargetFeedback.None;
    }

    public void Begin(DRPGBattleAudioPaletteSO palette)
    {
    }

    public void Play(BattleAudioCueRequest request)
    {
        LastAudioRequest = request;
    }

    public void End()
    {
    }
}
#endif
