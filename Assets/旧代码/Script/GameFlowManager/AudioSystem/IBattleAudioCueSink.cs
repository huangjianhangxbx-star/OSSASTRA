public interface IBattleAudioCueSink
{
    void Begin(DRPGBattleAudioPaletteSO palette);
    void Play(BattleAudioCueRequest request);
    void End();
}
