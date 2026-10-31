using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ossastra.Crafting.Core
{
    /// <summary>命令事实指纹，不是授权或提交回执；调用者仍须验证当前会话和账本。</summary>
    public static class CraftIdentity
    {
        public static string Fingerprint(string recipeId, int version, string stationDefinitionId,
            string stationInstanceId, string mapId, string actorId, long controlRevision,
            string interactionSessionId, string requestId, int quantity, long ledgerRevision,
            long accessGeneration, IEnumerable<CraftMaterialConsumption> consumptions)
        {
            CraftValidation.Identity(recipeId, nameof(recipeId));
            CraftValidation.Positive(version, nameof(version));
            CraftValidation.Identity(stationDefinitionId, nameof(stationDefinitionId));
            CraftValidation.Identity(stationInstanceId, nameof(stationInstanceId));
            CraftValidation.Identity(mapId, nameof(mapId));
            CraftValidation.Identity(actorId, nameof(actorId));
            CraftValidation.Identity(interactionSessionId, nameof(interactionSessionId));
            CraftValidation.Identity(requestId, nameof(requestId));
            if (controlRevision < 0) throw new ArgumentOutOfRangeException(nameof(controlRevision));
            if (ledgerRevision < 0) throw new ArgumentOutOfRangeException(nameof(ledgerRevision));
            if (accessGeneration < 0) throw new ArgumentOutOfRangeException(nameof(accessGeneration));
            if (quantity < 1 || quantity > 99) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (consumptions == null) throw new ArgumentNullException(nameof(consumptions));

            var ordered = new List<CraftMaterialConsumption>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var consumption in consumptions)
            {
                if (consumption == null) throw new ArgumentException("消费事实不能为空。", nameof(consumptions));
                if (!ids.Add(consumption.ItemId)) throw new ArgumentException("消费实例ID重复。", nameof(consumptions));
                ordered.Add(consumption);
            }
            ordered.Sort((a, b) =>
            {
                int order = StringComparer.Ordinal.Compare(a.DefinitionId, b.DefinitionId);
                return order != 0 ? order : StringComparer.Ordinal.Compare(a.ItemId, b.ItemId);
            });

            // 固定版本、固定字段序；所有字段按UTF8字节数加32位小端长度前缀，含计数和数字。
            // 严格UTF8拒绝无效代理项，避免替换字符使两个不同.NET字符串落到同一字节流。
            var encoding = new UTF8Encoding(false, true);
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, encoding, true))
                {
                    Write(writer, encoding, "Ossastra.Crafting.Command.v1");
                    Write(writer, encoding, recipeId);
                    Write(writer, encoding, Number(version));
                    Write(writer, encoding, stationDefinitionId);
                    Write(writer, encoding, stationInstanceId);
                    Write(writer, encoding, mapId);
                    Write(writer, encoding, actorId);
                    Write(writer, encoding, Number(controlRevision));
                    Write(writer, encoding, interactionSessionId);
                    Write(writer, encoding, requestId);
                    Write(writer, encoding, Number(quantity));
                    Write(writer, encoding, Number(ledgerRevision));
                    Write(writer, encoding, Number(accessGeneration));
                    Write(writer, encoding, Number(ordered.Count));
                    foreach (var consumption in ordered)
                    {
                        Write(writer, encoding, consumption.DefinitionId);
                        Write(writer, encoding, consumption.ItemId);
                        Write(writer, encoding, Number(consumption.Quantity));
                    }
                    writer.Flush();
                }
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(buffer.ToArray());
                    var result = new StringBuilder(64);
                    foreach (byte value in hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                    return result.ToString();
                }
            }
        }

        private static string Number(long value) { return value.ToString(CultureInfo.InvariantCulture); }

        private static void Write(BinaryWriter writer, Encoding encoding, string value)
        {
            byte[] bytes = encoding.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
