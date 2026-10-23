using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntPrepareState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;

    public HuntPrepareState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntPrepareState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntPrepare;
    public override void Enter()
    {
        Debug.Log("[RunPrepare] 准备一局");

        var hunt = root.huntFlow;

        hunt.huntFlowDataBus.Reset();

        // 读取选择的角色、地图、难度等
        // run.dataBus.runConfig = root.gameFlowDataBus.selectedRunConfig;

        huntFlowStateMachine.TransitionTo<HuntLoadMapState>();
    }

    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {
        return newState is HuntLoadMapState;
    }
}
