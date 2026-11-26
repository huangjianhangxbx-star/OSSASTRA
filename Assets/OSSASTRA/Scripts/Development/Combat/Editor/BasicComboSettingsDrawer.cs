/*
 * 脚本：BasicComboSettingsDrawer
 * 功能：显示统一连段参数和版本未升级资产的实际兼容值。
 * 边界：仅通过明确迁移按钮修改作者资产，保留 Undo 与通常的 Inspector 保存流程。
 */
using System;
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
            return Line * (NeedsMigration(version) ? 3 : 4) - EditorGUIUtility.standardVerticalSpacing;
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
                }
            }
            EditorGUI.EndProperty();
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
