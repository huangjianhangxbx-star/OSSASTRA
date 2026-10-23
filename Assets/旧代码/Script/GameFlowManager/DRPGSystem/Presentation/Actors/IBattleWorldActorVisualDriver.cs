public interface IBattleWorldActorVisualDriver
{
    void Bind(DRPGBattleParticipantViewModel actor);
    IBattlePresentationHandle PlayIntent(EAnimationLogicState intent);
    void RestoreVisual();
    void ResetVisual();
}
