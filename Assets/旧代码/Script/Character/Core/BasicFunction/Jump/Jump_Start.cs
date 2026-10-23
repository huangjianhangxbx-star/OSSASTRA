using UnityEngine;

/// <summary>
/// 跳跃起始阶段：验证跳跃资格，设置起跳速度，保留横向速度，立即进入 Rising。
/// </summary>
public class Jump_Start : JumpStateBase
{
    public override EState StateKey => EState.Jump_Start;
    public override string StateName => "Jump_Start";

    public override void Enter()
    {
        var config = stateMachine.Config;
        if (config == null || rigidbody == null)
        {
            Debug.LogWarning("[Jump] Jump_Start: config 或 rigidbody 缺失，取消跳跃");
            stateMachine.CancelJump("MissingConfigOrRigidbody");
            return;
        }

        if (config.enableDebugLogs)
            Debug.Log("[Jump] Startup -> Rising reason=Grounded/Coyote/BufferedLanding");

        // 保留横向速度，设置沿 GravityUp 的起跳速度
        Vector2 v = rigidbody.velocity;
        float lateralSpeed = Vector2.Dot(v, lateralAxis);

        rigidbody.velocity = gravityUp * config.jumpSpeed + lateralAxis * lateralSpeed;

        // 立即进入 Rising
        stateMachine.TransitionTo(EState.Jump_Rising);
    }

    public override void Exit() { }
    public override void Update(float deltaTime) { }
    public override void FixedUpdate(float fixedDeltaTime) { }
}
