
using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 移动状态机 - 管理基础移动状态（行走、奔跑、停止,下蹲）
/// </summary>
public class MovementStateMachine : ICharacterStateMachine, IAnimationProvider
{
    // ============ ICharacterStateMachine 接口实现 ============
    public string StateMachineName => "MovementStateMachine";

    public int Priority => 10;

    public string CurrentStateName => currentState?.StateName ?? "None";

    private CharacterStatsController statController;
    public CharacterStatsController StatController => statController;

    public bool IsActive
    {
        get
        {
            return dataBus != null && dataBus.isAlive && dataBus.canMove;
        }
    }

    // ============ 内部状态管理 ============
    private Dictionary<Type, MovementState> states;
    private MovementState currentState;

    private Vector3 lastCharacterPosition;

    private EAnimSemantic lastLogicSemantic;
    private bool hasSubmittedAnimationIntent;

    private int submittedFacingChangeId = -1;
    private int movementIntentReplayId;

    private EAnimSemantic committedMovementSemantic = EAnimSemantic.Idle;
    private EAnimSemantic pendingMovementSemantic = EAnimSemantic.Idle;
    private float pendingMovementSemanticTimer;
    private bool hasCommittedMovementSemantic;

    private const float ForwardBackSwitchConfirmTime = 0.06f;

    // 组件引用
    private CharacterDataBus dataBus;
    private Transform transform;
    private Rigidbody2D rigidbody;
    private CharacterRoot characterRoot;
    private StateArbitrator stateArbitrator;

    // 动画意图提交器
    private CharacterAnimationComposer animationComposer;
    private JumpGroundProbe _groundProbe;

    private bool IsTopDownMovement =>
        characterRoot != null &&
        characterRoot.movementMode == CharacterMovementMode.TopDown;

    // ============ 初始化 ============
    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;

        if (dataBus == null)
        {
            Debug.LogError("MovementStateMachine: 缺少 CharacterDataBus");
            return;
        }

        rigidbody = rb;
        transform = tf;
        characterRoot = root;

        if (IsTopDownMovement && rigidbody != null)
            rigidbody.gravityScale = 0f;

        // 创建地面探测器（与 JumpStateMachine 共享 isGrounded 权威写入）
        var collider = characterRoot.GetComponent<Collider2D>();
        if (collider == null)
            collider = characterRoot.GetComponentInChildren<Collider2D>();
        if (collider != null && characterRoot.jumpConfig != null)
        {
            _groundProbe = new JumpGroundProbe();
            _groundProbe.Initialize(collider, characterRoot.groundLayerMask, characterRoot.jumpConfig);
        }

        animationComposer = characterRoot.GetComponent<CharacterAnimationComposer>();
        if (animationComposer == null)
        {
            Debug.LogError("Missing CharacterAnimationComposer on CharacterRoot!");
            return;
        }

        statController = characterRoot.GetComponent<CharacterStatsController>();

        InitializeStates();

        TransitionTo<IdleState>();
    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, MovementState>
        {
            [typeof(IdleState)] = new IdleState(),
            [typeof(MoveState)] = new MoveState()
        };

        foreach (MovementState state in states.Values)
        {
            state.Initialize(this, dataBus, transform, rigidbody, characterRoot);
        }
    }

    // ============ 状态转换 ============
    public bool TransitionTo<T>() where T : MovementState
    {
        if (!states.TryGetValue(typeof(T), out MovementState newState))
        {
            Debug.LogWarning($"状态 {typeof(T).Name} 不存在");
            return false;
        }

        if (currentState != null && !currentState.CanTransitionTo(newState))
        {
            Debug.LogWarning($"无法从 {currentState.StateName} 转换到 {newState.StateName}");
            return false;
        }

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();

        return true;
    }

    public T GetState<T>() where T : MovementState
    {
        if (states.TryGetValue(typeof(T), out MovementState state))
        {
            return state as T;
        }

        return null;
    }

    // ============ ICharacterStateMachine 接口方法 ============
    public void Enter()
    {
        Debug.Log($"{StateMachineName} 被激活");

        if (currentState == null)
        {
            TransitionTo<IdleState>();
        }
        else
        {
            currentState.Enter();
        }

        // 激活时主动提交一次当前移动动画意图
        hasSubmittedAnimationIntent = false;
        submittedFacingChangeId = -1;
        movementIntentReplayId = 0;

        committedMovementSemantic = EAnimSemantic.Idle;
        pendingMovementSemantic = EAnimSemantic.Idle;
        pendingMovementSemanticTimer = 0f;
        hasCommittedMovementSemantic = false;

        OnEnterAnimation(999f);
    }

    public void Exit()
    {
        animationComposer?.RemoveIntent(AnimationIntentSourceStateMachine.Movement);
        hasSubmittedAnimationIntent = false;
    }

    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);

        UpdateMovementLogic(deltaTime);

        // 实际是刷新移动动画意图
        OnEnterAnimation(deltaTime);
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        MovementExcute(fixedDeltaTime);
    }

    private void UpdateMovementLogic(float deltaTime)
    {
        // 这里保留所有移动状态共享逻辑
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

    public bool RequestState(object stateData)
    {
        if (stateData is string requestedState)
        {
            switch (requestedState)
            {
                case "ForceIdle":
                    TransitionTo<IdleState>();
                    return true;

                case "ForceMove":
                    TransitionTo<MoveState>();
                    return true;
            }
        }

        return false;
    }

    private void MovementExcute(float fixedDeltaTime)
    {
        // 跳跃状态机活跃时，移动状态机不做地面探测和重力
        // 由 JumpStateMachine 统一管理 isGrounded 和空中物理
        if (dataBus.currentJumpState != EState.Jump_None)
        {
            // 只更新移动距离和位置记录
            float moveDistance = Vector2.Distance(rigidbody.position, lastCharacterPosition);
            dataBus.isMoving = moveDistance > 0.001f;
            dataBus.characterPosition = rigidbody.position;
            lastCharacterPosition = rigidbody.position;
            return;
        }

        if (characterRoot != null &&
            characterRoot.controlAuthority != null &&
            !characterRoot.controlAuthority.IsAllowed(CharacterControlChannel.Move))
        {
            rigidbody.velocity = Vector2.zero;
            dataBus.currentMoveSpeed = 0f;
            UpdateMovementTracking();
            return;
        }

        if (IsTopDownMovement)
        {
            currentState?.FixedUpdate(fixedDeltaTime);
            UpdateMovementTracking();
            return;
        }

        // 地面探测（唯一权威 isGrounded 写入者之一）
        if (_groundProbe != null)
        {
            _groundProbe.Probe();
            dataBus.isGrounded = _groundProbe.IsGrounded;
            if (dataBus.isGrounded)
                dataBus.lastGroundedTime = Time.time;
        }

        currentState?.FixedUpdate(fixedDeltaTime);

        // 自定义重力：gravityScale=0 时由这里统一应用基础重力
        if (rigidbody != null && dataBus != null && dataBus.isGrounded)
        {
            // 接地时保持微小下压力，确保贴地
            Vector2 v = rigidbody.velocity;
            if (v.y > -0.1f)
                rigidbody.velocity = new Vector2(v.x, -0.1f);
        }
        else if (rigidbody != null && dataBus != null && !dataBus.isGrounded)
        {
            // 空中但未跳跃时（从平台走出），应用下落重力
            var config = characterRoot?.jumpConfig;
            float gravity = config != null ? config.baseGravityAcceleration * config.fallGravityMultiplier : 30f;
            float maxFall = config != null ? config.maxFallSpeed : 20f;

            Vector2 v = rigidbody.velocity;
            v.y -= gravity * fixedDeltaTime;
            if (v.y < -maxFall) v.y = -maxFall;
            rigidbody.velocity = v;
        }

        UpdateMovementTracking();
    }

    private void UpdateMovementTracking()
    {
        float moveDistance2 = Vector2.Distance(rigidbody.position, lastCharacterPosition);
        bool isMoving = moveDistance2 > 0.001f;

        dataBus.isMoving = isMoving;
        dataBus.characterPosition = rigidbody.position;

        lastCharacterPosition = rigidbody.position;
    }

    /// <summary>
    /// 根据移动逻辑提交动画意图。
    /// 注意：这里不再直接解析 CharacterAnimationPlan，也不再直接触发 AnimationPlanRequest。
    /// </summary>
    public void OnEnterAnimation(float deltaTime)
    {
        EAnimSemantic rawSemantic = GetMovementLogicID();

        EAnimSemantic stableSemantic = ResolveStableMovementSemantic(
            rawSemantic,
            deltaTime
        );

        bool semanticChanged =
            !hasSubmittedAnimationIntent ||
            lastLogicSemantic != stableSemantic;

        bool shouldReplayForFacingChange =
            dataBus != null &&
            dataBus.isFacingChanging &&
            dataBus.requestedFacingSource == CharacterDataBus.RequestedFacingSource.Move &&
            submittedFacingChangeId != dataBus.facingChangeId;

        if (!semanticChanged && !shouldReplayForFacingChange)
            return;

        if (shouldReplayForFacingChange)
        {
            submittedFacingChangeId = dataBus.facingChangeId;
            movementIntentReplayId++;
        }

        lastLogicSemantic = stableSemantic;
        hasSubmittedAnimationIntent = true;

        SubmitMovementAnimationIntent(stableSemantic);
    }

    private EAnimSemantic GetMovementLogicID()
    {
        if (dataBus == null)
            return EAnimSemantic.Idle;

        switch (dataBus.currentMovementState)
        {
            case EState.Idle:
                return EAnimSemantic.Idle;

            case EState.Walk:
                return EAnimSemantic.Walk;

            case EState.Jog:
                return EAnimSemantic.Jog;

            case EState.JogFast:
                return EAnimSemantic.JogFast;

            case EState.Walk_Back:
                return EAnimSemantic.Walk_Back;

            case EState.Jog_Back:
                return EAnimSemantic.Jog_Back;
        }

        return EAnimSemantic.Idle;
    }


    private EAnimSemantic ResolveStableMovementSemantic(EAnimSemantic rawSemantic, float deltaTime)
    {
        /*
         * 第一次提交，直接接受。
         */
        if (!hasCommittedMovementSemantic)
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * Idle 应该立即响应。
         * 松开移动键时不要拖泥带水，否则手感会黏。
         */
        if (rawSemantic == EAnimSemantic.Idle)
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * Dash 这类强动作也立即响应。
         */
        if (rawSemantic == EAnimSemantic.JogFast)
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * 关键：
         * 如果 OrientationSystem 正处于“移动翻面确认中”，
         * 并且 rawSemantic 试图在 Jog/Jog_Back 或 Walk/Walk_Back 之间切换，
         * 那么先冻结在 committedMovementSemantic。
         *
         * 这可以消除：
         * Jog_Back -> Jog -> Idle
         * 或 Jog -> Jog_Back -> Jog
         * 这种翻面窗口内的高速抽搐。
         */
        bool isMoveTurnPending =
            characterRoot != null &&
            characterRoot.orientationSystem != null &&
            characterRoot.orientationSystem.IsMoveTurnPending;

        if (isMoveTurnPending &&
            IsForwardBackSwitch(committedMovementSemantic, rawSemantic))
        {
            pendingMovementSemantic = rawSemantic;
            pendingMovementSemanticTimer = 0f;

            return committedMovementSemantic;
        }

        /*
         * 如果不是前/后同速切换，比如：
         * Idle -> Jog_Back
         * Walk_Back -> Jog_Back
         * Jog -> Dash
         * 这些可以立即接受。
         */
        if (!IsForwardBackSwitch(committedMovementSemantic, rawSemantic))
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * 走到这里说明是：
         * Walk <-> Walk_Back
         * Jog <-> Jog_Back
         * 这种最容易抖的切换。
         *
         * 要求 rawSemantic 稳定一小段时间后才提交。
         */
        if (pendingMovementSemantic != rawSemantic)
        {
            pendingMovementSemantic = rawSemantic;
            pendingMovementSemanticTimer = 0f;
        }

        pendingMovementSemanticTimer += deltaTime;

        if (pendingMovementSemanticTimer >= ForwardBackSwitchConfirmTime)
        {
            CommitMovementSemantic(rawSemantic);
        }

        return committedMovementSemantic;
    }


    private void CommitMovementSemantic(EAnimSemantic semantic)
    {
        committedMovementSemantic = semantic;
        pendingMovementSemantic = semantic;
        pendingMovementSemanticTimer = 0f;
        hasCommittedMovementSemantic = true;
    }

    private bool IsForwardBackSwitch(EAnimSemantic from, EAnimSemantic to)
    {
        if (from == to)
            return false;

        if (from == EAnimSemantic.Walk && to == EAnimSemantic.Walk_Back)
            return true;

        if (from == EAnimSemantic.Walk_Back && to == EAnimSemantic.Walk)
            return true;

        if (from == EAnimSemantic.Jog && to == EAnimSemantic.Jog_Back)
            return true;

        if (from == EAnimSemantic.Jog_Back && to == EAnimSemantic.Jog)
            return true;

        return false;
    }





    private void SubmitMovementAnimationIntent(EAnimSemantic semantic)
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.Movement,
            semantic = semantic,
            layer = AnimationLayer.BaseFullBody,
            priority = 10,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = movementIntentReplayId
        });
    }

    // ============ 工具方法 ============
    public bool IsInState<T>() where T : MovementState
    {
        return currentState is T;
    }

    public float GetCurrentSpeed()
    {
        return currentState?.MovementSpeed ?? 0f;
    }
}
