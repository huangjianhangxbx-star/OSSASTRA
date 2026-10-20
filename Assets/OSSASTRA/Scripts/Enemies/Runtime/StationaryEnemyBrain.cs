/*
 * 脚本：StationaryEnemyBrain
 * 功能：固定站位有限感知/决策：显式候选、距离、LOS、最近选择及请求间隔。
 * 边界：无Update/动作时钟/HP写入，无玩家类型、寻路或追击；不执行攻击。
 * 协作：Inspector保存中立Vitals，逻辑消费ICombatTarget；EnemyRoot单调度。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Enemies
{
    public sealed class StationaryEnemyBrain : MonoBehaviour
    {
        [SerializeField] private CombatVitals[] targets=Array.Empty<CombatVitals>();
        [SerializeField, Min(.1f)] private float triggerDistance=1.7f, attackInterval=1.8f;
        private CombatVitals owner;
        private EnemyRoot root;
        private float nextAttack;
        public bool Initialize(CombatVitals value)
        {
            if(value==null || value.gameObject!=gameObject || value.Health==null) return false;
            if(owner!=null && owner!=value) return false;
            owner=value; root=GetComponent<EnemyRoot>(); ResetDecision(Time.time); return root!=null;
        }
        public void ResetDecision(float now) { nextAttack=now+attackInterval; }
        public void OnAccepted(float now) { nextAttack=now+attackInterval; }
        public bool IsEligible(ICombatTarget candidate)
        {
            if(!isActiveAndEnabled || owner==null || !owner.IsAvailable ||
                !CombatTargetValidity.IsAvailable(candidate) || candidate.TargetTransform==null) return false;
            // 必须仍在显式候选表；同ID不同实体拒绝，防止过期身份指向新对象。
            var found=false;
            foreach(var configured in targets)
            {
                if(configured==null) continue;
                if(ReferenceEquals(configured,candidate)) found=true;
                else if(configured.Health!=null && configured.Health.Id==candidate.Health.Id) return false;
            }
            if(!found || !CombatTargetPolicy.CanHit(owner.Health.Id,owner.Faction,owner.IsAlive,
                candidate.Health.Id,candidate.Faction,candidate.IsAlive)) return false;
            var offset=candidate.WorldPosition-transform.position; offset.y=0;
            if(offset.magnitude>triggerDistance) return false;
            var start=transform.position+Vector3.up*.8f;
            var end=candidate.WorldPosition+Vector3.up*.8f;
            foreach(var hit in Physics.RaycastAll(start,end-start,Vector3.Distance(start,end),~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(candidate.TargetTransform)) return false;
            return true;
        }
        public bool TrySelect(float now,out ICombatTarget selected)
        {
            selected=null;
            if(now<nextAttack || !isActiveAndEnabled) return false;
            var nearest=float.MaxValue;
            foreach(var candidate in targets)
            {
                if(!IsEligible(candidate)) continue;
                var distance=(candidate.WorldPosition-transform.position).sqrMagnitude;
                if(distance<nearest) { nearest=distance; selected=candidate; }
            }
            return selected!=null;
        }
        private void OnDisable() { if(root!=null) root.Cancel("brain-disabled"); }
        private void OnDrawGizmosSelected()
        { Gizmos.color=Color.red; Gizmos.DrawWireSphere(transform.position+Vector3.up*.8f,triggerDistance); }
    }
}