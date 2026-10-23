using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class HuntFlowStateMachine : IGameFlowState
{
    public string flowStateMachineName => "HuntFlowStateMachine";
    public string currentFlowStateName => currentState?.huntStateName ?? "None";

    public int Priority => 20;

   // public bool IsActive => currentState != null;


    private bool isRunning;
    public bool IsActive => isRunning;

    private Dictionary<Type, BaseHuntFlowState> states;
    private BaseHuntFlowState currentState;

    private GameFlowDataBus gameDataBus;
    private GameFlowRoot gameFlowRoot;

    public void Initialize(GameFlowDataBus bus, GameFlowRoot root)
    {
        gameDataBus = bus;
        gameFlowRoot = root;

        InitializeStates();

    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, BaseHuntFlowState>
        {
            [typeof(HuntPrepareState)] = new HuntPrepareState(gameFlowRoot),
            [typeof(HuntLoadMapState)] = new HuntLoadMapState(gameFlowRoot),
            [typeof(HuntSpawnPlayerState)] = new HuntSpawnPlayerState(gameFlowRoot),
            [typeof(HuntCountdownState)] = new HuntCountdownState(gameFlowRoot),
            [typeof(HuntCombatState)] = new HuntCombatState(gameFlowRoot),
            [typeof(HuntLevelUpState)] = new HuntLevelUpState(gameFlowRoot),
            //[typeof(HuntBossState)] = new HuntBossState(gameFlowRoot),
            [typeof(HuntResultState)] = new HuntResultState(gameFlowRoot),
            //[typeof(HuntCleanupState)] = new HuntCleanupState(gameFlowRoot),
        };

        foreach (var state in states.Values)
        {
            state.Initialize(this, gameDataBus);
        }
    }

    public bool TransitionTo<T>() where T : BaseHuntFlowState
    {
        if (!states.TryGetValue(typeof(T), out var newState))
        {
            Debug.LogWarning($"状态 {typeof(T).Name} 不存在");
            return false;
        }

        if (currentState != null && !currentState.CanTransitionTo(newState))
        {
            //Debug.LogWarning($"无法从 {currentState.huntStateName} 切换到 {newState.huntStateName}");
            return false;
        }

        BaseHuntFlowState oldState = currentState;
        currentState?.Exit();
        currentState = newState;

        gameFlowRoot.EventHub.Hunt.Publish(new HuntFlowStateChangedEvent
        {
            from = oldState != null ? oldState.FlowStateType : GameFlowStateType.None,
            to = currentState.FlowStateType
        });

        currentState.Enter();

        //Debug.Log($"[RunFlow] 切换到：{currentState.huntStateName}");

        return true;
    }

    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);
    }

    public void Enter()
    {
        TryActivate();
    }

    public void Exit()
    {
        currentState?.Exit();
    }

    public bool TryActivate()
    {
        if (isRunning)
        {
            return currentState is HuntCombatState &&
                gameFlowRoot != null &&
                gameFlowRoot.huntFlow != null &&
                (gameFlowRoot.huntFlow.huntFlowDataBus.player != null ||
                 gameFlowRoot.huntFlow.huntPlayerSpawner.HasPreparedNodeParty);
        }

        isRunning = true;
        if (currentState == null)
            TransitionTo<HuntPrepareState>();
        else
            currentState.Enter();

        bool ready = currentState is HuntCombatState &&
            gameFlowRoot != null &&
            gameFlowRoot.huntFlow != null &&
            (gameFlowRoot.huntFlow.huntFlowDataBus.player != null ||
             gameFlowRoot.huntFlow.huntPlayerSpawner.HasPreparedNodeParty);
        if (!ready)
            isRunning = false;
        return ready;
    }

    public void Deactivate()
    {
        bool wasRunning = isRunning;
        isRunning = false;
        if (gameFlowRoot != null && gameFlowRoot.huntFlow != null)
            gameFlowRoot.huntFlow.huntFlowDataBus.isRunActive = false;
        if (wasRunning)
            currentState?.Exit();
    }

    public bool RequestState(object stateData)
    {
        if (stateData is string request)
        {
            switch (request)
            {
                case "StartRun":
                    TransitionTo<HuntPrepareState>();
                    return true;

                case "PauseRun":
                    // 也可以交给独立 PauseFlowStateMachine
                    return true;

                case "EndRun":
                    TransitionTo<HuntResultState>();
                    return true;
            }
        }

        return false;
    }
}
