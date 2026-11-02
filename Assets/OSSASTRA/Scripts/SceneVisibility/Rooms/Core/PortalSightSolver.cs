/*
 * 功能：从实际种子空间求解双向门窗首跳视域，并独立求解只投向室外的相机窗景几何。
 * 边界：人物自然/有限策略与相机窗景分开；相机结果不授予房间种子、业务许可或邻室传播。
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
        internal PortalSightVolume(PortalSightSnapshot portal, ObserverSnapshot observer, string target, SightVector3 travelNormal, SightPlane[] planes,
            float? maxSightDistance = null)
        {
            PortalId = portal.Id; SourceSpaceId = observer.SpaceId; TargetSpaceId = target; Observer = observer;
            Aperture = portal.Aperture; TravelNormal = travelNormal; MaxSightDistance = maxSightDistance ?? portal.MaxSightDistance;
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
        /// <summary>历史二参合同：继续使用每扇门窗的靠近范围和窗后远面。</summary>
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

        /// <summary>人物首跳视域；策略只影响距离，不扩大真实孔洞、当前种子或前半空间。</summary>
        public static PortalSightResult Solve(ObserverSnapshot observer, IReadOnlyList<PortalSightSnapshot> portals, PortalSightPolicy policy)
        {
            if (policy.UsesLegacyPortalLimits) return Solve(observer, portals);
            string seed = !observer.Position.IsFinite || string.IsNullOrEmpty(observer.SpaceId) || observer.SpaceId == ObserverSnapshot.Exterior ? null : observer.SpaceId;
            var volumes = new List<PortalSightVolume>();
            if (portals == null || !observer.Position.IsFinite || !observer.Forward.IsFinite || observer.Forward.Length < PortalSightGeometry.Epsilon)
                return new PortalSightResult(seed, volumes.ToArray());

            SightVector3 forward = observer.Forward.Normalized;
            for (int i = 0; i < portals.Count; i++)
            {
                PortalSightSnapshot portal = portals[i];
                if (portal == null || string.IsNullOrWhiteSpace(portal.Id) || !portal.IsSightOpen || portal.SideA == portal.SideB) continue;
                bool fromA = observer.SpaceId == portal.SideA;
                if (!fromA && observer.SpaceId != portal.SideB) continue;
                SightVector3 travel = portal.NormalAToB.Normalized * (fromA ? 1 : -1);
                if (!PortalApertureCone.TryBuild(observer.Position, portal.Aperture, travel, out SightPlane[] aperturePlanes)) continue;
                if (policy.HasFiniteLimits && PortalSightGeometry.DistanceToPolygon(observer.Position, portal.Aperture, travel) > policy.ObserverRange + PortalSightGeometry.Epsilon) continue;

                bool hasForwardOpening = false;
                for (int vertex = 0; vertex < portal.Aperture.Count; vertex++)
                    hasForwardOpening |= SightVector3.Dot(portal.Aperture[vertex] - observer.Position, forward) > PortalSightGeometry.Epsilon;
                if (!hasForwardOpening) continue;

                var planes = new SightPlane[aperturePlanes.Length + (policy.HasFiniteLimits ? 2 : 1)];
                Array.Copy(aperturePlanes, planes, aperturePlanes.Length);
                int planeIndex = aperturePlanes.Length;
                if (policy.HasFiniteLimits)
                    planes[planeIndex++] = new SightPlane(-travel, SightVector3.Dot(travel, portal.Aperture[0]) + policy.MaxSightDistance);
                planes[planeIndex] = new SightPlane(forward, -SightVector3.Dot(forward, observer.Position));
                volumes.Add(new PortalSightVolume(portal, observer, fromA ? portal.SideB : portal.SideA, travel, planes,
                    policy.HasFiniteLimits ? policy.MaxSightDistance : float.PositiveInfinity));
            }
            return new PortalSightResult(seed, volumes.ToArray());
        }

        /// <summary>
        /// 当前房间开放外窗的相机孔洞锥；只供窗孔像素内的实际室外场景绘制，不能加入人物 AllowsPoint。
        /// 相机可以在房间体积之外，但须在该开口的室内半空间；结果永远没有完整房间种子。
        /// </summary>
        public static PortalSightResult SolveWindowScene(string observerSpaceId, SightVector3 cameraPosition,
            IReadOnlyList<PortalSightSnapshot> portals)
        {
            var volumes = new List<PortalSightVolume>();
            string source = ObserverSnapshot.NormalizeSpace(observerSpaceId);
            if (source == ObserverSnapshot.Exterior || !cameraPosition.IsFinite || portals == null)
                return new PortalSightResult(null, volumes.ToArray());

            for (int i = 0; i < portals.Count; i++)
            {
                PortalSightSnapshot portal = portals[i];
                if (portal == null || string.IsNullOrWhiteSpace(portal.Id) || !portal.IsSightOpen || portal.SideA == portal.SideB) continue;
                bool fromA = source == portal.SideA;
                if (!fromA && source != portal.SideB) continue;
                string target = fromA ? portal.SideB : portal.SideA;
                if (target != ObserverSnapshot.Exterior) continue;
                SightVector3 travel = portal.NormalAToB.Normalized * (fromA ? 1 : -1);
                if (!PortalApertureCone.TryBuild(cameraPosition, portal.Aperture, travel, out SightPlane[] planes)) continue;
                // Observer 仅承载该显示域的实际几何原点；FullSeedRoomId=null 防止成为人物观察权威。
                var cameraGeometry = new ObserverSnapshot(source, cameraPosition, travel);
                volumes.Add(new PortalSightVolume(portal, cameraGeometry, target, travel, planes, float.PositiveInfinity));
            }
            return new PortalSightResult(null, volumes.ToArray());
        }
    }
}
