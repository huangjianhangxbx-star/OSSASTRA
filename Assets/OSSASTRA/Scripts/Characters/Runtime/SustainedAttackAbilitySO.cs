/*
 * 脚本：SustainedAttackAbilitySO
 * 功能：让一次能力执行在起手、维持、收招间切换，并按角色模拟钟提交持续扇区攻击。
 * 边界：身体、移动、输入、消耗和命中继续由原权威处理。技能资产只存作者配置。
 * 协作：CharacterAbilities 提供发起手势的 held、来源和投递；各阶段共享同一个主动作身份。
 */
using System;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName = "OSSASTRA/Abilities/Executors/Sustained Attack")]
    public sealed class SustainedAttackAbilitySO : AbilityExecutorSO
    {
        public ActionDefinitionSO loopAction;
        public ActionDefinitionSO endAction;
        [Range(0f, 1f)] public float movementScale = 1f;
        [Min(.01f)] public float pulseInterval = .20f;

        public override bool UsesBodyAction => true;

        public override void Validate(AbilityDefinitionSO definition)
        {
            if (definition.action == null || loopAction == null || endAction == null ||
                definition.action == loopAction || definition.action == endAction || loopAction == endAction)
                throw new ArgumentException("持续攻击需要独立的起手、循环和收招动作。");
            if (float.IsNaN(movementScale) || movementScale < 0f || movementScale > 1f ||
                float.IsInfinity(pulseInterval) || float.IsNaN(pulseInterval) || pulseInterval < .01f)
                throw new ArgumentException("持续攻击移速倍率或伤害间隔无效。");
            if (definition.delivery.mode != DeliveryMode.Sector || definition.delivery.activeDuration != 0f ||
                definition.releases.Length != 1 || definition.releases[0].damage <= 0 || definition.releases[0].at != 0f ||
                definition.holdCostPerSecond != 0f ||
                (definition.occupancy & (AbilityOccupancy.Body | AbilityOccupancy.MainHand)) !=
                    (AbilityOccupancy.Body | AbilityOccupancy.MainHand))
                throw new ArgumentException("持续攻击需要占用身体与主手、单个伤害模板和瞬时扇区。");
            if (!string.IsNullOrWhiteSpace(definition.action.postureRecoveryEvent) ||
                !string.IsNullOrWhiteSpace(loopAction.postureRecoveryEvent))
                throw new ArgumentException("持续攻击的架势恢复事件应配置在收招。");

            var start = definition.action.Freeze();
            var loop = loopAction.Freeze();
            var end = endAction.Freeze();
            if (start.FinishAt <= 0 || loop.FinishAt <= 0 || end.FinishAt <= 0 || end.AttackReadyAt != end.FinishAt)
                throw new ArgumentException("三段需要正时长，收招结束前仍占用主攻击。");
        }

        public override CompiledAbilityExecutor CompileExecutor()
            => new Executor(loopAction, endAction, movementScale, pulseInterval);

        private sealed class Executor : CompiledAbilityExecutor
        {
            private readonly ActionDefinitionSO loopAction, endAction;
            private readonly float movementScale, pulseInterval;
            private readonly ConfiguredActionSnapshot loop, end;
            private readonly string recoveryEvent;

            public Executor(ActionDefinitionSO loopAction, ActionDefinitionSO endAction, float movementScale, float pulseInterval)
            {
                this.loopAction = loopAction;
                this.endAction = endAction;
                this.movementScale = movementScale;
                this.pulseInterval = pulseInterval;
            }

            private Executor(ConfiguredActionSnapshot start, ConfiguredActionSnapshot loop, ConfiguredActionSnapshot end,
                float movementScale, float pulseInterval, string recoveryEvent)
            {
                InitialPhase = start.WithTimeline(start.Timeline.WithoutTimedNodes());
                this.loop = loop.WithTimeline(loop.Timeline.WithoutTimedNodes(allowMovement: true, retainPoseWhileMoving: true));
                this.end = end.WithTimeline(end.Timeline.WithoutReleaseNode());
                this.movementScale = movementScale;
                this.pulseInterval = pulseInterval;
                this.recoveryEvent = recoveryEvent;
            }

            public override bool UsesBodyAction => true;
            public override ConfiguredActionSnapshot InitialPhase { get; }
            public override bool RepeatWhileHeld => false;

            public override CompiledAbilityExecutor Prepare(CharacterRoot owner, IAbilitySource source,
                CharacterAnimationPreparation preparation, ActionDefinitionSO initialAction)
            {
                // 技能三段在一次接受事务中解析。起手取同一份已请求的能力定义。
                if (initialAction == null)
                    throw new ArgumentException("持续攻击缺少当前请求的起手动作。");
                var first = owner.FreezeAbilityPhase(initialAction, preparation);
                var held = owner.FreezeAbilityPhase(loopAction, preparation);
                var last = owner.FreezeAbilityPhase(endAction, preparation);
                if (string.IsNullOrWhiteSpace(first.Clip) || string.IsNullOrWhiteSpace(held.Clip) ||
                    string.IsNullOrWhiteSpace(last.Clip) || first.Clip == held.Clip ||
                    first.Clip == last.Clip || held.Clip == last.Clip || first.Loop || !held.Loop || last.Loop)
                    throw new ArgumentException("持续攻击需要三个不同的身体片段，循环只在持续段开启。");
                return new Executor(first, held, last, movementScale, pulseInterval, endAction.postureRecoveryEvent);
            }

            public override AbilityRun CreateRun(AbilityRunContext context)
                => new Run(context, InitialPhase.Timeline.FinishAt, loop, end, movementScale, pulseInterval, recoveryEvent);
        }

        private sealed class Run : AbilityRun, ICharacterLocomotionScale
        {
            private enum Phase { Start, Loop, End }

            private readonly ConfiguredActionSnapshot loop, end;
            private readonly double startLength, interval;
            private readonly float movementScale;
            private readonly int inputGeneration, controlRevision, damage;
            private readonly float impact;
            private readonly string recoveryEvent;
            private Phase phase;
            private bool releaseRequested;
            private double nextPulse;
            private int index;

            public Run(AbilityRunContext context, double startLength, ConfiguredActionSnapshot loop,
                ConfiguredActionSnapshot end, float movementScale, double interval, string recoveryEvent) : base(context)
            {
                this.startLength = startLength;
                this.loop = loop;
                this.end = end;
                this.movementScale = movementScale;
                this.interval = interval;
                this.recoveryEvent = recoveryEvent;
                inputGeneration = context.owner.StateInput.GestureGeneration;
                controlRevision = context.owner.ActionControlRevision;
                damage = context.definition.plan.Emissions[0].Damage;
                impact = context.definition.posture[0];
            }

            public float LocomotionScale => !Ended && phase == Phase.Loop ? movementScale : 1f;

            public override void Tick(float delta, bool held)
            {
                if (Ended) return;
                var owner = Context.owner;
                if (owner.StateArbitrator.Combat.Runtime.ActionId != Context.id)
                {
                    Ended = true; // 收招完成时，原身体动作先发 Finished。
                    return;
                }

                // 正常松键保留输入代际。设备拔出、失控和 UI 捕获会撤销整个技能。
                if (owner.ActionControlRevision != controlRevision || owner.StateInput.GestureGeneration != inputGeneration ||
                    !owner.HasPlayerInput || owner.StateInput.InventoryCaptured)
                {
                    Abort("hold-input-invalidated");
                    return;
                }
                if (phase == Phase.End) return;

                releaseRequested |= !held;
                var now = owner.SimulationTime;
                if (phase == Phase.Start)
                {
                    if (now - Context.acceptedAt < startLength) return;
                    if (releaseRequested) ChangeToEnd();
                    else if (owner.ChangeAbilityPhase(Context.id, loop))
                    {
                        phase = Phase.Loop;
                        nextPulse = now + interval;
                    }
                    else Abort("hold-loop-phase-unavailable");
                    return;
                }

                if (releaseRequested)
                {
                    ChangeToEnd();
                    return;
                }
                if (now < nextPulse) return;
                if (!Context.TryCommitSource())
                {
                    Abort("hold-release-source-invalid");
                    return;
                }
                // 慢帧跳过错过的节拍，避免一次补发多次接触。每次仍走独立投递索引。
                nextPulse += (Math.Floor((now - nextPulse) / interval) + 1) * interval;
                Context.Deliver(new AbilityEmission(0, damage, index++), Context.definition.delivery,
                    Context.definition.damageChannel, impact);
            }

            private void ChangeToEnd()
            {
                phase = Phase.End;
                if (!Context.owner.ChangeAbilityPhase(Context.id, end, recoveryEvent))
                    Abort("hold-end-phase-unavailable");
            }

            private void Abort(string reason)
            {
                if (!Context.owner.CancelOwnedMainAction(Context.id, reason)) Cancel();
            }
        }
    }
}
