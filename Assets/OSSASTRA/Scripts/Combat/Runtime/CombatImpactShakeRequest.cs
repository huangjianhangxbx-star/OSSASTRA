/*
 * 脚本：CombatImpactShakeRequest
 * 功能：将已发生的撞击身份、方向和反馈强度交给相机领域。
 * 边界：不保存Camera、不拥有时钟、不写入相机位移。
 */
using System;
using UnityEngine;

namespace Ossastra.Combat
{
    public readonly struct CombatImpactShakeRequest
    {
        public Guid ParentExecutionId { get; }
        public Guid ActionId { get; }
        public int EmissionIndex { get; }
        public string ActorId { get; }
        public Vector3 WorldDirection { get; }
        public float Strength { get; }

        public CombatImpactShakeRequest(Guid parentExecutionId, Guid actionId, int emissionIndex,
            string actorId, Vector3 worldDirection, float strength)
        {
            ParentExecutionId = parentExecutionId;
            ActionId = actionId;
            EmissionIndex = emissionIndex;
            ActorId = actorId;
            WorldDirection = worldDirection;
            Strength = strength;
        }
    }

    /// <summary>相机接收事实冲击。播放、合成和清理均归相机所有。</summary>
    public interface ICombatImpactShakeSink
    {
        void RequestImpact(CombatImpactShakeRequest request);
    }
}
