/*
 * 脚本：BasicComboSettingsDrawer
 * 功能：显示统一连段参数、逐连接行为要求和旧资产的实际兼容值。
 * 边界：仅通过明确迁移按钮修改作者资产，保留 Undo 与通常的 Inspector 保存流程。
 */
using System;
using System.Linq;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;

namespace Ossastra.Development.Editor
{
    [CustomPropertyDrawer(typeof(BasicComboSettings))]
    internal sealed class BasicComboSettingsDrawer : PropertyDrawer
    {
        private static readonly string[] Fields = { "retentionSeconds", "continuationSeconds", "requireHit" };
        private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var version = property.FindPropertyRelative("version");
            return Line * (NeedsMigration(version) ? 3 : 4 + ConnectionCount(property)) - EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var version = property.FindPropertyRelative("version");
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                if (NeedsMigration(version)) DrawMigration(row, property);
                else
                {
                    foreach (string field in Fields)
                    {
                        row.y += Line;
                        EditorGUI.PropertyField(row, property.FindPropertyRelative(field));
                    }
                    DrawConnections(row, property);
                }
            }
            EditorGUI.EndProperty();
        }

        private static int ConnectionCount(SerializedProperty settings)
        {
            if (settings.serializedObject.isEditingMultipleObjects) return 0;
            var source = settings.serializedObject.targetObject;
            if (source is CharacterDefinitionSO character) return Math.Max(0, (character.basicAttacks?.Length ?? 0) - 1);
            if (source is MainActionProfileSO profile) return Math.Max(0, (profile.stages?.Length ?? 0) - 1);
            return 0;
        }

        private static void DrawConnections(Rect row, SerializedProperty settings)
        {
            var connections = settings.FindPropertyRelative("behaviorSuccessConnections");
            int count = ConnectionCount(settings);
            if (connections == null || count == 0) return;
            var selected = Enumerable.Range(0, connections.arraySize)
                .Select(index => connections.GetArrayElementAtIndex(index).intValue).ToHashSet();
            var labels = new[] { "沿用普攻准入", "前段行为成功" };
            bool changed = false;
            for (int from = 1; from <= count; from++)
            {
                row.y += Line;
                int current = selected.Contains(from) ? 1 : 0;
                int next = EditorGUI.Popup(row, $"第 {from} 段 → 第 {from + 1} 段", current, labels);
                if (current == next) continue;
                if (next == 1) selected.Add(from);
                else selected.Remove(from);
                changed = true;
            }
            if (!changed) return;
            connections.arraySize = selected.Count;
            int position = 0;
            foreach (int from in selected.OrderBy(index => index))
                connections.GetArrayElementAtIndex(position++).intValue = from;
        }

        private static bool NeedsMigration(SerializedProperty version)
            => version.hasMultipleDifferentValues || version.intValue == 0;

        private static void DrawMigration(Rect row, SerializedProperty property)
        {
            row.y += Line;
            string details = "使用旧参数的迁移值。";
            bool canMigrate = true;
            try
            {
                var settings = Resolve(property.serializedObject.targetObject);
                if (!property.serializedObject.isEditingMultipleObjects)
                    details = $"保留 {settings.retentionSeconds:G5} 秒 · 记忆 {settings.continuationSeconds:G5} 秒 · " +
                        (settings.requireHit ? "需命中" : "可空挥");
                // 多选时先核验全部来源，避免中途抛错后留下部分升级。
                foreach (var source in property.serializedObject.targetObjects) Resolve(source);
            }
            catch (ArgumentException error)
            {
                details = "旧连段参数无效 · " + error.ParamName;
                canMigrate = false;
            }
            EditorGUI.LabelField(row, details, EditorStyles.miniLabel);
            row.y += Line;
            using (new EditorGUI.DisabledScope(!canMigrate || EditorApplication.isPlaying))
            {
                if (!GUI.Button(EditorGUI.IndentedRect(row), "升级为三项连段参数")) return;
                property.serializedObject.ApplyModifiedProperties();
                var sources = property.serializedObject.targetObjects;
                Undo.RecordObjects(sources, "升级普攻连段参数");
                foreach (var source in sources)
                {
                    if (source is CharacterDefinitionSO character) character.MigrateComboSettings();
                    if (source is MainActionProfileSO weapon) weapon.MigrateComboSettings();
                    if (source is BasicChainProfile legacy)
                    {
                        legacy.MigrateComboSettings();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(legacy);
                    }
                    EditorUtility.SetDirty(source);
                }
                property.serializedObject.Update();
            }
        }

        private static BasicComboSettings Resolve(UnityEngine.Object source)
        {
            if (source is CharacterDefinitionSO character) return character.ComboSettings;
            if (source is MainActionProfileSO weapon) return weapon.ComboSettings;
            if (source is BasicChainProfile legacy) return legacy.ComboSettings;
            throw new ArgumentException("无可升级的普攻连段来源");
        }
    }
}
