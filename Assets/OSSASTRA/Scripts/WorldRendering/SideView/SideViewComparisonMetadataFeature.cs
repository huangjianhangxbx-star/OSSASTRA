/*
 * 脚本：SideViewComparisonMetadataFeature
 * 功能：以游戏相机的已剔除画片和角色生成横版专用环境许可及可采样深度。
 * 边界：只绘制有独立标签 Pass 的内容，不写相机色深附件、物理或房间权限。
 * 协作：在旧 WorldLook 前发布同帧借用附件，关闭 A 或释放相机时归还。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters.Rendering;
using Ossastra.WorldRendering.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.WorldRendering.SideView
{
    public sealed class SideViewComparisonMetadataFeature : ScriptableRendererFeature
    {
        private readonly Dictionary<int, SurfacePass> passes = new Dictionary<int, SurfacePass>();
        private bool subscribed;

        public override void Create()
        {
            ReleaseAll();
            Subscribe();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            Subscribe();
            var camera = data.cameraData.camera;
            if (camera == null || camera.cameraType != CameraType.Game ||
                !camera.TryGetComponent<SideViewCharacterRendering>(out var characters) || !characters.isActiveAndEnabled)
                return;
            int id = camera.GetInstanceID();
            if (!WorldLookCameraRegistry.TryGet(camera, out var look) || !look.Enabled || data.cameraData.postProcessEnabled)
            {
                Release(id);
                return;
            }
            if (!passes.TryGetValue(id, out var pass))
            {
                pass = new SurfacePass(camera);
                passes.Add(id, pass);
            }
            renderer.EnqueuePass(pass);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            WorldLookCameraRegistry.Released += Release;
            subscribed = true;
        }

        private void Release(int id)
        {
            if (!passes.TryGetValue(id, out var pass)) return;
            passes.Remove(id);
            pass.Dispose();
        }

        private void ReleaseAll()
        {
            if (subscribed) WorldLookCameraRegistry.Released -= Release;
            subscribed = false;
            foreach (var pass in passes.Values) pass.Dispose();
            passes.Clear();
        }

        protected override void Dispose(bool disposing) => ReleaseAll();
        private void OnDisable() => ReleaseAll();

        private sealed class SurfacePass : ScriptableRenderPass, IDisposable
        {
            private static readonly ShaderTagId PassName = new ShaderTagId("SideViewVisibleSurface");
            private readonly Camera camera;
            private RTHandle permission;
            private RTHandle depth;

            internal SurfacePass(Camera camera)
            {
                this.camera = camera;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                WorldLookLegalFrameRegistry.Invalidate(camera);
                var descriptor = data.cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.memoryless = RenderTextureMemoryless.None;
                descriptor.depthBufferBits = 0;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                RenderingUtils.ReAllocateIfNeeded(ref permission, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "Side view A surface permission");
                descriptor.graphicsFormat = GraphicsFormat.None;
                descriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
                RenderingUtils.ReAllocateIfNeeded(ref depth, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "Side view A surface depth");
                if (permission?.rt == null || depth?.rt == null) return;
                ConfigureTarget(permission, depth);
                ConfigureClear(ClearFlag.All, Color.clear);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (camera == null || data.cameraData.camera != camera ||
                    permission?.rt == null || !permission.rt.IsCreated() || depth?.rt == null || !depth.rt.IsCreated()) return;
                // 与游戏相机使用同一透明排序。片元 Pass 逐片保留真实 Alpha、角色偏移及保护标签。
                var drawing = CreateDrawingSettings(PassName, ref data, SortingCriteria.CommonTransparent);
                var filtering = new FilteringSettings(RenderQueueRange.transparent, camera.cullingMask);
                context.DrawRenderers(data.cullResults, ref drawing, ref filtering);
                WorldLookLegalFrameRegistry.Publish(camera, permission, depth, Time.frameCount);
            }

            public override void OnCameraCleanup(CommandBuffer cmd) => WorldLookLegalFrameRegistry.Invalidate(camera);

            public void Dispose()
            {
                WorldLookLegalFrameRegistry.Revoke(camera);
                permission?.Release();
                depth?.Release();
                permission = depth = null;
            }
        }
    }
}
