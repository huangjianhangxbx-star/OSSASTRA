using UnityEngine;

public sealed class BattleQuoteCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private BattleTurnQuotePresenter presenter;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.TurnQuote;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (presenter == null || request?.Actor == null ||
            !(cue is BattleQuoteCueSO quoteCue))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        return presenter.Show(request.Actor, quoteCue.Context);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
