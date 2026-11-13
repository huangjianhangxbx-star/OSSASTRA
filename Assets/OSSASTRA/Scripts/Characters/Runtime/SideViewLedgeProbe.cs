/*
 * 横版侧墙、顶缘和攀越净空查询。查询使用角色所在的 3D 物理场景和实际胶囊。
 * 返回当前几何证据与稳定锚点，由运动状态机处理输入、资格和唯一位移。
 */
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Characters
{
    internal readonly struct SideWallContact
    {
        public readonly RaycastHit Hit;
        public readonly int Side;
        public SideWallContact(RaycastHit hit, int side) { Hit = hit; Side = side; }
        public bool Exists => Side != 0 && Hit.collider != null;
    }

    internal readonly struct SideLedge
    {
        public readonly SideWallContact Wall;
        public readonly RaycastHit Top;
        public readonly Vector3 Grip, Hang, Above, Over;
        private readonly Matrix4x4 wallTransform, topTransform;
        private readonly Bounds wallBounds, topBounds;

        public SideLedge(SideWallContact wall, RaycastHit top, Vector3 grip, Vector3 hang, Vector3 above, Vector3 over)
        {
            Wall = wall;
            Top = top;
            Grip = grip;
            Hang = hang;
            Above = above;
            Over = over;
            wallTransform = wall.Hit.collider.transform.localToWorldMatrix;
            topTransform = top.collider.transform.localToWorldMatrix;
            wallBounds = wall.Hit.collider.bounds;
            topBounds = top.collider.bounds;
        }

        public bool IsValid => Wall.Exists && Top.collider != null &&
            StaticCollider(Wall.Hit.collider, wallTransform, wallBounds) &&
            StaticCollider(Top.collider, topTransform, topBounds);

        public bool SameEdge(SideLedge other) => Wall.Side != 0 && Wall.Side == other.Wall.Side &&
            Wall.Hit.collider == other.Wall.Hit.collider && Top.collider == other.Top.collider &&
            (Grip - other.Grip).sqrMagnitude < .04f;

        private static bool StaticCollider(Collider collider, Matrix4x4 initial, Bounds bounds)
            => collider.enabled && collider.gameObject.activeInHierarchy && collider.attachedRigidbody == null &&
                collider.transform.localToWorldMatrix == initial && collider.bounds == bounds;
    }

    internal sealed class SideViewLedgeProbe
    {
        private readonly CharacterController body;
        private readonly Func<Collider, bool> environment;
        private readonly Func<Collider, bool> blocksMovement;
        private RaycastHit[] hits = new RaycastHit[16];
        private Collider[] overlaps = new Collider[16];

        public SideViewLedgeProbe(CharacterController body, Func<Collider, bool> environment,
            Func<Collider, bool> blocksMovement)
        { this.body = body; this.environment = environment; this.blocksMovement = blocksMovement; }

        private PhysicsScene PhysicsWorld => body.gameObject.scene.GetPhysicsScene();
        private Vector3 Root => body.transform.position;
        private Vector3 CenterOffset => body.transform.TransformVector(body.center);
        private float Radius => body.radius * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));
        private float HalfHeight => Mathf.Max(Radius, body.height * Mathf.Abs(body.transform.lossyScale.y) * .5f);
        private float Skin => body.skinWidth * Mathf.Max(Mathf.Abs(body.transform.lossyScale.y), Mathf.Abs(body.transform.lossyScale.x));
        private float ClearanceRadius => Mathf.Max(.01f, Radius - Skin * .55f);

        public SideWallContact Wall(int side, SideViewTraversalSettings settings)
        {
            if (side == 0) return default;
            var center = Root + CenterOffset;
            var direction = Vector3.right * side;
            float distance = Radius + Mathf.Max(settings.wallReach, Skin * 2f);
            if (!Ray(center, direction, distance, settings, out var hit) ||
                hit.normal.x * side > -.78f || Mathf.Abs(hit.normal.y) > .5f || Mathf.Abs(hit.normal.z) > .5f)
                return default;
            return new SideWallContact(hit, side);
        }

        public bool TryLedge(SideWallContact wall, float verticalSweep, SideViewTraversalSettings settings,
            out SideLedge ledge, out string reason)
        {
            ledge = default;
            reason = "no-side-wall";
            if (!wall.Exists || wall.Hit.collider.attachedRigidbody != null) return false;
            var centerOffset = CenterOffset;
            var root = Root;
            float handOffset = centerOffset.y + HalfHeight - Mathf.Max(Skin * 2f, .10f);
            float handY = root.y + handOffset;
            float highest = handY + settings.gripAboveHand + Mathf.Clamp(verticalSweep, 0f, .30f);
            float lowest = handY - settings.gripBelowHand;
            float clearance = Mathf.Max(settings.surfaceClearance, Skin * 2f);
            float wallX = wall.Hit.point.x;
            var origin = new Vector3(wallX + wall.Side * clearance, highest + .025f, root.z);
            if (!Ray(origin, Vector3.down, highest - lowest + .025f, settings, out var top) ||
                top.collider.attachedRigidbody != null || top.normal.y < Mathf.Max(.8f, Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)))
            { reason = "top-outside-grip-window"; return false; }

            float topY = top.point.y;
            float standRootY = topY + HalfHeight - centerOffset.y + clearance;
            float hangX = wallX - wall.Side * (Radius + clearance) - centerOffset.x;
            float standX = wallX + wall.Side * (Radius + clearance * 2f) - centerOffset.x;
            var grip = new Vector3(wallX, topY, root.z);
            var hang = new Vector3(hangX, topY - handOffset, root.z);
            var above = new Vector3(hangX, standRootY, root.z);
            var over = new Vector3(standX, standRootY, root.z);
            if (!ClearPath(root, hang))
            { reason = "hang-capsule-blocked"; return false; }
            ledge = new SideLedge(wall, top, grip, hang, above, over);
            reason = null;
            return true;
        }

        public bool HasCrossedLedge(SideLedge ledge)
            => (Root.x + CenterOffset.x - ledge.Grip.x) * ledge.Wall.Side >= 0f;

        public bool ClimbClear(SideLedge ledge, Vector3 from, SideViewTraversalSettings settings)
        {
            if (!ledge.IsValid || !ClearPath(from, ledge.Above) ||
                !ClearPath(ledge.Above, ledge.Over)) return false;
            return SupportsLanding(ledge, settings);
        }

        public bool SupportsLanding(SideLedge ledge, SideViewTraversalSettings settings)
        {
            var center = ledge.Over + CenterOffset;
            var bottom = center - Vector3.up * HalfHeight;
            for (int i = -1; i <= 1; i++)
            {
                var origin = bottom + Vector3.right * (i * Radius * .75f);
                if (!Ray(origin, Vector3.down, Mathf.Max(.16f, settings.surfaceClearance * 2.5f), settings, out var support) ||
                    support.collider.attachedRigidbody != null || support.normal.y < .8f) return false;
            }
            return true;
        }

        public bool ClearPath(Vector3 from, Vector3 to) => ClearPath(from, to, out _);

        public bool ClearPath(Vector3 from, Vector3 to, out Collider obstruction)
        {
            obstruction = null;
            Capsule(to, out var p, out var q);
            int count;
            do
            {
                // 只有抓握与落脚受 surfaces 限定。净空应覆盖原胶囊实际会撞到的物件。
                count = PhysicsWorld.OverlapCapsule(p, q, ClearanceRadius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (!blocksMovement(overlaps[i])) continue;
                    obstruction = overlaps[i];
                    return false;
                }
                if (count < overlaps.Length) break;
                Array.Resize(ref overlaps, overlaps.Length * 2);
            } while (true);

            var displacement = to - from;
            float distance = displacement.magnitude;
            if (distance <= .001f) return true;
            Capsule(from, out p, out q);
            do
            {
                count = PhysicsWorld.CapsuleCast(p, q, ClearanceRadius, displacement / distance, hits, distance,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (!blocksMovement(hits[i].collider)) continue;
                    obstruction = hits[i].collider;
                    return false;
                }
                if (count < hits.Length) return true;
                Array.Resize(ref hits, hits.Length * 2);
            } while (true);
        }

        private void Capsule(Vector3 root, out Vector3 bottom, out Vector3 top)
        {
            var center = root + CenterOffset;
            var axis = Vector3.up * (HalfHeight - Radius);
            bottom = center - axis;
            top = center + axis;
        }

        private bool Ray(Vector3 start, Vector3 direction, float distance, SideViewTraversalSettings settings, out RaycastHit nearest)
        {
            nearest = default;
            int count;
            do
            {
                count = PhysicsWorld.Raycast(start, direction, hits, distance, settings.surfaces, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance >= minimum || !environment(hits[i].collider)) continue;
                nearest = hits[i];
                minimum = nearest.distance;
            }
            return nearest.collider != null;
        }
    }
}
