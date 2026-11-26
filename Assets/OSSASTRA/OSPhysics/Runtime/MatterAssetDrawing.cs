// 职责：显示地编求解出的原图件和稳定草木实例，并把局部燃料耗损传给图件材质。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics
{
    internal sealed class MatterAssetDrawing : IDisposable
    {
        private sealed class Batch
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Vector2> UV = new List<Vector2>();
            internal readonly List<Vector2> FuelUV = new List<Vector2>();
            internal readonly List<Vector2> Identity = new List<Vector2>();
            internal readonly List<Color> Colors = new List<Color>();
            internal readonly List<int> Triangles = new List<int>();
            internal Mesh Mesh;
            internal MeshRenderer Renderer;
            internal Material Material;

            internal void Clear()
            {
                Vertices.Clear(); UV.Clear(); FuelUV.Clear(); Identity.Clear(); Colors.Clear(); Triangles.Clear();
            }

            internal void Commit()
            {
                Mesh.Clear();
                Mesh.SetVertices(Vertices);
                Mesh.SetUVs(0, UV);
                Mesh.SetUVs(1, FuelUV);
                Mesh.SetUVs(2, Identity);
                Mesh.SetColors(Colors);
                Mesh.SetTriangles(Triangles, 0);
                Renderer.enabled = Vertices.Count > 0;
            }
        }

        private readonly Transform root;
        private readonly Shader shader;
        private readonly Vector2 origin;
        private readonly Texture2D damage;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Dictionary<(Texture, int), Batch> batches = new Dictionary<(Texture, int), Batch>();
        private bool dirty = true;
        private int fuelRevision = -1;

        internal float RebuildMilliseconds { get; private set; }
        internal int VisibleBatches { get; private set; }

        internal MatterAssetDrawing(Transform parent, Vector2 origin)
        {
            shader = Resources.Load<Shader>("OSPhysics/MatterAssetSurface");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("地图资产 Shader 无法加载。");
            this.origin = origin;
            root = new GameObject("资产交互画片").transform;
            root.SetParent(parent, false);
            damage = new Texture2D(MatterAssetFuel.AtlasSize, MatterAssetFuel.AtlasSize, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "OSPhysics asset fuel damage"
            };
        }

        internal void Invalidate() => dirty = true;

        internal void Present(IReadOnlyList<MatterAssetQuad> terrain, MatterAssetFuel fuel, bool show, bool body,
            bool trim, bool plants, float mood, bool light)
        {
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            VisibleBatches = 0;
            if (!show) return;

            if (dirty || fuelRevision != fuel.GeometryRevision)
            {
                fuelRevision = fuel.GeometryRevision;
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                foreach (var batch in batches.Values) batch.Clear();
                foreach (var part in terrain)
                {
                    var batch = Get(part.Image, 2960 + Mathf.Clamp(part.Order + 24, -20, 32));
                    Add(batch, part.A + origin, part.B + origin, part.C + origin, part.D + origin,
                        part.UV, part.Tint, part.FlipX, part.FlipY, part.FadeVertices, -1);
                }
                foreach (var item in fuel.Instances)
                {
                    Rect r = item.Bounds;
                    var batch = Get(item.Art.Image, 3002);
                    Add(batch, r.min, new Vector2(r.xMin, r.yMax), r.max, new Vector2(r.xMax, r.yMin),
                        item.Art.UV, Color.white, item.Flipped, false, 0, item.Slot);
                }
                foreach (var batch in batches.Values) batch.Commit();
                RebuildMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) *
                    1000.0 / System.Diagnostics.Stopwatch.Frequency);
                dirty = false;
            }

            if (fuel.Changed)
            {
                fuel.RefreshAtlas();
                damage.SetPixels32(fuel.Atlas);
                damage.Apply(false, false);
            }
            properties.SetFloat("_WorldMood", mood);
            properties.SetFloat("_UseOSLightWorldMap", light ? 1 : 0);
            foreach (var entry in batches)
            {
                int queue = entry.Key.Item2;
                var batch = entry.Value;
                bool visible = batch.Vertices.Count > 0 && (queue == 3002 ? plants :
                    (entry.Key.Item1 == Texture2D.whiteTexture ? body : trim));
                batch.Renderer.enabled = visible;
                if (!visible) continue;
                batch.Renderer.SetPropertyBlock(properties);
                VisibleBatches++;
            }
        }

        private Batch Get(Texture2D texture, int queue)
        {
            var key = (texture != null ? texture : Texture2D.whiteTexture, queue);
            if (batches.TryGetValue(key, out var existing)) return existing;
            var batch = new Batch();
            var material = new Material(shader) { name = "OSPhysics map asset", renderQueue = queue };
            material.SetTexture("_MainTex", key.Item1);
            material.SetTexture("_AssetDamage", damage);
            batch.Material = material;
            var go = new GameObject(texture != null ? texture.name : "实体填色");
            go.transform.SetParent(root, false);
            batch.Mesh = new Mesh { name = "OSPhysics placed assets", indexFormat = IndexFormat.UInt32 };
            batch.Mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = batch.Mesh;
            batch.Renderer = go.AddComponent<MeshRenderer>();
            batch.Renderer.sharedMaterial = material;
            batch.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            batch.Renderer.receiveShadows = false;
            batches.Add(key, batch);
            return batch;
        }

        private void Add(Batch batch, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Rect uv, Color tint,
            bool flipX, bool flipY, byte fade, int slot)
        {
            if (uv.width <= 0 || uv.height <= 0) uv = Rect.MinMaxRect(0, 0, 1, 1);
            float left = flipX ? uv.xMax : uv.xMin, right = flipX ? uv.xMin : uv.xMax;
            float bottom = flipY ? uv.yMax : uv.yMin, top = flipY ? uv.yMin : uv.yMax;
            int first = batch.Vertices.Count;
            var code = new Vector2(slot >= 0 ? 1 : 0, 0);
            var tile = slot >= 0 ? (Vector2)MatterAssetFuel.AtlasOrigin(slot) : Vector2.zero;
            Vector2 lo = (tile + Vector2.one * .5f) / MatterAssetFuel.AtlasSize;
            Vector2 hi = (tile + Vector2.one * (MatterAssetFuel.TilePixels - .5f)) / MatterAssetFuel.AtlasSize;
            void Vertex(Vector2 pos, Vector2 tex, Vector2 fuelPos, int bit)
            {
                batch.Vertices.Add(pos);
                batch.UV.Add(tex);
                batch.FuelUV.Add(fuelPos);
                batch.Identity.Add(code);
                var color = tint;
                if ((fade & 1 << bit) != 0) color.a = 0;
                batch.Colors.Add(color);
            }

            Vertex(a, new Vector2(left, bottom), lo, 0);
            Vertex(b, new Vector2(left, top), new Vector2(lo.x, hi.y), 1);
            Vertex(c, new Vector2(right, top), hi, 2);
            Vertex(d, new Vector2(right, bottom), new Vector2(hi.x, lo.y), 3);
            batch.Triangles.Add(first);
            batch.Triangles.Add(first + 1);
            batch.Triangles.Add(first + 2);
            batch.Triangles.Add(first);
            batch.Triangles.Add(first + 2);
            batch.Triangles.Add(first + 3);
        }

        public void Dispose()
        {
            foreach (var batch in batches.Values)
            {
                DestroyOwned(batch.Material);
                DestroyOwned(batch.Mesh);
            }
            DestroyOwned(damage);
            if (root != null) DestroyOwned(root.gameObject);
            batches.Clear();
        }

        private static void DestroyOwned(Object item)
        {
            if (Application.isPlaying) Object.Destroy(item);
            else Object.DestroyImmediate(item);
        }
    }
}
