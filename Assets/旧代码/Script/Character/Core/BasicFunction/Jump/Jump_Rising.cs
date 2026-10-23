using UnityEngine;

/// <summary>
/// 跳跃上升阶段。
/// 每物理帧：读取分量 → 计算重力 → 计算空中控制 → 一次写 velocity → 检测切换。
/// </summary>
public class Jump_Rising : JumpStateBase
{
    private bool _jumpCutApplied;

    public override EState StateKey => EState.Jump_Rising;
    public override string StateName => "Jump_Rising";

    public override void Enter()
    {
        _jumpCutApplied = false;
    }

    public override void Exit() { }

    public override void Update(float deltaTime)
    {
        if (_jumpCutApplied)
            return;

        if (dataBus.jumpReleasedThisFrame || !dataBus.jumpHeld)
        {
            _jumpCutApplied = true;

            if (stateMachine.Config.enableDebugLogs)
                Debug.Log("[Jump] JumpCut applied");
        }
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        var config = stateMachine.Config;
        if (config == null || rigidbody == null)
            return;

        // 1. 读取当前分量
        float upSpeed = UpwardSpeed;
        float lateralSpeed = LateralSpeed;

        // 2. Jump Cut：削减上升速度（仅一次，在物理帧中生效）
        if (_jumpCutApplied && upSpeed > 0f)
        {
            upSpeed *= config.jumpCutMultiplier;
            _jumpCutApplied = false; // 已应用

            if (config.enableDebugLogs)
                Debug.Log("[Jump] Rising -> Falling reason=JumpCut");
        }

        // 3. 检查天花板
        var probe = stateMachine.GroundProbe;
        if (probe != null && probe.IsHittingCeiling)
        {
            upSpeed = 0f;

            if (config.enableDebugLogs)
                Debug.Log("[Jump] Rising -> Falling reason=Ceiling");

            // 先写速度再切换
            rigidbody.velocity = ComposeVelocity(upSpeed, lateralSpeed);
            stateMachine.TransitionTo(EState.Jump_Fall);
            return;
        }

        // 4. 上升速度 <= 0 → 进入 Falling
        if (upSpeed <= 0f)
        {
            if (config.enableDebugLogs)
                Debug.Log("[Jump] Rising -> Falling reason=Apex");

            stateMachine.TransitionTo(EState.Jump_Fall);
            return;
        }

        // 5. 计算重力（选择倍率）
        float gravityMultiplier = upSpeed <= config.apexSpeedThreshold
            ? config.apexGravityMultiplier
            : config.riseGravityMultiplier;

        upSpeed = ComputeGravity(upSpeed, gravityMultiplier, config.maxFallSpeed, fixedDeltaTime);

        // 6. 计算空中横向控制
        lateralSpeed = ComputeAirControl(dataBus.moveInput.x, lateralSpeed, fixedDeltaTime);

        // 7. 一次写入最终 velocity
        rigidbody.velocity = ComposeVelocity(upSpeed, lateralSpeed);
    }
}
