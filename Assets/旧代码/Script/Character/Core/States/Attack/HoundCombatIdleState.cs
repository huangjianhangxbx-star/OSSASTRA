using UnityEngine;
using static CharacterDataBus;

public class HoundCombatIdleState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Idle";

    public override void Enter()
    {
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;

        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.isHoundAttacking = false;

        stateMachine.RemoveWeaponActionIntent();

        AnimationIntent poseIntent =
            stateMachine.GetWeaponPoseIntent();

        if (poseIntent == null)
            return;

        /*
         * Idle 下不允许继续保持瞄准 Pose。
         */
        if (poseIntent.semantic == EAnimSemantic.Gun_AimPose &&
            !dataBus.aimHeld)
        {
            stateMachine.SubmitLowReadyPoseIntent();
            return;
        }

        /*
         * 兼容旧腰射意图残留。
         * 新代码不会再提交 Gun_HipFirePose。
         */
        if (poseIntent.semantic == EAnimSemantic.Gun_HipFirePose)
        {
            stateMachine.SubmitLowReadyPoseIntent();
        }
    }

    public override void Update(float deltaTime)
    {
        HoundCombatActionStateMachine.CombatInputDecision decision = stateMachine.EvaluateIdleInput();
        stateMachine.ExecuteIdleInputDecision(decision);

        ///*
        // * 没有近战输入时，右键进入 AimState。
        // */
        //if (dataBus.aimHeld && stateMachine.CanAim())
        //{
        //    stateMachine.MarkCombatIntent();
        //    stateMachine.TransitionTo<HoundCombatAimState>();
        //    return;
        //}

        //if (dataBus.reloadPressedThisFrame &&
        //    stateMachine.CanReload())
        //{
        //    stateMachine.MarkCombatIntent();
        //    stateMachine.TransitionTo<HoundCombatReloadState>();
        //    return;
        //}
    }

}
