using UnityEngine;

public class OrientationSystem : MonoBehaviour, ICharacterSubsystem,ICharacterTickable,ICharacterLateTickable
{
    public enum OrientationType
    {
        Auto,
        Mouse,
        Movement,
        Hold,
        Target
    }

    public bool IsMoveTurnPending
    {
        get
        {
            return facingController != null && facingController.IsMoveTurnPending;
        }
    }

    public int StableFacingSign
    {
        get
        {
            return facingController != null ? facingController.StableSign : 1;
        }
    }


    [Header("只翻转这个视觉节点，不要翻 CharacterRoot")]
    [SerializeField] private Transform visualFlipRoot;

    [Header("默认朝向策略")]
    [SerializeField] private CharacterFacingMode defaultFacingMode =
        CharacterFacingMode.Cursor;

    [Header("普通方向输入转向判断时间")]
    [SerializeField] private const float moveFlipConfirmTime = 0.1f;//普通方向输入转向判断时间
    [Header("放下武器后转向判断时间")]
    [SerializeField, ReadOnly] private const float moveFlipConfirmTimeAfterWeapon = 0.08f;//放下武器后转向判断时间
    [SerializeField, ReadOnly] private float weaponMoveTurnSlowWindow = 0.35f;
    [SerializeField, ReadOnly] private float moveTurnMovementLockDuration = 0.25f;

    [SerializeField, ReadOnly] private float flipCooldownTime = 0.06f;
    [SerializeField, ReadOnly] private float weaponFacingReleaseDelay = 0.15f;

    private OrientationType currentType = OrientationType.Auto;

    private IOrientationController currentController;
    private IOrientationController cursorOrientationController;
    private IOrientationController movementOrientationController;

    private FacingConstraintController facingConstraintController;


    private FacingController facingController;

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private bool _isInitialized;


    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        dataBus = root.characterDataBus;

        if (dataBus == null)
        {
            Debug.LogError("[OrientationSystem] DataBus 为空！", this);
            return;
        }
        facingConstraintController = new FacingConstraintController(dataBus, weaponFacingReleaseDelay);
        if (visualFlipRoot == null)
        {
            Debug.LogError("[OrientationSystem] visualFlipRoot 没指定，请拖入 VisualRoot", this);
            return;
        }

        visualFlipRoot.localRotation = Quaternion.identity;

        dataBus.facingDirection = Vector3.left;
        dataBus.requestedFacingDirection = Vector3.left;
        dataBus.requestedFacingSource = CharacterDataBus.RequestedFacingSource.None;


        cursorOrientationController = new MouseOrientationController();
        movementOrientationController = new MovementOrientationController();

        (cursorOrientationController as BaseOrientationController)?.Initialize(dataBus, visualFlipRoot);
        (movementOrientationController as BaseOrientationController)?.Initialize(dataBus, visualFlipRoot);

        currentController = cursorOrientationController;
        currentType = OrientationType.Mouse;








        facingController = new FacingController(
    dataBus,
    visualFlipRoot,
    moveFlipConfirmTime,
    moveFlipConfirmTimeAfterWeapon,
    weaponMoveTurnSlowWindow,
    flipCooldownTime,
    moveTurnMovementLockDuration
        );

        _isInitialized = true;
    }

    public void ManagedUpdate(float deltaTime)
    {
        if (!_isInitialized || dataBus == null)
            return;

        SmartOrientationSystemSelector();

        facingConstraintController?.Tick(deltaTime);
        /*
         * 第一步：
         * 现有 OrientationController 只负责计算“请求朝向”
         */
        currentController?.Update(deltaTime);

        /*
         * 第二步：
         * FacingController 负责把请求朝向变成稳定朝向，并最终翻视觉节点
         */
        facingController?.Tick(deltaTime);
    }

    public void ManagedLateUpdate(float deltaTime)
    {
        if (!_isInitialized || dataBus == null)
            return;
        /*
         * 3. 最后阶段再补一次视觉朝向。
         *
         * 目的：
         * 如果动画、IK、武器系统、Spine 或其他脚本在本帧中途改了 visualFlipRoot，
         * 这里把最终朝向重新压回去。
         */
        facingController?.ReapplyVisualOnly();
    }

    private void SmartOrientationSystemSelector()
    {
        CharacterFacingMode mode = defaultFacingMode;
        CharacterControlAuthority authority = characterRoot != null
            ? characterRoot.controlAuthority
            : null;

        if (authority != null)
        {
            mode = authority.IsAllowed(CharacterControlChannel.Facing)
                ? authority.ResolveFacingMode(defaultFacingMode)
                : authority.ResolveFacingMode(CharacterFacingMode.Hold);
        }

        dataBus.activeFacingMode = mode;

        OrientationType requestedType = mode switch
        {
            CharacterFacingMode.Movement => OrientationType.Movement,
            CharacterFacingMode.Hold => OrientationType.Hold,
            _ => OrientationType.Mouse
        };

        if (currentType != requestedType)
            SwitchOrientController(requestedType);
    }

    private bool IsManualCursorDriving()
    {
        if (dataBus == null)
            return false;

        if (!dataBus.isAiming)
            return false;

        return dataBus.mouseMoving;
    }

    private bool ShouldUseTargetOrientation()
    {
        if (dataBus == null)
            return false;

        if (!HasValidLockTarget())
            return false;

        return dataBus.attackPressed;
    }

    private bool HasValidLockTarget()
    {
        if (dataBus == null)
            return false;

        if (!dataBus.hasAttackTarget)
            return false;

        if (dataBus.lockedAttackTarget == null)
            return false;

        if (!dataBus.lockedAttackTarget.IsValid())
            return false;

        return true;
    }

    private void SwitchOrientController(OrientationType type)
    {
        currentType = type;

        switch (type)
        {
            case OrientationType.Mouse:
                currentController = cursorOrientationController;
                break;

            case OrientationType.Movement:
                currentController = movementOrientationController;
                break;

            case OrientationType.Hold:
                currentController = null;
                dataBus.requestedFacingDirection = dataBus.facingDirection;
                dataBus.requestedFacingSource = CharacterDataBus.RequestedFacingSource.None;
                break;
        }
    }


    public void NotifyWeaponFacingActivity(float releaseDelay = -1f)
    {
        facingConstraintController?.NotifyWeaponFacingActivity(releaseDelay);
    }





}
