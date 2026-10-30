/*
 * 脚本：CharacterViewContracts
 * 功能：声明场景观察上下文与现有角色的窄接入契约，供独立程序集引用。
 * 边界：不引用角色运行时、研究场景、Input System、Spine或玩法状态；上下文只发布实际观察姿态。
 * 协作：场景显式绑定owner；CharacterRoot组织可选适配器，原输入/仲裁/动作保持唯一。
 */
using UnityEngine;

namespace Ossastra.Characters.ViewSpace
{
    /// <summary>场景发布同帧实际相机与水平正交基；结构相机不得替换GameplayCamera。</summary>
    public interface ICharacterViewContext
    {
        Camera GameplayCamera { get; }
        bool IsValid { get; }
        Vector3 ViewRight { get; }
        Vector3 ViewForward { get; }
        Quaternion ViewRotation { get; }
        bool FreezePresentation { get; }
    }

    /// <summary>只绑定观察上下文；敌人可消费共同观察基，无须伪造玩家输入捕获能力。</summary>
    public interface ICharacterViewConsumer
    {
        bool BindViewContext(object owner, ICharacterViewContext context);
        bool ReleaseViewContext(object owner);
    }

    public interface ICharacterViewParticipant : ICharacterViewConsumer
    {
        // 保留旧接口直接声明的反射入口；运行行为仍由同一实现承担。
        new bool BindViewContext(object owner, ICharacterViewContext context);
        new bool ReleaseViewContext(object owner);
        Transform CharacterTransform { get; }
        Vector3 LogicalDirection { get; }
        string ViewDiagnostics { get; }
        bool SetViewInputCapture(object owner, bool captured);
    }

    /// <summary>角色根组织的可选纯视觉深度；不持有控制/动画/物理权威，无模块的旧场不受影响。</summary>
    public interface ICharacterVisualDepth
    {
        void Initialize(Transform actor);
        // 根已完成当帧装备表现后，提供实际游戏相机；实现不另行Update角色。
        void Prepare(Camera gameplayCamera);
        void Suspend();
        void Dispose();
    }
}
