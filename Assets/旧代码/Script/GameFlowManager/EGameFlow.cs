using System.Collections;
using System.Collections.Generic;
using UnityEngine;




public enum GameFlowStateType // 有多少个状态机脚本写多少个，数量需要和实际的状态机脚本数量一样
{

    None,
    Exploration,

    #region 战斗

    BattleStart,//基础战斗模式
    TurnBegin,
    SkillSelect,
    TargetSelect,
    ActionPrepare,
    ActionExecution,
    ActionEnd,
    TurnEnd,
    BattleResult,




    HuntPrepare,
    HuntLoadMap,
    HuntSpawnPlayer,
    HuntCountdown,
    HuntLevelUp,
    HuntResult,
    HuntCombat,






    #endregion



}



public enum TargetRange 
{ 
    Single,
    Pattern, 
    FullField 
}
public enum PreferenceSlotPosition
{ 
    Front, 
    Back 
} // 0-1位为前，2-3位为后
public enum AudioStateType
{
    None,

    MenuMain,
    MenuSetting,

    HuntPrepare,
    HuntLoadMap,
    HuntCountdown,
    HuntCombat,
    HuntLevelUp,
    HuntResult,

    Pause,
    Dialogue,
    Cutscene
}