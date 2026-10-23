public interface IBattlePresentationDirector
{
    BattlePresentationRun Play(BattlePresentationRequest request);
    void CancelScope(
        BattlePresentationScope scope,
        BattlePresentationCancelReason reason);
    BattleInputGate InputGate { get; }
}
