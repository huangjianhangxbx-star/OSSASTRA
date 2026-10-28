/* 脚本：SessionTests
 * 功能：实际InventorySession源码的恢复/播种、多箱、事务门和运行字段夹具；不模拟真实Unity输入或渲染。
 */
using System;
using System.Linq;
using System.Reflection;
using Ossastra.Inventory;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using UnityEngine;
static class SessionTests
{
    static int passed;
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);passed++;}
    static void Field(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
    static void Start(InventorySession session)=>typeof(InventorySession).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,null);
    static (InventorySession session,CharacterControlCoordinator coordinator,ItemDefinitionSO food) Setup(bool start=true)
    {
        var s=new InventorySession();var c=new CharacterControlCoordinator();
        foreach(var id in new[]{"hero","partner"}){var root=new CharacterRoot{ActorId=id};root.components[typeof(CharacterEquipment)]=new CharacterEquipment{State=new EquipmentState(id,100)};c.Characters.Add(root);}c.Current=c.Characters[0];
        var food=new ItemDefinitionSO{definitionId="food",displayName="食物",maxStack=20};Field(s,"coordinator",c);Field(s,"catalog",new[]{food});Field(s,"chest",new Transform());Field(s,"initialTeam",new[]{new InventoryGrant{definition=food,quantity=3}});
        if(start)Start(s);return(s,c,food);
    }
    sealed class Startup:IInventoryStartupAdapter
    {
        public bool reject;public bool ReadyBeforeCallback,ReadyInCallback;public int Calls;public Action<InventorySession> restore;
        public bool RestoreOrSeed(InventorySession s,out bool seed,out string error){Calls++;ReadyBeforeCallback=s.Ready;seed=false;error=reject?"bad saved domain":null;if(!reject){s.Ledger.Grant("team","food",7);restore?.Invoke(s);}return !reject;}
        public void OnInventoryReady(InventorySession s){ReadyInCallback=s.Ready;}
    }
    static void Main()
    {
        var failed=Setup(false);var reject=new Startup{reject=true};failed.session.ConfigureStartupAdapter(reject);Start(failed.session);
        Check(!failed.session.Ready&&failed.session.Ledger.Items.Length==0&&failed.session.LastResult.Contains("bad saved domain"),"failed restore never enables Ready or seeds missing defaults");
        bool rejectedIncompleteCapture=false;try{failed.session.CaptureRuntimeState();}catch(InvalidOperationException){rejectedIncompleteCapture=true;}
        Check(rejectedIncompleteCapture,"failed startup cannot expose a partial runtime snapshot to shutdown persistence");
        var restored=Setup(false);var adapter=new Startup();restored.session.ConfigureStartupAdapter(adapter);Start(restored.session);
        Check(adapter.Calls==1&&!adapter.ReadyBeforeCallback&&adapter.ReadyInCallback&&restored.session.Ledger.Items.Single().Quantity==7,"restore runs before seeding and ready hook after binding");
        var legacy=Setup();Check(legacy.session.Ready&&legacy.session.Ledger.Items.Single().Quantity==3,"old scene without adapter retains original initial grants");
        legacy.session.enabled=false;var disabledSnapshot=legacy.session.CaptureRuntimeState();
        Check(!legacy.session.Ready&&disabledSnapshot.worldContainers.Single(w=>w.id=="external").mapId=="fixture-map"&&!legacy.session.Move(legacy.session.Ledger.Plan(legacy.session.Ledger.Items.Single().Id,"safe")).Ok,"disabled initialized session permits final read capture while inventory edits remain rejected");
        legacy.session.enabled=true;
        var s=legacy.session;
        Check(s.RegisterWorldContainer("box-a","fixture-map",new Vector3(1,0,0),2,2,"A箱",new[]{new InventoryGrant{definition=legacy.food,quantity=2}}).Ok,"first stable world box is registered with initial contents");
        Check(s.RegisterWorldContainer("box-a","fixture-map",new Vector3(1,0,0),2,2,"A箱",new[]{new InventoryGrant{definition=legacy.food,quantity=2}}).Ok&&s.Ledger.Items.Where(i=>i.Location.ContainerId=="box-a").Sum(i=>i.Quantity)==2,"same box registration cannot grant initial contents twice");
        Check(!s.RegisterWorldContainer("box-a","other-map",new Vector3(1,0,0),2,2,"A箱",null).Ok,"duplicate world identity with conflicting metadata is rejected");
        Check(s.RegisterWorldContainer("box-b","fixture-map",new Vector3(2,0,0),1,1,"B箱",null).Ok&&s.SetActiveExternal("box-b"),"second box can become explicit transfer context");
        var original=s.Ledger.Items.Single(i=>i.Location.ContainerId=="team");
        Check(s.QuickTransfer(original.Id).Ok&&s.Ledger.Find(original.Id).Location.ContainerId=="box-b","quick transfer follows active box instead of dictionary first item");
        Check(s.ContainerDisplayName("box-a")=="A箱"&&s.ContainerPosition("box-b").Value.x==2,"world box metadata is queried by stable identity");
        var atomic=Setup();var countBefore=atomic.session.Ledger.Items.Sum(i=>i.Quantity);var rejected=atomic.session.RegisterWorldContainer("too-small","fixture-map",new Vector3(1,0,0),1,1,"小箱",new[]{new InventoryGrant{definition=atomic.food,quantity=21}});
        Check(!rejected.Ok&&atomic.session.Ledger.Container("too-small")==null&&atomic.session.Ledger.Items.Sum(i=>i.Quantity)==countBefore,"failed full box seed creates neither partial contents nor logical container");
        var bulk=Setup();bulk.session.Ledger.Grant("team","food",177);bulk.session.RegisterWorldContainer("bulk","fixture-map",new Vector3(1,0,0),2,2,"批量箱",new[]{new InventoryGrant{definition=bulk.food,quantity=40}});bulk.session.SetActiveExternal("bulk");
        var partial=bulk.session.TakeAll();var repeated=bulk.session.TakeAll();
        Check(partial.Transferred==1&&partial.Remaining==1&&repeated.Transferred==0&&repeated.Remaining==1&&bulk.session.Ledger.Items.Sum(i=>i.Quantity)==220,"partial take all reports remaining and repeated attempt never duplicates prior successful items");
        s.SetActiveExternal("box-a");var taken=s.TakeAll();Check(taken.Transferred==1&&taken.Remaining==0&&s.Ledger.Items.Single(i=>i.Location.ContainerId=="team").Quantity==2,"take all moves real box contents and reports item counts");
        int changed=0;s.RuntimeChanged+=()=>changed++;var current=s.Ledger.Items.Single(i=>i.Location.ContainerId=="team");s.BindHotbar(2,current.Id);
        Check(changed>0,"hotbar changes signal runtime persistence independently of quantity mutation");
        s.ConfigureEditGate(()=>false);var planned=s.Ledger.Plan(current.Id,"safe");
        Check(!s.Move(planned).Ok&&!s.Sort("team").Ok&&!s.Drop(current.Id,1,new Vector3(),s.Ledger.AccessGeneration,s.Ledger.Container("team").Revision).Ok,"save failure edit gate rejects move sort and drop");
        var execution=Guid.NewGuid();Check(s.Ledger.Reserve(current.Id,1,execution).Ok&&s.Ledger.CommitConsumption(execution).Ok,"save edit gate never blocks already accepted domain consumption");
        s.ConfigureEditGate(null);var capture=s.CaptureRuntimeState();var clone=Setup();
        foreach(var box in new[]{("box-a",2,2),("box-b",1,1)})clone.session.Ledger.AddContainer(new InventoryContainer(box.Item1,box.Item2,box.Item3));
        // Ledger由领域恢复器先安装；本夹具只对runtime字段独立恢复，不冒称完整经济恢复。
        var clean=clone.session.CaptureRuntimeState();clean.hotbar[2]="food";clean.worldContainers=capture.worldContainers.Select(w=>new InventoryWorldContainerState{id=w.id,mapId=w.mapId,x=w.x,y=w.y,z=w.z,width=w.width,height=w.height,displayName=w.displayName,isGround=w.isGround,isEmpty=!clone.session.Ledger.Items.Any(i=>i.Location.ContainerId==w.id)}).ToArray();
        Check(clone.session.RestoreRuntimeState(clean,out var error)&&clone.session.Hotbar[2]=="food"&&clone.session.ContainerDisplayName("box-b")=="B箱","detached runtime snapshot installs matching complete world and hotbar state");
        var before=clone.session.CaptureRuntimeState();var invalid=clone.session.CaptureRuntimeState();invalid.hotbar[2]="missing-definition";
        Check(!clone.session.RestoreRuntimeState(invalid,out error)&&clone.session.Hotbar[2]==before.hotbar[2],"invalid runtime snapshot rejects all changes before installation");
        var preparedInput=clone.session.CaptureRuntimeState();var prepared=clone.session.PrepareRuntimeState(preparedInput,clone.session.Ledger.Capture(),out error);preparedInput.hotbar[2]="missing-definition";int notifications=0;clone.session.RuntimeChanged+=()=>notifications++;
        clone.session.PublishRuntimeState(prepared);Check(clone.session.Hotbar[2]=="food"&&notifications==0,"prepared runtime publish uses frozen references and sends no intermediate notification");
        var staged=Setup(false);var stagedSnapshot=Setup().session.CaptureRuntimeState();stagedSnapshot.worldContainers.Single(w=>w.id=="external").mapId="original-map";
        var stagedAdapter=new Startup{restore=target=>{var stage=target.PrepareRuntimeState(stagedSnapshot,target.Ledger.Capture(),out var e);if(stage==null)throw new Exception(e);target.PublishRuntimeState(stage);target.SetActiveMap("new-map");}};staged.session.ConfigureStartupAdapter(stagedAdapter);Start(staged.session);
        Check(staged.session.CaptureRuntimeState().worldContainers.Single(w=>w.id=="external").mapId=="original-map"&&!staged.session.WorldContainerIds.Contains("external"),"map rebind after restore never relocates saved legacy external box");
        var missingBase=clone.session.Ledger.Capture();missingBase.containers=missingBase.containers.Where(c=>c.id!="team").ToArray();
        Check(clone.session.ValidateRuntimeState(clone.session.CaptureRuntimeState(),missingBase)!=null,"runtime validation rejects missing required player container before live restore");
        var invalidHand=clone.session.Ledger.Capture();invalidHand.containers.Single(c=>c.id==InventorySession.HandId("hero",InventoryHand.Right)).owner="other-owner";
        Check(clone.session.ValidateRuntimeState(clone.session.CaptureRuntimeState(),invalidHand)!=null,"runtime validation rejects foreign owner in restored hand container");
        var emptyStrings=clone.session.CaptureRuntimeState();emptyStrings.activeExternalId="";emptyStrings.priorityDefinitionId="";emptyStrings.hotbar[0]="";var emptyOwners=clone.session.Ledger.Capture();foreach(var c in emptyOwners.containers.Where(c=>c.owner==null))c.owner="";
        var normalized=clone.session.PrepareRuntimeState(emptyStrings,emptyOwners,out error);
        Check(normalized!=null,"optional null strings and empty-string codec representation restore equivalently");
        var notify=Setup();int laterObserver=0;notify.session.Changed+=()=>throw new InvalidOperationException("injected view failure");notify.session.Changed+=()=>laterObserver++;
        Check(notify.session.RegisterWorldContainer("notify-box","fixture-map",new Vector3(1,0,0),1,1,"箱",null).Ok&&laterObserver>0,"completed world registration survives one observer failure and reaches remaining observers");
        s.SetActiveMap("another-map");Check(!s.CanAccessContainer("box-a")&&s.ContainerPosition("box-a")==null,"unloaded map retains box record but removes current access and proxy position");
        s.SetActiveMap("fixture-map");Check(s.ContainerDisplayName("box-a")=="A箱"&&s.Ledger.Container("box-a")!=null,"returning to map reuses original box state without regeneration");
        s.ConfigureEditGate(()=>false);legacy.coordinator.Characters[0].Die();legacy.coordinator.Characters[1].Die();
        Check(s.GroundContainers.Length==1&&s.Ledger.Items.Any(i=>i.Location.ContainerId.StartsWith("drop:wipe:")),"save edit gate never blocks original team defeat drop");
        Console.WriteLine("TOTAL "+passed);
    }
}
