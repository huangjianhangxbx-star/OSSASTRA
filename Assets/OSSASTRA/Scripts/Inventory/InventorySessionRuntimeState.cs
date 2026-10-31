/* 脚本：InventorySessionRuntimeState
 * 功能：唯一库存的宿主字段快照与多世界容器注册；领域物品/装备仍由Ledger保存。
 * 边界：不存窗口/拖拽令牌；明确世界容器类型与支撑面，旧记录按既有来源迁移，完整验证后才安装全部字段。
 * 协作：Commerce先恢复领域候选，再安装本快照；地图切换只改变访问/表现，不重新授予箱内物资。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ossastra.Inventory
{
    [Serializable]
    public sealed class InventoryWorldContainerState
    {
        public string id, mapId, displayName, surfaceId;
        public InventoryWorldContainerKind kind;
        public float x, y, z;
        public int width, height;
        public bool isGround, isEmpty;
    }

    [Serializable]
    public sealed class InventoryRuntimeState
    {
        public int schema = 1;
        public string currentMapId, activeExternalId, priorityDefinitionId;
        public int selectedHotbar = -1;
        public string[] hotbar = new string[10];
        public InventoryWorldContainerState[] worldContainers = Array.Empty<InventoryWorldContainerState>();
    }

    public sealed class InventoryBulkResult
    {
        public int Transferred { get; }
        public int Remaining { get; }
        public string Reason { get; }
        public bool Complete => Remaining == 0;
        public InventoryBulkResult(int transferred, int remaining, string reason)
        { Transferred = transferred; Remaining = remaining; Reason = reason; }
    }

    /// <summary>只由Session准备、仅一次发布的完整宿主候选；所有集合均在活领域恢复前分配完成。</summary>
    public sealed class PreparedInventoryRuntimeState
    {
        internal InventorySession owner;
        internal bool used;
        internal string currentMapId, activeExternalId, priorityDefinitionId;
        internal int? selectedHotbar;
        internal string[] hotbar;
        internal Dictionary<string, InventoryWorldContainerState> world;
        internal Dictionary<string, Vector3> ground;
    }

    public sealed partial class InventorySession
    {
        private Dictionary<string, InventoryWorldContainerState> worldContainers =
            new Dictionary<string, InventoryWorldContainerState>(StringComparer.Ordinal);
        private IInventoryStartupAdapter startupAdapter;
        private Func<bool> editGate;
        private bool initialized, runtimeStateRestored;
        private string activeMapId, activeExternalId;

        public event Action RuntimeChanged;
        public string CurrentMapId => activeMapId ?? (string.IsNullOrWhiteSpace(gameObject.scene.path) ? gameObject.scene.name : gameObject.scene.path);
        public string ActiveExternalId => activeExternalId;
        public IEnumerable<string> WorldContainerIds => worldContainers.Values.Where(r => r.mapId == CurrentMapId)
            .OrderBy(r => r.id, StringComparer.Ordinal).Select(r => r.id).ToArray();
        public bool CanEditInventory => Ready && (editGate == null || editGate());
        public InventoryDefinition[] InventoryDefinitions => definitions.Values.Select(d => d.FreezeInventory()).ToArray();

        public void ConfigureStartupAdapter(IInventoryStartupAdapter adapter)
        {
            if (Ledger != null || initialized) throw new InvalidOperationException("Inventory startup adapter must be configured before Start");
            if (startupAdapter != null && !ReferenceEquals(startupAdapter, adapter)) throw new InvalidOperationException("Inventory startup adapter already owned");
            startupAdapter = adapter;
        }
        public void ConfigureEditGate(Func<bool> gate) { editGate = gate; }
        public bool CanAccess(string id) => CanAccessContainer(id);

        /// <summary>只表示经济宿主字段已变化；消费/全灭不经过主动编辑门，保存失败也不抹掉这些事实。</summary>
        public void NotifyRuntimeChanged()
        {
            var handlers = RuntimeChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception error) { Debug.LogError("Inventory runtime observer: " + error.Message, this); }
        }

        public void SetActiveMap(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId)) throw new ArgumentException("Map identity is required");
            if (CurrentMapId == mapId) return;
            activeMapId = mapId; activeExternalId = null;
            // 兼容箱属于首次公共安装地图；后续切图不搬运它的逻辑位置或重新播种。
            if (!initialized && !runtimeStateRestored && worldContainers.TryGetValue("external", out var legacy)) legacy.mapId = mapId;
            if (initialized) { Ledger.InvalidateAccess(); NotifyRuntimeChanged(); }
        }

        public bool SetActiveExternal(string id)
        {
            if (id != null && (!Ready || !worldContainers.ContainsKey(id) || !CanAccessContainer(id))) return false;
            activeExternalId = id;
            return true;
        }

        public string ContainerDisplayName(string id)
        {
            if (id != null && worldContainers.TryGetValue(id, out var record)) return record.displayName;
            return id == "team" ? "团队背包" : id == "safe" ? "安全栏" : id == "reserve" ? "后备" : id;
        }

        /// <summary>同一稳定ID重复装配只重连代理；初始内容只在首次创建容器时完整提交一次。</summary>
        public InventoryResult RegisterWorldContainer(string id, string mapId, Vector3 position, int width, int height,
            string displayName, InventoryGrant[] initialGrants)
        {
            if (Ledger == null || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(mapId) || width < 1 || height < 1 ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z) || IsPlayerContainer(id) || id.StartsWith("drop:", StringComparison.Ordinal))
                return InventoryResult.Reject("世界箱身份、地图、格位或位置无效");
            if (worldContainers.TryGetValue(id, out var existing))
            {
                if (existing.mapId != mapId || existing.width != width || existing.height != height || existing.isGround ||
                    existing.x != position.x || existing.y != position.y || existing.z != position.z)
                    return InventoryResult.Reject("世界容器稳定ID与已有记录冲突，未重新授予");
                return InventoryResult.Success(id);
            }
            var grants = new List<InventoryContainerGrant>();
            foreach (var grant in initialGrants ?? Array.Empty<InventoryGrant>())
            {
                if (grant?.definition == null || grant.quantity < 1 || Definition(grant.definition.definitionId) != grant.definition)
                    return InventoryResult.Reject("世界箱初始定义或数量无效");
                var definition = grant.definition;
                var equipment = definition.equipment == null ? null : new Ossastra.CharacterCore.EquipmentItemState(
                    Guid.NewGuid().ToString("N"), definition.definitionId, definition.equipment.offhandKind, 100);
                grants.Add(new InventoryContainerGrant(definition.definitionId, grant.quantity, equipment));
            }
            // 由唯一账本一次创建和授予，失败不会留下半个箱子或部分初始物品。
            var result = Ledger.CreateContainerWithItems(new InventoryContainer(id, width, height), grants.ToArray());
            if (!result.Ok) return result;
            worldContainers.Add(id, Record(id, mapId, position, width, height, string.IsNullOrWhiteSpace(displayName) ? "物资箱" : displayName, false));
            NotifyRuntimeChanged(); OnChanged(); return InventoryResult.Success(id);
        }

        private static bool IsPlayerContainer(string id) => id == "team" || id == "safe" || id == "reserve" ||
            id != null && id.StartsWith("hand:", StringComparison.Ordinal);
        private static InventoryWorldContainerState Record(string id, string mapId, Vector3 point, int width, int height, string name, bool ground)
            => new InventoryWorldContainerState { id = id, mapId = mapId, x = point.x, y = point.y, z = point.z, width = width, height = height, displayName = name, isGround = ground,
                kind = !ground ? InventoryWorldContainerKind.Container : id.StartsWith("drop:manual:", StringComparison.Ordinal) ? InventoryWorldContainerKind.ManualDrop : InventoryWorldContainerKind.WipeDrop };
        private void RegisterLegacyExternal()
        {
            worldContainers["external"] = Record("external", CurrentMapId, chest.position, 5, 8, "试验物资箱", false);
        }
        private void RegisterGround(string id, Vector3 point)
        {
            var container = Ledger.Container(id);
            ground[id] = point;
            worldContainers[id] = Record(id, CurrentMapId, point, container.Columns, container.Rows, "掉落包", true);
            NotifyRuntimeChanged();
        }

        /// <summary>只读持久化捕获不依赖组件是否启用；同宿主先禁用库存时仍能保存，未完成启动的半账本仍拒绝。</summary>
        public InventoryRuntimeState CaptureRuntimeState()
        {
            if (!initialized || Ledger == null) throw new InvalidOperationException("Inventory is not initialized");
            return new InventoryRuntimeState
            {
                currentMapId = CurrentMapId, activeExternalId = activeExternalId, priorityDefinitionId = PriorityDefinitionId,
                selectedHotbar = SelectedHotbar ?? -1, hotbar = (string[])hotbar.Clone(),
                worldContainers = worldContainers.Values.OrderBy(r => r.id, StringComparer.Ordinal).Select(r => new InventoryWorldContainerState
                {
                    id = r.id, mapId = r.mapId, x = r.x, y = r.y, z = r.z, width = r.width, height = r.height,
                    displayName = r.displayName, isGround = r.isGround, kind = KindOf(r), surfaceId = r.surfaceId,
                    isEmpty = !Ledger.Items.Any(i => i.Location.ContainerId == r.id)
                }).ToArray()
            };
        }

        /// <summary>先对脱离活账的完整候选验证；失败返回原因，不改变任何当前字段。</summary>
        public string ValidateRuntimeState(InventoryRuntimeState value, InventoryLedgerSnapshot inventorySnapshot)
        {
            if (value == null || value.schema != 1 || string.IsNullOrWhiteSpace(value.currentMapId) || value.hotbar == null || value.hotbar.Length != 10 ||
                value.worldContainers == null || inventorySnapshot?.containers == null || inventorySnapshot.items == null ||
                value.selectedHotbar < -1 || value.selectedHotbar >= 10) return "库存宿主快照缺失或版本无效";
            if (value.hotbar.Any(id => !string.IsNullOrEmpty(id) && (Definition(id) == null || Definition(id).equipment != null)) ||
                !string.IsNullOrEmpty(value.priorityDefinitionId) && (Definition(value.priorityDefinitionId) == null || Definition(value.priorityDefinitionId).equipment != null) ||
                value.selectedHotbar >= 0 && string.IsNullOrEmpty(value.hotbar[value.selectedHotbar])) return "库存快捷/优先引用定义无效";
            var containers = new Dictionary<string, InventoryContainerSnapshot>(StringComparer.Ordinal);
            foreach (var container in inventorySnapshot.containers)
                if (container == null || string.IsNullOrWhiteSpace(container.id) || containers.ContainsKey(container.id)) return "库存候选容器身份重复或缺失";
                else containers.Add(container.id, container);
            // 通用Ledger只知道格位合法；宿主还必须有既定团队/安全/后备和两个稳定角色手槽，不能恢复成空账本。
            var required = new[] { new InventoryContainer("team", 5, 2), new InventoryContainer("safe", 2, 1, true), new InventoryContainer("reserve", 5, 1, true), new InventoryContainer("external", 5, 8) }
                .Concat(coordinator.Characters.SelectMany(actor => new[]
                {
                    new InventoryContainer(HandId(actor.ActorId, InventoryHand.Left), 1, 1, true, actor.ActorId, InventoryHand.Left),
                    new InventoryContainer(HandId(actor.ActorId, InventoryHand.Right), 1, 1, true, actor.ActorId, InventoryHand.Right)
                })).ToArray();
            foreach (var expected in required)
                if (!containers.TryGetValue(expected.Id, out var actual) || actual.columns != expected.Columns || actual.rows != expected.Rows ||
                    actual.slots != expected.Slots || actual.hand != (int)expected.Hand ||
                    (string.IsNullOrEmpty(expected.Owner) ? !string.IsNullOrEmpty(actual.owner) : actual.owner != expected.Owner))
                    return "恢复库存缺少必需容器或角色手槽身份/几何不匹配：" + expected.Id;
            if (containers.Keys.Any(id => id.StartsWith("hand:", StringComparison.Ordinal) && !required.Any(c => c.Id == id)))
                return "恢复库存包含未知角色的手槽";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in value.worldContainers)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.id) || string.IsNullOrWhiteSpace(row.mapId) || string.IsNullOrWhiteSpace(row.displayName) ||
                    !seen.Add(row.id) || IsPlayerContainer(row.id) || !Finite(row.x) || !Finite(row.y) || !Finite(row.z) || row.width < 1 || row.height < 1 ||
                    !containers.TryGetValue(row.id, out var container) || container.columns != row.width || container.rows != row.height || container.slots ||
                    row.isGround != row.id.StartsWith("drop:", StringComparison.Ordinal) || !ValidKind(row) ||
                    row.isEmpty != !inventorySnapshot.items.Any(i => i != null && i.containerId == row.id)) return "世界箱/掉落快照身份、布局或搜空状态无效";
            }
            if (containers.Keys.Any(id => !IsPlayerContainer(id) && !seen.Contains(id)) || !seen.Contains("external")) return "库存容器缺少完整世界记录";
            if (!string.IsNullOrEmpty(value.activeExternalId) && !seen.Contains(value.activeExternalId)) return "当前外箱引用已失效";
            return null;
        }

        public bool RestoreRuntimeState(InventoryRuntimeState value, out string error)
        {
            if (Ledger == null) { error = "库存尚未构建"; return false; }
            var prepared = PrepareRuntimeState(value, Ledger.Capture(), out error); if (prepared == null) return false;
            PublishRuntimeState(prepared);
            if (initialized) { Ledger.InvalidateAccess(); NotifyRuntimeChanged(); OnChanged(); }
            return true;
        }

        public PreparedInventoryRuntimeState PrepareRuntimeState(InventoryRuntimeState value, InventoryLedgerSnapshot inventorySnapshot, out string error)
        {
            error = ValidateRuntimeState(value, inventorySnapshot); if (error != null) return null;
            var world = value.worldContainers.ToDictionary(row => row.id,
                row => CopyWorldRecord(row), StringComparer.Ordinal);
            var groundPoints = world.Values.Where(r => r.isGround).ToDictionary(r => r.id, r => new Vector3(r.x, r.y, r.z), StringComparer.Ordinal);
            return new PreparedInventoryRuntimeState
            {
                // 一些字段编码器将null字符串写为空串；可选引用统一为空值，不宽容未知的非空ID。
                owner = this, world = world, ground = groundPoints, hotbar = value.hotbar.Select(id => string.IsNullOrEmpty(id) ? null : id).ToArray(),
                currentMapId = value.currentMapId, activeExternalId = string.IsNullOrEmpty(value.activeExternalId) ? null : value.activeExternalId,
                priorityDefinitionId = string.IsNullOrEmpty(value.priorityDefinitionId) ? null : value.priorityDefinitionId,
                selectedHotbar = value.selectedHotbar < 0 ? (int?)null : value.selectedHotbar
            };
        }

        /// <summary>共同恢复发布段仅切换已准备引用，不分配、不查配置、不通知；Ready后宿主统一刷新。</summary>
        public void PublishRuntimeState(PreparedInventoryRuntimeState prepared)
        {
            if (prepared == null || prepared.owner != this || prepared.used) throw new InvalidOperationException("Invalid/used runtime restore candidate");
            prepared.used = true; runtimeStateRestored = true; worldContainers = prepared.world; ground = prepared.ground; hotbar = prepared.hotbar;
            activeMapId = prepared.currentMapId; activeExternalId = prepared.activeExternalId;
            PriorityDefinitionId = prepared.priorityDefinitionId; SelectedHotbar = prepared.selectedHotbar;
        }

        public InventoryResult Sort(string containerId, int budget = 4000)
        {
            if (!CanEditInventory) return InventoryResult.Reject("当前经济保存状态不允许整理");
            if (!CanAccessContainer(containerId)) return InventoryResult.Reject("容器不可访问");
            var plan = Ledger.PlanSort(containerId, budget);
            // 计划只读；最终提交前再次重验访问/保存门，避免旧整理复活已交易/消费物。
            if (!CanEditInventory || !CanAccessContainer(containerId)) return InventoryResult.Reject("整理访问已变化");
            var result = Ledger.CommitSort(plan); LastResult = result.Ok ? "整理完成 · 可行布局，未证明最优" : result.Reason; OnChanged(); return result;
        }

        public InventoryBulkResult TakeAll() => TransferAll(activeExternalId, "team");
        public InventoryBulkResult StoreAll() => TransferAll("team", activeExternalId);
        private InventoryBulkResult TransferAll(string from, string to)
        {
            if (!CanEditInventory || from == null || to == null || from == to || !CanAccessContainer(from) || !CanAccessContainer(to))
                return new InventoryBulkResult(0, Ledger?.Items.Count(i => i.Location.ContainerId == from) ?? 0, "保存状态或外箱访问无效");
            var ids = Ledger.Items.Where(i => i.Location.ContainerId == from).OrderBy(i => i.Id, StringComparer.Ordinal).Select(i => i.Id).ToArray();
            int count = 0; string last = null;
            foreach (var id in ids)
            {
                var result = Move(Ledger.Plan(id, to));
                if (result.Ok) count++; else last = result.Reason;
            }
            int remaining = Ledger.Items.Count(i => i.Location.ContainerId == from);
            LastResult = "已转移 " + count + " 项，剩余 " + remaining + " 项" + (last == null ? "" : "：" + last);
            OnChanged(); return new InventoryBulkResult(count, remaining, LastResult);
        }
    }
}
