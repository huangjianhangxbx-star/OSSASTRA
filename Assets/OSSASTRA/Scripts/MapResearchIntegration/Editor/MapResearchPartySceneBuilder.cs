/*
 * 脚本：MapResearchPartySceneBuilder
 * 功能：以已保存单人样地为地图壳，复制Commerce实际双角色、队伍会话和一个敌人到独立研究场。
 * 边界：不转换/复制地图媒体，不保存原场或基础Prefab，不迁入商店/交易/旧相机，不重写角色或敌人逻辑。
 * 协作：仅Editor显式首次装配；跨实体引用统一重映射，MapResearchPartyBridge连接原协调者与观察端口。
 * 失败：拒绝覆盖已有目标；失败现场保留供唯一Unity writer检查，不自动重放或删除资产。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Characters.ViewSpace;
using Ossastra.Combat;
using Ossastra.Enemies;
using Ossastra.MapResearch;
using Ossastra.MapResearch.Editor;
using Ossastra.MapResearchIntegration.Diagnostics;
using Ossastra.MapResearchIntegration.Rendering;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MapCharacterRoot = Ossastra.Characters.CharacterRoot;
using MapEnemyRoot = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.MapResearchIntegration.Editor
{
    public static class MapResearchPartySceneBuilder
    {
        public const string ScenePath = "Assets/OSSASTRA/Scenes/Research/SC_DontStarvePartyCombatLab.unity";
        private const string MapScenePath = MapResearchSceneBuilder.ScenePath;
        private const string CommerceScenePath = "Assets/OSSASTRA/Scenes/SC_CommerceIntegration.unity";
        private const string BluePrefabPath = "Assets/OSSASTRA/Prefabs/Characters/PlayerCharacter_BlueFiveFace.prefab";
        private const string BasePrefabPath = "Assets/OSSASTRA/Prefabs/Characters/PlayerCharacter.prefab";
        private const string MaterialRoot = "Assets/OSSASTRA/Prefabs/Research/DSMap02/Materials";
        private const string ShaderName = "Ossastra/MapResearch/DepthPlane";

        /// <summary>只读检查四个源资产及上轮59项本机依赖；不猜测或重建原场GUID。</summary>
        public static string[] FindMissingSourceAssets(Func<string, bool> exists)
        {
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            string[] sources = { MapScenePath, CommerceScenePath, BluePrefabPath, BasePrefabPath };
            return sources.Where(path => !exists(path)).Concat(MapResearchSceneBuilder.FindMissingLocalDependencies(exists)).ToArray();
        }

        [MenuItem("Tools/OSSASTRA/地图研究/打开双角色战斗样地")]
        public static void OpenScene()
        {
            RequireEditClean();
            string[] missing = FindMissingSourceAssets(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
            if (missing.Length != 0 || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                string detail = missing.Length == 0 ? "尚未首次生成双角色样地。" : "缺少本机源依赖：\n" + string.Join("\n", missing.Take(8));
                Debug.LogWarning(detail + "\n" + string.Join("\n", missing));
                EditorUtility.DisplayDialog("样地尚不可打开", detail + "\n本机媒体、私有Prefab/Mesh与.meta必须配套。首次生成不覆盖已有场景，也不修复既有GUID引用。", "保留当前场景");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/OSSASTRA/地图研究/3 首次生成双角色战斗样地")]
        public static void BuildMenu() => Debug.Log(BuildScene());

        public static string BuildScene()
        {
            RequireEditClean();
            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null || System.IO.File.Exists(ScenePath))
                throw new InvalidOperationException("首次装配拒绝覆盖：" + ScenePath);
            string[] missing = FindMissingSourceAssets(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
            if (missing.Length != 0) throw new InvalidOperationException("本机共享资源/源场不完整：\n" + string.Join("\n", missing));
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("研究深度Shader未就绪：" + ShaderName);

            Scene source = SceneManager.GetSceneByPath(CommerceScenePath);
            bool openedSource = !source.IsValid() || !source.isLoaded;
            if (openedSource) source = EditorSceneManager.OpenScene(CommerceScenePath, OpenSceneMode.Additive);
            Scene target = default;
            try
            {
                CharacterControlCoordinator sourceCoordinator = InScene<CharacterControlCoordinator>(source).Single();
                CompanionFollowDirector sourceFollow = InScene<CompanionFollowDirector>(source).Single(x => x.Coordinator == sourceCoordinator);
                CombatEngagementSession sourceEngagement = InScene<CombatEngagementSession>(source).Single(x => x.Coordinator == sourceCoordinator);
                MapCharacterRoot[] sourceCharacters = sourceCoordinator.Characters.ToArray();
                if (sourceCharacters.Length != 2 || sourceCharacters.Any(x => x == null) || sourceCharacters.Select(x => x.ActorId).Distinct().Count() != 2)
                    throw new InvalidOperationException("Commerce必须提供两个真实且身份独立的队伍成员。");
                // 只改本场初控顺序；A为已验证小蓝，B保留Commerce的完整战斗模型与配置。
                MapCharacterRoot sourceBlue = sourceCharacters.Single(x => x.name == "PlayerCharacter_BlueFiveFace");
                sourceCharacters = new[] { sourceBlue, sourceCharacters.Single(x => x != sourceBlue) };
                MapEnemyRoot sourceEnemy = InScene<MapEnemyRoot>(source).Single(x => x.name == "StationaryCombatEnemy");
                if (sourceEnemy.GetComponent<ChaseEnemyBrain>() == null || sourceEnemy.GetComponent<EnemyMovement>() == null || sourceEnemy.GetComponent<EnemySpineView>() == null)
                    throw new InvalidOperationException("指定敌人缺少既有追击/运动/二维表现链。");

                Scene map = SceneManager.GetSceneByPath(MapScenePath);
                if (!map.IsValid() || !map.isLoaded) map = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Additive);
                // saveAsCopy只建立新场，不把场景复制操作保存回原单人场。
                if (!EditorSceneManager.SaveScene(map, ScenePath, true)) throw new InvalidOperationException("样地壳复制失败。");
                EditorSceneManager.CloseScene(map, true);
                target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(target);
                MapResearchRoot mapRoot = InScene<MapResearchRoot>(target).Single();
                if (mapRoot.props == null || mapRoot.props.Length != 40 || mapRoot.props.Any(x => x == null))
                    throw new InvalidOperationException("源样地必须保留40个有效物件，停止而不重做地图。");
                MapCharacterRoot oldActor = InScene<MapCharacterRoot>(target).Single();
                var replacements = ReadDepthMaterials(oldActor.gameObject);
                mapRoot.participantComponent = null;
                UnityEngine.Object.DestroyImmediate(oldActor.gameObject);

                var mapping = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                var characters = sourceCharacters.Select(x => CloneEntity(x.gameObject, target, mapping).GetComponent<MapCharacterRoot>()).ToArray();
                var enemy = CloneEntity(sourceEnemy.gameObject, target, mapping).GetComponent<MapEnemyRoot>();
                CharacterControlCoordinator coordinator = CloneService(sourceCoordinator, target, mapping);
                CompanionFollowDirector follow = CloneService(sourceFollow, target, mapping);
                CombatEngagementSession engagement = CloneService(sourceEngagement, target, mapping);
                CopyComponentsAndRemap(mapping, source, target);

                for (int i = 0; i < characters.Length; i++)
                {
                    GameObject actor = characters[i].gameObject;
                    StripUnrelatedComponents(actor);
                    actor.name = i == 0 ? "PlayerCharacter_BlueFiveFace_MapParty" : "PlayerCharacter_B_MapParty";
                    actor.transform.position = i == 0 ? new Vector3(0, .05f, 0) : new Vector3(-1.8f, .05f, -1.8f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);
                    var adapter = actor.GetComponent<CharacterViewSpaceAdapter>();
                    if (adapter == null) adapter = actor.AddComponent<CharacterViewSpaceAdapter>();
                    Transform visual = actor.transform.Find("VisualFlipRoot");
                    if (visual == null) throw new InvalidOperationException(actor.name + "缺少已约定的纯视觉VisualFlipRoot。");
                    SetReference(adapter, "visualRoot", visual);
                    SetReference(characters[i], "viewCamera", mapRoot.gameplayCamera);
                    CompanionCombatBrain brain = actor.GetComponent<CompanionCombatBrain>();
                    if (brain == null) throw new InvalidOperationException(actor.name + "缺少真实伙伴战斗Brain。");
                    SetReference(brain, "engagement", engagement);
                    ApplyDepthMaterials(actor, shader, replacements);
                }
                enemy.name = "StationaryCombatEnemy_MapParty";
                enemy.transform.position = new Vector3(0, .05f, 3f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
                StripUnrelatedComponents(enemy.gameObject);
                var enemyAdapter = enemy.GetComponent<EnemyViewSpaceAdapter>();
                if (enemyAdapter == null) enemyAdapter = enemy.gameObject.AddComponent<EnemyViewSpaceAdapter>();
                Transform enemyVisual = enemy.transform.Find("TEMP_2D_VISUAL");
                if (enemyVisual == null || enemyVisual.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("敌人TEMP_2D_VISUAL不是可确认的纯视觉子层。");
                SetReference(enemyAdapter, "visualRoot", enemyVisual);
                ApplyDepthMaterials(enemy.gameObject, shader, replacements);
                SetReferences(coordinator, "characters", characters); SetInt(coordinator, "initialIndex", 0);
                SetReference(follow, "coordinator", coordinator);
                SetReference(engagement, "coordinator", coordinator); SetReferences(engagement, "enemies", new[] { enemy });
                CombatVitals[] targets = characters.Select(x => x.GetComponent<CombatVitals>()).ToArray();
                var chase = enemy.GetComponent<ChaseEnemyBrain>(); SetReferences(chase, "targets", targets); chase.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(chase);
                var stationary = enemy.GetComponent<StationaryEnemyBrain>();
                if (stationary != null) { SetReferences(stationary, "targets", targets); stationary.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(stationary); }

                var bridge = mapRoot.gameObject.AddComponent<MapResearchPartyBridge>();
                bridge.coordinator = coordinator; bridge.characters = characters; bridge.enemies = new[] { enemy };
                var encounter = mapRoot.gameObject.AddComponent<MapResearchEncounterDev>();
                encounter.coordinator = coordinator; encounter.characters = characters; encounter.enemies = new[] { enemy };
                var recorder = mapRoot.gameObject.AddComponent<MapFlickerTraceRecorder>();
                recorder.Configure(mapRoot, characters);
                mapRoot.gameObject.AddComponent<MapFlickerTracePanel>().recorder = recorder;
                mapRoot.sessionComponent = bridge; mapRoot.followSmoothTime = .2f;
                mapRoot.collisionVolumes = (mapRoot.collisionVolumes ?? Array.Empty<Collider>())
                    .Concat(characters.SelectMany(x => x.GetComponentsInChildren<Collider>(true)))
                    .Concat(enemy.GetComponentsInChildren<Collider>(true)).Distinct().ToArray();
                VerifySceneBoundary(target);
                EditorSceneManager.MarkSceneDirty(target);
                if (!EditorSceneManager.SaveScene(target, ScenePath)) throw new InvalidOperationException("新双角色样地保存失败。");
                // 初始场已通过clean门；退出它们的装载但不保存，避免Play时出现两套会话。
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                { Scene other = SceneManager.GetSceneAt(i); if (other != target) EditorSceneManager.CloseScene(other, true); }
                return "已保存双角色单敌人样地；共享40物件/三地表，真实切控/伙伴/敌人链，未修改原场/基础Prefab/素材。";
            }
            finally
            {
                if (openedSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (target.IsValid() && target.isLoaded) SceneManager.SetActiveScene(target);
            }
        }

        private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();

        private static GameObject CloneEntity(GameObject source, Scene target, Dictionary<UnityEngine.Object, UnityEngine.Object> mapping)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
            GameObject clone = string.IsNullOrEmpty(path) ? new GameObject(source.name) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), target);
            if (clone.scene != target) SceneManager.MoveGameObjectToScene(clone, target);
            CopyHierarchy(source.transform, clone.transform, mapping);
            return clone;
        }

        private static T CloneService<T>(T source, Scene target, Dictionary<UnityEngine.Object, UnityEngine.Object> mapping) where T : Component
        {
            GameObject owner;
            if (mapping.TryGetValue(source.gameObject, out UnityEngine.Object known)) owner = (GameObject)known;
            else
            {
                owner = new GameObject(source.gameObject.name + "_MapPartySession"); SceneManager.MoveGameObjectToScene(owner, target);
                mapping.Add(source.gameObject, owner); mapping.Add(source.transform, owner.transform);
            }
            var result = (T)owner.AddComponent(source.GetType()); mapping.Add(source, result); return result;
        }

        private static void CopyHierarchy(Transform source, Transform target, Dictionary<UnityEngine.Object, UnityEngine.Object> mapping)
        {
            target.gameObject.layer = source.gameObject.layer; target.gameObject.tag = source.gameObject.tag;
            target.gameObject.SetActive(source.gameObject.activeSelf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
            mapping.Add(source.gameObject, target.gameObject);
            var ordinals = new Dictionary<Type, int>();
            foreach (Component component in source.GetComponents<Component>())
            {
                if (component == null) throw new InvalidOperationException("源实体存在MissingScript，停止复制。");
                Type type = component.GetType(); int ordinal = ordinals.TryGetValue(type, out int count) ? count : 0;
                ordinals[type] = ordinal + 1;
                Component[] existing = target.GetComponents(type);
                mapping.Add(component, existing.Length > ordinal ? existing[ordinal] : target.gameObject.AddComponent(type));
            }
            foreach (Transform child in source)
            {
                Transform[] matches = target.Cast<Transform>().Where(x => x.name == child.name).ToArray();
                if (matches.Length > 1) throw new InvalidOperationException("源实体子层名称重复：" + child.name);
                Transform copy = matches.Length == 1 ? matches[0] : (child is RectTransform ? new GameObject(child.name, typeof(RectTransform)) : new GameObject(child.name)).transform;
                if (copy.parent != target) copy.SetParent(target, false);
                CopyHierarchy(child, copy, mapping);
            }
        }

        private static void CopyComponentsAndRemap(Dictionary<UnityEngine.Object, UnityEngine.Object> mapping, Scene source, Scene target)
        {
            var copied = new HashSet<UnityEngine.Object>(mapping.Values);
            foreach (var pair in mapping)
            {
                if (!(pair.Key is Component from) || !(pair.Value is Component to)) continue;
                // Transform含原生父子引用，必须手动复制局部姿态，不能CopySerialized后临时重接原场。
                if (from is Transform sourceTransform && to is Transform targetTransform)
                {
                    targetTransform.localPosition = sourceTransform.localPosition; targetTransform.localRotation = sourceTransform.localRotation; targetTransform.localScale = sourceTransform.localScale;
                    if (from is RectTransform sourceRect && to is RectTransform targetRect)
                    { targetRect.anchorMin = sourceRect.anchorMin; targetRect.anchorMax = sourceRect.anchorMax; targetRect.pivot = sourceRect.pivot; targetRect.sizeDelta = sourceRect.sizeDelta; targetRect.anchoredPosition3D = sourceRect.anchoredPosition3D; }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetTransform);
                    continue;
                }
                EditorUtility.CopySerialized(from, to);
                var serialized = new SerializedObject(to); var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    // 原生所有权字段不是场景业务依赖；清m_GameObject会销毁CharacterController等原生组件。
                    if (IsNativeOwnership(property.propertyPath) || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object value = property.objectReferenceValue;
                    if (value == null) continue;
                    if (mapping.TryGetValue(value, out UnityEngine.Object mapped)) property.objectReferenceValue = mapped;
                    else if (copied.Contains(value) || EditorUtility.IsPersistent(value)) continue;
                    else
                    {
                        GameObject referenced = value is GameObject go ? go : value is Component component ? component.gameObject : null;
                        if (referenced == null) continue;
                        if (referenced.scene == source) property.objectReferenceValue = null;
                        else if (referenced.scene != target) throw new InvalidOperationException("未知跨场引用：" + to.GetType().Name + "." + property.propertyPath);
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(to);
            }
        }

        private static bool IsNativeOwnership(string path) => path == "m_GameObject" || path == "m_Script" || path == "m_CorrespondingSourceObject" || path == "m_PrefabInstance" || path == "m_PrefabAsset" || path == "m_EditorClassIdentifier";

        private static void StripUnrelatedComponents(GameObject actor)
        {
            foreach (MonoBehaviour component in actor.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x != null).ToArray())
            {
                string name = component.GetType().FullName;
                if (name.StartsWith("Ossastra.Commerce.", StringComparison.Ordinal) || name == "Ossastra.Development.CharacterDebugControls" || name == "Ossastra.Inventory.CompanionInventoryConsumer" || component.GetType().Name == "EnemyChaseDebugView")
                    UnityEngine.Object.DestroyImmediate(component);
            }
            foreach (Camera camera in actor.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }

        private static Dictionary<Material, Material> ReadDepthMaterials(GameObject actor)
        {
            var result = new Dictionary<Material, Material>();
            foreach (SkeletonRendererCustomMaterials custom in actor.GetComponentsInChildren<SkeletonRendererCustomMaterials>(true))
            {
                var list = new SerializedObject(custom).FindProperty("customMaterialOverrides");
                for (int i = 0; i < list.arraySize; i++)
                {
                    var item = list.GetArrayElementAtIndex(i);
                    var original = item.FindPropertyRelative("originalMaterial").objectReferenceValue as Material;
                    var replacement = item.FindPropertyRelative("replacementMaterial").objectReferenceValue as Material;
                    if (original != null && replacement != null && replacement.shader != null && replacement.shader.name == ShaderName) result[original] = replacement;
                }
            }
            return result;
        }

        private static void ApplyDepthMaterials(GameObject actor, Shader shader, Dictionary<Material, Material> replacements)
        {
            foreach (SkeletonRenderer renderer in actor.GetComponentsInChildren<SkeletonRenderer>(true))
            {
                var depth = renderer.GetComponent<MapResearchSpineDepth>();
                if (depth == null) depth = renderer.gameObject.AddComponent<MapResearchSpineDepth>();
                depth.sourceRenderer = renderer; depth.layerSpacing = -.0001f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(depth);
                if (renderer.skeletonDataAsset == null) throw new InvalidOperationException(actor.name + "缺少骨骼数据。");
                Material[] originals = renderer.skeletonDataAsset.atlasAssets.SelectMany(x => x.Materials).Distinct().ToArray();
                var custom = renderer.GetComponent<SkeletonRendererCustomMaterials>();
                if (custom == null) custom = renderer.gameObject.AddComponent<SkeletonRendererCustomMaterials>();
                custom.skeletonRenderer = renderer;
                var serialized = new SerializedObject(custom); var list = serialized.FindProperty("customMaterialOverrides"); list.arraySize = originals.Length;
                for (int i = 0; i < originals.Length; i++)
                {
                    Material original = originals[i];
                    if (!replacements.TryGetValue(original, out Material replacement))
                    {
                        Folder(MaterialRoot); string path = MaterialRoot + "/ActorDepth_" + replacements.Count + ".mat";
                        if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("拒绝覆盖既有研究材质：" + path);
                        replacement = new Material(shader); replacement.mainTexture = original.mainTexture; replacement.SetColor("_Color", Color.white); replacement.SetFloat("_Cutoff", .06f);
                        bool straight = original.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT") || original.HasProperty("_StraightAlphaInput") && original.GetFloat("_StraightAlphaInput") > .5f;
                        replacement.SetFloat("_PmaInput", straight ? 0 : 1); AssetDatabase.CreateAsset(replacement, path); replacements.Add(original, replacement);
                    }
                    var item = list.GetArrayElementAtIndex(i); item.FindPropertyRelative("overrideDisabled").boolValue = false;
                    item.FindPropertyRelative("originalMaterial").objectReferenceValue = original; item.FindPropertyRelative("replacementMaterial").objectReferenceValue = replacement;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo(); custom.enabled = false; custom.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(custom);
            }
        }

        private static void VerifySceneBoundary(Scene target)
        {
            foreach (Component component in InScene<Component>(target))
            {
                if (component == null) throw new InvalidOperationException("新场存在MissingScript。");
                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                {
                    if (IsNativeOwnership(property.propertyPath) || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (value == null || EditorUtility.IsPersistent(value)) continue;
                    GameObject referenced = value is GameObject go ? go : value is Component other ? other.gameObject : null;
                    if (referenced != null && referenced.scene != target) throw new InvalidOperationException("新场仍有外部场景引用：" + component.GetType().Name + "." + property.propertyPath);
                }
            }
            if (InScene<MapCharacterRoot>(target).Length != 2 || InScene<MapEnemyRoot>(target).Length != 1 || InScene<CharacterControlCoordinator>(target).Length != 1)
                throw new InvalidOperationException("新场实体/唯一协调者数量不符合2+1。");
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var serialized = new SerializedObject(target); var property = serialized.FindProperty(field); if (property == null) throw new InvalidOperationException("缺少字段：" + target.GetType().Name + "." + field); property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(target); }
        private static void SetReferences<T>(UnityEngine.Object target, string field, T[] values) where T : UnityEngine.Object
        { var serialized = new SerializedObject(target); var property = serialized.FindProperty(field); if (property == null || !property.isArray) throw new InvalidOperationException("缺少数组字段：" + field); property.arraySize = values.Length; for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(target); }
        private static void SetInt(UnityEngine.Object target, string field, int value)
        { var serialized = new SerializedObject(target); var property = serialized.FindProperty(field); if (property == null) throw new InvalidOperationException("缺少整数字段：" + field); property.intValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(target); }
        private static void RequireEditClean()
        { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("需要Edit/程序集ready且无PrefabStage。"); for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("保留未保存场景：" + SceneManager.GetSceneAt(i).path); }
        private static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); }
    }
}
#endif
