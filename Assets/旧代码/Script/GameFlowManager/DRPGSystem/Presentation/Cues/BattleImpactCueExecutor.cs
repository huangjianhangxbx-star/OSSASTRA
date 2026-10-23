using UnityEngine;

public sealed class BattleImpactCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private BattleImpactFeedbackPresenter presenter;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.ImpactFeedback;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (presenter == null || request == null ||
            !(cue is BattleImpactCueSO impactCue))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        return presenter.Present(request.Hits, impactCue.HoldSeconds);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
