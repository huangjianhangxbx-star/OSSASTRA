using UnityEngine;

[CreateAssetMenu(fileName = "BattleBackgroundCue", menuName = "ANKALUTE/DRPG/Presentation Cues/Background")]
public sealed class BattleBackgroundCueSO : BattlePresentationCueSO
{
    [SerializeField] private BattleBackgroundPresentationPolicy policy = BattleBackgroundPresentationPolicy.Dim;
    [SerializeField, Range(0f, 1f)] private float intensity = 0.55f;
    public override BattlePresentationCueKind Kind => BattlePresentationCueKind.Background;
    public BattleBackgroundPresentationPolicy Policy => policy;
    public float Intensity => Mathf.Clamp01(intensity);
}
