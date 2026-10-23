using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Binds a single inventory slot's visual elements to data.
/// Implements drag/drop interfaces to forward interactions to PackageUIController.
/// Does NOT own item data — only displays and forwards events.
/// </summary>
public class BackpackSlotView : MonoBehaviour,
    IPointerDownHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image selectionFrame;
    [SerializeField] private Image blockedOverlay;

    public int SlotIndex { get; private set; }

    private PackageUIController _controller;
    private bool _draggedThisFrame;
    private bool _dragFirstMove = true;

    private static string GetPath(Transform t)
    {
        if (t == null) return "null";
        var sb = new System.Text.StringBuilder(t.name);
        var p = t.parent;
        while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
    }

    private void Awake()
    {
        if (iconImage == null) { var t = transform.Find("Icon"); if (t != null) iconImage = t.GetComponent<Image>(); }
        if (quantityText == null) { var t = transform.Find("Qty"); if (t != null) quantityText = t.GetComponent<TextMeshProUGUI>(); }
        if (selectionFrame == null) { var t = transform.Find("Selection"); if (t != null) selectionFrame = t.GetComponent<Image>(); }
        if (blockedOverlay == null) { var t = transform.Find("Blocked"); if (t != null) blockedOverlay = t.GetComponent<Image>(); }

        var slotImage = GetComponent<Image>();
        if (slotImage != null) slotImage.raycastTarget = true;
        if (iconImage != null) iconImage.raycastTarget = false;
        if (selectionFrame != null) selectionFrame.raycastTarget = false;
        if (blockedOverlay != null) blockedOverlay.raycastTarget = false;
    }

    public void Initialize(int index, PackageUIController controller)
    {
        SlotIndex = index;
        _controller = controller;
        SetSelected(false);
        SetBlocked(false);
        Clear();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnPointerDown slot={SlotIndex} path={GetPath(transform)} pointerPress={eventData.pointerPress} button={eventData.button}");
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnInitializePotentialDrag slot={SlotIndex} path={GetPath(transform)}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        var item = _controller.GetBackpackItem(SlotIndex);
        if (item == null) { Debug.Log($"[Drag] OnBeginDrag ABORT slot={SlotIndex} item is null"); return; }

        _draggedThisFrame = true;
        _dragFirstMove = true;
        _controller.BeginDragFromBackpack(SlotIndex);
        Debug.Log($"[Drag] OnBeginDrag slot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragFirstMove)
        {
            Debug.Log($"[Drag] OnDrag (first) slot={SlotIndex} path={GetPath(transform)} raycast={eventData.pointerCurrentRaycast.gameObject}");
            _dragFirstMove = false;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnDrop TARGET slot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");

        if (_controller == null) return;
        if (!_controller.IsDragging) { Debug.Log("[Drag] OnDrop but controller not dragging"); return; }

        // Use controller's saved drag state instead of GetComponent
        _controller.HandleDropOnBackpackSlot(SlotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnEndDrag slot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag}");
        // Cleanup is handled by controller after drop, or as fallback here
        if (_controller != null && _controller.IsDragging)
            _controller.EndDrag(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_draggedThisFrame)
        {
            _draggedThisFrame = false;
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        var item = _controller.GetBackpackItem(SlotIndex);
        if (item != null)
            _controller.ToggleBackpackSelection(SlotIndex);
    }

    // --- Data binding ---

    public void Refresh(ItemInstance item)
    {
        if (item == null || item.definition == null) { Clear(); return; }
        if (iconImage != null) { iconImage.sprite = item.Icon; iconImage.enabled = item.Icon != null; iconImage.color = Color.white; }
        if (quantityText != null) { quantityText.text = item.quantity > 1 ? item.quantity.ToString() : ""; quantityText.gameObject.SetActive(item.quantity > 1); }
    }

    public void Clear()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (quantityText != null) { quantityText.text = ""; quantityText.gameObject.SetActive(false); }
    }

    public void SetSelected(bool selected) { if (selectionFrame != null) selectionFrame.gameObject.SetActive(selected); }
    public void SetBlocked(bool blocked) { if (blockedOverlay != null) blockedOverlay.gameObject.SetActive(blocked); }
}
