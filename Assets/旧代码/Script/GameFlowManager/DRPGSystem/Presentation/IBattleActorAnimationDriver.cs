public interface IBattleActorAnimationDriver
{
    void BindActor(DRPGCombatantProfileSO profile);
    IBattlePresentationHandle PlayIntent(EAnimationLogicState animationIntent);
    void SetLowHpState(DRPGLowHpPresentationState state);
}
