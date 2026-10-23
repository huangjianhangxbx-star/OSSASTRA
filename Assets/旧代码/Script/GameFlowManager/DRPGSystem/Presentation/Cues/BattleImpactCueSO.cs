using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleImpactCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Impact")]
public sealed class BattleImpactCueSO : BattlePresentationCueSO
{
    [Min(0.1f)]
    [SerializeField] private float holdSeconds = 1f;

    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.ImpactFeedback;
    public float HoldSeconds => Mathf.Max(0.1f, holdSeconds);
}
