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
    public DRPGBattleFlowStateMachine drpgBattleFlowSM;

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
        drpgBattleFlowSM = new DRPGBattleFlowStateMachine();
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

        RegisterStateMachine(GameFlowStateType.BattleStart, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.TurnBegin, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.SkillSelect, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.TargetSelect, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.ActionPrepare, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.ActionExecution, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.ActionEnd, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.TurnEnd, drpgBattleFlowSM);
        RegisterStateMachine(GameFlowStateType.BattleResult, drpgBattleFlowSM);












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
        // The application loop owns when an expedition begins. Title and
        // StrategicHub intentionally leave Hunt inactive.
        activeStateMachine = null;
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
        return highestPriorityMachine;

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

    public GalStartResult RequestGal(GalPlayRequest request)
    {
        if (gameFlowRoot == null || gameFlowRoot.galFlow == null)
            return GalStartResult.RejectedByFlow;

        return gameFlowRoot.galFlow.RequestGal(request);
    }

    public bool SubmitRequest(FlowRequest request)
    {
        if (request == null || stateMachinesDictionary == null)
            return false;

        return ProcessRequestImmediately(request);
    }

    public bool TryActivateHunt()
    {
        if (huntFlowSM == null)
            return false;
        if (activeStateMachine == huntFlowSM && huntFlowSM.IsActive)
            return true;
        if (activeStateMachine != null && activeStateMachine.IsActive)
            return false;

        IGameFlowState previous = activeStateMachine;
        try
        {
            previous?.Exit();
            activeStateMachine = huntFlowSM;
            if (!huntFlowSM.TryActivate())
            {
                huntFlowSM.Deactivate();
                activeStateMachine = null;
                return false;
            }

            OnStateChanged?.Invoke(previous, huntFlowSM);
            return true;
        }
        catch (Exception exception)
        {
            huntFlowSM.Deactivate();
            activeStateMachine = null;
            Debug.LogException(exception, this);
            return false;
        }
    }

    public void DeactivateHunt()
    {
        if (huntFlowSM == null)
            return;

        bool wasActive = ReferenceEquals(activeStateMachine, huntFlowSM);
        huntFlowSM.Deactivate();
        if (wasActive)
        {
            activeStateMachine = null;
            OnStateChanged?.Invoke(huntFlowSM, null);
        }
    }



    // ============ 工具方法 ============
    private void Log(string message)
    {

    }

}
