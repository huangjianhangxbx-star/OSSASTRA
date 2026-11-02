/* 功能：为登记GameplayCamera建立有限源绘制范围，再以合法表面重建颜色/深度与标签许可。
 * 边界：Full使用原材质；静态源按材质桶，动态Lit按只读源筛选位，动态无光照源用独立适配绘制。
 * 生命周期：唯一组织者发布帧；公开EndScope与真实Cleanup幂等归还借用范围，撤销与Dispose释放自有资源。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    public sealed class RoomMaskRendererFeature : ScriptableRendererFeature
    {
        public Shader surfaceMaskShader;
        public Shader portalMaskShader;
        public Shader compositeShader;
        [Tooltip("世界标签Shader的显式构建引用；视图只在已登记相机中按同名创建自有UI材质。")]
        public Shader worldLabelShader;
        [Tooltip("正向许可绘制的纯色真值Shader；未登记的世界颜色与深度不会保留。")]
        public Shader positiveTruthShader;
        public Shader positiveSpriteLitTruthShader;
        public Shader positiveLitShader, positiveUnlitShader, positiveSimpleLitShader;
        public Shader positiveThinTopShader, positiveWorldTileShader, positiveDecorationShader;
        public Shader positiveDepthPlaneShader, positiveSpineShader, positiveSpriteShader, positiveSpriteLitShader;
        readonly Dictionary<int, CameraPasses> cameras = new Dictionary<int, CameraPasses>();
        readonly Dictionary<int, RoomPositivePass> positiveCameras = new Dictionary<int, RoomPositivePass>();
        ScriptableRenderer rendererOwner;
        bool subscribed;

        public override void Create()
        {
            DisposeResources();
            RoomMaskCameraRegistry.Released += ReleaseCamera;
            RoomMaskCameraRegistry.FramePublished += PreparePublishedFrame;
            RoomMaskCameraRegistry.ScopeEnded += EndCameraScope;
            subscribed = true;
        }

        void PreparePublishedFrame(Camera camera)
        {
            if (camera == null || rendererOwner == null || camera.cameraType != CameraType.Game) return;
            // 只有这个Feature真正所属的Renderer可以借用源材质，避免未使用资产的Create订阅重复写入。
            if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.scriptableRenderer != rendererOwner) return;
            int id = camera.GetInstanceID();
            if (!RoomMaskCameraRegistry.TryGet(camera, out var frame)) return;
            if (!positiveCameras.TryGetValue(id, out var positive))
            { positive = new RoomPositivePass(camera); positiveCameras.Add(id, positive); }
            positive.PrepareBeforeCulling(frame, this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            rendererOwner = renderer;
            var camera = renderingData.cameraData.camera;
            if (camera == null || camera.cameraType != CameraType.Game ||
                !RoomMaskCameraRegistry.TryGet(camera, out var frame)) return;
            int id = camera.GetInstanceID();
            if (frame.PositiveRenderingEnabled)
            {
                if (!positiveCameras.TryGetValue(id, out var positive))
                { positive = new RoomPositivePass(camera); positiveCameras.Add(id, positive); }
                positive.SetFrame(frame, this);
                RoomMaskCameraRegistry.SetStatistics(camera, positive.Statistics);
                if (frame.Enabled) renderer.EnqueuePass(positive);
                return;
            }
            if (!cameras.TryGetValue(id, out var pair))
            {
                pair = new CameraPasses(this, camera); cameras.Add(id, pair);
            }
            pair.Stats.Active = false;
            RoomMaskCameraRegistry.SetStatistics(camera, pair.Stats);
            if (!frame.Enabled) return;
            pair.Frame = frame;
            renderer.EnqueuePass(pair.Mask);
            renderer.EnqueuePass(pair.Composite);
        }

        void ReleaseCamera(int id)
        {
            if (cameras.TryGetValue(id, out var pair)) { pair.Dispose(); cameras.Remove(id); }
            if (positiveCameras.TryGetValue(id, out var positive)) { positive.Dispose(); positiveCameras.Remove(id); }
        }
        void EndCameraScope(int id)
        { if (positiveCameras.TryGetValue(id, out var positive)) positive.EndScope(); }
        void DisposeResources()
        {
            if (subscribed)
            {
                RoomMaskCameraRegistry.Released -= ReleaseCamera;
                RoomMaskCameraRegistry.FramePublished -= PreparePublishedFrame;
                RoomMaskCameraRegistry.ScopeEnded -= EndCameraScope;
                subscribed = false;
            }
            foreach (var pair in cameras.Values) pair.Dispose(); cameras.Clear();
            foreach (var pair in positiveCameras.Values) pair.Dispose(); positiveCameras.Clear();
            rendererOwner = null;
        }
        protected override void Dispose(bool disposing) { DisposeResources(); }

        sealed class CameraPasses : IDisposable
        {
            internal readonly RoomMaskRendererFeature Feature;
            internal readonly Camera Camera;
            internal readonly RoomMaskStatistics Stats = new RoomMaskStatistics();
            internal readonly MaskPass Mask;
            internal readonly CompositePass Composite;
            internal RoomMaskFrame Frame;
            internal RTHandle Target;
            internal Material PortalMaterial, CompositeMaterial, UnexploredMaterial;
            Mesh unitCube;
            internal bool Ready;
            readonly Matrix4x4[] roomMatrices = new Matrix4x4[8];
            readonly Vector4[] halfExtents = new Vector4[8];
            readonly Matrix4x4[] knownMatrices = new Matrix4x4[8];
            readonly Vector4[] knownExtents = new Vector4[8];
            readonly Dictionary<AdapterKey, Material> adapters = new Dictionary<AdapterKey, Material>();
            readonly List<Material> sourceSlots = new List<Material>(8);
            readonly List<AdapterKey> stale = new List<AdapterKey>(8);
            readonly HashSet<AdapterKey> used = new HashSet<AdapterKey>();
            readonly MaterialPropertyBlock portalBlock = new MaterialPropertyBlock();
            readonly struct AdapterKey : IEquatable<AdapterKey>
            {
                internal readonly Material Source;
                readonly int sourceId;
                readonly bool known, clip;
                internal AdapterKey(Material source, bool known, bool clip)
                { Source = source; sourceId = source.GetInstanceID(); this.known = known; this.clip = clip; }
                public bool Equals(AdapterKey other) => ReferenceEquals(Source, other.Source) && known == other.known && clip == other.clip;
                public override bool Equals(object obj) => obj is AdapterKey other && Equals(other);
                public override int GetHashCode() => (sourceId * 397) ^ (known ? 2 : 0) ^ (clip ? 1 : 0);
            }

            internal CameraPasses(RoomMaskRendererFeature feature, Camera camera)
            {
                Feature = feature; Camera = camera;
                Mask = new MaskPass(this); Composite = new CompositePass(this);
            }

            internal bool Prepare(RenderTextureDescriptor descriptor, RTHandle actualDepth)
            {
                Ready = false; Stats.Error = ""; Stats.SurfaceDraws = Stats.PortalDraws = Stats.UnexploredVolumeDraws = 0;
                Stats.UnsupportedMaterialCount = Stats.PmaApproximationCount = 0;
                if (Frame == null || Frame.Volumes == null || Frame.Volumes.Length < 1 || Frame.Volumes.Length > 8)
                    return Fail("活动房间必须登记1—8个有效盒体。");
                if ((Frame.KnownVolumes != null && Frame.KnownVolumes.Length > 8) || (Frame.UnexploredVolumes != null && Frame.UnexploredVolumes.Length > 8))
                    return Fail("已到达/未知体积各自不得超过8个；不能静默截断。");
                if (Feature.surfaceMaskShader == null || Feature.portalMaskShader == null || Feature.compositeShader == null ||
                    !Feature.surfaceMaskShader.isSupported || !Feature.portalMaskShader.isSupported || !Feature.compositeShader.isSupported)
                    return Fail("缺少可运行的房间mask/门户/合成Shader。");
                if (actualDepth == null) return Fail("未取得本相机真实深度附件。");
                int samples = actualDepth.rt != null ? actualDepth.rt.antiAliasing : descriptor.msaaSamples;
                descriptor.depthBufferBits = 0; descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.msaaSamples = Mathf.Max(1, samples); descriptor.bindMS = false;
                // R/G是两个黑幕许可；B独立记录真实门户像素，供室外世界标签精确裁在窗孔内。
                descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                descriptor.enableRandomWrite = false; descriptor.useMipMap = false; descriptor.autoGenerateMips = false;
                if (SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor) < descriptor.msaaSamples)
                    return Fail("本设备mask颜色格式不能匹配真实深度的MSAA。");
                RenderingUtils.ReAllocateIfNeeded(ref Target, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "RoomVisibility Permission " + Camera.GetInstanceID());
                if (Target == null || Target.rt == null || Target.rt.antiAliasing != descriptor.msaaSamples)
                    return Fail("实际mask MSAA未匹配本相机深度附件；拒绝继续合成。");
                if (PortalMaterial == null) PortalMaterial = Owned(Feature.portalMaskShader, "RoomVisibility portal");
                if (CompositeMaterial == null) CompositeMaterial = Owned(Feature.compositeShader, "RoomVisibility curtain");
                if (UnexploredMaterial == null) UnexploredMaterial = Owned(Feature.surfaceMaskShader, "RoomVisibility unknown volume");
                if (unitCube == null) unitCube = CreateCube();
                Stats.Width = descriptor.width; Stats.Height = descriptor.height; Stats.Msaa = descriptor.msaaSamples;
                Stats.OwnedTextureCount = Target == null ? 0 : 1;
                const long bytes = 4L;
                Stats.EstimatedTextureBytes = descriptor.width * (long)descriptor.height * bytes * (descriptor.msaaSamples > 1 ? descriptor.msaaSamples + 1 : 1);
                for (int i = 0; i < Frame.Volumes.Length; ++i)
                {
                    var volume = Frame.Volumes[i];
                    if (volume.HalfExtents.x <= 0 || volume.HalfExtents.y <= 0 || volume.HalfExtents.z <= 0)
                        return Fail("房间mask盒体尺寸必须为正。");
                    roomMatrices[i] = volume.WorldToLocal; halfExtents[i] = volume.HalfExtents;
                }
                var known = Frame.KnownVolumes ?? Array.Empty<RoomMaskVolume>();
                for (int i = 0; i < known.Length; ++i)
                {
                    if (known[i].HalfExtents.x <= 0 || known[i].HalfExtents.y <= 0 || known[i].HalfExtents.z <= 0) return Fail("已到达mask体积尺寸必须为正。");
                    knownMatrices[i] = known[i].WorldToLocal; knownExtents[i] = known[i].HalfExtents;
                }
                var unknown = Frame.UnexploredVolumes ?? Array.Empty<RoomMaskVolume>();
                for (int i = 0; i < unknown.Length; ++i)
                    if (unknown[i].HalfExtents.x <= 0 || unknown[i].HalfExtents.y <= 0 || unknown[i].HalfExtents.z <= 0) return Fail("未知mask体积尺寸必须为正。");
                Stats.OwnedMeshCount = unitCube == null ? 0 : 1;
                Ready = true; return true;
            }

            bool Fail(string reason) { Stats.Error = reason; return false; }

            internal void Draw(CommandBuffer cmd)
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                cmd.SetGlobalInteger("_RoomVolumeCount", Frame.Volumes.Length);
                cmd.SetGlobalMatrixArray("_RoomWorldToLocal", roomMatrices);
                cmd.SetGlobalVectorArray("_RoomHalfExtents", halfExtents);
                cmd.SetGlobalInteger("_RoomKnownVolumeCount", Frame.KnownVolumes == null ? 0 : Frame.KnownVolumes.Length);
                cmd.SetGlobalMatrixArray("_RoomKnownWorldToLocal", knownMatrices);
                cmd.SetGlobalVectorArray("_RoomKnownHalfExtents", knownExtents);
                used.Clear();
                DrawSurfaces(cmd, Frame.BuildingSurfaces, false);
                var unknown = Frame.UnexploredVolumes ?? Array.Empty<RoomMaskVolume>();
                for (int i = 0; i < unknown.Length; ++i)
                {
                    var volume = unknown[i];
                    var localCamera = volume.WorldToLocal.MultiplyPoint3x4(Camera.transform.position);
                    bool inside = Mathf.Abs(localCamera.x) <= volume.HalfExtents.x && Mathf.Abs(localCamera.y) <= volume.HalfExtents.y && Mathf.Abs(localCamera.z) <= volume.HalfExtents.z;
                    cmd.SetGlobalFloat("_RoomCameraInsideUnknownVolume", inside ? 1 : 0);
                    var matrix = volume.WorldToLocal.inverse * Matrix4x4.Scale(volume.HalfExtents * 2f);
                    cmd.DrawMesh(unitCube, matrix, UnexploredMaterial, 0, 2); ++Stats.UnexploredVolumeDraws;
                }
                DrawSurfaces(cmd, Frame.Surfaces, true);
                var portals = Frame.Portals ?? Array.Empty<RoomMaskPortal>();
                for (int i = 0; i < portals.Length; ++i)
                {
                    var portal = portals[i]; if (portal.ApertureMesh == null || portal.OutwardNormal.sqrMagnitude < .99f) continue;
                    portalBlock.Clear(); portalBlock.SetVector("_RoomPortalOutward", portal.OutwardNormal.normalized);
                    cmd.DrawMesh(portal.ApertureMesh, portal.LocalToWorld, PortalMaterial, 0, 0, portalBlock); ++Stats.PortalDraws;
                }
                stale.Clear(); foreach (var entry in adapters) if (entry.Key.Source == null || !used.Contains(entry.Key)) stale.Add(entry.Key);
                for (int i = 0; i < stale.Count; ++i) { DestroyOwned(adapters[stale[i]]); adapters.Remove(stale[i]); }
                Stats.OwnedMaterialCount = adapters.Count + (PortalMaterial != null ? 1 : 0) + (CompositeMaterial != null ? 1 : 0) + (UnexploredMaterial != null ? 1 : 0);
                if (Stats.UnsupportedMaterialCount > 0) Stats.Error = "存在未支持材质，许可mask不完整；不能视为渲染验收通过。";
                Stats.Active = true;
                Stats.CommandBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            }

            void DrawSurfaces(CommandBuffer cmd, RoomMaskSurface[] values, bool known)
            {
                var surfaces = values ?? Array.Empty<RoomMaskSurface>();
                for (int i = 0; i < surfaces.Length; ++i)
                {
                    var surface = surfaces[i]; var source = surface.Renderer;
                    // forceRenderingOff may now be held by the same Root's finite capture. The Root
                    // publishes eligible members before that scope; manual mask draw does not reveal color.
                    if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;
                    if (source is SpriteRenderer sprite && (sprite.drawMode != SpriteDrawMode.Simple || sprite.maskInteraction != SpriteMaskInteraction.None))
                    { ++Stats.UnsupportedMaterialCount; continue; }
                    source.GetSharedMaterials(sourceSlots);
                    for (int slot = 0; slot < sourceSlots.Count; ++slot)
                    {
                        var original = sourceSlots[slot];
                        int mode = Mode(original);
                        if (mode < 0) { ++Stats.UnsupportedMaterialCount; continue; }
                        var key = new AdapterKey(original, known, surface.ClipToVolumes);
                        if (!adapters.TryGetValue(key, out var material))
                        {
                            material = Owned(Feature.surfaceMaskShader, original.name + " [Room mask]");
                            adapters.Add(key, material);
                        }
                        used.Add(key); material.CopyPropertiesFromMaterial(original);
                        material.SetFloat("_RoomSurfaceMode", mode);
                        material.SetFloat("_RoomClipToVolumes", surface.ClipToVolumes ? 1 : 0);
                        material.SetFloat("_RoomClipUseKnown", known ? 1 : 0);
                        material.SetFloat("_RoomMaskChannels", known ? 12 : 8);
                        material.SetFloat("_RoomSourceAlphaClip", original.IsKeywordEnabled("_ALPHATEST_ON") ? 1 : 0);
                        bool upright = original.shader.name == "Ossastra/Characters/UprightDepthPlane" || original.shader.name == "Ossastra/Characters/UprightSpinePma" || original.shader.name == "Ossastra/Characters/UprightSpriteDefault";
                        if (!upright) material.SetFloat("_VisualDepthEnabled", 0);
                        if (mode == 0) material.SetFloat("_Cull", original.HasProperty("_Cull") ? original.GetFloat("_Cull") : 2);
                        else material.SetFloat("_Cull", 0);
                        if (mode >= 4) ++Stats.PmaApproximationCount;
                        // 普通Lit/cutout依赖硬件逐MSAA sample深度；SV_Depth中心值不能代替它。
                        bool hardwareDepth = mode == 0 || mode == 2 || (mode == 3 && original.shader.name == "Ossastra/MapResearch/DepthPlane");
                        cmd.DrawRenderer(source, material, slot, mode == 5 ? 1 : hardwareDepth ? 3 : 0); ++Stats.SurfaceDraws;
                    }
                }
            }

            static int Mode(Material material)
            {
                if (material == null || material.shader == null) return -1;
                string name = material.shader.name;
                if ((name == "Universal Render Pipeline/Lit" || name == "Universal Render Pipeline/Simple Lit" || name == "Universal Render Pipeline/Unlit") &&
                    (!material.HasProperty("_Surface") || material.GetFloat("_Surface") == 0) &&
                    (!material.HasProperty("_ZWrite") || material.GetFloat("_ZWrite") == 1) &&
                    (!material.HasProperty("_SrcBlend") || material.GetFloat("_SrcBlend") == (float)BlendMode.One) &&
                    (!material.HasProperty("_DstBlend") || material.GetFloat("_DstBlend") == (float)BlendMode.Zero)) return 0;
                if (name == "Ossastra/GroundSurfaces/ThinTop") return 1;
                if (name == "Ossastra/GroundWorkshop/DecorationCutout") return 2;
                if (name == "Ossastra/MapResearch/DepthPlane" || name == "Ossastra/Characters/UprightDepthPlane") return 3;
                if (name == "Spine/Skeleton" || name == "Ossastra/Characters/UprightSpinePma") return 4;
                if (name == "Ossastra/Characters/UprightSpriteDefault" || name == "Sprites/Default") return 5;
                return -1;
            }

            internal void CompositeDraw(CommandBuffer cmd)
            {
                cmd.SetGlobalTexture("_RoomVisibilityPermission", Target.nameID);
                cmd.SetGlobalVector("_RoomMaskSize", new Vector4(Stats.Width, Stats.Height, 1f / Stats.Width, 1f / Stats.Height));
                cmd.SetGlobalFloat("_RoomVisibilityMaskEnabled", 1);
                cmd.SetGlobalFloat("_RoomVisibilityExteriorEnabled", Frame.ExteriorBlackoutEnabled ? 1 : 0);
                cmd.SetGlobalFloat("_RoomVisibilityInteriorEnabled", Frame.InteriorUnexploredEnabled ? 1 : 0);
                cmd.SetGlobalFloat("_RoomWorldLabelMaskReady", 1);
                CompositeMaterial.SetColor("_RoomExteriorColor", Frame.ExteriorColor);
                CompositeMaterial.SetColor("_RoomInteriorColor", Frame.InteriorColor);
                CompositeMaterial.SetFloat("_RoomExteriorEnabled", Frame.ExteriorBlackoutEnabled ? 1 : 0);
                CompositeMaterial.SetFloat("_RoomInteriorEnabled", Frame.InteriorUnexploredEnabled ? 1 : 0);
                CompositeMaterial.SetFloat("_RoomMaskDebug", Frame.DebugMode);
                cmd.DrawProcedural(Matrix4x4.identity, CompositeMaterial, 0, MeshTopology.Triangles, 3);
            }

            public void Dispose()
            {
                Target?.Release(); Target = null;
                foreach (var entry in adapters) DestroyOwned(entry.Value); adapters.Clear();
                DestroyOwned(PortalMaterial); PortalMaterial = null; DestroyOwned(CompositeMaterial); CompositeMaterial = null;
                DestroyOwned(UnexploredMaterial); UnexploredMaterial = null; DestroyOwned(unitCube); unitCube = null;
                Stats.OwnedTextureCount = Stats.OwnedMaterialCount = 0; Stats.Active = Ready = false;
                Stats.OwnedMeshCount = 0;
            }

            static Mesh CreateCube()
            {
                var mesh = new Mesh { name = "RoomVisibility logical unknown volume", hideFlags = HideFlags.HideAndDontSave };
                mesh.vertices = new[] { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
                mesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                mesh.RecalculateBounds(); return mesh;
            }
        }

        sealed class MaskPass : ScriptableRenderPass
        {
            readonly CameraPasses owner;
            internal MaskPass(CameraPasses owner)
            { this.owner = owner; renderPassEvent = RenderPassEvent.AfterRenderingTransparents; }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                if (!owner.Prepare(renderingData.cameraData.cameraTargetDescriptor, renderer.cameraDepthTargetHandle)) return;
                ConfigureTarget(owner.Target, renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.Color, new Color(0, 1, 0, 0));
                ConfigureColorStoreAction(owner.Stats.Msaa > 1 ? RenderBufferStoreAction.StoreAndResolve : RenderBufferStoreAction.Store);
                ConfigureDepthStoreAction(RenderBufferStoreAction.Store);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!owner.Ready || renderingData.cameraData.camera != owner.Camera) return;
                var cmd = CommandBufferPool.Get("RoomVisibility permission surfaces and portals");
                try { owner.Draw(cmd); context.ExecuteCommandBuffer(cmd); }
                finally { CommandBufferPool.Release(cmd); }
            }
            public override void OnCameraCleanup(CommandBuffer cmd)
            { if (cmd != null) cmd.SetGlobalFloat("_RoomVisibilityMaskEnabled", 0); }
        }

        sealed class CompositePass : ScriptableRenderPass
        {
            readonly CameraPasses owner;
            internal CompositePass(CameraPasses owner)
            { this.owner = owner; renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing; }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) { ResetTarget(); }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!owner.Ready || renderingData.cameraData.camera != owner.Camera) return;
                var cmd = CommandBufferPool.Get("RoomVisibility curtain after post processing");
                try
                {
                    CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
                    owner.CompositeDraw(cmd); context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
        }

        static Material Owned(Shader shader, string name)
        { return new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave }; }
        static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
