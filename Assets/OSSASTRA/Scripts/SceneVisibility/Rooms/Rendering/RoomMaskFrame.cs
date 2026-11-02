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

    public sealed class RoomMaskStatistics
    {
        public int Width, Height, Msaa, SurfaceDraws, PortalDraws, UnexploredVolumeDraws;
        public int OwnedMeshCount;
        public int OwnedTextureCount, OwnedMaterialCount, UnsupportedMaterialCount, PmaApproximationCount;
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
            Shader.SetGlobalFloat("_RoomWorldLabelMaskEnabled", frame.Enabled ? 1 : 0);
            Shader.SetGlobalFloat("_RoomWorldLabelMaskReady", 0);
            Shader.SetGlobalFloat("_RoomWorldLabelExteriorEnabled", frame.ExteriorBlackoutEnabled ? 1 : 0);
            Shader.SetGlobalFloat("_RoomWorldLabelInteriorEnabled", frame.InteriorUnexploredEnabled ? 1 : 0);
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
