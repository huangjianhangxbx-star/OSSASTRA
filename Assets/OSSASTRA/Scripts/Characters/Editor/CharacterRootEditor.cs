/*
 * 脚本：CharacterRootEditor
 * 功能：在角色根提供身体/动画SO/Rig导航及已执行表现的只读信息，原序列化字段完整保留。
 * 边界：绘制不初始化、不解析Router、不受理/推进/播放动作，不把Prefab默认伪作实例覆盖。
 * 协作：从实际view读取CharacterAnimationNavigation；运行数据显示Controller/Composer已保存结果。
 */
using UnityEditor;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.CharacterAnimation;

namespace Ossastra.Authoring
{
    using CharacterRoot = Ossastra.Characters.CharacterRoot;
    using EAnimationLogicState = Ossastra.CharacterAnimation.EAnimationLogicState;
    [CustomEditor(typeof(CharacterRoot)), CanEditMultipleObjects]
    public sealed class CharacterRootEditor : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying && targets.Length == 1;

        public override void OnInspectorGUI()
        {
            if (targets.Length == 1) DrawNavigation((CharacterRoot)target);
            else EditorGUILayout.HelpBox("多选保留原字段编辑；请选择一个角色查看其实际身体绑定与导航。", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("原有角色配置", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }

        private void DrawNavigation(CharacterRoot root)
        {
            var bindings = CharacterAnimationNavigation.Read(root);
            EditorGUILayout.LabelField("身体动画与配置", EditorStyles.boldLabel);
            var prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject);
            EditorGUILayout.HelpBox(EditorUtility.IsPersistent(root) ? "Prefab资产默认配置（不是当前场景实例）" :
                "当前场景实例：" + root.gameObject.scene.path + (string.IsNullOrEmpty(prefab) ? "\n没有Prefab来源" : "\nPrefab来源：" + prefab), MessageType.Info);
            if (!string.IsNullOrEmpty(bindings.problem)) EditorGUILayout.HelpBox(bindings.problem, MessageType.Warning);
            Reference("身体view绑定", bindings.boundView, root, "view");
            Reference("Spine对象", bindings.skeleton);
            Reference("Controller", bindings.controller);
            Reference("ConfigManager", bindings.config);
            Reference("Composer", bindings.composer);
            Reference("Handle", bindings.handle);
            Reference("Base Map", bindings.baseMap, bindings.config, "baseMap");
            Reference("Logic Router", bindings.router, bindings.config, "logicRouter");
            if (bindings.config != null)
            {
                Reference("Status Overrides", bindings.config.statusOverrides, bindings.config, "statusOverrides");
                var overrides = bindings.config.itemOverrides;
                EditorGUILayout.LabelField("Item Overrides", overrides == null ? "未配置" : overrides.Count.ToString());
                if (overrides != null) for (var i = 0; i < overrides.Count; i++) Reference("  覆盖 " + i, overrides[i], bindings.config, "itemOverrides.Array.data[" + i + "]");
            }
            Reference("Character Definition", bindings.definition, root, "characterDefinition");
            Reference("Rig Profile", bindings.rig);
            EditorGUILayout.HelpBox(bindings.equipmentOrigin, MessageType.None);
            DrawEquipment("右手", bindings.rightEquipment);
            DrawEquipment("左手", bindings.leftEquipment);
            using (new EditorGUILayout.HorizontalScope())
            {
                Button(root, bindings, "定位身体节点", CharacterAnimationNavigationTarget.Body);
                Button(root, bindings, "打开基础映射", CharacterAnimationNavigationTarget.BaseMap);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                Button(root, bindings, "打开逻辑路由", CharacterAnimationNavigationTarget.Router);
                Button(root, bindings, "打开 Rig 配置", CharacterAnimationNavigationTarget.Rig);
            }
            if (Application.isPlaying && bindings.controller != null) DrawRuntime(root, bindings);
        }

        private static void Reference(string label, Object value, Object owner = null, string propertyName = null)
        {
            // 禁止ObjectField写回；原有可编辑字段仍由默认Inspector/相应组件或SO管理。
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(label, value, typeof(Object), true);
                using (new EditorGUI.DisabledScope(value == null))
                    if (GUILayout.Button("定位", GUILayout.Width(42)))
                    { Selection.activeObject = value; EditorGUIUtility.PingObject(value); }
            }
            if (owner == null || string.IsNullOrEmpty(propertyName)) return;
            var property = new SerializedObject(owner).FindProperty(propertyName);
            if (property != null && property.prefabOverride) EditorGUILayout.LabelField("  生效位置", "场景实例覆盖");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(owner);
            if (source == null) return;
            var original = new SerializedObject(source).FindProperty(propertyName);
            if (original != null && original.propertyType == SerializedPropertyType.ObjectReference)
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("  Prefab默认", original.objectReferenceValue, typeof(Object), false);
        }

        private static void DrawEquipment(string label, EquipmentDefinitionSO equipment)
        {
            Reference(label + "装备定义", equipment);
            if (equipment == null) return;
            Reference("  身体覆盖", equipment.bodyAnimationOverride);
            Reference("  独立Spine表现", equipment.spinePresentation);
            if (equipment.spinePresentation != null) Reference("  装备动画映射", equipment.spinePresentation.animationMap);
        }

        private static void Button(CharacterRoot root, CharacterAnimationBindings bindings, string label, CharacterAnimationNavigationTarget target)
        {
            using (new EditorGUI.DisabledScope(CharacterAnimationNavigation.Target(bindings, target) == null))
                if (GUILayout.Button(label)) CharacterAnimationNavigation.Navigate(root, target);
        }

        private static void DrawRuntime(CharacterRoot root, CharacterAnimationBindings bindings)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("已执行表现 · 只读", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("当前身体片段", bindings.controller.CurrentClip ?? "无轨道");
            EditorGUILayout.LabelField("实际来源", bindings.controller.CurrentSource ?? "未提交");
            EditorGUILayout.LabelField("当前动作ID", root.DataBus != null && root.DataBus.ActionActive ? root.DataBus.ActionId.ToString() : "没有进行中的身体动作");
            var intent = bindings.composer == null ? null : bindings.composer.CurrentBodyIntent;
            EditorGUILayout.LabelField("已提交表现动作ID", intent == null ? "未提交" : intent.actionId.ToString());
            EditorGUILayout.LabelField("已提交表现阶段ID", intent == null ? "未提交" : intent.phaseId.ToString());
            var logic = intent?.frozenPlan?.runtimeLogicId ?? intent?.logicId ?? EAnimationLogicState.None;
            EditorGUILayout.LabelField("已记录逻辑ID", logic == EAnimationLogicState.None ? "本持续意图未记录最终ID" : logic.ToString());
            EditorGUILayout.HelpBox("有效配置引用见上方。此处读取已提交Intent；不重新解析规则。当前未记录具体命中规则，不作推断。", MessageType.None);
        }
    }
}
