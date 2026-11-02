/* 功能：在唯一SceneVisibilityRoot调度下解析原主控建筑/房间；许可仅来自当前种子及有限门窗。
 * 边界：不拥有Current、位置、HP、物理、灯光或存档；不逐帧扫描全场，不订阅第二套相机回调。
 * 协作：纯MembershipResolver计算身份；作者组件提供范围/成员；RoomMaskRendererFeature消费明确相机帧。
 * 生命周期：Begin提交同栋顶盖、前墙与正向绘制的相机临时scope，End只归还scope；禁用/异常/卸载释放自己登记。
 */
using System;
using System.Collections.Generic;
using C = Ossastra.Characters;
using UnityEngine;
using Ossastra.SceneVisibility.Rooms.Core;
using Ossastra.SceneVisibility.Rooms.Rendering;

namespace Ossastra.SceneVisibility.Rooms
{
    [DisallowMultipleComponent]
    public sealed partial class RoomVisibilityModule : MonoBehaviour
    {
        [Header("作者登记；不根据名称、Layer或门户连通猜建筑")]
        public BuildingVisibilityRoot[] buildings = Array.Empty<BuildingVisibilityRoot>();
        [Tooltip("跨多个空间的原地表显示；许可遮罩按实际片元位置裁剪，不用Renderer中心分房。")]
        public Renderer[] sharedSurfaces = Array.Empty<Renderer>();
        [Header("独立显示需求；不暂停世界")]
        public bool effectEnabled = true;
        public bool revealRoofs = true;
        [Min(.1f), Tooltip("同一角色跨越此距离视作传送，绕过旧门槛滞回；不改变角色位置。")]
        public float immediateResolveDistance = 3f;

        public BuildingVisibilityRoot ActiveBuilding { get; private set; }
        public RoomRegion ActiveRoom { get; private set; }
        public int DiscoveredRoomCount => discovered.Count;
        public int SuppressedRendererCount => visualScope.Count;
        public double MembershipMilliseconds { get; private set; }
        public string Status { get; private set; } = "未初始化";
        public bool IsReady => initialized && owner != null && boundCamera != null;

        readonly HashSet<string> discovered = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<Renderer, RoomRegion> contentOwners = new Dictionary<Renderer, RoomRegion>();
        readonly HashSet<Renderer> permanentMembers = new HashSet<Renderer>();
        readonly HashSet<Renderer> roofMembers = new HashSet<Renderer>();
        readonly RoomRendererScope visualScope = new RoomRendererScope();
        readonly RoomMaskFrame frame = new RoomMaskFrame();
        readonly List<RoomMaskVolume> knownVolumeScratch = new List<RoomMaskVolume>(8);
        readonly List<Collider> labelRaycastExclusionScratch = new List<Collider>(16);
        readonly List<RoomRegion> regionScratch = new List<RoomRegion>(16);
        readonly List<BuildingVisibilityRoot> regionOwnerScratch = new List<BuildingVisibilityRoot>(16);
        SceneVisibilityRoot owner;
        Camera boundCamera;
        RoomMembershipResolver buildingResolver, roomResolver;
        RoomCandidate[] buildingCandidates = Array.Empty<RoomCandidate>(), roomCandidates = Array.Empty<RoomCandidate>();
        BuildingVisibilityRoot[] registeredBuildings = Array.Empty<BuildingVisibilityRoot>();
        RoomRegion[] registeredRooms = Array.Empty<RoomRegion>();
        BuildingVisibilityRoot[] roomBuildings = Array.Empty<BuildingVisibilityRoot>();
        string[] buildingIds = Array.Empty<string>(), roomIds = Array.Empty<string>();
        C.CharacterRoot lastActor;
        string lastActorId;
        Vector3 lastFoot;
        int lastLifeGeneration;
        bool initialized, globalEnabled, immediateRequested = true;
        BuildingVisibilityRoot statusBuilding;
        RoomRegion statusRoom;
        int statusDiscoveryCount = -1;

        public bool TryValidateConfiguration(SceneVisibilityRoot context, out string reason)
        {
            reason = null;
            if (context == null || context.gameplayCamera == null || context.coordinator == null)
            { reason = "房间模块缺少唯一Root相机/原控制协调器"; return false; }
            if (buildings == null || buildings.Length == 0)
            { reason = "房间模块没有登记建筑"; return false; }
            if (sharedSurfaces == null) { reason = "跨空间地面列表为null"; return false; }
            if (sightBlockers == null || sightBlockers.Length > 64 || !RoomRegion.Finite(observerHeight) || observerHeight <= 0f)
            { reason = "观察高度必须为正数；不透光Box列表不能为空或超过64项"; return false; }
            var buildingNames = new HashSet<string>(StringComparer.Ordinal);
            var roomNames = new HashSet<string>(StringComparer.Ordinal);
            var roofNames = new HashSet<string>(StringComparer.Ordinal);
            var portalNames = new HashSet<string>(StringComparer.Ordinal);
            var allRoofs = new HashSet<Renderer>();
            var allStructure = new HashSet<Renderer>();
            var allContent = new HashSet<Renderer>();
            foreach (var building in buildings)
            {
                if (building == null || string.IsNullOrWhiteSpace(building.buildingId) || !buildingNames.Add(building.buildingId))
                { reason = "建筑引用/ID为空或重复"; return false; }
                if (!RoomRegion.ValidateBoxes(building.transform, building.occupancyVolumes, out reason))
                { reason = building.buildingId + "：" + reason; return false; }
                if (building.occupancyVolumes.Length > 8 || !MarginsValid(building.enterInset, building.exitMargin))
                { reason = building.buildingId + "：建筑体积超过8盒或入退容差无效"; return false; }
                if (building.regions == null || building.regions.Length == 0 || building.roofGroups == null ||
                    building.roofGroups.Length == 0 || building.structuralMembers == null || building.structuralMembers.Length == 0)
                { reason = building.buildingId + "：需要明确房间、全部顶盖和永久结构"; return false; }
                var ownRooms = new HashSet<RoomRegion>(); int totalRoomBoxes = 0;
                foreach (var region in building.regions)
                {
                    if (region == null || !ownRooms.Add(region) || string.IsNullOrWhiteSpace(region.roomId) ||
                        !roomNames.Add(region.roomId) || string.IsNullOrWhiteSpace(region.floorId))
                    { reason = building.buildingId + "：房间引用/ID/楼层为空或重复"; return false; }
                    if (!RoomRegion.ValidateBoxes(region.transform, region.volumes, out reason))
                    { reason = region.roomId + "：" + reason; return false; }
                    totalRoomBoxes += region.volumes.Length;
                    if (totalRoomBoxes > 8 || !MarginsValid(region.enterInset, region.exitMargin))
                    { reason = building.buildingId + "：房间并集超过8盒或入退容差无效"; return false; }
                    if (region.members == null) { reason = region.roomId + "：内容列表为null"; return false; }
                    foreach (var member in region.members)
                        if (member == null || !allContent.Add(member))
                        { reason = region.roomId + "：内容成员为空/跨房间重复；须编辑期分件"; return false; }
                }
                foreach (var roof in building.roofGroups)
                {
                    if (roof == null || string.IsNullOrWhiteSpace(roof.roofGroupId) || !roofNames.Add(roof.roofGroupId) ||
                        roof.members == null || roof.members.Length == 0)
                    { reason = building.buildingId + "：顶盖组为空或ID重复"; return false; }
                    foreach (var member in roof.members)
                        if (member == null || !allRoofs.Add(member))
                        { reason = roof.roofGroupId + "：顶盖为空或跨组/建筑重复归属"; return false; }
                    if (roof.labelRaycastExclusions == null)
                    { reason = roof.roofGroupId + "：标签屋顶射线排除列表为null"; return false; }
                    foreach (var collider in roof.labelRaycastExclusions)
                        if (collider == null || collider.gameObject.scene != building.gameObject.scene)
                        { reason = roof.roofGroupId + "：屋顶标签排除Collider为空或不属于本作者场"; return false; }
                }
                foreach (var member in building.structuralMembers)
                    if (member == null) { reason = building.buildingId + "：永久结构引用为空"; return false; }
                    else if (!allStructure.Add(member)) { reason = building.buildingId + "：结构成员跨建筑重复登记"; return false; }
                if (building.frontWalls == null) { reason = building.buildingId + "：前墙列表为null"; return false; }
                var wallIds = new HashSet<string>(StringComparer.Ordinal); var wallMembers = new HashSet<Renderer>();
                foreach (var wall in building.frontWalls)
                {
                    if (wall == null || string.IsNullOrWhiteSpace(wall.wallId) || !wallIds.Add(wall.wallId) ||
                        wall.primaryRoom == null || !ownRooms.Contains(wall.primaryRoom) ||
                        (wall.secondaryRoom != null && !ownRooms.Contains(wall.secondaryRoom)) ||
                        !RoomRegion.Finite(wall.OutwardNormal) || wall.OutwardNormal.sqrMagnitude < .001f || wall.members == null || wall.members.Length == 0)
                    { reason = building.buildingId + "：前墙段身份、所属区域或法线无效"; return false; }
                    foreach (var member in wall.members)
                        if (member == null || Array.IndexOf(building.structuralMembers, member) < 0 || !wallMembers.Add(member))
                        { reason = wall.wallId + "：前墙成员不属于本栋结构或重复登记"; return false; }
                }
                if (building.portals == null) { reason = building.buildingId + "：门户列表为null"; return false; }
                foreach (var portal in building.portals)
                    if (portal == null || string.IsNullOrWhiteSpace(portal.portalId) || !portalNames.Add(portal.portalId) ||
                        portal.inside == null || !ownRooms.Contains(portal.inside) ||
                        (portal.outside != null && !ownRooms.Contains(portal.outside)) ||
                        portal.apertureMesh == null || !portal.apertureMesh.isReadable || portal.apertureMesh.vertexCount < 3 ||
                        !RoomRegion.Finite(portal.transform.lossyScale))
                    { reason = building.buildingId + "：门户ID/所属区域/真实孔洞引用无效"; return false; }
                foreach (var portal in building.portals)
                {
                    Vector3[] polygon = portal.GetWorldAperture(); var points = new SightVector3[polygon.Length];
                    for (int i = 0; i < points.Length; i++) points[i] = new SightVector3(polygon[i].x, polygon[i].y, polygon[i].z);
                    if (polygon.Length < 3 || polygon.Length > 7 || !RoomRegion.Finite(portal.observerRange) || portal.observerRange <= 0f ||
                        !RoomRegion.Finite(portal.maxSightDistance) || portal.maxSightDistance <= 0f ||
                        !PortalSightGeometry.ValidatePolygon(points, new SightVector3(portal.OutwardNormal.x, portal.OutwardNormal.y, portal.OutwardNormal.z), out var ignoredCenter))
                    { reason = portal.portalId + "：需要3—7边真实凸孔洞及正的观察/延伸距离；不取填充凸包"; return false; }
                }
            }
            foreach (var member in allRoofs)
                if (allStructure.Contains(member) || allContent.Contains(member) || Array.IndexOf(sharedSurfaces, member) >= 0)
                { reason = "同一Renderer混合了顶盖和永久结构/内容/承载面，拒绝揭顶"; return false; }
            foreach (var member in allContent)
                if (allStructure.Contains(member) || Array.IndexOf(sharedSurfaces, member) >= 0)
                { reason = "区域内容与永久结构/跨空间承载面混用同一Renderer"; return false; }
            foreach (var building in buildings) foreach (var roof in building.roofGroups)
                foreach (var collider in roof.labelRaycastExclusions)
                {
                    foreach (var structure in allStructure)
                        if (collider.transform == structure.transform || collider.transform.IsChildOf(structure.transform))
                        { reason = roof.roofGroupId + "：永久墙/结构Collider不能作为揭顶标签射线排除"; return false; }
                    foreach (var content in allContent)
                        if (collider.transform == content.transform || collider.transform.IsChildOf(content.transform))
                        { reason = roof.roofGroupId + "：室内内容Collider不能作为揭顶标签射线排除"; return false; }
                }
            if (sharedSurfaces == null) { reason = "跨空间地面列表为null"; return false; }
            foreach (var member in sharedSurfaces)
                if (member == null) { reason = "跨空间地面引用为空"; return false; }
            // 只在登记/显式检查时遍历本建筑子树；不按名字或朝上法线猜屋顶，也不逐帧查全场。
            // 外部原工位/敌人可由members显式引用，反向无需改它们的业务层级。
            foreach (var building in buildings)
                foreach (var member in building.GetComponentsInChildren<Renderer>(true))
                    if (!allRoofs.Contains(member) && !allStructure.Contains(member) && !allContent.Contains(member) &&
                        Array.IndexOf(sharedSurfaces, member) < 0)
                    { reason = building.buildingId + "：存在未登记分类的Renderer：" + member.name; return false; }
            if (context.groups != null) foreach (var group in context.groups)
            {
                if (group == null) continue;
                if (group.members == null) { reason = "局部物件组成员列表为null"; return false; }
                foreach (var member in group.members)
                {
                    if (member != null && allRoofs.Contains(member))
                    { reason = "同一Renderer同时登记顶盖和局部物件组，必须解除重复所有权"; return false; }
                    if (member != null && group.AllowsCharacterReveal && (allStructure.Contains(member) || Array.IndexOf(sharedSurfaces, member) >= 0))
                    { reason = "永久结构/承载面不能许可角色透视"; return false; }
                }
            }
            return true;
        }

        public void Initialize(SceneVisibilityRoot context)
        {
            if (owner != null && owner != context) throw new InvalidOperationException("房间模块已有另一Root所有者");
            if (!TryValidateConfiguration(context, out string reason)) throw new InvalidOperationException(reason);
            ReleaseOwnedState();
            owner = context; boundCamera = context.gameplayCamera;
            registeredBuildings = (BuildingVisibilityRoot[])buildings.Clone();
            buildingIds = new string[registeredBuildings.Length]; buildingCandidates = new RoomCandidate[registeredBuildings.Length];
            regionScratch.Clear(); regionOwnerScratch.Clear(); permanentMembers.Clear(); roofMembers.Clear(); contentOwners.Clear();
            for (int i = 0; i < registeredBuildings.Length; ++i)
            {
                var building = registeredBuildings[i]; buildingIds[i] = building.buildingId;
                foreach (var region in building.regions)
                {
                    regionScratch.Add(region); regionOwnerScratch.Add(building);
                    foreach (var member in region.members) contentOwners.Add(member, region);
                }
                foreach (var member in building.structuralMembers) permanentMembers.Add(member);
                foreach (var roof in building.roofGroups) foreach (var member in roof.members) roofMembers.Add(member);
            }
            registeredRooms = regionScratch.ToArray(); roomBuildings = regionOwnerScratch.ToArray();
            roomIds = new string[registeredRooms.Length]; roomCandidates = new RoomCandidate[registeredRooms.Length];
            for (int i = 0; i < registeredRooms.Length; ++i) roomIds[i] = registeredRooms[i].roomId;
            if (buildingResolver == null) buildingResolver = new RoomMembershipResolver(buildingIds);
            else buildingResolver.SetRegisteredRooms(buildingIds);
            if (roomResolver == null) roomResolver = new RoomMembershipResolver(roomIds);
            else roomResolver.SetRegisteredRooms(roomIds);
            discovered.RemoveWhere(id => Array.IndexOf(roomIds, id) < 0);
            InitializeSightRegistration();
            initialized = true; immediateRequested = true; Status = "已登记；等待主控空间采样";
            // 首次Canvas标签创建前公布本相机使用可选房间策略；此时Enabled=false，不创建GPU遮罩。
            // UI可从一开始绑定专用材质，避免入室首帧晚于Canvas重建才切换材质。
            PublishDisabled();
        }

        /// <summary>唯一Root在原有限捕获之前调用；不会自己订阅渲染事件或推进玩法。</summary>
        public void BeginCamera(SceneVisibilityRoot context, Camera camera, bool enabled)
        {
            if (context == null || camera == null || camera != context.gameplayCamera) return;
            if (!initialized || owner != context || boundCamera != camera) Initialize(context);
            visualScope.Dispose();
            positiveScope.Dispose();
            globalEnabled = enabled && effectEnabled && isActiveAndEnabled;
            if (!globalEnabled || !positiveRenderingEnabled) { globalEnabled = false; PublishDisabled(); return; }
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            ResolveMembership();
            MembershipMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            try { BuildPositiveFrame(); }
            catch (Exception error) { PublishRejectedFrame(error.Message); return; }
            // 同栋所有顶组在同一快照内先提交，不能按房间或预算逐帧交换屋顶。
            if (frame.PositiveRenderingEnabled && ActiveBuilding != null && revealRoofs)
                foreach (var roof in ActiveBuilding.roofGroups) if (roof != null)
                    foreach (var member in roof.members) visualScope.Suppress(member);
            ApplyPositiveRegistration();
            RoomMaskCameraRegistry.Publish(this, camera, frame);
            if (statusBuilding != ActiveBuilding || statusRoom != ActiveRoom || statusDiscoveryCount != discovered.Count)
            {
                Status = ActiveBuilding == null ? "室外；整顶保留，门窗只给有限预览" :
                    "建筑 " + ActiveBuilding.buildingId + " / 区域 " + (ActiveRoom == null ? "过渡范围" : ActiveRoom.roomId) +
                    " / 历史到达（无显示权） " + discovered.Count;
                statusBuilding = ActiveBuilding; statusRoom = ActiveRoom; statusDiscoveryCount = discovered.Count;
            }
        }

        public void EndCamera(SceneVisibilityRoot context, Camera camera)
        {
            if (context != owner || camera != boundCamera) return;
            try { visualScope.Dispose(); positiveScope.Dispose(); }
            finally { RoomMaskCameraRegistry.EndScope(this, camera); }
        }

        public void SuspendCamera(SceneVisibilityRoot context)
        {
            if (context != owner) return;
            visualScope.Dispose(); positiveScope.Dispose(); globalEnabled = false;
            if (isActiveAndEnabled) PublishDisabled();
            else ReleaseOwnedState();
        }

        public void Release(SceneVisibilityRoot context)
        {
            if (owner != null && context != owner) return;
            ReleaseOwnedState(); owner = null; boundCamera = null; initialized = false; globalEnabled = false;
        }

        public void RequestImmediateMembershipRefresh() => immediateRequested = true;

        /// <summary>
        /// 开发面板重置本模块运行会话的到达记录；不改角色位置、Current或真实存档。
        /// 下一次正式采样仍会按规则将主控实际所在区域记为已到达。
        /// </summary>
        public void ResetSessionDiscovery()
        {
            discovered.Clear(); immediateRequested = true; statusDiscoveryCount = -1;
        }

        public bool IsDiscovered(RoomRegion region) => region != null && discovered.Contains(region.roomId);
        public bool ContainsActiveKnownPoint(Vector3 world)
        {
            return ActiveRoom != null && ActiveRoom.isActiveAndEnabled && ActiveRoom.Contains(world);
        }

        public bool CanRevealTarget(VisibilityTarget target)
        {
            if (!globalEnabled || ActiveBuilding == null) return true;
            if (target == null || target.actor == null) return false;
            var room = RoomForActor(target.actor);
            return room != null && room == ActiveRoom;
        }

        public bool AllowsLocalGroup(SceneOccluderGroup group)
        {
            if (group == null) return false;
            if (!globalEnabled) return true;
            if (!localObjectRevealEnabled) return false;
            foreach (var member in group.Resolved)
            {
                if (member == null) continue;
                if (roofMembers.Contains(member) || permanentMembers.Contains(member) || Array.IndexOf(sharedSurfaces, member) >= 0) return false;
                if (contentOwners.TryGetValue(member, out var room) && room != ActiveRoom) return false;
            }
            return true;
        }

        /// <summary>供世界标签作空间许可粗筛；最终像素边界仍由同相机mask控制，不授予攻击/交互LOS。</summary>
        public bool AllowsWorldPoint(Vector3 world)
        {
            return !globalEnabled || AllowsSightPoint(world);
        }

        void ResolveMembership()
        {
            var actor = owner.coordinator.Current;
            bool living = actor != null && actor.IsAlive && actor.isActiveAndEnabled;
            bool immediate = immediateRequested;
            Vector3 foot = living ? actor.transform.position : lastFoot;
            if (living)
            {
                if (actor != lastActor || actor.ActorId != lastActorId || actor.LifeGeneration != lastLifeGeneration ||
                    (actor == lastActor && (foot - lastFoot).sqrMagnitude > immediateResolveDistance * immediateResolveDistance)) immediate = true;
                if (actor != lastActor || actor.ActorId != lastActorId) lastActorId = actor.ActorId;
                lastActor = actor; lastLifeGeneration = actor.LifeGeneration; lastFoot = foot;
            }
            for (int i = 0; i < registeredBuildings.Length; ++i)
            {
                var building = registeredBuildings[i]; bool available = building != null && building.isActiveAndEnabled;
                buildingCandidates[i] = new RoomCandidate(buildingIds[i], 0,
                    available && building.Contains(foot), available && building.Contains(foot, -building.enterInset),
                    available && building.Contains(foot, building.exitMargin));
            }
            string buildingId = buildingResolver.Resolve(lastActorId, living, buildingCandidates, immediate);
            ActiveBuilding = null;
            for (int i = 0; i < registeredBuildings.Length; ++i)
                if (buildingIds[i] == buildingId && registeredBuildings[i] != null && registeredBuildings[i].isActiveAndEnabled)
                { ActiveBuilding = registeredBuildings[i]; break; }
            for (int i = 0; i < registeredRooms.Length; ++i)
            {
                var room = registeredRooms[i]; bool available = room != null && room.isActiveAndEnabled && roomBuildings[i] == ActiveBuilding;
                roomCandidates[i] = new RoomCandidate(roomIds[i], room == null ? 0 : room.priority,
                    available && room.Contains(foot), available && room.Contains(foot, -room.enterInset),
                    available && room.Contains(foot, room.exitMargin));
            }
            string roomId = roomResolver.Resolve(lastActorId, living, roomCandidates, immediate);
            ActiveRoom = null;
            for (int i = 0; i < registeredRooms.Length; ++i)
                if (roomIds[i] == roomId && registeredRooms[i] != null && registeredRooms[i].isActiveAndEnabled && roomBuildings[i] == ActiveBuilding)
                { ActiveRoom = registeredRooms[i]; break; }
            // 只有主控真正进入才记到达；门窗观察、伙伴AI、滞回保留本身都不能解锁新区域。
            if (living && ActiveRoom != null && ActiveRoom.Contains(foot)) discovered.Add(ActiveRoom.roomId);
            immediateRequested = false;
        }

        RoomRegion RoomForActor(C.CharacterRoot actor)
        {
            if (actor == null) return null;
            if (owner != null && owner.coordinator.Current == actor && ActiveRoom != null) return ActiveRoom;
            return RoomAt(actor.transform.position, null);
        }
        RoomRegion RoomAt(Vector3 point, BuildingVisibilityRoot onlyBuilding)
        {
            RoomRegion best = null;
            for (int i = 0; i < registeredRooms.Length; ++i)
            {
                var region = registeredRooms[i];
                if (region == null || !region.isActiveAndEnabled || (onlyBuilding != null && roomBuildings[i] != onlyBuilding) || !region.Contains(point)) continue;
                if (best == null || region.priority > best.priority || (region.priority == best.priority && string.CompareOrdinal(region.roomId, best.roomId) < 0)) best = region;
            }
            return best;
        }
        static bool MarginsValid(float enter, float exit) => RoomRegion.Finite(enter) && RoomRegion.Finite(exit) && enter >= 0f && exit >= 0f;
        void PublishDisabled()
        {
            frame.Enabled = false;
            frame.PositiveRenderingEnabled = false;
            if (boundCamera != null) RoomMaskCameraRegistry.Publish(this, boundCamera, frame);
        }
        void ReleaseOwnedState()
        {
            visualScope.Dispose();
            positiveScope.Dispose();
            // Camera可能先于模块被Unity销毁；保留其托管包装器以撤销同一InstanceID的自有登记。
            if (!ReferenceEquals(boundCamera, null)) RoomMaskCameraRegistry.Release(this, boundCamera);
        }
        void OnDisable() { ReleaseOwnedState(); globalEnabled = false; }
        void OnDestroy() { ReleaseOwnedState(); initialized = false; owner = null; boundCamera = null; discovered.Clear(); }
    }
}
