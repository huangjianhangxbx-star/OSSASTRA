using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DRPGInventoryDropZone : MonoBehaviour,
    IDropHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private UnityEngine.UI.Graphic highlight;
    [SerializeField] private Color idleColor = new Color(0.04f, 0.1f, 0.14f, 0.9f);
    [SerializeField] private Color hoverColor = new Color(0.08f, 0.65f, 0.76f, 0.72f);

    public void OnDrop(PointerEventData eventData)
    {
        DRPGLootSlotView dragged = DRPGLootSlotView.ActiveDrag;
        if (dragged != null && dragged.IsSettlementSource)
            dragged.TransferEntireStack();
        SetHighlight(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (DRPGLootSlotView.ActiveDrag != null)
            SetHighlight(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlight(false);
    }

    private void SetHighlight(bool active)
    {
        if (highlight == null)
            highlight = GetComponent<UnityEngine.UI.Graphic>();
        if (highlight != null)
            highlight.color = active ? hoverColor : idleColor;
    }
}
