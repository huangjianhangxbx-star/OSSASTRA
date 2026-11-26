/*
 * 功能：将物质燃点组织成预算受控的一批完整火片。
 * 负责：响应区块修订、保留分桶相位、提交共享物质遮挡，以及释放自有临时资源。
 * 只读 MatterWorld 与状态纹理；透明图与灯光系统由外部提供。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics
{
    public sealed class MatterSpriteFire : IDisposable
    {
        private const int SiteSide = 4;
        private const int SiteBudget = 96;
        private const int ChunkSide = MatterWorld.ChunkSize;
        private const int SitesPerChunk = ChunkSide / SiteSide;

        private struct FireSite
        {
            public int X, Y, Count, Heat;
        }

        private readonly struct Style
        {
            public readonly float Aspect;
            public readonly Texture2D Texture;

            public Style(Texture2D texture)
            {
                Texture = texture;
                Aspect = (float)texture.width / texture.height;
            }
        }

        private readonly MatterWorld world;
        private readonly Vector2 origin;
        private readonly float cellSize;
        private readonly GameObject root;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly Mesh mesh;
        private readonly Texture2D fallback;
        private readonly uint[] revisions;
        private readonly FireSite[] sites;
        private readonly MatterSample[] samples = new MatterSample[ChunkSide * ChunkSide];
        private readonly List<Vector3> positions = new List<Vector3>(SiteBudget * 4);
        private readonly List<Vector2> uv = new List<Vector2>(SiteBudget * 4);
        private readonly List<Vector4> info = new List<Vector4>(SiteBudget * 4);
        private readonly List<int> triangles = new List<int>(SiteBudget * 6);

        private bool initialized;
        private bool disposed;
        private Style style;
        private int count;

        public int ActiveCount => !disposed && renderer.enabled ? count : 0;
        public bool UsesFallback => style.Texture == fallback;

        public static bool TryCreate(Transform parent, MatterWorld world, Texture stateTexture, Vector2 worldOrigin,
            float cellSize, out MatterSpriteFire fire)
        {
            fire = null;
            if (parent == null || world == null || stateTexture == null || cellSize <= 0) return false;
            var shader = Resources.Load<Shader>("OSPhysics/MatterSpriteFire");
            if (shader == null || !shader.isSupported) return false;
            fire = new MatterSpriteFire(parent, world, stateTexture, worldOrigin, cellSize, shader);
            return true;
        }

        private MatterSpriteFire(Transform parent, MatterWorld world, Texture state, Vector2 origin,
            float cellSize, Shader shader)
        {
            this.world = world;
            this.origin = origin;
            this.cellSize = cellSize;
            revisions = new uint[world.ChunkColumns * world.ChunkRows];
            sites = new FireSite[revisions.Length * SitesPerChunk * SitesPerChunk];

            fallback = CreateFallback();
            root = new GameObject("完整火图批次") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(parent, false);
            mesh = new Mesh { name = "OSPhysics fire quad batch", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            material = new Material(shader) { name = "OSPhysics full fire alpha", hideFlags = HideFlags.DontSave };
            renderer.sharedMaterial = material;
            material.SetTexture("_MatterState", state);
            material.SetVector("_MatterGridSize", new Vector4(world.Width, world.Height, 0, 0));
            material.SetVector("_GridMapping", new Vector4(origin.x, origin.y, cellSize, 1 / cellSize));
        }

        /// <summary>使用实验模拟时钟与只读状态。在显示禁用时跳过 CPU 重建，下次启用检查各区块修订。</summary>
        public void Present(bool enabled, float visualTime, Texture2D flame, Color tint, float gain)
        {
            if (disposed) return;
            if (!enabled || gain <= 0)
            {
                renderer.enabled = false;
                return;
            }

            Texture2D source = flame != null && flame != Texture2D.whiteTexture && flame.width > 1 && flame.height > 1
                ? flame : fallback;
            bool changedStyle = source != style.Texture;
            if (changedStyle)
            {
                style = new Style(source);
                material.SetTexture("_MainTex", source);
            }
            if (UpdateSites() || changedStyle)
                RebuildMesh();

            material.SetFloat("_VisualTime", visualTime);
            material.SetColor("_FlameColor", tint);
            material.SetFloat("_FireGain", gain);
            renderer.enabled = count > 0;
        }

        private bool UpdateSites()
        {
            // 正常水流无火时跳过所有燃点扫描。若最后一缕火刚熄，清空镜像以及上次区块修订。
            if (world.Metrics.BurningCells == 0)
            {
                if (count == 0) return false;
                Array.Clear(sites, 0, sites.Length);
                initialized = false;
                return true;
            }

            bool changed = false;
            for (int y = 0; y < world.ChunkRows; y++)
            for (int x = 0; x < world.ChunkColumns; x++)
            {
                int chunk = y * world.ChunkColumns + x;
                uint revision = world.GetChunkRevision(x, y);
                if (initialized && revisions[chunk] == revision) continue;
                revisions[chunk] = revision;
                changed |= ReadSites(chunk, x * ChunkSide, y * ChunkSide);
            }
            initialized = true;
            return changed;
        }

        private bool ReadSites(int chunk, int x, int y)
        {
            int width = Math.Min(ChunkSide, world.Width - x);
            int height = Math.Min(ChunkSide, world.Height - y);
            world.CopyRegion(x, y, width, height, samples);
            bool changed = false;
            int from = chunk * SitesPerChunk * SitesPerChunk;
            for (int row = 0; row < SitesPerChunk; row++)
            for (int col = 0; col < SitesPerChunk; col++)
            {
                int startX = col * SiteSide, startY = row * SiteSide;
                int endX = Math.Min(width, startX + SiteSide), endY = Math.Min(height, startY + SiteSide);
                var next = default(FireSite);
                int selectedCenterDistance = int.MaxValue;

                // 固定 4×4 分桶限制连续燃料上的火片密度。锚点始终选择真实燃格，
                // 同一顶行时优先瓦片中部，因此跳动最多发生在原锚点所在燃烧边界变化时。
                for (int sy = startY; sy < endY; sy++)
                for (int sx = startX; sx < endX; sx++)
                {
                    var sample = samples[sy * width + sx];
                    if (!sample.Burning || sample.Blocked || sample.Amount <= 0) continue;
                    int distance = Math.Abs(2 * (sx - startX) - SiteSide + 1);
                    if (next.Count == 0 || y + sy > next.Y ||
                        (y + sy == next.Y && distance < selectedCenterDistance))
                    {
                        next.X = x + sx;
                        next.Y = y + sy;
                        selectedCenterDistance = distance;
                    }
                    next.Count++;
                    next.Heat = Math.Max(next.Heat, (sample.Heat + 31) / 64);
                }

                int index = from + row * SitesPerChunk + col;
                var previous = sites[index];
                changed |= next.Count != previous.Count || next.X != previous.X ||
                    next.Y != previous.Y || next.Heat != previous.Heat;
                sites[index] = next;
            }
            return changed;
        }

        private void RebuildMesh()
        {
            positions.Clear();
            uv.Clear();
            info.Clear();
            triangles.Clear();
            count = 0;
            for (int id = 0; id < sites.Length && count < SiteBudget; id++)
            {
                var site = sites[id];
                if (site.Count == 0) continue;
                AddSprite(id, site);
                count++;
            }
            mesh.Clear(false);
            if (count == 0) return;
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, info);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(origin + new Vector2(world.Width, world.Height) * (.5f * cellSize),
                new Vector3(world.Width * cellSize + 4, world.Height * cellSize + 4, 1));
        }

        private void AddSprite(int id, FireSite site)
        {
            uint hash = unchecked((uint)(id + 1) * 2654435761u);
            float phase = (hash & 1023) / 1023f;
            float heat = Mathf.Clamp01(site.Heat / 16f);
            float height = cellSize * (5.4f + 2.6f * heat + .28f * site.Count) * (.90f + .22f * phase);
            float width = height * style.Aspect;
            var cell = new Vector2(site.X + .5f, site.Y + .5f);
            var anchor = origin + cell * cellSize;
            var extra = new Vector4(cell.x, cell.y, phase, heat);
            // 预留全图下沿透明留白，火苗可见底部仍落在燃料格附近。
            float baseY = anchor.y - .08f * height;
            int start = positions.Count;
            positions.Add(new Vector3(anchor.x - width * .5f, baseY, 0));
            positions.Add(new Vector3(anchor.x + width * .5f, baseY, 0));
            positions.Add(new Vector3(anchor.x - width * .5f, baseY + height, 0));
            positions.Add(new Vector3(anchor.x + width * .5f, baseY + height, 0));
            bool flip = (hash & 1024) != 0;
            uv.Add(flip ? Vector2.right : Vector2.zero);
            uv.Add(flip ? Vector2.zero : Vector2.right);
            uv.Add(flip ? Vector2.one : Vector2.up);
            uv.Add(flip ? Vector2.up : Vector2.one);
            for (int n = 0; n < 4; n++) info.Add(extra);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        // 公开检出的原创单张轮廓仅作图源回退。没有外部资源时仍走整片 Alpha 和实体裁剪。
        private static Texture2D CreateFallback()
        {
            const int width = 40, height = 84;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "OSPhysics original fire fallback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (int row = 0; row < height; row++)
            {
                float y = (row + .5f) / height;
                float center = .48f + .16f * y * y + Mathf.Sin(y * 8) * .06f * y;
                float envelope = .40f * Mathf.Sin(Mathf.PI * Mathf.Pow(y, .56f)) *
                    (1 - .28f * Mathf.Pow(y, .8f));
                for (int col = 0; col < width; col++)
                {
                    float x = (col + .5f) / width;
                    float dx = Mathf.Abs(x - center);
                    float edge = envelope - dx;
                    float tip = .29f - Mathf.Abs(y - .47f) * 1.12f - Mathf.Abs(x - .72f) * 1.38f;
                    float opacity = Mathf.Clamp01(Mathf.Max(edge, tip) * width);
                    float warmth = Mathf.Clamp01((edge * 3.8f + .1f) * (1 - y) * 1.65f);
                    pixels[row * width + col] = new Color(1, .26f + warmth * .62f,
                        .08f + warmth * .54f, opacity);
                }
            }
            result.SetPixels32(pixels);
            result.Apply(false, true);
            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            renderer.enabled = false;
            MatterPaintedStage.DestroyOwned(root);
            MatterPaintedStage.DestroyOwned(mesh);
            MatterPaintedStage.DestroyOwned(material);
            MatterPaintedStage.DestroyOwned(fallback);
        }
    }
}
