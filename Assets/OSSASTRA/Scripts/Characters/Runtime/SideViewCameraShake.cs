/*
 * 脚本：SideViewCameraShake
 * 功能：将逐发射冲击合成为有界、可归零的横版镜头偏移。
 * 边界：只推进表现时间和偏移，不读写相机、角色或碰撞。相机跟随组件拥有最终提交。
 */
using System;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    [Serializable]
    public sealed class SideViewCameraShakeSettings
    {
        public bool enabled = true;
        [Range(0f, 1f)] public float intensity = .8f;
        [Range(.02f, .5f)] public float duration = .16f;
        [Range(1f, 35f)] public float frequency = 14f;
        [Range(0f, 1f)] public float directionWeight = .75f;
        [Range(0f, .04f)] public float horizontalAmplitude = .011f;
        [Range(0f, .04f)] public float verticalAmplitude = .007f;
        [Range(0f, .04f)] public float maxViewportFraction = .014f;
        public AnimationCurve envelope = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }

    internal sealed class SideViewCameraShake
    {
        private const int Capacity = 8;
        private readonly Impact[] active = new Impact[Capacity];
        private int count;

        private struct Impact
        {
            public CombatImpactShakeRequest request;
            public float age;
        }

        internal void Request(CombatImpactShakeRequest request, SideViewCameraShakeSettings settings)
        {
            if (!settings.enabled || settings.intensity <= 0f || !(request.Strength > 0f) || float.IsInfinity(request.Strength)) return;
            int weakest = 0;
            float weakestWeight = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var previous = active[i];
                // 同一动作的一次发射可以击中多个对象，延用冲击时钟并取最强请求。
                if (previous.request.ActionId == request.ActionId && previous.request.ParentExecutionId == request.ParentExecutionId &&
                    previous.request.EmissionIndex == request.EmissionIndex && previous.request.ActorId == request.ActorId)
                {
                    if (request.Strength > previous.request.Strength) active[i].request = request;
                    return;
                }
                float weight = Weight(previous, settings);
                if (weight < weakestWeight) { weakestWeight = weight; weakest = i; }
            }
            var impact = new Impact { request = request };
            if (count < Capacity) active[count++] = impact;
            else if (request.Strength > weakestWeight) active[weakest] = impact;
        }

        internal Vector2 Tick(float delta, float viewportHeight, SideViewCameraShakeSettings settings)
        {
            if (!settings.enabled || settings.intensity <= 0f) { Clear(); return Vector2.zero; }
            var normalized = Vector2.zero;
            float duration = Mathf.Max(.02f, settings.duration);
            for (int i = count - 1; i >= 0; i--)
            {
                var item = active[i];
                if (item.age >= duration)
                {
                    active[i] = active[--count];
                    active[count] = default;
                    continue;
                }
                var direction = ((Vector2)item.request.WorldDirection).normalized;
                if (direction == Vector2.zero) direction = Vector2.right;
                float phase = item.age * Mathf.Max(1f, settings.frequency) * (2f * Mathf.PI);
                var perpendicular = new Vector2(-direction.y, direction.x);
                var oscillation = direction * Mathf.Cos(phase) + perpendicular * (.38f * Mathf.Sin(phase));
                var wave = Vector2.Lerp(new Vector2(Mathf.Cos(phase), Mathf.Sin(phase) * .38f), oscillation, settings.directionWeight);
                normalized += Vector2.Scale(wave, new Vector2(settings.horizontalAmplitude, settings.verticalAmplitude)) * Weight(item, settings);
                active[i].age += Mathf.Max(0f, delta);
            }
            return Vector2.ClampMagnitude(normalized, Mathf.Max(0f, settings.maxViewportFraction)) *
                (Mathf.Max(0f, viewportHeight) * Mathf.Clamp01(settings.intensity));
        }

        private static float Weight(Impact item, SideViewCameraShakeSettings settings)
        {
            float t = Mathf.Clamp01(item.age / Mathf.Max(.02f, settings.duration));
            float envelope = settings.envelope == null ? 1f - t : Mathf.Clamp01(settings.envelope.Evaluate(t));
            return envelope * Mathf.Clamp01(item.request.Strength);
        }

        internal void Clear()
        {
            Array.Clear(active, 0, count);
            count = 0;
        }
    }
}
