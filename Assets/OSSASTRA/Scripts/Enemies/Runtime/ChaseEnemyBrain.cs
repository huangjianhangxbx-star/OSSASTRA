/*
 * 脚本：ChaseEnemyBrain
 * 功能：显式中立候选的有限感知、追击、停距、返回和受阻重评。
 * 边界：无玩家Root/Input/Current；不直接移动，不推进攻击，不回血/传送。
 * 协作：EnemyRoot唯一调用Tick，EnemyMovement提供上帧真实位移反馈。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using Ossastra.EnemyCore;
using UnityEngine;
namespace Ossastra.Enemies
{
    public sealed class ChaseEnemyBrain : MonoBehaviour,IEnemyBrain
    {
        [SerializeField] private CombatVitals[] targets=Array.Empty<CombatVitals>();
        [Header("有限直线追击 SAMPLE；无NavMesh")]
        [SerializeField,Min(.1f)] private float detectRadius=4,leashRadius=6,stopDistance=1.2f,attackRange=1.7f,attackInterval=1.8f;
        private CombatVitals owner;
        private IEnemyMovement movement;
        private EnemyRoot root;
        private ICombatTarget selected;
        private Vector3 home;
        private float nextAttack,blockedFor,holdUntil;
        private bool returning;
        public bool IsAvailable=>isActiveAndEnabled && owner!=null && owner.IsAvailable;
        public Vector3 MoveDirection { get; private set; }
        public ChasePhase Phase { get; private set; }
        public Vector3 Home=>home;
        public string TargetId=>CombatTargetValidity.IsAvailable(selected)?selected.Health.Id:"none";
        public bool Initialize(CombatVitals value)
        {
            if(value==null||value.gameObject!=gameObject||value.Health==null)return false;
            owner=value;root=GetComponent<EnemyRoot>();movement=GetComponent<EnemyMovement>();home=transform.position;
            ResetDecision(Time.time);return root!=null&&movement!=null;
        }
        private bool Valid(ICombatTarget candidate)
        {
            if(!IsAvailable||!CombatTargetValidity.IsAvailable(candidate)||candidate.TargetTransform==null)return false;
            var found=false;
            foreach(var t in targets)
            {if(t==null)continue;if(ReferenceEquals(t,candidate))found=true;else if(t.Health!=null&&t.Health.Id==candidate.Health.Id)return false;}
            return found && CombatTargetPolicy.CanHit(owner.Health.Id,owner.Faction,true,candidate.Health.Id,candidate.Faction,true)
                && FlatDistance(home,candidate.WorldPosition)<=leashRadius && CombatSight.IsClear(transform,candidate.TargetTransform);
        }
        public void Tick(float now,float delta)
        {
            if(!IsAvailable){ResetDecision(now);return;}
            // 有移动意图却没有实际进展才累计受阻；攻击停手不被误判卡墙。
            if(movement.HasMovementRequest && movement.Velocity.sqrMagnitude<.01f)blockedFor+=delta;else blockedFor=0;
            if(blockedFor>.6f){holdUntil=now+1;blockedFor=0;returning=true;selected=null;}
            if(returning && FlatDistance(home,transform.position)<.15f)returning=false;
            if(selected!=null&&!Valid(selected)){selected=null;returning=true;}
            if(!returning && selected==null)
            {
                var nearest=detectRadius;
                foreach(var t in targets)
                {if(!Valid(t))continue;var distance=FlatDistance(transform.position,t.WorldPosition);if(distance<nearest){nearest=distance;selected=t;}}
            }
            Phase=ChasePolicy.Decide(true,!returning&&selected!=null,selected!=null,
                selected==null?0:FlatDistance(transform.position,selected.WorldPosition),FlatDistance(home,transform.position),stopDistance,leashRadius,.15f,now<holdUntil);
            if(Phase==ChasePhase.ReturnHome){returning=true;selected=null;MoveDirection=(home-transform.position).normalized;}
            else if(Phase==ChasePhase.Approach)MoveDirection=(selected.WorldPosition-transform.position).normalized;
            else MoveDirection=Vector3.zero;
        }
        public bool IsEligible(ICombatTarget candidate)=>ReferenceEquals(candidate,selected)&&Valid(candidate)&&FlatDistance(transform.position,candidate.WorldPosition)<=attackRange;
        public bool TrySelect(float now,out ICombatTarget target)
        {target=null;if(now<nextAttack||Phase!=ChasePhase.InRangeAttack||!IsEligible(selected))return false;target=selected;return true;}
        public void OnAccepted(float now){nextAttack=now+attackInterval;}
        public void ResetDecision(float now)
        {selected=null;MoveDirection=Vector3.zero;Phase=ChasePhase.Idle;blockedFor=0;holdUntil=0;nextAttack=now+attackInterval;returning=FlatDistance(home,transform.position)>.15f;}
        private void OnDisable(){if(root!=null)root.Cancel("chase-brain-disabled");MoveDirection=Vector3.zero;selected=null;movement?.Stop();}
        private static float FlatDistance(Vector3 a,Vector3 b){var d=a-b;d.y=0;return d.magnitude;}
    }
}
