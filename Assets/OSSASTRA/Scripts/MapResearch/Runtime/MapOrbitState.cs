/*
 * 脚本：MapOrbitState
 * 功能：保存八档观察目标与连续实际角度，提供有界、最短路径的相机转向。
 * 边界：不读取按键、不写Transform；每次请求最多一档，不累计无限角度或请求队列。
 * 协作：MapResearchRoot读取实际角度后发布同一帧的相机空间，角色仍走原控制链。
 */
using System;
using UnityEngine;

namespace Ossastra.MapResearch
{
    public sealed class MapOrbitState
    {
        private readonly float initialYaw;
        public int TargetSector { get; private set; }
        public float ActualYaw { get; private set; }
        public float TargetYaw => Mathf.Repeat(initialYaw + TargetSector * 45f, 360f);

        public MapOrbitState(float initialYaw)
        {
            if (!IsFinite(initialYaw)) throw new ArgumentOutOfRangeException(nameof(initialYaw));
            this.initialYaw = Mathf.Repeat(initialYaw, 360f);
            Reset();
        }

        /// <summary>一条有效事件只请求一档；相反键同时按下由调用方合成为零。</summary>
        public void RequestStep(int direction)
        {
            int step = direction > 0 ? 1 : direction < 0 ? -1 : 0;
            TargetSector = (TargetSector + step + 8) % 8;
        }

        public void Tick(float deltaTime, float degreesPerSecond)
        {
            if (!IsFinite(deltaTime) || !IsFinite(degreesPerSecond) || deltaTime <= 0f || degreesPerSecond <= 0f) return;
            // 使用实际角度到最新目标的短弧，反向请求不会等待旧动画或绕过一整圈。
            ActualYaw = Mathf.Repeat(Mathf.MoveTowardsAngle(ActualYaw, TargetYaw, degreesPerSecond * deltaTime), 360f);
        }

        public void Reset()
        {
            TargetSector = 0;
            ActualYaw = initialYaw;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
