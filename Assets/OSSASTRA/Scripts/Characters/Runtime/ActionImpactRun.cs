/* 功能：本次正式 Accepted 的效果快照，消费动作事实及网关结算后的真实回执。
 * 边界：向运动所有者请求运动，不写 Transform / HP。离手投递按真实 Release 的引用持有快照。
 */
using System;
using System.Runtime.CompilerServices;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using EnemyActor = Ossastra.Enemies.EnemyRoot;
using UnityEngine;

namespace Ossastra.Characters
{
    internal sealed class ActionImpactRun
    {
        private sealed class ReleaseBinding
        {
            internal readonly ActionImpactRun run;
            internal readonly string channel;
            internal ReleaseBinding(ActionImpactRun run, string channel) { this.run = run; this.channel = channel; }
        }
        private static readonly ConditionalWeakTable<AttackRelease, ReleaseBinding> bindings = new ConditionalWeakTable<AttackRelease, ReleaseBinding>();
        private readonly Component owner;
        private readonly CharacterRoot player;
        private readonly EnemyActor enemy;
        private readonly CombatVitals vitals;
        private readonly CharacterController enemyBody;
        private readonly int life;
        private readonly Guid id;
        private readonly float facing;
        private readonly string source;
        private readonly ActionEffectEntry[] entries;
        private bool[] observedBindings;
        private readonly ActionEffectLedger ledger;
        private bool LegacyDash { get; set; }
        internal Guid Id => id;

        private ActionImpactRun(Component owner, CharacterRoot player, EnemyActor enemy, Guid id, Vector3 aim, string source, ActionEffectEntry[] entries)
        {
            this.owner = owner;
            this.player = player;
            this.enemy = enemy;
            vitals = player ? player.Vitals : enemy.Vitals;
            enemyBody = enemy ? enemy.GetComponent<CharacterController>() : null;
            this.id = id;
            this.source = source;
            this.entries = entries;
            ledger = new ActionEffectLedger(id, entries.Length);
            life = player ? player.LifeGeneration : vitals.DeathCount;
            facing = Mathf.Abs(aim.x) > .001f ? Mathf.Sign(aim.x) : player ? player.DataBus.VisualFacingX : 1;
        }

        internal static ActionImpactRun Accept(CharacterRoot owner, ScriptableObject definition, ActionEffectList list, Guid id, Vector3 aim, string source, bool legacyDash = false)
        {
            if (!owner || !definition || owner.StateMovement?.IsSideView != true) return null;
            var entries = ActionEffectList.Freeze(owner, definition, list);
            if (entries.Length == 0) return null;
            var run = new ActionImpactRun(owner, owner, null, id, aim, source, entries) { LegacyDash = legacyDash };
            run.Fire(ActionEffectTrigger.Accepted);
            return run;
        }

        internal static ActionImpactRun AcceptEnemy(EnemyActor owner, ActionDefinitionSO definition, Guid id)
        {
            if (!owner || !definition || !owner.Vitals || owner.Vitals.SpatialPlane != CombatPlane.SideXY) return null;
            var entries = ActionEffectList.Freeze(null, definition, definition.effects);
            if (entries.Length == 0) return null;
            var run = new ActionImpactRun(owner, null, owner, id, owner.AttackDirection, definition.name, entries);
            run.Fire(ActionEffectTrigger.Accepted);
            return run;
        }

        internal void Observe(ActionFact fact, string phaseClip = null)
        {
            if (ledger.Ended || fact.ActionId != id) return;
            switch (fact.Kind)
            {
                case ActionFactKind.PhaseChanged: Fire(ActionEffectTrigger.PhaseChanged, namedEvent: phaseClip); break;
                case ActionFactKind.AttackReady: Fire(ActionEffectTrigger.AttackReady); break;
                case ActionFactKind.MoveReady:
                    Fire(ActionEffectTrigger.MoveReady);
                    foreach (var entry in entries)
                        if (entry is InputMovementScaleEffect move && move.endAtMoveReady)
                            ActionImpactMotor.ClearInput(owner, id, move.id);
                    break;
                case ActionFactKind.Finished:
                    ReportMissingBindings();
                    End();
                    break;
                case ActionFactKind.Cancelled: End(); break;
            }
        }

        // 已绑定的编辑器试验在动作自然结束时出证。中断的动作可能从未进入相关阶段。
        private void ReportMissingBindings()
        {
            var trace = CombatTraceProbe.For(owner);
            if (trace == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (observedBindings?[i] == true) continue;
                string wanted = entry.trigger == ActionEffectTrigger.SpineEvent ? entry.spineEvent :
                    entry.trigger == ActionEffectTrigger.PhaseChanged ? entry.phaseClip : null;
                if (!string.IsNullOrEmpty(wanted))
                    trace.Note("招式效果", "本次未收到绑定", source + " / " + entry.id + " / " + entry.trigger + " / " + wanted, id, id);
            }
        }

        internal void Spine(string name) => Fire(ActionEffectTrigger.SpineEvent, namedEvent: name);
        internal void End()
        {
            if (ledger.Ended) return;
            ledger.End();
            ActionImpactMotor.Release(owner, id);
            CombatTraceProbe.For(owner)?.Note("招式效果", "结束/归还窗口", source, id);
        }

        internal void Bind(AttackRelease release, string channel)
        {
            if (!ledger.Owns(release) || bindings.TryGetValue(release, out _)) return;
            bindings.Add(release, new ReleaseBinding(this, channel));
            Fire(ActionEffectTrigger.Released, release, channel);
        }

        internal static void Contact(AttackRelease release, CombatVitals attacker, Component target, HitOutcome hit, string channel)
        {
            if (!bindings.TryGetValue(release, out var bound)) return;
            var run = bound.run;
            if (!run.owner || run.vitals != attacker) return;
            run.Fire(ActionEffectTrigger.Contact, release, string.IsNullOrEmpty(channel) ? bound.channel : channel, target, hit);
        }

        private void Fire(ActionEffectTrigger trigger, AttackRelease release = null, string channel = null,
            Component target = null, HitOutcome hit = null, string namedEvent = null)
        {
            if (!owner || !vitals || !vitals.IsAvailable) return;
            if (player ? !player.isActiveAndEnabled || player.LifeGeneration != life :
                !enemy || !enemy.isActiveAndEnabled || vitals.DeathCount != life) return;
            for (int i = 0; i < entries.Length; ++i)
            {
                var entry = entries[i];
                if (entry.trigger != trigger || trigger == ActionEffectTrigger.SpineEvent && entry.spineEvent != namedEvent ||
                    trigger == ActionEffectTrigger.PhaseChanged && !string.IsNullOrEmpty(entry.phaseClip) && entry.phaseClip != namedEvent) continue;
                if ((trigger == ActionEffectTrigger.SpineEvent || trigger == ActionEffectTrigger.PhaseChanged) && CombatTraceProbe.For(owner) != null)
                    (observedBindings ??= new bool[entries.Length])[i] = true;
                if (entry.emissionIndex >= 0 && entry.emissionIndex != release?.EmissionIndex ||
                    !string.IsNullOrEmpty(entry.channel) && entry.channel != channel) continue;
                bool onTarget = entry is TargetKnockbackEffect || entry is LaunchEffect launch && launch.target;
                if (hit != null && !ledger.AllowsContact(hit.Reason, entry.onGuard, onTarget, target && hit.HpAfter > 0)) continue;
                if (hit == null && (onTarget || ledger.Ended)) continue;
                if (entry.ground != ActionEffectGround.Any && (entry.ground == ActionEffectGround.Grounded) != (player ? player.DataBus.IsGrounded : enemyBody && enemyBody.isGrounded)) continue;
                double now = player ? player.SimulationTime : enemy.SimulationTime;
                if (!ledger.Claim(i, entry.frequency, entry.maxTriggers, entry.interval, now, release?.ActionId ?? id, hit?.TargetId ?? "")) continue;
                bool accepted = Apply(entry, target, out string reason);
                CombatTraceProbe.For(owner)?.Note("招式效果", accepted ? "激活" : "拒绝",
                    source + " / " + entry.GetType().Name + " / " + entry.id + " / " + reason, release?.ActionId ?? id, id,
                    release?.EmissionIndex ?? -1, hit?.TargetId ?? "");
            }
        }

        private bool Apply(ActionEffectEntry entry, Component target, out string reason)
        {
            if (LegacyDash && entry is SelfDisplacementEffect) { reason = "原阶段前冲优先"; return false; }
            if (entry is InputMovementScaleEffect input)
            {
                if (!player) { reason = "敌人无玩家输入窗口"; return false; }
                int direction = input.allowed == ActionInputDirection.Both ? 0 : (int)facing * (input.allowed == ActionInputDirection.Forward ? 1 : -1);
                return ActionImpactMotor.SetInput(owner, id, entry.id, input.scale, direction, input.fullBody, out reason, facing);
            }
            if (entry is LaunchEffect launch)
                return ActionImpactMotor.TryLaunch(launch.target ? target : owner, id,
                    new Vector2(facing * launch.velocity.x, launch.velocity.y), launch.target, out reason);
            if (!(entry is FixedDisplacementEffect move)) { reason = "未知词条"; return false; }
            bool onTarget = move is TargetKnockbackEffect;
            Vector2 direction2D = Vector2.right * facing;
            if (move is SelfDisplacementEffect step)
            {
                if (step.direction == ActionImpactDirection.Input)
                {
                    if (!player) { reason = "敌人无玩家方向输入"; return false; }
                    float inputX = player.DataBus.Input.move.x;
                    if (Mathf.Abs(inputX) < .0001f) { reason = "触发时无横向输入"; return false; }
                    direction2D = Vector2.right * Mathf.Sign(inputX);
                }
                if (step.direction == ActionImpactDirection.Backward) direction2D = -direction2D;
                if (step.direction == ActionImpactDirection.Custom) direction2D = step.customDirection.normalized;
            }
            else if (move is SelfRecoilEffect) direction2D = -direction2D;
            else if (move is TargetKnockbackEffect push && push.awayFromAttacker)
            {
                float offset = target.transform.position.x - owner.transform.position.x;
                if (Mathf.Abs(offset) > .001f) direction2D = Vector2.right * Mathf.Sign(offset);
            }
            return ActionImpactMotor.TryDisplace(onTarget ? target : owner, id, direction2D * move.distance,
                move.duration, move.curve, onTarget, move is SelfDisplacementEffect active && active.replaceInput, out reason, move is SelfRecoilEffect);
        }
    }
}
