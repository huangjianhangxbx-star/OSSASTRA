using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 跳跃状态机。直接有限状态机，不再使用 StateStackManager。
/// 集成土狼时间、输入缓存、局部重力坐标和地面探测。
/// </summary>
public class JumpStateMachine : ICharacterStateMachine, IAnimationProvider
{
    private readonly Dictionary<EState, JumpStateBase> states = new();
    private JumpStateBase currentState;

    private CharacterDataBus dataBus;
    private Transform transform;
    private Rigidbody2D rigidbody2D;
    private CharacterRoot characterRoot;
    private CharacterAnimationConfigManager configMgr;
    private CharacterAnimationComposer _animationComposer;

    // 配置与探测
    private JumpConfigSO _config;
    private JumpGroundProbe _groundProbe;
    private Collider2D _collider;
    private LayerMask _groundLayerMask;

    // Coyote Time / Jump Buffer
    private bool _jumpConsumed;

    // 防止起跳瞬间误判地面：必须确实离开地面后才允许进入 Land
    public bool HasLeftGround => _hasLeftGround;
    private bool _hasLeftGround;

    // 动画去重
    private EAnimationLogicState lastLogicID;

    public string StateMachineName => "JumpStateMachine";
    public int Priority => 20;
    public string CurrentStateName => currentState?.StateName ?? "None";
    public bool IsActive => currentState != null;

    /// <summary>暴露给子状态使用的配置</summary>
    public JumpConfigSO Config => _config;
    /// <summary>暴露地面探测给子状态</summary>
    public JumpGroundProbe GroundProbe => _groundProbe;
    /// <summary>重力方向</summary>
    public Vector2 GravityDown => Vector2.down;
    public Vector2 GravityUp => Vector2.up;
    public Vector2 LateralAxis => Vector2.right;

    // ============ 初始化 ============
    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        this.dataBus = bus;
        this.rigidbody2D = rb;
        this.transform = tf;
        this.characterRoot = root;

        if (dataBus == null)
        {
            Debug.LogError("[JumpStateMachine] 缺少 CharacterDataBus");
            return;
        }

        configMgr = characterRoot.GetComponent<CharacterAnimationConfigManager>();
        if (configMgr == null)
            Debug.LogError("[JumpStateMachine] Missing CharacterAnimationConfigManager");

        _animationComposer = characterRoot.GetComponent<CharacterAnimationComposer>();

        // 获取配置
        _config = characterRoot.jumpConfig;
        if (_config == null)
        {
            Debug.LogWarning("[JumpStateMachine] jumpConfig 未配置，使用默认值");
        }

        // 获取 Collider
        _collider = characterRoot.GetComponent<Collider2D>();
        if (_collider == null)
            _collider = characterRoot.GetComponentInChildren<Collider2D>();

        // 获取地面层
        _groundLayerMask = characterRoot.groundLayerMask;

        // 创建地面探测器
        if (_collider != null && _config != null)
        {
            _groundProbe = new JumpGroundProbe();
            _groundProbe.Initialize(_collider, _groundLayerMask, _config);
        }

        // gravityScale 已在 Inspector 中设为 0，由自定义重力接管

        InitializeAllStates();
    }

    private void InitializeAllStates()
    {
        var startup = new Jump_Start();
        var rising = new Jump_Rising();
        var falling = new Jump_Fall();
        var landing = new Jump_Land();

        var allStates = new JumpStateBase[] { startup, rising, falling, landing };
        foreach (var s in allStates)
        {
            s.Initialize(this, dataBus, rigidbody2D);
            s.SetGravityDirection(GravityDown);
            states[s.StateKey] = s;
        }
    }

    // ============ ICharacterStateMachine 接口 ============
    public void Enter()
    {
        // 提交高优先级动画意图，覆盖 Movement 的 Track 0
        SubmitJumpAnimationIntent();
    }

    public void Exit()
    {
        if (currentState != null)
        {
            currentState.Exit();
            currentState = null;
            dataBus.currentJumpState = EState.Jump_None;
        }

        // 移除跳跃动画意图，让 Movement 恢复
        if (_animationComposer != null)
            _animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.Jump);

        lastLogicID = EAnimationLogicState.Idle;
    }

    public bool RequestState(object stateData)
    {
        if (IsActive)
            return false;

        // 记录跳跃按键时间（用于 Jump Buffer）
        dataBus.lastJumpPressedTime = Time.time;

        // 检查是否可以起跳
        bool canJumpNow = dataBus.isGrounded || HasCoyoteTime;

        if (canJumpNow && !_jumpConsumed)
        {
            _jumpConsumed = true;
            _hasLeftGround = false;
            TransitionTo(EState.Jump_Start);
            return true;
        }

        // 不能立即起跳，但输入已缓存（等落地后自动起跳）
        return false;
    }

    // ============ Coyote Time / Jump Buffer ============
    public bool HasCoyoteTime
    {
        get
        {
            if (_config == null) return false;
            return Time.time - dataBus.lastGroundedTime <= _config.coyoteTime;
        }
    }

    public bool HasBufferedJump
    {
        get
        {
            if (_config == null) return false;
            return Time.time - dataBus.lastJumpPressedTime <= _config.jumpBufferTime;
        }
    }

    /// <summary>消费跳跃缓存</summary>
    public void ConsumeJump()
    {
        _jumpConsumed = true;
    }

    /// <summary>重置跳跃消费状态（落地时调用）</summary>
    public void ResetJumpConsumption()
    {
        _jumpConsumed = false;
    }

    /// <summary>落地吸附：将角色移动到实际地面命中位置</summary>
    public void SnapToGround()
    {
        if (rigidbody2D == null || _groundProbe == null)
            return;

        float distance = _groundProbe.GroundDistance;

        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f)
            return;

        rigidbody2D.position += GravityDown * distance;
    }

    // ============ 状态转换 ============
    public void TransitionTo(EState stateKey)
    {
        if (!states.TryGetValue(stateKey, out var next))
        {
            Debug.LogWarning($"[JumpStateMachine] 状态未注册: {stateKey}");
            return;
        }

        currentState?.Exit();
        currentState = next;
        dataBus.currentJumpState = stateKey;
        currentState.Enter();

        // 状态切换后重新提交动画意图（语义可能改变）
        SubmitJumpAnimationIntent();
    }

    public void CompleteJump()
    {
        currentState?.Exit();
        currentState = null;
        dataBus.currentJumpState = EState.Jump_None;
        ResetJumpConsumption();

        if (_animationComposer != null)
            _animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.Jump);
        lastLogicID = EAnimationLogicState.Idle;
    }

    /// <summary>因配置缺失等原因取消跳跃，直接回退到非活跃状态</summary>
    public void CancelJump(string reason)
    {
        Debug.LogWarning($"[Jump] CancelJump: {reason}");
        currentState?.Exit();
        currentState = null;
        dataBus.currentJumpState = EState.Jump_None;

        if (_animationComposer != null)
            _animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.Jump);
        lastLogicID = EAnimationLogicState.Idle;
    }

    // ============ 每帧更新 ============
    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        if (currentState == null)
            return;

        // 执行地面探测（跳跃期间也是唯一写入者）
        if (_groundProbe != null)
        {
            _groundProbe.Probe();
            dataBus.isGrounded = _groundProbe.IsGrounded;
            if (dataBus.isGrounded)
                dataBus.lastGroundedTime = Time.time;

            // 记录确实离开过地面（防止起跳瞬间误判）
            if (!dataBus.isGrounded)
                _hasLeftGround = true;
        }

        currentState.FixedUpdate(fixedDeltaTime);
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

    // ============ 动画语义 ============
    private void SubmitJumpAnimationIntent()
    {
        if (_animationComposer == null)
            return;

        // 使用 exclusiveFullBody 抑制 Movement 的 Track 0 动画
        // 由于当前没有真实跳跃动画，使用 Idle 语义作为占位
        // 高优先级 + exclusiveFullBody 会覆盖 Movement 的 BaseFullBody
        _animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.Jump,
            semantic = dataBus.currentJumpState switch
            {
                EState.Jump_Start => EAnimSemantic.Jump_Start,
                EState.Jump_Rising => EAnimSemantic.Jump_Rising,
                EState.Jump_Fall => EAnimSemantic.Jump_Falling,
                EState.Jump_Land => EAnimSemantic.Jump_Landing,
                _ => EAnimSemantic.Jump_Rising
            },
            layer = AnimationLayer.BaseFullBody,
            priority = 25,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        });
    }

    public void OnEnterAnimation()
    {
        // 跳跃期间持续持有动画意图，由 Enter/Exit 管理提交和移除
        // 子状态切换不需要重新提交（同一意图持续活跃）
    }
}
