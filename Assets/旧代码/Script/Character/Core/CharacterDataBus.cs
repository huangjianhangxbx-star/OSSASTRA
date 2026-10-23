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
    public CharacterFacingMode activeFacingMode { get; set; } = CharacterFacingMode.Cursor;
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
