using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Routes character input between gameplay and the scene-level Package UI.
/// Exclusive modal ownership is delegated to GameUIModalCoordinator.
/// </summary>
public class CharacterUIInputRouter : MonoBehaviour, ICharacterSubsystem
{
    public enum PlayerInputContext
    {
        Gameplay,
        PackageUI
    }

    [SerializeField] private GameObject packageCanvasPrefab;

    public PlayerInputContext CurrentContext { get; private set; } =
        PlayerInputContext.Gameplay;
    public bool IsUIOpen => CurrentContext == PlayerInputContext.PackageUI;
    public bool IsGameplayActive => CurrentContext == PlayerInputContext.Gameplay;

    private CharacterRoot characterRoot;
    private CharacterPickupController pickupController;
    private PackageUIController packageUI;
    private GameUIRoot uiRoot;
    private bool isInitialized;
    private readonly InputAction backAction = new InputAction(
        "Package Back",
        InputActionType.Button,
        "<Mouse>/rightButton");

    private void Awake()
    {
        backAction.performed += OnBackPerformed;
    }

    private void OnEnable()
    {
        backAction.Enable();
    }

    public void Initialize(CharacterRoot root)
    {
        if (isInitialized)
            return;
        if (root == null)
        {
            Debug.LogError(
                "[CharacterUIInputRouter] CharacterRoot is required.",
                this);
            return;
        }

        characterRoot = root;
        pickupController = root.characterPickupController;
        if (!TryCreatePackageCanvas())
            return;
        isInitialized = true;
    }

    private bool TryCreatePackageCanvas()
    {
        if (packageCanvasPrefab == null)
        {
            Debug.LogError(
                "[CharacterUIInputRouter] Package canvas prefab is not configured.",
                this);
            return false;
        }

        uiRoot = FindFirstObjectByType<GameUIRoot>();
        if (uiRoot == null)
        {
            var rootObject = new GameObject("GameUIRoot");
            uiRoot = rootObject.AddComponent<GameUIRoot>();
        }

        packageUI = uiRoot.GetOrCreatePackageUI(packageCanvasPrefab);
        if (packageUI == null)
            return false;

        packageUI.Initialize(characterRoot.characterPackageSystem, this);
        return true;
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
        if (CurrentContext == PlayerInputContext.PackageUI)
            return;
        if (uiRoot == null || !CanOpenInCurrentPhase() ||
            !uiRoot.ModalCoordinator.TryAcquire(this))
            return;

        CurrentContext = PlayerInputContext.PackageUI;
        SetGameplayEnabled(false);
        packageUI?.Show();
    }

    public void ClosePackageUI()
    {
        if (CurrentContext == PlayerInputContext.Gameplay)
            return;

        packageUI?.Hide();
        CurrentContext = PlayerInputContext.Gameplay;
        SetGameplayEnabled(true);
        uiRoot?.ModalCoordinator.Release(this);
    }

    private void OnDisable()
    {
        backAction.Disable();
        if (CurrentContext == PlayerInputContext.PackageUI)
            ClosePackageUI();
    }

    private void OnDestroy()
    {
        backAction.performed -= OnBackPerformed;
        backAction.Dispose();
    }

    private void OnBackPerformed(InputAction.CallbackContext context)
    {
        if (CurrentContext != PlayerInputContext.PackageUI)
            return;
        if (packageUI != null && packageUI.TryNavigateBack())
            return;
        ClosePackageUI();
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
