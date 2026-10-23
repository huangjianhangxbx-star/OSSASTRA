/* 脚本：InventorySession
 * 功能：同场唯一库存会话，创建有限容器、适配装备手槽与原子命令、检查当前访问资格。
 * 边界：不持有第二HP/Current/动作钟；初始化只在该会话发生一次，返场与HUD不再授予物品。
 * 协作：Ledger唯一位置/数量；CharacterEquipment只读手槽；全灭仅在本候选装配启用。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using Ossastra.Combat;
namespace Ossastra.Inventory
{
    using CharacterRoot=Ossastra.Characters.CharacterRoot;
    using ItemDefinitionSO=Ossastra.Characters.ItemDefinitionSO;
    [Serializable] public sealed class InventoryGrant {public ItemDefinitionSO definition;[Min(1)] public int quantity=1;}
    [DefaultExecutionOrder(-175)]
    public sealed class InventorySession:MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        [SerializeField] private ItemDefinitionSO[] catalog=Array.Empty<ItemDefinitionSO>();
        [SerializeField] private InventoryGrant[] initialTeam=Array.Empty<InventoryGrant>(),initialChest=Array.Empty<InventoryGrant>();
        [SerializeField] private Transform chest;
        [SerializeField,Min(.1f)] private float accessDistance=3;
        private readonly Dictionary<string,ItemDefinitionSO> definitions=new Dictionary<string,ItemDefinitionSO>(StringComparer.Ordinal);
        private readonly Dictionary<string,Vector3> ground=new Dictionary<string,Vector3>();
        private readonly string[] hotbar=new string[10];
        private int controlRevision;
        private bool previousExternalAccessible,wiped;
        public InventoryLedger Ledger {get;private set;}
        public CharacterControlCoordinator Coordinator=>coordinator;
        public string LastResult {get;private set;}="等待库存初始化";
        public string PriorityDefinitionId {get;private set;}
        public int? SelectedHotbar {get;private set;}
        public string[] Hotbar=>(string[])hotbar.Clone();
        public event Action Changed;
        public bool Ready=>Ledger!=null&&isActiveAndEnabled;
        public string[] GroundContainers=>ground.Keys.ToArray();
        public string[] AccessibleExternal=>Ready?new[]{"external"}.Concat(ground.Keys).Where(CanAccessContainer).ToArray():Array.Empty<string>();
        public string ReserveOwnerId=>coordinator?.Characters.ElementAtOrDefault(1)?.ActorId;
        public static string HandId(string actor,InventoryHand hand)=>"hand:"+actor+":"+hand;
        public ItemDefinitionSO Definition(string id)=>id!=null&&definitions.TryGetValue(id,out var d)?d:null;
        private sealed class Slots:ICharacterEquipmentInventory
        {
            private readonly InventorySession session;private readonly CharacterRoot root;
            public Slots(InventorySession session,CharacterRoot root){this.session=session;this.root=root;}
            public EquipmentItemState Right=>session.Ledger.EquipmentAt(HandId(root.ActorId,InventoryHand.Right));
            public EquipmentItemState Left=>session.Ledger.EquipmentAt(HandId(root.ActorId,InventoryHand.Left));
            public EquipmentDefinitionSO RightDefinition=>session.Definition(Right?.DefinitionId)?.equipment;
            public EquipmentDefinitionSO LeftDefinition=>session.Definition(Left?.DefinitionId)?.equipment;
            public EquipmentDefinitionSO[] AvailableOffhands=>session.Ledger.Items.Where(i=>i.Location.ContainerId=="team"||i.Location.ContainerId==HandId(root.ActorId,InventoryHand.Left))
                .Select(i=>session.Definition(i.DefinitionId)?.equipment).Where(d=>d!=null&&!d.mainHand).Distinct().ToArray();
            public bool TryEquip(EquipmentDefinitionSO definition)
            {
                var item=session.Ledger.Items.FirstOrDefault(i=>i.DefinitionId==definition?.definitionId&&(i.Location.ContainerId=="team"||i.Location.ContainerId==HandId(root.ActorId,InventoryHand.Left)));
                return item!=null&&session.Move(session.Ledger.Plan(item.Id,HandId(root.ActorId,InventoryHand.Left))).Ok;
            }
        }
        private void Start()
        {
            try
            {
                if(coordinator==null||coordinator.Characters.Count!=2||coordinator.Characters.Any(r=>r==null||r.GetComponent<CharacterEquipment>()?.State==null)||chest==null)throw new ArgumentException("InventorySession coordinator/chest/initialized members missing");
                Ledger=new InventoryLedger();
                foreach(var d in catalog)
                {if(d==null||definitions.ContainsKey(d.definitionId))throw new ArgumentException("InventorySession catalog duplicate/missing definition");definitions.Add(d.definitionId,d);Ledger.Register(d.FreezeInventory());}
                Ledger.AddContainer(new InventoryContainer("team",5,2));Ledger.AddContainer(new InventoryContainer("safe",2,1,true));
                Ledger.AddContainer(new InventoryContainer("reserve",5,1,true));Ledger.AddContainer(new InventoryContainer("external",5,8));
                foreach(var root in coordinator.Characters)
                {
                    var gear=root.GetComponent<CharacterEquipment>();
                    foreach(var hand in new[]{InventoryHand.Left,InventoryHand.Right})Ledger.AddContainer(new InventoryContainer(HandId(root.ActorId,hand),1,1,true,root.ActorId,hand));
                    SeedEquipped(root,gear,InventoryHand.Right,gear.RightDefinition,gear.State.RightInstanceId,100,gear.State.RightRuntime);
                    SeedEquipped(root,gear,InventoryHand.Left,gear.LeftDefinition,gear.State.LeftInstanceId,gear.State.Durability,gear.State.LeftRuntime);
                }
                Grant(initialTeam,"team");Grant(initialChest,"external");
                foreach(var root in coordinator.Characters)root.GetComponent<CharacterEquipment>().BindInventory(new Slots(this,root));
                foreach(var root in coordinator.Characters)root.Died+=OnDied;
                Ledger.Changed+=OnChanged;controlRevision=coordinator.ControlRevision;previousExternalAccessible=CanAccessContainer("external");
                LastResult="物品会话已就绪";OnChanged();
            }
            catch(Exception error){LastResult="库存初始化拒绝："+error.Message;Debug.LogError(LastResult,this);enabled=false;}
        }
        private void Grant(InventoryGrant[] grants,string container)
        {
            foreach(var grant in grants)
            {
                if(grant?.definition==null)throw new ArgumentException("Missing initial item definition");
                var d=grant.definition;var payload=d.equipment==null?null:new EquipmentItemState(Guid.NewGuid().ToString("N"),d.definitionId,d.equipment.offhandKind,100);
                var result=Ledger.Grant(container,d.definitionId,grant.quantity,payload);if(!result.Ok)throw new ArgumentException(container+" / "+d.definitionId+": "+result.Reason);
            }
        }
        private void SeedEquipped(CharacterRoot root,CharacterEquipment gear,InventoryHand hand,EquipmentDefinitionSO definition,string id,int durability,Ossastra.CharacterCore.Abilities.EquipmentAbilityState runtime)
        {
            if(definition==null)return;
            if(Definition(definition.definitionId)?.equipment!=definition)throw new ArgumentException("Equipped definition not in inventory catalog: "+definition.definitionId);
            var payload=new EquipmentItemState(id,definition.definitionId,definition.offhandKind,Math.Max(1,durability),runtime);
            var result=Ledger.Grant(HandId(root.ActorId,hand),definition.definitionId,1,payload);if(!result.Ok)throw new ArgumentException(result.Reason);
        }
        private void OnChanged()=>Changed?.Invoke();
        private void Update()
        {
            if(!Ready)return;
            var accessible=CanAccessContainer("external");
            if(controlRevision!=coordinator.ControlRevision||accessible!=previousExternalAccessible)
            {controlRevision=coordinator.ControlRevision;previousExternalAccessible=accessible;Ledger.InvalidateAccess();}
            if(wiped&&coordinator.Characters.Any(r=>r.IsAlive))wiped=false;
        }
        private void OnDied(CharacterRoot dead)
        {
            if(!Ready)return;Ledger.InvalidateAccess();
            if(wiped||coordinator.Characters.Any(r=>r.IsAlive))return;
            wiped=true;var id="drop:wipe:"+Guid.NewGuid().ToString("N");var sources=new[]{"team","reserve"}.Concat(coordinator.Characters.SelectMany(r=>new[]{HandId(r.ActorId,InventoryHand.Left),HandId(r.ActorId,InventoryHand.Right)})).ToArray();
            var q=Ledger.DropAll(Guid.NewGuid(),sources,id);if(q.Ok)ground.Add(id,dead.transform.position);LastResult=q.Ok?"全队覆灭：普通物品留在掉落包，安全栏保留":q.Reason;OnChanged();
        }
        public bool CanAccessContainer(string id)
        {
            var actor=coordinator?.Current;if(actor==null||!actor.IsAlive||actor.IsSimulationSuspended)return false;
            if(id=="team"||id=="safe")return true;
            if(id=="reserve")return coordinator.Characters.ElementAtOrDefault(1)?.IsAlive==true;
            if(id!=null&&id.StartsWith("hand:",StringComparison.Ordinal))
            {var owner=coordinator.Characters.FirstOrDefault(r=>id==HandId(r.ActorId,InventoryHand.Left)||id==HandId(r.ActorId,InventoryHand.Right));return owner!=null&&owner.IsAlive;}
            var position=id=="external"&&chest!=null?(Vector3?)chest.position:ground.TryGetValue(id??"",out var p)?p:(Vector3?)null;
            return position.HasValue&&InReach(actor,position.Value);
        }
        public bool InReach(CharacterRoot actor,Vector3 position)
        {
            if(actor==null)return false;var delta=position-actor.transform.position;
            return Mathf.Abs(delta.y)<=.5f&&new Vector2(delta.x,delta.z).magnitude<=accessDistance&&CombatDeliveryQueue.Clear(actor.transform.position,position);
        }
        private string Access(InventoryMove move)
        {
            if(!Ready||coordinator.Current==null||!coordinator.Current.CanSimulateIntent)return "当前本体不可整理库存";
            if(!CanAccessContainer(move.From)||!CanAccessContainer(move.To))return "容器已超距/遮挡或本体失效";
            var engaged=FindObjectOfType<CombatEngagementSession>()?.Engaged??false;
            foreach(var root in coordinator.Characters)
                if((move.From==HandId(root.ActorId,InventoryHand.Left)||move.From==HandId(root.ActorId,InventoryHand.Right)||move.To==HandId(root.ActorId,InventoryHand.Left)||move.To==HandId(root.ActorId,InventoryHand.Right))&&(engaged||!root.GetComponent<CharacterEquipment>().CanChangeEquipment))return "请脱战并结束装备所属角色动作后换装";
            var item=Ledger.Find(move.ItemId);var definition=Definition(item?.DefinitionId);
            if(move.To=="reserve"&&(definition==null||definition.equipment!=null))return "后备只接收普通道具，武器请放团队或安全包";
            if(definition?.equipment!=null)
            {
                foreach(var root in coordinator.Characters.Where(r=>move.To==HandId(r.ActorId,InventoryHand.Left)||move.To==HandId(r.ActorId,InventoryHand.Right)))
                {
                    try{var data=root.GetComponentInChildren<Spine.Unity.SkeletonAnimation>()?.Skeleton?.Data;definition.equipment.spinePresentation?.Freeze(data,root.Definition?.rigProfile);}
                    catch(ArgumentException error){return error.Message;}
                }
            }
            return null;
        }
        public InventoryResult Move(InventoryMove command)
        {var q=Ready?Ledger.Transfer(command,Access):InventoryResult.Reject("库存未就绪");LastResult=q.Ok?"已转移":q.Reason;OnChanged();return q;}
        public InventoryResult QuickTransfer(string itemId)
        {
            var item=Ledger?.Find(itemId);if(item==null)return InventoryResult.Reject("物品已失效");
            var to=item.Location.ContainerId=="team"?AccessibleExternal.FirstOrDefault()??"reserve":"team";
            return Move(Ledger.Plan(itemId,to));
        }
        public InventoryResult SetPriority(string itemId)
        {
            if(!Ready||!CanAccessContainer("reserve"))return InventoryResult.Reject("后备不可访问");
            if(itemId==null){PriorityDefinitionId=null;LastResult="已清空优先意图，物品仍在后备";OnChanged();return InventoryResult.Success();}
            var item=Ledger.Find(itemId);if(item==null||item.Location.ContainerId!="reserve")return InventoryResult.Reject("先交付道具，再设优先");
            PriorityDefinitionId=item.DefinitionId;LastResult="优先考虑 "+Definition(item.DefinitionId).displayName;OnChanged();return InventoryResult.Success(item.Id);
        }
        public InventoryResult BindHotbar(int slot,string itemId)
        {
            if(slot<0||slot>=10||!Ready)return InventoryResult.Reject("无效快捷栏位置");
            var item=Ledger.Find(itemId);if(itemId!=null&&(item==null||item.Location.ContainerId!="team"||Definition(item.DefinitionId)?.equipment!=null))return InventoryResult.Reject("只能引用团队普通物品");
            hotbar[slot]=item?.DefinitionId;if(itemId==null&&SelectedHotbar==slot)SelectedHotbar=null;OnChanged();return InventoryResult.Success();
        }
        public bool SelectHotbar(int slot)
        {
            if(slot<0||slot>=10||!Ready||!Ledger.Items.Any(i=>i.Location.ContainerId=="team"&&i.DefinitionId==hotbar[slot])){LastResult="快捷栏为空或库存已耗尽";return false;}
            SelectedHotbar=slot;LastResult=Definition(hotbar[slot]).displayName+" · 仅选择/引用，不直接消费";OnChanged();return true;
        }
        public bool TryGroundPoint(Vector2 pointer,out Vector3 point)
        {
            point=default;var camera=Camera.main;var actor=coordinator?.Current;if(camera==null||actor==null)return false;
            foreach(var hit in Physics.RaycastAll(camera.ScreenPointToRay(pointer),1000,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                if(hit.normal.y>.7f&&hit.collider.GetComponentInParent<CombatVitals>()==null&&Mathf.Abs(hit.point.y-actor.transform.position.y)<.5f&&CombatDeliveryQueue.Clear(actor.transform.position,hit.point)){point=hit.point;return true;}
            return false;
        }
        public InventoryResult Drop(string itemId,int quantity,Vector3 point,long expectedGeneration,long fromRevision)
        {
            if(!Ready||!CanAccessContainer("team")||expectedGeneration!=Ledger.AccessGeneration||!Finite(point.x)||!Finite(point.y)||!Finite(point.z))return InventoryResult.Reject("丢弃请求已失效");
            var actor=coordinator.Current;var item=Ledger.Find(itemId);if(item==null||Ledger.Container(item.Location.ContainerId).Revision!=fromRevision||!new[]{"team","safe","reserve"}.Contains(item.Location.ContainerId))return InventoryResult.Reject("物品或来源已变化");
            var floor=Physics.RaycastAll(point+Vector3.up*.25f,Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<CombatVitals>()==null);
            if(!floor||Mathf.Abs(actor.transform.position.y-point.y)>.5f||!CombatDeliveryQueue.Clear(actor.transform.position,point))return InventoryResult.Reject("鼠标处不是同层可见的合法地面");
            var id="drop:manual:"+Guid.NewGuid().ToString("N");var d=Ledger.Definition(item.DefinitionId);Ledger.AddContainer(new InventoryContainer(id,d.Width,d.Height));ground.Add(id,point);
            // 丢弃点可超出拾取半径；源和本体仍在提交时校验，落地后再按3米访问。
            var q=Ledger.Transfer(Ledger.Plan(item.Id,id,quantity),command=>CanAccessContainer(command.From)&&actor==coordinator.Current&&actor.CanSimulateIntent?null:"丢弃来源或本体已失效");
            if(!q.Ok){ground.Remove(id);Ledger.RemoveEmptyContainer(id);}LastResult=q.Ok?"物品已落在鼠标位置":q.Reason;OnChanged();return q;
        }
        public Vector3? ContainerPosition(string id)=>id=="external"?chest?.position:ground.TryGetValue(id??"",out var point)?point:(Vector3?)null;
        public void ReclaimEmptyGround()
        {if(!Ready)return;foreach(var id in ground.Keys.ToArray())if(!Ledger.Items.Any(i=>i.Location.ContainerId==id)){ground.Remove(id);Ledger.RemoveEmptyContainer(id);}OnChanged();}
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private void OnDestroy()
        {if(Ledger!=null)Ledger.Changed-=OnChanged;if(coordinator!=null)foreach(var root in coordinator.Characters)if(root!=null)root.Died-=OnDied;}
    }
}

