/*
 * 脚本：SfxContracts
 * 功能：集中声明短音请求、准入结果与已占用声部的只读合同。
 * 边界：事实身份来自适配器，合同不判定攻击、伤害或动画，不直接播放声音。
 * 协作：事实适配器 → SfxArbitrator → SfxVoicePlayer；Accepted不等于实际可听。
 */
using UnityEngine;

namespace Ossastra.Audio
{
    /// <summary>短音混音语义；未来语音和环境仅保留路由，不代表本轮已有完整服务。</summary>
    public enum SfxBus { Player, Companion, Enemy, UI, Interaction, Voice, Ambience }

    /// <summary>已发生事实的声音请求；同一动作的不同子击必须提供不同FactKey。</summary>
    public readonly struct SfxRequest
    {
        public string EventId { get; }
        public string SourceId { get; }
        public string WorldScopeId { get; }
        public int SourceGeneration { get; }
        public string FactKey { get; }
        public Vector3 WorldPosition { get; }
        public double OccurredAt { get; }

        public SfxRequest(string eventId, string sourceId, string worldScopeId, int sourceGeneration,
            string factKey, Vector3 worldPosition, double occurredAt)
        {
            EventId = eventId;
            SourceId = sourceId;
            WorldScopeId = worldScopeId;
            SourceGeneration = sourceGeneration;
            FactKey = factKey;
            WorldPosition = worldPosition;
            OccurredAt = occurredAt;
        }
    }

    /// <summary>仲裁分配许可；播放器仍须核对VoiceId是否活跃，再确认实际SourceStarted。</summary>
    public readonly struct SfxDecision
    {
        public bool Accepted { get; }
        public string Reason { get; }
        public long VoiceId { get; }
        public long StolenVoiceId { get; }
        public int ClipIndex { get; }
        public float Pitch { get; }

        internal SfxDecision(bool accepted, string reason, long voiceId = 0, long stolenVoiceId = 0,
            int clipIndex = -1, float pitch = 1)
        {
            Accepted = accepted;
            Reason = reason;
            VoiceId = voiceId;
            StolenVoiceId = stolenVoiceId;
            ClipIndex = clipIndex;
            Pitch = pitch;
        }
    }

    /// <summary>分配到回收之间唯一的一份声部记录；包含尚未交给AudioSource的许可。</summary>
    public sealed class SfxVoiceRecord
    {
        public long VoiceId { get; }
        public SfxRequest Request { get; }
        public SfxEventDefinitionSO Definition { get; }
        public int Priority { get; }
        public int ClipIndex { get; }
        public float Pitch { get; }

        internal SfxVoiceRecord(long voiceId, SfxRequest request, SfxEventDefinitionSO definition,
            int clipIndex, float pitch)
        {
            VoiceId = voiceId;
            Request = request;
            Definition = definition;
            // 许可签发时冻结优先级；编辑器后续改SO不得倒改旧声部的抢占资格。
            Priority = definition.priority;
            ClipIndex = clipIndex;
            Pitch = pitch;
        }
    }
}
