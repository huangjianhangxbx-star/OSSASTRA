using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleDamageBrokenCutInCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Break Cut-In")]
public sealed class BattleDamageBrokenCutInCueSO : BattlePresentationCueSO
{
    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.DamageBrokenCutIn;
}
