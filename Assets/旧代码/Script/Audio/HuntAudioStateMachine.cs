using System;
using System.Collections.Generic;
using UnityEngine;


public class HuntAudioStateMachine : IAudioStateMachine
{
    public string AudioStateMachineName => "HuntAudioStateMachine";
    public string CurrentStateName => currentState?.StateName ?? "None";

    public int Priority => 20;
    public bool IsActive => isRunning;

    private bool isRunning;

    private Dictionary<Type, BaseHuntAudioState> states;
    private BaseHuntAudioState currentState;

    private GameFlowRoot gameFlowRoot;
    private GameAudioRoot audioRoot;
    private GameEventHub eventHub;

    public void Initialize(GameFlowRoot root, GameAudioRoot audioRoot, GameEventHub eventHub)
    {
        this.gameFlowRoot = root;
        this.audioRoot = audioRoot;
        this.eventHub = eventHub;

        InitializeStates();
    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, BaseHuntAudioState>
        {

            [typeof(HuntAudioCombatState)] = new HuntAudioCombatState(),
        };

        foreach (BaseHuntAudioState state in states.Values)
        {
            state.Initialize(this, gameFlowRoot, audioRoot, eventHub);
        }
    }

    public bool RequestState(object stateData)
    {
        if (stateData is AudioStateType type)
        {
            switch (type)
            {
                case AudioStateType.HuntCombat:
                    return TransitionTo<HuntAudioCombatState>();
            }
        }

        return false;
    }

    public bool TransitionTo<T>() where T : BaseHuntAudioState
    {
        if (!states.TryGetValue(typeof(T), out BaseHuntAudioState newState))
            return false;

        if (currentState != null && !currentState.CanTransitionTo(newState))
            return false;

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();

        return true;
    }

    public void Enter()
    {
        isRunning = true;

        if (currentState == null)
            TransitionTo<HuntAudioCombatState>();
        else
            currentState.Enter();
    }

    public void Exit()
    {
        currentState?.Exit();
        isRunning = false;
    }

    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);
    }
}