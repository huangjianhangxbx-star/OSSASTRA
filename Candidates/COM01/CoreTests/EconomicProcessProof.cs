/* 测试：两个实际独立进程通过真实经济DTO与串行文件保存器交接，不以停止Play代替重启。 */
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Commerce;
using Ossastra.Commerce.Persistence;
using Ossastra.Inventory;

static class EconomicProcessProof
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
    static (InventoryLedger ledger,CommerceEconomy economy) Fixture()
    {
        var ledger=new InventoryLedger();ledger.Register(new InventoryDefinition("trade-coin",1,1,999999));ledger.Register(new InventoryDefinition("food",1,1,10));ledger.Register(new InventoryDefinition("blade",1,1,1,InventoryHand.Right));
        ledger.AddContainer(new InventoryContainer("team",4,2));ledger.AddContainer(new InventoryContainer("safe",2,1,true));ledger.AddContainer(new InventoryContainer("hand:hero:Right",1,1,true,"hero",InventoryHand.Right));
        var economy=new CommerceEconomy(ledger,new AccessPolicy());var definition=new MerchantDefinition("sample",new[]{new MerchantGoods("food",10,4,5)},30);economy.RegisterMerchant(definition,"shop-a");economy.RegisterMerchant(definition,"shop-b");return(ledger,economy);
    }
    internal static int Run(string[] args)
    {
        try
        {
            if(args[0]=="--process-proof")
            {
                string directory=Path.Combine(AppContext.BaseDirectory,"economic-process-proof-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
                Spawn("--process-writer",directory);Spawn("--process-reader",directory);Console.WriteLine("PASS actual economic writer process exited, new reader process restored: "+directory);return 0;
            }
            if(args.Length!=2)throw new ArgumentException("Missing isolated proof directory");
            var fixture=Fixture();var store=new CommerceFileSaveStore(args[1]);
            if(args[0]=="--process-writer")
            {
                fixture.ledger.Grant("team","trade-coin",30);
                var purchase=fixture.economy.QuotePurchase("hero","shop-a","food",2,1);Require(purchase.Ok,purchase.Reason);Require(fixture.economy.Execute(new TradeCommand(Guid.NewGuid(),purchase.Quote)).Ok,"purchase failed");
                var food=fixture.ledger.Items.Single(i=>i.DefinitionId=="food");var sale=fixture.economy.QuoteSale("hero","shop-a",food.Id,1,1);Require(sale.Ok,sale.Reason);Require(fixture.economy.Execute(new TradeCommand(Guid.NewGuid(),sale.Quote)).Ok,"sale failed");
                Require(fixture.ledger.CreateContainerWithItems(new InventoryContainer("box-a",2,1),new[]{new InventoryContainerGrant("food",2)}).Ok,"box seed failed");
                Require(fixture.ledger.CreateContainerWithItems(new InventoryContainer("drop-a",1,1),Array.Empty<InventoryContainerGrant>()).Ok,"drop container failed");
                Require(fixture.ledger.Transfer(fixture.ledger.Plan(food.Id,"drop-a")).Ok,"drop transfer failed");
                var equipment=new EquipmentItemState("persistent-blade","blade",OffhandKind.Other,1);equipment.Wear(Guid.NewGuid());equipment.Runtime.Tick(10,true);equipment.Runtime.Magazine.Consume(Guid.NewGuid(),10);equipment.Runtime.Frost.Spend(1,10);equipment.Runtime.Cooldowns.Commit("skill-a",Guid.NewGuid(),10,8);fixture.ledger.Grant("hand:hero:Right","blade",1,equipment);
                fixture.economy.Advance(10);
                var snapshot=fixture.economy.Capture();snapshot.ownerKeys=new[]{"hero"};snapshot.dialogueFlags=new[]{"freya:greeting"};snapshot.worldContainers=new[]{new CommerceWorldContainerSnapshot{id="box-a",mapId="map-a",kind="box",x=2},new CommerceWorldContainerSnapshot{id="drop-a",mapId="map-a",kind="drop",x=3}};
                var bytes=JsonSerializer.SerializeToUtf8Bytes(snapshot,Json);
                var saves=new CommerceSaveCoordinator(store,"economic-proof","writer",0,0);saves.RequestSave(snapshot.stateRevision,bytes,1,snapshot.definitionFingerprint);Require(saves.CloseAsync().GetAwaiter().GetResult(),"normal close failed");Console.WriteLine("PASS writer bought/sold and saved DTO with equipment, two merchants, box/drop records");return 0;
            }
            if(args[0]=="--process-reader")
            {
                var loaded=store.Load("economic-proof");Require(loaded.Status==CommerceSaveLoadStatus.Loaded,loaded.Error);
                var snapshot=JsonSerializer.Deserialize<CommerceEconomySnapshot>(loaded.Snapshot.Payload,Json);var restored=fixture.economy.Restore(snapshot);Require(restored.Ok,restored.Reason);
                Require(fixture.ledger.Items.Where(i=>i.DefinitionId=="trade-coin").Sum(i=>i.Quantity)==14,"currency not restored");
                Require(fixture.ledger.Items.Where(i=>i.DefinitionId=="food").Sum(i=>i.Quantity)==3,"food not restored");
                Require(fixture.ledger.Items.Single(i=>i.Location.ContainerId=="drop-a").Quantity==1&&fixture.ledger.Items.Single(i=>i.Location.ContainerId=="box-a").Quantity==2,"box/drop not restored");
                Require(fixture.economy.Merchant("shop-a").stock.Single().quantity==3&&fixture.economy.Merchant("shop-b").stock.Single().quantity==5,"merchant stocks mixed");
                Require(fixture.economy.SimulationTime==10&&fixture.economy.Merchant("shop-a").nextRefresh==30,"refresh clock changed while process closed");
                var equipment=fixture.ledger.Find("persistent-blade").Equipment;Require(equipment.Durability==0&&equipment.Runtime.Magazine.Count==3&&equipment.Runtime.Frost.Current==2&&equipment.Runtime.Cooldowns.Remaining("skill-a",10)==8,"equipment got a free reset");
                Require(snapshot.worldContainers.Length==2&&snapshot.dialogueFlags.Single()=="freya:greeting","host data missing");
                Console.WriteLine("PASS fresh reader restored exact currency, items, identity, equipment resources, distinct stocks, clock and host DTO records");return 0;
            }
            throw new ArgumentException("Unknown test mode");
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void Spawn(string mode,string directory)
    {
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(typeof(EconomicProcessProof).Assembly.Location);start.ArgumentList.Add(mode);start.ArgumentList.Add(directory);
        using(var process=Process.Start(start))
        {string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();Require(process.WaitForExit(30000),"proof child timeout");Console.Write(output);Require(process.ExitCode==0,error);}
    }
}
