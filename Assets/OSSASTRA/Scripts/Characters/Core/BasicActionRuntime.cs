/*
 * 脚本：BasicActionRuntime
 * 功能：维护单角色的动作身份、时钟与Accepted／Released／Ready／终结事实。
 * 边界：不采设备、不驱动Spine、不查询碰撞、不直接改HP。
 * 协作：Root请求并推进；Release对象交世界适配器；暂停不推进时钟。
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ossastra.CharacterCore
{
    public enum ActionFactKind { Accepted, Released, MoveReady, AttackReady, Finished, Cancelled, PhaseChanged }

    // 事实是结果，不是任意广播；调用者按返回顺序处理。
    public sealed class ActionFact
    {
        public ActionFactKind Kind { get; }
        public Guid ActionId { get; }
        public string Source { get; }
        public string Reason { get; }
        public double At { get; }
        public AttackRelease Release { get; }

        internal ActionFact(ActionFactKind kind, Guid id, string source, double at,
            string reason = null, AttackRelease release = null)
        {
            Kind = kind;
            ActionId = id;
            Source = source;
            At = at;
            Reason = reason;
            Release = release;
        }
    }

    public sealed class ActionRequestResult
    {
        public bool Accepted => Fact != null;
        public string Reason { get; }
        public ActionFact Fact { get; }
        public ActionFact PreviousTerminal { get; }

        internal ActionRequestResult(string reason, ActionFact fact = null, ActionFact previousTerminal = null)
        {
            Reason = reason;
            Fact = fact;
            PreviousTerminal = previousTerminal;
        }
    }

    // 单角色单动作权威，不读设备/Spine/世界碰撞，也不直接写HP。
    public sealed class BasicActionRuntime
    {
        private readonly string actorId;
        private BasicAttackDefinition definition;
        private (double At, ActionFactKind Kind)[] nodes;
        private int nextNode;
        private double elapsed;
        private double timeCorrection;
        private Guid actionId;
        private string source;
        private bool moveReady;
        private bool attackReady;

        public bool IsActive => definition != null;
        public bool MoveReady => !IsActive || moveReady;
        public bool AttackReady => !IsActive || attackReady;
        public Guid ActionId => IsActive ? actionId : Guid.Empty;
        // 同一时钟的绝对读数供有限接续窗口使用；没有动作时也推进，不另建连段计时器。
        public double SimulationTime { get; private set; }

        public BasicActionRuntime(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
            this.actorId = actorId;
        }

        public ActionRequestResult Request(BasicAttackDefinition attack, string requestSource,
            ControlAuthority gate, bool paused = false)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (gate == null) throw new ArgumentNullException(nameof(gate));
            if (string.IsNullOrWhiteSpace(requestSource)) throw new ArgumentException("Request source required", nameof(requestSource));
            if (paused) return new ActionRequestResult("paused");
            if (!gate.Allows(ControlChannel.Attack)) return new ActionRequestResult("attack-blocked");
            if (IsActive && !attackReady) return new ActionRequestResult("busy");
            // Ready允许一次新按下；显式终结旧身体动作，不遗失其已释放效果。
            var previousTerminal = IsActive ? Cancel("replaced-by-next-attack") : null;
            definition = attack;
            source = requestSource;
            actionId = Guid.NewGuid();
            SetPhase(attack);
            return new ActionRequestResult(null, Fact(ActionFactKind.Accepted, 0), previousTerminal);
        }

        /// <summary>只切换当前动作的阶段；保留受理身份与总时钟，不重复受理或支付。</summary>
        public ActionFact Rephase(Guid expectedActionId, BasicAttackDefinition phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            if (!IsActive || expectedActionId != actionId) return null;
            SetPhase(phase);
            return Fact(ActionFactKind.PhaseChanged, 0);
        }

        private void SetPhase(BasicAttackDefinition attack)
        {
            definition = attack;
            elapsed = 0;
            timeCorrection = 0;
            nextNode = 0;
            moveReady = false;
            attackReady = false;
            // 两种Ready可互换先后；同一时刻固定Release→Move→Attack→Finish。
            nodes = new[] {
                (attack.ReleaseAt, ActionFactKind.Released),
                (attack.MoveReadyAt, ActionFactKind.MoveReady),
                (attack.AttackReadyAt, ActionFactKind.AttackReady),
                (attack.FinishAt, ActionFactKind.Finished)
            }.OrderBy(n => n.Item1).ThenBy(n => n.Item2).ToArray();
        }

        public IReadOnlyList<ActionFact> Advance(double delta, bool paused)
        {
            if (!BasicAttackDefinition.Finite(delta) || delta < 0)
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (paused) return Array.Empty<ActionFact>();
            SimulationTime += delta;
            if (!IsActive) return Array.Empty<ActionFact>();
            // 补偿累计舍入误差；不靠提前Release的任意epsilon修补帧边界。
            var correctedDelta = delta - timeCorrection;
            var sum = elapsed + correctedDelta;
            timeCorrection = (sum - elapsed) - correctedDelta;
            elapsed = Math.Min(definition.FinishAt, sum);
            if (elapsed == definition.FinishAt) timeCorrection = 0;
            var facts = new List<ActionFact>();
            while (nextNode < nodes.Length && elapsed >= nodes[nextNode].At)
            {
                var node = nodes[nextNode++];
                AttackRelease release = null;
                if (node.Kind == ActionFactKind.Released)
                    release = new AttackRelease(actionId, actorId, definition.Damage, definition.PostureImpact);
                if (node.Kind == ActionFactKind.MoveReady) moveReady = true;
                if (node.Kind == ActionFactKind.AttackReady) attackReady = true;
                facts.Add(Fact(node.Kind, node.At, release: release));
                if (node.Kind == ActionFactKind.Finished) definition = null;
            }
            return facts;
        }

        public ActionFact Cancel(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation cause required", nameof(reason));
            if (!IsActive) return null;
            var terminal = Fact(ActionFactKind.Cancelled, elapsed, reason);
            definition = null;
            return terminal;
        }

        private ActionFact Fact(ActionFactKind kind, double at, string reason = null, AttackRelease release = null)
            => new ActionFact(kind, actionId, source, at, reason, release);
    }
}
