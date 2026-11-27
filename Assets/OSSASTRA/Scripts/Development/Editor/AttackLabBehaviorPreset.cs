/*
 * 功能：按角色、连段来源和招式资产标识保存与读取本机行为候选。
 * 边界：使用 Unity 序列化保存多态效果，写入 UserSettings，正式招式由显式写回管理。
 */
using System;
using System.Globalization;
using System.IO;
using Ossastra.Characters;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabBehaviorPreset : ScriptableObject
    {
        [SerializeField] private ActionEffectList thirdEffects, fourthEffects;
        [SerializeField] private string executor;
        [SerializeField] private bool requireSuccess;

        private static string PathFor(string actor, Object origin, Object third, Object fourth)
        {
            string Identity(Object item)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string guid, out long localId))
                    throw new ArgumentException("本机候选需要可以定位的作者资产。");
                return guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
            }

            string key = Hash128.Compute(actor + "|" + Identity(origin) + "|" + Identity(third) + "|" + Identity(fourth)).ToString();
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings", "AttackLabBehaviorPresets", key + ".asset");
        }

        internal static void Store(string actor, Object origin, Object third, Object fourth, AttackLabBehaviorDraft draft)
            => StoreAt(PathFor(actor, origin, third, fourth), draft);

        internal static void StoreAt(string path, AttackLabBehaviorDraft draft)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var record = CreateInstance<AttackLabBehaviorPreset>();
            try
            {
                record.thirdEffects = draft.Third.effects;
                record.fourthEffects = draft.Fourth.effects;
                record.executor = EditorJsonUtility.ToJson(draft.Third.executor);
                record.requireSuccess = draft.RequireSuccess;
                InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { record }, staging, true);
                if (new FileInfo(staging).Length == 0) throw new IOException("本机候选的序列化结果为空。");
                if (File.Exists(path)) File.Replace(staging, path, null);
                else File.Move(staging, path);
            }
            finally
            {
                DestroyImmediate(record);
                if (File.Exists(staging)) File.Delete(staging);
            }
        }

        internal static bool Restore(string actor, Object origin, Object third, Object fourth, AttackLabBehaviorDraft draft)
            => RestoreAt(PathFor(actor, origin, third, fourth), draft);

        internal static bool RestoreAt(string path, AttackLabBehaviorDraft draft)
        {
            if (!File.Exists(path)) return false;
            var items = InternalEditorUtility.LoadSerializedFileAndForget(path);
            try
            {
                if (items == null || items.Length != 1 || !(items[0] is AttackLabBehaviorPreset record) ||
                    record.thirdEffects == null || record.fourthEffects == null || string.IsNullOrEmpty(record.executor))
                    throw new InvalidDataException("本机候选内容不完整。");
                EditorJsonUtility.FromJsonOverwrite(record.executor, draft.Third.executor);
                draft.Third.effects = record.thirdEffects;
                draft.Fourth.effects = record.fourthEffects;
                draft.RequireSuccess = record.requireSuccess;
                return true;
            }
            finally
            {
                if (items != null)
                    foreach (var item in items)
                        if (item) DestroyImmediate(item);
            }
        }
    }
}
