using UnityEngine;

public interface IDRPGBattleTargetFeedbackSurface
{
    void ApplyTargetFeedback(
        int runtimeId,
        DRPGBattleTargetFeedback feedback);
    void ClearTargetFeedback();
}

public sealed class BattleTargetFeedbackCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private MonoBehaviour surfaceComponent;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.TargetFeedback;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        IDRPGBattleTargetFeedbackSurface surface =
            surfaceComponent as IDRPGBattleTargetFeedbackSurface;
        if (surface == null || request == null ||
            !(cue is BattleTargetFeedbackCueSO feedbackCue))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        if (feedbackCue.Feedback == DRPGBattleTargetFeedback.None)
        {
            surface.ClearTargetFeedback();
        }
        else
        {
            for (int i = 0; i < request.Targets.Count; i++)
            {
                BattleParticipantPresentationSnapshot target = request.Targets[i];
                if (target != null)
                {
                    surface.ApplyTargetFeedback(
                        target.RuntimeId,
                        feedbackCue.Feedback);
                }
            }
        }

        return Settled(BattlePresentationOutcome.Completed);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
