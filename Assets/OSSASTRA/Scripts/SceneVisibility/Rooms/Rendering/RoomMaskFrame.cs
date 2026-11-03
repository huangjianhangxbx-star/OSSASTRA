/* 功能：提供单相机房间许可遮罩的显式帧数据与所有者登记。
 * 边界：不查找角色、房间或Renderer，不接管玩法状态；数组由调用者登记并在相机绘制期间保持稳定。
 * 生命周期：组织者每帧Publish，禁用/卸载时Release；EndCamera不释放登记，以便复用有限GPU资源。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    public struct RoomMaskVolume
    {
        public Matrix4x4 WorldToLocal;
        public Vector3 HalfExtents;
    }

    public struct RoomMaskSurface
    {
        public Renderer Renderer;
        public bool ClipToVolumes;
    }

    public struct RoomMaskPortal
    {
        public Mesh ApertureMesh;
        public Matrix4x4 LocalToWorld;
        public Vector3 OutwardNormal;
    }

    public sealed class RoomMaskFrame
    {
        // 正向绘制帧：旧双黑幕字段仅供未迁移场景，不再授予本模式可见权。
        public bool PositiveRenderingEnabled;
        public bool TruthMode = true;
        public bool ExteriorView;
        public Vector3 ObserverPosition;
        public Vector3 CameraObserverPosition;
        public RoomSightDomain[] WindowSightDomains = Array.Empty<RoomSightDomain>();
        public RoomMaskVolume[] CameraOpaqueBlockers = Array.Empty<RoomMaskVolume>();
        public RoomFogSettings Fog = new RoomFogSettings();
        // 主控、传送、门窗/阻挡拓扑改变时递增；旧世界域不能跨权威重置延续。
        public int VisibilityEpoch;
        public double PresentationTime = -1;
        public Func<Vector3, bool> AllowsPoint;
        public RoomMaskVolume[] SeedVolumes = Array.Empty<RoomMaskVolume>();
        public RoomMaskVolume[] BuildingVolumes = Array.Empty<RoomMaskVolume>();
        public RoomSightDomain[] SightDomains = Array.Empty<RoomSightDomain>();
        public RoomMaskVolume[] OpaqueBlockers = Array.Empty<RoomMaskVolume>();
        public RoomPositiveSurface[] PositiveSurfaces = Array.Empty<RoomPositiveSurface>();
        // 仅室外预览的目标建筑屋顶可消费局部像素许可；不等同于整栋激活/揭顶。
        public Renderer[] PreviewRoofSurfaces = Array.Empty<Renderer>();
        public bool Enabled;
        public bool ExteriorBlackoutEnabled = true;
        public bool InteriorUnexploredEnabled = true;
        public Color ExteriorColor = Color.black;
        public Color InteriorColor = Color.black;
        // 兼容旧灰盒调用；新双黑幕使用明确的ExteriorColor/InteriorColor。
        public Color CurtainColor = Color.black;
        public int DebugMode;
        public RoomMaskVolume[] Volumes = Array.Empty<RoomMaskVolume>();
        public RoomMaskVolume[] KnownVolumes = Array.Empty<RoomMaskVolume>();
        public RoomMaskVolume[] UnexploredVolumes = Array.Empty<RoomMaskVolume>();
        public RoomMaskSurface[] BuildingSurfaces = Array.Empty<RoomMaskSurface>();
        public RoomMaskSurface[] Surfaces = Array.Empty<RoomMaskSurface>();
        public RoomMaskPortal[] Portals = Array.Empty<RoomMaskPortal>();
        public Collider[] LabelRaycastExclusions = Array.Empty<Collider>();
    }

    /// <summary>平面内侧为 dot(normal, world)+w >= 0；室外目标额外排除所有建筑体积。</summary>
    public sealed class RoomSightDomain
    {
        public Vector3 ObserverPosition;
        public bool IsCameraWindow;
        public Vector4[] Planes = Array.Empty<Vector4>();
        public RoomMaskVolume[] TargetVolumes = Array.Empty<RoomMaskVolume>();
        public bool TargetExterior;
    }

    [Serializable]
    public sealed class RoomFogSettings
    {
        public bool Enabled = true;
        public float InitialVisibility = .3f, GainSeconds = .5f, LossSeconds = .3f;
        public float SpreadSeconds = .12f, SpreadRadius = 8, InwardFeather = .35f;
        public bool DitherEnabled;
        public float DitherCellSize = .06f, DitherStrength = .12f;
        public Color Color = Color.black;
    }

    public enum RoomPositiveSurfaceKind { Full, Partial, Roof }

    public struct RoomPositiveSurface
    {
        public Renderer Renderer;
        public RoomPositiveSurfaceKind Kind;
        public RoomMaskVolume[] OwnerVolumes;
        public bool OwnerExterior;
        // 原业务表现登记强制只读源材质；动态Lit组由有限筛选位保持URP逐对象光照。
        public bool IndependentDraw;
        // 由作者/组织者明确登记；角色、敌人、动画、可破坏物与门窗活动片不得猜作静态历史。
        public bool StaticEnvironment;
        // 仅环境后处理显示角色，与静态历史/当前许可独立；人物、敌人与信息叶为false。
        public bool WorldLookEnvironment;
        public uint DrawMask;
        public Material DrawMaterial;
    }

    public sealed class RoomMaskStatistics
    {
        public int Width, Height, Msaa, SurfaceDraws, PortalDraws, UnexploredVolumeDraws;
        public int OwnedMeshCount;
        public int OwnedTextureCount, OwnedMaterialCount, UnsupportedMaterialCount, PmaApproximationCount;
        public int OwnedBufferCount;
        public long EstimatedBufferBytes;
        public int OwnedPropertyBlockCount;
        public int SourceMaterialBucketCount, RetainedMaterialConflicts;
        public long EstimatedTextureBytes;
        public double CommandBuildMilliseconds;
        public bool Active;
        public string Error = "";
    }

    public static class RoomMaskCameraRegistry
    {
        sealed class Entry { public object Owner; public Camera Camera; public RoomMaskFrame Frame; }
        static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        static readonly Dictionary<int, RoomMaskStatistics> statistics = new Dictionary<int, RoomMaskStatistics>();
        internal static event Action<int> Released;
        internal static event Action<int> ScopeEnded;
        /// <summary>最新相机快照已发布/撤销；只供已有世界表现重算显隐，不推进业务或角色。</summary>
        public static event Action<Camera> FramePublished;

        public static void Publish(object owner, Camera camera, RoomMaskFrame frame)
        {
            if (owner == null || camera == null || frame == null) throw new ArgumentException("许可遮罩需要明确owner、相机与帧。");
            int id = camera.GetInstanceID();
            if (entries.TryGetValue(id, out var entry))
            {
                if (!ReferenceEquals(entry.Owner, owner) || entry.Camera != camera)
                    throw new InvalidOperationException("同一相机的房间许可遮罩已有其它owner。");
                entry.Frame = frame;
            }
            else entries.Add(id, new Entry { Owner = owner, Camera = camera, Frame = frame });
            // 同一Time.frameCount也可能再次绘制相机；新绘制不能消费前次合法世界附件。
            Ossastra.WorldRendering.Rendering.WorldLookLegalFrameRegistry.Invalidate(camera);
            Shader.SetGlobalFloat("_RoomWorldLabelMaskEnabled", frame.Enabled ? 1 : 0);
            Shader.SetGlobalFloat("_RoomWorldLabelMaskReady", 0);
            Shader.SetGlobalFloat("_RoomWorldLabelExteriorEnabled", frame.PositiveRenderingEnabled || frame.ExteriorBlackoutEnabled ? 1 : 0);
            Shader.SetGlobalFloat("_RoomWorldLabelInteriorEnabled", frame.PositiveRenderingEnabled || frame.InteriorUnexploredEnabled ? 1 : 0);
            FramePublished?.Invoke(camera);
        }

        public static void Release(object owner, Camera camera)
        {
            // Unity场景卸载时Camera可能先被销毁；仍按托管包装器的稳定InstanceID释放自己的资源。
            if (ReferenceEquals(camera, null)) return;
            int id = camera.GetInstanceID();
            if (!entries.TryGetValue(id, out var entry) || !ReferenceEquals(entry.Owner, owner)) return;
            entries.Remove(id); statistics.Remove(id); Released?.Invoke(id);
            Shader.SetGlobalFloat("_RoomWorldLabelMaskEnabled", 0);
            Shader.SetGlobalFloat("_RoomWorldLabelMaskReady", 0);
            FramePublished?.Invoke(camera);
        }

        // 公开Begin/End即使没有真实URP CameraCleanup也必须归还借用范围；帧与有限缓存继续保留。
        public static void EndScope(object owner, Camera camera)
        {
            if (owner == null || ReferenceEquals(camera, null)) return;
            int id = camera.GetInstanceID();
            if (!entries.TryGetValue(id, out var entry) || !ReferenceEquals(entry.Owner, owner)) return;
            ScopeEnded?.Invoke(id);
        }

        public static bool TryGet(Camera camera, out RoomMaskFrame frame)
        {
            frame = null;
            if (camera == null || !entries.TryGetValue(camera.GetInstanceID(), out var entry) || entry.Camera != camera) return false;
            frame = entry.Frame; return frame != null;
        }

        public static bool TryGetStatistics(Camera camera, out RoomMaskStatistics result)
        {
            result = null;
            return camera != null && statistics.TryGetValue(camera.GetInstanceID(), out result);
        }

        internal static void SetStatistics(Camera camera, RoomMaskStatistics result)
        { if (camera != null) statistics[camera.GetInstanceID()] = result; }
    }
}
