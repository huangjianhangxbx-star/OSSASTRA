using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

public sealed class GameRenderFeature : ScriptableRendererFeature
{
    private sealed class CharacterGlobalsPass : ScriptableRenderPass
    {
        private GameRenderProfile profile;
        private bool active;

        public CharacterGlobalsPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
        }

        public void Bind(GameRenderProfile source, bool isActive)
        {
            profile = source;
            active = isActive;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("GameRender Character Defaults");
            GameRenderCharacterGlobals.Write(cmd, profile, active);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private sealed class CoveragePass : ScriptableRenderPass
    {
        private static readonly ShaderTagId Universal2D = new ShaderTagId("Universal2D");
        private static readonly ShaderTagId DefaultUnlit = new ShaderTagId("SRPDefaultUnlit");
        private RTHandle coverage;
        private Material material;

        public CoveragePass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public void Bind(Material value) => material = value;

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref coverage, descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_GameRenderProtectedCoverage");
            ConfigureTarget(coverage);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (coverage == null || material == null) return;
            var camera = renderingData.cameraData.camera;
            var sorting = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
            var drawing = new DrawingSettings(Universal2D, sorting)
            {
                overrideMaterial = material,
                overrideMaterialPassIndex = 0,
                perObjectData = PerObjectData.None
            };
            drawing.SetShaderPassName(1, DefaultUnlit);
            var filtering = new FilteringSettings(RenderQueueRange.all, camera.cullingMask);
            filtering.sortingLayerRange = new SortingLayerRange(20, 24);
            context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
            filtering.sortingLayerRange = new SortingLayerRange(32, 35);
            context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
            filtering.sortingLayerRange = new SortingLayerRange(37, 37);
            context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
            var cmd = CommandBufferPool.Get("GameRender Protected Coverage");
            cmd.SetGlobalTexture("_GameRenderProtectedCoverage", coverage.nameID);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Release()
        {
            coverage?.Release();
            coverage = null;
        }
    }

    private sealed class StylePass : ScriptableRenderPass
    {
        private readonly int shaderPass;
        private readonly ProfilingSampler sampler;
        private RTHandle source;
        private RTHandle temporary;
        private Material material;
        private GameRenderProfile profile;

        public StylePass(int shaderPass, RenderPassEvent when, string name)
        {
            this.shaderPass = shaderPass;
            renderPassEvent = when;
            sampler = new ProfilingSampler(name);
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void Bind(RTHandle target, Material styleMaterial, GameRenderProfile styleProfile)
        {
            source = target;
            material = styleMaterial;
            profile = styleProfile;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref temporary, descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: shaderPass == 0 ? "_GameRenderStyleTemp" : "_GameRenderGrainTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (source == null || temporary == null || material == null || profile == null) return;
            var cmd = CommandBufferPool.Get(shaderPass == 0 ? "GameRender Style" : "GameRender Grain");
            using (new ProfilingScope(cmd, sampler))
            {
                if (shaderPass == 0)
                {
                    material.SetFloat("_InkStrength", GameRenderParameters.Clamp(profile.inkStrength, 0f, 1f));
                    material.SetFloat("_DarkThreshold", GameRenderParameters.Clamp(profile.darknessThreshold, 0f, 1f));
                    material.SetFloat("_DarkSpread", GameRenderParameters.Clamp(profile.darkSpread, 0f, 1f));
                    material.SetFloat("_OuterLine", GameRenderParameters.Clamp(profile.outerLineStrength, 0f, 2f));
                    material.SetFloat("_InnerLine", GameRenderParameters.Clamp(profile.innerLineStrength, 0f, 2f));
                    material.SetFloat("_HighlightLift", GameRenderParameters.Clamp(profile.highlightLift, 0f, 1f));
                    material.SetFloat("_RoughEdge", GameRenderParameters.Clamp(profile.roughEdgeStrength, 0f, 1f));
                    material.SetFloat("_DebugClassification", profile.debugClassification ? 1f : 0f);
                }
                else
                {
                    material.SetFloat("_GrainStrength", GameRenderParameters.Clamp(profile.grainStrength, 0f, 0.12f));
                    material.SetFloat("_GrainPixelSize", GameRenderParameters.Clamp(profile.grainPixelSize, 0.5f, 4f));
                    material.SetFloat("_GrainFrame", Mathf.Floor(Time.realtimeSinceStartup *
                        GameRenderParameters.Clamp(profile.grainFramesPerSecond, 0f, 8f)));
                }
                Blitter.BlitCameraTexture(cmd, source, temporary, material, shaderPass);
                Blitter.BlitCameraTexture(cmd, temporary, source);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Release()
        {
            temporary?.Release();
            temporary = null;
        }
    }

    private Material material;
    private Material coverageMaterial;
    private CharacterGlobalsPass characterGlobalsPass;
    private CoveragePass coveragePass;
    private StylePass worldPass;
    private StylePass grainPass;

    public override void Create()
    {
        worldPass?.Release();
        grainPass?.Release();
        coveragePass?.Release();
        var shader = Shader.Find("Hidden/ANKALUTE/GameRenderStyle");
        if (shader != null && material == null) material = CoreUtils.CreateEngineMaterial(shader);
        var coverageShader = Shader.Find("Hidden/ANKALUTE/GameRenderCharacterMask");
        if (coverageShader != null && coverageMaterial == null)
            coverageMaterial = CoreUtils.CreateEngineMaterial(coverageShader);
        characterGlobalsPass = new CharacterGlobalsPass();
        coveragePass = new CoveragePass();
        worldPass = new StylePass(0, RenderPassEvent.BeforeRenderingPostProcessing, "GameRender World Style");
        grainPass = new StylePass(1, RenderPassEvent.AfterRenderingPostProcessing, "GameRender World Grain");
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var manager = GameRenderManager.ForCamera(renderingData.cameraData.camera);
        if (manager == null || renderingData.cameraData.renderType != CameraRenderType.Base) return;
        characterGlobalsPass.Bind(manager.Profile, manager.EnableNewStyle);
        renderer.EnqueuePass(characterGlobalsPass);
        if (!manager.EnableNewStyle || material == null || coverageMaterial == null) return;
        var profile = manager.Profile;
        bool world = GameRenderParameters.HasWorldEffect(profile);
        bool grain = GameRenderParameters.HasGrainEffect(profile);
        if (world || grain) renderer.EnqueuePass(coveragePass);
        if (world) renderer.EnqueuePass(worldPass);
        if (grain) renderer.EnqueuePass(grainPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        var manager = GameRenderManager.ForCamera(renderingData.cameraData.camera);
        if (manager == null || !manager.EnableNewStyle || material == null || coverageMaterial == null ||
            renderingData.cameraData.renderType != CameraRenderType.Base) return;
        coveragePass.Bind(coverageMaterial);
        worldPass.Bind(renderer.cameraColorTargetHandle, material, manager.Profile);
        grainPass.Bind(renderer.cameraColorTargetHandle, material, manager.Profile);
    }

    protected override void Dispose(bool disposing)
    {
        worldPass?.Release();
        grainPass?.Release();
        coveragePass?.Release();
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(coverageMaterial);
        material = null;
        coverageMaterial = null;
    }
}
