using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleTargetFeedbackCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Target Feedback")]
public sealed class BattleTargetFeedbackCueSO : BattlePresentationCueSO
{
    [SerializeField] private DRPGBattleTargetFeedback feedback =
        DRPGBattleTargetFeedback.Selected;

    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.TargetFeedback;
    public DRPGBattleTargetFeedback Feedback => feedback;
}
