/* 脚本：CombatRecovery
 * 功能：将实际HP损失、夺回和父执行寿命接到唯一RecoverableHealthState。
 * 边界：仅显式装配的单位启用；不重建Health，不独立Update，不把零伤格挡记作虚血。
 * 协作：Vitals分发损失；Combat网关分发实际伤害；Root/执行与离手投递持有父租约，最后释放时清预算。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatRecovery:MonoBehaviour
    {
        private CombatVitals vitals;
        private RecoverableHealthState state;
        private readonly Dictionary<Guid,int> parents=new Dictionary<Guid,int>();
        public double Gray=>state?.Gray??0;
        public double HoldRemaining=>state?.HoldRemaining??0;
        public int ActiveParents=>parents.Count;
        public void Initialize(CombatVitals owner)
        {if(state!=null)return;vitals=owner;if(owner?.Health!=null)state=new RecoverableHealthState(owner.Health);}
        public void RecordLoss(HitOutcome hit)
        {if(isActiveAndEnabled&&vitals!=null&&vitals.Faction==CombatFaction.Allied&&hit.Reason==HitReason.Applied)state?.RecordLoss(hit.HpBefore-hit.HpAfter);}
        public void Tick(double delta){if(isActiveAndEnabled)state?.Tick(delta);}
        public void OnApplied(AttackRelease release,HitOutcome outcome)
        {
            if(!isActiveAndEnabled||vitals==null||!vitals.IsAlive||vitals.Faction!=CombatFaction.Allied||outcome.Reason!=HitReason.Applied)return;
            state?.Reclaim(release.ParentExecutionId,outcome.HpBefore-outcome.HpAfter,release.ReclaimBudgetFraction,release.ReclaimRate);
        }
        public void RetainParent(Guid parent)
        {if(parent==Guid.Empty)return;parents.TryGetValue(parent,out var count);parents[parent]=count+1;}
        public void ReleaseParent(Guid parent)
        {
            if(!parents.TryGetValue(parent,out var count))return;
            if(count>1){parents[parent]=count-1;return;}parents.Remove(parent);state?.CompleteParent(parent);
        }
        public void ResetState(){state?.Reset();parents.Clear();}
    }
}
