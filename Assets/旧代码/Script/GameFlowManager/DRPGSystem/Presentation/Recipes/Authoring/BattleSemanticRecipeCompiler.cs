using System;
using System.Collections.Generic;
using UnityEngine;

public interface IBattleSemanticRecipeCompiler
{
    ResolvedBattlePresentationRecipe Compile(
        ResolvedBattleSkillPresentation semantic,
        BattlePresentationRequest request);
}

public sealed class BattleSemanticRecipeCompiler :
    IBattleSemanticRecipeCompiler,
    IDisposable
{
    private const float FocusMinimumSeconds = 0.20f;
    private const float IntentMinimumSeconds = 0.55f;
    private const float ImpactMinimumSeconds = 0.75f;
    private const float RestoreMinimumSeconds = 0.30f;

    private readonly Dictionary<string, ResolvedBattlePresentationRecipe> cache =
        new Dictionary<string, ResolvedBattlePresentationRecipe>(StringComparer.Ordinal);
    private readonly List<BattleSemanticPresentationCueSO> ownedCues =
        new List<BattleSemanticPresentationCueSO>();
    private bool disposed;

    public ResolvedBattlePresentationRecipe Compile(
        ResolvedBattleSkillPresentation semantic,
        BattlePresentationRequest request)
    {
        if (semantic == null)
            throw new ArgumentNullException(nameof(semantic));
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (disposed)
            throw new ObjectDisposedException(nameof(BattleSemanticRecipeCompiler));

        string cacheKey = semantic.PresentationKey + "|" + request.Moment;
        if (cache.TryGetValue(cacheKey, out ResolvedBattlePresentationRecipe cached))
            return cached;

        var beats = new List<ResolvedBattlePresentationBeat>();
        if (request.Moment == BattlePresentationMoment.ActionWindup)
        {
            var focusCues = new List<ResolvedBattlePresentationCue>();
            Add(focusCues, BattleSemanticPresentationModuleKind.Hud,
                semantic.Hud.Enabled, semantic.Hud.Definition,
                BattlePresentationLaneKind.ScreenModal, false);
            Add(focusCues, BattleSemanticPresentationModuleKind.Stage,
                semantic.Stage.Enabled, semantic.Stage.Definition,
                BattlePresentationLaneKind.ScreenFX, false);
            Add(focusCues, BattleSemanticPresentationModuleKind.Camera,
                semantic.Camera.Enabled, semantic.Camera.Definition,
                BattlePresentationLaneKind.ScreenFX, false);
            Add(focusCues, BattleSemanticPresentationModuleKind.Attacker,
                semantic.Attacker.Enabled, semantic.Attacker.Definition,
                BattlePresentationLaneKind.ActiveActor, false);
            Add(focusCues, BattleSemanticPresentationModuleKind.Target,
                semantic.Target.Enabled, semantic.Target.Definition,
                BattlePresentationLaneKind.TargetFeedback, false);
            Add(focusCues, BattleSemanticPresentationModuleKind.Background,
                semantic.Background.Enabled, semantic.Background.Definition,
                BattlePresentationLaneKind.ScreenFX, false);
            AddBeat(
                beats,
                "semantic-focus-" + request.Moment,
                FocusMinimumSeconds,
                false,
                focusCues);

            var intentCues = new List<ResolvedBattlePresentationCue>();
            Add(intentCues, BattleSemanticPresentationModuleKind.Spine,
                semantic.Spine.Enabled, semantic.Spine.Definition,
                BattlePresentationLaneKind.Participant, true);
            AddBeat(
                beats,
                "semantic-intent-" + request.Moment,
                IntentMinimumSeconds,
                true,
                intentCues);
        }
        else if (request.Moment == BattlePresentationMoment.ImpactResolved)
        {
            var impactCues = new List<ResolvedBattlePresentationCue>();
            Add(impactCues, BattleSemanticPresentationModuleKind.Feedback,
                semantic.Feedback.Enabled, semantic.Feedback.Definition,
                BattlePresentationLaneKind.ScreenFX, true);
            AddBeat(
                beats,
                "semantic-impact-" + request.Moment,
                Mathf.Max(
                    ImpactMinimumSeconds,
                    semantic.Feedback.Enabled
                        ? semantic.Feedback.Definition.HoldSeconds
                        : 0f),
                semantic.Feedback.Enabled,
                impactCues);
        }
        else if (request.Moment == BattlePresentationMoment.ActionSettled)
        {
            var exitCues = new List<ResolvedBattlePresentationCue>();
            Add(exitCues, BattleSemanticPresentationModuleKind.Exit,
                semantic.Exit.Enabled, semantic.Exit.Definition,
                BattlePresentationLaneKind.ScreenModal, true);
            AddBeat(
                beats,
                "semantic-exit-" + request.Moment,
                Mathf.Max(
                    RestoreMinimumSeconds,
                    semantic.Exit.Enabled
                        ? semantic.Exit.Definition.RestoreSeconds
                        : 0f),
                false,
                exitCues);
        }
        bool ownsWindupGate =
            request.Moment == BattlePresentationMoment.ActionWindup &&
            semantic.Spine.Enabled && beats.Count > 0;
        bool ownsImpactGate =
            request.Moment == BattlePresentationMoment.ImpactResolved &&
            semantic.Feedback.Enabled && beats.Count > 0;
        float maximumGateSeconds = ownsWindupGate
            ? FocusMinimumSeconds + IntentMinimumSeconds + 0.75f
            : ownsImpactGate
                ? Mathf.Max(1f, semantic.Feedback.Definition.HoldSeconds + 0.75f)
                : 0f;
        var result = new ResolvedBattlePresentationRecipe(
            new[] { "semantic:" + semantic.PresentationKey },
            ownsWindupGate || ownsImpactGate
                ? BattlePresentationGateKind.ActionFlow
                : BattlePresentationGateKind.None,
            maximumGateSeconds,
            beats);
        cache.Add(cacheKey, result);
        return result;
    }

    private static void AddBeat(
        ICollection<ResolvedBattlePresentationBeat> destination,
        string beatId,
        float minimumDurationSeconds,
        bool releasesGate,
        IReadOnlyCollection<ResolvedBattlePresentationCue> cues)
    {
        if (cues == null || cues.Count == 0)
            return;
        destination.Add(new ResolvedBattlePresentationBeat(
            beatId,
            0f,
            Mathf.Max(0f, minimumDurationSeconds),
            releasesGate,
            cues));
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        cache.Clear();
        for (int i = ownedCues.Count - 1; i >= 0; i--)
        {
            BattleSemanticPresentationCueSO cue = ownedCues[i];
            if (cue == null)
                continue;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(cue);
            else
                UnityEngine.Object.DestroyImmediate(cue);
        }
        ownedCues.Clear();
    }

    private void Add<TDefinition>(
        ICollection<ResolvedBattlePresentationCue> destination,
        BattleSemanticPresentationModuleKind kind,
        bool enabled,
        TDefinition definition,
        BattlePresentationLaneKind lane,
        bool blocksBeat)
        where TDefinition : class
    {
        if (!enabled)
            return;
        BattleSemanticPresentationCueSO cue =
            BattleSemanticPresentationCueSO.Create(kind, definition);
        ownedCues.Add(cue);
        destination.Add(new ResolvedBattlePresentationCue(
            cue,
            lane,
            BattlePresentationConflictPolicy.Replace,
            blocksBeat));
    }
}
