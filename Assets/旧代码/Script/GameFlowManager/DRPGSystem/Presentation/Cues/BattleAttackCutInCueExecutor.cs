using UnityEngine;

public sealed class BattleAttackCutInCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private BattleAttackCutInPresenter presenter;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.AttackCutIn;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (presenter == null || request == null || request.Actor == null ||
            !(cue is BattleAttackCutInCueSO cutInCue))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        return presenter.Play(
            cutInCue.Stage,
            request.Actor,
            cutInCue.DurationSeconds);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
