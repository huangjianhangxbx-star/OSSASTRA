/* 编辑地图后的可见画片、3D 碰撞及光照登记。内容相同的分块继续使用原场景对象。 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ossastra.SideView.TerrainLab;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.SideView.TerrainWorkshop
{
    public static class TerrainWorkshopBake
    {
        public const string OutputName = "地形工坊 · 派生输出";
        private const int ChunkCells = 12;
        private const string CollisionName = "碰撞";

        private sealed class Batch
        {
            public int x, y, order;
            public TerrainLayerKind layer;
            public float light, parallax;
            public Texture2D image;
            public Color tint;
            public List<TerrainPart> parts = new List<TerrainPart>();

            public string Identity()
            {
                string textureId = "";
                long textureLocal = 0;
                if (image != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(image, out textureId, out textureLocal);
                return FormattableString.Invariant($"{x}:{y}:{(int)layer}:{order}:{textureId}:{textureLocal}:{TintKey(tint)}");
            }

            public string Signature()
            {
                var contents = new StringBuilder();
                contents.Append(Identity()).Append(FormattableString.Invariant($":{light:R}:{parallax:R}"));
                foreach (var part in parts)
                {
                    Vector2[] points = { part.A, part.B, part.C, part.D };
                    foreach (var point in points)
                        contents.Append(FormattableString.Invariant($":{point.x:R},{point.y:R}"));
                    var uv = part.UV;
                    contents.Append(FormattableString.Invariant($":{uv.x:R},{uv.y:R},{uv.width:R},{uv.height:R}:{part.FlipX}:{part.FlipY}"));
                }
                return Hash128.Compute(contents.ToString()).ToString();
            }
        }

        public static TerrainSolution Rebuild(TerrainWorkshopRoot root)
        {
            if (root == null || root.map == null)
                throw new InvalidOperationException("需先为工坊根绑定地图资产。");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑状态重建地图。");
            if (root.locked || root.frozen)
                throw new InvalidOperationException("地图已锁定或派生结果已冻结。请先解除对应状态。");
            var scene = root.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || EditorUtility.IsPersistent(root))
                throw new InvalidOperationException("需在打开的作者场景中重建。");

            var map = root.map;
            var path = AssetDatabase.GetAssetPath(map);
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("请先创建或保存地图资产。");
            var catalogs = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<SideViewComparisonCardCatalog>(true)).ToArray();
            if (catalogs.Length > 1)
                throw new InvalidOperationException("场景存在多个横版渲染清单，无法判断地形应登记在哪一个。");
            var catalog = catalogs.FirstOrDefault();
            var grid = map.Read();
            var solution = TerrainAssembly.Solve(map, grid);
            var batches = Collect(map, solution);
            float cell = Mathf.Max(.05f, map.cellSize);
            float depth = Mathf.Max(.2f, map.collisionDepth);
            var boxes = grid.CollisionBoxes().ToArray();
            var output = root.transform.Find(OutputName);
            var retainedEntries = catalog == null ? null : catalog.Entries.Where(entry => entry.Renderer != null &&
                (output == null || !entry.Renderer.transform.IsChildOf(output))).ToList();
            var previous = output == null ? new Dictionary<string, Transform>() :
                output.Cast<Transform>().ToDictionary(child => child.name, StringComparer.Ordinal);
            var retained = new HashSet<Transform>();
            var created = new List<Transform>();
            var materialByKey = new Dictionary<(Texture2D, Color), Material>();
            var expected = new List<(Batch batch, Transform child)>();
            bool madeOutput = output == null;
            if (madeOutput) output = AddChild(root.transform, OutputName);
            Transform collision = null;

            try
            {
                string basis = root.cardMaterial == null ? "default" :
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root.cardMaterial));
                foreach (var batch in batches)
                {
                    string name = $"画片 {batch.layer} [{batch.x},{batch.y}] {batch.Signature()} {basis}";
                    if (!previous.TryGetValue(name, out var child))
                    {
                        // 完成持久材质解析再建立场景对象，失败时仍可保留上一套派生结果。
                        var key = (batch.image, batch.tint);
                        if (!materialByKey.TryGetValue(key, out var material))
                        {
                            material = MaterialFor(path, root.cardMaterial, batch.image, batch.tint);
                            materialByKey.Add(key, material);
                        }
                        child = AddChild(output, name);
                        created.Add(child);
                        var patch = child.gameObject.AddComponent<NgTerrainPatch>();
                        patch.SetQuads(batch.parts.Select(Quad).ToArray());
                        var renderer = child.GetComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.sortingOrder = batch.order;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        child.gameObject.AddComponent<TerrainWorkshopCardPolicy>().Configure(batch.parallax, batch.light);
                    }
                    retained.Add(child);
                    expected.Add((batch, child));
                }

                string collisionKey = CollisionName + " " + FormattableString.Invariant($"{cell:R}:{depth:R}");
                if (!previous.TryGetValue(collisionKey, out collision))
                {
                    collision = AddChild(output, collisionKey);
                    created.Add(collision);
                }
                retained.Add(collision);
                var prior = collision.Cast<Transform>().ToDictionary(child => child.name, StringComparer.Ordinal);
                var geometry = new HashSet<string>(StringComparer.Ordinal);
                foreach (var box in boxes)
                {
                    string key = FormattableString.Invariant($"{box.X},{box.Y} {box.Width}×{box.Height}");
                    geometry.Add(key);
                    if (prior.ContainsKey(key)) continue;
                    var block = AddChild(collision, key);
                    created.Add(block);
                    var collider = block.gameObject.AddComponent<BoxCollider>();
                    collider.center = new Vector3((box.X + box.Width * .5f) * cell, (box.Y + box.Height * .5f) * cell, 0);
                    collider.size = new Vector3(box.Width * cell, box.Height * cell, depth);
                }
                foreach (var pair in prior)
                    if (!geometry.Contains(pair.Key))
                        Object.DestroyImmediate(pair.Value.gameObject);
                foreach (var child in previous.Values)
                    if (!retained.Contains(child))
                        Object.DestroyImmediate(child.gameObject);
            }
            catch
            {
                foreach (var item in created.AsEnumerable().Reverse())
                    if (item != null) Object.DestroyImmediate(item.gameObject);
                if (madeOutput && output != null) Object.DestroyImmediate(output.gameObject);
                throw;
            }

            if (catalog != null)
            {
                retainedEntries.AddRange(expected.Select(pair => CatalogEntry(pair.batch, pair.child.GetComponent<MeshRenderer>())));
                catalog.SetAuthoringEntries(retainedEntries.ToArray());
                EditorUtility.SetDirty(catalog);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return solution;
        }

        public static void SetBakedVisible(Transform root, bool visible)
        {
            if (root == null) return;
            var output = root.Find(OutputName);
            if (output == null) return;
            foreach (var renderer in output.GetComponentsInChildren<MeshRenderer>(true))
                renderer.forceRenderingOff = !visible;
        }

        public static void DrawPreview(Transform root, TerrainSolution solution)
        {
            if (root == null || solution == null || Event.current.type != EventType.Repaint) return;
            Handles.BeginGUI();
            var originalColor = GUI.color;
            var originalMatrix = GUI.matrix;
            try
            {
                var map = root.GetComponent<TerrainWorkshopRoot>().map;
                foreach (var part in solution.Parts.OrderBy(part => SortOrder(map, part)))
                {
                    var a = HandleUtility.WorldToGUIPoint(root.TransformPoint(part.A));
                    var b = HandleUtility.WorldToGUIPoint(root.TransformPoint(part.B));
                    var c = HandleUtility.WorldToGUIPoint(root.TransformPoint(part.C));
                    var horizontal = c - b;
                    var vertical = a - b;
                    if (horizontal.sqrMagnitude < .05f || vertical.sqrMagnitude < .05f) continue;
                    var matrix = Matrix4x4.identity;
                    matrix.SetColumn(0, new Vector4(horizontal.x, horizontal.y, 0, 0));
                    matrix.SetColumn(1, new Vector4(vertical.x, vertical.y, 0, 0));
                    matrix.SetColumn(3, new Vector4(b.x, b.y, 0, 1));
                    GUI.matrix = originalMatrix * matrix;
                    GUI.color = part.Tint;
                    var uv = part.UV;
                    if (part.FlipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
                    if (part.FlipY) uv = new Rect(uv.x, uv.yMax, uv.width, -uv.height);
                    GUI.DrawTextureWithTexCoords(new Rect(0, 0, 1, 1),
                        part.Art == null ? Texture2D.whiteTexture : part.Art.Image, uv, true);
                }
            }
            finally
            {
                GUI.matrix = originalMatrix;
                GUI.color = originalColor;
                Handles.EndGUI();
            }
        }

        private static IEnumerable<TerrainPart> ChunkParts(TerrainMap map, TerrainSolution solution)
        {
            float chunk = Mathf.Max(.05f, map.cellSize) * ChunkCells;
            foreach (var part in solution.Parts)
            {
                if (part.Layer != TerrainLayerKind.Body)
                {
                    yield return part;
                    continue;
                }
                var bounds = part.Bounds;
                int firstX = Mathf.FloorToInt(bounds.xMin / chunk), lastX = Mathf.CeilToInt(bounds.xMax / chunk);
                int firstY = Mathf.FloorToInt(bounds.yMin / chunk), lastY = Mathf.CeilToInt(bounds.yMax / chunk);
                for (int y = firstY; y < lastY; y++)
                for (int x = firstX; x < lastX; x++)
                {
                    var rect = Rect.MinMaxRect(Mathf.Max(bounds.xMin, x * chunk), Mathf.Max(bounds.yMin, y * chunk),
                        Mathf.Min(bounds.xMax, (x + 1) * chunk), Mathf.Min(bounds.yMax, (y + 1) * chunk));
                    var uv = part.UV;
                    float u = (rect.xMin - bounds.xMin) / bounds.width, v = (rect.yMin - bounds.yMin) / bounds.height;
                    yield return new TerrainPart
                    {
                        Bounds = rect, A = rect.min, B = new Vector2(rect.xMin, rect.yMax),
                        C = rect.max, D = new Vector2(rect.xMax, rect.yMin), Layer = part.Layer, Slot = part.Slot,
                        Art = part.Art, Tint = part.Tint, UV = new Rect(uv.x + uv.width * u, uv.y + uv.height * v,
                            uv.width * rect.width / bounds.width, uv.height * rect.height / bounds.height)
                    };
                }
            }
        }

        private static IReadOnlyList<Batch> Collect(TerrainMap map, TerrainSolution solution)
        {
            float chunk = Mathf.Max(.05f, map.cellSize) * ChunkCells;
            var groups = ChunkParts(map, solution).GroupBy(part =>
                (x: Mathf.FloorToInt(part.Bounds.center.x / chunk), y: Mathf.FloorToInt(part.Bounds.center.y / chunk),
                part.Layer, order: SortOrder(map, part), image: part.Art == null ? null : part.Art.Image, part.Tint));
            return groups.Select(group => new Batch
            {
                x = group.Key.x, y = group.Key.y, layer = group.Key.Layer, order = group.Key.order,
                image = group.Key.image, tint = group.Key.Tint, parts = group.ToList(),
                light = Mathf.Clamp01(map.Layer(group.Key.Layer)?.light ?? 0),
                parallax = map.Layer(group.Key.Layer)?.parallax ?? 0
            }).OrderBy(batch => batch.order).ThenBy(batch => batch.Identity(), StringComparer.Ordinal).ToArray();
        }

        private static int SortOrder(TerrainMap map, TerrainPart part)
        {
            int order = map.Layer(part.Layer)?.order ?? 0;
            return part.Slot == TerrainSlot.Convex || part.Slot == TerrainSlot.Concave ? order + 1 : order;
        }

        private static NgTerrainPatch.Quad Quad(TerrainPart part)
        {
            var quad = new NgTerrainPatch.Quad(part.A, part.B, part.C, part.D, part.FlipX, part.FlipY);
            quad.uv = part.UV;
            return quad;
        }

        private static Material MaterialFor(string mapPath, Material template, Texture2D image, Color tint)
        {
            string directory = Path.GetDirectoryName(mapPath).Replace('\\', '/');
            string guid = AssetDatabase.AssetPathToGUID(mapPath);
            string folder = directory + "/WorkShopMesh_" + guid.Substring(0, 8);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(directory, "WorkShopMesh_" + guid.Substring(0, 8));
            string textureId = "";
            long textureLocal = 0;
            if (image != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(image, out textureId, out textureLocal);
            string materialKey = template == null ? "default" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(template));
            string key = $"{textureId}{textureLocal}:{TintKey(tint)}:{materialKey}";
            string path = $"{folder}/Surface_{Hash128.Compute(key)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = template == null ? Shader.Find("Sprites/Default") : template.shader;
            if (shader == null) throw new InvalidOperationException("找不到可用的地形画片材质。");
            var material = template == null ? new Material(shader) : new Material(template);
            if (!material.HasProperty("_MainTex") || !material.HasProperty("_Color"))
            {
                Object.DestroyImmediate(material);
                throw new InvalidOperationException("地形画片材质需要 MainTex 和 Color 属性。");
            }
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetTexture("_MainTex", image == null ? Texture2D.whiteTexture : image);
            material.SetColor("_Color", tint);
            if (material.HasProperty("_RendererColor")) material.SetColor("_RendererColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static SideViewComparisonCardCatalog.Entry CatalogEntry(Batch batch, MeshRenderer renderer)
        {
            var material = renderer.sharedMaterial;
            bool far = batch.layer == TerrainLayerKind.Far;
            bool below = batch.layer < TerrainLayerKind.Foreground;
            string kind = batch.layer switch
            {
                TerrainLayerKind.Far => "FarBackground",
                TerrainLayerKind.Middle => "NearBackground",
                TerrainLayerKind.Near => "NearBackground",
                TerrainLayerKind.Body => "Ground",
                TerrainLayerKind.Border => "Structure",
                _ => "Decoration"
            };
            return new SideViewComparisonCardCatalog.Entry(renderer, material, below, far, false, true,
                far ? -1 : 0, below ? 1 : 0, .22f * batch.light, Vector4.one, material.shader.name, kind);
        }

        private static Transform AddChild(Transform root, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(root, false);
            return child;
        }

        private static string TintKey(Color tint) => FormattableString.Invariant($"{tint.r:R},{tint.g:R},{tint.b:R},{tint.a:R}");
    }
}
