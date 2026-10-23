using UnityEngine;
using static CharacterDataBus;

//只要左键还在请求开火：
//1. 判断当前 FireStance
//2. 请求对应姿势
//3. 调 GunSystemController 尝试真实开火
//4. 如果真实 fired，提交 Fire 动画
//5. 如果不再开火，根据 aimHeld 返回 Aim 或 Idle
public class HoundCombatFireState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Fire";

    private float firePoseTimer;

    public override void Enter()
    {
        if (!stateMachine.CanFireCurrentFirearm())
        {
            ExitFireToLowReady();
            return;
        }

        dataBus.currentHoundCombatAction =
            HoundCombatActionType.Fire;

        dataBus.isHoundAttacking = true;
        dataBus.isAiming = dataBus.aimHeld;
        dataBus.isReloading = false;

        firePoseTimer = 0f;

        stateMachine.MarkCombatIntent();
        SubmitCurrentStancePose();

        /*
         * 第一枪必须在 Enter 中处理。
         *
         * AimState 是在 houndAttackPressedThisFrame 为 true 时
         * 切换到 FireState 的。
         *
         * 如果等下一帧 FireState.Update 才调用开火，
         * InputHandler.LateUpdate 已经会清除 PressedThisFrame，
         * 半自动武器可能收不到第一次点击。
         */
        TryFireCurrentFrame();
    }

    public override void Update(float deltaTime)
    {
        if (!stateMachine.CanFireCurrentFirearm())
        {
            ExitFireToLowReady();
            return;
        }

        dataBus.currentHoundCombatAction =
            HoundCombatActionType.Fire;

        dataBus.isHoundAttacking = true;
        dataBus.isAiming = dataBus.aimHeld;
        dataBus.isReloading = false;

        stateMachine.MarkCombatIntent();
        SubmitCurrentStancePose();

        /*
         * 瞄准开火期间请求换弹。
         */
        if (dataBus.reloadPressedThisFrame && stateMachine.CanRequestReload())
        {
            InterruptFireToReload();
            return;
        }

        /*
         * 已经合法进入 FireState 后，
         * Held 可以交给 GunSystemController：
         *
         * SemiAuto：通常只响应 PressedThisFrame
         * FullAuto：通常响应 Held
         * Burst：由枪械系统管理 burst
         */
        if (!dataBus.houndAttackHeld)
            firePoseTimer -= deltaTime;

        FirearmFireContinuation continuation =
            HoundCombatActionStateMachine.ResolveFireContinuation(
                dataBus.houndAttackHeld,
                dataBus.aimHeld,
                firePoseTimer <= 0f);

        switch (continuation)
        {
            case FirearmFireContinuation.ContinueFire:
                TryFireCurrentFrame();
                return;

            case FirearmFireContinuation.WaitForPose:
                return;

            case FirearmFireContinuation.Aim:
                stateMachine.TransitionTo<HoundCombatAimState>();
                return;

            default:
                ExitFireToLowReady();
                return;
        }
    }

    public override void Exit()
    {
        dataBus.isHoundAttacking = false;

        if (dataBus.currentHoundCombatAction ==
            HoundCombatActionType.Fire)
        {
            dataBus.currentHoundCombatAction =
                HoundCombatActionType.None;
        }

        /*
         * 不在这里无条件将 isAiming 设为 false。
         *
         * 因为 Fire -> Aim 时，角色仍然处于瞄准状态；
         * 具体值由目标状态的 Enter() 决定。
         */
    }

    private void TryFireCurrentFrame()
    {
        bool fired = stateMachine.FireCurrentWeapon();

        if (!fired)
            return;

        stateMachine.SubmitFireIntent();
        firePoseTimer = stateMachine.GetFireDuration();
    }

    private void SubmitCurrentStancePose()
    {
        FirearmStance stance =
            HoundCombatActionStateMachine.ResolveFirearmStance(dataBus.aimHeld);

        if (stance == FirearmStance.Ads)
            stateMachine.SubmitAimPoseIntent();
        else
            stateMachine.SubmitHipFirePoseIntent();
    }

    private void ExitFireToLowReady()
    {
        dataBus.isHoundAttacking = false;
        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;

        /*
         * 清掉 Fire 动作意图，并明确提交 LowReady。
         */
        stateMachine.RemoveWeaponActionIntent();
        stateMachine.SubmitLowReadyPoseIntent();

        stateMachine.TransitionTo<HoundCombatIdleState>();
    }

    private void InterruptFireToReload()
    {
        dataBus.isHoundAttacking = false;
        dataBus.isAiming = dataBus.aimHeld;
        dataBus.isReloading = true;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.Reload;

        stateMachine.RemoveWeaponActionIntent();
        stateMachine.TransitionTo<HoundCombatReloadState>();
    }
}
