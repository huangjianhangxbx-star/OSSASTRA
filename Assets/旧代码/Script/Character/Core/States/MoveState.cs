using PixelCrushers.DialogueSystem.Articy.Articy_4_0;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveState : MovementState
{
    private bool IsTopDownMovement =>
        characterRoot != null &&
        characterRoot.movementMode == CharacterMovementMode.TopDown;

    public override string StateName => "Move";

    public override float MovementSpeed => ResolveMoveSpeed(ResolveMovementState());
    [SerializeField, ReadOnly] private const float TurnLockMoveSpeed = 1.5f;
    public override void Enter()
    {
        SyncMovementRuntime();
        //Debug.Log("�����ƶ�״̬");
    }

    public override void Exit()
    {
        // 不再无条件清零 velocity，避免破坏跳跃 Y 速度
        // 仅清零横向速度，保留纵向
        if (rigidbody != null)
        {
            rigidbody.velocity = IsTopDownMovement
                ? Vector2.zero
                : new Vector2(0f, rigidbody.velocity.y);
        }
    }

    public override void Update(float deltaTime)
    {
        if (dataBus.moveInputAmount < 0.01f)
        {
            stateMachine.TransitionTo<IdleState>();
            return;
        }
        SyncMovementRuntime();

        EState movementState = ResolveMovementState();

        dataBus.currentMovementState = movementState;
        dataBus.currentMoveSpeed = ResolveMoveSpeed(movementState);
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        // 跳跃所有权防线：跳跃状态机活跃时，移动状态机不得写物理速度
        if (dataBus.currentJumpState != EState.Jump_None)
            return;

        if (dataBus.moveInputAmount < 0.01f)
        {
            // 无输入：只清横向，保留 Y
            rigidbody.velocity = IsTopDownMovement
                ? Vector2.zero
                : new Vector2(0f, rigidbody.velocity.y);
            dataBus.currentMoveSpeed = 0f;
            dataBus.currentMovementState = EState.Idle;
            return;
        }

        EState movementState = ResolveMovementState();
        float logicSpeed = ResolveMoveSpeed(movementState);
        float physicalSpeed = logicSpeed;

        if (ShouldLockMoveSpeedForTurn())
        {
            physicalSpeed = TurnLockMoveSpeed;
        }

        Vector2 input = dataBus.moveInput;

        if (IsTopDownMovement)
        {
            rigidbody.velocity = input.normalized * physicalSpeed;
            dataBus.currentMovementState = movementState;
        }
        else if (dataBus.isGrounded)
        {
            // 接地：完整控制
            rigidbody.velocity = input.normalized * physicalSpeed;
            dataBus.currentMovementState = movementState;
        }
        else
        {
            // 空中：只控制横向，保留 Y
            rigidbody.velocity = new Vector2(input.normalized.x * physicalSpeed, rigidbody.velocity.y);
        }

        dataBus.currentMoveSpeed = logicSpeed;
    }



    private float ResolveMoveSpeed(EState movementState)
    {
        if (movementState == EState.Idle)
            return 0f;

        CharacterStatsController stats = stateMachine.StatController;

        float stateBaseSpeed = ResolveStateBaseMoveSpeed(stats, movementState);
        float globalMoveSpeedBonus = ResolveGlobalMoveSpeedBonus(stats);

        return Mathf.Max(0f, stateBaseSpeed + globalMoveSpeedBonus);
    }
    private float ResolveStateBaseMoveSpeed(CharacterStatsController stats, EState movementState)
    {
        switch (movementState)
        {
            case EState.Walk:
                return GetStatOrDefault(stats, CharacterStats.WalkSpeed, 2f);

            case EState.Jog:
                return GetStatOrDefault(stats, CharacterStats.JogSpeed, 4f);

            case EState.JogFast:
                return GetStatOrDefault(stats, CharacterStats.JogFastSpeed, 8f);

            case EState.Walk_Back:
                return GetStatOrDefault(stats, CharacterStats.WalkBackSpeed, 2f);

            case EState.Jog_Back:
                return GetStatOrDefault(stats, CharacterStats.JogBackSpeed, 4f);

            default:
                return 0f;
        }
    }

    private float ResolveGlobalMoveSpeedBonus(CharacterStatsController stats)
    {
        if (stats == null)
            return 0f;

        return stats.GetStat(CharacterStats.MoveSpeed);
    }








    private float GetStatOrDefault(
        CharacterStatsController stats,
        CharacterStats stat,
        float fallback)
    {
        if (stats == null)
            return fallback;

        float value = stats.GetStat(stat);

        if (value <= 0f)
            return fallback;

        return value;
    }








    private EState ResolveMovementState()
    {
        if (dataBus.moveInputAmount < 0.01f)
            return EState.Idle;

        bool backward = IsMovingBackwardNow();

        if (backward)
        {
            return EState.Walk_Back;
        }

        if (dataBus.isForceWalkSlow)
            return EState.Walk;

        if (dataBus.dashPressed)
            return EState.JogFast;

        return EState.Jog;
    }

    private bool ShouldLockMoveSpeedForTurn()
    {
        if (dataBus == null)
            return false;

        return dataBus.isMoveTurnMovementLocked;
    }


    private void SyncMovementRuntime()
    {
        if (dataBus == null)
            return;

        if (dataBus.moveInputAmount < 0.01f)
        {
            dataBus.currentMovementState = EState.Idle;
            dataBus.currentMoveSpeed = 0f;
            return;
        }

        EState movementState = ResolveMovementState();

        dataBus.currentMovementState = movementState;
        dataBus.currentMoveSpeed = ResolveMoveSpeed(movementState);
    }


    private bool IsMovingBackwardNow()
    {
        if (dataBus == null)
            return false;

        if (dataBus.moveInputAmount < 0.01f)
        {
            dataBus.isMovingBackward = false;
            return false;
        }

        Vector2 move = dataBus.moveInput;

        if (IsTopDownMovement &&
            dataBus.activeFacingMode == CharacterFacingMode.Cursor)
        {
            Vector2 aim = dataBus.aimDirection;

            if (aim.sqrMagnitude <= 0.0001f)
            {
                aim = (Vector2)(
                    dataBus.mousePositionOnScreen -
                    dataBus.characterPosition);
            }

            bool isBackpedaling = TopDownBackpedalPolicy.Resolve(
                move,
                aim,
                dataBus.isMovingBackward);

            dataBus.isMovingBackward = isBackpedaling;
            return isBackpedaling;
        }

        if (Mathf.Abs(move.x) < 0.01f)
        {
            dataBus.isMovingBackward = false;
            return false;
        }

        int moveSign = move.x >= 0f ? 1 : -1;
        int facingSign = dataBus.currentFacing == Facing.Right ? 1 : -1;

        bool isMovingBackward = moveSign != facingSign;
        dataBus.isMovingBackward = isMovingBackward;
        return isMovingBackward;
    }



    public override bool CanTransitionTo(MovementState newState)
    {
        return newState is IdleState;
    }


}
