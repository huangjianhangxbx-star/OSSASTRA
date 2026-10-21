/* 脚本：CharacterActionCoordinator
 * 功能：角色内统一动作资格与一次接受/费用事务，主副手按各自通道查询。
 * 边界：不持有第二套动作/生命/CD；不推进时间，不驱动骨骼或写HP。
 * 协作：Root创建；能力先查自身几何/配置，再提交请求；失败不调用取消/接受端口。
 */
using System;
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    public sealed class CharacterActionCoordinator:ICharacterActionCoordinator
    {
        private readonly CharacterRoot owner;
        private readonly CharacterStamina stamina;
        private readonly CharacterDodge dodge;
        private readonly CharacterOffhand offhand;
        private readonly ActionAdmissionLedger ledger=new ActionAdmissionLedger();
        public string LastResult {get;private set;}="未请求";
        public CharacterActionCoordinator(CharacterRoot owner,CharacterStamina stamina,CharacterDodge dodge,CharacterOffhand offhand)
        {this.owner=owner;this.stamina=stamina;this.dodge=dodge;this.offhand=offhand;}
        public CharacterActionRequest CreateRequest(CharacterActionKind kind,CharacterActionSource source,string sourceLabel=null)
            =>new CharacterActionRequest(owner.ActorId,owner.LifeGeneration,owner.ActionControlRevision,kind,source,Guid.NewGuid(),sourceLabel);
        public bool CanAccept(CharacterActionRequest request,float cost,out string reason)
        {
            reason=null;
            if(!Enum.IsDefined(typeof(CharacterActionKind),request.Kind)||!Enum.IsDefined(typeof(CharacterActionSource),request.Source))reason="invalid-kind/source";
            else if(request.ActorId!=owner.ActorId||request.LifeGeneration!=owner.LifeGeneration)reason="stale-life/actor";
            else if(request.ControlRevision!=owner.ActionControlRevision)reason="stale-control";
            else if(!owner.CanSimulateIntent)reason="life/pause/focus/posture";
            else if(request.Source==CharacterActionSource.Companion?owner.HasPlayerInput:!owner.HasPlayerInput)reason="source-authority";
            else if(request.Kind!=CharacterActionKind.Main&&request.Source==CharacterActionSource.Companion)reason="companion-capability";
            else if(request.Kind==CharacterActionKind.Main&&(!owner.DataBus.AllowsAttack||!owner.DataBus.AttackReady||dodge!=null&&dodge.IsActive))reason="main-not-ready";
            else if(request.Kind==CharacterActionKind.Dodge&&(!owner.DataBus.AllowsMovement||!owner.DataBus.MoveReady||dodge!=null&&dodge.IsActive))reason="motion-not-ready";
            else if(request.Kind==CharacterActionKind.Guard&&dodge!=null&&dodge.IsActive)reason="dodge-active";
            else if(request.Kind==CharacterActionKind.Offhand&&offhand!=null&&(offhand.IsAccepting||offhand.InFlight||offhand.CooldownRemaining>0))reason="offhand-not-ready";
            else if(float.IsNaN(cost)||float.IsInfinity(cost)||cost<0)reason="invalid-cost";
            else if(stamina!=null&&!stamina.CanSpend(cost))reason="stamina";
            return reason==null;
        }
        public bool TryCommit(CharacterActionRequest request,float cost,Func<Guid> accept,out string reason)
        {
            CanAccept(request,cost,out reason);
            var ok=ledger.TryCommit(request.RequestId,reason,()=>
            {
                var actionId=accept();if(actionId==Guid.Empty)return false;
                // 所有资格在同一同步事务内检查；无外部回调可插入另一笔费用。
                if(stamina!=null&&!stamina.Spend(actionId,cost,owner.SimulationTime,request.SourceLabel??request.Source+":"+request.Kind))
                    throw new InvalidOperationException("Accepted action payment conflict");
                return true;
            });
            if(!ok&&reason==null)reason="duplicate-or-port-rejected";
            LastResult=ok?"Accepted "+request.Kind+" #"+request.RequestId.ToString("N"):"Rejected "+reason;
            return ok;
        }
    }
}
