# ProjectScript Code Documentation - Part 10

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/GameEvent/UIEvents.cs` | 14 | - | struct UIButtonClickedEvent, struct UIPanelOpenedEvent, struct UIPanelClosedEvent | 0 |
| `Assets/Script/GameFlowManager/EGameFlow.cs` | 78 | - | enum GameFlowStateType, enum TargetRange, enum PreferenceSlotPosition, enum AudioStateType | 0 |
| `Assets/Script/GameFlowManager/ExplorationFlow/BaseExploreFlowState.cs` | 38 | - | class BaseExploreFlowState | 0 |
| `Assets/Script/GameFlowManager/ExplorationFlow/BasicExploreState.cs` | 35 | - | class BasicExploreState | 0 |
| `Assets/Script/GameFlowManager/ExplorationFlow/ExploreFlowStateMachine.cs` | 169 | - | class ExploreFlowStateMachine | 0 |
| `Assets/Script/GameFlowManager/FlowRequest.cs` | 24 | - | class FlowRequest | 0 |
| `Assets/Script/GameFlowManager/GameFlowArbitrator.cs` | 249 | - | class GameFlowArbitrator | 0 |
| `Assets/Script/GameFlowManager/GameFlowDataBus.cs` | 15 | - | class GameFlowDataBus | 0 |
| `Assets/Script/GameFlowManager/GameFlowRoot.cs` | 145 | - | class GameFlowRoot | 0 |
| `Assets/Script/GameFlowManager/GameFlowStateRequest.cs` | 25 | - | class GameFlowStateRequest | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/BaseHuntFlowState.cs` | 27 | - | class BaseHuntFlowState | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntCombatState.cs` | 82 | - | class HuntCombatState | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntCountdownState.cs` | 51 | - | class HuntCountdownState | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntFlowDataBus.cs` | 43 | - | class HuntFlowDataBus | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntFlowRoot.cs` | 56 | - | class HuntFlowRoot | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntFlowStateMachine.cs` | 130 | - | class HuntFlowStateMachine | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntLevelUpState.cs` | 57 | - | class HuntLevelUpState | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntLoadMapState.cs` | 38 | - | class HuntLoadMapState | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntPlayerSpawner.cs` | 78 | - | class HuntPlayerSpawner | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntPrepareState.cs` | 35 | - | class HuntPrepareState | 0 |

## Public API

### `Assets/Script/GameEvent/UIEvents.cs`
- `public struct UIButtonClickedEvent
{`
- `public string buttonId;`
- `public struct UIPanelOpenedEvent
{`
- `public string panelId;`
- `public struct UIPanelClosedEvent
{`
- `public string panelId;`
### `Assets/Script/GameFlowManager/EGameFlow.cs`
- `public enum GameFlowStateType // 有多少个状态机脚本写多少个，数量需要和实际的状态机脚本数量一样
{`
- `public enum TargetRange 
{`
- `public enum PreferenceSlotPosition
{`
- `public enum AudioStateType
{`
### `Assets/Script/GameFlowManager/ExplorationFlow/BaseExploreFlowState.cs`
- `public abstract class BaseExploreFlowState
{`
- `public abstract string exploreStateName {`
- `protected ExploreFlowStateMachine exploreFlowStateMachine;`
- `protected GameFlowDataBus dataBus;`
- `public virtual void Initialize(ExploreFlowStateMachine stateMachine, GameFlowDataBus bus)
    {`
- `public virtual void Enter() {`
- `public virtual void Exit() {`
- `public virtual void Update(float deltaTime) {`
- `public abstract bool CanTransitionTo(BaseExploreFlowState newState);`
### `Assets/Script/GameFlowManager/ExplorationFlow/BasicExploreState.cs`
- `public class BasicExploreState : BaseExploreFlowState
{`
- `public override string exploreStateName => "BattleFlowState";`
- `public override void Update(float deltaTime)
    {`
- `public override bool CanTransitionTo(BaseExploreFlowState newState)
    {`
### `Assets/Script/GameFlowManager/ExplorationFlow/ExploreFlowStateMachine.cs`
- `public class ExploreFlowStateMachine : IGameFlowState
{`
- `public string flowStateMachineName => "ExploreFlowState";`
- `public int Priority => 10;`
- `public string currentFlowStateName => currentState?.exploreStateName ?? "None";`
- `public bool IsActive
    {`
- `public void Update(float deltaTime)
    {`
- `public void Initialize(GameFlowDataBus bus, GameFlowRoot root)
    {`
- `public bool TransitionTo<T>() where T : BaseExploreFlowState
    {`
- `public bool RequestState(object stateData)
    {`
- `public void Enter()
    {`
- `public void Exit()
    {`
### `Assets/Script/GameFlowManager/FlowRequest.cs`
- `public abstract class FlowRequest
{`
- `public FlowRequest()
    {`
- `public GameFlowStateType flowType {`
- `public float RequestTime {`
- `public object Data {`
### `Assets/Script/GameFlowManager/GameFlowArbitrator.cs`
- `public class GameFlowArbitrator : MonoBehaviour, IGameSubsystem
{`
- `public HuntFlowStateMachine huntFlowSM;`
- `public void Initialize(GameFlowRoot root)
    {`
- `public event Action<IGameFlowState, IGameFlowState> OnStateChanged;`
### `Assets/Script/GameFlowManager/GameFlowDataBus.cs`
- `public class GameFlowDataBus
{`
### `Assets/Script/GameFlowManager/GameFlowRoot.cs`
- `public class GameFlowRoot : MonoBehaviour
{`
- `public static GameFlowRoot Instance {`
- `public GameEventHub EventHub {`
- `public void Initialize()
    {`
### `Assets/Script/GameFlowManager/GameFlowStateRequest.cs`
- `public class GameFlowStateRequest : FlowRequest
{`
- `public GameFlowStateRequest(GameFlowStateType type,object data = null) : base()
    {`
### `Assets/Script/GameFlowManager/HuntFlow/BaseHuntFlowState.cs`
- `public abstract class BaseHuntFlowState
{`
- `protected HuntFlowStateMachine huntFlowStateMachine;`
- `protected GameFlowDataBus gameFlowDataBus;`
- `public abstract string huntStateName {`
- `public abstract GameFlowStateType FlowStateType {`
- `public virtual void Initialize(HuntFlowStateMachine machine, GameFlowDataBus bus)
    {`
- `public abstract void Enter();`
- `public virtual void Update(float deltaTime) {`
- `public virtual void Exit() {`
- `public abstract bool CanTransitionTo(BaseHuntFlowState newState);`
### `Assets/Script/GameFlowManager/HuntFlow/HuntCombatState.cs`
- `public class HuntCombatState : BaseHuntFlowState
{`
- `public HuntCombatState(GameFlowRoot root)
    {`
- `public override string huntStateName => "HuntCombatState";`
- `public override GameFlowStateType FlowStateType => GameFlowStateType.HuntCombat;`
- `public override void Enter()
    {`
- `public override void Update(float deltaTime)
    {`
- `public override void Exit()
    {`
- `public override bool CanTransitionTo(BaseHuntFlowState newState)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntCountdownState.cs`
- `public class HuntCountdownState : BaseHuntFlowState
{`
- `public HuntCountdownState(GameFlowRoot root)
    {`
- `public override string huntStateName => " HuntCountdownState";`
- `public override GameFlowStateType FlowStateType => GameFlowStateType.HuntCountdown;`
- `public override void Enter()
    {`
- `public override void Update(float deltaTime)
    {`
- `public override bool CanTransitionTo(BaseHuntFlowState newState)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntFlowDataBus.cs`
- `public class HuntFlowDataBus
{`
- `public CharacterRoot player;`
- `public bool isRunActive;`
- `public bool isPaused;`
- `public bool isGameOver;`
- `public float elapsedTime;`
- `public float runDuration;`
- `public int killCount;`
- `public int currentLevel;`
- `public int currentExp;`
- `public int expToNextLevel;`
- `public List<CharacterRoot> aliveEnemies = new();`
- `public List<CharacterRoot> aliveAllies = new();`
- `public void Reset()
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntFlowRoot.cs`
- `public class HuntFlowRoot : MonoBehaviour
{`
- `public void Initialize(GameFlowRoot root)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntFlowStateMachine.cs`
- `public class HuntFlowStateMachine : IGameFlowState
{`
- `public string flowStateMachineName => "HuntFlowStateMachine";`
- `public string currentFlowStateName => currentState?.huntStateName ?? "None";`
- `public int Priority => 20;`
- `public bool IsActive => isRunning;`
- `public void Initialize(GameFlowDataBus bus, GameFlowRoot root)
    {`
- `public bool TransitionTo<T>() where T : BaseHuntFlowState
    {`
- `public void Update(float deltaTime)
    {`
- `public void Enter()
    {`
- `public void Exit()
    {`
- `public bool RequestState(object stateData)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntLevelUpState.cs`
- `public class HuntLevelUpState : BaseHuntFlowState
{`
- `public HuntLevelUpState(GameFlowRoot root)
    {`
- `public override string huntStateName => "HuntLevelUpState";`
- `public override GameFlowStateType FlowStateType => GameFlowStateType.HuntLevelUp;`
- `public override void Enter()
    {`
- `public override void Exit()
    {`
- `public override bool CanTransitionTo(BaseHuntFlowState newState)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntLoadMapState.cs`
- `public class HuntLoadMapState : BaseHuntFlowState
{`
- `public HuntLoadMapState(GameFlowRoot root)
    {`
- `public override string huntStateName => "HuntLoadMapState";`
- `public override GameFlowStateType FlowStateType => GameFlowStateType.HuntLoadMap;`
- `public override void Enter()
    {`
- `public override bool CanTransitionTo(BaseHuntFlowState newState)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntPlayerSpawner.cs`
- `public class HuntPlayerSpawner : MonoBehaviour, IHuntSubsystem
{`
- `public GameObject actorPrefab;`
- `public CharacterSO playerSO;`
- `public Transform spawnPoint;`
- `public void Initialize(HuntFlowRoot root)
    {`
- `public CharacterRoot SpawnPlayer()
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntPrepareState.cs`
- `public class HuntPrepareState : BaseHuntFlowState
{`
- `public HuntPrepareState(GameFlowRoot root)
    {`
- `public override string huntStateName => "HuntPrepareState";`
- `public override GameFlowStateType FlowStateType => GameFlowStateType.HuntPrepare;`
- `public override void Enter()
    {`
- `public override bool CanTransitionTo(BaseHuntFlowState newState)
    {`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/GameEvent/UIEvents.cs`

```cs
public struct UIButtonClickedEvent
{
    public string buttonId;
}

public struct UIPanelOpenedEvent
{
    public string panelId;
}

public struct UIPanelClosedEvent
{
    public string panelId;
}
```

### `Assets/Script/GameFlowManager/EGameFlow.cs`

```cs
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
```

### `Assets/Script/GameFlowManager/ExplorationFlow/BaseExploreFlowState.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseExploreFlowState
{

    public abstract string exploreStateName { get; }

    /// <summary>当前是否活跃（想执行）</summary>
    //bool IsActive { get; }

    ///// <summary>当前状态名称（用于调试）</summary>
    //string CurrentFlowStateName { get; }

    protected ExploreFlowStateMachine exploreFlowStateMachine;
    protected GameFlowDataBus dataBus;

    public virtual void Initialize(ExploreFlowStateMachine stateMachine, GameFlowDataBus bus)
    {
        stateMachine = exploreFlowStateMachine;
        bus = dataBus;
    }

    public virtual void Enter() { }


    /// <summary>退出状态机（被仲裁器取消时调用）</summary>
    public virtual void Exit() { }

    /// <summary>每帧更新（只有活跃状态机会被调用）</summary>
    public virtual void Update(float deltaTime) { }

    /// <summary>接收状态请求（外部调用）</summary>
    public abstract bool CanTransitionTo(BaseExploreFlowState newState);

}
```

### `Assets/Script/GameFlowManager/ExplorationFlow/BasicExploreState.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicExploreState : BaseExploreFlowState
{
    public override string exploreStateName => "BattleFlowState";













    public override void Update(float deltaTime)
    {




    }

    public override bool CanTransitionTo(BaseExploreFlowState newState)
    {
        throw new System.NotImplementedException();
    }

}
```

### `Assets/Script/GameFlowManager/ExplorationFlow/ExploreFlowStateMachine.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class ExploreFlowStateMachine : IGameFlowState
{
    public string flowStateMachineName => "ExploreFlowState";

    public int Priority => 10; // 较低优先级，可被特殊动作中断

    public string currentFlowStateName => currentState?.exploreStateName ?? "None";


    private Dictionary<Type, BaseExploreFlowState> states;
    private BaseExploreFlowState currentState;

    private GameFlowDataBus dataBus;
    private GameFlowRoot root;


    public bool IsActive
    {
        get
        {
            // 移动状态机总是活跃的，除非角色死亡或不能移动
            return currentState != null;
        }
    }





    public void Update(float deltaTime)
    {




    }


    public void Initialize(GameFlowDataBus bus, GameFlowRoot root)
    {
        this.dataBus = bus;
        if (dataBus == null)
        {
            Debug.LogError("MovementStateMachine: 缺少CharacterDataBus");
            return;
        }
        this.root = root;
        // 初始化状态机的所有状态
        InitializeStates();

        //设置初始状态
        //TransitionTo<BasicBattleState>();

        //Debug.Log($"{StateMachineName} 初始化完成");
    }










    private void InitializeStates()
    {
        states = new Dictionary<Type, BaseExploreFlowState>
        {
            [typeof(BasicExploreState)] = new BasicExploreState(),
            //[typeof(WalkState)] = new WalkState(),
            //[typeof(RunState)] = new RunState(),
            //[typeof(CrouchState)] = new CrouchState()
            // 可以在这里添加更多移动状态：CrouchState, SlideState等
        };

        // 初始化每个状态
        foreach (var state in states.Values)
        {
            state.Initialize(this,dataBus);
        }
    }

    public bool TransitionTo<T>() where T : BaseExploreFlowState
    {
        if (!states.TryGetValue(typeof(T), out var newState))
        {
            Debug.LogWarning($"状态 {typeof(T).Name} 不存在");
            return false;
        }

        // 检查是否可以转换
        if (currentState != null && !currentState.CanTransitionTo(newState))
        {
            Debug.LogWarning($"无法从 {currentState.exploreStateName} 转换到 {newState.exploreStateName}");
            return false;
        }

        // 执行转换
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();

        //Debug.Log($"移动状态转换: {currentState.StateName}");
        return true;
    }




    public bool RequestState(object stateData)
    {
        // 移动状态机通常不接收外部请求
        // 移动状态转换由输入系统控制

        // 但如果需要，可以处理特定请求
        // 例如：强制站立、强制奔跑等
        if (stateData is string requestedState)
        {
            switch (requestedState)
            {
                case "ForceExplore":
                    TransitionTo<BasicExploreState>();
                    return true;

                    //case "ForceWalk":
                    //    TransitionTo<WalkState>();
                    //    return true;

                    //case "ForceRun":
                    //    TransitionTo<RunState>();
                    //    return true;
                    //case "ForceCrouch":
                    //    TransitionTo<CrouchState>();
                    //    return true;
            }
        }
        return false;
    }


    // ============ ICharacterStateMachine 接口方法 ============
    public void Enter()
    {
        Debug.Log($"{flowStateMachineName} 被激活");
        // 移动状态机被激活时，确保当前状态正确
        if (currentState == null)
        {
            TransitionTo<BasicExploreState>();
        }
        else
        {
            currentState.Enter();
        }
    }

    public void Exit()
    {
        //Debug.Log($"{StateMachineName} 被暂停");
        // 移动状态机被暂停时，可以做一些清理工作
        // 例如：停止移动特效
    }
}
```

### `Assets/Script/GameFlowManager/FlowRequest.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class FlowRequest
{
    public FlowRequest()
    {
        RequestTime = Time.time;
    }

    public GameFlowStateType flowType { get; protected set; }


    /// <summary>请求发起时间（用于超时处理）</summary>
    public float RequestTime { get; private set; }

    /// <summary>请求数据（具体内容由子类定义）</summary>
    public object Data { get; protected set; }



}
```

### `Assets/Script/GameFlowManager/GameFlowArbitrator.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using static UnityEngine.CullingGroup;

public class GameFlowArbitrator : MonoBehaviour, IGameSubsystem
{

    //public static GameFlowRoot Instance { get; private set; }
    private HashSet<IGameFlowState> uniqueStateMachines;


    //字典，用于存储状态类型和对应的状态机实例
    private Dictionary<GameFlowStateType, IGameFlowState> stateMachinesDictionary;

    //列表，用于存储所有注册的状态机实例（方便遍历筛选最高优先级的状态机执行）
    private Queue<FlowRequest> requestQueue;

    //当前活跃状态机
    private IGameFlowState activeStateMachine;

    // 状态机实例（将在Start中初始化）
    public HuntFlowStateMachine huntFlowSM;

    //private ExploreFlowStateMachine exploreSM;

    private GameFlowDataBus dataBus;
    private GameFlowRoot gameFlowRoot;





    private void Start()
    {
        SetInitialState();
    }
    void Update()
    {
        float deltaTime = Time.deltaTime;

        ProcessRequestQueue();

        // 步骤2: 决定当前应该激活哪个状态机
        IGameFlowState newActiveMachine = DetermineActiveStateMachine();

        //Debug.Log(newActiveMachine);
        // 步骤3: 如果需要切换状态机
        // 只有当新老大的优先级 >= 当前老大，或者当前老大已经不活跃了，才切
        if (newActiveMachine != null && newActiveMachine != activeStateMachine)
        {
            if (activeStateMachine == null ||
                newActiveMachine.Priority > activeStateMachine.Priority ||
                !activeStateMachine.IsActive)
            {
                PerformStateTransition(activeStateMachine, newActiveMachine);
            }
        }


        // 步骤4: 检查遍历当前注册的状态机的update
        if (activeStateMachine != null && activeStateMachine.IsActive)
        {

            activeStateMachine.Update(deltaTime);
        }
    }

    //对于角色仲裁器可以在characterRoot中统一初始化时机，那么对于游戏流程仲裁器，初始化时机也可以放在GameFlowRoot的awake中
    public void Initialize(GameFlowRoot root)
    {

        this.gameFlowRoot = root;
        this.dataBus = root.gameFlowDataBus;

        //初始化字典
        stateMachinesDictionary = new Dictionary<GameFlowStateType, IGameFlowState>();
        requestQueue = new Queue<FlowRequest>();
        uniqueStateMachines = new HashSet<IGameFlowState>();



        //调用初始化状态机的方法，创建并注册所有状态机实例
        InitializeStateMachines();





    }
    //初始化注册的状态机
    private void InitializeStateMachines()
    {
        //Log("初始化状态机...");
        // 创建状态机实例
        huntFlowSM = new HuntFlowStateMachine();
        //exploreSM = new ExploreFlowStateMachine();
        // 初始化并注册
        // 同一个状态机处理多个状态类型


        //====================战斗流程状态机============================   
        RegisterStateMachine(GameFlowStateType.HuntPrepare, huntFlowSM);
        RegisterStateMachine(GameFlowStateType.HuntLoadMap, huntFlowSM);
        RegisterStateMachine(GameFlowStateType.HuntSpawnPlayer, huntFlowSM);
        RegisterStateMachine(GameFlowStateType.HuntCombat, huntFlowSM);
        RegisterStateMachine(GameFlowStateType.HuntLevelUp, huntFlowSM);
        RegisterStateMachine(GameFlowStateType.HuntCountdown, huntFlowSM);
        RegisterStateMachine(GameFlowStateType.HuntResult, huntFlowSM);












        //RegisterStateMachine(GameFlowStateType.Exploration, exploreSM);
        // 这里可以继续注册更多状态机...
    }

    private void RegisterStateMachine(GameFlowStateType type, IGameFlowState machine)
    {
        if (machine == null)
        {
            Debug.LogError($"尝试注册空状态机: {type}");
            return;
        }
        // 同一个状态机只初始化一次
        if (!uniqueStateMachines.Contains(machine))
        {
            machine.Initialize(dataBus, gameFlowRoot);
            uniqueStateMachines.Add(machine);
        }
        stateMachinesDictionary[type] = machine;
    }


    private void SetInitialState()
    {
        // 默认为空闲状态
        activeStateMachine = huntFlowSM;
        activeStateMachine.Enter();
        //Log($"初始状态: {activeStateMachine.flowStateMachineName}");
    }

    private void ProcessRequestQueue()
    {
        // 处理队列中的所有请求（按先进先出）
        while (requestQueue.Count > 0)
        {

            FlowRequest request = requestQueue.Dequeue();
            ProcessRequestImmediately(request);
        }

    }
    private bool ProcessRequestImmediately(FlowRequest request)
    {

        //查询地址簿：仲裁器手里有一个字典(stateMachines)。它查了一下：“谁负责处理 Jump_PreStart？”
        //找到目标：字典告诉它：“是 jumpSM(JumpStateMachine) 负责。”
        //传达命令：仲裁器敲开 jumpSM 的门，调用 jumpSM.RequestState()，把数据丢给它。

        // 查找对应的状态机,依靠每个状态机脚本都被独立的characterStateType标记
        if (!stateMachinesDictionary.TryGetValue(request.flowType, out IGameFlowState targetMachine))
        {
            //LogWarning($"没有找到处理 {request.characterStateType} 的状态机");
            return false;
        }
        // 将请求传递给状态机
        bool accepted = targetMachine.RequestState(request.Data);
        //if (accepted)
        //{
        //    Log($"状态请求被接受: {request.characterStateType}");
        //}
        //else
        //{
        //    Log($"状态请求被拒绝: {request.characterStateType}");
        //}
        return accepted;
    }


    // ============ 仲裁逻辑 ============
    private IGameFlowState DetermineActiveStateMachine()
    {

        IGameFlowState highestPriorityMachine = null;
        int maxPriority = int.MinValue;

        foreach (var machine in uniqueStateMachines)
        {
            if (machine.IsActive)
            {
                if (machine.Priority > maxPriority)
                {
                    maxPriority = machine.Priority;
                    highestPriorityMachine = machine;
                }
            }
        }
        return highestPriorityMachine ?? huntFlowSM;
        //return highestPriorityMachine;

    }

    private void PerformStateTransition(IGameFlowState from, IGameFlowState to)
    {
        //通过from，to 决定新旧状态，调用旧状态的exit，新状态的enter

        //Log($"状态切换: {from?.StateMachineName ?? "None"} -> {to.StateMachineName}");
        // 退出旧状态
        from?.Exit();

        // 进入新状态
        activeStateMachine = to;
        activeStateMachine.Enter();

        // 触发事件（可选）
        OnStateChanged?.Invoke(from, to);
    }









    // ============ 事件 ============
    public event Action<IGameFlowState, IGameFlowState> OnStateChanged;



    // ============ 工具方法 ============
    private void Log(string message)
    {

    }

}
```

### `Assets/Script/GameFlowManager/GameFlowDataBus.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameFlowDataBus
{







}
```

### `Assets/Script/GameFlowManager/GameFlowRoot.cs`

```cs
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameFlowRoot : MonoBehaviour
{
    public static GameFlowRoot Instance { get; private set; }



    private bool isInitialized = false;

    [field: SerializeField] public GameFlowDataBus gameFlowDataBus { get; private set; }

    [field: SerializeField] public GameFlowArbitrator gameFlowArbitrator { get; private set; }
    [field: SerializeField] public HuntFlowRoot huntFlow { get; private set; }

    [field: SerializeField] public GameAudioRoot audioRoot { get; private set; }
    [field: SerializeField] public AudioArbitrator audioArbitrator { get; private set; }



    public GameEventHub EventHub { get; private set; }


    private void Reset()
    {
        AutoGetComponent();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[GameFlowRoot] 场景中出现了多个 GameFlowRoot，销毁重复对象", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Initialize();
    }
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Initialize()
    {
        if (isInitialized) return;


        AutoGetComponent();
        EventHub = new GameEventHub();
        gameFlowDataBus = new GameFlowDataBus();

        if (audioRoot != null)
        {
            audioRoot.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 没有找到 GameAudioRoot", this);
        }
        if (audioArbitrator != null)
        {
            audioArbitrator.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 没有找到 AudioArbitrator", this);
        }

        // 1. 先初始化 BattleFlow
        if (huntFlow != null)
        {
            //battleFlow.Initialize(this);

            huntFlow.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 没有找到 HuntFlowRoot", this);
        }



        // 2. 再初始化流程仲裁器/状态机
        if (gameFlowArbitrator != null)
        {
            gameFlowArbitrator.Initialize(this);
        }
        else
        {
            Debug.LogError("[GameFlowRoot] 没有找到 GameFlowArbitrator", this);
        }

        Debug.Log("[GameFlowRoot] 初始化完成");

        isInitialized = true;
    }

    private void AutoGetComponent()
    {
        gameFlowArbitrator = GetOrAddComponent<GameFlowArbitrator>();

        if (audioRoot == null)
        {
            audioRoot = GetComponentInChildren<GameAudioRoot>(true);
        }

        if (audioArbitrator == null)
        {
            audioArbitrator = GetComponentInChildren<AudioArbitrator>(true);
        }


        if (huntFlow == null)
        {
            huntFlow = GetComponentInChildren<HuntFlowRoot>(true);
        }
 

    }

    private T GetOrAddComponent<T>() where T : Component
    {
        T comp = GetComponent<T>();

        if (comp == null)
        {
            comp = gameObject.AddComponent<T>();
        }

        return comp;
    }


}
```

### `Assets/Script/GameFlowManager/GameFlowStateRequest.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameFlowStateRequest : FlowRequest
{

    // 通过构造函数给父类的 protected 属性赋值
    //构造函数是一种特殊的方法，特点：
    //名称必须和类名完全相同
    //没有返回值（连 void 都不用写）
    //在创建对象时自动调用
    public GameFlowStateRequest(GameFlowStateType type,object data = null) : base()
    {
        this.flowType = type;
        //this.IsUrgent = isUrgent;
        this.Data = data;
    }





}
```

### `Assets/Script/GameFlowManager/HuntFlow/BaseHuntFlowState.cs`

```cs
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
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntCombatState.cs`

```cs
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
        hunt.huntFlowDataBus.isPaused = false;

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
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntCountdownState.cs`

```cs
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
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntFlowDataBus.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntFlowDataBus
{
    public CharacterRoot player;
    public bool isRunActive;
    public bool isPaused;
    public bool isGameOver;

    public float elapsedTime;
    public float runDuration;

    public int killCount;
    public int currentLevel;
    public int currentExp;
    public int expToNextLevel;

    public List<CharacterRoot> aliveEnemies = new();
    public List<CharacterRoot> aliveAllies = new();

    //public RunConfigSO runConfig;
    //public MapConfigSO mapConfig;
    //public EnemyWaveConfigSO waveConfig;

    public void Reset()
    {
        isRunActive = false;
        isPaused = false;
        isGameOver = false;

        elapsedTime = 0f;
        killCount = 0;
        currentLevel = 1;
        currentExp = 0;

        player = null;
        aliveEnemies.Clear();
        aliveAllies.Clear();
    }
}
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntFlowRoot.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntFlowRoot : MonoBehaviour
{
    [SerializeField] private GameFlowRoot gameFlowRoot;
    [field: SerializeField] public HuntFlowDataBus huntFlowDataBus { get; private set; }
    [field: SerializeField] public HuntPlayerSpawner huntPlayerSpawner { get; private set; }




    private bool isInitialized;
    private void Reset()
    {
        AutoGetComponents();
    }

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized) return;
        isInitialized = true;

        gameFlowRoot = root;




        AutoGetComponents();

        huntFlowDataBus ??= new HuntFlowDataBus();

        IHuntSubsystem[] subsystems = GetComponentsInChildren<IHuntSubsystem>(true);
        foreach (var subsystem in subsystems)
        {
            subsystem.Initialize(this);
        }

        //Debug.Log($"[RunFlowRoot] 初始化完成，子系统数量：{subsystems.Length}");
    }

    private void AutoGetComponents()
    {
        if (gameFlowRoot == null)
            gameFlowRoot = GetComponentInParent<GameFlowRoot>();

        if (huntPlayerSpawner == null)
            huntPlayerSpawner = GetComponentInChildren<HuntPlayerSpawner>(true);

    }



}
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntFlowStateMachine.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class HuntFlowStateMachine : IGameFlowState
{
    public string flowStateMachineName => "HuntFlowStateMachine";
    public string currentFlowStateName => currentState?.huntStateName ?? "None";

    public int Priority => 20;

   // public bool IsActive => currentState != null;


    private bool isRunning;
    public bool IsActive => isRunning;

    private Dictionary<Type, BaseHuntFlowState> states;
    private BaseHuntFlowState currentState;

    private GameFlowDataBus gameDataBus;
    private GameFlowRoot gameFlowRoot;

    public void Initialize(GameFlowDataBus bus, GameFlowRoot root)
    {
        gameDataBus = bus;
        gameFlowRoot = root;

        InitializeStates();

    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, BaseHuntFlowState>
        {
            [typeof(HuntPrepareState)] = new HuntPrepareState(gameFlowRoot),
            [typeof(HuntLoadMapState)] = new HuntLoadMapState(gameFlowRoot),
            [typeof(HuntSpawnPlayerState)] = new HuntSpawnPlayerState(gameFlowRoot),
            [typeof(HuntCountdownState)] = new HuntCountdownState(gameFlowRoot),
            [typeof(HuntCombatState)] = new HuntCombatState(gameFlowRoot),
            [typeof(HuntLevelUpState)] = new HuntLevelUpState(gameFlowRoot),
            //[typeof(HuntBossState)] = new HuntBossState(gameFlowRoot),
            [typeof(HuntResultState)] = new HuntResultState(gameFlowRoot),
            //[typeof(HuntCleanupState)] = new HuntCleanupState(gameFlowRoot),
        };

        foreach (var state in states.Values)
        {
            state.Initialize(this, gameDataBus);
        }
    }

    public bool TransitionTo<T>() where T : BaseHuntFlowState
    {
        if (!states.TryGetValue(typeof(T), out var newState))
        {
            Debug.LogWarning($"状态 {typeof(T).Name} 不存在");
            return false;
        }

        if (currentState != null && !currentState.CanTransitionTo(newState))
        {
            //Debug.LogWarning($"无法从 {currentState.huntStateName} 切换到 {newState.huntStateName}");
            return false;
        }

        BaseHuntFlowState oldState = currentState;
        currentState?.Exit();
        currentState = newState;

        gameFlowRoot.EventHub.Hunt.Publish(new HuntFlowStateChangedEvent
        {
            from = oldState != null ? oldState.FlowStateType : GameFlowStateType.None,
            to = currentState.FlowStateType
        });

        currentState.Enter();

        //Debug.Log($"[RunFlow] 切换到：{currentState.huntStateName}");

        return true;
    }

    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);
    }

    public void Enter()
    {


        isRunning = true;
        if (currentState == null)
            TransitionTo<HuntPrepareState>();
        else
            currentState.Enter();
    }

    public void Exit()
    {
        currentState?.Exit();
    }

    public bool RequestState(object stateData)
    {
        if (stateData is string request)
        {
            switch (request)
            {
                case "StartRun":
                    TransitionTo<HuntPrepareState>();
                    return true;

                case "PauseRun":
                    // 也可以交给独立 PauseFlowStateMachine
                    return true;

                case "EndRun":
                    TransitionTo<HuntResultState>();
                    return true;
            }
        }

        return false;
    }
}
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntLevelUpState.cs`

```cs
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
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntLoadMapState.cs`

```cs
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
        //Debug.Log("[RunLoadMap] 加载地图");

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
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntPlayerSpawner.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntPlayerSpawner : MonoBehaviour, IHuntSubsystem
{
    private HuntFlowRoot huntFlowRoot;

    public GameObject actorPrefab;
    public CharacterSO playerSO;
    public Transform spawnPoint;

    [Header("Camera")]
    [SerializeField] private ProCameraRuntimeBinder cameraBinder;


    public void Initialize(HuntFlowRoot root)
    {
        huntFlowRoot = root;
        if (cameraBinder == null)
        {
            cameraBinder = FindObjectOfType<ProCameraRuntimeBinder>();
        }
    }

    public CharacterRoot SpawnPlayer()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;

        GameObject go = Instantiate(actorPrefab, pos, Quaternion.identity);
        if (go == null)
        {
            Debug.LogError("[HuntPlayerSpawner] Instantiate actorPrefab ʧ��");
            return null;
        }
        CharacterRoot player = go.GetComponent<CharacterRoot>();
        if (player == null)
        {
            Debug.LogError("[HuntPlayerSpawner] ���ɵ� actorPrefab ��û�� CharacterRoot ���");
            return null;
        }

        if (playerSO == null)
        {
            Debug.LogError("[HuntPlayerSpawner] playerSO Ϊ��");
            return null;
        }

        //player.Initialize();
        player.InitializeWithSO(playerSO);

        player.characterDataBus.battleCamp = BattleCamp.MateSide;
        player.characterDataBus.currentHp = player.characterStatsController.GetStat(CharacterStats.MaxHP);//ͨ����������ֵ����ǽ�ɫ���״̬��ȷ�����ǻᱻ����������ȷʶ��ʹ���
        player.characterDataBus.canMove = true;

        player.skeletonAnimation.Skeleton.ScaleX = player.characterDataBus.FinalVisualScaleX;

        player.name = playerSO.characterName;

        player.stateArbitrator.SubmitRequest(
            new CharacterStateRequest(CharacterStateType.Idle, true)
        );

        huntFlowRoot.huntFlowDataBus.player = player;
        if (cameraBinder != null)
        {
            cameraBinder.BindTarget(player.transform);
        }
        else
        {
            Debug.LogWarning("[HuntPlayerSpawner] û���ҵ� ProCameraRuntimeBinder���������������");
        }
        //Debug.Log($"[HuntPlayerSpawner] ������ң�{player.name}");

        return player;
    }
}
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntPrepareState.cs`

```cs
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
```
