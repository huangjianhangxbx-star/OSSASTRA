// 职责：编排实验场手绘层，把同一张只读物质状态纹理交给各透明批次。拥有并释放临时渲染资源。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics
{
    internal sealed class MatterPaintedStage : IDisposable
    {
        private readonly struct Batch
        {
            public readonly MeshRenderer Renderer;
            public readonly Material Material;

            public Batch(MeshRenderer renderer, Material material)
            {
                Renderer = renderer;
                Material = material;
            }
        }

        private readonly Transform root;
        private readonly Camera camera;
        private readonly Vector4 worldRect;
        private readonly float cellSize;
        private readonly MatterWorld world;
        private readonly Texture state;
        private MatterSpriteFire fire;
        private bool attemptedFire;
        private readonly Batch[] matter = new Batch[4];
        private readonly MatterLocalTextures local = new MatterLocalTextures();
        private readonly MatterPaintProfile[] profiles = new MatterPaintProfile[2];
        private readonly List<Object> resources = new List<Object>();
        private readonly Batch backdrop, poster, structure, branchSilhouettes, ceilingFrame, floorFrame, foreground;
        private MatterPaintProfile profile;
        private MatterPaintLayers layers = MatterPaintLayers.All;
        private MatterPaintStyle style;
        private bool previewLocal;
        private MatterProductionLight lighting;
        private const string FullDNewUnavailable = "完整 D-New 需要正式角色与合成相机";
        private string lightReason = FullDNewUnavailable;
        private bool attemptedLight;
        private float waterOpacity = 1f, fxGain = 1f;

        public bool SupportsWorldLight => lighting != null && lighting.IsLive;
        public int PrivateSlotCount => local.Count;
        public int BatchCount { get; private set; }
        public int LightTextureCount => lighting?.TextureCount ?? 0;
        public int ActiveSpriteFires => fire?.ActiveCount ?? 0;
        public bool UsesFallbackFire => fire?.UsesFallback ?? true;
        public string ArtStatus => local.Status;
        public string LightStatus => lighting == null ? lightReason :
            (lighting.IsLive ? "实机世界灯图 · 未接完整 D-New" : "世界灯图等待相机提交");

        public MatterPaintedStage(Transform owner, Camera stageCamera, Mesh surface, Texture state, Vector4 grid, Vector2 origin,
            float cell, MatterWorld world)
        {
            var sourceShader = Resources.Load<Shader>("OSPhysics/MatterPainted");
            var sceneryShader = Resources.Load<Shader>("OSPhysics/MatterScenery");
            if (sourceShader == null || !sourceShader.isSupported || sceneryShader == null || !sceneryShader.isSupported)
                throw new InvalidOperationException("手绘物质 Shader 不可用。");
            camera = stageCamera;
            this.world = world;
            this.state = state;
            cellSize = cell;
            worldRect = new Vector4(origin.x, origin.y, grid.x * cell, grid.y * cell);
            root = new GameObject("手绘物质分层") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(owner, false);

            Mesh sceneryQuad = Own(MakeQuad());
            backdrop = MakeBatch("远景 · 原创画面", sceneryShader, sceneryQuad, 2900);
            poster = MakeBatch("远景 · 本机图件", sceneryShader, sceneryQuad, 2901);
            structure = MakeBatch("中景 · 独立构件", sceneryShader, sceneryQuad, 2902);
            branchSilhouettes = MakeBatch("前景 · 剪影", sceneryShader, Own(MakeSilhouette()), 3040);
            ceilingFrame = MakeBatch("前景 · 洞顶剪影", sceneryShader, sceneryQuad, 3041);
            floorFrame = MakeBatch("前景 · 洞底剪影", sceneryShader, sceneryQuad, 3042);
            foreground = MakeBatch("前景 · 本机图件", sceneryShader, sceneryQuad, 3043);
            for (int i = 0; i < matter.Length; i++)
            {
                var batch = MakeBatch(new[] { "地形主体与饰边", "水油与沉积", "草木与焦痕", "火焰和烟" }[i],
                    sourceShader, surface, 3000 + i);
                batch.Material.SetInt("_PaintLayer", i);
                batch.Material.SetTexture("_MatterState", state);
                batch.Material.SetVector("_MatterGridSize", grid);
                batch.Material.SetVector("_WorldRect", worldRect);
                matter[i] = batch;
            }

            ApplyProfile(MatterPaintStyle.Ruins, false);
            SetVisible(false);
        }

        public void ApplyProfile(MatterPaintStyle next, bool privateArt)
        {
            if (profile != null && style == next && previewLocal == privateArt) return;
            style = next;
            previewLocal = privateArt;
            int index = (int)next;
            if (profiles[index] == null)
            {
                profiles[index] = Resources.Load<MatterPaintProfile>("OSPhysics/Profiles/" + next);
                if (profiles[index] == null) profiles[index] = Own(MatterPaintProfile.CreateSample(next));
            }
            profile = profiles[index];
            local.Load(next, privateArt);

            SetScenery(backdrop, profile.Background, profile.BackgroundTint, 0);
            SetScenery(poster, local.Get("background", null) != profile.Background ? local.Get("background", null) : null,
                new Color(.65f, .80f, .82f, .63f), 1);
            SetScenery(structure, local.Get("structure", profile.Structure), new Color(.61f, .79f, .77f, .75f), 1);
            SetScenery(foreground, local.Get("foreground", profile.Foreground), new Color(.25f, .36f, .33f, .86f), 0);
            Color frameTint = next == MatterPaintStyle.Moss ? new Color(.73f, .90f, .57f) : new Color(.71f,.81f,.93f);
            SetScenery(ceilingFrame, Resources.Load<Texture2D>("OSPhysics/Art/CeilingFrame"), frameTint, 0);
            SetScenery(floorFrame, Resources.Load<Texture2D>("OSPhysics/Art/FloorFrame"), frameTint, 0);
            SetScenery(branchSilhouettes, null, next == MatterPaintStyle.Moss ?
                new Color(.08f, .125f, .08f, .96f) : new Color(.078f, .096f, .13f, .96f), 2);
            foreach (var batch in matter)
            {
                var m = batch.Material;
                m.SetFloat("_Style", index);
                m.SetColor("_WaterColor", profile.WaterColor);
                m.SetColor("_WaterDeep", profile.WaterDeep);
                m.SetColor("_OilColor", profile.OilColor);
                m.SetColor("_StoneColor", profile.StoneColor);
                m.SetColor("_EdgeColor", profile.EdgeColor);
                m.SetColor("_PlantColor", profile.PlantColor);
                m.SetColor("_WoodColor", profile.WoodColor);
                m.SetColor("_SmokeColor", profile.SmokeColor);
                m.SetColor("_FlameColor", profile.FlameColor);
                m.SetFloat("_SurfaceWidth", profile.SurfaceWidth);
                m.SetFloat("_WaveAmount", profile.WaveAmount);
                float frequency = profile.TextureScale / 1.25f;
                m.SetVector("_SurfaceScales", new Vector4(1.35f, .8f, 1.05f, 1.7f) * frequency);
                m.SetVector("_EffectScales", new Vector4(4.2f, .7f, 1.6f, 0) * frequency);
                SetTexture(m, "_StoneTex", "stone", profile.Stone);
                SetTexture(m, "_WaterTex", "water", profile.Water);
                SetTexture(m, "_OilTex", "oil", profile.Oil);
                SetTexture(m, "_PlantTex", "plant", profile.Plant);
                SetTexture(m, "_WoodTex", "wood", profile.Wood);
                SetTexture(m, "_FlameTex", "flame", profile.Flame);
                SetTexture(m, "_SmokeTex", "smoke", profile.Smoke);
                SetTexture(m, "_SandTex", "sand", profile.Sand);
                var rim = local.Get("waterEdge", profile.WaterEdge);
                m.SetTexture("_WaterEdgeTex", rim != null ? rim : Texture2D.whiteTexture);
                m.SetFloat("_UseWaterEdgeTex", rim != null ? 1 : 0);
            }
        }

        public void SetVisible(bool show)
        {
            if (root != null && root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show)
            {
                BatchCount = 0;
                fire?.Present(false, 0, null, Color.white, 0);
                lighting?.Dispose();
                lighting = null;
                attemptedLight = false;
                lightReason = FullDNewUnavailable;
            }
        }

        public void Present(float clock, Vector4[] fires, float mood, bool useWorldLighting, MatterPaintLayers visibility,
            float opacity, float effects, float lampX, bool assetView = false)
        {
            if (useWorldLighting && !attemptedLight)
            {
                attemptedLight = true;
                if (!MatterProductionLight.TryCreate(camera, out lighting, out var error))
                    lightReason = string.IsNullOrWhiteSpace(error) ? "未发现可独立启用的世界灯图" : error;
            }
            else if (!useWorldLighting && attemptedLight)
            {
                lighting?.Dispose();
                lighting = null;
                attemptedLight = false;
                lightReason = FullDNewUnavailable;
            }
            if (lighting != null)
            {
                lighting.PreviewLampX = lampX;
                lighting.Refresh(fires, new Vector2(worldRect.x, worldRect.y), cellSize, mood);
            }

            // 相机每帧驱动视差，样板的模拟时钟只用于水纹、火焰和烟。暂停不改变纹理时相。
            float realMap = lighting != null && lighting.IsLive ? 1 : 0;
            layers = visibility;
            waterOpacity = opacity;
            fxGain = effects;
            BatchCount = 0;
            var center = new Vector2(worldRect.x + worldRect.z * .5f, worldRect.y + worldRect.w * .5f);
            var cameraPoint = camera == null ? center : (Vector2)camera.transform.position;
            var pan = cameraPoint - center;
            Place(backdrop, center + pan * profile.BackgroundParallax + new Vector2(1.35f, .2f), new Vector2(37, 18.5f));
            var near = center - pan * profile.ForegroundParallax;
            Place(ceilingFrame, near + new Vector2(1.1f, 7.7f), new Vector2(36, 5.7f));
            Place(floorFrame, near + new Vector2(1.3f, -7.65f), new Vector2(36.5f, 3.8f));
            Place(poster, center + new Vector2(-4.1f, .7f) + pan * (profile.BackgroundParallax * .8f), new Vector2(17.7f, 12.2f));
            Place(structure, center + new Vector2(7.2f, -.95f) + pan * .11f, new Vector2(11, 12.8f));
            branchSilhouettes.Renderer.transform.localPosition = new Vector3(center.x - pan.x * profile.ForegroundParallax,
                center.y - pan.y * profile.ForegroundParallax, 0);
            Place(foreground, center + new Vector2(-10.8f, -5.75f) - pan * profile.ForegroundParallax, new Vector2(6.8f, 5.7f));
            DrawScenery(backdrop, Has(MatterPaintLayers.Background), mood, realMap);
            DrawScenery(poster, Has(MatterPaintLayers.Background) && HasTexture(poster), mood, realMap);
            DrawScenery(structure, Has(MatterPaintLayers.Decoration) && HasTexture(structure) &&
                (!assetView || Has(MatterPaintLayers.Background)), mood, realMap);
            bool foregroundOn = Has(MatterPaintLayers.Foreground);
            bool ceilingArt = HasTexture(ceilingFrame);
            bool floorArt = HasTexture(floorFrame);
            DrawScenery(branchSilhouettes, foregroundOn && !ceilingArt && !floorArt, mood, realMap);
            DrawScenery(ceilingFrame, foregroundOn && ceilingArt, mood, realMap);
            DrawScenery(floorFrame, foregroundOn && floorArt, mood, realMap);
            DrawScenery(foreground, Has(MatterPaintLayers.Foreground) && HasTexture(foreground), mood, realMap);

            if (assetView && !attemptedFire)
            {
                attemptedFire = true;
                MatterSpriteFire.TryCreate(root, world, state, new Vector2(worldRect.x, worldRect.y), cellSize, out fire);
            }
            fire?.Present(assetView, clock, local.Get("flame", profile.Flame), profile.FlameColor, profile.FireGain * fxGain);

            var mask = new Vector4(Has(MatterPaintLayers.Body) ? 1 : 0, Has(MatterPaintLayers.Trim) ? 1 : 0,
                1, 0);
            for (int i = 0; i < matter.Length; i++)
            {
                var batch = matter[i];
                batch.Renderer.enabled = (i != 0 || mask.x + mask.y + mask.z > 0) && (!assetView || i != 2);
                if (batch.Renderer.enabled) BatchCount++;
                var m = batch.Material;
                m.SetFloat("_VisualTime", clock);
                m.SetFloat("_AssetView", assetView ? 1 : 0);
                m.SetFloat("_WorldMood", mood);
                m.SetVector("_LayerVisibility", mask);
                m.SetFloat("_WaterAlpha", Mathf.Clamp01(profile.WaterAlpha * waterOpacity));
                m.SetFloat("_FireGain", profile.FireGain * fxGain);
                m.SetFloat("_SmokeGain", profile.SmokeGain * fxGain);
                m.SetVectorArray("_MatterFireLights", fires);
                m.SetFloat("_UseOSLightWorldMap", realMap);
                m.SetFloat("_UsePreviewFireLights", 1 - realMap);
            }
        }

        private bool Has(MatterPaintLayers layer) => (layers & layer) != 0;
        private static bool HasTexture(Batch batch) => batch.Material.GetTexture("_MainTex") != Texture2D.whiteTexture;

        private void DrawScenery(Batch batch, bool enabled, float mood, float realMap)
        {
            batch.Renderer.enabled = enabled;
            if (!enabled) return;
            BatchCount++;
            batch.Material.SetFloat("_WorldMood", mood);
            batch.Material.SetFloat("_UseOSLightWorldMap", realMap);
        }

        private static void SetScenery(Batch batch, Texture texture, Color tint, int shape)
        {
            batch.Material.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
            batch.Material.SetColor("_Tint", tint);
            batch.Material.SetFloat("_StageShape", shape);
        }

        private void SetTexture(Material material, string uniform, string slot, Texture2D fallback)
        {
            var texture = local.Get(slot, fallback);
            material.SetTexture(uniform, texture != null ? texture : Texture2D.whiteTexture);
        }

        private static void Place(Batch batch, Vector2 center, Vector2 size)
        {
            batch.Renderer.transform.localPosition = center;
            batch.Renderer.transform.localScale = new Vector3(size.x, size.y, 1);
        }

        private Batch MakeBatch(string title, Shader shader, Mesh mesh, int queue)
        {
            var go = new GameObject(title) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = Own(new Material(shader) { name = "OSPhysics " + title, hideFlags = HideFlags.DontSave, renderQueue = queue });
            renderer.sharedMaterial = material;
            return new Batch(renderer, material);
        }

        private static Mesh MakeQuad()
        {
            var mesh = new Mesh { name = "OSPhysics scenery quad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 只在构图边缘画近镜剪影，中央活动地带保持开敞。
        private static Mesh MakeSilhouette()
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            void Strip(float left, float right, float baseline, float peak, int segments, float phase)
            {
                int first = positions.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float x = Mathf.Lerp(left, right, t);
                    float smooth = Mathf.Sin(t * Mathf.PI);
                    float irregular = .64f + .20f * Mathf.Cos(t * 23 + phase) + .11f * Mathf.Sin(t * 49 + phase);
                    positions.Add(new Vector3(x, baseline));
                    positions.Add(new Vector3(x, baseline + peak * smooth * irregular));
                    colors.Add(new Color(.42f, .47f, .53f, 1));
                    colors.Add(new Color(.97f, .95f, 1f, .96f));
                    if (i == 0) continue;
                    int j = first + 2 * i;
                    triangles.AddRange(new[] { j - 2, j - 1, j, j - 1, j + 1, j });
                }
            }
            Strip(-30,-3.5f,-7.7f,2.6f,95,.9f);
            Strip(4.5f,26,-7.9f,2.6f,89,2.6f);
            Strip(-26,-4.5f,7.7f,-3.3f,68,1.6f);
            Strip(1.9f,24,7.9f,-3.1f,79,.3f);
            var mesh = new Mesh { name = "OSPhysics cave silhouette", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.uv = new Vector2[positions.Count];
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private T Own<T>(T obj) where T : Object
        {
            resources.Add(obj);
            return obj;
        }

        public void Dispose()
        {
            fire?.Dispose();
            fire = null;
            lighting?.Dispose();
            lighting = null;
            local.Dispose();
            if (root != null) DestroyOwned(root.gameObject);
            foreach (var resource in resources) DestroyOwned(resource);
            resources.Clear();
        }

        internal static void DestroyOwned(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
