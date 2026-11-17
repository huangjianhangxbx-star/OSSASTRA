/*
 * 脚本：SideViewEnemyTactics
 * 功能：六种当前横版敌人的待机、觉醒、运动和招式策略。
 * 职责：每种策略消费同一套感知、地形和动作事实，经本体唯一运动器执行。
 * 边界：动作提交和一次性命中权威留在 EnemyRoot，表现只读取状态。
 */
using Ossastra.CharacterCore;
using Ossastra.EnemyCore;
using UnityEngine;

namespace Ossastra.Enemies
{
    public sealed partial class SideViewEnemyBrain
    {
        private abstract class Tactics
        {
            protected readonly SideViewEnemyBrain Brain;
            protected Tactics(SideViewEnemyBrain brain) => Brain = brain;
            protected SideViewEnemyPhase Phase => Brain.Phase;
            protected Vector3 Direction => Brain.Direction;
            protected Vector3 Position => Brain.Position;
            protected Vector3 Home => Brain.home;
            protected bool HasTarget => Brain.HasTarget;
            protected bool Acting => Phase >= SideViewEnemyPhase.Windup;
            protected float Age(float now) => Brain.PhaseAge(now);
            protected float WalkSpeed => Brain.walkSpeed;
            protected float DashSpeed => Brain.attackSpeed;
            protected float Reach => Brain.attackRange;
            protected Vector3 Offset => Brain.TargetOffset;

            public virtual float Clearance => 0;
            public virtual float MemorySeconds => 6;
            public virtual float Cooldown => Brain.retryDelay;
            public virtual float VisualAngle => 0;
            public virtual Vector3 HitDirection => Direction;
            public virtual string AnimationKey => Phase switch
            {
                SideViewEnemyPhase.Idle => "idle",
                SideViewEnemyPhase.Alert => "alert",
                SideViewEnemyPhase.Windup => "windup",
                SideViewEnemyPhase.Active => "active",
                _ => "recover"
            };

            public virtual void Reset(float now) { }
            public virtual void Decide(float now, bool tookDamage)
            {
                if (HasTarget) Brain.Face(Offset.x);
            }
            public virtual bool TryAttack(float now, Vector3 offset)
                => Mathf.Abs(offset.x) <= Reach && Mathf.Abs(offset.y) <= Brain.verticalRange;
            public virtual BasicAttackDefinition Timing(BasicAttackDefinition authored) => authored;
            public virtual void Commit(float now) { }
            public virtual void Observe(ActionFactKind fact, float now) { }
            public virtual bool ContactEnabled(float now) => true;
            public abstract void Move(float now, float delta);

            protected static BasicAttackDefinition Nodes(BasicAttackDefinition authored, double release, double active, double recover)
                => new BasicAttackDefinition(release, release + active, release + active + recover,
                    release + active + recover, authored.Damage, authored.PostureImpact);

            protected float GroundApproach(float stopAt)
            {
                var offset = HasTarget ? Offset.x : Home.x - Position.x;
                return Mathf.Abs(offset) > stopAt ? Mathf.Sign(offset) * WalkSpeed : 0;
            }
        }

        private sealed class ThumperTactics : Tactics
        {
            private string turnClip;
            private float awakeUntil;
            private float turnUntil;
            private bool woke;
            public ThumperTactics(SideViewEnemyBrain brain) : base(brain) { }
            public override string AnimationKey
            {
                get
                {
                    if (Phase == SideViewEnemyPhase.Alert)
                        return Time.time < awakeUntil ? "wake" : "alert";
                    if (Phase == SideViewEnemyPhase.Active && turnClip != null) return turnClip;
                    return base.AnimationKey;
                }
            }

            public override void Reset(float now)
            {
                woke = false;
                awakeUntil = turnUntil = now;
                turnClip = null;
            }

            public override void Decide(float now, bool tookDamage)
            {
                base.Decide(now, tookDamage);
                if (Phase == SideViewEnemyPhase.Idle)
                {
                    woke = false;
                    return;
                }
                if (woke && !tookDamage) return;
                awakeUntil = now + (woke ? .45f : 1f);
                woke = true;
            }

            public override bool TryAttack(float now, Vector3 offset)
                => now >= awakeUntil && base.TryAttack(now, offset) && Brain.GroundAhead(Direction.x);
            public override BasicAttackDefinition Timing(BasicAttackDefinition authored) => Nodes(authored, .92, 2.4, .42);
            public override float Cooldown => .65f;
            public override void Commit(float now) => turnClip = null;
            public override bool ContactEnabled(float now) => turnClip == null;

            public override void Move(float now, float delta)
            {
                if (Phase != SideViewEnemyPhase.Active)
                {
                    Brain.Walk(0, delta);
                    return;
                }

                if (turnClip != null && now >= turnUntil)
                {
                    turnClip = null;
                    Brain.facing = -Brain.facing;
                }

                if (turnClip == null)
                {
                    var wall = Brain.WallAhead(Direction.x, DashSpeed * delta);
                    if (wall || !Brain.GroundAhead(Direction.x))
                    {
                        turnClip = wall ? "bump" : "turn";
                        turnUntil = now + (wall ? .17f : .36f);
                    }
                }
                // 碰墙／崖边暂停该次接触，续滚仍消费同一动作回执。
                Brain.Walk(turnClip == null ? Direction.x * DashSpeed : 0, delta);
            }
        }

        private sealed class FlyTactics : Tactics
        {
            private float startledUntil;
            private bool startled;
            private bool collided;
            private float collisionAt;
            public FlyTactics(SideViewEnemyBrain brain) : base(brain) { }
            public override float Clearance => 1.8f;
            public override float MemorySeconds => 8;
            public override float Cooldown => .25f;
            public override Vector3 HitDirection => Vector3.down;
            public override string AnimationKey => Phase switch
            {
                SideViewEnemyPhase.Alert when Time.time < startledUntil => "wake",
                SideViewEnemyPhase.Active when collided => "bump",
                SideViewEnemyPhase.Recovery when collided => "idle",
                _ => base.AnimationKey
            };

            public override void Reset(float now)
            {
                startledUntil = now;
                collided = startled = false;
            }

            public override void Decide(float now, bool tookDamage)
            {
                base.Decide(now, tookDamage);
                if (Phase == SideViewEnemyPhase.Idle) startled = false;
                else if (!startled || tookDamage)
                {
                    startledUntil = now + .42f;
                    startled = true;
                }
            }

            public override bool TryAttack(float now, Vector3 offset)
                => now >= startledUntil && SideViewEnemyRules.CanFlyDrill(offset.x, offset.y, Reach);
            public override BasicAttackDefinition Timing(BasicAttackDefinition authored) => Nodes(authored, .6, .6, .7);
            public override void Commit(float now) => collided = false;
            // 贴地一帧仍以真实重叠结算，之后的碰地恢复期不继续造成伤害。
            public override bool ContactEnabled(float now) => !collided || now == collisionAt;

            public override void Move(float now, float delta)
            {
                switch (Phase)
                {
                    case SideViewEnemyPhase.Active:
                        if (!collided)
                        {
                            var hit = Brain.Step(Vector3.down * DashSpeed, delta);
                            collided = (hit & CollisionFlags.Below) != 0 && Position.y - Brain.FloorY(Position.x) < .2f;
                            if (collided) collisionAt = now;
                        }
                        break;
                    case SideViewEnemyPhase.Recovery:
                        if (collided) Brain.Step(Vector3.up * (Age(now) < .24f ? 2.2f : .8f), delta);
                        break;
                    case SideViewEnemyPhase.Windup:
                        break;
                    default:
                        var buzz = new Vector3(Mathf.Sin(now * 1.8f) * .62f, Mathf.Sin(now * 2.5f) * .24f, 0);
                        var goal = Home + buzz;
                        if (HasTarget && now >= startledUntil)
                        {
                            goal = Position + Offset + Vector3.up * 1.28f;
                            goal.y = Mathf.Max(goal.y, Brain.FloorY(goal.x) + .85f);
                        }
                        Brain.Glide(goal, WalkSpeed, delta);
                        break;
                }
            }
        }

        private sealed class CrawlerTactics : Tactics
        {
            private Vector3 surface;
            private Vector3 signalSource;
            private int crawlSign;
            private float turnUntil;
            private float signalAt;
            private float signalUntil;
            private bool singing;
            public CrawlerTactics(SideViewEnemyBrain brain) : base(brain) { }
            public override Vector3 HitDirection => Vector3.zero;
            public override float VisualAngle => Vector3.SignedAngle(Vector3.up, surface, Vector3.forward);
            public override string AnimationKey => Time.time < turnUntil ? "turn"
                : Time.time >= signalAt && Time.time < signalUntil ? singing ? "sing" : "wake" : "idle";
            public override float Cooldown => .3f;

            public override void Reset(float now)
            {
                surface = Quaternion.Euler(0, 0, Brain.surfaceAngle) * Vector3.up;
                crawlSign = -1;
                turnUntil = signalAt = signalUntil = now;
                singing = false;
            }

            public bool HeardSignal(Vector3 source, bool sustained, float now)
            {
                // 只接收既成场景事件。外圈惊醒后可转向，内圈演奏暂停爬行。
                if (surface.y < .85f || (source - Position).sqrMagnitude > (sustained ? 2.25f : 6.25f))
                    return false;
                signalSource = source;
                singing = sustained;
                signalAt = now + (sustained ? .3f : .2f);
                signalUntil = signalAt + (sustained ? 1f : .55f);
                return true;
            }

            public override void Decide(float now, bool tookDamage)
            {
                if (tookDamage && HasTarget) HeardSignal(Position + Offset, false, now);
            }
            public override BasicAttackDefinition Timing(BasicAttackDefinition authored) => Nodes(authored, 0, .12, .12);
            public override bool TryAttack(float now, Vector3 offset)
                => Mathf.Abs(offset.x) <= Reach && Mathf.Abs(offset.y) <= .72f;

            public override void Move(float now, float delta)
            {
                if (now >= signalAt && now < signalUntil)
                {
                    if (!singing) crawlSign = SideViewEnemyRules.Facing(signalSource.x - Position.x, crawlSign);
                    Brain.Walk(0, delta);
                    return;
                }

                var bounds = Brain.body.bounds;
                var center = bounds.center;
                var radius = Brain.body.radius;
                var supportDistance = surface.y == 0 ? radius : bounds.extents.y;
                var tangent = new Vector3(surface.y, -surface.x, 0) * crawlSign;
                if (Brain.TryTerrain(center, tangent, supportDistance + .09f, out var inside))
                {
                    TurnTo(inside.normal, now);
                }
                else if (!Brain.TryTerrain(center + tangent * .18f, -surface, supportDistance + .23f, out _))
                {
                    // 外角从前方的斜向探针重新找同块表面；无支撑则利用重力落回地形。
                    var outside = center + tangent * (supportDistance + .15f);
                    if (Brain.TryTerrain(outside, (-surface - tangent).normalized, supportDistance + .55f, out var edge))
                        TurnTo(edge.normal, now);
                    else if (surface == Vector3.up && Brain.TryTerrain(center, Vector3.down, supportDistance + .22f, out _))
                    {
                        crawlSign = -crawlSign;
                        turnUntil = now + .25f;
                    }
                    else
                    {
                        surface = Vector3.up;
                        Brain.Walk(0, delta);
                        return;
                    }
                }
                Brain.verticalVelocity = 0;
                Brain.facing = crawlSign;
                tangent = new Vector3(surface.y, -surface.x, 0) * crawlSign;
                Brain.Step(tangent * WalkSpeed - surface * 1.7f, delta);
            }

            private void TurnTo(Vector3 normal, float now)
            {
                var axis = Mathf.Abs(normal.y) >= Mathf.Abs(normal.x)
                    ? Vector3.up * Mathf.Sign(normal.y) : Vector3.right * Mathf.Sign(normal.x);
                if (axis == surface) return;
                surface = axis;
                turnUntil = now + .25f;
            }
        }

        private sealed class GangTactics : Tactics
        {
            private bool broadSwing;
            private bool wakePlayed;
            private bool extended;
            private float wakeUntil;
            public GangTactics(SideViewEnemyBrain brain) : base(brain) { }
            public override string AnimationKey
            {
                get
                {
                    if (Phase == SideViewEnemyPhase.Alert)
                        return Time.time < wakeUntil ? "wake" : Mathf.Abs(Brain.MoveDirection.x) > .1f ? "alert" : "idle";
                    return broadSwing && Acting ? base.AnimationKey + "2" : base.AnimationKey;
                }
            }

            public override void Reset(float now)
            {
                broadSwing = extended = wakePlayed = false;
                wakeUntil = now;
            }

            public override void Decide(float now, bool tookDamage)
            {
                base.Decide(now, tookDamage);
                if (Phase == SideViewEnemyPhase.Idle) wakePlayed = false;
                else if (!wakePlayed)
                {
                    wakeUntil = now + .67f;
                    wakePlayed = true;
                }
            }

            public override bool TryAttack(float now, Vector3 offset)
            {
                if (now < wakeUntil || !base.TryAttack(now, offset) || Mathf.Abs(offset.y) > .85f) return false;
                broadSwing = Mathf.Abs(offset.x) <= Reach * .55f || Brain.WallAhead(Direction.x, .35f);
                // 远距前进招保留较长循环，贴身或地形受限时提前收势。
                extended = !broadSwing && Mathf.Abs(offset.x) > Reach * .72f && Brain.GroundAhead(Direction.x);
                return true;
            }

            public override BasicAttackDefinition Timing(BasicAttackDefinition authored)
            {
                if (broadSwing) return Nodes(authored, 1.5, 1.667, 2);
                return Nodes(authored, 2, extended ? 1.667 : .84, 2.5);
            }

            public override void Move(float now, float delta)
            {
                var velocity = GroundApproach(Reach * .83f);
                if (Acting)
                {
                    // 原帧已去除攻击根位移，A 的三段推进只由本体位移执行。
                    velocity = broadSwing ? 0 : Phase == SideViewEnemyPhase.Windup ? .85f
                        : Phase == SideViewEnemyPhase.Active ? 2.03f : .42f;
                    velocity *= Direction.x;
                }
                else if (now < wakeUntil) velocity = 0;
                Brain.Walk(velocity, delta);
            }
        }

        private sealed class MimicTactics : Tactics
        {
            private bool lunge;
            private bool awoken;
            private bool retreat;
            private float wakeUntil;
            public MimicTactics(SideViewEnemyBrain brain) : base(brain) { }
            public override string AnimationKey
            {
                get
                {
                    if (Phase == SideViewEnemyPhase.Alert && !awoken) return "idle";
                    if (Phase == SideViewEnemyPhase.Alert && Time.time < wakeUntil) return "wake";
                    if (Phase == SideViewEnemyPhase.Alert)
                        return Mathf.Abs(Brain.MoveDirection.x) > .1f ? "walk" : "alert";
                    if (Phase == SideViewEnemyPhase.Recovery && !retreat) return "alert";
                    return lunge && Acting ? base.AnimationKey + "2" : base.AnimationKey;
                }
            }

            public override void Reset(float now)
            {
                awoken = lunge = retreat = false;
                wakeUntil = now;
            }

            public override void Decide(float now, bool tookDamage)
            {
                base.Decide(now, tookDamage);
                if (Phase == SideViewEnemyPhase.Idle) awoken = false;
                else if (!awoken && (tookDamage || (HasTarget && Mathf.Abs(Offset.x) <= Reach * 1.5f)))
                {
                    wakeUntil = now + .83f;
                    awoken = true;
                }
            }

            public override bool TryAttack(float now, Vector3 offset)
            {
                if (!awoken || now < wakeUntil || !base.TryAttack(now, offset) || Mathf.Abs(offset.y) > .82f) return false;
                lunge = Mathf.Abs(offset.x) > Reach * .56f && Brain.GroundAhead(Direction.x) &&
                    !Brain.WallAhead(Direction.x, .3f);
                return lunge || Mathf.Abs(offset.x) <= Reach * .72f;
            }

            public override BasicAttackDefinition Timing(BasicAttackDefinition authored)
                => lunge ? Nodes(authored, .58, 1.42, 1.333) : Nodes(authored, .43, 1.237, 1.333);
            public override void Observe(ActionFactKind fact, float now)
            {
                if (fact == ActionFactKind.MoveReady)
                    retreat = HasTarget && Mathf.Abs(Offset.x) <= Reach && Brain.GroundAhead(-Direction.x) &&
                        !Brain.WallAhead(-Direction.x, .45f);
            }

            public override void Move(float now, float delta)
            {
                var velocity = 0f;
                if (Phase == SideViewEnemyPhase.Active && lunge && Age(now) < .42f)
                    velocity = Direction.x * DashSpeed;
                else if (Phase == SideViewEnemyPhase.Recovery && retreat && Age(now) < .68f)
                    velocity = -Direction.x * 2.1f;
                else if (Phase == SideViewEnemyPhase.Alert && awoken && now >= wakeUntil)
                    velocity = Mathf.Abs(Offset.x) > Reach * .6f ? Direction.x * 1.7f : 0;
                Brain.Walk(velocity, delta);
            }
        }

        private sealed class MotherTactics : Tactics
        {
            private bool slam;
            private bool impacted;
            private bool awakened;
            private int repeatedSwoops;
            private float wakeUntil;
            private float favoredX;
            private float attackAltitude;
            public MotherTactics(SideViewEnemyBrain brain) : base(brain) { }
            public override float Clearance => 2.2f;
            public override float Cooldown => slam ? 1.2f : 2f;
            public override Vector3 HitDirection => slam ? Vector3.down : Direction;
            public override string AnimationKey
            {
                get
                {
                    if (Phase == SideViewEnemyPhase.Alert && Time.time < wakeUntil) return "wake";
                    if (!Acting || !slam) return base.AnimationKey;
                    if (Phase == SideViewEnemyPhase.Windup)
                        return Age(Time.time) < .7f ? "windup2" : "ascend";
                    return base.AnimationKey + "2";
                }
            }

            public override void Reset(float now)
            {
                slam = impacted = awakened = false;
                repeatedSwoops = 0;
                wakeUntil = now;
            }

            public override void Decide(float now, bool tookDamage)
            {
                base.Decide(now, tookDamage);
                if (awakened || Phase != SideViewEnemyPhase.Alert) return;
                awakened = true;
                wakeUntil = now + 1.7f;
            }

            public override bool TryAttack(float now, Vector3 offset)
            {
                if (now < wakeUntil || !base.TryAttack(now, offset)) return false;
                var hp = Brain.owner.Health;
                // 前段由 HP 门强制飞扑。之后中近距才选落地招，限制飞扑连续次数。
                var canSlam = SideViewEnemyRules.MotherMaySlam(hp.Current, hp.Maximum);
                slam = canSlam && (Mathf.Abs(offset.x) < Reach * .42f || repeatedSwoops >= 2);
                return true;
            }

            public override BasicAttackDefinition Timing(BasicAttackDefinition authored)
                => slam ? Nodes(authored, 1.35, .85, .85) : Nodes(authored, .4, 1.15, .82);

            public override void Commit(float now)
            {
                favoredX = Position.x + Mathf.Clamp(Offset.x, -Reach, Reach);
                attackAltitude = Brain.FloorY(Position.x) + 1.25f;
                impacted = false;
                repeatedSwoops = slam ? 0 : repeatedSwoops + 1;
            }

            public override bool ContactEnabled(float now) => !slam || Position.y - Brain.FloorY(Position.x) < 1.2f;
            public override void Move(float now, float delta)
            {
                if (Phase == SideViewEnemyPhase.Windup)
                {
                    if (slam)
                    {
                        var goal = new Vector3(favoredX, Home.y + (Age(now) >= .7f ? 1.8f : 0), Position.z);
                        Brain.Glide(goal, WalkSpeed, delta);
                    }
                    return;
                }
                if (Phase == SideViewEnemyPhase.Active)
                {
                    if (impacted) return;
                    var flags = slam
                        ? Brain.Step(Vector3.down * DashSpeed, delta)
                        : Brain.Step(Direction * DashSpeed +
                            Vector3.up * Mathf.Clamp((attackAltitude - Position.y) * 8, -4, 4), delta);
                    impacted = slam ? (flags & CollisionFlags.Below) != 0
                        : Brain.WallAhead(Direction.x, .1f);
                    return;
                }

                var goalX = HasTarget ? Mathf.Clamp(Position.x + Offset.x * .35f, Home.x - 8, Home.x + 8) : Home.x;
                var goalY = Mathf.Max(Home.y, Brain.FloorY(goalX) + Clearance);
                Brain.Glide(new Vector3(goalX, goalY, Position.z), WalkSpeed, delta);
            }
        }
    }
}
