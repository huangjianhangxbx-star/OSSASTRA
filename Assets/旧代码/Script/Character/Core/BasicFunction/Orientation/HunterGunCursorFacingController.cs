using UnityEngine;

public class HunterGunCursorFacingController : MonoBehaviour, ICharacterSubsystem,ICharacterLateTickable
{
    [Header("Runtime References")]
    [SerializeField] private CharacterRoot characterRoot;

    [Tooltip("通常填 VisualRoot_HunterGun。不填则默认使用本物体")]
    [SerializeField] private Transform hunterGunVisualRoot;

    [Tooltip("鼠标/准星/光标 Transform")]
    [SerializeField] private Transform cursorTransform;

    [Tooltip("判断左右的原点。建议填 VisualRoot_HunterGun 或 AimOrigin")]
    [SerializeField] private Transform facingOriginTransform;

    [Header("Facing Calibration")]
    [Tooltip("HunterGun Spine 默认是否朝右。你的图里枪默认朝右，一般勾选")]
    [SerializeField] private bool defaultFacesRight = true;

    [Tooltip("如果左右反了，就切换这个")]
    [SerializeField] private bool invertFacing = false;

    [Header("Stability")]
    [Tooltip("光标距离原点 X 小于这个值时，不改变翻转，避免中心抖动")]
    [SerializeField] private float deadZoneX = 0.15f;

    [Tooltip("光标在另一侧持续多久才确认翻转")]
    [SerializeField] private float confirmTime = 0.03f;

    private float currentSign = 1f;
    private float pendingSign = 0f;
    private float pendingTimer = 0f;

    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        ResolveReferences();

        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();

        ResolveReferences();
    }

    public void ManagedLateUpdate(float deltaTime)
    {
        UpdateFacing(deltaTime);
        ApplyFacing();
    }



    private void ResolveReferences()
    {
        if (hunterGunVisualRoot == null)
            hunterGunVisualRoot = transform;

        if (facingOriginTransform == null)
            facingOriginTransform = hunterGunVisualRoot;

        if (cursorTransform == null && characterRoot != null && characterRoot.aimCursorSystem != null)
        {
            // 如果你的 AimCursorSystem 没有暴露 Cursor Transform，这行可以删掉，直接 Inspector 手拖 Cursor。
            // cursorTransform = characterRoot.aimCursorSystem.CursorTransform;
        }
    }

    private void UpdateFacing(float deltaTime)
    {
        if (cursorTransform == null || facingOriginTransform == null)
            return;

        float dx = cursorTransform.position.x - facingOriginTransform.position.x;

        if (Mathf.Abs(dx) < deadZoneX)
        {
            pendingSign = 0f;
            pendingTimer = 0f;
            return;
        }

        float desiredSign = dx >= 0f ? 1f : -1f;

        if (desiredSign == currentSign)
        {
            pendingSign = 0f;
            pendingTimer = 0f;
            return;
        }

        if (pendingSign != desiredSign)
        {
            pendingSign = desiredSign;
            pendingTimer = 0f;
        }

        pendingTimer += deltaTime;

        if (pendingTimer >= confirmTime)
        {
            currentSign = desiredSign;
            pendingSign = 0f;
            pendingTimer = 0f;
        }
    }

    private void ApplyFacing()
    {
        if (hunterGunVisualRoot == null)
            return;

        bool wantRight = currentSign > 0f;

        if (invertFacing)
            wantRight = !wantRight;

        float yAngle;

        if (defaultFacesRight)
            yAngle = wantRight ? 0f : 180f;
        else
            yAngle = wantRight ? 180f : 0f;

        hunterGunVisualRoot.localRotation = Quaternion.Euler(0f, yAngle, 0f);
    }
}