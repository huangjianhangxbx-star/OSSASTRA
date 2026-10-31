/* 脚本：InventoryConversion
 * 功能：普通材料到物品/独立装备的只读候选与一次原子转换，复用原经济批次及布局规则。
 * 边界：不认识配方/工位，不公开批次字典；装备交付先深复制，提交后才通知观察者。
 * 协作：调用方预分配回执，publishReceipt仅安装引用且不得抛异常/重入；命令去重由上层回执负责。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;

namespace Ossastra.Inventory
{
    public sealed class InventoryConsumption
    {
        public string ItemId { get; }
        public string DefinitionId { get; }
        public int Quantity { get; }
        public InventoryConsumption(string itemId, string definitionId, int quantity)
        { ItemId = itemId; DefinitionId = definitionId; Quantity = quantity; }
    }

    public sealed class InventoryConversionOutput
    {
        public string ItemId { get; }
        public string DefinitionId { get; }
        public int QuantityAdded { get; }
        internal InventoryConversionOutput(string itemId, string definitionId, int quantity)
        { ItemId = itemId; DefinitionId = definitionId; QuantityAdded = quantity; }
    }

    /// <summary>不暴露活状态或payload；输出为不可变增加量，允许调用方在提交前完整准备回执。</summary>
    public sealed class PreparedInventoryConversion
    {
        internal readonly InventoryLedger owner;
        internal readonly InventoryLedger.EconomicBatch batch;
        internal readonly Dictionary<string, string> outputSignatures;
        private readonly InventoryConversionOutput[] outputs;
        public string ContainerId { get; }
        public long ExpectedRevision => batch.expectedRevision;
        public long AccessGeneration => batch.accessGeneration;
        public InventoryConversionOutput[] Outputs => (InventoryConversionOutput[])outputs.Clone();
        internal PreparedInventoryConversion(InventoryLedger owner, InventoryLedger.EconomicBatch batch, string containerId,
            InventoryConversionOutput[] outputs, Dictionary<string, string> outputSignatures)
        { this.owner = owner; this.batch = batch; ContainerId = containerId; this.outputs = outputs; this.outputSignatures = outputSignatures; }
    }

    public sealed partial class InventoryLedger
    {
        private const int MaximumConversionRows = 64, MaximumConversionStacks = 256;

        public PreparedInventoryConversion PrepareConversion(string containerId, InventoryConsumption[] consumes,
            InventoryContainerGrant[] grants, long expectedRevision, long expectedAccessGeneration, out string error)
        {
            error = null;
            if (expectedRevision != mutationRevision || expectedAccessGeneration != AccessGeneration || expectedRevision < 0 || expectedAccessGeneration < 0)
            { error = "库存预览或访问已变化，请重新确认"; return null; }
            var container = Container(containerId);
            if (container == null || container.Hand != InventoryHand.None || consumes == null || grants == null ||
                consumes.Length < 1 || consumes.Length > MaximumConversionRows || grants.Length < 1 || grants.Length > MaximumConversionRows)
            { error = "转换容器、材料或产物批次无效"; return null; }
            if (!Validate(out error)) return null;
            try
            {
                var batch = PrepareEconomicBatch();
                var costs = new Dictionary<string, InventoryConsumption>(StringComparer.Ordinal);
                foreach (var row in consumes)
                {
                    if (row == null || string.IsNullOrWhiteSpace(row.ItemId) || string.IsNullOrWhiteSpace(row.DefinitionId) || row.Quantity < 1)
                    { error = "材料身份或数量无效"; return null; }
                    if (costs.TryGetValue(row.ItemId, out var prior))
                    {
                        if (prior.DefinitionId != row.DefinitionId) { error = "同一材料实例的定义冲突"; return null; }
                        costs[row.ItemId] = new InventoryConsumption(row.ItemId, row.DefinitionId, checked(prior.Quantity + row.Quantity));
                    }
                    else costs.Add(row.ItemId, row);
                }
                // 所有材料先在候选扣完，再计算产物空间；预留中的整堆保守排除。
                foreach (var row in costs.Values.OrderBy(r => r.ItemId, StringComparer.Ordinal))
                {
                    if (!batch.state.TryGetValue(row.ItemId, out var item) || item.DefinitionId != row.DefinitionId ||
                        item.Location.ContainerId != containerId || item.Equipment != null || Definition(item.DefinitionId).Hand != InventoryHand.None ||
                        Locked(item.Id) || row.Quantity > item.Quantity)
                    { error = "普通材料身份、数量、来源或动作预留已变化"; return null; }
                    item.Quantity -= row.Quantity;
                    if (item.Quantity == 0) batch.state.Remove(item.Id);
                }
                batch.changed.Add(containerId);
                var outputAmounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var outputIds = new HashSet<string>(StringComparer.Ordinal);
                var outputPayloads = new List<EquipmentItemState>();
                var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
                long stacks = 0;
                foreach (var grant in grants)
                {
                    var definition = grant == null ? null : Definition(grant.DefinitionId);
                    if (definition == null || grant.Quantity < 1)
                    { error = "产物定义或数量无效"; return null; }
                    stacks += ((long)grant.Quantity + definition.MaxStack - 1) / definition.MaxStack;
                    if (stacks > MaximumConversionStacks) { error = "产物批次超过有界容量"; return null; }
                    if (grant.Equipment == null)
                    {
                        if (definition.Hand != InventoryHand.None) { error = "装备产物缺少显式实例"; return null; }
                        var before = batch.state.Values.Where(i => i.DefinitionId == definition.Id && i.Location.ContainerId == containerId)
                            .ToDictionary(i => i.Id, i => i.Quantity, StringComparer.Ordinal);
                        var result = batch.Grant(containerId, definition.Id, grant.Quantity);
                        if (!result.Ok) { error = result.Reason; return null; }
                        foreach (var item in batch.state.Values.Where(i => i.DefinitionId == definition.Id && i.Location.ContainerId == containerId))
                        {
                            before.TryGetValue(item.Id, out var quantity);
                            var added = item.Quantity - quantity;
                            if (added > 0) { outputAmounts.TryGetValue(item.Id, out var old); outputAmounts[item.Id] = checked(old + added); }
                        }
                    }
                    else
                    {
                        var source = grant.Equipment;
                        if (definition.Hand == InventoryHand.None || grant.Quantity != 1 || source.DefinitionId != definition.Id ||
                            source.Durability < 1 || !Enum.IsDefined(typeof(OffhandKind), source.Kind) || items.ContainsKey(source.Id) ||
                            !outputIds.Add(source.Id) || outputPayloads.Any(p => ReferenceEquals(p, source) || ReferenceEquals(p.Runtime, source.Runtime)) ||
                            items.Values.Any(i => i.Equipment != null && ReferenceEquals(i.Equipment.Runtime, source.Runtime)))
                        { error = "装备产物身份、独立资源或初始状态无效"; return null; }
                        outputPayloads.Add(source);
                        // 原持久化工厂校验并深复制全部能力字段；调用方后续修改原对象不能影响候选。
                        var equipment = EquipmentPersistentSnapshot.Restore(source.CapturePersistent());
                        var item = new InventoryItem(equipment.Id, definition.Id, 1, default, equipment);
                        if (!Place(batch.state, item, containerId)) { error = "扣料后的最终背包容量不足，整批未制作"; return null; }
                        batch.state.Add(item.Id, item); outputAmounts.Add(item.Id, 1);
                        signatures.Add(item.Id, equipment.PersistentSignature());
                    }
                }
                if (!ValidateEconomicBatch(batch, out error)) return null;
                var facts = outputAmounts.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new InventoryConversionOutput(p.Key, batch.state[p.Key].DefinitionId, p.Value)).ToArray();
                return new PreparedInventoryConversion(this, batch, containerId, facts, signatures);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is OverflowException)
            { error = "转换候选拒绝：" + ex.Message; return null; }
        }

        /// <summary>publishReceipt仅切换预分配回执引用，不得分配、通知、重入或抛异常；异常沿原批次恢复库存。</summary>
        public InventoryResult CommitConversion(PreparedInventoryConversion candidate, Func<string> finalAccessCheck, Action publishReceipt)
        {
            if (candidate == null || candidate.owner != this || finalAccessCheck == null || publishReceipt == null)
                return InventoryResult.Reject("转换候选、最终访问门或回执发布缺失");
            var result = CommitEconomicBatch(candidate.batch, () =>
            {
                var denied = finalAccessCheck();
                if (denied != null) return denied;
                if (mutationRevision == long.MaxValue || candidate.batch.changed.Any(id => Container(id)?.Revision == long.MaxValue))
                    return "库存修订已达上限，未提交转换";
                foreach (var pair in candidate.outputSignatures)
                    if (!candidate.batch.state.TryGetValue(pair.Key, out var item) || item.Equipment == null || item.Equipment.PersistentSignature() != pair.Value)
                        return "候选装备状态已变化，请重新确认";
                return null;
            }, publishReceipt);
            if (result.Ok) NotifyEconomicCommit();
            return result;
        }
    }
}
