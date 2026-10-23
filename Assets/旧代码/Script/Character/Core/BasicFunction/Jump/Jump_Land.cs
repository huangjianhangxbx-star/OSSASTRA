using UnityEngine;

/// <summary>
/// 跳跃落地阶段。
/// - 清除朝下的穿透速度
/// - 轻落地短暂表现后完成跳跃
/// - 若有有效 Jump Buffer 可直接重新起跳
/// </summary>
public class Jump_Land : JumpStateBase
{
    private float _landTimer;

    public override EState StateKey => EState.Jump_Land;
    public override string StateName => "Jump_Landing";

    public override void Enter()
    {
        var config = stateMachine.Config;

        // 清除朝下的穿透速度，保留横向
        if (rigidbody != null)
        {
            Vector2 v = rigidbody.velocity;
            float lateral = Vector2.Dot(v, lateralAxis);
            rigidbody.velocity = lateralAxis * lateral;
        }

        _landTimer = config != null ? config.landingDuration : 0.03f;

        // 重置跳跃消费（落地后可以再次跳跃）
        stateMachine.ResetJumpConsumption();

        // 如果有有效 Jump Buffer，直接重新起跳
        if (stateMachine.HasBufferedJump && dataBus.canJump)
        {
            if (config != null && config.enableDebugLogs)
                Debug.Log("[Jump] Landing -> Startup reason=BufferedLanding");

            stateMachine.ConsumeJump();
            stateMachine.TransitionTo(EState.Jump_Start);
            return;
        }

        if (config != null && config.enableDebugLogs)
            Debug.Log("[Jump] Landing -> Complete");
    }

    public override void Exit() { }

    public override void Update(float deltaTime)
    {
        _landTimer -= deltaTime;
        if (_landTimer <= 0f)
        {
            stateMachine.CompleteJump();
        }
    }

    public override void FixedUpdate(float fixedDeltaTime) { }
}
