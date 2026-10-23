using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntCountdownState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;
    private float timer;

    public HuntCountdownState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => " HuntCountdownState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntCountdown;
    public override void Enter()
    {
        // Debug.Log("[RunSpawnPlayer] 生成玩家");

        var hunt = root.huntFlow;

        timer = 3f;
        Debug.Log("[RunCountdown] 倒计时开始");

        //root.runFlow.runUIWindow.ShowCountdown(3);

    }

    public override void Update(float deltaTime)
    {
        timer -= deltaTime;

        if (timer <= 0f)
        {
            huntFlowStateMachine.TransitionTo<HuntCombatState>();
        }
    }


    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {
        return newState is HuntCombatState;
    }





}
