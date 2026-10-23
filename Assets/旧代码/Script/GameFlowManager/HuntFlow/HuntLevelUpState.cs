using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntLevelUpState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;

    public HuntLevelUpState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntLevelUpState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntLevelUp;
    public override void Enter()
    {

        Debug.Log("[RunLevelUp] 进入升级选择");

        var hunt = root.huntFlow;


        //hunt.dataBus.isPaused = true;
        //Time.timeScale = 0f;

        //var choices = run.expDirector.GenerateLevelUpChoices();

        //run.runUIWindow.ShowLevelUpChoices(choices, OnChoiceSelected);
    }

    //private void OnChoiceSelected(LevelUpChoice choice)
    //{
    //    var run = root.runFlow;

    //    run.abilityDirector.ApplyUpgrade(choice);

    //    run.dataBus.isPaused = false;
    //    Time.timeScale = 1f;

    //    stateMachine.TransitionTo<RunCombatState>();
    //}

    public override void Exit()
    {
        //root.huntFlow.runUIWindow.HideLevelUpChoices();
    }

    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {

        return newState is HuntCombatState;


    }
}
