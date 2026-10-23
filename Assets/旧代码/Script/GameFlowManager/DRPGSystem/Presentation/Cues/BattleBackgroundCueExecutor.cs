using UnityEngine;

public sealed class BattleBackgroundCueExecutor : MonoBehaviour, IBattlePresentationCueExecutor
{
    [SerializeField] private BattleConflictPresentationContext context;
    public BattlePresentationCueKind Kind => BattlePresentationCueKind.Background;
    public void Configure(BattleConflictPresentationContext value) => context = value;
    public IBattlePresentationHandle Play(BattlePresentationCueSO cue, BattlePresentationRequest request)
    {
        if (context == null || !(cue is BattleBackgroundCueSO typed) || request == null)
            return Settled(BattlePresentationOutcome.Skipped);
        context.ApplyBackground(request, typed.Policy, typed.Intensity);
        return Settled(BattlePresentationOutcome.Completed);
    }
    private static IBattlePresentationHandle Settled(BattlePresentationOutcome outcome)
    { var handle = new BattlePresentationHandle(); handle.TrySettle(outcome); return handle; }
}
