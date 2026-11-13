/*
 * 脚本：CombatContactGeometry
 * 功能：记录已释放的世界方向、到达方向、平面和碰撞点质量。
 * 边界：只转换几何，不保存角色或Collider引用，不改命中结果。
 */
using UnityEngine;

namespace Ossastra.Combat
{
    public enum CombatPlane { GroundXZ, SideXY }
    public enum CombatContactPointQuality { TargetAnchor, EstimatedOverlap, PhysicsCast }

    /// <summary>已捕获的世界接触点。只有实际物理查询提供法线时才记录。</summary>
    public readonly struct CombatContactPoint
    {
        public Vector3 Position { get; }
        private readonly Vector3? visualPosition;
        public Vector3 VisualPosition => visualPosition ?? Position;
        public CombatContactPointQuality Quality { get; }
        public Vector3? Normal { get; }
        public bool HasNormal => Normal.HasValue;

        public CombatContactPoint(Vector3 position, CombatContactPointQuality quality, Vector3? normal = null)
            : this(position, quality, normal, null) { }

        internal CombatContactPoint(Vector3 position, CombatContactPointQuality quality, Vector3? normal,
            Vector3? visualPosition)
        {
            Position = position;
            Quality = quality;
            Normal = normal;
            this.visualPosition = visualPosition;
        }
    }

    /// <summary>一次投递的空间事实。释放后不再读取角色转身或输入。</summary>
    public readonly struct CombatContactGeometry
    {
        public CombatPlane Plane { get; }
        public Vector3 ReleaseOrigin { get; }
        public Vector3 ReleaseDirection { get; }
        public Vector3 ArrivalDirection { get; }
        /// <summary>主手普通攻击的释放段号，其他投递为 0。仅供表现选择。</summary>
        public int BasicAttackStage { get; }

        public CombatContactGeometry(CombatPlane plane, Vector3 releaseOrigin, Vector3 releaseDirection,
            Vector3 arrivalDirection, int basicAttackStage = 0)
        {
            Plane = plane;
            ReleaseOrigin = releaseOrigin;
            ReleaseDirection = Project(releaseDirection, plane).normalized;
            ArrivalDirection = Project(arrivalDirection, plane).normalized;
            BasicAttackStage = basicAttackStage;
        }

        internal CombatContactGeometry WithArrival(Vector3 arrivalDirection)
            => new CombatContactGeometry(Plane, ReleaseOrigin, ReleaseDirection, arrivalDirection, BasicAttackStage);

        public static Vector3 Project(Vector3 direction, CombatPlane plane)
        {
            if (plane == CombatPlane.SideXY) direction.z = 0;
            else direction.y = 0;
            return direction;
        }

        internal static bool ContainsSector(Vector3 toTarget, Vector3 forward, float arc, CombatPlane plane)
        {
            var delta = Project(toTarget, plane);
            return delta.sqrMagnitude < .0001f || arc >= 360 ||
                Vector3.Angle(delta, Project(forward, plane)) <= arc * .5f;
        }
    }
}
