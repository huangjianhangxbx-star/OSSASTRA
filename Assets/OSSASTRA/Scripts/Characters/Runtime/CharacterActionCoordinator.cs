/* 脚本：CharacterActionCoordinator
 * 功能：角色内统一动作资格与一次接受/费用事务，主副手按各自通道查询。
 * 边界：不持有第二套动作/生命/CD；不推进时间，不驱动骨骼或写HP。
 * 协作：Root创建；切换技需在队伍同步交接中持有单次请求；能力先查自身几何/配置，再提交请求；失败不调用取消/接受端口。
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
        private Guid switchLinkRequest;

        internal bool CanPrepareSwitchAttack(CharacterActionRequest request, float cost, out string reason)
            => CanAccept(request, cost, request.Source == CharacterActionSource.SwitchLink &&
                request.Kind == CharacterActionKind.Main && request.RequestId != Guid.Empty, out reason);

        internal bool TryCommitSwitchAttack(CharacterActionRequest request, float cost, Func<Guid> accept, out string reason)
        {
            if (switchLinkRequest != Guid.Empty || request.Source != CharacterActionSource.SwitchLink ||
                request.Kind != CharacterActionKind.Main || request.RequestId == Guid.Empty)
            {
                reason = "invalid-switch-link-request";
                return false;
            }
            // 只授权这一个准备请求。设备采样和所有普通动作仍使用原控制代际。
            switchLinkRequest = request.RequestId;
            try { return TryCommit(request, cost, accept, out reason); }
            finally { switchLinkRequest = Guid.Empty; }
        }
        public string LastResult {get;private set;}="未请求";
        public CharacterActionCoordinator(CharacterRoot owner,CharacterStamina stamina,CharacterDodge dodge,CharacterOffhand offhand)
        {this.owner=owner;this.stamina=stamina;this.dodge=dodge;this.offhand=offhand;}
        public CharacterActionRequest CreateRequest(CharacterActionKind kind,CharacterActionSource source,string sourceLabel=null)
            =>new CharacterActionRequest(owner.ActorId,owner.LifeGeneration,owner.ActionControlRevision,kind,source,Guid.NewGuid(),sourceLabel);
        public bool CanAccept(CharacterActionRequest request,float cost,out string reason)
            => CanAccept(request, cost, switchLinkRequest != Guid.Empty && switchLinkRequest == request.RequestId, out reason);

        private bool CanAccept(CharacterActionRequest request, float cost, bool switchLinkAuthorized, out string reason)
        {
            reason=null;
            if(!Enum.IsDefined(typeof(CharacterActionKind),request.Kind)||!Enum.IsDefined(typeof(CharacterActionSource),request.Source))reason="invalid-kind/source";
            else if(request.ActorId!=owner.ActorId||request.LifeGeneration!=owner.LifeGeneration)reason="stale-life/actor";
            else if(request.ControlRevision!=owner.ActionControlRevision)reason="stale-control";
            else if(!owner.CanSimulateIntent)reason="life/pause/focus/posture";
            else if(request.Kind==CharacterActionKind.Main&&owner.GetComponent<CharacterEquipment>() is CharacterEquipment gear&&gear.InventoryBound&&gear.RightDefinition==null)reason="main-equipment-missing";
            else if(request.Source == CharacterActionSource.SwitchLink ? !switchLinkAuthorized :
                request.Source == CharacterActionSource.Companion ? owner.HasPlayerInput : !owner.HasPlayerInput)reason="source-authority";
            else if(request.Source==CharacterActionSource.Companion&&owner.StateInput.BlocksSwitchLinkCompanion(owner))
                reason="switch-link-companion-suspended";
            else if(request.Kind!=CharacterActionKind.Main&&request.Kind!=CharacterActionKind.Support&&request.Source==CharacterActionSource.Companion)reason="companion-capability";
            else if(request.Source!=CharacterActionSource.Companion&&request.Source!=CharacterActionSource.Inventory&&owner.GetComponent<CharacterInputHandler>()?.InventoryCaptured==true)reason="inventory-input-capture";
            else if(owner.Abilities!=null&&!owner.Abilities.CanAccept(request.Kind,out var abilityReason))reason=abilityReason;
            else if(owner.StateArbitrator!=null&&!owner.StateArbitrator.AllowsRequest(request.Kind,request.IsAttack,out var stateReason))reason=stateReason;
            else if(request.Kind==CharacterActionKind.Offhand&&offhand!=null&&(offhand.IsAccepting||offhand.InFlight||offhand.CooldownRemaining>0))reason="offhand-not-ready";
            else if (owner.UsesActionPosture && request.PostureTarget.HasValue &&
                (float.IsNaN(request.PostureTarget.Value) || float.IsInfinity(request.PostureTarget.Value) || request.PostureTarget.Value < 0)) reason = "invalid-posture-exposure";
            else if(float.IsNaN(cost)||float.IsInfinity(cost)||cost<0)reason="invalid-cost";
            else if(!owner.UsesActionPosture&&stamina!=null&&(request.Kind!=CharacterActionKind.Jump||cost>0)&&!stamina.CanSpend(cost))reason="stamina";
            return reason==null;
        }
        public bool TryCommit(CharacterActionRequest request,float cost,Func<Guid> accept,out string reason)
        {
            CanAccept(request,cost,out reason);
            var ok=ledger.TryCommit(request.RequestId,reason,()=>
            {
                var actionId=accept();if(actionId==Guid.Empty)return false;
                if (owner.UsesActionPosture)
                {
                    owner.RegisterActionExposure(request, actionId, request.PostureTarget);
                    return true;
                }
                // 所有资格在同一同步事务内检查；无外部回调可插入另一笔费用。
                if(stamina!=null&&(request.Kind!=CharacterActionKind.Jump||cost>0)&&!stamina.Spend(actionId,cost,owner.SimulationTime,request.SourceLabel??request.Source+":"+request.Kind))
                    throw new InvalidOperationException("Accepted action payment conflict");
                return true;
            });
            if(!ok&&reason==null)reason="duplicate-or-port-rejected";
            LastResult=ok?"Accepted "+request.Kind+" #"+request.RequestId.ToString("N"):"Rejected "+reason;
            if (!ok) Ossastra.Combat.CombatTraceProbe.For(owner)?.Note("状态","请求被拒",request.Kind+" · "+reason+" · 请求 "+request.RequestId.ToString("N"));
            return ok;
        }
    }
}
