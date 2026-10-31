using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ossastra.Crafting.Core
{
    /// <summary>存档消费事实DTO；字段供序列化使用，进入Journal时会深拷贝并验证。</summary>
    [Serializable]
    public sealed class CraftReceiptConsumption
    {
        public string itemId;
        public string definitionId;
        public int quantity;
    }

    /// <summary>已交付产物DTO；普通物品合堆可能复用旧ItemId，因此只要求同一回执内唯一。</summary>
    [Serializable]
    public sealed class CraftReceiptOutput
    {
        public string itemId;
        public string definitionId;
        public int quantity;
    }

    /// <summary>一次已提交请求的持久事实；可变DTO不直接作为Journal内部权威对象。</summary>
    [Serializable]
    public sealed class CraftReceiptRecord
    {
        public string requestId;
        public string fingerprint;
        public string recipeId;
        public int version;
        public int quantity;
        public string stationDefinitionId;
        public string stationInstanceId;
        public string mapId;
        public string actorId;
        public long controlRevision;
        public string sessionId;
        public long ledgerRevision;
        public long accessGeneration;
        public CraftReceiptConsumption[] consumptions;
        public CraftReceiptOutput[] outputs;
    }

    /// <summary>
    /// 不可变的最近128次提交事实。查回执用于幂等重放结果，不负责授权新的合成。
    /// Runtime必须只接纳当前签发的preview；旧记录淘汰后也不能重新接纳任意旧票据。
    /// </summary>
    public sealed class CraftReceiptJournal
    {
        private const int Capacity = 128;
        private readonly CraftReceiptRecord[] records;

        public CraftReceiptJournal() { records = new CraftReceiptRecord[0]; }

        private CraftReceiptJournal(CraftReceiptRecord[] frozenRecords) { records = frozenRecords; }

        /// <summary>每次返回独立深拷贝，避免UI或JSON库通过字段/数组改动内部历史。</summary>
        public IReadOnlyList<CraftReceiptRecord> Records
        {
            get
            {
                var copies = new List<CraftReceiptRecord>(records.Length);
                foreach (var record in records) copies.Add(Copy(record));
                return new ReadOnlyCollection<CraftReceiptRecord>(copies);
            }
        }

        public bool Find(string requestId, string fingerprint, out CraftReceiptRecord record, out string error)
        {
            record = null;
            error = null;
            foreach (var stored in records)
            {
                if (!StringComparer.Ordinal.Equals(stored.requestId, requestId)) continue;
                if (!StringComparer.Ordinal.Equals(stored.fingerprint, fingerprint))
                {
                    error = "RequestConflict";
                    return false;
                }
                record = Copy(stored);
                return true;
            }
            return false;
        }

        /// <summary>仅准备新历史，不修改旧Journal；重复ID一律拒绝，调用者应先Find处理重放。</summary>
        public CraftReceiptJournal PrepareAppend(CraftReceiptRecord record)
        {
            var candidate = Copy(record);
            Validate(candidate);
            foreach (var stored in records)
                if (StringComparer.Ordinal.Equals(stored.requestId, candidate.requestId))
                    throw new ArgumentException("回执请求ID已存在，不能覆盖。", nameof(record));

            int skip = records.Length == Capacity ? 1 : 0;
            var next = new CraftReceiptRecord[records.Length - skip + 1];
            // 内部对象从不外泄且不再修改；两个不可变Journal可安全共享旧的冻结事实。
            Array.Copy(records, skip, next, 0, records.Length - skip);
            next[next.Length - 1] = candidate;
            return new CraftReceiptJournal(next);
        }

        /// <summary>恢复时拒绝坏档，不截断超长数据、不吞掉重复请求，也不隐式迁移配方版本。</summary>
        public static CraftReceiptJournal Restore(IEnumerable<CraftReceiptRecord> records, CraftCatalog catalog)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var restored = new List<CraftReceiptRecord>();
            var requestIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (restored.Count == Capacity) throw new ArgumentException("回执存档超过128条。", nameof(records));
                var frozen = Copy(record);
                Validate(frozen);
                if (!requestIds.Add(frozen.requestId)) throw new ArgumentException("存档请求ID重复。", nameof(records));
                ValidateRecipe(frozen, catalog);
                restored.Add(frozen);
            }
            return new CraftReceiptJournal(restored.ToArray());
        }

        private static void Validate(CraftReceiptRecord record)
        {
            RequireGuid(record.requestId, "requestId");
            RequireGuid(record.sessionId, "sessionId");
            if (record.fingerprint == null || record.fingerprint.Length != 64)
                throw new ArgumentException("指纹必须为64位小写十六进制。");
            foreach (char value in record.fingerprint)
                if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f')))
                    throw new ArgumentException("指纹必须为64位小写十六进制。");
            CraftValidation.Identity(record.recipeId, "recipeId");
            CraftValidation.Positive(record.version, "version");
            if (record.quantity < 1 || record.quantity > 99) throw new ArgumentOutOfRangeException("quantity");
            CraftValidation.Identity(record.stationDefinitionId, "stationDefinitionId");
            CraftValidation.Identity(record.stationInstanceId, "stationInstanceId");
            CraftValidation.Identity(record.mapId, "mapId");
            CraftValidation.Identity(record.actorId, "actorId");
            if (record.controlRevision < 0 || record.ledgerRevision < 0 || record.accessGeneration < 0)
                throw new ArgumentException("回执版本字段不能为负数。");
            if (record.consumptions == null || record.consumptions.Length == 0)
                throw new ArgumentException("回执必须包含消费事实。");
            if (record.outputs == null || record.outputs.Length == 0)
                throw new ArgumentException("回执必须包含交付事实。");

            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            var consumptionFacts = new List<CraftMaterialConsumption>();
            long totalCost = 0;
            foreach (var source in record.consumptions)
            {
                if (source == null) throw new ArgumentException("消费项不能为空。");
                CraftValidation.Identity(source.itemId, "itemId");
                CraftValidation.Identity(source.definitionId, "definitionId");
                CraftValidation.Positive(source.quantity, "quantity");
                if (!sourceIds.Add(source.itemId)) throw new ArgumentException("消费实例ID重复。");
                totalCost += source.quantity;
                if (totalCost > int.MaxValue) throw new ArgumentException("材料总数量溢出。");
                consumptionFacts.Add(new CraftMaterialConsumption(source.itemId, source.definitionId, source.quantity));
            }
            var outputIds = new HashSet<string>(StringComparer.Ordinal);
            long totalOutput = 0;
            foreach (var output in record.outputs)
            {
                if (output == null) throw new ArgumentException("交付项不能为空。");
                CraftValidation.Identity(output.itemId, "itemId");
                CraftValidation.Identity(output.definitionId, "definitionId");
                CraftValidation.Positive(output.quantity, "quantity");
                if (!outputIds.Add(output.itemId)) throw new ArgumentException("同次回执交付实例ID重复。");
                totalOutput += output.quantity;
                if (totalOutput > 99) throw new ArgumentException("同次交付数量不能超过99。");
            }

            string expected = CraftIdentity.Fingerprint(record.recipeId, record.version, record.stationDefinitionId,
                record.stationInstanceId, record.mapId, record.actorId, record.controlRevision, record.sessionId,
                record.requestId, record.quantity, record.ledgerRevision, record.accessGeneration, consumptionFacts);
            if (!StringComparer.Ordinal.Equals(record.fingerprint, expected)) throw new ArgumentException("回执指纹与命令事实不符。");
        }

        private static void ValidateRecipe(CraftReceiptRecord record, CraftCatalog catalog)
        {
            CraftRecipe recipe;
            if (!catalog.TryGetRecipe(record.recipeId, out recipe)) throw new ArgumentException("存档配方不在目录中。");
            if (record.version != recipe.Version) throw new ArgumentException("回执配方版本不符，需要显式迁移。");
            if (record.quantity > recipe.MaxBatch) throw new ArgumentException("回执批量超过配方上限。");
            var costs = new Dictionary<string, int>(StringComparer.Ordinal);
            var products = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                int totalCost = 0;
                int totalOutput = 0;
                foreach (var ingredient in recipe.Ingredients)
                {
                    int amount = checked(ingredient.Quantity * record.quantity);
                    costs.Add(ingredient.DefinitionId, amount);
                    totalCost = checked(totalCost + amount);
                }
                foreach (var output in recipe.Outputs)
                {
                    int amount = checked(output.Quantity * record.quantity);
                    products.Add(output.DefinitionId, amount);
                    totalOutput = checked(totalOutput + amount);
                }
                if (totalOutput > 99) throw new ArgumentException("回执批量产出超过预算。");
            }
            catch (OverflowException ex) { throw new ArgumentException("回执配方批量数量溢出。", ex); }

            // 逐定义精确减到0：既不允许缺料/少交付，也不允许夹带额外定义或多扣多发。
            foreach (var source in record.consumptions) Subtract(costs, source.definitionId, source.quantity);
            foreach (var output in record.outputs) Subtract(products, output.definitionId, output.quantity);
            foreach (var remaining in costs.Values) if (remaining != 0) throw new ArgumentException("回执材料数量与配方不符。");
            foreach (var remaining in products.Values) if (remaining != 0) throw new ArgumentException("回执产物数量与配方不符。");
            // 工位定义是否仍合法由Runtime目录/保存参与者验证；此层不读取场景或工位服务。
        }

        private static void Subtract(Dictionary<string, int> expected, string definitionId, int quantity)
        {
            int remaining;
            if (!expected.TryGetValue(definitionId, out remaining) || remaining < quantity)
                throw new ArgumentException("回执定义或数量与配方不符。");
            expected[definitionId] = remaining - quantity;
        }

        private static void RequireGuid(string value, string field)
        {
            Guid parsed;
            // TryParseExact仍会接受两端空白，长度门保证存档中保存的就是32字符N格式。
            if (value == null || value.Length != 32 || !Guid.TryParseExact(value, "N", out parsed) || parsed == Guid.Empty)
                throw new ArgumentException("回执" + field + "必须为非空Guid N格式。", field);
        }

        private static CraftReceiptRecord Copy(CraftReceiptRecord source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new CraftReceiptRecord
            {
                requestId = source.requestId,
                fingerprint = source.fingerprint,
                recipeId = source.recipeId,
                version = source.version,
                quantity = source.quantity,
                stationDefinitionId = source.stationDefinitionId,
                stationInstanceId = source.stationInstanceId,
                mapId = source.mapId,
                actorId = source.actorId,
                controlRevision = source.controlRevision,
                sessionId = source.sessionId,
                ledgerRevision = source.ledgerRevision,
                accessGeneration = source.accessGeneration,
                consumptions = source.consumptions == null ? null : new CraftReceiptConsumption[source.consumptions.Length],
                outputs = source.outputs == null ? null : new CraftReceiptOutput[source.outputs.Length]
            };
            if (copy.consumptions != null)
                for (int i = 0; i < copy.consumptions.Length; i++)
                {
                    var value = source.consumptions[i];
                    copy.consumptions[i] = value == null ? null : new CraftReceiptConsumption { itemId = value.itemId, definitionId = value.definitionId, quantity = value.quantity };
                }
            if (copy.outputs != null)
                for (int i = 0; i < copy.outputs.Length; i++)
                {
                    var value = source.outputs[i];
                    copy.outputs[i] = value == null ? null : new CraftReceiptOutput { itemId = value.itemId, definitionId = value.definitionId, quantity = value.quantity };
                }
            return copy;
        }
    }
}
