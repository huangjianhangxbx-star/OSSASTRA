# ProjectScript Code Documentation - Part 5

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemInstance.cs` | 30 | - | class ItemInstance | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/WeaponItemDefinitionSO.cs` | 13 | - | class WeaponItemDefinitionSO | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Input/CharacterUIInputRouter.cs` | 159 | - | class CharacterUIInputRouter, enum PlayerInputContext | 1 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterBackpack.cs` | 158 | - | class CharacterBackpack | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterPackageSystem.cs` | 165 | - | class CharacterPackageSystem | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterWeaponSlots.cs` | 83 | - | class CharacterWeaponSlots | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/IItemContainer.cs` | 11 | - | interface IItemContainer | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/InventoryTransferService.cs` | 183 | - | class InventoryTransferService, struct TransferResult | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/CharacterPickupController.cs` | 197 | - | class CharacterPickupController | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/PickupCandidateDetector.cs` | 103 | - | class PickupCandidateDetector | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/WorldPickupItem.cs` | 78 | - | class WorldPickupItem | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/BackpackGridView.cs` | 269 | - | class BackpackGridView | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/BackpackSlotView.cs` | 144 | - | class BackpackSlotView | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/ExternalContainerPanel.cs` | 60 | - | class ExternalContainerPanel | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/GameUIRoot.cs` | 12 | - | class GameUIRoot | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/ItemDetailsPanel.cs` | 61 | - | class ItemDetailsPanel | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PackagePageController.cs` | 97 | - | class PackagePageController, enum PackagePageType | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PackageUIController.cs` | 392 | - | class PackageUIController | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PickupPromptLayer.cs` | 10 | - | class PickupPromptLayer | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WeaponSlotView.cs` | 123 | - | class WeaponSlotView | 0 |

## Public API

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemInstance.cs`
- `public sealed class ItemInstance
{`
- `public string instanceId;`
- `public ItemDefinitionSO definition;`
- `public int quantity = 1;`
- `public ItemInstance() {`
- `public ItemInstance(ItemDefinitionSO def, int qty = 1)
    {`
- `public ItemCategory Category => definition != null ? definition.Category : ItemCategory.Misc;`
- `public bool IsWeapon => Category == ItemCategory.Weapon;`
- `public bool Stackable => definition != null && definition.Stackable;`
- `public string DisplayName => definition != null ? definition.DisplayName : "Unknown";`
- `public Sprite Icon => definition != null ? definition.InventoryIcon : null;`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/WeaponItemDefinitionSO.cs`
- `public class WeaponItemDefinitionSO : ItemDefinitionSO
{`
- `public WeaponConfigSO WeaponConfig => weaponConfig;`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Input/CharacterUIInputRouter.cs`
- `public class CharacterUIInputRouter : MonoBehaviour, ICharacterSubsystem
{`
- `public enum PlayerInputContext//管理两个上下文：
    {`
- `public PlayerInputContext CurrentContext {`
- `public bool IsUIOpen => CurrentContext == PlayerInputContext.PackageUI;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void TogglePackageUI()
    {`
- `public void OpenPackageUI()
    {`
- `public void ClosePackageUI()
    {`
- `public bool IsGameplayActive => CurrentContext == PlayerInputContext.Gameplay;`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterBackpack.cs`
- `public class CharacterBackpack : IItemContainer
{`
- `public int Capacity => capacity;`
- `public IReadOnlyList<InventorySlotData> Slots => slots;`
- `public CharacterBackpack(int capacity = 30)
    {`
- `public ItemInstance GetItem(int slotIndex)
    {`
- `public bool CanAccept(ItemInstance item, int slotIndex)
    {`
- `public bool IsFull()
    {`
- `public int FindFirstEmpty()
    {`
- `public int FindStackableSlot(ItemInstance item)
    {`
- `public static bool CanStack(ItemInstance a, ItemInstance b)
    {`
- `internal void SetItemInternal(int slotIndex, ItemInstance item)
    {`
- `internal ItemInstance RemoveItemInternal(int slotIndex)
    {`
- `internal int TryAddItemInternal(ItemInstance item)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterPackageSystem.cs`
- `public class CharacterPackageSystem : MonoBehaviour, ICharacterSubsystem
{`
- `public CharacterBackpack Backpack => _backpack;`
- `public CharacterWeaponSlots WeaponSlots => _weaponSlots;`
- `public InventoryTransferService TransferService => _transferService;`
- `public event System.Action<int, ItemInstance> OnBackpackSlotChanged;`
- `public event System.Action<int, ItemInstance> OnWeaponSlotChanged;`
- `public event System.Action<int, int> OnActiveWeaponSlotChanged;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void InitializeNewGame()
    {`
- `public bool TryPickupItem(ItemInstance item)
    {`
- `public TransferResult EquipWeapon(int backpackSlotIndex, int weaponSlotIndex)
    {`
- `public TransferResult UnequipWeapon(int weaponSlotIndex)
    {`
- `public TransferResult UnequipWeaponToSlot(int weaponSlotIndex, int backpackSlotIndex)
    {`
- `public TransferResult MoveBackpackItem(int fromSlot, int toSlot)
    {`
- `public TransferResult SwapWeaponSlots(int fromSlot, int toSlot)
    {`
- `public bool SetActiveWeaponSlot(int index)
    {`
- `public int CycleNextWeaponSlot()
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterWeaponSlots.cs`
- `public class CharacterWeaponSlots : IItemContainer
{`
- `public const int SlotCount = 3;`
- `public int Capacity => SlotCount;`
- `public int ActiveSlotIndex => activeSlotIndex;`
- `public IReadOnlyList<InventorySlotData> Slots => slots;`
- `public CharacterWeaponSlots()
    {`
- `public ItemInstance GetItem(int slotIndex)
    {`
- `public bool CanAccept(ItemInstance item, int slotIndex)
    {`
- `public bool IsSlotEmpty(int slotIndex)
    {`
- `public bool HasActiveWeapon => !IsSlotEmpty(activeSlotIndex);`
- `public ItemInstance ActiveWeapon => GetItem(activeSlotIndex);`
- `public bool TrySetActiveSlot(int index)
    {`
- `public int FindNextOccupiedSlot(int from)
    {`
- `internal void SetItemInternal(int slotIndex, ItemInstance item)
    {`
- `internal ItemInstance RemoveItemInternal(int slotIndex)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/IItemContainer.cs`
- `public interface IItemContainer
{`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/InventoryTransferService.cs`
- `public class InventoryTransferService
{`
- `public InventoryTransferService(CharacterBackpack backpack, CharacterWeaponSlots weaponSlots)
    {`
- `public TransferResult TryEquipWeapon(int backpackSlotIndex, int weaponSlotIndex)
    {`
- `public TransferResult TryUnequipWeaponToSlot(int weaponSlotIndex, int targetBackpackSlot)
    {`
- `public TransferResult TryUnequipWeapon(int weaponSlotIndex)
    {`
- `public int TryAddPickup(ItemInstance item)
    {`
- `public TransferResult TryMoveBackpackItem(int fromSlot, int toSlot)
    {`
- `public TransferResult TrySwapWeaponSlots(int fromSlot, int toSlot)
    {`
- `public struct TransferResult
{`
- `public bool success;`
- `public string errorMessage;`
- `public static TransferResult Success() => new TransferResult {`
- `public static TransferResult Failed(string msg) => new TransferResult {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/CharacterPickupController.cs`
- `public class CharacterPickupController : MonoBehaviour, ICharacterSubsystem
{`
- `public bool IsInitialized => _isInitialized;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void OnPickupPressed()
    {`
- `public void SetInputBlocked(bool blocked)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/PickupCandidateDetector.cs`
- `public class PickupCandidateDetector : MonoBehaviour
{`
- `public WorldPickupItem CurrentBest => _currentBest;`
- `public int CandidateCount => _candidates.Count;`
- `public bool HasCandidate => _currentBest != null;`
- `public void RemoveCandidate(WorldPickupItem item)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/WorldPickupItem.cs`
- `public class WorldPickupItem : MonoBehaviour
{`
- `public ItemInstance Instance {`
- `public ItemDefinitionSO Definition => itemDefinition;`
- `public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;`
- `public void InjectInstance(ItemInstance instance)
    {`
- `public void OnPickedUp()
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/BackpackGridView.cs`
- `public class BackpackGridView : MonoBehaviour
{`
- `public int SelectedSlot => _selectedGlobalSlot;`
- `public int CurrentPage => _currentPage;`
- `public int PageCount => _pageCount;`
- `public void Initialize(CharacterPackageSystem packageSystem, PackageUIController controller)
    {`
- `public void GoToPage(int page)
    {`
- `public void GoToPrevPage()
    {`
- `public void GoToNextPage()
    {`
- `public void RefreshAll()
    {`
- `public void RefreshSlot(int globalSlotIndex)
    {`
- `public void SetSelected(int globalSlotIndex)
    {`
- `public BackpackSlotView GetSlotView(int localIndex)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/BackpackSlotView.cs`
- `public class BackpackSlotView : MonoBehaviour,
    IPointerDownHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{`
- `public int SlotIndex {`
- `public void Initialize(int index, PackageUIController controller)
    {`
- `public void OnPointerDown(PointerEventData eventData)
    {`
- `public void OnInitializePotentialDrag(PointerEventData eventData)
    {`
- `public void OnBeginDrag(PointerEventData eventData)
    {`
- `public void OnDrag(PointerEventData eventData)
    {`
- `public void OnDrop(PointerEventData eventData)
    {`
- `public void OnEndDrag(PointerEventData eventData)
    {`
- `public void OnPointerClick(PointerEventData eventData)
    {`
- `public void Refresh(ItemInstance item)
    {`
- `public void Clear()
    {`
- `public void SetSelected(bool selected) {`
- `public void SetBlocked(bool blocked) {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/ExternalContainerPanel.cs`
- `public class ExternalContainerPanel : MonoBehaviour
{`
- `public bool IsOpen => _container != null;`
- `public void OpenExternalContainer(IItemContainer container, string title)
    {`
- `public void CloseExternalContainer()
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/GameUIRoot.cs`
- `public sealed class GameUIRoot : MonoBehaviour
{`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/ItemDetailsPanel.cs`
- `public class ItemDetailsPanel : MonoBehaviour
{`
- `public void ShowItem(ItemInstance item)
    {`
- `public void Clear()
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PackagePageController.cs`
- `public class PackagePageController : MonoBehaviour
{`
- `public enum PackagePageType
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PackageUIController.cs`
- `public class PackageUIController : MonoBehaviour
{`
- `public bool IsOpen => _isOpen;`
- `public bool IsDragging => _isDragging;`
- `public void Initialize(CharacterPackageSystem packageSystem, CharacterUIInputRouter inputRouter)
    {`
- `public void OpenExternalContainer(IItemContainer container, string title)
    {`
- `public void CloseExternalContainer()
    {`
- `public void Show()
    {`
- `public void Hide()
    {`
- `public void Toggle()
    {`
- `public ItemInstance GetBackpackItem(int slotIndex)
    {`
- `public ItemInstance GetWeaponSlotItem(int slotIndex)
    {`
- `public void ToggleBackpackSelection(int slotIndex)
    {`
- `public void CancelSelection()
    {`
- `public void BeginDragFromBackpack(int slotIndex)
    {`
- `public void BeginDragFromWeaponSlot(int slotIndex)
    {`
- `public void EndDrag()
    {`
- `public void CancelDrag()
    {`
- `public void HandleDropOnBackpackSlot(int targetSlot)
    {`
- `public void HandleDropOnWeaponSlot(int targetSlot)
    {`
- `public void TrySelectionEquip(int weaponSlotIndex)
    {`
- `public void SetActiveWeapon(int slotIndex)
    {`
- `public void RefreshAll()
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PickupPromptLayer.cs`
- `public sealed class PickupPromptLayer : MonoBehaviour
{`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WeaponSlotView.cs`
- `public class WeaponSlotView : MonoBehaviour,
    IPointerDownHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{`
- `public int SlotIndex {`
- `public void Initialize(int index, PackageUIController controller)
    {`
- `public void OnPointerDown(PointerEventData eventData)
    {`
- `public void OnInitializePotentialDrag(PointerEventData eventData)
    {`
- `public void OnBeginDrag(PointerEventData eventData)
    {`
- `public void OnDrag(PointerEventData eventData)
    {`
- `public void OnDrop(PointerEventData eventData)
    {`
- `public void OnEndDrag(PointerEventData eventData)
    {`
- `public void OnPointerClick(PointerEventData eventData)
    {`
- `public void Refresh(ItemInstance item)
    {`
- `public void Clear() {`
- `public void SetSelected(bool selected) {`
- `public void SetActive(bool active) {`

## Referenced assets

- `Assets/Script/Character/Core/Systems/PackageSystem/Prefabs/UI/PackageCanvas.prefab`

## Complete source

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemInstance.cs`

```cs
using System;
using UnityEngine;

/// <summary>
/// Runtime instance of "which specific item this is".
/// Weapons always get a unique instanceId and quantity=1, never stack.
/// Stackable items with same definition can merge up to maxStack.
/// </summary>
[Serializable]
public sealed class ItemInstance
{
    public string instanceId;
    public ItemDefinitionSO definition;
    public int quantity = 1;

    public ItemInstance() { }

    public ItemInstance(ItemDefinitionSO def, int qty = 1)
    {
        instanceId = Guid.NewGuid().ToString("N");
        definition = def;
        quantity = Mathf.Max(1, qty);
    }

    public ItemCategory Category => definition != null ? definition.Category : ItemCategory.Misc;
    public bool IsWeapon => Category == ItemCategory.Weapon;
    public bool Stackable => definition != null && definition.Stackable;
    public string DisplayName => definition != null ? definition.DisplayName : "Unknown";
    public Sprite Icon => definition != null ? definition.InventoryIcon : null;
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/WeaponItemDefinitionSO.cs`

```cs
using UnityEngine;

/// <summary>
/// Weapon item definition (Plan B: wraps existing WeaponConfigSO without modifying it).
/// </summary>
[CreateAssetMenu(fileName = "WeaponItemDef", menuName = "ScriptableObject/Package/WeaponItemDefinition")]
public class WeaponItemDefinitionSO : ItemDefinitionSO
{
    [SerializeField] private WeaponConfigSO weaponConfig;

    public WeaponConfigSO WeaponConfig => weaponConfig;
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Input/CharacterUIInputRouter.cs`

```cs
using UnityEngine;

/// <summary>
/// Routes input between Gameplay and PackageUI contexts.
/// Owns the Package Canvas as a scene-level UI (never a child of the character),
/// owns the system-cursor state, and toggles the character's gameplay input master switch.
/// 控制背包面板显示、隐藏和初始化的脚本。这个路由器本身不直接操作背包格子，而是调用：_packageUI.Show();_packageUI.Hide();

/// </summary>
public class CharacterUIInputRouter : MonoBehaviour, ICharacterSubsystem
{
    public enum PlayerInputContext//管理两个上下文：
    {
        Gameplay,//玩家正在控制角色移动、攻击、拾取
        PackageUI//玩家正在操作背包界面
    }

    [SerializeField] private GameObject packageCanvasPrefab;//背包 UI 的预制体引用。

    public PlayerInputContext CurrentContext { get; private set; } = PlayerInputContext.Gameplay;//当前上下文保存：外部可以读取，但不能直接修改，只能通过：
                                                                                                 //OpenPackageUI()ClosePackageUI()TogglePackageUI()来切换。
    public bool IsUIOpen => CurrentContext == PlayerInputContext.PackageUI;

    private CharacterRoot _characterRoot;
    private CharacterPickupController _pickupController;
    private PackageUIController _packageUI;
    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized) return;
        _characterRoot = root;
        _pickupController = root.characterPickupController;

        // Auto-load prefab if not assigned
        if (packageCanvasPrefab == null)
        {
            //        |-- 如果没有配置背包预制体 --在 Unity 编辑器中通过路径自动加载



#if UNITY_EDITOR
            packageCanvasPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Script/Character/Core/Systems/PackageSystem/Prefabs/UI/PackageCanvas.prefab");
#endif
        }

        TryCreatePackageCanvas();

        _isInitialized = true;
    }

    /// <summary>
    /// Instantiates the Package Canvas under the scene-level GameUIRoot (never under the character),
    /// and refuses to dynamically add a missing PackageUIController (prefab must be complete).
    /// </summary>
    private void TryCreatePackageCanvas()
    {
        if (packageCanvasPrefab == null)
        {
            Debug.LogError("[CharacterUIInputRouter] packageCanvasPrefab 未配置，无法创建背包 UI。", this);
            return;
        }

        // 1. Locate or create the scene-level UI root (detached from character hierarchy)
        var uiRoot = FindObjectOfType<GameUIRoot>();
        Transform uiRootTransform;
        if (uiRoot != null)
        {
            uiRootTransform = uiRoot.transform;
        }
        else
        {
            var go = new GameObject("GameUIRoot");
            go.AddComponent<GameUIRoot>();
            uiRootTransform = go.transform;
        }

        // 2. Instantiate the canvas under the UI root. Do NOT parent to the character.
        var canvasGO = Instantiate(packageCanvasPrefab, uiRootTransform, false);

        // 3. Require a valid PackageUIController in the prefab — never dynamically add one.
        _packageUI = canvasGO.GetComponent<PackageUIController>();
        if (_packageUI == null)
        {
            Debug.LogError(
                "[CharacterUIInputRouter] PackageCanvas prefab 缺少 PackageUIController，请先在 Inspector 中配置完整的 prefab。",
                canvasGO);
            return;
        }

        // 4. Initialize directly; controller hides itself after init.
        _packageUI.Initialize(_characterRoot.characterPackageSystem, this);
    }

    public void TogglePackageUI()
    {
        if (CurrentContext == PlayerInputContext.Gameplay)
            OpenPackageUI();
        else
            ClosePackageUI();
    }

    public void OpenPackageUI()
    {
        if (CurrentContext == PlayerInputContext.PackageUI) return;

        CurrentContext = PlayerInputContext.PackageUI;

        // 1) 禁用 Gameplay 输入（含状态清理）
        if (_characterRoot != null && _characterRoot.inputHandler != null)
            _characterRoot.inputHandler.SetGameplayInputEnabled(false);

        // 2) 阻止拾取并隐藏拾取提示
        if (_pickupController != null)
            _pickupController.SetInputBlocked(true);

        // 3) 统一管理系统鼠标状态
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 4) 打开背包面板
        if (_packageUI != null)
            _packageUI.Show();

        Debug.Log($"[PackageUI] Open frame={Time.frameCount} ctx={CurrentContext}");
    }

    public void ClosePackageUI()
    {
        if (CurrentContext == PlayerInputContext.Gameplay)
            return;

        // 1) 先关闭 UI，避免同一帧点击穿透到 Gameplay 攻击
        if (_packageUI != null)
            _packageUI.Hide();

        // 2) 恢复 Gameplay 上下文
        CurrentContext = PlayerInputContext.Gameplay;

        // 3) 解除拾取阻止
        if (_pickupController != null)
            _pickupController.SetInputBlocked(false);

        // 4) 恢复 Gameplay 输入
        if (_characterRoot != null && _characterRoot.inputHandler != null)
            _characterRoot.inputHandler.SetGameplayInputEnabled(true);

        // 5) 关闭背包时不隐藏系统鼠标；可见性与锁定状态交由各自的瞄准/准星系统接管，
        //    Router 只负责确保不再将鼠标限制在 Game 窗口，避免影响右键瞄准后的状态。
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"[PackageUI] Close frame={Time.frameCount} ctx={CurrentContext}");
    }

    public bool IsGameplayActive => CurrentContext == PlayerInputContext.Gameplay;
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterBackpack.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// General-purpose backpack with N slots. First version: 30 slots.
/// </summary>
[Serializable]
public class CharacterBackpack : IItemContainer
{
    [SerializeField] private int capacity = 30;
    [SerializeField] private List<InventorySlotData> slots = new List<InventorySlotData>();

    public int Capacity => capacity;
    public IReadOnlyList<InventorySlotData> Slots => slots;

    public CharacterBackpack(int capacity = 30)
    {
        this.capacity = Mathf.Max(1, capacity);
        InitSlots();
    }

    private void InitSlots()
    {
        slots.Clear();
        for (int i = 0; i < capacity; i++)
            slots.Add(new InventorySlotData(i));
    }

    public ItemInstance GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return null;
        return slots[slotIndex].item;
    }

    public bool CanAccept(ItemInstance item, int slotIndex)
    {
        if (item == null || item.definition == null) return false;
        if (slotIndex < 0 || slotIndex >= slots.Count) return false;
        return true;
    }

    public bool IsFull()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].IsEmpty) return false;
        return true;
    }

    public int FindFirstEmpty()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].IsEmpty) return i;
        return -1;
    }

    /// <summary>
    /// Try to find an existing stack that can accept more of the same item.
    /// Returns slot index or -1.
    /// </summary>
    public int FindStackableSlot(ItemInstance item)
    {
        if (item == null || !item.Stackable) return -1;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty) continue;
            if (!CanStack(slot.item, item)) continue;
            if (slot.item.quantity >= slot.item.definition.MaxStack) continue;
            return i;
        }
        return -1;
    }

    public static bool CanStack(ItemInstance a, ItemInstance b)
    {
        if (a == null || b == null) return false;
        if (a.definition == null || b.definition == null) return false;
        if (a.definition != b.definition) return false;
        if (!a.definition.Stackable) return false;
        if (a.definition.Category == ItemCategory.Weapon) return false;
        return true;
    }

    // --- Internal set/remove (only called by InventoryTransferService) ---

    internal void SetItemInternal(int slotIndex, ItemInstance item)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return;
        slots[slotIndex].item = item;
    }

    internal ItemInstance RemoveItemInternal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return null;
        var item = slots[slotIndex].item;
        slots[slotIndex].item = null;
        return item;
    }

    /// <summary>
    /// Try to add an item instance. Returns leftover quantity that didn't fit (0 = success).
    /// For weapons: always occupies a new empty slot, quantity must be 1.
    /// For stackables: fills existing stacks first, then new slots.
    /// </summary>
    internal int TryAddItemInternal(ItemInstance item)
    {
        if (item == null || item.definition == null) return 0;

        int remaining = item.quantity;

        // Stackable: try existing stacks first
        if (item.Stackable)
        {
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty) continue;
                if (!CanStack(slot.item, item)) continue;
                int space = slot.item.definition.MaxStack - slot.item.quantity;
                if (space <= 0) continue;
                int add = Mathf.Min(space, remaining);
                slot.item.quantity += add;
                remaining -= add;
            }
        }

        // New slots for remaining
        while (remaining > 0)
        {
            int emptyIdx = FindFirstEmpty();
            if (emptyIdx < 0) break;

            int maxStack = item.Stackable ? item.definition.MaxStack : 1;
            int add = Mathf.Min(maxStack, remaining);
            var newInstance = new ItemInstance
            {
                instanceId = item.instanceId,
                definition = item.definition,
                quantity = add
            };
            if (!item.Stackable)
            {
                // Non-stackable: keep the original instance (unique instanceId)
                newInstance = item;
                remaining = 0;
            }
            else
            {
                remaining -= add;
            }
            SetItemInternal(emptyIdx, newInstance);
        }

        return remaining;
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterPackageSystem.cs`

```cs
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Unified entry point for character inventory: backpack + weapon slots + transfer service.
/// </summary>
public class CharacterPackageSystem : MonoBehaviour, ICharacterSubsystem
{
    [FoldoutGroup("Backpack", false)]
    [SerializeField] private int backpackCapacity = 30;

    [FoldoutGroup("Starting Weapon", false)]
    [SerializeField] private WeaponItemDefinitionSO startingWeapon;
    [FoldoutGroup("Starting Weapon", false)]
    [SerializeField] private int startingWeaponSlot = 0;

    [FoldoutGroup("Runtime", false)]
    [ShowInInspector, ReadOnly] private CharacterBackpack _backpack;
    [ShowInInspector, ReadOnly] private CharacterWeaponSlots _weaponSlots;
    private InventoryTransferService _transferService;
    private CharacterRoot _characterRoot;
    private bool _isInitialized;

    public CharacterBackpack Backpack => _backpack;
    public CharacterWeaponSlots WeaponSlots => _weaponSlots;
    public InventoryTransferService TransferService => _transferService;

    // Events
    public event System.Action<int, ItemInstance> OnBackpackSlotChanged; // (slotIndex, item)
    public event System.Action<int, ItemInstance> OnWeaponSlotChanged;   // (slotIndex, item)
    public event System.Action<int, int> OnActiveWeaponSlotChanged;       // (prevIndex, newIndex)

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized) return;
        _characterRoot = root;

        _backpack = new CharacterBackpack(backpackCapacity);
        _weaponSlots = new CharacterWeaponSlots();
        _transferService = new InventoryTransferService(_backpack, _weaponSlots);

        _isInitialized = true;
    }

    /// <summary>
    /// Called once for new game initialization. Not called on scene reload or component re-enable.
    /// </summary>
    public void InitializeNewGame()
    {
        if (startingWeapon != null)
        {
            var instance = new ItemInstance(startingWeapon);
            _weaponSlots.SetItemInternal(startingWeaponSlot, instance);
            OnWeaponSlotChanged?.Invoke(startingWeaponSlot, instance);
        }
    }

    /// <summary>
    /// Attempt to pick up an item from the world. Returns true if fully consumed.
    /// </summary>
    public bool TryPickupItem(ItemInstance item)
    {
        if (item == null || _transferService == null)
            return false;

        int leftover = _transferService.TryAddPickup(item);

        for (int i = 0; i < _backpack.Capacity; i++)
        {
            OnBackpackSlotChanged?.Invoke(
                i,
                _backpack.GetItem(i)
            );
        }

        return leftover == 0;
    }

    // --- Convenience wrappers for transfer service ---

    public TransferResult EquipWeapon(int backpackSlotIndex, int weaponSlotIndex)
    {
        var result = _transferService.TryEquipWeapon(backpackSlotIndex, weaponSlotIndex);
        if (result.success)
        {
            OnBackpackSlotChanged?.Invoke(backpackSlotIndex, _backpack.GetItem(backpackSlotIndex));
            OnWeaponSlotChanged?.Invoke(weaponSlotIndex, _weaponSlots.GetItem(weaponSlotIndex));
        }
        return result;
    }

    public TransferResult UnequipWeapon(int weaponSlotIndex)
    {
        var result = _transferService.TryUnequipWeapon(weaponSlotIndex);
        if (result.success)
        {
            OnWeaponSlotChanged?.Invoke(weaponSlotIndex, null);
            // Refresh all backpack slots since item went to first-available
            for (int i = 0; i < _backpack.Capacity; i++)
                OnBackpackSlotChanged?.Invoke(i, _backpack.GetItem(i));
        }
        return result;
    }

    /// <summary>
    /// Unequip weapon to a specific backpack slot (with swap if occupied).
    /// </summary>
    public TransferResult UnequipWeaponToSlot(int weaponSlotIndex, int backpackSlotIndex)
    {
        var result = _transferService.TryUnequipWeaponToSlot(weaponSlotIndex, backpackSlotIndex);
        if (result.success)
        {
            OnWeaponSlotChanged?.Invoke(weaponSlotIndex, _weaponSlots.GetItem(weaponSlotIndex));
            OnBackpackSlotChanged?.Invoke(backpackSlotIndex, _backpack.GetItem(backpackSlotIndex));
        }
        return result;
    }

    public TransferResult MoveBackpackItem(int fromSlot, int toSlot)
    {
        var result = _transferService.TryMoveBackpackItem(fromSlot, toSlot);
        if (result.success)
        {
            OnBackpackSlotChanged?.Invoke(fromSlot, _backpack.GetItem(fromSlot));
            OnBackpackSlotChanged?.Invoke(toSlot, _backpack.GetItem(toSlot));
        }
        return result;
    }

    public TransferResult SwapWeaponSlots(int fromSlot, int toSlot)
    {
        var result = _transferService.TrySwapWeaponSlots(fromSlot, toSlot);
        if (result.success)
        {
            OnWeaponSlotChanged?.Invoke(fromSlot, _weaponSlots.GetItem(fromSlot));
            OnWeaponSlotChanged?.Invoke(toSlot, _weaponSlots.GetItem(toSlot));
        }
        return result;
    }

    public bool SetActiveWeaponSlot(int index)
    {
        if (index < 0 || index >= CharacterWeaponSlots.SlotCount) return false;
        if (_weaponSlots.IsSlotEmpty(index)) return false;

        int prev = _weaponSlots.ActiveSlotIndex;
        if (prev == index) return true; // Already active, no event

        if (_weaponSlots.TrySetActiveSlot(index))
        {
            OnActiveWeaponSlotChanged?.Invoke(prev, index);
            return true;
        }
        return false;
    }

    public int CycleNextWeaponSlot()
    {
        int next = _weaponSlots.FindNextOccupiedSlot(_weaponSlots.ActiveSlotIndex);
        if (next >= 0)
            SetActiveWeaponSlot(next);
        return next;
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/CharacterWeaponSlots.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed 3 weapon slots. Only accepts ItemCategory.Weapon.
/// Integrates with existing CharacterWeaponInventory as an adapter.
/// </summary>
[Serializable]
public class CharacterWeaponSlots : IItemContainer
{
    public const int SlotCount = 3;

    [SerializeField] private List<InventorySlotData> slots = new List<InventorySlotData>(SlotCount);
    [SerializeField] private int activeSlotIndex = 0;

    public int Capacity => SlotCount;
    public int ActiveSlotIndex => activeSlotIndex;
    public IReadOnlyList<InventorySlotData> Slots => slots;

    public CharacterWeaponSlots()
    {
        for (int i = 0; i < SlotCount; i++)
            slots.Add(new InventorySlotData(i));
    }

    public ItemInstance GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        return slots[slotIndex].item;
    }

    public bool CanAccept(ItemInstance item, int slotIndex)
    {
        if (item == null || item.definition == null) return false;
        if (slotIndex < 0 || slotIndex >= SlotCount) return false;
        return item.Category == ItemCategory.Weapon;
    }

    public bool IsSlotEmpty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return true;
        return slots[slotIndex].IsEmpty;
    }

    public bool HasActiveWeapon => !IsSlotEmpty(activeSlotIndex);

    public ItemInstance ActiveWeapon => GetItem(activeSlotIndex);

    public bool TrySetActiveSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return false;
        activeSlotIndex = index;
        return true;
    }

    public int FindNextOccupiedSlot(int from)
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            int idx = (from + i) % SlotCount;
            if (!IsSlotEmpty(idx)) return idx;
        }
        return -1;
    }

    // --- Internal (called by InventoryTransferService) ---

    internal void SetItemInternal(int slotIndex, ItemInstance item)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        slots[slotIndex].item = item;
    }

    internal ItemInstance RemoveItemInternal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        var item = slots[slotIndex].item;
        slots[slotIndex].item = null;
        return item;
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/IItemContainer.cs`

```cs
/// <summary>
/// Abstraction for any container that holds ItemInstances.
/// Backpack, WeaponSlots, StorageContainer, ShopContainer, WorldDropContainer all implement this.
/// </summary>
public interface IItemContainer
{
    int Capacity { get; }
    ItemInstance GetItem(int slotIndex);
    bool CanAccept(ItemInstance item, int slotIndex);
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Inventory/InventoryTransferService.cs`

```cs
using System;
using UnityEngine;

/// <summary>
/// Atomic transfer service. All item movement between containers goes through here.
/// UI must never modify container lists directly.
/// </summary>
public class InventoryTransferService
{
    private readonly CharacterBackpack _backpack;
    private readonly CharacterWeaponSlots _weaponSlots;

    public InventoryTransferService(CharacterBackpack backpack, CharacterWeaponSlots weaponSlots)
    {
        _backpack = backpack;
        _weaponSlots = weaponSlots;
    }

    /// <summary>
    /// Move weapon from backpack slot to weapon slot.
    /// If target slot occupied, performs deterministic swap: old weapon → source backpack slot.
    /// </summary>
    public TransferResult TryEquipWeapon(int backpackSlotIndex, int weaponSlotIndex)
    {
        var item = _backpack.GetItem(backpackSlotIndex);
        if (item == null)
            return TransferResult.Failed("Backpack slot is empty");

        if (!_weaponSlots.CanAccept(item, weaponSlotIndex))
            return TransferResult.Failed("Only weapons can go into weapon slots");

        var existing = _weaponSlots.GetItem(weaponSlotIndex);

        // Remove from backpack
        _backpack.RemoveItemInternal(backpackSlotIndex);

        // If weapon slot occupied, swap directly back to the SAME backpack slot
        if (existing != null)
        {
            _backpack.SetItemInternal(backpackSlotIndex, existing);
        }

        // Place weapon into slot
        _weaponSlots.SetItemInternal(weaponSlotIndex, item);
        return TransferResult.Success();
    }

    /// <summary>
    /// Move weapon from weapon slot back to a SPECIFIC backpack slot.
    /// If target backpack slot has an item, performs swap: backpack item → weapon slot.
    /// </summary>
    public TransferResult TryUnequipWeaponToSlot(int weaponSlotIndex, int targetBackpackSlot)
    {
        var weaponItem = _weaponSlots.GetItem(weaponSlotIndex);
        if (weaponItem == null)
            return TransferResult.Failed("Weapon slot is empty");

        if (targetBackpackSlot < 0 || targetBackpackSlot >= _backpack.Capacity)
            return TransferResult.Failed("Invalid backpack slot index");

        var existingBackpackItem = _backpack.GetItem(targetBackpackSlot);

        if (existingBackpackItem != null)
        {
            // Target backpack slot has an item — swap if it's a weapon
            if (existingBackpackItem.Category != ItemCategory.Weapon)
                return TransferResult.Failed("Target backpack slot has non-weapon item");

            // Swap: weapon slot → backpack, backpack → weapon slot
            _weaponSlots.SetItemInternal(weaponSlotIndex, existingBackpackItem);
            _backpack.SetItemInternal(targetBackpackSlot, weaponItem);
        }
        else
        {
            // Target slot empty — simple move
            _weaponSlots.RemoveItemInternal(weaponSlotIndex);
            _backpack.SetItemInternal(targetBackpackSlot, weaponItem);
        }

        return TransferResult.Success();
    }

    /// <summary>
    /// Move weapon from weapon slot back to backpack (first available slot).
    /// </summary>
    public TransferResult TryUnequipWeapon(int weaponSlotIndex)
    {
        var item = _weaponSlots.GetItem(weaponSlotIndex);
        if (item == null)
            return TransferResult.Failed("Weapon slot is empty");

        int emptyIdx = _backpack.FindFirstEmpty();
        if (emptyIdx < 0)
            return TransferResult.Failed("Backpack full, cannot unequip");

        _weaponSlots.RemoveItemInternal(weaponSlotIndex);
        _backpack.SetItemInternal(emptyIdx, item);
        return TransferResult.Success();
    }

    /// <summary>
    /// Add a picked-up item to backpack.
    /// Returns leftover quantity (0 = fully added).
    /// </summary>
    public int TryAddPickup(ItemInstance item)
    {
        return _backpack.TryAddItemInternal(item);
    }

    /// <summary>
    /// Move/swap item between two backpack slots (deterministic).
    /// Same slot = no-op success. Empty target = move. Occupied = swap or stack.
    /// </summary>
    public TransferResult TryMoveBackpackItem(int fromSlot, int toSlot)
    {
        if (fromSlot == toSlot)
            return TransferResult.Success();

        var item = _backpack.GetItem(fromSlot);
        if (item == null)
            return TransferResult.Failed("Source slot is empty");

        var existing = _backpack.GetItem(toSlot);

        _backpack.RemoveItemInternal(fromSlot);

        if (existing != null)
        {
            // Try stacking first
            if (CharacterBackpack.CanStack(existing, item))
            {
                int space = existing.definition.MaxStack - existing.quantity;
                int add = Mathf.Min(space, item.quantity);
                existing.quantity += add;
                item.quantity -= add;

                if (item.quantity > 0)
                    _backpack.SetItemInternal(fromSlot, item);
                return TransferResult.Success();
            }

            // Deterministic swap
            _backpack.SetItemInternal(toSlot, item);
            _backpack.SetItemInternal(fromSlot, existing);
        }
        else
        {
            _backpack.SetItemInternal(toSlot, item);
        }

        return TransferResult.Success();
    }

    /// <summary>
    /// Swap weapon between two weapon slots (deterministic).
    /// Same slot = no-op success.
    /// </summary>
    public TransferResult TrySwapWeaponSlots(int fromSlot, int toSlot)
    {
        if (fromSlot == toSlot)
            return TransferResult.Success();

        var item = _weaponSlots.GetItem(fromSlot);
        if (item == null)
            return TransferResult.Failed("Source weapon slot is empty");

        var existing = _weaponSlots.GetItem(toSlot);
        _weaponSlots.RemoveItemInternal(fromSlot);
        _weaponSlots.SetItemInternal(toSlot, item);
        _weaponSlots.SetItemInternal(fromSlot, existing);
        return TransferResult.Success();
    }
}

public struct TransferResult
{
    public bool success;
    public string errorMessage;

    public static TransferResult Success() => new TransferResult { success = true };
    public static TransferResult Failed(string msg) => new TransferResult { success = false, errorMessage = msg };
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/CharacterPickupController.cs`

```cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main pickup controller. Placed on the character alongside CharacterPackageSystem.
/// Receives E-press events from InputHandler, manages candidate detection, and shows prompts.
/// The prompt lives inside a HUD Canvas (PickupPromptLayer) — never under this character's Transform.
/// </summary>
public class CharacterPickupController : MonoBehaviour, ICharacterSubsystem
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private WorldPickupPrompt promptPrefab;
    [Tooltip("若指定，Prompt 会实例化到该 HUD 提示图层下方。为空则自动查找/创建 PickupPromptLayer。")]
    [SerializeField] private RectTransform promptParent;

    private CharacterRoot _characterRoot;
    private CharacterPackageSystem _packageSystem;
    private PickupCandidateDetector _detector;
    private WorldPickupPrompt _promptInstance;
    private Camera _mainCamera;
    private bool _isInitialized;
    private bool _inputBlocked;

    public bool IsInitialized => _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized) return;
        _characterRoot = root;
        _packageSystem = root.characterPackageSystem;

        if (_packageSystem == null)
        {
            Debug.LogError("[CharacterPickupController] CharacterPackageSystem not found", this);
            return;
        }

        // Create dedicated trigger child for pickup detection.
        // Never touch the character's own collider (it's used for ground collision).
        var triggerGO = new GameObject("PickupTrigger");
        triggerGO.transform.SetParent(transform, false);
        triggerGO.transform.localPosition = Vector3.zero;
        var triggerCircle = triggerGO.AddComponent<CircleCollider2D>();
        triggerCircle.isTrigger = true;
        triggerCircle.radius = detectionRadius;

        _detector = triggerGO.AddComponent<PickupCandidateDetector>();

        _mainCamera = Camera.main;

        CreatePromptInstance();

        _isInitialized = true;
    }

    /// <summary>
    /// Creates the pickup prompt inside the HUD PickupPromptLayer.
    /// If a HUD layer already exists it is reused, otherwise a scene-level HUD Canvas is created.
    /// </summary>
    private void CreatePromptInstance()
    {
        Transform promptHost = ResolvePromptLayer();

        if (promptPrefab != null)
        {
            _promptInstance = Instantiate(promptPrefab, promptHost, false);
        }
        else
        {
            // Build a simple prompt UI at runtime inside the HUD layer.
            var promptGO = new GameObject("WorldPickupPrompt");
            promptGO.transform.SetParent(promptHost, false);
            var promptRT = promptGO.AddComponent<RectTransform>();
            promptRT.sizeDelta = new Vector2(140, 50);

            var cg = promptGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var bg = promptGO.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            bg.raycastTarget = false;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(promptGO.transform, false);
            var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = "[E]";
            text.fontSize = 14;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            _promptInstance = promptGO.AddComponent<WorldPickupPrompt>();
        }

        if (_promptInstance != null)
            _promptInstance.Initialize(_mainCamera);
    }

    /// <summary>
    /// Locates an existing PickupPromptLayer, or creates a scene-level HUD Canvas containing one.
    /// Never falls back to this character's Transform.
    /// </summary>
    private Transform ResolvePromptLayer()
    {
        // Explicit inspector assignment takes priority.
        if (promptParent != null)
            return promptParent;

        // Reuse an existing HUD prompt layer.
        var layer = Object.FindObjectOfType<PickupPromptLayer>();
        if (layer != null)
            return layer.transform;

        // Host canvas — scene-level Screen Space Overlay, detached from any character.
        var canvasGO = new GameObject("HUDCanvas_Pickup");
        canvasGO.transform.position = Vector3.zero;
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // The dedicated prompt layer rectangle (full-screen stretch) inside the HUD Canvas.
        var layerGO = new GameObject("PickupPromptLayer");
        layerGO.transform.SetParent(canvasGO.transform, false);
        var layerRT = layerGO.AddComponent<RectTransform>();
        layerRT.anchorMin = Vector2.zero;
        layerRT.anchorMax = Vector2.one;
        layerRT.offsetMin = Vector2.zero;
        layerRT.offsetMax = Vector2.zero;
        layerGO.AddComponent<PickupPromptLayer>();

        return layerGO.transform;
    }

    /// <summary>
    /// Called by InputHandler when E is pressed. Input system routes this here.
    /// </summary>
    public void OnPickupPressed()
    {
        if (!_isInitialized || _inputBlocked) return;
        if (_detector == null || !_detector.HasCandidate) return;

        var candidate = _detector.CurrentBest;
        if (candidate == null || candidate.Instance == null) return;

        bool success = _packageSystem.TryPickupItem(candidate.Instance);

        if (success)
        {
            _detector.RemoveCandidate(candidate);
            candidate.OnPickedUp();
        }
        else
        {
            // Backpack full — keep world object, prompt will show "Backpack Full"
        }
    }

    /// <summary>
    /// Block/unblock pickup input when UI is open.
    /// </summary>
    public void SetInputBlocked(bool blocked)
    {
        _inputBlocked = blocked;
        if (blocked && _promptInstance != null)
            _promptInstance.Hide();
    }

    private void Update()
    {
        if (!_isInitialized || _detector == null) return;
        if (_inputBlocked) return;

        if (_promptInstance == null) return;

        if (_detector.HasCandidate)
        {
            var candidate = _detector.CurrentBest;
            bool backpackFull = _packageSystem.Backpack != null && _packageSystem.Backpack.IsFull();
            _promptInstance.Show(
                candidate.PromptAnchor.position + Vector3.up * 0.5f,
                candidate.Instance?.DisplayName ?? "???",
                backpackFull);
        }
        else
        {
            _promptInstance.Hide();
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/PickupCandidateDetector.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects nearby WorldPickupItems via trigger overlaps.
/// Maintains a sorted list of candidates by distance.
/// Must be on the same GameObject as a trigger Collider2D.
/// </summary>
public class PickupCandidateDetector : MonoBehaviour
{
    private readonly List<WorldPickupItem> _candidates = new List<WorldPickupItem>();
    private WorldPickupItem _currentBest;
    private float _switchCooldown = 0.15f;
    private float _lastSwitchTime;

    public WorldPickupItem CurrentBest => _currentBest;
    public int CandidateCount => _candidates.Count;
    public bool HasCandidate => _currentBest != null;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pickup = other.GetComponent<WorldPickupItem>();
        if (pickup != null && !_candidates.Contains(pickup))
        {
            _candidates.Add(pickup);
            UpdateBestCandidate(force: true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var pickup = other.GetComponent<WorldPickupItem>();
        if (pickup != null)
        {
            _candidates.Remove(pickup);
            if (_currentBest == pickup)
            {
                _currentBest = null;
                UpdateBestCandidate(force: true);
            }
        }
    }

    private void Update()
    {
        // Clean up destroyed/missing candidates
        _candidates.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        // Periodically re-evaluate best candidate (with small cooldown to prevent flicker)
        if (Time.time - _lastSwitchTime >= _switchCooldown)
        {
            UpdateBestCandidate(force: false);
        }
    }

    private void UpdateBestCandidate(bool force)
    {
        if (_candidates.Count == 0)
        {
            if (_currentBest != null)
            {
                _currentBest = null;
                _lastSwitchTime = Time.time;
            }
            return;
        }

        WorldPickupItem best = null;
        float bestDist = float.MaxValue;

        foreach (var candidate in _candidates)
        {
            if (candidate == null) continue;
            float dist = (candidate.transform.position - transform.position).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        if (best != _currentBest)
        {
            if (force || Time.time - _lastSwitchTime >= _switchCooldown)
            {
                _currentBest = best;
                _lastSwitchTime = Time.time;
            }
        }
    }

    /// <summary>
    /// Remove a candidate from the list (e.g. after pickup).
    /// </summary>
    public void RemoveCandidate(WorldPickupItem item)
    {
        if (item == null) return;
        _candidates.Remove(item);
        if (_currentBest == item)
            _currentBest = null;
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Pickup/WorldPickupItem.cs`

```cs
using UnityEngine;

/// <summary>
/// World-space pickup entity. Holds an ItemDefinitionSO and creates an ItemInstance on start.
/// If injected with an existing ItemInstance (e.g. player dropped weapon), preserves it.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class WorldPickupItem : MonoBehaviour
{
    [Header("Item Definition")]
    [SerializeField] private ItemDefinitionSO itemDefinition;
    [SerializeField] private int quantity = 1;

    [Header("Prompt Anchor")]
    [SerializeField] private Transform promptAnchor;

    /// <summary>
    /// If set, this pickup was dropped by the player and should preserve the original instance.
    /// </summary>
    private ItemInstance _injectedInstance;

    public ItemInstance Instance { get; private set; }
    public ItemDefinitionSO Definition => itemDefinition;
    public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

    private void Awake()
    {
        // Ensure collider is trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void Start()
    {
        if (_injectedInstance != null)
        {
            Instance = _injectedInstance;
        }
        else if (itemDefinition != null)
        {
            Instance = new ItemInstance(itemDefinition, quantity);
        }

        // Set sprite from definition if available
        if (itemDefinition != null && itemDefinition.InventoryIcon != null)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = itemDefinition.InventoryIcon;
        }
    }

    /// <summary>
    /// Inject an existing ItemInstance (e.g. from player dropping a weapon).
    /// Must be called before Start().
    /// </summary>
    public void InjectInstance(ItemInstance instance)
    {
        _injectedInstance = instance;
        if (instance?.definition != null)
        {
            itemDefinition = instance.definition;
            quantity = instance.quantity;
        }
    }

    /// <summary>
    /// Called by CharacterPickupController when the item is successfully picked up.
    /// </summary>
    public void OnPickedUp()
    {
        Destroy(gameObject);
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/BackpackGridView.cs`

```cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the backpack grid with pagination support.
/// Creates only slotsPerPage slots, rebinds them to global indices on page change.
/// </summary>
public class BackpackGridView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BackpackSlotView slotViewPrefab;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private TextMeshProUGUI capacityText;
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TextMeshProUGUI pageIndicatorText;

    [Header("Layout")]
    [SerializeField] private int slotsPerPage = 20;
    [SerializeField] private int columns = 5;
    [SerializeField] private Vector2 slotSize = new Vector2(64, 64);
    [SerializeField] private float spacing = 4f;

    private readonly List<BackpackSlotView> _slotViews = new List<BackpackSlotView>();
    private CharacterPackageSystem _packageSystem;
    private PackageUIController _controller;
    private int _selectedGlobalSlot = -1;
    private int _currentPage = 0;
    private int _pageCount = 1;

    public int SelectedSlot => _selectedGlobalSlot;
    public int CurrentPage => _currentPage;
    public int PageCount => _pageCount;

    public void Initialize(CharacterPackageSystem packageSystem, PackageUIController controller)
    {
        _packageSystem = packageSystem;
        _controller = controller;

        // Wire pagination buttons
        if (prevPageButton != null)
            prevPageButton.onClick.AddListener(GoToPrevPage);
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(GoToNextPage);

        CreateSlotViews();
        UpdatePagination();
    }

    private void CreateSlotViews()
    {
        if (gridContainer == null)
            gridContainer = transform;

        // Clear existing
        for (int i = gridContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(gridContainer.GetChild(i).gameObject);
        _slotViews.Clear();

        // Ensure GridLayoutGroup
        var grid = gridContainer.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = gridContainer.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = slotSize;
        grid.spacing = Vector2.one * spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.padding = new RectOffset(2, 2, 2, 2);

        // Create exactly slotsPerPage views — they will be rebound on page change
        for (int i = 0; i < slotsPerPage; i++)
        {
            BackpackSlotView view;
            if (slotViewPrefab != null)
                view = Instantiate(slotViewPrefab, gridContainer);
            else
                view = CreateRuntimeSlotView(gridContainer, i);

            _slotViews.Add(view);
        }

        // Bind to current page
        RebindSlots();
    }

    private void RebindSlots()
    {
        int capacity = _packageSystem?.Backpack?.Capacity ?? 0;
        _pageCount = Mathf.Max(1, Mathf.CeilToInt((float)capacity / slotsPerPage));

        if (_currentPage >= _pageCount)
            _currentPage = _pageCount - 1;
        if (_currentPage < 0)
            _currentPage = 0;

        int baseIndex = _currentPage * slotsPerPage;

        for (int i = 0; i < _slotViews.Count; i++)
        {
            int globalIndex = baseIndex + i;

            if (globalIndex < capacity)
            {
                // Valid slot — enable and bind
                _slotViews[i].gameObject.SetActive(true);
                _slotViews[i].Initialize(globalIndex, _controller);
                _slotViews[i].Refresh(_packageSystem.Backpack.GetItem(globalIndex));

                // Restore selection visual
                _slotViews[i].SetSelected(globalIndex == _selectedGlobalSlot);
            }
            else
            {
                // Out of bounds — hide
                _slotViews[i].gameObject.SetActive(false);
            }
        }
    }

    private void UpdatePagination()
    {
        int capacity = _packageSystem?.Backpack?.Capacity ?? 0;
        _pageCount = Mathf.Max(1, Mathf.CeilToInt((float)capacity / slotsPerPage));

        // Capacity text
        if (capacityText != null)
        {
            int used = 0;
            for (int i = 0; i < capacity; i++)
            {
                if (!_packageSystem.Backpack.GetItem(i)?.definition?.Equals(null) ?? false)
                    used++;
                else if (_packageSystem.Backpack.GetItem(i) != null)
                    used++;
            }
            capacityText.text = $"Backpack  {used} / {capacity}";
        }

        // Page indicator
        if (pageIndicatorText != null)
            pageIndicatorText.text = $"{_currentPage + 1} / {_pageCount}";

        // Button states
        if (prevPageButton != null)
            prevPageButton.interactable = _currentPage > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = _currentPage < _pageCount - 1;
    }

    public void GoToPage(int page)
    {
        if (page < 0 || page >= _pageCount) return;
        if (page == _currentPage) return;
        _currentPage = page;
        RebindSlots();
        UpdatePagination();
    }

    public void GoToPrevPage()
    {
        GoToPage(_currentPage - 1);
    }

    public void GoToNextPage()
    {
        GoToPage(_currentPage + 1);
    }

    public void RefreshAll()
    {
        if (_packageSystem?.Backpack == null) return;

        int baseIndex = _currentPage * slotsPerPage;
        for (int i = 0; i < _slotViews.Count; i++)
        {
            int globalIndex = baseIndex + i;
            if (globalIndex < _packageSystem.Backpack.Capacity)
                _slotViews[i].Refresh(_packageSystem.Backpack.GetItem(globalIndex));
        }

        UpdatePagination();
    }

    public void RefreshSlot(int globalSlotIndex)
    {
        if (_packageSystem?.Backpack == null) return;
        if (globalSlotIndex < 0 || globalSlotIndex >= _packageSystem.Backpack.Capacity) return;

        // Check if this slot is on current page
        int baseIndex = _currentPage * slotsPerPage;
        int localIndex = globalSlotIndex - baseIndex;

        if (localIndex >= 0 && localIndex < _slotViews.Count)
        {
            _slotViews[localIndex].Refresh(_packageSystem.Backpack.GetItem(globalSlotIndex));
        }

        UpdatePagination();
    }

    public void SetSelected(int globalSlotIndex)
    {
        // Clear previous
        int prevLocal = _selectedGlobalSlot - (_currentPage * slotsPerPage);
        if (prevLocal >= 0 && prevLocal < _slotViews.Count)
            _slotViews[prevLocal].SetSelected(false);

        _selectedGlobalSlot = globalSlotIndex;

        int localIndex = globalSlotIndex - (_currentPage * slotsPerPage);
        if (localIndex >= 0 && localIndex < _slotViews.Count)
            _slotViews[localIndex].SetSelected(true);
    }

    public BackpackSlotView GetSlotView(int localIndex)
    {
        if (localIndex < 0 || localIndex >= _slotViews.Count) return null;
        return _slotViews[localIndex];
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
        iconRT.anchorMin = new Vector2(0.15f, 0.15f);
        iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.offsetMin = Vector2.zero; iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.enabled = false;
        iconImg.raycastTarget = false;

        var qtyGO = new GameObject("Qty");
        qtyGO.transform.SetParent(go.transform, false);
        var qtyRT = qtyGO.AddComponent<RectTransform>();
        qtyRT.anchorMin = new Vector2(0.5f, 0f);
        qtyRT.anchorMax = new Vector2(1f, 0.4f);
        qtyRT.offsetMin = Vector2.zero; qtyRT.offsetMax = Vector2.zero;
        var qtyText = qtyGO.AddComponent<TextMeshProUGUI>();
        qtyText.fontSize = 12;
        qtyText.alignment = TextAlignmentOptions.BottomRight;
        qtyText.raycastTarget = false;
        qtyGO.SetActive(false);

        return go.AddComponent<BackpackSlotView>();
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/BackpackSlotView.cs`

```cs
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
            _controller.EndDrag();
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
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/ExternalContainerPanel.cs`

```cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the right-side external container panel.
/// Only visible when a scene container (chest, shop, etc.) is opened.
/// </summary>
public class ExternalContainerPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI containerNameText;
    [SerializeField] private TextMeshProUGUI capacityText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform gridContainer;

    private IItemContainer _container;

    public bool IsOpen => _container != null;

    private void Awake()
    {
        // Ensure panel background doesn't block raycasts when hidden
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseExternalContainer);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Opens the panel and binds it to an external container.
    /// </summary>
    public void OpenExternalContainer(IItemContainer container, string title)
    {
        if (container == null) return;

        _container = container;
        gameObject.SetActive(true);

        if (containerNameText != null)
            containerNameText.text = title;

        if (capacityText != null)
            capacityText.text = $"0 / {container.Capacity}";

        // TODO: Populate grid with container items (read-only for now)
    }

    /// <summary>
    /// Closes the panel and clears the container reference.
    /// </summary>
    public void CloseExternalContainer()
    {
        _container = null;
        gameObject.SetActive(false);
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/GameUIRoot.cs`

```cs
using UnityEngine;

/// <summary>
/// Marker component marking the scene-level Game UI root.
/// UI canvases (HUD, Package) are instantiated here, NOT under the character,
/// so they are unaffected by character flip / scale / Spine / pixelation.
/// Router locates this via FindObjectOfType at initialization.
/// </summary>
public sealed class GameUIRoot : MonoBehaviour
{
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/ItemDetailsPanel.cs`

```cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple item details panel in the center area.
/// Shows basic info from the selected ItemInstance.
/// </summary>
public class ItemDetailsPanel : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private GameObject emptyStateText;

    private void Awake()
    {
        // Don't block raycasts — center panel must allow drag passthrough
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
        if (iconImage != null) iconImage.raycastTarget = false;
    }

    public void ShowItem(ItemInstance item)
    {
        if (item == null || item.definition == null)
        {
            Clear();
            return;
        }

        if (emptyStateText != null)
            emptyStateText.SetActive(false);

        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = item.Icon != null;
        }

        if (nameText != null)
            nameText.text = item.DisplayName;

        if (categoryText != null)
            categoryText.text = item.Category.ToString();

        if (quantityText != null)
            quantityText.text = $"x{item.quantity}";
    }

    public void Clear()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (nameText != null) nameText.text = "";
        if (categoryText != null) categoryText.text = "";
        if (quantityText != null) quantityText.text = "";
        if (emptyStateText != null) emptyStateText.SetActive(true);
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PackagePageController.cs`

```cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls top-level page navigation for the Package UI.
/// Currently only Equipment page is active; others are placeholders.
/// </summary>
public class PackagePageController : MonoBehaviour
{
    public enum PackagePageType
    {
        Equipment,
        CharacterStatus,
        Skills,
        Quests
    }

    [Header("Tab Buttons")]
    [SerializeField] private Button equipmentTab;
    [SerializeField] private Button characterStatusTab;
    [SerializeField] private Button skillsTab;
    [SerializeField] private Button questsTab;

    [Header("Page Containers")]
    [SerializeField] private GameObject equipmentPage;
    [SerializeField] private GameObject characterStatusPage;
    [SerializeField] private GameObject skillsPage;
    [SerializeField] private GameObject questsPage;

    [Header("Colors")]
    [SerializeField] private Color activeTabColor = new Color(0.2f, 0.35f, 0.6f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.12f, 0.12f, 0.15f, 0.8f);
    [SerializeField] private Color disabledTabColor = new Color(0.08f, 0.08f, 0.1f, 0.6f);

    private PackagePageType _currentPage = PackagePageType.Equipment;

    private void Awake()
    {
        // Wire button clicks
        if (equipmentTab != null)
            equipmentTab.onClick.AddListener(() => SwitchToPage(PackagePageType.Equipment));

        // Disable unimplemented pages
        DisableTab(characterStatusTab);
        DisableTab(skillsTab);
        DisableTab(questsTab);

        // Show only equipment page
        ShowPage(PackagePageType.Equipment);
    }

    private void DisableTab(Button tab)
    {
        if (tab == null) return;
        tab.interactable = false;
        var img = tab.GetComponent<Image>();
        if (img != null) img.color = disabledTabColor;
    }

    private void SwitchToPage(PackagePageType page)
    {
        if (page != PackagePageType.Equipment) return; // Only equipment implemented
        ShowPage(page);
    }

    private void ShowPage(PackagePageType page)
    {
        _currentPage = page;

        SetPageActive(equipmentPage, page == PackagePageType.Equipment);
        SetPageActive(characterStatusPage, page == PackagePageType.CharacterStatus);
        SetPageActive(skillsPage, page == PackagePageType.Skills);
        SetPageActive(questsPage, page == PackagePageType.Quests);

        UpdateTabColors();
    }

    private void SetPageActive(GameObject pageObj, bool active)
    {
        if (pageObj == null) return;
        pageObj.SetActive(active);
    }

    private void UpdateTabColors()
    {
        UpdateTabColor(equipmentTab, _currentPage == PackagePageType.Equipment);
    }

    private void UpdateTabColor(Button tab, bool isActive)
    {
        if (tab == null || !tab.interactable) return;
        var img = tab.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? activeTabColor : inactiveTabColor;
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PackageUIController.cs`

```cs
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

    private CharacterPackageSystem _packageSystem;
    private CharacterUIInputRouter _inputRouter;
    private bool _isOpen;
    private int _selectedBackpackSlot = -1;
    private bool _isDragging;

    // Drag state
    private int _dragSourceSlot = -1;
    private bool _dragFromWeaponSlot;

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

    // --- Drag state: OnBeginDrag sets, OnEndDrag clears ---

    public void BeginDragFromBackpack(int slotIndex)
    {
        var item = GetBackpackItem(slotIndex);
        if (item == null) return;

        _isDragging = true;
        _dragSourceSlot = slotIndex;
        _dragFromWeaponSlot = false;

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

        if (dragIcon != null)
            dragIcon.enabled = false;
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
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/PickupPromptLayer.cs`

```cs
using UnityEngine;

/// <summary>
/// Marker component marking the HUD canvas layer that hosts pickup prompts.
/// WorldPickupPrompt belongs inside a HUD Canvas, never under the character Transform.
/// </summary>
public sealed class PickupPromptLayer : MonoBehaviour
{
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WeaponSlotView.cs`

```cs
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
            _controller.EndDrag();
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
        if (iconImage != null) { iconImage.sprite = item.Icon; iconImage.enabled = item.Icon != null; }
    }

    public void Clear() { if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; } }
    public void SetSelected(bool selected) { if (selectionFrame != null) selectionFrame.gameObject.SetActive(selected); }
    public void SetActive(bool active) { if (activeHighlight != null) activeHighlight.gameObject.SetActive(active); }
}
```
