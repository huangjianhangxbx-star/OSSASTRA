using UnityEngine;

public sealed class BattleConflictRestoreCueExecutor : MonoBehaviour, IBattlePresentationCueExecutor
{
    [SerializeField] private BattleConflictPresentationContext context;
    public BattlePresentationCueKind Kind => BattlePresentationCueKind.ConflictRestore;
    public void Configure(BattleConflictPresentationContext value) => context = value;
    public IBattlePresentationHandle Play(BattlePresentationCueSO cue, BattlePresentationRequest request)
    {
        if (request != null && request.IsStandardBasicAttackLeaf)
            return Settled(BattlePresentationOutcome.Skipped);
        if (context == null || !(cue is BattleConflictRestoreCueSO) || request == null)
            return Settled(BattlePresentationOutcome.Skipped);
        context.Restore(request);
        return Settled(BattlePresentationOutcome.Completed);
    }
    private static IBattlePresentationHandle Settled(BattlePresentationOutcome outcome)
    { var handle = new BattlePresentationHandle(); handle.TrySettle(outcome); return handle; }
}
