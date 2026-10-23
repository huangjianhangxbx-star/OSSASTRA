using System;using System.Linq;using Ossastra.Inventory;using Ossastra.CharacterCore;
static class InventoryTests
{
 sealed class Slots:IEquipmentSlots {public EquipmentItemState Right {get;set;}public EquipmentItemState Left {get;set;}}
 static int count;static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 static InventoryLedger Setup(){var m=new InventoryLedger();m.Register(new InventoryDefinition("food",1,1,20));m.Register(new InventoryDefinition("blade",1,2,1,InventoryHand.Right));m.AddContainer(new InventoryContainer("team",5,2));m.AddContainer(new InventoryContainer("safe",2,1,true));m.AddContainer(new InventoryContainer("hand",1,1,true,"hero",InventoryHand.Right));m.AddContainer(new InventoryContainer("ground",10,10,true));return m;}
 static void Main()
 {
    var slots=new Slots();var facade=new EquipmentState("hero",100);facade.BindSlots(slots);Check(facade.RightInstanceId=="none"&&facade.LeftKind==OffhandKind.None,"empty external slots have no phantom equipment");var owned=new EquipmentItemState("stable-shield","shield-a",OffhandKind.Shield,31);slots.Left=owned;Check(facade.LeftInstanceId=="stable-shield"&&ReferenceEquals(facade.LeftRuntime,owned.Runtime),"equipment facade reads inventory payload");var hit=Guid.NewGuid();Check(facade.Wear(hit)&&!facade.Wear(hit)&&owned.Durability==30,"wear one actual item once");slots.Left=null;Check(!facade.TryEquip(OffhandKind.Shield,true)&&facade.LeftInstanceId=="none","legacy equip cannot bypass bound inventory");slots.Left=owned;Check(facade.Durability==30,"reequip does not reset durability");
  var m=Setup();var payload=new EquipmentItemState("weapon-1","blade",OffhandKind.Other,76);payload.Runtime.MainActionProfileId="form-II";
  Check(m.Grant("team","blade",1,payload).Ok,"grant real equipment");var item=m.Items.Single();var id=item.Id;
  Check(m.Transfer(m.Plan(id,"hand")).Ok,"pack to hand");Check(m.Items.Single().Id==id&&ReferenceEquals(m.Items.Single().Equipment,payload),"equipment stable identity/payload");
  Check(m.Transfer(m.Plan(id,"ground")).Ok&&m.Transfer(m.Plan(id,"team")).Ok,"hand ground backpack path");Check(m.Find(id).Equipment.Durability==76&&m.Find(id).Equipment.Runtime.MainActionProfileId=="form-II","move preserves wear and form");
  var stale=m.Plan(id,"hand");Check(m.Grant("team","food",2).Ok,"grant quantity");Check(!m.Transfer(stale).Ok&&m.Find(id).Location.ContainerId=="team","stale revision no mutation");
  var food=m.Items.First(i=>i.DefinitionId=="food");var before=food.Id;Check(m.Transfer(m.Plan(food.Id,"safe",1)).Ok,"split one into safe");Check(m.Items.Count(i=>i.DefinitionId=="food")==2&&m.Find(before).Quantity==1,"split gives distinct stack identity");
  var split=m.Items.First(i=>i.DefinitionId=="food"&&i.Location.ContainerId=="safe");Check(m.Transfer(m.Plan(split.Id,"team")).Ok,"merge into existing stack");Check(m.Items.Count(i=>i.DefinitionId=="food")==1&&m.Find(before).Quantity==2,"merge conserves quantity");
  var token=Guid.NewGuid();Check(m.Reserve(before,1,token).Ok,"reserve real quantity");Check(!m.Transfer(m.Plan(before,"safe")).Ok,"reserved stack cannot move");Check(m.CommitConsumption(token).Ok&&m.Find(before).Quantity==1,"release consumes one");Check(!m.CommitConsumption(token).Ok&&m.Find(before).Quantity==1,"duplicate release does not consume");
  var staleAccess=m.Plan(id,"hand");m.InvalidateAccess();Check(!m.Transfer(staleAccess).Ok,"access generation invalidates drag");
  Check(!m.Transfer(m.Plan(before,"hand")).Ok,"non equipment cannot enter hand");
  Check(m.Transfer(m.Plan(before,"safe")).Ok,"safe protected slot");var wipe=Guid.NewGuid();Check(m.DropAll(wipe,new[]{"team","hand"},"drop-1").Ok,"wipe batch transfer");Check(!m.Container("drop-1").Slots&&m.Container("drop-1").Columns==5,"death bag uses source grid geometry");Check(m.Find(before).Location.ContainerId=="safe"&&m.Find(id).Location.ContainerId=="drop-1","safe survives wipe original weapon drops");Check(!m.DropAll(wipe,new[]{"team"},"drop-2").Ok&&!m.Containers.Any(c=>c.Id=="drop-2"),"wipe identity deduplicates");
    var over=Setup();over.Grant("team","food",25);var stacks=over.Items.OrderByDescending(i=>i.Quantity).ToArray();var full=stacks[0];var little=stacks[1];Check(!over.Transfer(over.Plan(little.Id,"team",1,full.Location.X,full.Location.Y)).Ok&&over.Find(little.Id).Quantity==5,"explicit overflowing merge rejects entire move");
  var swap=Setup();swap.Grant("team","food",1);swap.Grant("team","blade",1,new EquipmentItemState("swap-blade","blade",OffhandKind.Other,100));var f=swap.Items.First(i=>i.DefinitionId=="food");var b=swap.Find("swap-blade");Check(swap.Transfer(swap.Plan(f.Id,"team",0,b.Location.X,b.Location.Y)).Ok&&swap.Find("swap-blade").Location.X==f.Location.X,"explicit grid swap preserves both items");
    var oldSort=swap.PlanSort("team");swap.Grant("team","food",1);Check(!swap.CommitSort(oldSort).Ok,"stale sort cannot overwrite new quantities");Check(!swap.CommitSort(swap.PlanSort("team",0)).Ok,"bounded sort failure preserves manual operation");Check(swap.CommitSort(swap.PlanSort("team")).Ok&&swap.Validate(out _),"finite fixed orientation sorting commits valid layout");Check(!swap.RemoveEmptyContainer("team"),"cannot remove occupied container");swap.AddContainer(new InventoryContainer("empty",1,1));Check(swap.RemoveEmptyContainer("empty")&&swap.Container("empty")==null,"empty ground container reclaimed");
    var packed=Setup();packed.Grant("team","food",25);var pstack=packed.Items.Single(i=>i.Quantity==20);packed.Transfer(packed.Plan(pstack.Id,"safe",5));var separate=packed.Items.Single(i=>i.Location.ContainerId=="safe");packed.Transfer(packed.Plan(separate.Id,"team",0,4,1));Check(packed.Items.Length==3,"three partial stacks fixture");Check(packed.CommitSort(packed.PlanSort("team")).Ok&&packed.Items.Length==2&&packed.Items.Sum(i=>i.Quantity)==25,"sorting merges legal stacks while conserving quantities");
  Check(m.Validate(out var error),"global unique placement and valid quantities: "+error);Console.WriteLine("TOTAL "+count);
 }
}





