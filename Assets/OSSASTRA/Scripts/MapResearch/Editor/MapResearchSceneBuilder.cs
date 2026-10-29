/*
 * 脚本：MapResearchSceneBuilder
 * 功能：从核对过的本机清单定向导入少量地表/物件，并首次建立独立地图研究场。
 * 边界：不覆盖已有成品，不保存原场/基础Prefab，不提取原作人物，不改全局管线/键位/构建列表。
 * 协作：Unity writer显式执行；原角色实例覆盖按路径/组件序号复制并重映射，媒体与派生Mesh仅本机。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Ossastra.Characters.ViewSpace;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MapCharacterRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.MapResearch.Editor
{
    public static class MapResearchSceneBuilder
    {
        public const string ScenePath = "Assets/OSSASTRA/Scenes/Research/SC_DontStarveMapLab.unity";
        public const string ActorPath = "Assets/OSSASTRA/MapResearch/Prefabs/PlayerCharacter_MapView.prefab";
        public const string LocalRoot = "Assets/OSSASTRA/Art/DontStarveReferenceLocal";
        public const string GeneratedRoot = "Assets/OSSASTRA/MapResearch/LocalGenerated";
        private const string MaterialRoot = "Assets/OSSASTRA/MapResearch/Materials";
        private const string PrefabRoot = "Assets/OSSASTRA/MapResearch/Prefabs";
        private const string PropPrefabRoot = GeneratedRoot + "/Prefabs";
        private const string SourceScene = "Assets/OSSASTRA/Scenes/SC_CommerceIntegration.unity";
        private const string SourceActor = "PlayerCharacter_BlueFiveFace";
        private const string SourcePrefab = "Assets/OSSASTRA/Prefabs/Characters/PlayerCharacter_BlueFiveFace.prefab";
        private const string ShaderName = "Ossastra/MapResearch/DepthPlane";

        [MenuItem("Tools/OSSASTRA/地图研究/打开饥荒地图样地")]
        public static void OpenScene()
        {
            RequireEditClean();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("尚未生成样地", "先选择已核对的本机asset-manifest.json执行定向导入，再首次生成。原作媒体不会随公开仓库提供。", "知道了");
                return;
            }
            string[] missing = FindMissingLocalDependencies(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
            if (missing.Length != 0)
            {
                // 场景壳可公开，本机媒体/派生物不能公开；先完整检查，避免切到残缺场景。
                string preview = string.Join("\n", missing.Take(10));
                if (missing.Length > 10) preview += "\n……其余" + (missing.Length - 10) + "项见Console完整列表。";
                string guidance = "本机原作媒体及派生Prefab/Mesh不随公开仓库提供。请恢复这份样地配套的本机资源与.meta（GUID）。仅导入PNG或执行“首次生成”不能自动修复现有场景壳的GUID引用；此入口不会切场或自动重建。";
                Debug.LogError("地图样地缺少" + missing.Length + "项本机依赖：\n" + string.Join("\n", missing) + "\n" + guidance);
                EditorUtility.DisplayDialog("本机地图依赖不完整", "缺少或无法加载" + missing.Length + "项依赖：\n" + preview + "\n\n" + guidance, "保留当前场景");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>检查本轮固定样地的59项本机依赖；可注入可用性检查，不访问清单或修改资产。</summary>
        public static string[] FindMissingLocalDependencies(Func<string, bool> exists)
        {
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            // 这是已生成场景壳的依赖集合，不以另一份清单猜测替换已有GUID。
            string[] ids = { "evergreen_tall", "evergreen_short", "stump", "grass", "sapling", "berrybush", "rock", "chest", "firepit" };
            var paths = new List<string>(59);
            foreach (string id in ids) paths.Add(LocalRoot + "/Props/" + id + ".png");
            paths.Add(GroundTexturePath("forest", 15));
            foreach (string id in new[] { "grass", "dirt" })
                for (int mask = 1; mask <= 15; mask++) paths.Add(GroundTexturePath(id, mask));
            foreach (string id in ids) paths.Add(PropPrefabRoot + "/PF_" + id + ".prefab");
            foreach (string id in ids) paths.Add(GeneratedRoot + "/Meshes/" + id + ".asset");
            paths.Add(GeneratedRoot + "/Meshes/Ground4.asset");
            return paths.Where(path => !exists(path)).ToArray();
        }

        [MenuItem("Tools/OSSASTRA/地图研究/1 选择本机清单并导入")]
        public static void ImportMenu()
        {
            string path = EditorUtility.OpenFilePanel("选择asset-manifest.json", "", "json");
            if (!string.IsNullOrEmpty(path)) ImportSelectedMedia(path);
        }

        [MenuItem("Tools/OSSASTRA/地图研究/2 首次生成独立样地")]
        public static void BuildMenu()
        {
            string path = EditorUtility.OpenFilePanel("选择已导入的asset-manifest.json", "", "json");
            if (!string.IsNullOrEmpty(path)) BuildScene(path);
        }

        public static string ImportSelectedMedia(string manifestPath)
        {
            RequireEditClean();
            JObject manifest = ReadManifest(manifestPath);
            Folder(LocalRoot + "/Props"); Folder(LocalRoot + "/Ground");
            int count = 0;
            foreach (JObject prop in manifest["props"])
            {
                CopyTexture(manifestPath, (string)prop["pngPath"], LocalRoot + "/Props/" + Id(prop) + ".png", (string)prop["sha256"]);
                count++;
            }
            foreach (JObject ground in manifest["ground"])
            {
                int[] masks = Id(ground) == "forest" ? new[] { 15 } : Enumerable.Range(1, 15).ToArray();
                foreach (int mask in masks)
                {
                    JObject region = GroundRegion(ground, mask);
                    CopyTexture(manifestPath, (string)region["pngPath"], GroundTexturePath(Id(ground), mask), (string)region["sha256"]);
                    count++;
                }
            }
            return "定向导入完成：" + count + "张贴图；未导入原作人物、完整归档或音频。";
        }

        public static string BuildScene(string manifestPath)
        {
            RequireEditClean();
            JObject manifest = ReadManifest(manifestPath);
            RequireNew(ScenePath); RequireNew(ActorPath);
            foreach (JObject prop in manifest["props"]) RequireNew(PropPrefabRoot + "/PF_" + Id(prop) + ".prefab");
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("缺少已编译研究Shader：" + ShaderName);
            foreach (JObject prop in manifest["props"]) RequireTexture(LocalRoot + "/Props/" + Id(prop) + ".png");
            Folder(GeneratedRoot + "/Meshes"); Folder(PropPrefabRoot); Folder(MaterialRoot); Folder(PrefabRoot); Folder("Assets/OSSASTRA/Scenes/Research");

            Scene original = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(SourceScene);
            bool openedSource = !source.IsValid() || !source.isLoaded;
            if (openedSource) source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            Scene scene = default;
            try
            {
                GameObject sourceActor = source.GetRootGameObjects().SingleOrDefault(x => x.name == SourceActor);
                if (sourceActor == null) throw new InvalidOperationException("原场没有唯一指定角色：" + SourceScene + "/" + SourceActor);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var world = new GameObject("固定世界根_手工研究样地");
                var rootObject = new GameObject("MapResearchRoot");
                var cameraInput = rootObject.AddComponent<MapResearchCameraInput>();
                var root = rootObject.AddComponent<MapResearchRoot>();
                root.cameraInput = cameraInput;
                root.initialYaw = 45f; root.pitch = 42.857f; root.distance = 25f;
                root.fieldOfView = 35f; root.orthographic = false;
                root.minimumZoom = 8f; root.maximumZoom = 45f; root.gridSize = 4f; root.gridExtent = 4;
                root.gameplayCamera = NewCamera("GameplayCamera", true);
                root.observerCamera = NewCamera("StructureObserverCamera", false);
                root.observerCamera.tag = "Untagged"; root.observerCamera.enabled = false;
                root.observerCamera.GetComponent<AudioListener>().enabled = false;
                var actor = CloneActor(sourceActor, scene, root.gameplayCamera, shader);
                root.participantComponent = actor.GetComponent<MapCharacterRoot>();
                PrefabUtility.SaveAsPrefabAssetAndConnect(actor, ActorPath, InteractionMode.AutomatedAction, out bool savedActor);
                if (!savedActor) throw new InvalidOperationException("研究角色Variant保存失败：" + ActorPath);
                // Prefab不能保留场景相机引用；保存后仅在本场实例上显式覆写。
                SetReference(root.participantComponent, "viewCamera", root.gameplayCamera);

                var views = new List<ScenePropView>();
                var colliders = new List<Collider>();
                BuildGround(manifest, world.transform, shader);
                var floor = new GameObject("地面碰撞_不跟随图片"); floor.transform.SetParent(world.transform);
                var groundCollider = floor.AddComponent<BoxCollider>(); groundCollider.center = new Vector3(0, -0.25f, 0); groundCollider.size = new Vector3(32, 0.5f, 32); colliders.Add(groundCollider);
                AddBorders(world.transform, colliders);
                var prefabs = new Dictionary<string, GameObject>();
                foreach (JObject prop in manifest["props"]) prefabs.Add(Id(prop), BuildPropPrefab(prop, shader));
                string[] layout = { "evergreen_tall", "evergreen_short", "grass", "sapling", "berrybush", "rock", "chest", "stump", "firepit" };
                for (int i = 0; i < 40; i++)
                {
                    string id = layout[i % layout.Length];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[id], scene);
                    instance.name = id + "_" + i.ToString("D2"); instance.transform.SetParent(world.transform);
                    // 手工样地布局；连续位置不强制吸格，空出人物与物件前后绕行路线。
                    int row = i / 8, column = i % 8;
                    float x = -13f + column * 3.65f + (row % 2) * 0.45f;
                    float z = -12f + row * 5.8f;
                    if (Mathf.Abs(x) < 2.5f && Mathf.Abs(z) < 4f) x += 4f;
                    instance.transform.position = new Vector3(x, 0.025f, z);
                    var view = instance.GetComponent<ScenePropView>(); view.stableId = "sample-" + i.ToString("D2") + "-" + id;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                    views.Add(view); colliders.AddRange(instance.GetComponentsInChildren<Collider>(true));
                }
                BuildDiagnostic(world.transform, colliders);
                root.props = views.ToArray(); root.collisionVolumes = colliders.ToArray();
                Quaternion initialView = Quaternion.Euler(root.pitch, root.initialYaw, 0f);
                Vector3 initialTarget = actor.transform.position + root.targetOffset;
                root.gameplayCamera.transform.SetPositionAndRotation(initialTarget - initialView * Vector3.forward * root.distance, initialView);
                root.gameplayCamera.fieldOfView = root.fieldOfView;
                root.observerCamera.transform.SetPositionAndRotation(root.gameplayCamera.transform.position, initialView);
                foreach (ScenePropView view in views) view.displayPlane.rotation = initialView;
                BuildOverlayBlocker(root);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("研究场保存失败：" + ScenePath);
                // 所有起始场均已确认clean；只关闭其内存装载，不保存，避免试玩时两套场景一起运行。
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    Scene other = SceneManager.GetSceneAt(i);
                    if (other != scene) EditorSceneManager.CloseScene(other, true);
                }
                return "已保存独立研究场：40物件/9模板/3地表/一个原角色Variant；世界根固定，滚轮旋转，不改原Q/E。";
            }
            finally
            {
                if (openedSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                else if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }

        private static GameObject CloneActor(GameObject source, Scene scene, Camera camera, Shader shader)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            if (asset == null) throw new InvalidOperationException("缺少原角色Prefab：" + SourcePrefab);
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            CopyHierarchy(source.transform, actor.transform, map);
            var copiedObjects = new HashSet<UnityEngine.Object>(map.Values);
            foreach (var pair in map)
            {
                if (!(pair.Key is Component from) || !(pair.Value is Component to)) continue;
                // Transform的序列化数据含父子引用；不能CopySerialized，否则会短暂重接原场层级。
                // 层级已由CopyHierarchy单独建立，仅复制原作者的局部姿态和RectTransform几何。
                if (from is Transform sourceTransform && to is Transform targetTransform)
                {
                    targetTransform.localPosition = sourceTransform.localPosition;
                    targetTransform.localRotation = sourceTransform.localRotation;
                    targetTransform.localScale = sourceTransform.localScale;
                    if (from is RectTransform sourceRect && to is RectTransform targetRect)
                    {
                        targetRect.anchorMin = sourceRect.anchorMin; targetRect.anchorMax = sourceRect.anchorMax;
                        targetRect.pivot = sourceRect.pivot; targetRect.sizeDelta = sourceRect.sizeDelta;
                        targetRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(to);
                    continue;
                }
                EditorUtility.CopySerialized(from, to);
                var serialized = new SerializedObject(to); var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    // Unity原生归属/Prefab基础设施不是业务引用；清空m_GameObject会销毁原生组件。
                    if (IsNativeOwnership(property.propertyPath)) continue;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (value == null) continue;
                    if (map.TryGetValue(value, out var mapped)) property.objectReferenceValue = mapped;
                    else if (copiedObjects.Contains(value) || EditorUtility.IsPersistent(value)) continue;
                    else if (value is GameObject sceneObject)
                    {
                        if (sceneObject.scene == source.scene) property.objectReferenceValue = null;
                        else if (sceneObject.scene != scene) throw new InvalidOperationException("发现其他场景的未知引用，停止克隆：" + property.propertyPath);
                    }
                    else if (value is Component sceneComponent)
                    {
                        if (sceneComponent.gameObject.scene == source.scene) property.objectReferenceValue = null;
                        else if (sceneComponent.gameObject.scene != scene) throw new InvalidOperationException("发现其他场景的未知组件引用，停止克隆：" + property.propertyPath);
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(to);
            }
            foreach (string typeName in new[] { "Ossastra.Development.CharacterDebugControls", "Ossastra.Characters.CompanionCombatBrain", "Ossastra.Inventory.CompanionInventoryConsumer" })
                foreach (var component in actor.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x != null && x.GetType().FullName == typeName).ToArray()) UnityEngine.Object.DestroyImmediate(component);
            foreach (Camera oldCamera in actor.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(oldCamera.gameObject);
            actor.name = "PlayerCharacter_MapView"; actor.transform.position = new Vector3(0, 0.05f, 0);
            var adapter = actor.GetComponent<CharacterViewSpaceAdapter>(); if (adapter == null) adapter = actor.AddComponent<CharacterViewSpaceAdapter>();
            Transform visual = actor.transform.Find("VisualFlipRoot");
            if (visual == null) throw new InvalidOperationException("原角色缺少VisualFlipRoot，不猜测显示层。");
            SetReference(adapter, "visualRoot", visual);
            var root = actor.GetComponent<MapCharacterRoot>();
            if (root == null) throw new InvalidOperationException("克隆缺少原CharacterRoot。");
            SetReference(root, "viewCamera", null);
            var replacements = new Dictionary<Material, Material>();
            foreach (SkeletonRenderer renderer in actor.GetComponentsInChildren<SkeletonRenderer>(true))
            {
                if (renderer.skeletonDataAsset == null) continue;
                Material[] originals = renderer.skeletonDataAsset.atlasAssets.SelectMany(x => x.Materials).Distinct().ToArray();
                var custom = renderer.GetComponent<SkeletonRendererCustomMaterials>();
                if (custom == null) custom = renderer.gameObject.AddComponent<SkeletonRendererCustomMaterials>();
                custom.skeletonRenderer = renderer;
                var settings = new SerializedObject(custom); var list = settings.FindProperty("customMaterialOverrides"); list.arraySize = originals.Length;
                for (int i = 0; i < originals.Length; i++)
                {
                    Material original = originals[i];
                    if (!replacements.TryGetValue(original, out Material replacement))
                    {
                        replacement = Material(shader, MaterialRoot + "/ActorDepth_" + replacements.Count + ".mat", original.mainTexture, Color.white);
                        bool straight = original.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT") || original.HasProperty("_StraightAlphaInput") && original.GetFloat("_StraightAlphaInput") > 0.5f;
                        replacement.SetFloat("_PmaInput", straight ? 0f : 1f); AssetDatabase.SaveAssetIfDirty(replacement);
                        replacements.Add(original, replacement);
                    }
                    var entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("overrideDisabled").boolValue = false;
                    entry.FindPropertyRelative("originalMaterial").objectReferenceValue = original;
                    entry.FindPropertyRelative("replacementMaterial").objectReferenceValue = replacement;
                }
                settings.ApplyModifiedPropertiesWithoutUndo(); custom.enabled = false; custom.enabled = true;
            }
            return actor;
        }

        private static bool IsNativeOwnership(string path) => path == "m_GameObject" || path == "m_Script" || path == "m_CorrespondingSourceObject" || path == "m_PrefabInstance" || path == "m_PrefabAsset" || path == "m_EditorClassIdentifier";

        private static void CopyHierarchy(Transform source, Transform target, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
        {
            target.gameObject.layer = source.gameObject.layer; target.gameObject.SetActive(source.gameObject.activeSelf);
            target.gameObject.tag = source.gameObject.tag;
            map.Add(source.gameObject, target.gameObject);
            var ordinals = new Dictionary<Type, int>();
            foreach (Component component in source.GetComponents<Component>())
            {
                if (component == null) throw new InvalidOperationException("原角色含MissingScript，先停止克隆。");
                Type type = component.GetType(); int ordinal = ordinals.TryGetValue(type, out int seen) ? seen : 0;
                ordinals[type] = ordinal + 1;
                Component[] existing = target.GetComponents(type);
                Component destination = existing.Length > ordinal ? existing[ordinal] : target.gameObject.AddComponent(type);
                map.Add(component, destination);
            }
            foreach (Transform child in source)
            {
                Transform[] matches = target.Cast<Transform>().Where(x => x.name == child.name).ToArray();
                if (matches.Length > 1) throw new InvalidOperationException("角色层级名称重复，拒绝模糊匹配：" + child.name);
                Transform copy = matches.Length == 1 ? matches[0] : new GameObject(child.name).transform;
                if (copy.parent != target) copy.SetParent(target, false);
                CopyHierarchy(child, copy, map);
            }
        }

        private static GameObject BuildPropPrefab(JObject prop, Shader shader)
        {
            string id = Id(prop); string path = PropPrefabRoot + "/PF_" + id + ".prefab";
            var go = new GameObject(id); var plane = new GameObject("DisplayPlane"); plane.transform.SetParent(go.transform, false);
            try
            {
                var rect = (JArray)prop["frames"][0]["rectPixelsBottomLeft"];
                float ppu = (float)prop["pixelsPerUnit"], width = (float)rect[2], height = (float)rect[3];
                var pivot = (JArray)prop["pivotPixelsBottomLeft"];
                Mesh mesh = Quad((-(float)pivot[0]) / ppu, (width - (float)pivot[0]) / ppu, (-(float)pivot[1]) / ppu, (height - (float)pivot[1]) / ppu, false);
                AssetDatabase.CreateAsset(mesh, GeneratedRoot + "/Meshes/" + id + ".asset");
                plane.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = plane.AddComponent<MeshRenderer>(); renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Texture2D texture = RequireTexture(LocalRoot + "/Props/" + id + ".png");
                renderer.sharedMaterial = Material(shader, MaterialRoot + "/Prop_" + id + ".mat", texture, Color.white);
                var view = go.AddComponent<ScenePropView>();
                view.stableId = id; view.sourceArchive = string.Join(";", prop["sourceRefs"].Select(x => (string)x["relativePath"]));
                view.sourceEntry = (string)prop["sourceBank"] + "/" + (string)prop["sourceBuild"] + "/" + (string)prop["sourceAnimation"];
                view.sourceSha256 = (string)prop["sha256"]; view.evidenceLevel = "SOURCE_VERIFIED素材 + UNITY_ADAPTATION显示平面/比例";
                view.displayPlane = plane.transform; view.displayRenderer = renderer; view.mode = PropViewMode.CameraPlane;
                view.pixelsPerUnit = ppu; view.snapsToGrid = false; view.sheetTexture = texture; view.loopAnimation = (bool)prop["loop"];
                float textureWidth = (float)prop["width"], textureHeight = (float)prop["height"];
                view.sheetFrames = prop["frames"].Select(frame =>
                {
                    var region = (JArray)frame["rectPixelsBottomLeft"];
                    return new ScenePropFrame { uv = new Rect((float)region[0] / textureWidth, (float)region[1] / textureHeight, (float)region[2] / textureWidth, (float)region[3] / textureHeight), durationSeconds = (float)frame["durationSeconds"] };
                }).ToArray();
                float radius = (float)prop["sourceCollisionRadius"]; view.footprint = new Vector2(radius, radius);
                if (radius > 0f) { var collider = go.AddComponent<CapsuleCollider>(); collider.radius = radius; collider.height = Mathf.Max(1f, radius * 2); collider.center = Vector3.up * collider.height * 0.5f; }
                var saved = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
                if (!success) throw new InvalidOperationException("物件Prefab保存失败：" + path);
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void BuildGround(JObject manifest, Transform parent, Shader shader)
        {
            Mesh mesh = Quad(-2, 2, -2, 2, true); AssetDatabase.CreateAsset(mesh, GeneratedRoot + "/Meshes/Ground4.asset");
            var materials = new Dictionary<string, Material>();
            foreach (JObject ground in manifest["ground"])
            {
                string id = Id(ground); int[] masks = id == "forest" ? new[] { 15 } : Enumerable.Range(1, 15).ToArray();
                foreach (int mask in masks) materials.Add(id + mask, Material(shader, MaterialRoot + "/Ground_" + id + mask + ".mat", RequireTexture(GroundTexturePath(id, mask)), Color.white));
            }
            for (int x = -4; x < 4; x++) for (int z = -4; z < 4; z++)
            {
                GroundCell("forest", 15, x, z, 0.01f, parent, mesh, materials);
                int grass = Mask(x, z, (a, b) => a * a + b * b <= 9);
                if (grass > 0) GroundCell("grass", grass, x, z, 0.014f, parent, mesh, materials);
                int dirt = Mask(x, z, (a, b) => a >= 0 && a <= 1 && b >= -3 && b <= 2);
                if (dirt > 0) GroundCell("dirt", dirt, x, z, 0.018f, parent, mesh, materials);
            }
        }

        private static int Mask(int x, int z, Func<int, int, bool> inside) => (inside(x, z + 1) ? 1 : 0) | (inside(x + 1, z + 1) ? 2 : 0) | (inside(x + 1, z) ? 4 : 0) | (inside(x, z) ? 8 : 0);
        private static void GroundCell(string id, int mask, int x, int z, float y, Transform parent, Mesh mesh, Dictionary<string, Material> materials)
        {
            var cell = new GameObject(id + "_" + x + "_" + z + "_mask" + mask); cell.transform.SetParent(parent); cell.transform.position = new Vector3((x + 0.5f) * 4f, y, (z + 0.5f) * 4f);
            cell.AddComponent<MeshFilter>().sharedMesh = mesh; cell.AddComponent<MeshRenderer>().sharedMaterial = materials[id + mask];
        }

        private static void AddBorders(Transform parent, List<Collider> colliders)
        {
            for (int i = 0; i < 4; i++)
            {
                var wall = new GameObject("样地边界碰撞_" + i); wall.transform.SetParent(parent);
                var collider = wall.AddComponent<BoxCollider>(); bool x = i < 2;
                wall.transform.position = x ? new Vector3(i == 0 ? -16.5f : 16.5f, 1, 0) : new Vector3(0, 1, i == 2 ? -16.5f : 16.5f);
                collider.size = x ? new Vector3(1, 2, 34) : new Vector3(34, 2, 1); colliders.Add(collider);
            }
        }

        private static void BuildDiagnostic(Transform parent, List<Collider> colliders)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) throw new InvalidOperationException("缺少URP Lit诊断材质Shader。");
            var material = new Material(shader); material.SetColor("_BaseColor", new Color(0.38f, 0.45f, 0.52f)); AssetDatabase.CreateAsset(material, MaterialRoot + "/SelfOwned3DDiagnostic.mat");
            for (int i = 0; i < 3; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "自有3D混合遮挡诊断_" + i; cube.transform.SetParent(parent);
                cube.transform.position = new Vector3(10f + i * 1.8f, i == 1 ? 1.2f : 0.6f, -3f);
                cube.transform.localScale = i == 1 ? new Vector3(1, 2.4f, 1) : new Vector3(1.3f, 1.2f, 1.3f);
                cube.GetComponent<Renderer>().sharedMaterial = material; colliders.Add(cube.GetComponent<Collider>());
            }
            var light = new GameObject("DiagnosticLight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1f; light.transform.rotation = Quaternion.Euler(45, -30, 0);
        }

        private static void BuildOverlayBlocker(MapResearchRoot root)
        {
            var canvasObject = new GameObject("研究说明射线边界_不代替正式HUD", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("PointerBlocker", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvasObject.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(12, -12); rect.sizeDelta = new Vector2(750, 93);
            var image = panel.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = true; root.overlayPointerBlocker = rect;
            // 原场暂时以Additive只读装载，不能把其EventSystem误认为新场已经装配。
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Camera NewCamera(string name, bool main)
        {
            var go = new GameObject(name, typeof(Camera), typeof(AudioListener)); var camera = go.GetComponent<Camera>();
            go.tag = main ? "MainCamera" : "Untagged"; camera.nearClipPlane = 0.05f; camera.farClipPlane = 180f; camera.backgroundColor = new Color(0.14f, 0.16f, 0.18f); camera.clearFlags = CameraClearFlags.SolidColor;
            return camera;
        }

        private static Mesh Quad(float left, float right, float bottom, float top, bool ground)
        {
            var mesh = new Mesh();
            mesh.vertices = ground ? new[] { new Vector3(left, 0, bottom), new Vector3(right, 0, bottom), new Vector3(right, 0, top), new Vector3(left, 0, top) } : new[] { new Vector3(left, bottom, 0), new Vector3(right, bottom, 0), new Vector3(right, top, 0), new Vector3(left, top, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }; mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.colors = Enumerable.Repeat(Color.white, 4).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Material Material(Shader shader, string path, Texture texture, Color tint)
        {
            RequireNew(path); var material = new Material(shader); material.SetTexture("_MainTex", texture); material.SetColor("_Color", tint); material.SetFloat("_Cutoff", 0.06f); material.SetFloat("_PmaInput", 0); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + "缺少字段" + field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        private static JObject ReadManifest(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("未找到本机素材清单", path);
            JObject result = JObject.Parse(File.ReadAllText(path));
            if ((string)result["format"] != "DS-MAP-01-local-assets-v1" || !(bool)result["localOnly"] || result["props"].Count() != 9 || result["ground"].Count() != 3) throw new InvalidOperationException("素材清单身份/数量不匹配；停止，不猜测转换格式。");
            return result;
        }
        private static string Id(JObject item)
        {
            string id = (string)item["id"]; if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, "^[A-Za-z0-9_-]+$")) throw new InvalidOperationException("非法素材ID"); return id;
        }
        private static JObject GroundRegion(JObject ground, int mask)
        {
            string name = (string)ground["recommendedMaskToRegion"][mask.ToString()];
            JObject[] candidates = ground["regions"].Cast<JObject>().Where(x => (string)x["name"] == name).ToArray();
            JObject result = candidates.FirstOrDefault(x => string.IsNullOrEmpty((string)x["variant"])) ?? candidates.FirstOrDefault();
            if (result == null) throw new InvalidOperationException("地表缺角点素材：" + Id(ground) + "/" + mask); return result;
        }
        private static string GroundTexturePath(string id, int mask) => LocalRoot + "/Ground/" + id + "-mask" + mask + ".png";
        private static void CopyTexture(string manifestPath, string source, string destination, string expected)
        {
            string scope = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) + Path.DirectorySeparatorChar;
            string resolved = Path.GetFullPath(source);
            if (!resolved.StartsWith(scope, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("清单贴图越出本机转换目录：" + source);
            if (!File.Exists(resolved) || Hash(resolved) != expected) throw new InvalidOperationException("转换贴图缺失或SHA256不匹配：" + source);
            string output = Path.GetFullPath(destination);
            if (File.Exists(output)) { if (Hash(output) != expected) throw new InvalidOperationException("目标已有不同内容，拒绝覆盖：" + destination); }
            else File.Copy(resolved, output, false);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(destination) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("没有纹理导入器：" + destination);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.alphaIsTransparency = false; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
        }
        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static Texture2D RequireTexture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("未导入本机贴图：" + path);
        private static void RequireNew(string path)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)) || File.Exists(path)) throw new InvalidOperationException("首次生成拒绝覆盖已有资产：" + path);
        }
        private static void RequireEditClean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("需要Edit/程序集ready且没有PrefabStage。");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("保留当前未保存场景，停止导入/生成：" + SceneManager.GetSceneAt(i).path);
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
