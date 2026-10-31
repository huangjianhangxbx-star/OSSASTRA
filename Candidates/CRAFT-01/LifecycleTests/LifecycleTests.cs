// 真CommerceService/Session/Ledger/Economy/保存协调器；内存store只替代磁盘，UI/Unity外壳不代签Native销毁次序。
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ossastra.Inventory;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using Ossastra.Commerce;
using Ossastra.Commerce.Runtime;
using Ossastra.Commerce.Persistence;
using UnityEngine;

sealed class MemoryStore:ICommerceSaveStore
{
    public CommerceSaveSnapshot Last;public bool Fail;public int Writes;
    public TaskCompletionSource<bool> Gate,Entered;
    public async Task WriteAsync(CommerceSaveSnapshot snapshot,CancellationToken token)
    {if(Gate!=null){Entered.TrySetResult(true);await Gate.Task;}if(Fail)throw new System.IO.IOException("injected lifecycle write failure");Last=snapshot;Writes++;}
}
sealed class ProofParticipant:ICommerceSnapshotParticipant
{
    public string SnapshotKey=>"lifecycle-proof";public event Action Changed {add{}remove{}}
    public CommerceSnapshotSection Capture()=>new CommerceSnapshotSection{key=SnapshotKey,schema=1,payload="{\"receiptCount\":3,\"stableItem\":\"proof-equipment\"}"};
    public bool TryPrepareRestore(CommerceSnapshotSection section,CommerceRuntimeSnapshot aggregate,out object prepared,out string error){prepared=new object();error=null;return true;}
    public void PublishRestore(object prepared){}
}
static class LifecycleTests
{
    sealed class Fixture
    {public InventorySession session;public CommerceService service;public CommerceSaveCoordinator saves;public MemoryStore store;}
    static readonly List<object> facts=new List<object>();static int passed,failed;
    static void Need(bool value,string why){if(!value)throw new Exception(why);}
    static void Set(object value,string name,object field)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(value,field);
    static object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    static object OptionalField(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(value);
    static void Call(object value,string name)
    {try{value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,null);}catch(TargetInvocationException ex){throw ex.InnerException;}}
    static async Task<Fixture> Setup(bool withGear=false)
    {
        Debug.Errors.Clear();var s=new InventorySession();var c=new CharacterControlCoordinator();
        foreach(var id in new[]{"hero","partner"}){var a=new Ossastra.Characters.CharacterRoot{ActorId=id};a.components[typeof(CharacterEquipment)]=new CharacterEquipment{State=new EquipmentState(id,100)};c.Characters.Add(a);}c.Current=c.Characters[0];
        var catalog=new List<ItemDefinitionSO>{new ItemDefinitionSO{definitionId="stone",maxStack=99},new ItemDefinitionSO{definitionId="trade-coin",maxStack=99}};
        if(withGear)catalog.Add(new ItemDefinitionSO{definitionId="lifecycle-gear",equipment=new EquipmentDefinitionSO{definitionId="lifecycle-gear",mainHand=true}});
        Set(s,"coordinator",c);Set(s,"catalog",catalog.ToArray());Set(s,"chest",new Transform());Call(s,"Start");Need(s.Ready,s.LastResult);Need(s.Ledger.Grant("team","stone",3).Ok,"initial fixture grant");
        if(withGear)Need(s.Ledger.Grant("team","lifecycle-gear",1,new EquipmentItemState("proof-equipment","lifecycle-gear",OffhandKind.None,100)).Ok,"equipment fixture");
        var service=new CommerceService{inventory=s,profileId="lifecycle-isolated",mapId="fixture-map"};
        var economy=new CommerceEconomy(s.Ledger,service);Set(service,"<Economy>k__BackingField",economy);
        var store=new MemoryStore();var saves=new CommerceSaveCoordinator(store,"lifecycle-isolated",Guid.NewGuid().ToString("N"));Set(service,"saves",saves);
        service.RegisterSnapshotParticipant(new ProofParticipant());service.OnInventoryReady(s);Need(await saves.FlushAsync(),"initial save");
        return new Fixture{session=s,service=service,saves=saves,store=store};
    }
    static object Observe(Fixture f)=>new{hostRevision=f.service.Revision,economyRevision=f.service.Economy.StateRevision,economyTime=f.service.Economy.SimulationTime,
        ledgerRevision=f.session.Ledger.MutationRevision,persistedRevision=f.saves.Status.PersistedRevision,stateRevision=f.saves.Status.StateRevision,
        saveSequence=f.saves.Status.SaveSequence,saved=f.saves.Status.IsSaved,closed=f.saves.Status.IsClosed,lifecyclePayloadPresent=Field(f.service,"lifecyclePayload")!=null,
        currentStone=f.session.Ledger.Items.Where(i=>i.DefinitionId=="stone").Sum(i=>i.Quantity),persistedStone=CommerceSnapshotCodec.Decode(f.store.Last.Payload).economy.inventory.items.Where(i=>i.definitionId=="stone").Sum(i=>i.quantity),
        diagnostic=f.service.Diagnostic,errors=Debug.Errors.ToArray()};
    static void InventoryFirstExit(Fixture f)
    {
        // 显式null只模拟Unity destroyed-object-null的该条件分支；真实Native对象销毁顺序另由Root复验。
        f.service.inventory=null;Call(f.service,"OnDisable");Call(f.service,"OnDestroy");
    }
    static async Task Test(string name,Func<Task> body)
    {try{await body();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
    static async Task Main()
    {
        await Test("completed explicit flush with unchanged authority tolerates inventory-first exit",async()=>{
            var f=await Setup();Need(await f.service.FlushBeforeSceneChangeAsync(),"explicit flush failed");var expected=CommerceSnapshotCodec.Encode(f.service.Capture());var before=Observe(f);
            Need(f.store.Last.Payload.SequenceEqual(expected),"actual latest aggregate was not persisted");InventoryFirstExit(f);var after=Observe(f);
            facts.Add(new{test="no-change-after-proven-flush",before,after,persistedAggregateStillExact=f.store.Last.Payload.SequenceEqual(expected)});
            Need(!f.service.Diagnostic.Contains("退出保存未完成")&&Debug.Errors.Count==0,"false exit failure although exact current aggregate was already durably flushed");
        });
        await Test("unpersisted later ledger mutation must remain an exit failure",async()=>{
            var f=await Setup();Need(await f.service.FlushBeforeSceneChangeAsync(),"flush");long persisted=f.saves.Status.PersistedRevision;
            Need(f.session.Ledger.Grant("team","stone",1).Ok,"new authoritative mutation");Need(f.service.Revision>persisted&&!f.saves.Status.IsSaved,"new revision should be dirty");var before=Observe(f);InventoryFirstExit(f);facts.Add(new{test="true-later-dirty-state-lost",before,after=Observe(f)});
            Need(f.service.Diagnostic.Contains("退出保存未完成")&&!f.saves.Status.IsClosed&&CommerceSnapshotCodec.Decode(f.store.Last.Payload).economy.inventory.items.Single(i=>i.definitionId=="stone").quantity==3,"dirty newer state cannot be disguised as saved old checkpoint");
        });
        await Test("latest frozen request still writing can be drained after inventory release",async()=>{
            var f=await Setup();Need(await f.service.FlushBeforeSceneChangeAsync(),"flush");Need(f.session.Ledger.Grant("team","stone",1).Ok,"later mutation");
            f.store.Gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);f.store.Entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);f.service.RequestSave();await f.store.Entered.Task;var before=Observe(f);
            var release=Task.Run(async()=>{await Task.Delay(25);f.store.Gate.TrySetResult(true);});InventoryFirstExit(f);await release;facts.Add(new{test="pending-latest-frozen-snapshot-drained",before,after=Observe(f)});
            Need(f.saves.Status.IsClosed&&f.saves.Status.IsSaved&&CommerceSnapshotCodec.Decode(f.store.Last.Payload).economy.inventory.items.Single(i=>i.definitionId=="stone").quantity==4,"latest pending frozen bytes were not drained");
            Need(!f.service.Diagnostic.Contains("退出保存未完成"),"false capture error survived even after latest frozen request completed");
        });
        await Test("inventory alive finalizer captures later domain mutation before closing",async()=>{
            var f=await Setup();Need(await f.service.FlushBeforeSceneChangeAsync(),"flush");Need(f.session.Ledger.Grant("team","stone",1).Ok,"later mutation");var before=Observe(f);Call(f.service,"OnDisable");Call(f.service,"OnDestroy");facts.Add(new{test="inventory-alive-final-capture",before,after=Observe(f)});
            Need(!f.service.Diagnostic.Contains("退出保存未完成")&&f.saves.Status.IsClosed&&CommerceSnapshotCodec.Decode(f.store.Last.Payload).economy.inventory.items.Single(i=>i.definitionId=="stone").quantity==4,"healthy final capture must persist later state");
        });
        await Test("later ordinary saved snapshot refreshes lifecycle checkpoint witness",async()=>{
            var f=await Setup();Need(await f.service.FlushBeforeSceneChangeAsync(),"flush");Need(f.session.Ledger.Grant("team","stone",1).Ok,"later mutation");f.service.RequestSave();Need(await f.saves.FlushAsync(),"newer ordinary save");var expected=CommerceSnapshotCodec.Encode(f.service.Capture());var before=Observe(f);InventoryFirstExit(f);facts.Add(new{test="newer-ordinary-request-updates-checkpoint",before,after=Observe(f),persistedAggregateStillExact=f.store.Last.Payload.SequenceEqual(expected)});
            Need(f.store.Last.Payload.SequenceEqual(expected)&&!f.service.Diagnostic.Contains("退出保存未完成"),"latest ordinary RequestSave was durable but lifecycle witness was stale/missing");
        });
        await Test("real failed writer cannot be hidden by successful earlier flush",async()=>{
            var f=await Setup();Need(await f.service.FlushBeforeSceneChangeAsync(),"flush");f.store.Fail=true;Need(f.session.Ledger.Grant("team","stone",1).Ok,"mutation");f.service.RequestSave();Need(!await f.saves.FlushAsync(),"injected writer must fail");var before=Observe(f);InventoryFirstExit(f);facts.Add(new{test="real-write-failure",before,after=Observe(f)});
            Need(f.saves.Status.HasFailure&&!f.saves.Status.IsClosed&&f.service.Diagnostic.Contains("退出保存未完成"),"write failure must remain visible");
        });
        await Test("same host revision does not prove unchanged economy snapshot",async()=>{
            var f=await Setup();Need(await f.service.FlushBeforeSceneChangeAsync(),"flush");long host=f.service.Revision;var prior=f.service.Economy.StateRevision;var saved=f.store.Last.Payload;
            f.service.Economy.Advance(.25);Need(f.service.Revision==host&&f.service.Economy.StateRevision>prior&&f.saves.Status.IsSaved,"actual economy time should advance independently of host dirty event");var current=CommerceSnapshotCodec.Encode(f.service.Capture());Need(!current.SequenceEqual(saved),"same host revision contains changed true aggregate");var before=Observe(f);InventoryFirstExit(f);facts.Add(new{test="same-host-revision-later-economy-content",before,after=Observe(f),payloadChangedBeforeDependencyRelease=true});
            // 此例仅证明局限：完整检查点一致恢复不等于未通知的最后瞬时时钟零丢失。
            Need(f.service.Revision==host&&!current.SequenceEqual(saved),"host revision alone cannot prove byte-identical final aggregate");
        });
        await Test("live dependency same revision equipment change must queue newer bytes",async()=>{
            var f=await Setup(true);Need(await f.service.FlushBeforeSceneChangeAsync(),"flush");long host=f.service.Revision,sequence=f.saves.Status.SaveSequence;var gear=f.session.Ledger.Find("proof-equipment").Equipment;
            Need(gear.Wear(Guid.NewGuid())&&gear.Runtime.Magazine.Consume(Guid.NewGuid()),"real equipment mutation");Need(f.service.Revision==host,"equipment payload mutation unexpectedly emitted inventory event");var before=Observe(f);Call(f.service,"OnDisable");Call(f.service,"OnDestroy");var restored=CommerceSnapshotCodec.Decode(f.store.Last.Payload).economy.inventory.items.Single(i=>i.id=="proof-equipment").equipment;facts.Add(new{test="live-same-revision-new-equipment-content",before,after=Observe(f),savedDurability=restored.durability,savedAmmo=restored.runtime.magazine.rounds.Length});
            Need(!f.service.Diagnostic.Contains("退出保存未完成")&&f.saves.Status.SaveSequence>sequence&&restored.durability==99&&restored.runtime.magazine.rounds.Length==3,"same host revision must not bypass live final Capture/byte comparison");
        });
        await Test("synchronously rejected queue cannot publish a false lifecycle witness",async()=>{
            var f=await Setup();Need(await f.saves.CloseAsync(),"close coordinator fixture");var payload=Field(f.service,"lifecyclePayload");var cachedRevision=OptionalField(f.service,"lifecycleRevision");var cachedSequence=OptionalField(f.service,"lifecycleSaveSequence");var lastSimulation=Field(f.service,"lastSavedSimulation");var sequence=f.saves.Status.SaveSequence;
            f.service.Economy.Advance(.25);bool refused=false;try{f.service.RequestSave();}catch(InvalidOperationException){refused=true;}
            facts.Add(new{test="queue-rejection-preserves-witness",refused,cacheReferencePreserved=ReferenceEquals(payload,Field(f.service,"lifecyclePayload")),cachedRevisionBefore=cachedRevision,cachedRevisionAfter=OptionalField(f.service,"lifecycleRevision"),cachedSequenceBefore=cachedSequence,cachedSequenceAfter=OptionalField(f.service,"lifecycleSaveSequence"),saveSequenceBefore=sequence,saveSequenceAfter=f.saves.Status.SaveSequence});
            Need(refused&&ReferenceEquals(payload,Field(f.service,"lifecyclePayload"))&&Equals(cachedRevision,OptionalField(f.service,"lifecycleRevision"))&&Equals(cachedSequence,OptionalField(f.service,"lifecycleSaveSequence"))&&Equals(lastSimulation,Field(f.service,"lastSavedSimulation"))&&f.saves.Status.SaveSequence==sequence,"rejected RequestSave must not claim new accepted bytes or sequence");
        });
        Console.WriteLine("FACTS "+JsonConvert.SerializeObject(facts,Formatting.Indented));Console.WriteLine("RESULT passed="+passed+" failed="+failed);Environment.ExitCode=failed==0?0:1;
    }
}
