/*
 * 脚本：CharacterContracts
 * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
 * 边界：只定义能力；实际实现与调用顺序不在本文件。
 * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
 */
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
    public interface ICharacterPlayerControlSource
    {
        bool IsPlayerInputAllowed(ICharacterInputSource source);
    }
    /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
    public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
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
    public interface ICharacterHitResolver
    {
        string LastResult { get; }
        void Resolve(AttackRelease release, Vector3 releaseDirection);
    }
    // World target adapter: retains the existing sole Health writer.
    public interface ICharacterHitTarget
    {
        Health Health { get; }
        void ShowHit();
    }
}
