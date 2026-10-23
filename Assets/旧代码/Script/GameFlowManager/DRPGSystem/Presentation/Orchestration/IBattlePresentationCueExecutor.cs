public interface IBattlePresentationCueExecutor
{
    BattlePresentationCueKind Kind { get; }
    IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request);
}
