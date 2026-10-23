using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class ExploreFlowStateMachine : IGameFlowState
{
    public string flowStateMachineName => "ExploreFlowState";

    public int Priority => 10; // 较低优先级，可被特殊动作中断

    public string currentFlowStateName => currentState?.exploreStateName ?? "None";


    private Dictionary<Type, BaseExploreFlowState> states;
    private BaseExploreFlowState currentState;

    private GameFlowDataBus dataBus;
    private GameFlowRoot root;


    public bool IsActive
    {
        get
        {
            // 移动状态机总是活跃的，除非角色死亡或不能移动
            return currentState != null;
        }
    }





    public void Update(float deltaTime)
    {




    }


    public void Initialize(GameFlowDataBus bus, GameFlowRoot root)
    {
        this.dataBus = bus;
        if (dataBus == null)
        {
            Debug.LogError("MovementStateMachine: 缺少CharacterDataBus");
            return;
        }
        this.root = root;
        // 初始化状态机的所有状态
        InitializeStates();

        //设置初始状态
        //TransitionTo<BasicBattleState>();

        //Debug.Log($"{StateMachineName} 初始化完成");
    }










    private void InitializeStates()
    {
        states = new Dictionary<Type, BaseExploreFlowState>
        {
            [typeof(BasicExploreState)] = new BasicExploreState(),
            //[typeof(WalkState)] = new WalkState(),
            //[typeof(RunState)] = new RunState(),
            //[typeof(CrouchState)] = new CrouchState()
            // 可以在这里添加更多移动状态：CrouchState, SlideState等
        };

        // 初始化每个状态
        foreach (var state in states.Values)
        {
            state.Initialize(this,dataBus);
        }
    }

    public bool TransitionTo<T>() where T : BaseExploreFlowState
    {
        if (!states.TryGetValue(typeof(T), out var newState))
        {
            Debug.LogWarning($"状态 {typeof(T).Name} 不存在");
            return false;
        }

        // 检查是否可以转换
        if (currentState != null && !currentState.CanTransitionTo(newState))
        {
            Debug.LogWarning($"无法从 {currentState.exploreStateName} 转换到 {newState.exploreStateName}");
            return false;
        }

        // 执行转换
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();

        //Debug.Log($"移动状态转换: {currentState.StateName}");
        return true;
    }




    public bool RequestState(object stateData)
    {
        // 移动状态机通常不接收外部请求
        // 移动状态转换由输入系统控制

        // 但如果需要，可以处理特定请求
        // 例如：强制站立、强制奔跑等
        if (stateData is string requestedState)
        {
            switch (requestedState)
            {
                case "ForceExplore":
                    TransitionTo<BasicExploreState>();
                    return true;

                    //case "ForceWalk":
                    //    TransitionTo<WalkState>();
                    //    return true;

                    //case "ForceRun":
                    //    TransitionTo<RunState>();
                    //    return true;
                    //case "ForceCrouch":
                    //    TransitionTo<CrouchState>();
                    //    return true;
            }
        }
        return false;
    }


    // ============ ICharacterStateMachine 接口方法 ============
    public void Enter()
    {
        Debug.Log($"{flowStateMachineName} 被激活");
        // 移动状态机被激活时，确保当前状态正确
        if (currentState == null)
        {
            TransitionTo<BasicExploreState>();
        }
        else
        {
            currentState.Enter();
        }
    }

    public void Exit()
    {
        //Debug.Log($"{StateMachineName} 被暂停");
        // 移动状态机被暂停时，可以做一些清理工作
        // 例如：停止移动特效
    }
}
