/*
 * 横版墙边运动子状态。拥有挂点、输入边沿、翻越阶段和墙跳锁步。
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
        private enum ClimbPhase { Lift, Cross, Land, Return }

        private readonly CharacterMovement motor;
        private readonly SideViewLedgeProbe probe;
        private readonly SideViewLedgeInput ledgeInput = new SideViewLedgeInput();
        private SideLedge anchor, lastDropped;
        private SideWallContact contact, kickedWall;
        private ClimbPhase climbPhase;
        private bool announcedHang, landedFromClimb;
        private int lockSteps, sinceKick, direction, directionRevision, lastDropInput, wallSide;
        private int controlRevision;
        private bool ownedControl;
        private float slideTime, slideSpeed;
        private SideTraversalEventKind? pendingEvent;
        private int eventSide;
        private Vector3 plannedFrom, plannedTo;
        private int blockedSteps;

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
            lockSteps = sinceKick = direction = directionRevision = lastDropInput = 0;
            slideTime = slideSpeed = 0f;
            blockedSteps = 0;
            plannedFrom = plannedTo = default;
            pendingEvent = null;
            ownedControl = false;
            LastRejection = null;
            ChangeMode(SideViewTraversalMode.Grounded);
        }

        public void Sample(int input, bool playerControl, int revision, SideViewTraversalSettings settings)
        {
            if (direction != input || controlRevision != revision || ownedControl != playerControl) directionRevision++;
            direction = input;
            controlRevision = revision;
            ownedControl = playerControl;
            contact = default;
            if (!playerControl || motor.Grounded || BlocksAttack || lockSteps > 0) return;
            int preferred = direction != 0 ? direction : wallSide != 0 ? wallSide : 1;
            contact = probe.Wall(preferred, settings);
            if (!contact.Exists) contact = probe.Wall(-preferred, settings);
        }

        public void SuspendInput()
        {
            ownedControl = false;
            contact = default;
            ledgeInput.Sample(0, controlRevision, false);
        }

        public bool CanKick(SideViewTraversalSettings settings) => contact.Exists &&
            (!kickedWall.Exists || kickedWall.Hit.collider != contact.Hit.collider || kickedWall.Side != contact.Side ||
             sinceKick >= settings.wallJumpSameWallSteps);

        public void Kick(SideViewTraversalSettings settings)
        {
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
                anchor = default;
                landedFromClimb = announcedHang = false;
                blockedSteps = 0;
                lockSteps = 0;
                contact = default;
                ChangeMode(motor.Grounded ? SideViewTraversalMode.Grounded : SideViewTraversalMode.Airborne);
                return default;
            }
            if (lockSteps > 0)
            {
                int elapsed = settings.wallJumpLongLockSteps - lockSteps;
                bool first = elapsed == 0;
                bool shorten = direction == kickedWall.Side && elapsed >= settings.wallJumpShortLockSteps;
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
                    anchor = default;
                    ChangeMode(SideViewTraversalMode.Airborne);
                    return default;
                }
                return Mode == SideViewTraversalMode.LedgeHang ? Hang(step, settings) : Climb(step, settings);
            }
            if (motor.Grounded)
            {
                ChangeMode(SideViewTraversalMode.Grounded);
                return default;
            }
            if (contact.Exists && ownedControl && direction == contact.Side && motor.VerticalSpeed <= settings.gripMaxRisingSpeed)
            {
                if (probe.TryLedge(contact, Mathf.Max(0, -motor.VerticalSpeed * step), settings, out var ledge, out var reason))
                {
                    if (!ledge.SameEdge(lastDropped) || directionRevision != lastDropInput)
                    {
                        anchor = ledge;
                        ledgeInput.Enter(direction, controlRevision);
                        announcedHang = false;
                        blockedSteps = 0;
                        LastRejection = null;
                        ChangeMode(SideViewTraversalMode.LedgeHang, direction);
                        return Hang(step, settings);
                    }
                    LastRejection = "ledge-input-already-consumed";
                }
                else LastRejection = reason;
            }
            if (contact.Exists && direction == contact.Side && motor.VerticalSpeed <= 0f)
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

        public void Observe(SideViewTraversalSettings settings)
        {
            if (sinceKick < int.MaxValue) sinceKick++;
            if (motor.Grounded)
            {
                kickedWall = default;
                lockSteps = 0;
                lastDropped = default;
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
            // 环境净空与角色碰撞可截断合法的查询目标，禁止顶着障碍反复推进。
            bool blocked = BlocksAttack && (plannedTo - plannedFrom).sqrMagnitude > .0004f &&
                (motor.transform.position - plannedFrom).sqrMagnitude < .000025f;
            blockedSteps = blocked ? blockedSteps + 1 : 0;
            if (blockedSteps >= 2)
            {
                blockedSteps = 0;
                landedFromClimb = false;
                LastRejection = "ledge-movement-blocked";
                if (Mode == SideViewTraversalMode.LedgeClimb && climbPhase != ClimbPhase.Return &&
                    probe.ClearPath(motor.transform.position, anchor.Hang, settings)) climbPhase = ClimbPhase.Return;
                else
                {
                    lastDropped = anchor;
                    lastDropInput = directionRevision;
                    anchor = default;
                    ChangeMode(SideViewTraversalMode.Airborne);
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
            var command = ledgeInput.Sample(direction, controlRevision, ownedControl);
            if (command == LedgeInputCommand.Drop)
            {
                lastDropped = anchor;
                lastDropInput = directionRevision;
                SetEvent(SideTraversalEventKind.Drop, anchor.Wall.Side);
                anchor = default;
                ChangeMode(SideViewTraversalMode.Airborne);
                return default;
            }
            if (command == LedgeInputCommand.Climb && announcedHang)
            {
                if (probe.ClimbClear(anchor, settings))
                {
                    climbPhase = ClimbPhase.Lift;
                    LastRejection = null;
                    ChangeMode(SideViewTraversalMode.LedgeClimb, anchor.Wall.Side);
                    return Climb(step, settings);
                }
                LastRejection = "climb-path-blocked";
            }
            return Along(anchor.Hang, settings.grabSpeed, step, settings);
        }

        private SideViewMotion Climb(float step, SideViewTraversalSettings settings)
        {
            if (climbPhase == ClimbPhase.Lift && Reached(anchor.Above)) climbPhase = ClimbPhase.Cross;
            if (climbPhase == ClimbPhase.Cross && Reached(anchor.Over)) climbPhase = ClimbPhase.Land;
            if (climbPhase == ClimbPhase.Return && Reached(anchor.Hang))
            {
                ledgeInput.Enter(anchor.Wall.Side, controlRevision);
                ledgeInput.Sample(direction, controlRevision, ownedControl);
                ChangeMode(SideViewTraversalMode.LedgeHang, anchor.Wall.Side);
                return new SideViewMotion(0f, 0f, true);
            }
            if (climbPhase == ClimbPhase.Land && probe.SupportsLanding(anchor, settings))
            {
                landedFromClimb = true;
                plannedTo = plannedFrom + Vector3.down * (3f * step);
                return new SideViewMotion(0f, -3f, true, true);
            }
            if (climbPhase == ClimbPhase.Lift && probe.ClearPath(motor.transform.position, anchor.Above, settings))
                return Along(anchor.Above, settings.climbUpSpeed, step, settings);
            if (climbPhase == ClimbPhase.Cross && probe.ClearPath(motor.transform.position, anchor.Over, settings))
                return Along(anchor.Over, settings.climbAcrossSpeed, step, settings);
            if (probe.ClearPath(motor.transform.position, anchor.Hang, settings))
            {
                climbPhase = ClimbPhase.Return;
                landedFromClimb = false;
                return Along(anchor.Hang, settings.climbUpSpeed, step, settings);
            }
            LastRejection = "climb-interrupted";
            anchor = default;
            landedFromClimb = false;
            ChangeMode(SideViewTraversalMode.Airborne);
            return default;
        }

        private SideViewMotion Along(Vector3 target, float speed, float step, SideViewTraversalSettings settings)
        {
            var current = motor.transform.position;
            var next = Vector3.MoveTowards(current, target, speed * step);
            if (!probe.ClearPath(current, next, settings))
            {
                LastRejection = "ledge-capsule-obstructed";
                anchor = default;
                ChangeMode(SideViewTraversalMode.Airborne);
                return default;
            }
            plannedFrom = current;
            plannedTo = next;
            return new SideViewMotion((next.x - current.x) / step, (next.y - current.y) / step, true);
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
