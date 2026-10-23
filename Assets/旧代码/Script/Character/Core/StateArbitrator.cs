using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态仲裁器 - 整个角色状态系统的"大脑"
/// </summary>
public class StateArbitrator : MonoBehaviour,ICharacterSubsystem,ICharacterTickable,ICharacterFixedTickable
{

    // ============ 配置字段 ============
    [Header("状态机配置")]
    [SerializeField] private bool enableLogging = true;
    [Header("中断规则")]
    [Tooltip("是否允许低优先级中断高优先级（通常为false）")]
    [SerializeField] private bool allowLowPriorityInterruption = false;
    // ============ 运行时字段 ============
    private Dictionary<CharacterStateType, ICharacterStateMachine> stateMachinesDictionary;

    private HashSet<ICharacterStateMachine> uniqueStateMachines = new HashSet<ICharacterStateMachine>();//用哈希表来存储唯一的状态机实例，避免重复添加同一个状态机到列表中
    private List<IParallelCharacterStateMachine> parallelStateMachines = new List<IParallelCharacterStateMachine>();

    private Queue<StateRequest> requestQueue;

    private ICharacterStateMachine activeStateMachine;
    private CharacterDataBus dataBus;
    private Transform transform;
    private Rigidbody2D rigidbody2D;
    private CharacterRoot characterRoot;

    //==============初始化锁=============
    private bool _isInitialized;
    //private bool machinesCreated = false;


    //private List<ICharacterStateMachine> stateMachinesList;
    // 状态机实例（将在Start中初始化）
    private MovementStateMachine movementSM;
    private JumpStateMachine jumpSM;
    public WeaponEquipStateMachine weaponEquipSM;
    public WeaponPostureStateMachine weaponPostureSM;
    public HoundCombatActionStateMachine houndCombatActionSM;


    // ============ 初始化 ============

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        this.characterRoot = root;
        this.dataBus = root.characterDataBus;
        this.rigidbody2D = root.rigidBody2d;
        this.transform = root.transform;
        //Log("状态仲裁器初始化...");

        // 初始化数据结构
        stateMachinesDictionary = new Dictionary<CharacterStateType, ICharacterStateMachine>();
        requestQueue = new Queue<StateRequest>();
        // 创建所有状态机实例
        InitializeStateMachines();

        _isInitialized = true;
    }

    void Start()
    {

        // 设置初始状态
        // 如果已经在战斗中（由 Initialize 后的 Request 触发），不要重置
        if (activeStateMachine == null)
        {
            //Debug.Log("<color=red>仲裁器 Start 被调用！</color>");
            SetInitialState();
        }
    }

    private void InitializeStateMachines()
    {


        //if (machinesCreated) return; // 绝不允许重复创建状态机实例，否则之前的状态全丢了


        Log("初始化状态机...");
        // 创建状态机实例
        movementSM = new MovementStateMachine();
        houndCombatActionSM = new HoundCombatActionStateMachine();
        weaponEquipSM = new WeaponEquipStateMachine();
        weaponPostureSM = new WeaponPostureStateMachine();



        jumpSM = new JumpStateMachine();

        // 初始化并注册
        // 同一个状态机处理多个状态类型
        RegisterStateMachine(CharacterStateType.Idle, movementSM);
        RegisterStateMachine(CharacterStateType.Walk, movementSM);  
        RegisterStateMachine(CharacterStateType.Run, movementSM);    
        RegisterStateMachine(CharacterStateType.Crouch, movementSM);
        RegisterStateMachine(CharacterStateType.Jump_PreStart, jumpSM);

        //RegisterParallelStateMachine(autoAttackSM);
        RegisterParallelStateMachine(houndCombatActionSM);
        RegisterParallelStateMachine(weaponEquipSM);
        RegisterParallelStateMachine(weaponPostureSM);

        parallelStateMachines.Sort(
    (a, b) => a.Priority.CompareTo(b.Priority)
);



        // 这里可以继续注册更多状态机...

        //machinesCreated = true;

    }

    private void RegisterStateMachine(CharacterStateType type, ICharacterStateMachine machine)
    {

        if (machine == null) return;


        if (!uniqueStateMachines.Contains(machine))
        {
            machine.Initialize(dataBus, rigidbody2D, transform, characterRoot);
            uniqueStateMachines.Add(machine);
        }

        // 2. 【最简映射】一行搞定字典注册（无论是否已存在）
        stateMachinesDictionary[type] = machine;


        //Log($"注册状态机: {type} -> {machine.StateMachineName}");
    }
    private void RegisterParallelStateMachine(IParallelCharacterStateMachine machine)
    {
        if (machine == null)
            return;

        machine.Initialize(dataBus, rigidbody2D, transform, characterRoot);

        if (!parallelStateMachines.Contains(machine))
            parallelStateMachines.Add(machine);
    }
    private void SetInitialState()
    {
        // 默认为空闲状态
        activeStateMachine = movementSM;
        //activeStateMachine.Enter();
        Log($"初始状态: {activeStateMachine.StateMachineName}");
    }

    // ============ 主要更新循环 ============
    public void ManagedUpdate(float deltaTime)
    {
        // 步骤1: 处理所有排队的状态请求
        ProcessRequestQueue();
        // 步骤2: 决定当前应该激活哪个状态机
        ICharacterStateMachine newActiveMachine = DetermineActiveStateMachine();
        // 步骤3: 如果需要切换状态机
        if (newActiveMachine != null)//如果没找到任何活跃状态机，维持现状
        {
            if (activeStateMachine == null)
            {
                activeStateMachine = newActiveMachine;
                activeStateMachine.Enter();
            }
            else if (newActiveMachine != activeStateMachine)
            {
                // 只有当新老大的优先级 >= 当前老大，或者当前老大已经不活跃了，才切
                if (newActiveMachine.Priority > activeStateMachine.Priority || !activeStateMachine.IsActive)
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

        UpdateParallelStateMachines(deltaTime);
    }

    private void UpdateParallelStateMachines(float deltaTime)
    {
        for (int i = 0; i < parallelStateMachines.Count; i++)
        {
            IParallelCharacterStateMachine machine = parallelStateMachines[i];

            if (machine == null)
                continue;

            if (machine.IsActive)
            {
                machine.Update(deltaTime);
            }
            else
            {
                machine.Exit();
            }
        }
    }


    public void ManagedFixedUpdate(float deltaTime)
    {
        if (activeStateMachine != null && activeStateMachine.IsActive)
        {
            activeStateMachine.FixedUpdate(deltaTime);
        }

        for (int i = 0; i < parallelStateMachines.Count; i++)
        {
            IParallelCharacterStateMachine machine = parallelStateMachines[i];

            if (machine == null)
                continue;

            if (machine.IsActive)
                machine.FixedUpdate(deltaTime);
        }
    }

    // ============ 请求处理 ============
    /// <summary>
    /// 提交状态请求（外部系统调用这个）
    /// </summary>
    public bool SubmitRequest(StateRequest request)
    {
        //特性 Queue<T>（队列）	List<T>（列表）
        //数据结构 先进先出（FIFO）	动态数组
        //访问方式 只能从队头取出，队尾添加 任意索引访问
        //典型用途 排队系统、任务队列 数据存储、遍历
        //if (request == null)
        //{
        //    LogWarning("收到空的状态请求");
        //    return false;
        //}
        //Log($"收到状态请求: {request.Type} (紧急: {request.IsUrgent})");
        // 紧急请求立即处理，非紧急请求加入队列
        if (request.IsUrgent)
        {
            return ProcessRequestImmediately(request);
        }
        else
        {
            requestQueue.Enqueue(request);
            return true;
        }
    }
    private void ProcessRequestQueue()
    {
        // 处理队列中的所有请求（按先进先出）
        while (requestQueue.Count > 0)
        {
            
            StateRequest request = requestQueue.Dequeue();
            ProcessRequestImmediately(request);
        }

    }
    private bool ProcessRequestImmediately(StateRequest request)
    {

        //查询地址簿：仲裁器手里有一个字典(stateMachines)。它查了一下：“谁负责处理 Jump_PreStart？”
        //找到目标：字典告诉它：“是 jumpSM(JumpStateMachine) 负责。”
        //传达命令：仲裁器敲开 jumpSM 的门，调用 jumpSM.RequestState()，把数据丢给它。

        // 查找对应的状态机,依靠每个状态机脚本都被独立的characterStateType标记
        if (!stateMachinesDictionary.TryGetValue(request.characterStateType, out ICharacterStateMachine targetMachine))
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
    private ICharacterStateMachine DetermineActiveStateMachine()
    {
        //查询每个状态机的active情况，若active，则对比谁是高权限
        //Debug.Log($"=== 开始仲裁 ===");
        foreach (var m in stateMachinesDictionary.Values)
        {
            //Debug.Log($"{m.StateMachineName}: IsActive={m.IsActive}, Priority={m.Priority}");
        }

        ICharacterStateMachine highestPriorityMachine = null;
        int maxPriority = int.MinValue;

        // 1. 遍历所有注册的状态机
        foreach (var machine in uniqueStateMachines)
        {
            // 只要它是活跃的 (IsActive)
            if (machine.IsActive)
            {
                // 2. 找到优先级最高的那一个
                if (machine.Priority > maxPriority)
                {
                    maxPriority = machine.Priority;
                    highestPriorityMachine = machine;
                }
            }
        }

        return highestPriorityMachine; // 允许返回 null
    }

    //private ICharacterStateMachine CalculateBestStateMachine()
    //{
    //    ICharacterStateMachine highestSM = null;
    //    int maxPriority = -1;

    //    // 遍历所有注册的状态机（按优先级排序）
    //    foreach (var sm in stateMachinesList) // 建议你存一个 List 方便遍历
    //    {
    //        if (sm.IsActive && sm.Priority > maxPriority)
    //        {
    //            maxPriority = sm.Priority;
    //            highestSM = sm;
    //        }
    //    }
    //    return highestSM;
    //}

    private bool CanTransitionTo(ICharacterStateMachine from, ICharacterStateMachine to)
    {
        if (from == null || to == null) return true;
        if (from == to) return true;


        // 简单粗暴但有效的临时方案（适合调试）：
        //if (to.IsUrgentRequestActive) // 需要在 JumpSM 中设置这个字段
        //    return true;
        // 规则1: 高优先级可以中断低优先级
        if (to.Priority > from.Priority)
        {
            // 检查目标状态机是否允许中断当前状态
            return to.CanInterrupt(from);
        }
        // 规则2: 如果允许低优先级中断（通常为false）
        if (allowLowPriorityInterruption && to.Priority <= from.Priority)
        {
            // 检查当前状态机是否允许被中断
            return from.CanBeInterruptedBy(to);
        }
        // 规则3: 紧急请求可以中断（即使优先级相同或更低）
        // 这个需要在请求处理时特殊标记
        return false;
    }
    private void PerformStateTransition(ICharacterStateMachine from, ICharacterStateMachine to)
    {
        string reason = "";
        if (to.Priority > from.Priority) reason = "更高优先级抢占";
        if (!from.IsActive) reason = "旧状态机不再活跃";

        //Debug.Log($"<color=yellow>[状态切换]</color> 从 {from.StateMachineName} 切到 {to.StateMachineName}。原因: {reason}");
        // 退出旧状态
        from?.Exit();

        // 进入新状态
        activeStateMachine = to;
        activeStateMachine.Enter();

        // 触发事件（可选）
        OnStateChanged?.Invoke(from, to);
    }
    // ============ 工具方法 ============
    private void Log(string message)
    {
        if (enableLogging)
        {
            //Debug.Log($"[Arbitrator] {message}");
        }
    }
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[Arbitrator] {message}");
    }
    // ============ 公共接口 ============
    /// <summary>获取当前活跃状态机</summary>
    public ICharacterStateMachine GetActiveStateMachine() => activeStateMachine;

    /// <summary>获取指定类型的状态机</summary>
    public ICharacterStateMachine GetStateMachine(CharacterStateType type)
    {
        stateMachinesDictionary.TryGetValue(type, out var machine);
        return machine;
    }
    // ============ 事件 ============
    public event Action<ICharacterStateMachine, ICharacterStateMachine> OnStateChanged;
}
