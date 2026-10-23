using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pure uGUI view for one logical inventory slot. It never mutates inventory
/// state directly; all writes are delegated to the controller's transaction.
/// </summary>
public sealed class DRPGBackpackSlotView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    public const float DefaultCellSize = 100f;

    private DRPGPartyBackpackController controller;
    private IDRPGInventoryContainer container;
    private int slotIndex;
    private Image background;
    private Image icon;
    private TMP_Text quantity;
    private TMP_Text indexLabel;
    private Outline hoverOutline;

    public IDRPGInventoryContainer Container => container;
    public int SlotIndex => slotIndex;
    public DRPGInventoryItemStack Item =>
        container != null ? container.GetSlot(slotIndex).Item : null;

    public void Initialize(
        DRPGPartyBackpackController owner,
        IDRPGInventoryContainer source,
        int index,
        TMP_FontAsset font)
    {
        controller = owner;
        container = source;
        slotIndex = index;
        BuildIfNeeded(font);
        Refresh();
    }

    public void Refresh()
    {
        DRPGInventoryItemStack item = Item;
        bool occupied = item != null;
        icon.enabled = occupied && item.Icon != null;
        icon.sprite = occupied ? item.Icon : null;
        quantity.text = occupied && item.Quantity > 1
            ? item.Quantity.ToString()
            : string.Empty;
        indexLabel.text = (slotIndex + 1).ToString("00");
        background.color = occupied
            ? new Color(0.035f, 0.20f, 0.24f, 0.98f)
            : new Color(0.015f, 0.075f, 0.09f, 0.96f);
    }

    public void SetHovered(bool value)
    {
        hoverOutline.enabled = value;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
        controller?.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
        controller?.HideTooltip(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            controller?.BeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.ContinueDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        controller?.EndDrag(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        controller?.DropOn(this);
    }

    private void BuildIfNeeded(TMP_FontAsset font)
    {
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(DefaultCellSize, DefaultCellSize);

        background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.raycastTarget = true;

        hoverOutline = GetComponent<Outline>();
        if (hoverOutline == null)
            hoverOutline = gameObject.AddComponent<Outline>();
        hoverOutline.effectColor = new Color(0.15f, 0.95f, 1f, 1f);
        hoverOutline.effectDistance = new Vector2(3f, -3f);
        hoverOutline.enabled = false;

        GameObject iconObject = CreateChild("Icon");
        icon = iconObject.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        SetOffsets(icon.rectTransform, 10f, 10f, 10f, 10f);

        GameObject indexObject = CreateChild("Index");
        indexLabel = indexObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(indexLabel, font, 15f, new Color(0.35f, 0.75f, 0.8f, 0.72f));
        indexLabel.alignment = TextAlignmentOptions.TopLeft;
        SetOffsets(indexLabel.rectTransform, 5f, 5f, 5f, 5f);

        GameObject quantityObject = CreateChild("Quantity");
        quantity = quantityObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(quantity, font, 22f, Color.white);
        quantity.alignment = TextAlignmentOptions.BottomRight;
        quantity.fontStyle = FontStyles.Bold;
        SetOffsets(quantity.rectTransform, 5f, 5f, 5f, 5f);
    }

    private GameObject CreateChild(string childName)
    {
        var child = new GameObject(childName, typeof(RectTransform));
        child.transform.SetParent(transform, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        return child;
    }

    private static void ConfigureText(
        TMP_Text text,
        TMP_FontAsset font,
        float size,
        Color color)
    {
        text.text = string.Empty;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        if (font != null)
            text.font = font;
    }

    private static void SetOffsets(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float top)
    {
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
