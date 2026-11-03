/* 功能：在当前相机合法世界重建后应用既有配置的环境色调、空气、轻量Bloom与景深。
 * 边界：只消费本帧许可和新深度，不开启内建后处理、不换管线；人物/信息/未知区域保持原色。
 * 生命周期：每登记相机持有两材质、颜色副本与解析深度；配置撤销、合法源撤销和Dispose精确释放。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.WorldRendering.Rendering
{
    public sealed class WorldLookRendererFeature : ScriptableRendererFeature
    {
        [Tooltip("环境效果显式构建引用；只对当前合法世界执行。")]
        public Shader environmentLookShader;
        [Tooltip("从当前合法真实深度附件解析MSAA的显式构建引用。")]
        public Shader legalDepthCopyShader;
        readonly Dictionary<int, LookPass> cameras = new Dictionary<int, LookPass>();
        bool subscribed;
        public int ActiveCameraCount => cameras.Count;
        public int OwnedTextureCount { get { int count = 0; foreach (var pass in cameras.Values) count += pass.TextureCount; return count; } }
        public int OwnedMaterialCount { get { int count = 0; foreach (var pass in cameras.Values) count += pass.MaterialCount; return count; } }
        public string Status { get; private set; } = "等待环境配置与合法帧。";

        public override void Create()
        {
            ReleaseResources();
            Subscribe();
        }

        void Subscribe()
        {
            if (subscribed) return;
            WorldLookCameraRegistry.Released += ReleaseCamera;
            WorldLookLegalFrameRegistry.Released += ReleaseCamera;
            subscribed = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            // 显式ReleaseResources/OnDisable之后再次启用时，Unity不保证为已有Renderer重跑Create。
            Subscribe();
            var camera = data.cameraData.camera;
            if (camera == null || camera.cameraType != CameraType.Game || data.cameraData.isPreviewCamera) return;
            int id = camera.GetInstanceID();
            if (!WorldLookCameraRegistry.TryGet(camera, out var settings) || settings == null || !settings.Enabled)
            { ReleaseCamera(id); Status = "当前相机环境效果已关闭。"; return; }
            if (data.cameraData.postProcessEnabled)
            { ReleaseCamera(id); Status = "拒绝内建后处理与未认证旧深度共同运行。"; return; }
            if (environmentLookShader == null || !environmentLookShader.isSupported || legalDepthCopyShader == null || !legalDepthCopyShader.isSupported)
            { ReleaseCamera(id); Status = "缺少可运行的环境/合法深度Shader显式引用。"; return; }
            if (!cameras.TryGetValue(id, out var pass))
            {
                pass = new LookPass(this, camera, environmentLookShader, legalDepthCopyShader); cameras.Add(id, pass);
            }
            pass.Settings = settings;
            renderer.EnqueuePass(pass);
        }

        void ReleaseCamera(int id)
        {
            if (cameras.TryGetValue(id, out var pass)) { cameras.Remove(id); pass.Dispose(); }
        }
        public void ReleaseResources()
        {
            if (subscribed)
            {
                WorldLookCameraRegistry.Released -= ReleaseCamera;
                WorldLookLegalFrameRegistry.Released -= ReleaseCamera;
                subscribed = false;
            }
            foreach (var pass in cameras.Values) pass.Dispose(); cameras.Clear();
            Status = "环境自有资源已释放。";
        }
        protected override void Dispose(bool disposing) => ReleaseResources();
        void OnDisable() => ReleaseResources();

        sealed class LookPass : ScriptableRenderPass, IDisposable
        {
            readonly WorldLookRendererFeature owner;
            readonly Camera camera;
            Material look, depthCopy;
            RTHandle colorCopy, legalDepth;
            bool ready;
            internal WorldLookSettings Settings { get; set; }
            internal int TextureCount => (colorCopy != null ? 1 : 0) + (legalDepth != null ? 1 : 0);
            internal int MaterialCount => (look != null ? 1 : 0) + (depthCopy != null ? 1 : 0);

            internal LookPass(WorldLookRendererFeature owner, Camera camera, Shader lookShader, Shader depthShader)
            {
                this.owner = owner; this.camera = camera;
                look = CoreUtils.CreateEngineMaterial(lookShader); depthCopy = CoreUtils.CreateEngineMaterial(depthShader);
                look.name = "WorldLook environment only " + camera.GetInstanceID();
                depthCopy.name = "WorldLook legal depth resolve " + camera.GetInstanceID();
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingTransparents + 1);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                ready = look != null && depthCopy != null && Settings != null && Settings.Enabled;
                if (!ready) return;
                var descriptor = data.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0; descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.msaaSamples = 1; descriptor.bindMS = false;
                descriptor.useMipMap = descriptor.autoGenerateMips = descriptor.enableRandomWrite = false;
                descriptor.memoryless = RenderTextureMemoryless.None;
                RenderingUtils.ReAllocateIfNeeded(ref colorCopy, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "WorldLook legal color copy");
                descriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
                RenderingUtils.ReAllocateIfNeeded(ref legalDepth, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "WorldLook fresh legal depth");
                ready = colorCopy != null && legalDepth != null && colorCopy.rt != null && legalDepth.rt != null;
                ConfigureTarget(data.cameraData.renderer.cameraColorTargetHandle);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (!ready || look == null || depthCopy == null || camera == null || Settings == null || !Settings.Enabled) return;
                if (!WorldLookLegalFrameRegistry.TryGet(camera, Time.frameCount, out var permission, out var sourceDepth))
                { owner.ReleaseCamera(camera.GetInstanceID()); owner.Status = "本帧无已完成的合法世界；环境效果不写入，自有资源已释放。"; return; }
                if (sourceDepth.rt.width != legalDepth.rt.width || sourceDepth.rt.height != legalDepth.rt.height ||
                    permission.rt.width != legalDepth.rt.width || permission.rt.height != legalDepth.rt.height)
                { owner.ReleaseCamera(camera.GetInstanceID()); owner.Status = "合法源尺寸不匹配；环境效果不写入，自有资源已释放。"; return; }
                var source = data.cameraData.renderer.cameraColorTargetHandle;
                // 本场专属Renderer使用Intermediate Always。不能把不可采样BackBuffer当颜色纹理。
                if (source == null || source.rt == null || !source.rt.IsCreated())
                { owner.ReleaseCamera(camera.GetInstanceID()); owner.Status = "需要可采样的合法世界颜色附件；环境效果已保守关闭。"; return; }
                var cmd = CommandBufferPool.Get("WorldLook qualified environment");
                try
                {
                    int samples = Mathf.Max(1, sourceDepth.rt.antiAliasing);
                    if (samples != 1 && samples != 2 && samples != 4 && samples != 8)
                    { owner.Status = "不支持的合法深度MSAA样本数；环境效果不写入。"; return; }
                    CoreUtils.SetKeyword(depthCopy, "_WORLD_LOOK_DEPTH_MSAA_2", samples == 2);
                    CoreUtils.SetKeyword(depthCopy, "_WORLD_LOOK_DEPTH_MSAA_4", samples == 4);
                    CoreUtils.SetKeyword(depthCopy, "_WORLD_LOOK_DEPTH_MSAA_8", samples == 8);
                    cmd.SetGlobalTexture("_WorldLookDepthSource", sourceDepth.nameID);
                    cmd.SetGlobalVector("_WorldLookDepthSourceSize", new Vector4(sourceDepth.rt.width, sourceDepth.rt.height, 1f / sourceDepth.rt.width, 1f / sourceDepth.rt.height));
                    CoreUtils.SetRenderTarget(cmd, legalDepth);
                    bool flip = data.cameraData.IsHandleYFlipped(sourceDepth) != data.cameraData.IsHandleYFlipped(legalDepth);
                    Blitter.BlitTexture(cmd, sourceDepth, flip ? new Vector4(1, -1, 0, 1) : new Vector4(1, 1, 0, 0), depthCopy, 0);
                    BindSettings(permission, data.cameraData);
                    Blitter.BlitCameraTexture(cmd, source, colorCopy);
                    Blitter.BlitCameraTexture(cmd, colorCopy, source, look, 0);
                    context.ExecuteCommandBuffer(cmd);
                    owner.Status = "已接入本帧合法环境：色调/空气/景深/Bloom按配置；角色与黑幕保留。";
                }
                finally { CommandBufferPool.Release(cmd); }
            }

            void BindSettings(RTHandle permission, CameraData data)
            {
                var settings = Settings;
                look.SetTexture("_WorldLookPermission", permission.rt); look.SetTexture("_WorldLookDepth", legalDepth.rt);
                look.SetVector("_WorldLookTexelSize", new Vector4(1f / legalDepth.rt.width, 1f / legalDepth.rt.height, legalDepth.rt.width, legalDepth.rt.height));
                look.SetVector("_WorldLookUvTransform", new Vector4(1, 1, 0, 0));
                look.SetMatrix("_WorldLookInverseViewProjection", (data.GetGPUProjectionMatrix() * data.GetViewMatrix()).inverse);
                look.SetVector("_WorldLookFeatures", new Vector4(settings.GradeEnabled ? 1 : 0, settings.FocusEnabled ? 1 : 0, settings.AirEnabled ? 1 : 0, settings.BloomEnabled ? 1 : 0));
                look.SetVector("_WorldLookGrade", new Vector4(settings.Exposure, settings.Contrast, settings.Saturation, settings.Temperature));
                look.SetColor("_WorldLookFilter", settings.GradeFilter);
                look.SetVector("_WorldLookFocus", new Vector4(settings.FocusDepth, settings.SharpBand, settings.Transition, settings.BlurRadius));
                look.SetVector("_WorldLookBloom", new Vector4(settings.BloomIntensity, settings.BloomThreshold, settings.BloomScatter, 0));
                look.SetFloat("_WorldLookVignette", settings.VignetteEnabled ? settings.Vignette : 0);
                look.SetColor("_WorldLookAirNear", settings.AirNear); look.SetColor("_WorldLookAirMid", settings.AirMid); look.SetColor("_WorldLookAirFar", settings.AirFar);
                look.SetVector("_WorldLookAirDistances", new Vector4(settings.AirNearDistance, settings.AirMidDistance, settings.AirFarDistance, settings.Extinction));
                look.SetVector("_WorldLookAirStyle", new Vector4(settings.SaturationLoss, settings.ContrastLoss, settings.NearInfluence, 0));
            }

            public void Dispose()
            {
                ready = false; Settings = null;
                colorCopy?.Release(); colorCopy = null; legalDepth?.Release(); legalDepth = null;
                CoreUtils.Destroy(look); look = null; CoreUtils.Destroy(depthCopy); depthCopy = null;
            }
        }
    }
}
