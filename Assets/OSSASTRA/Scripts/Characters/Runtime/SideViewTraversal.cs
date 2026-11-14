/*
 * 横版墙边运动子状态。拥有挂点、翻越阶段和可选墙跳锁步。
 * 环境由探测器提供证据，原 CharacterMovement 每步只执行一次碰撞位移。
 */
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    public enum SideViewTraversalMode { Grounded, Airborne, WallSlide, LedgeHang, LedgeClimb }
    public enum SideTraversalEventKind { Grab, WallJump, ClimbComplete, Drop }

    public readonly struct SideTraversalEvent
    {
        public readonly SideTraversalEventKind Kind;
        public readonly Vector3 Position;
        public readonly int WallSide;
        public readonly long StepId;
        public readonly uint ContactEpoch;

        public SideTraversalEvent(SideTraversalEventKind kind, Vector3 position, int side, long stepId, uint contactEpoch)
        { Kind = kind; Position = position; WallSide = side; StepId = stepId; ContactEpoch = contactEpoch; }
    }

    internal readonly struct SideViewMotion
    {
        public readonly float? X, Y;
        public readonly bool ConstrainedPose, CanLand;
        public SideViewMotion(float? x, float? y, bool constrainedPose = false, bool canLand = false)
        { X = x; Y = y; ConstrainedPose = constrainedPose; CanLand = canLand; }
    }

    internal sealed class SideViewTraversal
    {
        private enum ClimbPhase { Lift, Cross, Land, ReturnAcross, ReturnDown }

        private const float GrabTapMaxHold = .08f;
        private const float GrabTapGrace = .10f;

        private readonly CharacterMovement motor;
        private readonly SideViewLedgeProbe probe;
        private readonly SideViewLedgeInput ledgeInput = new SideViewLedgeInput();
        private SideLedge anchor, lastDropped;
        private SideWallContact contact, kickedWall;
        private ClimbPhase climbPhase;
        private bool announcedHang, landedFromClimb, climbCommitted;
        private int lockSteps, sinceKick, lastDropInput, wallSide;
        private int failedClimbIntent;
        private bool climbFailed, climbPathWasBlocked;
        private Collider climbObstacle;
        private Bounds climbObstacleBounds;
        private Matrix4x4 climbObstacleTransform;
        private float slideTime, slideSpeed;
        private SideTraversalEventKind? pendingEvent;
        private int eventSide;
        private Vector3 plannedFrom, plannedTo;
        private int blockedSteps;
        private int grabTapSide, grabTapDevice, grabTapControlRevision;
        private float grabTapHeldSeconds, grabTapRemaining;

        public SideViewTraversal(CharacterMovement motor)
        { this.motor = motor; probe = motor.CreateLedgeProbe(); }

        public SideViewTraversalMode Mode { get; private set; }
        public int Revision { get; private set; }
        public int WallSide => wallSide;
        public string LastRejection { get; private set; }
        public bool BlocksAttack => Mode == SideViewTraversalMode.LedgeHang || Mode == SideViewTraversalMode.LedgeClimb;
        public bool OwnsAirState => BlocksAttack || Mode == SideViewTraversalMode.WallSlide || lockSteps > 0;

        public void Reset()
        {
            anchor = lastDropped = default;
            contact = kickedWall = default;
            announcedHang = landedFromClimb = false;
            lockSteps = sinceKick = lastDropInput = 0;
            slideTime = slideSpeed = 0f;
            blockedSteps = 0;
            plannedFrom = plannedTo = default;
            pendingEvent = null;
            ledgeInput.Suspend();
            ClearGrabTap();
            ClearClimbFailure();
            LastRejection = null;
            ChangeMode(SideViewTraversalMode.Grounded);
        }

        public void ObserveInput(int input, bool playerControl, int revision, int device)
        {
            int previous = ledgeInput.Direction;
            ledgeInput.Observe(input, revision, playerControl, device);
            int current = ledgeInput.Direction;

            if (!playerControl || motor.Grounded || BlocksAttack || lockSteps > 0 ||
                (grabTapSide != 0 && (revision != grabTapControlRevision ||
                    (device >= 0 && device != grabTapDevice) || (input != 0 && input != grabTapSide))))
            {
                ClearGrabTap();
                return;
            }

            if (previous == 0 && current != 0)
            {
                grabTapSide = current;
                grabTapDevice = device;
                grabTapControlRevision = revision;
                grabTapHeldSeconds = grabTapRemaining = 0f;
            }
            else if (input == 0 && previous != 0 && previous == grabTapSide)
            {
                grabTapRemaining = GrabTapGrace;
            }
        }

        public void ClearGrabTap()
        {
            grabTapSide = 0;
            grabTapHeldSeconds = grabTapRemaining = 0f;
        }

        public void SampleContact(bool canTraverse, SideViewTraversalSettings settings)
        {
            contact = default;
            if (!canTraverse || !ledgeInput.HasControl || motor.Grounded || BlocksAttack || lockSteps > 0) return;
            int direction = ledgeInput.HeldDirection;
            int preferred = direction != 0 ? direction : wallSide != 0 ? wallSide : 1;
            contact = probe.Wall(preferred, settings);
            if (!contact.Exists) contact = probe.Wall(-preferred, settings);
        }

        public void SuspendInput()
        {
            ledgeInput.Suspend();
            ClearGrabTap();
            contact = default;
        }

        public bool CanKick(SideViewTraversalSettings settings) => settings?.enabled == true && settings.allowWallJump && contact.Exists &&
            (!kickedWall.Exists || kickedWall.Hit.collider != contact.Hit.collider || kickedWall.Side != contact.Side ||
             sinceKick >= settings.wallJumpSameWallSteps);

        public void Kick(SideViewTraversalSettings settings)
        {
            ClearGrabTap();
            kickedWall = contact;
            lockSteps = Mathf.Max(1, settings.wallJumpLongLockSteps);
            sinceKick = 0;
            SetEvent(SideTraversalEventKind.WallJump, contact.Side);
            ChangeMode(SideViewTraversalMode.Airborne);
        }

        public SideViewMotion Plan(bool canOwn, float step, CharacterJumpProfile profile)
        {
            var settings = profile.traversal;
            plannedFrom = plannedTo = motor.transform.position;
            if (!canOwn)
            {
                ClearGrabTap();
                anchor = default;
                ClearClimbFailure();
                landedFromClimb = announcedHang = false;
                blockedSteps = 0;
                lockSteps = 0;
                contact = default;
                ChangeMode(motor.Grounded ? SideViewTraversalMode.Grounded : SideViewTraversalMode.Airborne);
                return default;
            }
            if (!settings.allowWallJump) lockSteps = 0;
            if (lockSteps > 0)
            {
                int elapsed = settings.wallJumpLongLockSteps - lockSteps;
                bool first = elapsed == 0;
                bool shorten = ledgeInput.HeldDirection == kickedWall.Side && elapsed >= settings.wallJumpShortLockSteps;
                if (!shorten)
                {
                    lockSteps--;
                    float progress = (float)elapsed / Mathf.Max(1, settings.wallJumpLongLockSteps);
                    float speed = Mathf.Lerp(settings.wallJumpKickSpeed, profile.runSpeed, progress);
                    return new SideViewMotion(-kickedWall.Side * speed, first ? settings.wallJumpUpSpeed : (float?)null);
                }
                lockSteps = 0;
            }
            if (BlocksAttack)
            {
                if (!anchor.IsValid)
                {
                    LastRejection = "ledge-anchor-lost";
                    AbandonAnchor();
                    return default;
                }
                return Mode == SideViewTraversalMode.LedgeHang ? Hang(step, settings) : Climb(step, settings);
            }
            if (motor.Grounded)
            {
                ClearGrabTap();
                ChangeMode(SideViewTraversalMode.Grounded);
                return default;
            }
            if (TryFindGrabLedge(step, settings, out var ledge, out var reason))
            {
                if (!ledge.SameEdge(lastDropped) || ledgeInput.Revision != lastDropInput)
                {
                    anchor = ledge;
                    ClearGrabTap();
                    ClearClimbFailure();
                    announcedHang = false;
                    blockedSteps = 0;
                    LastRejection = null;
                    ChangeMode(SideViewTraversalMode.LedgeHang, ledge.Wall.Side);
                    return Hang(step, settings);
                }
                LastRejection = "ledge-input-already-consumed";
            }
            else if (reason != null) LastRejection = reason;
            if (settings.allowWallSlide && contact.Exists && ledgeInput.HeldDirection == contact.Side && motor.VerticalSpeed <= 0f)
            {
                if (Mode != SideViewTraversalMode.WallSlide || WallSide != contact.Side)
                { slideTime = 0f; slideSpeed = 0f; }
                ChangeMode(SideViewTraversalMode.WallSlide, contact.Side);
                slideTime += step;
                if (slideTime > settings.wallSlideStickSeconds)
                    slideSpeed = Mathf.Min(settings.wallSlideMaxSpeed, slideSpeed + settings.wallSlideAcceleration * step);
                return new SideViewMotion(null, -slideSpeed);
            }
            ChangeMode(SideViewTraversalMode.Airborne);
            return default;
        }

        private bool TryFindGrabLedge(float step, SideViewTraversalSettings settings,
            out SideLedge ledge, out string reason)
        {
            ledge = default;
            reason = null;
            bool canGrip = motor.VerticalSpeed <= settings.gripMaxRisingSpeed;
            float sweep = Mathf.Max(0f, -motor.VerticalSpeed * step);
            int direction = ledgeInput.Direction;
            if (direction != 0)
            {
                if (grabTapSide != 0)
                {
                    grabTapHeldSeconds += step;
                    if (grabTapHeldSeconds > GrabTapMaxHold + .00001f) ClearGrabTap();
                }
                return canGrip && contact.Side == direction && contact.Exists &&
                    probe.TryLedge(contact, sweep, settings, out ledge, out reason);
            }

            if (grabTapRemaining <= .00001f) return false;
            // 松开的短按优先使用原抓边资格，扩展检查不改变墙滑、墙跳或长按翻越。
            var wall = contact.Side == grabTapSide ? contact :
                contact.Exists ? probe.Wall(grabTapSide, settings) : default;
            var tapWall = wall.Exists ? wall : probe.TapWall(grabTapSide, settings);
            grabTapRemaining -= step;
            if (!tapWall.Exists || grabTapRemaining <= .00001f) ClearGrabTap();
            if (!canGrip || !tapWall.Exists) return false;
            return (wall.Exists && probe.TryLedge(wall, sweep, settings, out ledge, out reason)) ||
                probe.TryTapLedge(tapWall, sweep, settings, out ledge, out reason);
        }

        public void Observe(SideViewTraversalSettings settings)
        {
            if (sinceKick < int.MaxValue) sinceKick++;
            if (motor.Grounded)
            {
                ClearGrabTap();
                kickedWall = default;
                lockSteps = 0;
                lastDropped = default;
                ClearClimbFailure();
                if (landedFromClimb) SetEvent(SideTraversalEventKind.ClimbComplete, anchor.Wall.Side);
                landedFromClimb = false;
                anchor = default;
                ChangeMode(SideViewTraversalMode.Grounded);
            }
            else if (Mode == SideViewTraversalMode.Grounded)
            {
                ChangeMode(SideViewTraversalMode.Airborne);
            }
            else if (Mode == SideViewTraversalMode.LedgeHang && !announcedHang && Reached(anchor.Hang))
            {
                announcedHang = true;
                SetEvent(SideTraversalEventKind.Grab, anchor.Wall.Side);
            }
            // 实际胶囊可能截断查询许可。只比较请求轴的真实进度，不能把沿墙偏移当成上攀成功。
            var requested = plannedTo - plannedFrom;
            bool blocked = BlocksAttack && requested.sqrMagnitude > .000064f &&
                Vector3.Dot(motor.transform.position - plannedFrom, requested) < requested.sqrMagnitude * .5f;
            blockedSteps = blocked ? blockedSteps + 1 : 0;
            if (blockedSteps >= 2)
            {
                blockedSteps = 0;
                if (Mode == SideViewTraversalMode.LedgeClimb && !IsReturning)
                    BeginReturn(settings, motor.LastTraversalObstacle, "ledge-movement-blocked");
                else
                {
                    LastRejection = "ledge-movement-blocked";
                    AbandonAnchor();
                }
            }
            if (pendingEvent.HasValue)
            {
                if (pendingEvent != SideTraversalEventKind.WallJump || motor.VerticalSpeed > 0f)
                    motor.EmitTraversal(pendingEvent.Value, eventSide);
                pendingEvent = null;
            }
        }

        private SideViewMotion Hang(float step, SideViewTraversalSettings settings)
        {
            var command = ledgeInput.ForWall(anchor.Wall.Side);
            if (command == LedgeInputCommand.Drop)
            {
                SetEvent(SideTraversalEventKind.Drop, anchor.Wall.Side);
                AbandonAnchor();
                return default;
            }
            if (command == LedgeInputCommand.Climb && announcedHang && CanStartClimb(settings))
            {
                ClearClimbFailure();
                climbPhase = ClimbPhase.Lift;
                climbCommitted = false;
                LastRejection = null;
                ChangeMode(SideViewTraversalMode.LedgeClimb, anchor.Wall.Side);
                return Climb(step, settings);
            }
            if (Reached(anchor.Hang)) return new SideViewMotion(0f, 0f, true);
            if (TryAlong(anchor.Hang, settings.grabSpeed, step, out var motion)) return motion;
            LastRejection = "ledge-capsule-obstructed";
            AbandonAnchor();
            return default;
        }

        private bool CanStartClimb(SideViewTraversalSettings settings)
        {
            if (!probe.ClimbClear(anchor, motor.transform.position, settings))
            {
                if (climbFailed) climbPathWasBlocked = true;
                LastRejection = "climb-path-blocked";
                return false;
            }

            // 真实 Move 曾失败但查询仍说畅通时，等待阻挡变化或玩家新意图，避免每步重启。
            if (!climbFailed || failedClimbIntent != ledgeInput.Revision || climbPathWasBlocked) return true;
            return !ReferenceEquals(climbObstacle, null) &&
                (climbObstacle == null || !climbObstacle.enabled || !climbObstacle.gameObject.activeInHierarchy ||
                 climbObstacle.bounds != climbObstacleBounds ||
                 climbObstacle.transform.localToWorldMatrix != climbObstacleTransform);
        }

        private SideViewMotion Climb(float step, SideViewTraversalSettings settings)
        {
            if (IsReturning) return ReturnToHang(step, settings);
            if (!climbCommitted)
            {
                // 胶囊中心越过台沿后完成落地，越过之前松键可沿原路径退回。
                climbCommitted = climbPhase == ClimbPhase.Cross && probe.HasCrossedLedge(anchor);
                if (!climbCommitted && ledgeInput.ForWall(anchor.Wall.Side) == LedgeInputCommand.None)
                {
                    blockedSteps = 0;
                    StartReturnToHang();
                    return ReturnToHang(step, settings);
                }
            }
            if (climbPhase == ClimbPhase.Lift && Reached(anchor.Above)) climbPhase = ClimbPhase.Cross;
            if (climbPhase == ClimbPhase.Cross && Reached(anchor.Over)) climbPhase = ClimbPhase.Land;
            if (climbPhase == ClimbPhase.Land && probe.SupportsLanding(anchor, settings))
            {
                landedFromClimb = true;
                plannedTo = plannedFrom + Vector3.down * (3f * step);
                return new SideViewMotion(0f, -3f, true, true);
            }
            Collider obstacle = null;
            if (climbPhase == ClimbPhase.Lift &&
                TryAlong(anchor.Above, settings.climbUpSpeed * settings.climbLiftSpeedScale, step, out var lift, out obstacle))
                return lift;
            if (climbPhase == ClimbPhase.Cross && TryAlong(anchor.Over, settings.climbAcrossSpeed, step, out var across, out obstacle))
                return across;

            BeginReturn(settings, obstacle, "climb-interrupted");
            return ReturnToHang(step, settings);
        }

        private bool IsReturning => climbPhase == ClimbPhase.ReturnAcross || climbPhase == ClimbPhase.ReturnDown;

        private void BeginReturn(SideViewTraversalSettings settings, Collider obstacle, string reason)
        {
            LastRejection = reason;
            climbFailed = true;
            failedClimbIntent = ledgeInput.Revision;
            climbObstacle = obstacle;
            if (obstacle != null)
            {
                climbObstacleBounds = obstacle.bounds;
                climbObstacleTransform = obstacle.transform.localToWorldMatrix;
            }
            climbPathWasBlocked = !probe.ClimbClear(anchor, anchor.Hang, settings);
            StartReturnToHang();
        }

        private void StartReturnToHang()
        {
            landedFromClimb = false;
            climbPhase = climbPhase == ClimbPhase.Lift ? ClimbPhase.ReturnDown : ClimbPhase.ReturnAcross;
        }

        private SideViewMotion ReturnToHang(float step, SideViewTraversalSettings settings)
        {
            // 沿已走过的折线退回。横移时保持原高度，不能从台面斜穿回挂点。
            if (climbPhase == ClimbPhase.ReturnAcross)
            {
                var current = motor.transform.position;
                var level = new Vector3(current.x, anchor.Above.y, current.z);
                if (!Reached(level))
                {
                    if (TryAlong(level, settings.climbUpSpeed, step, out var rise)) return rise;
                    AbandonAnchor();
                    return default;
                }
                if (!Reached(anchor.Above))
                {
                    if (TryAlong(anchor.Above, settings.climbAcrossSpeed, step, out var across)) return across;
                    AbandonAnchor();
                    return default;
                }
                climbPhase = ClimbPhase.ReturnDown;
            }
            if (Reached(anchor.Hang))
            {
                ChangeMode(SideViewTraversalMode.LedgeHang, anchor.Wall.Side);
                return new SideViewMotion(0f, 0f, true);
            }
            if (TryAlong(anchor.Hang, settings.climbUpSpeed, step, out var down)) return down;
            AbandonAnchor();
            return default;
        }

        private void AbandonAnchor()
        {
            lastDropped = anchor;
            lastDropInput = ledgeInput.Revision;
            anchor = default;
            announcedHang = landedFromClimb = false;
            ClearClimbFailure();
            ChangeMode(SideViewTraversalMode.Airborne);
        }

        private void ClearClimbFailure()
        {
            climbFailed = climbPathWasBlocked = false;
            climbObstacle = null;
        }

        private bool TryAlong(Vector3 target, float speed, float step, out SideViewMotion motion)
            => TryAlong(target, speed, step, out motion, out _);

        private bool TryAlong(Vector3 target, float speed, float step, out SideViewMotion motion, out Collider obstacle)
        {
            var current = motor.transform.position;
            var next = Vector3.MoveTowards(current, target, speed * step);
            motion = default;
            if (!probe.ClearPath(current, next, out obstacle)) return false;
            plannedFrom = current;
            plannedTo = next;
            motion = new SideViewMotion((next.x - current.x) / step, (next.y - current.y) / step, true);
            return true;
        }

        private bool Reached(Vector3 point) => (motor.transform.position - point).sqrMagnitude <= .001225f;

        private void ChangeMode(SideViewTraversalMode next, int side = 0)
        {
            if (Mode == next && wallSide == side) return;
            Mode = next;
            wallSide = side;
            Revision++;
        }

        private void SetEvent(SideTraversalEventKind kind, int side)
        { pendingEvent = kind; eventSide = side; }
    }
}
