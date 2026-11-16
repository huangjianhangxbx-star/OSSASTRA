/*
 * 脚本：CharacterMovement
 * 功能：执行唯一碰撞位移，并为横版连携提供净空、落脚重建和受支撑的退场查询。
 * 协作：横版运动状态传入跳跃、攀越、退场或能力运动；队伍交接仅使用受约束的地面落位。
 * 边界：只管理运动；不决定攻击方向、不结算伤害。
 * 运动事实：Root 初始化 Bus；CharacterController 实际位移决定速度和 Run 资格。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Characters
{
    internal readonly struct SideViewSwitchPlacement
    {
        internal readonly CharacterMovement Incoming, Outgoing;
        internal readonly uint IncomingRevision, OutgoingRevision;
        internal readonly Vector3 OutgoingPosition;
        internal readonly Collider Support;
        internal Vector3 Position { get; }

        internal SideViewSwitchPlacement(CharacterMovement incoming, CharacterMovement outgoing,
            Vector3 position, Collider support)
        {
            Incoming = incoming;
            Outgoing = outgoing;
            IncomingRevision = incoming.ContactRevision;
            OutgoingRevision = outgoing.ContactRevision;
            OutgoingPosition = outgoing.transform.position;
            Position = position;
            Support = support;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMovement : MonoBehaviour, ICharacterMovement
    {
        [SerializeField, Min(.01f)] private float speed = 3;
        [SerializeField, Min(.01f)] private float groundStick = 2;
        [SerializeField] private CharacterJumpProfile jumpProfile;
        private CharacterController body;
        private CharacterDataBus dataBus;
        private Ossastra.Combat.CombatStatusEffects statusEffects;
        private ICharacterLocomotionScale locomotionScale;
        private ICharacterEnvironmentClearance environmentClearance;
        public bool IsMoving { get; private set; }
        public Vector3 Velocity { get; private set; }
        public CharacterJumpProfile JumpProfile => jumpProfile;
        public bool IsSideView => jumpProfile != null;
        public float VerticalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public CollisionFlags LastCollision { get; private set; }
        internal uint ContactRevision { get; private set; }
        private float sideDepth;
        private Collider stepSupport;
        private SideViewLedgeProbe switchGroundProbe;
        private Vector3 traversalMoveDirection;
        internal Collider LastTraversalObstacle { get; private set; }
        private SideViewPartyContacts partyContacts;
        private RaycastHit[] headHits = new RaycastHit[8];
        private RaycastHit[] abilityPathHits = new RaycastHit[8];
        private Vector3 previousPose, latestPose;
        private float presentationFraction = 1f;
        private bool hasPose, hasEstablishedSupport;
        private long sideStepId;

        public event Action<SideMovementStep> SideStepCompleted;
        public event Action<SideTraversalEvent> SideTraversalOccurred;
        public Vector3 PresentedPosition => IsSideView && hasPose
            ? Vector3.Lerp(previousPose, latestPose, presentationFraction) : transform.position;
        public Vector3 PresentationOffset => PresentedPosition - transform.position;

        internal void SetSideViewContacts(SideViewPartyContacts contacts) => partyContacts = contacts;

        internal void SetSideViewPresentationFraction(float fraction) => presentationFraction = Mathf.Clamp01(fraction);

        internal void ResetSideViewPose()
        {
            previousPose = latestPose = transform.position;
            presentationFraction = 1f;
            hasPose = IsSideView;
        }
        private bool movingSideView;
        // 只读诊断：区分未获许可、未请求速度与真实碰撞截断，不参与运动决策。
        public bool LastAllowed {get;private set;}
        public Vector3 RequestedVelocity {get;private set;}
        public bool LastEnvironmentSideBlocked {get;private set;}
        internal void BindEnvironmentClearance(ICharacterEnvironmentClearance clearance) { environmentClearance = clearance; }

        public bool Initialize(CharacterDataBus dataBus)
        {
            if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
            this.dataBus = dataBus;
            body = GetComponent<CharacterController>();
            sideDepth = transform.position.z;
            ResetSideViewPose();
            statusEffects=GetComponent<Ossastra.Combat.CombatStatusEffects>();
            locomotionScale=GetComponent<CharacterAbilities>();
            return body != null;
        }

        public void Tick(bool allowed, float delta)
        {
            ClearMovementFacts();LastAllowed=allowed;
            if (dataBus == null || body == null || !body.enabled || delta <= 0) return;
            MoveOnce(ResolveVelocity(allowed, speed), -groundStick, delta, false, out _);
        }

        public void ConfigureSideView(CharacterJumpProfile profile)
        {
            jumpProfile = profile;
            sideDepth = transform.position.z;
            Stop();
        }

        internal SideViewLedgeProbe CreateLedgeProbe() => new SideViewLedgeProbe(body, IsTraversalObstacle, IsEnvironmentContact);

        private SideViewLedgeProbe SwitchGroundProbe => switchGroundProbe ??
            (switchGroundProbe = new SideViewLedgeProbe(body, IsSwitchSupport, IsSwitchObstacle));

        internal bool HasStableSwitchGround => IsSideView && isActiveAndEnabled && body != null && body.enabled &&
            Grounded && IsSwitchSupport(stepSupport);

        // 查询阶段保留双方原位。只在交接点及小范围内找连续地面和可容纳入场者的净空。
        internal bool TryPrepareSwitchPlacement(CharacterMovement outgoing, out SideViewSwitchPlacement placement)
        {
            placement = default;
            if (outgoing == null || outgoing == this || !HasStableSwitchGround || !outgoing.HasStableSwitchGround ||
                gameObject.scene != outgoing.gameObject.scene || Mathf.Abs(sideDepth - outgoing.sideDepth) > .005f ||
                partyContacts?.IsPeer(body, outgoing.body) != true) return false;

            var probe = SwitchGroundProbe;
            var anchor = outgoing.transform.position;
            // 原位也要有可靠中心支撑，阻止倚在台沿的胶囊把入场者投射到下面一层。
            if (!outgoing.SwitchGroundProbe.TrySwitchGround(anchor, .07f, 0f, out var seat, out _) ||
                Mathf.Abs(seat.y - anchor.y) > outgoing.SwitchGroundProbe.GroundContactTolerance) return false;
            anchor.z = sideDepth;
            anchor.y += outgoing.SwitchGroundProbe.FootOffset - probe.FootOffset;
            Vector3 position = default;
            Collider support = null;
            bool found = SideViewSwitchPath.TryFindPlacementOffset(Mathf.Min(.08f, probe.BodyWidth * .2f), .02f,
                offset =>
                {
                    if (offset != 0f && Mathf.Abs(outgoing.ResolveSwitchPath(offset, false, out _) - offset) > .001f)
                        return false;
                    return probe.TrySwitchGround(anchor + Vector3.right * offset, .11f, 0f, out position, out support);
                }, out _);
            if (!found) return false;
            placement = new SideViewSwitchPlacement(this, outgoing, position, support);
            return true;
        }

        // 执行取消本身不影响地面身份。传送前复核空间与支撑，传送后重建接触和插值历史。
        internal bool TryCommitSwitchPlacement(in SideViewSwitchPlacement placement)
        {
            var outgoing = placement.Outgoing;
            if (placement.Incoming != this || outgoing == null ||
                ContactRevision != placement.IncomingRevision || outgoing.ContactRevision != placement.OutgoingRevision ||
                (outgoing.transform.position - placement.OutgoingPosition).sqrMagnitude > .000001f ||
                !TryPrepareSwitchPlacement(outgoing, out var current) || current.Support != placement.Support ||
                (current.Position - placement.Position).sqrMagnitude > .000001f) return false;

            Teleport(current.Position);
            partyContacts?.Prepare(body);
            Physics.SyncTransforms();
            stepSupport = current.Support;
            Grounded = hasEstablishedSupport = true;
            return true;
        }

        internal float PrepareSwitchRetreat(int facing, float widthMultiplier)
        {
            if (!HasStableSwitchGround || facing == 0 || widthMultiplier <= 0f ||
                float.IsNaN(widthMultiplier) || float.IsInfinity(widthMultiplier)) return 0f;
            float distance = -Mathf.Sign(facing) * SwitchGroundProbe.BodyWidth * widthMultiplier;
            return ResolveSwitchPath(distance, true, out _);
        }

        internal Vector3 PlanSwitchRetreatStep(float distance)
        {
            if (!HasStableSwitchGround || Mathf.Abs(distance) < .0001f) return Vector3.zero;
            ResolveSwitchPath(distance, true, out var position);
            return position - transform.position;
        }

        private float ResolveSwitchPath(float distance, bool ledgeMargin, out Vector3 position)
        {
            var probe = SwitchGroundProbe;
            var origin = transform.position;
            var previous = origin;
            float margin = ledgeMargin ? Mathf.Sign(distance) * probe.BodyWidth * .15f : 0f;
            float spacing = Mathf.Clamp(probe.BodyWidth * .1f, .02f, .045f);
            float reached = SideViewSwitchPath.ReachableDistance(distance, spacing, offset =>
            {
                var next = previous;
                next.x = origin.x + offset;
                if (!probe.TrySwitchGround(next, .09f, margin, out var grounded, out _) ||
                    !SideViewSwitchPath.KeepsSupport(grounded.x - previous.x, grounded.y - previous.y,
                        probe.GroundSlope, probe.GroundContactTolerance) ||
                    !probe.ClearPath(previous, grounded)) return false;
                previous = grounded;
                return true;
            });
            position = previous;
            return reached;
        }

        internal void EmitTraversal(SideTraversalEventKind kind, int side)
            => SideTraversalOccurred?.Invoke(new SideTraversalEvent(kind, transform.position, side, sideStepId, ContactRevision));

        internal SideViewAbilityMotionStep TickSideView(bool allowed, float delta, JumpVerticalCommand command,
            float gravityScale, bool jumpAccepted, SideViewMotion traversalMotion = default,
            SideViewAbilityMotionCommand? abilityMotion = null, PostureReactionStep? reactionMotion = null,
            Vector3? switchRetreat = null)
        {
            if (!IsSideView || body == null || !body.enabled || delta <= 0f) return default;
            var before = transform.position;
            if (!hasPose || latestPose != before) ResetSideViewPose();
            bool groundedBefore = Grounded;
            bool footstepAllowed = allowed && !abilityMotion.HasValue && !reactionMotion.HasValue && !switchRetreat.HasValue && !traversalMotion.ConstrainedPose &&
                dataBus.MotionOverride?.IsActive != true && statusEffects?.HasForcedMotion != true &&
                statusEffects?.BlocksIntent != true;
            partyContacts?.Prepare(body);
            LastAllowed = allowed;

            bool abilityDrive = !reactionMotion.HasValue && abilityMotion.HasValue && !abilityMotion.Value.IsBounce;
            Vector3 horizontal;
            if (reactionMotion.HasValue)
            {
                var motion = reactionMotion.Value;
                horizontal = Vector3.right * motion.Horizontal;
                if (motion.LaunchVertical.HasValue) VerticalSpeed = motion.LaunchVertical.Value;
            }
            else if (abilityMotion.HasValue)
            {
                var velocity = abilityMotion.Value.Velocity;
                horizontal = Vector3.right * velocity.x;
                if (abilityDrive) VerticalSpeed = velocity.y;
            }
            else if (switchRetreat.HasValue)
                horizontal = Vector3.right * (switchRetreat.Value.x / delta);
            else
            {
                horizontal = ResolveVelocity(allowed, jumpProfile.runSpeed);
                horizontal.y = horizontal.z = 0f;
                if (traversalMotion.X.HasValue) horizontal.x = traversalMotion.X.Value;
            }
            if (!abilityDrive)
            {
                if (command == JumpVerticalCommand.Sustain) VerticalSpeed = jumpProfile.jumpSpeed;
                if (command == JumpVerticalCommand.Cut && VerticalSpeed > 0f)
                    VerticalSpeed *= jumpProfile.releaseSpeedScale;
                bool stayingGrounded = Grounded && command == JumpVerticalCommand.Gravity &&
                    reactionMotion?.LaunchVertical.HasValue != true;
                float acceleration = jumpProfile.gravity * Mathf.Clamp01(gravityScale);
                VerticalSpeed = stayingGrounded ? -groundStick
                    : Mathf.Max(-jumpProfile.fallSpeed, VerticalSpeed - acceleration * delta);
                if (traversalMotion.Y.HasValue) VerticalSpeed = traversalMotion.Y.Value;
            }
            float downBeforeContact = Mathf.Max(0f, -VerticalSpeed);
            float physicalVertical = VerticalSpeed;
            if (switchRetreat.HasValue && Mathf.Abs(switchRetreat.Value.x) > .0001f)
                physicalVertical = (switchRetreat.Value.y - .003f) / delta;
            var terrain = MoveOnce(horizontal, physicalVertical, delta, true, out var attempted,
                traversalMotion.ConstrainedPose, abilityDrive);
            bool sweptGround = terrain.collider != null && terrain.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad);
            if (sweptGround && stepSupport == null &&
                Vector3.Dot(transform.position - before, attempted.normalized) + .002f >= terrain.distance)
                stepSupport = terrain.collider;
            Grounded = (!traversalMotion.ConstrainedPose || traversalMotion.CanLand) &&
                VerticalSpeed <= 0f && stepSupport != null &&
                ((LastCollision & CollisionFlags.Below) != 0 || sweptGround);
            if (!Grounded && switchRetreat.HasValue && VerticalSpeed <= 0f)
            {
                // 沿斜地退场时位移可能含正 Y；以实际步后胶囊接触补足原生 Below 未报告的支撑。
                var probe = SwitchGroundProbe;
                if (probe.TrySwitchGround(transform.position, .05f, 0f, out var seated, out var support) &&
                    Mathf.Abs(seated.y - transform.position.y) <= probe.GroundContactTolerance)
                {
                    stepSupport = support;
                    Grounded = true;
                }
            }
            bool sweptHead = terrain.collider != null && terrain.normal.y < -.5f;
            bool hitHead = (LastCollision & CollisionFlags.Above) != 0 || sweptHead;
            if (Grounded || hitHead && VerticalSpeed > 0f) VerticalSpeed = 0f;

            previousPose = before;
            latestPose = transform.position;
            bool launched = jumpAccepted && !hitHead && latestPose.y > before.y + .001f;
            float landingSpeed = !traversalMotion.ConstrainedPose && !groundedBefore && Grounded && hasEstablishedSupport ? downBeforeContact : 0f;
            hasEstablishedSupport |= Grounded;
            SideStepCompleted?.Invoke(new SideMovementStep(++sideStepId, ContactRevision, groundedBefore,
                Grounded, launched, footstepAllowed, Mathf.Abs(latestPose.x - before.x), delta,
                landingSpeed, latestPose, Grounded ? stepSupport : null));

            if (!abilityMotion.HasValue) return default;
            bool hitGround = Grounded || sweptGround;
            var direction = attempted.normalized;
            float forwardTravel = Vector3.Dot(latestPose - before, direction);
            bool nativeSideBlocked = (LastCollision & CollisionFlags.Sides) != 0 &&
                forwardTravel + .002f < attempted.magnitude;
            bool hitWall = LastEnvironmentSideBlocked || nativeSideBlocked ||
                terrain.collider != null && !sweptGround && !sweptHead;
            var attackEnd = latestPose;
            if (abilityDrive && (hitGround || hitWall || hitHead))
            {
                // 能力沿提交方向扫掠。碰撞器的沿面滑动不能把攻击延长到阻挡后方。
                float maximum = terrain.collider == null ? attempted.magnitude : terrain.distance;
                float traveled = Mathf.Clamp(forwardTravel, 0f, maximum);
                attackEnd = before + direction * traveled;
            }
            return new SideViewAbilityMotionStep(before, latestPose, attackEnd, terrain.normal,
                hitGround, hitWall, hitHead, Grounded);
        }

        internal void PublishSideViewVelocity(Vector3 before, float simulatedDelta)
        {
            var displacement = transform.position - before;
            Velocity = new Vector3(displacement.x / simulatedDelta, 0f, 0f);
            IsMoving = Mathf.Abs(displacement.x) > .001f;
        }

        private Vector3 ResolveVelocity(bool allowed, float runSpeed)
        {
            var move = allowed ? Vector2.ClampMagnitude(dataBus.Input.move, 1) : Vector2.zero;
            // 平时与闪避共同走这一处Move；覆盖速度仍接受本体移动权限和真实墙体碰撞。
            var velocity=allowed&&dataBus.MotionOverride!=null&&dataBus.MotionOverride.IsActive
                ?dataBus.MotionOverride.MotionVelocity:new Vector3(move.x,0,move.y)*runSpeed*(locomotionScale?.LocomotionScale??1);
            // 受迫位移优先于自愿移动，但仍只经过本方法一次Move和墙体碰撞。
            if(statusEffects!=null)velocity=statusEffects.HasForcedMotion?statusEffects.ForcedVelocity:
                statusEffects.BlocksIntent?Vector3.zero:velocity*statusEffects.SpeedScale;
            return velocity;
        }

        private RaycastHit MoveOnce(Vector3 velocity, float vertical, float delta, bool sideView,
            out Vector3 attempted, bool captureTraversalObstacle = false, bool clipAbilityDrive = false)
        {
            RequestedVelocity=velocity;
            var before = transform.position;
            var requestedDisplacement=(velocity + Vector3.up * vertical) * delta;
            if (sideView) requestedDisplacement.z = sideDepth - before.z;
            LastTraversalObstacle = null;
            traversalMoveDirection = captureTraversalObstacle ? requestedDisplacement.normalized : Vector3.zero;
            var constrained=environmentClearance==null
                ?new CharacterClearanceResult(requestedDisplacement,false):environmentClearance.Constrain(requestedDisplacement);
            LastEnvironmentSideBlocked=constrained.SideBlocked;
            attempted = constrained.Displacement;
            var terrain = clipAbilityDrive ? FirstAbilityTerrainHit(attempted) : default;
            var displacementToMove = terrain.collider == null ? attempted : attempted.normalized * terrain.distance;
            stepSupport = null;
            movingSideView = sideView;
            var collision = CollisionFlags.None;
            try { collision = body.Move(displacementToMove); }
            finally { movingSideView = false; }
            LastCollision = collision;
            if(((collision&CollisionFlags.Sides)!=0||LastEnvironmentSideBlocked)&&statusEffects!=null&&statusEffects.HasForcedMotion)statusEffects.StopForcedMotion();
            // 用碰撞处理后的实际位移报告运动，而不是把“按住移动键”当成真的跑动。
            var displacement = transform.position - before;
            displacement.y = 0;
            if (sideView) displacement.z = 0;
            Velocity = displacement / delta;
            IsMoving = displacement.sqrMagnitude > .000001f;
            return terrain;
        }

        private RaycastHit FirstAbilityTerrainHit(Vector3 displacement)
        {
            float distance = displacement.magnitude;
            if (distance < .000001f) return default;

            var scale = transform.lossyScale;
            float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            if (radius <= 0f) return default;
            var axis = Vector3.up * Mathf.Max(0f, body.height * Mathf.Abs(scale.y) * .5f - radius);
            var center = transform.TransformPoint(body.center);
            var direction = displacement / distance;
            var physicsWorld = gameObject.scene.GetPhysicsScene();
            int count;
            while (true)
            {
                count = physicsWorld.CapsuleCast(center - axis, center + axis, radius, direction,
                    abilityPathHits, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (count < abilityPathHits.Length) break;
                Array.Resize(ref abilityPathHits, abilityPathHits.Length * 2);
            }

            var nearest = default(RaycastHit);
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = abilityPathHits[i];
                if (hit.distance >= nearestDistance || Vector3.Dot(direction, hit.normal) >= -.0001f ||
                    !IsTraversalObstacle(hit.collider)) continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            return nearest;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!movingSideView || !IsEnvironmentContact(hit.collider)) return;
            // 只记录妨碍本次受约束位移的碰撞，沿墙滑行的侧触不算。
            if (Vector3.Dot(traversalMoveDirection, hit.normal) < -.1f)
                LastTraversalObstacle = hit.collider;
            if (hit.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad) &&
                hit.point.y < transform.TransformPoint(body.center).y)
                stepSupport = hit.collider;
        }

        internal bool HasJumpHeadroom()
        {
            var center = transform.TransformPoint(body.center);
            var top = center + Vector3.up * (body.height * .5f - body.radius);
            // 同伴可能比顶壁更近。遍历全部命中，不能因首个命中是己方就忽略后方顶壁。
            int count;
            do
            {
                count = Physics.SphereCastNonAlloc(top, body.radius * .85f, Vector3.up, headHits, .8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (IsEnvironmentContact(headHits[i].collider)) return false;
                if (count < headHits.Length) return true;
                Array.Resize(ref headHits, headHits.Length * 2);
            } while (true);
        }

        private bool IsEnvironmentContact(Collider candidate)
            => candidate != null && candidate != body && partyContacts?.IsPeer(body, candidate) != true &&
                !Physics.GetIgnoreLayerCollision(gameObject.layer, candidate.gameObject.layer) &&
                !Physics.GetIgnoreCollision(body, candidate);

        private bool IsTraversalObstacle(Collider candidate)
            => IsEnvironmentContact(candidate) && !(candidate is CharacterController) &&
                candidate.GetComponentInParent<Ossastra.Combat.CombatVitals>() == null;

        private bool IsSwitchObstacle(Collider candidate)
            => IsEnvironmentContact(candidate) && !candidate.transform.IsChildOf(transform);

        private bool IsSwitchSupport(Collider candidate)
            => IsSwitchObstacle(candidate) && !(candidate is CharacterController) &&
                candidate.GetComponentInParent<Ossastra.Combat.CombatVitals>() == null;

        private void ClearMovementFacts()
        { LastAllowed=false;RequestedVelocity=Vector3.zero;LastEnvironmentSideBlocked=false;IsMoving=false;Velocity=Vector3.zero; }

        /// <summary>只清运动事实，不Move、不改坐标；死亡/禁用路径也必须形成真实退出边沿。</summary>
        public void Stop()
        {
            ClearMovementFacts();
            VerticalSpeed = 0;
            Grounded = false;
            hasEstablishedSupport = false;
            stepSupport = null;
            ResetSideViewPose();
            LastCollision = CollisionFlags.None;
            ContactRevision++;
        }
        private void OnDisable(){Stop();}

        public void Teleport(Vector3 position)
        {
            if (body == null) return;
            var wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = position;
            body.enabled = wasEnabled;
            sideDepth = position.z;
            Stop();
        }
    }
}
