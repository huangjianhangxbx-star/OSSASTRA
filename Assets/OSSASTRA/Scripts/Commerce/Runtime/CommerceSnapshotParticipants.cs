/* 脚本：CommerceSnapshotParticipants
 * 功能：把显式登记领域的值载荷纳入同一经济快照；先准备全部候选，再由宿主同步发布。
 * 边界：不理解场景物规则、不另开存档、不保存活Unity引用；缺失、未知、重复载荷拒绝恢复。
 * 协作：参与者Changed进入既有保存修订；PublishRestore必须只安装准备好的引用，不通知、不抛异常。
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ossastra.Commerce.Runtime
{
    [Serializable]
    public sealed class CommerceSnapshotSection
    {
        public string key;
        public int schema;
        public string payload;
    }

    /// <summary>窄的同快照参与合同；Prepare不改活状态，Publish只切换预分配引用且不得抛异常。</summary>
    public interface ICommerceSnapshotParticipant
    {
        string SnapshotKey { get; }
        event Action Changed;
        CommerceSnapshotSection Capture();
        bool TryPrepareRestore(CommerceSnapshotSection section, CommerceRuntimeSnapshot aggregate, out object prepared, out string error);
        void PublishRestore(object prepared);
    }

    public sealed class CommerceSnapshotParticipants : IDisposable
    {
        const int MaximumSections = 64, MaximumPayloadCharacters = 8 * 1024 * 1024;
        readonly Dictionary<string, ICommerceSnapshotParticipant> participants = new Dictionary<string, ICommerceSnapshotParticipant>(StringComparer.Ordinal);
        bool frozen, disposed;
        public event Action Changed;
        public int ObserverFailureCount { get; private set; }

        public sealed class PreparedRestore
        {
            internal CommerceSnapshotParticipants owner;
            internal ICommerceSnapshotParticipant[] participants;
            internal object[] states;
            internal bool used;
        }

        public void Register(ICommerceSnapshotParticipant participant)
        {
            if (disposed || frozen) throw new InvalidOperationException("快照领域登记已关闭；必须在库存恢复开始前登记");
            if (participant == null || !ValidKey(participant.SnapshotKey) || participants.Count >= MaximumSections || participants.ContainsKey(participant.SnapshotKey))
                throw new ArgumentException("快照领域身份缺失、重复或超出预算");
            participants.Add(participant.SnapshotKey, participant); participant.Changed += NotifyChanged;
        }
        public void FreezeRegistration() { frozen = true; }
        static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 128 && !key.Any(char.IsWhiteSpace);
        static bool ValidSection(CommerceSnapshotSection section) => section != null && ValidKey(section.key) && section.schema > 0 &&
            section.payload != null && section.payload.Length <= MaximumPayloadCharacters;
        static CommerceSnapshotSection Copy(CommerceSnapshotSection section)
            => new CommerceSnapshotSection { key = section.key, schema = section.schema, payload = section.payload };

        public CommerceSnapshotSection[] Capture()
        {
            if (disposed) throw new InvalidOperationException("快照领域聚合器已释放");
            return participants.OrderBy(p => p.Key, StringComparer.Ordinal).Select(pair =>
            {
                var section = pair.Value.Capture();
                if (!ValidSection(section) || section.key != pair.Key || pair.Value.SnapshotKey != pair.Key)
                    throw new InvalidOperationException("领域快照无效：" + pair.Key);
                return Copy(section);
            }).ToArray();
        }

        public bool TryPrepareRestore(CommerceRuntimeSnapshot snapshot, out PreparedRestore candidate, out string error)
        {
            candidate = null; error = null;
            if (disposed || snapshot?.sections == null || snapshot.sections.Length > MaximumSections)
            { error = "领域快照集合缺失或超限"; return false; }
            var byKey = new Dictionary<string, CommerceSnapshotSection>(StringComparer.Ordinal);
            foreach (var section in snapshot.sections)
            {
                if (!ValidSection(section) || byKey.ContainsKey(section.key) || !participants.ContainsKey(section.key))
                { error = "领域快照无效、重复或未登记：" + section?.key; return false; }
                byKey.Add(section.key, Copy(section));
            }
            var owners = participants.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
            var states = new object[owners.Length];
            for (int i = 0; i < owners.Length; ++i)
            {
                var pair = owners[i];
                if (!byKey.TryGetValue(pair.Key, out var section))
                { error = "存档缺少已登记领域，未当新档播种：" + pair.Key; return false; }
                try
                {
                    if (!pair.Value.TryPrepareRestore(section, snapshot, out states[i], out var reason) || states[i] == null)
                    { error = "领域恢复拒绝 " + pair.Key + "：" + reason; return false; }
                }
                catch (Exception ex) { error = "领域恢复损坏 " + pair.Key + "：" + ex.Message; return false; }
            }
            candidate = new PreparedRestore { owner = this, participants = owners.Select(p => p.Value).ToArray(), states = states };
            return true;
        }
        public void PublishRestore(PreparedRestore candidate)
        {
            if (disposed || candidate == null || candidate.owner != this || candidate.used) throw new InvalidOperationException("领域恢复候选缺失或已使用");
            candidate.used = true;
            // 宿主已准备并验证领域、库存和经济；这里不得做解析、随机、I/O或派发通知。
            for (int i = 0; i < candidate.participants.Length; ++i) candidate.participants[i].PublishRestore(candidate.states[i]);
        }
        void NotifyChanged()
        {
            var handlers = Changed; if (handlers == null || disposed) return;
            foreach (Action handler in handlers.GetInvocationList()) try { handler(); } catch { ObserverFailureCount++; }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var participant in participants.Values) participant.Changed -= NotifyChanged;
            participants.Clear(); Changed = null;
        }
    }
}
