using UnityEngine;

public sealed class BattleWorldActorSpineDriver : MonoBehaviour,
    IBattleWorldActorVisualDriver
{
    [Tooltip("Any Spine-backed component implementing IBattleActorAnimationDriver.")]
    [SerializeField] private MonoBehaviour animationDriverComponent;

    private IBattleActorAnimationDriver animationDriver;
    private DRPGBattleParticipantViewModel boundActor;

    public void Bind(DRPGBattleParticipantViewModel actor)
    {
        boundActor = actor;
        ResolveDriver();
        animationDriver?.BindActor(actor != null ? actor.Profile : null);
        animationDriver?.SetLowHpState(
            actor != null
                ? actor.LowHpState
                : DRPGLowHpPresentationState.Normal);
    }

    public void RestoreVisual()
    {
        ResolveDriver();
        animationDriver?.BindActor(
            boundActor != null ? boundActor.Profile : null);
        animationDriver?.SetLowHpState(
            boundActor != null
                ? boundActor.LowHpState
                : DRPGLowHpPresentationState.Normal);
    }

    public IBattlePresentationHandle PlayIntent(EAnimationLogicState intent)
    {
        ResolveDriver();
        if (animationDriver != null)
            return animationDriver.PlayIntent(intent);

        var skipped = new BattlePresentationHandle();
        skipped.TrySettle(BattlePresentationOutcome.Skipped);
        return skipped;
    }

    public void ResetVisual()
    {
        boundActor = null;
        ResolveDriver();
        animationDriver?.BindActor(null);
        animationDriver?.SetLowHpState(DRPGLowHpPresentationState.Normal);
    }

    private void ResolveDriver()
    {
        if (animationDriver == null)
            animationDriver = animationDriverComponent as IBattleActorAnimationDriver;
    }
}
