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
    // Retained to preserve serialized enum values from older firearm profiles.
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
