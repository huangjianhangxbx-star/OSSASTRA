using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the ESC lifecycle for the independent game menu. It never changes
/// timeScale and acquires the same modal lease used by PackageSystem.
/// </summary>
public sealed class GameMenuInputRouter : MonoBehaviour
{
    [SerializeField] private GameObject gameMenuPrefab;

    private readonly InputAction escapeAction =
        new InputAction("Game Menu", InputActionType.Button, "<Keyboard>/escape");
    private readonly InputAction backAction =
        new InputAction("Game Menu Back", InputActionType.Button, "<Mouse>/rightButton");
    private GameUIRoot uiRoot;
    private GameMenuController menu;
    private CharacterRoot characterRoot;
    private CharacterPickupController pickupController;
    private bool initialized;

    public void Initialize(GameUIRoot owner)
    {
        if (initialized)
            return;
        uiRoot = owner != null ? owner : GetComponent<GameUIRoot>();
        escapeAction.performed += OnEscape;
        backAction.performed += OnBack;
        initialized = true;
    }

    private void Awake()
    {
        Initialize(GetComponent<GameUIRoot>());
    }

    private void OnEnable()
    {
        escapeAction.Enable();
        backAction.Enable();
    }

    private void OnDisable()
    {
        escapeAction.Disable();
        backAction.Disable();
        if (menu != null && menu.IsOpen)
            CloseImmediate();
    }

    private void OnDestroy()
    {
        escapeAction.performed -= OnEscape;
        escapeAction.Dispose();
        backAction.performed -= OnBack;
        backAction.Dispose();
    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        if (menu != null && menu.IsOpen)
        {
            if (!menu.TryNavigateBack())
                CloseAnimated();
            return;
        }

        Open();
    }

    private void OnBack(InputAction.CallbackContext context)
    {
        if (menu == null || !menu.IsOpen)
            return;
        if (!menu.TryNavigateBack())
            CloseAnimated();
    }

    public void Open()
    {
        if (uiRoot == null || !CanOpenInCurrentPhase() ||
            !uiRoot.ModalCoordinator.TryAcquire(this))
            return;

        if (gameMenuPrefab == null)
            gameMenuPrefab = Resources.Load<GameObject>("DRPG/UI/PF_GameMenu");
        menu = uiRoot.GetOrCreateGameMenu(gameMenuPrefab);
        if (menu == null)
        {
            uiRoot.ModalCoordinator.Release(this);
            return;
        }

        DRPGProgressionRoot progressionRoot =
            GameFlowRoot.Instance != null
                ? GameFlowRoot.Instance.drpgProgression
                : FindFirstObjectByType<DRPGProgressionRoot>();
        if (progressionRoot == null)
        {
            Debug.LogError("[GameMenu] DRPGProgressionRoot is missing.", this);
            uiRoot.ModalCoordinator.Release(this);
            return;
        }

        progressionRoot.Initialize();
        DRPGEquipmentRuntimeRoot equipmentRuntime =
            GameFlowRoot.Instance != null
                ? GameFlowRoot.Instance.drpgEquipment
                : FindFirstObjectByType<DRPGEquipmentRuntimeRoot>();
        if (equipmentRuntime != null && !equipmentRuntime.IsInitialized)
            equipmentRuntime.Initialize(progressionRoot.Service);
        ResolveCharacter();
        SetGameplayEnabled(false);
        menu.Initialize(
            progressionRoot.Service,
            equipmentRuntime != null ? equipmentRuntime.Equipment : null,
            progressionRoot.PartyComposition);
        menu.Show();
    }

    public void CloseAnimated()
    {
        if (menu == null || !menu.IsOpen)
            return;
        menu.HideAnimated(FinishClose);
    }

    private void CloseImmediate()
    {
        menu?.HideImmediate();
        FinishClose();
    }

    private void FinishClose()
    {
        SetGameplayEnabled(true);
        uiRoot?.ModalCoordinator.Release(this);
    }

    private void ResolveCharacter()
    {
        characterRoot = FindFirstObjectByType<CharacterRoot>();
        pickupController = characterRoot != null
            ? characterRoot.characterPickupController
            : null;
    }

    private static bool CanOpenInCurrentPhase()
    {
        DRPGGameLoopRuntimeRoot loop =
            FindFirstObjectByType<DRPGGameLoopRuntimeRoot>();
        return loop == null || loop.CanUseExplorationUI;
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (pickupController != null)
            pickupController.SetInputBlocked(!enabled);
        if (characterRoot != null && characterRoot.inputHandler != null)
            characterRoot.inputHandler.SetGameplayInputEnabled(enabled);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
