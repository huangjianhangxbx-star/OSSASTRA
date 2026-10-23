using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntResultState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;

    public HuntResultState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntResultState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntResult;
    public override void Enter()
    {

 
        var hunt = root.huntFlow;

        Debug.Log("[RunResult] 一局结束");

 

        //run.dataBus.isRunActive = false;
        //run.dataBus.isPaused = true;

        //run.enemySpawnDirector.StopSpawn();
        //run.abilityDirector.StopAbilities();

        //Time.timeScale = 0f;

        //run.runUIWindow.ShowResult(run.dataBus);

    }

   

    public override void Exit()
    {
        Debug.Log("[RunCombat] 离开战斗循环");
    }
    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {

        return newState is HuntLevelUpState; //* || newState is HuntBossState || newState is HuntResultState;*/


    }
}
