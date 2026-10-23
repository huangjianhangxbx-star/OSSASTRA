/*
 * 脚本：CharacterContracts
 * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
 * 边界：只定义能力；实际实现与调用顺序不在本文件。
 * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
 */
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
using UnityEngine;
using System.Collections.Generic;

namespace Ossastra.Characters
{
    /// <summary>同一玩家的用途来源记忆；不包含角色状态或设备全局开关。</summary>
    public interface IPlayerInputSourceSession
    {
        PlayerInputSourceState InputSources { get; }
    }
    /// <summary>真实库存拥有手槽；本体装备门面只消费定义和命令，不缓存另一份位置。</summary>
    public interface ICharacterEquipmentInventory : IEquipmentSlots
    {
        EquipmentDefinitionSO RightDefinition {get;}
        EquipmentDefinitionSO LeftDefinition {get;}
        EquipmentDefinitionSO[] AvailableOffhands {get;}
        bool TryEquip(EquipmentDefinitionSO definition);
    }
    /// <summary>根显式推进的定义驱动能力：输入路由与已释放效果分开，旧样板路径与此端口互斥。</summary>
    public interface ICharacterAbilityController
    {
        void Initialize(CharacterRoot owner);
        void TickInput(CharacterInputFrame frame);
        void TickEffects(float delta);
        bool OwnsRelease(System.Guid actionId);
        void ObserveFact(ActionFact fact);
        bool CanAccept(CharacterActionKind kind,out string reason);
        bool IsBusy {get;}
        void CancelPending(string reason);
    }
    /// <summary>执行器只访问已验证来源；装备耐久与物品数量分别实现，不由投递器持有。</summary>
    public interface IAbilitySource:IAbilityStateSource
    {
        string OwnerId {get;}
        string InstanceId {get;}
        bool IsAvailable {get;}
        bool TryReserve(System.Guid transactionId);
        bool CommitRelease(System.Guid executionId);
        void CancelReservation();
    }
    /// <summary>可携带能力payload的稳定实例；库存装备也应返回同一对象，不能在换人/换槽时复制CD与弹药。</summary>
    public interface IAbilityStateSource { EquipmentAbilityState RuntimeState {get;} }
    /// <summary>目标由合法库存/伙伴请求冻结；执行器仍复核距离、视线与目标生命，不读取另一套设备输入。</summary>
    public interface IAbilityTargetSource
    {
        Ossastra.Combat.CombatVitals Target {get;}
        UnityEngine.Vector3? AimPoint {get;}
    }
    /// <summary>可选本体补给消费者；根在自治普攻前依序采样，不由场景服务另开角色Update。</summary>
    public interface ICharacterInventoryConsumer
    {
        void Initialize(CharacterRoot owner);
        bool TickCandidate();
    }
    /// <summary>消耗品短暂挂点表现；根在身体骨架求姿态后推进，不反向影响消费结果。</summary>
    public interface ICharacterTransientItemPresentation
    {
        void Initialize(CharacterRoot owner);
        void TickPresentation();
        void Hide();
    }
    /// <summary>角色技能能力；根调度输入、唯一释放事实和离手效果，UI只提交请求。</summary>
    public interface ICharacterSkills
    {
        void Initialize(CharacterRoot owner);
        void TickInput(CharacterInputFrame frame);
        bool TryRequest(int slot,CharacterActionSource source);
        bool TryHandleRelease(AttackRelease release,Vector3 direction);
        void TickEffects(float delta);
        void Clear();
    }
    /// <summary>动作接受后冻结播放配置；表现端不反向接受动作或写伤害。</summary>
    public interface ICharacterConfiguredAnimation
    {
        bool CanCaptureAction(ActionDefinitionSO definition,out string reason);
        void CaptureAction(System.Guid actionId,ActionDefinitionSO definition);
        ConfiguredActionSnapshot FreezeAction(ActionDefinitionSO definition);
        void CapturePhase(System.Guid actionId,ConfiguredActionSnapshot phase);
    }
    /// <summary>位移能力的只读表现身份；仍由唯一Movement执行速度。</summary>
    public interface ICharacterMotionAnimation:ICharacterMotionOverride
    {
        System.Guid ActionId {get;}
        float Duration {get;}
    }
    public enum CharacterActionKind { Main, Dodge, Guard, Offhand, Support }
    public enum CharacterActionSource { Player, Companion, UserInterface, Inventory }
    /// <summary>请求携带角色/生命/控制代际和请求身份；动作身份由原执行端接受时产生。</summary>
    public readonly struct CharacterActionRequest
    {
        public string ActorId {get;}
        public string SourceLabel {get;}
        public int LifeGeneration {get;}
        public int ControlRevision {get;}
        public CharacterActionKind Kind {get;}
        public CharacterActionSource Source {get;}
        public System.Guid RequestId {get;}
        public CharacterActionRequest(string actorId,int lifeGeneration,int controlRevision,CharacterActionKind kind,CharacterActionSource source,System.Guid requestId,string sourceLabel=null)
        {ActorId=actorId;LifeGeneration=lifeGeneration;ControlRevision=controlRevision;Kind=kind;Source=source;RequestId=requestId;SourceLabel=sourceLabel;}
    }
    /// <summary>本体统一接受入口：先完整校验，再调用执行端口并按真实动作身份付款。</summary>
    public interface ICharacterActionCoordinator
    {
        string LastResult {get;}
        CharacterActionRequest CreateRequest(CharacterActionKind kind,CharacterActionSource source,string sourceLabel=null);
        bool CanAccept(CharacterActionRequest request,float cost,out string reason);
        bool TryCommit(CharacterActionRequest request,float cost,System.Func<System.Guid> accept,out string reason);
    }
    /// <summary>副手未释放前的空闲资格；在途派生不占据主手动作时钟。</summary>
    public interface ICharacterOffhandStatus { bool IsAccepting {get;} }
    /// <summary>本体体力只读视图；付款仅经本体资源模块，不在DataBus复制余额。</summary>
    public interface ICharacterStamina
    {
        double Current { get; }
        double Maximum { get; }
    }
    /// <summary>原运动执行器读取机动速度；能力不直接调用Move或修改位置。</summary>
    public interface ICharacterMotionOverride
    {
        bool IsActive { get; }
        Vector3 MotionVelocity { get; }
    }
    /// <summary>伙伴战斗策略提供移动候选；选择器与Root保留有效输入及实际运动执行权。</summary>
    public interface ICompanionMoveCandidate
    {
        bool TryGetCombatMove(out Vector2 move);
    }
    /// <summary>伙伴战斗策略提供攻击目标候选；Root决定是否受理并执行动作。</summary>
    public interface ICompanionActionCandidate
    {
        bool TryGetAttackTarget(out CombatVitals target);
        /// <summary>Root受理当前候选后通知策略开始冷却；候选本身不启动动作。</summary>
        void OnAccepted();
    }
    /// <summary>队伍只分配合法目标；角色能力自行计算移动，不从此契约取得方向或策略状态。</summary>
    public interface ICharacterFollowTargetSource
    {
        bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target);
    }
    /// <summary>场景唯一控制权的只读视图；代际和共享屏障同时约束玩家与伙伴来源。</summary>
    public interface ICharacterControlSession
    {
        CharacterRoot Current { get; }
        IReadOnlyList<CharacterRoot> Characters { get; }
        bool IsIntentSamplingAllowed { get; }
        int ControlRevision { get; }
    }
    /// <summary>伙伴策略只提供移动候选帧；选择器剥离攻击、暂停、取消和开发命令。</summary>
    public interface ICompanionIntentSource
    {
        bool IsAvailable { get; }
        CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta);
    }
    /// <summary>玩家源的实时许可；外层已采样帧在许可撤回时同样失效，兼容未挂跟随模块的旧场。</summary>
    public interface ICharacterInputAvailability
    {
        bool HasPlayerInput { get; }
    }
    /// <summary>场景权威分别回答有效实控归属和设备采样许可；输入源不保存第二份Current。</summary>
    public interface ICharacterPlayerControlSource
    {
        bool IsPlayerControlOwner(ICharacterInputSource source);
        bool IsPlayerInputAllowed(ICharacterInputSource source);
    }
    /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
    public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
    /// <summary>提供单角色唯一有效输入快照，不判定动作是否成功。</summary>
    public interface ICharacterInputSource
    {
        CharacterInputFrame CurrentFrame { get; }
        bool IsConfigured { get; }
        CharacterInputFrame Read();
    }
    /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
    public interface ICharacterMovement : ICharacterSubsystem
    {
        bool IsMoving { get; }
        Vector3 Velocity { get; }
        void Tick(bool allowed, float delta);
        void Teleport(Vector3 position);
    }
    /// <summary>持续能力只修饰自愿移动速度；不接管动作位移、受迫移动或物理写入。</summary>
    public interface ICharacterLocomotionScale { float LocomotionScale {get;} }
    /// <summary>分别维护攻击方向与可见翻转，由Root显式调度。</summary>
    public interface ICharacterOrientation
    {
        Vector3 AttackDirection { get; }
        float VisualFacingX { get; }
        void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
        void OrientTowards(Vector3 worldPosition);
        void FaceAttackDirection();
    }
    /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
    public interface ICharacterPresentation : ICharacterSubsystem
    {
        string CurrentClip { get; }
        void SetPaused(bool paused);
        void Present();
    }
}
