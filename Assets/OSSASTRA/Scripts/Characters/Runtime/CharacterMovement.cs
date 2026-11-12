/*
 * 脚本：CharacterMovement
 * 功能：依据角色输入与移动许可执行真实碰撞位移。横版配置启用时由运动子状态传入Y轴策略。
 * 边界：只管理运动；不决定攻击方向、不结算伤害。
 * 协作：Root初始化Bus并调用Tick；CharacterController实际位移决定速度和Run资格。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
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
        private SideViewPartyContacts partyContacts;
        private RaycastHit[] headHits = new RaycastHit[8];
        private Vector3 previousPose, latestPose;
        private float presentationFraction = 1f;
        private bool hasPose, hasEstablishedSupport;
        private long sideStepId;

        public event Action<SideMovementStep> SideStepCompleted;
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
            MoveOnce(ResolveVelocity(allowed, speed), -groundStick, delta, false);
        }

        public void ConfigureSideView(CharacterJumpProfile profile)
        {
            jumpProfile = profile;
            sideDepth = transform.position.z;
            Stop();
        }

        internal void TickSideView(bool allowed, float delta, JumpVerticalCommand command, float gravityScale, bool jumpAccepted)
        {
            if (!IsSideView || body == null || !body.enabled || delta <= 0f) return;
            var before = transform.position;
            if (!hasPose || latestPose != before) ResetSideViewPose();
            bool groundedBefore = Grounded;
            bool footstepAllowed = allowed && dataBus.MotionOverride?.IsActive != true &&
                statusEffects?.HasForcedMotion != true && statusEffects?.BlocksIntent != true;
            partyContacts?.Prepare(body);
            LastAllowed = allowed;
            var horizontal = ResolveVelocity(allowed, jumpProfile.runSpeed);
            horizontal.y = horizontal.z = 0f;
            if (command == JumpVerticalCommand.Sustain) VerticalSpeed = jumpProfile.jumpSpeed;
            if (command == JumpVerticalCommand.Cut && VerticalSpeed > 0f)
                VerticalSpeed *= jumpProfile.releaseSpeedScale;
            bool stayingGrounded = Grounded && command == JumpVerticalCommand.Gravity;
            float acceleration = jumpProfile.gravity * Mathf.Clamp01(gravityScale);
            VerticalSpeed = stayingGrounded ? -groundStick
                : Mathf.Max(-jumpProfile.fallSpeed, VerticalSpeed - acceleration * delta);
            float downBeforeContact = Mathf.Max(0f, -VerticalSpeed);
            MoveOnce(horizontal, VerticalSpeed, delta, true);
            Grounded = VerticalSpeed <= 0f && stepSupport != null && (LastCollision & CollisionFlags.Below) != 0;
            bool hitHead = (LastCollision & CollisionFlags.Above) != 0;
            if (Grounded || hitHead && VerticalSpeed > 0f) VerticalSpeed = 0f;

            previousPose = before;
            latestPose = transform.position;
            bool launched = jumpAccepted && !hitHead && latestPose.y > before.y + .001f;
            float landingSpeed = !groundedBefore && Grounded && hasEstablishedSupport ? downBeforeContact : 0f;
            hasEstablishedSupport |= Grounded;
            SideStepCompleted?.Invoke(new SideMovementStep(++sideStepId, ContactRevision, groundedBefore,
                Grounded, launched, footstepAllowed, Mathf.Abs(latestPose.x - before.x), delta,
                landingSpeed, latestPose, Grounded ? stepSupport : null));
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

        private void MoveOnce(Vector3 velocity, float vertical, float delta, bool sideView)
        {
            RequestedVelocity=velocity;
            var before = transform.position;
            var requestedDisplacement=(velocity + Vector3.up * vertical) * delta;
            if (sideView) requestedDisplacement.z = sideDepth - before.z;
            var constrained=environmentClearance==null
                ?new CharacterClearanceResult(requestedDisplacement,false):environmentClearance.Constrain(requestedDisplacement);
            LastEnvironmentSideBlocked=constrained.SideBlocked;
            stepSupport = null;
            movingSideView = sideView;
            var collision = CollisionFlags.None;
            try { collision = body.Move(constrained.Displacement); }
            finally { movingSideView = false; }
            LastCollision = collision;
            if(((collision&CollisionFlags.Sides)!=0||LastEnvironmentSideBlocked)&&statusEffects!=null&&statusEffects.HasForcedMotion)statusEffects.StopForcedMotion();
            // 用碰撞处理后的实际位移报告运动，而不是把“按住移动键”当成真的跑动。
            var displacement = transform.position - before;
            displacement.y = 0;
            if (sideView) displacement.z = 0;
            Velocity = displacement / delta;
            IsMoving = displacement.sqrMagnitude > .000001f;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (movingSideView && IsEnvironmentContact(hit.collider) &&
                hit.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad) &&
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
