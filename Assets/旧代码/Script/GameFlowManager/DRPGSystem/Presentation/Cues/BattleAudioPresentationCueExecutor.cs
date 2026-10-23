using UnityEngine;

public sealed class BattleAudioPresentationCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    [SerializeField] private MonoBehaviour audioCueSinkComponent;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.Audio;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        IBattleAudioCueSink sink = audioCueSinkComponent as IBattleAudioCueSink;
        if (sink == null || request == null ||
            !(cue is BattleAudioPresentationCueSO audioCue))
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        int actorRuntimeId = request.Actor != null
            ? request.Actor.RuntimeId
            : 0;
        int targetRuntimeId = request.Targets.Count > 0 &&
            request.Targets[0] != null
                ? request.Targets[0].RuntimeId
                : 0;
        sink.Play(new BattleAudioCueRequest(
            audioCue.AudioCue,
            actorRuntimeId,
            targetRuntimeId,
            request.SkillPresentationKey,
            audioCue.SemanticCueId));
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
