/*
 * 脚本：FrontierGroundSceneBuilder
 * 功能：首次复制已保存的双角色单敌场，只替换研究世界根，装配原生 Tilemap 小样或整图。
 * 边界：不保存源场、不覆盖已有候选、不改角色/会话/输入/深度链；示范布局为自有作者数据。
 * 协作：GroundSourceImporter 创建七层原生规则和材质；Surface 限定编辑范围；画笔与示范共用 Tilemap。
 * 生命周期：显式 Editor 操作；重置示范使用一个原生 Undo 事务，刷新显示不重置手绘格子。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.MapResearch;
using Ossastra.MapResearchIntegration;
using Ossastra.MapResearchIntegration.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using GroundCharacterRoot = Ossastra.Characters.CharacterRoot;
using GroundEnemyRoot = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.GroundResearch.Editor
{
    public static class FrontierGroundSceneBuilder
    {
        public const string ScenePath = "Assets/OSSASTRA/Scenes/Research/SC_FrontierGroundLab.unity";
        public const string SourceScenePath = MapResearchPartySceneBuilder.ScenePath;
        private const string LegacyWorldName = "固定世界根_手工研究样地";
        private const string CollisionRootName = "地表碰撞_固定世界尺度";
        private const string FloorName = "地表连续碰撞";
        private static readonly string[] WallNames = { "地表边界_西", "地表边界_东", "地表边界_南", "地表边界_北" };
        private static readonly BoundsInt FullCells = new BoundsInt(-8, -6, 0, 16, 12, 1);
        private static readonly BoundsInt SampleCells = new BoundsInt(-2, -2, 0, 4, 4, 1);

        [MenuItem("Tools/OSSASTRA/地表研究/打开独立研究场")]
        public static void OpenSavedScene()
        {
            RequireEditContext();
            RequireCleanScenes();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("缺少已保存候选场，不自动重新生成：" + ScenePath);
            if (!GroundSourceImporter.RequiredAssetsReady(out string detail))
                throw new InvalidOperationException("本机地表依赖未就绪，保留当前现场：" + detail);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = InScene<GroundResearchSurface>(scene).Single().gameObject;
        }

        /// <summary>首次建立 64×48 世界单位整图；已有候选场景必须由用户打开，不在这里覆盖。</summary>
        public static string BuildScene() => Build(false);

        /// <summary>首次建立 16×16 同源小样，供材质与邻边验收；通过后显式 ExpandToFull。</summary>
        public static string BuildSampleScene() => Build(true);

        private static string Build(bool sample)
        {
            RequireEditContext();
            RequireCleanScenes();
            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null || File.Exists(AbsoluteAssetPath(ScenePath)))
                throw new InvalidOperationException("首次生成拒绝覆盖已有候选：" + ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new InvalidOperationException("缺少已保存双角色单敌源场：" + SourceScenePath);
            if (!GroundSourceImporter.RequiredAssetsReady(out string detail))
                throw new InvalidOperationException("七层本机地表来源未就绪：" + detail);

            Scene original = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(SourceScenePath);
            bool openedSource = !source.IsValid() || !source.isLoaded;
            Scene target = default(Scene);
            try
            {
                if (openedSource) source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
                VerifySource(source);
                // saveAsCopy 只写新的路径；原场的对象引用由 Unity 场景序列化复制，不手工重组实体。
                if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                    throw new InvalidOperationException("源场另存副本失败：" + ScenePath);
                target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(target);
                VerifySource(target);
                if (source.isDirty) throw new InvalidOperationException("源场在复制期间变为 dirty，保留现场，不关闭或保存。");
                if (!EditorSceneManager.CloseScene(source, true))
                    throw new InvalidOperationException("源场只读装载未能关闭，停止后续装配。");

                MapResearchRoot map = InScene<MapResearchRoot>(target).Single();
                GameObject legacyWorld = UniqueRoot(target, LegacyWorldName);
                map.props = Array.Empty<ScenePropView>();
                // 旧 Builder 已确认该根独占旧地面、40物件、原地板/四边墙和诊断块；实体不在它下面。
                UnityEngine.Object.DestroyImmediate(legacyWorld);
                map.collisionVolumes = (map.collisionVolumes ?? Array.Empty<Collider>()).Where(x => x != null).ToArray();
                map.gridExtent = 16;

                var ground = new GameObject("GroundRoot_开拓物语地表研究");
                SceneManager.MoveGameObjectToScene(ground, target);
                ground.transform.localScale = new Vector3(4f, 1f, 4f);
                Grid grid = ground.AddComponent<Grid>();
                grid.cellSwizzle = GridLayout.CellSwizzle.XZY;
                grid.cellSize = Vector3.one;
                grid.cellGap = Vector3.zero;
                var surface = ground.AddComponent<GroundResearchSurface>();
                surface.grid = grid;
                surface.editableCells = sample ? SampleCells : FullCells;
                surface.layers = GroundSourceImporter.CreateTilemaps(ground.transform);
                ValidateLayers(surface);
                CreateColliders(surface);
                ResetDemonstration(surface);

                GroundEnemyRoot enemy = InScene<GroundEnemyRoot>(target).Single();
                enemy.AttacksEnabled = false;
                EditorUtility.SetDirty(enemy);
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
                EditorUtility.SetDirty(map);
                EditorSceneManager.MarkSceneDirty(target);
                if (!EditorSceneManager.SaveScene(target, ScenePath))
                    throw new InvalidOperationException("新地表候选保存失败：" + ScenePath);

                // 只有开工时已 clean 且此刻仍 clean 的其他装载场才关闭；绝不为生成样地保存源场。
                for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
                {
                    Scene other = SceneManager.GetSceneAt(index);
                    if (other == target) continue;
                    if (other.isDirty) throw new InvalidOperationException("其他场景出现未保存变更，保留现场：" + other.path);
                    if (!EditorSceneManager.CloseScene(other, true))
                        throw new InvalidOperationException("其他 clean 场景未能关闭：" + other.path);
                }
                Selection.activeGameObject = ground;
                return "已保存独立地表" + (sample ? "16×16小样" : "64×48整图") +
                    "；实际XZ面积 " + surface.WorldArea.ToString("0.###") +
                    "，保留原双角色单敌/镜头链，默认敌方攻击关闭。";
            }
            finally
            {
                // 失败后保留新候选供检查；未修改的额外源装载可关闭，dirty 则保留，不自动重放。
                if (openedSource && source.IsValid() && source.isLoaded && !source.isDirty)
                    EditorSceneManager.CloseScene(source, true);
                if (target.IsValid() && target.isLoaded) SceneManager.SetActiveScene(target);
                else if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }

        /// <summary>显式扩为 64×48，重设同一批 Tilemap；不会生成第二套地图或缩放角色。</summary>
        public static void ExpandToFull(GroundResearchSurface surface)
        {
            RequireSurface(surface);
            ValidateLayers(surface);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("扩展研究地表为64×48");
            try
            {
                Undo.RecordObject(surface, "扩展地表作者范围");
                surface.editableCells = FullCells;
                ResizeColliders(surface, true);
                ResetDemonstration(surface);
                EditorUtility.SetDirty(surface);
                EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                throw;
            }
            finally { Undo.IncrementCurrentGroup(); }
        }

        /// <summary>明确重置七个原生图层；同一个 Undo 可恢复全部手绘，不保存场景或改源 TileBase。</summary>
        public static void ResetDemonstration(GroundResearchSurface surface)
        {
            RequireSurface(surface);
            ValidateLayers(surface);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            const string label = "重置研究地表示范布局";
            Undo.SetCurrentGroupName(label);
            try
            {
                foreach (GroundLayer layer in surface.layers) Undo.RegisterCompleteObjectUndo(layer.tilemap, label);
                foreach (GroundLayer layer in surface.layers) layer.tilemap.ClearAllTiles();
                BoundsInt cells = surface.EditableCells;
                for (int layerIndex = 0; layerIndex < surface.layers.Length; layerIndex++)
                {
                    GroundLayer layer = surface.layers[layerIndex];
                    var positions = new List<Vector3Int>();
                    var tiles = new List<TileBase>();
                    foreach (Vector3Int cell in cells.allPositionsWithin)
                    {
                        float x = (cell.x - cells.xMin + 0.5f) / cells.size.x;
                        float y = (cell.y - cells.yMin + 0.5f) / cells.size.y;
                        if (!DemoContains(layerIndex, x, y, cells.size)) continue;
                        positions.Add(cell);
                        tiles.Add(layer.paintTile);
                    }
                    layer.tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
                    EditorUtility.SetDirty(layer.tilemap);
                }
                RebuildDisplay(surface);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            }
            catch
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                throw;
            }
            finally { Undo.IncrementCurrentGroup(); }
        }

        /// <summary>只刷新原生邻接显示与世界裁切范围，完全保留作者已刷的格子。</summary>
        public static void RebuildDisplay(GroundResearchSurface surface)
        {
            RequireSurface(surface);
            ValidateLayers(surface);
            foreach (GroundLayer layer in surface.layers) layer.tilemap.RefreshAllTiles();
            GroundSourceImporter.UpdateMaterialBounds(surface);
            SceneView.RepaintAll();
        }

        private static bool DemoContains(int layer, float x, float y, Vector3Int cellSize)
        {
            // 自有归一化布局：先保证完整矩形底，再叠色区；原生 RuleTile 自行决定邻边图。
            switch (layer)
            {
                case 0: return true;
                case 1: return x > 0.10f && x < 0.90f && y > 0.08f && y < 0.92f;
                case 2: return Ellipse(x, y, 0.25f, 0.73f, 0.23f, 0.22f);
                case 3: return x < 0.42f + 0.06f * Mathf.Sin(y * 12f) && y < 0.46f + 0.03f * Mathf.Sin(x * 9f);
                case 4: return x > 0.55f + 0.04f * Mathf.Sin(y * 11f) && y > 0.52f;
                case 5: return Ellipse(x, y, 0.78f, 0.25f, 0.15f, 0.18f);
                case 6:
                    float road = 0.44f + 0.10f * Mathf.Sin(x * Mathf.PI * 2f);
                    bool mainRoad = Mathf.Abs(y - road) <= 0.9f / cellSize.y;
                    bool branch = y > road && Mathf.Abs(x - (0.56f + 0.08f * Mathf.Sin(y * 5f))) <= 1f / cellSize.x;
                    return mainRoad || branch;
                default: throw new InvalidOperationException("示范布局没有此图层角色：" + layer);
            }
        }

        private static bool Ellipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
        {
            float dx = (x - centerX) / radiusX;
            float dy = (y - centerY) / radiusY;
            return dx * dx + dy * dy < 1f;
        }

        private static void CreateColliders(GroundResearchSurface surface)
        {
            var collisionRoot = new GameObject(CollisionRootName);
            collisionRoot.transform.SetParent(surface.transform, false);
            var floor = new GameObject(FloorName);
            floor.transform.SetParent(collisionRoot.transform, false);
            floor.AddComponent<BoxCollider>();
            foreach (string wallName in WallNames)
            {
                var wall = new GameObject(wallName);
                wall.transform.SetParent(collisionRoot.transform, false);
                wall.AddComponent<BoxCollider>();
            }
            ResizeColliders(surface, false);
        }

        private static void ResizeColliders(GroundResearchSurface surface, bool recordUndo)
        {
            Transform collisionRoot = surface.transform.Find(CollisionRootName);
            if (collisionRoot == null) throw new InvalidOperationException("缺少本候选连续碰撞根：" + CollisionRootName);
            Bounds bounds = surface.WorldBounds;
            if (bounds.size.x <= 0f || bounds.size.z <= 0f) throw new InvalidOperationException("地表世界占地退化，不能重设碰撞。");
            Vector3 parentScale = surface.transform.lossyScale;
            if (Mathf.Abs(parentScale.x) < 0.0001f || Mathf.Abs(parentScale.y) < 0.0001f || Mathf.Abs(parentScale.z) < 0.0001f)
                throw new InvalidOperationException("地表变换缩放为零，不能建立连续碰撞。");
            if (recordUndo) Undo.RecordObject(collisionRoot, "调整地表碰撞世界范围");
            collisionRoot.SetPositionAndRotation(bounds.center, Quaternion.identity);
            // Tilemap 仍按4米逻辑格展示；碰撞子根消去它的缩放，Collider.size直接表达世界单位。
            collisionRoot.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            var colliders = new List<Collider>();
            BoxCollider floor = RequiredBox(collisionRoot, FloorName);
            SetBox(floor, new Vector3(0f, -0.25f, 0f), new Vector3(bounds.size.x, 0.5f, bounds.size.z), recordUndo);
            colliders.Add(floor);
            for (int index = 0; index < WallNames.Length; index++)
            {
                bool side = index < 2;
                Vector3 position = side ? new Vector3((index == 0 ? -1f : 1f) * (bounds.size.x * 0.5f + 0.5f), 1f, 0f) :
                    new Vector3(0f, 1f, (index == 2 ? -1f : 1f) * (bounds.size.z * 0.5f + 0.5f));
                Vector3 size = side ? new Vector3(1f, 2f, bounds.size.z + 2f) : new Vector3(bounds.size.x + 2f, 2f, 1f);
                BoxCollider wall = RequiredBox(collisionRoot, WallNames[index]);
                SetBox(wall, position, size, recordUndo);
                colliders.Add(wall);
            }
            MapResearchRoot map = InScene<MapResearchRoot>(surface.gameObject.scene).Single();
            if (recordUndo) Undo.RecordObject(map, "更新地表碰撞诊断引用");
            map.collisionVolumes = (map.collisionVolumes ?? Array.Empty<Collider>()).Where(x => x != null).Concat(colliders).Distinct().ToArray();
            EditorUtility.SetDirty(map);
        }

        private static BoxCollider RequiredBox(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            BoxCollider box = child == null ? null : child.GetComponent<BoxCollider>();
            if (box == null) throw new InvalidOperationException("缺少候选碰撞：" + name);
            return box;
        }

        private static void SetBox(BoxCollider box, Vector3 position, Vector3 size, bool recordUndo)
        {
            if (recordUndo)
            {
                Undo.RecordObject(box.transform, "调整地表碰撞位置");
                Undo.RecordObject(box, "调整地表碰撞范围");
            }
            box.transform.localPosition = position;
            box.center = Vector3.zero;
            box.size = size;
            EditorUtility.SetDirty(box);
        }

        private static void VerifySource(Scene source)
        {
            if (!source.IsValid() || !source.isLoaded || source.isDirty) throw new InvalidOperationException("源场必须有效、已保存且 clean。");
            GameObject world = UniqueRoot(source, LegacyWorldName);
            MapResearchRoot[] maps = InScene<MapResearchRoot>(source);
            GroundCharacterRoot[] characters = InScene<GroundCharacterRoot>(source);
            GroundEnemyRoot[] enemies = InScene<GroundEnemyRoot>(source);
            MapResearchPartyBridge[] bridges = InScene<MapResearchPartyBridge>(source);
            if (maps.Length != 1 || characters.Length != 2 || enemies.Length != 1 || bridges.Length != 1)
                throw new InvalidOperationException("源场必须提供唯一镜头根、双角色、单敌与唯一会话桥；不猜测替代对象。");
            if (world.GetComponentsInChildren<GroundCharacterRoot>(true).Length != 0 || world.GetComponentsInChildren<GroundEnemyRoot>(true).Length != 0 ||
                maps[0].transform.IsChildOf(world.transform) || bridges[0].transform.IsChildOf(world.transform))
                throw new InvalidOperationException("旧世界根混入实体/会话，拒绝删除。");
            if (maps[0].props == null || maps[0].props.Length != 40 || maps[0].props.Any(x => x == null || !x.transform.IsChildOf(world.transform)))
                throw new InvalidOperationException("旧世界根与40物件引用不符，停止而不重做原场。");
            MapResearchPartyBridge bridge = bridges[0];
            if (bridge.coordinator == null || maps[0].sessionComponent != bridge || bridge.characters == null || bridge.enemies == null ||
                bridge.characters.Length != 2 || bridge.enemies.Length != 1 ||
                !new HashSet<GroundCharacterRoot>(characters).SetEquals(bridge.characters) || !new HashSet<GroundEnemyRoot>(enemies).SetEquals(bridge.enemies))
                throw new InvalidOperationException("源场观察桥与真实角色/敌人身份引用不一致。");
        }

        private static void RequireSurface(GroundResearchSurface surface)
        {
            RequireEditContext();
            if (surface == null || EditorUtility.IsPersistent(surface) || !surface.gameObject.scene.IsValid() ||
                !surface.gameObject.scene.isLoaded || surface.gameObject.scene.path != ScenePath)
                throw new InvalidOperationException("只能显式编辑本候选场的 GroundResearchSurface：" + ScenePath);
        }

        private static void ValidateLayers(GroundResearchSurface surface)
        {
            if (surface == null || surface.grid == null || surface.layers == null || surface.layers.Length != 7)
                throw new InvalidOperationException("本轮示范需要已登记的七层原生 Tilemap。");
            if (surface.layers.Any(x => x == null || x.paintTile == null || x.tilemap == null || !surface.Owns(x.tilemap)) ||
                surface.layers.Select(x => x.tilemap).Distinct().Count() != 7)
                throw new InvalidOperationException("图层缺少原生 Tile、存在重复/外部 Tilemap，或作者范围无效。");
        }

        private static void RequireEditContext()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("先等待 Editor Edit/ready，再执行地表装配或作者操作。");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Prefab Stage仍打开，保留现场，不执行地表操作。");
        }

        private static void RequireCleanScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty) throw new InvalidOperationException("场景存在未保存变更，保留现场：" + scene.path);
            }
        }

        private static GameObject UniqueRoot(Scene scene, string name)
        {
            GameObject[] matches = scene.GetRootGameObjects().Where(x => x.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("场景根不唯一：" + scene.path + "/" + name + "，数量=" + matches.Length);
            return matches[0];
        }

        private static T[] InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();

        private static string AbsoluteAssetPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
#endif
