using UnityEngine;

[DisallowMultipleComponent]
public sealed class DRPGBattleAudioCueSink : MonoBehaviour, IBattleAudioCueSink
{
    [SerializeField] private GameAudioRoot audioRoot;
    [SerializeField, Min(0f)] private float musicFadeSeconds = 0.45f;

    private DRPGBattleAudioPaletteSO palette;

    public void Begin(DRPGBattleAudioPaletteSO battlePalette)
    {
        palette = battlePalette;
        ResolveAudioRoot();
    }

    public void Play(BattleAudioCueRequest request)
    {
        if (palette == null || !palette.TryResolve(
                request,
                out AudioEventSO audioEvent,
                out BattleAudioPlaybackChannel channel))
        {
            return;
        }

        GameAudioRoot root = ResolveAudioRoot();
        if (root == null)
            return;

        switch (channel)
        {
            case BattleAudioPlaybackChannel.Music:
                root.PlayMusic(audioEvent, musicFadeSeconds);
                break;

            case BattleAudioPlaybackChannel.Ui:
                root.PlayUi(audioEvent);
                break;

            case BattleAudioPlaybackChannel.Voice:
                root.PlayVoice(audioEvent);
                break;

            default:
                root.PlaySfx(audioEvent, transform.position);
                break;
        }
    }

    public void End()
    {
        GameAudioRoot root = ResolveAudioRoot();
        if (root != null)
            root.StopMusic(musicFadeSeconds);
        palette = null;
    }

    private GameAudioRoot ResolveAudioRoot()
    {
        if (audioRoot != null)
            return audioRoot;

        if (GameFlowRoot.Instance != null)
            audioRoot = GameFlowRoot.Instance.audioRoot;
        if (audioRoot == null)
            audioRoot = FindFirstObjectByType<GameAudioRoot>();
        return audioRoot;
    }
}
