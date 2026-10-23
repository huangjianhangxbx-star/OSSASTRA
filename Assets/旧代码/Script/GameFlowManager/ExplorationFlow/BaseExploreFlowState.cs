using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseExploreFlowState
{

    public abstract string exploreStateName { get; }

    /// <summary>当前是否活跃（想执行）</summary>
    //bool IsActive { get; }

    ///// <summary>当前状态名称（用于调试）</summary>
    //string CurrentFlowStateName { get; }

    protected ExploreFlowStateMachine exploreFlowStateMachine;
    protected GameFlowDataBus dataBus;

    public virtual void Initialize(ExploreFlowStateMachine stateMachine, GameFlowDataBus bus)
    {
        stateMachine = exploreFlowStateMachine;
        bus = dataBus;
    }

    public virtual void Enter() { }


    /// <summary>退出状态机（被仲裁器取消时调用）</summary>
    public virtual void Exit() { }

    /// <summary>每帧更新（只有活跃状态机会被调用）</summary>
    public virtual void Update(float deltaTime) { }

    /// <summary>接收状态请求（外部调用）</summary>
    public abstract bool CanTransitionTo(BaseExploreFlowState newState);

}
