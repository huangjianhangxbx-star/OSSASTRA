using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DRPGPartyBackpackInputRouter : MonoBehaviour
{
    [SerializeField] private GameObject backpackPrefab;
    private readonly InputAction toggleAction = new InputAction(
        "DRPG Party Backpack", InputActionType.Button, "<Keyboard>/tab");
    private readonly InputAction backAction = new InputAction(
        "DRPG Party Backpack Back", InputActionType.Button, "<Mouse>/rightButton");
    private GameUIRoot uiRoot;
    private DRPGPartyBackpackController backpack;
    private CharacterRoot characterRoot;
    private CharacterPickupController pickupController;
    private bool initialized;

    public void Initialize(GameUIRoot owner)
    {
        if (initialized) return;
        uiRoot = owner != null ? owner : GetComponent<GameUIRoot>();
        toggleAction.performed += OnToggle;
        backAction.performed += OnBack;
        initialized = true;
    }
    private void Awake() => Initialize(GetComponent<GameUIRoot>());
    private void OnEnable() { toggleAction.Enable(); backAction.Enable(); }
    private void OnDisable()
    {
        toggleAction.Disable(); backAction.Disable();
        if (backpack != null && backpack.IsOpen) CloseImmediate();
    }
    private void OnDestroy()
    {
        toggleAction.performed -= OnToggle; toggleAction.Dispose();
        backAction.performed -= OnBack; backAction.Dispose();
    }
    private void OnToggle(InputAction.CallbackContext context)
    {
        if (backpack != null && backpack.IsOpen) CloseAnimated(); else Open();
    }
    private void OnBack(InputAction.CallbackContext context)
    {
        if (backpack != null && backpack.IsOpen &&
            !backpack.TryNavigateBack())
        {
            CloseAnimated();
        }
    }

    public void Open()
    {
        OpenInternal(null, null);
    }

    /// <summary>
    /// Public integration seam for chests, field bags and other DRPG slot
    /// containers. The same modal/input ownership rules apply as Tab opening.
    /// </summary>
    public void OpenWithContainer(
        IDRPGInventoryContainer container,
        string containerTitle)
    {
        if (container == null)
            return;
        OpenInternal(container, containerTitle);
    }

    private void OpenInternal(
        IDRPGInventoryContainer externalContainer,
        string externalContainerTitle)
    {
        if (uiRoot == null || !CanOpenInCurrentPhase() ||
            !uiRoot.ModalCoordinator.TryAcquire(this)) return;
        if (backpackPrefab == null)
            backpackPrefab = Resources.Load<GameObject>("DRPG/UI/PF_DRPGPartyBackpack");
        backpack = uiRoot.GetOrCreateDRPGBackpack(backpackPrefab);
        if (backpack == null) { uiRoot.ModalCoordinator.Release(this); return; }
        DRPGProgressionRoot progression = GameFlowRoot.Instance != null
            ? GameFlowRoot.Instance.drpgProgression : FindFirstObjectByType<DRPGProgressionRoot>();
        DRPGEquipmentRuntimeRoot equipment = GameFlowRoot.Instance != null
            ? GameFlowRoot.Instance.drpgEquipment : FindFirstObjectByType<DRPGEquipmentRuntimeRoot>();
        if (progression == null || equipment == null)
        {
            Debug.LogError("[DRPGBackpack] Progression or equipment root is missing.", this);
            uiRoot.ModalCoordinator.Release(this);
            return;
        }
        progression.Initialize();
        if (!equipment.IsInitialized) equipment.Initialize(progression.Service);
        ResolveCharacter();
        SetGameplayEnabled(false);
        if (externalContainer == null)
        {
            backpack.Show(
                equipment.Equipment.PartyInventory,
                CloseAnimated);
        }
        else
        {
            backpack.ShowWithContainer(
                equipment.Equipment.PartyInventory,
                externalContainer,
                externalContainerTitle,
                CloseAnimated);
        }
    }

    public void CloseAnimated()
    {
        if (backpack == null || !backpack.IsOpen) return;
        backpack.HideAnimated(FinishClose);
    }
    private void CloseImmediate() { backpack?.HideImmediate(); FinishClose(); }
    private void FinishClose()
    {
        SetGameplayEnabled(true);
        uiRoot?.ModalCoordinator.Release(this);
    }
    private bool CanOpenInCurrentPhase()
    {
        DRPGGameLoopRuntimeRoot loop = FindFirstObjectByType<DRPGGameLoopRuntimeRoot>();
        return loop == null || loop.CanUseExplorationUI;
    }
    private void ResolveCharacter()
    {
        characterRoot = FindFirstObjectByType<CharacterRoot>();
        pickupController = characterRoot != null ? characterRoot.characterPickupController : null;
    }
    private void SetGameplayEnabled(bool enabled)
    {
        if (pickupController != null) pickupController.SetInputBlocked(!enabled);
        if (characterRoot != null && characterRoot.inputHandler != null)
            characterRoot.inputHandler.SetGameplayInputEnabled(enabled);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
