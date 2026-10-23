using UnityEngine;

public sealed class BattleStageCueExecutor : MonoBehaviour, IBattlePresentationCueExecutor
{
    [SerializeField] private BattleConflictPresentationContext context;
    public BattlePresentationCueKind Kind => BattlePresentationCueKind.Stage;
    public void Configure(BattleConflictPresentationContext value) => context = value;
    public IBattlePresentationHandle Play(BattlePresentationCueSO cue, BattlePresentationRequest request)
    {
        if (context == null || !(cue is BattleStageCueSO typed) || request == null)
            return Settled(BattlePresentationOutcome.Skipped);
        context.SwitchStage(request, typed.StageProfile);
        return Settled(BattlePresentationOutcome.Completed);
    }
    private static IBattlePresentationHandle Settled(BattlePresentationOutcome outcome)
    { var handle = new BattlePresentationHandle(); handle.TrySettle(outcome); return handle; }
}
