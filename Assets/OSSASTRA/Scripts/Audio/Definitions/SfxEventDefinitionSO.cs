/*
 * 脚本：SfxEventDefinitionSO
 * 功能：定义一个短音事件的真实媒体、变体、音量、音高、路由和内容节奏参数。
 * 边界：SO只保存作者配置，不保存运行声部、已播放事实或角色结算状态。
 * 协作：Catalog校验身份，Arbitrator选片与准入，VoicePlayer读取播放参数。
 */
using UnityEngine;

namespace Ossastra.Audio
{
    [CreateAssetMenu(menuName = "OSSASTRA/Audio/SFX Event", fileName = "SfxEvent")]
    public sealed class SfxEventDefinitionSO : ScriptableObject
    {
        [Header("稳定事件身份与媒体变体")]
        public string eventId;
        public AudioClip[] clips;
        [Tooltip("同一来源的同一事件有多个片段时，排除上次已准入片段；单片段事件仍正常播放。")]
        public bool avoidImmediateRepeat = true;
        [Tooltip("作者可为特定高密度声音设置最短间隔；默认0不限制，不替代精确事实去重。")]
        [Min(0)] public float minIntervalSeconds;
        [Header("声音参数；较高优先级可以抢占较低优先级")]
        [Range(0, 1)] public float volume = .3f;
        [Range(.01f, 3)] public float pitchMin = 1;
        [Range(.01f, 3)] public float pitchMax = 1;
        [Range(0, 100)] public int priority = 50;
        [Min(1)] public int maxPerSource = 4;
        [Min(.001f)] public float maxAgeSeconds = .5f;
        [Header("混音与可选空间衰减；默认保留旧2D短音")]
        public SfxBus bus = SfxBus.Player;
        public bool spatial;
        [Tooltip("持续声仍占用同一仲裁池；只能由持有来源租约的环境适配器显式停止。")]
        public bool loop;
        [Tooltip("内容出处与身份置信度，仅供诊断；不参与玩法判定。")]
        public string provenance;
        [Min(.001f)] public float minDistance = 1;
        [Min(.001f)] public float maxDistance = 20;

        /// <summary>拒绝不完整或非有限配置；不会补造声音或在SO写入运行时修复值。</summary>
        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("INVALID_EVENT_ID", out reason);
            if (!System.Enum.IsDefined(typeof(SfxBus), bus)) return Invalid("INVALID_BUS", out reason);
            if (!Finite(volume) || volume < 0 || volume > 1) return Invalid("INVALID_VOLUME", out reason);
            if (!Finite(pitchMin) || !Finite(pitchMax) || pitchMin < .01f || pitchMax < pitchMin || pitchMax > 3)
                return Invalid("INVALID_PITCH", out reason);
            if (priority < 0 || priority > 100) return Invalid("INVALID_PRIORITY", out reason);
            if (maxPerSource <= 0) return Invalid("INVALID_SOURCE_LIMIT", out reason);
            if (!Finite(maxAgeSeconds) || maxAgeSeconds <= 0) return Invalid("INVALID_MAX_AGE", out reason);
            if (!Finite(minIntervalSeconds) || minIntervalSeconds < 0) return Invalid("INVALID_CONTENT_INTERVAL", out reason);
            if (!Finite(minDistance) || !Finite(maxDistance) || minDistance <= 0 || maxDistance < minDistance)
                return Invalid("INVALID_DISTANCE", out reason);
            if (clips == null || clips.Length == 0) return Invalid("ASSET_BLOCKED_NO_CLIPS", out reason);
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null || clips[index].samples <= 0 || clips[index].frequency <= 0 ||
                    clips[index].loadState == AudioDataLoadState.Failed)
                    return Invalid("ASSET_BLOCKED_INVALID_CLIP", out reason);
            }
            reason = "VALID";
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Invalid(string value, out string reason) { reason = value; return false; }
    }
}
