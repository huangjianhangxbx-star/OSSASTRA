using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// DRPG-native grid backpack. The view may present the party backpack alone or
/// beside any transactional inventory container (chest, field bag, rewards).
/// </summary>
public sealed class DRPGPartyBackpackController : MonoBehaviour
{
    private static readonly Color Cyan = new Color(0.08f, 0.82f, 0.9f, 1f);
    private static readonly Color Magenta = new Color(0.82f, 0.03f, 0.34f, 1f);
    private static readonly Color Panel = new Color(0.01f, 0.065f, 0.08f, 0.985f);
    private static readonly Color DeepPanel = new Color(0.005f, 0.025f, 0.04f, 0.96f);

    [SerializeField] private TMP_FontAsset font;

    private readonly List<DRPGBackpackSlotView> partySlots =
        new List<DRPGBackpackSlotView>();
    private readonly List<DRPGBackpackSlotView> externalSlots =
        new List<DRPGBackpackSlotView>();

    private CanvasGroup canvasGroup;
    private RectTransform frameRect;
    private RectTransform partyContent;
    private RectTransform externalContent;
    private RectTransform detailsRect;
    private GameObject externalPanel;
    private TMP_Text capacityText;
    private TMP_Text detailsTitle;
    private TMP_Text detailsCategory;
    private TMP_Text detailsBody;
    private TMP_Text statusText;
    private TMP_Text externalTitleText;
    private Image dragGhost;
    private GameObject discardModal;
    private TMP_Text discardMessage;
    private Toggle neverAskToggle;

    private DRPGPartyInventoryState partyInventory;
    private IDRPGInventoryContainer externalContainer;
    private string externalTitle = "外部容器";
    private Action closeRequested;
    private Tween tween;
    private DRPGBackpackSlotView dragSource;
    private bool dragResolved;
    private bool refreshAfterDrag;
    private DRPGInventoryDiscardRequest pendingDiscard;
    private int observedPartyRevision = -1;
    private int observedExternalRevision = -1;

    public bool IsOpen => gameObject.activeSelf && canvasGroup != null &&
        canvasGroup.interactable;

    private void Awake()
    {
        BuildIfNeeded();
        HideImmediate();
    }

    private void Update()
    {
        if (!IsOpen || dragSource != null || pendingDiscard != null)
            return;
        if (partyInventory != null &&
            partyInventory.Revision != observedPartyRevision ||
            externalContainer != null &&
            externalContainer.Revision != observedExternalRevision)
        {
            RefreshAll();
        }
    }

    public void Show(
        DRPGPartyInventoryState inventory,
        Action onCloseRequested)
    {
        ShowWithContainer(inventory, null, null, onCloseRequested);
    }

    public void ShowWithContainer(
        DRPGPartyInventoryState inventory,
        IDRPGInventoryContainer otherContainer,
        string otherContainerTitle,
        Action onCloseRequested)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));

        BuildIfNeeded();
        partyInventory = inventory;
        externalContainer = otherContainer;
        externalTitle = string.IsNullOrWhiteSpace(otherContainerTitle)
            ? "外部容器"
            : otherContainerTitle;
        closeRequested = onCloseRequested;
        RefreshAll();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        tween?.Kill();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        tween = canvasGroup.DOFade(1f, 0.16f).SetUpdate(true);
    }

    public bool TryNavigateBack()
    {
        if (pendingDiscard != null || discardModal.activeSelf)
        {
            CancelDiscard();
            return true;
        }
        if (dragSource != null)
        {
            CancelDrag();
            return true;
        }
        return false;
    }

    public void HideAnimated(Action completed)
    {
        if (!gameObject.activeSelf)
        {
            completed?.Invoke();
            return;
        }
        CancelTransientUI();
        tween?.Kill();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        tween = canvasGroup.DOFade(0f, 0.12f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                completed?.Invoke();
            });
    }

    public void HideImmediate()
    {
        tween?.Kill();
        CancelTransientUI();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    public void ShowTooltip(DRPGBackpackSlotView slot)
    {
        if (slot == null || slot.Item == null)
        {
            SetDetails("空格", "", "拖入物品以调整背包位置。", false);
            return;
        }

        var entry = new DRPGPartyBackpackEntry(slot.SlotIndex, slot.Item);
        string description = string.IsNullOrWhiteSpace(slot.Item.Description)
            ? "暂无物品说明。"
            : slot.Item.Description;
        SetDetails(
            entry.DisplayName,
            entry.Category + "  ·  " + entry.Detail,
            description,
            true);
    }

    public void HideTooltip(DRPGBackpackSlotView slot)
    {
        if (dragSource == null)
            SetDetails("物品详情", "", "将鼠标停留在格子上查看说明。", false);
    }

    public void BeginDrag(
        DRPGBackpackSlotView source,
        PointerEventData eventData)
    {
        if (!IsOpen || pendingDiscard != null || source == null ||
            source.Item == null || dragSource != null)
        {
            return;
        }

        dragSource = source;
        dragResolved = false;
        refreshAfterDrag = false;
        dragGhost.sprite = source.Item.Icon;
        dragGhost.enabled = source.Item.Icon != null;
        dragGhost.color = new Color(1f, 1f, 1f, 0.82f);
        dragGhost.rectTransform.position = eventData.position;
        dragGhost.transform.SetAsLastSibling();
        ShowTooltip(source);
        statusText.text = "拖到其他格子可移动或交换；拖出整个背包将申请遗弃。";
    }

    public void ContinueDrag(PointerEventData eventData)
    {
        if (dragSource != null)
            dragGhost.rectTransform.position = eventData.position;
    }

    public void DropOn(DRPGBackpackSlotView destination)
    {
        if (dragSource == null || destination == null || dragResolved)
            return;

        dragResolved = true;
        DRPGInventoryOperationResult result =
            DRPGInventoryTransferService.Transfer(
                dragSource.Container,
                dragSource.SlotIndex,
                destination.Container,
                destination.SlotIndex);
        refreshAfterDrag = result.Success;
        ShowOperationResult(result, "物品已移动。");
    }

    public void EndDrag(PointerEventData eventData)
    {
        if (dragSource == null)
            return;

        DRPGBackpackSlotView source = dragSource;
        bool shouldDiscard = !dragResolved && frameRect != null &&
            !RectTransformUtility.RectangleContainsScreenPoint(
                frameRect,
                eventData.position,
                eventData.pressEventCamera);
        CancelDragVisualOnly();

        if (shouldDiscard)
            RequestDiscard(source.Container, source.SlotIndex);
        else if (refreshAfterDrag)
            RefreshAll();
        else if (!dragResolved)
            statusText.text = "未落入有效格子，物品已返回原位。";
    }

    private void RequestDiscard(
        IDRPGInventoryContainer source,
        int sourceSlot)
    {
        var request = new DRPGInventoryDiscardRequest(source, sourceSlot);
        if (request.Item == null)
        {
            statusText.text = "遗弃失败：原格子已发生变化。";
            return;
        }
        DRPGInventoryOperationResult allowed =
            request.Policy.CanDiscard(request.Item);
        if (!allowed.Success)
        {
            ShowOperationResult(allowed, string.Empty);
            return;
        }

        if (DRPGInventoryInteractionSettings.Session.DiscardConfirmationMode ==
            DRPGDiscardConfirmationMode.NeverAsk)
        {
            ShowOperationResult(request.Execute(), "物品已遗弃。");
            RefreshAll();
            return;
        }

        pendingDiscard = request;
        neverAskToggle.isOn = false;
        discardMessage.text = "是否永久遗弃「" + request.Item.DisplayName +
            (request.Item.Quantity > 1
                ? "」 ×" + request.Item.Quantity
                : "」") + "？\n此操作不会在场景中生成掉落物。";
        discardModal.SetActive(true);
        discardModal.transform.SetAsLastSibling();
    }

    private void ConfirmDiscard()
    {
        if (pendingDiscard == null)
        {
            CancelDiscard();
            return;
        }
        if (neverAskToggle.isOn)
        {
            DRPGInventoryInteractionSettings.Session
                .SetDiscardConfirmationMode(
                    DRPGDiscardConfirmationMode.NeverAsk);
        }

        DRPGInventoryOperationResult result = pendingDiscard.Execute();
        pendingDiscard = null;
        discardModal.SetActive(false);
        ShowOperationResult(result, "物品已遗弃。");
        RefreshAll();
    }

    private void CancelDiscard()
    {
        pendingDiscard = null;
        if (discardModal != null)
            discardModal.SetActive(false);
        if (statusText != null)
            statusText.text = "已取消遗弃。";
    }

    private void CancelDrag()
    {
        CancelDragVisualOnly();
        statusText.text = "已取消拖拽。";
    }

    private void CancelDragVisualOnly()
    {
        dragSource = null;
        dragResolved = false;
        dragGhost.enabled = false;
    }

    private void CancelTransientUI()
    {
        if (dragGhost != null)
            CancelDragVisualOnly();
        pendingDiscard = null;
        if (discardModal != null)
            discardModal.SetActive(false);
    }

    private void RefreshAll()
    {
        if (partyInventory == null)
            return;

        capacityText.text = "格位  " + partyInventory.UsedSlots + "/" +
            partyInventory.SlotCapacity + "     负重  " +
            partyInventory.CurrentWeight.ToString("0.0") + "/" +
            partyInventory.WeightCapacity.ToString("0.0");
        RebuildGrid(partyContent, partyInventory, partySlots);
        observedPartyRevision = partyInventory.Revision;

        bool hasExternal = externalContainer != null;
        externalPanel.SetActive(hasExternal);
        if (hasExternal)
        {
            externalTitleText.text = externalTitle + "  " +
                externalContainer.UsedSlots + "/" +
                externalContainer.Capacity;
            RebuildGrid(externalContent, externalContainer, externalSlots);
            observedExternalRevision = externalContainer.Revision;
            SetDetails(
                "双容器交换",
                "拖拽事务",
                "可在团队背包与当前容器之间直接移动、堆叠或交换物品。",
                false);
        }
        else
        {
            ClearGrid(externalContent, externalSlots);
            observedExternalRevision = -1;
            SetDetails(
                "物品详情",
                "",
                "将鼠标停留在格子上查看说明。",
                false);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(partyContent);
        if (hasExternal)
            LayoutRebuilder.ForceRebuildLayoutImmediate(externalContent);
    }

    private void RebuildGrid(
        RectTransform content,
        IDRPGInventoryContainer container,
        IList<DRPGBackpackSlotView> destination)
    {
        ClearGrid(content, destination);
        for (int slotIndex = 0; slotIndex < container.Capacity; slotIndex++)
        {
            GameObject slotObject = CreateUI(
                "Slot_" + slotIndex,
                content,
                typeof(Image),
                typeof(DRPGBackpackSlotView));
            DRPGBackpackSlotView view =
                slotObject.GetComponent<DRPGBackpackSlotView>();
            view.Initialize(this, container, slotIndex, font);
            destination.Add(view);
        }
    }

    private static void ClearGrid(
        RectTransform content,
        IList<DRPGBackpackSlotView> views)
    {
        views.Clear();
        if (content == null)
            return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void SetDetails(
        string title,
        string category,
        string body,
        bool occupied)
    {
        detailsTitle.text = title ?? string.Empty;
        detailsTitle.color = occupied ? Color.white : Cyan;
        detailsCategory.text = category ?? string.Empty;
        detailsBody.text = body ?? string.Empty;
    }

    private void ShowOperationResult(
        DRPGInventoryOperationResult result,
        string successMessage)
    {
        statusText.color = result.Success
            ? new Color(0.25f, 1f, 0.72f, 1f)
            : new Color(1f, 0.35f, 0.42f, 1f);
        statusText.text = result.Success
            ? successMessage
            : "操作失败：" + result.Message;
    }

    private void BuildIfNeeded()
    {
        if (canvasGroup != null)
            return;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();
        Stretch(root);

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 180;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Image shade = CreateUI(
            "Backdrop",
            transform,
            typeof(Image)).GetComponent<Image>();
        Stretch(shade.rectTransform);
        shade.color = new Color(0.002f, 0.008f, 0.015f, 0.88f);

        GameObject frame = CreateUI(
            "BackpackFrame",
            transform,
            typeof(Image),
            typeof(Outline));
        frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(1840f, 1080f);
        frame.GetComponent<Image>().color = Panel;
        Outline frameOutline = frame.GetComponent<Outline>();
        frameOutline.effectColor = Cyan;
        frameOutline.effectDistance = new Vector2(3f, -3f);

        TMP_Text title = CreateText(
            "Title",
            frame.transform,
            "PARTY BACKPACK / 团队背包",
            42f,
            Color.white,
            TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.72f, 1f),
            new Vector2(48f, -112f), new Vector2(-8f, -30f));

        capacityText = CreateText(
            "Capacity",
            frame.transform,
            string.Empty,
            25f,
            Cyan,
            TextAlignmentOptions.MidlineLeft);
        SetRect(capacityText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0.72f, 1f),
            new Vector2(50f, -162f),
            new Vector2(-8f, -112f));

        Button close = CreateButton(
            "CloseButton",
            frame.transform,
            "TAB / 关闭",
            Magenta);
        SetRect(close.GetComponent<RectTransform>(),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-276f, -104f),
            new Vector2(-44f, -42f));
        close.onClick.AddListener(() => closeRequested?.Invoke());

        partyContent = CreateGridPane(
            "PartyPane",
            frame.transform,
            "团队背包格位",
            new Vector2(0.025f, 0.08f),
            new Vector2(0.68f, 0.82f),
            9,
            out _);

        externalPanel = CreateUI(
            "ExternalPanel",
            frame.transform,
            typeof(Image));
        SetRect(externalPanel.GetComponent<RectTransform>(),
            new Vector2(0.70f, 0.39f),
            new Vector2(0.975f, 0.82f),
            Vector2.zero,
            Vector2.zero);
        externalPanel.GetComponent<Image>().color = DeepPanel;
        externalTitleText = CreateText(
            "ExternalTitle",
            externalPanel.transform,
            "外部容器",
            25f,
            Cyan,
            TextAlignmentOptions.MidlineLeft);
        SetRect(externalTitleText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(18f, -54f),
            new Vector2(-18f, -8f));
        externalContent = CreateGridArea(
            externalPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(14f, 14f),
            new Vector2(-14f, -62f),
            4,
            out _);

        GameObject details = CreateUI(
            "DetailsPanel",
            frame.transform,
            typeof(Image));
        detailsRect = details.GetComponent<RectTransform>();
        SetRect(detailsRect,
            new Vector2(0.70f, 0.08f),
            new Vector2(0.975f, 0.37f),
            Vector2.zero,
            Vector2.zero);
        details.GetComponent<Image>().color = DeepPanel;
        detailsTitle = CreateText(
            "DetailsTitle",
            details.transform,
            "物品详情",
            31f,
            Cyan,
            TextAlignmentOptions.TopLeft);
        SetRect(detailsTitle.rectTransform,
            new Vector2(0f, 0.72f),
            new Vector2(1f, 1f),
            new Vector2(24f, 0f),
            new Vector2(-24f, -20f));
        detailsCategory = CreateText(
            "DetailsCategory",
            details.transform,
            string.Empty,
            20f,
            new Color(0.3f, 0.9f, 0.95f, 1f),
            TextAlignmentOptions.TopLeft);
        SetRect(detailsCategory.rectTransform,
            new Vector2(0f, 0.55f),
            new Vector2(1f, 0.76f),
            new Vector2(24f, 0f),
            new Vector2(-24f, 0f));
        detailsBody = CreateText(
            "DetailsBody",
            details.transform,
            string.Empty,
            21f,
            new Color(0.78f, 0.9f, 0.92f, 1f),
            TextAlignmentOptions.TopLeft);
        detailsBody.enableWordWrapping = true;
        SetRect(detailsBody.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.56f),
            new Vector2(24f, 18f),
            new Vector2(-24f, 0f));

        statusText = CreateText(
            "Status",
            frame.transform,
            "拖拽格子整理物品。",
            21f,
            new Color(0.65f, 0.83f, 0.86f, 1f),
            TextAlignmentOptions.Center);
        SetRect(statusText.rectTransform,
            new Vector2(0.03f, 0.015f),
            new Vector2(0.97f, 0.07f),
            Vector2.zero,
            Vector2.zero);

        dragGhost = CreateUI(
            "DragGhost",
            transform,
            typeof(Image)).GetComponent<Image>();
        dragGhost.rectTransform.anchorMin = dragGhost.rectTransform.anchorMax =
            new Vector2(0f, 0f);
        dragGhost.rectTransform.sizeDelta = new Vector2(92f, 92f);
        dragGhost.preserveAspect = true;
        dragGhost.raycastTarget = false;
        dragGhost.enabled = false;

        BuildDiscardModal();
    }

    private RectTransform CreateGridPane(
        string objectName,
        Transform parent,
        string title,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int columns,
        out ScrollRect scrollRect)
    {
        GameObject panel = CreateUI(objectName, parent, typeof(Image));
        SetRect(panel.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax,
            Vector2.zero,
            Vector2.zero);
        panel.GetComponent<Image>().color = DeepPanel;
        TMP_Text label = CreateText(
            "PaneTitle",
            panel.transform,
            title,
            25f,
            Cyan,
            TextAlignmentOptions.MidlineLeft);
        SetRect(label.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(20f, -58f),
            new Vector2(-20f, -8f));
        return CreateGridArea(
            panel.transform,
            Vector2.zero,
            Vector2.one,
            new Vector2(16f, 16f),
            new Vector2(-16f, -66f),
            columns,
            out scrollRect);
    }

    private RectTransform CreateGridArea(
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        int columns,
        out ScrollRect scrollRect)
    {
        GameObject scrollObject = CreateUI(
            "ScrollArea",
            parent,
            typeof(Image),
            typeof(ScrollRect));
        RectTransform scrollRectTransform =
            scrollObject.GetComponent<RectTransform>();
        SetRect(scrollRectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
        scrollObject.GetComponent<Image>().color =
            new Color(0f, 0.018f, 0.028f, 0.9f);

        GameObject viewportObject = CreateUI(
            "Viewport",
            scrollObject.transform,
            typeof(Image),
            typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        SetRect(viewport,
            Vector2.zero,
            Vector2.one,
            new Vector2(0f, 0f),
            new Vector2(-30f, 0f));
        viewportObject.GetComponent<Image>().color = Color.clear;

        GameObject contentObject = CreateUI(
            "Content",
            viewportObject.transform,
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(14, 14, 14, 14);
        grid.cellSize = new Vector2(
            DRPGBackpackSlotView.DefaultCellSize,
            DRPGBackpackSlotView.DefaultCellSize);
        grid.spacing = new Vector2(12f, 12f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateVerticalScrollbar(scrollObject.transform);
        SetRect(scrollbar.GetComponent<RectTransform>(),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-22f, 4f),
            new Vector2(-4f, -4f));

        scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 32f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.Permanent;
        return content;
    }

    private Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        GameObject root = CreateUI(
            "Scrollbar",
            parent,
            typeof(Image),
            typeof(Scrollbar));
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.52f);
        GameObject area = CreateUI("SlidingArea", root.transform);
        Stretch(area.GetComponent<RectTransform>());
        GameObject handleObject = CreateUI(
            "Handle",
            area.transform,
            typeof(Image));
        Image handle = handleObject.GetComponent<Image>();
        handle.color = new Color(0.05f, 0.68f, 0.76f, 0.92f);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        Scrollbar scrollbar = root.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private void BuildDiscardModal()
    {
        discardModal = CreateUI(
            "DiscardModal",
            transform,
            typeof(Image));
        Stretch(discardModal.GetComponent<RectTransform>());
        discardModal.GetComponent<Image>().color =
            new Color(0f, 0f, 0f, 0.78f);

        GameObject panel = CreateUI(
            "ConfirmPanel",
            discardModal.transform,
            typeof(Image),
            typeof(Outline));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 400f);
        panel.GetComponent<Image>().color = Panel;
        panel.GetComponent<Outline>().effectColor = Magenta;

        TMP_Text title = CreateText(
            "Title",
            panel.transform,
            "遗弃物品",
            34f,
            Color.white,
            TextAlignmentOptions.Center);
        SetRect(title.rectTransform,
            new Vector2(0f, 0.78f),
            new Vector2(1f, 1f),
            new Vector2(24f, 0f),
            new Vector2(-24f, -18f));

        discardMessage = CreateText(
            "Message",
            panel.transform,
            string.Empty,
            24f,
            new Color(0.88f, 0.94f, 0.96f, 1f),
            TextAlignmentOptions.Center);
        discardMessage.enableWordWrapping = true;
        SetRect(discardMessage.rectTransform,
            new Vector2(0.06f, 0.39f),
            new Vector2(0.94f, 0.78f),
            Vector2.zero,
            Vector2.zero);

        neverAskToggle = CreateToggle(panel.transform);
        SetRect(neverAskToggle.GetComponent<RectTransform>(),
            new Vector2(0.18f, 0.27f),
            new Vector2(0.82f, 0.39f),
            Vector2.zero,
            Vector2.zero);

        Button cancel = CreateButton(
            "Cancel",
            panel.transform,
            "取消",
            new Color(0.08f, 0.18f, 0.22f, 1f));
        SetRect(cancel.GetComponent<RectTransform>(),
            new Vector2(0.12f, 0.07f),
            new Vector2(0.47f, 0.23f),
            Vector2.zero,
            Vector2.zero);
        cancel.onClick.AddListener(CancelDiscard);

        Button confirm = CreateButton(
            "Confirm",
            panel.transform,
            "确认遗弃",
            Magenta);
        SetRect(confirm.GetComponent<RectTransform>(),
            new Vector2(0.53f, 0.07f),
            new Vector2(0.88f, 0.23f),
            Vector2.zero,
            Vector2.zero);
        confirm.onClick.AddListener(ConfirmDiscard);
        discardModal.SetActive(false);
    }

    private Toggle CreateToggle(Transform parent)
    {
        GameObject root = CreateUI("NeverAsk", parent, typeof(Toggle));
        GameObject box = CreateUI(
            "Background",
            root.transform,
            typeof(Image));
        SetRect(box.GetComponent<RectTransform>(),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, -18f),
            new Vector2(36f, 18f));
        box.GetComponent<Image>().color = new Color(0.03f, 0.12f, 0.15f, 1f);
        GameObject check = CreateUI(
            "Checkmark",
            box.transform,
            typeof(Image));
        Stretch(check.GetComponent<RectTransform>());
        check.GetComponent<RectTransform>().offsetMin = new Vector2(7f, 7f);
        check.GetComponent<RectTransform>().offsetMax = new Vector2(-7f, -7f);
        check.GetComponent<Image>().color = Cyan;
        TMP_Text label = CreateText(
            "Label",
            root.transform,
            "以后不再询问（可在设置中恢复）",
            21f,
            new Color(0.78f, 0.9f, 0.92f, 1f),
            TextAlignmentOptions.MidlineLeft);
        SetRect(label.rectTransform,
            Vector2.zero,
            Vector2.one,
            new Vector2(50f, 0f),
            Vector2.zero);
        Toggle toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();
        return toggle;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color normal)
    {
        GameObject obj = CreateUI(
            objectName,
            parent,
            typeof(Image),
            typeof(Button));
        Image image = obj.GetComponent<Image>();
        image.color = normal;
        Button button = obj.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        TMP_Text text = CreateText(
            "Label",
            obj.transform,
            label,
            23f,
            Color.white,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        TMP_Text text = CreateUI(
            objectName,
            parent,
            typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        if (font != null)
            text.font = font;
        return text;
    }

    private static GameObject CreateUI(
        string objectName,
        Transform parent,
        params Type[] components)
    {
        var types = new Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        Array.Copy(components, 0, types, 1, components.Length);
        var result = new GameObject(objectName, types);
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
