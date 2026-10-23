//本脚本用于定义ANOIR的状态






public enum ANOIR_BasicActionState_LongTerm //ANOIR主体的实时主要状态
{
    //每个状态匹配一个动画，如果动画大类有区别，一定要设立一个新状态种类
    ANOIR_Walk,
    ANOIR_Idle,
    ANOIR_Run,
    ANOIR_Crouch,
    ANOIR_CrouchWalk,

    ANOIR_Jump,


}


public enum ANOIR_BasicActionState_ShortTerm //ANOIR主体的实时主要状态
{
    //每个状态匹配一个动画，如果动画大类有区别，一定要设立一个新状态种类
    NoneState,
    ANOIR_Avoid,
    ANOIR_Slide,
    ANOIR_BrakeStop,  // 新增刹停状态




    ANOIR_JumpStart,
    ANOIR_JumpInAir,
    ANOIR_JumpEnd,

}








//public enum ANOIR_CrouchState
//{
//    ANOIR_IsCrouch,
//    ANOIR_NoCrouch


//}




public enum ANOIR_AimState
{
    ANOIR_Aim,
    ANOIR_NoAim
}


public enum ANOIR_CloseAttackState
{
    ANOIR_CloseAttack,
    ANOIR_NoCloseAttack

}



public enum ANOIR_WeaponState
{
    ANOIR_HoldMachineGun,
    ANOIR_HoldPistal
}