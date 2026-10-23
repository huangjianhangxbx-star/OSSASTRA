using UnityEngine;

/// <summary>
/// [已停用] 原 Jump_Peak，不再作为独立物理状态。
/// 顶点判断改为 Rising 中的 apexGravityMultiplier 逻辑。
/// 保留文件避免 .meta 引用丢失。
/// </summary>
public class Jump_Peak : JumpStateBase
{
    public override EState StateKey => EState.Jump_Peak;
    public override string StateName => "Jump_Peak (Deprecated)";

    public override void Enter() { }
    public override void Exit() { }
    public override void Update(float deltaTime) { }
    public override void FixedUpdate(float fixedDeltaTime) { }
}
