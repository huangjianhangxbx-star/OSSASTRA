/*
 * 脚本：CharacterConfigurationCatalog
 * 功能：为现有工作台补充savedscene绑定/Prefab默认、SO引用角色、Rig与装备握持关系及导出来源。
 * 边界：只写工作台Generated报告；已开场只读、临时加法场finally关闭，不运行角色初始化，不修改/保存作者资产。
 * 协作：CharacterContentWorkbench仍导出完整模型/字段/动作；本类补关系，兼容引用与推荐入口分开。
 */
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ossastra.Characters;

namespace Ossastra.Authoring
{
    using CharacterRoot = Ossastra.Characters.CharacterRoot;

    public static class CharacterConfigurationCatalog
    {
        public static string FieldRole(UnityEngine.Object asset, string field)
        {
            if (asset is CharacterAnimationProfileSO ||
                asset is CharacterDefinitionSO && field == "animationProfile" ||
                asset is ActionDefinitionSO && (field == "animationKey" || field.StartsWith("animationKey.", StringComparison.Ordinal)) ||
                asset is EquipmentSpinePresentationSO presentation && presentation.animationMap != null && field.StartsWith("animations", StringComparison.Ordinal))
                return "historical-compatibility";
            return "serialized-configuration";
        }

        public static void Export(string scenePath, string reportPath, ScriptableObject[] assets, int modelCount)
        {
            WriteReferences(reportPath, assets);
            WriteSceneBindings(scenePath, reportPath);
            WriteMounts(reportPath, assets);
            File.WriteAllText(reportPath + "/export-metadata.json", JsonUtility.ToJson(new ExportMetadata
            {
                schema = 3, generatedAtUtc = DateTime.UtcNow.ToString("O"), gitHead = ReadGitHead(),
                reviewBaseline = "0b922064b217e46e274ec29b5c2349ac355aa2e1", scene = scenePath,
                unity = Application.unityVersion, source = "saved-scene-dependencies-and-clean-scene",
                soCount = assets.Length, modelCount = modelCount,
                note = "字段/指纹来自本机当前资产与磁盘；gitHead不代表没有未提交修改。运行表现与静态配置关系分开验证。"
            }), new System.Text.UTF8Encoding(false));
        }

        [Serializable]
        private sealed class ExportMetadata
        {
            public int schema, soCount, modelCount;
            public string generatedAtUtc, gitHead, reviewBaseline, scene, unity, source, note;
        }

        private static void WriteReferences(string path, IEnumerable<ScriptableObject> assets)
        {
            var rows = new List<string> { "asset\tguid\ttype\tfield\ttargetAsset\ttargetGuid\ttargetType\trole" };
            foreach (var asset in assets)
            {
                var source = AssetDatabase.GetAssetPath(asset);
                var property = new SerializedObject(asset).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script" || property.objectReferenceValue == null) continue;
                    var target = property.objectReferenceValue; var targetPath = AssetDatabase.GetAssetPath(target);
                    rows.Add(Row(source, AssetDatabase.AssetPathToGUID(source), asset.GetType().FullName, property.propertyPath,
                        targetPath, AssetDatabase.AssetPathToGUID(targetPath), target.GetType().FullName, FieldRole(asset, property.propertyPath)));
                }
            }
            File.WriteAllLines(path + "/SO-references.tsv", rows);
        }

        private static void WriteSceneBindings(string scenePath, string path)
        {
            var rows = new List<string> { "actor\tnode\tcomponent\tfield\tactualValue\torigin\tprefab\tprefabDefault\tactualTargetGuid" };
            // 导出的依赖清单来自已保存Scene，因此关系也从同一已保存Scene读取；不混入未保存现场。
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            var ownsScene = !scene.IsValid() || !scene.isLoaded;
            if (!ownsScene && scene.isDirty) throw new InvalidOperationException("请先保存或恢复目标场景，再导出磁盘基准。");
            if (ownsScene) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CharacterRoot>(true)).OrderBy(r => r.ActorId))
                {
                    var binding = CharacterAnimationNavigation.Read(root);
                    AddBinding(rows, root, root, "characterDefinition"); AddBinding(rows, root, root, "view");
                    if (binding.controller != null) AddBinding(rows, root, binding.controller, "skeleton");
                    if (binding.config != null)
                    {
                        AddBinding(rows, root, binding.config, "baseMap"); AddBinding(rows, root, binding.config, "logicRouter");
                        AddBinding(rows, root, binding.config, "statusOverrides");
                        var array = new SerializedObject(binding.config).FindProperty("itemOverrides");
                        if (array != null) for (var i = 0; i < array.arraySize; i++) AddBinding(rows, root, binding.config, array.GetArrayElementAtIndex(i).propertyPath);
                    }
                    var gear = root.GetComponent<CharacterEquipment>();
                    if (gear != null) AddBinding(rows, root, gear, "rightDefinition");
                    rows.Add(Row(root.ActorId, Hierarchy(root.transform), "CharacterEquipment", "left/right runtime slots", "Play由InventorySession绑定", "runtime-binding-not-an-edit-instance", "", "", ""));
                }
            }
            finally { if (ownsScene) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
            File.WriteAllLines(path + "/scene-bindings.tsv", rows);
        }

        private static void AddBinding(List<string> rows, CharacterRoot root, Component owner, string field)
        {
            var property = new SerializedObject(owner).FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;
            var original = PrefabUtility.GetCorrespondingObjectFromSource(owner);
            var defaultProperty = original == null ? null : new SerializedObject(original).FindProperty(field);
            var value = property.objectReferenceValue;
            rows.Add(Row(root.ActorId, Hierarchy(owner.transform), owner.GetType().FullName, field, Describe(value),
                property.prefabOverride ? "scene-instance-override" : original != null ? "prefab-default" : "scene-authored",
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(owner.gameObject), Describe(defaultProperty?.objectReferenceValue),
                value == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))));
        }

        private static void WriteMounts(string path, ScriptableObject[] assets)
        {
            var rows = new List<string> { "equipment\tpresentation\trig\tmodel\tsemantic\tkind\tbodyBone\tbodySlot\tpoint\tgrip\tgripExists\tlocalOffset\tlocalScale\trotationOffset\tsortingOrderOffset\tfollowRotation\tfollowColor\tfollowVisibility\tanimationMap" };
            foreach (var equipment in assets.OfType<EquipmentDefinitionSO>())
            {
                var presentation = equipment.spinePresentation; if (presentation == null) continue;
                foreach (var rig in assets.OfType<CharacterRigProfileSO>())
                {
                    var body = rig.model == null ? null : rig.model.GetSkeletonData(false);
                    var socket = string.IsNullOrEmpty(presentation.semanticSocketId) ? null : rig.Resolve(presentation.semanticSocketId, body);
                    var definition = rig.sockets.FirstOrDefault(s => s.semanticSocketId == presentation.semanticSocketId);
                    var equipmentData = presentation.skeletonData == null ? null : presentation.skeletonData.GetSkeletonData(false);
                    rows.Add(Row(AssetDatabase.GetAssetPath(equipment), AssetDatabase.GetAssetPath(presentation), AssetDatabase.GetAssetPath(rig), AssetDatabase.GetAssetPath(rig.model),
                        presentation.semanticSocketId, definition == null ? "legacy-explicit-bone" : definition.kind.ToString(), socket?.BoneName ?? presentation.bodyBone,
                        socket?.SlotName ?? presentation.bodySlot, definition?.pointAttachmentName, presentation.gripBone,
                        (equipmentData != null && equipmentData.FindBone(presentation.gripBone) != null).ToString(), presentation.localOffset.ToString("R"),
                        Number(presentation.localScale), Number(presentation.rotationOffset), presentation.sortingOrderOffset.ToString(),
                        presentation.followBoneRotation.ToString(), presentation.followSlotColor.ToString(), presentation.followSlotAttachmentVisibility.ToString(), AssetDatabase.GetAssetPath(presentation.animationMap)));
                }
            }
            File.WriteAllLines(path + "/rig-equipment-bindings.tsv", rows);
        }

        private static string Describe(UnityEngine.Object value)
        {
            if (value == null) return "";
            var path = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(path)) return path + (value is Component member ? " :: " + Hierarchy(member.transform) + " [" + member.GetType().Name + "]" : "");
            if (value is Component component) return Hierarchy(component.transform) + " [" + component.GetType().Name + "]";
            if (value is GameObject go) return Hierarchy(go.transform);
            return value.name;
        }
        private static string Hierarchy(Transform transform) => transform.parent == null ? transform.name : Hierarchy(transform.parent) + "/" + transform.name;
        private static string Row(params string[] fields) => string.Join("\t", fields.Select(f => (f ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " / ")));
        private static string Number(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static string ReadGitHead()
        {
            var root = Path.GetFullPath("."); var directory = Path.Combine(root, ".git");
            if (File.Exists(directory))
            {
                var pointer = File.ReadAllText(directory).Trim();
                if (!pointer.StartsWith("gitdir: ", StringComparison.Ordinal)) return "unresolved-git-directory";
                directory = Path.GetFullPath(Path.Combine(root, pointer.Substring(8)));
            }
            var headPath = Path.Combine(directory, "HEAD"); if (!File.Exists(headPath)) return "unresolved-head";
            var head = File.ReadAllText(headPath).Trim(); if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head;
            var reference = head.Substring(5); var loose = Path.Combine(directory, reference);
            if (File.Exists(loose)) return File.ReadAllText(loose).Trim();
            var packed = Path.Combine(directory, "packed-refs");
            return File.Exists(packed) ? File.ReadLines(packed).FirstOrDefault(line => line.EndsWith(" " + reference, StringComparison.Ordinal))?.Split(' ')[0] ?? "unresolved-reference" : "unresolved-reference";
        }
    }
}
