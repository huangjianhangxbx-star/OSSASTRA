// 职责：上传只读物质快照、维护旧诊断视图，并向手绘层共享 GPU 纹理和火光区。
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics
{
    public enum MatterDisplay { Natural, Grid, Heat, Collision, Painted, PaintedWorldLight, MapAssets, MapAssetsWorldLight }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MatterVisual : MonoBehaviour
    {
        private struct FireRegion
        {
            public Vector2 center;
            public int count;
        }

        private const int FireLightBudget = 4;
        private static readonly int StateTextureId = Shader.PropertyToID("_MatterState");
        private static readonly int GridSizeId = Shader.PropertyToID("_MatterGridSize");
        private static readonly int DisplayId = Shader.PropertyToID("_MatterDisplay");
        private static readonly int SecondsId = Shader.PropertyToID("_MatterSeconds");
        private static readonly int FireId = Shader.PropertyToID("_MatterFireLights");

        private readonly Vector4[] fireLights = new Vector4[FireLightBudget];
        private readonly FireRegion[] largestFires = new FireRegion[FireLightBudget];
        private MatterWorld world;
        private Material material;
        private Mesh mesh;
        private Texture2D stateTexture;
        private Texture2D uploadTile;
        private Color32[] pixels;
        private uint[] revisions;
        private FireRegion[] fires;
        private bool initialUpload;
        private bool useRegionalCopies;
        private MatterPaintedStage painted;
        private Vector2 stageOrigin;
        private float stageCell;
        private bool paintedFailed;
        private string paintedError;
        private MeshRenderer legacy;

        public bool PaintedReady => painted != null;
        public string PaintedArtStatus => painted?.ArtStatus ?? (paintedError ?? "使用原创配置");
        public string PaintedLightStatus => painted?.LightStatus ?? "完整 D-New 需要正式角色与合成相机";
        public bool WorldLightLive => painted != null && painted.SupportsWorldLight;
        public int PaintedPrivateSlots => painted?.PrivateSlotCount ?? 0;
        public int PaintedBatchCount => painted?.BatchCount ?? 0;
        public int PaintedLightMaps => painted?.LightTextureCount ?? 0;
        public int ActiveSpriteFires => painted?.ActiveSpriteFires ?? 0;
        public bool UsesFallbackFire => painted?.UsesFallbackFire ?? true;

        public int LastUploadedChunks { get; private set; }
        public int LocalFireLightCount { get; private set; }
        public bool UsesRegionalCopies => useRegionalCopies;

        public void Bind(MatterWorld source, Vector2 origin, float cell)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!(cell > 0f) || float.IsInfinity(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            Release();
            world = source;
            stageOrigin = origin;
            stageCell = cell;
            legacy = GetComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("OSPhysics/MatterSurface");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("OSPhysics/MatterSurface is unavailable on this renderer.");

            int columns = source.ChunkColumns;
            int rows = source.ChunkRows;
            revisions = new uint[columns * rows];
            fires = new FireRegion[revisions.Length];
            initialUpload = true;
            useRegionalCopies = (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0;
            stateTexture = NewTexture(source.Width, source.Height, "OSPhysics snapshot");
            // Keep the full snapshot GPU-only on supported platforms. Apply must not overwrite region copies.
            if (useRegionalCopies)
            {
                stateTexture.Apply(false, true);
                uploadTile = NewTexture(MatterWorld.ChunkSize, MatterWorld.ChunkSize, "OSPhysics upload tile");
            }
            pixels = new Color32[useRegionalCopies ? MatterWorld.ChunkSize * MatterWorld.ChunkSize : source.Width * source.Height];

            mesh = CreateSurface(origin, new Vector2(source.Width * cell, source.Height * cell));
            GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader) { name = "OSPhysics matter surface", hideFlags = HideFlags.DontSave };
            material.SetTexture(StateTextureId, stateTexture);
            material.SetVector(GridSizeId, new Vector4(source.Width, source.Height, 1f / source.Width, 1f / source.Height));
            GetComponent<MeshRenderer>().sharedMaterial = material;
            Refresh(MatterDisplay.Natural, 0f);
        }

        public void Refresh(MatterDisplay display, float time)
        {
            if (world == null || material == null) return;

            LastUploadedChunks = 0;
            for (int chunkY = 0; chunkY < world.ChunkRows; chunkY++)
                for (int chunkX = 0; chunkX < world.ChunkColumns; chunkX++)
                {
                    int index = chunkY * world.ChunkColumns + chunkX;
                    uint revision = world.GetChunkRevision(chunkX, chunkY);
                    if (!initialUpload && revision == revisions[index]) continue;
                    revisions[index] = revision;
                    UploadChunk(chunkX, chunkY, index);
                    LastUploadedChunks++;
                }

            initialUpload = false;
            if (LastUploadedChunks > 0)
            {
                if (!useRegionalCopies)
                {
                    stateTexture.SetPixels32(pixels);
                    stateTexture.Apply(false, false);
                }
                UploadFireLights();
            }
            material.SetInt(DisplayId, display > MatterDisplay.Collision ? 0 : (int)display);
            material.SetFloat(SecondsId, time);
        }

        // 此处共享现有纹理，不分配另一份状态贴图或建立第二个模拟器。
        public void PresentPainted(Camera camera, MatterDisplay display, MatterPaintStyle style, bool local,
            MatterPaintLayers layers, float time, float mood, float opacity, float effects, float lampX)
        {
            bool assetView = display == MatterDisplay.MapAssets || display == MatterDisplay.MapAssetsWorldLight;
            bool usePaint = assetView || display == MatterDisplay.Painted || display == MatterDisplay.PaintedWorldLight;
            if (usePaint && painted == null && !paintedFailed)
            {
                try
                {
                    var grid = new Vector4(world.Width, world.Height, 1f / world.Width, 1f / world.Height);
                    painted = new MatterPaintedStage(transform, camera, mesh, stateTexture, grid, stageOrigin, stageCell, world);
                }
                catch (InvalidOperationException e)
                {
                    paintedFailed = true;
                    paintedError = e.Message;
                    Debug.LogError(e.Message, this);
                }
            }

            legacy.enabled = !usePaint || painted == null;
            if (painted == null) return;
            painted.SetVisible(usePaint);
            if (!usePaint) return;
            painted.ApplyProfile(style, local);
            bool useWorldLight = display == MatterDisplay.PaintedWorldLight || display == MatterDisplay.MapAssetsWorldLight;
            painted.Present(time, fireLights, mood, useWorldLight, layers, opacity, effects, lampX, assetView);
        }

        private void UploadChunk(int chunkX, int chunkY, int index)
        {
            const int side = MatterWorld.ChunkSize;
            int startX = chunkX * side;
            int startY = chunkY * side;
            int width = Mathf.Min(side, world.Width - startX);
            int height = Mathf.Min(side, world.Height - startY);
            Vector2 fireSum = Vector2.zero;
            int burning = 0;
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    int x = startX + column;
                    int y = startY + row;
                    world.TrySnapshot(x, y, out var sample);
                    int flags = (int)sample.Kind;
                    if (sample.Fixed) flags |= 8;
                    if (sample.Barrier) flags |= 16;
                    if (sample.Burning)
                    {
                        flags |= 32;
                        fireSum += new Vector2(x + .5f, y + .5f);
                        burning++;
                    }
                    int pixel = useRegionalCopies ? row * side + column : y * world.Width + x;
                    pixels[pixel] = new Color32((byte)flags, (byte)sample.Amount,
                        (byte)((sample.Heat * 255 + 511) / 1023), (byte)sample.Smoke);
                }

            fires[index] = burning == 0 ? default : new FireRegion { center = fireSum / burning, count = burning };
            if (!useRegionalCopies) return;
            uploadTile.SetPixels32(pixels);
            uploadTile.Apply(false, false);
            Graphics.CopyTexture(uploadTile, 0, 0, 0, 0, width, height, stateTexture, 0, 0, startX, startY);
        }

        private void UploadFireLights()
        {
            Array.Clear(largestFires, 0, FireLightBudget);
            foreach (var group in fires)
            {
                for (int slot = 0; slot < FireLightBudget; slot++)
                {
                    if (group.count <= largestFires[slot].count) continue;
                    for (int shift = FireLightBudget - 1; shift > slot; shift--)
                        largestFires[shift] = largestFires[shift - 1];
                    largestFires[slot] = group;
                    break;
                }
            }

            LocalFireLightCount = 0;
            for (int slot = 0; slot < FireLightBudget; slot++)
            {
                var group = largestFires[slot];
                if (group.count == 0)
                {
                    fireLights[slot] = Vector4.zero;
                    continue;
                }
                float radius = Mathf.Clamp(9f + Mathf.Sqrt(group.count) * 2f, 9f, 38f);
                float intensity = Mathf.Min(.7f, .16f + group.count * .018f);
                fireLights[slot] = new Vector4(group.center.x, group.center.y, radius, intensity);
                LocalFireLightCount++;
            }
            material.SetVectorArray(FireId, fireLights);
        }

        private Mesh CreateSurface(Vector2 origin, Vector2 size)
        {
            float z = transform.position.z;
            var surface = new Mesh { name = "OSPhysics quad", hideFlags = HideFlags.DontSave };
            surface.vertices = new[]
            {
                transform.InverseTransformPoint(new Vector3(origin.x, origin.y, z)),
                transform.InverseTransformPoint(new Vector3(origin.x + size.x, origin.y, z)),
                transform.InverseTransformPoint(new Vector3(origin.x, origin.y + size.y, z)),
                transform.InverseTransformPoint(new Vector3(origin.x + size.x, origin.y + size.y, z))
            };
            surface.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            surface.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            surface.RecalculateBounds();
            surface.UploadMeshData(false);
            return surface;
        }

        private static Texture2D NewTexture(int width, int height, string label) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = label,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

        private void OnDisable() => painted?.SetVisible(false);

        private void OnDestroy() => Release();

        private void Release()
        {
            painted?.Dispose();
            painted = null;
            paintedFailed = false;
            paintedError = null;
            DestroyOwned(mesh);
            DestroyOwned(material);
            DestroyOwned(stateTexture);
            DestroyOwned(uploadTile);
            world = null;
            mesh = null;
            material = null;
            stateTexture = null;
            uploadTile = null;
            pixels = null;
            revisions = null;
            fires = null;
            LastUploadedChunks = 0;
            LocalFireLightCount = 0;
        }

        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
