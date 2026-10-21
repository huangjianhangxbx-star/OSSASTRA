/* 本体可选伙伴战斗能力：Root每帧显式Sample，输出有限移动和一次Stage1候选。
 * 不读InputAction、不Advance动作、不扣HP、不移动Transform；切控代际屏障撤销旧候选。
 * 冷却仅在Accepted后开始；受阻退回Follow。只使用明确场景成员，拒绝重复身份。
 * 协作契约集中声明于CharacterContracts，本文件仅实现候选生成与受理反馈。
 */
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CompanionCombatBrain : MonoBehaviour, ICompanionMoveCandidate, ICompanionActionCandidate
    {
        [SerializeField] private CombatEngagementSession engagement;
        [SerializeField, Min(.1f)] private float anchorRadius = 3, attackInterval = 1.3f;
        private CharacterRoot owner;
        private CombatMeleeHit melee;
        private CombatVitals target;
        private double nextAttack;
        private int sampledFrame = -1, revision = -1;
        private float blockedTime, retryAt;
        private Vector3 previousPosition;
        private Vector2 move;
        private bool requestedMove;
        public CompanionCombatDecision Decision { get; private set; }
        public string TargetId => target == null ? "无" : target.Health?.Id;
        public string Reason { get; private set; } = "未采样";
        public void Initialize(CharacterRoot root) { owner = root; melee = GetComponent<CombatMeleeHit>(); previousPosition=transform.position; }
        private bool Context => isActiveAndEnabled && owner != null && owner.CanSimulateIntent &&
            engagement != null && engagement.CanSample && engagement.Engaged &&
            engagement.Coordinator.Current != owner && engagement.Coordinator.Characters.Contains(owner);
        private bool Fresh => Context && sampledFrame == Time.frameCount && revision == engagement.Coordinator.ControlRevision;
        private bool Valid(CombatVitals candidate)
        {
            if(candidate == null || !candidate.IsAvailable || owner.Vitals == null || !owner.Vitals.IsAvailable) return false;
            var enemies = engagement.Enemies;
            return enemies.Count(e => e != null && e.Vitals != null && e.Vitals.Health != null && e.Vitals.Health.Id == candidate.Health.Id) == 1 &&
                enemies.Any(e => e != null && e.isActiveAndEnabled && e.Vitals == candidate) &&
                CombatTargetPolicy.CanHit(owner.ActorId,owner.Vitals.Faction,true,candidate.Health.Id,candidate.Faction,true) &&
                Mathf.Abs(candidate.transform.position.y-transform.position.y) < .5f &&
                CombatSight.IsClear(transform,candidate.transform,.8f);
        }
        public void Sample(double now, float delta)
        {
            sampledFrame=Time.frameCount; move=Vector2.zero;
            if(!Context || melee == null) { ResetCandidate(); return; }
            var session=engagement.Coordinator;
            if(revision != session.ControlRevision) { target=null; blockedTime=0; requestedMove=false; }
            revision=session.ControlRevision;
            var position=transform.position;
            if(requestedMove && owner.DataBus.MoveReady && Vector3.Distance(position,previousPosition)<.001f) blockedTime+=delta;
            else blockedTime=0;
            previousPosition=position; requestedMove=false;
            if(blockedTime>.6f) { retryAt=Time.time+1; blockedTime=0; target=null; }
            var anchor=session.Current.transform.position;
            bool Inside(CombatVitals c) => Vector3.Distance(c.transform.position,anchor)<=anchorRadius;
            if(!Valid(target) || !Inside(target)) target=null;
            if(target == null && Time.time>=retryAt)
                target=engagement.Enemies.Where(e=>e!=null).Select(e=>e.Vitals).Where(c=>Valid(c)&&Inside(c))
                    .OrderBy(c=>Vector3.SqrMagnitude(c.transform.position-position)).FirstOrDefault();
            var inRange=target!=null && Vector3.Distance(position,target.transform.position)<=melee.Reach+melee.Radius*.7f;
            Decision=CompanionCombatPolicy.Decide(true,false,true,true,target!=null,
                Vector3.Distance(position,anchor)<=anchorRadius,inRange,now>=nextAttack && owner.DataBus.AttackReady,Time.time<retryAt);
            Reason=Decision.ToString();
            if(Decision==CompanionCombatDecision.Approach)
            { var d=target.transform.position-position; move=new Vector2(d.x,d.z).normalized; requestedMove=true; }
        }
        public bool TryGetCombatMove(out Vector2 value)
        { value=move; return Fresh && (Decision==CompanionCombatDecision.Approach || Decision==CompanionCombatDecision.Wait || Decision==CompanionCombatDecision.Attack); }
        public bool TryGetAttackTarget(out CombatVitals value)
        { value=target; return Fresh && Decision==CompanionCombatDecision.Attack && Valid(target); }
        public void OnAccepted() { nextAttack=owner.SimulationTime+attackInterval; Decision=CompanionCombatDecision.Wait; }
        public bool IsAcceptedTargetValid(CombatVitals value) => engagement != null && Valid(value);
        public void ResetCandidate() { target=null; move=Vector2.zero; requestedMove=false; Decision=CompanionCombatDecision.Inactive; Reason="无有效自治资格"; }
        private void OnDisable() { ResetCandidate(); }
    }
}
