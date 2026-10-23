using UnityEngine;

/// <summary>
/// [已停用] 原 Jump_PreStart，有效起跳逻辑已迁移到 Jump_Start。
/// 保留文件避免 .meta 引用丢失。
/// 不注册、不转换、不参与运行时状态字典。
/// </summary>
public class Jump_PreStart : JumpStateBase
{
    public override EState StateKey => EState.Jump_PreStart;
    public override string StateName => "Jump_PreStart (Deprecated)";

    public override void Enter() { }
    public override void Exit() { }
    public override void Update(float deltaTime) { }
    public override void FixedUpdate(float fixedDeltaTime) { }
}
