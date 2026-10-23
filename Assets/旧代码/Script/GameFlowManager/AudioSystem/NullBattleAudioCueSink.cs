public sealed class NullBattleAudioCueSink : IBattleAudioCueSink
{
    public static NullBattleAudioCueSink Instance { get; } =
        new NullBattleAudioCueSink();

    private NullBattleAudioCueSink()
    {
    }

    public void Begin(DRPGBattleAudioPaletteSO palette)
    {
    }

    public void Play(BattleAudioCueRequest request)
    {
    }

    public void End()
    {
    }
}
