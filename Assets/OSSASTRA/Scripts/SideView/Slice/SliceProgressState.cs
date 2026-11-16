/*
 * 脚本：SliceProgressState
 * 功能：跨地图检查点、当前主控与 Boss 一次性完成事实的纯值状态。
 * 边界：不读场景或文件；JSON 仅作为 Commerce section 的 payload。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ossastra.SideView.Slice
{
    public sealed class SliceProgressState
    {
        public const string SectionKey = "ossastra.sideview.slice-progress";
        [Serializable]
        private sealed class Payload
        {
            public int schema = 1;
            public string checkpointMapId;
            public string checkpointAnchorId;
            public string activeActorId;
            public int facingX = 1;
            public string[] defeatedBossIds = Array.Empty<string>();
            public bool completed;
        }

        private string checkpointMapId, checkpointAnchorId, activeActorId;
        private int facingX;
        private bool completed;
        private readonly HashSet<string> defeatedBossIds = new HashSet<string>(StringComparer.Ordinal);

        public string CheckpointMapId => checkpointMapId;
        public string CheckpointAnchorId => checkpointAnchorId;
        public string ActiveActorId => activeActorId;
        public int FacingX => facingX;
        public bool IsCompleted => completed;
        public string[] DefeatedBossIds => defeatedBossIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        public bool IsBossDefeated(string bossId) => bossId != null && defeatedBossIds.Contains(bossId);

        public static SliceProgressState New(string mapId, string anchorId, string actorId, int facing)
        {
            var state = new SliceProgressState();
            state.RecordCheckpoint(mapId, anchorId, actorId, facing);
            return state;
        }

        public void RecordCheckpoint(string mapId, string anchorId, string actorId, int facing)
        {
            if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(anchorId)
                || string.IsNullOrWhiteSpace(actorId) || mapId.Length > 128 || anchorId.Length > 128
                || actorId.Length > 128 || facing != -1 && facing != 1)
                throw new ArgumentException("检查点地图、锚点、角色或朝向无效");
            checkpointMapId = mapId;
            checkpointAnchorId = anchorId;
            activeActorId = actorId;
            facingX = facing;
        }

        public void SetActiveActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128)
                throw new ArgumentException("当前主控 ID 无效");
            activeActorId = actorId;
        }

        public bool MarkBossDefeated(string bossId)
        {
            if (string.IsNullOrWhiteSpace(bossId) || bossId.Length > 128)
                throw new ArgumentException("Boss ID 无效");
            return defeatedBossIds.Add(bossId);
        }

        public bool MarkCompleted(string bossId)
        {
            if (!IsBossDefeated(bossId) || completed) return false;
            completed = true;
            return true;
        }

        public string ToJson() => JsonConvert.SerializeObject(new Payload
        {
            checkpointMapId = checkpointMapId,
            checkpointAnchorId = checkpointAnchorId,
            activeActorId = activeActorId,
            facingX = facingX,
            defeatedBossIds = DefeatedBossIds,
            completed = completed
        });

        public static SliceProgressState FromJson(string json, IEnumerable<string> ownerActorIds)
        {
            try
            {
                var value = JsonConvert.DeserializeObject<Payload>(json);
                var owners = new HashSet<string>(ownerActorIds ?? Array.Empty<string>(), StringComparer.Ordinal);
                if (value == null || value.schema != 1 || value.defeatedBossIds == null
                    || value.defeatedBossIds.Any(string.IsNullOrWhiteSpace)
                    || value.defeatedBossIds.Any(x => x.Length > 128)
                    || value.defeatedBossIds.Distinct(StringComparer.Ordinal).Count() != value.defeatedBossIds.Length
                    || !owners.Contains(value.activeActorId)
                    || value.completed && value.defeatedBossIds.Length == 0)
                    throw new ArgumentException("进度载荷版本、角色或 Boss 事实无效");
                var state = New(value.checkpointMapId, value.checkpointAnchorId,
                    value.activeActorId, value.facingX);
                foreach (var bossId in value.defeatedBossIds) state.MarkBossDefeated(bossId);
                state.completed = value.completed;
                return state;
            }
            catch (JsonException ex) { throw new ArgumentException("进度载荷 JSON 损坏", ex); }
        }
    }
}
