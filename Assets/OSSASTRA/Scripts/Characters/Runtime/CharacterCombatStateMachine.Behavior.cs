/* 功能：管理一次普通招式的有界后继资格，供正式输入与原动画窗口消费。
 * 边界：Runtime 保留目标生命与来源，Core 保留纯身份、窗口和缓冲。运动、命中及图像归专职 Run。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed partial class CharacterCombatStateMachine
    {
        private sealed class BasicBehaviorConnection
        {
            public Guid Id { get; }
            public int Stage { get; }
            public bool Required { get; }
            public bool HandedOff { get; set; }
            public bool Resolved { get; set; }
            public readonly BasicChainState chain;
            public readonly IAbilitySource source;
            public CombatVitals target;
            public int targetRevision;
            public bool hasTarget;
            public readonly int sourceLife;

            public BasicBehaviorConnection(Guid id, int stage, bool required, BasicChainState chain,
                IAbilitySource source, int sourceLife)
            {
                Id = id;
                Stage = stage;
                Required = required;
                this.chain = chain;
                this.source = source;
                this.sourceLife = sourceLife;
            }
        }

        private BasicComboSettings ResolveBehaviorComboSettings(UnityEngine.Object source, BasicComboSettings settings)
        {
#if UNITY_EDITOR
            if (EditorBehaviorComboOverride != null) settings = EditorBehaviorComboOverride(owner, source, settings);
#endif
            return settings;
        }

        private void BeginBasicBehavior(CharacterAbilities abilities, ActionDefinitionSO action,
            CompiledAbilityExecutor executor, IAbilitySource source, Guid id, int stage, bool companion, bool required)
        {
            var connection = new BasicBehaviorConnection(id, stage, required, chain, source, owner.LifeGeneration);
            behaviorConnection = connection;
            ReportBehavior(connection, "等待真实释放", null, null);
            abilities.AttachBasicBehavior(action, source, executor, id, stage, companion, ImpactFor(id),
                () => IsBehaviorConnectionValid(connection),
                (success, reason, target) => ResolveBehavior(connection, success, reason, target),
                (phase, target, detail) => ReportBehavior(connection, phase, target, detail));
        }

        private bool IsBehaviorConnectionValid(BasicBehaviorConnection connection) =>
            behaviorConnection == connection && connection.source.IsAvailable && owner.LifeGeneration == connection.sourceLife &&
            (!connection.Required ? runtime.ActionId == connection.Id :
             chain == connection.chain && chain.IsConnectionOpen(connection.Id, runtime.SimulationTime));

        private void ResolveBehavior(BasicBehaviorConnection connection, bool success, string reason, CombatVitals target)
        {
            if (behaviorConnection != connection || connection.Resolved) return;
            if (connection.Required && !connection.chain.ReportBehavior(connection.Id, success, runtime.SimulationTime)) return;
            connection.Resolved = true;
            if (success && target != null)
            {
                connection.target = target;
                connection.targetRevision = target.LifeRevision;
                connection.hasTarget = true;
            }
            ReportBehavior(connection, success ? "已成功" : "失败", target, reason);
        }

        private void ReportBehavior(BasicBehaviorConnection connection, string phase, CombatVitals target, string detail)
        {
            if (behaviorConnection != connection) return;
            var label = target == null ? "" : " · " + target.name;
            var message = string.IsNullOrEmpty(detail) ? "" : " · " + detail;
            BehaviorStatus = phase + label + message + " · " + connection.Id.ToString("N").Substring(0, 8);
        }

        private string DescribeBehaviorWait()
        {
            var connection = behaviorConnection;
            if (connection == null || !connection.Required || chain == null) return "无受条件约束的接段";
            if (chain.BehaviorResult == ChainBehaviorResult.Failed) return "本轮接段已关闭";
            if (chain.AwaitingMarker || !runtime.AttackReady) return "等待动画开放";
            if (chain.BehaviorResult == ChainBehaviorResult.Pending) return "等待行为结果";
            return chain.HasBuffer ? "等待动作准入" : "等待新的普攻输入";
        }

        internal void ObserveBehaviorRelease(Guid id) => RecordBasicRelease(id);

        private void CheckBehaviorConnection()
        {
            var connection = behaviorConnection;
            if (connection == null || !connection.Required || chain.BehaviorResult == ChainBehaviorResult.Failed) return;
            if (!IsBehaviorConnectionValid(connection))
                InvalidateBehaviorConnection(connection.source.IsAvailable ? "接段窗口已过期" : "本次来源已失效");
            else if (connection.hasTarget && (connection.target == null || !connection.target.IsAvailable ||
                connection.target.LifeRevision != connection.targetRevision ||
                connection.target.gameObject.scene != owner.gameObject.scene))
                InvalidateBehaviorConnection("目标已失效");
        }

        private void InvalidateBehaviorConnection(string reason)
        {
            if (behaviorConnection == null) return;
            chain?.InvalidateBehavior(behaviorConnection.Id);
            // 卡肉缓存也归当前连接，失败不会触发下一轮的第一击。
            DiscardPendingComboInput();
            behaviorConnection.Resolved = true;
            ReportBehavior(behaviorConnection, "失败", null, reason);
        }
    }
}
