using UnityEngine;

namespace Ossastra.SceneVisibility
{
    // 一个显式登记组的观测结果；支撑身份、目标优先级和组预算由Root处理。
    public readonly struct VisibilityProbeResult
    {
        public bool Eligible => BlockedPoints > 0;
        public float Coverage => TestedPoints > 0 ? (float)BlockedPoints / TestedPoints : 0f;
        public int TestedPoints { get; }
        public int BlockedPoints { get; }
        // 统计实际Collider.Raycast和ClosestPoint调用总数，不伪称只有射线开销。
        public int Queries { get; }
        // 以下两项按“有效参考点×代理”计数，不等于不同组的数量。
        public int InsideProxy { get; }
        public int UnsupportedInside { get; }
        public int RejectedPoints { get; }
        public bool InvalidCamera { get; }

        internal VisibilityProbeResult(int tested, int blocked, int queries, int inside,
            int unsupported, int rejected, bool invalidCamera)
        {
            TestedPoints = tested; BlockedPoints = blocked; Queries = queries;
            InsideProxy = inside; UnsupportedInside = unsupported;
            RejectedPoints = rejected; InvalidCamera = invalidCamera;
        }
    }

    // 只查询调用者显式给出的组内代理，不扫全场、不读取已经淡出的颜色/深度。
    // Root对每个组独立调用，因此较近的其他组不会吞掉本组命中；没有NonAlloc命中缓冲。
    public static class VisibilityProbe
    {
        const float SurfaceEpsilon = .0001f;

        public static VisibilityProbeResult Evaluate(Camera camera, Vector3[] stableWorldPoints,
            Collider[] registeredVolumes, Transform ignoredActor)
        {
            int pointCount = stableWorldPoints == null ? 0 : stableWorldPoints.Length;
            if (!ValidCamera(camera)) return new VisibilityProbeResult(0, 0, 0, 0, 0, pointCount, true);
            int tested = 0, blocked = 0, queries = 0, inside = 0, unsupported = 0, rejected = 0;
            for (int pointIndex = 0; pointIndex < pointCount; ++pointIndex)
            {
                Vector3 point = stableWorldPoints[pointIndex];
                if (!Finite(point)) { ++rejected; continue; }
                Vector3 viewport = camera.WorldToViewportPoint(point);
                if (!Finite(viewport) || viewport.z <= camera.nearClipPlane || viewport.z >= camera.farClipPlane ||
                    viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                { ++rejected; continue; }

                // 必须逐参考点反投影；正交时各射线平行，不能改为从Camera Transform汇聚。
                Ray ray = camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
                if (!Finite(ray.origin) || !Finite(ray.direction) || ray.direction.sqrMagnitude <= 0f)
                { ++rejected; continue; }
                ray.direction = ray.direction.normalized;
                float distance = Vector3.Dot(point - ray.origin, ray.direction);
                float maximumDistance = distance - SurfaceEpsilon;
                if (!Finite(maximumDistance) || maximumDistance <= 0f) { ++rejected; continue; }
                ++tested;
                bool pointBlocked = false;
                if (registeredVolumes != null)
                {
                    for (int volumeIndex = 0; volumeIndex < registeredVolumes.Length; ++volumeIndex)
                    {
                        Collider volume = registeredVolumes[volumeIndex];
                        if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy ||
                            (ignoredActor != null && volume.transform.IsChildOf(ignoredActor))) continue;
                        // 显式登记的query-only Trigger同样参与；不修改全局queriesHitTriggers。
                        bool duplicate = false;
                        for (int prior = 0; prior < volumeIndex; ++prior)
                            if (registeredVolumes[prior] == volume) { duplicate = true; break; }
                        if (duplicate) continue;
                        if (volume is MeshCollider emptyMesh && emptyMesh.sharedMesh == null) continue;
                        Bounds bounds = volume.bounds;
                        if (!Finite(bounds.center) || !Finite(bounds.extents) || bounds.extents.sqrMagnitude <= 0f) continue;

                        if (SupportsInside(volume))
                        {
                            ++queries;
                            Vector3 closest = volume.ClosestPoint(ray.origin);
                            if (Finite(closest) && (closest - ray.origin).sqrMagnitude <= SurfaceEpsilon * SurfaceEpsilon)
                            {
                                // 这些支持形体的ClosestPoint在内部返回原点，补足无入口面的Raycast情形。
                                ++inside; pointBlocked = true;
                                continue;
                            }
                        }
                        else if (bounds.Contains(ray.origin))
                        {
                            // 非凸/未知形体的Bounds只提示内部判定不确定，不作为遮挡成立依据。
                            ++unsupported;
                        }

                        ++queries;
                        if (volume.Raycast(ray, out RaycastHit hit, maximumDistance) &&
                            Finite(hit.distance) && hit.distance >= 0f && hit.distance <= maximumDistance)
                            pointBlocked = true;
                        // 仍继续查询本组其余代理，不依赖全场最近命中或未定义的命中排序。
                    }
                }
                if (pointBlocked) ++blocked;
            }
            return new VisibilityProbeResult(tested, blocked, queries, inside, unsupported, rejected, false);
        }

        static bool SupportsInside(Collider volume) => volume is BoxCollider || volume is SphereCollider ||
            volume is CapsuleCollider || (volume is MeshCollider mesh && mesh.convex);

        static bool ValidCamera(Camera camera)
        {
            if (camera == null || !Finite(camera.nearClipPlane) || !Finite(camera.farClipPlane) ||
                camera.nearClipPlane <= 0f || camera.farClipPlane <= camera.nearClipPlane ||
                !Finite(camera.aspect) || camera.aspect <= 0f) return false;
            Rect rect = camera.rect;
            if (!Finite(rect.x) || !Finite(rect.y) || !Finite(rect.width) || !Finite(rect.height) || rect.width <= 0f || rect.height <= 0f)
                return false;
            Matrix4x4 view = camera.worldToCameraMatrix, projection = camera.projectionMatrix;
            for (int index = 0; index < 16; ++index)
                if (!Finite(view[index]) || !Finite(projection[index])) return false;
            // 不要求enabled：可用禁用的自有相机作投影合同，不会改变任何正式相机。
            return true;
        }
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
