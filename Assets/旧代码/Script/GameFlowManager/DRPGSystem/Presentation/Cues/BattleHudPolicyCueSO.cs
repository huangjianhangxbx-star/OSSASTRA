using UnityEngine;

[CreateAssetMenu(fileName = "BattleHudPolicyCue", menuName = "ANKALUTE/DRPG/Presentation Cues/HUD Policy")]
public sealed class BattleHudPolicyCueSO : BattlePresentationCueSO
{
    [SerializeField] private BattleHudVisibilityPolicy policy = BattleHudVisibilityPolicy.HideAll;
    public override BattlePresentationCueKind Kind => BattlePresentationCueKind.HudPolicy;
    public BattleHudVisibilityPolicy Policy => policy;
}
