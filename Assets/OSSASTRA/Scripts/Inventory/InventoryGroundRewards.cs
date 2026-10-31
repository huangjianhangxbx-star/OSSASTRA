/* 脚本：InventoryGroundRewards
 * 功能：自然奖励整批接入唯一Ledger，原子安装地面记录与调用方已经准备的奖励回执。
 * 边界：不随机、不寻地、不保存第二本物品；稳定身份与合法支撑点由领域适配提供并复验。
 * 协作：原交互与显示按类型读取；旧manual/wipe记录受控迁移，不把自然奖励伪称玩家丢弃。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Inventory
{
    public enum InventoryWorldContainerKind { Legacy = 0, Container = 1, ManualDrop = 2, WipeDrop = 3, NaturalDrop = 4 }

    public sealed partial class InventorySession
    {
        private static InventoryWorldContainerKind KindOf(InventoryWorldContainerState row)
        {
            if (row.kind != InventoryWorldContainerKind.Legacy) return row.kind;
            if (!row.isGround) return InventoryWorldContainerKind.Container;
            if (row.id != null && row.id.StartsWith("drop:manual:", StringComparison.Ordinal)) return InventoryWorldContainerKind.ManualDrop;
            if (row.id != null && row.id.StartsWith("drop:wipe:", StringComparison.Ordinal)) return InventoryWorldContainerKind.WipeDrop;
            return InventoryWorldContainerKind.Legacy;
        }
        private static bool ValidKind(InventoryWorldContainerState row)
        {
            var kind = KindOf(row);
            if (!Enum.IsDefined(typeof(InventoryWorldContainerKind), kind) || kind == InventoryWorldContainerKind.Legacy) return false;
            if (row.surfaceId != null && row.surfaceId.Length > 256) return false;
            if (kind == InventoryWorldContainerKind.Container) return !row.isGround;
            if (!row.isGround) return false;
            if (kind == InventoryWorldContainerKind.ManualDrop) return row.id.StartsWith("drop:manual:", StringComparison.Ordinal);
            if (kind == InventoryWorldContainerKind.WipeDrop) return row.id.StartsWith("drop:wipe:", StringComparison.Ordinal);
            return row.id.StartsWith("drop:resource:", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(row.surfaceId);
        }
        private static InventoryWorldContainerState CopyWorldRecord(InventoryWorldContainerState row)
            => new InventoryWorldContainerState { id = row.id, mapId = row.mapId, displayName = row.displayName,
                x = row.x, y = row.y, z = row.z, width = row.width, height = row.height,
                isGround = row.isGround, isEmpty = row.isEmpty, kind = KindOf(row), surfaceId = row.surfaceId };

        /// <summary>直接拾取按明确类型识别；全灭包仍走原容器UI，不加入单条散落拾取分支。</summary>
        public bool IsLoosePickup(string id)
            => id != null && worldContainers.TryGetValue(id, out var row) && row.mapId == CurrentMapId &&
                (KindOf(row) == InventoryWorldContainerKind.ManualDrop || KindOf(row) == InventoryWorldContainerKind.NaturalDrop);

        /// <summary>完整批次和回执共同发布；publishReceipt只切换预分配引用，不分配、不重入、不通知。</summary>
        public InventoryResult CommitGroundReward(string containerId, string mapId, Vector3 position, string surfaceId,
            string displayName, InventoryContainerGrant[] grants, Action publishReceipt)
        {
            if (!CanEditInventory || string.IsNullOrWhiteSpace(containerId) || !containerId.StartsWith("drop:resource:", StringComparison.Ordinal) ||
                mapId != CurrentMapId || string.IsNullOrWhiteSpace(surfaceId) || surfaceId.Length > 256 ||
                string.IsNullOrWhiteSpace(displayName) || !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                grants == null || grants.Length == 0 || grants.Length > 64 || publishReceipt == null ||
                worldContainers.ContainsKey(containerId) || Ledger.Container(containerId) != null)
                return InventoryResult.Reject("自然奖励来源、地图、支撑面、回执或保存状态无效");
            long rows = 0, stacks = 0; int columns = 1;
            foreach (var grant in grants)
            {
                var definition = grant == null ? null : Ledger.Definition(grant.DefinitionId);
                if (definition == null || grant.Quantity < 1) return InventoryResult.Reject("自然奖励定义或数量无效");
                long count = ((long)grant.Quantity + definition.MaxStack - 1) / definition.MaxStack;
                stacks += count; rows += count * definition.Height; columns = Math.Max(columns, definition.Width);
                if (stacks > 256 || rows > 8192 || columns > 256) return InventoryResult.Reject("自然奖励批次超过有界容量");
            }
            var row = Record(containerId, mapId, position, columns, (int)rows, displayName, true);
            row.kind = InventoryWorldContainerKind.NaturalDrop; row.surfaceId = surfaceId;
            // 所有字典分配在提交前；Ledger通知之前同时切换宿主记录与领域回执。
            var nextWorld = new Dictionary<string, InventoryWorldContainerState>(worldContainers, StringComparer.Ordinal) { [containerId] = row };
            var nextGround = new Dictionary<string, Vector3>(ground, StringComparer.Ordinal) { [containerId] = position };
            var oldWorld = worldContainers; var oldGround = ground;
            InventoryResult result;
            try
            {
                result = Ledger.CreateContainerWithItems(new InventoryContainer(containerId, columns, (int)rows), grants, () =>
                { worldContainers = nextWorld; ground = nextGround; publishReceipt(); });
            }
            catch { worldContainers = oldWorld; ground = oldGround; throw; }
            if (!result.Ok) return result;
            NotifyRuntimeChanged(); OnChanged(); return result;
        }
    }
}
