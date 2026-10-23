using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleAudioPresentationCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Audio")]
public sealed class BattleAudioPresentationCueSO : BattlePresentationCueSO
{
    [SerializeField] private BattleAudioCue audioCue = BattleAudioCue.Attack;
    [SerializeField] private string semanticCueId = string.Empty;

    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.Audio;
    public BattleAudioCue AudioCue => audioCue;
    public string SemanticCueId => semanticCueId ?? string.Empty;
}
