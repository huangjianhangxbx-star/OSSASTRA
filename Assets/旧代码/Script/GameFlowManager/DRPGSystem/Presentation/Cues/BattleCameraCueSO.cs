using UnityEngine;

[CreateAssetMenu(fileName = "BattleCameraCue", menuName = "ANKALUTE/DRPG/Presentation Cues/Camera")]
public sealed class BattleCameraCueSO : BattlePresentationCueSO
{
    [SerializeField] private BattleCameraShotDefinition shot;
    public override BattlePresentationCueKind Kind => BattlePresentationCueKind.Camera;
    public BattleCameraShotDefinition Shot => shot;
}
