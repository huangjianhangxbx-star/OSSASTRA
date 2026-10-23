/* 脚本：InventoryItemUse
 * 功能：把真实库存条目适配为A的能力来源；预留数量、释放消费和目标资格共用原能力流水线。
 * 边界：不生成炸弹、不直接写HP，不以装备耐久支付消耗品。共享Y仍由原服务管理。
 * 协作：同一实例关联独立能力payload；UI/有限伙伴消费者从同一入口提交冻结目标。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
namespace Ossastra.Inventory
{
    using CharacterRoot=Ossastra.Characters.CharacterRoot;
    using ItemDefinitionSO=Ossastra.Characters.ItemDefinitionSO;
    [DisallowMultipleComponent]
    public sealed class InventoryItemUse:MonoBehaviour,IInventoryItemUse
    {
        [SerializeField] private InventorySession session;
        private readonly Dictionary<string,EquipmentAbilityState> resources=new Dictionary<string,EquipmentAbilityState>();
        public string LastResult {get;private set;}="未使用物品";
        public event Action<CharacterRoot,Guid,ItemDefinitionSO> Released;
        private void NotifyReleased(CharacterRoot actor,Guid execution,ItemDefinitionSO definition)
        {
            if(Released==null)return;
            foreach(Action<CharacterRoot,Guid,ItemDefinitionSO> listener in Released.GetInvocationList())
                try{listener(actor,execution,definition);}catch(Exception error){Debug.LogError("物品表现观察者失败 #"+execution+" / "+definition.name+": "+error,this);}
        }
        public bool TryUse(string itemId)
        {
            var actor=session?.Coordinator?.Current;
            return TryUseFor(itemId,actor,actor?.Vitals,null,false);
        }
        public bool TryUseFor(string itemId,CharacterRoot actor,CombatVitals target,Vector3? aim,bool companion)
        {
            if(session==null||!session.Ready||actor==null||!actor.CanSimulateIntent)return Reject("物品/角色未就绪");
            var input=actor.GetComponent<CharacterInputHandler>();
            if(companion?input.HasPlayerControl:session.Coordinator.Current!=actor||!actor.HasPlayerInput)return Reject("使用控制身份已变化");
            var item=session.Ledger.Find(itemId);var definition=session.Definition(item?.DefinitionId);
            if(item==null||definition==null||definition.equipment!=null||definition.ability==null||definition.useKind==InventoryUseKind.None||
                !(companion?item.Location.ContainerId=="reserve":new[]{"team","safe","reserve"}.Contains(item.Location.ContainerId)))return Reject("此物品没有已接入的使用入口");
            if(definition.useKind==InventoryUseKind.Regeneration&&(target==null||!target.IsAlive||target.GetComponent<CombatRegeneration>()?.CanBegin(definition.regenerationPerSecond,definition.regenerationSeconds)!=true))return Reject("需生命未满且没有正在生效的持续恢复");
            if(!string.IsNullOrEmpty(definition.temporarySocketId))
            {
                try{var data=actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>()?.Skeleton?.Data;if(actor.Definition?.rigProfile==null)return Reject(definition.name+".temporarySocketId requires character RigProfile");actor.Definition.rigProfile.Resolve(definition.temporarySocketId,data);}
                catch(ArgumentException error){return Reject(error.Message);}
            }
            if(!resources.TryGetValue(item.Id,out var runtime)){runtime=new EquipmentAbilityState();resources.Add(item.Id,runtime);}
            var source=new Source(session,item,definition,actor,target,aim,companion,runtime,(id,d)=>NotifyReleased(actor,id,d));
            var abilities=actor.GetComponent<CharacterAbilities>();if(abilities==null)return Reject("角色能力端口缺失");
            var ok=abilities.TryRequestFromSource(definition.ability,source,AbilityOperation.Secondary,companion?CharacterActionSource.Companion:CharacterActionSource.Inventory,definition.icon,.25f);
            LastResult=ok?"已接受 "+definition.displayName:abilities.LastResult;return ok;
        }
        private bool Reject(string reason){LastResult="拒绝："+reason;return false;}
        private sealed class Source:IAbilitySource,IAbilityTargetSource
        {
            private readonly InventorySession session;private readonly CharacterRoot actor;private readonly string container;
            private readonly int generation;private readonly long access,revision;private readonly bool companion;
            private readonly ItemDefinitionSO definition;private readonly float rate,seconds;
            private Guid reservation;private bool committed;
            private readonly Action<Guid,ItemDefinitionSO> released;
            public string OwnerId=>actor.ActorId;
            public string InstanceId {get;}
            public EquipmentAbilityState RuntimeState {get;}
            public CombatVitals Target {get;}
            public Vector3? AimPoint {get;}
            public Source(InventorySession session,InventoryItem item,ItemDefinitionSO definition,CharacterRoot actor,CombatVitals target,Vector3? aim,bool companion,EquipmentAbilityState runtime,Action<Guid,ItemDefinitionSO> released)
            {this.session=session;this.actor=actor;this.definition=definition;this.released=released;rate=definition.regenerationPerSecond;seconds=definition.regenerationSeconds;Target=target;AimPoint=aim;this.companion=companion;InstanceId=item.Id;container=item.Location.ContainerId;generation=actor.LifeGeneration;access=session.Ledger.AccessGeneration;revision=session.Ledger.Container(container).Revision;RuntimeState=runtime;}
            public bool IsAvailable
            {
                get
                {
                    if(session==null||!session.Ready||actor==null||!actor.IsAlive||actor.LifeGeneration!=generation||!actor.isActiveAndEnabled)return false;
                    if(committed)return true;
                    var item=session.Ledger.Find(InstanceId);
                    if(item==null||item.Quantity<1||item.Location.ContainerId!=container||session.Ledger.AccessGeneration!=access||session.Ledger.Container(container).Revision!=revision)return false;
                    if(companion)
                    {if(actor.GetComponent<CharacterInputHandler>().HasPlayerControl||actor.ActorId!=session.ReserveOwnerId)return false;}
                    else if(session.Coordinator.Current!=actor||!actor.HasPlayerInput||!session.CanAccessContainer(container))return false;
                    if(definition.useKind==InventoryUseKind.Regeneration)
                    {if(Target==null||!Target.IsAlive||Target.GetComponent<CombatRegeneration>()?.CanBegin(rate,seconds)!=true||Mathf.Abs(Target.transform.position.y-actor.transform.position.y)>.5f||Vector3.Distance(Target.transform.position,actor.transform.position)>3||!CombatDeliveryQueue.Clear(actor.transform.position,Target.transform.position))return false;}
                    return true;
                }
            }
            public bool TryReserve(Guid execution)
            {if(reservation!=Guid.Empty||!IsAvailable)return false;var q=session.Ledger.Reserve(InstanceId,1,execution);if(!q.Ok)return false;reservation=execution;return true;}
            public bool CommitRelease(Guid execution)
            {if(execution==Guid.Empty||committed||reservation==Guid.Empty||!IsAvailable)return false;var q=session.Ledger.CommitConsumption(reservation);if(!q.Ok)return false;reservation=Guid.Empty;committed=true;released?.Invoke(execution,definition);return true;}
            public void CancelReservation(){if(reservation!=Guid.Empty&&session!=null&&session.Ready)session.Ledger.CancelReservation(reservation);reservation=Guid.Empty;}
        }
    }
}

