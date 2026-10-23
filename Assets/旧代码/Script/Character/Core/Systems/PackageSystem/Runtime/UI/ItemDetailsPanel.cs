using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple item details panel in the center area.
/// Shows basic info from the selected ItemInstance.
/// </summary>
public class ItemDetailsPanel : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private GameObject emptyStateText;

    private void Awake()
    {
        // Don't block raycasts — center panel must allow drag passthrough
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
        if (iconImage != null) iconImage.raycastTarget = false;
    }

    public void ShowItem(ItemInstance item)
    {
        if (item == null || item.definition == null)
        {
            Clear();
            return;
        }

        if (emptyStateText != null)
            emptyStateText.SetActive(false);

        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = item.Icon != null;
        }

        if (nameText != null)
            nameText.text = item.DisplayName;

        if (categoryText != null)
            categoryText.text = item.Category.ToString();

        if (quantityText != null)
            quantityText.text = $"x{item.quantity}";
    }

    public void Clear()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (nameText != null) nameText.text = "";
        if (categoryText != null) categoryText.text = "";
        if (quantityText != null) quantityText.text = "";
        if (emptyStateText != null) emptyStateText.SetActive(true);
    }
}
