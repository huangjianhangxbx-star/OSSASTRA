using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class AimCursorSystem : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{
    [Header("׼����ʾ����")]
    [SerializeField] private Transform cursorVisual;
    [SerializeField] private SpriteRenderer cursorSprite;

    [Header("׼�Ļ�������")]
    public float defaultDistance = 3f;

    [Header("׼���ƶ�����")]
    public float followSmoothTime = 0.06f;
    public float gamepadCursorSpeed = 8f;

    [Header("��������")]
    public float mousePixelDeadZone = 0.5f;
    public float gamepadDeadZone = 0.15f;

    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;
    private InputHandler inputHandler;
    private Camera mainCamera;

    private Vector2 lastMouseScreenPosition;
    private bool hasMouseInitialized;

    private float horizontalFollowOffset;
    private Vector3 targetWorldPosition;
    private Vector3 smoothVelocity;
    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        dataBus = root.characterDataBus;
        inputHandler = root.GetComponentInChildren<InputHandler>();
        mainCamera = Camera.main;

        if (inputHandler == null)
        {
            Debug.LogError($"[{name}] AimCursorSystem �Ҳ��� InputHandler");
            return;
        }

        if (cursorVisual == null)
        {
            Debug.LogError($"[{name}] AimCursorSystem û��ָ�� Cursor Visual����� Cursor2 �ϵ� cursorVisual �ϡ�");
            return;
        }

        if (cursorSprite == null)
        {
            cursorSprite = cursorVisual.GetComponent<SpriteRenderer>();
        }

        inputHandler.mouseAction?.Enable();
        inputHandler.gamePad_MouseAction?.Enable();

        Vector3 characterPosition = GetCharacterPosition();
        Vector3 initDirection = GetInitialDirection();

        horizontalFollowOffset = initDirection.x * defaultDistance;
        targetWorldPosition = new Vector3(
            characterPosition.x + horizontalFollowOffset,
            characterPosition.y,
            characterPosition.z
        );

        cursorVisual.position = targetWorldPosition;

        if (inputHandler.mouseAction != null)
        {
            lastMouseScreenPosition = inputHandler.mouseAction.ReadValue<Vector2>();
            hasMouseInitialized = true;
        }

        // ���Ĭ��ʼ�տɼ�
        SetCursorVisible(true);

        dataBus.mouseMoving = false;
        dataBus.mousePositionOnScreen = cursorVisual.position;
        dataBus.aimCursorWorldPosition = cursorVisual.position;
        WriteDataBus();

        _isInitialized = true;
    }
    public void ManagedUpdate(float deltaTime)
    {
        if (characterRoot == null || dataBus == null || inputHandler == null || cursorVisual == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // �������� isAiming�����ʼ�ո���
        SetCursorVisible(true);

        UpdateCursor(deltaTime);
        WriteDataBus();
    }
 
    private void UpdateCursor(float deltaTime)
    {
        Vector3 characterPosition = GetCharacterPosition();

        Vector2 mouseScreenPosition = ReadMouseScreenPosition();
        Vector2 gamepadInput = ReadGamepadInput();

        bool gamepadActive = gamepadInput.magnitude > gamepadDeadZone;
        bool mouseActive = IsMouseMoved(mouseScreenPosition);

        if (gamepadActive)
        {
            HandleGamepadCursor(characterPosition, gamepadInput, deltaTime);
        }
        else if (mouseActive)
        {
            HandleMouseCursor(characterPosition, mouseScreenPosition);
        }
        else
        {
            HandleFollowCharacter(characterPosition);
        }

        cursorVisual.position = Vector3.SmoothDamp(
            cursorVisual.position,
            targetWorldPosition,
            ref smoothVelocity,
            followSmoothTime
        );

        lastMouseScreenPosition = mouseScreenPosition;
        dataBus.mouseMoving = gamepadActive || mouseActive;
    }

    private void HandleGamepadCursor(Vector3 characterPosition, Vector2 gamepadInput, float deltaTime)
    {
        dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Gamepad;

        Vector3 move = new Vector3(gamepadInput.x, gamepadInput.y, 0f);
        targetWorldPosition = cursorVisual.position + move * gamepadCursorSpeed * deltaTime;

        horizontalFollowOffset = targetWorldPosition.x - characterPosition.x;
    }

    private void HandleMouseCursor(Vector3 characterPosition, Vector2 mouseScreenPosition)
    {
        dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Mouse;

        targetWorldPosition = ScreenToWorldOnCharacterPlane(mouseScreenPosition, characterPosition);
        horizontalFollowOffset = targetWorldPosition.x - characterPosition.x;
    }

    private void HandleFollowCharacter(Vector3 characterPosition)
    {
        targetWorldPosition = new Vector3(
            characterPosition.x + horizontalFollowOffset,
            targetWorldPosition.y,
            targetWorldPosition.z
        );
    }

    private Vector2 ReadMouseScreenPosition()
    {
        if (inputHandler == null || inputHandler.mouseAction == null)
            return lastMouseScreenPosition;

        return inputHandler.mouseAction.ReadValue<Vector2>();
    }

    private Vector2 ReadGamepadInput()
    {
        if (inputHandler == null || inputHandler.gamePad_MouseAction == null)
            return Vector2.zero;

        return inputHandler.gamePad_MouseAction.ReadValue<Vector2>();
    }

    private bool IsMouseMoved(Vector2 currentMouseScreenPosition)
    {
        if (!hasMouseInitialized)
        {
            lastMouseScreenPosition = currentMouseScreenPosition;
            hasMouseInitialized = true;
            return false;
        }

        float delta = Vector2.Distance(currentMouseScreenPosition, lastMouseScreenPosition);
        return delta > mousePixelDeadZone;
    }

    private Vector3 ScreenToWorldOnCharacterPlane(Vector2 screenPosition, Vector3 characterPosition)
    {
        if (mainCamera == null)
            return characterPosition;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.forward, characterPosition);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 world = ray.GetPoint(distance);
            world.z = characterPosition.z;
            return world;
        }

        return characterPosition;
    }

    private Vector3 GetCharacterPosition()
    {
        if (characterRoot != null)
            return characterRoot.transform.position;

        return Vector3.zero;
    }

    private Vector3 GetInitialDirection()
    {
        if (dataBus != null && dataBus.facingDirection.sqrMagnitude > 0.001f)
        {
            Vector3 dir = dataBus.facingDirection;
            dir.z = 0f;
            return dir.normalized;
        }

        return Vector3.right;
    }

    private void WriteDataBus()
    {
        dataBus.mousePositionOnScreen = cursorVisual.position;
        dataBus.aimCursorWorldPosition = cursorVisual.position;

        Vector3 characterPosition = GetCharacterPosition();
        Vector3 aimDir = cursorVisual.position - characterPosition;
        aimDir.z = 0f;

        if (aimDir.sqrMagnitude > 0.001f)
        {
            dataBus.aimDirection = aimDir.normalized;
        }
    }

    private void SetCursorVisible(bool visible)
    {
        if (cursorSprite != null)
            cursorSprite.enabled = visible;
    }
}