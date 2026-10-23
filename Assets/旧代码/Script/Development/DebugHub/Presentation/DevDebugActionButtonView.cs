using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable presentation-only view for one development debug action.
/// It never owns or executes gameplay logic.
/// </summary>
public sealed class DevDebugActionButtonView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Image stateRail;
    [SerializeField] private CanvasGroup canvasGroup;

    private IDevDebugAction action;
    private Action<IDevDebugAction> selected;
    private Action<IDevDebugAction> hovered;
    private Action<IDevDebugAction> hoverEnded;

    public IDevDebugAction Action => action;

    public void Bind(
        IDevDebugAction value,
        DevDebugAvailability availability,
        Action<IDevDebugAction> onSelected,
        Action<IDevDebugAction> onHovered,
        Action<IDevDebugAction> onHoverEnded)
    {
        ResolveReferences();
        action = value;
        selected = onSelected;
        hovered = onHovered;
        hoverEnded = onHoverEnded;

        if (titleText != null)
            titleText.SetText(value != null ? value.Title : "UNBOUND");
        if (summaryText != null)
            summaryText.SetText(value != null ? value.Summary : string.Empty);

        bool available = value != null && availability.IsAvailable;
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            button.interactable = available;
        }

        if (stateText != null)
        {
            stateText.SetText(available ? "READY" : "LOCKED");
            stateText.color = available
                ? new Color(0.25f, 0.94f, 1f, 1f)
                : new Color(1f, 0.55f, 0.18f, 1f);
        }

        if (stateRail != null)
        {
            stateRail.color = available
                ? new Color(0.05f, 0.82f, 0.92f, 1f)
                : new Color(0.95f, 0.35f, 0.10f, 1f);
        }

        if (canvasGroup != null)
            canvasGroup.alpha = available ? 1f : 0.58f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (action != null)
            hovered?.Invoke(action);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (action != null)
            hoverEnded?.Invoke(action);
    }

    private void HandleClick()
    {
        if (action != null)
            selected?.Invoke(action);
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (titleText == null)
            titleText = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (summaryText == null)
            summaryText = transform.Find("Summary")?.GetComponent<TMP_Text>();
        if (stateText == null)
            stateText = transform.Find("State")?.GetComponent<TMP_Text>();
        if (stateRail == null)
            stateRail = transform.Find("StateRail")?.GetComponent<Image>();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }
}
