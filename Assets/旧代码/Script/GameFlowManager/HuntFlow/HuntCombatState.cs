using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntCombatState : BaseHuntFlowState
{
     private readonly GameFlowRoot root;

    public HuntCombatState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntCombatState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntCombat;
    public override void Enter()
    {

        //Debug.Log("[RunCombat] 正式开始生存战斗");

        var hunt = root.huntFlow;

        hunt.huntFlowDataBus.isRunActive = true;

        //run.enemySpawnDirector.StartSpawn();
        //run.waveDirector.StartWave();
        //run.abilityDirector.StartAbilities();

        //EventCenter.Instance.EventTrigger(GameEvent.RunStarted);

    }

    public override void Update(float deltaTime)
    {
        var hunt = root.huntFlow;
        var data = hunt.huntFlowDataBus;

        if (!data.isRunActive || data.isPaused)
            return;

        data.elapsedTime += deltaTime;

        //run.waveDirector.Tick(deltaTime);
        //run.enemySpawnDirector.Tick(deltaTime);
        //run.abilityDirector.Tick(deltaTime);
        //run.expDirector.Tick(deltaTime);
        //run.lootDirector.Tick(deltaTime);
        //run.runUIWindow.RefreshRunTimer(data.elapsedTime);

        //if (data.player == null || !data.player.characterDataBus.isAlive)
        //{
        //    stateMachine.TransitionTo<RunResultState>();
        //    return;
        //}

        //if (data.elapsedTime >= data.runDuration)
        //{
        //    stateMachine.TransitionTo<RunResultState>();
        //    return;
        //}

        //if (run.expDirector.HasLevelUpRequest)
        //{
        //    stateMachine.TransitionTo<RunLevelUpState>();
        //    return;
        //}
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
