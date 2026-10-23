using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One draggable field-recovery item. Domain ownership is committed only when
/// the pointer is released over the backpack drop zone (or the entry is clicked).
/// </summary>
public sealed class BattleRecoveryItemView : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text kindLabel;
    [SerializeField] private CanvasGroup canvasGroup;

    private RectTransform rect;
    private RectTransform dropZone;
    private RectTransform dragLayer;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPosition;
    private Action collect;
    private bool dragging;

    public void Bind(
        string displayName,
        string kind,
        Sprite sprite,
        RectTransform targetDropZone,
        RectTransform targetDragLayer,
        Action collectAction)
    {
        rect ??= transform as RectTransform;
        canvasGroup ??= GetComponent<CanvasGroup>();
        dropZone = targetDropZone;
        dragLayer = targetDragLayer;
        collect = collectAction;
        label?.SetText(displayName ?? "UNKNOWN");
        kindLabel?.SetText(kind ?? string.Empty);
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!dragging && eventData.button == PointerEventData.InputButton.Left)
            collect?.Invoke();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rect == null || dragLayer == null)
            return;
        dragging = true;
        originalParent = rect.parent;
        originalSiblingIndex = rect.GetSiblingIndex();
        originalAnchoredPosition = rect.anchoredPosition;
        rect.SetParent(dragLayer, true);
        rect.SetAsLastSibling();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.86f;
            canvasGroup.blocksRaycasts = false;
        }
        rect.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging && rect != null)
            rect.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;
        bool accepted = dropZone != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                dropZone,
                eventData.position,
                eventData.pressEventCamera);
        RestoreLayout();
        dragging = false;
        if (accepted)
            collect?.Invoke();
    }

    private void RestoreLayout()
    {
        if (rect != null && originalParent != null)
        {
            rect.SetParent(originalParent, false);
            rect.SetSiblingIndex(originalSiblingIndex);
            rect.anchoredPosition = originalAnchoredPosition;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void OnDisable()
    {
        if (dragging)
            RestoreLayout();
        dragging = false;
    }
}
