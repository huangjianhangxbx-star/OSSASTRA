/* 功能：清除旧世界颜色/深度后，只绘制当前帧登记的合法表面；纯色路径先验证空间真值。
 * 边界：不生产房间/门户、不修改原材质、物理或相机，不将旧世界深度作为许可依据。
 * 生命周期：每相机复用自有适配材质；登记撤销/Renderer销毁时精确释放；超预算保守全黑。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    internal sealed class RoomPositivePass : ScriptableRenderPass, IDisposable
    {
        readonly Camera camera;
        readonly Dictionary<MaterialKey, Material> materials = new Dictionary<MaterialKey, Material>();
        readonly HashSet<MaterialKey> used = new HashSet<MaterialKey>();
        readonly List<MaterialKey> stale = new List<MaterialKey>();
        readonly List<Material> slots = new List<Material>(8);
        readonly HashSet<uint> independentLitGroups = new HashSet<uint>();
        readonly List<IndependentItem> independentItems = new List<IndependentItem>(32);
        struct IndependentItem { internal RoomPositiveSurface Surface; internal Material Source; internal int Slot, Queue, Layer, Order; internal float Distance; }
        readonly Matrix4x4[] seedMatrices = new Matrix4x4[8], buildingMatrices = new Matrix4x4[32],
            targetMatrices = new Matrix4x4[64], blockerMatrices = new Matrix4x4[64], ownerMatrices = new Matrix4x4[8], cameraBlockerMatrices = new Matrix4x4[64];
        readonly Vector4[] seedExtents = new Vector4[8], buildingExtents = new Vector4[32],
            targetExtents = new Vector4[64], blockerExtents = new Vector4[64], planes = new Vector4[80],
            meta = new Vector4[8], exterior = new Vector4[8], ownerExtents = new Vector4[8], cameraBlockerExtents = new Vector4[64],
            windowPlanes = new Vector4[80], windowMeta = new Vector4[8];
        readonly RoomFogWorldHistory fogHistory = new RoomFogWorldHistory();
        double presentationTime;
        RoomMaskFrame frame;
        RoomMaskRendererFeature feature;
        Shader truthShader;
        Shader spriteLitTruthShader;
        RTHandle previewCoverage;
        RTHandle previewDepth;
        RTHandle labelPermission;
        readonly RoomPositivePropertyScope propertyScope = new RoomPositivePropertyScope();
        readonly RoomPositiveMaterialBuckets materialBuckets = new RoomPositiveMaterialBuckets();
        static readonly List<ShaderTagId> sourceTags = new List<ShaderTagId>
        { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
        // 原角色覆盖辅助体会继承源渲染位，且只有SRPDefaultUnlit深度pass；动态Lit绝不选择它。
        static readonly List<ShaderTagId> independentLitTags = new List<ShaderTagId>
        { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };
        int preparedFrame = -1;
        bool preparedBeforeCulling;
        string preparationError = "等待剔除前的登记帧。";
        bool previewEnabled;
        bool ready;
        internal readonly RoomMaskStatistics Statistics = new RoomMaskStatistics();

        readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            internal readonly Material Source;
            internal readonly RoomPositiveSurfaceKind Kind;
            internal readonly bool CutRoof;
            internal readonly bool Permission;
            internal readonly bool Shaded;
            readonly int id;
            internal MaterialKey(Material source, RoomPositiveSurfaceKind kind, bool cutRoof, bool permission, bool shaded = false)
            { Source = source; Kind = kind; CutRoof = cutRoof; Permission = permission; Shaded = shaded; id = source.GetInstanceID(); }
            public bool Equals(MaterialKey other) => ReferenceEquals(Source, other.Source) && Kind == other.Kind && CutRoof == other.CutRoof && Permission == other.Permission && Shaded == other.Shaded;
            public override bool Equals(object value) => value is MaterialKey other && Equals(other);
            public override int GetHashCode() => id * 397 ^ (int)Kind * 8 ^ (CutRoof ? 4 : 0) ^ (Permission ? 2 : 0) ^ (Shaded ? 1 : 0);
        }

        internal RoomPositivePass(Camera camera)
        { this.camera = camera; renderPassEvent = RenderPassEvent.AfterRenderingTransparents; }

        internal void SetFrame(RoomMaskFrame next, RoomMaskRendererFeature nextFeature)
        {
            frame = next;
            feature = nextFeature;
            // 真值图不经过色调映射/景深；最终材质阶段才在后处理前重新生成受限世界。
            renderPassEvent = next.TruthMode ? RenderPassEvent.AfterRenderingPostProcessing : RenderPassEvent.AfterRenderingTransparents;
            if (truthShader != feature.positiveTruthShader || spriteLitTruthShader != feature.positiveSpriteLitTruthShader)
            { ClearMaterials(); truthShader = feature.positiveTruthShader; spriteLitTruthShader = feature.positiveSpriteLitTruthShader; }
            Statistics.Active = false;
        }

        internal void PrepareBeforeCulling(RoomMaskFrame next, RoomMaskRendererFeature nextFeature)
        {
            CancelPreparedState(); SetFrame(next, nextFeature);
            if (!next.Enabled || !next.PositiveRenderingEnabled) return;
            try
            {
                if (!Validate(out string error)) { preparationError = error; return; }
                presentationTime = next.PresentationTime >= 0 ? next.PresentationTime : Time.unscaledTimeAsDouble;
                fogHistory.Update(next, presentationTime);
                if (!next.TruthMode)
                {
                    // 原Shader克隆只借用独立队列，不改变其Caster；真正颜色随后用独立Sight材质与URP PerObjectData绘制。
                    materialBuckets.Prepare(next, ShaderFor);
                    propertyScope.Apply(next);
                }
                preparedFrame = Time.frameCount; preparedBeforeCulling = true; preparationError = "";
            }
            catch (Exception error)
            {
                CancelPreparedState(); preparationError = "剔除前源材质登记失败：" + error.Message;
            }
        }

        void CancelPreparedState()
        {
            propertyScope.Restore(); materialBuckets.Restore();
            preparedBeforeCulling = false; preparedFrame = -1;
        }
        internal void EndScope() { CancelPreparedState(); }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var renderer = renderingData.cameraData.renderer;
            ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
            // 原世界和原世界深度不能继续参与许可绘制，否则隐藏物仍留下轮廓/遮挡。
            ConfigureClear(ClearFlag.All, Color.black);
            ConfigureDepthStoreAction(RenderBufferStoreAction.Store);
            ready = Validate(out string error);
            Statistics.Error = error;
            if (!preparedBeforeCulling || preparedFrame != Time.frameCount)
            { ready = false; Statistics.Error = string.IsNullOrEmpty(preparationError) ? "本帧未在剔除前准备源材质；保守清黑。" : preparationError; }
            if (!frame.TruthMode && renderingData.cameraData.postProcessEnabled)
            { ready = false; Statistics.Error = "当前有限世界后端只认证无后处理相机；拒绝旧CameraDepthTexture参与景深/深度后处理。"; }
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            Statistics.Width = descriptor.width; Statistics.Height = descriptor.height;
            Statistics.Msaa = descriptor.msaaSamples;
            Statistics.SurfaceDraws = Statistics.PortalDraws = Statistics.UnexploredVolumeDraws = 0;
            Statistics.SourceMaterialBucketCount = materialBuckets.Active.Count;
            Statistics.RetainedMaterialConflicts = materialBuckets.RetainedConflictCount;
            Statistics.OwnedMaterialCount = materials.Count + materialBuckets.OwnedMaterialCount;
            Statistics.OwnedPropertyBlockCount = propertyScope.BlockCount;
            Statistics.OwnedBufferCount = fogHistory.Buffer == null ? 0 : 1; Statistics.EstimatedBufferBytes = fogHistory.Bytes;
            Statistics.OwnedTextureCount = Statistics.OwnedMeshCount = 0;
            Statistics.EstimatedTextureBytes = 0;
            previewEnabled = ready && frame.ExteriorView && frame.PreviewRoofSurfaces != null && frame.PreviewRoofSurfaces.Length > 0;
            if (previewEnabled)
            {
                descriptor.depthBufferBits = 0; descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm; descriptor.bindMS = false;
                descriptor.enableRandomWrite = false; descriptor.useMipMap = descriptor.autoGenerateMips = false;
                RenderingUtils.ReAllocateIfNeeded(ref previewCoverage, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "RoomVisibility legal roof-preview coverage");
                var depthDescriptor = descriptor;
                depthDescriptor.graphicsFormat = GraphicsFormat.None; depthDescriptor.depthBufferBits = 24;
                RenderingUtils.ReAllocateIfNeeded(ref previewDepth, depthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "RoomVisibility legal roof-preview depth");
                if (previewCoverage == null || previewCoverage.rt == null || previewCoverage.rt.graphicsFormat == GraphicsFormat.None ||
                    previewDepth == null || previewDepth.rt == null || previewDepth.rt.depthStencilFormat == GraphicsFormat.None ||
                    previewDepth.rt.antiAliasing != previewCoverage.rt.antiAliasing)
                {
                    ready = false; Statistics.Error = "预览颜色/独立深度附件无效或MSAA不一致；拒绝以深度纹理冒充颜色许可。";
                }
            }
            if (previewCoverage != null && previewCoverage.rt != null)
            {
                Statistics.OwnedTextureCount = (previewCoverage != null ? 1 : 0) + (previewDepth != null ? 1 : 0);
                int samples = Mathf.Max(1, previewCoverage.rt.antiAliasing);
                int depthBytes = previewDepth != null && previewDepth.rt != null && previewDepth.rt.depthStencilFormat == GraphicsFormat.D32_SFloat_S8_UInt ? 8 : 4;
                Statistics.EstimatedTextureBytes = previewCoverage.rt.width * (long)previewCoverage.rt.height * ((4 + depthBytes) * samples + (samples > 1 ? 4 : 0));
            }
            if (ready && !frame.TruthMode)
            {
                descriptor.depthBufferBits = 0; descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm; descriptor.bindMS = false;
                int actualSamples = renderer.cameraDepthTargetHandle.rt != null ? renderer.cameraDepthTargetHandle.rt.antiAliasing : descriptor.msaaSamples;
                descriptor.msaaSamples = Mathf.Max(1, actualSamples);
                RenderingUtils.ReAllocateIfNeeded(ref labelPermission, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "RoomVisibility final world-label permission");
                if (labelPermission == null || labelPermission.rt == null || labelPermission.rt.graphicsFormat == GraphicsFormat.None || labelPermission.rt.antiAliasing != descriptor.msaaSamples)
                { ready = false; Statistics.Error = "最终标签许可颜色附件与真实受限深度不匹配。"; }
            }
            if (labelPermission != null && labelPermission.rt != null)
            {
                ++Statistics.OwnedTextureCount; int samples = Mathf.Max(1, labelPermission.rt.antiAliasing);
                Statistics.EstimatedTextureBytes += labelPermission.rt.width * (long)labelPermission.rt.height * 4 * (samples + (samples > 1 ? 1 : 0));
            }
        }

        bool Validate(out string error)
        {
            error = "";
            if (frame == null || !frame.PositiveRenderingEnabled || !frame.Enabled)
            { error = "缺少正向许可帧。"; return false; }
            if (truthShader == null || !truthShader.isSupported)
            { error = "缺少可运行PositiveTruth Shader显式引用。"; return false; }
            if (!CopyVolumes(frame.SeedVolumes, seedMatrices, seedExtents) ||
                !CopyVolumes(frame.BuildingVolumes, buildingMatrices, buildingExtents) ||
                !CopyVolumes(frame.OpaqueBlockers, blockerMatrices, blockerExtents) ||
                !CopyVolumes(frame.CameraOpaqueBlockers, cameraBlockerMatrices, cameraBlockerExtents))
            { error = "种子/建筑/不透明阻挡盒无效或超出8/32/64预算。"; return false; }
            var domains = frame.SightDomains;
            if (domains == null || domains.Length > 8)
            { error = "门户视域无效或超过8个；未静默截断。"; return false; }
            int planeOffset = 0, targetOffset = 0;
            for (int i = 0; i < domains.Length; ++i)
            {
                var domain = domains[i];
                if (domain == null || domain.Planes == null || domain.Planes.Length < 4 || domain.Planes.Length > 10 ||
                    domain.TargetVolumes == null || domain.TargetVolumes.Length > 8 ||
                    (!domain.TargetExterior && domain.TargetVolumes.Length == 0))
                { error = "门户必须有4—10个有效半空间及明确目标空间。"; return false; }
                for (int p = 0; p < domain.Planes.Length; ++p)
                {
                    Vector4 plane = domain.Planes[p];
                    if (!Finite(plane) || new Vector3(plane.x, plane.y, plane.z).sqrMagnitude < .000001f)
                    { error = "门户平面非法；拒绝渲染。"; return false; }
                    planes[planeOffset + p] = plane / new Vector3(plane.x, plane.y, plane.z).magnitude;
                }
                for (int v = 0; v < domain.TargetVolumes.Length; ++v)
                {
                    var volume = domain.TargetVolumes[v];
                    if (!ValidVolume(volume)) { error = "门户目标盒无效。"; return false; }
                    targetMatrices[targetOffset + v] = volume.WorldToLocal;
                    targetExtents[targetOffset + v] = volume.HalfExtents;
                }
                meta[i] = new Vector4(planeOffset, domain.Planes.Length, targetOffset, domain.TargetVolumes.Length);
                exterior[i] = new Vector4(domain.TargetExterior ? 1 : 0, 0, 0, 0);
                planeOffset += domain.Planes.Length; targetOffset += domain.TargetVolumes.Length;
            }
            if (frame.WindowSightDomains == null || frame.WindowSightDomains.Length > 8 || !Finite(frame.CameraObserverPosition))
            { error = "相机窗景超过8个或观察点无效。"; return false; }
            int windowOffset = 0;
            for (int i=0;i<frame.WindowSightDomains.Length;++i)
            {
                var domain=frame.WindowSightDomains[i];
                if(domain==null||!domain.IsCameraWindow||!domain.TargetExterior||domain.Planes==null||domain.Planes.Length<4||domain.Planes.Length>10||
                    domain.TargetVolumes==null||domain.TargetVolumes.Length!=0)
                { error="相机窗景只接受4—10平面且目标仅室外的明确窗孔。";return false; }
                for(int p=0;p<domain.Planes.Length;++p)
                {
                    var plane=domain.Planes[p];float length=new Vector3(plane.x,plane.y,plane.z).magnitude;
                    if(!Finite(plane)||length<.000001f){error="相机窗孔平面无效。";return false;}
                    windowPlanes[windowOffset+p]=plane/length;
                }
                windowMeta[i]=new Vector4(windowOffset,domain.Planes.Length,0,0);windowOffset+=domain.Planes.Length;
            }
            var fog=frame.Fog;
            if(fog!=null&&fog.Enabled&&(!Finite(new Vector4(fog.InitialVisibility,fog.GainSeconds,fog.LossSeconds,fog.SpreadSeconds))||
               !Finite(new Vector4(fog.SpreadRadius,fog.InwardFeather,fog.DitherCellSize,fog.DitherStrength))||
               fog.InitialVisibility<0||fog.InitialVisibility>1||fog.GainSeconds<=0||fog.GainSeconds>.5f||fog.LossSeconds<=0||fog.LossSeconds>.35f||
               fog.SpreadSeconds<0||fog.SpreadSeconds>=Mathf.Min(fog.GainSeconds,fog.LossSeconds)||fog.SpreadRadius<=0||fog.InwardFeather<0||
               fog.DitherCellSize<=0||fog.DitherStrength<0||fog.DitherStrength>1))
            {error="黑幕时序/柔边配置非法或超出四快照的0.5秒预算。";return false;}
            if (frame.PositiveSurfaces == null || frame.PositiveSurfaces.Length > 4096)
            { error = "显式表面列表为空或超过4096预算。"; return false; }
            foreach (var surface in frame.PositiveSurfaces)
            {
                if (surface.OwnerVolumes != null && !CopyVolumes(surface.OwnerVolumes, ownerMatrices, ownerExtents))
                { error = "所属空间必须为有效的1—8个盒体；不截断或按物件中心冒充整件许可。"; return false; }
                if (surface.Renderer == null) continue;
                if (surface.Renderer.sharedMaterials.Length > 16)
                { error = "单表面超过16材质槽预算；拒绝无界适配。"; return false; }
                if (frame.TruthMode || surface.Kind == RoomPositiveSurfaceKind.Full) continue;
                uint family = surface.Kind == RoomPositiveSurfaceKind.Roof ? RoomPositiveRenderingLayers.RoofBit : RoomPositiveRenderingLayers.Classify(surface.Renderer);
                if (family == 0) continue; // 绘制阶段统计并保守省略不支持源，不授予其它内容权限。
                if (RoomPositiveRenderingLayers.RequiresIndependentDraw(surface) && RoomPositiveRenderingLayers.IsIndependentLitFamily(family) &&
                    (surface.DrawMaterial == null || surface.DrawMask == 0 || surface.DrawMask == RoomPositiveRenderingLayers.IndependentBit))
                { error = "动态Lit缺少剔除前的独立材质筛选组；拒绝借用角色共享材质。"; return false; }
                if (surface.Kind == RoomPositiveSurfaceKind.Roof && RoomPositiveRenderingLayers.Classify(surface.Renderer) != RoomPositiveRenderingLayers.PartialLitBit)
                { error = "当前局部屋顶像素路径只支持原URP Lit顶盖。"; return false; }
                Shader shader = ShaderFor(family, out int unused);
                if (shader == null || !shader.isSupported)
                { error = "缺少可运行的正向源家族Shader引用：" + family; return false; }
            }
            return true;
        }

        Shader ShaderFor(uint family, out int pass)
        {
            pass = 0;
            if (family == RoomPositiveRenderingLayers.PartialLitBit || family == RoomPositiveRenderingLayers.RoofBit) return feature.positiveLitShader;
            if (family == RoomPositiveRenderingLayers.PartialSimpleLitBit) return feature.positiveSimpleLitShader;
            if (family == RoomPositiveRenderingLayers.PartialUnlitBit) return feature.positiveUnlitShader;
            if (family == RoomPositiveRenderingLayers.PartialThinTopBit) return feature.positiveThinTopShader;
            if (family == RoomPositiveRenderingLayers.PartialWorldTileBit) return feature.positiveWorldTileShader;
            if (family == RoomPositiveRenderingLayers.PartialDecorationBit) return feature.positiveDecorationShader;
            if (family == RoomPositiveRenderingLayers.PartialDepthPlaneBit) return feature.positiveDepthPlaneShader;
            if (family == RoomPositiveRenderingLayers.PartialMapDepthBit) { pass = 1; return feature.positiveDepthPlaneShader; }
            if (family == RoomPositiveRenderingLayers.PartialSpineBit) return feature.positiveSpineShader;
            if (family == RoomPositiveRenderingLayers.PartialSkeletonBit) { pass = 1; return feature.positiveSpineShader; }
            if (family == RoomPositiveRenderingLayers.PartialSpriteBit) return feature.positiveSpriteShader;
            if (family == RoomPositiveRenderingLayers.PartialDefaultSpriteBit) { pass = 1; return feature.positiveSpriteShader; }
            if (family == RoomPositiveRenderingLayers.PartialLitSpineBit) return feature.positiveSpriteLitShader;
            return null;
        }

        static bool Finite(Vector4 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.z) && !float.IsNaN(value.w) && !float.IsInfinity(value.w);

        static bool ValidVolume(RoomMaskVolume value)
        {
            if (!Finite(value.HalfExtents) || value.HalfExtents.x <= 0 || value.HalfExtents.y <= 0 || value.HalfExtents.z <= 0) return false;
            for (int row = 0; row < 4; ++row) if (!Finite(value.WorldToLocal.GetRow(row))) return false;
            return Mathf.Abs(value.WorldToLocal.determinant) > 1e-9f;
        }

        static bool CopyVolumes(RoomMaskVolume[] values, Matrix4x4[] matrices, Vector4[] extents)
        {
            if (values == null || values.Length > matrices.Length) return false;
            for (int i = 0; i < values.Length; ++i)
            {
                if (!ValidVolume(values[i])) return false;
                matrices[i] = values[i].WorldToLocal; extents[i] = values[i].HalfExtents;
            }
            return true;
        }

        void Upload(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cmd.SetGlobalMatrix("_RoomSightInverseViewProjection", (renderingData.cameraData.GetGPUProjectionMatrix() * renderingData.cameraData.GetViewMatrix()).inverse);
            cmd.SetGlobalVector("_RoomSightTargetSize", new Vector4(Statistics.Width, Statistics.Height, 1f / Statistics.Width, 1f / Statistics.Height));
            cmd.SetGlobalVector("_RoomSightObserver", frame.ObserverPosition);
            cmd.SetGlobalVector("_RoomSightCameraObserver", frame.CameraObserverPosition);
            cmd.SetGlobalInteger("_RoomSightCameraBlockerCount", frame.CameraOpaqueBlockers.Length);
            cmd.SetGlobalMatrixArray("_RoomSightCameraBlockerMatrices", cameraBlockerMatrices);
            cmd.SetGlobalVectorArray("_RoomSightCameraBlockerExtents", cameraBlockerExtents);
            cmd.SetGlobalInteger("_RoomSightWindowCount", frame.WindowSightDomains.Length);
            cmd.SetGlobalVectorArray("_RoomSightWindowPlanes", windowPlanes);cmd.SetGlobalVectorArray("_RoomSightWindowMeta", windowMeta);
            cmd.SetGlobalInteger("_RoomSightPassMode", 0);
            var fog=frame.Fog??new RoomFogSettings{Enabled=false};
            cmd.SetGlobalVector("_RoomFogTiming",new Vector4(fog.InitialVisibility,fog.GainSeconds,fog.LossSeconds,fog.SpreadSeconds));
            cmd.SetGlobalVector("_RoomFogStyle",new Vector4(fog.Enabled?1:0,fog.InwardFeather,fog.SpreadRadius,0));
            cmd.SetGlobalVector("_RoomFogDither",new Vector4(fog.DitherEnabled?1:0,fog.DitherCellSize,fog.DitherStrength,0));
            cmd.SetGlobalColor("_RoomFogColor",fog.Color);
            cmd.SetGlobalFloat("_RoomFogNow",fogHistory.Time(presentationTime));
            cmd.SetGlobalInteger("_RoomFogHistoryCount",fogHistory.Count);
            if(fogHistory.Buffer!=null)cmd.SetGlobalBuffer("_RoomFogHistory",fogHistory.Buffer);
            cmd.SetGlobalInteger("_RoomSightExteriorView", frame.ExteriorView ? 1 : 0);
            cmd.SetGlobalInteger("_RoomSightSeedCount", frame.SeedVolumes.Length);
            cmd.SetGlobalMatrixArray("_RoomSightSeedMatrices", seedMatrices); cmd.SetGlobalVectorArray("_RoomSightSeedExtents", seedExtents);
            cmd.SetGlobalInteger("_RoomSightBuildingCount", frame.BuildingVolumes.Length);
            cmd.SetGlobalMatrixArray("_RoomSightBuildingMatrices", buildingMatrices); cmd.SetGlobalVectorArray("_RoomSightBuildingExtents", buildingExtents);
            cmd.SetGlobalInteger("_RoomSightBlockerCount", frame.OpaqueBlockers.Length);
            cmd.SetGlobalMatrixArray("_RoomSightBlockerMatrices", blockerMatrices); cmd.SetGlobalVectorArray("_RoomSightBlockerExtents", blockerExtents);
            cmd.SetGlobalInteger("_RoomSightDomainCount", frame.SightDomains.Length);
            cmd.SetGlobalVectorArray("_RoomSightMeta", meta); cmd.SetGlobalVectorArray("_RoomSightExterior", exterior);
            cmd.SetGlobalVectorArray("_RoomSightPlanes", planes);
            cmd.SetGlobalMatrixArray("_RoomSightTargetMatrices", targetMatrices); cmd.SetGlobalVectorArray("_RoomSightTargetExtents", targetExtents);
            // 真值期世界标签保守关闭；不得采样上一帧旧双黑幕而误认为最终许可像素。
            cmd.SetGlobalFloat("_RoomWorldLabelMaskReady", 0);
            cmd.SetGlobalFloat("_RoomVisibilityMaskEnabled", 0);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera != camera) return;
            var cmd = CommandBufferPool.Get("RoomVisibility qualified world");
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            bool complete = false;
            try
            {
                var renderer = renderingData.cameraData.renderer;
                CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, ClearFlag.All, Color.black);
                cmd.SetGlobalFloat("_RoomWorldLabelMaskReady", 0);
                if (ready && !frame.TruthMode && !ValidateIndependentLitSources(out string independentError))
                { ready = false; Statistics.Error = independentError; }
                if (ready)
                {
                    Upload(cmd, ref renderingData); used.Clear(); Statistics.UnsupportedMaterialCount = 0;
                    cmd.SetGlobalFloat("_RoomPositivePermissionMode", 0);
                    if (previewEnabled)
                    {
                        CoreUtils.SetRenderTarget(cmd, previewCoverage, previewDepth, ClearFlag.All, Color.black);
                        cmd.SetGlobalFloat("_RoomPositiveCoverageMode", 1);
                        foreach (var surface in frame.PositiveSurfaces)
                            if (surface.Kind != RoomPositiveSurfaceKind.Roof) DrawTruth(cmd, surface);
                        if (previewCoverage.rt.antiAliasing > 1) cmd.ResolveAntiAliasedSurface(previewCoverage.rt);
                        cmd.SetGlobalTexture("_RoomSightCoverageTexture", previewCoverage.nameID);
                    }
                    CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, ClearFlag.All, Color.black);
                    cmd.SetGlobalFloat("_RoomPositiveCoverageMode", 0);
                    if (frame.TruthMode)
                    {
                        cmd.SetGlobalInteger("_RoomSightPassMode", 2);
                        foreach (var surface in frame.PositiveSurfaces) DrawTruth(cmd, surface);
                    }
                    else
                    {
                        // 使用原角色所有begin回调之后的最新材质脚点，刷新自有颜色材质；不再使用overrideShader。
                        materialBuckets.RefreshAdapters();
                        Statistics.OwnedPropertyBlockCount = propertyScope.BlockCount;
                        cmd.SetGlobalInteger("_RoomSightOwnerCount", 0); cmd.SetGlobalInteger("_RoomSightOwnerExterior", 0);
                        cmd.SetGlobalInteger("_RoomSightPassMode", 1);
                        context.ExecuteCommandBuffer(cmd); cmd.Clear();
                        DrawFinalWorld(context, cmd, ref renderingData);
                        // 标签许可只消费刚刚重建的合法世界深度，绝不复用旧世界遮挡。
                        CoreUtils.SetRenderTarget(cmd, labelPermission, renderer.cameraDepthTargetHandle, ClearFlag.Color, Color.black);
                        cmd.SetGlobalInteger("_RoomSightPassMode", 0);
                        cmd.SetGlobalFloat("_RoomPositivePermissionMode", 1);
                        foreach (var surface in frame.PositiveSurfaces) DrawTruth(cmd, surface, true);
                        if (labelPermission.rt.antiAliasing > 1) cmd.ResolveAntiAliasedSurface(labelPermission.rt);
                        CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
                        cmd.SetGlobalFloat("_RoomPositivePermissionMode", 0);
                        cmd.SetGlobalTexture("_RoomVisibilityPermission", labelPermission.nameID);
                        cmd.SetGlobalFloat("_RoomWorldLabelMaskReady", 1);
                    }
                    stale.Clear(); foreach (var pair in materials) if (pair.Key.Source == null || !used.Contains(pair.Key)) stale.Add(pair.Key);
                    foreach (var key in stale) { DestroyOwned(materials[key]); materials.Remove(key); }
                    Statistics.OwnedMaterialCount = materials.Count + materialBuckets.OwnedMaterialCount;
                    Statistics.Active = true;
                    if (Statistics.UnsupportedMaterialCount > 0)
                        Statistics.Error = "存在未支持的源材质，已保守不画；本轮渲染验收不完整。";
                }
                context.ExecuteCommandBuffer(cmd); complete = true;
            }
            finally
            {
                if (!complete) CancelPreparedState();
                CommandBufferPool.Release(cmd);
                Statistics.CommandBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            }
        }

        bool ValidateIndependentLitSources(out string error)
        {
            error = "";
            foreach (var surface in frame.PositiveSurfaces)
            {
                if (surface.Kind == RoomPositiveSurfaceKind.Full || !RoomPositiveRenderingLayers.RequiresIndependentDraw(surface) ||
                    surface.DrawMask == RoomPositiveRenderingLayers.IndependentBit) continue;
                var renderer = surface.Renderer;
                if (renderer == null) continue;
                renderer.GetSharedMaterials(slots);
                if (surface.DrawMaterial == null || slots.Count == 0)
                { error = "动态Lit的原源材质已失效；保守清黑：" + renderer.name; return false; }
                foreach (var material in slots)
                    if (!ReferenceEquals(material, surface.DrawMaterial))
                    { error = "原角色回调改变了动态Lit材质身份；等待下一次合法分组：" + renderer.name; return false; }
            }
            return true;
        }

        void DrawFinalWorld(ScriptableRenderContext context, CommandBuffer cmd, ref RenderingData renderingData)
        {
            bool full = false;
            foreach (var surface in frame.PositiveSurfaces)
            {
                var source = surface.Renderer;
                if (source == null || !source.enabled || source.forceRenderingOff || !source.gameObject.activeInHierarchy) continue;
                if (RoomPositiveRenderingLayers.Classify(source) == 0) { ++Statistics.UnsupportedMaterialCount; continue; }
                if (surface.Kind == RoomPositiveSurfaceKind.Full) full = true;
            }
            for (int queue = 0; queue < 2; ++queue)
            {
                bool transparent = queue != 0;
                if (full)
                    DrawSourceGroup(context, ref renderingData, RoomPositiveRenderingLayers.FullBit, null, 0, transparent, -1);
                foreach (var bucket in materialBuckets.Active)
                    if (bucket.Transparent == transparent)
                        DrawSourceGroup(context, ref renderingData, bucket.Family, bucket.Adapter, bucket.Pass, transparent, bucket.Queue);
                // 动态Lit仅借独占筛选位，不换sharedMaterials，仍由URP填写灯光/探针逐对象数据。
                independentLitGroups.Clear();
                foreach (var surface in frame.PositiveSurfaces)
                {
                    if (surface.Kind == RoomPositiveSurfaceKind.Full || surface.DrawMaterial == null ||
                        !RoomPositiveRenderingLayers.RequiresIndependentDraw(surface) || !independentLitGroups.Add(surface.DrawMask)) continue;
                    var source = surface.DrawMaterial;
                    if (IsTransparent(source) != transparent) continue;
                    uint family = RoomPositiveRenderingLayers.Classify(source);
                    var adapter = ShadedAdapter(source, surface.Kind, family, out int pass);
                    if (adapter != null) DrawSourceGroup(context, ref renderingData, surface.DrawMask, adapter, pass, transparent, -1, true);
                }
                DrawIndependentUnlit(cmd, transparent);
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
            }
        }

        static bool IsTransparent(Material source) => (source.renderQueue < 0 ? source.shader.renderQueue : source.renderQueue) > 2500;

        Material ShadedAdapter(Material source, RoomPositiveSurfaceKind kind, uint family, out int pass)
        {
            var shader = ShaderFor(family, out pass);
            if (source == null || shader == null || !shader.isSupported) { ++Statistics.UnsupportedMaterialCount; return null; }
            var key = new MaterialKey(source, kind, false, false, true);
            if (!materials.TryGetValue(key, out var adapter))
            {
                adapter = new Material(shader) { name = source.name + " [Room read-only source adapter]", hideFlags = HideFlags.HideAndDontSave };
                materials.Add(key, adapter);
            }
            else if (adapter.shader != shader) adapter.shader = shader;
            adapter.CopyPropertiesFromMaterial(source); used.Add(key); return adapter;
        }

        void UploadOwner(CommandBuffer cmd, RoomPositiveSurface surface)
        {
            int count = surface.OwnerVolumes == null ? 0 : surface.OwnerVolumes.Length;
            cmd.SetGlobalInteger("_RoomSightOwnerCount", count);
            cmd.SetGlobalInteger("_RoomSightOwnerExterior", surface.OwnerExterior ? 1 : 0);
            cmd.SetGlobalFloat("_RoomSightStaticEnvironment",surface.StaticEnvironment?1:0);
            if (count > 0)
            {
                CopyVolumes(surface.OwnerVolumes, ownerMatrices, ownerExtents);
                cmd.SetGlobalMatrixArray("_RoomSightOwnerMatrices", ownerMatrices); cmd.SetGlobalVectorArray("_RoomSightOwnerExtents", ownerExtents);
            }
        }

        void DrawIndependentUnlit(CommandBuffer cmd, bool transparent)
        {
            independentItems.Clear();
            foreach (var surface in frame.PositiveSurfaces)
            {
                var renderer = surface.Renderer;
                if (surface.Kind == RoomPositiveSurfaceKind.Full || surface.DrawMask != RoomPositiveRenderingLayers.IndependentBit ||
                    renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
                renderer.GetSharedMaterials(slots);
                for (int slot = 0; slot < slots.Count; ++slot)
                {
                    var source = slots[slot]; uint family = RoomPositiveRenderingLayers.Classify(source);
                    if (family == 0 || RoomPositiveRenderingLayers.IsIndependentLitFamily(family)) { ++Statistics.UnsupportedMaterialCount; continue; }
                    if (IsTransparent(source) != transparent) continue;
                    Vector3 center = renderer.bounds.center;
                    float distance = camera.transparencySortMode == TransparencySortMode.CustomAxis ? Vector3.Dot(center, camera.transparencySortAxis) :
                        camera.orthographic || camera.transparencySortMode == TransparencySortMode.Orthographic ? Vector3.Dot(center - camera.transform.position, camera.transform.forward) : (center - camera.transform.position).sqrMagnitude;
                    independentItems.Add(new IndependentItem { Surface = surface, Source = source, Slot = slot, Queue = source.renderQueue < 0 ? source.shader.renderQueue : source.renderQueue,
                        Layer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), Order = renderer.sortingOrder, Distance = distance });
                }
            }
            independentItems.Sort((a, b) =>
            {
                int order = a.Layer.CompareTo(b.Layer); if (order == 0) order = a.Order.CompareTo(b.Order);
                if (order == 0) order = a.Queue.CompareTo(b.Queue);
                return order != 0 ? order : transparent ? b.Distance.CompareTo(a.Distance) : a.Distance.CompareTo(b.Distance);
            });
            foreach (var item in independentItems)
            {
                uint family = RoomPositiveRenderingLayers.Classify(item.Source);
                var adapter = ShadedAdapter(item.Source, item.Surface.Kind, family, out int pass);
                if (adapter == null) continue;
                UploadOwner(cmd, item.Surface);
                cmd.SetGlobalFloat("_RoomSightRoofMode", 0); cmd.SetGlobalFloat("_RoomSightCutRoof", 0);
                cmd.DrawRenderer(item.Surface.Renderer, adapter, item.Slot, pass); ++Statistics.SurfaceDraws;
            }
        }

        void DrawSourceGroup(ScriptableRenderContext context, ref RenderingData renderingData, uint mask, Material adapter, int pass, bool transparent, int exactQueue, bool independentLit = false)
        {
            var sorting = transparent ? SortingCriteria.CommonTransparent : renderingData.cameraData.defaultOpaqueSortFlags;
            var drawing = CreateDrawingSettings(independentLit ? independentLitTags : sourceTags, ref renderingData, sorting);
            if (adapter != null)
            {
                // overrideMaterial持有与实际Shader一致的属性/采样器布局，避免本机overrideShader不安全路径。
                drawing.overrideMaterial = adapter; drawing.overrideMaterialPassIndex = pass;
                drawing.enableDynamicBatching = false; drawing.enableInstancing = false;
            }
            var range = exactQueue < 0 ? (transparent ? RenderQueueRange.transparent : RenderQueueRange.opaque) :
                new RenderQueueRange { lowerBound = exactQueue, upperBound = exactQueue };
            var filtering = new FilteringSettings(range, camera.cullingMask, mask);
            // CullingResults只在剔除前读取独立源队列；URP仍提供原mainLightIndex及perObjectData。
            context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            CancelPreparedState();
            if (cmd != null) { cmd.SetGlobalFloat("_RoomPositiveCoverageMode", 0); cmd.SetGlobalFloat("_RoomPositivePermissionMode", 0);cmd.SetGlobalInteger("_RoomSightPassMode",0); }
        }

        void DrawTruth(CommandBuffer cmd, RoomPositiveSurface surface, bool permission = false)
        {
            var renderer = surface.Renderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) return;
            if (RoomPositiveRenderingLayers.Classify(renderer) == 0) { ++Statistics.UnsupportedMaterialCount; return; }
            int ownerCount = surface.OwnerVolumes == null ? 0 : surface.OwnerVolumes.Length;
            cmd.SetGlobalInteger("_RoomSightOwnerCount", ownerCount);
            cmd.SetGlobalInteger("_RoomSightOwnerExterior", surface.OwnerExterior ? 1 : 0);
            if (ownerCount > 0)
            {
                CopyVolumes(surface.OwnerVolumes, ownerMatrices, ownerExtents);
                cmd.SetGlobalMatrixArray("_RoomSightOwnerMatrices", ownerMatrices); cmd.SetGlobalVectorArray("_RoomSightOwnerExtents", ownerExtents);
            }
            renderer.GetSharedMaterials(slots);
            for (int slot = 0; slot < slots.Count; ++slot)
            {
                var source = materialBuckets.ResolveOriginal(slots[slot]); uint family = RoomPositiveRenderingLayers.Classify(source);
                bool cutRoof = previewEnabled && surface.Kind == RoomPositiveSurfaceKind.Roof && Array.IndexOf(frame.PreviewRoofSurfaces, renderer) >= 0;
                var key = new MaterialKey(source, surface.Kind, cutRoof, permission);
                if (!materials.TryGetValue(key, out var adapter))
                {
                    Shader adapterShader = family == RoomPositiveRenderingLayers.PartialLitSpineBit ? spriteLitTruthShader : truthShader;
                    if (adapterShader == null || !adapterShader.isSupported) { ++Statistics.UnsupportedMaterialCount; continue; }
                    adapter = new Material(adapterShader) { name = source.name + " [Room positive truth]", hideFlags = HideFlags.HideAndDontSave };
                    materials.Add(key, adapter);
                }
                used.Add(key); adapter.CopyPropertiesFromMaterial(source);
                bool upright = source.shader.name.StartsWith("Ossastra/Characters/Upright", StringComparison.Ordinal);
                if (!upright && adapter.HasProperty("_VisualDepthEnabled")) adapter.SetFloat("_VisualDepthEnabled", 0);
                int mode = family == RoomPositiveRenderingLayers.PartialThinTopBit ? 1 :
                    family == RoomPositiveRenderingLayers.PartialDecorationBit ? 2 :
                    (family == RoomPositiveRenderingLayers.PartialDepthPlaneBit || family == RoomPositiveRenderingLayers.PartialMapDepthBit) ? 3 :
                    (family == RoomPositiveRenderingLayers.PartialSpineBit || family == RoomPositiveRenderingLayers.PartialSkeletonBit) ? 4 :
                    (family == RoomPositiveRenderingLayers.PartialSpriteBit || family == RoomPositiveRenderingLayers.PartialDefaultSpriteBit) ? 5 :
                    family == RoomPositiveRenderingLayers.PartialWorldTileBit ? 6 : 0;
                if (family != RoomPositiveRenderingLayers.PartialLitSpineBit)
                {
                    adapter.SetFloat("_RoomSurfaceMode", mode);
                    adapter.SetFloat("_RoomSourceAlphaClip", source.IsKeywordEnabled("_ALPHATEST_ON") ? 1 : 0);
                }
                adapter.SetFloat("_RoomPositiveDepthWrite", key.Permission ? 0 : 1);
                adapter.SetFloat("_RoomPositivePartial", surface.Kind == RoomPositiveSurfaceKind.Partial ? 1 : 0);
                adapter.SetFloat("_RoomPositiveCutRoof", key.CutRoof ? 1 : 0);
                adapter.SetColor("_RoomPositiveColor", surface.Kind == RoomPositiveSurfaceKind.Partial ? new Color(1, .38f, .04f, 1) :
                    surface.Kind == RoomPositiveSurfaceKind.Roof ? new Color(.2f, .35f, .8f, 1) : new Color(.05f, .9f, .85f, 1));
                adapter.SetFloat("_Cull", mode == 0 && source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 0);
                bool hardware = mode == 0 || mode == 2 || mode == 6 || (mode == 3 && !upright);
                cmd.DrawRenderer(renderer, adapter, slot, family == RoomPositiveRenderingLayers.PartialLitSpineBit ? 0 : mode == 5 ? 1 : hardware ? 3 : 0);
                ++Statistics.SurfaceDraws;
            }
        }

        void ClearMaterials() { foreach (var value in materials.Values) DestroyOwned(value); materials.Clear(); }
        static void DestroyOwned(Object value)
        { if (value != null) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); } }
        public void Dispose()
        {
            CancelPreparedState(); ClearMaterials(); propertyScope.Clear(); materialBuckets.Dispose(); fogHistory.Dispose(); Statistics.OwnedPropertyBlockCount = Statistics.OwnedBufferCount = 0; Statistics.EstimatedBufferBytes = 0; Statistics.Active = false;
            independentItems.Clear(); independentLitGroups.Clear(); slots.Clear(); used.Clear(); stale.Clear();
            Statistics.OwnedMaterialCount = materialBuckets.OwnedMaterialCount;
            Statistics.SourceMaterialBucketCount = 0; Statistics.RetainedMaterialConflicts = materialBuckets.RetainedConflictCount;
            previewCoverage?.Release(); previewCoverage = null;
            previewDepth?.Release(); previewDepth = null;
            labelPermission?.Release(); labelPermission = null;
            Statistics.OwnedTextureCount = Statistics.OwnedMeshCount = 0;
        }
    }
}
