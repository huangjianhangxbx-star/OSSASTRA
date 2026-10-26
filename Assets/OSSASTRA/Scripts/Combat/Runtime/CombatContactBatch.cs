/* 脚本：CombatContactBatch
 * 功能：冻结网关已结算批次的身份、节点、位置与逐目标实际回执。
 * 边界：不重新结算HP，不合并整个父动作，不把空挥或Duplicate转换为命中。
 * 协作：网关完成原回执分发后发布；非角色目标可没有CombatVitals，仍保留TargetId。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Combat
{
    /// <summary>接触的生成原因；同一父动作可以同时拥有直击、周期与派生效果。</summary>
    public enum CombatContactCause { Direct, Periodic, Secondary }

    public sealed class CombatContactReceipt
    {
        public HitOutcome Outcome { get; }
        public CombatVitals Target { get; }
        public string TargetId { get; }
        public Vector3 Position { get; }

        public CombatContactReceipt(HitOutcome outcome, CombatVitals target, Vector3 position)
        {
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            Target = target;
            TargetId = outcome.TargetId;
            Position = position;
        }
    }

    public sealed class CombatContactBatch
    {
        public Guid ParentExecutionId { get; }
        public Guid ActionId { get; }
        public int EmissionIndex { get; }
        public string ActorId { get; }
        public CombatVitals Source { get; }
        public Vector3 Origin { get; }
        public CombatContactCause Cause { get; }
        /// <summary>可选领域效果事实，如ice.pulse；空白不能从Periodic或Secondary自行猜测。</summary>
        public string EffectSemantic { get; }
        public IReadOnlyList<CombatContactReceipt> Receipts { get; }

        public CombatContactBatch(Guid parentExecutionId, Guid actionId, int emissionIndex, string actorId,
            CombatVitals source, Vector3 origin, IEnumerable<CombatContactReceipt> receipts, CombatContactCause cause=CombatContactCause.Direct,string effectSemantic=null)
        {
            ParentExecutionId = parentExecutionId;
            ActionId = actionId;
            EmissionIndex = emissionIndex;
            ActorId = actorId;
            Source = source;
            Origin = origin;
            Cause = cause;
            EffectSemantic=effectSemantic??"";
            // 复制再包装，不能把调用方的可变数组作为只读接口直接暴露。
            Receipts = Array.AsReadOnly((receipts ?? throw new ArgumentNullException(nameof(receipts))).ToArray());
        }
    }
}
