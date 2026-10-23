using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有状态机必须实现的接口
/// </summary>
public interface ICharacterStateMachine
{
    // ============ 基本属性 ============

    /// <summary>状态机名称（用于调试）</summary>
    string StateMachineName { get; }

    /// <summary>当前是否活跃（想执行）</summary>
    bool IsActive { get; }

    /// <summary>状态优先级（数值越大优先级越高）</summary>
    int Priority { get; }

    /// <summary>当前状态名称（用于调试）</summary>
    string CurrentStateName { get; }

    //bool IsUrgentRequestActive {  get; }

    // ============ 生命周期方法 ============

    /// <summary>状态机初始化</summary>
    void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf,CharacterRoot root);

    /// <summary>进入状态机（被仲裁器选中时调用）</summary>
    void Enter();

    /// <summary>退出状态机（被仲裁器取消时调用）</summary>
    void Exit();

    /// <summary>每帧更新（只有活跃状态机会被调用）</summary>
    void Update(float deltaTime);

    /// <summary>固定更新（物理相关）</summary>
    void FixedUpdate(float fixedDeltaTime);


    //// 新增：当本状态机激活时，应发布动画请求
    //void OnEnterAnimation();
    // ============ 状态查询 ============

    /// <summary>检查是否能被指定状态中断</summary>
    bool CanBeInterruptedBy(ICharacterStateMachine other);

    /// <summary>检查是否能中断指定状态</summary>
    bool CanInterrupt(ICharacterStateMachine other);

    /// <summary>接收状态请求（外部调用）</summary>
    bool RequestState(object stateData);//需要写入回应状态的条件
}
