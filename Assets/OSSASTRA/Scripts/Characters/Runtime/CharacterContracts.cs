/*
 * 脚本：CharacterContracts
 * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
 * 边界：只定义能力；实际实现与调用顺序不在本文件。
 * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
 */
using Ossastra.CharacterCore;
using UnityEngine;
using System.Collections.Generic;

namespace Ossastra.Characters
{
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
    /// <summary>消费一次释放与方向快照，执行世界目标筛选。</summary>
    public interface ICharacterHitResolver
    {
        string LastResult { get; }
        void Resolve(AttackRelease release, Vector3 releaseDirection);
    }
    /// <summary>提供同一Health和命中反馈；生产命中模块不依赖开发靶类型。</summary>
    public interface ICharacterHitTarget
    {
        Health Health { get; }
        void ShowHit();
    }
}
