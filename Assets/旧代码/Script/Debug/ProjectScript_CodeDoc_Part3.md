# ProjectScript Code Documentation - Part 3

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/ICharacterTickable.cs` | 4 | - | interface ICharacterTickable | 0 |
| `Assets/Script/Character/Core/IParallelCharacterStateMachine.cs` | 4 | - | interface IParallelCharacterStateMachine | 0 |
| `Assets/Script/Character/Core/StateArbitrator.cs` | 409 | - | class StateArbitrator | 0 |
| `Assets/Script/Character/Core/StateRequest.cs` | 38 | - | class StateRequest | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatActionState.cs` | 54 | - | class HoundCombatActionState | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatActionStateAnimationBridge.cs` | 270 | - | class HoundCombatActionStateAnimationBridge | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatActionStateMachine.cs` | 959 | - | class HoundCombatActionStateMachine, enum CombatInputDecision | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatAimState.cs` | 109 | - | class HoundCombatAimState | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatFireState.cs` | 184 | - | class HoundCombatFireState | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatIdleState.cs` | 88 | - | class HoundCombatIdleState | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatMeleeState.cs` | 434 | - | class HoundCombatMeleeState | 0 |
| `Assets/Script/Character/Core/States/Attack/HoundCombatReloadState.cs` | 240 | - | class HoundCombatReloadState | 0 |
| `Assets/Script/Character/Core/States/IdleState.cs` | 61 | - | class IdleState | 0 |
| `Assets/Script/Character/Core/States/MovementState.cs` | 42 | - | class MovementState | 0 |
| `Assets/Script/Character/Core/States/MovementStateMachine.cs` | 505 | - | class MovementStateMachine | 0 |
| `Assets/Script/Character/Core/States/MoveState.cs` | 240 | - | class MoveState | 0 |
| `Assets/Script/Character/Core/States/WeaponEquip/WeaponEquipStateMachine.cs` | 739 | - | class WeaponEquipStateMachine | 0 |
| `Assets/Script/Character/Core/States/WeaponPosture/WeaponPostureStateMachine.cs` | 163 | - | class WeaponPostureStateMachine | 0 |
| `Assets/Script/Character/Core/StateType.cs` | 5 | - | - | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationArbitrator.cs` | 67 | - | class AnimationArbitrator | 0 |

## Public API

### `Assets/Script/Character/Core/ICharacterTickable.cs`
- `public interface ICharacterTickable
{`
### `Assets/Script/Character/Core/IParallelCharacterStateMachine.cs`
- `public interface IParallelCharacterStateMachine : ICharacterStateMachine
{`
### `Assets/Script/Character/Core/StateArbitrator.cs`
- `public class StateArbitrator : MonoBehaviour,ICharacterSubsystem,ICharacterTickable,ICharacterFixedTickable
{`
- `public WeaponEquipStateMachine weaponEquipSM;`
- `public WeaponPostureStateMachine weaponPostureSM;`
- `public HoundCombatActionStateMachine houndCombatActionSM;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedUpdate(float deltaTime)
    {`
- `public void ManagedFixedUpdate(float deltaTime)
    {`
- `public bool SubmitRequest(StateRequest request)
    {`
- `public ICharacterStateMachine GetActiveStateMachine() => activeStateMachine;`
- `public ICharacterStateMachine GetStateMachine(CharacterStateType type)
    {`
- `public event Action<ICharacterStateMachine, ICharacterStateMachine> OnStateChanged;`
### `Assets/Script/Character/Core/StateRequest.cs`
- `public abstract class StateRequest
{`
- `public CharacterStateType characterStateType {`
- `public int OverridePriority {`
- `public bool IsUrgent {`
- `public float RequestTime {`
- `public object Data {`
- `public System.Action onActionPoint;`
- `public StateRequest()
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatActionState.cs`
- `public abstract class HoundCombatActionState
{`
- `protected HoundCombatActionStateMachine stateMachine;`
- `protected CharacterDataBus dataBus;`
- `protected Transform transform;`
- `protected Rigidbody2D rigidbody;`
- `protected CharacterRoot characterRoot;`
- `public abstract string StateName {`
- `public virtual void Initialize(
        HoundCombatActionStateMachine owner,
        CharacterDataBus bus,
        Transform tf,
        Rigidbody2D rb,
        CharacterRoot root)
    {`
- `public virtual void Enter()
    {`
- `public virtual void Exit()
    {`
- `public virtual void Update(float deltaTime)
    {`
- `public virtual void FixedUpdate(float fixedDeltaTime)
    {`
- `public virtual bool CanTransitionTo(HoundCombatActionState next)
    {`
- `public virtual void OnSpineSignal(CharacterSpineAnimationSignal signal)
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatActionStateAnimationBridge.cs`
- `public class HoundCombatActionStateAnimationBridge
{`
- `public HoundCombatActionStateAnimationBridge(
        CharacterDataBus dataBus,
        CharacterRoot characterRoot,
        CharacterAnimationComposer animationComposer)
    {`
- `public void Tick()
    {`
- `public void ClearAll()
    {`
- `public AnimationIntent GetWeaponPoseIntent()
    {`
- `public bool ShouldKeepHipFirePose()
    {`
- `public void KeepHipFirePose(float duration)
    {`
- `public void SubmitAimPoseIntent()
    {`
- `public void SubmitHipFirePoseIntent()
    {`
- `public void SubmitFireIntent(float duration)
    {`
- `public void SubmitReloadIntent(float duration)
    {`
- `public void SubmitMeleeIntent(EAnimSemantic semantic, float duration, int replayId)
    {`
- `public void RemoveWeaponPoseIntent()
    {`
- `public void RemoveWeaponActionIntent()
    {`
- `public void StopHipFirePoseHold()
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatActionStateMachine.cs`
- `public class HoundCombatActionStateMachine : IParallelCharacterStateMachine
{`
- `public enum CombatInputDecision
    {`
- `public string StateMachineName => "HoundCombatActionStateMachine";`
- `public int Priority => 15;`
- `public string CurrentStateName => currentState != null ? currentState.StateName : "None";`
- `public bool IsActive
    {`
- `public GunSystemController gunController
    {`
- `public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {`
- `public void Enter()
    {`
- `public void Exit()
    {`
- `public void Update(float deltaTime)
    {`
- `public void FixedUpdate(float fixedDeltaTime)
    {`
- `public bool RequestState(object stateData)
    {`
- `public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {`
- `public bool CanInterrupt(ICharacterStateMachine other)
    {`
- `public bool TransitionTo<T>() where T : HoundCombatActionState
    {`
- `public bool IsCombatAvailable()
    {`
- `public bool IsCurrentWeaponFirearm()
    {`
- `public bool IsCurrentWeaponMelee()
    {`
- `public bool CanAim()
    {`
- `public bool CanFireCurrentWeapon()
    {`
- `public bool CanReload()
    {`
- `public void MarkCombatIntent()
    {`
- `public CombatInputDecision EvaluateIdleInput()
    {`
- `public void ExecuteIdleInputDecision(
    CombatInputDecision decision)
    {`
- `public bool CanRequestReload()
    {`
- `public FirearmActionProfileSO GetFirearmProfile()
    {`
- `public WeaponActionProfileSO GetActionProfile()
    {`
- `public float GetFireDuration()
    {`
- `public float GetReloadDuration()
    {`
- `public float GetMeleeAnimationDuration(
    EAnimSemantic semantic,
    float fallback)
    {`
- `public bool FireCurrentWeapon()
    {`
- `public void ApplyReload()
    {`
- `public AnimationIntent GetWeaponPoseIntent()
    {`
- `public bool ShouldKeepHipFirePose()
    {`
- `public void KeepHipFirePose(float duration)
    {`
- `public void SubmitAimPoseIntent()
    {`
- `public void SubmitHipFirePoseIntent()
    {`
- `public void SubmitFireIntent()
    {`
- `public void SubmitReloadIntent(float duration)
    {`
- `public void SubmitMeleeIntent(EAnimSemantic semantic, float duration, int replayId)
    {`
- `public void RemoveWeaponPoseIntent()
    {`
- `public void SubmitLowReadyPoseIntent()
    {`
- `public void SetWeaponPoseLowReady()
    {`
- `public void HardClearWeaponAnimation()
    {`
- `public string ResolveReloadAnimationName()
    {`
- `public float GetAnimationDurationOrFallback(string animationName, float fallback)
    {`
- `public void StopFirePoseHold()
    {`
- `public void RemoveWeaponActionIntent()
    {`
- `public void RemoveMeleeIntent()
    {`
- `public void ClearRuntimeFlags()
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatAimState.cs`
- `public class HoundCombatAimState : HoundCombatActionState
{`
- `public override string StateName => "HoundCombat_Aim";`
- `public override void Enter()
    {`
- `public override void Update(float deltaTime)
    {`
- `public override void Exit()
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatFireState.cs`
- `public class HoundCombatFireState : HoundCombatActionState
{`
- `public override string StateName => "HoundCombat_Fire";`
- `public override void Enter()
    {`
- `public override void Update(float deltaTime)
    {`
- `public override void Exit()
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatIdleState.cs`
- `public class HoundCombatIdleState : HoundCombatActionState
{`
- `public override string StateName => "HoundCombat_Idle";`
- `public override void Enter()
    {`
- `public override void Update(float deltaTime)
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatMeleeState.cs`
- `public class HoundCombatMeleeState : HoundCombatActionState
{`
- `public override string StateName =>"HoundCombat_FirearmMelee";`
- `public override void Enter()
    {`
- `public override void Exit()
    {`
- `public override void Update(float deltaTime)
    {`
- `public override void FixedUpdate(float fixedDeltaTime)
    {`
### `Assets/Script/Character/Core/States/Attack/HoundCombatReloadState.cs`
- `public class HoundCombatReloadState : HoundCombatActionState
{`
- `public override string StateName => "HoundCombat_Reload";`
- `public override void Enter()
    {`
- `public override void Exit()
    {`
- `public override void Update(float deltaTime)
    {`
- `public void OnReloadCommitEvent()
    {`
- `public void OnReloadMagazineFallEvent()
    {`
- `public void OnReloadMagazineTakeEvent()
    {`
- `public override void OnSpineSignal(CharacterSpineAnimationSignal signal)
    {`
### `Assets/Script/Character/Core/States/IdleState.cs`
- `public class IdleState : MovementState
{`
- `public override string StateName => "Idle";`
- `public override float MovementSpeed => 0f;`
- `public override void Enter()
    {`
- `public override void Update(float deltaTime)
    {`
- `public override void FixedUpdate(float fixedDeltaTime)
    {`
- `public override bool CanTransitionTo(MovementState newState)
    {`
### `Assets/Script/Character/Core/States/MovementState.cs`
- `public abstract class MovementState
{`
- `public abstract string StateName {`
- `public abstract float MovementSpeed {`
- `protected MovementStateMachine stateMachine;`
- `protected CharacterDataBus dataBus;`
- `protected Transform transform;`
- `protected Rigidbody2D rigidbody;`
- `protected CharacterRoot characterRoot;`
- `public virtual void Initialize(MovementStateMachine machine, CharacterDataBus bus,Transform trans, Rigidbody2D rb,CharacterRoot root)
    {`
- `public virtual void Enter() {`
- `public virtual void Exit() {`
- `public virtual void Update(float deltaTime) {`
- `public virtual void FixedUpdate(float fixedDeltaTime) {`
- `public abstract bool CanTransitionTo(MovementState newState);`
### `Assets/Script/Character/Core/States/MovementStateMachine.cs`
- `public class MovementStateMachine : ICharacterStateMachine, IAnimationProvider
{`
- `public string StateMachineName => "MovementStateMachine";`
- `public int Priority => 10;`
- `public string CurrentStateName => currentState?.StateName ?? "None";`
- `public CharacterStatsController StatController => statController;`
- `public bool IsActive
    {`
- `public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {`
- `public bool TransitionTo<T>() where T : MovementState
    {`
- `public T GetState<T>() where T : MovementState
    {`
- `public void Enter()
    {`
- `public void Exit()
    {`
- `public void Update(float deltaTime)
    {`
- `public void FixedUpdate(float fixedDeltaTime)
    {`
- `public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {`
- `public bool CanInterrupt(ICharacterStateMachine other)
    {`
- `public bool RequestState(object stateData)
    {`
- `public void OnEnterAnimation(float deltaTime)
    {`
- `public bool IsInState<T>() where T : MovementState
    {`
- `public float GetCurrentSpeed()
    {`
### `Assets/Script/Character/Core/States/MoveState.cs`
- `public class MoveState : MovementState
{`
- `public override string StateName => "Move";`
- `public override float MovementSpeed => ResolveMoveSpeed(ResolveMovementState());`
- `public override void Enter()
    {`
- `public override void Exit()
    {`
- `public override void Update(float deltaTime)
    {`
- `public override void FixedUpdate(float fixedDeltaTime)
    {`
- `public override bool CanTransitionTo(MovementState newState)
    {`
### `Assets/Script/Character/Core/States/WeaponEquip/WeaponEquipStateMachine.cs`
- `public class WeaponEquipStateMachine : IParallelCharacterStateMachine
{`
- `public string StateMachineName => "WeaponEquipStateMachine";`
- `public int Priority => 14;`
- `public string CurrentStateName => dataBus != null
        ? dataBus.weaponEquipState.ToString()
        : "None";`
- `public bool IsActive
    {`
- `public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {`
- `public void Enter()
    {`
- `public void Update(float deltaTime)
    {`
- `public void FixedUpdate(float fixedDeltaTime)
    {`
- `public bool RequestState(object stateData)
    {`
- `public bool RequestSwitchSlot(
        WeaponSlotId slotId)
    {`
- `public bool RequestSwitchToNextSlot()
    {`
- `public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {`
- `public bool CanInterrupt(ICharacterStateMachine other)
    {`
- `public void ApplyWeaponVisibility(bool visible)
    {`
- `public void Exit()
    {`
### `Assets/Script/Character/Core/States/WeaponPosture/WeaponPostureStateMachine.cs`
- `public class WeaponPostureStateMachine : IParallelCharacterStateMachine
{`
- `public string StateMachineName => "WeaponPostureStateMachine";`
- `public int Priority => 16;`
- `public string CurrentStateName => dataBus != null
        ? dataBus.weaponPostureState.ToString()
        : "None";`
- `public bool IsActive
    {`
- `public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {`
- `public void Enter()
    {`
- `public void Exit()
    {`
- `public void Update(float deltaTime)
    {`
- `public void FixedUpdate(float fixedDeltaTime)
    {`
- `public bool RequestState(object stateData)
    {`
- `public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {`
- `public bool CanInterrupt(ICharacterStateMachine other)
    {`
### `Assets/Script/Character/Core/StateType.cs`
- No public/protected declarations detected.
### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationArbitrator.cs`
- `public class AnimationArbitrator
{`
- `public static CharacterAnimationPlan Resolve(
        EAnimationLogicState id,
        AnimationPlanMappingSO baseMap,
        List<AnimationPlanOverrideRuleSetSO> itemOverrides,
        AnimationPlanOverrideRuleSetSO statusOverrides,
        CharacterRoot root)
    {`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Character/Core/ICharacterTickable.cs`

```cs
public interface ICharacterTickable
{
    void ManagedUpdate(float deltaTime);
}
```

### `Assets/Script/Character/Core/IParallelCharacterStateMachine.cs`

```cs
using UnityEngine;
public interface IParallelCharacterStateMachine : ICharacterStateMachine
{
}
```

### `Assets/Script/Character/Core/StateArbitrator.cs`

```cs
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
```

### `Assets/Script/Character/Core/StateRequest.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateRequest
{
    /// <summary>请求类型</summary>
    public CharacterStateType characterStateType { get; protected set; }
    //protected set 意味着：
    //只有它自己或者它的子类（ CharacterStateRequest）可以修改这个值。
    //InputHandler 不属于它的子类，所以不能修改。
    //通过在子类的构造函数中赋值，就完美解决了权限和抽象类无法实例化的问题。


    /// <summary>请求优先级（可选，默认使用状态机优先级）</summary>
    public int OverridePriority { get; protected set; } = -1;

    /// <summary>请求是否紧急（紧急请求可以打断非紧急的）</summary>
    public bool IsUrgent { get; protected set; } = false;

    /// <summary>请求发起时间（用于超时处理）</summary>
    public float RequestTime { get; private set; }

    /// <summary>请求数据（具体内容由子类定义）</summary>
    public object Data { get; protected set; }

    public System.Action onActionPoint; // 回调

    public StateRequest()
    {
        RequestTime = Time.time;
    }
}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatActionState.cs`

```cs
using UnityEngine;

public abstract class HoundCombatActionState
{
    protected HoundCombatActionStateMachine stateMachine;
    protected CharacterDataBus dataBus;
    protected Transform transform;
    protected Rigidbody2D rigidbody;
    protected CharacterRoot characterRoot;

    public abstract string StateName { get; }

    public virtual void Initialize(
        HoundCombatActionStateMachine owner,
        CharacterDataBus bus,
        Transform tf,
        Rigidbody2D rb,
        CharacterRoot root)
    {
        stateMachine = owner;
        dataBus = bus;
        transform = tf;
        rigidbody = rb;
        characterRoot = root;
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public virtual void Update(float deltaTime)
    {
    }

    public virtual void FixedUpdate(float fixedDeltaTime)
    {
    }

    public virtual bool CanTransitionTo(HoundCombatActionState next)
    {
        return true;
    }

    public virtual void OnSpineSignal(CharacterSpineAnimationSignal signal)
    {
    }



}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatActionStateAnimationBridge.cs`

```cs
using UnityEngine;

public class HoundCombatActionStateAnimationBridge
{
    private readonly CharacterDataBus dataBus;
    private readonly CharacterRoot characterRoot;
    private readonly CharacterAnimationComposer animationComposer;

    private int actionReplayId;
    private float hipFirePoseExpireTime;
    //    解决视觉问题：腰射开一枪
    //-> Fire oneShot 动画还没播完
    //-> 玩家松开左键
    //-> 如果立刻回 LowReady，上半身会突然掉枪


    public HoundCombatActionStateAnimationBridge(
        CharacterDataBus dataBus,
        CharacterRoot characterRoot,
        CharacterAnimationComposer animationComposer)
    {
        this.dataBus = dataBus;
        this.characterRoot = characterRoot;
        this.animationComposer = animationComposer;
    }

    public void Tick()
    {
        //UpdateHipFirePoseLifetime();
        UpdateWeaponPoseSafety();
    }

    public void ClearAll()
    {
        StopHipFirePoseHold();
        RemoveWeaponActionIntent();
        RemoveWeaponPoseIntent();
    }

    public AnimationIntent GetWeaponPoseIntent()
    {
        if (animationComposer == null)
            return null;

        return animationComposer.GetIntent(AnimationIntentSourceStateMachine.HoundWeaponPose);
    }

    public bool ShouldKeepHipFirePose()
    {
        return Time.time < hipFirePoseExpireTime;
    }

    public void KeepHipFirePose(float duration)
    {
        hipFirePoseExpireTime = Mathf.Max(
            hipFirePoseExpireTime,
            Time.time + Mathf.Max(0.05f, duration)
        );

        SubmitHipFirePoseIntent();
    }

    public void SubmitAimPoseIntent()
    {
        if (animationComposer == null)
            return;


        //    Gun_HipFirePose / Gun_AimPose / Weapon_LowReady:
        //持续姿态
        //oneShot = false
        //已经是这个姿态时重复 Submit 没意义，还可能导致重算 / 掉帧
        if (HasWeaponPose(EAnimSemantic.Gun_AimPose))
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponPose,
            semantic = EAnimSemantic.Gun_AimPose,
            layer = AnimationLayer.UpperBody,
            priority = 55,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        });
    }

    public void SubmitHipFirePoseIntent()
    {
        if (animationComposer == null)
            return;

        if (HasWeaponPose(EAnimSemantic.Gun_HipFirePose))
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponPose,
            semantic = EAnimSemantic.Gun_HipFirePose,
            layer = AnimationLayer.UpperBody,
            priority = 54,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        });
    }

    public void SubmitFireIntent(float duration)
    {
        if (animationComposer == null)
            return;

        actionReplayId++;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponAction,
            semantic = EAnimSemantic.Gun_Fire,
            layer = AnimationLayer.UpperBody,
            priority = 60,
            exclusiveFullBody = false,
            oneShot = true,
            duration = duration,
            replayId = actionReplayId
        });
    }

    public void SubmitReloadIntent(float duration)
    {
        if (animationComposer == null)
            return;

        actionReplayId++;
        animationComposer.SubmitIntent(new AnimationIntent
        {

            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponAction,
            semantic = EAnimSemantic.Gun_Reload,
            layer = AnimationLayer.UpperBody,
            priority = 108,
            exclusiveFullBody = false,
            oneShot = true,
            duration = duration,
            replayId = actionReplayId
        });
    }

    public void SubmitMeleeIntent(EAnimSemantic semantic, float duration, int replayId)
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundAttack,
            semantic = semantic,
            layer = AnimationLayer.FullBodyAction,
            priority = 60,
            exclusiveFullBody = true,//很重要，让近战动画（必须为轨道0），拥有抑制其他动画的能力，而不需要其他轨道的动画取消intent
                                     //造成需要退出又重新进入状态所产生的GAP。
            oneShot = true,
            duration = duration,
            replayId = replayId
        });
    }

    public void RemoveWeaponPoseIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.HoundWeaponPose);
    }

    public void RemoveWeaponActionIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.HoundWeaponAction);
    }

    private void UpdateHipFirePoseLifetime()
    {
        if (animationComposer == null || dataBus == null)
            return;

        if (dataBus.aimHeld)
            return;

        AnimationIntent intent = GetWeaponPoseIntent();

        if (intent == null)
            return;

        if (intent.semantic != EAnimSemantic.Gun_HipFirePose)
            return;

        bool stillFiring =
            dataBus.houndAttackHeld ||
            dataBus.houndAttackPressedThisFrame ||
            dataBus.isHoundAttacking ||
            dataBus.currentHoundCombatAction == HoundCombatActionType.Fire;

        if (stillFiring)
            return;

        if (Time.time < hipFirePoseExpireTime)
            return;

        StopHipFirePoseHold();
        characterRoot.stateArbitrator.houndCombatActionSM.SetWeaponPoseLowReady();
    }

    private void UpdateWeaponPoseSafety()
    {
        if (animationComposer == null || dataBus == null)
            return;

        AnimationIntent intent = GetWeaponPoseIntent();

        if (intent == null)
            return;

        if (intent.semantic == EAnimSemantic.Gun_AimPose)
        {
            bool shouldKeepAim =
                dataBus.aimHeld ||
                dataBus.isAiming ||
                dataBus.currentHoundCombatAction == HoundCombatActionType.Aim;

            if (!shouldKeepAim)
                characterRoot.stateArbitrator.houndCombatActionSM.SetWeaponPoseLowReady();

            return;
        }

        if (intent.semantic == EAnimSemantic.Gun_HipFirePose)
        {
            bool shouldKeepHipFire =
                dataBus.houndAttackHeld ||
                dataBus.houndAttackPressedThisFrame ||
                dataBus.isHoundAttacking ||
                dataBus.currentHoundCombatAction == HoundCombatActionType.Fire ||
                Time.time < hipFirePoseExpireTime;

            if (!shouldKeepHipFire)
            {
                StopHipFirePoseHold();
                characterRoot.stateArbitrator.houndCombatActionSM.SetWeaponPoseLowReady();
            }
        }
    }
    private bool HasWeaponPose(EAnimSemantic semantic)
    {
        AnimationIntent intent = GetWeaponPoseIntent();
        return intent != null && intent.semantic == semantic;
    }
    public void StopHipFirePoseHold()
    {
        hipFirePoseExpireTime = 0f;
    }
}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatActionStateMachine.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;


public class HoundCombatActionStateMachine : IParallelCharacterStateMachine
{
    public enum CombatInputDecision
    {
        None,

        EnterAim,
        Reload,

        FirearmMelee,

        MeleeAttack,
        MeleeSpecial
    }


    //负责Aim / Fire / Reload / Melee
    public string StateMachineName => "HoundCombatActionStateMachine";
    public int Priority => 15;
    public string CurrentStateName => currentState != null ? currentState.StateName : "None";
    private const float WeaponFacingReleaseDelay = 0.08f;

    public bool IsActive
    {
        get
        {
            WeaponRuntimeData weapon =GetActiveWeapon();
            return dataBus != null&& dataBus.isAlive&& weapon != null&& weapon.ActionProfile != null;
        }
    }

    private Dictionary<Type, HoundCombatActionState> states;
    private HoundCombatActionState currentState;

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private Transform transform;
    private Rigidbody2D rigidbody;
    private CharacterAnimationComposer animationComposer;
    private int actionReplayId;
    private CharacterWeaponInventory inventory;

    private HoundCombatActionStateAnimationBridge animationBridge;


    public GunSystemController gunController
    {
        get
        {
            return characterRoot != null ? characterRoot.gunSystemController : null;
        }
    }

    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;
        rigidbody = rb;
        transform = tf;
        characterRoot = root;

        if (characterRoot != null)
            animationComposer = characterRoot.characterAnimationComposer;



        animationBridge = new HoundCombatActionStateAnimationBridge(dataBus,characterRoot,animationComposer);
        if (characterRoot != null && characterRoot.characterSpineEventSystem != null)
        {
            characterRoot.characterSpineEventSystem.OnSpineSignal += HandleSpineSignal;
        }
        if (characterRoot != null)
        {
            inventory = characterRoot.characterWeaponInventory;
            if (inventory == null)
            {
                inventory =characterRoot.GetComponent<CharacterWeaponInventory>();
            }
        }
        InitializeStates();
        TransitionTo<HoundCombatIdleState>();
    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, HoundCombatActionState>
        {
            [typeof(HoundCombatIdleState)] = new HoundCombatIdleState(),
            [typeof(HoundCombatAimState)] = new HoundCombatAimState(),
            [typeof(HoundCombatFireState)] = new HoundCombatFireState(),
            [typeof(HoundCombatReloadState)] = new HoundCombatReloadState(),
            [typeof(HoundCombatMeleeState)] = new HoundCombatMeleeState()
        };

        foreach (HoundCombatActionState state in states.Values)
        {
            state.Initialize(this, dataBus, transform, rigidbody, characterRoot);
        }
    }

    public void Enter()
    {
    }
    public void Exit()
    {
        ClearRuntimeFlags();
        animationBridge?.ClearAll();
    }

    public void Update(float deltaTime)
    {
        if (!IsActive)
        {
            ClearRuntimeFlags();
            animationBridge?.ClearAll();
            return;
        }

        currentState?.Update(deltaTime);
        animationBridge?.Tick();

    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        if (!IsActive)
            return;

        currentState?.FixedUpdate(fixedDeltaTime);
    }

    public bool RequestState(object stateData)
    {
        return false;
    }

    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return true;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }
    public bool TransitionTo<T>() where T : HoundCombatActionState
    {
        if (!states.TryGetValue(typeof(T), out HoundCombatActionState newState))
        {
            Debug.LogWarning($"[HoundCombatActionStateMachine] 状态不存在: {typeof(T).Name}");
            return false;
        }

        if (currentState != null && !currentState.CanTransitionTo(newState))
            return false;

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
        return true;
    }

    public bool IsCombatAvailable()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return dataBus != null
            && dataBus.isAlive
            && weapon != null
            && weapon.ActionProfile != null
            && dataBus.weaponEquipState == WeaponEquipState.Equipped
            && dataBus.canUseWeapon;


    }
    public bool IsCurrentWeaponFirearm()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return weapon != null
            && weapon.IsFirearm
            && dataBus != null
            && dataBus.weaponEquipState == WeaponEquipState.Equipped
            && weapon.FirearmActionProfile != null;
    }

    public bool IsCurrentWeaponMelee()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return weapon != null
            && weapon.IsMelee
            && dataBus != null
            && dataBus.weaponEquipState == WeaponEquipState.Equipped;
    }

    public bool CanAim()
    {
        return IsCombatAvailable() && IsCurrentWeaponFirearm();
    }

    public bool CanFireCurrentWeapon()
    {
        return dataBus != null
            && dataBus.aimHeld
            && dataBus.isAiming
            && IsCurrentWeaponFirearm()
            && IsCombatAvailable();
    }

    public bool CanReload()
    {
        return IsCombatAvailable() && IsCurrentWeaponFirearm();
    }

    public void MarkCombatIntent()
    {
        if (dataBus == null)
            return;

        dataBus.lastCombatIntentTime = Time.time;
    }

    private WeaponRuntimeData GetActiveWeapon()
    {
        if (inventory != null)
        {
            return inventory.ActiveWeapon;
        }
        /*
         * 迁移期兼容回退。
         */
        return dataBus != null ? dataBus.currentWeaponRuntimeData : null;
    }


    public CombatInputDecision EvaluateIdleInput()
    {
        if (!IsCombatAvailable())
            return CombatInputDecision.None;

        WeaponRuntimeData weapon =
            GetActiveWeapon();

        if (weapon == null)
            return CombatInputDecision.None;

        if (weapon.IsFirearm)
        {
            return EvaluateIdleFirearmInput();
        }

        if (weapon.IsMelee)
        {
            return EvaluateIdleMeleeInput();
        }

        return CombatInputDecision.None;
    }

    private CombatInputDecision
    EvaluateIdleFirearmInput()
    {
        /*
         * 换弹优先。
         */
        if (dataBus.reloadPressedThisFrame)
        {
            return CombatInputDecision.Reload;
        }
        /*
         * 右键进入 Aim。
         */
        if (dataBus.aimHeld)
        {
            return CombatInputDecision.EnterAim;
        }
        /*
         * 没有瞄准时按左键：
         * 解释为枪械近身攻击。
         */
        if (dataBus.houndAttackPressedThisFrame)
        {
            return CombatInputDecision.FirearmMelee;
        }
        return CombatInputDecision.None;
    }
    private CombatInputDecision
    EvaluateIdleMeleeInput()
    {
        /*
         * 真正近战武器左键。
         *
         * 当前先保留入口，不直接进入现有 MeleeState，
         * 因为现有 MeleeState 很可能是枪械踢击逻辑。
         */
        if (dataBus.houndAttackPressedThisFrame)
        {
            return CombatInputDecision.MeleeAttack;
        }
        /*
         * 真正近战武器右键特殊动作入口。
         */
        if (dataBus.aimPressedThisFrame)
        {
            return CombatInputDecision.MeleeSpecial;
        }
        return CombatInputDecision.None;
    }

    public void ExecuteIdleInputDecision(
    CombatInputDecision decision)
    {
        switch (decision)
        {
            case CombatInputDecision.None:
                return;

            case CombatInputDecision.EnterAim:
                MarkCombatIntent();
                TransitionTo<HoundCombatAimState>();
                return;

            case CombatInputDecision.Reload:
                TryStartReloadFromIdle();
                return;

            case CombatInputDecision.FirearmMelee:
                TryStartFirearmMeleeFromIdle();
                return;

            case CombatInputDecision.MeleeAttack:
                HandleMeleeAttackNotImplemented();
                return;

            case CombatInputDecision.MeleeSpecial:
                HandleMeleeSpecialNotImplemented();
                return;
        }
    }
    private void TryStartReloadFromIdle()
    {
        //if (!CanReload())
        //    return;

        //if (gunController == null)
        //    return;

        //if (!gunController.CanStartReload())
        //    return;

        //MarkCombatIntent();
        //TransitionTo<HoundCombatReloadState>();
        if (!CanRequestReload())
            return;

        MarkCombatIntent();
        TransitionTo<HoundCombatReloadState>();
    }
    private void TryStartFirearmMeleeFromIdle()
    {
        if (!IsCurrentWeaponFirearm())
            return;

        MarkCombatIntent();

        /*
         * 当前 HoundCombatMeleeState 暂时继续负责
         * 枪械未瞄准时的近身攻击。
         */
        TransitionTo<HoundCombatMeleeState>();
    }
    private void HandleMeleeAttackNotImplemented()
    {
        WeaponRuntimeData weapon =
            GetActiveWeapon();

        Debug.Log(
            $"[HoundCombat] MeleeAttack 尚未实现。" +
            $" Weapon={GetWeaponDebugName(weapon)}",
            characterRoot
        );
    }

    private void HandleMeleeSpecialNotImplemented()
    {
        WeaponRuntimeData weapon =
            GetActiveWeapon();

        Debug.Log(
            $"[HoundCombat] MeleeSpecial 尚未实现。" +
            $" Weapon={GetWeaponDebugName(weapon)}",
            characterRoot
        );
    }
    private string GetWeaponDebugName(
    WeaponRuntimeData weapon)
    {
        if (weapon == null)
            return "NULL";

        if (weapon.config == null)
            return "RuntimeWithoutConfig";

        return
            $"{weapon.config.weaponName}" +
            $" / Type={weapon.config.weaponType}" +
            $" / Asset={weapon.config.name}";
    }


    /// <summary>
    /// 判断当前是否能够发起换弹请求。
    /// 只用于决定是否进入 ReloadState。
    /// </summary>
    public bool CanRequestReload()
    {
        if (!CanReload())
            return false;

        if (gunController == null)
            return false;

        return gunController.CanStartReload();
    }





    #region 武器桥接
    public FirearmActionProfileSO GetFirearmProfile()
    {
        WeaponRuntimeData weapon =GetActiveWeapon();
        return weapon != null? weapon.FirearmActionProfile: null;
    }
    public WeaponActionProfileSO GetActionProfile()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return weapon != null ? weapon.ActionProfile: null;
    }

    public float GetFireDuration()
    {
        FirearmActionProfileSO profile = GetFirearmProfile();

        if (profile == null)
            return 0.15f;

        return Mathf.Max(0.05f, profile.fireAnimationDuration);
    }
    public float GetReloadDuration()
    {
        FirearmActionProfileSO profile = GetFirearmProfile();

        if (profile == null)
            return 2.6f;

        return Mathf.Max(0.1f, profile.reloadAnimationFallbackDuration);
    }

    public float GetMeleeAnimationDuration(
    EAnimSemantic semantic,
    float fallback)
    {
        fallback = Mathf.Max(0.05f, fallback);

        if (characterRoot == null ||
            characterRoot.characterAnimationConfigManager == null)
        {
            return fallback;
        }

        EAnimationLogicState logicState;

        switch (semantic)
        {
            case EAnimSemantic.Attack_Normal_1:
                logicState = EAnimationLogicState.Attack_Normal_1;
                break;

            case EAnimSemantic.Attack_Normal_2:
                logicState = EAnimationLogicState.Attack_Normal_2;
                break;

            case EAnimSemantic.Attack_Normal_3:
                logicState = EAnimationLogicState.Attack_Normal_3;
                break;

            default:
                Debug.LogWarning(
                    $"[Melee] 不支持的近战语义: {semantic}",
                    characterRoot
                );

                return fallback;
        }

        CharacterAnimationPlan plan =
            characterRoot.characterAnimationConfigManager.ResolvePlan(
                logicState
            );

        if (plan == null ||
            plan.clips == null ||
            plan.clips.Count <= 0)
        {
            Debug.LogWarning(
                $"[Melee] 没有找到动画方案: {logicState}",
                characterRoot
            );

            return fallback;
        }

        string animationName = string.Empty;

        /*
         * 优先寻找 Track 0。
         * 你的 ATK1、ATK2 当前都配置在 Track 0。
         */
        for (int i = 0; i < plan.clips.Count; i++)
        {
            CharacterAnimationClip clip = plan.clips[i];

            if (clip == null)
                continue;

            if (clip.track == 0 &&
                !string.IsNullOrEmpty(clip.animaName))
            {
                animationName = clip.animaName;
                break;
            }
        }

        /*
         * 没找到 Track 0 时，退回到方案中的第一个有效动画。
         */
        if (string.IsNullOrEmpty(animationName))
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                CharacterAnimationClip clip = plan.clips[i];

                if (clip != null &&
                    !string.IsNullOrEmpty(clip.animaName))
                {
                    animationName = clip.animaName;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning(
                $"[Melee] 动画方案中没有有效动画名: {logicState}",
                characterRoot
            );

            return fallback;
        }

        /*
         * 必须明确使用 Hound 骨架。
         *
         * 不要使用 GetComponentInChildren<SkeletonAnimation>()，
         * 因为你的角色下面同时有 Hound 和 HunterGun 骨架，
         * 它可能错误拿到枪械骨架。
         */
        Spine.Unity.SkeletonAnimation skeletonAnimation =
            characterRoot.houndSkeletonAnimation;

        if (skeletonAnimation == null ||
            skeletonAnimation.Skeleton == null ||
            skeletonAnimation.Skeleton.Data == null)
        {
            Debug.LogWarning(
                $"[Melee] Hound Skeleton 尚未初始化，使用保底时长。" +
                $" Animation={animationName}",
                characterRoot
            );

            return fallback;
        }

        Spine.Animation animation =
            skeletonAnimation.Skeleton.Data.FindAnimation(
                animationName
            );

        if (animation == null)
        {
            Debug.LogWarning(
                $"[Melee] Spine 中找不到动画: {animationName}",
                characterRoot
            );

            return fallback;
        }

        return Mathf.Max(0.05f, animation.Duration);
    }

    public bool FireCurrentWeapon()
    {
        if (!CanFireCurrentWeapon())
            return false;

        if (gunController == null)
        {
            Debug.LogWarning(
                "[HoundCombatAction] GunSystemController == null",
                characterRoot
            );

            return false;
        }

        bool fired = gunController.TryHandleFireInput(
            dataBus.houndAttackPressedThisFrame,
            dataBus.houndAttackHeld
        );

        return fired;
    }







    public void ApplyReload()
    {
        if (!IsCurrentWeaponFirearm())
            return;

        if (gunController == null)
        {
            Debug.LogWarning("[HoundCombatAction] GunSystemController == null");
            return;
        }

        if (!gunController.CanStartReload())
            return;

        gunController.CommitReload();

        WeaponRuntimeData weapon = GetActiveWeapon();

        WeaponConfigSO config = weapon != null? weapon.config : null;

        string weaponName = config != null ? config.name: "Unknown";
        Debug.Log( $"[HoundCombatAction] ApplyReload: {weaponName}",characterRoot);

        //WeaponConfigSO config = dataBus.currentWeaponRuntimeData.config;
        //string weaponName = config != null ? config.name : "Unknown";
        //Debug.Log($"[HoundCombatAction] ApplyReload: {weaponName}");
    }
    #endregion

    #region 动画意图桥接
    public AnimationIntent GetWeaponPoseIntent()
    {
        return animationBridge != null ? animationBridge.GetWeaponPoseIntent() : null;
    }

    public bool ShouldKeepHipFirePose()
    {
        return animationBridge != null && animationBridge.ShouldKeepHipFirePose();
    }

    public void KeepHipFirePose(float duration)
    {
        LockMoveFacingByWeapon();
        animationBridge?.KeepHipFirePose(duration);
    }

    public void SubmitAimPoseIntent()
    {
        LockMoveFacingByWeapon();
        animationBridge?.SubmitAimPoseIntent();
    }

    public void SubmitHipFirePoseIntent()
    {
        LockMoveFacingByWeapon();
        animationBridge?.SubmitHipFirePoseIntent();
    }

    public void SubmitFireIntent()
    {
        LockMoveFacingByWeapon();
        animationBridge?.SubmitFireIntent(GetFireDuration());
    }

    public void SubmitReloadIntent(float duration)
    {

        LockMoveFacingByWeapon();
        animationBridge?.SubmitReloadIntent(duration);
    }

    public void SubmitMeleeIntent(EAnimSemantic semantic, float duration, int replayId)
    {
        animationBridge?.SubmitMeleeIntent(semantic, duration, replayId);
    }

    public void RemoveWeaponPoseIntent()
    {
        animationBridge?.RemoveWeaponPoseIntent();
    }
    public void SubmitLowReadyPoseIntent()
    {
        if (characterRoot == null || animationComposer == null)
        {
            return;
        }
        AnimationIntent current = GetWeaponPoseIntent();

        if (current != null && current.semantic == EAnimSemantic.Weapon_LowReady)
            return;

        AnimationIntent intent = new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponPose,
            semantic = EAnimSemantic.Weapon_LowReady,
            layer = AnimationLayer.UpperBody,
            priority = 100,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        };

        characterRoot.characterAnimationComposer.SubmitIntent(intent);

    }
    /// <summary>
    /// 松开右键 -> SetWeaponPoseLowReady
    //  松开左键 -> SetWeaponPoseLowReady
    //  腰射自然结束 -> SetWeaponPoseLowReady
    /// </summary>
    public void SetWeaponPoseLowReady()
    {
        animationBridge?.StopHipFirePoseHold();
        SubmitLowReadyPoseIntent();
    }
    /// <summary>
    /// 长按 R 卸武器 -> HardClearWeaponAnimation
    //  死亡/缴械/切无武器 -> HardClearWeaponAnimation
    /// </summary>
    public void HardClearWeaponAnimation()
    {
        /*
         * 停止腰射姿态计时。
         */
        animationBridge?.StopHipFirePoseHold();

        /*
         * 清除枪械动作、枪械 Pose 和近身攻击意图。
         */
        RemoveWeaponActionIntent();
        RemoveWeaponPoseIntent();
        RemoveMeleeIntent();

        if (animationComposer != null)
        {
            animationComposer.RemoveIntent(
                AnimationIntentSourceStateMachine
                    .WeaponPosture
            );

            animationComposer.ClearTracks(1, 2);
        }

        /*
         * 清理运行时战斗状态。
         */
        ClearRuntimeFlags();

        /*
         * 武器切换属于强制中断。
         * 不经过旧状态的普通 Exit 结算。
         */
        ForceTransitionToIdle();
    }

    /// <summary>
    /// 武器收起、切槽、死亡或缴械时，
    /// 不经过普通 CanTransitionTo 检查，直接重置到 Idle。
    ///
    /// 这里故意不调用旧状态的 Exit()。
    /// 因为 ReloadState.Exit()、MeleeState.Exit() 等以后可能包含
    /// 动作结算逻辑，强制中断时不应该意外提交换弹或伤害。
    /// </summary>
    private void ForceTransitionToIdle()
    {
        if (states == null)
            return;

        if (!states.TryGetValue(
                typeof(HoundCombatIdleState),
                out HoundCombatActionState idleState))
        {
            Debug.LogWarning(
                "[HoundCombat] 强制重置失败：找不到 IdleState。",
                characterRoot
            );

            return;
        }
        /*
         * HardClearWeaponAnimation 已经负责清除：
         * 动画意图、姿态意图和 DataBus 标记。
         *
         * 所以这里直接替换状态，不调用旧状态 Exit。
         */
        currentState = idleState;
        currentState.Enter();
    }

    public string ResolveReloadAnimationName()
    {
        if (characterRoot == null || characterRoot.characterAnimationConfigManager == null)
            return string.Empty;

        CharacterAnimationPlan plan =
            characterRoot.characterAnimationConfigManager.ResolvePlan(EAnimationLogicState.Reload);

        if (plan == null || plan.clips == null || plan.clips.Count <= 0)
            return string.Empty;

        for (int i = 0; i < plan.clips.Count; i++)
        {
            CharacterAnimationClip clip = plan.clips[i];

            if (clip == null)
                continue;

            if (clip.track == 1 && !string.IsNullOrEmpty(clip.animaName))
                return clip.animaName;
        }

        for (int i = 0; i < plan.clips.Count; i++)
        {
            CharacterAnimationClip clip = plan.clips[i];

            if (clip != null && !string.IsNullOrEmpty(clip.animaName))
                return clip.animaName;
        }

        return string.Empty;
    }

    public float GetAnimationDurationOrFallback(string animationName, float fallback)
    {
        if (string.IsNullOrEmpty(animationName))
            return Mathf.Max(0f, fallback);

        //Spine.Unity.SkeletonAnimation skeletonAnimation =
        //    characterRoot.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();

        Spine.Unity.SkeletonAnimation skeletonAnimation = characterRoot != null ? characterRoot.houndSkeletonAnimation: null;

        if (skeletonAnimation == null ||skeletonAnimation.Skeleton == null ||skeletonAnimation.Skeleton.Data == null)
        {
            return Mathf.Max(0f, fallback);
        }

        Spine.Animation animation = skeletonAnimation.Skeleton.Data.FindAnimation(animationName);

        if (animation == null)
        {
            Debug.LogWarning($"[Reload] Animation not found: {animationName}", characterRoot);
            return Mathf.Max(0f, fallback);
        }

        return Mathf.Max(0f, animation.Duration);
    }

    public void StopFirePoseHold()
    {
        animationBridge?.StopHipFirePoseHold();
    }

    public void RemoveWeaponActionIntent()
    {
        animationBridge?.RemoveWeaponActionIntent();
    }


    public void RemoveMeleeIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(
            AnimationIntentSourceStateMachine.HoundAttack
        );
    }






    #endregion

    #region 生命周期/安全兜底
    public void ClearRuntimeFlags()
    {
        if (dataBus == null)
            return;
        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.isHoundAttacking = false;
        dataBus.currentHoundCombatAction = HoundCombatActionType.None;
    }


    private void LockMoveFacingByWeapon()
    {
        if (characterRoot == null || characterRoot.orientationSystem == null)
            return;

        characterRoot.orientationSystem.NotifyWeaponFacingActivity(WeaponFacingReleaseDelay);

    }
    #endregion

    #region 动画回调

    private void HandleSpineSignal(CharacterSpineAnimationSignal signal)
    {
        if (!IsActive)
            return;

        currentState?.OnSpineSignal(signal);
    }



    #endregion










}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatAimState.cs`

```cs
using UnityEngine;
using static CharacterDataBus;

public class HoundCombatAimState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Aim";

    public override void Enter()
    {
        dataBus.isAiming = true;
        dataBus.isHoundAttacking = false;
        dataBus.isReloading = false;
        dataBus.currentHoundCombatAction = HoundCombatActionType.Aim;

        stateMachine.MarkCombatIntent();
        stateMachine.SubmitAimPoseIntent();
    }

    public override void Update(float deltaTime)
    {
        /*
    * 枪械、装备状态或角色状态已经不允许瞄准。
    */
        if (!stateMachine.CanAim())
        {
            ExitAimToIdle();
            return;
        }

        /*
         * 右键松开，立即退出瞄准并放下枪。
         */
        if (!dataBus.aimHeld)
        {
            ExitAimToIdle();
            return;
        }

        /*
         * 当前仍然处于有效瞄准状态。
         */
        dataBus.isAiming = true;
        dataBus.isHoundAttacking = false;
        dataBus.isReloading = false;

        dataBus.currentHoundCombatAction = HoundCombatActionType.Aim;

        stateMachine.MarkCombatIntent();
        stateMachine.SubmitAimPoseIntent();
        /*
         * 瞄准状态下允许换弹。
         */
        if (dataBus.reloadPressedThisFrame && stateMachine.CanRequestReload())
        {
            stateMachine.MarkCombatIntent();
            stateMachine.TransitionTo<HoundCombatReloadState>();
            return;
        }

        /*
         * 只有左键的新按下事件才能从 Aim 进入 Fire。
         *
         * 不使用 Held 作为入口，
         * 避免先按住左键、再按右键时自动开枪。
         */
        if (dataBus.houndAttackPressedThisFrame)
        {
            stateMachine.MarkCombatIntent();
            stateMachine.TransitionTo<HoundCombatFireState>();
            return;
        }
    }

    public override void Exit()
    {
        /*
         * Aim -> Fire 时，这里会先执行。
         * FireState.Enter() 随后会重新设置 isAiming = true。
         */
        if (dataBus.currentHoundCombatAction ==
            HoundCombatActionType.Aim)
        {
            dataBus.currentHoundCombatAction =
                HoundCombatActionType.None;
        }

        dataBus.isAiming = false;
    }

    private void ExitAimToIdle()
    {
        dataBus.isAiming = false;
        dataBus.isHoundAttacking = false;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;

        /*
         * 先把枪械姿态切换为 LowReady，
         * 再进入 Idle。
         *
         * 不把放下枪完全交给 IdleState，
         * 避免旧 Aim Pose 因状态/动画去重而残留。
         */
        stateMachine.RemoveWeaponActionIntent();
        stateMachine.SubmitLowReadyPoseIntent();

        stateMachine.TransitionTo<HoundCombatIdleState>();
    }
}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatFireState.cs`

```cs
using UnityEngine;
using static CharacterDataBus;

//只要左键还在请求开火：
//1. 判断当前 FireStance
//2. 请求对应姿势
//3. 调 GunSystemController 尝试真实开火
//4. 如果真实 fired，提交 Fire 动画
//5. 如果不再开火，根据 aimHeld 返回 Aim 或 Idle
public class HoundCombatFireState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Fire";

    private float firePoseTimer;

    public override void Enter()
    {
        /*
         * FireState 只接受有效瞄准状态。
         *
         * 正常情况下，只有 AimState 能进入这里。
         * 这里仍然做一次安全检查，防止未来其他状态误调用。
         */
        if (!dataBus.aimHeld ||
            !stateMachine.CanAim() ||
            !stateMachine.IsCurrentWeaponFirearm())
        {
            ExitFireToLowReady();
            return;
        }

        dataBus.currentHoundCombatAction =
            HoundCombatActionType.Fire;

        dataBus.isHoundAttacking = true;
        dataBus.isAiming = true;
        dataBus.isReloading = false;

        firePoseTimer = 0f;

        stateMachine.MarkCombatIntent();
        stateMachine.SubmitAimPoseIntent();

        /*
         * 第一枪必须在 Enter 中处理。
         *
         * AimState 是在 houndAttackPressedThisFrame 为 true 时
         * 切换到 FireState 的。
         *
         * 如果等下一帧 FireState.Update 才调用开火，
         * InputHandler.LateUpdate 已经会清除 PressedThisFrame，
         * 半自动武器可能收不到第一次点击。
         */
        TryFireCurrentFrame();
    }

    public override void Update(float deltaTime)
    {
        if (!stateMachine.IsCombatAvailable())
        {
            ExitFireToLowReady();
            return;
        }

        /*
         * 松开右键后，无论左键是否还在按住，
         * 都必须立即停止枪械开火。
         *
         * 不允许自动退化成腰射。
         */
        if (!dataBus.aimHeld ||
            !stateMachine.CanAim() ||
            !stateMachine.IsCurrentWeaponFirearm())
        {
            ExitFireToLowReady();
            return;
        }

        dataBus.currentHoundCombatAction =
            HoundCombatActionType.Fire;

        dataBus.isHoundAttacking = true;
        dataBus.isAiming = true;
        dataBus.isReloading = false;

        stateMachine.MarkCombatIntent();
        stateMachine.SubmitAimPoseIntent();

        /*
         * 瞄准开火期间请求换弹。
         */
        if (dataBus.reloadPressedThisFrame && stateMachine.CanRequestReload())
        {
            InterruptFireToReload();
            return;
        }

        /*
         * 已经合法进入 FireState 后，
         * Held 可以交给 GunSystemController：
         *
         * SemiAuto：通常只响应 PressedThisFrame
         * FullAuto：通常响应 Held
         * Burst：由枪械系统管理 burst
         */
        if (dataBus.houndAttackHeld)
        {
            TryFireCurrentFrame();
            return;
        }

        /*
         * 左键已经松开。
         *
         * 等 Fire 动画的短暂表现时间结束后回到 Aim，
         * 只要右键仍按住就继续保持瞄准。
         */
        firePoseTimer -= deltaTime;

        if (firePoseTimer > 0f)
            return;

        stateMachine.TransitionTo<HoundCombatAimState>();
    }

    public override void Exit()
    {
        dataBus.isHoundAttacking = false;

        if (dataBus.currentHoundCombatAction ==
            HoundCombatActionType.Fire)
        {
            dataBus.currentHoundCombatAction =
                HoundCombatActionType.None;
        }

        /*
         * 不在这里无条件将 isAiming 设为 false。
         *
         * 因为 Fire -> Aim 时，角色仍然处于瞄准状态；
         * 具体值由目标状态的 Enter() 决定。
         */
    }

    private void TryFireCurrentFrame()
    {
        bool fired = stateMachine.FireCurrentWeapon();

        if (!fired)
            return;

        stateMachine.SubmitFireIntent();
        firePoseTimer = stateMachine.GetFireDuration();
    }

    private void ExitFireToLowReady()
    {
        dataBus.isHoundAttacking = false;
        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;

        /*
         * 清掉 Fire 动作意图，并明确提交 LowReady。
         */
        stateMachine.RemoveWeaponActionIntent();
        stateMachine.SubmitLowReadyPoseIntent();

        stateMachine.TransitionTo<HoundCombatIdleState>();
    }

    private void InterruptFireToReload()
    {
        dataBus.isHoundAttacking = false;
        dataBus.isAiming = dataBus.aimHeld;
        dataBus.isReloading = true;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.Reload;

        stateMachine.RemoveWeaponActionIntent();
        stateMachine.TransitionTo<HoundCombatReloadState>();
    }
}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatIdleState.cs`

```cs
using UnityEngine;
using static CharacterDataBus;

public class HoundCombatIdleState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Idle";

    public override void Enter()
    {
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;

        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.isHoundAttacking = false;

        stateMachine.RemoveWeaponActionIntent();

        AnimationIntent poseIntent =
            stateMachine.GetWeaponPoseIntent();

        if (poseIntent == null)
            return;

        /*
         * Idle 下不允许继续保持瞄准 Pose。
         */
        if (poseIntent.semantic == EAnimSemantic.Gun_AimPose &&
            !dataBus.aimHeld)
        {
            stateMachine.SubmitLowReadyPoseIntent();
            return;
        }

        /*
         * 兼容旧腰射意图残留。
         * 新代码不会再提交 Gun_HipFirePose。
         */
        if (poseIntent.semantic == EAnimSemantic.Gun_HipFirePose)
        {
            stateMachine.SubmitLowReadyPoseIntent();
        }
    }

    public override void Update(float deltaTime)
    {
        HoundCombatActionStateMachine.CombatInputDecision decision = stateMachine.EvaluateIdleInput();
        stateMachine.ExecuteIdleInputDecision(decision);







        ///*
        // * 未处于 AimState 时，新按下左键就是近战。
        // *
        // * 这里未来应该调用近战槽，
        // * 不检查当前枪械槽的武器类型。
        // */
        //if (dataBus.houndAttackPressedThisFrame)
        //{
        //    stateMachine.MarkCombatIntent();
        //    stateMachine.TransitionTo<HoundCombatMeleeState>();
        //    return;
        //}

        ///*
        // * 没有近战输入时，右键进入 AimState。
        // */
        //if (dataBus.aimHeld && stateMachine.CanAim())
        //{
        //    stateMachine.MarkCombatIntent();
        //    stateMachine.TransitionTo<HoundCombatAimState>();
        //    return;
        //}

        //if (dataBus.reloadPressedThisFrame &&
        //    stateMachine.CanReload())
        //{
        //    stateMachine.MarkCombatIntent();
        //    stateMachine.TransitionTo<HoundCombatReloadState>();
        //    return;
        //}
    }

}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatMeleeState.cs`

```cs
using UnityEngine;
using static CharacterDataBus;

public class HoundCombatMeleeState : HoundCombatActionState
{
    public override string StateName =>"HoundCombat_FirearmMelee";

    /*
     * ��ǰǹе��������ģ��ʹ������������
     *
     * �Ժ���Դ�ǹе���������ж�ȡ�����磺
     *
     * int comboCount =
     *     firearmActionProfile.firearmMeleeComboCount;
     */
    private const int SimulatedComboCount = 3;

    /*
     * ����޷���ȡ�� Spine ������ʵʱ����
     * ��ʹ������ı��׳���ʱ�䡣
     */
    private const float AttackDurationFallback = 1f;

    /*
     * һ�ι���������ɺ�����ж೤ʱ�����������һ�Ρ�
     *
     * ���磺
     * ATK1 ������ 0.35 �����ٴΰ���������Բ��� ATK2��
     */
    private const float ComboInputWindowDuration = 0.35f;

    private bool isAttacking;
    private bool isWaitingForNextAttack;
    private bool bufferedNextAttack;

    /*
     * ʹ�ô� 0 ��ʼ��������
     *
     * 0 = Attack_Normal_1
     * 1 = Attack_Normal_2
     */
    private int comboIndex = -1;

    /*
     * ÿ�β��Ž�ս������������
     * Composer/AnimationHandleSystem ���� replayId �ж��Ƿ���Ҫ�ز���
     */
    private int replayId;

    private float attackTimer;
    private float comboInputWindowTimer;

    public override void Enter()
    {
        /*
          * ��ǰ���״ֻ̬����
          *
          * ǹеδ��׼ʱ�Ľ���������
          *
          * �����Ľ�ս������ʱ���ܽ������״̬��
          */
        if (!stateMachine.IsCurrentWeaponFirearm())
        {
            //Debug.LogWarning(
            //    "[FirearmMelee] ��ǰ�������ǿ���ǹе��" +
            //    "�ܾ�����ǹе��������״̬��",
            //    characterRoot
            //);

            stateMachine.TransitionTo<
                HoundCombatIdleState>();

            return;
        }

        dataBus.currentHoundCombatAction =
            HoundCombatActionType.FirearmMelee;

        dataBus.isHoundAttacking = true;
        dataBus.isAiming = false;
        dataBus.isReloading = false;

        isAttacking = false;
        isWaitingForNextAttack = false;
        bufferedNextAttack = false;

        comboIndex = -1;
        attackTimer = 0f;
        comboInputWindowTimer = 0f;

        stateMachine.MarkCombatIntent();

        /*
         * ǹе��������ʹ��ȫ��������
         *
         * ��ʱ�Ƴ�ǹе������ǹе��̬��
         * ���� Weapon_LowReady / Aim ����ȫ������������
         */
        stateMachine.RemoveWeaponActionIntent();
        stateMachine.RemoveWeaponPoseIntent();

        /*
         * ��һ������Ѿ��� IdleState ���ѣ�
         * ��˽���״̬���������ŵ�һ�Ρ�
         */
        StartAttack(0);
    }

    public override void Exit()
    {
        dataBus.isHoundAttacking = false;

        if (dataBus.currentHoundCombatAction ==
            HoundCombatActionType.FirearmMelee)
        {
            dataBus.currentHoundCombatAction =
                HoundCombatActionType.None;
        }

        isAttacking = false;
        isWaitingForNextAttack = false;
        bufferedNextAttack = false;

        attackTimer = 0f;
        comboInputWindowTimer = 0f;

        /*
         * �����Ϊ�쳣���жϻ�״̬�л���ǰ�˳���
         * ��ʽɾ��ǹе����������ͼ��
         */
        stateMachine.RemoveMeleeIntent();

        /*
         * û�м�����׼ʱ����ǹе�ָ� LowReady��
         *
         * ����Ҽ���Ȼ��ס��Ŀ�� AimState.Enter()
         * ���ύ Aim Pose�����ﲻ�ظ��ύ LowReady��
         */
        if (!dataBus.aimHeld &&
            stateMachine.IsCombatAvailable() &&
            stateMachine.IsCurrentWeaponFirearm())
        {
            stateMachine.SubmitLowReadyPoseIntent();
        }
    }

    public override void Update(float deltaTime)
    {
        /*
         * ��ǰ״ֻ̬������ǹе����������
         *
         * ���������вۡ���������ֹʹ��������
         * ��ǰ����������ǹеʱ�������˳���
         */
        if (!stateMachine.IsCombatAvailable() ||
            !stateMachine.IsCurrentWeaponFirearm())
        {
            stateMachine.RemoveMeleeIntent();

            stateMachine.TransitionTo<
                HoundCombatIdleState>();

            return;
        }

        if (isAttacking)
        {
            UpdateCurrentAttack(deltaTime);
            return;
        }

        if (isWaitingForNextAttack)
        {
            UpdateComboInputWindow(deltaTime);
            return;
        }

        FinishCombo();
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        /*
         * ������Ҫ��ս�ڼ����ƶ���ͻ����λ��ʱ��
         * ����������ʵ�֡�
         */
    }

    /// <summary>
    /// ���µ�ǰ���ڲ��ŵĹ����Ρ�
    /// </summary>
    private void UpdateCurrentAttack(float deltaTime)
    {
        /*
         * ��ǰ�����λ�����һ��ʱ��
         * �Ž����������档
         *
         * ʹ�� PressedThisFrame�������� Held��
         * ��ֹ��������Զ������������й����Ρ�
         */
        if (dataBus.houndAttackPressedThisFrame &&
            HasNextComboStage())
        {
            bufferedNextAttack = true;

            //Debug.Log($"[Melee] �ѻ�����һ�ι�����" + $" CurrentStage={comboIndex + 1}",characterRoot);
        }

        attackTimer -= deltaTime;

        if (attackTimer > 0f)
            return;

        CompleteCurrentAttack();
    }

    /// <summary>
    /// ��ǰһ�ζ�����ʱ������
    /// </summary>
    private void CompleteCurrentAttack()
    {
        isAttacking = false;

        /*
         * ����ڵ�ǰ�������������ڼ䣬
         * �Ѿ����¹���һ�ι�����
         *
         * ��ǰ������ɺ���������һ�Ρ�
         */
        if (bufferedNextAttack && HasNextComboStage())
        {
            bufferedNextAttack = false;
            StartAttack(comboIndex + 1);
            return;
        }

        bufferedNextAttack = false;

        /*
         * ��ǰ�Ѿ������һ�Σ�ֱ�ӽ���������
         */
        if (!HasNextComboStage())
        {
            FinishCombo();
            return;
        }

        /*
         * ��ǰ��֮������һ�Σ��������δ��ǰ���롣
         * ��ʼ�ȴ�������������������롣
         */
        isWaitingForNextAttack = true;
        comboInputWindowTimer = ComboInputWindowDuration;

        //Debug.Log($"[Melee] �� {comboIndex + 1} �ν�����" +$"�ȴ���һ�����룬����={ComboInputWindowDuration:F2}s",characterRoot);
    }

    /// <summary>
    /// ��һ�β��Ž�������������봰�ڡ�
    /// </summary>
    private void UpdateComboInputWindow(float deltaTime)
    {
        /*
         * ���������°��������������һ�Ρ�
         */
        if (dataBus.houndAttackPressedThisFrame &&
            HasNextComboStage())
        {
            isWaitingForNextAttack = false;
            comboInputWindowTimer = 0f;

            StartAttack(comboIndex + 1);
            return;
        }

        /*
         * ��һ�ν�����������תΪ��ס��׼��
         * ����ֱ�ӽ�����ս��������׼��
         *
         * ����жϷ���ǰ�棬���ͬһ֡ͬʱ����
         * ��ս�����������׼����ʱ�����ȼ���������
         */
        if (dataBus.aimHeld && stateMachine.CanAim())
        {
            isWaitingForNextAttack = false;
            stateMachine.TransitionTo<HoundCombatAimState>();
            return;
        }

        comboInputWindowTimer -= deltaTime;

        if (comboInputWindowTimer > 0f)
            return;

        //Debug.Log(
        //    $"[Melee] �������봰�ڽ��������ն���={comboIndex + 1}",
        //    characterRoot
        //);

        FinishCombo();
    }

    /// <summary>
    /// ����ָ�������Ĺ����Ρ�
    /// </summary>
    private void StartAttack(int newComboIndex)
    {
        int comboCount = GetComboCount();

        if (comboCount <= 0)
        {
            //Debug.LogWarning(
            //    "[Melee] ComboCount <= 0���޷����Ž�ս������",
            //    characterRoot
            //);

            FinishCombo();
            return;
        }

        if (newComboIndex < 0 || newComboIndex >= comboCount)
        {
            //Debug.LogWarning(
            //    $"[Melee] �������������������Χ��" +
            //    $" Index={newComboIndex}, ComboCount={comboCount}",
            //    characterRoot
            //);

            FinishCombo();
            return;
        }

        comboIndex = newComboIndex;

        isAttacking = true;
        isWaitingForNextAttack = false;
        bufferedNextAttack = false;

        EAnimSemantic semantic =
            ComboIndexToSemantic(comboIndex);

        /*
         * ���ȶ�ȡ Spine �ж�Ӧ��������ʵʱ����
         * ���������ӳ��ȱʧ����ʹ�� 0.5 �뱣��ֵ��
         */
        attackTimer =
            stateMachine.GetMeleeAnimationDuration(
                semantic,
                AttackDurationFallback
            );

        attackTimer = Mathf.Max(0.05f, attackTimer);

        replayId++;

        stateMachine.MarkCombatIntent();

        /*
         * �����ύ��ս������ͼ��
         *
         * ���ﲻ�ܵ��� FireCurrentWeapon()��
         */
        stateMachine.SubmitMeleeIntent(
            semantic,
            attackTimer,
            replayId
        );

        //Debug.Log(
        //    $"[Melee] ���ŵ� {comboIndex + 1}/{comboCount} �Ρ�" +
        //    $" Semantic={semantic}," +
        //    $" Duration={attackTimer:F3}," +
        //    $" ReplayId={replayId}",
        //    characterRoot
        //);
    }

    private void FinishCombo()
    {
        isAttacking = false;
        isWaitingForNextAttack = false;
        bufferedNextAttack = false;

        /*
         * ��ս����ʱ����Ҽ��԰�ס��
         * ������׼���������Զ���ǹ��
         */
        if (dataBus.aimHeld && stateMachine.CanAim())
        {
            stateMachine.TransitionTo<HoundCombatAimState>();
            return;
        }

        stateMachine.TransitionTo<HoundCombatIdleState>();
    }

    private bool HasNextComboStage()
    {
        return comboIndex + 1 < GetComboCount();
    }

    private int GetComboCount()
    {
     //* ��ǰǹе����������ʱ�̶�Ϊ���Ρ�
     //*
     //* Clamp �� 1~3������ΪĿǰֻ�У�
     //* Attack_Normal_1 / 2 / 3��
     //*/
        return Mathf.Clamp(SimulatedComboCount, 1, 3);
    }

    private EAnimSemantic ComboIndexToSemantic(int index)
    {
        switch (index)
        {
            case 0:
                return EAnimSemantic.Attack_Normal_1;

            case 1:
                return EAnimSemantic.Attack_Normal_2;

            case 2:
                return EAnimSemantic.Attack_Normal_3;

            default:
                Debug.LogWarning(
                    $"[Melee] δ�������������: {index}�����˵���һ�Ρ�",
                    characterRoot
                );

                return EAnimSemantic.Attack_Normal_1;
        }
    }
}
```

### `Assets/Script/Character/Core/States/Attack/HoundCombatReloadState.cs`

```cs
using UnityEngine;
using static CharacterDataBus;

public class HoundCombatReloadState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Reload";
    private bool reloadApplied;
    private bool reloadAnimationFinished;
 
 

    private const string ReloadMainAnimationName = "Weapon/1_Reload";



    private string currentReloadAnimationName;
    private float reloadTimeoutTimer;

    private const float ReloadFallbackExtraTime = 0.2f;
    private const float MinReloadFallbackTimeout = 0.5f;






    public override void Enter()
    {
        if (!stateMachine.IsCurrentWeaponFirearm())
        {
            stateMachine.TransitionTo<
                HoundCombatIdleState>();

            return;
        }


        if (!stateMachine.CanReload())
        {
            if (dataBus.aimHeld && stateMachine.CanAim())
                stateMachine.TransitionTo<HoundCombatAimState>();
            else
                stateMachine.TransitionTo<HoundCombatIdleState>();
            return;
        }

        currentReloadAnimationName = stateMachine.ResolveReloadAnimationName();

        float reloadAnimLength = stateMachine.GetAnimationDurationOrFallback(
            currentReloadAnimationName,
            stateMachine.GetReloadDuration()
        );

        reloadTimeoutTimer = Mathf.Max(
            MinReloadFallbackTimeout,
            reloadAnimLength + ReloadFallbackExtraTime
        );

        stateMachine.SubmitReloadIntent(reloadTimeoutTimer);



        dataBus.currentHoundCombatAction = HoundCombatActionType.Reload;
        dataBus.isReloading = true;
        dataBus.isAiming = false;
        dataBus.isHoundAttacking = false;
        reloadApplied = false;
        reloadAnimationFinished = false;
        reloadTimeoutTimer = Mathf.Max(0.5f, stateMachine.GetReloadDuration() + 0.2f);
        stateMachine.MarkCombatIntent();
  
        stateMachine.gunController.BeginReload(); // 新增：播放换弹表现 / 发音频 / 进动画

        characterRoot.gunSystemController?.SetHandIKLocked(
            rightLocked: true,
            leftLocked: false
        );
    }

    public override void Exit()
    {
        characterRoot.gunSystemController?.SetLeftHandIKLocked(true);
        dataBus.isReloading = false;
        reloadApplied= false;
    }

    public override void Update(float deltaTime)
    {
        if (!stateMachine.IsCombatAvailable())
        {
            FinishReloadState();
            return;
        }

        reloadTimeoutTimer -= deltaTime;

        if (reloadAnimationFinished)
        {
            if (!reloadApplied)
            {
                Debug.LogWarning("[Reload] Ignored reload complete before reload_commit event.");
                reloadAnimationFinished = false;
                return;
            }

            FinishReloadState();
            return;

        }

        if (reloadTimeoutTimer <= 0f)
        {
            if (!reloadApplied)
            {
                Debug.LogWarning("[Reload] Reload timeout ended before reload_commit event. Applying fallback reload.");
                ApplyReloadOnce();
            }

            FinishReloadState();
        }

    }

    private void ApplyReloadOnce()
    {
        if (reloadApplied)
            return;

        reloadApplied = true;
        stateMachine.ApplyReload();
    }

    private void FinishReloadState()
    {
        stateMachine.gunController.EndReload();

        if (dataBus.aimHeld && stateMachine.CanAim())
        {
            stateMachine.SubmitAimPoseIntent();

            // 关键：提交接续姿态后，立刻移除 Reload action。
            // 这样 Track 1 会从 Reload 直接落到 Aim，而不是等 oneShot 过期。
            stateMachine.RemoveWeaponActionIntent();

            stateMachine.TransitionTo<HoundCombatAimState>();
        }
        else
        {
            stateMachine.SubmitLowReadyPoseIntent();

            // 关键：同上，直接释放 Reload 对 Track 1 的高优先级占用。
            stateMachine.RemoveWeaponActionIntent();

            stateMachine.TransitionTo<HoundCombatIdleState>();
        }
    }




    public void OnReloadCommitEvent()
    {
        ApplyReloadOnce();
    }



    public void OnReloadMagazineFallEvent()
    {

        Debug.Log("弹夹掉落");
    }
    public void OnReloadMagazineTakeEvent()
    {

        Debug.Log("弹夹拿取");
    }


    /// <summary>
    /// //////
    /// </summary>
    /// <param name="signal"></param>

    public override void OnSpineSignal(CharacterSpineAnimationSignal signal)
    {
        //Debug.Log(
        //    $"[ReloadState SpineSignal] type={signal.type}, " +
        //    $"anim={signal.animationName}, event={signal.eventName}, " +
        //    $"track={signal.trackIndex}, time={signal.trackTime}"
        //);

        if (signal.type == CharacterSpineSignalType.EventReached)
        {
            if (!IsCurrentReloadAnimation(signal.animationName))
                return;

            HandleReloadSpineEvent(signal.eventName);
            return;
        }

        if (signal.type == CharacterSpineSignalType.AnimationCompleted)
        {
            if (!IsCurrentReloadAnimation(signal.animationName))
                return;

            reloadAnimationFinished = true;
        }
    }

    private void HandleReloadSpineEvent(string eventName)
    {
        switch (eventName)
        {
            case "Reload/Reload_magazineFall":
                OnReloadMagazineFallEvent();
                break;

            case "Reload/Reload_magazineTake":
                OnReloadMagazineTakeEvent();
                break;

            case "Reload/Reload_Commit":
                OnReloadCommitEvent();
                break;
        }
    }

    private bool IsCurrentReloadAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
            return false;

        if (string.IsNullOrEmpty(currentReloadAnimationName))
            return false;

        return animationName == currentReloadAnimationName;
    }

}
```

### `Assets/Script/Character/Core/States/IdleState.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleState : MovementState
{
    public override string StateName => "Idle";
    public override float MovementSpeed => 0f;

    private float idleTime = 0f;

    public override void Enter()
    {
        idleTime = 0f;
        dataBus.currentMoveSpeed = 0f;
        dataBus.currentMovementState = EState.Idle;

        // 只清横向速度，保留纵向（避免破坏跳跃 Y 速度）
        if (rigidbody != null)
            rigidbody.velocity = new Vector2(0f, rigidbody.velocity.y);

    }

    public override void Update(float deltaTime)
    {

        idleTime += deltaTime;
        //================�ڲ�С״̬ת��=============
        dataBus.currentMovementState = EState.Idle;
        //=============��״̬ת��===================
        if (dataBus.moveInputAmount > 0.1f) // ������
        {
            //Debug.Log($"Idle��⵽����: {dataBus.moveInput}, �л���Walk");
            stateMachine.TransitionTo<MoveState>();

            return; // ��Ҫ��ת���������˳�
        }
        // ����������ÿ5�벥��һ�κ�������
        if (idleTime >= 5f)
        {
            // PlayIdleBreathAnimation();
            idleTime = 0f;
        }
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        // Idle״̬ȷ���ٶ�Ϊ0
        if (rigidbody != null)
        {
            rigidbody.velocity = new Vector2(0, rigidbody.velocity.y);
        }
    }

    public override bool CanTransitionTo(MovementState newState)
    {
        // ���Դ�վ���л������߻���
        return newState is MoveState;
    }
}
```

### `Assets/Script/Character/Core/States/MovementState.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有移动状态的基类
/// </summary>
public abstract class MovementState
{
    // 状态基本信息
    public abstract string StateName { get; }
    public abstract float MovementSpeed { get; }

    // 状态机引用
    protected MovementStateMachine stateMachine;
    protected CharacterDataBus dataBus;
    protected Transform transform;
    protected Rigidbody2D rigidbody;
    protected CharacterRoot characterRoot;



    //子状态机的初始化会被状态机调用
    public virtual void Initialize(MovementStateMachine machine, CharacterDataBus bus,Transform trans, Rigidbody2D rb,CharacterRoot root)
    {
        stateMachine = machine;
        dataBus = bus;
        transform = trans;
        rigidbody = rb;
        characterRoot = root;
    }

    // 状态生命周期
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update(float deltaTime) { }
    public virtual void FixedUpdate(float fixedDeltaTime) { }

    // 状态转换检查
    public abstract bool CanTransitionTo(MovementState newState);
}
```

### `Assets/Script/Character/Core/States/MovementStateMachine.cs`

```cs

using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 移动状态机 - 管理基础移动状态（行走、奔跑、停止,下蹲）
/// </summary>
public class MovementStateMachine : ICharacterStateMachine, IAnimationProvider
{
    // ============ ICharacterStateMachine 接口实现 ============
    public string StateMachineName => "MovementStateMachine";

    public int Priority => 10;

    public string CurrentStateName => currentState?.StateName ?? "None";

    private CharacterStatsController statController;
    public CharacterStatsController StatController => statController;

    public bool IsActive
    {
        get
        {
            return dataBus != null && dataBus.isAlive && dataBus.canMove;
        }
    }

    // ============ 内部状态管理 ============
    private Dictionary<Type, MovementState> states;
    private MovementState currentState;

    private Vector3 lastCharacterPosition;

    private EAnimSemantic lastLogicSemantic;
    private bool hasSubmittedAnimationIntent;

    private int submittedFacingChangeId = -1;
    private int movementIntentReplayId;

    private EAnimSemantic committedMovementSemantic = EAnimSemantic.Idle;
    private EAnimSemantic pendingMovementSemantic = EAnimSemantic.Idle;
    private float pendingMovementSemanticTimer;
    private bool hasCommittedMovementSemantic;

    private const float ForwardBackSwitchConfirmTime = 0.06f;

    // 组件引用
    private CharacterDataBus dataBus;
    private Transform transform;
    private Rigidbody2D rigidbody;
    private CharacterRoot characterRoot;
    private StateArbitrator stateArbitrator;

    // 动画意图提交器
    private CharacterAnimationComposer animationComposer;
    private JumpGroundProbe _groundProbe;

    // ============ 初始化 ============
    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;

        if (dataBus == null)
        {
            Debug.LogError("MovementStateMachine: 缺少 CharacterDataBus");
            return;
        }

        rigidbody = rb;
        transform = tf;
        characterRoot = root;

        // 创建地面探测器（与 JumpStateMachine 共享 isGrounded 权威写入）
        var collider = characterRoot.GetComponent<Collider2D>();
        if (collider == null)
            collider = characterRoot.GetComponentInChildren<Collider2D>();
        if (collider != null && characterRoot.jumpConfig != null)
        {
            _groundProbe = new JumpGroundProbe();
            _groundProbe.Initialize(collider, characterRoot.groundLayerMask, characterRoot.jumpConfig);
        }

        animationComposer = characterRoot.GetComponent<CharacterAnimationComposer>();
        if (animationComposer == null)
        {
            Debug.LogError("Missing CharacterAnimationComposer on CharacterRoot!");
            return;
        }

        statController = characterRoot.GetComponent<CharacterStatsController>();

        InitializeStates();

        TransitionTo<IdleState>();
    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, MovementState>
        {
            [typeof(IdleState)] = new IdleState(),
            [typeof(MoveState)] = new MoveState()
        };

        foreach (MovementState state in states.Values)
        {
            state.Initialize(this, dataBus, transform, rigidbody, characterRoot);
        }
    }

    // ============ 状态转换 ============
    public bool TransitionTo<T>() where T : MovementState
    {
        if (!states.TryGetValue(typeof(T), out MovementState newState))
        {
            Debug.LogWarning($"状态 {typeof(T).Name} 不存在");
            return false;
        }

        if (currentState != null && !currentState.CanTransitionTo(newState))
        {
            Debug.LogWarning($"无法从 {currentState.StateName} 转换到 {newState.StateName}");
            return false;
        }

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();

        return true;
    }

    public T GetState<T>() where T : MovementState
    {
        if (states.TryGetValue(typeof(T), out MovementState state))
        {
            return state as T;
        }

        return null;
    }

    // ============ ICharacterStateMachine 接口方法 ============
    public void Enter()
    {
        Debug.Log($"{StateMachineName} 被激活");

        if (currentState == null)
        {
            TransitionTo<IdleState>();
        }
        else
        {
            currentState.Enter();
        }

        // 激活时主动提交一次当前移动动画意图
        hasSubmittedAnimationIntent = false;
        submittedFacingChangeId = -1;
        movementIntentReplayId = 0;

        committedMovementSemantic = EAnimSemantic.Idle;
        pendingMovementSemantic = EAnimSemantic.Idle;
        pendingMovementSemanticTimer = 0f;
        hasCommittedMovementSemantic = false;

        OnEnterAnimation(999f);
    }

    public void Exit()
    {
        animationComposer?.RemoveIntent(AnimationIntentSourceStateMachine.Movement);
        hasSubmittedAnimationIntent = false;
    }

    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);

        UpdateMovementLogic(deltaTime);

        // 实际是刷新移动动画意图
        OnEnterAnimation(deltaTime);
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        MovementExcute(fixedDeltaTime);
    }

    private void UpdateMovementLogic(float deltaTime)
    {
        // 这里保留所有移动状态共享逻辑
    }

    // ============ 中断处理 ============
    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return other.Priority > Priority;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }

    public bool RequestState(object stateData)
    {
        if (stateData is string requestedState)
        {
            switch (requestedState)
            {
                case "ForceIdle":
                    TransitionTo<IdleState>();
                    return true;

                case "ForceMove":
                    TransitionTo<MoveState>();
                    return true;
            }
        }

        return false;
    }

    private void MovementExcute(float fixedDeltaTime)
    {
        // 跳跃状态机活跃时，移动状态机不做地面探测和重力
        // 由 JumpStateMachine 统一管理 isGrounded 和空中物理
        if (dataBus.currentJumpState != EState.Jump_None)
        {
            // 只更新移动距离和位置记录
            float moveDistance = Vector2.Distance(rigidbody.position, lastCharacterPosition);
            dataBus.isMoving = moveDistance > 0.001f;
            dataBus.characterPosition = rigidbody.position;
            lastCharacterPosition = rigidbody.position;
            return;
        }

        // 地面探测（唯一权威 isGrounded 写入者之一）
        if (_groundProbe != null)
        {
            _groundProbe.Probe();
            dataBus.isGrounded = _groundProbe.IsGrounded;
            if (dataBus.isGrounded)
                dataBus.lastGroundedTime = Time.time;
        }

        currentState?.FixedUpdate(fixedDeltaTime);

        // 自定义重力：gravityScale=0 时由这里统一应用基础重力
        if (rigidbody != null && dataBus != null && dataBus.isGrounded)
        {
            // 接地时保持微小下压力，确保贴地
            Vector2 v = rigidbody.velocity;
            if (v.y > -0.1f)
                rigidbody.velocity = new Vector2(v.x, -0.1f);
        }
        else if (rigidbody != null && dataBus != null && !dataBus.isGrounded)
        {
            // 空中但未跳跃时（从平台走出），应用下落重力
            var config = characterRoot?.jumpConfig;
            float gravity = config != null ? config.baseGravityAcceleration * config.fallGravityMultiplier : 30f;
            float maxFall = config != null ? config.maxFallSpeed : 20f;

            Vector2 v = rigidbody.velocity;
            v.y -= gravity * fixedDeltaTime;
            if (v.y < -maxFall) v.y = -maxFall;
            rigidbody.velocity = v;
        }

        float moveDistance2 = Vector2.Distance(rigidbody.position, lastCharacterPosition);
        bool isMoving = moveDistance2 > 0.001f;

        dataBus.isMoving = isMoving;
        dataBus.characterPosition = rigidbody.position;

        lastCharacterPosition = rigidbody.position;
    }

    /// <summary>
    /// 根据移动逻辑提交动画意图。
    /// 注意：这里不再直接解析 CharacterAnimationPlan，也不再直接触发 AnimationPlanRequest。
    /// </summary>
    public void OnEnterAnimation(float deltaTime)
    {
        EAnimSemantic rawSemantic = GetMovementLogicID();

        EAnimSemantic stableSemantic = ResolveStableMovementSemantic(
            rawSemantic,
            deltaTime
        );

        bool semanticChanged =
            !hasSubmittedAnimationIntent ||
            lastLogicSemantic != stableSemantic;

        bool shouldReplayForFacingChange =
            dataBus != null &&
            dataBus.isFacingChanging &&
            dataBus.requestedFacingSource == CharacterDataBus.RequestedFacingSource.Move &&
            submittedFacingChangeId != dataBus.facingChangeId;

        if (!semanticChanged && !shouldReplayForFacingChange)
            return;

        if (shouldReplayForFacingChange)
        {
            submittedFacingChangeId = dataBus.facingChangeId;
            movementIntentReplayId++;
        }

        lastLogicSemantic = stableSemantic;
        hasSubmittedAnimationIntent = true;

        SubmitMovementAnimationIntent(stableSemantic);
    }

    private EAnimSemantic GetMovementLogicID()
    {
        if (dataBus == null)
            return EAnimSemantic.Idle;

        switch (dataBus.currentMovementState)
        {
            case EState.Idle:
                return EAnimSemantic.Idle;

            case EState.Walk:
                return EAnimSemantic.Walk;

            case EState.Jog:
                return EAnimSemantic.Jog;

            case EState.JogFast:
                return EAnimSemantic.JogFast;

            case EState.Walk_Back:
                return EAnimSemantic.Walk_Back;

            case EState.Jog_Back:
                return EAnimSemantic.Jog_Back;
        }

        return EAnimSemantic.Idle;
    }


    private EAnimSemantic ResolveStableMovementSemantic(EAnimSemantic rawSemantic, float deltaTime)
    {
        /*
         * 第一次提交，直接接受。
         */
        if (!hasCommittedMovementSemantic)
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * Idle 应该立即响应。
         * 松开移动键时不要拖泥带水，否则手感会黏。
         */
        if (rawSemantic == EAnimSemantic.Idle)
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * Dash 这类强动作也立即响应。
         */
        if (rawSemantic == EAnimSemantic.JogFast)
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * 关键：
         * 如果 OrientationSystem 正处于“移动翻面确认中”，
         * 并且 rawSemantic 试图在 Jog/Jog_Back 或 Walk/Walk_Back 之间切换，
         * 那么先冻结在 committedMovementSemantic。
         *
         * 这可以消除：
         * Jog_Back -> Jog -> Idle
         * 或 Jog -> Jog_Back -> Jog
         * 这种翻面窗口内的高速抽搐。
         */
        bool isMoveTurnPending =
            characterRoot != null &&
            characterRoot.orientationSystem != null &&
            characterRoot.orientationSystem.IsMoveTurnPending;

        if (isMoveTurnPending &&
            IsForwardBackSwitch(committedMovementSemantic, rawSemantic))
        {
            pendingMovementSemantic = rawSemantic;
            pendingMovementSemanticTimer = 0f;

            return committedMovementSemantic;
        }

        /*
         * 如果不是前/后同速切换，比如：
         * Idle -> Jog_Back
         * Walk_Back -> Jog_Back
         * Jog -> Dash
         * 这些可以立即接受。
         */
        if (!IsForwardBackSwitch(committedMovementSemantic, rawSemantic))
        {
            CommitMovementSemantic(rawSemantic);
            return committedMovementSemantic;
        }

        /*
         * 走到这里说明是：
         * Walk <-> Walk_Back
         * Jog <-> Jog_Back
         * 这种最容易抖的切换。
         *
         * 要求 rawSemantic 稳定一小段时间后才提交。
         */
        if (pendingMovementSemantic != rawSemantic)
        {
            pendingMovementSemantic = rawSemantic;
            pendingMovementSemanticTimer = 0f;
        }

        pendingMovementSemanticTimer += deltaTime;

        if (pendingMovementSemanticTimer >= ForwardBackSwitchConfirmTime)
        {
            CommitMovementSemantic(rawSemantic);
        }

        return committedMovementSemantic;
    }


    private void CommitMovementSemantic(EAnimSemantic semantic)
    {
        committedMovementSemantic = semantic;
        pendingMovementSemantic = semantic;
        pendingMovementSemanticTimer = 0f;
        hasCommittedMovementSemantic = true;
    }

    private bool IsForwardBackSwitch(EAnimSemantic from, EAnimSemantic to)
    {
        if (from == to)
            return false;

        if (from == EAnimSemantic.Walk && to == EAnimSemantic.Walk_Back)
            return true;

        if (from == EAnimSemantic.Walk_Back && to == EAnimSemantic.Walk)
            return true;

        if (from == EAnimSemantic.Jog && to == EAnimSemantic.Jog_Back)
            return true;

        if (from == EAnimSemantic.Jog_Back && to == EAnimSemantic.Jog)
            return true;

        return false;
    }





    private void SubmitMovementAnimationIntent(EAnimSemantic semantic)
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.Movement,
            semantic = semantic,
            layer = AnimationLayer.BaseFullBody,
            priority = 10,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = movementIntentReplayId
        });
    }

    // ============ 工具方法 ============
    public bool IsInState<T>() where T : MovementState
    {
        return currentState is T;
    }

    public float GetCurrentSpeed()
    {
        return currentState?.MovementSpeed ?? 0f;
    }
}
```

### `Assets/Script/Character/Core/States/MoveState.cs`

```cs
using PixelCrushers.DialogueSystem.Articy.Articy_4_0;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveState : MovementState
{

    public override string StateName => "Move";

    public override float MovementSpeed => ResolveMoveSpeed(ResolveMovementState());
    [SerializeField, ReadOnly] private const float TurnLockMoveSpeed = 1.5f;
    public override void Enter()
    {
        SyncMovementRuntime();
        //Debug.Log("�����ƶ�״̬");
    }

    public override void Exit()
    {
        // 不再无条件清零 velocity，避免破坏跳跃 Y 速度
        // 仅清零横向速度，保留纵向
        if (rigidbody != null)
        {
            rigidbody.velocity = new Vector2(0f, rigidbody.velocity.y);
        }
    }

    public override void Update(float deltaTime)
    {
        if (dataBus.moveInputAmount < 0.01f)
        {
            stateMachine.TransitionTo<IdleState>();
            return;
        }
        SyncMovementRuntime();

        EState movementState = ResolveMovementState();

        dataBus.currentMovementState = movementState;
        dataBus.currentMoveSpeed = ResolveMoveSpeed(movementState);
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        // 跳跃所有权防线：跳跃状态机活跃时，移动状态机不得写物理速度
        if (dataBus.currentJumpState != EState.Jump_None)
            return;

        if (dataBus.moveInputAmount < 0.01f)
        {
            // 无输入：只清横向，保留 Y
            rigidbody.velocity = new Vector2(0f, rigidbody.velocity.y);
            dataBus.currentMoveSpeed = 0f;
            dataBus.currentMovementState = EState.Idle;
            return;
        }

        EState movementState = ResolveMovementState();
        float logicSpeed = ResolveMoveSpeed(movementState);
        float physicalSpeed = logicSpeed;

        if (ShouldLockMoveSpeedForTurn())
        {
            physicalSpeed = TurnLockMoveSpeed;
        }

        Vector2 input = dataBus.moveInput;

        if (dataBus.isGrounded)
        {
            // 接地：完整控制
            rigidbody.velocity = input.normalized * physicalSpeed;
            dataBus.currentMovementState = movementState;
        }
        else
        {
            // 空中：只控制横向，保留 Y
            rigidbody.velocity = new Vector2(input.normalized.x * physicalSpeed, rigidbody.velocity.y);
        }

        dataBus.currentMoveSpeed = logicSpeed;
    }



    private float ResolveMoveSpeed(EState movementState)
    {
        if (movementState == EState.Idle)
            return 0f;

        CharacterStatsController stats = stateMachine.StatController;

        float stateBaseSpeed = ResolveStateBaseMoveSpeed(stats, movementState);
        float globalMoveSpeedBonus = ResolveGlobalMoveSpeedBonus(stats);

        return Mathf.Max(0f, stateBaseSpeed + globalMoveSpeedBonus);
    }
    private float ResolveStateBaseMoveSpeed(CharacterStatsController stats, EState movementState)
    {
        switch (movementState)
        {
            case EState.Walk:
                return GetStatOrDefault(stats, CharacterStats.WalkSpeed, 2f);

            case EState.Jog:
                return GetStatOrDefault(stats, CharacterStats.JogSpeed, 4f);

            case EState.JogFast:
                return GetStatOrDefault(stats, CharacterStats.JogFastSpeed, 8f);

            case EState.Walk_Back:
                return GetStatOrDefault(stats, CharacterStats.WalkBackSpeed, 2f);

            case EState.Jog_Back:
                return GetStatOrDefault(stats, CharacterStats.JogBackSpeed, 4f);

            default:
                return 0f;
        }
    }

    private float ResolveGlobalMoveSpeedBonus(CharacterStatsController stats)
    {
        if (stats == null)
            return 0f;

        return stats.GetStat(CharacterStats.MoveSpeed);
    }








    private float GetStatOrDefault(
        CharacterStatsController stats,
        CharacterStats stat,
        float fallback)
    {
        if (stats == null)
            return fallback;

        float value = stats.GetStat(stat);

        if (value <= 0f)
            return fallback;

        return value;
    }








    private EState ResolveMovementState()
    {
        if (dataBus.moveInputAmount < 0.01f)
            return EState.Idle;

        bool backward = IsMovingBackwardNow();

        if (backward)
        {
            if (dataBus.isForceWalkSlow)
                return EState.Walk_Back;

            return EState.Jog_Back;
        }

        if (dataBus.isForceWalkSlow)
            return EState.Walk;

        if (dataBus.dashPressed)
            return EState.JogFast;

        return EState.Jog;
    }

    private bool ShouldLockMoveSpeedForTurn()
    {
        if (dataBus == null)
            return false;

        return dataBus.isMoveTurnMovementLocked;
    }


    private void SyncMovementRuntime()
    {
        if (dataBus == null)
            return;

        if (dataBus.moveInputAmount < 0.01f)
        {
            dataBus.currentMovementState = EState.Idle;
            dataBus.currentMoveSpeed = 0f;
            return;
        }

        EState movementState = ResolveMovementState();

        dataBus.currentMovementState = movementState;
        dataBus.currentMoveSpeed = ResolveMoveSpeed(movementState);
    }


    private bool IsMovingBackwardNow()
    {
        if (dataBus == null)
            return false;

        if (dataBus.moveInputAmount < 0.01f)
            return false;

        Vector2 move = dataBus.moveInput;

        if (Mathf.Abs(move.x) < 0.01f)
            return false;

        int moveSign = move.x >= 0f ? 1 : -1;
        int facingSign = dataBus.currentFacing == Facing.Right ? 1 : -1;

        return moveSign != facingSign;
    }



    public override bool CanTransitionTo(MovementState newState)
    {
        return newState is IdleState;
    }


}
```

### `Assets/Script/Character/Core/States/WeaponEquip/WeaponEquipStateMachine.cs`

```cs

//����
//�����Ƿ�װ��������
//���� R �л�װ��/����
//��ȫ���Զ�����
//�Զ�������
//���� Equip / Cancel ����
//���������Ƿ����
//�ύ������ͼ��
//�ύ���壺
//EAnimSemantic.Weapon_Equip
//EAnimSemantic.Weapon_Cancel
using UnityEngine;

public class WeaponEquipStateMachine : IParallelCharacterStateMachine
{
    public string StateMachineName => "WeaponEquipStateMachine";

    // Ҫ�� HoundCombatActionStateMachine ǰִ��
    public int Priority => 14;

    public string CurrentStateName => dataBus != null
        ? dataBus.weaponEquipState.ToString()
        : "None";

    public bool IsActive
    {
        get
        {
            return dataBus != null && dataBus.isAlive;
        }
    }

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private CharacterAnimationComposer animationComposer;

    private CharacterWeaponInventory inventory;
    private CharacterEquipmentController equipmentController;
    private ExternalWeaponMount weaponMount;

    private float transitionTimer;
    private int replayId;

    // ��һ����д����������Դ� WeaponCombatProfileSO �� WeaponVisualProfileSO ��ȡ
    private const float DefaultEquipDuration = 0.35f;
    private const float DefaultCancelDuration = 0.35f;

    /*
 * ��ִ�е��в�����
 *
 * ����ֻ�����װ��״̬���У�
 * DataBus ����Ҫ�����λ�л��㷨���������ڡ�
 */
    private bool hasPendingSlotSwitch;
    private WeaponSlotId pendingSlotId;

    private bool equipAfterSwitch;

    private bool HasActiveWeapon =>
        inventory != null &&
        inventory.ActiveWeapon != null;




    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;
        characterRoot = root;

        if (characterRoot != null)
        {
            animationComposer =
                characterRoot.characterAnimationComposer;

            inventory =
                characterRoot.characterWeaponInventory;

            if (inventory == null)
            {
                inventory =
                    characterRoot.GetComponent<
                        CharacterWeaponInventory>();
            }

            equipmentController =
                characterRoot.GetComponent<
                    CharacterEquipmentController>();

            weaponMount =
                characterRoot.GetComponentInChildren<
                    ExternalWeaponMount>(true);
        }
        if (dataBus == null)
            return;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        ApplyWeaponVisibility(false);
    }

    public void Enter()
    {
    }

    public void Update(float deltaTime)
    {

        /*
 * �����л��������κ�װ�����ɽ׶ζ����Խ��ա�
 *
 * ������� Cancelling���µ����ּ���������
 * pendingSlotId������ʹ�����һ������
 */
        ConsumeWeaponSwitchInput();
        /*
 * �ȴ�����ɡ�
 * �������ʱ����ִ�в�λ�滻����ʼ��һ�� Equip��
 */
        UpdateTransition(deltaTime);
        /*
 * ֻ�е�ǰ���ڹ�����ʱ�Ž�����ͨ����
 * �в��������ͨ�� RequestSwitchSlot() ������ʱ�̸��� pending��
 */
        if (!IsTransitioning())
        {
            HandleRequests();
        }

        /*
 * û��������û�д��в�ʱ�����ֳ�������
 */
        if (!HasActiveWeapon &&
            !hasPendingSlotSwitch)
        {
            RemoveEquipIntent();
            ForceCancelled();
            return;
        }
        SyncWeaponVisualState();
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
    }

    public bool RequestState(object stateData)
    {
        /*
           * �����Ժ�ͨ��ͳһ״̬������ WeaponSlotId��
           */
        if (stateData is WeaponSlotId slotId)
        {
            return RequestSwitchSlot(slotId);
        }

        return false;
    }

    /// <summary>
    /// �����л���ָ�������ۡ�
    ///
    /// ��ǰ�׶ο��ɵ��԰�ťֱ�ӵ��á�
    /// ��һ�׶����� InputHandler �� 1/2/3 ������á�
    /// </summary>
    public bool RequestSwitchSlot(
        WeaponSlotId slotId)
    {
        if (dataBus == null ||
            inventory == null ||
            equipmentController == null)
        {
            return false;
        }

        if (!inventory.CanSelectSlot(slotId))
        {
            Debug.LogWarning(
                $"[WeaponEquip] ��λ����ѡ��: {slotId}",
                characterRoot
            );

            return false;
        }

        /*
         * �Ѿ��ǵ�ǰ�ۣ�����û�������л������ظ�ִ�С�
         */
        if (slotId == inventory.ActiveSlotId &&
            !hasPendingSlotSwitch)
        {
            switch (dataBus.weaponEquipState)
            {
                case WeaponEquipState.Cancelled:
                    {
                        /*
                         * ��ǰ��λ��ȷ��ֻ��������������״̬��
                         * ���ּ�Ӧֱ���ó���ǰ������
                         */
                        if (!dataBus.isInSafeZone)
                        {
                            StartEquip();
                        }
                        Debug.Log( $"[WeaponEquip] {slotId} �Ѿ��ǵ�ǰ��λ��" +$"ִ�е�ǰ�����ó���",characterRoot);
                        return true;
                    }
                case WeaponEquipState.Equipped:
                    {
                        Debug.Log( $"[WeaponEquip] �����л�����" +$"{slotId} �Ѿ�װ��������ʹ��״̬��", characterRoot);
                        return true;
                    }
                case WeaponEquipState.Equipping:
                    {
                        Debug.Log($"[WeaponEquip] �����л�����" +$"{slotId} ����װ����",characterRoot);
                        return true;
                    }

                case WeaponEquipState.Cancelling:
                    {
                        /*
                         * ��ǰ�����������𣬵�����ְ��˵�ǰ��λ��
                         * �� Cancel ��ɺ�����װ����ǰ��λ��
                         */
                        pendingSlotId = slotId;
                        hasPendingSlotSwitch = true;
                        equipAfterSwitch = true;
                        Debug.Log($"[WeaponEquip] ��ǰ��λ��������" +$"��ɺ�����װ�� {slotId}��",characterRoot);
                        return true;
                    }
            }
        }

        /*
         * ����л��������ٴ������������ּ���
         * ʹ�����һ������
         */
        pendingSlotId = slotId;
        hasPendingSlotSwitch = true;

        /*
         * ���ּ��л���Ŀ�����ó���������
         */
        equipAfterSwitch = true;

        Debug.Log($"[WeaponEquip] �յ��в�����" +$" Current={inventory.ActiveSlotId}," + $" Target={pendingSlotId}," + $" EquipState={dataBus.weaponEquipState}",characterRoot);

        switch (dataBus.weaponEquipState)
        {
            case WeaponEquipState.Equipped:
            case WeaponEquipState.Equipping:
                StartCancel();
                break;

            case WeaponEquipState.Cancelled:
                CommitPendingSlotSwitch();
                break;

            case WeaponEquipState.Cancelling:
                /*
                 * ��������ʱֻ���� pendingSlotId��
                 * Cancel ��ɺ�ʹ����������
                 */
                break;
        }

        return true;
    }

    public bool RequestSwitchToNextSlot()
    {
        if (inventory == null)
            return false;
        /*
 * ����Ѿ���һ�����л�Ŀ�꣬
 * ��һ���ֻ�Ӧ�Ӵ��л�Ŀ���������顣
 */
        WeaponSlotId cycleStartSlot =
            hasPendingSlotSwitch
                ? pendingSlotId
                : inventory.ActiveSlotId;
        bool found =inventory.TryGetNextOccupiedSlotFrom(cycleStartSlot,out WeaponSlotId nextSlotId);
        if (!found)
        {
            Debug.Log("[WeaponEquip] û���ҵ���һ�����������ۡ�",characterRoot );
            return false;
        }
        return RequestSwitchSlot(nextSlotId);
    }

    private void HandleRequests()
    {
        if (dataBus == null)
            return;

        if (dataBus.isInSafeZone)
        {
            if (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipped ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping)
            {
                /*
                 * ��ȫ���������вۡ�
                 */
                hasPendingSlotSwitch = false;
                equipAfterSwitch = false;
                StartCancel();
            }

            dataBus.requestCancelWeapon = false;
            return;
        }

        if (dataBus.requestCancelWeapon)
        {
            dataBus.requestCancelWeapon = false;

            if (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipped ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping)
            {
                hasPendingSlotSwitch = false;
                equipAfterSwitch = false;
                StartCancel();
            }

            return;
        }

        if (!dataBus.weaponToggleLongPressedThisFrame)
            return;

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Cancelled)
        {
            StartEquip();
            return;
        }

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Equipped)
        {
            hasPendingSlotSwitch = false;
            equipAfterSwitch = false;
            StartCancel();
        }
    }









    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return true;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }


    private void StartEquip()
    {
        if (dataBus == null ||
            !HasActiveWeapon)
        {
            return;
        }

        if (dataBus.weaponEquipState ==
                WeaponEquipState.Equipping ||
            dataBus.weaponEquipState ==
                WeaponEquipState.Equipped)
        {
            return;
        }

        replayId++;

        dataBus.weaponEquipState =
            WeaponEquipState.Equipping;

        /*
         * װ��������ʼʱ�������֡�
         */
        ApplyWeaponVisibility(true);

        transitionTimer = GetEquipDuration();

        SubmitEquipIntent(
            EAnimSemantic.Weapon_Equip,
            transitionTimer,
            replayId
        );
    }


    private void StartCancel()
    {
        if (dataBus == null)
            return;

        if (dataBus.weaponEquipState ==
                WeaponEquipState.Cancelled ||
            dataBus.weaponEquipState ==
                WeaponEquipState.Cancelling)
        {
            return;
        }

        replayId++;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelling;

        transitionTimer = GetCancelDuration();

        SubmitEquipIntent(
            EAnimSemantic.Weapon_Cancel,
            transitionTimer,
            replayId
        );

        if (characterRoot != null &&
            characterRoot.stateArbitrator != null &&
            characterRoot.stateArbitrator
                .houndCombatActionSM != null)
        {
            characterRoot.stateArbitrator
                .houndCombatActionSM
                .HardClearWeaponAnimation();
        }
    }

    private void UpdateTransition(float deltaTime)
    {
        if (dataBus == null ||
            !IsTransitioning())
        {
            return;
        }

        transitionTimer -= deltaTime;

        if (transitionTimer > 0f)
            return;

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Equipping)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Equipped;

            RemoveEquipIntent();
            return;
        }

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Cancelling)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;

            RemoveEquipIntent();
            ApplyWeaponVisibility(false);

            /*
             * �������Ѿ���ȫ����
             * ���ڲ����滻ģ�ͺ� ActiveSlot��
             */
            if (hasPendingSlotSwitch)
            {
                CommitPendingSlotSwitch();
            }
        }
    }


    private void CommitPendingSlotSwitch()
    {
        if (!hasPendingSlotSwitch ||
            inventory == null ||
            equipmentController == null)
        {
            return;
        }

        WeaponSlotId targetSlot =
            pendingSlotId;

        bool shouldEquip =
            equipAfterSwitch;

        /*
         * ���� pending������ص���ʧ�ܷ�֧���롣
         */
        hasPendingSlotSwitch = false;
        equipAfterSwitch = false;
        ApplyWeaponVisibility(false);
        bool slotChanged =inventory.TrySetActiveSlot(targetSlot);
        if (!slotChanged)
        {
            Debug.LogWarning(
                $"[WeaponEquip] ActiveSlot �л�ʧ��: " +
                $"{targetSlot}",
                characterRoot
            );

            return;
        }

        bool mounted =
            equipmentController
                .MountActiveInventoryWeapon();

        if (!mounted)
        {
            Debug.LogError(
                $"[WeaponEquip] ����������ʧ��: " +
                $"Slot={targetSlot}",
                characterRoot
            );

            ForceCancelled();
            return;
        }

        /*
         * MountWeaponRuntimeData ����ս����ǣ�
         * ������ȷ����װ��״̬Ϊ Cancelled��
         * Ȼ��ʼ������ Equip��
         */
        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        Debug.Log(
            $"[WeaponEquip] ���л���λ��" +
            $" Active={inventory.ActiveSlotId}," +
            $" Weapon={GetActiveWeaponName()}",
            characterRoot
        );

        if (shouldEquip &&
            !dataBus.isInSafeZone)
        {
            StartEquip();
        }
    }


    private bool IsTransitioning()
    {
        return dataBus != null &&
               (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Cancelling);
    }

    private void ForceCancelled()
    {
        if (dataBus == null)
            return;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        transitionTimer = 0f;

        RemoveEquipIntent();
        ApplyWeaponVisibility(false);
    }

    private float GetEquipDuration()
    {
        return DefaultEquipDuration;
    }

    private float GetCancelDuration()
    {
        return DefaultCancelDuration;
    }

    private void SubmitEquipIntent(
        EAnimSemantic semantic,
        float duration,
        int currentReplayId)
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(
            new AnimationIntent
            {
                root = characterRoot,
                intentSourceStateMatine =
                    AnimationIntentSourceStateMachine
                        .WeaponEquip,
                semantic = semantic,
                layer = AnimationLayer.UpperBody,
                priority = 70,
                exclusiveFullBody = false,
                oneShot = true,
                duration = duration,
                replayId = currentReplayId
            }
        );
    }

    private void RemoveEquipIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(
            AnimationIntentSourceStateMachine
                .WeaponEquip
        );
    }








    public void ApplyWeaponVisibility(bool visible)
    {
        if (weaponMount == null && characterRoot != null)
            weaponMount = characterRoot.GetComponentInChildren<ExternalWeaponMount>(true);

        if (weaponMount == null)
            return;

        weaponMount.SetWeaponVisible(visible);
    }

    private void SyncWeaponVisualState()
    {
        if (dataBus == null)
            return;

        /*
         * Equipping���Ѿ��ó�����ʾ��
         * Equipped����ʾ��
         * Cancelling�����𶯻���û������������ʾ��
         * Cancelled�����ء�
         */
        bool shouldBeVisible =
            dataBus.weaponEquipState !=
            WeaponEquipState.Cancelled;

        ApplyWeaponVisibility(shouldBeVisible);
    }

    private string GetActiveWeaponName()
    {
        WeaponRuntimeData weapon =
            inventory != null
                ? inventory.ActiveWeapon
                : null;

        if (weapon == null ||
            weapon.config == null)
        {
            return "NULL";
        }

        return weapon.config.weaponName;
    }

    private void ConsumeWeaponSwitchInput()
    {
        if (dataBus == null)
            return;

        bool hasRequest = dataBus.TryConsumeWeaponSwitchInput(out WeaponSwitchInputRequestType requestType, out WeaponSlotId requestedSlot );

        if (!hasRequest)
            return;

        switch (requestType)
        {
            case WeaponSwitchInputRequestType.SelectSlot:
                {
                    bool accepted = RequestSwitchSlot(requestedSlot);
                    Debug.Log($"[WeaponEquip] Consume SelectSlot." +$" Slot={requestedSlot}," +$" Accepted={accepted}",characterRoot );
                    break;
                }

            case WeaponSwitchInputRequestType.CycleNext:
                {
                    bool accepted = RequestSwitchToNextSlot();
                    Debug.Log( $"[WeaponEquip] Consume CycleNext." +$" Accepted={accepted}", characterRoot);
                    break;
                }
        }
    }





    public void Exit()
    {
        /*
         * StateArbitrator ��ǰ��Բ���Ծ�Ĳ���״̬��
         * ÿ֡���� Exit���������������ظ�ִ�С�
         */
        RemoveEquipIntent();

        if (dataBus != null &&
            !hasPendingSlotSwitch)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;
        }

        if (!hasPendingSlotSwitch)
        {
            ApplyWeaponVisibility(false);
        }
    }





}
```

### `Assets/Script/Character/Core/States/WeaponPosture/WeaponPostureStateMachine.cs`

```cs
using UnityEngine;

public class WeaponPostureStateMachine : IParallelCharacterStateMachine
{
    public string StateMachineName => "WeaponPostureStateMachine";

    // Ҫ�� HoundCombatActionStateMachine ��ִ��
    public int Priority => 16;

    public string CurrentStateName => dataBus != null
        ? dataBus.weaponPostureState.ToString()
        : "None";

    public bool IsActive
    {
        get
        {
            return dataBus != null
                && dataBus.isAlive
                && dataBus.currentWeaponRuntimeData != null;
        }
    }

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private CharacterAnimationComposer animationComposer;

    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;
        characterRoot = root;

        if (characterRoot != null)
            animationComposer = characterRoot.characterAnimationComposer;
        dataBus.weaponPostureState = WeaponPostureState.None;
    }

    public void Enter()
    {


    }

    public void Exit()
    {
        SetNone();
    }

    public void Update(float deltaTime)
    {
        if (!IsActive)
        {
            SetNone();
            return;
        }

        if (ShouldUseLowReady())
        {
            SetLowReady();
        }
        else
        {
            SetNone();
        }
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
    }

    public bool RequestState(object stateData)
    {
        return false;
    }

    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return true;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }

    private bool ShouldUseLowReady()
    {
        if (dataBus == null)
            return false;

        // ֻ����װ��״̬������ LowReady
        if (dataBus.weaponEquipState != WeaponEquipState.Equipped)
            return false;

        // ��ȫ������ʾ LowReady
        if (dataBus.isInSafeZone)
            return false;

        // װ��/��������в���ʾ LowReady
        if (dataBus.isWeaponEquipTransitioning)
            return false;

        // ��׼���� Gun_Aim �ӹ�
        if (dataBus.isAiming)
            return false;

        // �������� Gun_Reload �ӹ�
        if (dataBus.isReloading)
            return false;

        // �������� Fire / Melee �ӹ�
        // isHoundAttacking removed - now handled by exclusiveFullBody in MergePlans
        if (dataBus.isHunterAttacking)
            return false;

        return true;
    }

    private void SetLowReady()
    {
        if (dataBus == null)
            return;

        dataBus.weaponPostureState = WeaponPostureState.LowReady;

        SubmitLowReadyIntent();
    }

    private void SetNone()
    {
        if (dataBus != null)
            dataBus.weaponPostureState = WeaponPostureState.None;

        RemovePostureIntent();
    }

    private void SubmitLowReadyIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.WeaponPosture,
            semantic = EAnimSemantic.Weapon_LowReady,
            layer = AnimationLayer.UpperBody,
            priority = 40,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0.2f,
            replayId = 0
        });
    }

    private void RemovePostureIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.WeaponPosture);
    }
}
```

### `Assets/Script/Character/Core/StateType.cs`

```cs

/// <summary>
/// 所有状态类型枚举
/// </summary>
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationArbitrator.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

public class AnimationArbitrator
{
    public static CharacterAnimationPlan Resolve(
        EAnimationLogicState id,
        AnimationPlanMappingSO baseMap,
        List<AnimationPlanOverrideRuleSetSO> itemOverrides,
        AnimationPlanOverrideRuleSetSO statusOverrides,
        CharacterRoot root)
    {
        if (root == null || root.characterDataBus == null)
        {
            Debug.LogWarning("[AnimationArbitrator] root 或 characterDataBus 为空");
            return null;
        }

        CharacterAnimationPlan resolvedPlan = null;

        // 1. 状态覆盖优先
        if (statusOverrides != null)
        {
            resolvedPlan = statusOverrides.GetOverride(id, root.characterDataBus);
        }

        // 2. 装备/职业覆盖
        if (resolvedPlan == null && itemOverrides != null)
        {
            for (int i = itemOverrides.Count - 1; i >= 0; i--)
            {
                AnimationPlanOverrideRuleSetSO overrideSet = itemOverrides[i];

                if (overrideSet == null)
                    continue;

                resolvedPlan = overrideSet.GetOverride(id, root.characterDataBus);

                if (resolvedPlan != null)
                    break;
            }
        }

        // 3. 基础映射兜底
        if (resolvedPlan == null && baseMap != null)
        {
            resolvedPlan = baseMap.GetDefault(id);

            if (resolvedPlan == null)
            {
                Debug.LogWarning(
                    $"[AnimationArbitrator] baseMap 未命中: id={id}, int={(int)id}, baseMap={baseMap.name}, character={root.name}"
                );
            }
        }

        // 4. Clone + 补收件人
        if (resolvedPlan != null)
        {
            return resolvedPlan.CloneFor(root);
        }

        //Debug.LogWarning($"[AnimationArbitrator] 未找到动画映射: {id} | 角色: {root.name}");
        return null;
    }
}
```
