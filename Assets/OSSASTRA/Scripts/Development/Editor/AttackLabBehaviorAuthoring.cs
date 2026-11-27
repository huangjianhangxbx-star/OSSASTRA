/*
 * 功能：在招式 Inspector 内选择、创建和编辑专用行为，供行为候选与正式招式共用。
 * 边界：配置归引用者或作者资产所有。复制到本招式前，保留共享执行器只读提示。
 */
using System;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    [CustomEditor(typeof(AbilityDefinitionSO))]
    internal sealed class AbilityBehaviorSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "executor");
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            AttackLabBehaviorAuthoring.DrawBinding(serializedObject, false);
            var ability = (AbilityDefinitionSO)target;
            if (ability.action && GUILayout.Button("定位动作与 Mapping"))
            {
                Selection.activeObject = ability.action;
                EditorGUIUtility.PingObject(ability.action);
            }
        }
    }

    internal static class AttackLabBehaviorAuthoring
    {
        internal static void DrawBinding(SerializedObject owner, bool allowBasicBehavior)
        {
            var binding = owner.FindProperty("executor");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("专用行为", EditorStyles.boldLabel);
            var before = binding.objectReferenceValue as AbilityExecutorSO;
            var picked = (AbilityExecutorSO)EditorGUILayout.ObjectField("行为配置", before, typeof(AbilityExecutorSO), false);
            if (picked != before)
            {
                string mismatch = Compatibility(picked, allowBasicBehavior);
                if (mismatch != null) EditorUtility.DisplayDialog("无法关联行为", mismatch, "关闭");
                else
                {
                    binding.objectReferenceValue = picked;
                    owner.ApplyModifiedProperties();
                    owner.Update();
                }
            }
            binding = owner.FindProperty("executor");
            var executor = binding.objectReferenceValue as AbilityExecutorSO;
            if (!executor)
            {
                EditorGUILayout.LabelField("无专用行为", EditorStyles.miniLabel);
                if (!allowBasicBehavior)
                    EditorGUILayout.HelpBox("技能定义需要有效的执行器资产。", MessageType.Info);
                else if (AssetDatabase.Contains(owner.targetObject) && GUILayout.Button("添加专用行为…"))
                {
                    var menu = new GenericMenu();
                    Object target = owner.targetObject;
                    menu.AddItem(new GUIContent("锁链匕首"), false, () => BindCopy(target, null));
                    menu.ShowAsContext();
                }
                return;
            }

            bool shared = !AssetDatabase.IsSubAsset(executor) ||
                AssetDatabase.GetAssetPath(executor) != AssetDatabase.GetAssetPath(owner.targetObject);

            if (shared)
            {
                EditorGUILayout.HelpBox("共享执行器由引用的资产管理。可定位来源，或复制一份供本招式独立编辑。", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("定位共享执行器")) EditorGUIUtility.PingObject(executor);
                    using (new EditorGUI.DisabledScope(!AssetDatabase.Contains(owner.targetObject)))
                        if (GUILayout.Button("复制到本招式")) BindCopy(owner.targetObject, executor);
                }
            }
            using (new EditorGUI.DisabledScope(shared))
                DrawParameters(executor);
            if (GUILayout.Button("解除行为引用"))
            {
                // 解除引用保留原资产及 Undo 所需对象，也避免删除共享引用仍在使用的配置。
                binding.objectReferenceValue = null;
                owner.ApplyModifiedProperties();
            }
        }

        internal static bool DrawParameters(AbilityExecutorSO executor)
        {
            using (var data = new SerializedObject(executor))
            {
                if (executor is ChainDaggerExecutorSO)
                {
                    var projectile = data.FindProperty("projectile");
                    EditorGUILayout.PropertyField(projectile.FindPropertyRelative("range"), new GUIContent("射程（U）"));
                    EditorGUILayout.PropertyField(projectile.FindPropertyRelative("speed"), new GUIContent("飞行速度（U/s）"));
                    EditorGUILayout.PropertyField(projectile.FindPropertyRelative("collisionRadius"), new GUIContent("接触半径（U）"));
                    EditorGUILayout.PropertyField(projectile.FindPropertyRelative("collisionLayers"), new GUIContent("接触层"));
                    EditorGUILayout.PropertyField(data.FindProperty("pullSpeed"), new GUIContent("回拉速度（U/s）"));
                    EditorGUILayout.PropertyField(data.FindProperty("pullTimeout"), new GUIContent("回拉期限（秒）"));
                    EditorGUILayout.PropertyField(data.FindProperty("dockDistance"), new GUIContent("停靠距离（U）"));
                    EditorGUILayout.PropertyField(data.FindProperty("dockTolerance"), new GUIContent("到位容差（U）"));
                    EditorGUILayout.PropertyField(data.FindProperty("heightTolerance"), new GUIContent("高度差上限（U）"));
                    EditorGUILayout.PropertyField(data.FindProperty("maxOwnerDrift"), new GUIContent("回拉期间自身移动上限（U）"));
                    return data.ApplyModifiedProperties();
                }
                var property = data.GetIterator();
                bool enter = true;
                while (property.NextVisible(enter))
                {
                    enter = false;
                    if (property.propertyPath != "m_Script")
                        EditorGUILayout.PropertyField(property, true);
                }
                return data.ApplyModifiedProperties();
            }
        }

        private static string Compatibility(AbilityExecutorSO executor, bool basic)
        {
            if (!executor) return null;
            if (!basic) return executor is ChainDaggerExecutorSO ? "锁链匕首使用普通攻击的释放事件和接段规则。" : null;
            try
            {
                return executor.CompileExecutor().SupportsBasicAction ? null : "该执行器只支持原技能入口。";
            }
            catch (ArgumentException error) { return error.Message; }
        }

        internal static AbilityExecutorSO CopyToOwner(Object owner, AbilityExecutorSO existing)
        {
            if (!AssetDatabase.IsMainAsset(owner) || !AssetDatabase.IsOpenForEdit(owner))
                throw new InvalidOperationException("请选择可写的招式主资产。");
            var copy = existing ? Object.Instantiate(existing) : ScriptableObject.CreateInstance<ChainDaggerExecutorSO>();
            copy.name = existing ? existing.name + "（招式内）" : "锁链匕首";
            copy.hideFlags = HideFlags.None;
            try
            {
                AssetDatabase.AddObjectToAsset(copy, owner);
                Undo.RegisterCreatedObjectUndo(copy, "新增招式行为配置");
                return copy;
            }
            catch
            {
                Object.DestroyImmediate(copy, true);
                throw;
            }
        }

        private static void BindCopy(Object target, AbilityExecutorSO existing)
        {
            if (!target) return;
            try
            {
                using (var owner = new SerializedObject(target))
                {
                    var binding = owner.FindProperty("executor");
                    if (binding == null || binding.objectReferenceValue != existing) return;
                    binding.objectReferenceValue = CopyToOwner(target, existing);
                    owner.ApplyModifiedProperties();
                }
            }
            catch (InvalidOperationException error)
            {
                EditorUtility.DisplayDialog("无法创建专用行为", error.Message, "关闭");
            }
        }
    }
}
