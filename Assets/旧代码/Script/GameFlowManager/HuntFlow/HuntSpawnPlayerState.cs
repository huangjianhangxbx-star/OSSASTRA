using Com.LuisPedroFonseca.ProCamera2D;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntSpawnPlayerState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;

    public HuntSpawnPlayerState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntSpawnPlayerState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntSpawnPlayer;

    public override void Enter()
    {
        if (root.huntFlow.huntPlayerSpawner.TryPrepareNodeParty())
        {
            huntFlowStateMachine.TransitionTo<HuntCombatState>();
            return;
        }
        CharacterRoot player = root.huntFlow.huntPlayerSpawner.SpawnPlayer();




        if (player == null)
        {
            Debug.LogError("[HuntSpawnPlayerState] 玩家生成失败");
            return;
        }

        huntFlowStateMachine.TransitionTo<HuntCombatState>();

    }

    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {
        return newState is HuntCombatState;
    }
}
