using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleState : MovementState
{
    private bool IsTopDownMovement =>
        characterRoot != null &&
        characterRoot.movementMode == CharacterMovementMode.TopDown;

    public override string StateName => "Idle";
    public override float MovementSpeed => 0f;

    private float idleTime = 0f;

    public override void Enter()
    {
        idleTime = 0f;
        dataBus.currentMoveSpeed = 0f;
        dataBus.currentMovementState = EState.Idle;
        dataBus.isMovingBackward = false;

        // 只清横向速度，保留纵向（避免破坏跳跃 Y 速度）
        if (rigidbody != null)
        {
            rigidbody.velocity = IsTopDownMovement
                ? Vector2.zero
                : new Vector2(0f, rigidbody.velocity.y);
        }

    }

    public override void Update(float deltaTime)
    {

        idleTime += deltaTime;
        //================�ڲ�С״̬ת��=============
        dataBus.currentMovementState = EState.Idle;
        //=============��״̬ת��===================
        if (dataBus.moveInputAmount > 0.1f) // ������
        {
            //Debug.Log($"Idle��⵽����: {dataBus.moveInput}, �л���Walk");
            stateMachine.TransitionTo<MoveState>();

            return; // ��Ҫ��ת���������˳�
        }
        // ����������ÿ5�벥��һ�κ�������
        if (idleTime >= 5f)
        {
            // PlayIdleBreathAnimation();
            idleTime = 0f;
        }
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        // Idle״̬ȷ���ٶ�Ϊ0
        if (rigidbody != null)
        {
            rigidbody.velocity = IsTopDownMovement
                ? Vector2.zero
                : new Vector2(0f, rigidbody.velocity.y);
        }
    }

    public override bool CanTransitionTo(MovementState newState)
    {
        // ���Դ�վ���л������߻���
        return newState is MoveState;
    }
}
