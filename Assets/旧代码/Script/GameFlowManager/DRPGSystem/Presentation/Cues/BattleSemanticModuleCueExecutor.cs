using UnityEngine;

public sealed class BattleSemanticModuleCueExecutor : MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private BattleConflictPresentationContext context;
    [SerializeField] private BattlePresentationResourceCatalog resources;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.SemanticModule;

    public void Configure(
        BattleConflictPresentationContext presentationContext,
        BattlePresentationResourceCatalog resourceCatalog)
    {
        context = presentationContext;
        resources = resourceCatalog;
    }

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (context == null || request == null ||
            !(cue is BattleSemanticPresentationCueSO semantic))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        if (request.IsStandardBasicAttackLeaf &&
            (semantic.ModuleKind == BattleSemanticPresentationModuleKind.Exit ||
             semantic.ModuleKind == BattleSemanticPresentationModuleKind.Spine))
            return Settled(BattlePresentationOutcome.Skipped);

        switch (semantic.ModuleKind)
        {
            case BattleSemanticPresentationModuleKind.Hud:
                BattleHudPresentationDefinition hud =
                    semantic.GetDefinition<BattleHudPresentationDefinition>();
                if (hud != null)
                    context.ApplyHud(request, hud.Visibility);
                break;
            case BattleSemanticPresentationModuleKind.Stage:
                ApplyStage(
                    semantic.GetDefinition<BattleStagePresentationDefinition>(),
                    request);
                break;
            case BattleSemanticPresentationModuleKind.Camera:
                ApplyCamera(
                    semantic.GetDefinition<BattleCameraPresentationDefinition>(),
                    request);
                break;
            case BattleSemanticPresentationModuleKind.Attacker:
                BattleActorPresentationDefinition attacker =
                    semantic.GetDefinition<BattleActorPresentationDefinition>();
                if (attacker != null)
                    context.ApplyAttacker(request, attacker.Visible, attacker.Scale);
                break;
            case BattleSemanticPresentationModuleKind.Target:
                BattleTargetPresentationDefinition target =
                    semantic.GetDefinition<BattleTargetPresentationDefinition>();
                if (target != null)
                {
                    context.ApplyTargets(
                        request,
                        target.Focus,
                        target.Scale,
                        target.RetreatDistance,
                        target.Others);
                }
                break;
            case BattleSemanticPresentationModuleKind.Background:
                BattleBackgroundPresentationDefinition background =
                    semantic.GetDefinition<BattleBackgroundPresentationDefinition>();
                if (background != null)
                {
                    context.ApplyBackground(
                        request,
                        background.Policy,
                        background.Intensity);
                }
                break;
            case BattleSemanticPresentationModuleKind.Spine:
                return context.PlayActorIntent(request);
            case BattleSemanticPresentationModuleKind.Exit:
                BattleExitPresentationDefinition exit =
                    semantic.GetDefinition<BattleExitPresentationDefinition>();
                return context.PlayRestore(request, exit);
            case BattleSemanticPresentationModuleKind.Feedback:
                BattleFeedbackPresentationDefinition feedback =
                    semantic.GetDefinition<BattleFeedbackPresentationDefinition>();
                return feedback != null
                    ? context.PlayImpactFeedback(request, feedback)
                    : Settled(BattlePresentationOutcome.Skipped);
        }

        return Settled(BattlePresentationOutcome.Completed);
    }

    private void ApplyStage(
        BattleStagePresentationDefinition definition,
        BattlePresentationRequest request)
    {
        if (definition == null ||
            definition.Policy == BattleStagePresentationPolicy.KeepCurrent)
        {
            context.Ensure(request);
            return;
        }

        if (resources != null && resources.TryGetStage(
                definition.StageProfileKey,
                out BattleStageProfileSO profile))
        {
            context.SwitchStage(request, profile);
        }
    }

    private void ApplyCamera(
        BattleCameraPresentationDefinition definition,
        BattlePresentationRequest request)
    {
        if (definition == null || resources == null ||
            !resources.TryGetShot(
                definition.CameraRigKey,
                context.ActiveStage,
                out BattleCameraShotDefinition baseShot))
        {
            return;
        }

        context.PlayCamera(
            request,
            new BattleCameraShotDefinition(
                baseShot.ShotId,
                baseShot.LocalPosition,
                baseShot.LocalEulerAngles,
                definition.FieldOfView,
                definition.TransitionSeconds),
            definition.FramingMode,
            definition.HorizontalPadding,
            definition.VerticalPadding,
            definition.FieldOfView,
            definition.TransitionSeconds);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
