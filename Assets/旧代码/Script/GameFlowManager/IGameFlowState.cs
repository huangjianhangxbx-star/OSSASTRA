using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGameFlowState
{
    string flowStateMachineName { get; }

    /// <summary>当前是否活跃（想执行）</summary>
    bool IsActive { get; }

    /// <summary>状态优先级（数值越大优先级越高）</summary>
    int Priority { get; }
    /// <summary>当前状态名称（用于调试）</summary>
    string currentFlowStateName { get; }

    void Initialize(GameFlowDataBus gameFlowDataBus,GameFlowRoot root );
    void Enter();

    /// <summary>退出状态机（被仲裁器取消时调用）</summary>
    void Exit();

    /// <summary>每帧更新（只有活跃状态机会被调用）</summary>
    void Update(float deltaTime);

    /// <summary>接收状态请求（外部调用）</summary>
    bool RequestState(object stateData);//需要写入回应状态的条件

}
