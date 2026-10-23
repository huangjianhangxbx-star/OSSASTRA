using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleTimelineCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Timeline Transition")]
public sealed class BattleTimelineCueSO : BattlePresentationCueSO
{
    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.TimelineTransition;
}
