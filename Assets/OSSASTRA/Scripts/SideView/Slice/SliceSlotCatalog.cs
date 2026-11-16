/*
 * 脚本：SliceSlotCatalog
 * 功能：固定槽位的只读预览和显式复制/删除；预览从 Commerce 主快照提取进度。
 * 边界：不自动使用备份，不替代进场后 CommerceService 的完整领域恢复校验。
 */
using System;
using System.Linq;
using Ossastra.Commerce.Persistence;
using Ossastra.Commerce.Runtime;

namespace Ossastra.SideView.Slice
{
    public enum SliceSlotState { Empty, Loadable, Invalid }

    public sealed class SliceSlotPreview
    {
        public string ProfileId { get; internal set; }
        public SliceSlotState State { get; internal set; }
        public string CheckpointMapId { get; internal set; }
        public string CheckpointAnchorId { get; internal set; }
        public string ActiveActorId { get; internal set; }
        public bool IsCompleted { get; internal set; }
        public bool BackupAvailable { get; internal set; }
        public string Error { get; internal set; }
    }

    public sealed class SliceSlotCatalog
    {
        public static readonly string[] SlotIds = Enumerable.Range(1, 8)
            .Select(n => "slice-slot-" + n.ToString("00")).ToArray();
        private readonly CommerceFileSaveStore store;
        private readonly Func<string, string, bool> knownAnchor;
        public string LastError { get; private set; }

        public SliceSlotCatalog(CommerceFileSaveStore store, Func<string, string, bool> knownAnchor)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.knownAnchor = knownAnchor ?? throw new ArgumentNullException(nameof(knownAnchor));
        }

        public static bool IsSlotId(string profileId)
            => SlotIds.Contains(profileId, StringComparer.Ordinal);

        public SliceSlotPreview Inspect(string profileId)
        {
            if (!IsSlotId(profileId)) throw new ArgumentException("槽位 ID 不在本切片目录", nameof(profileId));
            SliceProgressState progress = null;
            var result = store.Load(profileId, snapshot => Validate(snapshot, out progress));
            var preview = new SliceSlotPreview
            { ProfileId = profileId, BackupAvailable = result.BackupAvailable };
            if (result.Status == CommerceSaveLoadStatus.Missing)
            {
                preview.State = result.BackupAvailable ? SliceSlotState.Invalid : SliceSlotState.Empty;
                preview.Error = result.BackupAvailable ? "主档不存在；备份需显式恢复" : null;
            }
            else if (result.Status == CommerceSaveLoadStatus.Invalid)
            {
                preview.State = SliceSlotState.Invalid;
                preview.Error = result.Error;
            }
            else
            {
                preview.State = SliceSlotState.Loadable;
                preview.CheckpointMapId = progress.CheckpointMapId;
                preview.CheckpointAnchorId = progress.CheckpointAnchorId;
                preview.ActiveActorId = progress.ActiveActorId;
                preview.IsCompleted = progress.IsCompleted;
            }
            return preview;
        }

        public SliceSlotPreview[] InspectAll() => SlotIds.Select(Inspect).ToArray();

        public bool Copy(string sourceProfileId, string destinationProfileId)
        {
            LastError = null;
            try
            {
                if (Inspect(sourceProfileId).State != SliceSlotState.Loadable
                    || Inspect(destinationProfileId).State != SliceSlotState.Empty)
                    throw new InvalidOperationException("源槽不可读取或目标槽已占用");
                store.CopyProfile(sourceProfileId, destinationProfileId,
                    snapshot => Validate(snapshot, out _));
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        public bool Delete(string profileId)
        {
            LastError = null;
            try
            {
                if (!IsSlotId(profileId)) throw new ArgumentException("槽位 ID 不在本切片目录");
                return store.DeleteProfile(profileId);
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        private string Validate(CommerceSaveSnapshot snapshot, out SliceProgressState progress)
        {
            progress = null;
            try
            {
                if (snapshot.PayloadSchema != 2 || string.IsNullOrWhiteSpace(snapshot.DefinitionFingerprint))
                    throw new ArgumentException("存档载荷格式或定义指纹不兼容");
                var aggregate = CommerceSnapshotCodec.Decode(snapshot.Payload);
                if (aggregate == null || aggregate.schema != 2 || aggregate.revision != snapshot.StateRevision
                    || aggregate.economy == null || aggregate.inventoryRuntime == null
                    || aggregate.ownerIds == null || aggregate.sections == null)
                    throw new ArgumentException("存档聚合字段缺失或修订不匹配");
                var sections = aggregate.sections.Where(s => s != null && s.key == SliceProgressState.SectionKey).ToArray();
                if (sections.Length != 1 || sections[0].schema != 1)
                    throw new ArgumentException("存档缺少唯一的切片进度 section");
                progress = SliceProgressState.FromJson(sections[0].payload, aggregate.ownerIds);
                if (!knownAnchor(progress.CheckpointMapId, progress.CheckpointAnchorId))
                    throw new ArgumentException("存档检查点不在场景目录");
                return null;
            }
            catch (Exception ex) { progress = null; return ex.Message; }
        }
    }
}
