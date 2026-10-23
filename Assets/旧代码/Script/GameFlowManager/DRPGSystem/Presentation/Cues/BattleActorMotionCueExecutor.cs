using UnityEngine;

public sealed class BattleActorMotionCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private BattleActorPresenter presenter;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.ActorMotion;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (presenter == null || request?.Actor == null ||
            !(cue is BattleActorMotionCueSO) ||
            presenter.BoundRuntimeId != request.Actor.RuntimeId)
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        return presenter.PlayIntent(request.AnimationIntent);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
