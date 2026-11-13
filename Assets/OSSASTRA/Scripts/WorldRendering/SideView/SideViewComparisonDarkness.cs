using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Ossastra.WorldRendering.SideView
{
    /// <summary>一份 B 方案运行副本，持有当前相机的遮罩和暗区过渡。</summary>
    internal sealed class SideViewComparisonDarkness : IDisposable
    {
        internal const string ShaderName = "Hidden/Ossastra/SideView/ComparisonDarkness";
        internal const int CompositePass = 3;
        internal const float VignettePixelsToUnits = 51.192489624f;

        private const float NormalScale = 5.5f;
        private const float DarkOneScale = 2.7f;
        private const float DarkTwoScale = 1.8f;
        private const float ScaleTransitionSeconds = 1f;

        private static readonly int GpuVpId = Shader.PropertyToID("_SvDarknessGpuVP");
        private static readonly int TexelSizeId = Shader.PropertyToID("_SvDarknessMaskTexelSize");
        private static readonly int SpriteId = Shader.PropertyToID("_SvDarknessSpriteTexture");
        private static readonly int TintId = Shader.PropertyToID("_SvDarknessTintGamma");
        private static readonly int AlphaOnlyId = Shader.PropertyToID("_SvDarknessAlphaOnly");
        private static readonly int FeatherId = Shader.PropertyToID("_SvDarknessFeather");
        private static readonly int CutoutId = Shader.PropertyToID("_SvDarknessCutoutTexture");
        private static readonly int CutoutEnabledId = Shader.PropertyToID("_SvDarknessCutoutEnabled");
        private static readonly int CutoutScaleBiasId = Shader.PropertyToID("_SvDarknessCutoutScaleBias");
        private static readonly int OverlaysId = Shader.PropertyToID("_SvDarknessOverlaysTexture");

        private readonly Transform sourceRoot;
        private readonly Texture ultrawideCutout;
        private readonly Texture whiteFader;
        private readonly Mesh quad;
        private readonly MaterialPropertyBlock drawProperties = new();

        private RenderTexture cutout;
        private RenderTexture overlays;
        private bool hasObserver;
        private float scaleFrom = NormalScale;
        private float scaleTo = NormalScale;
        private float scaleStarted;

        internal Material Material { get; }
        internal RenderTexture Cutout => cutout;
        internal RenderTexture Overlays => overlays;

        private Matrix4x4 SourceToWorld => sourceRoot ? sourceRoot.localToWorldMatrix : Matrix4x4.identity;

        internal SideViewComparisonDarkness(Shader shader, Texture ultrawideCutout, Texture whiteFader, Transform sourceRoot = null)
        {
            if (!shader) throw new ArgumentNullException(nameof(shader));
            if (!ultrawideCutout) throw new ArgumentNullException(nameof(ultrawideCutout));
            if (!whiteFader) throw new ArgumentNullException(nameof(whiteFader));

            this.sourceRoot = sourceRoot;
            this.ultrawideCutout = ultrawideCutout;
            this.whiteFader = whiteFader;
            Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            quad = CreateQuad();
        }

        internal bool Prepare(int width, int height)
        {
            if (!Material || Material.passCount <= CompositePass || width <= 0 || height <= 0)
                return false;
            if (cutout && overlays && cutout.width == width && cutout.height == height &&
                cutout.IsCreated() && overlays.IsCreated())
                return true;

            var nextCutout = CreateTarget(width, height, "Side view B darkness cutout RGBA");
            var nextOverlays = CreateTarget(width, height, "Side view B darkness overlay Gamma PMA");
            if (!nextCutout || !nextOverlays)
            {
                ReleaseTarget(nextCutout);
                ReleaseTarget(nextOverlays);
                return false;
            }

            ReleaseTarget(cutout);
            ReleaseTarget(overlays);
            cutout = nextCutout;
            overlays = nextOverlays;
            return true;
        }

        internal float VignetteScale(Vector3 observerPosition)
        {
            Vector3 source = sourceRoot ? sourceRoot.InverseTransformPoint(observerPosition) : observerPosition;
            float target = TargetScale(source);
            if (!hasObserver)
            {
                hasObserver = true;
                scaleFrom = scaleTo = target;
                scaleStarted = Time.time;
                return target;
            }

            float now = Time.time;
            float progress = Mathf.Clamp01((now - scaleStarted) / ScaleTransitionSeconds);
            float scale = Mathf.Lerp(scaleFrom, scaleTo, progress);
            if (scaleTo != target)
            {
                scaleFrom = scale;
                scaleTo = target;
                scaleStarted = now;
            }
            return scale;
        }

        private static float TargetScale(Vector3 source)
        {
            // 11535 优先级 1，覆盖重叠区中的 11536（优先级 0）。坐标为 Tut_01 世界平面。
            if (Inside(source, 82.507598f, 15.128475f, 131.417834f, 27.036092f))
                return DarkTwoScale;
            if (Inside(source, 67.387603f, 1.567002f, 116.297839f, 26.310264f))
                return DarkOneScale;
            return NormalScale;
        }

        private static bool Inside(Vector3 point, float left, float bottom, float right, float top) =>
            point.x >= left && point.x <= right && point.y >= bottom && point.y <= top;

        internal void RecordCutout(CommandBuffer cmd, Matrix4x4 gpuViewProjection)
        {
            ClearTarget(cmd, cutout);
            cmd.SetGlobalMatrix(GpuVpId, gpuViewProjection);
            cmd.SetGlobalVector(TexelSizeId, new Vector4(1f / cutout.width, 1f / cutout.height, cutout.width, cutout.height));

            Matrix4x4 world = SourceToWorld;
            // 10253 同时翻转 X/Y，Awake 属性块 _JustAlphaLerp=1。
            DrawCutout(cmd, quad, world * QuadAt(0.13f, 12.44f, 0f, -6.6549f, -10.271472f),
                ultrawideCutout, new Vector4(0f, 0f, 0f, 1f), true);
            DrawCutout(cmd, quad, world * QuadAt(48.93457f, 12.2f, 0f, 53.58398f, 80.119292f),
                whiteFader, new Vector4(0.9632353187f, 0f, 0f, 0.384f), false);
            DrawCutout(cmd, quad, world * QuadAt(48.93457f, 77.399994f, 0f, 27.65625f, 201.24153f),
                whiteFader, new Vector4(0.9632353187f, 0f, 0f, 0.441f), false);
        }

        // 扩展源可在切换渲染目标前写入作者指定的 RGB（含 G/B）或单独写 A。
        internal void DrawCutout(CommandBuffer cmd, Mesh mesh, Matrix4x4 localToWorld, Texture texture,
            Vector4 channelsAndOpacity, bool alphaOnly)
        {
            Draw(cmd, mesh, localToWorld, texture, channelsAndOpacity, 0, alphaOnly, false);
        }

        internal void RecordSceneBorders(CommandBuffer cmd)
        {
            ClearTarget(cmd, overlays);
            BindVignetteCutout(cmd);
            Matrix4x4 world = SourceToWorld;
            DrawBorderPair(cmd, world, 36.72f);
            DrawBorderPair(cmd, world, 90.57f);
        }

        // 可在静态边缘之后追加实际排序的场景边缘或运行时 Blackout。
        internal void DrawOverlay(CommandBuffer cmd, Mesh mesh, Matrix4x4 localToWorld, Texture texture,
            Vector4 tintGamma, bool isMaskBlackout)
        {
            Draw(cmd, mesh, localToWorld, texture, tintGamma, isMaskBlackout ? 2 : 1, false, false);
        }

        private void DrawBorderPair(CommandBuffer cmd, Matrix4x4 world, float y)
        {
            var black = new Vector4(0f, 0f, 0f, 1f);
            Draw(cmd, quad, world * QuadAt(90.03f, y, 0.00001f, -20f, 15.862f),
                Texture2D.whiteTexture, black, 1, false, false);
            Draw(cmd, quad, world * QuadAt(79.905f, y, 0.00001f, -0.25f, 15.862f),
                Texture2D.whiteTexture, black, 1, false, true);
        }

        internal void BindVignetteCutout(CommandBuffer cmd)
        {
            cmd.SetGlobalTexture(CutoutId, cutout);
            cmd.SetGlobalVector(CutoutScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
            cmd.SetGlobalFloat(CutoutEnabledId, 1f);
        }

        internal void BindOverlays(CommandBuffer cmd) => cmd.SetGlobalTexture(OverlaysId, overlays);

        internal static void ClearBindings(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat(CutoutEnabledId, 0f);
            cmd.SetGlobalTexture(CutoutId, Texture2D.blackTexture);
            cmd.SetGlobalTexture(OverlaysId, Texture2D.blackTexture);
        }

        private void Draw(CommandBuffer cmd, Mesh mesh, Matrix4x4 localToWorld, Texture texture, Vector4 tint,
            int pass, bool alphaOnly, bool feather)
        {
            drawProperties.Clear();
            drawProperties.SetTexture(SpriteId, texture);
            drawProperties.SetVector(TintId, tint);
            drawProperties.SetFloat(AlphaOnlyId, alphaOnly ? 1f : 0f);
            drawProperties.SetFloat(FeatherId, feather ? 1f : 0f);
            cmd.DrawMesh(mesh, localToWorld, Material, 0, pass, drawProperties);
        }

        private static void ClearTarget(CommandBuffer cmd, RenderTexture target)
        {
            cmd.SetRenderTarget(target);
            cmd.SetViewport(new Rect(0f, 0f, target.width, target.height));
            cmd.ClearRenderTarget(false, true, Color.clear);
        }

        private static Matrix4x4 QuadAt(float x, float y, float z, float width, float height) =>
            Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity, new Vector3(width, height, 1f));

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh
            {
                name = "Side view B darkness source quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f)
                },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static RenderTexture CreateTarget(int width, int height, string name)
        {
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 0);
            var target = new RenderTexture(descriptor)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (target.Create()) return target;
            ReleaseTarget(target);
            return null;
        }

        private static void ReleaseTarget(RenderTexture target)
        {
            if (!target) return;
            target.Release();
            Destroy(target);
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            ReleaseTarget(cutout);
            ReleaseTarget(overlays);
            cutout = overlays = null;
            Destroy(quad);
            Destroy(Material);
        }
    }
}
