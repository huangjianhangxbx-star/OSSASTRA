/*
 * 功能：只由当前存活主控解析活动房间；角色身份变化与显式立即解析绕过门槛滞回。
 * 边界：不拥有Unity对象、空间几何、角色控制、时间、显示或存档状态。
 * 协作：运行适配层登记稳定roomId并传入脚点采样；ActiveRoomId供本相机房间呈现使用。
 */
using System;
using System.Collections.Generic;

namespace Ossastra.SceneVisibility.Rooms.Core
{
    public sealed class RoomMembershipResolver
    {
        private Dictionary<string, int> registeredIndices;
        private string[] registeredIds;
        private RoomCandidate[] samples;
        private int[] sampleStamps;
        private int sampleStamp;
        private string previousActorId;
        private bool hadLivingActor;

        public string ActiveRoomId { get; private set; }

        public RoomMembershipResolver(IReadOnlyList<string> registeredRoomIds)
        {
            SetRegisteredRooms(registeredRoomIds);
        }

        /// <summary>
        /// 在初始化或显式重接时登记房间；拒绝空/重复ID，失败不改变旧登记或活动域。
        /// 删除当前活动域会立即清空归属，即使当前没有存活主控也不会保留悬空引用。
        /// </summary>
        public void SetRegisteredRooms(IReadOnlyList<string> roomIds)
        {
            if (roomIds == null) throw new ArgumentNullException(nameof(roomIds));

            // 先独立验证新登记，再一次性提交；运行中不分配用于检查ID的临时集合。
            var nextIndices = new Dictionary<string, int>(roomIds.Count, StringComparer.Ordinal);
            var nextIds = new string[roomIds.Count];
            for (int i = 0; i < roomIds.Count; ++i)
            {
                string id = roomIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("Room ID must not be empty.", nameof(roomIds));
                if (nextIndices.ContainsKey(id))
                    throw new ArgumentException("Duplicate room ID: " + id, nameof(roomIds));
                nextIndices.Add(id, i);
                nextIds[i] = id;
            }

            registeredIndices = nextIndices;
            registeredIds = nextIds;
            samples = new RoomCandidate[nextIds.Length];
            sampleStamps = new int[nextIds.Length];
            sampleStamp = 0;
            if (ActiveRoomId != null && !registeredIndices.ContainsKey(ActiveRoomId))
                ActiveRoomId = null;
        }

        /// <summary>
        /// 解析唯一主控的候选快照。身份变化、从无存活主控恢复或forceImmediate使用准确Inside；
        /// 其他帧先沿CanRetain保留原房间，再沿CanEnter选新域。无存活主控时冻结最后合法域。
        /// </summary>
        public string Resolve(string actorId, bool living, IReadOnlyList<RoomCandidate> candidates, bool forceImmediate = false)
        {
            if (!living)
            {
                hadLivingActor = false;
                return ActiveRoomId;
            }
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException("A living actor needs a stable ID.", nameof(actorId));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            int stamp = NextSampleStamp();
            for (int i = 0; i < candidates.Count; ++i)
            {
                RoomCandidate candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate.RoomId))
                    throw new ArgumentException("Candidate room ID must not be empty.", nameof(candidates));
                if (!registeredIndices.TryGetValue(candidate.RoomId, out int index))
                    throw new ArgumentException("Candidate room is not registered: " + candidate.RoomId, nameof(candidates));
                if (sampleStamps[index] == stamp)
                    throw new ArgumentException("Duplicate candidate room ID: " + candidate.RoomId, nameof(candidates));
                samples[index] = candidate;
                sampleStamps[index] = stamp;
            }

            // 只有完整候选快照验证后才提交身份与活动域。错误帧不能污染下一帧的滞回依据。
            bool immediate = forceImmediate || !hadLivingActor ||
                !string.Equals(previousActorId, actorId, StringComparison.Ordinal);
            if (!immediate && ActiveRoomId != null &&
                registeredIndices.TryGetValue(ActiveRoomId, out int currentIndex) &&
                sampleStamps[currentIndex] == stamp && samples[currentIndex].CanRetain)
                return ActiveRoomId;

            int bestIndex = -1;
            for (int i = 0; i < samples.Length; ++i)
            {
                if (sampleStamps[i] != stamp || !(immediate ? samples[i].Inside : samples[i].CanEnter))
                    continue;
                if (bestIndex < 0 || samples[i].Priority > samples[bestIndex].Priority ||
                    (samples[i].Priority == samples[bestIndex].Priority &&
                     string.CompareOrdinal(registeredIds[i], registeredIds[bestIndex]) < 0))
                    bestIndex = i;
            }

            ActiveRoomId = bestIndex < 0 ? null : registeredIds[bestIndex];
            previousActorId = actorId;
            hadLivingActor = true;
            return ActiveRoomId;
        }

        private int NextSampleStamp()
        {
            // stamp替代逐帧HashSet.Clear/new；溢出时只清本解析器自己的索引缓存。
            if (sampleStamp == int.MaxValue)
            {
                Array.Clear(sampleStamps, 0, sampleStamps.Length);
                sampleStamp = 0;
            }
            return ++sampleStamp;
        }
    }
}
