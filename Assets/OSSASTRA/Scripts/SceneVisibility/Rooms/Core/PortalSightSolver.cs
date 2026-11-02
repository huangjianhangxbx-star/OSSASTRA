/*
 * 功能：从实际种子空间求解双向门窗的第一跳有限视域，不将邻室变为完整可见房间。
 * 边界：通行、揭顶、前墙、照明与渲染各自决策；本模块仅输出合法观察半空间。
 * 生命周期：输入与输出作只读快照；不持有场景引用，不维护跨帧传播或探索历史。
 */
using System;
using System.Collections.Generic;

namespace Ossastra.SceneVisibility.Rooms.Core
{
    public sealed class PortalSightSnapshot
    {
        public string Id { get; }
        public string SideA { get; }
        public string SideB { get; }
        public IReadOnlyList<SightVector3> Aperture { get; }
        public SightVector3 NormalAToB { get; }
        public bool IsSightOpen { get; }
        public float ObserverRange { get; }
        public float MaxSightDistance { get; }
        public PortalSightSnapshot(string id, string sideA, string sideB, SightVector3[] aperture,
            SightVector3 normalAToB, bool isSightOpen, float observerRange, float maxSightDistance)
        {
            Id = id; SideA = ObserverSnapshot.NormalizeSpace(sideA); SideB = ObserverSnapshot.NormalizeSpace(sideB);
            Aperture = Array.AsReadOnly(aperture == null ? new SightVector3[0] : (SightVector3[])aperture.Clone());
            NormalAToB = normalAToB; IsSightOpen = isSightOpen; ObserverRange = observerRange; MaxSightDistance = maxSightDistance;
        }
    }

    public sealed class PortalSightVolume
    {
        public string PortalId { get; }
        public string SourceSpaceId { get; }
        public string TargetSpaceId { get; }
        public ObserverSnapshot Observer { get; }
        public IReadOnlyList<SightVector3> Aperture { get; }
        public SightVector3 TravelNormal { get; }
        public float MaxSightDistance { get; }
        public IReadOnlyList<SightPlane> ClipPlanes { get; }
        internal PortalSightVolume(PortalSightSnapshot portal, ObserverSnapshot observer, string target, SightVector3 travelNormal, SightPlane[] planes)
        {
            PortalId = portal.Id; SourceSpaceId = observer.SpaceId; TargetSpaceId = target; Observer = observer;
            Aperture = portal.Aperture; TravelNormal = travelNormal; MaxSightDistance = portal.MaxSightDistance;
            ClipPlanes = Array.AsReadOnly(planes);
        }
        public bool ContainsPoint(SightVector3 point)
        {
            if (!point.IsFinite) return false;
            for (int i = 0; i < ClipPlanes.Count; i++)
                if (ClipPlanes[i].SignedDistance(point) < 0) return false;
            return true;
        }
    }

    public sealed class PortalSightResult
    {
        public string FullSeedRoomId { get; }
        public IReadOnlyList<PortalSightVolume> Volumes { get; }
        internal PortalSightResult(string seed, PortalSightVolume[] volumes)
        {
            FullSeedRoomId = seed; Volumes = Array.AsReadOnly(volumes);
        }
    }

    public static class PortalSightSolver
    {
        public static PortalSightResult Solve(ObserverSnapshot observer, IReadOnlyList<PortalSightSnapshot> portals)
        {
            string seed = !observer.Position.IsFinite || string.IsNullOrEmpty(observer.SpaceId) || observer.SpaceId == ObserverSnapshot.Exterior ? null : observer.SpaceId;
            var volumes = new List<PortalSightVolume>();
            if (portals == null || !observer.Position.IsFinite || !observer.Forward.IsFinite || observer.Forward.Length < PortalSightGeometry.Epsilon)
                return new PortalSightResult(seed, volumes.ToArray());

            // 只读取本帧原始 observer.SpaceId；从未把已生成的 Partial 目标入队。
            for (int i = 0; i < portals.Count; i++)
            {
                PortalSightSnapshot portal = portals[i];
                if (portal == null || string.IsNullOrWhiteSpace(portal.Id) || !portal.IsSightOpen || portal.SideA == portal.SideB) continue;
                if (!SightVector3.Finite(portal.ObserverRange) || portal.ObserverRange < 0 ||
                    !SightVector3.Finite(portal.MaxSightDistance) || portal.MaxSightDistance <= PortalSightGeometry.Epsilon) continue;
                bool fromA = observer.SpaceId == portal.SideA;
                if (!fromA && observer.SpaceId != portal.SideB) continue;
                if (!PortalSightGeometry.ValidatePolygon(portal.Aperture, portal.NormalAToB, out SightVector3 center)) continue;
                SightVector3 travel = portal.NormalAToB.Normalized * (fromA ? 1 : -1);
                if (SightVector3.Dot(observer.Position - center, travel) >= -PortalSightGeometry.Epsilon) continue;
                if (PortalSightGeometry.DistanceToPolygon(observer.Position, portal.Aperture, travel) > portal.ObserverRange + PortalSightGeometry.Epsilon) continue;

                SightVector3 forward = observer.Forward.Normalized;
                bool hasForwardOpening = false;
                for (int vertex = 0; vertex < portal.Aperture.Count; vertex++)
                    hasForwardOpening |= SightVector3.Dot(portal.Aperture[vertex] - observer.Position, forward) > PortalSightGeometry.Epsilon;
                if (!hasForwardOpening) continue;

                var planes = new SightPlane[portal.Aperture.Count + 3];
                bool valid = true;
                for (int edge = 0; edge < portal.Aperture.Count; edge++)
                {
                    SightVector3 a = portal.Aperture[edge] - observer.Position;
                    SightVector3 b = portal.Aperture[(edge + 1) % portal.Aperture.Count] - observer.Position;
                    SightVector3 normal = SightVector3.Cross(a, b).Normalized;
                    if (normal.Length < PortalSightGeometry.Epsilon) { valid = false; break; }
                    if (SightVector3.Dot(normal, center - observer.Position) < 0) normal = -normal;
                    planes[edge] = new SightPlane(normal, -SightVector3.Dot(normal, observer.Position));
                }
                if (!valid) continue;
                // 微小正近面拒绝开口面本身，避免源侧与目标侧在同一像素同时授权。
                int planeIndex = portal.Aperture.Count;
                planes[planeIndex] = new SightPlane(travel, -SightVector3.Dot(travel, center) - PortalSightGeometry.Epsilon);
                planes[planeIndex + 1] = new SightPlane(-travel, SightVector3.Dot(travel, center) + portal.MaxSightDistance);
                planes[planeIndex + 2] = new SightPlane(forward, -SightVector3.Dot(forward, observer.Position));
                volumes.Add(new PortalSightVolume(portal, observer, fromA ? portal.SideB : portal.SideA, travel, planes));
            }
            return new PortalSightResult(seed, volumes.ToArray());
        }
    }
}
