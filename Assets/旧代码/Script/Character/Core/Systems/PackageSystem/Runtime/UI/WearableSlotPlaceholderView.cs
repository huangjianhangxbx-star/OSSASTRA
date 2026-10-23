using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Placeholder view for wearable equipment slots that are not yet implemented.
/// Displays a locked/disabled state and does NOT accept drag/drop.
/// </summary>
public class WearableSlotPlaceholderView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image lockOverlay;

    public int SlotIndex { get; private set; }

    public void Initialize(int index, string displayLabel)
    {
        SlotIndex = index;
        if (label != null)
            label.text = displayLabel;

        // Ensure no raycast interception — these slots can't receive drops
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
        if (icon != null) icon.raycastTarget = false;
        if (lockOverlay != null) lockOverlay.raycastTarget = false;
        if (label != null) label.raycastTarget = false;
    }
}
