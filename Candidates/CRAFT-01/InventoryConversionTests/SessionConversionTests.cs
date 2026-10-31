// 使用原COM01外壳，测真实Session转换门；不代签物理LOS/原装备SO/Unity运行。
using System;
using System.Linq;
using System.Reflection;
using Ossastra.Inventory;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using UnityEngine;
static class SessionConversionTests
{
    static int passed;
    static void Need(bool c,string m){if(!c)throw new Exception(m);}
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    static InventorySession Setup()
    {
        var s=new InventorySession();var c=new CharacterControlCoordinator();foreach(var id in new[]{"hero","partner"}){var a=new Ossastra.Characters.CharacterRoot{ActorId=id};a.components[typeof(CharacterEquipment)]=new CharacterEquipment{State=new EquipmentState(id,100)};c.Characters.Add(a);}c.Current=c.Characters[0];
        Set(s,"coordinator",c);Set(s,"catalog",new[]{new ItemDefinitionSO{definitionId="stone",maxStack=99},new ItemDefinitionSO{definitionId="bar",maxStack=99}});Set(s,"chest",new Transform());typeof(InventorySession).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);Need(s.Ready,s.LastResult);s.Ledger.Grant("team","stone",3);return s;
    }
    static object Prepare(InventorySession s)
    {
        var type=typeof(InventoryLedger).Assembly.GetType("Ossastra.Inventory.InventoryConsumption");Need(type!=null,"team conversion should prepare: consumption API missing");var input=Array.CreateInstance(type,1);input.SetValue(Activator.CreateInstance(type,s.Ledger.Items.Single().Id,"stone",1),0);
        var m=typeof(InventorySession).GetMethod("PrepareTeamConversion");Need(m!=null,"team conversion should prepare: Session wrapper missing");
        var args=new object[]{input,new[]{new InventoryContainerGrant("bar",1)},s.Ledger.MutationRevision,s.Ledger.AccessGeneration,null};return m.Invoke(s,args);
    }
    static InventoryResult Commit(InventorySession s,object p,Func<string> final=null,Action publish=null)=>(InventoryResult)typeof(InventorySession).GetMethod("CommitTeamConversion").Invoke(s,new object[]{p,final??(()=>null),publish??(()=>{})});
    static void Run(string n,Action b){b();passed++;Console.WriteLine("PASS "+n);}
    static void Main()
    {
        Run("team wrapper prepares read only and publishes receipt before Changed",()=>{var s=Setup();var p=Prepare(s);Need(p!=null&&s.Ledger.Items.Single().Quantity==3,"read only");bool receipt=false;int changed=0;s.Changed+=()=>{Need(receipt,"receipt before UI");changed++;};Need(Commit(s,p,publish:()=>receipt=true).Ok&&changed==1&&s.Ledger.Items.Single(i=>i.DefinitionId=="stone").Quantity==2,"wrapper commit");});
        Run("save failure rejects preparation and final commit",()=>{var s=Setup();var p=Prepare(s);s.ConfigureEditGate(()=>false);Need(Prepare(s)==null&&!Commit(s,p).Ok&&s.Ledger.Items.Single().Quantity==3,"save gate");});
        Run("dead suspended and changed actor reject old confirmation",()=>{foreach(var mode in new[]{0,1,2}){var s=Setup();var p=Prepare(s);if(mode==0)s.Coordinator.Current.IsAlive=false;else if(mode==1)s.Coordinator.Current.IsSimulationSuspended=true;else{s.Coordinator.Current=s.Coordinator.Characters[1];s.Coordinator.ControlRevision++;}Need(!Commit(s,p).Ok&&s.Ledger.Items.Single().Quantity==3,"actor context gate");}});
        Run("craft final qualification is checked at commit",()=>{var s=Setup();var p=Prepare(s);Need(!Commit(s,p,()=>"station disabled").Ok&&s.Ledger.Items.Single().Quantity==3,"final station gate");});
        Run("invalid author definition returns refusal without throwing or losing materials",()=>{var s=Setup();s.Definition("bar").maxStack=0;Need(Prepare(s)==null&&s.Ledger.Items.Single().Quantity==3,"invalid author data refusal");});
        Console.WriteLine("RESULT "+passed+"/"+passed);
    }
}
