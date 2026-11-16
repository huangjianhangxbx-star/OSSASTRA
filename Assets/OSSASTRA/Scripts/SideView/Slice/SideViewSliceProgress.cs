/*
 * 脚本：SideViewSliceProgress
 * 功能：把检查点、当前主控和 Boss 一次性事实注册到原 Commerce 聚合快照。
 * 时序：Awake 注册；InventorySession.Start 统一恢复；Start 仅消费标题 Continue 落点。
 */
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.Commerce.Runtime;
using Ossastra.SideViewWorld;

namespace Ossastra.SideView.Slice
{
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class SideViewSliceProgress : MonoBehaviour, ICommerceSnapshotParticipant
    {
        public const string SectionKey = SliceProgressState.SectionKey;
        public CommerceService commerce;
        public CharacterControlCoordinator party;
        public SideViewSliceSessionFlow sessionFlow;
        public string mapId;
        public string initialAnchorId;
        public SideViewSliceAnchor[] anchors = Array.Empty<SideViewSliceAnchor>();
        public string bossId;

        private SliceProgressState state;
        private bool registered;
        public string SnapshotKey => SectionKey;
        public bool Ready => registered && commerce != null && commerce.Ready && party != null
            && string.Equals(mapId, commerce.mapId, StringComparison.Ordinal);
        public string Diagnostic { get; private set; } = "等待进度恢复";
        public string CheckpointMapId => state?.CheckpointMapId;
        public string CheckpointAnchorId => state?.CheckpointAnchorId;
        public string ActiveActorId => state?.ActiveActorId;
        public bool IsBossDefeated => state != null && state.IsBossDefeated(bossId);
        public bool IsCompleted => state != null && state.IsCompleted;
        public event Action Changed;

        private void Awake()
        {
            try
            {
                if (commerce == null || party == null || string.IsNullOrWhiteSpace(mapId)
                    || mapId != commerce.mapId || anchors == null || anchors.Length == 0
                    || anchors.Any(a => a == null || string.IsNullOrWhiteSpace(a.anchorId))
                    || anchors.Select(a => a.anchorId).Distinct(StringComparer.Ordinal).Count() != anchors.Length
                    || !anchors.Any(a => a.anchorId == initialAnchorId))
                    throw new ArgumentException("进度根缺少 Commerce、地图身份或稳定安全锚点");
                var initialActor = party.Current != null ? party.Current.ActorId
                    : party.Characters.FirstOrDefault()?.ActorId;
                var initial = anchors.Single(a => a.anchorId == initialAnchorId);
                state = SliceProgressState.New(mapId, initialAnchorId, initialActor,
                    initial.facingX < 0 ? -1 : 1);
                commerce.RegisterSnapshotParticipant(this);
                registered = true;
                Diagnostic = "等待共同快照恢复";
            }
            catch (Exception ex)
            {
                Diagnostic = ex.Message;
                Debug.LogError("[SV-SLICE-01] " + Diagnostic, this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (sessionFlow == null) sessionFlow = SideViewSliceSessionFlow.Instance;
            if (Ready && sessionFlow != null && sessionFlow.HasPendingResumeFor(mapId))
                TryApplyCheckpoint();
        }

        public bool HasAnchor(string anchorId)
            => anchors != null && anchors.Any(a => a != null && a.anchorId == anchorId);

        /// <summary>同步记录领域事实；椅子成功提示须另外等待原 Commerce 保存屏障。</summary>
        public bool RecordCheckpoint(string anchorId)
        {
            if (!CanMutate() || !HasAnchor(anchorId)) return false;
            var actor = party.Current;
            if (actor == null) return false;
            int facing = actor.GetComponent<CharacterOrientation>()?.VisualFacingX < 0 ? -1 : 1;
            state.RecordCheckpoint(mapId, anchorId, actor.ActorId, facing);
            Changed?.Invoke();
            commerce.RequestSave();
            Diagnostic = "检查点已记录，等待保存：" + anchorId;
            return true;
        }

        public async Task<bool> SaveCheckpointAsync(string anchorId)
        {
            if (!RecordCheckpoint(anchorId)) return false;
            bool saved = await commerce.FlushBeforeSceneChangeAsync();
            Diagnostic = saved ? "检查点已保存：" + anchorId : "检查点仍在内存；保存失败，可重试";
            return saved;
        }

        public bool MarkBossDefeated(string defeatedBossId)
        {
            if (!CanRecordWorldFact() || string.IsNullOrWhiteSpace(bossId) || defeatedBossId != bossId)
                return false;
            if (state.MarkBossDefeated(defeatedBossId))
            {
                Changed?.Invoke();
                commerce.RequestSave();
            }
            Diagnostic = "Boss 击杀事实已记录：" + defeatedBossId;
            return true;
        }

        public bool FinishBossAndReturn()
        {
            if (!CanRecordWorldFact() || !IsBossDefeated || state.IsCompleted
                || sessionFlow == null || !sessionFlow.CanRequestReturnToTitle) return false;
            if (!state.MarkCompleted(bossId)) return false;
            Changed?.Invoke();
            commerce.RequestSave();
            return sessionFlow.RequestReturnToTitle("boss-complete");
        }

        public bool FinishBossAndReturn(string defeatedBossId)
        {
            if (sessionFlow == null || !sessionFlow.CanRequestReturnToTitle) return false;
            return MarkBossDefeated(defeatedBossId) && FinishBossAndReturn();
        }

        private bool CanMutate() => CanRecordWorldFact() && commerce.SaveStatus.CanEdit;

        private bool CanRecordWorldFact() => Ready && state != null && commerce.SaveStatus != null
            && !commerce.SaveStatus.IsClosed
            && (SideViewTransitionRuntime.Instance == null || !SideViewTransitionRuntime.Instance.Busy);

        /// <summary>只在标题 Continue 的一次性落点申请下生效；普通门的目标 Start 不移动玩家。</summary>
        public bool TryApplyCheckpoint()
        {
            if (!Ready || state == null || sessionFlow == null
                || !sessionFlow.HasPendingResumeFor(mapId)) return false;
            if (state.CheckpointMapId != mapId || party.Characters.Count != 2
                || !HasAnchor(state.CheckpointAnchorId)
                || !party.Characters.Any(c => c != null && c.ActorId == state.ActiveActorId))
            {
                Diagnostic = "读档锚点与当前场景或角色不匹配";
                return false;
            }
            var anchor = anchors.Single(a => a.anchorId == state.CheckpointAnchorId);
            var first = party.Characters[0];
            var second = party.Characters[1];
            if (first == null || second == null
                || first.GetComponent<CharacterMovement>() == null
                || second.GetComponent<CharacterMovement>() == null
                || first.GetComponent<CharacterOrientation>() == null
                || second.GetComponent<CharacterOrientation>() == null)
            {
                Diagnostic = "读档角色缺少运动或朝向组件";
                return false;
            }
            if (!party.TryRestoreControl(state.ActiveActorId))
            {
                Diagnostic = "读档主控角色不可恢复";
                return false;
            }
            first.GetComponent<CharacterMovement>().Teleport(anchor.firstActorPosition);
            second.GetComponent<CharacterMovement>().Teleport(anchor.secondActorPosition);
            var direction = Vector3.right * state.FacingX;
            first.GetComponent<CharacterOrientation>().InitializeArrivalFacing(direction);
            second.GetComponent<CharacterOrientation>().InitializeArrivalFacing(direction);
            Physics.SyncTransforms();
            sessionFlow.ConfirmResumeApplied(this);
            Diagnostic = "已从检查点恢复：" + state.CheckpointAnchorId;
            return true;
        }

        public CommerceSnapshotSection Capture()
        {
            if (state == null) throw new InvalidOperationException("进度尚未初始化");
            var flow = sessionFlow != null ? sessionFlow : SideViewSliceSessionFlow.Instance;
            // Inventory.Start issues an early save before Start applies the Continue checkpoint.
            // Preserve the saved actor ID until that one-shot handoff is complete.
            if (party?.Current != null && (flow == null || !flow.HasPendingResumeFor(mapId)))
                state.SetActiveActor(party.Current.ActorId);
            return new CommerceSnapshotSection { key = SectionKey, schema = 1, payload = state.ToJson() };
        }

        public bool TryPrepareRestore(CommerceSnapshotSection section, CommerceRuntimeSnapshot aggregate,
            out object prepared, out string error)
        {
            prepared = null;
            error = null;
            try
            {
                if (section == null || section.key != SectionKey || section.schema != 1
                    || aggregate?.ownerIds == null)
                    throw new ArgumentException("进度 section 缺失或版本不兼容");
                prepared = SliceProgressState.FromJson(section.payload, aggregate.ownerIds);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public void PublishRestore(object prepared)
        {
            state = (SliceProgressState)prepared;
            Diagnostic = "进度已从同一快照恢复";
        }
    }
}
