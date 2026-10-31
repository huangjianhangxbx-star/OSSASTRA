#if UNITY_EDITOR
/* 脚本：CraftingAuthoring
 * 功能：只读校验本场合成组装、配方与原库存目录引用；提供显式空实例ID作者操作。
 * 边界：不运行Awake、不绑定服务、不创建运行时状态、不保存或进入Play。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ossastra.Characters;
using Ossastra.Commerce.Runtime;
using Ossastra.Crafting.Core;
using Ossastra.Inventory;

namespace Ossastra.Crafting.Editor
{
    using ItemDefinitionSO = Ossastra.Characters.ItemDefinitionSO;
    /// <summary>作者校验入口；只读检查明确场景，不向全局搜索并自动接线任何Root。</summary>
    public static class CraftingAuthoring
    {
        [MenuItem("OSSASTRA/Crafting/Validate Active Scene")]
        public static void ValidateActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var errors = ValidateScene(scene);
            if (errors.Length == 0) Debug.Log("[Crafting] 作者配置校验通过：" + scene.path + "。未修改场景。");
            else Debug.LogError("[Crafting] 作者配置校验拒绝（" + errors.Length + "项）：\n" + string.Join("\n", errors));
        }

        /// <summary>返回全部可定位的错误；空数组表示此场作者配置通过，不代表运行/保存验收。</summary>
        public static string[] ValidateScene(Scene scene)
        {
            var errors = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                errors.Add("目标场景无效或尚未加载。");
                return errors.ToArray();
            }
            var roots = Components<CraftingRoot>(scene);
            if (roots.Length != 1)
            {
                errors.Add("场景必须且只能有一个CraftingRoot（包含停用对象），当前：" + roots.Length + "。");
                return errors.ToArray();
            }
            // Additive中另一个Root会造成双宿主，校验不通过隐藏/停用对象来掩盖重复配置。
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (other != scene && other.isLoaded && Components<CraftingRoot>(other).Length != 0)
                    errors.Add("另一个已加载场景也含CraftingRoot：" + other.path + "。请先明确唯一宿主。");
            }
            var root = roots[0];
            if (string.IsNullOrWhiteSpace(root.mapId)) errors.Add("CraftingRoot.mapId不能为空。");
            RequireSceneReference(root.inventory, scene, "CraftingRoot.inventory", errors);
            RequireSceneReference(root.commerce, scene, "CraftingRoot.commerce", errors);
            RequireSceneReference(root.inventoryView, scene, "CraftingRoot.inventoryView", errors);
            RequireSceneReference(root.userInterface, scene, "CraftingRoot.userInterface", errors);
            RequireSceneReference(root.mapView, scene, "CraftingRoot.mapView", errors);
            RequireSceneReference(root.gameplayCamera, scene, "CraftingRoot.gameplayCamera", errors);
            if (Components<InventorySession>(scene).Length != 1) errors.Add("本场InventorySession必须唯一。");
            if (Components<CommerceService>(scene).Length != 1) errors.Add("本场CommerceService共同保存宿主必须唯一。");
            if (root.commerce != null)
            {
                if (root.commerce.inventory != root.inventory) errors.Add("CommerceService.inventory与CraftingRoot.inventory不是同一引用。");
                if (root.commerce.inventoryView != root.inventoryView) errors.Add("CommerceService.inventoryView与CraftingRoot.inventoryView不是同一引用。");
                if (!StringComparer.Ordinal.Equals(root.commerce.mapId, root.mapId)) errors.Add("CraftingRoot.mapId与CommerceService.mapId不一致。");
            }
            if (root.inventoryView != null && root.inventoryView.Session != root.inventory)
                errors.Add("原InventoryView.Session与CraftingRoot.inventory不是同一引用。");
            if (root.inventory != null && root.inventory.Coordinator == null) errors.Add("原库存缺少明确Coordinator引用。");
            if (root.mapView != null && root.gameplayCamera != null && root.mapView.gameplayCamera != root.gameplayCamera)
                errors.Add("MapResearchRoot.gameplayCamera与合成根镜头不一致。");

            var items = ReadInventoryCatalog(root.inventory, errors);
            var definitions = ValidateStationCatalog(root, errors);
            ValidateRecipes(root, items, definitions, errors);
            ValidateStations(root, scene, definitions, errors);
            return errors.ToArray();
        }

        private static Dictionary<string, ItemDefinitionSO> ReadInventoryCatalog(InventorySession inventory, List<string> errors)
        {
            var items = new Dictionary<string, ItemDefinitionSO>(StringComparer.Ordinal);
            if (inventory == null) return items;
            // 编辑模式不调用Definition()：它的运行时字典直到Start才建立。只读真实序列化目录。
            var data = new SerializedObject(inventory);
            var catalog = data.FindProperty("catalog");
            if (catalog == null || !catalog.isArray)
            {
                errors.Add("原InventorySession缺少可读取的catalog序列化字段。");
                return items;
            }
            for (int i = 0; i < catalog.arraySize; i++)
            {
                var item = catalog.GetArrayElementAtIndex(i).objectReferenceValue as ItemDefinitionSO;
                string label = "原库存catalog[" + i + "]";
                if (item == null) { errors.Add(label + "引用为空。"); continue; }
                if (string.IsNullOrWhiteSpace(item.definitionId)) { errors.Add(label + "定义ID为空。"); continue; }
                if (items.ContainsKey(item.definitionId)) errors.Add(label + "定义ID重复：" + item.definitionId);
                else items.Add(item.definitionId, item);
                try { item.FreezeInventory(); }
                catch (Exception ex) { errors.Add(label + "不能冻结为原库存定义：" + ex.Message); }
            }
            return items;
        }

        private static HashSet<CraftingStationDefinitionSO> ValidateStationCatalog(CraftingRoot root, List<string> errors)
        {
            var definitions = new HashSet<CraftingStationDefinitionSO>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (root.stationCatalog == null) { errors.Add("CraftingRoot.stationCatalog不能为null。"); return definitions; }
            for (int i = 0; i < root.stationCatalog.Length; i++)
            {
                var definition = root.stationCatalog[i];
                if (definition == null) { errors.Add("stationCatalog[" + i + "]引用为空。"); continue; }
                try { definition.Stamp(); }
                catch (Exception ex) { errors.Add("stationCatalog[" + i + "]无效：" + ex.Message); }
                if (!definitions.Add(definition) || !ids.Add(definition.definitionId ?? string.Empty))
                    errors.Add("stationCatalog工位定义重复：" + definition.definitionId);
            }
            return definitions;
        }

        private static void ValidateRecipes(CraftingRoot root, Dictionary<string, ItemDefinitionSO> items,
            HashSet<CraftingStationDefinitionSO> stations, List<string> errors)
        {
            if (root.recipes == null || root.recipes.Length == 0 || root.recipes.Length > 256)
            { errors.Add("CraftingRoot.recipes数量必须在1至256之间。"); return; }
            var frozen = new List<CraftRecipe>();
            for (int i = 0; i < root.recipes.Length; i++)
            {
                var recipe = root.recipes[i];
                if (recipe == null) { errors.Add("recipes[" + i + "]配方引用为空。"); continue; }
                string label = "配方[" + i + "] " + recipe.recipeId;
                try { frozen.Add(recipe.Freeze()); }
                catch (Exception ex) { errors.Add(label + "冻结拒绝：" + ex.Message); }
                if (!stations.Any(s => s.supported && s.capabilities != null && s.capabilities.Contains(recipe.stationCapability)))
                    errors.Add(label + "没有支持其能力的工位类型：" + recipe.stationCapability);
                if (recipe.ingredients != null)
                    for (int j = 0; j < recipe.ingredients.Length; j++)
                    {
                        var source = recipe.ingredients[j];
                        if (source == null || source.item == null) { errors.Add(label + "材料[" + j + "]引用为空。"); continue; }
                        ValidateItemReference(source.item, items, label + "材料[" + j + "]", errors);
                        if (source.item.equipment != null) errors.Add(label + "材料不能是装备：" + source.item.definitionId);
                    }
                if (recipe.outputs != null)
                    for (int j = 0; j < recipe.outputs.Length; j++)
                    {
                        var output = recipe.outputs[j];
                        if (output == null || output.item == null) { errors.Add(label + "产物[" + j + "]引用为空。"); continue; }
                        ValidateItemReference(output.item, items, label + "产物[" + j + "]", errors);
                        if (output.item.equipment != null && !StringComparer.Ordinal.Equals(output.item.definitionId, output.item.equipment.definitionId))
                            errors.Add(label + "装备产物的ItemDefinitionSO与EquipmentDefinitionSO定义ID不一致。");
                    }
            }
            try { new CraftCatalog(frozen); }
            catch (Exception ex) { errors.Add("配方冻结目录拒绝：" + ex.Message); }
        }

        private static void ValidateItemReference(ItemDefinitionSO item, Dictionary<string, ItemDefinitionSO> items, string label, List<string> errors)
        {
            ItemDefinitionSO original;
            if (string.IsNullOrWhiteSpace(item.definitionId) || !items.TryGetValue(item.definitionId, out original) || original != item)
                errors.Add(label + "必须引用原库存catalog中的同一SO，不能仅同名/同ID：" + item.definitionId);
        }

        private static void ValidateStations(CraftingRoot root, Scene scene, HashSet<CraftingStationDefinitionSO> definitions, List<string> errors)
        {
            if (root.stations == null || root.stations.Length > 128) { errors.Add("CraftingRoot.stations必须为非null且最多128项。"); return; }
            var instances = new HashSet<CraftingStation>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < root.stations.Length; i++)
            {
                var station = root.stations[i];
                string label = "工位[" + i + "]";
                if (station == null) { errors.Add(label + "引用为空。"); continue; }
                label += " " + station.name;
                if (!instances.Add(station)) errors.Add(label + "同一组件被重复登记。");
                if (station.gameObject.scene != scene) errors.Add(label + "不在目标场景中。");
                if (string.IsNullOrWhiteSpace(station.instanceId)) errors.Add(label + "实例ID为空。");
                else if (!ids.Add(station.instanceId)) errors.Add(label + "实例ID重复：" + station.instanceId);
                if (!StringComparer.Ordinal.Equals(station.mapId, root.mapId)) errors.Add(label + "mapId与CraftingRoot不一致。");
                if (station.definition == null || !definitions.Contains(station.definition)) errors.Add(label + "定义必须是Root.stationCatalog的同一引用。");
                if (station.interactionAnchor == null || !station.interactionAnchor.IsChildOf(station.transform)) errors.Add(label + "必须显式指定自身层级内的interactionAnchor。");
                if (!Finite(station.interactionDistance) || station.interactionDistance < .1f || !Finite(station.maximumHeightDifference) ||
                    station.maximumHeightDifference < .05f || station.maximumHeightDifference > 1f || !Finite(station.sightHeight) || station.sightHeight < .1f)
                    errors.Add(label + "距离/同层阈值/视线高度超出合法配置范围。");
                if ((station.interactionLayers.value & (1 << station.gameObject.layer)) == 0) errors.Add(label + "自身Layer不在interactionLayers中。");
                if (station.ownedColliders == null || station.ownedColliders.Length == 0) errors.Add(label + "必须明确登记至少一个真实自有Collider。");
                else
                {
                    var colliders = new HashSet<Collider>();
                    bool solid = false;
                    foreach (var collider in station.ownedColliders)
                    {
                        if (collider == null || !collider.transform.IsChildOf(station.transform)) { errors.Add(label + "自有Collider为空或不属于该工位层级。"); continue; }
                        if (!colliders.Add(collider)) errors.Add(label + "ownedColliders重复引用。");
                        if (collider.enabled && !collider.isTrigger) solid = true;
                        if (station.billboardVisual != null && collider.transform.IsChildOf(station.billboardVisual)) errors.Add(label + "billboardVisual会旋转真实Collider，请分离视觉与碰撞层。");
                    }
                    if (!solid) errors.Add(label + "缺少enabled且非Trigger的真实碰撞。");
                }
                if (station.billboardVisual != null && (station.billboardVisual == station.transform || !station.billboardVisual.IsChildOf(station.transform)))
                    errors.Add(label + "二维视觉必须是独立子层，不能直接旋转工位根。");
            }
            foreach (var station in Components<CraftingStation>(scene))
                if (!instances.Contains(station)) errors.Add("场景工位尚未显式加入Root.stations：" + station.name);
            // 中转场允许0个工位；全目录仍必须存在，以便同快照恢复时验证历史回执。
        }

        /// <summary>唯一显式作者写操作：只给尚未登记且ID为空的场景实例生成身份，不自动登记或保存。</summary>
        public static bool TryGenerateInstanceId(CraftingStation station, out string result)
        {
            result = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { result = "只能在Edit模式生成场景实例ID。"; return false; }
            if (station == null || EditorUtility.IsPersistent(station) || !station.gameObject.scene.IsValid() || !station.gameObject.scene.isLoaded)
            { result = "请选择已加载场景中的实例；不能给Prefab资产写永久实例ID。"; return false; }
            if (!string.IsNullOrEmpty(station.instanceId)) { result = "已有实例ID不会被覆盖；复制实例需先手动清空其ID。"; return false; }
            var roots = Components<CraftingRoot>(station.gameObject.scene);
            if (roots.Length != 1 || roots[0].stations == null) { result = "场景需有唯一Root及非null的stations登记数组。"; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var instances = new HashSet<CraftingStation>();
            foreach (var registered in roots[0].stations)
            {
                if (registered == station) { result = "目标已登记；该按钮只服务尚未登记的新实例。"; return false; }
                if (registered == null || string.IsNullOrWhiteSpace(registered.instanceId) || !instances.Add(registered) || !ids.Add(registered.instanceId))
                { result = "现有Root.stations含空引用、空ID或重复，需先明确修复，不能继续生成。"; return false; }
            }
            string next = Guid.NewGuid().ToString("N");
            if (ids.Contains(next)) { result = "新ID与已有登记冲突，本次未修改，请重新操作。"; return false; }
            Undo.RecordObject(station, "为新装备台实例生成ID");
            station.instanceId = next;
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            EditorUtility.SetDirty(station);
            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
            result = "已生成 " + next + "。请显式加入Root.stations，确认mapId/定义/锚点后保存场景。";
            return true;
        }

        private static T[] Components<T>(Scene scene) where T : Component
        { return scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray(); }
        private static void RequireSceneReference(Component value, Scene scene, string label, List<string> errors)
        {
            if (value == null) errors.Add(label + "引用缺失。");
            else if (value.gameObject.scene != scene) errors.Add(label + "必须引用本场对象。");
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    /// <summary>Root保留默认序列化作者面板，只附加显式只读校验。</summary>
    [CustomEditor(typeof(CraftingRoot))]
    public sealed class CraftingRootInspector : UnityEditor.Editor
    {
        private string report;
        private bool valid;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("验证配置"))
            {
                var errors = CraftingAuthoring.ValidateScene(((CraftingRoot)target).gameObject.scene);
                valid = errors.Length == 0;
                report = valid ? "本场合成作者配置通过；未修改场景。" : string.Join("\n", errors);
            }
            if (report != null) EditorGUILayout.HelpBox(report, valid ? MessageType.Info : MessageType.Error);
        }
    }

    /// <summary>工位验证和空实例ID生成分别执行；禁用/撤销不会自动改写已登记身份。</summary>
    [CustomEditor(typeof(CraftingStation))]
    public sealed class CraftingStationInspector : UnityEditor.Editor
    {
        private string report;
        private bool valid;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var station = (CraftingStation)target;
            if (GUILayout.Button("验证配置"))
            {
                var errors = CraftingAuthoring.ValidateScene(station.gameObject.scene);
                valid = errors.Length == 0;
                report = valid ? "工位及本场登记配置通过；未修改场景。" : string.Join("\n", errors);
            }
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(station.instanceId) || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("为该未登记实例生成新ID")) valid = CraftingAuthoring.TryGenerateInstanceId(station, out report);
            EditorGUILayout.HelpBox("从模板放入场景的新实例：先保留空ID，显式生成后再加入Root.stations。已有ID不会自动改写。", MessageType.Info);
            if (report != null) EditorGUILayout.HelpBox(report, valid ? MessageType.Info : MessageType.Error);
        }
    }

    /// <summary>配方按中文职责分组，仍使用原SerializedProperty绘制和Unity默认Undo序列化。</summary>
    [CustomEditor(typeof(CraftingRecipeSO))]
    public sealed class CraftingRecipeInspector : UnityEditor.Editor
    {
        private string report;
        private bool valid;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Group("稳定身份与工位资格", "recipeId", "version", "stationCapability", "maxBatch");
            Group("玩家显示", "displayName", "category", "description", "icon");
            Group("真实普通材料", "ingredients");
            Group("新产物与显式初始耐久", "outputs");
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("验证当前配方"))
            {
                try
                {
                    var frozen = ((CraftingRecipeSO)target).Freeze();
                    report = "配方本体冻结通过：" + frozen.Id + " / v" + frozen.Version + "。请显式加入Root.recipes，再执行场景配置校验。";
                    valid = true;
                }
                catch (Exception ex) { valid = false; report = "配方冻结拒绝：" + ex.Message; }
            }
            EditorGUILayout.HelpBox("新增同类配方使用已有Create/OSSASTRA/Crafting/Recipe模板。资源需显式加入Root.recipes；改变结算内容请递增版本，不从当前角色复制装备状态。", MessageType.Info);
            if (report != null) EditorGUILayout.HelpBox(report, valid ? MessageType.Info : MessageType.Error);
        }

        private void Group(string title, params string[] fields)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string field in fields)
            {
                var property = serializedObject.FindProperty(field);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
        }
    }
}
#endif
