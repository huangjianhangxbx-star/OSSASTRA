using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.Characters.Rendering
{
    public sealed class SideViewCharacterRendererFeature : ScriptableRendererFeature
    {
        private CoveragePass pass;
        public override void Create() => pass = new CoveragePass();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.Game && camera.TryGetComponent<SideViewCharacterRendering>(out var binding) &&
                binding.isActiveAndEnabled) renderer.EnqueuePass(pass);
        }

        private sealed class CoveragePass : ScriptableRenderPass
        {
            private static readonly int FusionCoverage = Shader.PropertyToID("_SideViewAceFusionCoverage");

            public CoveragePass() => renderPassEvent = RenderPassEvent.BeforeRenderingTransparents - 1;

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var data = renderingData.cameraData;
                if (!data.camera.TryGetComponent<SideViewCharacterRendering>(out var binding) || !binding.isActiveAndEnabled) return;
                var descriptor = data.cameraTargetDescriptor;
                var renderer = data.renderer;
                var cmd = CommandBufferPool.Get("Side view character silhouettes and lighting");
                try
                {
                    Ossastra.EnvironmentSimulation.Rendering.EnvironmentSkyBinding.UploadOrdinaryForCamera(cmd, data.camera);
                    binding.Prepare(cmd, data.camera, data.GetGPUProjectionMatrix() * data.GetViewMatrix(), descriptor.width, descriptor.height);
                    CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
                    cmd.SetViewport(new Rect(0, 0, descriptor.width, descriptor.height));
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                SideViewOuterLight.ResetFusion(cmd);
                cmd.SetGlobalTexture(FusionCoverage, Texture2D.blackTexture);
            }
        }
    }
}
