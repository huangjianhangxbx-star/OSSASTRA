/*
 * 功能：按本帧实际相机朝向决定完整种子房间的前向结构墙段显示。
 * 边界：仅当前实际完整种子房间可请求其必要前墙段隐藏，包括共墙；不授予邻室内容可见权。
 * 生命周期：纯值决策；墙面/封口/阴影的恢复由运行显示 scope 负责。
 */
namespace Ossastra.SceneVisibility.Rooms.Core
{
    public readonly struct FrontWallSnapshot
    {
        public string Id { get; }
        public string PrimaryRoomId { get; }
        public string SecondaryRoomId { get; }
        public SightVector3 Center { get; }
        public SightVector3 NormalFromPrimary { get; }
        public bool AllowHide { get; }
        public FrontWallSnapshot(string id, string primaryRoomId, string secondaryRoomId, SightVector3 center, SightVector3 normalFromPrimary, bool allowHide)
        { Id = id; PrimaryRoomId = ObserverSnapshot.NormalizeSpace(primaryRoomId); SecondaryRoomId = ObserverSnapshot.NormalizeSpace(secondaryRoomId); Center = center; NormalFromPrimary = normalFromPrimary; AllowHide = allowHide; }
    }
    public static class FrontWallSolver
    {
        public static bool ShouldHide(FrontWallSnapshot wall, string fullSeedRoomId, SightVector3 cameraForward, float threshold = .05f)
        {
            if (!wall.AllowHide || string.IsNullOrWhiteSpace(wall.Id) || string.IsNullOrWhiteSpace(fullSeedRoomId) ||
                fullSeedRoomId == ObserverSnapshot.Exterior || !wall.Center.IsFinite || !cameraForward.IsFinite ||
                !wall.NormalFromPrimary.IsFinite || !SightVector3.Finite(threshold) || threshold < 0 || threshold >= 1) return false;
            bool primary = wall.PrimaryRoomId == fullSeedRoomId;
            if (!primary && wall.SecondaryRoomId != fullSeedRoomId) return false;
            SightVector3 normal = new SightVector3(wall.NormalFromPrimary.X, 0, wall.NormalFromPrimary.Z).Normalized;
            SightVector3 view = new SightVector3(cameraForward.X, 0, cameraForward.Z).Normalized;
            if (normal.Length < PortalSightGeometry.Epsilon || view.Length < PortalSightGeometry.Epsilon) return false;
            if (!primary) normal = -normal;
            return SightVector3.Dot(normal, view) < -threshold;
        }
    }
}
