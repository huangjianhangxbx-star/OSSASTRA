// 真实Ledger转换合同；反射边界使尚未实现接口时得到明确行为RED而非编译错误。
using System;
using System.Linq;
using System.Reflection;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Inventory;

static class ConversionTests
{
    static int passed;
    static Type Consumption => typeof(InventoryLedger).Assembly.GetType("Ossastra.Inventory.InventoryConsumption");
    static void Need(bool condition,string message) { if(!condition) throw new Exception(message); }
    static object Prepare(InventoryLedger ledger, InventoryContainerGrant[] grants, int quantity=1, string materialId=null, long? revision=null, long? generation=null, string container="team")
    {
        Need(Consumption!=null,"conversion should prepare consumed-space candidate: InventoryConsumption API missing");
        var consume=Array.CreateInstance(Consumption,1);
        consume.SetValue(Activator.CreateInstance(Consumption, materialId??ledger.Items.First(i=>i.DefinitionId=="stone").Id,"stone",quantity),0);
        var args=new object[]{container,consume,grants,revision??ledger.MutationRevision,generation??ledger.AccessGeneration,null};
        var method=typeof(InventoryLedger).GetMethod("PrepareConversion"); Need(method!=null,"PrepareConversion API missing");
        return method.Invoke(ledger,args);
    }
    static InventoryResult Commit(InventoryLedger ledger,object candidate,Func<string> access=null,Action publish=null)
    {
        try { return (InventoryResult)typeof(InventoryLedger).GetMethod("CommitConversion").Invoke(ledger,new object[]{candidate,access??(()=>null),publish??(()=>{})}); }
        catch(TargetInvocationException ex) { throw ex.InnerException; }
    }
    static InventoryLedger Setup(int columns=1,int count=1)
    {
        var l=new InventoryLedger();l.Register(new InventoryDefinition("stone",1,1,99));l.Register(new InventoryDefinition("bar",1,1,99));
        l.Register(new InventoryDefinition("sword",1,1,1,InventoryHand.Right));l.AddContainer(new InventoryContainer("team",columns,1));
        Need(l.Grant("team","stone",count).Ok,"fixture stone");return l;
    }
    static EquipmentItemState Gear(string id="crafted")=>new EquipmentItemState(id,"sword",OffhandKind.None,100);
    static InventoryContainerGrant[] Sword(EquipmentItemState gear=null)=>new[]{new InventoryContainerGrant("sword",1,gear??Gear())};
    static void Run(string name,Action test){test();passed++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        Run("full bag consumes material before placing equipment",()=>{
            var l=Setup();var p=Prepare(l,Sword());Need(p!=null&&l.Items.Single().DefinitionId=="stone","prepare must be read only");
            bool receipt=false,observer=false;l.Changed+=()=>{Need(receipt&&l.Find("crafted")!=null,"notification sees complete receipt and output");observer=true;};
            Need(Commit(l,p,publish:()=>receipt=true).Ok,"conversion succeeds");Need(observer&&l.Items.Single().DefinitionId=="sword","one output replaces material");
        });
        Run("partial ordinary stack merges without needing spare cell",()=>{
            var l=Setup(2,3);l.Grant("team","bar",4);var id=l.Items.Single(i=>i.DefinitionId=="bar").Id;
            var p=Prepare(l,new[]{new InventoryContainerGrant("bar",2)},2);Need(p!=null,"merge plan");
            var outputs=(Array)p.GetType().GetProperty("Outputs").GetValue(p);var row=outputs.GetValue(0);
            Need((string)row.GetType().GetProperty("ItemId").GetValue(row)==id&&(int)row.GetType().GetProperty("QuantityAdded").GetValue(row)==2,"output addition fact");
            Need(Commit(l,p).Ok&&l.Find(id).Quantity==6&&l.Items.Single(i=>i.DefinitionId=="stone").Quantity==1,"exact consumption and merge");
        });
        Run("second equipment cannot fit rejects entire batch",()=>{
            var l=Setup();var p=Prepare(l,new[]{new InventoryContainerGrant("sword",1,Gear("a")),new InventoryContainerGrant("sword",1,Gear("b"))});
            Need(p==null&&l.Items.Single().DefinitionId=="stone","no partial grant or loss");
        });
        Run("reserved source stack rejected even unreserved remainder exists",()=>{
            var l=Setup(2,3);var id=l.Items[0].Id;l.Reserve(id,1,Guid.NewGuid());Need(Prepare(l,Sword(),1,id)==null&&l.Find(id).Quantity==3,"entire stack protected");
        });
        Run("reservation cancel and access changes stale prepared candidate",()=>{
            foreach(var mode in new[]{0,1,2}){var l=Setup();var p=Prepare(l,Sword());var token=Guid.NewGuid();if(mode==0)l.Reserve(l.Items[0].Id,1,token);else if(mode==1){l.Reserve(l.Items[0].Id,1,token);l.CancelReservation(token);}else l.InvalidateAccess();Need(!Commit(l,p).Ok&&l.Find("crafted")==null,"stale candidate refuses");}
        });
        Run("existing equipment wear and resource changes invalidate signature",()=>{
            foreach(bool wear in new[]{true,false}){var l=Setup(2);l.Grant("team","sword",1,Gear("live"));var p=Prepare(l,Sword());var live=l.Find("live").Equipment;if(wear)live.Wear(Guid.NewGuid());else live.Runtime.Magazine.Consume(Guid.NewGuid());Need(!Commit(l,p).Ok&&l.Find("crafted")==null,"live signature mismatch");}
        });
        Run("output cloned so caller edits do not change prepared or granted payload",()=>{
            var l=Setup();var external=Gear();var p=Prepare(l,Sword(external));external.Wear(Guid.NewGuid());external.Runtime.Magazine.Consume(Guid.NewGuid());
            Need(Commit(l,p).Ok,"commit detached equipment");var actual=l.Find("crafted").Equipment;Need(actual.Durability==100&&actual.Runtime.Magazine.Count==4&&!ReferenceEquals(actual,external)&&!ReferenceEquals(actual.Runtime,external.Runtime),"payload deeply detached and fresh defaults");
        });
        Run("live runtime and duplicate output runtime rejected",()=>{
            var l=Setup(3);var live=Gear("live");l.Grant("team","sword",1,live);
            Need(Prepare(l,Sword(new EquipmentItemState("new","sword",OffhandKind.None,100,live.Runtime)))==null,"cannot launder live runtime");
            var r=new EquipmentAbilityState();Need(Prepare(l,new[]{new InventoryContainerGrant("sword",1,new EquipmentItemState("a","sword",OffhandKind.None,100,r)),new InventoryContainerGrant("sword",1,new EquipmentItemState("b","sword",OffhandKind.None,100,r))})==null,"outputs cannot share runtime");
            Need(Prepare(l,new[]{new InventoryContainerGrant("sword",1,Gear("x")),new InventoryContainerGrant("sword",1,Gear("x"))})==null,"duplicate ids rejected");
        });
        Run("publish exception rolls back all ledger revisions without notification",()=>{
            var l=Setup();var p=Prepare(l,Sword());var rev=l.MutationRevision;var cr=l.Container("team").Revision;int notifications=0;l.Changed+=()=>notifications++;
            bool threw=false;try{Commit(l,p,publish:()=>throw new InvalidOperationException("publish probe"));}catch(InvalidOperationException){threw=true;}
            Need(threw&&l.MutationRevision==rev&&l.Container("team").Revision==cr&&l.Items.Single().DefinitionId=="stone"&&notifications==0,"rollback preserves live ledger");
        });
        Run("bad observer cannot turn committed result into failure",()=>{
            var l=Setup();var p=Prepare(l,Sword());int seen=0;l.Changed+=()=>throw new Exception("observer probe");l.Changed+=()=>seen++;
            Need(Commit(l,p).Ok&&seen==1&&l.NotificationErrorCount==1&&l.Find("crafted")!=null,"success survives observer error");Need(!Commit(l,p).Ok,"candidate cannot commit twice");
        });
        Run("final access rejects and reentrant mutation invalidates",()=>{
            var l=Setup();var p=Prepare(l,Sword());Need(!Commit(l,p,()=>"station gone").Ok&&l.Items[0].DefinitionId=="stone","access refusal");
            Need(!Commit(l,p,()=>{l.InvalidateAccess();return null;}).Ok&&l.Find("crafted")==null,"reentrant revision refuses");
        });
        Run("foreign owner expired expected version and invalid input refuse",()=>{
            var l=Setup();var p=Prepare(l,Sword());Need(!Commit(Setup(),p).Ok,"foreign owner");Need(Prepare(l,Sword(),revision:l.MutationRevision-1)==null,"expected old version");Need(Prepare(l,Sword(),quantity:0)==null,"zero consume");Need(Prepare(l,Sword(),quantity:2)==null,"insufficient material");
            Need(Prepare(l,new[]{new InventoryContainerGrant("sword",2,Gear())})==null,"equipment cannot stack");
        });
        Run("several material stacks aggregate exact costs and preserve remaining identities",()=>{
            var l=Setup(3,99);l.Grant("team","stone",8);var rows=l.Items.OrderBy(i=>i.Quantity).ToArray();
            var p=l.PrepareConversion("team",new[]{new InventoryConsumption(rows[0].Id,"stone",3),new InventoryConsumption(rows[1].Id,"stone",2)},new[]{new InventoryContainerGrant("bar",4)},l.MutationRevision,l.AccessGeneration,out var error);
            Need(p!=null,error);Need(Commit(l,p).Ok&&l.Find(rows[0].Id).Quantity==5&&l.Find(rows[1].Id).Quantity==97&&l.Items.Single(i=>i.DefinitionId=="bar").Quantity==4,"multiple stacks exact");
        });
        Run("wrong definition protected source and duplicate overflow cannot consume",()=>{
            var l=Setup();l.AddContainer(new InventoryContainer("safe",1,1));l.Grant("safe","stone",2);var team=l.Items.Single(i=>i.Location.ContainerId=="team");var safe=l.Items.Single(i=>i.Location.ContainerId=="safe");
            foreach(var rows in new[]{new[]{new InventoryConsumption(team.Id,"bar",1)},new[]{new InventoryConsumption(safe.Id,"stone",1)},new[]{new InventoryConsumption(team.Id,"stone",int.MaxValue),new InventoryConsumption(team.Id,"stone",1)}})
                Need(l.PrepareConversion("team",rows,Sword(),l.MutationRevision,l.AccessGeneration,out _)==null&&l.Find(team.Id).Quantity==1&&l.Find(safe.Id).Quantity==2,"invalid exact allocation refusal");
            Need(Prepare(l,new[]{new InventoryContainerGrant("bar",int.MaxValue)})==null,"unbounded output refuses before placement loop");
        });
        Run("enough total cells with incompatible shape still rejects",()=>{
            var l=new InventoryLedger();l.Register(new InventoryDefinition("stone",1,1,1));l.Register(new InventoryDefinition("block",1,1,1));l.Register(new InventoryDefinition("wide",2,1,1,InventoryHand.Right));l.AddContainer(new InventoryContainer("team",2,2));l.Grant("team","stone",1);l.Grant("team","block",2);
            var gear=new EquipmentItemState("wide-output","wide",OffhandKind.None,100);var p=Prepare(l,new[]{new InventoryContainerGrant("wide",1,gear)});Need(p==null&&l.Items.Length==3&&l.Find("wide-output")==null,"diagonal spaces cannot fit wide output");
        });
        Run("independent fresh confirmations retain separate payloads",()=>{
            var l=Setup(3,2);Need(Commit(l,Prepare(l,Sword(Gear("first")))).Ok,"first new command");Need(Commit(l,Prepare(l,Sword(Gear("second")))).Ok,"second new command");
            var first=l.Find("first").Equipment;var second=l.Find("second").Equipment;first.Wear(Guid.NewGuid());first.Runtime.Magazine.Consume(Guid.NewGuid());Need(second.Durability==100&&second.Runtime.Magazine.Count==4&&!ReferenceEquals(first.Runtime,second.Runtime),"separate item resources");
        });
        Run("revision overflow refuses without wrapping counters",()=>{
            var l=Setup();typeof(InventoryLedger).GetField("mutationRevision",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(l,long.MaxValue);var p=Prepare(l,Sword());Need(p!=null&&!Commit(l,p).Ok&&l.MutationRevision==long.MaxValue&&l.Items.Single().DefinitionId=="stone","mutation overflow");
            l=Setup();l.Container("team").Revision=long.MaxValue;p=Prepare(l,Sword());Need(p!=null&&!Commit(l,p).Ok&&l.Container("team").Revision==long.MaxValue,"container overflow");
        });
        Console.WriteLine("RESULT "+passed+"/"+passed);
    }
}
