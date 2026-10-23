using System.Collections.Generic;
using UnityEngine;

public sealed class BattleDamageBrokenCutInCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private BattleDamageBrokenCutInPresenter presenter;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.DamageBrokenCutIn;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (presenter == null || request == null ||
            !(cue is BattleDamageBrokenCutInCueSO))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        var eligibleIds = new HashSet<int>(
            request.Facts.DamageBrokenCutInTargetIds);
        var targets = new List<BattleParticipantPresentationSnapshot>();
        for (int i = 0; i < request.Targets.Count; i++)
        {
            BattleParticipantPresentationSnapshot target = request.Targets[i];
            if (target != null && eligibleIds.Contains(target.RuntimeId))
                targets.Add(target);
        }
        return presenter.Play(targets);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
