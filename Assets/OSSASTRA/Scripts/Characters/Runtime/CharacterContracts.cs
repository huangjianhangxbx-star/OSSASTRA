/*
 * 脚本：CharacterContracts
 * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
 * 边界：只定义能力；实际实现与调用顺序不在本文件。
 * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
 */
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
using System.Collections.Generic;

namespace Ossastra.Characters
{
    public enum CharacterActionKind { Main, Dodge, Guard, Offhand }
    public enum CharacterActionSource { Player, Companion, UserInterface }
    /// <summary>请求携带角色/生命/控制代际和请求身份；动作身份由原执行端接受时产生。</summary>
    public readonly struct CharacterActionRequest
    {
        public readonly string ActorId,SourceLabel;
        public readonly int LifeGeneration,ControlRevision;
        public readonly CharacterActionKind Kind;
        public readonly CharacterActionSource Source;
        public readonly System.Guid RequestId;
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
    /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
    public interface ICharacterPlayerControlSource
    {
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
