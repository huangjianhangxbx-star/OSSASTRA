using UnityEngine;

[CreateAssetMenu(fileName = "BattleStageCue", menuName = "ANKALUTE/DRPG/Presentation Cues/Stage")]
public sealed class BattleStageCueSO : BattlePresentationCueSO
{
    [SerializeField] private BattleStageProfileSO stageProfile;
    public override BattlePresentationCueKind Kind => BattlePresentationCueKind.Stage;
    public BattleStageProfileSO StageProfile => stageProfile;
}
