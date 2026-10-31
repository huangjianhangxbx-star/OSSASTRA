/* 脚本：InventorySessionConversion
 * 功能：team普通材料转换的宿主窄门；绑定候选的操作者/控制修订/地图，最终重验保存与工位资格。
 * 边界：不持有物品数量、配方或第二套回执，不直接装备；原Ledger一次提交后负责安全通知。
 * 协作：弱键上下文不会因预览刷新永久保留旧候选；外部发布只安装预分配状态，不重入或通知。
 */
using System;
using System.Runtime.CompilerServices;
using Ossastra.Characters;

namespace Ossastra.Inventory
{
    using CharacterRoot = Ossastra.Characters.CharacterRoot;
    public sealed partial class InventorySession
    {
        private sealed class ConversionAccessContext
        {
            internal CharacterRoot actor;
            internal int controlRevision;
            internal string mapId;
        }
        private readonly ConditionalWeakTable<PreparedInventoryConversion, ConversionAccessContext> conversionContexts =
            new ConditionalWeakTable<PreparedInventoryConversion, ConversionAccessContext>();

        private string ConversionAccess(ConversionAccessContext context)
        {
            if (!CanEditInventory || !CanAccessContainer("team") || coordinator?.Current == null || !coordinator.Current.CanSimulateIntent)
                return "库存、保存或当前角色不允许制作";
            if (context != null && (context.actor != coordinator.Current || context.controlRevision != coordinator.ControlRevision || context.mapId != CurrentMapId))
                return "制作操作者、控制修订或地图已变化";
            return null;
        }

        public PreparedInventoryConversion PrepareTeamConversion(InventoryConsumption[] consumes, InventoryContainerGrant[] grants,
            long expectedRevision, long expectedAccessGeneration, out string error)
        {
            error = ConversionAccess(null);
            if (error != null) return null;
            if (consumes == null || grants == null) { error = "制作材料或产物缺失"; return null; }
            foreach (var row in consumes)
            {
                var definition = row == null ? null : Definition(row.DefinitionId);
                if (definition == null || definition.equipment != null) { error = "只允许使用团队普通材料"; return null; }
            }
            foreach (var grant in grants)
            {
                var definition = grant == null ? null : Definition(grant.DefinitionId);
                var registered = grant == null ? null : Ledger.Definition(grant.DefinitionId);
                if (definition == null || registered == null) { error = "制作产物未登记"; return null; }
                InventoryDefinition frozen;
                try { frozen = definition.FreezeInventory(); }
                catch (ArgumentException ex) { error = "产物作者定义无效：" + ex.Message; return null; }
                if (frozen.Width != registered.Width || frozen.Height != registered.Height || frozen.MaxStack != registered.MaxStack ||
                    frozen.Hand != registered.Hand || frozen.WeightGrams != registered.WeightGrams ||
                    (definition.equipment == null) != (grant.Equipment == null) ||
                    definition.equipment != null && (definition.equipment.definitionId != definition.definitionId ||
                        grant.Equipment.Kind != definition.equipment.offhandKind))
                { error = "产物作者定义、装备类型或库存定义不一致"; return null; }
            }
            var candidate = Ledger.PrepareConversion("team", consumes, grants, expectedRevision, expectedAccessGeneration, out error);
            if (candidate != null) conversionContexts.Add(candidate, new ConversionAccessContext
            { actor = coordinator.Current, controlRevision = coordinator.ControlRevision, mapId = CurrentMapId });
            return candidate;
        }

        public InventoryResult CommitTeamConversion(PreparedInventoryConversion candidate, Func<string> finalCraftingAccessCheck, Action publishReceipt)
        {
            if (candidate == null || candidate.ContainerId != "team" || !conversionContexts.TryGetValue(candidate, out var context) ||
                finalCraftingAccessCheck == null || publishReceipt == null)
                return InventoryResult.Reject("制作候选、工位访问门或回执发布无效");
            return Ledger.CommitConversion(candidate, () =>
            {
                var denied = ConversionAccess(context);
                if (denied != null) return denied;
                denied = finalCraftingAccessCheck();
                // 外部资格回调后再次检查宿主，防同帧切控尚未等到Update推进库存访问代际。
                return denied ?? ConversionAccess(context);
            }, publishReceipt);
        }
    }
}
