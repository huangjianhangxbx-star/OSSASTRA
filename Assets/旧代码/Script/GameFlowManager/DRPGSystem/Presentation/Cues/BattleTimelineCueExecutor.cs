using UnityEngine;

public sealed class BattleTimelineCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private BattleTimelineView timelineView;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.TimelineTransition;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (timelineView == null || request == null ||
            !(cue is BattleTimelineCueSO timelineCue) ||
            request.TimelineRuntimeIds.Count == 0)
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        return timelineView.PlayTransition(
            request.TimelineRuntimeIds,
            timelineCue.DurationSeconds);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
