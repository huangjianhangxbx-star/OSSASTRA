using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName = "OSSASTRA/Abilities/Executors/Side View Dive")]
    public sealed class SideViewDiveAbilitySO : AbilityExecutorSO
    {
        [SerializeField, Range(.01f, 1f), Tooltip("归一化方向所需的最小向下分量。")]
        private float minimumDown = .15f;
        [SerializeField, Min(0), Tooltip("俯冲开始前固定角色的准备时间。")]
        private float preparationSeconds = .08f;
        [SerializeField, Min(.1f), Tooltip("俯冲的起始速度（米/秒）。")]
        private float speed = 12f;
        [SerializeField, Min(0), Tooltip("沿冻结方向的加速度（米/秒²）。")]
        private float acceleration = 14f;

        [SerializeField, Min(.01f), Tooltip("沿实际路径扫掠的攻击球半径。")]
        private float attackRadius = .44f;
        [SerializeField, Tooltip("攻击球中心相对角色原点的高度。")]
        private float attackHeight = .42f;

        [SerializeField, Range(0, 1), Tooltip("腾起首步保留的横向速度比例。")]
        private float retainedHorizontal = .20f;
        [SerializeField, Min(.02f), Tooltip("命中腾起后的动作交接时间。")]
        private float airRecoverySeconds = .10f;
        [SerializeField, Min(.02f), Tooltip("落地收招时间。")]
        private float landRecoverySeconds = .15f;
        [SerializeField, Tooltip("可选的腾起和收招动画。留空时暂用本动作映射。")]
        private ActionDefinitionSO recoveryAction;

        public override bool UsesBodyAction => true;
        internal float MinimumDown => minimumDown;

        public override void Validate(AbilityDefinitionSO definition)
        {
            var values = new[] { minimumDown, preparationSeconds, speed, acceleration,
                attackRadius, attackHeight, retainedHorizontal, airRecoverySeconds, landRecoverySeconds };
            if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v < 0) ||
                minimumDown <= 0 || minimumDown > 1 || retainedHorizontal > 1 ||
                speed <= 0 || attackRadius <= 0 ||
                airRecoverySeconds < .02f || landRecoverySeconds < .02f ||
                definition.action == null || definition.releases.Length != 1 || definition.releases[0].damage <= 0)
                throw new ArgumentException("俯冲需要合法参数、身体动画和唯一伤害节点");
            // 本候选需要显式预览许可；正式架势资源接口未接入。
            if (definition.acceptCost != 0 || definition.releaseCost != 0 ||
                definition.holdCostPerSecond != 0 || definition.wearOnRelease)
                throw new ArgumentException("俯冲预览采用无费用样本");
        }

        public override CompiledAbilityExecutor CompileExecutor()
            => new Executor(preparationSeconds, speed, acceleration, attackRadius, attackHeight,
                retainedHorizontal, airRecoverySeconds, landRecoverySeconds, recoveryAction);

        private sealed class Executor : CompiledAbilityExecutor
        {
            public readonly float Preparation, Speed, Acceleration, Radius, Height, RetainedX, AirRecovery, LandRecovery;
            private readonly ActionDefinitionSO recovery;
            private readonly ConfiguredActionSnapshot initial, recover;

            public Executor(float preparation, float speed, float acceleration, float radius, float height,
                float retainedX, float airRecovery, float landRecovery, ActionDefinitionSO recovery,
                ConfiguredActionSnapshot initial = null, ConfiguredActionSnapshot recover = null)
            {
                Preparation = preparation; Speed = speed; Acceleration = acceleration;
                Radius = radius; Height = height; RetainedX = retainedX;
                AirRecovery = airRecovery; LandRecovery = landRecovery; this.recovery = recovery;
                this.initial = initial; this.recover = recover;
            }

            public override bool UsesBodyAction => true;
            public override ConfiguredActionSnapshot InitialPhase => initial;

            public override CompiledAbilityExecutor Prepare(CharacterRoot owner, IAbilitySource source,
                CharacterAnimationPreparation preparation)
            {
                if (!owner.TryGetComponent<SideViewDiveMount>(out var mount) || !mount.Owns(source))
                    throw new ArgumentException("俯冲来源未登记或未启用预览");
                var snapshot = owner.FreezeAbilityPhase(mount.Definition.action, preparation);
                return new Executor(Preparation, Speed, Acceleration, Radius, Height,
                    RetainedX, AirRecovery, LandRecovery, recovery,
                    snapshot.WithTimeline(snapshot.Timeline.WithoutTimedNodes()),
                    recovery == null ? null : owner.FreezeAbilityPhase(recovery, preparation));
            }

            public ConfiguredActionSnapshot Recovery(bool landed)
            {
                float finish = landed ? LandRecovery : AirRecovery;
                return (recover ?? initial).WithTimeline(new BasicAttackDefinition(0, landed ? finish : 0, finish, finish, 1));
            }

            public override AbilityRun CreateRun(AbilityRunContext context) => new Run(context, this);
        }

        private sealed class Run : AbilityRun, ISideViewAbilityMotion
        {
            private enum Phase { Preparing, Diving, BouncePending, Recovering }
            private Phase phase;
            private readonly Executor config;
            private readonly int controlRevision;
            private readonly float maximumSpeed;
            private float remainingPreparation, diveSpeed, lastHorizontalSpeed;
            private Vector2 diveDirection;
            private AttackRelease release;

            public Run(AbilityRunContext context, Executor config) : base(context)
            {
                this.config = config;
                controlRevision = context.owner.ActionControlRevision;
                remainingPreparation = config.Preparation;
                diveDirection = (Vector2)context.direction;
                diveSpeed = config.Speed;
                maximumSpeed = Mathf.Max(config.Speed, context.owner.StateMovement.JumpProfile.fallSpeed);
            }

            public override void Tick(float delta, bool held)
            {
                if (Ended) return;
                if (Context.owner.StateArbitrator.Combat.Runtime.ActionId != Context.id)
                {
                    release = null;
                    Ended = true;
                }
                else if (Context.owner.ActionControlRevision != controlRevision)
                {
                    Cancel();
                    Context.owner.CancelOwnedMainAction(Context.id, "dive-control-changed");
                }
            }

            public bool TryPlanStep(float delta, out SideViewAbilityMotionCommand command)
            {
                command = default;
                if (Ended || phase == Phase.Recovering || Context.owner.ActionControlRevision != controlRevision)
                    return false;

                if (phase == Phase.BouncePending)
                {
                    command = SideViewAbilityMotionCommand.Bounce(lastHorizontalSpeed * config.RetainedX);
                    return true;
                }
                if (remainingPreparation > .0001f)
                {
                    remainingPreparation -= delta;
                    command = SideViewAbilityMotionCommand.Drive(Vector2.zero);
                    return true;
                }
                if (phase == Phase.Preparing)
                {
                    if (!Context.TryCommitSource() || Context.owner.Vitals == null)
                    {
                        Abort("dive-source-expired");
                        return false;
                    }

                    var node = Context.definition.plan.Emissions[0];
                    var damage = Mathf.Max(1, Mathf.RoundToInt((float)Context.effects.Damage(Context.definition.damageChannel, node.Damage)));
                    release = AttackRelease.ForContactWindow(Context.id, 0, Context.owner.ActorId, damage,
                        Context.definition.posture[0], Context.definition.reclaimBudget, Context.definition.reclaimRate);
                    phase = Phase.Diving;
                    CombatContactGateway.PublishSectorRelease(release, Context.owner.Vitals,
                        Geometry(Context.owner.transform.position), config.Height);
                    Context.NotifyFeedback("release");
                }
                var velocity = diveDirection * diveSpeed;
                lastHorizontalSpeed = velocity.x;
                diveSpeed = Mathf.Min(maximumSpeed, diveSpeed + config.Acceleration * delta);
                command = SideViewAbilityMotionCommand.Drive(velocity);
                return true;
            }

            public void ObserveStep(in SideViewAbilityMotionStep step)
            {
                if (Ended) return;
                if (phase == Phase.BouncePending)
                {
                    BeginRecovery(false, "dive-bounce");
                    return;
                }
                if (phase == Phase.Preparing)
                {
                    if (step.Grounded) BeginRecovery(true, "dive-land");
                    return;
                }
                if (phase != Phase.Diving) return;

                var geometry = Geometry(step.Before).WithArrival(step.AttackEnd - step.Before);
                if (SideViewDiveSweep.Resolve(release, Context.owner.Vitals, geometry, step.AttackEnd,
                    config.Height, config.Radius, Context.definition.delivery.collisionLayers, step.TerrainBlocked))
                {
                    release = null;
                    phase = Phase.BouncePending;
                    Context.NotifyFeedback("hit");
                    return;
                }
                if (step.Grounded) BeginRecovery(true, "dive-land");
                else if (step.HitWall || step.HitHead)
                {
                    // 去掉朝障碍物的分量，沿墙或陡坡继续下冲。
                    var normal = ((Vector2)step.TerrainNormal).normalized;
                    var downhill = Vector2.down - normal * Mathf.Min(0f, Vector2.Dot(Vector2.down, normal));
                    diveDirection = downhill * -Context.direction.y;
                }
            }

            private CombatContactGeometry Geometry(Vector3 origin)
                => new CombatContactGeometry(CombatPlane.SideXY, origin, Context.direction, Context.direction);

            private void BeginRecovery(bool landed, string reason)
            {
                phase = Phase.Recovering;
                release = null;
                if (!Context.owner.ChangeAbilityPhase(Context.id, config.Recovery(landed))) Abort(reason);
                else Context.NotifyFeedback(reason);
            }

            private void Abort(string reason)
            {
                Cancel();
                Context.owner.CancelOwnedMainAction(Context.id, reason);
            }

            public override void Cancel()
            {
                release = null;
                base.Cancel();
            }
        }
    }
}
