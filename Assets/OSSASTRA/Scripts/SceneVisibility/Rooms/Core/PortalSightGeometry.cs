/*
 * 功能：为建筑开口视域提供与引擎无关的三维值、半空间及有限几何运算。
 * 边界：不读取相机、物理世界或角色，不持有 Unity 对象；单位由运行适配层统一为米。
 * 生命周期：全部为值类型，无场景资源、静态缓存或释放职责。
 */
using System;

namespace Ossastra.SceneVisibility.Rooms.Core
{
    public readonly struct SightVector3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public SightVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float Length => (float)Math.Sqrt(Dot(this, this));
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        public SightVector3 Normalized => Length > 1e-6f && IsFinite ? this * (1f / Length) : default;
        public static SightVector3 operator +(SightVector3 a, SightVector3 b) => new SightVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static SightVector3 operator -(SightVector3 a, SightVector3 b) => new SightVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static SightVector3 operator -(SightVector3 a) => a * -1;
        public static SightVector3 operator *(SightVector3 a, float scale) => new SightVector3(a.X * scale, a.Y * scale, a.Z * scale);
        public static float Dot(SightVector3 a, SightVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static SightVector3 Cross(SightVector3 a, SightVector3 b) => new SightVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>合法半空间为 Normal·point + Distance >= 0；Normal 必须为单位向量。</summary>
    public readonly struct SightPlane
    {
        public SightVector3 Normal { get; }
        public float Distance { get; }
        public SightPlane(SightVector3 normal, float distance) { Normal = normal; Distance = distance; }
        public float SignedDistance(SightVector3 point) => SightVector3.Dot(Normal, point) + Distance;
    }

    internal static class PortalSightGeometry
    {
        internal const float Epsilon = .0001f;

        internal static bool ValidatePolygon(System.Collections.Generic.IReadOnlyList<SightVector3> polygon, SightVector3 normal, out SightVector3 center)
        {
            center = default;
            if (polygon == null || polygon.Count < 3 || !normal.IsFinite || normal.Length < Epsilon) return false;
            normal = normal.Normalized;
            SightVector3 area = default;
            for (int i = 0; i < polygon.Count; i++)
            {
                SightVector3 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                if (!a.IsFinite || !b.IsFinite || (b - a).Length < Epsilon) return false;
                if (Math.Abs(SightVector3.Dot(a - polygon[0], normal)) > Epsilon) return false;
                // 相对第一个顶点求面积，避免远离原点时交叉项的大数抵消。
                area += SightVector3.Cross(a - polygon[0], b - polygon[0]);
                center += a;
            }
            float signedArea = SightVector3.Dot(area, normal);
            if (Math.Abs(signedArea) < Epsilon) return false;
            float winding = signedArea > 0 ? 1 : -1;
            for (int edge = 0; edge < polygon.Count; edge++)
            {
                SightVector3 a = polygon[edge], delta = polygon[(edge + 1) % polygon.Count] - a;
                for (int vertex = 0; vertex < polygon.Count; vertex++)
                {
                    float side = SightVector3.Dot(SightVector3.Cross(delta, polygon[vertex] - a), normal) * winding;
                    if (side < -Epsilon) return false; // 凹、多次绕转或自交不能偷偷取凸包。
                }
            }
            center *= 1f / polygon.Count;
            return center.IsFinite;
        }

        internal static float DistanceToPolygon(SightVector3 point, System.Collections.Generic.IReadOnlyList<SightVector3> polygon, SightVector3 normal)
        {
            SightVector3 projected = point - normal * SightVector3.Dot(point - polygon[0], normal);
            bool positive = false, negative = false;
            float closestEdgeSquared = float.PositiveInfinity;
            for (int i = 0; i < polygon.Count; i++)
            {
                SightVector3 a = polygon[i], edge = polygon[(i + 1) % polygon.Count] - a;
                float side = SightVector3.Dot(SightVector3.Cross(edge, projected - a), normal);
                positive |= side > Epsilon; negative |= side < -Epsilon;
                float parameter = SightVector3.Dot(point - a, edge) / SightVector3.Dot(edge, edge);
                parameter = Math.Max(0, Math.Min(1, parameter));
                SightVector3 distance = point - (a + edge * parameter);
                closestEdgeSquared = Math.Min(closestEdgeSquared, SightVector3.Dot(distance, distance));
            }
            if (!(positive && negative)) return Math.Abs(SightVector3.Dot(point - polygon[0], normal));
            return (float)Math.Sqrt(closestEdgeSquared);
        }
    }
}
