using UnityEngine;

/// <summary>
/// 跳跃下落阶段。
/// 每物理帧：先检测地面 → 吸附到命中位置 → 清 Y 速度 → 进入 Land。
/// 否则才计算重力、空中控制并写速度。
/// </summary>
public class Jump_Fall : JumpStateBase
{
    public override EState StateKey => EState.Jump_Fall;
    public override string StateName => "Jump_Falling";

    public override void Enter() { }
    public override void Exit() { }

    public override void Update(float deltaTime)
    {
        if (dataBus.jumpPressedThisFrame)
        {
            dataBus.lastJumpPressedTime = Time.time;
        }
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        var config = stateMachine.Config;
        if (config == null || rigidbody == null)
            return;

        // 1. 先检测落地条件
        var probe = stateMachine.GroundProbe;

        bool canLand =
            probe != null &&
            probe.IsGrounded &&
            stateMachine.HasLeftGround &&
            UpwardSpeed <= 0.1f;

        if (canLand)
        {
            float landingSpeed = Mathf.Max(0f, -UpwardSpeed);

            if (config.enableDebugLogs)
            {
                Debug.Log(
                    $"[Jump] Falling -> Landing " +
                    $"landingSpeed={landingSpeed:F1}, " +
                    $"groundDistance={probe.GroundDistance:F3}"
                );
            }

            // 吸附到实际地面命中位置
            stateMachine.SnapToGround();

            // 清除 Y 速度
            Vector2 velocity = rigidbody.velocity;
            velocity.y = 0f;
            rigidbody.velocity = velocity;

            stateMachine.TransitionTo(EState.Jump_Land);
            return;
        }

        // 2. 读取当前分量
        float upSpeed = UpwardSpeed;
        float lateralSpeed = LateralSpeed;

        // 3. 计算重力
        upSpeed = ComputeGravity(upSpeed, config.fallGravityMultiplier, config.maxFallSpeed, fixedDeltaTime);

        // 4. 计算空中横向控制
        lateralSpeed = ComputeAirControl(dataBus.moveInput.x, lateralSpeed, fixedDeltaTime);

        // 5. 一次写入最终 velocity
        rigidbody.velocity = ComposeVelocity(upSpeed, lateralSpeed);
    }
}
