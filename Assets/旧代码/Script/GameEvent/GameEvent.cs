public enum GameEvent
{
    //======================动画系统=============
    AnimationRequest,
    AnimationPlanRequest,

    #region AINO的动作
    ANOIR_MoveOrNot,

    ANOIR_IsWalk,
    ANOIR_IsJog,
    ANOIR_IsDash,
    ANOIR_IsIdle,
    ANOIR_IsCrouch,
    ANOIR_IsCrouchWalk,
    ANOIR_IsAvoid,
    ANOIR_IsAim,
    ANOIR_IsReload,//用于换弹动画
    ANOIR_IsEquip,//用于装备武器动画
    ANOIR_IsDisboard,//用于卸载武器动画

    ANOIR_IsSlide,

    ANOIR_IsBrakeStop,



    VELGITA_ShootTriggered,
    VELGITA_ShootEnd,






    ANOIR_IsJump,//概括所有自主滞空情况，包括主动跳跃，直接下落等

    ANOIR_IsJumpStart,
    ANOIR_IsInAir,
    ANOIR_IsJumpEnd,


    VELGITA_IsFireOrLightAttack_StartSingle,
    VELGITA_IsFireOrLightAttack_StartContinue,
    VELGITA_IsFireOrLightAttack_End,






    ANOIR_IsCloseAttack,
    ANOIR_IsAttack,




    #region 输入阻断
    ANOIR_CanNotAct,
    ANOIR_CanAct,

    #endregion

    #region 受伤状态
    ANOIR_Injured_Start,
    ANOIR_Injured_End,

    #endregion





    #endregion

    #region 鼠标输入状态
    Input_LeftButtonDown,
    Input_LeftButtonContinue,
    Input_LeftButtonUp,
    Input_RightButtonDown,
    Input_RightButtonContinue,
    Input_RightButtonUp,
    Input_ScrollUp,
    Input_ScrollDown,
    #endregion


    #region 键盘输入状态(使用高级输入系统，只需要up/dwon两个状态
    Input_CrouchKeyDown,//下蹲
    Input_CrouchKeyUp,
    Input_MovementKeyDown,//移动按键较为特殊，只需要判断是否摁下（WSAD）
    Input_MovementKeyUp,
    Input_PickUpKeyDown,//拾取
    Input_PickUpKeyUp,
    Input_DashKeyDown,//冲刺
    Input_DashKeyUp,
    Input_AvoidKeyDown,//冲刺
    Input_AvoidKeyUp,
    Input_WeaponStock1KeyDown,
    Input_WeaponStock2KeyDown,
    Input_ReloadKeyDown,
    Input_ReloadKeyUp,

    #endregion

    #region 武器系统/道具系统
    //广播：PickUpItem
    //接收：WeaponManager/
    ANOIR_GetItem,
    ANOIR_GetWeapon,
    ANOIR_HasEquipWeapon,
    ANOIR_HasDisboardWeapon,

    #endregion



    #region 背包系统
    ANOIR_BagSwitch,


    #endregion



    #region 战斗系统
    //============UI============
    ShowSkillUI,    // 逻辑层通知 UI：请刷新这 4 个技能图标

    RefreshTimeLineUI, // 逻辑层通知 UI：请刷新技能图标（比如变灰、亮起等）

    TargetSelect,//进入TargetSelectState,选择技能释放目标

    SkillSelectChange,//当玩家选择了一个技能，流程进入了TargetSelectState，却又点击了另一个技能UI图标，发布这个通知
    SkillClicked,  // UI 层通知逻辑层：玩家刚才点击了这个技能图标
    SkillSelectFinish,// 玩家选完了，发布通知
    ShowCharacterPromptUI,// 逻辑层通知 UI：请显示这个角色的提示UI（比如头像、名字等）









    BattleSystem_Start, 
    
    BattleSystem_End,


    #endregion



}