using UnityEngine;
using static CharacterDataBus;

public class HoundCombatAimState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Aim";

    public override void Enter()
    {
        dataBus.isAiming = true;
        dataBus.isHoundAttacking = false;
        dataBus.isReloading = false;
        dataBus.currentHoundCombatAction = HoundCombatActionType.Aim;

        stateMachine.MarkCombatIntent();
        stateMachine.SubmitAimPoseIntent();
    }

    public override void Update(float deltaTime)
    {
        /*
    * 枪械、装备状态或角色状态已经不允许瞄准。
    */
        if (!stateMachine.CanAim())
        {
            ExitAimToIdle();
            return;
        }

        /*
         * 右键松开，立即退出瞄准并放下枪。
         */
        if (!dataBus.aimHeld)
        {
            ExitAimToIdle();
            return;
        }

        /*
         * 当前仍然处于有效瞄准状态。
         */
        dataBus.isAiming = true;
        dataBus.isHoundAttacking = false;
        dataBus.isReloading = false;

        dataBus.currentHoundCombatAction = HoundCombatActionType.Aim;

        stateMachine.MarkCombatIntent();
        stateMachine.SubmitAimPoseIntent();
        /*
         * 瞄准状态下允许换弹。
         */
        if (dataBus.reloadPressedThisFrame && stateMachine.CanRequestReload())
        {
            stateMachine.MarkCombatIntent();
            stateMachine.TransitionTo<HoundCombatReloadState>();
            return;
        }

        /*
         * 只有左键的新按下事件才能从 Aim 进入 Fire。
         *
         * 不使用 Held 作为入口，
         * 避免先按住左键、再按右键时自动开枪。
         */
        if (dataBus.houndAttackPressedThisFrame)
        {
            stateMachine.MarkCombatIntent();
            stateMachine.TransitionTo<HoundCombatFireState>();
            return;
        }
    }

    public override void Exit()
    {
        /*
         * Aim -> Fire 时，这里会先执行。
         * FireState.Enter() 随后会重新设置 isAiming = true。
         */
        if (dataBus.currentHoundCombatAction ==
            HoundCombatActionType.Aim)
        {
            dataBus.currentHoundCombatAction =
                HoundCombatActionType.None;
        }

        dataBus.isAiming = false;
    }

    private void ExitAimToIdle()
    {
        dataBus.isAiming = false;
        dataBus.isHoundAttacking = false;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;

        /*
         * 先把枪械姿态切换为 LowReady，
         * 再进入 Idle。
         *
         * 不把放下枪完全交给 IdleState，
         * 避免旧 Aim Pose 因状态/动画去重而残留。
         */
        stateMachine.RemoveWeaponActionIntent();
        stateMachine.SubmitLowReadyPoseIntent();

        stateMachine.TransitionTo<HoundCombatIdleState>();
    }
}