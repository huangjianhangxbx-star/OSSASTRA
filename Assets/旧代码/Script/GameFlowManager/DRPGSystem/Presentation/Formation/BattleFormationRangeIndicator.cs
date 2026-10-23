using UnityEngine;
using UnityEngine.UI;

public sealed class BattleFormationRangeIndicator : MonoBehaviour
{
    [SerializeField] private Image frontChip;
    [SerializeField] private Image middleChip;
    [SerializeField] private Image rearChip;
    [SerializeField] private Color unavailableColor =
        new Color(0.16f, 0.17f, 0.21f, 0.72f);

    public void Bind(DRPGFormationRowMask mask)
    {
        Apply(frontChip, mask.Contains(DRPGFormationRow.Front),
            DRPGFormationRow.Front);
        Apply(middleChip, mask.Contains(DRPGFormationRow.Middle),
            DRPGFormationRow.Middle);
        Apply(rearChip, mask.Contains(DRPGFormationRow.Rear),
            DRPGFormationRow.Rear);
    }

    private void Apply(Image image, bool available, DRPGFormationRow row)
    {
        if (image != null)
            image.color = available
                ? DRPGFormationColorPalette.Resolve(row)
                : unavailableColor;
    }
}
