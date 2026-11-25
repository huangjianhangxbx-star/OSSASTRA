/*
 * 招式效果编辑验收。只创建本次拥有的临时 SO，覆盖两种招式、托管引用编辑、拷贝隔离、写回字段和 Undo。
 * 必须在 Edit 模式运行；finally 清除自建资产和本试验对象的 Undo 历史。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    public static class AttackLabImpactEditingAcceptance
    {
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("需要 Edit 模式");
            return Verify<ActionDefinitionSO>() + "\n" + Verify<AbilityDefinitionSO>();
        }

        private static void Check(bool condition, string operation)
        {
            if (!condition) throw new InvalidOperationException("招式效果编辑验收失败：" + operation);
        }

        private static string Verify<T>() where T : ScriptableObject
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/OSSASTRA/Configs/ImpactEditVerification.asset");
            T original = ScriptableObject.CreateInstance<T>();
            T draft = null;
            T applied = null;
            bool created = false;
            try
            {
                var sourceName = "ImpactEditVerification";
                original.name = sourceName;
                AssetDatabase.CreateAsset(original,path);
                created = AssetDatabase.Contains(original);
                Check(created, "创建临时来源");
                draft = Object.Instantiate(original);
                draft.hideFlags = HideFlags.HideAndDontSave;
                AttackLabImpactSamples.Configure(draft, 0);
                var candidate = AttackLabImpactSamples.Read(draft);
                Check(AttackLabImpactSamples.Read(original).entries.Count == 0, "创建候选未修改基线");
                Check(AttackLabImpactSamples.Problems(candidate) == null && candidate.entries.Count == 3, "配置轻击");
                var firstId = candidate.entries[0].id;

                // 走与属性抽屉相同的 SerializedProperty 接口，模拟添加、排序、复制、删除、禁用和总旁路。
                using (var so = new SerializedObject(draft))
                {
                    var list = so.FindProperty("effects.entries");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(3).managedReferenceValue = new LaunchEffect();
                    list.MoveArrayElement(3,0);
                    so.ApplyModifiedProperties();
                    Check(AttackLabImpactSamples.Read(draft).entries[0] is LaunchEffect, "添加并排序");
                    so.Update();
                    list = so.FindProperty("effects.entries");
                    var originalEntry = list.GetArrayElementAtIndex(1).managedReferenceValue as SelfDisplacementEffect;
                    var copy = JsonUtility.FromJson<SelfDisplacementEffect>(JsonUtility.ToJson(originalEntry));
                    copy.id = Guid.NewGuid().ToString("N");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(4).managedReferenceValue = copy;
                    list.DeleteArrayElementAtIndex(0);
                    list.GetArrayElementAtIndex(0).FindPropertyRelative("enabled").boolValue = false;
                    so.FindProperty("effects.bypass").boolValue = true;
                    so.ApplyModifiedProperties();
                }
                candidate = AttackLabImpactSamples.Read(draft);
                Check(candidate.entries.Count == 4 && candidate.entries[0].id == firstId && !candidate.entries[0].enabled &&
                      candidate.entries.Last().id != firstId && candidate.bypass, "删除／禁用／复制身份／总旁路");
                applied = Object.Instantiate(draft);
                candidate.bypass = false;
                Check(AttackLabImpactSamples.Read(applied).bypass, "应用的 B 与待试副本独立");

                // 普攻/技能走面板的同一字段定向写入函数，当前 Undo 组内仅包含此项。
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                AttackLabImpactPanel.WriteEffects(draft, original);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                Check(AttackLabImpactSamples.Read(original).entries.Count == 4, "写回共享来源");
                Check(!ReferenceEquals(AttackLabImpactSamples.Read(original).entries[0], candidate.entries[0]), "写回深复制");
                Undo.PerformUndo();
                Check(AttackLabImpactSamples.Read(original).entries.Count == 0, "写回撤销");
                Undo.PerformRedo();
                Check(AttackLabImpactSamples.Read(original).entries.Count == 4, "写回重做");
                EditorUtility.SetDirty(original);
                AssetDatabase.SaveAssetIfDirty(original);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                original = AssetDatabase.LoadAssetAtPath<T>(path);
                var saved = AttackLabImpactSamples.Read(original);
                Check(saved.entries[0] is SelfDisplacementEffect && saved.entries[0].id == firstId && saved.entries.Count == 4,
                    "重导入类型、身份和顺序");
                Check(AttackLabImpactSamples.Problems(saved) == null, "重导入校验");
                return typeof(T).Name + " · 托管引用编辑／基线隔离／Undo/Redo／保存重导入通过";
            }
            finally
            {
                if (draft) { Undo.ClearUndo(draft); Object.DestroyImmediate(draft); }
                if (applied) Object.DestroyImmediate(applied);
                if (original) Undo.ClearUndo(original);
                if (created) AssetDatabase.DeleteAsset(path);
                else if (original && !AssetDatabase.Contains(original)) Object.DestroyImmediate(original);
            }
        }
    }
}
