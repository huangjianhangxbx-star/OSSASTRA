using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Binds a weapon slot visual to data. Shows weapon icon or empty state.
/// Implements drag/drop interfaces to forward interactions to PackageUIController.
/// </summary>
public class WeaponSlotView : MonoBehaviour,
    IPointerDownHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private Image selectionFrame;
    [SerializeField] private Image activeHighlight;

    public int SlotIndex { get; private set; }

    private PackageUIController _controller;
    private bool _draggedThisFrame;
    private bool _dragFirstMove = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (iconImage == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();
        }
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "null";
        var sb = new System.Text.StringBuilder(t.name);
        var p = t.parent;
        while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
    }

    public void Initialize(int index, PackageUIController controller)
    {
        ResolveReferences();
        SlotIndex = index;
        _controller = controller;
        SetSelected(false);
        SetActive(false);
        if (slotLabel != null) slotLabel.text = (index + 1).ToString();
        Clear();

        var slotImage = GetComponent<Image>();
        if (slotImage != null) slotImage.raycastTarget = true;
        if (iconImage != null) iconImage.raycastTarget = false;
        if (selectionFrame != null) selectionFrame.raycastTarget = false;
        if (activeHighlight != null) activeHighlight.raycastTarget = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnPointerDown weaponSlot={SlotIndex} path={GetPath(transform)} pointerPress={eventData.pointerPress} button={eventData.button}");
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnInitializePotentialDrag weaponSlot={SlotIndex} path={GetPath(transform)}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        var item = _controller.GetWeaponSlotItem(SlotIndex);
        if (item == null) { Debug.Log($"[Drag] OnBeginDrag ABORT weaponSlot={SlotIndex} item is null"); return; }

        _draggedThisFrame = true;
        _dragFirstMove = true;
        _controller.BeginDragFromWeaponSlot(SlotIndex);
        Debug.Log($"[Drag] OnBeginDrag weaponSlot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragFirstMove)
        {
            Debug.Log($"[Drag] OnDrag (first) weaponSlot={SlotIndex} path={GetPath(transform)} raycast={eventData.pointerCurrentRaycast.gameObject}");
            _dragFirstMove = false;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnDrop TARGET weaponSlot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag} raycast={eventData.pointerCurrentRaycast.gameObject}");

        if (_controller == null) return;
        if (!_controller.IsDragging) { Debug.Log("[Drag] OnDrop but controller not dragging"); return; }

        _controller.HandleDropOnWeaponSlot(SlotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[Drag] OnEndDrag weaponSlot={SlotIndex} path={GetPath(transform)} pointerDrag={eventData.pointerDrag}");
        if (_controller != null && _controller.IsDragging)
            _controller.EndDrag(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_draggedThisFrame) { _draggedThisFrame = false; return; }
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_controller == null) return;

        _controller.TrySelectionEquip(SlotIndex);
        _controller.SetActiveWeapon(SlotIndex);
    }

    // --- Data binding ---

    public void Refresh(ItemInstance item)
    {
        if (item == null || item.definition == null) { Clear(); return; }
        ResolveReferences();
        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.enabled = item.Icon != null;
        }
    }

    public void Clear() { if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; } }
    public void SetSelected(bool selected) { if (selectionFrame != null) selectionFrame.gameObject.SetActive(selected); }
    public void SetActive(bool active) { if (activeHighlight != null) activeHighlight.gameObject.SetActive(active); }
}
