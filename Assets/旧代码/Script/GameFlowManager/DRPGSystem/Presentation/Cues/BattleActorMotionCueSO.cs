using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleActorMotionCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Actor Motion")]
public sealed class BattleActorMotionCueSO : BattlePresentationCueSO
{
    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.ActorMotion;
}
