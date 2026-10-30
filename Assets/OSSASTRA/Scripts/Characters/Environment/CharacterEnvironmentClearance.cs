/* CharacterEnvironmentClearance：固定主体包络只约束显式实墙；原 CC 尺寸/受击/台阶不变。
 * Root 初始化、暂停、释放；无 Update、位置写入、第二 Move、角色/场景扫描或动画 Bounds。
 * 数学 capsule 仅存在自有 private PhysicsScene，active 原生形状、不模拟、不进入主场查询。
 * 原 Movement 合成普通/闪避/受迫位移后调用一次；返回侧阻用于既有受迫运动终止链。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Characters
{
    public enum CharacterClearanceStatus
    {
        Uninitialized, Ready, Idle, Clear, Constrained, Recovered,
        InvalidConfiguration, QueryOverflow, RecoveryLimit, UnresolvedOverlap, SlideLimit, Suspended
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMovement))]
    public sealed class CharacterEnvironmentClearance : MonoBehaviour, ICharacterEnvironmentClearance
    {
        [Header("固定主体，世界米；不随镜头、动画、武器或发丝变化")]
        [Min(.01f)] public float coreRadius = .55f;
        [Min(.02f)] public float coreHeight = 1.8f;
        [Min(.001f)] public float contactMargin = .015f;
        [Min(.01f)] public float maximumInitialRecovery = .55f;
        [Header("显式环境实墙 volumes；可站立Box仍保留；不填独立顶盖/坡道/角色/代理")]
        public Collider[] environmentSolids = Array.Empty<Collider>();
        public LayerMask environmentLayers = ~0;

        const int Capacity = 64, RecoveryPasses = 4, SlidePasses = 3;
        const float Tiny = .00001f;
        readonly HashSet<Collider> solids = new HashSet<Collider>();
        readonly Collider[] overlaps = new Collider[Capacity];
        readonly RaycastHit[] sweeps = new RaycastHit[Capacity];
        Transform actor;
        CharacterController controller;
        Scene mathScene;
        GameObject mathObject;
        CapsuleCollider mathCapsule;
        int queryMask;
        bool initialized;
        float shapeRadius, shapeHeight;

        public CharacterClearanceStatus Status { get; private set; } = CharacterClearanceStatus.Uninitialized;
        public int QueryCount { get; private set; }
        public int ContactCount { get; private set; }
        public int RecoveryIterations { get; private set; }
        public int SlideIterations { get; private set; }
        public int OverflowCount { get; private set; }
        public float InitialPenetration { get; private set; }
        public Vector3 Recovery { get; private set; }
        public Vector3 LastContactNormal { get; private set; }
        public Collider LastContact { get; private set; }
        public Vector3 LastAllowedDisplacement { get; private set; }
        public bool LastSideBlocked { get; private set; }
        public bool OwnsMathShape => mathCapsule != null && mathScene.IsValid() && mathScene.isLoaded;
        public int RegisteredSolidCount => solids.Count;

        public bool Initialize(Transform owner, CharacterController body)
        {
            Dispose();
            actor = owner; controller = body;
            if (owner == null || owner != transform || body == null || body.transform != owner || !ValidGeometry() ||
                !Application.isPlaying || owner.gameObject.scene.GetPhysicsScene() != Physics.defaultPhysicsScene)
            { Status = CharacterClearanceStatus.InvalidConfiguration; return false; }
            queryMask = 0;
            if (environmentSolids != null)
                foreach (var solid in environmentSolids)
                {
                    if (solid == null || solid.isTrigger || solid.gameObject.scene != owner.gameObject.scene ||
                        solid.transform.IsChildOf(owner) || solid.GetComponentInParent<Ossastra.Combat.CombatVitals>() != null ||
                        solid.GetComponentInParent<Canvas>() != null) continue;
                    if (solids.Add(solid)) queryMask |= 1 << solid.gameObject.layer;
                }
            queryMask &= environmentLayers.value;
            if (solids.Count == 0 || solids.Count > Capacity || queryMask == 0)
            { solids.Clear(); Status = CharacterClearanceStatus.InvalidConfiguration; return false; }
            initialized = true;
            if (isActiveAndEnabled) EnsureMathShape();
            Status = CharacterClearanceStatus.Ready;
            return true;
        }

        bool ValidGeometry()
        {
            if (!Finite(coreRadius) || !Finite(coreHeight) || !Finite(contactMargin) || !Finite(maximumInitialRecovery) ||
                coreRadius <= 0 || coreHeight < coreRadius * 2 || contactMargin <= 0 || maximumInitialRecovery <= 0) return false;
            return actor == null || Vector3.Dot(actor.up, Vector3.up) > .9999f;
        }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        void EnsureMathShape()
        {
            if (mathCapsule == null)
            {
                mathScene = SceneManager.CreateScene("CharacterClearanceMath_" + Guid.NewGuid().ToString("N"),
                    new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                mathObject = new GameObject("CharacterClearanceMathCapsule") { hideFlags = HideFlags.HideAndDontSave };
                mathObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(mathObject, mathScene);
                mathCapsule = mathObject.AddComponent<CapsuleCollider>();
                mathCapsule.direction = 1;
                shapeRadius = shapeHeight = -1;
            }
            if (shapeRadius != coreRadius || shapeHeight != coreHeight)
            {
                mathCapsule.radius = shapeRadius = coreRadius;
                mathCapsule.height = shapeHeight = coreHeight;
                mathCapsule.center = Vector3.up * (coreHeight * .5f);
            }
            // Never-enabled inactive primitives returned false MTD in this Tuanjie build.
            // Enable only after moving into the private physics world. Never add a Rigidbody or Simulate.
            if (!mathCapsule.enabled) mathCapsule.enabled = true;
            if (!mathObject.activeSelf) mathObject.SetActive(true);
        }

        bool Accepted(Collider solid) => solid != null && solid.enabled && solid.gameObject.activeInHierarchy &&
            !solid.isTrigger && solids.Contains(solid) && solid.gameObject.scene == actor.gameObject.scene;
        void Capsule(Vector3 foot, out Vector3 a, out Vector3 b)
        { a = foot + Vector3.up * coreRadius; b = foot + Vector3.up * (coreHeight - coreRadius); }
        bool Walkable(Vector3 normal) => normal.y >= Mathf.Cos(Mathf.Clamp(controller.slopeLimit, 0, 89) * Mathf.Deg2Rad);
        bool Overflow(int count)
        {
            if (count < Capacity) return false;
            OverflowCount++; Status = CharacterClearanceStatus.QueryOverflow;
            return true;
        }
        CharacterClearanceResult Result(Vector3 displacement, bool blocked)
        {
            LastAllowedDisplacement = displacement; LastSideBlocked = blocked;
            return new CharacterClearanceResult(displacement, blocked);
        }

        public CharacterClearanceResult Constrain(Vector3 requestedDisplacement)
        {
            QueryCount = ContactCount = RecoveryIterations = SlideIterations = 0;
            InitialPenetration = 0; Recovery = LastContactNormal = Vector3.zero; LastContact = null;
            if (!initialized || actor == null || controller == null || !isActiveAndEnabled)
                return Result(requestedDisplacement, false);
            if (!Finite(requestedDisplacement) || !ValidGeometry())
            { Status = CharacterClearanceStatus.InvalidConfiguration; return Result(Vector3.zero, true); }
            var horizontal = Vector3.ProjectOnPlane(requestedDisplacement, Vector3.up);
            // Static camera rotation cannot produce a physical correction. Initial overlap is inspected/recovered
            // only when original Movement has an actual horizontal movement request.
            if (horizontal.sqrMagnitude < Tiny * Tiny)
            { Status = CharacterClearanceStatus.Idle; return Result(requestedDisplacement, false); }
            EnsureMathShape();
            Status = CharacterClearanceStatus.Clear;
            var vertical = Vector3.up * requestedDisplacement.y;
            if (!TryRecover(actor.position, out var recovery)) return Result(vertical, true);
            Recovery = recovery;
            var position = actor.position + recovery;
            var allowed = Vector3.zero;
            var remaining = horizontal;
            bool blocked = recovery.sqrMagnitude > Tiny * Tiny;
            for (int pass = 0; pass < SlidePasses && remaining.sqrMagnitude > Tiny * Tiny; ++pass)
            {
                SlideIterations++;
                Capsule(position + allowed, out var a, out var b);
                float length = remaining.magnitude; var direction = remaining / length;
                QueryCount++;
                int count = Physics.CapsuleCastNonAlloc(a, b, coreRadius, direction, sweeps,
                    length + contactMargin, queryMask, QueryTriggerInteraction.Ignore);
                if (Overflow(count)) return Result(vertical + recovery + allowed, true);
                int nearest = -1; float distance = float.PositiveInfinity;
                for (int i = 0; i < count; ++i)
                {
                    var hit = sweeps[i];
                    if (!Accepted(hit.collider) || Walkable(hit.normal) || Mathf.Abs(hit.normal.y) > .95f ||
                        hit.collider.bounds.max.y <= position.y + controller.stepOffset + controller.skinWidth ||
                        Vector3.Dot(direction, hit.normal) >= -Tiny || hit.distance >= distance) continue;
                    nearest = i; distance = hit.distance;
                }
                if (nearest < 0) { allowed += remaining; remaining = Vector3.zero; break; }
                var contact = sweeps[nearest];
                float travel = Mathf.Clamp(distance - contactMargin, 0, length);
                allowed += direction * travel; remaining -= direction * travel;
                var normal = Vector3.ProjectOnPlane(contact.normal, Vector3.up).normalized;
                float inward = Vector3.Dot(remaining, normal);
                if (inward < 0) remaining -= normal * inward;
                blocked = true; ContactCount++; LastContact = contact.collider; LastContactNormal = contact.normal;
            }
            if (remaining.sqrMagnitude > Tiny * Tiny) Status = CharacterClearanceStatus.SlideLimit;
            else Status = recovery.sqrMagnitude > Tiny * Tiny ? CharacterClearanceStatus.Recovered :
                blocked ? CharacterClearanceStatus.Constrained : CharacterClearanceStatus.Clear;
            return Result(vertical + recovery + allowed, blocked);
        }

        bool TryRecover(Vector3 foot, out Vector3 recovery)
        {
            recovery = Vector3.zero;
            for (int pass = 0; pass < RecoveryPasses; ++pass)
            {
                RecoveryIterations++;
                Capsule(foot + recovery, out var a, out var b);
                QueryCount++;
                int count = Physics.OverlapCapsuleNonAlloc(a, b, coreRadius, overlaps, queryMask, QueryTriggerInteraction.Ignore);
                if (Overflow(count)) return false;
                bool corrected = false;
                for (int i = 0; i < count; ++i)
                {
                    var wall = overlaps[i]; if (!Accepted(wall) || wall.bounds.max.y <= foot.y + controller.stepOffset + controller.skinWidth) continue;
                    if (!Physics.ComputePenetration(mathCapsule, foot + recovery, Quaternion.identity,
                        wall, wall.transform.position, wall.transform.rotation, out var normal, out var distance))
                    { Status = CharacterClearanceStatus.UnresolvedOverlap; return false; }
                    if (distance <= .005f) continue;
                    // This list contains walls, not inferred support surfaces. An upward/downward MTD
                    // of a real wall is still unresolved overlap; never silently label it clear.
                    if (Mathf.Abs(normal.y) >= .7f)
                    { Status = CharacterClearanceStatus.UnresolvedOverlap; return false; }
                    InitialPenetration = Mathf.Max(InitialPenetration, distance);
                    if (pass == RecoveryPasses - 1) { Status = CharacterClearanceStatus.RecoveryLimit; return false; }
                    var horizontal = Vector3.ProjectOnPlane(normal, Vector3.up);
                    float horizontalLength = horizontal.magnitude;
                    if (horizontalLength < Tiny) { Status = CharacterClearanceStatus.UnresolvedOverlap; return false; }
                    // MTD distance is along a tilted normal; divide by its horizontal length so this
                    // horizontal-only correction still separates the same side plane.
                    var correction = horizontal / horizontalLength * (distance / horizontalLength + contactMargin);
                    if ((recovery + correction).magnitude > maximumInitialRecovery)
                    { Status = CharacterClearanceStatus.RecoveryLimit; return false; }
                    recovery += correction; corrected = true; ContactCount++;
                    LastContact = wall; LastContactNormal = normal;
                    // The previous overlap array describes the previous pose. Re-query after EACH MTD;
                    // another cached collider may already be separated by this correction.
                    break;
                }
                if (!corrected) return true;
            }
            Status = CharacterClearanceStatus.RecoveryLimit; return false;
        }

        public void Suspend()
        {
            if (mathObject != null) { mathObject.SetActive(false); Destroy(mathObject); }
            mathObject = null; mathCapsule = null;
            if (mathScene.IsValid() && mathScene.isLoaded) SceneManager.UnloadSceneAsync(mathScene);
            mathScene = default;
            LastContact = null; LastSideBlocked = false;
            LastAllowedDisplacement = Recovery = LastContactNormal = Vector3.zero;
            Status = CharacterClearanceStatus.Suspended;
        }
        public void Dispose()
        {
            Suspend(); initialized = false; actor = null; controller = null; solids.Clear(); queryMask = 0;
            LastContact = null; LastAllowedDisplacement = Recovery = LastContactNormal = Vector3.zero; LastSideBlocked = false;
        }
        void OnDisable() { Suspend(); }
        void OnDestroy() { Dispose(); }
    }
}
