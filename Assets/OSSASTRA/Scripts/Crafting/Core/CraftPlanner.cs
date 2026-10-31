using System;
using System.Collections.Generic;

namespace Ossastra.Crafting.Core
{
    /// <summary>无副作用材料分配；最终容量、工位会话和库存版本必须由提交者在原子候选中复核。</summary>
    public static class CraftPlanner
    {
        public static CraftMaterialPlan Plan(CraftRecipe recipe, int quantity, IEnumerable<CraftMaterialStack> stacks)
        {
            if (recipe == null) return Failure("InvalidRecipe");
            if (quantity < 1 || quantity > 99 || quantity > recipe.MaxBatch) return Failure("InvalidQuantity");
            if (stacks == null) return Failure("InvalidMaterials");
            var required = new Dictionary<string, int>(StringComparer.Ordinal);
            var available = new Dictionary<string, int>(StringComparer.Ordinal);
            var sources = new List<CraftMaterialStack>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                int outputTotal = 0;
                foreach (var output in recipe.Outputs) outputTotal = checked(outputTotal + checked(output.Quantity * quantity));
                if (outputTotal > 99) return Failure("OutputBudgetExceeded");
                int materialTotal = 0;
                foreach (var cost in recipe.Ingredients)
                {
                    int amount = checked(cost.Quantity * quantity);
                    materialTotal = checked(materialTotal + amount);
                    required.Add(cost.DefinitionId, amount);
                    available.Add(cost.DefinitionId, 0);
                }

                // 即使重复项被预留或与本配方无关也拒绝，避免含糊实例身份穿过提交边界。
                foreach (var source in stacks)
                {
                    if (source == null) return Failure("InvalidMaterials");
                    if (!ids.Add(source.ItemId)) return Failure("DuplicateItemId");
                    if (source.Reserved || source.IsEquipment || !required.ContainsKey(source.DefinitionId)) continue;
                    available[source.DefinitionId] = checked(available[source.DefinitionId] + source.Quantity);
                    sources.Add(source);
                }
            }
            catch (OverflowException) { return Failure("Overflow"); }

            foreach (var cost in required)
                if (available[cost.Key] < cost.Value) return Failure("InsufficientMaterials", required, available);

            // 与容器枚举/场景顺序无关；先定义再实例ID，均Ordinal。源快照永远只读。
            sources.Sort((a, b) =>
            {
                int order = StringComparer.Ordinal.Compare(a.DefinitionId, b.DefinitionId);
                return order != 0 ? order : StringComparer.Ordinal.Compare(a.ItemId, b.ItemId);
            });
            var remaining = new Dictionary<string, int>(required, StringComparer.Ordinal);
            var consumptions = new List<CraftMaterialConsumption>();
            foreach (var source in sources)
            {
                int take = Math.Min(source.Quantity, remaining[source.DefinitionId]);
                if (take == 0) continue;
                consumptions.Add(new CraftMaterialConsumption(source.ItemId, source.DefinitionId, take));
                remaining[source.DefinitionId] -= take;
            }
            return new CraftMaterialPlan(true, string.Empty, consumptions, required, available);
        }

        private static CraftMaterialPlan Failure(string reason, IDictionary<string, int> required = null, IDictionary<string, int> available = null)
        {
            return new CraftMaterialPlan(false, reason, new CraftMaterialConsumption[0],
                required ?? new Dictionary<string, int>(StringComparer.Ordinal),
                available ?? new Dictionary<string, int>(StringComparer.Ordinal));
        }
    }
}
