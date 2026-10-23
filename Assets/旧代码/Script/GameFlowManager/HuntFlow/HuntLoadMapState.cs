using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntLoadMapState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;

    public HuntLoadMapState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntLoadMapState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntLoadMap;

    public override void Enter()
    {
        //Debug.Log("[RunLoadMap] º”‘ÿµÿÕº");

        var hunt = root.huntFlow;


        huntFlowStateMachine.TransitionTo<HuntSpawnPlayerState>();
        //hunt.mapLoader.LoadMap(() =>
        //{
        //    
        //});


    }

    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {
        return newState is HuntSpawnPlayerState;
    }
}
