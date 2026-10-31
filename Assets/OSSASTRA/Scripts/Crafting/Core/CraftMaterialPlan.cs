using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ossastra.Crafting.Core
{
    /// <summary>一次规划读取的材料快照；Runtime必须先过滤合法团队来源，预留按整堆排除。</summary>
    public sealed class CraftMaterialStack
    {
        public string ItemId { get; }
        public string DefinitionId { get; }
        public int Quantity { get; }
        public bool Reserved { get; }
        public bool IsEquipment { get; }

        public CraftMaterialStack(string itemId, string definitionId, int quantity, bool reserved = false, bool isEquipment = false)
        {
            ItemId = CraftValidation.Identity(itemId, nameof(itemId));
            DefinitionId = CraftValidation.Identity(definitionId, nameof(definitionId));
            Quantity = CraftValidation.Positive(quantity, nameof(quantity));
            Reserved = reserved;
            IsEquipment = isEquipment;
        }
    }

    /// <summary>消费计划的具体实例事实；仅是请求，不能由此宣称已经扣料或发布库存。</summary>
    public sealed class CraftMaterialConsumption
    {
        public string ItemId { get; }
        public string DefinitionId { get; }
        public int Quantity { get; }

        public CraftMaterialConsumption(string itemId, string definitionId, int quantity)
        {
            ItemId = CraftValidation.Identity(itemId, nameof(itemId));
            DefinitionId = CraftValidation.Identity(definitionId, nameof(definitionId));
            Quantity = CraftValidation.Positive(quantity, nameof(quantity));
        }
    }

    /// <summary>冻结的规划结果；失败时消费列表始终为空，Required/Available只表示本配方相关定义。</summary>
    public sealed class CraftMaterialPlan
    {
        public bool Ok { get; }
        public string Reason { get; }
        public IReadOnlyList<CraftMaterialConsumption> Consumptions { get; }
        public IReadOnlyDictionary<string, int> Required { get; }
        public IReadOnlyDictionary<string, int> Available { get; }

        internal CraftMaterialPlan(bool ok, string reason, IEnumerable<CraftMaterialConsumption> consumptions,
            IDictionary<string, int> required, IDictionary<string, int> available)
        {
            Ok = ok;
            Reason = reason ?? string.Empty;
            Consumptions = new ReadOnlyCollection<CraftMaterialConsumption>(
                ok ? new List<CraftMaterialConsumption>(consumptions) : new List<CraftMaterialConsumption>());
            Required = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(required, StringComparer.Ordinal));
            Available = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(available, StringComparer.Ordinal));
        }
    }
}
