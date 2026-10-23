using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Development-only F10 lifecycle and modal ownership for the debug hub.
/// </summary>
public sealed class DevDebugHubInputRouter : MonoBehaviour
{
    [SerializeField] private GameObject debugHubPrefab;

    private readonly InputAction toggleAction =
        new InputAction("Development Debug Hub", InputActionType.Button, "<Keyboard>/f10");
    private readonly InputAction escapeAction =
        new InputAction("Close Development Debug Hub", InputActionType.Button, "<Keyboard>/escape");
    private readonly InputAction backAction =
        new InputAction("Back Development Debug Hub", InputActionType.Button, "<Mouse>/rightButton");
    private GameUIRoot uiRoot;
    private DevDebugHubController hub;
    private CharacterRoot characterRoot;
    private CharacterPickupController pickupController;
    private bool initialized;

    public bool IsOpen => hub != null && hub.IsOpen;

    public void Initialize(GameUIRoot owner)
    {
        if (initialized)
        {
            if (uiRoot == null)
                uiRoot = owner;
            return;
        }

        uiRoot = owner != null ? owner : GetComponent<GameUIRoot>();
        toggleAction.performed += OnToggle;
        escapeAction.performed += OnClose;
        backAction.performed += OnClose;
        initialized = true;
    }

    private void Awake()
    {
        Initialize(GetComponent<GameUIRoot>());
    }

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        toggleAction.Enable();
        escapeAction.Enable();
        backAction.Enable();
#endif
    }

    private void OnDisable()
    {
        toggleAction.Disable();
        escapeAction.Disable();
        backAction.Disable();
        if (IsOpen)
            CloseImmediate();
    }

    public void Toggle()
    {
        if (IsOpen)
            CloseAnimated();
        else
            Open();
    }

    public bool Open()
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        return false;
#else
        if (IsOpen)
            return true;
        if (uiRoot == null)
            uiRoot = GetComponent<GameUIRoot>();
        if (uiRoot == null || !uiRoot.ModalCoordinator.TryAcquire(this))
            return false;

        if (hub == null && !CreateHub())
        {
            uiRoot.ModalCoordinator.Release(this);
            return false;
        }

        ResolveCharacter();
        SetGameplayEnabled(false);
        hub.Show();
        return true;
#endif
    }

    public void CloseAnimated()
    {
        if (!IsOpen)
        {
            FinishClose();
            return;
        }
        hub.HideAnimated(FinishClose);
    }

    public void CloseImmediate()
    {
        hub?.HideImmediate();
        FinishClose();
    }

    /// <summary>
    /// Actions that open another modal or start a flow release this hub first.
    /// If execution fails, the hub reopens and displays the failure.
    /// </summary>
    public DevDebugActionResult ExecuteAction(IDevDebugAction action)
    {
        if (action == null)
            return DevDebugActionResult.Failed("调试项不存在。");

        bool transitionAway = action.CloseHubOnSuccess;
        DevDebugHubController source = hub;
        if (transitionAway)
            CloseImmediate();

        DevDebugActionResult result = DevDebugHubRuntime.Registry.Execute(action.Id);
        if (!transitionAway)
        {
            source?.PresentResult(result);
            return result;
        }

        if (!result.Success && Open())
            hub?.PresentResult(result);
        return result;
    }

    private bool CreateHub()
    {
        if (debugHubPrefab == null)
        {
            debugHubPrefab = Resources.Load<GameObject>(
                "Development/PF_DevDebugHub");
        }
        if (debugHubPrefab == null)
        {
            Debug.LogError(
                "[DevDebugHub] Missing Resources/Development/PF_DevDebugHub.prefab.",
                this);
            return false;
        }

        GameObject instance = Instantiate(debugHubPrefab, uiRoot.transform, false);
        hub = instance.GetComponent<DevDebugHubController>();
        if (hub == null)
        {
            Debug.LogError(
                "[DevDebugHub] Prefab root requires DevDebugHubController.",
                instance);
            Destroy(instance);
            return false;
        }

        hub.Initialize(DevDebugHubRuntime.Registry);
        return true;
    }

    private void OnToggle(InputAction.CallbackContext context)
    {
        Toggle();
    }

    private void OnClose(InputAction.CallbackContext context)
    {
        if (IsOpen)
            CloseAnimated();
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

    private void SetGameplayEnabled(bool enabled)
    {
        if (pickupController != null)
            pickupController.SetInputBlocked(!enabled);
        if (characterRoot != null && characterRoot.inputHandler != null)
            characterRoot.inputHandler.SetGameplayInputEnabled(enabled);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        toggleAction.performed -= OnToggle;
        escapeAction.performed -= OnClose;
        backAction.performed -= OnClose;
        toggleAction.Dispose();
        escapeAction.Dispose();
        backAction.Dispose();
        uiRoot?.ModalCoordinator.Release(this);
    }
}
