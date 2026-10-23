using UnityEngine;

/// <summary>
/// 跳跃子状态基类。提供重力方向、配置和速度工具方法。
/// 所有速度操作都基于重力方向投影，不直接写 velocity.y。
/// </summary>
public abstract class JumpStateBase
{
    public abstract EState StateKey { get; }
    public abstract string StateName { get; }

    protected JumpStateMachine stateMachine;
    protected CharacterDataBus dataBus;
    protected Rigidbody2D rigidbody;

    protected Vector2 gravityDown = Vector2.down;
    protected Vector2 gravityUp = Vector2.up;
    protected Vector2 lateralAxis = Vector2.right;

    public virtual void Initialize(JumpStateMachine machine, CharacterDataBus bus, Rigidbody2D rb)
    {
        this.stateMachine = machine;
        this.dataBus = bus;
        this.rigidbody = rb;
    }

    public void SetGravityDirection(Vector2 down)
    {
        gravityDown = down.normalized;
        gravityUp = -gravityDown;
        lateralAxis = new Vector2(-gravityDown.y, gravityDown.x).normalized;
    }

    protected float UpwardSpeed => Vector2.Dot(rigidbody.velocity, gravityUp);
    protected float LateralSpeed => Vector2.Dot(rigidbody.velocity, lateralAxis);

    /// <summary>合成速度，每物理帧只写一次</summary>
    protected Vector2 ComposeVelocity(float upwardSpeed, float lateralSpeed)
    {
        return gravityUp * upwardSpeed + lateralAxis * lateralSpeed;
    }

    /// <summary>
    /// 计算重力作用后的上升速度。不直接写 velocity。
    /// </summary>
    protected float ComputeGravity(float currentUpwardSpeed, float multiplier, float maxFallSpeed, float fixedDeltaTime)
    {
        float gravity = stateMachine.Config.baseGravityAcceleration * multiplier;
        float newUpwardSpeed = currentUpwardSpeed - gravity * fixedDeltaTime;

        // 限制最大下落速度
        float maxDownward = -maxFallSpeed;
        if (newUpwardSpeed < maxDownward)
            newUpwardSpeed = maxDownward;

        return newUpwardSpeed;
    }

    /// <summary>
    /// 计算空中横向控制后的横向速度。不直接写 velocity。
    /// </summary>
    protected float ComputeAirControl(float input, float currentLateral, float fixedDeltaTime)
    {
        var config = stateMachine.Config;

        if (Mathf.Abs(input) > 0.01f)
        {
            return Mathf.MoveTowards(
                currentLateral,
                input * config.maxAirMoveSpeed,
                config.airAcceleration * fixedDeltaTime
            );
        }
        else
        {
            return Mathf.MoveTowards(
                currentLateral,
                0f,
                config.airDeceleration * fixedDeltaTime
            );
        }
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update(float deltaTime) { }
    public virtual void FixedUpdate(float fixedDeltaTime) { }
}
