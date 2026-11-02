/* 功能：让明确的世界血条/掉落标签消费本GameplayCamera已发布的空间许可；不改变业务访问或攻击LOS。
 * 边界：没有房间帧时沿旧行为；未知室内掉落Sprite仅隐藏自己的表现，室外Sprite留给GPU按窗口像素处理。
 * 协作：Root发布最新帧后世界视图只重算已有UI；外窗标签额外做真实孔洞与前景Collider保守检查。
 * 生命周期：开口网格只读缓存，登记撤销时释放缓存；UI材质由调用视图独立拥有并销毁。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    public static class RoomWorldPresentationGate
    {
        sealed class Aperture { public Vector3[] Vertices; public int[] Triangles; }
        static readonly Dictionary<Mesh, Aperture> apertures = new Dictionary<Mesh, Aperture>();
        static readonly RaycastHit[] hits = new RaycastHit[64];
        static bool reportedMissingShader;

        static RoomWorldPresentationGate() { RoomMaskCameraRegistry.Released += Released; }
        static void Released(int cameraId) { apertures.Clear(); }

        public static Material CreateLabelMaterial(Camera camera, bool portalOnly = false)
        {
            if (!RoomMaskCameraRegistry.TryGet(camera, out var frame)) return null;
            var shader = Shader.Find("Ossastra/Rooms/WorldLabel");
            if (shader == null || !shader.isSupported)
            {
                if (!reportedMissingShader) Debug.LogError("房间世界标签缺少可用WorldLabel Shader；像素许可尚未接通。");
                reportedMissingShader = true; return null;
            }
            var material = new Material(shader) { name = "RoomVisibility owned world-label mask" + (portalOnly ? " [external portal]" : ""), hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_RoomWorldLabelRequirePortal", portalOnly ? 1 : 0);
            return material;
        }

        public static bool RequiresPortal(Camera camera, Vector3 worldPoint)
        {
            if (!RoomMaskCameraRegistry.TryGet(camera, out var frame) || !frame.Enabled) return false;
            if (frame.PositiveRenderingEnabled)
                return !Contains(frame.SeedVolumes, worldPoint) && (!frame.ExteriorView || Contains(frame.BuildingVolumes, worldPoint));
            return frame.ExteriorBlackoutEnabled && !Contains(frame.Volumes, worldPoint);
        }

        public static bool AllowsOwnedGroundSprite(Camera camera, Vector3 worldPoint)
        {
            if (!RoomMaskCameraRegistry.TryGet(camera, out var frame) || !frame.Enabled) return true;
            if (frame.PositiveRenderingEnabled)
                return frame.AllowsPoint != null && frame.AllowsPoint(worldPoint);
            if (!frame.InteriorUnexploredEnabled) return true;
            // 室外完整Sprite不能按中心是否落在窗口决定显隐，仍由GPU逐像素许可域处理。
            return !Contains(frame.Volumes, worldPoint) || Contains(frame.KnownVolumes, worldPoint);
        }

        public static bool AllowsLabel(Camera camera, Vector3 worldPoint, Transform targetOwner = null)
        {
            if (!RoomMaskCameraRegistry.TryGet(camera, out var frame) || !frame.Enabled) return true;
            if (frame.PositiveRenderingEnabled)
            {
                if (frame.AllowsPoint == null || !frame.AllowsPoint(worldPoint)) return false;
                Vector3 projection = camera.WorldToViewportPoint(worldPoint);
                if (projection.z <= 0 || projection.x < 0 || projection.x > 1 || projection.y < 0 || projection.y > 1) return false;
                Ray cameraRay = camera.ViewportPointToRay(projection);
                float length = Vector3.Dot(worldPoint - cameraRay.origin, cameraRay.direction);
                return length > 0 && ColliderPathClear(camera, frame, cameraRay, length, worldPoint, targetOwner);
            }
            if (Contains(frame.Volumes, worldPoint))
                return !frame.InteriorUnexploredEnabled || Contains(frame.KnownVolumes, worldPoint);
            if (!frame.ExteriorBlackoutEnabled) return true;
            var projected = camera.WorldToViewportPoint(worldPoint);
            if (projected.z <= 0f || projected.x < 0f || projected.x > 1f || projected.y < 0f || projected.y > 1f) return false;
            var ray = camera.ViewportPointToRay(projected);
            float pointDistance = Vector3.Dot(worldPoint - ray.origin, ray.direction);
            if (pointDistance <= 0f || frame.Portals == null) return false;
            foreach (var portal in frame.Portals)
            {
                if (portal.ApertureMesh == null) continue;
                float denominator = Vector3.Dot(ray.direction, portal.OutwardNormal);
                if (denominator <= .00001f) continue;
                Vector3 planePoint = portal.LocalToWorld.MultiplyPoint3x4(Vector3.zero);
                float distance = Vector3.Dot(planePoint - ray.origin, portal.OutwardNormal) / denominator;
                if (distance <= 0f || distance >= pointDistance || !Contains(frame.KnownVolumes, ray.GetPoint(Mathf.Max(0f, distance - .025f)))) continue;
                if (!InAperture(portal, ray.GetPoint(distance))) continue;
                if (ColliderPathClear(camera, frame, ray, pointDistance, worldPoint, targetOwner)) return true;
            }
            return false;
        }

        static bool ColliderPathClear(Camera camera, RoomMaskFrame frame, Ray ray, float distance, Vector3 point, Transform targetOwner)
        {
            var physics = camera.gameObject.scene.GetPhysicsScene();
            if (!physics.IsValid()) return false;
            int count = physics.Raycast(ray.origin, ray.direction, hits, Mathf.Max(0f, distance - .015f), camera.cullingMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false; // 有界保守拒绝，不在密集物理场景分配RaycastAll数组。
            for (int i = 0; i < count; ++i)
            {
                var collider = hits[i].collider;
                if (collider == null) continue;
                if (frame.LabelRaycastExclusions != null && Array.IndexOf(frame.LabelRaycastExclusions, collider) >= 0) continue;
                if (targetOwner != null && (collider.transform == targetOwner || collider.transform.IsChildOf(targetOwner))) continue;
                // 标签锚点自身所处的目标Collider不算前景阻挡；不忽略其它forceOff/非登记物体。
                if (collider.bounds.Contains(point)) continue;
                return false;
            }
            return true;
        }

        static bool InAperture(RoomMaskPortal portal, Vector3 point)
        {
            var mesh = portal.ApertureMesh;
            if (!mesh.isReadable) return false;
            if (!apertures.TryGetValue(mesh, out var aperture))
            {
                aperture = new Aperture { Vertices = mesh.vertices, Triangles = mesh.triangles }; apertures.Add(mesh, aperture);
            }
            Vector3 local = portal.LocalToWorld.inverse.MultiplyPoint3x4(point);
            if (Mathf.Abs(local.z) > .02f) return false;
            Vector2 p = new Vector2(local.x, local.y);
            for (int i = 0; i + 2 < aperture.Triangles.Length; i += 3)
            {
                Vector2 a = aperture.Vertices[aperture.Triangles[i]], b = aperture.Vertices[aperture.Triangles[i + 1]], c = aperture.Vertices[aperture.Triangles[i + 2]];
                Vector2 u = b - a, v = c - a, w = p - a;
                float denominator = u.x * v.y - u.y * v.x;
                if (Mathf.Abs(denominator) < 1e-8f) continue;
                float s = (w.x * v.y - w.y * v.x) / denominator, t = (u.x * w.y - u.y * w.x) / denominator;
                if (s >= -.0001f && t >= -.0001f && s + t <= 1.0001f) return true;
            }
            return false;
        }

        static bool Contains(RoomMaskVolume[] volumes, Vector3 point)
        {
            if (volumes == null) return false;
            foreach (var volume in volumes)
            {
                var local = volume.WorldToLocal.MultiplyPoint3x4(point); var half = volume.HalfExtents;
                if (Mathf.Abs(local.x) <= half.x + .001f && Mathf.Abs(local.y) <= half.y + .001f && Mathf.Abs(local.z) <= half.z + .001f) return true;
            }
            return false;
        }
    }
}
