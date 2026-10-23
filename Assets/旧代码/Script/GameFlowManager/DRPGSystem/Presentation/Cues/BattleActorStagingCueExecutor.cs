using UnityEngine;

public sealed class BattleActorStagingCueExecutor : MonoBehaviour, IBattlePresentationCueExecutor
{
    [SerializeField] private BattleConflictPresentationContext context;
    public BattlePresentationCueKind Kind => BattlePresentationCueKind.ActorStaging;
    public void Configure(BattleConflictPresentationContext value) => context = value;
    public IBattlePresentationHandle Play(BattlePresentationCueSO cue, BattlePresentationRequest request)
    {
        if (context == null || !(cue is BattleActorStagingCueSO typed) || request == null)
            return Settled(BattlePresentationOutcome.Skipped);
        context.ApplyActors(request, typed.AttackerScale, typed.TargetScale, typed.TargetRetreatDistance, typed.OtherActors);
        return Settled(BattlePresentationOutcome.Completed);
    }
    private static IBattlePresentationHandle Settled(BattlePresentationOutcome outcome)
    { var handle = new BattlePresentationHandle(); handle.TrySettle(outcome); return handle; }
}
