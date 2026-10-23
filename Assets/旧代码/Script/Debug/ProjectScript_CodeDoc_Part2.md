# ProjectScript Code Documentation - Part 2

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/BasicFunction/Jump/JumpStateMachine.cs` | 321 | - | class JumpStateMachine | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/BaseOrientationController.cs` | 41 | - | class BaseOrientationController | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/FacingConstraintController.cs` | 82 | - | class FacingConstraintController | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/FacingController.cs` | 327 | - | class FacingController | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/HunterGunCursorFacingController.cs` | 137 | - | class HunterGunCursorFacingController | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/IOrientationController.cs` | 12 | - | interface IOrientationController | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/LockTargetController_Hound.cs` | 276 | - | class LockTargetIKController | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/MouseOrientationController.cs` | 91 | - | class MouseOrientationController | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/OrientationSystem.cs` | 213 | - | class OrientationSystem, enum OrientationType | 0 |
| `Assets/Script/Character/Core/BasicFunction/Orientation/WeaponFacingPolicy.cs` | 72 | - | class WeaponFacingPolicy | 0 |
| `Assets/Script/Character/Core/CharacterDataBus.cs` | 445 | - | class CharacterDataBus, enum InputDevice_AimType, enum RequestedFacingSource | 0 |
| `Assets/Script/Character/Core/CharacterRoot.cs` | 635 | - | class CharacterRoot | 0 |
| `Assets/Script/Character/Core/CharacterSO.cs` | 75 | - | class CharacterSO | 0 |
| `Assets/Script/Character/Core/CharacterStateRequest.cs` | 49 | - | class CharacterStateRequest | 0 |
| `Assets/Script/Character/Core/CombatActionData.cs` | 24 | - | class CombatActionData | 0 |
| `Assets/Script/Character/Core/ECharacter.cs` | 434 | - | enum Facing, enum WeaponDefaultFacing, enum WeaponSwitchInputRequestType, enum CombatDamageValueMode, enum AnimationLayer, enum AnimationIntentSourceStateMachine, enum WeaponPostureState, enum EAnimSemantic, enum BattleCamp, enum EnemyRank, enum WeaponEquipState, enum EAnimationLogicState, enum EState, enum CharacterStateType, enum CharacterStats, enum StatModifierMode, enum ActionEffectType, enum MoveType, enum WeaponType, enum FirearmCategory, enum GunFireModeType, enum GunHitType, enum HoundCombatActionType, enum TransitionSuppressReason, enum WeaponSlotId | 0 |
| `Assets/Script/Character/Core/ICharacterFixedTickable.cs` | 4 | - | interface ICharacterFixedTickable | 0 |
| `Assets/Script/Character/Core/ICharacterLateTickable.cs` | 5 | - | interface ICharacterLateTickable | 0 |
| `Assets/Script/Character/Core/ICharacterStateMachine.cs` | 57 | - | interface ICharacterStateMachine | 0 |
| `Assets/Script/Character/Core/ICharacterSubsystem.cs` | 10 | - | interface ICharacterSubsystem | 0 |

## Public API

### `Assets/Script/Character/Core/BasicFunction/Jump/JumpStateMachine.cs`
- `public class JumpStateMachine : ICharacterStateMachine, IAnimationProvider
{`
- `public bool HasLeftGround => _hasLeftGround;`
- `public string StateMachineName => "JumpStateMachine";`
- `public int Priority => 20;`
- `public string CurrentStateName => currentState?.StateName ?? "None";`
- `public bool IsActive => currentState != null;`
- `public JumpConfigSO Config => _config;`
- `public JumpGroundProbe GroundProbe => _groundProbe;`
- `public Vector2 GravityDown => Vector2.down;`
- `public Vector2 GravityUp => Vector2.up;`
- `public Vector2 LateralAxis => Vector2.right;`
- `public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {`
- `public void Enter()
    {`
- `public void Exit()
    {`
- `public bool RequestState(object stateData)
    {`
- `public bool HasCoyoteTime
    {`
- `public bool HasBufferedJump
    {`
- `public void ConsumeJump()
    {`
- `public void ResetJumpConsumption()
    {`
- `public void SnapToGround()
    {`
- `public void TransitionTo(EState stateKey)
    {`
- `public void CompleteJump()
    {`
- `public void CancelJump(string reason)
    {`
- `public void Update(float deltaTime)
    {`
- `public void FixedUpdate(float fixedDeltaTime)
    {`
- `public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {`
- `public bool CanInterrupt(ICharacterStateMachine other)
    {`
- `public void OnEnterAnimation()
    {`
### `Assets/Script/Character/Core/BasicFunction/Orientation/BaseOrientationController.cs`
- `public abstract class BaseOrientationController : IOrientationController
{`
- `protected Vector3 currentDirection;`
- `protected Transform visualTransform;`
- `protected CharacterDataBus dataBus;`
- `public virtual void Initialize(CharacterDataBus bus, Transform visualTr)
    {`
- `public abstract void UpdateOrientation(float deltaTime);`
- `public abstract void OrientMethod();`
- `public virtual void Update(float deltaTime) {`
- `public Vector3 GetCurrentDirection() => currentDirection;`
- `protected void ApplyVisualRotation()
    {`
### `Assets/Script/Character/Core/BasicFunction/Orientation/FacingConstraintController.cs`
- `public class FacingConstraintController
{`
- `public FacingConstraintController(
        CharacterDataBus dataBus,
        float defaultWeaponReleaseDelay)
    {`
- `public void Tick(float deltaTime)
    {`
- `public void NotifyWeaponFacingActivity(float releaseDelay = -1f)
    {`
- `public void ClearWeaponFacingLock()
    {`
- `public bool HasWeaponFacingOwnership => WeaponFacingPolicy.HasWeaponFacingOwnership(dataBus);`
### `Assets/Script/Character/Core/BasicFunction/Orientation/FacingController.cs`
- `public class FacingController
{`
- `public bool IsMoveTurnPending {`
- `public int StableSign => stableSign;`
- `public int PendingSign => pendingSign;`
- `public FacingController(
        CharacterDataBus dataBus,
        Transform visualFlipRoot,
        float moveFlipConfirmTime,
        float moveFlipConfirmTimeAfterWeapon,
        float weaponMoveTurnSlowWindow,
        float flipCooldownTime,
        float moveTurnMovementLockDuration)
    {`
- `public void Tick(float deltaTime)
    {`
- `public void ReapplyVisualOnly()
    {`
### `Assets/Script/Character/Core/BasicFunction/Orientation/HunterGunCursorFacingController.cs`
- `public class HunterGunCursorFacingController : MonoBehaviour, ICharacterSubsystem,ICharacterLateTickable
{`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedLateUpdate(float deltaTime)
    {`
### `Assets/Script/Character/Core/BasicFunction/Orientation/IOrientationController.cs`
- `public interface IOrientationController
{`
### `Assets/Script/Character/Core/BasicFunction/Orientation/LockTargetController_Hound.cs`
- `public class LockTargetIKController : MonoBehaviour, ICharacterSubsystem,ICharacterLateTickable
{`
- `public bool HasValidLockTarget
    {`
- `public Vector3 CurrentIKWorldPosition => currentIKWorldPosition;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedLateUpdate(float deltaTime)
{`
- `public void ForceRefresh()
    {`
### `Assets/Script/Character/Core/BasicFunction/Orientation/MouseOrientationController.cs`
- `public class MouseOrientationController : BaseOrientationController
{`
- `public override void Update(float deltaTime)
    {`
- `public override void OrientMethod()
    {`
- `public override void UpdateOrientation(float deltaTime)
    {`
### `Assets/Script/Character/Core/BasicFunction/Orientation/OrientationSystem.cs`
- `public class OrientationSystem : MonoBehaviour, ICharacterSubsystem,ICharacterTickable,ICharacterLateTickable
{`
- `public enum OrientationType
    {`
- `public bool IsMoveTurnPending
    {`
- `public int StableFacingSign
    {`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedUpdate(float deltaTime)
    {`
- `public void ManagedLateUpdate(float deltaTime)
    {`
- `public void NotifyWeaponFacingActivity(float releaseDelay = -1f)
    {`
### `Assets/Script/Character/Core/BasicFunction/Orientation/WeaponFacingPolicy.cs`
- `public static class WeaponFacingPolicy
{`
- `public static bool CanWeaponControlFacing(CharacterDataBus bus)
    {`
- `public static bool HasAttackFacingRequest(CharacterDataBus bus)
    {`
- `public static bool HasAimFacingRequest(CharacterDataBus bus)
    {`
- `public static bool HasWeaponFacingLease(CharacterDataBus bus)
    {`
- `public static bool HasWeaponFacingOwnership(CharacterDataBus bus)
    {`
### `Assets/Script/Character/Core/CharacterDataBus.cs`
- `public class CharacterDataBus
{`
- `public enum InputDevice_AimType//瞄准设备类型枚举
    {`
- `public enum RequestedFacingSource
    {`
- `public string characterName;`
- `public CharacterSO characterSO;`
- `public Vector3 aimCursorWorldPosition;`
- `public Vector3 aimDirection;`
- `public bool aimInputActiveThisFrame;`
- `public bool hunterAttackHeld;`
- `public bool hunterAttackPressedThisFrame;`
- `public bool houndAttackHeld;`
- `public bool houndAttackPressedThisFrame;`
- `public bool aimHeld;`
- `public bool aimPressedThisFrame;`
- `public bool aimReleasedThisFrame;`
- `public bool reloadPressedThisFrame;`
- `public bool attackPressed
    {`
- `public Vector2 moveInput {`
- `public float moveInputAmount
    {`
- `public bool hasMoveInput
    {`
- `public float currentMoveSpeed {`
- `public bool isMovementActive
    {`
- `public bool mouseMoving {`
- `public InputDevice_AimType currentInputDevice {`
- `public bool jumpPressed {`
- `public bool jumpHeld {`
- `public bool jumpPressedThisFrame {`
- `public bool jumpReleasedThisFrame {`
- `public float lastGroundedTime {`
- `public float lastJumpPressedTime {`
- `public bool dashPressed {`
- `public bool avoidPressed {`
- `public float gamepadMouseControlSensitivity {`
- `public bool aimInputTypeIsTriger {`
- `public bool crouchInputTypeIsTriger {`
- `public bool forceSlowWalkInputTypeIsTriger {`
- `public bool isForceWalkSlow {`
- `public Vector3 characterPosition {`
- `public Vector3 mousePositionOnScreen {`
- `public bool isGrounded {`
- `public Vector3 facingDirection{`
- `public Facing currentFacing;`
- `public Facing targetFacing;`
- `public bool isFacingChanging;`
- `public int facingChangeId;`
- `public float moveTurnMovementLockUntilTime;`
- `public int moveTurnMovementLockId;`
- `public bool isMoveTurnMovementLocked
    {`
- `public Vector3 requestedFacingDirection {`
- `public RequestedFacingSource requestedFacingSource = RequestedFacingSource.None;`
- `public BattleCamp battleCamp {`
- `public Facing assetDefaultFacing;`
- `public float FinalVisualScaleX => (battleCamp == BattleCamp.MateSide)
        ? (assetDefaultFacing == Facing.Right ? 1f : -1f) // 友方阵营与原图朝向一致则不翻转(1)，否则翻转(-1)
        : (assetDefaultFacing == Facing.Right ? -1f : 1f);`
- `public float currentHp;`
- `public float maxRemainHp;`
- `public float currentRemainHp;`
- `public float stamina = 100f;`
- `public bool isChangeToRemain;`
- `public bool isCompleteDisappear = false;`
- `public bool isSlotForceChange;`
- `public bool hasNewStatus;`
- `public CharacterStatProfileSO statProfile;`
- `public int level = 1;`
- `public WeaponRuntimeData currentWeaponRuntimeData;`
- `public CharacterWeaponInventory weaponInventory;`
- `public bool hasAttackTarget = false;`
- `public LockTargetPoint lockedAttackTarget;`
- `public EnemyRoot lockedAttackTargetRoot;`
- `public Vector3 attackTargetWorldPosition;`
- `public AnimationPlanMappingSO basicAnimationPlan;`
- `public List<AnimationPlanOverrideRuleSetSO> overrideAnimationPlan;`
- `public int displayHp => Mathf.CeilToInt(currentHp);`
- `public bool isAlive = true;`
- `public bool isMoving {`
- `public bool isMovingBackward {`
- `public bool isCrouch {`
- `public bool isAiming;`
- `public bool isReloading;`
- `public bool isHoundAttacking;`
- `public bool isHunterAttacking;`
- `public bool isCharacterAttacking => isHoundAttacking || isHunterAttacking;`
- `public float lastCombatIntentTime;`
- `public float autoHolsterDelay = 2.5f;`
- `public bool canMove {`
- `public bool canJump {`
- `public HoundCombatActionType currentHoundCombatAction = HoundCombatActionType.None;`
- `public EState currentMovementState {`
- `public EState currentJumpState {`
- `public bool blocksMoveFacingByJump
    {`
- `public WeaponEquipState weaponEquipState = WeaponEquipState.Cancelled;`
- `public WeaponPostureState weaponPostureState = WeaponPostureState.None;`
- `public WeaponSwitchInputRequestType weaponSwitchInputRequestType =WeaponSwitchInputRequestType.None;`
- `public WeaponSlotId requestedWeaponSlot =WeaponSlotId.Slot1;`
- `public bool isWeaponEquipTransitioning
    {`
- `public bool canUseWeapon
    {`
- `public bool isInSafeZone;`
- `public bool weaponToggleLongPressedThisFrame;`
- `public bool requestCancelWeapon;`
- `public int comboCount {`
- `public float lastAttackTime {`
- `public bool blockMoveFacingByWeapon;`
- `public float blockMoveFacingExpireTime;`
- `public float lastWeaponFacingActivityTime;`
- `public Vector3 lastWeaponFacingDirection {`
- `public bool hasLastWeaponFacingDirection {`
- `public void LoadFromSO(CharacterSO so)
    {`
- `public void RequestWeaponSlotSwitch(
        WeaponSlotId slotId)
    {`
- `public void RequestWeaponSlotCycleNext()
    {`
- `public bool TryConsumeWeaponSwitchInput(
        out WeaponSwitchInputRequestType requestType,
        out WeaponSlotId requestedSlot)
    {`
### `Assets/Script/Character/Core/CharacterRoot.cs`
- `public class CharacterRoot : MonoBehaviour
{`
- `public CharacterDataBus characterDataBus {`
- `public Spine.Skeleton houndSkeleton =>
        houndSkeletonAnimation != null ? houndSkeletonAnimation.Skeleton : null;`
- `public Spine.Skeleton hunterGunSkeleton =>
        hunterGunSkeletonAnimation != null ? hunterGunSkeletonAnimation.Skeleton : null;`
- `public SkeletonAnimation skeletonAnimation => houndSkeletonAnimation;`
- `public Spine.Skeleton skeleton =>
        houndSkeletonAnimation != null ? houndSkeletonAnimation.Skeleton : null;`
- `public void BootstrapOnly()//第一步为准备角色数据外壳
    {`
- `public void InitializeWithSO(CharacterSO so)
    {`
- `public void AutoGetComponent()
    {`
- `public void LoadCharacterDataFromSO(CharacterSO so)
    {`
### `Assets/Script/Character/Core/CharacterSO.cs`
- `public class CharacterSO : ScriptableObject
{`
- `public Sprite characterIcon;`
- `public CharacterStatProfileSO characterStatProfileSO;`
- `public SkeletonDataAsset chracterModel;`
- `public AnimationPlanMappingSO basicAnimationPlanMapping;`
- `public List<AnimationPlanOverrideRuleSetSO> animationPlanOverrideRuleSet;`
- `public string characterName;`
- `public BattleCamp battleCamp;`
- `public Facing assetDefaultFacing = Facing.Left;`
- `public float maxHp = 100f;`
- `public float maxRemainHp = 10f;`
### `Assets/Script/Character/Core/CharacterStateRequest.cs`
- `public class CharacterStateRequest : StateRequest
{`
- `public CharacterStateRequest(CharacterStateType type,bool isUrgent = false,object data = null,System.Action onActionPoint = null) : base()
    {`
- `public void ExecuteCombat()
    {`
### `Assets/Script/Character/Core/CombatActionData.cs`
- `public class CombatActionData
{`
- `public CharacterStateType Skill {`
- `public CharacterRoot Target;`
### `Assets/Script/Character/Core/ECharacter.cs`
- `public enum Facing {`
- `public enum WeaponDefaultFacing
{`
- `public enum WeaponSwitchInputRequestType
{`
- `public enum CombatDamageValueMode
{`
- `public enum AnimationLayer
{`
- `public enum AnimationIntentSourceStateMachine
{`
- `public enum WeaponPostureState // 武器姿态
{`
- `public enum EAnimSemantic // 动画逻辑 ID，状态机用于标记状态
{`
- `public enum BattleCamp
{`
- `public enum EnemyRank
{`
- `public enum WeaponEquipState
{`
- `public enum EAnimationLogicState // 动画逻辑 ID，对应角色 Spine 中包含的动画名称
{`
- `public enum EState
{`
- `public enum CharacterStateType // 有多少个状态就写多少个枚举，需要和实际的状态脚本保持一致
{`
- `public enum CharacterStats
{`
- `public enum StatModifierMode
{`
- `public enum ActionEffectType
{`
- `public enum MoveType
{`
- `public enum WeaponType
{`
- `public enum FirearmCategory
{`
- `public enum GunFireModeType
{`
- `public enum GunHitType
{`
- `public enum HoundCombatActionType
{`
- `public enum TransitionSuppressReason
{`
- `public enum WeaponSlotId
{`
### `Assets/Script/Character/Core/ICharacterFixedTickable.cs`
- `public interface ICharacterFixedTickable
{`
### `Assets/Script/Character/Core/ICharacterLateTickable.cs`
- `public interface ICharacterLateTickable
{`
### `Assets/Script/Character/Core/ICharacterStateMachine.cs`
- `public interface ICharacterStateMachine
{`
### `Assets/Script/Character/Core/ICharacterSubsystem.cs`
- `public interface ICharacterSubsystem
{`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Character/Core/BasicFunction/Jump/JumpStateMachine.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 跳跃状态机。直接有限状态机，不再使用 StateStackManager。
/// 集成土狼时间、输入缓存、局部重力坐标和地面探测。
/// </summary>
public class JumpStateMachine : ICharacterStateMachine, IAnimationProvider
{
    private readonly Dictionary<EState, JumpStateBase> states = new();
    private JumpStateBase currentState;

    private CharacterDataBus dataBus;
    private Transform transform;
    private Rigidbody2D rigidbody2D;
    private CharacterRoot characterRoot;
    private CharacterAnimationConfigManager configMgr;
    private CharacterAnimationComposer _animationComposer;

    // 配置与探测
    private JumpConfigSO _config;
    private JumpGroundProbe _groundProbe;
    private Collider2D _collider;
    private LayerMask _groundLayerMask;

    // Coyote Time / Jump Buffer
    private bool _jumpConsumed;

    // 防止起跳瞬间误判地面：必须确实离开地面后才允许进入 Land
    public bool HasLeftGround => _hasLeftGround;
    private bool _hasLeftGround;

    // 动画去重
    private EAnimationLogicState lastLogicID;

    public string StateMachineName => "JumpStateMachine";
    public int Priority => 20;
    public string CurrentStateName => currentState?.StateName ?? "None";
    public bool IsActive => currentState != null;

    /// <summary>暴露给子状态使用的配置</summary>
    public JumpConfigSO Config => _config;
    /// <summary>暴露地面探测给子状态</summary>
    public JumpGroundProbe GroundProbe => _groundProbe;
    /// <summary>重力方向</summary>
    public Vector2 GravityDown => Vector2.down;
    public Vector2 GravityUp => Vector2.up;
    public Vector2 LateralAxis => Vector2.right;

    // ============ 初始化 ============
    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        this.dataBus = bus;
        this.rigidbody2D = rb;
        this.transform = tf;
        this.characterRoot = root;

        if (dataBus == null)
        {
            Debug.LogError("[JumpStateMachine] 缺少 CharacterDataBus");
            return;
        }

        configMgr = characterRoot.GetComponent<CharacterAnimationConfigManager>();
        if (configMgr == null)
            Debug.LogError("[JumpStateMachine] Missing CharacterAnimationConfigManager");

        _animationComposer = characterRoot.GetComponent<CharacterAnimationComposer>();

        // 获取配置
        _config = characterRoot.jumpConfig;
        if (_config == null)
        {
            Debug.LogWarning("[JumpStateMachine] jumpConfig 未配置，使用默认值");
        }

        // 获取 Collider
        _collider = characterRoot.GetComponent<Collider2D>();
        if (_collider == null)
            _collider = characterRoot.GetComponentInChildren<Collider2D>();

        // 获取地面层
        _groundLayerMask = characterRoot.groundLayerMask;

        // 创建地面探测器
        if (_collider != null && _config != null)
        {
            _groundProbe = new JumpGroundProbe();
            _groundProbe.Initialize(_collider, _groundLayerMask, _config);
        }

        // gravityScale 已在 Inspector 中设为 0，由自定义重力接管

        InitializeAllStates();
    }

    private void InitializeAllStates()
    {
        var startup = new Jump_Start();
        var rising = new Jump_Rising();
        var falling = new Jump_Fall();
        var landing = new Jump_Land();

        var allStates = new JumpStateBase[] { startup, rising, falling, landing };
        foreach (var s in allStates)
        {
            s.Initialize(this, dataBus, rigidbody2D);
            s.SetGravityDirection(GravityDown);
            states[s.StateKey] = s;
        }
    }

    // ============ ICharacterStateMachine 接口 ============
    public void Enter()
    {
        // 提交高优先级动画意图，覆盖 Movement 的 Track 0
        SubmitJumpAnimationIntent();
    }

    public void Exit()
    {
        if (currentState != null)
        {
            currentState.Exit();
            currentState = null;
            dataBus.currentJumpState = EState.Jump_None;
        }

        // 移除跳跃动画意图，让 Movement 恢复
        if (_animationComposer != null)
            _animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.Jump);

        lastLogicID = EAnimationLogicState.Idle;
    }

    public bool RequestState(object stateData)
    {
        if (IsActive)
            return false;

        // 记录跳跃按键时间（用于 Jump Buffer）
        dataBus.lastJumpPressedTime = Time.time;

        // 检查是否可以起跳
        bool canJumpNow = dataBus.isGrounded || HasCoyoteTime;

        if (canJumpNow && !_jumpConsumed)
        {
            _jumpConsumed = true;
            _hasLeftGround = false;
            TransitionTo(EState.Jump_Start);
            return true;
        }

        // 不能立即起跳，但输入已缓存（等落地后自动起跳）
        return false;
    }

    // ============ Coyote Time / Jump Buffer ============
    public bool HasCoyoteTime
    {
        get
        {
            if (_config == null) return false;
            return Time.time - dataBus.lastGroundedTime <= _config.coyoteTime;
        }
    }

    public bool HasBufferedJump
    {
        get
        {
            if (_config == null) return false;
            return Time.time - dataBus.lastJumpPressedTime <= _config.jumpBufferTime;
        }
    }

    /// <summary>消费跳跃缓存</summary>
    public void ConsumeJump()
    {
        _jumpConsumed = true;
    }

    /// <summary>重置跳跃消费状态（落地时调用）</summary>
    public void ResetJumpConsumption()
    {
        _jumpConsumed = false;
    }

    /// <summary>落地吸附：将角色移动到实际地面命中位置</summary>
    public void SnapToGround()
    {
        if (rigidbody2D == null || _groundProbe == null)
            return;

        float distance = _groundProbe.GroundDistance;

        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f)
            return;

        rigidbody2D.position += GravityDown * distance;
    }

    // ============ 状态转换 ============
    public void TransitionTo(EState stateKey)
    {
        if (!states.TryGetValue(stateKey, out var next))
        {
            Debug.LogWarning($"[JumpStateMachine] 状态未注册: {stateKey}");
            return;
        }

        currentState?.Exit();
        currentState = next;
        dataBus.currentJumpState = stateKey;
        currentState.Enter();

        // 状态切换后重新提交动画意图（语义可能改变）
        SubmitJumpAnimationIntent();
    }

    public void CompleteJump()
    {
        currentState?.Exit();
        currentState = null;
        dataBus.currentJumpState = EState.Jump_None;
        ResetJumpConsumption();

        if (_animationComposer != null)
            _animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.Jump);
        lastLogicID = EAnimationLogicState.Idle;
    }

    /// <summary>因配置缺失等原因取消跳跃，直接回退到非活跃状态</summary>
    public void CancelJump(string reason)
    {
        Debug.LogWarning($"[Jump] CancelJump: {reason}");
        currentState?.Exit();
        currentState = null;
        dataBus.currentJumpState = EState.Jump_None;

        if (_animationComposer != null)
            _animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.Jump);
        lastLogicID = EAnimationLogicState.Idle;
    }

    // ============ 每帧更新 ============
    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        if (currentState == null)
            return;

        // 执行地面探测（跳跃期间也是唯一写入者）
        if (_groundProbe != null)
        {
            _groundProbe.Probe();
            dataBus.isGrounded = _groundProbe.IsGrounded;
            if (dataBus.isGrounded)
                dataBus.lastGroundedTime = Time.time;

            // 记录确实离开过地面（防止起跳瞬间误判）
            if (!dataBus.isGrounded)
                _hasLeftGround = true;
        }

        currentState.FixedUpdate(fixedDeltaTime);
    }

    // ============ 中断处理 ============
    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return other.Priority > Priority;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }

    // ============ 动画语义 ============
    private void SubmitJumpAnimationIntent()
    {
        if (_animationComposer == null)
            return;

        // 使用 exclusiveFullBody 抑制 Movement 的 Track 0 动画
        // 由于当前没有真实跳跃动画，使用 Idle 语义作为占位
        // 高优先级 + exclusiveFullBody 会覆盖 Movement 的 BaseFullBody
        _animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.Jump,
            semantic = dataBus.currentJumpState switch
            {
                EState.Jump_Start => EAnimSemantic.Jump_Start,
                EState.Jump_Rising => EAnimSemantic.Jump_Rising,
                EState.Jump_Fall => EAnimSemantic.Jump_Falling,
                EState.Jump_Land => EAnimSemantic.Jump_Landing,
                _ => EAnimSemantic.Jump_Rising
            },
            layer = AnimationLayer.BaseFullBody,
            priority = 25,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        });
    }

    public void OnEnterAnimation()
    {
        // 跳跃期间持续持有动画意图，由 Enter/Exit 管理提交和移除
        // 子状态切换不需要重新提交（同一意图持续活跃）
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/BaseOrientationController.cs`

```cs
using UnityEngine;

public abstract class BaseOrientationController : IOrientationController
{
    protected Vector3 currentDirection;
    protected Transform visualTransform;
    protected CharacterDataBus dataBus;

    public virtual void Initialize(CharacterDataBus bus, Transform visualTr)
    {
        visualTransform = visualTr;
        dataBus = bus;

        if (dataBus == null)
            Debug.LogError("BaseOrientationController: dataBus 为 null!");

        if (visualTransform == null)
            Debug.LogError("BaseOrientationController: visualTransform 为 null!");
    }

    public abstract void UpdateOrientation(float deltaTime);
    public abstract void OrientMethod();

    public virtual void Update(float deltaTime) { }

    public Vector3 GetCurrentDirection() => currentDirection;

    protected void ApplyVisualRotation()
    {
        //if (visualTransform == null || dataBus == null)
        //    return;

        //// 你的旧逻辑：Vector3.left 对应 0，Vector3.right 对应 180
        //float targetAngle = dataBus.facingDirection == Vector3.left ? 0f : 180f;

        //Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);

        //// 只旋转视觉节点，不旋转 CharacterRoot
        //visualTransform.localRotation = targetRotation;
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/FacingConstraintController.cs`

```cs
using UnityEngine;
using static CharacterDataBus;

/// <summary>
/// 武器朝向约束控制器。
/// 使用 WeaponFacingPolicy 统一判断武器是否拥有朝向所有权。
/// 维护 blockMoveFacingByWeapon 租约。
/// </summary>
public class FacingConstraintController
{
    private readonly CharacterDataBus dataBus;
    private readonly float defaultWeaponReleaseDelay;

    public FacingConstraintController(
        CharacterDataBus dataBus,
        float defaultWeaponReleaseDelay)
    {
        this.dataBus = dataBus;
        this.defaultWeaponReleaseDelay = Mathf.Max(0.01f, defaultWeaponReleaseDelay);
    }

    public void Tick(float deltaTime)
    {
        if (dataBus == null)
            return;

        UpdateWeaponMoveFacingLock();
    }

    public void NotifyWeaponFacingActivity(float releaseDelay = -1f)
    {
        if (dataBus == null)
            return;

        dataBus.lastWeaponFacingActivityTime = Time.time;

        float delay = releaseDelay > 0f
            ? releaseDelay
            : defaultWeaponReleaseDelay;

        dataBus.blockMoveFacingExpireTime = Mathf.Max(
            dataBus.blockMoveFacingExpireTime,
            Time.time + Mathf.Max(0.01f, delay)
        );
    }

    public void ClearWeaponFacingLock()
    {
        if (dataBus == null)
            return;

        dataBus.blockMoveFacingExpireTime = 0f;
        dataBus.blockMoveFacingByWeapon = false;
    }

    /// <summary>
    /// 武器是否拥有朝向所有权（统一判断）。
    /// </summary>
    public bool HasWeaponFacingOwnership => WeaponFacingPolicy.HasWeaponFacingOwnership(dataBus);

    private void UpdateWeaponMoveFacingLock()
    {
        // 使用统一的武器活跃判断
        if (IsWeaponInputActive() || IsWeaponStateActive())
            NotifyWeaponFacingActivity(defaultWeaponReleaseDelay);

        dataBus.blockMoveFacingByWeapon =
            Time.time < dataBus.blockMoveFacingExpireTime;
    }

    private bool IsWeaponInputActive()
    {
        return dataBus.aimHeld;
    }

    private bool IsWeaponStateActive()
    {
        return dataBus.isAiming
            || dataBus.currentHoundCombatAction == HoundCombatActionType.Aim;
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/FacingController.cs`

```cs
using UnityEngine;
//读取 requestedFacingSource
//判断是否需要即时翻转，还是走移动方向确认
//写：
//currentFacing
//targetFacing
//isFacingChanging
//facingDirection
//moveSpeed
public class FacingController
{
    private readonly Transform visualFlipRoot;
    private readonly CharacterDataBus dataBus;
    public bool IsMoveTurnPending { get; private set; }//是否有移动翻转 pending
    private readonly float moveTurnMovementLockDuration;


    private int stableSign = 1;//当前稳定朝向
    private int pendingSign = 1;//目标/待确认朝向

    public int StableSign => stableSign;
    public int PendingSign => pendingSign;

    private float pendingTimer;
    private float flipCooldownTimer;

    private int lastAppliedSign = 999;

    private readonly float moveFlipConfirmTime;
    private readonly float flipCooldownTime;
    private readonly float moveFlipConfirmTimeAfterWeapon;
    private readonly float weaponMoveTurnSlowWindow;



    public FacingController(
        CharacterDataBus dataBus,
        Transform visualFlipRoot,
        float moveFlipConfirmTime,
        float moveFlipConfirmTimeAfterWeapon,
        float weaponMoveTurnSlowWindow,
        float flipCooldownTime,
        float moveTurnMovementLockDuration)
    {
        this.dataBus = dataBus;
        this.visualFlipRoot = visualFlipRoot;
        this.moveFlipConfirmTime = moveFlipConfirmTime;
        this.moveFlipConfirmTimeAfterWeapon = moveFlipConfirmTimeAfterWeapon;
        this.weaponMoveTurnSlowWindow = weaponMoveTurnSlowWindow;
        this.flipCooldownTime = flipCooldownTime;
        this.moveTurnMovementLockDuration = moveTurnMovementLockDuration;

        stableSign = GetSign(dataBus.facingDirection);
        pendingSign = stableSign;

        dataBus.facingDirection = stableSign > 0 ? Vector3.right : Vector3.left;
        dataBus.requestedFacingDirection = dataBus.facingDirection;
        dataBus.requestedFacingSource = CharacterDataBus.RequestedFacingSource.None;

        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

        ApplyVisualFacing(stableSign, true);
    }



    public void Tick(float deltaTime)
    {
        if (dataBus == null || visualFlipRoot == null)
            return;

        if (flipCooldownTimer > 0f)
            flipCooldownTimer -= deltaTime;

        int requestedSign = GetSign(dataBus.requestedFacingDirection);
        CharacterDataBus.RequestedFacingSource source = dataBus.requestedFacingSource;

        switch (source)
        {
            case CharacterDataBus.RequestedFacingSource.Attack:
            case CharacterDataBus.RequestedFacingSource.Aim:
            case CharacterDataBus.RequestedFacingSource.Target:
                TickInstantFacing(requestedSign);
                break;

            case CharacterDataBus.RequestedFacingSource.Move:
                TickMoveFacing(requestedSign, deltaTime);
                break;

            case CharacterDataBus.RequestedFacingSource.None:
            default:
                TickKeepFacing();
                break;
        }
    }

    private void TickInstantFacing(int requestedSign)
    {
        /*
         * 攻击 / 瞄准 / 锁定目标：
         * 这里是强制改变朝向，不走 Move 的延迟方向逻辑。
         */
        IsMoveTurnPending = false;

        AcceptFacingImmediately(requestedSign);

        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);
    }

    private void TickMoveFacing(int requestedSign, float deltaTime)
    {
        /*
         * 移动方向和当前稳定朝向一致时，
         * 不需要翻转，也不用进入 pending。
         */
        if (requestedSign == stableSign)
        {
            IsMoveTurnPending = false;
            ClearPending();

            WriteStableFacing();
            WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

            ApplyVisualFacing(stableSign, false);
            return;
        }

        /*
         * 冷却期：
         * 刚发生翻转，不允许立即翻转
         */
        if (flipCooldownTimer > 0f)
        {
            IsMoveTurnPending = true;

            WriteStableFacing();

            // 当前朝向是 stableSign，目标是 requestedSign
            WriteFacingRuntimeToDataBus(stableSign, requestedSign, true);

            ApplyVisualFacing(stableSign, false);
            return;
        }

        /*
         * 方向与稳定朝向不同，进入移动方向确认期。
         */
        IsMoveTurnPending = true;

        if (pendingSign != requestedSign)
        {
            pendingSign = requestedSign;
            pendingTimer = 0f;
            dataBus.facingChangeId++;

            BeginMoveTurnMovementLock();

        }

        pendingTimer += deltaTime;

        /*
         * 注意：
         * 确认期间，当前朝向保持 stableSign，
         * 目标朝向是 pendingSign，
         * 这是为了动画系统需要播放 TurnAround 过渡动画。
         */
        WriteFacingRuntimeToDataBus(stableSign, pendingSign, true);

        float confirmTime = GetCurrentMoveFlipConfirmTime();

        if (pendingTimer >= confirmTime)
        {
            AcceptFacingImmediately(requestedSign);

            WriteFacingRuntimeToDataBus(stableSign, stableSign, false);
            return;
        }

        /*
         * 尚未确认前，保持稳定朝向
         */
        WriteStableFacing();
        ApplyVisualFacing(stableSign, false);
    }

    private float GetCurrentMoveFlipConfirmTime()
    {
        if (dataBus == null)
            return moveFlipConfirmTime;

        bool recentlyUsedWeaponFacing =
            Time.time - dataBus.lastWeaponFacingActivityTime <= weaponMoveTurnSlowWindow;

        if (recentlyUsedWeaponFacing)
            return moveFlipConfirmTimeAfterWeapon;

        return moveFlipConfirmTime;
    }

    private void TickKeepFacing()
    {
        /*
         * 无明确输入时，保持稳定朝向
         */
        IsMoveTurnPending = false;

        ClearPending();

        WriteStableFacing();
        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

        ApplyVisualFacing(stableSign, false);
    }

    private void AcceptFacingImmediately(int sign)
    {
        if (sign == 0)
            return;

        IsMoveTurnPending = false;

        bool changed = stableSign != sign;

        stableSign = sign;
        pendingSign = sign;
        pendingTimer = 0f;

        if (changed)
            flipCooldownTimer = flipCooldownTime;

        WriteStableFacing();
        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

        ApplyVisualFacing(stableSign, false);
    }

    private void ClearPending()
    {
        pendingSign = stableSign;
        pendingTimer = 0f;
    }

    private void WriteStableFacing()
    {
        dataBus.facingDirection = stableSign > 0
            ? Vector3.right
            : Vector3.left;
    }

    private int GetSign(Vector3 direction)
    {
        return direction.x >= 0f ? 1 : -1;
    }

    private void ApplyVisualFacing(int sign, bool force)
    {
        if (visualFlipRoot == null)
            return;

        if (!force && lastAppliedSign == sign)
            return;

        lastAppliedSign = sign;

        Vector3 scale = visualFlipRoot.localScale;

        float absX = Mathf.Abs(scale.x);
        float absY = Mathf.Abs(scale.y);
        float absZ = Mathf.Abs(scale.z);

        /*
         * 翻转成功后：
         * left  -> Y = 0
         * right -> Y = 180
         *
         * 说明：模型默认朝向
         *
         * 因此：
         * sign < 0 左：scale.x = 正
         * sign > 0 右：scale.x = 负
         */
        scale.x = sign > 0 ? -absX : absX;
        scale.y = absY;
        scale.z = absZ;

        visualFlipRoot.localScale = scale;

        // 骨节点只用做 scale 翻转，不做 Y 轴旋转
        visualFlipRoot.localRotation = Quaternion.identity;
    }


    public void ReapplyVisualOnly()
    {
        ApplyVisualFacing(stableSign, true);
    }

    private Facing SignToFacing(int sign)
    {
        return sign >= 0 ? Facing.Right : Facing.Left;
    }


    private void WriteFacingRuntimeToDataBus(int currentSign, int targetSign, bool isChanging)
    {
        if (dataBus == null)
            return;

        dataBus.currentFacing = SignToFacing(currentSign);
        dataBus.targetFacing = SignToFacing(targetSign);
        dataBus.isFacingChanging = isChanging;
    }

    private void BeginMoveTurnMovementLock()
    {
        if (dataBus == null)
            return;

        dataBus.moveTurnMovementLockId++;
        dataBus.moveTurnMovementLockUntilTime = Mathf.Max(
            dataBus.moveTurnMovementLockUntilTime,
            Time.time + moveTurnMovementLockDuration
        );
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/HunterGunCursorFacingController.cs`

```cs
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
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/IOrientationController.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IOrientationController
{
    //void Initialize(CharacterDataBus dataBus);
    void Update(float deltaTime);
    Vector3 GetCurrentDirection();
    //bool ShouldOverrideOtherControllers();
}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/LockTargetController_Hound.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;

public class LockTargetIKController : MonoBehaviour, ICharacterSubsystem,ICharacterLateTickable
{
    [Header("Runtime References")]
    [SerializeField] private CharacterRoot characterRoot;
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [Header("Spine Bone")]
    [Tooltip("Spine 中锁定目标用的 IK 骨骼名称。必须和 Spine 里完全一致")]
    [SerializeField] private string ikAimBoneName_Hund = "IK_Aim_Hound";

    [Header("Target Offset")]
    [Tooltip("世界坐标偏移。用于微调 IK_Hund 对准 LockPoint 后的位置")]
    [SerializeField] private Vector2 worldOffset = Vector2.zero;

    [Tooltip("Skeleton 坐标偏移。用于修正 Spine 骨骼坐标")]
    [SerializeField] private Vector2 skeletonSpaceOffset = Vector2.zero;

    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;

    [Tooltip("IK_Hund 追随 LockPoint 的速度")]
    [SerializeField] private float followSpeed = 30f;

    [Header("When No Target")]
    [Tooltip("没有锁定目标时，是否保持上一帧 IK_Hund 位置")]
    [SerializeField] private bool holdLastPositionWhenNoTarget = false;

    [Tooltip("没有锁定目标且不保持时，IK_Hund 是否回到角色自身位置")]
    [SerializeField] private bool resetToCharacterPositionWhenNoTarget = false;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool logRefresh = false;

    private CharacterDataBus dataBus;
    private Skeleton cachedSkeleton;
    private Bone ikHundBone;

    private Vector2 currentIKWorldPosition;
    private bool hasCurrentIKWorldPosition;
    private bool _isInitialized;

    public bool HasValidLockTarget
    {
        get
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
    }

    public Vector3 CurrentIKWorldPosition => currentIKWorldPosition;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        if (characterRoot != null)
        {
            dataBus = characterRoot.characterDataBus;

            if (skeletonAnimation == null)
                skeletonAnimation = characterRoot.houndSkeletonAnimation;
        }

        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);

        RefreshSpineReferences("Initialize");

        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();

        if (characterRoot != null)
        {
            dataBus = characterRoot.characterDataBus;

            if (skeletonAnimation == null)
                skeletonAnimation = characterRoot.houndSkeletonAnimation;
        }

        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);

        RefreshSpineReferences("Awake");
    }

    private void Start()
    {
        RefreshSpineReferences("Start");
    }

    public void ManagedLateUpdate(float deltaTime)
{
        ApplyLockTargetIK();
    }


    public void ForceRefresh()
    {
        RefreshSpineReferences("ForceRefresh");
    }

    private void ApplyLockTargetIK()
    {
        if (characterRoot != null && dataBus == null)
            dataBus = characterRoot.characterDataBus;

        if (skeletonAnimation == null)
            return;

        Skeleton currentSkeleton = skeletonAnimation.Skeleton;

        if (currentSkeleton == null)
            return;

        if (cachedSkeleton != currentSkeleton)
        {
            RefreshSpineReferences("Skeleton changed");
        }

        if (ikHundBone == null)
            return;

        Vector2 targetWorldPosition;
        bool hasTarget = TryGetLockTargetWorldPosition(out targetWorldPosition);

        if (!hasTarget)
        {
            if (holdLastPositionWhenNoTarget && hasCurrentIKWorldPosition)
                targetWorldPosition = currentIKWorldPosition;
            else if (resetToCharacterPositionWhenNoTarget)
                targetWorldPosition = transform.position;
            else
                return;
        }

        if (!hasCurrentIKWorldPosition)
        {
            currentIKWorldPosition = targetWorldPosition;
            hasCurrentIKWorldPosition = true;
        }
        else
        {
            if (enableSmoothing)
            {
                float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
                currentIKWorldPosition = Vector2.Lerp(currentIKWorldPosition, targetWorldPosition, t);
            }
            else
            {
                currentIKWorldPosition = targetWorldPosition;
            }
        }

        Vector3 skeletonLocal3 = skeletonAnimation.transform.InverseTransformPoint(currentIKWorldPosition);
        Vector2 targetSkeletonSpace = new Vector2(skeletonLocal3.x, skeletonLocal3.y);
        targetSkeletonSpace += skeletonSpaceOffset;

        ikHundBone.SetPositionSkeletonSpace(targetSkeletonSpace);

        currentSkeleton.UpdateWorldTransform(Spine.Physics.Pose);
    }

    private bool TryGetLockTargetWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = Vector2.zero;

        if (dataBus == null)
            return false;

        if (!dataBus.hasAttackTarget)
            return false;

        if (dataBus.lockedAttackTarget == null)
            return false;

        if (!dataBus.lockedAttackTarget.IsValid())
            return false;

        worldPosition = dataBus.lockedAttackTarget.GetWorldPosition();
        worldPosition += worldOffset;

        return true;
    }

    private void RefreshSpineReferences(string reason)
    {
        if (skeletonAnimation == null)
        {
            if (characterRoot != null)
                skeletonAnimation = characterRoot.houndSkeletonAnimation;

            if (skeletonAnimation == null)
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (skeletonAnimation == null)
        {
            cachedSkeleton = null;
            ikHundBone = null;
            return;
        }

        cachedSkeleton = skeletonAnimation.Skeleton;

        if (cachedSkeleton == null)
        {
            ikHundBone = null;
            return;
        }

        ikHundBone = cachedSkeleton.FindBone(ikAimBoneName_Hund);

        hasCurrentIKWorldPosition = false;

        if (logRefresh)
        {
            Debug.Log(
                $"[LockTargetIKController] RefreshSpineReferences: {reason}, " +
                $"SkeletonHash={cachedSkeleton.GetHashCode()}, " +
                $"Bone={(ikHundBone != null ? ikHundBone.Data.Name : "NULL")}",
                this
            );
        }

        if (ikHundBone == null)
        {
            Debug.LogWarning(
                $"[LockTargetIKController] 找不到 Spine Bone: {ikAimBoneName_Hund}",
                this
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        if (!Application.isPlaying)
            return;

        if (!hasCurrentIKWorldPosition)
            return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(currentIKWorldPosition, 0.1f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, currentIKWorldPosition);
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/MouseOrientationController.cs`

```cs
using UnityEngine;
//读取 aimHeld / houndAttackHeld / moveInput / 鼠标位置
//写 requestedFacingDirection
//写 requestedFacingSource
public class MouseOrientationController : BaseOrientationController
{
    private const float CursorDeadZoneX = 0.05f;
    private const float MoveInputDeadZoneX = 0.1f;

    public override void Update(float deltaTime)
    {
        OrientMethod();
    }

    public override void OrientMethod()
    {
        if (dataBus == null)
            return;

        Vector3 characterPosition = dataBus.characterPosition;
        Vector3 cursorWorldPosition = dataBus.mousePositionOnScreen;

        float moveX = dataBus.moveInput.x;
        float cursorDx = cursorWorldPosition.x - characterPosition.x;

        bool hasCursorSide = Mathf.Abs(cursorDx) > CursorDeadZoneX;

        float currentSign = dataBus.facingDirection.x >= 0f ? 1f : -1f;

        float requestedSign = currentSign;
        CharacterDataBus.RequestedFacingSource source = CharacterDataBus.RequestedFacingSource.None;

        // 统一铁则：
        // 瞄准模式 → 鼠标/光标位置决定朝向
        // 非瞄准模式 → 移动输入决定朝向（包括近战攻击、跳跃等）
        bool hasAimRequest = WeaponFacingPolicy.HasAimFacingRequest(dataBus);

        // 1. 瞄准模式且有可靠光标方向 → 光标决定朝向
        if (hasAimRequest && hasCursorSide)
        {
            requestedSign = cursorDx > 0f ? 1f : -1f;
            source = CharacterDataBus.RequestedFacingSource.Aim;
            RecordLastWeaponFacing(requestedSign);
        }
        // 2. 瞄准模式但光标在死区 → 保持当前朝向
        else if (hasAimRequest)
        {
            requestedSign = currentSign;
            source = CharacterDataBus.RequestedFacingSource.None;
        }
        // 3. 非瞄准模式 → 移动输入决定朝向
        else if (Mathf.Abs(moveX) > MoveInputDeadZoneX)
        {
            requestedSign = moveX > 0f ? 1f : -1f;
            source = CharacterDataBus.RequestedFacingSource.Move;
        }
        // 4. 无移动输入 → 保持当前朝向
        else
        {
            requestedSign = currentSign;
            source = CharacterDataBus.RequestedFacingSource.None;
        }

        dataBus.requestedFacingDirection = requestedSign > 0f
            ? Vector3.right
            : Vector3.left;

        dataBus.requestedFacingSource = source;
    }

    /// <summary>记录最后有效武器朝向方向</summary>
    private void RecordLastWeaponFacing(float sign)
    {
        dataBus.lastWeaponFacingDirection = sign > 0f ? Vector3.right : Vector3.left;
        dataBus.hasLastWeaponFacingDirection = true;
    }

    /// <summary>获取最后武器朝向符号，无记录时回退到 currentSign</summary>
    private float GetLastWeaponFacingSign(float fallbackSign)
    {
        if (dataBus.hasLastWeaponFacingDirection)
            return dataBus.lastWeaponFacingDirection.x >= 0f ? 1f : -1f;
        return fallbackSign;
    }

    public override void UpdateOrientation(float deltaTime)
    {
        Update(deltaTime);
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/OrientationSystem.cs`

```cs
using UnityEngine;

public class OrientationSystem : MonoBehaviour, ICharacterSubsystem,ICharacterTickable,ICharacterLateTickable
{
    public enum OrientationType
    {
        Auto,
        Mouse,
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
    private IOrientationController aimOrientationController;

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


        aimOrientationController = new MouseOrientationController();

        (aimOrientationController as BaseOrientationController)?.Initialize(dataBus, visualFlipRoot);

        currentController = aimOrientationController;
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
        if (currentType != OrientationType.Mouse)
            SwitchOrientController(OrientationType.Mouse);
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
                currentController = aimOrientationController;
                break;
        }
    }


    public void NotifyWeaponFacingActivity(float releaseDelay = -1f)
    {
        facingConstraintController?.NotifyWeaponFacingActivity(releaseDelay);
    }





}
```

### `Assets/Script/Character/Core/BasicFunction/Orientation/WeaponFacingPolicy.cs`

```cs
using UnityEngine;

/// <summary>
/// 统一的武器朝向所有权判断。
/// 将"有效武器朝向请求"与"武器租约"分开，
/// 避免未装备武器时误获朝向控制权。
/// </summary>
public static class WeaponFacingPolicy
{
    /// <summary>
    /// 武器是否处于可控制朝向的状态（已装备且可使用）。
    /// </summary>
    public static bool CanWeaponControlFacing(CharacterDataBus bus)
    {
        if (bus == null)
            return false;

        return bus.weaponEquipState == WeaponEquipState.Equipped
            && bus.canUseWeapon;
    }

    /// <summary>
    /// 是否有有效的攻击朝向请求。
    /// 包含输入层和运行状态层。
    /// </summary>
    public static bool HasAttackFacingRequest(CharacterDataBus bus)
    {
        if (!CanWeaponControlFacing(bus))
            return false;

        return bus.houndAttackHeld
            || bus.houndAttackPressedThisFrame
            || bus.isHoundAttacking
            || bus.currentHoundCombatAction == HoundCombatActionType.Fire;
    }

    /// <summary>
    /// 是否有有效的瞄准朝向请求。
    /// 包含输入层和运行状态层。
    /// </summary>
    public static bool HasAimFacingRequest(CharacterDataBus bus)
    {
        if (!CanWeaponControlFacing(bus))
            return false;

        return bus.aimHeld
            || bus.isAiming
            || bus.currentHoundCombatAction == HoundCombatActionType.Aim;
    }

    /// <summary>
    /// 武器朝向租约是否仍然有效（释放延迟内）。
    /// </summary>
    public static bool HasWeaponFacingLease(CharacterDataBus bus)
    {
        if (bus == null)
            return false;

        return bus.blockMoveFacingByWeapon
            && Time.time < bus.blockMoveFacingExpireTime;
    }

    /// <summary>
    /// 武器是否拥有朝向所有权（兼容旧调用）。
    /// 等价于：有攻击请求 或 有瞄准请求。
    /// </summary>
    public static bool HasWeaponFacingOwnership(CharacterDataBus bus)
    {
        return HasAttackFacingRequest(bus) || HasAimFacingRequest(bus);
    }
}
```

### `Assets/Script/Character/Core/CharacterDataBus.cs`

```cs
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
/// <summary>
/// 角色状态数据总线，运行时数据容器
/// </summary>
public class CharacterDataBus
{
    //==============输入设备接口=============
    public enum InputDevice_AimType//瞄准设备类型枚举
    {
        Mouse,
        Gamepad
    }
    public enum RequestedFacingSource
    {
        None,
        Move,
        Aim,
        Attack,
        Target
    }

    //=============角色基础数据==========
    public string characterName;
    public CharacterSO characterSO; // 记录角色的基础SO


    // ============ 瞄准输入 ============
    [Header("Aim Cursor")]
    public Vector3 aimCursorWorldPosition;
    public Vector3 aimDirection;
    public bool aimInputActiveThisFrame;

    [Header("Attack Input")]
    public bool hunterAttackHeld;
    public bool hunterAttackPressedThisFrame;
    public bool houndAttackHeld;
    public bool houndAttackPressedThisFrame;
    [Header("Combat Input")]
    public bool aimHeld;
    public bool aimPressedThisFrame;
    public bool aimReleasedThisFrame;
    public bool reloadPressedThisFrame;
    public bool attackPressed
    {
        get
        {
            return hunterAttackHeld || houndAttackHeld;
        }
    }
    public Vector2 moveInput { get; set; }//写入：InputHandler, 读取 给所有需要移动的脚本
    /// <summary>
    /// 移动输入强度。
    /// 键盘通常输出 0 或 1。
    /// 摇杆可以输出 0~1。
    /// 这不是角色的实际速度。
    /// </summary>
    public float moveInputAmount
    {
        get
        {
            return Mathf.Clamp01(moveInput.magnitude);
        }
    }
    /// <summary>
    /// 是否有移动输入。
    /// </summary>
    public bool hasMoveInput
    {
        get
        {
            return moveInput.sqrMagnitude > 0.01f;
        }
    }
    /// <summary>
    /// 当前移动系统刚采集到的移动速度。
    /// 由 MovementState 写入。
    /// 例如 Walk=2, Jog=4, Dash=6。
    /// </summary>
    public float currentMoveSpeed { get; set; }

    /// <summary>
    /// 当前是否处于活跃的移动状态。
    /// 注意：这里只表示移动状态逻辑层是否活跃，不等于是否有输入。
    /// 外部直接调用即可，预留给以后可能有的改动。
    /// </summary>
    public bool isMovementActive
    {
        get
        {
            return currentMoveSpeed > 0.01f;
        }
    }





    public bool mouseMoving { get; set; } = false;//鼠标是否在移动 //写入：InputHandler
    public InputDevice_AimType currentInputDevice { get; set; }//当前输入设备，默认为鼠标//写入：InputHandler
    public bool jumpPressed { get; set; }
    public bool jumpHeld { get; set; }
    public bool jumpPressedThisFrame { get; set; }
    public bool jumpReleasedThisFrame { get; set; }
    public float lastGroundedTime { get; set; }
    public float lastJumpPressedTime { get; set; }
    public bool dashPressed { get; set; }//写入：InputHandler
    public bool avoidPressed { get; set; }//写入：InputHandler
    public float gamepadMouseControlSensitivity { get; private set; } = 3000f; // 手柄灵敏度，读取用
    public bool aimInputTypeIsTriger { get; set; } = false;//瞄准输入类型枚举，手动选择Trigger/Press)，默认为Press
    public bool crouchInputTypeIsTriger { get; set; } = false;//蹲下输入类型枚举，手动选择Trigger/Press)，默认为Press
    public bool forceSlowWalkInputTypeIsTriger { get; set; } = true;//强制慢走输入类型枚举，手动选择Trigger/Press)，默认为Press
    //public bool isReloadKeyLongPress {  get; set; } = false;//写入：InputHandler
    public bool isForceWalkSlow {  get; set; } = false;////写入：InputHandler




    // ============ 战斗位置 ============
    public Vector3 characterPosition { get; set; }//写入：ICharacterStateMachine
    public Vector3 mousePositionOnScreen { get; set; }//写入：InputHandler

    public bool isGrounded { get; set; } = true;
    public Vector3 facingDirection{ get; set; }//朝向 //写入：OrientationSystem


    public Facing currentFacing;
    public Facing targetFacing;
    public bool isFacingChanging;
    public int facingChangeId;

    public float moveTurnMovementLockUntilTime;
    public int moveTurnMovementLockId;

    public bool isMoveTurnMovementLocked
    {
        get
        {
            return Time.time < moveTurnMovementLockUntilTime;
        }
    }

    public Vector3 requestedFacingDirection { get; set; }

    public RequestedFacingSource requestedFacingSource = RequestedFacingSource.None;
    public BattleCamp battleCamp { get; set; }//角色所属阵营，需要由SO来指定


    // ============ 关键角色状态(数值存储用 float) ============


    //[Header("数值状态字段")]
    // 出生朝向由默认出场(静止状态模型方向反，需要SO提供
    public Facing assetDefaultFacing;
    ///所属阵营决定原本朝向：因为原画中角色 Skeleton 应该用 ScaleX 为 1 还是 -1，需要在角色初始化时由外部设定。
    public float FinalVisualScaleX => (battleCamp == BattleCamp.MateSide)
        ? (assetDefaultFacing == Facing.Right ? 1f : -1f) // 友方阵营与原图朝向一致则不翻转(1)，否则翻转(-1)
        : (assetDefaultFacing == Facing.Right ? -1f : 1f); // 敌方阵营与原图朝向相反则翻转(-1)，否则不翻转(1)

    //public float maxHp = 100f; // 由SO提供
    public float currentHp;    // 战斗中修改
    public float maxRemainHp;    //   残机由敌方阵营使用，由SO提供
    public float currentRemainHp;    //  残机由敌方阵营使用，战斗中修改
    public float stamina = 100f;

    //[Header("实时状态字段")]
    // 注意：这些字段被BattleDirector判断是否需要出场
    public bool isChangeToRemain;  // 残机由敌方阵营使用，角色血量归零，模型替换为残机
    public bool isCompleteDisappear = false;   // 残机血量归零，完全退场
    public bool isSlotForceChange;   // 位置被强制改变（如被击飞到其他位置）
    public bool hasNewStatus; // 刚受到状态（如击飞等）


    //new 战斗
    [Header("角色数值字段")]
    public CharacterStatProfileSO statProfile;

    [Header("角色等级")]
    public int level = 1;


    /// <summary>
    /// 迁移期间临时字段。
    ///
    /// 当前存在的主要唯一的临时写入者是 CharacterWeaponInventory。
    /// 该字段移除时，将 Inventory 同步迁移到战斗系统的新枪械装备系统取代。
    ///
    /// 代码不要直接写入此临时字段。
    /// </summary>
    [Header("当前武器")]
    [System.NonSerialized]
    public WeaponRuntimeData currentWeaponRuntimeData;

    public CharacterWeaponInventory weaponInventory;


    [Header("自动锁定")]
    public bool hasAttackTarget = false;
    public LockTargetPoint lockedAttackTarget;
    public EnemyRoot lockedAttackTargetRoot;
    public Vector3 attackTargetWorldPosition;

    //基础SO
    public AnimationPlanMappingSO basicAnimationPlan;
    public List<AnimationPlanOverrideRuleSetSO> overrideAnimationPlan;


    // ============ 逻辑/显示转换 (只读取用) ============

    /// <summary>
    /// 用于 UI 展示的整数血量，即使不足0.1血也显示为1。
    /// </summary>
    public int displayHp => Mathf.CeilToInt(currentHp);

    /// <summary>
    /// 最大血量整数化。
    /// </summary>
    //public int displayMaxHp => Mathf.RoundToInt(maxHp);

    /// <summary>
    /// 判定逻辑层，只有当血量小于 0 时，才把isCompleteDisappear为true，才判定死亡
    /// </summary>

    public bool isAlive = true;

    /// <summary>
    /// 上一帧的剩余血量百分比，用于更新血条 Image.fillAmount。
    /// </summary>
    //public float hpPercentage => Mathf.Clamp01(currentHp / maxHp);



    // ============ 运动角色状态(开关型) ============
    


    public bool isMoving { get; set; } = false;//角色是否在移动//写入：ICharacterStateMachine
    public bool isMovingBackward {  get; set; } = false;//角色是否在向后移动//写入：BackWardMovement
    //public bool isAim { get; set; } = true;//角色是否处于瞄准状态 //写入：InputHandler
    public bool isCrouch {  get; set; } = false;//角色是否处于瞄准状态 //写入：InputHandler
    //public bool isHoldWeapon { get; set; } = false;//角色是否处于持枪装备状态

    [Header("Combat Runtime")]
    public bool isAiming;
    public bool isReloading;
    public bool isHoundAttacking;
    public bool isHunterAttacking;

    public bool isCharacterAttacking => isHoundAttacking || isHunterAttacking;

    public float lastCombatIntentTime;
    public float autoHolsterDelay = 2.5f;

    public bool canMove { get; set; } = true;
    public bool canJump { get; set; } = true;

    public HoundCombatActionType currentHoundCombatAction = HoundCombatActionType.None;










    // ============ 角色状态(状态机用) ============
    public EState currentMovementState { get; set; } = EState.Idle;
    public EState currentJumpState { get; set; } = EState.Jump_None;

    /// <summary>
    /// 跳跃期间是否阻止 Move 朝向。
    /// 覆盖 Jump_Start / Jump_Rising / Jump_Fall / Jump_Land。
    /// CompleteJump 后变为 false，允许地面 Move 接管。
    /// </summary>
    public bool blocksMoveFacingByJump
    {
        get
        {
            return currentJumpState == EState.Jump_Start
                || currentJumpState == EState.Jump_Rising
                || currentJumpState == EState.Jump_Fall
                || currentJumpState == EState.Jump_Land;
        }
    }




    //===============武器装备状态================
    [Header("Weapon Equip Runtime")]
    public WeaponEquipState weaponEquipState = WeaponEquipState.Cancelled;
    public WeaponPostureState weaponPostureState = WeaponPostureState.None;

    public WeaponSwitchInputRequestType weaponSwitchInputRequestType =WeaponSwitchInputRequestType.None;
    public WeaponSlotId requestedWeaponSlot =WeaponSlotId.Slot1;


    /// <summary>
    /// 是否正处于装备/卸下过程中。
    /// </summary>
    public bool isWeaponEquipTransitioning
    {
        get
        {
            return weaponEquipState == WeaponEquipState.Equipping
                || weaponEquipState == WeaponEquipState.Cancelling;
        }
    }

    /// <summary>
    /// 判定武器是否可以使用。
    /// 如果正在瞄准、攻击等行为应该可用。
    /// </summary>
    public bool canUseWeapon
    {
        get
        {
            return isAlive
                && currentWeaponRuntimeData != null
                && weaponEquipState == WeaponEquipState.Equipped
                && !isInSafeZone
                && !isWeaponEquipTransitioning;
        }
    }

    /// <summary>
    /// 是否在安全区域。
    /// 安全区域内会自动触发 CancelWeapon。
    /// </summary>
    public bool isInSafeZone;

    /// <summary>
    /// 按下 R 键触发，由 InputHandler 写入。
    /// </summary>
    public bool weaponToggleLongPressedThisFrame;
    /// <summary>
    /// 外部系统触发的自动取消武器请求。
    /// 比如安全区域入场、自动收回武器等。
    /// </summary>
    public bool requestCancelWeapon;









    // ============ 战斗数据 ============
    public int comboCount { get; set; }
    public float lastAttackTime { get; set; }

    public bool blockMoveFacingByWeapon;
    public float blockMoveFacingExpireTime;
    public float lastWeaponFacingActivityTime;

    /// <summary>
    /// 最后一次有效武器朝向方向。
    /// 当光标进入死区或 Aim/Fire 状态切换时，保持此方向。
    /// </summary>
    public Vector3 lastWeaponFacingDirection { get; set; } = Vector3.left;
    public bool hasLastWeaponFacingDirection { get; set; } = false;


    // ============ 辅助字段 ============



    //读取数据的特殊方法，需被BattleManager调用来根据角色专属来设置
    public void LoadFromSO(CharacterSO so)
    {
        characterSO = so;
        this.statProfile = so.characterStatProfileSO;
        characterName = so.characterName;
        this.assetDefaultFacing = so.assetDefaultFacing;
        battleCamp = so.battleCamp;
        //maxHp = so.maxHp;
        maxRemainHp = so.maxRemainHp;

        basicAnimationPlan = so.basicAnimationPlanMapping;
        overrideAnimationPlan = so.animationPlanOverrideRuleSet;
        // 其他数据同步...




    }


    #region 武器切换请求辅助方法
    /// <summary>
    /// 输入系统直接选择一个武器槽位。
    /// DataBus 只存储请求，不负责执行切换。
    /// </summary>
    public void RequestWeaponSlotSwitch(
        WeaponSlotId slotId)
    {
        requestedWeaponSlot = slotId;

        weaponSwitchInputRequestType =
            WeaponSwitchInputRequestType.SelectSlot;
    }

    /// <summary>
    /// 输入系统循环切换到下一个武器槽位。
    /// </summary>
    public void RequestWeaponSlotCycleNext()
    {
        weaponSwitchInputRequestType =
            WeaponSwitchInputRequestType.CycleNext;
    }

    /// <summary>
    /// 装备状态机消费当前武器切换请求。
    /// 消费后请求清空。
    /// </summary>
    public bool TryConsumeWeaponSwitchInput(
        out WeaponSwitchInputRequestType requestType,
        out WeaponSlotId requestedSlot)
    {
        requestType = weaponSwitchInputRequestType;
        requestedSlot = requestedWeaponSlot;

        if (requestType ==
            WeaponSwitchInputRequestType.None)
        {
            return false;
        }

        weaponSwitchInputRequestType =
            WeaponSwitchInputRequestType.None;

        return true;
    }
    #endregion


}
```

### `Assets/Script/Character/Core/CharacterRoot.cs`

```cs
using MoreMountains.Feel;
using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;

[DefaultExecutionOrder(-500)]
public class CharacterRoot : MonoBehaviour
{
    //    在 Unity 开发中，“初始化防御（Initialization Guard）”不仅是一个好习惯，更是构建复杂系统（如你现在的回合制战斗系统）时的底层安全准则。

    //简单的回答是：是的，凡是涉及到“生成新实例（new）”或“绑定事件（+=）”的初始化逻辑，都必须进行防御。
    //以下是为什么要这么做的三个深度原因，以及未来你该如何避坑：
    //1. 解决 Unity 生命周期的“不可控性”
    //Unity 的 Awake 和 Start 方法是由系统调用的。当你动态生成（Instantiate）一个物体时：
    //同步执行：Awake 会在 Instantiate 返回前立即执行。
    //异步冲突：你的脚本（如 BattleSpawner）通常在 Instantiate 之后紧接着调用 Initialize()。
    //后果：如果你在 Awake 里 new 了一次状态机，在 Initialize 里又 new 了一次，你就亲手制造了一个“状态黑洞”。
    //外界赋予第一个实例的所有数据（比如 isForceActive = true），都会在第二个实例生成时被瞬间抹除。
    //2. 保证操作的“幂等性”（Idempotency）
    //在软件工程中，幂等性意味着：无论你调用一个方法 1 次还是 100 次，结果都应该是一样的，不会产生副作用。
    //如果你的初始化逻辑不防重：
    //重复执行 new：会导致旧引用丢失、内存泄漏或逻辑断裂
    //重复执行 events += Func：会导致事件触发时跑两遍（比如一个技能放出两个火球、结算两次伤害）。
    private bool _isInitialized = false;//防止重复初始化的标志位，虽然目前的设计里不太可能，但以防万一，还是加上了这个保险措施。


    private bool _isBootstrapped = false;
    private bool _isGameplayInitialized = false;



    //遵循良好的设计原则：组合优于继承、依赖注入（通过 Initialize(this)）、防御性编程（??= 和 AutoGetComponent）。



    //[Header("Core References")]
    //=============将所有需要挂载的核心脚本作为属性公开=============
    //自动属性{  get; private set; }：它无法直接序列化（保存到磁盘）这种带 { get; set; } 的属性。
    //强制存盘：[field: SerializeField] 告诉 Unity：“虽然这是一个属性，但请把那个隐藏变量序列化（保存）到硬盘里。”
    //纯数据类（DataBus）：千万别序列化！：因为它每次运行都是 new 出来的，绝对不要加 [field: SerializeField]。
    //不要删 { get; private set; }：它是最好的保护，防止外部脚本乱改 Root 里的组件。
    //补全 GetComponent：对于那些没有 [field: SerializeField] 的属性，必须在 Awake/Initialize 里写 GetComponent。
    //DataLog/DataBus：去掉那里的 [field: SerializeField]，逻辑生成的东西不需要序列化。
    #region 数据
    public CharacterDataBus characterDataBus { get; private set; }//N
    [field: SerializeField] public CharacterStatsController characterStatsController { get; private set; }//N
    #endregion
    #region 输入
    [field: SerializeField] public InputHandler inputHandler{ get; private set; }//update、lateupdate
    #endregion
    #region 动作
    [field: SerializeField] public StateArbitrator stateArbitrator{ get; private set; }//update、fixedupdate
    [field: SerializeField] public OrientationSystem orientationSystem{ get; private set; }//update、lateupdate
    [field: SerializeField] public CharacterAnimationConfigManager characterAnimationConfigManager { get; private set; }//N
    [field: SerializeField] public AnimationHandleSystem animationHandleSystem{ get; private set; }//N

    #endregion
    #region 皮肤
    [field: SerializeField] public WardrobeManager wardrobeManager { get; private set; }//N
    [field: SerializeField] public SpineSkinApplicator spineSkinApplicator { get; private set; }//N
    #endregion
    #region 战斗
    [field: SerializeField] public AimCursorSystem aimCursorSystem { get; private set; }//update
    [field: SerializeField] public CharacterAttackController characterAttackController { get; private set; }//N

    [field: SerializeField] public AimIKController aimIKController {get; private set; }//lateupdate
    [field: SerializeField] public LockTargetIKController lockTargetIKController { get; private set; }//lateupdate
    [field: SerializeField] public HunterGunCursorFacingController hunterGunCursorFacingController { get; private set; }//lateupdate
    [field: SerializeField] public HunterGunBonePositionMatcher hunterGunBonePositionMatcher { get; private set; }//update、fixedupdate
    [field: SerializeField] public CharacterEquipmentController characterEquipmentController { get; private set; }//N
    [field: SerializeField] public GunSystemController gunSystemController { get; private set; }//N

    [field: SerializeField] public CharacterSpineEventSystem characterSpineEventSystem { get; private set; }//N

    [field: SerializeField] public CharacterWeaponInventory characterWeaponInventory{ get; private set; }//N


    #endregion
    #region 背包系统
    [field: SerializeField] public CharacterPackageSystem characterPackageSystem { get; private set; }
    [field: SerializeField] public CharacterPickupController characterPickupController { get; private set; }
    [field: SerializeField] public CharacterUIInputRouter characterUIInputRouter { get; private set; }
    #endregion

    #region 视觉
    //[Header("Spine - Hound 角色本体")]
    [field: SerializeField] public SkeletonAnimation houndSkeletonAnimation { get; private set; }//N
    //[Header("Spine - HunterGun 枪械承载骨架")]
    [field: SerializeField] public SkeletonAnimation hunterGunSkeletonAnimation { get; private set; }//N
    public Spine.Skeleton houndSkeleton =>
        houndSkeletonAnimation != null ? houndSkeletonAnimation.Skeleton : null;
    public Spine.Skeleton hunterGunSkeleton =>
        hunterGunSkeletonAnimation != null ? hunterGunSkeletonAnimation.Skeleton : null;
    // 临时兼容旧代码：旧的 characterRoot.skeletonAnimation 默认指向 Hound
    public SkeletonAnimation skeletonAnimation => houndSkeletonAnimation;
    public Spine.Skeleton skeleton =>
        houndSkeletonAnimation != null ? houndSkeletonAnimation.Skeleton : null;

    [field: SerializeField] public CharacterAnimationComposer characterAnimationComposer { get; private set; }//update

    [field: SerializeField] public GunHandOcclusionByAimAngle gunHandOcclusionByAimAngle { get; private set; }//LateUpdate

    #endregion
    #region Unity物理
    [field: SerializeField] public Rigidbody2D rigidBody2d { get; private set; }//N
    [field: SerializeField] public BoxCollider2D boxCollider2d { get; private set; }//N
    [field: SerializeField] public JumpConfigSO jumpConfig { get; private set; }
    [field: SerializeField] public LayerMask groundLayerMask { get; private set; } = 1 << 8; // 默认 Enemy 层，需在 Inspector 配置为地面层
    //public Transform CachedTransform { get; private set; }
    //Unity 里，所有的 GameObject 在诞生的那一刻，就一定且必须自带一个 Transform 组件。
    //你不能删除 Transform,不能给一个物体挂载第二个 Transform。
    //GetComponent<Transform>() 就永远能拿到值，永远不会走到 AddComponent 那一步。
    //在任何 MonoBehaviour 脚本里，直接用小写的 transform 即可。不需要定义，不需要 GetComponent，它是 Unity 送给你的“入职大礼包”。

    #endregion

    private void Reset() // 当脚本被挂载到物体上，或者点击组件右上角的 Reset 时触发
    {
        AutoGetComponent();
    }

    //private T GetOrAddComponent<T>() where T : Component//适合“这个组件必定应该挂在当前物体上，而且没有任何 Inspector 配置”的组件。
    //{
    //    T comp = GetComponent<T>();
    //    if (comp == null) comp = gameObject.AddComponent<T>();
    //    return comp;
    //}
    //=============统一在root初始化=============
    private void Awake()
    {
        BootstrapOnly();
    }

    private void Update()
    {
        if (!_isGameplayInitialized)
            return;
        float dt = Time.deltaTime;
        // 1. 输入先更新
        TickUpdate(inputHandler, dt);

        // 2. 光标位置先更新，否则 Orientation 用到的 mousePositionOnScreen 可能是旧的
        TickUpdate(aimCursorSystem, dt);

        // 3. 攻击/瞄准状态先更新，因为 Orientation 要读 attackPressed / aimHeld
        TickUpdate(characterAttackController, dt);

        // 枪械内部 burst / cooldown 更新
        TickUpdate(gunSystemController, dt);

        // 4. 朝向必须早于 StateArbitrator
        //    否则 MovementStateMachine 会基于旧 facing 算 Jog_Back，下一帧又变 Jog
        TickUpdate(orientationSystem, dt);

        // 5. 状态机此时读取到的是本帧稳定后的 facing
        TickUpdate(stateArbitrator, dt);

        // 6. 动画 Composer 本身主要处理过期 oneShot
        TickUpdate(characterAnimationComposer, dt);

        // 7. 装备等后置
        TickUpdate(characterEquipmentController, dt);

        // AnimationHandleSystem 是事件驱动的，不需要 TickUpdate
        // TickUpdate(animationHandleSystem, dt);
    }
    private void LateUpdate()
    {
        if (!_isGameplayInitialized)
            return;

        if (!_isGameplayInitialized)
            return;

        float dt = Time.deltaTime;

        TickLateUpdate(hunterGunBonePositionMatcher, dt);
        TickLateUpdate(hunterGunCursorFacingController, dt);

        TickLateUpdate(aimIKController, dt);
        TickLateUpdate(lockTargetIKController, dt);
        // 枪械朝向和 IK 都完成后，再决定手部 Separator 是否隐藏。
        TickLateUpdate(gunHandOcclusionByAimAngle, dt);
        // 最后压视觉朝向
        TickLateUpdate(orientationSystem, dt);

        // 最后清理本帧输入标记
        // 必须放在所有 Update / LateUpdate 逻辑之后
        TickLateUpdate(inputHandler, dt);

    }

    private void FixedUpdate()
    {
        if (!_isGameplayInitialized)
            return;

        float fdt = Time.fixedDeltaTime;
        TickFixedUpdate(stateArbitrator, fdt);
    }

    public void BootstrapOnly()//第一步为准备角色数据外壳
    {
        if (_isBootstrapped) return;
        AutoGetComponent();
        characterDataBus = new CharacterDataBus();
        _isBootstrapped = true;

        // 角色(Render_Character=22)忽略与 Enemy(8) 的物理碰撞
        // 伤害由 TakeDamage 系统处理，不需要物理碰撞阻挡
        Physics2D.IgnoreLayerCollision(22, 8, true);

        Debug.Log("[CharacterRoot] BootstrapOnly 完成");
    }

    public void InitializeWithSO(CharacterSO so)
    {
        //先 LoadFromSO
        //先换 Spine SkeletonDataAsset
        //再初始化依赖 Skeleton 的系统
        //最后加载动画映射 / 技能
        BootstrapOnly();

        if (_isGameplayInitialized)
        {
            Debug.LogWarning("[CharacterRoot] InitializeWithSO 重复调用，已忽略");
            return;
        }

        if (so == null)
        {
            Debug.LogError("[CharacterRoot] InitializeWithSO 失败：so == null");
            return;
        }

        // 1. 加载数据
        characterDataBus.LoadFromSO(so);

        Debug.Log($"[CharacterRoot] 已加载角色SO: {so.name}");
        Debug.Log($"[CharacterRoot] statProfile = {characterDataBus.statProfile}");

        // 2. 先应用 Hound 视觉骨架
        ApplySkeletonFromSO(so);

        // 3. 再初始化依赖 root / dataBus / skeleton 的子系统
        InitializeGameplaySubsystems();

        // 4. 再加载动画、技能、武器覆盖等配置
        ApplyGameplayConfigFromSO(so);

        _isGameplayInitialized = true;

        Debug.Log("[CharacterRoot] InitializeWithSO 完成");
    }



    private void ApplySkeletonFromSO(CharacterSO so)
    {
        if (so == null)
            return;

        if (so.chracterModel != null && houndSkeletonAnimation != null)
        {
            houndSkeletonAnimation.skeletonDataAsset = so.chracterModel;
            houndSkeletonAnimation.Initialize(true);
        }

        // HunterGun 目前是 prefab 固定骨架，不从 CharacterSO 加载
    }

    private void ApplyGameplayConfigFromSO(CharacterSO so)
    {
        if (so == null)
            return;

        if (characterAnimationConfigManager != null)
        {
            characterAnimationConfigManager.LoadCharacterBasicAnimation();
            characterAnimationConfigManager.RefreshWeaponAnimationOverride();
        }


        //if (lockTargetIKController != null)
        //{
        //    lockTargetIKController.ForceRefresh();
        //}

    }









    private void InitializeGameplaySubsystems()
    {
        // 1. 先初始化基础数据相关
        if (characterStatsController != null)
            characterStatsController.Initialize(this);

        // 2. 动画相关先于状态机
        if (characterAnimationConfigManager != null)
            characterAnimationConfigManager.Initialize(this);

        if (characterAnimationComposer != null)
            characterAnimationComposer.Initialize(this);

        if (animationHandleSystem != null)
            animationHandleSystem.Initialize(this);

        if (characterAttackController != null)
            characterAttackController.Initialize(this);

        // 3. 输入 / 朝向 / 战斗辅助
        if (inputHandler != null)
            inputHandler.Initialize(this);

        if (orientationSystem != null)
            orientationSystem.Initialize(this);

        if (aimCursorSystem != null)
            aimCursorSystem.Initialize(this);

        if (hunterGunBonePositionMatcher != null)
            hunterGunBonePositionMatcher.Initialize(this);

        if (hunterGunCursorFacingController != null)
            hunterGunCursorFacingController.Initialize(this);

        if (aimIKController != null)
            aimIKController.Initialize(this);

        if (lockTargetIKController != null)
            lockTargetIKController.Initialize(this);

        if (gunHandOcclusionByAimAngle != null)
            gunHandOcclusionByAimAngle.Initialize(this);

        if (gunSystemController != null)//在状态机之前，装备系统之前
            gunSystemController.Initialize(this);

        if (characterPackageSystem != null)
        {
            characterPackageSystem.Initialize(this);
            characterPackageSystem.InitializeNewGame();
        }

        if (characterWeaponInventory != null)
            characterWeaponInventory.Initialize(this);

        if (characterPickupController != null)
            characterPickupController.Initialize(this);

        if (characterUIInputRouter != null)
            characterUIInputRouter.Initialize(this);

        if (characterEquipmentController != null)
            characterEquipmentController.Initialize(this);

        if (wardrobeManager != null)
            wardrobeManager.Initialize(this);

        if (characterSpineEventSystem != null)
            characterSpineEventSystem.Initialize(this);

        // 4. 最后状态机
        if (stateArbitrator != null)
            stateArbitrator.Initialize(this);


    }


    public void AutoGetComponent()
    {
        #region 数据
        if (characterStatsController == null)
            characterStatsController = GetComponent<CharacterStatsController>();
        #endregion

        #region 输入
        if (inputHandler == null)
            inputHandler = GetComponent<InputHandler>();
        #endregion

        #region 动作
        if (stateArbitrator == null)
            stateArbitrator = GetComponent<StateArbitrator>();

        if (orientationSystem == null)
            orientationSystem = GetComponent<OrientationSystem>();

        if (characterAnimationConfigManager == null)
            characterAnimationConfigManager = GetComponent<CharacterAnimationConfigManager>();

        if (characterAnimationComposer == null)
            characterAnimationComposer = GetComponent<CharacterAnimationComposer>();

        if (animationHandleSystem == null)
            animationHandleSystem = GetComponent<AnimationHandleSystem>();


        if (characterSpineEventSystem == null)
            characterSpineEventSystem = GetComponent<CharacterSpineEventSystem>();
        #endregion

        #region 皮肤
        if (wardrobeManager == null)
            wardrobeManager = GetComponent<WardrobeManager>();

        if (spineSkinApplicator == null)
            spineSkinApplicator = GetComponent<SpineSkinApplicator>();
        #endregion

        #region 战斗
        if (aimCursorSystem == null)
            aimCursorSystem = GetComponent<AimCursorSystem>();

        if(characterAttackController == null)
            characterAttackController = GetComponent<CharacterAttackController>();

        if (lockTargetIKController == null)
            lockTargetIKController = GetComponent<LockTargetIKController>();


        if (aimIKController == null)
            aimIKController = GetComponent<AimIKController>();

        if (characterEquipmentController == null)
            characterEquipmentController = GetComponent<CharacterEquipmentController>();

        if (hunterGunCursorFacingController == null)
            hunterGunCursorFacingController = GetComponentInChildren<HunterGunCursorFacingController>(true);

        if (hunterGunBonePositionMatcher == null)
            hunterGunBonePositionMatcher = GetComponentInChildren<HunterGunBonePositionMatcher>(true);
        
        if (gunHandOcclusionByAimAngle == null)
            gunHandOcclusionByAimAngle = GetComponentInChildren<GunHandOcclusionByAimAngle>(true);

        if (gunSystemController == null)
            gunSystemController = GetComponentInChildren<GunSystemController>(true);

        if(characterWeaponInventory == null)
            characterWeaponInventory = GetComponentInChildren<CharacterWeaponInventory>(true);

        if (characterPackageSystem == null)
            characterPackageSystem = GetComponentInChildren<CharacterPackageSystem>(true);

        if (characterPickupController == null)
            characterPickupController = GetComponentInChildren<CharacterPickupController>(true);

        if (characterUIInputRouter == null)
            characterUIInputRouter = GetComponentInChildren<CharacterUIInputRouter>(true);


        #endregion

        #region Spine组件
        //更专业的做法是手动把角色本体的 SkeletonAnimation 和枪械承载骨架的 SkeletonAnimation 分别拖到 Inspector 上的 houndSkeletonAnimation 和 hunterGunSkeletonAnimation 里。
        //不要长期依赖名字查找。

        if (houndSkeletonAnimation == null || hunterGunSkeletonAnimation == null)
        {
            SkeletonAnimation[] skeletonAnimations = GetComponentsInChildren<SkeletonAnimation>(true);

            foreach (SkeletonAnimation skel in skeletonAnimations)
            {
                if (skel == null)
                    continue;

                string goName = skel.gameObject.name;

                if (houndSkeletonAnimation == null)
                {
                    if (goName.Contains("Hound") || goName.Contains("VisualFlipRoot"))
                    {
                        houndSkeletonAnimation = skel;
                    }
                }

                if (hunterGunSkeletonAnimation == null)
                {
                    if (goName.Contains("HunterGun"))
                    {
                        hunterGunSkeletonAnimation = skel;
                    }
                }
            }

            // 兜底：如果只有一个，默认当 Hound
            if (houndSkeletonAnimation == null && skeletonAnimations.Length > 0)
                houndSkeletonAnimation = skeletonAnimations[0];
        }
        #endregion

        #region Unity物理
        if (rigidBody2d == null)
            rigidBody2d = GetComponent<Rigidbody2D>();

        if (boxCollider2d == null)
            boxCollider2d = GetComponent<BoxCollider2D>();
        #endregion

        ValidateComponents();
    }


    private void ValidateComponents()
    {
        if (characterStatsController == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterStatsController> ", this);
        if (houndSkeletonAnimation == null)
            Debug.LogError("[CharacterRoot] 缺少 houndSkeletonAnimation。它应该指向 VisualRoot_Hound 上的 SkeletonAnimation。", this);

        if (hunterGunSkeletonAnimation == null)
            Debug.LogWarning("[CharacterRoot] 缺少 hunterGunSkeletonAnimation。HunterGun 位置同步和枪械 IK 不会生效。", this);
        //if(characterCameraTargetSystem ==null)
        //    Debug.LogError("[CharacterRoot] 缺少 CharacterCameraTargetSystem", this);
        if (stateArbitrator == null)
            Debug.LogError("[CharacterRoot] 缺少 StateArbitrator", this);
        if (orientationSystem == null)
            Debug.LogError("[CharacterRoot] 缺少 OrientationSystem", this);
        if (characterAnimationConfigManager == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterAnimationConfigManager", this);
        if (animationHandleSystem == null)
            Debug.LogError("[CharacterRoot] 缺少 AnimationHandleSystem", this);
        if (wardrobeManager == null)
            Debug.LogWarning("[CharacterRoot] 缺少 WardrobeManager", this);
        if(aimCursorSystem ==null)
            Debug.LogWarning("[CharacterRoot] 缺少 AimCursorSystem", this);

        if(aimIKController == null)
            Debug.LogError("[CharacterRoot] 缺少 AimIKController ", this);
        if (lockTargetIKController == null)
            Debug.LogWarning("[CharacterRoot] 缺少 LockTargetIKController，锁定目标 IK_Hund 不会生效", this);
        if (characterEquipmentController == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterEquipmentController> ", this);
        if(characterAnimationComposer == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterAnimationComposer> ", this);
        if (hunterGunCursorFacingController == null)
            Debug.LogWarning("[CharacterRoot] 缺少 HunterGunCursorFacingController，枪口朝向鼠标的功能不会生效", this);
        if (hunterGunBonePositionMatcher == null)
            Debug.LogWarning("[CharacterRoot] 缺少 HunterGunBonePositionMatcher，HunterGun 位置同步功能不会生效", this);
        if (gunHandOcclusionByAimAngle == null)
            Debug.LogWarning("[CharacterRoot] 缺少 GunHandOcclusionByAimAngle，持枪手部遮挡不会自动处理", this);
        if (characterAttackController == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterAttackController> ", this);
        if (gunSystemController == null)
            Debug.LogError("[CharacterRoot] 缺少 GunSystemController> ", this);
        if (characterSpineEventSystem == null)
            Debug.LogWarning("[CharacterRoot] 缺少 CharacterSpineEventSystem，Spine 动画事件不会转发到角色逻辑。", this);
        if(characterWeaponInventory == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterWeaponInventory> ", this);
        if (characterPackageSystem == null)
            Debug.LogWarning("[CharacterRoot] 缺少 CharacterPackageSystem，背包功能不可用", this);
    }




    //特殊的给每个角色各自的SkeletonAnimation、characterSkillBook赋值的方法
    public void LoadCharacterDataFromSO(CharacterSO so)
    {
        if (so == null)
        {
            Debug.LogError("[CharacterRoot] LoadCharacterDataFromSO 失败：so == null");
            return;
        }

        // 1. 同步数据
        characterDataBus.LoadFromSO(so);

        // 2. 重新计算属性，这是关键
        if (characterStatsController != null)
        {
            characterStatsController.RecalculateStats();

            Debug.Log($"[CharacterRoot] 加载角色数据后 MoveSpeed = {characterStatsController.GetStat(CharacterStats.MoveSpeed)}");
        }
        else
        {
            Debug.LogError("[CharacterRoot] characterStatsController == null");
        }

        // 3. 更换 Spine 皮肤
        if (so.chracterModel != null)
        {
            skeletonAnimation.skeletonDataAsset = so.chracterModel;
            skeletonAnimation.Initialize(true);
        }

        characterAnimationConfigManager.LoadCharacterBasicAnimation();

    }


    private void TickUpdate(object system, float deltaTime)
    {
        if (system is ICharacterTickable tickable)
            tickable.ManagedUpdate(deltaTime);
    }

    private void TickLateUpdate(object system, float deltaTime)
    {
        if (system is ICharacterLateTickable tickable)
            tickable.ManagedLateUpdate(deltaTime);
    }

    private void TickFixedUpdate(object system, float fixedDeltaTime)
    {
        if (system is ICharacterFixedTickable tickable)
            tickable.ManagedFixedUpdate(fixedDeltaTime);
    }











}
```

### `Assets/Script/Character/Core/CharacterSO.cs`

```cs
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "ScriptableObject/��ɫ/��ɫ����")]
public class CharacterSO : ScriptableObject
{
    //[AssetSelector(AssetPreview = true)]
    //[PreviewField(100, ObjectFieldAlignment.Left)]
    //[HideLabel]
    [PreviewField(80)] // �ص㣺������ Odin ���κεط����ܿ���ͼ��ǰ��
    public Sprite characterIcon;

    //==========���ݽ���=============
    //[LabelText("��ɫstats")]
    public CharacterStatProfileSO characterStatProfileSO;//��ɫ��������


    //[LabelText("��������")]
    public SkeletonDataAsset chracterModel;//Spineģ������



    //===========�������ݽ���===================

    [AssetSelector]
    public AnimationPlanMappingSO basicAnimationPlanMapping;

    [AssetSelector]
    public List<AnimationPlanOverrideRuleSetSO> animationPlanOverrideRuleSet;



    //=============��ɫ����

    //[LabelText("��ɫ����")]
    public string characterName;

    [Header("�߼�����")]//�ṩ��ɫ��ר��������dataBus��ȡ

    public BattleCamp battleCamp;//��ɫ������Ӫ����Ҫ��SO����ָ��

    public Facing assetDefaultFacing = Facing.Left; // ������������Ĭ�ϳ���(��ֹ����ģ�巽��㷴��



    public float maxHp = 100f;//����ֵ����ʼֵ��SO����ָ����ս���и����ܻ��������

    public float maxRemainHp = 10f;


    //public bool isAlive;//�Ƿ����ʼֵΪtrue��ս���и����ܻ��������

    //[Header("��ʼ���ܼ�")]
    //public List<SkillSO> availableSkills; // ��ְҵ��ѧ������м���



    //[Header("��ʼװ��")]
    //public WeaponConfigSO initialWeaponConfig;










}
```

### `Assets/Script/Character/Core/CharacterStateRequest.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 这是一个具体的实现类，不再是 abstract,因为 StateRequest的abstract属性导致无法用new构筑实例，而输入系统驱动下推自动机的机制需要这个实例
public class CharacterStateRequest : StateRequest
{
    // 通过构造函数给父类的 protected 属性赋值
    //构造函数是一种特殊的方法，特点：
    //名称必须和类名完全相同
    //没有返回值（连 void 都不用写）
    //在创建对象时自动调用
    //public CharacterStateRequest(CharacterStateType type, bool isUrgent = false, object data = null) : base()
    //{
    //    this.characterStateType = type;
    //    this.IsUrgent = isUrgent;
    //    this.Data = data;
    //}

    //增加回调

    public CharacterStateRequest(CharacterStateType type,bool isUrgent = false,object data = null,System.Action onActionPoint = null) : base()
    {
        this.characterStateType = type;
        this.IsUrgent = isUrgent;
        this.Data = data;
        // 关键：由于父类已经定义了该变量，这里直接赋值
        this.onActionPoint = onActionPoint;
    }




    // 如果需要覆盖优先级，可以再加一个构造函数或参数(重载）
    //public CharacterStateRequest(CharacterStateType type, int priority, bool isUrgent = false) : base()
    //{
    //    this.characterStateType = type;
    //    this.OverridePriority = priority;
    //    this.IsUrgent = isUrgent;
    //}


    public void ExecuteCombat()
    {

    }

}
```

### `Assets/Script/Character/Core/CombatActionData.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//CombatActionData 的核心作用是“传参”：
//如果没有这个类，你的状态机只能切换状态，但无法知道：

//这个技能目标（Target）是谁？（是左边的史莱姆还是右边的巨龙？）
//这个技能的具体数据（SkillSO）是什么？

//概念 出现时机	它的作用（潜台词）	存储内容
//CombatActionData	点击按钮/选择目标时	“我想做什么？”（订单）	选了哪个技能？选了哪个目标？
//SkillPayload	技能真正开始释放时	“正在发生什么？”（现场数据）	我是谁？目标是谁？由于由于由于由于由于最终伤害是多少？


public class CombatActionData
{
    public CharacterStateType Skill { get; }
    //public CombatActionData(ESkill skill) => Skill = skill;
    public CharacterRoot Target; // 目标（谁被打了）
    // 未来你可以在这里加更多：cost消耗、触发条件、第几回合等
}
```

### `Assets/Script/Character/Core/ECharacter.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Facing { Left = -1, Right = 1 }

public enum WeaponDefaultFacing
{
    Right,
    Left
}

public enum WeaponSwitchInputRequestType
{
    None = 0,

    /// <summary>
    /// 手柄选择指定武器槽位
    /// </summary>
    SelectSlot = 1,

    /// <summary>
    /// 切换到下一个武器槽位
    /// </summary>
    CycleNext = 2
}

public enum CombatDamageValueMode
{
    /// <summary>
    /// 完全使用效果资产中的固定伤害值。
    /// 适合枪械踢击等独立动作。
    /// </summary>
    Flat,

    /// <summary>
    /// 使用武器 CommonCoreStats.damage 乘倍率。
    /// 适合普通战斗伤害结算。
    /// </summary>
    WeaponCoreMultiplier,

    /// <summary>
    /// 固定值加上武器伤害乘倍率。
    /// </summary>
    FlatPlusWeaponCoreMultiplier
}

public enum AnimationLayer
{
    /// <summary>
    /// 基础全身层。
    /// 用于 Idle / Walk / Jog / Dash 等。
    /// 第一阶段移动状态机主要使用此层。
    /// </summary>
    BaseFullBody = 0,

    /// <summary>
    /// 下半身层。
    /// 第二阶段如 Jog_LowerOnly / Walk_LowerOnly。
    /// </summary>
    LowerBody = 10,

    /// <summary>
    /// 上半身层。
    /// 第二阶段如 Aim / Fire / Reload / UpperOnly Attack。
    /// </summary>
    UpperBody = 20,

    /// <summary>
    /// 高优先级全身动作层。
    /// 用于扩展如技能、受击、处决技。
    /// 第一阶段 AutoAttack 使用此层。
    /// </summary>
    FullBodyAction = 30,

    /// <summary>
    /// 叠加层。
    /// 如顿帧、受击、闪回等不取代表情。
    /// </summary>
    Additive = 40
}

public enum AnimationIntentSourceStateMachine
{
    None = 0,

    Movement = 10,       // 移动状态机
    HoundAttack = 15,    // 近战攻击意图来源
    HoundWeaponPose = 16,// 枪械预备 pose 意图来源（瞄准等）
    HoundWeaponAction = 17,// 枪械实际行为意图来源（射击、换弹）

    Jump = 25,           // 跳跃状态机意图来源

    Skill = 40,
    Hurt = 50,
    Status = 60,
    Debug = 90,

    WeaponEquip,   // 武器装备状态机提交装备/卸下动画意图，固定优先级后缀
    WeaponPosture, // 武器姿态状态机提交收枪/持枪动画意图，低于行为优先级
}

public enum WeaponPostureState // 武器姿态
{
    None,
    LowReady
}

public enum EAnimSemantic // 动画逻辑 ID，状态机用于标记状态
{
    None,

    // ================= 移动相关（提交这些） =================
    Idle,
    Walk,
    Jog,
    JogFast,
    Dash,
    Walk_Back,
    Jog_Back,

    TurnAround,
    MoveStart,
    MoveStop,
    AimEnter,
    AimExit,

    // ================= 跳跃相关 =================
    Jump_Start,
    Jump_Rising,
    Jump_Falling,
    Jump_Landing,

    // ================= 武器相关全身动作 =================
    Gun_AimPose,
    Gun_HipFirePose,    // 腰射持枪姿态
    Gun_Fire,           // 瞄准射击
    Gun_Reload,

    Weapon_LowReady,
    Weapon_Equip,
    Weapon_Cancel,

    Attack_Normal_1,
    Attack_Normal_2,
    Attack_Normal_3,
}

public enum BattleCamp
{
    MateSide,    // 友方阵营
    EnemySide,   // 敌方阵营
}

public enum EnemyRank
{
    Minion,
    Elite,
    Boss
}

public enum WeaponEquipState
{
    Cancelled,      // 被中断/取消，不成功也不失败
    Equipping,      // 正在拿出武器
    Equipped,       // 武器装备成功
    Cancelling      // 正在收回武器
}

public enum EAnimationLogicState // 动画逻辑 ID，对应角色 Spine 中包含的动画名称
{
    #region 近战攻击类（第1位头部）
    Attack_Normal_1 = 11,
    Attack_Normal_2 = 12,
    Attack_Normal_3 = 13,
    #endregion

    // ================= 基础移动 =================
    #region 移动类（第2位头部）
    Walk = 21,
    WalkBack = 22,
    Jog = 23,
    JogBack = 24,
    JogFast = 25,
    #endregion

    Dash,
    Idle,
    Crouch,

    // ================= 枪械系统 =================
    #region 枪械类（第3位头部）
    Aim = 31,
    Fire = 32,
    Reload = 33,
    Weapon_LowReady = 34,
    NotAimShootReady = 35,

    Avoid_Full,

    // ================= 跳跃 =================
    Jump_PreStart_Full,
    Jump_Start_Full,
    Jump_Rising_Full,
    Jump_Peak_Full,
    JumpSecond__Air_Full,
    Jump_Fall_Full,
    Jump_Land_Full,
    #endregion

    // ================= 过渡动画 =================
    #region 过渡类（第500位头部）
    Jog_End_Full,    // 停步
    TurnAround,      // 转身

    AimToGundown = 501,
    GundownToAim = 502,
    GundownToNotAimShoot = 503,
    NotAimShootToGundown = 504,

    IdleToJogFast = 505,
    IdleToJogSlow = 506,
    IdleToWalk = 507,
    JogFastToIdle = 508,
    JogSlowToIdle = 509,
    WalkToIdle = 510,
    #endregion

    CombatIdle,   // 战斗待机，角色会根据当前职业的持枪姿态播放
    SkillNotice,   // 进入技能选择，但值未让角色选择时，角色会有一个提示动作
    SkillReady,    // 选择了技能并选择目标，该技能被记录，播放一个技能待机预备动作

    // =============== 技能 =============== 执行阶段，每个是一个只有一帧的技能动作
    Skill_1, Skill_2, Skill_3, Skill_4, Skill_5, Skill_6, Skill_7, Skill_8,
    Skill_9, Skill_10, Skill_11, Skill_12, Skill_13, Skill_14, Skill_15, Skill_16,

    None
}

public enum EState
{
    // ============== 移动 ==============
    Idle,
    Walk,
    Jog,
    JogFast,
    Dash,
    Walk_Back,
    Jog_Back,
    Ceouch,

    // ============== 跳跃 ==============
    Jump_None,
    Jump_PreStart,
    Jump_Start,
    Jump_Rising,
    Jump_Peak,
    JumpSecond__Air,
    Jump_Fall,
    Jump_Land,
}

public enum CharacterStateType // 有多少个状态就写多少个枚举，需要和实际的状态脚本保持一致
{
    // ============ 基础移动 ============
    Idle,
    Walk,
    Run,
    Crouch,

    // ============ 特殊动作 ============
    TurnAround,
    BrakeStop,
    Dash,

    // ============ 跳跃系列 ============
    Jump_PreStart,
    Jump_Start,
    Jump_Rising,
    Jump_Peak,
    Jump_Fall,
    Jump_Land,

    // ============ 战斗系统 ============
    CombatIdleState,
    SkillSelect,
    SkillExecute,
    SkillReact,

    // ============ 系统状态 ============
    Hurt,
    Stun,
    Death,
    Interact,

    // ============ 自定义状态（扩展用） ============
    Custom
}

public enum CharacterStats
{
    // 基础
    MaxHP,
    Defense,
    Shield,
    MaxStamina,

    // 移动
    MoveSpeed,
    WalkSpeed,
    JogSpeed,
    JogFastSpeed,
    WalkBackSpeed,
    JogBackSpeed,
    DashCooldown,

    // 通用战斗加成
    DamageBonus,
    CritChanceBonus,
    CritDamageBonus,

    // 枪械加成
    FirearmDamageBonus,
    FireRateBonus,
    ReloadSpeedBonus,
    RecoilControl,
    AccuracyBonus,
    ProjectileSpeedBonus,

    // 近战加成
    MeleeDamageBonus,
    MeleeAttackSpeedBonus,
    MeleeStaminaCostReduction,

    // 感知 / 辅助
    SearchRange,
    AimSpeedBonus
}

public enum StatModifierMode
{
    Add,            // 加法（+10 等）
    PercentAdd,     // 百分比加法（+20%）
    Multiply,       // 乘法（*1.2）
    Override        // 覆盖（如定时 MoveSpeed = 0）
}

public enum ActionEffectType
{
    Damage,
    Heal,
    SlotMove,
    Buff
}

public enum MoveType
{
    None,
    Swap,
    Push
}

public enum WeaponType
{
    Firearm,
    Melee
}

public enum FirearmCategory
{
    None = 0,

    Pistol,         // 手枪
    SubmachineGun,  // 冲锋枪
    Rifle,          // 步枪
    Shotgun,        // 霰弹枪
    Sniper,         // 狙击枪
    Energy,         // 能量武器
    Special         // 特殊武器
}

public enum GunFireModeType
{
    SemiAuto,       // 半自动：每次按键只射击一次
    Burst,          // 点射：连续射击固定发数
    FullAuto,       // 全自动：按住持续射击
    Charge,         // 蓄力：按住后释放才开火
    Beam,           // 光束：按住期间持续开火
    Special         // 特殊
}

public enum GunHitType
{
    Hitscan,
    Projectile,
}

public enum HoundCombatActionType
{
    None,
    Aim,
    Fire,
    Reload,
    /// <summary>
    /// 枪械未瞄准时的近战攻击（踢击等）。
    /// 当前实际播放 Attack_Normal_1 / Attack_Normal_2 序列。
    /// </summary>
    FirearmMelee,
    /// <summary>
    /// 独立装备了近战武器时的近战攻击。
    /// 当前只有设计，暂未实现。
    /// </summary>
    Melee,
    /// <summary>
    /// 近战攻击的技能化功能。
    /// 当前只有设计，暂未实现。
    /// </summary>
    MeleeSpecial
}

public enum TransitionSuppressReason
{
    None,
    MovementInputUnstable,
    MovementDirectionFlip,
}

public enum WeaponSlotId
{
    Slot1 = 0,
    Slot2 = 1,
    Slot3 = 2
}
```

### `Assets/Script/Character/Core/ICharacterFixedTickable.cs`

```cs
public interface ICharacterFixedTickable
{
    void ManagedFixedUpdate(float fixedDeltaTime);
}
```

### `Assets/Script/Character/Core/ICharacterLateTickable.cs`

```cs

public interface ICharacterLateTickable
{
    void ManagedLateUpdate(float deltaTime);
}
```

### `Assets/Script/Character/Core/ICharacterStateMachine.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有状态机必须实现的接口
/// </summary>
public interface ICharacterStateMachine
{
    // ============ 基本属性 ============

    /// <summary>状态机名称（用于调试）</summary>
    string StateMachineName { get; }

    /// <summary>当前是否活跃（想执行）</summary>
    bool IsActive { get; }

    /// <summary>状态优先级（数值越大优先级越高）</summary>
    int Priority { get; }

    /// <summary>当前状态名称（用于调试）</summary>
    string CurrentStateName { get; }

    //bool IsUrgentRequestActive {  get; }

    // ============ 生命周期方法 ============

    /// <summary>状态机初始化</summary>
    void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf,CharacterRoot root);

    /// <summary>进入状态机（被仲裁器选中时调用）</summary>
    void Enter();

    /// <summary>退出状态机（被仲裁器取消时调用）</summary>
    void Exit();

    /// <summary>每帧更新（只有活跃状态机会被调用）</summary>
    void Update(float deltaTime);

    /// <summary>固定更新（物理相关）</summary>
    void FixedUpdate(float fixedDeltaTime);


    //// 新增：当本状态机激活时，应发布动画请求
    //void OnEnterAnimation();
    // ============ 状态查询 ============

    /// <summary>检查是否能被指定状态中断</summary>
    bool CanBeInterruptedBy(ICharacterStateMachine other);

    /// <summary>检查是否能中断指定状态</summary>
    bool CanInterrupt(ICharacterStateMachine other);

    /// <summary>接收状态请求（外部调用）</summary>
    bool RequestState(object stateData);//需要写入回应状态的条件
}
```

### `Assets/Script/Character/Core/ICharacterSubsystem.cs`

```cs

public interface ICharacterSubsystem
{
    // 只给一个入口：总店长
    void Initialize(CharacterRoot root);
}
```
