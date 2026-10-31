/* 脚本：WorldPropsRoot
 * 功能：场景物领域组装根与同经济快照参与者；保存跨区域状态，有界推进奖励和活跃反馈。
 * 边界：不复制Current/背包/主时钟，不逐帧扫描静态物件；坏档不按新档播种。
 * 协作：Awake注册参与者，原Inventory.Start完成整体恢复后才绑定受击；奖励通过原Ledger原子发布。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Ossastra.Inventory;
using Ossastra.Characters;
using Ossastra.Commerce.Runtime;
using Ossastra.Audio;
using AudioOwner = Ossastra.Audio.GameAudioRoot;

namespace Ossastra.WorldProps
{
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class WorldPropsRoot : MonoBehaviour, ICommerceSnapshotParticipant
    {
        public string mapId = "worldprops-main";
        public WorldPropDefinitionSO[] definitions = Array.Empty<WorldPropDefinitionSO>();
        public WorldPropRoot[] props = Array.Empty<WorldPropRoot>();
        public CommerceService commerce;
        public InventorySession inventory;
        public CharacterControlCoordinator coordinator;
        public Camera gameplayCamera;
        [Min(1)]
        public int rewardBudget = 8;
        [Min(.1f)]
        public float retrySeconds = 1;
        [Header("仅限制表现计时，不限制真实物件与奖励")]
        [Range(1, 128)]
        public int maximumActiveFeedback = 32;
        public WorldPropStore Store { get; private set; }
        public bool Ready => bound && commerce != null && commerce.Ready && inventory != null && inventory.Ready;
        public string Diagnostic { get; private set; } = "未初始化";
        public int PendingCount => pending.Count;
        public int ActiveFeedbackCount => active.Count;
        public int DeliveredCount { get; private set; }
        public string SnapshotKey => "ossastra.world-props";

        public event Action Changed;
        readonly List<WorldPropView> active = new List<WorldPropView>();
        readonly Queue<WorldPropRoot> pending = new Queue<WorldPropRoot>();
        readonly HashSet<WorldPropRoot> queued = new HashSet<WorldPropRoot>();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        WorldPropDefinition[] frozen;
        bool bound, registered;
        float nextRetry;
        float lastYaw = float.NaN;
        SfxSourceHandle audioSource;
        [Serializable]
        sealed class Payload
        {
            public int schema = 1;
            public string[] visitedMaps;
            public WorldPropRecord[] records;
        }

        sealed class Candidate
        {
            public WorldPropStore store;
            public HashSet<string> visited;
        }

        void Awake()
        {
            try
            {
                if (commerce == null || inventory == null || coordinator == null || mapId != commerce.mapId || props == null || definitions == null || props.Any(p => p == null || p.definition == null || string.IsNullOrWhiteSpace(p.instanceId)) || props.Select(p => p.instanceId).Distinct().Count() != props.Length)
                    throw new ArgumentException("场景物根依赖/地图/稳定ID缺失或重复");
                frozen = definitions.Select(d => d == null ? throw new ArgumentException("定义缺失") : d.Freeze()).ToArray();
                Store = new WorldPropStore();
                foreach (var d in frozen)
                    Store.Register(d);
                AttachCurrent(Store, visited);
                Store.Changed += Notify;
                commerce.RegisterSnapshotParticipant(this);
                registered = true;
                Diagnostic = "等待原库存同快照恢复";
            }
            catch (Exception ex)
            {
                Diagnostic = ex.Message;
                Debug.LogError("[WorldProps] " + Diagnostic, this);
                enabled = false;
            }
        }

        void AttachCurrent(WorldPropStore store, HashSet<string> maps)
        {
            bool wasVisited = maps.Contains(mapId);
            foreach (var prop in props)
            {
                if (wasVisited && store.Get(mapId, prop.instanceId, prop.lifeGeneration) == null)
                    throw new ArgumentException("已访问地图缺物件记录；拒绝静默重生：" + prop.instanceId);
                store.Attach(mapId, prop.instanceId, prop.lifeGeneration, prop.definition.definitionId);
            }

            maps.Add(mapId);
        }

        void Bind()
        {
            var ids = new HashSet<string>(coordinator.Characters.Select(c => c.ActorId), StringComparer.Ordinal);
            foreach (var p in props)
            {
                p.Initialize(this, Store.Get(mapId, p.instanceId, p.lifeGeneration), ids);
                if (p.State.Phase == WorldPropPhase.BreakPending)
                    Enqueue(p);
            }

            bound = true;
            Diagnostic = "就绪：原攻击/拾取/同档保存";
        }

        void Update()
        {
            if (!registered)
                return;
            if (!bound)
            {
                if (commerce.Ready && inventory.Ready)
                    Bind();
                else
                    return;
            }

            if (gameplayCamera != null && (float.IsNaN(lastYaw) || Mathf.Abs(Mathf.DeltaAngle(lastYaw, gameplayCamera.transform.eulerAngles.y)) > .001f))
            {
                lastYaw = gameplayCamera.transform.eulerAngles.y;
                foreach (var p in props)
                    if (p.faceCamera && p.visualRoot != null)
                        p.visualRoot.rotation = Quaternion.Euler(0, lastYaw, 0);
            }

            if ((audioSource == null || audioSource.IsDisposed) && AudioOwner.Instance != null && AudioOwner.Instance.SfxAvailable)
                audioSource = AudioOwner.Instance.RegisterSfxSource("worldprops:" + mapId);
            TrimFeedback(0);
            if (active.Count > 0)
                foreach (var view in active.ToArray())
                {
                    if (view == null)
                    {
                        active.Remove(view);
                        continue;
                    }

                    view.Tick(Time.deltaTime, gameplayCamera);
                    if (!view.IsAnimating)
                        active.Remove(view);
                }

            if (Time.unscaledTime < nextRetry)
                return;
            nextRetry = Time.unscaledTime + retrySeconds;
            int budget = Mathf.Min(pending.Count, Mathf.Clamp(rewardBudget, 1, 64));
            while (budget-- > 0 && pending.Count > 0)
            {
                var prop = pending.Dequeue();
                queued.Remove(prop);
                if (prop == null || prop.State.Phase != WorldPropPhase.BreakPending)
                    continue;
                TryReward(prop);
                if (prop.State.Phase == WorldPropPhase.BreakPending)
                    Enqueue(prop);
            }
        }

        void TryReward(WorldPropRoot prop)
        {
            if (!inventory.CanEditInventory)
            {
                Diagnostic = "保存/库存编辑门关闭；奖励仍待交付";
                return;
            }

            // 空奖励仍提交耗尽回执，不依赖根本不需要使用的落点/支撑面。
            var position = Vector3.zero;
            if (prop.State.Plan.Length > 0 && !WorldPropPlacement.TryPlace(prop, out position, out var error))
            {
                Diagnostic = error;
                return;
            }

            try
            {
                var accepted = prop.State.TryDeliver((id, drops, commit) =>
                {
                    if (drops.Length == 0)
                    {
                        commit();
                        return true;
                    }

                    var grants = drops.Select(d => new InventoryContainerGrant(d.definitionId, d.quantity)).ToArray();
                    var result = inventory.CommitGroundReward("drop:resource:" + id, mapId, position, prop.surfaceId, prop.definition.name, grants, commit);
                    if (!result.Ok)
                        Diagnostic = result.Reason;
                    return result.Ok;
                });
                if (accepted)
                {
                    DeliveredCount++;
                    Diagnostic = "破坏奖励已提交；等待原交互拾取";
                }
            }
            catch (Exception ex)
            {
                Diagnostic = "奖励提交异常：" + ex.Message;
                Debug.LogError(Diagnostic, this);
            }
        }

        public void Enqueue(WorldPropRoot prop)
        {
            if (prop != null && queued.Add(prop))
                pending.Enqueue(prop);
        }

        public void Activate(WorldPropView view)
        {
            if (view == null)
                return;
            active.Remove(view);
            if (!view.IsAnimating)
                return;
            TrimFeedback(1);
            view.Tick(0, gameplayCamera);
            active.Add(view);
        }

        void TrimFeedback(int incoming)
        {
            var limit = Mathf.Clamp(maximumActiveFeedback, 1, 128);
            // 最近命中的反馈优先；被淘汰者必须收尾，不能留下不再Tick的常亮条或歪斜视觉。
            while (active.Count + incoming > limit && active.Count > 0)
            {
                var oldest = active[0];
                active.RemoveAt(0);
                if (oldest != null)
                    oldest.StopFeedback();
            }
        }

        public void PlayFact(WorldPropRoot prop, string fact, string soundEvent)
        {
            if (!string.IsNullOrWhiteSpace(soundEvent))
                audioSource?.Submit(soundEvent, prop.instanceId + ":" + fact, prop.transform.position);
        }

        void Notify()
        {
            var h = Changed;
            if (h == null)
                return;
            foreach (Action callback in h.GetInvocationList())
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Diagnostic = "状态通知失败：" + ex.Message;
                }
        }

        public CommerceSnapshotSection Capture() => new CommerceSnapshotSection
        {
            key = SnapshotKey,
            schema = 1,
            payload = JsonConvert.SerializeObject(new Payload { visitedMaps = visited.OrderBy(x => x, StringComparer.Ordinal).ToArray(), records = Store.Capture() })
        };
        public bool TryPrepareRestore(CommerceSnapshotSection section, CommerceRuntimeSnapshot aggregate, out object prepared, out string error)
        {
            prepared = null;
            error = null;
            try
            {
                if (section == null || section.key != SnapshotKey || section.schema != 1)
                    throw new ArgumentException("缺少兼容场景物section；旧档不自动播种");
                var value = JsonConvert.DeserializeObject<Payload>(section.payload);
                if (value == null || value.schema != 1 || value.visitedMaps == null || value.visitedMaps.Length == 0 || value.records == null || value.records.Length > 100000 || value.visitedMaps.Any(string.IsNullOrWhiteSpace) || value.visitedMaps.Distinct().Count() != value.visitedMaps.Length || value.records.Any(r => r == null || !value.visitedMaps.Contains(r.mapId)))
                    throw new ArgumentException("物件保存结构损坏");
                var candidate = WorldPropStore.Restore(value.records, frozen);
                var maps = new HashSet<string>(value.visitedMaps, StringComparer.Ordinal);
                AttachCurrent(candidate, maps);
                // 双向核对：地面自然物必须有已提交回执；Depleted无容器允许已拾取/合堆/回收。
                foreach (var row in aggregate.inventoryRuntime.worldContainers)
                {
                    if (row == null || row.id == null || !row.id.StartsWith("drop:resource:break:", StringComparison.Ordinal))
                        continue;
                    var state = candidate.States.SingleOrDefault(s => s.BreakId != null && "drop:resource:" + s.BreakId == row.id);
                    if (state == null || state.Phase != WorldPropPhase.Depleted || state.MapId != row.mapId)
                        throw new ArgumentException("自然掉落缺少同批已提交破坏回执；拒绝混合档重发");
                }

                prepared = new Candidate
                {
                    store = candidate,
                    visited = maps
                };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public void PublishRestore(object prepared)
        {
            var candidate = (Candidate)prepared;
            Store.Changed -= Notify;
            Store = candidate.store;
            visited = candidate.visited;
            Store.Changed += Notify;
        }

        void OnDisable()
        {
            bound = false;
            foreach (var view in active)
                if (view != null)
                    view.StopFeedback();
            active.Clear();
            pending.Clear();
            queued.Clear();
            audioSource?.Dispose();
            audioSource = null;
        }

        void OnDestroy()
        {
            if (Store != null)
                Store.Changed -= Notify;
            audioSource?.Dispose();
        }
    }
}
