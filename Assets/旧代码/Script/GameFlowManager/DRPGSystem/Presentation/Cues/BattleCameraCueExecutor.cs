using UnityEngine;

public sealed class BattleCameraCueExecutor : MonoBehaviour, IBattlePresentationCueExecutor
{
    [SerializeField] private BattleConflictPresentationContext context;
    public BattlePresentationCueKind Kind => BattlePresentationCueKind.Camera;
    public void Configure(BattleConflictPresentationContext value) => context = value;
    public IBattlePresentationHandle Play(BattlePresentationCueSO cue, BattlePresentationRequest request)
    {
        if (context == null || !(cue is BattleCameraCueSO typed) || request == null || typed.Shot == null)
            return Settled(BattlePresentationOutcome.Skipped);
        context.PlayCamera(request, typed.Shot);
        return Settled(BattlePresentationOutcome.Completed);
    }
    private static IBattlePresentationHandle Settled(BattlePresentationOutcome outcome)
    { var handle = new BattlePresentationHandle(); handle.TrySettle(outcome); return handle; }
}
