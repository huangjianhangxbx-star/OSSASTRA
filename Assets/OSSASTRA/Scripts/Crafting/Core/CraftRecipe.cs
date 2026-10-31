using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ossastra.Crafting.Core
{
    /// <summary>普通材料定义与正数量；不承载任何库存实例或运行时余额。</summary>
    public sealed class CraftIngredient
    {
        public string DefinitionId { get; }
        public int Quantity { get; }

        public CraftIngredient(string definitionId, int quantity)
        {
            DefinitionId = CraftValidation.Identity(definitionId, nameof(definitionId));
            Quantity = CraftValidation.Positive(quantity, nameof(quantity));
        }
    }

    /// <summary>配方产物的定义引用；实际新装备状态由库存集成工厂创建。</summary>
    public sealed class CraftOutput
    {
        public string DefinitionId { get; }
        public int Quantity { get; }

        public CraftOutput(string definitionId, int quantity)
        {
            DefinitionId = CraftValidation.Identity(definitionId, nameof(definitionId));
            Quantity = CraftValidation.Positive(quantity, nameof(quantity));
        }
    }

    /// <summary>冻结后的配方；构造时拒绝无效作者数据，不在提交时读取可变配置。</summary>
    public sealed class CraftRecipe
    {
        public string Id { get; }
        public int Version { get; }
        public string StationCapability { get; }
        public string Category { get; }
        public IReadOnlyList<CraftIngredient> Ingredients { get; }
        public IReadOnlyList<CraftOutput> Outputs { get; }
        public int MaxBatch { get; }

        public CraftRecipe(string id, int version, string stationCapability, string category,
            IEnumerable<CraftIngredient> ingredients, IEnumerable<CraftOutput> outputs, int maxBatch = 1)
        {
            Id = CraftValidation.Identity(id, nameof(id));
            Version = CraftValidation.Positive(version, nameof(version));
            StationCapability = CraftValidation.Identity(stationCapability, nameof(stationCapability));
            Category = CraftValidation.Identity(category, nameof(category));
            if (maxBatch < 1 || maxBatch > 99) throw new ArgumentOutOfRangeException(nameof(maxBatch));
            if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));

            // 同定义只保留一条成本；checked同时约束同材料合计与单次总成本，防止溢出绕过数量判断。
            var costs = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int totalCost = 0;
            foreach (var ingredient in ingredients)
            {
                if (ingredient == null) throw new ArgumentException("材料项不能为空。", nameof(ingredients));
                int current;
                costs.TryGetValue(ingredient.DefinitionId, out current);
                costs[ingredient.DefinitionId] = checked(current + ingredient.Quantity);
                totalCost = checked(totalCost + ingredient.Quantity);
            }
            if (costs.Count == 0) throw new ArgumentException("配方至少需要一种材料。", nameof(ingredients));
            var frozenCosts = new List<CraftIngredient>();
            foreach (var pair in costs) frozenCosts.Add(new CraftIngredient(pair.Key, pair.Value));

            var products = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int totalOutput = 0;
            foreach (var output in outputs)
            {
                if (output == null) throw new ArgumentException("产物项不能为空。", nameof(outputs));
                totalOutput = checked(totalOutput + output.Quantity);
                if (totalOutput > 99) throw new ArgumentOutOfRangeException(nameof(outputs), "单次总产物不能超过99。");
                int current;
                products.TryGetValue(output.DefinitionId, out current);
                products[output.DefinitionId] = checked(current + output.Quantity);
            }
            if (products.Count == 0) throw new ArgumentException("配方至少需要一种产物。", nameof(outputs));
            var frozenProducts = new List<CraftOutput>();
            foreach (var pair in products) frozenProducts.Add(new CraftOutput(pair.Key, pair.Value));

            Ingredients = new ReadOnlyCollection<CraftIngredient>(frozenCosts);
            Outputs = new ReadOnlyCollection<CraftOutput>(frozenProducts);
            MaxBatch = maxBatch;
        }
    }

    /// <summary>冻结的配方目录；只解析明确ID，不提供隐式别名、科技或场景查询。</summary>
    public sealed class CraftCatalog
    {
        private readonly Dictionary<string, CraftRecipe> byId;
        public IReadOnlyList<CraftRecipe> Recipes { get; }
        public const int FingerprintVersion = 1;

        public CraftCatalog(IEnumerable<CraftRecipe> recipes)
        {
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            byId = new Dictionary<string, CraftRecipe>(StringComparer.Ordinal);
            var ordered = new List<CraftRecipe>();
            foreach (var recipe in recipes)
            {
                if (recipe == null) throw new ArgumentException("目录配方不能为空。", nameof(recipes));
                CraftValidation.Identity(recipe.Id, nameof(recipes));
                CraftValidation.Identity(recipe.StationCapability, nameof(recipes));
                CraftValidation.Positive(recipe.Version, nameof(recipes));
                if (byId.ContainsKey(recipe.Id)) throw new ArgumentException("配方ID重复：" + recipe.Id, nameof(recipes));
                byId.Add(recipe.Id, recipe);
                ordered.Add(recipe);
            }
            ordered.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            Recipes = new ReadOnlyCollection<CraftRecipe>(ordered);
        }

        public bool TryGetRecipe(string id, out CraftRecipe recipe)
        {
            if (id == null) { recipe = null; return false; }
            return byId.TryGetValue(id, out recipe);
        }
    }

    /// <summary>纯领域输入校验；ID保持原字节语义，不静默Trim或改变大小写。</summary>
    internal static class CraftValidation
    {
        public static string Identity(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("身份不能为空。", parameter);
            return value;
        }

        public static int Positive(int value, string parameter)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(parameter, "数量或版本必须为正整数。");
            return value;
        }
    }
}
