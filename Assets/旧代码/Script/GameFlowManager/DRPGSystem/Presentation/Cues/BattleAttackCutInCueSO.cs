using UnityEngine;

public enum BattleAttackCutInStage
{
    Open = 0,
    Impact = 1,
    Close = 2
}

[CreateAssetMenu(
    fileName = "BattleAttackCutInCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Attack Cut-In")]
public sealed class BattleAttackCutInCueSO : BattlePresentationCueSO
{
    [SerializeField] private BattleAttackCutInStage stage;

    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.AttackCutIn;
    public BattleAttackCutInStage Stage => stage;
}
