using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseHuntFlowState
{
    protected HuntFlowStateMachine huntFlowStateMachine;
    protected GameFlowDataBus gameFlowDataBus;

    public abstract string huntStateName { get; }
    public abstract GameFlowStateType FlowStateType { get; }
    public virtual void Initialize(HuntFlowStateMachine machine, GameFlowDataBus bus)
    {
        huntFlowStateMachine = machine;
        gameFlowDataBus = bus;
    }

    public abstract void Enter();

    public virtual void Update(float deltaTime) { }

    public virtual void Exit() { }

    public abstract bool CanTransitionTo(BaseHuntFlowState newState);

}
