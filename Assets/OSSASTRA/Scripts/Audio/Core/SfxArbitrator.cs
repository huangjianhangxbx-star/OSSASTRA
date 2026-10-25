/*
 * 脚本：SfxArbitrator
 * 功能：纯短音准入策略，拥有世界/来源代次、精确事实去重、作者内容节奏和有界声部预算。
 * 边界：不创建AudioSource、不执行战斗或修改SO；分配许可不代表媒体已经播放。
 * 协作：GameAudioRoot提交事实，VoicePlayer据VoiceId取许可并在结束时Release。
 * 关键约束：本轮短音抢占立即Stop，不存在预算之外的淡出源；软预算触发抢占，硬预算绝不可突破。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Audio
{
    public sealed class SfxArbitrator
    {
        private const int recentLimit = 1024;
        private const int contentLimit = 1024;
        private const double recentSeconds = 2;
        private readonly Dictionary<long, SfxVoiceRecord> active = new Dictionary<long, SfxVoiceRecord>();
        private readonly Dictionary<string, int> sources = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<FactIdentity, double> recent = new Dictionary<FactIdentity, double>();
        private readonly Queue<RecentFact> recentOrder = new Queue<RecentFact>();
        private readonly Dictionary<ContentIdentity, LinkedListNode<ContentSample>> contentHistory = new Dictionary<ContentIdentity, LinkedListNode<ContentSample>>();
        private readonly LinkedList<ContentSample> contentOrder = new LinkedList<ContentSample>();
        private readonly Func<double> random;
        private readonly System.Random defaultRandom = new System.Random();
        private int nextGeneration;
        private long nextVoice;

        public string WorldScopeId { get; private set; } = string.Empty;
        public int SoftLimit { get; }
        public int HardLimit { get; }
        public int ActiveCount => active.Count;
        public int RecentFactCount => recent.Count;
        public int ContentHistoryCount => contentHistory.Count;
        public IReadOnlyCollection<SfxVoiceRecord> ActiveVoices => active.Values;

        public SfxArbitrator(int softLimit = 24, int hardLimit = 32, Func<double> randomFunc = null)
        {
            if (softLimit <= 0 || hardLimit < softLimit) throw new ArgumentOutOfRangeException(nameof(softLimit));
            SoftLimit = softLimit;
            HardLimit = hardLimit;
            random = randomFunc ?? new Func<double>(defaultRandom.NextDouble);
        }

        /// <summary>换世界会撤销所有短音许可；不能像BGM已开始内容那样跨世界保留旧短音。</summary>
        public void SetWorldScope(string worldScopeId)
        {
            WorldScopeId = worldScopeId ?? string.Empty;
            active.Clear();
            sources.Clear();
            recent.Clear();
            recentOrder.Clear();
            contentHistory.Clear();
            contentOrder.Clear();
        }

        /// <summary>来源重新注册获得全新代次，旧异步请求/旧关闭通知无法影响新来源。</summary>
        public int OpenSource(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("来源ID不能为空", nameof(sourceId));
            if (string.IsNullOrEmpty(WorldScopeId)) throw new InvalidOperationException("尚无有效音声世界作用域");
            if (sources.TryGetValue(sourceId, out int oldGeneration)) StopSource(sourceId, oldGeneration);
            if (nextGeneration == int.MaxValue) throw new InvalidOperationException("来源代次已耗尽");
            int generation = ++nextGeneration;
            sources[sourceId] = generation;
            return generation;
        }

        public void CloseSource(string sourceId, int sourceGeneration)
        {
            if (sourceId == null || !sources.TryGetValue(sourceId, out int current) || current != sourceGeneration) return;
            StopSource(sourceId, sourceGeneration);
            sources.Remove(sourceId);
        }

        public void StopSource(string sourceId, int sourceGeneration)
        {
            var ids = new List<long>();
            foreach (SfxVoiceRecord voice in active.Values)
                if (voice.Request.SourceId == sourceId && voice.Request.SourceGeneration == sourceGeneration) ids.Add(voice.VoiceId);
            foreach (long id in ids) active.Remove(id);
            // 停止/重建来源同时撤销其内容历史；旧代次关闭不能清掉新代次的选择状态。
            var identities = new List<ContentIdentity>();
            foreach (ContentIdentity identity in contentHistory.Keys)
                if (identity.IsSource(sourceId, sourceGeneration)) identities.Add(identity);
            foreach (ContentIdentity identity in identities)
            {
                contentOrder.Remove(contentHistory[identity]);
                contentHistory.Remove(identity);
            }
        }

        public void StopWorld() => SetWorldScope(string.Empty);
        public bool TryGetVoice(long voiceId, out SfxVoiceRecord voice) => active.TryGetValue(voiceId, out voice);
        public bool Release(long voiceId) => active.Remove(voiceId);

        public SfxDecision Submit(SfxRequest request, SfxEventDefinitionSO definition, double now)
        {
            if (!Finite(now) || now < 0 || !Finite(request.OccurredAt) || request.OccurredAt < 0 ||
                request.OccurredAt > now || !Finite(request.WorldPosition.x) || !Finite(request.WorldPosition.y) || !Finite(request.WorldPosition.z))
                return Reject("INVALID_REQUEST_TIME_OR_POSITION");
            if (string.IsNullOrWhiteSpace(request.EventId) || string.IsNullOrWhiteSpace(request.SourceId) || string.IsNullOrWhiteSpace(request.FactKey))
                return Reject("INVALID_REQUEST_IDENTITY");
            if (string.IsNullOrEmpty(WorldScopeId) || !string.Equals(WorldScopeId, request.WorldScopeId, StringComparison.Ordinal))
                return Reject("WRONG_WORLD");
            if (!sources.TryGetValue(request.SourceId, out int generation) || generation != request.SourceGeneration)
                return Reject("STALE_SOURCE");
            if (definition == null) return Reject("ASSET_BLOCKED_NO_DEFINITION");
            if (!definition.TryValidate(out string reason)) return Reject(reason);
            if (!string.Equals(request.EventId, definition.eventId, StringComparison.Ordinal)) return Reject("EVENT_DEFINITION_MISMATCH");
            if (now - request.OccurredAt > definition.maxAgeSeconds) return Reject("EXPIRED");

            TrimRecent(now);
            var fact = new FactIdentity(request);
            if (recent.ContainsKey(fact)) return Reject("DUPLICATE_FACT");
            foreach (SfxVoiceRecord voice in active.Values)
                if (new FactIdentity(voice.Request).Equals(fact)) return Reject("DUPLICATE_FACT");

            var content = new ContentIdentity(request);
            contentHistory.TryGetValue(content, out LinkedListNode<ContentSample> previous);
            // 内容节奏是作者显式选择的听感限制，默认关闭；不同来源/事件和精确事实身份互不代替。
            if (previous != null && now - previous.Value.AcceptedAt < definition.minIntervalSeconds)
                return Reject("CONTENT_INTERVAL");

            int sourceCount = 0;
            SfxVoiceRecord sourceVictim = null;
            SfxVoiceRecord globalVictim = null;
            foreach (SfxVoiceRecord voice in active.Values)
            {
                if (LowerThan(voice, globalVictim)) globalVictim = voice;
                if (voice.Request.SourceId != request.SourceId || voice.Request.SourceGeneration != request.SourceGeneration) continue;
                sourceCount++;
                if (LowerThan(voice, sourceVictim)) sourceVictim = voice;
            }

            // 同源上限先约束该来源；不能靠抢其它角色的声音绕过本来源并发。
            SfxVoiceRecord stolen = null;
            if (sourceCount >= definition.maxPerSource)
            {
                // 新事件上限更小时，单次替换无法恢复合法数量；拒绝新声，不批量杀掉其它合法旧声。
                if (sourceCount > definition.maxPerSource) return Reject("SOURCE_LIMIT");
                if (sourceVictim == null || sourceVictim.Priority >= definition.priority) return Reject("SOURCE_LIMIT");
                stolen = sourceVictim;
            }
            else if (active.Count >= SoftLimit || active.Count >= HardLimit)
            {
                if (globalVictim == null || globalVictim.Priority >= definition.priority) return Reject("VOICE_BUDGET");
                stolen = globalVictim;
            }

            if (stolen == null && active.Count >= HardLimit) return Reject("HARD_VOICE_BUDGET");
            int clipCount = definition.clips.Length;
            int previousIndex = previous == null ? -1 : previous.Value.ClipIndex;
            bool excludePrevious = definition.avoidImmediateRepeat && clipCount > 1 && previousIndex >= 0 && previousIndex < clipCount;
            int choiceCount = excludePrevious ? clipCount - 1 : clipCount;
            int clipIndex = Mathf.Min(choiceCount - 1, (int)(Sample() * choiceCount));
            // 从N-1个候选直接映射回原数组，固定一次抽样；避免恒定随机源导致重抽死循环。
            if (excludePrevious && clipIndex >= previousIndex) clipIndex++;
            float pitch = Mathf.Lerp(definition.pitchMin, definition.pitchMax, (float)Sample());
            if (stolen != null) active.Remove(stolen.VoiceId);
            long id = ++nextVoice;
            active.Add(id, new SfxVoiceRecord(id, request, definition, clipIndex, pitch));
            recent[fact] = now;
            recentOrder.Enqueue(new RecentFact(fact, now));
            RememberContent(content, previous, clipIndex, now);
            TrimRecent(now);
            return new SfxDecision(true, stolen == null ? "ACCEPTED" : "ACCEPTED_STOLE_LOWER_PRIORITY", id, stolen?.VoiceId ?? 0, clipIndex, pitch);
        }

        private double Sample()
        {
            double value = random();
            return Finite(value) ? Math.Max(0, Math.Min(.999999999999, value)) : 0;
        }

        private void TrimRecent(double now)
        {
            while (recentOrder.Count > 0 && (recent.Count > recentLimit || now - recentOrder.Peek().Time > recentSeconds))
            {
                RecentFact oldest = recentOrder.Dequeue();
                if (recent.TryGetValue(oldest.Fact, out double time) && time == oldest.Time) recent.Remove(oldest.Fact);
            }
        }

        private void RememberContent(ContentIdentity identity, LinkedListNode<ContentSample> previous, int clipIndex, double now)
        {
            // 只有成功分配许可才推进片段和间隔；拒绝/缺媒体/预算不足均不消费下一次机会。
            if (previous != null) contentOrder.Remove(previous);
            var node = contentOrder.AddLast(new ContentSample(identity, clipIndex, now));
            contentHistory[identity] = node;
            if (contentHistory.Count <= contentLimit) return;
            LinkedListNode<ContentSample> oldest = contentOrder.First;
            contentHistory.Remove(oldest.Value.Identity);
            contentOrder.RemoveFirst();
        }

        private static bool LowerThan(SfxVoiceRecord candidate, SfxVoiceRecord other) =>
            other == null || candidate.Priority < other.Priority ||
            candidate.Priority == other.Priority && candidate.VoiceId < other.VoiceId;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static SfxDecision Reject(string reason) => new SfxDecision(false, reason);

        private readonly struct RecentFact
        {
            public FactIdentity Fact { get; }
            public double Time { get; }
            public RecentFact(FactIdentity fact, double time) { Fact = fact; Time = time; }
        }

        private readonly struct ContentSample
        {
            public ContentIdentity Identity { get; }
            public int ClipIndex { get; }
            public double AcceptedAt { get; }
            public ContentSample(ContentIdentity identity, int clipIndex, double acceptedAt)
            { Identity = identity; ClipIndex = clipIndex; AcceptedAt = acceptedAt; }
        }

        // 世界切换统一清空；来源代次+事件是内容历史的作用域，合法子击依然拥有各自FactKey。
        private readonly struct ContentIdentity : IEquatable<ContentIdentity>
        {
            private readonly string source, eventId;
            private readonly int generation;
            public ContentIdentity(SfxRequest request)
            { source = request.SourceId; eventId = request.EventId; generation = request.SourceGeneration; }
            public bool IsSource(string sourceId, int sourceGeneration) => source == sourceId && generation == sourceGeneration;
            public bool Equals(ContentIdentity other) => generation == other.generation && source == other.source && eventId == other.eventId;
            public override bool Equals(object other) => other is ContentIdentity identity && Equals(identity);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = generation;
                    hash = hash * 397 ^ (source?.GetHashCode() ?? 0);
                    return hash * 397 ^ (eventId?.GetHashCode() ?? 0);
                }
            }
        }

        // 直接比较各身份字段，避免用分隔符拼接造成带特殊字符的合法ID碰撞。
        private readonly struct FactIdentity : IEquatable<FactIdentity>
        {
            private readonly string world, source, eventId, fact;
            private readonly int generation;
            public FactIdentity(SfxRequest request)
            {
                world = request.WorldScopeId; source = request.SourceId; eventId = request.EventId;
                fact = request.FactKey; generation = request.SourceGeneration;
            }
            public bool Equals(FactIdentity other) => generation == other.generation && world == other.world && source == other.source && eventId == other.eventId && fact == other.fact;
            public override bool Equals(object other) => other is FactIdentity identity && Equals(identity);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = generation;
                    hash = hash * 397 ^ (world?.GetHashCode() ?? 0);
                    hash = hash * 397 ^ (source?.GetHashCode() ?? 0);
                    hash = hash * 397 ^ (eventId?.GetHashCode() ?? 0);
                    return hash * 397 ^ (fact?.GetHashCode() ?? 0);
                }
            }
        }
    }
}
