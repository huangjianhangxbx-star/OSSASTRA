using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DRPGLootSlotView : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text weightText;
    [SerializeField] private CanvasGroup canvasGroup;

    private DRPGGameLoopRuntimeRoot owner;
    private DRPGLootStackState stack;
    private bool isSettlementSource;
    private RectTransform dragGhost;

    public static DRPGLootSlotView ActiveDrag { get; private set; }
    public DRPGLootStackState Stack => stack;
    public bool IsSettlementSource => isSettlementSource;

    public void Bind(
        DRPGGameLoopRuntimeRoot gameLoop,
        DRPGLootStackState value,
        bool settlementSource)
    {
        owner = gameLoop;
        stack = value;
        isSettlementSource = settlementSource;
        if (icon != null)
        {
            icon.sprite = value?.Definition != null
                ? value.Definition.Icon
                : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }
        displayName?.SetText(value?.Definition != null
            ? value.Definition.DisplayName
            : "空槽");
        quantityText?.SetText(value != null ? "×" + value.Quantity : string.Empty);
        weightText?.SetText(value?.Definition != null
            ? (value.Definition.Weight * value.Quantity).ToString("0.0") + " kg"
            : string.Empty);
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isSettlementSource || stack == null || owner == null)
            return;
        ActiveDrag = this;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.5f;
            canvasGroup.blocksRaycasts = false;
        }
        CreateDragGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ActiveDrag == this && dragGhost != null)
            dragGhost.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        if (dragGhost != null)
            Destroy(dragGhost.gameObject);
        dragGhost = null;
        if (ActiveDrag == this)
            ActiveDrag = null;
    }

    public void TransferEntireStack()
    {
        if (isSettlementSource && owner != null && stack != null)
        {
            owner.TransferLoot(stack.Definition.ItemId, stack.Quantity);
        }
    }

    private void CreateDragGhost(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        var ghost = new GameObject(
            "LootDragGhost",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));
        dragGhost = ghost.GetComponent<RectTransform>();
        dragGhost.SetParent(canvas.transform, false);
        dragGhost.SetAsLastSibling();
        dragGhost.sizeDelta = new Vector2(88f, 88f);
        dragGhost.position = eventData.position;
        UnityEngine.UI.Image image = ghost.GetComponent<UnityEngine.UI.Image>();
        image.sprite = stack.Definition.Icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = image.sprite != null
            ? Color.white
            : new Color(0.1f, 0.9f, 1f, 0.85f);
    }
}
