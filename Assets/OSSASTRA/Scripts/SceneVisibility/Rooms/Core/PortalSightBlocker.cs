/*
 * 功能：以登记的不透明 OBB 判定观察点到候选片元的有限线段是否受阻。
 * 边界：不扫描 Physics；透明玻璃不得登记为 blocker，运行层负责准确采样实体轴和尺寸。
 * 生命周期：不可变值快照，无资源所有权。无效数据保守判为阻挡，由作者诊断显示。
 */
namespace Ossastra.SceneVisibility.Rooms.Core
{
    public readonly struct PortalSightBlocker
    {
        public SightVector3 Center { get; }
        public SightVector3 AxisX { get; }
        public SightVector3 AxisY { get; }
        public SightVector3 AxisZ { get; }
        public SightVector3 HalfExtents { get; }
        public bool IsValid => Center.IsFinite && HalfExtents.IsFinite && HalfExtents.X > 0 && HalfExtents.Y > 0 && HalfExtents.Z > 0 &&
            Unit(AxisX) && Unit(AxisY) && Unit(AxisZ) &&
            System.Math.Abs(SightVector3.Dot(AxisX, AxisY)) < .001f && System.Math.Abs(SightVector3.Dot(AxisX, AxisZ)) < .001f && System.Math.Abs(SightVector3.Dot(AxisY, AxisZ)) < .001f;
        public PortalSightBlocker(SightVector3 center, SightVector3 axisX, SightVector3 axisY, SightVector3 axisZ, SightVector3 halfExtents)
        { Center = center; AxisX = axisX; AxisY = axisY; AxisZ = axisZ; HalfExtents = halfExtents; }
        public bool OccludesSegment(SightVector3 from, SightVector3 to, float endpointBias = .02f)
        {
            if (!IsValid || !from.IsFinite || !to.IsFinite || !SightVector3.Finite(endpointBias) || endpointBias < 0) return true;
            SightVector3 delta = to - from;
            float length = delta.Length;
            if (length <= endpointBias || length < .000001f) return false;
            float enter = 0, exit = 1 - endpointBias / length;
            SightVector3 offset = from - Center;
            return IntersectAxis(SightVector3.Dot(offset, AxisX), SightVector3.Dot(delta, AxisX), HalfExtents.X, ref enter, ref exit) &&
                IntersectAxis(SightVector3.Dot(offset, AxisY), SightVector3.Dot(delta, AxisY), HalfExtents.Y, ref enter, ref exit) &&
                IntersectAxis(SightVector3.Dot(offset, AxisZ), SightVector3.Dot(delta, AxisZ), HalfExtents.Z, ref enter, ref exit) && exit >= enter;
        }
        private static bool Unit(SightVector3 axis) => axis.IsFinite && System.Math.Abs(SightVector3.Dot(axis, axis) - 1) < .001f;
        private static bool IntersectAxis(float origin, float delta, float halfExtent, ref float enter, ref float exit)
        {
            if (System.Math.Abs(delta) < .000001f) return System.Math.Abs(origin) <= halfExtent;
            float a = (-halfExtent - origin) / delta, b = (halfExtent - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = System.Math.Max(enter, a); exit = System.Math.Min(exit, b);
            return enter <= exit;
        }
    }
}
