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

    /// <summary>场景只绑定观察上下文与输入捕获；不能借此移动角色、受理动作或播放动画。</summary>
    public interface ICharacterViewParticipant
    {
        Transform CharacterTransform { get; }
        Vector3 LogicalDirection { get; }
        string ViewDiagnostics { get; }
        bool BindViewContext(object owner, ICharacterViewContext context);
        bool ReleaseViewContext(object owner);
        bool SetViewInputCapture(object owner, bool captured);
    }
}
