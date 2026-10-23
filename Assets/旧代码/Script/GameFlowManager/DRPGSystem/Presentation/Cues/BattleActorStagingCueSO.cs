using UnityEngine;

[CreateAssetMenu(fileName = "BattleActorStagingCue", menuName = "ANKALUTE/DRPG/Presentation Cues/Actor Staging")]
public sealed class BattleActorStagingCueSO : BattlePresentationCueSO
{
    [SerializeField, Min(0.01f)] private float attackerScale = 1.05f;
    [SerializeField, Min(0.01f)] private float targetScale = 1.08f;
    [SerializeField] private float targetRetreatDistance = 0.35f;
    [SerializeField] private BattleOtherTargetPresentationPolicy otherActors = BattleOtherTargetPresentationPolicy.Dim;
    public override BattlePresentationCueKind Kind => BattlePresentationCueKind.ActorStaging;
    public float AttackerScale => Mathf.Max(0.01f, attackerScale);
    public float TargetScale => Mathf.Max(0.01f, targetScale);
    public float TargetRetreatDistance => targetRetreatDistance;
    public BattleOtherTargetPresentationPolicy OtherActors => otherActors;
}
