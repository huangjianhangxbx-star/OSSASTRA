using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Data-driven UGUI shell for the development debug registry.
/// Runtime actions remain independent from this presentation layer.
/// </summary>
public sealed class DevDebugHubController : MonoBehaviour
{
    [Header("Shell")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform frame;
    [SerializeField] private Button closeButton;

    [Header("Category rail")]
    [SerializeField] private RectTransform categoryRoot;
    [SerializeField] private Button categoryButtonTemplate;

    [Header("Action list")]
    [SerializeField] private RectTransform actionRoot;
    [SerializeField] private DevDebugActionButtonView actionButtonTemplate;

    [Header("Details")]
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailSummaryText;
    [SerializeField] private TMP_Text detailBodyText;
    [SerializeField] private TMP_Text detailStateText;
    [SerializeField] private TMP_Text resultText;

    private readonly List<Button> categoryButtons = new List<Button>();
    private readonly List<DevDebugActionButtonView> actionButtons =
        new List<DevDebugActionButtonView>();
    private readonly List<string> categories = new List<string>();
    private DevDebugRegistry registry;
    private string selectedCategory;
    private IDevDebugAction selectedAction;
    private Sequence transition;
    private Vector2 frameRestPosition;
    private bool frameRestCaptured;
    private bool initialized;

    public bool IsOpen { get; private set; }

    public void Initialize(DevDebugRegistry owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        if (registry != null)
            registry.Changed -= Refresh;
        registry = owner;
        registry.Changed += Refresh;

        if (!initialized)
        {
            closeButton?.onClick.AddListener(RequestClose);
            CaptureRestPosition();
            initialized = true;
        }

        Refresh();
        HideImmediate();
    }

    public void Show()
    {
        if (registry == null)
            Initialize(DevDebugHubRuntime.Registry);

        gameObject.SetActive(true);
        IsOpen = true;
        Refresh();
        CaptureRestPosition();
        KillTransition();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        if (frame != null)
        {
            frame.anchoredPosition = frameRestPosition + new Vector2(0f, -28f);
            frame.localScale = new Vector3(0.986f, 0.986f, 1f);
        }

        transition = DOTween.Sequence().SetUpdate(true);
        if (canvasGroup != null)
            transition.Join(canvasGroup.DOFade(1f, 0.14f).SetEase(Ease.OutQuad));
        if (frame != null)
        {
            transition.Join(frame.DOAnchorPos(frameRestPosition, 0.19f)
                .SetEase(Ease.OutCubic));
            transition.Join(frame.DOScale(1f, 0.20f)
                .SetEase(Ease.OutBack, 1.06f));
        }
    }

    public void HideAnimated(Action completed)
    {
        if (!IsOpen)
        {
            completed?.Invoke();
            return;
        }

        IsOpen = false;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        KillTransition();
        transition = DOTween.Sequence().SetUpdate(true);
        if (canvasGroup != null)
            transition.Join(canvasGroup.DOFade(0f, 0.10f).SetEase(Ease.InQuad));
        if (frame != null)
        {
            transition.Join(frame.DOAnchorPos(
                    frameRestPosition + new Vector2(0f, -16f),
                    0.11f)
                .SetEase(Ease.InQuad));
            transition.Join(frame.DOScale(0.982f, 0.11f).SetEase(Ease.InQuad));
        }
        transition.OnComplete(() =>
        {
            gameObject.SetActive(false);
            completed?.Invoke();
        });
    }

    public void HideImmediate()
    {
        KillTransition();
        IsOpen = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (frame != null)
        {
            CaptureRestPosition();
            frame.anchoredPosition = frameRestPosition;
            frame.localScale = Vector3.one;
        }
        gameObject.SetActive(false);
    }

    public void PresentResult(DevDebugActionResult result)
    {
        if (resultText == null)
            return;

        string prefix;
        Color color;
        switch (result.Status)
        {
            case DevDebugActionStatus.Succeeded:
                prefix = "[PASS] ";
                color = new Color(0.25f, 0.94f, 1f, 1f);
                break;
            case DevDebugActionStatus.Unavailable:
                prefix = "[WAIT] ";
                color = new Color(1f, 0.68f, 0.18f, 1f);
                break;
            default:
                prefix = "[FAIL] ";
                color = new Color(1f, 0.30f, 0.28f, 1f);
                break;
        }

        resultText.color = color;
        resultText.SetText(prefix + result.Message);
    }

    private void Refresh()
    {
        if (registry == null || categoryRoot == null || actionRoot == null ||
            categoryButtonTemplate == null || actionButtonTemplate == null)
        {
            return;
        }

        IReadOnlyList<IDevDebugAction> snapshot = registry.Snapshot();
        RebuildCategories(snapshot);
        RebuildActions(snapshot);
    }

    private void RebuildCategories(IReadOnlyList<IDevDebugAction> actions)
    {
        DestroyRuntimeViews(categoryButtons);
        categories.Clear();
        for (int i = 0; i < actions.Count; i++)
        {
            string category = actions[i].Category;
            if (!categories.Contains(category))
                categories.Add(category);
        }

        if (categories.Count == 0)
            selectedCategory = null;
        else if (string.IsNullOrWhiteSpace(selectedCategory) ||
                 !categories.Contains(selectedCategory))
            selectedCategory = categories[0];

        for (int i = 0; i < categories.Count; i++)
        {
            string category = categories[i];
            Button button = Instantiate(categoryButtonTemplate, categoryRoot);
            button.gameObject.name = "Category_" + category;
            button.gameObject.SetActive(true);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label?.SetText(category.ToUpperInvariant());
            string captured = category;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectCategory(captured));
            ApplyCategoryVisual(button, category == selectedCategory);
            categoryButtons.Add(button);
        }
    }

    private void RebuildActions(IReadOnlyList<IDevDebugAction> actions)
    {
        DestroyRuntimeViews(actionButtons);
        selectedAction = null;
        for (int i = 0; i < actions.Count; i++)
        {
            IDevDebugAction action = actions[i];
            if (!string.Equals(
                    action.Category,
                    selectedCategory,
                    StringComparison.Ordinal))
            {
                continue;
            }

            DevDebugAvailability availability = SafeEvaluate(action);
            DevDebugActionButtonView view = Instantiate(
                actionButtonTemplate,
                actionRoot);
            view.gameObject.name = "Action_" + action.Id;
            view.gameObject.SetActive(true);
            view.Bind(
                action,
                availability,
                Execute,
                ShowDetails,
                RestoreSelectedDetails);
            actionButtons.Add(view);
            if (selectedAction == null)
                selectedAction = action;
        }

        ShowDetails(selectedAction);
        LayoutRebuilder.ForceRebuildLayoutImmediate(actionRoot);
    }

    private void SelectCategory(string category)
    {
        selectedCategory = category;
        IReadOnlyList<IDevDebugAction> snapshot = registry.Snapshot();
        RebuildCategories(snapshot);
        RebuildActions(snapshot);
    }

    private void Execute(IDevDebugAction action)
    {
        if (action == null || registry == null)
            return;
        selectedAction = action;

        DevDebugHubInputRouter router = ResolveRouter();
        if (router != null)
        {
            router.ExecuteAction(action);
            return;
        }

        DevDebugActionResult result = registry.Execute(action.Id);
        PresentResult(result);
        Refresh();
    }

    private void ShowDetails(IDevDebugAction action)
    {
        selectedAction = action;
        if (action == null)
        {
            detailTitleText?.SetText("NO DEBUG ACTIONS");
            detailSummaryText?.SetText("当前分类没有已注册调试项。");
            detailBodyText?.SetText(string.Empty);
            detailStateText?.SetText("EMPTY");
            return;
        }

        DevDebugAvailability availability = SafeEvaluate(action);
        detailTitleText?.SetText(action.Title);
        detailSummaryText?.SetText(action.Summary);
        detailBodyText?.SetText(action.Details);
        detailStateText?.SetText(
            availability.IsAvailable
                ? "READY · 可执行"
                : "LOCKED · " + availability.Reason);
        if (detailStateText != null)
        {
            detailStateText.color = availability.IsAvailable
                ? new Color(0.25f, 0.94f, 1f, 1f)
                : new Color(1f, 0.58f, 0.18f, 1f);
        }
    }

    private void RestoreSelectedDetails(IDevDebugAction action)
    {
        ShowDetails(selectedAction ?? action);
    }

    private static DevDebugAvailability SafeEvaluate(IDevDebugAction action)
    {
        try
        {
            return action != null
                ? action.Evaluate()
                : DevDebugAvailability.Unavailable("调试项不存在。");
        }
        catch (Exception exception)
        {
            return DevDebugAvailability.Unavailable(
                "条件检查异常：" + exception.Message);
        }
    }

    private static void ApplyCategoryVisual(Button button, bool selected)
    {
        if (button == null)
            return;
        ColorBlock colors = button.colors;
        colors.normalColor = selected
            ? new Color(0.05f, 0.64f, 0.74f, 1f)
            : new Color(0.035f, 0.09f, 0.13f, 1f);
        colors.highlightedColor = new Color(0.08f, 0.78f, 0.88f, 1f);
        colors.pressedColor = new Color(0.04f, 0.48f, 0.58f, 1f);
        button.colors = colors;
    }

    private static void DestroyRuntimeViews<T>(List<T> views)
        where T : Component
    {
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(views[i].gameObject);
                else
                    DestroyImmediate(views[i].gameObject);
            }
        }
        views.Clear();
    }

    private void RequestClose()
    {
        DevDebugHubInputRouter router = ResolveRouter();
        if (router != null)
            router.CloseAnimated();
        else
            HideAnimated(null);
    }

    private DevDebugHubInputRouter ResolveRouter()
    {
        DevDebugHubInputRouter router =
            GetComponentInParent<DevDebugHubInputRouter>();
        if (router == null && GameUIRoot.Instance != null)
            router = GameUIRoot.Instance.GetComponent<DevDebugHubInputRouter>();
        return router;
    }

    private void CaptureRestPosition()
    {
        if (frameRestCaptured || frame == null)
            return;
        frameRestPosition = frame.anchoredPosition;
        frameRestCaptured = true;
    }

    private void KillTransition()
    {
        transition?.Kill(false);
        transition = null;
    }

    private void OnDestroy()
    {
        KillTransition();
        if (registry != null)
            registry.Changed -= Refresh;
        closeButton?.onClick.RemoveListener(RequestClose);
    }
}
