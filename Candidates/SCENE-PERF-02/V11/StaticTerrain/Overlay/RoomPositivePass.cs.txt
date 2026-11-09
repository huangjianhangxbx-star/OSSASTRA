/* 功能：清除旧世界颜色/深度后，只绘制当前帧登记的合法表面；纯色路径先验证空间真值。
 * 边界：不生产房间/门户、不修改原材质、物理或相机；只向环境表现发布当帧合法深度与环境许可。
 * 生命周期：本Pass持有核心世界资源；角色覆盖/外缘由独立阶段持有与局部降级，核心超预算保守阻断。
 * 观测：有限ProfilingSampler只标记现有完整阶段；CPU范围可包含原生调用/等待，并不等于纯C#有效工作。
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
        // 不跨CommandBuffer.Clear放置GPU范围；null命令缓冲范围只记录调用侧CPU，不增提交或改变绘制顺序。
        internal static readonly ProfilingSampler perfPrepareBeforeCulling = new ProfilingSampler("RoomPositive.PrepareBeforeCulling");
        internal static readonly ProfilingSampler perfBucketsPrepare = new ProfilingSampler("RoomPositive.Buckets.Prepare");
        internal static readonly ProfilingSampler perfPropertyScopeApply = new ProfilingSampler("RoomPositive.PropertyScope.Apply");
        internal static readonly ProfilingSampler perfRefreshAdapters = new ProfilingSampler("RoomPositive.RefreshAdapters");
        internal static readonly ProfilingSampler perfCharacterCoverage = new ProfilingSampler("RoomPositive.Character.PrepareCoverage");
        internal static readonly ProfilingSampler perfDrawFinalWorld = new ProfilingSampler("RoomPositive.DrawFinalWorld");
        internal static readonly ProfilingSampler perfPermissionBuild = new ProfilingSampler("RoomPositive.Permission.Build");
        internal static readonly ProfilingSampler perfPreviewCoverage = new ProfilingSampler("RoomPositive.PreviewCoverage.Build");
        internal static readonly ProfilingSampler perfCharacterOuter = new ProfilingSampler("RoomPositive.Character.AppendOuter");
        internal static readonly ProfilingSampler perfTruthMaterialCopy = new ProfilingSampler("RoomPositive.DrawTruth.MaterialCopy");
        readonly Camera camera;
        readonly Dictionary<Material,uint> validationFamilies=new Dictionary<Material,uint>();
        bool nativeTruthBucketsEnabled = true;
        int nativeTruthBucketCount, nativeTruthSlotCount;
        readonly Dictionary<Material, TruthNativeBucket> truthNativeByClone = new Dictionary<Material, TruthNativeBucket>();
        readonly List<TruthNativeBucket> truthNativeBuckets = new List<TruthNativeBucket>();
        readonly List<Material> truthNativeSlots = new List<Material>(16);
        bool truthNativeAvailable;
        sealed class TruthNativeBucket
        {
            internal RoomPositiveMaterialBuckets.Bucket Bucket;
            internal RoomPositiveSurface Representative;
            internal bool Seen, Rejected, Submitted;
            internal int Slots;
        }
        readonly Plane[] truthFrustumPlanes = new Plane[6];
        bool truthFrustumValid;
        int truthBoundsRejected;
        readonly Dictionary<MaterialKey, Material> materials = new Dictionary<MaterialKey, Material>();
        readonly Dictionary<MaterialKey, TruthCopyState> truthCopyStates = new Dictionary<MaterialKey, TruthCopyState>();
        struct TruthCopyState { internal int SourceCrc, AdapterCrc, DerivedMask, DerivedCrc; internal string[] Keywords; }
        static readonly int[] truthDerivedFloats = {
            Shader.PropertyToID("_VisualDepthEnabled"), Shader.PropertyToID("_RoomSurfaceMode"),
            Shader.PropertyToID("_RoomSourceAlphaClip"), Shader.PropertyToID("_RoomPositiveDepthWrite"),
            Shader.PropertyToID("_RoomPositivePartial"), Shader.PropertyToID("_RoomPositiveCutRoof"), Shader.PropertyToID("_Cull") };
        static readonly int truthColorId = Shader.PropertyToID("_RoomPositiveColor");
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
        readonly Ossastra.CharacterLighting.Rendering.CharacterLightingRenderStage characterStage;
        readonly RoomEnvironmentRenderStage environmentStage;
        static readonly List<ShaderTagId> sourceTags = new List<ShaderTagId>
        { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
        // 原角色覆盖辅助体会继承源渲染位，且只有SRPDefaultUnlit深度pass；动态Lit绝不选择它。
        static readonly List<ShaderTagId> independentLitTags = new List<ShaderTagId>
        { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };
        int preparedFrame = -1;
        bool preparedBeforeCulling;
        RoomStaticTerrainPreparation terrainPreparation;
        readonly HashSet<Material> validatedTerrainSources=new HashSet<Material>();
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
            internal readonly bool EnvironmentFull;
            readonly int id;
            internal MaterialKey(Material source, RoomPositiveSurfaceKind kind, bool cutRoof, bool permission, bool shaded = false, bool environmentFull = false)
            { Source = source; Kind = kind; CutRoof = cutRoof; Permission = permission; Shaded = shaded; EnvironmentFull=environmentFull; id = source.GetInstanceID(); }
            public bool Equals(MaterialKey other) => ReferenceEquals(Source, other.Source) && Kind == other.Kind && CutRoof == other.CutRoof && Permission == other.Permission && Shaded == other.Shaded && EnvironmentFull==other.EnvironmentFull;
            public override bool Equals(object value) => value is MaterialKey other && Equals(other);
            public override int GetHashCode() => id * 397 ^ (int)Kind * 16 ^ (CutRoof ? 8 : 0) ^ (Permission ? 4 : 0) ^ (Shaded ? 2 : 0) ^ (EnvironmentFull ? 1 : 0);
        }

        internal RoomPositivePass(Camera camera)
        { this.camera = camera; characterStage=new Ossastra.CharacterLighting.Rendering.CharacterLightingRenderStage(camera); environmentStage=new RoomEnvironmentRenderStage(camera); renderPassEvent = RenderPassEvent.AfterRenderingTransparents; }

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

        internal void PrepareBeforeCulling(RoomMaskFrame next, RoomMaskRendererFeature nextFeature, RoomStaticTerrainPreparation terrain = null)
        {
            using (new ProfilingScope(null, perfPrepareBeforeCulling))
            {
            CancelPreparedState(); SetFrame(next, nextFeature);
            if (!next.Enabled || !next.PositiveRenderingEnabled) return;
            terrainPreparation=terrain;
            try
            {
                using(new ProfilingScope(null,RoomStaticTerrainPreparation.perfCapturePrepare))
                    terrainPreparation?.CapturePrepare(camera,next);
                if (!Validate(out string error)) { preparationError = error; return; }
                // 环境工作集在接管前准备；环境能力不足只停环境受光，不撤销已验证的世界许可。
                environmentStage.PrepareSurface();
                presentationTime = next.PresentationTime >= 0 ? next.PresentationTime : Time.unscaledTimeAsDouble;
                fogHistory.Update(next, presentationTime);
                if (!next.TruthMode)
                {
                    // 原Shader克隆只借用独立队列，不改变其Caster；真正颜色随后用独立Sight材质与URP PerObjectData绘制。
                    using (new ProfilingScope(null, perfBucketsPrepare))
                        materialBuckets.PrepareTerrain(next, ShaderFor,terrainPreparation,true);
                    if(!PrepareKnownMaterialPasses(out string passError))
                    {CancelPreparedState();preparationError=passError;return;}
                    using (new ProfilingScope(null, perfPropertyScopeApply))
                        propertyScope.ApplyTerrain(next,terrainPreparation);
                }
                preparedFrame = Time.frameCount; preparedBeforeCulling = true; preparationError = "";
            }
            catch (Exception error)
            {
                CancelPreparedState(); preparationError = "剔除前源材质登记失败：" + error.Message;
            }
            finally { terrainPreparation?.EndPrepare(); }
            }
        }

        void CancelPreparedState()
        {
            terrainPreparation?.EndPrepare();terrainPreparation=null;
            propertyScope.Restore(); materialBuckets.Restore();
            preparedBeforeCulling = false; preparedFrame = -1;
        }
        internal void EndScope() { CancelPreparedState(); Ossastra.WorldRendering.Rendering.WorldLookLegalFrameRegistry.Invalidate(camera); }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Ossastra.WorldRendering.Rendering.WorldLookLegalFrameRegistry.Invalidate(camera);
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
            if(!ready||frame.TruthMode)characterStage.Suspend();
        }

        bool Validate(out string error)
        {
            validationFamilies.Clear();
            try
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
                // 未知核心源不能等清除世界后才统计并连带撤销Look。不可绘制的空装备/失活面不影响本帧。
                var sourceRenderer=surface.Renderer;
                if(!HasDrawableSource(sourceRenderer))continue;
                Material[] sourceMaterials=null;
                bool retainedTerrain=terrainPreparation!=null&&terrainPreparation.TryPrepared(sourceRenderer,out sourceMaterials);
                if(!retainedTerrain)sourceMaterials=sourceRenderer.sharedMaterials;
                if (sourceMaterials.Length > 16)
                { error = "单表面超过16材质槽预算；拒绝无界适配。"; return false; }
                uint sourceFamily = retainedTerrain?RoomPositiveRenderingLayers.PartialLitBit:RoomPositiveRenderingLayers.Classify(sourceRenderer,sourceMaterials,validationFamilies);
                if(sourceFamily==0)
                {error="核心表面含未支持或混合的源材质家族；接管前安全阻断："+sourceRenderer.name;return false;}
                if (frame.TruthMode || surface.Kind == RoomPositiveSurfaceKind.Full) continue;
                uint family = surface.Kind == RoomPositiveSurfaceKind.Roof ? RoomPositiveRenderingLayers.RoofBit : sourceFamily;
                if (family == 0) {error="核心源家族在准备时失效。";return false;}
                if (!retainedTerrain&&RoomPositiveRenderingLayers.RequiresIndependentDraw(surface,sourceMaterials,validationFamilies) && RoomPositiveRenderingLayers.IsIndependentLitFamily(family) &&
                    (surface.DrawMaterial == null || surface.DrawMask == 0 || surface.DrawMask == RoomPositiveRenderingLayers.IndependentBit))
                { error = "动态Lit缺少剔除前的独立材质筛选组；拒绝借用角色共享材质。"; return false; }
                if (surface.Kind == RoomPositiveSurfaceKind.Roof && sourceFamily != RoomPositiveRenderingLayers.PartialLitBit)
                { error = "当前局部屋顶像素路径只支持已登记的Lit顶盖。"; return false; }
                // 同一个Lit调度位可包含原Lit和显式Voxel槽；每槽验证其真正使用的适配器。
                foreach (var source in sourceMaterials)
                {
                    if(retainedTerrain&&!validatedTerrainSources.Add(source))continue;
                    Shader shader = ShaderFor(source, family, out int unused);
                    if (shader == null || !shader.isSupported)
                    { error = "缺少可运行的正向源家族Shader引用：" + (source.shader == null ? "null" : source.shader.name); return false; }
                }
            }
            return true;
                    }
            finally { validationFamilies.Clear();validatedTerrainSources.Clear(); }
        }

        static bool HasDrawableSource(Renderer renderer)
        {
            if(renderer==null||!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy)return false;
            if(renderer is SpriteRenderer sprite)return sprite.sprite!=null;
            if(renderer is MeshRenderer)
            {var filter=renderer.GetComponent<MeshFilter>();return filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.vertexCount>0;}
            return true;
        }

        Shader ShaderFor(Material source, uint family, out int pass)
        {
            pass = 0;
            if ((family == RoomPositiveRenderingLayers.PartialLitBit || family == RoomPositiveRenderingLayers.RoofBit) &&
                source != null && source.shader != null && source.shader.name == RoomPositiveRenderingLayers.VoxelLitShaderName)
                return feature.positiveVoxelLitShader;
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
            RefreshTruthFrustum();
            if (ready) BuildTruthNativePlan(); else ClearTruthNativePlan();
            var cmd = CommandBufferPool.Get("RoomVisibility qualified world");
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            bool complete = false;
            try
            {
                var renderer = renderingData.cameraData.renderer;
                if (ready && !frame.TruthMode && !ValidateIndependentLitSources(out string independentError))
                { ready = false; Statistics.Error = independentError; }
                // 环境全局先于任何预览/真值数据绘制；独立缓冲不参与清除和观察许可。
                if(ready)environmentStage.UploadSurface(context,frame);
                // 每个Clear后的新构造批独立限定编译策略；Restore先写入该批，再提交。
                using(new Ossastra.Rendering.RenderCommandScope(cmd))
                {
                    CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, ClearFlag.All, Color.black);
                    cmd.SetGlobalFloat("_RoomWorldLabelMaskReady", 0);
                    if (ready)
                    {
                        Upload(cmd, ref renderingData);
                        used.Clear(); Statistics.UnsupportedMaterialCount = 0;
                        cmd.SetGlobalFloat("_RoomPositivePermissionMode", 0);
                        if (previewEnabled && !truthNativeAvailable)
                        {
                            using (new ProfilingScope(cmd, perfPreviewCoverage))
                            {
                            CoreUtils.SetRenderTarget(cmd, previewCoverage, previewDepth, ClearFlag.All, Color.black);
                            cmd.SetGlobalFloat("_RoomPositiveCoverageMode", 1);
                            foreach (var surface in frame.PositiveSurfaces)
                                if (surface.Kind != RoomPositiveSurfaceKind.Roof) DrawTruth(cmd, surface);
                            if (previewCoverage.rt.antiAliasing > 1) cmd.ResolveAntiAliasedSurface(previewCoverage.rt);
                            cmd.SetGlobalTexture("_RoomSightCoverageTexture", previewCoverage.nameID);
                            }
                        }
                        CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, ClearFlag.All, Color.black);
                        cmd.SetGlobalFloat("_RoomPositiveCoverageMode", 0);
                        if (frame.TruthMode)
                        {
                            cmd.SetGlobalInteger("_RoomSightPassMode", 2);
                            foreach (var surface in frame.PositiveSurfaces) DrawTruth(cmd, surface);
                        }
                    }
                }
                context.ExecuteCommandBuffer(cmd);cmd.Clear();
                if (ready && previewEnabled && truthNativeAvailable) DrawNativePreview(context, cmd, ref renderingData);
                if (ready)
                {
                    if(!frame.TruthMode)
                    {
                        // 环境命令各自构造/提交，失败的半成命令从未进入核心缓冲。
                        environmentStage.AppendSky(context,frame);
                        // 使用原角色所有begin回调之后的最新材质脚点，刷新自有颜色材质；不再使用overrideShader。
                        using (new ProfilingScope(null, perfRefreshAdapters))
                            materialBuckets.RefreshAdapters();
                        // 动画/装备已提交；独立阶段只持有自己的完整alpha缓存，构造失败不丢弃核心世界命令。
                        using (new ProfilingScope(null, perfCharacterCoverage))
                            characterStage.PrepareCoverage(context,renderingData.cameraData.GetGPUProjectionMatrix()*renderingData.cameraData.GetViewMatrix(),
                                renderingData.cameraData.cameraTargetDescriptor,renderer.cameraColorTargetHandle,renderer.cameraDepthTargetHandle);
                        Statistics.OwnedPropertyBlockCount = propertyScope.BlockCount;
                        using(new Ossastra.Rendering.RenderCommandScope(cmd))
                        {
                            cmd.SetGlobalInteger("_RoomSightOwnerCount", 0); cmd.SetGlobalInteger("_RoomSightOwnerExterior", 0);
                            cmd.SetGlobalInteger("_RoomSightPassMode", 1);
                        }
                        context.ExecuteCommandBuffer(cmd); cmd.Clear();
                        using (new ProfilingScope(null, perfDrawFinalWorld))
                            DrawFinalWorld(context, cmd, ref renderingData);
                        environmentStage.AppendWeather(context,frame,ref renderingData);
                        // 标签许可只消费刚刚重建的合法世界深度，绝不复用旧世界遮挡。
                        if (truthNativeAvailable) DrawNativePermission(context, cmd, ref renderingData);
                        else
                        {
                        using(new Ossastra.Rendering.RenderCommandScope(cmd))
                        {
                            using (new ProfilingScope(cmd, perfPermissionBuild))
                            {
                            CoreUtils.SetRenderTarget(cmd, labelPermission, renderer.cameraDepthTargetHandle, ClearFlag.Color, Color.black);
                            cmd.SetGlobalInteger("_RoomSightPassMode", 0);
                            cmd.SetGlobalFloat("_RoomPositivePermissionMode", 1);
                            // 环境先画，人物/信息最后排除；透明角色也不能把背后的环境色当作可后处理自身。
                            foreach (var surface in frame.PositiveSurfaces) if (surface.WorldLookEnvironment) DrawTruth(cmd, surface, true);
                            foreach (var surface in frame.PositiveSurfaces) if (!surface.WorldLookEnvironment) DrawTruth(cmd, surface, true);
                            if (labelPermission.rt.antiAliasing > 1) cmd.ResolveAntiAliasedSurface(labelPermission.rt);
                            CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
                            cmd.SetGlobalFloat("_RoomPositivePermissionMode", 0);
                            cmd.SetGlobalTexture("_RoomVisibilityPermission", labelPermission.nameID);
                            cmd.SetGlobalFloat("_RoomWorldLabelMaskReady", 1);
                            }
                        }
                        }
                        // 完整世界/标签/真实新深度已完成；新增像素仍在WorldLook与HUD之前。
                        // 外缘单独校验源、目标和每像素路径，不从黑色或静态历史推导角色许可。
                        // 先完成并提交本帧核心颜色/深度/标签；可选外缘使用独立命令缓冲，失败不污染此提交。
                        context.ExecuteCommandBuffer(cmd);cmd.Clear();
                        using (new ProfilingScope(null, perfCharacterOuter))
                            characterStage.AppendOuter(context,renderingData.cameraData.cameraTargetDescriptor,renderer.cameraColorTargetHandle,
                                renderer.cameraDepthTargetHandle,labelPermission,frame,UploadOwner);
                    }
                    stale.Clear(); foreach (var pair in materials) if (pair.Key.Source == null || !used.Contains(pair.Key)) stale.Add(pair.Key);
                    foreach (var key in stale) { DestroyOwned(materials[key]); materials.Remove(key); truthCopyStates.Remove(key); }
                    Statistics.OwnedMaterialCount = materials.Count + materialBuckets.OwnedMaterialCount;
                    Statistics.Active = true;
                    if(characterStage.HasFault||environmentStage.HasFault)Statistics.Error="合法世界有效；"+characterStage.CoverageError+" "+characterStage.OuterError+" "+environmentStage.Error;
                    if (Statistics.UnsupportedMaterialCount > 0)
                        Statistics.Error = "存在未支持的源材质，已保守不画；本轮渲染验收不完整。";
                }
                context.ExecuteCommandBuffer(cmd); complete = true;
                // C诊断只展示新增外缘能量；不让后续环境雾/曝光从已清黑的世界产生新颜色。
                if (ready && Statistics.Active && !frame.TruthMode && Statistics.UnsupportedMaterialCount == 0 &&
                    !characterStage.DiagnosticActive)
                    Ossastra.WorldRendering.Rendering.WorldLookLegalFrameRegistry.Publish(camera, labelPermission, renderingData.cameraData.renderer.cameraDepthTargetHandle, Time.frameCount, characterStage.OuterEnergy);
                else Ossastra.WorldRendering.Rendering.WorldLookLegalFrameRegistry.Revoke(camera);
            }
            finally
            {
                if (!complete) { CancelPreparedState(); Ossastra.WorldRendering.Rendering.WorldLookLegalFrameRegistry.Revoke(camera); }
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

        bool PrepareKnownMaterialPasses(out string error)
        {
            error="";
            foreach(var bucket in materialBuckets.Active)
                if(!Ossastra.Rendering.RenderPassReadiness.Ensure(bucket.Adapter,bucket.Pass))
                {error="已登记世界适配材质的实际Pass未就绪；接管前安全阻断。";return false;}
            foreach(var surface in frame.PositiveSurfaces)
            {
                var renderer=surface.Renderer;
                if(surface.Kind!=RoomPositiveSurfaceKind.Full||!HasDrawableSource(renderer))continue;
                renderer.GetSharedMaterials(slots);
                foreach(var source in slots)
                    if(!EnsureSourcePass(source))
                    {error="完整表面的实际源Pass未就绪；接管前安全阻断："+renderer.name;return false;}
            }
            return true;
        }

        // 与DrawRenderers的标签优先序一致，只准备当前会被选中的源Pass，不全量预热Shader。
        static bool EnsureSourcePass(Material source)
        {
            if(source==null||source.shader==null)return false;
            var lightMode=new ShaderTagId("LightMode");
            foreach(var tag in sourceTags)
                for(int pass=0;pass<source.passCount;++pass)
                {
                    var value=source.shader.FindPassTagValue(pass,lightMode);
                    if(value.Equals(tag)||(tag.Equals(new ShaderTagId("SRPDefaultUnlit"))&&value.Equals(default(ShaderTagId))))
                        return Ossastra.Rendering.RenderPassReadiness.Ensure(source,pass);
                }
            return false;
        }

        void DrawFinalWorld(ScriptableRenderContext context, CommandBuffer cmd, ref RenderingData renderingData)
        {
            bool full = false;
            foreach (var surface in frame.PositiveSurfaces)
            {
                var source = surface.Renderer;
                if (!HasDrawableSource(source)) continue;
                if (RoomPositiveRenderingLayers.Classify(source) == 0) { ++Statistics.UnsupportedMaterialCount; continue; }
                if (surface.Kind == RoomPositiveSurfaceKind.Full) full = true;
            }
            for (int queue = 0; queue < 2; ++queue)
            {
                using(new Ossastra.Rendering.RenderCommandScope(cmd))Ossastra.CharacterLighting.CharacterLightingSystem.Clear(cmd);
                context.ExecuteCommandBuffer(cmd);cmd.Clear();
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
                using(new Ossastra.Rendering.RenderCommandScope(cmd))DrawIndependentUnlit(cmd, transparent);
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
            }
        }

        static bool IsTransparent(Material source) => (source.renderQueue < 0 ? source.shader.renderQueue : source.renderQueue) > 2500;

        Material ShadedAdapter(Material source, RoomPositiveSurfaceKind kind, uint family, out int pass)
        {
            var shader = ShaderFor(source, family, out pass);
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
            cmd.SetGlobalFloat("_EnvironmentFullSurface",surface.EnvironmentFullSurface?1:0);
            cmd.SetGlobalFloat("_EnvironmentSurfaceRole",surface.EnvironmentSurfaceRole);
            cmd.SetGlobalFloat("_EnvironmentResponseOverride",surface.EnvironmentResponseOverride);
            cmd.SetGlobalFloat("_EnvironmentReceiveShadow",surface.EnvironmentReceiveShadow);
            cmd.SetGlobalFloat("_EnvironmentMinimumOverride",surface.EnvironmentMinimumOverride);
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
                    !HasDrawableSource(renderer)) continue;
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
                if(Ossastra.CharacterLighting.CharacterLightingSystem.TryGet(camera,out var characterLighting))
                    characterLighting.Bind(cmd,item.Surface.Renderer,characterStage.Coverage,frame);
                else Ossastra.CharacterLighting.CharacterLightingSystem.Clear(cmd);
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
                // 源keyword可能在动画/装备回调后更新；只认证即将用到的适配Pass。
                if(!Ossastra.Rendering.RenderPassReadiness.Ensure(adapter,pass)){++Statistics.UnsupportedMaterialCount;return;}
                // overrideMaterial持有与实际Shader一致的属性/采样器布局，避免本机overrideShader不安全路径。
                drawing.overrideMaterial = adapter; drawing.overrideMaterialPassIndex = pass;
                drawing.enableDynamicBatching = false; drawing.enableInstancing = false;
            }
            else
            {
                foreach(var surface in frame.PositiveSurfaces)
                {
                    var source=surface.Renderer;
                    if(surface.Kind!=RoomPositiveSurfaceKind.Full||!HasDrawableSource(source))continue;
                    source.GetSharedMaterials(slots);
                    foreach(var material in slots)
                        if(material==null||(IsTransparent(material)==transparent&&!EnsureSourcePass(material))){++Statistics.UnsupportedMaterialCount;return;}
                }
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
        { DrawTruthFallback(cmd, surface, permission, false); }

        void DrawTruthFallback(CommandBuffer cmd, RoomPositiveSurface surface, bool permission, bool skipNative)
        {
            // 全槽已由本次native调用提交时，省掉旧逐物体分类/owner/材质准备；重新读实际引用，不能跳过混合或被外部换材的对象。
            if (skipNative && AllTruthSlotsSubmitted(surface.Renderer)) return;
            cmd.SetGlobalFloat("_RoomWorldLookEnvironment", surface.WorldLookEnvironment ? 1 : 0);
            var renderer = surface.Renderer;
            if (!HasDrawableSource(renderer)) return;
            uint rendererFamily = RoomPositiveRenderingLayers.Classify(renderer);
            if (rendererFamily == 0) { ++Statistics.UnsupportedMaterialCount; return; }
            if (TruthOutsideFrustum(renderer, rendererFamily)) { ++truthBoundsRejected; return; }
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
                if (skipNative && truthNativeByClone.TryGetValue(slots[slot], out var native) && native.Submitted) continue;
                var source = materialBuckets.ResolveOriginal(slots[slot]); uint family = RoomPositiveRenderingLayers.Classify(source);
                var adapter = ExtractTruthAdapter(source, family, surface, permission, out int truthPass);
                if (adapter == null) continue;
                cmd.DrawRenderer(renderer, adapter, slot, truthPass);
                ++Statistics.SurfaceDraws;
            }
        }

        // 只接管已准备、同一Truth合同的静态opaque桶；计数是native调用/登记槽数，不冒充GPU可见draw数。
        void BuildTruthNativePlan()
        {
            ClearTruthNativePlan();
            if (!nativeTruthBucketsEnabled || frame == null || frame.TruthMode || camera == null || camera.stereoEnabled ||
                frame.PositiveSurfaces == null || frame.PositiveSurfaces.Length > 4096) return;
            foreach (var bucket in materialBuckets.Active)
            {
                if (bucket.Transparent || bucket.Clone == null || bucket.Original == null ||
                    (bucket.Family != RoomPositiveRenderingLayers.PartialLitBit && bucket.Family != RoomPositiveRenderingLayers.PartialSimpleLitBit && bucket.Family != RoomPositiveRenderingLayers.PartialUnlitBit)) continue;
                if (!bucket.Original.GetShaderPassEnabled("UniversalForward") || !bucket.Original.GetShaderPassEnabled("UniversalForwardOnly") ||
                    !bucket.Original.GetShaderPassEnabled("SRPDefaultUnlit")) continue;
                var plan = new TruthNativeBucket { Bucket = bucket };
                truthNativeByClone.Add(bucket.Clone, plan); truthNativeBuckets.Add(plan);
            }
            foreach (var surface in frame.PositiveSurfaces)
            {
                var renderer = surface.Renderer; if (renderer == null) continue;
                renderer.GetSharedMaterials(truthNativeSlots);
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter == null ? null : filter.sharedMesh;
                bool eligible = surface.StaticEnvironment && surface.Kind == RoomPositiveSurfaceKind.Partial && renderer is MeshRenderer &&
                    HasDrawableSource(renderer) && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0 &&
                    renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly && renderer.GetComponentInParent<LODGroup>() == null &&
                    mesh != null && mesh.subMeshCount == truthNativeSlots.Count && truthNativeSlots.Count > 0;
                // Any missing/unprepared/mixed slot rejects all participating buckets, not just the bad slot.
                for (int slot = 0; slot < truthNativeSlots.Count; ++slot)
                    if (truthNativeSlots[slot] == null || !truthNativeByClone.ContainsKey(truthNativeSlots[slot])) eligible = false;
                foreach (var material in truthNativeSlots)
                {
                    if (material == null || !truthNativeByClone.TryGetValue(material, out var plan)) continue;
                    if (!eligible || surface.DrawMask != plan.Bucket.Family || material.renderQueue != plan.Bucket.Queue) plan.Rejected = true;
                    if (!plan.Seen) { plan.Seen = true; plan.Representative = surface; }
                    else if (plan.Representative.WorldLookEnvironment != surface.WorldLookEnvironment || plan.Representative.EnvironmentFullSurface != surface.EnvironmentFullSurface) plan.Rejected = true;
                    ++plan.Slots;
                }
            }
            foreach (var plan in truthNativeBuckets) if (plan.Seen && !plan.Rejected) { truthNativeAvailable = true; break; }
        }

        void ClearTruthNativePlan()
        {
            nativeTruthBucketCount = nativeTruthSlotCount = 0;
            truthNativeAvailable = false; truthNativeByClone.Clear(); truthNativeBuckets.Clear();
        }

        bool AllTruthSlotsSubmitted(Renderer renderer)
        {
            if (renderer == null) return false;
            renderer.GetSharedMaterials(truthNativeSlots);
            if (truthNativeSlots.Count == 0) return false;
            foreach (var material in truthNativeSlots)
                if (material == null || !truthNativeByClone.TryGetValue(material, out var plan) || !plan.Submitted) return false;
            return true;
        }

        void DrawTruthSurfaces(ScriptableRenderContext context, CommandBuffer cmd, ref RenderingData renderingData, bool permission, int environmentFilter)
        {
            foreach (var plan in truthNativeBuckets) plan.Submitted = false;
            // Owner is per Renderer/slot in the already prepared RoomPositivePropertyScope.
            // Restore/submit command compilation scope before invoking native DrawRenderers.
            using (new Ossastra.Rendering.RenderCommandScope(cmd))
                cmd.SetGlobalFloat("_RoomWorldLookEnvironment", environmentFilter == 1 ? 1 : 0);
            context.ExecuteCommandBuffer(cmd); cmd.Clear();
            foreach (var plan in truthNativeBuckets)
            {
                if (!plan.Seen || plan.Rejected || (environmentFilter >= 0 && plan.Representative.WorldLookEnvironment != (environmentFilter == 1))) continue;
                var bucket = plan.Bucket;
                var adapter = ExtractTruthAdapter(bucket.Original, bucket.Family, plan.Representative, permission, out int truthPass);
                if (adapter == null || truthPass != 3 || !Ossastra.Rendering.RenderPassReadiness.Ensure(adapter, truthPass)) continue;
                var drawing = CreateDrawingSettings(sourceTags, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                drawing.overrideMaterial = adapter; drawing.overrideMaterialPassIndex = truthPass;
                drawing.enableDynamicBatching = false; drawing.enableInstancing = false;
                var filtering = new FilteringSettings(new RenderQueueRange { lowerBound = bucket.Queue, upperBound = bucket.Queue }, camera.cullingMask, bucket.Family);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
                plan.Submitted = true; ++nativeTruthBucketCount; nativeTruthSlotCount += plan.Slots;
            }
            using (new Ossastra.Rendering.RenderCommandScope(cmd))
            {
                foreach (var surface in frame.PositiveSurfaces)
                {
                    if ((!permission && surface.Kind == RoomPositiveSurfaceKind.Roof) ||
                        (environmentFilter >= 0 && surface.WorldLookEnvironment != (environmentFilter == 1))) continue;
                    DrawTruthFallback(cmd, surface, permission, true);
                }
            }
            context.ExecuteCommandBuffer(cmd); cmd.Clear();
        }

        void DrawNativePreview(ScriptableRenderContext context, CommandBuffer cmd, ref RenderingData renderingData)
        {
            // CPU-only marker: a GPU ProfilingScope must not cross Clear/native DrawRenderers.
            using (new ProfilingScope(null, perfPreviewCoverage))
            {
                using (new Ossastra.Rendering.RenderCommandScope(cmd))
                {
                    CoreUtils.SetRenderTarget(cmd, previewCoverage, previewDepth, ClearFlag.All, Color.black);
                    cmd.SetGlobalFloat("_RoomPositiveCoverageMode", 1);
                }
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
                DrawTruthSurfaces(context, cmd, ref renderingData, false, -1);
                using (new Ossastra.Rendering.RenderCommandScope(cmd))
                {
                    if (previewCoverage.rt.antiAliasing > 1) cmd.ResolveAntiAliasedSurface(previewCoverage.rt);
                    cmd.SetGlobalTexture("_RoomSightCoverageTexture", previewCoverage.nameID);
                    CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle, ClearFlag.All, Color.black);
                    cmd.SetGlobalFloat("_RoomPositiveCoverageMode", 0);
                }
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
            }
        }

        void DrawNativePermission(ScriptableRenderContext context, CommandBuffer cmd, ref RenderingData renderingData)
        {
            using (new ProfilingScope(null, perfPermissionBuild))
            {
                using (new Ossastra.Rendering.RenderCommandScope(cmd))
                {
                    CoreUtils.SetRenderTarget(cmd, labelPermission, renderingData.cameraData.renderer.cameraDepthTargetHandle, ClearFlag.Color, Color.black);
                    cmd.SetGlobalInteger("_RoomSightPassMode", 0); cmd.SetGlobalFloat("_RoomPositivePermissionMode", 1);
                }
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
                DrawTruthSurfaces(context, cmd, ref renderingData, true, 1);
                DrawTruthSurfaces(context, cmd, ref renderingData, true, 0);
                using (new Ossastra.Rendering.RenderCommandScope(cmd))
                {
                    if (labelPermission.rt.antiAliasing > 1) cmd.ResolveAntiAliasedSurface(labelPermission.rt);
                    CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    cmd.SetGlobalFloat("_RoomPositivePermissionMode", 0);
                    cmd.SetGlobalTexture("_RoomVisibilityPermission", labelPermission.nameID); cmd.SetGlobalFloat("_RoomWorldLabelMaskReady", 1);
                }
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
            }
        }

        Material ExtractTruthAdapter(Material source, uint family, RoomPositiveSurface surface, bool permission, out int truthPass)
        {
            truthPass = 3;
                bool cutRoof = previewEnabled && surface.Kind == RoomPositiveSurfaceKind.Roof && Array.IndexOf(frame.PreviewRoofSurfaces, surface.Renderer) >= 0;
                var key = new MaterialKey(source, surface.Kind, cutRoof, permission,environmentFull:surface.EnvironmentFullSurface);
                if (!materials.TryGetValue(key, out var adapter))
                {
                    Shader adapterShader = family == RoomPositiveRenderingLayers.PartialLitSpineBit ? spriteLitTruthShader : truthShader;
                    if (adapterShader == null || !adapterShader.isSupported) { ++Statistics.UnsupportedMaterialCount; return null; }
                    adapter = new Material(adapterShader) { name = source.name + " [Room positive truth]", hideFlags = HideFlags.HideAndDontSave };
                    materials.Add(key, adapter);
                }
                bool firstUse = used.Add(key);
                bool upright = source.shader.name.StartsWith("Ossastra/Characters/Upright", StringComparison.Ordinal);
                int mode = family == RoomPositiveRenderingLayers.PartialThinTopBit ? 1 :
                    family == RoomPositiveRenderingLayers.PartialDecorationBit ? 2 :
                    (family == RoomPositiveRenderingLayers.PartialDepthPlaneBit || family == RoomPositiveRenderingLayers.PartialMapDepthBit) ? 3 :
                    (family == RoomPositiveRenderingLayers.PartialSpineBit || family == RoomPositiveRenderingLayers.PartialSkeletonBit) ? 4 :
                    (family == RoomPositiveRenderingLayers.PartialSpriteBit || family == RoomPositiveRenderingLayers.PartialDefaultSpriteBit) ? 5 :
                    family == RoomPositiveRenderingLayers.PartialWorldTileBit ? 6 : 0;
                // 同一Execute只在首用检查；跨帧校验源和自有副本，变化/污染时才重抄。
                // 签名在全部派生覆盖后记录；Owner仍逐draw上传，许可/屋顶/环境模式各自持有完整Key。
                if (firstUse)
                {
                Shader expectedShader = family == RoomPositiveRenderingLayers.PartialLitSpineBit ? spriteLitTruthShader : truthShader;
                if (expectedShader == null || !expectedShader.isSupported) { ++Statistics.UnsupportedMaterialCount; used.Remove(key); return null; }
                if (adapter.shader != expectedShader) { adapter.shader = expectedShader; truthCopyStates.Remove(key); }
                int sourceCrc = source.ComputeCRC();
                bool refresh = !truthCopyStates.TryGetValue(key, out var copyState) || copyState.SourceCrc != sourceCrc || adapter.ComputeCRC() != copyState.AdapterCrc ||
                    TruthDerivedCrc(adapter, copyState.DerivedMask) != copyState.DerivedCrc || !SameTruthKeywords(adapter, copyState.Keywords);
                if (refresh)
                {
                using (new ProfilingScope(null, perfTruthMaterialCopy))
                    adapter.CopyPropertiesFromMaterial(source);
                if (!upright && adapter.HasProperty("_VisualDepthEnabled")) adapter.SetFloat("_VisualDepthEnabled", 0);
                if (family != RoomPositiveRenderingLayers.PartialLitSpineBit)
                {
                    adapter.SetFloat("_RoomSurfaceMode", mode);
                    adapter.SetFloat("_RoomSourceAlphaClip", source.IsKeywordEnabled("_ALPHATEST_ON") ? 1 : 0);
                }
                adapter.SetFloat("_RoomPositiveDepthWrite", key.Permission ? 0 : 1);
                adapter.SetFloat("_RoomPositivePartial", surface.Kind == RoomPositiveSurfaceKind.Partial&&!surface.EnvironmentFullSurface ? 1 : 0);
                adapter.SetFloat("_RoomPositiveCutRoof", key.CutRoof ? 1 : 0);
                adapter.SetColor("_RoomPositiveColor", surface.Kind == RoomPositiveSurfaceKind.Partial&&!surface.EnvironmentFullSurface ? new Color(1, .38f, .04f, 1) :
                    surface.Kind == RoomPositiveSurfaceKind.Roof ? new Color(.2f, .35f, .8f, 1) : new Color(.05f, .9f, .85f, 1));
                adapter.SetFloat("_Cull", mode == 0 && source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 0);
                int derivedMask = 0;
                for (int i = 0; i < truthDerivedFloats.Length; ++i) if (adapter.HasProperty(truthDerivedFloats[i])) derivedMask |= 1 << i;
                if (adapter.HasProperty(truthColorId)) derivedMask |= 1 << truthDerivedFloats.Length;
                truthCopyStates[key] = new TruthCopyState { SourceCrc = sourceCrc, AdapterCrc = adapter.ComputeCRC(), DerivedMask = derivedMask,
                    DerivedCrc = TruthDerivedCrc(adapter, derivedMask), Keywords = adapter.shaderKeywords };
                }
                }
                bool hardware = mode == 0 || mode == 2 || mode == 6 || (mode == 3 && !upright);
                truthPass = family == RoomPositiveRenderingLayers.PartialLitSpineBit ? 0 : mode == 5 ? 1 : hardware ? 3 : 0;
                return adapter;
        }

        // 当前分支的Material CRC未覆盖异Shader复制后的全部派生值/未知关键词；这些由本模块显式补检。
        static int TruthDerivedCrc(Material adapter, int mask)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < truthDerivedFloats.Length; ++i) if ((mask & (1 << i)) != 0) hash = hash * 31 + adapter.GetFloat(truthDerivedFloats[i]).GetHashCode();
                if ((mask & (1 << truthDerivedFloats.Length)) != 0) hash = hash * 31 + adapter.GetColor(truthColorId).GetHashCode();
                return hash;
            }
        }
        static bool SameTruthKeywords(Material adapter, string[] expected)
        {
            var actual = adapter.shaderKeywords;
            if (expected == null || actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; ++i) if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal)) return false;
            return true;
        }
        // 每次Execute刷新；只省镜外标准3D Truth提交，不改登记、Caster、FinalWorld或任何2D路径。
        void RefreshTruthFrustum()
        {
            truthBoundsRejected = 0; truthFrustumValid = false;
            if (camera == null || camera.stereoEnabled) return;
            var projection = camera.projectionMatrix; var view = camera.worldToCameraMatrix;
            if (!TruthMatrixFinite(projection) || !TruthMatrixFinite(view)) return;
            var vp = projection * view; if (!TruthMatrixFinite(vp)) return;
            GeometryUtility.CalculateFrustumPlanes(vp, truthFrustumPlanes);
            foreach (var p in truthFrustumPlanes)
                if (!Finite(new Vector4(p.normal.x,p.normal.y,p.normal.z,p.distance)) || p.normal.sqrMagnitude < 1e-12f) return;
            truthFrustumValid = true;
        }
        bool TruthOutsideFrustum(Renderer renderer, uint family)
        {
            if (!truthFrustumValid || !(renderer is MeshRenderer) ||
                (family != RoomPositiveRenderingLayers.PartialLitBit && family != RoomPositiveRenderingLayers.PartialSimpleLitBit && family != RoomPositiveRenderingLayers.PartialUnlitBit)) return false;
            try
            {
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter == null ? null : filter.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0) return false;
                var local = mesh.bounds; var bounds = renderer.bounds; var m = renderer.localToWorldMatrix;
                if (!TruthBoundsValid(local) || !TruthBoundsValid(bounds) || !TruthMatrixFinite(m)) return false;
                Vector3 e = local.extents, x = m.MultiplyVector(new Vector3(e.x,0,0)), y = m.MultiplyVector(new Vector3(0,e.y,0)), z = m.MultiplyVector(new Vector3(0,0,e.z));
                var extents = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
                var world = new Bounds(m.MultiplyPoint3x4(local.center), extents * 2); if (!TruthBoundsValid(world)) return false;
                // 合并mesh变换后包围体，避免缩小的Renderer自定义bounds造成误裁；触边再留0.02余量。
                bounds.Encapsulate(world); bounds.Expand(.02f);
                return TruthBoundsValid(bounds) && !GeometryUtility.TestPlanesAABB(truthFrustumPlanes, bounds);
            }
            catch { return false; } // 无法取得可信包围体时沿用原绘制，不能因诊断性预筛隐藏未知内容。
        }
        static bool TruthMatrixFinite(Matrix4x4 matrix)
        { for (int row=0;row<4;++row) if (!Finite(matrix.GetRow(row))) return false; return true; }
        static bool TruthBoundsValid(Bounds bounds)
        {
            var e = bounds.extents;
            return Finite(bounds.center) && Finite(e) && e.x>=0 && e.y>=0 && e.z>=0 && e.sqrMagnitude>1e-12f && e.sqrMagnitude<float.MaxValue;
        }

        void ClearMaterials() { foreach (var value in materials.Values) DestroyOwned(value); materials.Clear(); truthCopyStates.Clear(); }
        static void DestroyOwned(Object value)
        { if (value != null) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); } }
        public void Dispose()
        {
            characterStage.Dispose();
            Ossastra.WorldRendering.Rendering.WorldLookLegalFrameRegistry.Revoke(camera);
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
