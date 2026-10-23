using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays one slot per backpack capacity inside a bounded ScrollRect content area.
/// </summary>
public class BackpackGridView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BackpackSlotView slotViewPrefab;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private TextMeshProUGUI capacityText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;

    [Header("Layout")]
    [SerializeField] private int columns = 5;
    [SerializeField] private Vector2 slotSize = new Vector2(100, 100);
    [SerializeField] private float spacing = 4f;

    private readonly List<BackpackSlotView> _slotViews = new List<BackpackSlotView>();
    private CharacterPackageSystem _packageSystem;
    private PackageUIController _controller;
    private int _selectedGlobalSlot = -1;

    public int SelectedSlot => _selectedGlobalSlot;
    public int SlotCount => _slotViews.Count;

    public void Initialize(CharacterPackageSystem packageSystem, PackageUIController controller)
    {
        _packageSystem = packageSystem;
        _controller = controller;
        ConfigureScrollRect();
        CreateSlotViews();
        RefreshAll();
    }

    private void ConfigureScrollRect()
    {
        if (content == null) content = gridContainer as RectTransform;
        if (scrollRect == null || content == null) return;
        if (viewport == null) viewport = scrollRect.viewport;
        scrollRect.content = content;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
    }

    private void CreateSlotViews()
    {
        if (gridContainer == null) gridContainer = transform;
        if (content == null) content = gridContainer as RectTransform;
        if (content == null || _packageSystem?.Backpack == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            if (content.GetChild(i).GetComponent<BackpackSlotView>() != null)
                DestroyImmediate(content.GetChild(i).gameObject);
        }
        _slotViews.Clear();

        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = slotSize;
        grid.spacing = Vector2.one * spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.padding = new RectOffset(2, 2, 2, 2);

        for (int i = 0; i < _packageSystem.Backpack.Capacity; i++)
        {
            BackpackSlotView view = slotViewPrefab != null
                ? Instantiate(slotViewPrefab, content)
                : CreateRuntimeSlotView(content, i);
            _slotViews.Add(view);
        }
        RebindSlots();
    }

    private void RebindSlots()
    {
        if (_packageSystem?.Backpack == null) return;
        for (int i = 0; i < _slotViews.Count; i++)
        {
            var view = _slotViews[i];
            view.gameObject.SetActive(true);
            view.Initialize(i, _controller);
            view.Refresh(_packageSystem.Backpack.GetItem(i));
            view.SetSelected(i == _selectedGlobalSlot);
        }
        UpdateContentHeight();
    }

    private void UpdateContentHeight()
    {
        if (content == null) return;
        int rowCount = Mathf.Max(1, Mathf.CeilToInt((float)_slotViews.Count / Mathf.Max(1, columns)));
        float height = rowCount * slotSize.y + Mathf.Max(0, rowCount - 1) * spacing + 4f;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void UpdateCapacityText()
    {
        if (capacityText == null || _packageSystem?.Backpack == null) return;
        int used = 0;
        for (int i = 0; i < _packageSystem.Backpack.Capacity; i++)
            if (_packageSystem.Backpack.GetItem(i) != null) used++;
        capacityText.text = $"Backpack  {used} / {_packageSystem.Backpack.Capacity}";
    }

    public void RefreshAll()
    {
        if (_packageSystem?.Backpack == null) return;
        if (_slotViews.Count != _packageSystem.Backpack.Capacity)
            CreateSlotViews();
        else
            RebindSlots();
        UpdateCapacityText();
    }

    public void RefreshSlot(int globalSlotIndex)
    {
        if (_packageSystem?.Backpack == null) return;
        if (globalSlotIndex < 0 || globalSlotIndex >= _slotViews.Count) return;
        _slotViews[globalSlotIndex].Refresh(_packageSystem.Backpack.GetItem(globalSlotIndex));
        UpdateCapacityText();
    }

    public void SetSelected(int globalSlotIndex)
    {
        if (_selectedGlobalSlot >= 0 && _selectedGlobalSlot < _slotViews.Count)
            _slotViews[_selectedGlobalSlot].SetSelected(false);
        _selectedGlobalSlot = globalSlotIndex;
        if (globalSlotIndex >= 0 && globalSlotIndex < _slotViews.Count)
            _slotViews[globalSlotIndex].SetSelected(true);
    }

    public BackpackSlotView GetSlotView(int localIndex)
    {
        return localIndex >= 0 && localIndex < _slotViews.Count ? _slotViews[localIndex] : null;
    }

    private BackpackSlotView CreateRuntimeSlotView(Transform parent, int index)
    {
        var go = new GameObject($"Slot_{index}");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        bg.raycastTarget = true;

        var selGO = new GameObject("Selection");
        selGO.transform.SetParent(go.transform, false);
        var selRT = selGO.AddComponent<RectTransform>();
        selRT.anchorMin = Vector2.zero; selRT.anchorMax = Vector2.one;
        selRT.offsetMin = Vector2.zero; selRT.offsetMax = Vector2.zero;
        var selImg = selGO.AddComponent<Image>();
        selImg.color = new Color(0.35f, 0.5f, 0.8f, 0.6f);
        selImg.raycastTarget = false;
        selGO.SetActive(false);

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.15f); iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.offsetMin = Vector2.zero; iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.enabled = false; iconImg.raycastTarget = false;

        var qtyGO = new GameObject("Qty");
        qtyGO.transform.SetParent(go.transform, false);
        var qtyRT = qtyGO.AddComponent<RectTransform>();
        qtyRT.anchorMin = new Vector2(0.5f, 0f); qtyRT.anchorMax = new Vector2(1f, 0.4f);
        qtyRT.offsetMin = Vector2.zero; qtyRT.offsetMax = Vector2.zero;
        var qtyText = qtyGO.AddComponent<TextMeshProUGUI>();
        qtyText.fontSize = 12; qtyText.alignment = TextAlignmentOptions.BottomRight; qtyText.raycastTarget = false;
        qtyGO.SetActive(false);
        return go.AddComponent<BackpackSlotView>();
    }
}
