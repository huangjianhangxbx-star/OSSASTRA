/*
 * 脚本：SideViewComparisonSilksongFeature
 * 功能：为正式横版相机安排 B 方案的远景、角色光图和最终颜色处理。
 * 边界：只消费该相机的剔除结果和颜色附件，普通角色与前景仍走原透明绘制。
 * 协作：后台阶段成功提交后才抑制同一批画片的常规透明 Pass，防止多画或漏画。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.WorldRendering.SideView
{
    public sealed class SideViewComparisonSilksongFeature : ScriptableRendererFeature
    {
        private readonly Dictionary<int, CameraPasses> cameras = new();

        public override void Create()
        {
            ReleaseAll();
            SideViewRenderComparison.Released += Release;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var camera = data.cameraData.camera;
            if (camera == null || camera.cameraType != CameraType.Game) return;
            int id = camera.GetInstanceID();
            if (!camera.TryGetComponent<SideViewRenderComparison>(out var comparison) || !comparison.IsSilksong ||
                data.cameraData.postProcessEnabled || data.cameraData.renderType != CameraRenderType.Base)
            {
                Release(id);
                return;
            }
            if (!cameras.TryGetValue(id, out var passes))
            {
                passes = new CameraPasses(comparison);
                cameras.Add(id, passes);
            }
            renderer.EnqueuePass(passes.Background);
            renderer.EnqueuePass(passes.Grade);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data)
        {
            var camera = data.cameraData.camera;
            if (camera == null || !cameras.TryGetValue(camera.GetInstanceID(), out var passes)) return;
            passes.Background.SetTargets(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
            passes.Grade.SetTarget(renderer.cameraColorTargetHandle);
        }

        private void Release(int id)
        {
            if (!cameras.TryGetValue(id, out var passes)) return;
            cameras.Remove(id);
            passes.Dispose();
        }

        private void ReleaseAll()
        {
            SideViewRenderComparison.Released -= Release;
            foreach (var passes in cameras.Values) passes.Dispose();
            cameras.Clear();
        }

        protected override void Dispose(bool disposing) => ReleaseAll();
        private void OnDisable() => ReleaseAll();

        private static RenderTextureDescriptor ColorDescriptor(ref RenderingData data)
        {
            var desc = data.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.bindMS = false;
            desc.depthBufferBits = 0;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.memoryless = RenderTextureMemoryless.None;
            return desc;
        }

        private static Vector4 TexelSize(RTHandle target) => new(1f / target.rt.width, 1f / target.rt.height, target.rt.width, target.rt.height);

        private sealed class CameraPasses : IDisposable
        {
            internal readonly BackgroundPass Background;
            internal readonly GradePass Grade;

            internal CameraPasses(SideViewRenderComparison comparison)
            {
                Background = new BackgroundPass(comparison);
                Grade = new GradePass(comparison);
            }

            public void Dispose()
            {
                Background.Dispose();
                Grade.Dispose();
            }
        }

        private sealed class BackgroundPass : ScriptableRenderPass, IDisposable
        {
            private static readonly ShaderTagId FarTag = new("SideViewFarCard");
            private static readonly ShaderTagId NearTag = new("SideViewUnderActorCard");
            private static readonly int SplitId = Shader.PropertyToID("_SideViewSplitBackground");
            private static readonly int BlurStepId = Shader.PropertyToID("_SvLayerTexelStep");
            private static readonly int LightRectId = Shader.PropertyToID("_SvHeroLightViewportRect");
            private static readonly int LightTextureId = Shader.PropertyToID("_SvHeroLightTexture");
            private static readonly int LightTintId = Shader.PropertyToID("_SvHeroLightTintGamma");
            private readonly SideViewRenderComparison comparison;
            private RTHandle cameraColor, cameraDepth, far, farScratch, backdrop;

            internal BackgroundPass(SideViewRenderComparison comparison)
            {
                this.comparison = comparison;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            internal void SetTargets(RTHandle color, RTHandle depth)
            {
                cameraColor = color;
                cameraDepth = depth;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                if (cameraColor == null || cameraDepth == null) return;
                var desc = ColorDescriptor(ref data);
                RenderingUtils.ReAllocateIfNeeded(ref backdrop, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Side view B near background");
                desc.width = Mathf.Max(1, Mathf.RoundToInt(360f * desc.width / desc.height));
                desc.height = 360;
                desc.useDynamicScale = false;
                RenderingUtils.ReAllocateIfNeeded(ref far, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Side view B far 360p");
                RenderingUtils.ReAllocateIfNeeded(ref farScratch, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Side view B far blur");
                ConfigureTarget(cameraColor, cameraDepth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (cameraColor?.rt == null || cameraDepth == null || far?.rt == null || farScratch?.rt == null || backdrop?.rt == null) return;
                var material = comparison.SilksongLayers;
                var camera = data.cameraData.camera;
                var filtering = new FilteringSettings(RenderQueueRange.transparent, camera.cullingMask);
                var cmd = CommandBufferPool.Get("Side view B background and hero light");

                // 远层与近层用同一次剔除和最终相机矩阵。固定 Z 透明顺序保持 Screen 水流在应有层次。
                Color background = camera.backgroundColor;
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) background = background.linear;
                CoreUtils.SetRenderTarget(cmd, far, ClearFlag.Color, background);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var farDrawing = CreateDrawingSettings(FarTag, ref data, SortingCriteria.CommonTransparent);
                context.DrawRenderers(data.cullResults, ref farDrawing, ref filtering);

                for (int iteration = 0; iteration < 2; iteration++)
                {
                    cmd.SetGlobalVector(BlurStepId, new Vector4(1f / far.rt.width, 0, 0, 0));
                    Blitter.BlitCameraTexture(cmd, far, farScratch, material, 0);
                    cmd.SetGlobalVector(BlurStepId, new Vector4(0, 1f / far.rt.height, 0, 0));
                    Blitter.BlitCameraTexture(cmd, farScratch, far, material, 0);
                }
                CoreUtils.SetRenderTarget(cmd, cameraColor, cameraDepth);
                // 回填落在背景深度，保留原来已提交的不透明对象及其深度。
                Blitter.BlitTexture(cmd, far, new Vector4(1, 1, 0, 0), material, 3);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var nearDrawing = CreateDrawingSettings(NearTag, ref data, SortingCriteria.CommonTransparent);
                context.DrawRenderers(data.cullResults, ref nearDrawing, ref filtering);

                if (comparison.TryGetHeroLightRect(out var rect))
                {
                    Blitter.BlitCameraTexture(cmd, cameraColor, backdrop);
                    cmd.SetGlobalTexture(LightTextureId, comparison.HeroLight);
                    cmd.SetGlobalVector(LightRectId, rect);
                    cmd.SetGlobalVector(LightTintId, comparison.HeroLightGamma);
                    Blitter.BlitCameraTexture(cmd, backdrop, cameraColor, material, 1);
                }
                CoreUtils.SetRenderTarget(cmd, cameraColor, cameraDepth);
                cmd.SetGlobalFloat(SplitId, 1);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                comparison.LastBackgroundFrame = Time.frameCount;
            }

            public void Dispose()
            {
                far?.Release();
                farScratch?.Release();
                backdrop?.Release();
                far = farScratch = backdrop = cameraColor = cameraDepth = null;
            }
        }

        private sealed class GradePass : ScriptableRenderPass, IDisposable
        {
            private static readonly int SourceSizeId = Shader.PropertyToID("_SvSourceTexelSize");
            private static readonly int BloomSizeId = Shader.PropertyToID("_SvBloomTexelSize");
            private static readonly int IterationId = Shader.PropertyToID("_SvBloomIteration");
            private static readonly int BloomId = Shader.PropertyToID("_SvBloomTexture");
            private static readonly int BloomScaleId = Shader.PropertyToID("_SvBloomScaleBias");
            private static readonly int VignetteTextureId = Shader.PropertyToID("_SvVignetteTexture");
            private static readonly int VignetteRectId = Shader.PropertyToID("_SvVignetteViewportRect");
            private static readonly int VignetteTintId = Shader.PropertyToID("_SvVignetteTintGamma");
            private readonly SideViewRenderComparison comparison;
            private readonly SideViewComparisonDarkness darkness;
            private RTHandle cameraColor, source, bloom, bloomScratch;

            internal GradePass(SideViewRenderComparison comparison)
            {
                this.comparison = comparison;
                darkness = comparison.CreateDarkness();
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 3;
            }

            internal void SetTarget(RTHandle color) => cameraColor = color;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                if (cameraColor == null) return;
                var desc = ColorDescriptor(ref data);
                RenderingUtils.ReAllocateIfNeeded(ref source, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Side view B color before grade");
                desc.width = Mathf.Max(1, desc.width / 4);
                desc.height = Mathf.Max(1, desc.height / 4);
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.useDynamicScale = false;
                RenderingUtils.ReAllocateIfNeeded(ref bloom, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Side view B bloom");
                RenderingUtils.ReAllocateIfNeeded(ref bloomScratch, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Side view B bloom blur");
                ConfigureTarget(cameraColor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (cameraColor?.rt == null || source?.rt == null || bloom?.rt == null || bloomScratch?.rt == null) return;
                var material = comparison.SilksongGrade;
                var cmd = CommandBufferPool.Get("Side view B bloom and RGB curves");
                Blitter.BlitCameraTexture(cmd, cameraColor, source);
                bool hasDarkness = darkness.Prepare(source.rt.width, source.rt.height);
                if (hasDarkness)
                {
                    darkness.RecordCutout(cmd, data.cameraData.GetGPUProjectionMatrix() * data.cameraData.GetViewMatrix());
                    darkness.RecordSceneBorders(cmd);
                    darkness.BindOverlays(cmd);
                    Blitter.BlitCameraTexture(cmd, source, cameraColor, darkness.Material, SideViewComparisonDarkness.CompositePass);
                    Blitter.BlitCameraTexture(cmd, cameraColor, source);
                    comparison.LastDarknessFrame = Time.frameCount;
                }
                if (comparison.TryGetHeroPosition(out var position) && comparison.TryGetVignetteRect(darkness.VignetteScale(position), out var rect))
                {
                    cmd.SetGlobalTexture(VignetteTextureId, comparison.Vignette);
                    cmd.SetGlobalVector(VignetteRectId, rect);
                    cmd.SetGlobalVector(VignetteTintId, new Vector4(0, 0, 0, 1));
                    if (hasDarkness) darkness.BindVignetteCutout(cmd);
                    Blitter.BlitCameraTexture(cmd, source, cameraColor, comparison.SilksongLayers, 2);
                    Blitter.BlitCameraTexture(cmd, cameraColor, source);
                }
                SideViewComparisonDarkness.ClearBindings(cmd);
                cmd.SetGlobalVector(SourceSizeId, TexelSize(source));
                cmd.SetGlobalVector(BloomSizeId, TexelSize(bloom));
                Blitter.BlitCameraTexture(cmd, source, bloom, material, 0);
                for (int iteration = 0; iteration < 2; iteration++)
                {
                    cmd.SetGlobalFloat(IterationId, iteration);
                    cmd.SetGlobalVector(SourceSizeId, TexelSize(bloom));
                    Blitter.BlitCameraTexture(cmd, bloom, bloomScratch, material, 1);
                    cmd.SetGlobalVector(SourceSizeId, TexelSize(bloomScratch));
                    Blitter.BlitCameraTexture(cmd, bloomScratch, bloom, material, 2);
                }
                cmd.SetGlobalTexture(BloomId, bloom.nameID);
                cmd.SetGlobalVector(BloomScaleId, new Vector4(1, 1, 0, 0));
                cmd.SetGlobalVector(SourceSizeId, TexelSize(source));
                Blitter.BlitCameraTexture(cmd, source, cameraColor, material, 3);
                cmd.SetGlobalTexture(BloomId, Texture2D.blackTexture);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                comparison.LastGradeFrame = Time.frameCount;
            }

            public void Dispose()
            {
                source?.Release();
                bloom?.Release();
                bloomScratch?.Release();
                darkness.Dispose();
                source = bloom = bloomScratch = cameraColor = null;
            }
        }
    }
}
