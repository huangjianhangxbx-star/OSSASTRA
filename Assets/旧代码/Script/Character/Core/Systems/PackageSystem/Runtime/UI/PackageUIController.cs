using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Main UI controller for the package panel.
/// Manages backpack grid + weapon slots + selection + drag/drop.
/// Subscribes to CharacterPackageSystem events; never owns data.
/// </summary>
public class PackageUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BackpackGridView backpackGrid;
    [SerializeField] private WeaponSlotView[] weaponSlotViews = new WeaponSlotView[3];
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image dragIcon;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private ItemDetailsPanel itemDetailsPanel;
    [SerializeField] private ExternalContainerPanel externalContainerPanel;
    [SerializeField] private WorldItemDropSpawner worldItemDropSpawner;
    [SerializeField] private WorldDropPolicySO worldDropPolicy;

    private CharacterPackageSystem _packageSystem;
    private CharacterUIInputRouter _inputRouter;
    private bool _isOpen;
    private int _selectedBackpackSlot = -1;
    private bool _isDragging;

    // Drag state
    private int _dragSourceSlot = -1;
    private bool _dragFromWeaponSlot;
    private bool _dropHandled;

    public bool IsOpen => _isOpen;
    public bool IsDragging => _isDragging;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (panelRoot == null)
        {
            var panelT = transform.Find("Panel");
            if (panelT != null)
                panelRoot = panelT.gameObject;
        }

        if (dragIcon != null)
            dragIcon.raycastTarget = false;
    }

    public void Initialize(CharacterPackageSystem packageSystem, CharacterUIInputRouter inputRouter)
    {
        _packageSystem = packageSystem;
        _inputRouter = inputRouter;

        if (backpackGrid != null)
            backpackGrid.Initialize(packageSystem, this);

        for (int i = 0; i < weaponSlotViews.Length; i++)
        {
            if (weaponSlotViews[i] != null)
                weaponSlotViews[i].Initialize(i, this);
        }

        if (_packageSystem != null)
        {
            _packageSystem.OnBackpackSlotChanged += OnBackpackSlotChanged;
            _packageSystem.OnWeaponSlotChanged += OnWeaponSlotChanged;
            _packageSystem.OnActiveWeaponSlotChanged += OnActiveWeaponSlotChanged;
        }

        // Ensure central details panel is cleared
        if (itemDetailsPanel != null)
            itemDetailsPanel.Clear();

        // External container panel starts hidden — only shown by scene containers
        if (externalContainerPanel != null)
            externalContainerPanel.CloseExternalContainer();

        RefreshAll();
        Hide();
    }

    // --- External container interface (delegated to right panel) ---

    /// <summary>
    /// Opens an external container (chest, shop, etc.) on the right side.
    /// Does NOT affect the left-side character panel.
    /// </summary>
    public void OpenExternalContainer(IItemContainer container, string title)
    {
        if (externalContainerPanel != null)
            externalContainerPanel.OpenExternalContainer(container, title);
    }

    /// <summary>
    /// Closes the external container panel, clearing its reference. Left panel unaffected.
    /// </summary>
    public void CloseExternalContainer()
    {
        if (externalContainerPanel != null)
            externalContainerPanel.CloseExternalContainer();
    }

    private void OnDestroy()
    {
        if (_packageSystem != null)
        {
            _packageSystem.OnBackpackSlotChanged -= OnBackpackSlotChanged;
            _packageSystem.OnWeaponSlotChanged -= OnWeaponSlotChanged;
            _packageSystem.OnActiveWeaponSlotChanged -= OnActiveWeaponSlotChanged;
        }
    }

    // --- Visibility ---

    public void Show()
    {
        SetVisible(true);
        RefreshAll();
    }

    public void Hide()
    {
        CancelSelection();
        SetVisible(false);

        // Close external container when closing the entire package UI
        CloseExternalContainer();
    }

    public void Toggle()
    {
        if (_isOpen) Hide();
        else Show();
    }

    private void SetVisible(bool visible)
    {
        _isOpen = visible;

        if (panelRoot != null)
            panelRoot.SetActive(visible);

        if (canvasGroup != null)
        {
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.alpha = 1f;
        }
    }

    // --- Event handlers ---

    private void OnBackpackSlotChanged(int slotIndex, ItemInstance item)
    {
        if (backpackGrid != null)
            backpackGrid.RefreshSlot(slotIndex);
    }

    private void OnWeaponSlotChanged(int slotIndex, ItemInstance item)
    {
        if (slotIndex >= 0 && slotIndex < weaponSlotViews.Length && weaponSlotViews[slotIndex] != null)
            weaponSlotViews[slotIndex].Refresh(item);
    }

    private void OnActiveWeaponSlotChanged(int prevIndex, int newIndex)
    {
        if (prevIndex >= 0 && prevIndex < weaponSlotViews.Length && weaponSlotViews[prevIndex] != null)
            weaponSlotViews[prevIndex].SetActive(false);
        if (newIndex >= 0 && newIndex < weaponSlotViews.Length && weaponSlotViews[newIndex] != null)
            weaponSlotViews[newIndex].SetActive(true);
    }

    // --- Data accessors for slot views ---

    public ItemInstance GetBackpackItem(int slotIndex)
    {
        return _packageSystem?.Backpack?.GetItem(slotIndex);
    }

    public ItemInstance GetWeaponSlotItem(int slotIndex)
    {
        return _packageSystem?.WeaponSlots?.GetItem(slotIndex);
    }

    // --- Selection ---

    public void ToggleBackpackSelection(int slotIndex)
    {
        if (_selectedBackpackSlot == slotIndex)
        {
            CancelSelection();
        }
        else
        {
            _selectedBackpackSlot = slotIndex;
            if (backpackGrid != null)
                backpackGrid.SetSelected(slotIndex);

            // Update item details panel
            if (itemDetailsPanel != null)
            {
                var item = GetBackpackItem(slotIndex);
                itemDetailsPanel.ShowItem(item);
            }
        }
    }

    public void CancelSelection()
    {
        _selectedBackpackSlot = -1;
        if (backpackGrid != null)
            backpackGrid.SetSelected(-1);
        if (itemDetailsPanel != null)
            itemDetailsPanel.Clear();
        CancelDrag();
    }

    public bool TryNavigateBack()
    {
        if (!_isOpen)
            return false;
        if (_isDragging)
        {
            CancelDrag();
            return true;
        }
        if (externalContainerPanel != null && externalContainerPanel.IsOpen)
        {
            externalContainerPanel.CloseExternalContainer();
            return true;
        }
        if (_selectedBackpackSlot >= 0)
        {
            CancelSelection();
            return true;
        }
        return false;
    }

    // --- Drag state: OnBeginDrag sets, OnEndDrag clears ---

    public void BeginDragFromBackpack(int slotIndex)
    {
        var item = GetBackpackItem(slotIndex);
        if (item == null) return;

        _isDragging = true;
        _dragSourceSlot = slotIndex;
        _dragFromWeaponSlot = false;
        _dropHandled = false;

        if (dragIcon != null)
        {
            dragIcon.sprite = item.Icon;
            dragIcon.enabled = item.Icon != null;
            dragIcon.color = Color.white;
            dragIcon.transform.SetAsLastSibling();
        }
    }

    public void BeginDragFromWeaponSlot(int slotIndex)
    {
        var item = GetWeaponSlotItem(slotIndex);
        if (item == null) return;

        _isDragging = true;
        _dragSourceSlot = slotIndex;
        _dragFromWeaponSlot = true;
        _dropHandled = false;

        if (dragIcon != null)
        {
            dragIcon.sprite = item.Icon;
            dragIcon.enabled = item.Icon != null;
            dragIcon.color = Color.white;
            dragIcon.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// Clears drag state. Called by OnEndDrag ONLY. Not called from OnDrop or Update.
    /// </summary>
    public void EndDrag()
    {
        _isDragging = false;
        _dragSourceSlot = -1;
        _dragFromWeaponSlot = false;
        _dropHandled = false;

        if (dragIcon != null)
            dragIcon.enabled = false;
    }

    public void EndDrag(PointerEventData eventData)
    {
        if (_isDragging &&
            !_dropHandled &&
            !_dragFromWeaponSlot &&
            eventData != null &&
            eventData.pointerCurrentRaycast.gameObject == null)
        {
            TryDropDraggedBackpackItem(eventData.position);
        }

        EndDrag();
    }

    private void TryDropDraggedBackpackItem(Vector2 screenPosition)
    {
        var item = GetBackpackItem(_dragSourceSlot);
        if (item == null ||
            worldDropPolicy == null ||
            !worldDropPolicy.CanDrop(item) ||
            worldItemDropSpawner == null)
            return;

        if (!worldItemDropSpawner.TryResolveDrop(screenPosition, out var plan))
            return;

        int sourceSlot = _dragSourceSlot;
        if (!_packageSystem.TryExtractBackpackItem(
                sourceSlot,
                worldDropPolicy,
                out var extracted))
            return;

        var pickup = worldItemDropSpawner.Spawn(extracted, plan);
        if (pickup == null &&
            !_packageSystem.TryRestoreBackpackItem(sourceSlot, extracted))
        {
            Debug.LogError(
                $"[PackageUI] Failed to spawn and restore dropped item from slot {sourceSlot}");
        }
    }

    public void CancelDrag()
    {
        EndDrag();
    }

    // --- Drop handlers: called by slot views' OnDrop ---
    // These use the saved drag state (_dragSourceSlot, _dragFromWeaponSlot).
    // They do NOT call EndDrag — OnEndDrag handles cleanup after OnDrop returns.

    /// <summary>
    /// Target = backpack slot. Routes based on drag source.
    /// </summary>
    public void HandleDropOnBackpackSlot(int targetSlot)
    {
        if (!_isDragging) return;
        _dropHandled = true;

        TransferResult result;

        if (_dragFromWeaponSlot)
        {
            // Weapon slot → Backpack slot
            result = _packageSystem.UnequipWeaponToSlot(_dragSourceSlot, targetSlot);
        }
        else
        {
            // Backpack → Backpack
            if (_dragSourceSlot == targetSlot)
            {
                // Same slot = no-op
                return;
            }
            result = _packageSystem.MoveBackpackItem(_dragSourceSlot, targetSlot);
        }

        if (!result.success)
            Debug.Log($"[PackageUI] Drop on backpack slot {targetSlot} failed: {result.errorMessage}");
    }

    /// <summary>
    /// Target = weapon slot. Routes based on drag source.
    /// </summary>
    public void HandleDropOnWeaponSlot(int targetSlot)
    {
        if (!_isDragging) return;
        _dropHandled = true;

        TransferResult result;

        if (_dragFromWeaponSlot)
        {
            // Weapon slot → Weapon slot
            if (_dragSourceSlot == targetSlot)
            {
                // Same slot = no-op
                return;
            }
            result = _packageSystem.SwapWeaponSlots(_dragSourceSlot, targetSlot);
        }
        else
        {
            // Backpack → Weapon slot
            result = _packageSystem.EquipWeapon(_dragSourceSlot, targetSlot);
        }

        if (!result.success)
            Debug.Log($"[PackageUI] Drop on weapon slot {targetSlot} failed: {result.errorMessage}");
    }

    /// <summary>
    /// Selection-box equip: click backpack item, then click weapon slot.
    /// </summary>
    public void TrySelectionEquip(int weaponSlotIndex)
    {
        if (_selectedBackpackSlot < 0) return;

        var result = _packageSystem.EquipWeapon(_selectedBackpackSlot, weaponSlotIndex);
        if (!result.success)
            Debug.Log($"[PackageUI] Equip failed: {result.errorMessage}");

        CancelSelection();
    }

    /// <summary>
    /// Set active weapon slot (clicking a weapon slot).
    /// </summary>
    public void SetActiveWeapon(int slotIndex)
    {
        if (_packageSystem?.WeaponSlots == null) return;
        if (!_packageSystem.WeaponSlots.IsSlotEmpty(slotIndex))
            _packageSystem.SetActiveWeaponSlot(slotIndex);
    }

    // --- Refresh ---

    public void RefreshAll()
    {
        if (backpackGrid != null)
            backpackGrid.RefreshAll();

        if (_packageSystem?.WeaponSlots != null)
        {
            for (int i = 0; i < weaponSlotViews.Length; i++)
            {
                if (weaponSlotViews[i] == null) continue;
                var item = _packageSystem.WeaponSlots.GetItem(i);
                weaponSlotViews[i].Refresh(item);
                weaponSlotViews[i].SetActive(i == _packageSystem.WeaponSlots.ActiveSlotIndex);
            }
        }
    }

    private void Update()
    {
        if (!_isOpen) return;

        if (_isDragging && dragIcon != null && dragIcon.enabled)
        {
            dragIcon.transform.position = Input.mousePosition;
        }
    }
}
