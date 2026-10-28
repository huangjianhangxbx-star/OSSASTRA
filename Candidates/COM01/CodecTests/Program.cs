/* 测试：实际生产CommerceSnapshotCodec完整字段往返与失败拒绝。
 * 依据：真实Unity JsonUtility读档把null装备造默认对象，继而因弹仓capacity=0拒绝恢复。
 * 边界：不替换生产编码、不写真实存档；用真实Ledger/装备能力构造合法快照并交新Economy恢复。
 */
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using Ossastra.Inventory;
using Ossastra.Commerce;
using Ossastra.Commerce.Runtime;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;

sealed class Access:ICommerceAccessPolicy
{
    public CommerceAccess Check(string actorId,string merchantId,long generation)
        =>new CommerceAccess(true,null,1,new[]{"team","safe"},"team","team",new[]{"team","safe"});
}

static class Program
{
    static int passed,failed;
    static readonly Guid WearId=Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid ReloadId=Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid ShotId=Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid CooldownId=Guid.Parse("44444444-4444-4444-4444-444444444444");
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Test(string name,Action body)
    {try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception error){failed++;Console.WriteLine("FAIL "+name+": "+error.GetType().Name+" "+error.Message);}}

    static (InventoryLedger ledger,CommerceEconomy economy) Setup()
    {
        var ledger=new InventoryLedger();
        ledger.Register(new InventoryDefinition("trade-coin",1,1,999999));
        ledger.Register(new InventoryDefinition("food",1,1,20));
        ledger.Register(new InventoryDefinition("ore",2,1,4));
        ledger.Register(new InventoryDefinition("blade",1,1,1,InventoryHand.Right,owners:new[]{"hero-a","hero-b"}));
        ledger.AddContainer(new InventoryContainer("team",5,2));ledger.AddContainer(new InventoryContainer("safe",2,1,true));
        ledger.AddContainer(new InventoryContainer("reserve",5,1,true));ledger.AddContainer(new InventoryContainer("external",5,8));
        ledger.AddContainer(new InventoryContainer("box:two",2,2));ledger.AddContainer(new InventoryContainer("drop:manual:codec",2,1));
        foreach(var actor in new[]{"hero-a","hero-b"})foreach(var hand in new[]{InventoryHand.Left,InventoryHand.Right})
            ledger.AddContainer(new InventoryContainer("hand:"+actor+":"+hand,1,1,true,actor,hand));
        var economy=new CommerceEconomy(ledger,new Access());
        var definition=new MerchantDefinition("sample",new[]{new MerchantGoods("food",10,4,5),new MerchantGoods("ore",12,6,3)},30);
        economy.RegisterMerchant(definition,"shop-a");economy.RegisterMerchant(definition,"shop-b");return(ledger,economy);
    }
    static EquipmentItemState Equipment()
    {
        var item=new EquipmentItemState("blade:broken","blade",OffhandKind.Other,1);
        Require(item.Wear(WearId),"装备磨损夹具未成立");
        item.Runtime.MainActionProfileId="profile:codec";item.Runtime.SkillSelections[0]="skill:one";item.Runtime.SkillSelections[1]="";
        item.Runtime.Tick(2.5,true,false,.5);
        Require(item.Runtime.Cooldowns.Commit("skill:one",CooldownId,item.Runtime.Clock(AbilityCooldownPolicy.WhileEquipped),8.25),"CD夹具未成立");
        Require(item.Runtime.Magazine.BeginReload(ReloadId,AmmoKind.Mortar,3,true,2.5,0),"特殊弹夹具未成立");
        Require(item.Runtime.Magazine.Consume(ShotId,2.5),"发射夹具未成立");
        item.Runtime.Frost.SetModifiers(5,1.5);Require(item.Runtime.Frost.Spend(1.25,2.5),"霜寒夹具未成立");
        var source=new EffectSourceDefinition("effect:codec","gunner","precision",1,"precision","exclusive:codec",.1,2,.5,3,"combo:",new[]{
            new EffectModifier("precision",guaranteedCrit:true,reloadShotRate:.2,reloadShotDamage:.3),
            new EffectModifier("rocket-land",shotRateBonus:.5,shotRateDuration:6.25)});
        Require(item.Runtime.Sources.Grant("source:codec",source,7),"来源夹具未成立");
        var effects=item.Runtime.Sources.Snapshot("gunner",new[]{"precision","rocket-land"},item.Runtime.Frost.Current,false);
        item.Runtime.ShotEnhancements.PrecisionAccepted(effects,2.5);item.Runtime.ShotEnhancements.RocketLanded(effects,2.5,item.Runtime.Sources);
        // 未完成装填是执行草稿，不应在恢复后自动完成；已经生效的2发迫击炮弹必须完整保存。
        Require(item.Runtime.Magazine.BeginReload(Guid.Parse("55555555-5555-5555-5555-555555555555"),AmmoKind.Normal,4,false,2.5,10),"未完成装填夹具未成立");
        return item;
    }
    static CommerceRuntimeSnapshot Fixture()
    {
        var setup=Setup();using(var economy=setup.economy)
        {
            Require(setup.ledger.Grant("team","trade-coin",42).Ok,"授币夹具失败");
            Require(setup.ledger.Grant("team","food",3).Ok,"食物夹具失败");
            Require(setup.ledger.Grant("team","blade",1,Equipment()).Ok,"装备夹具失败");
            Require(setup.ledger.Grant("box:two","ore",2).Ok,"外箱夹具失败");
            Require(setup.ledger.Grant("drop:manual:codec","food",1).Ok,"地面夹具失败");
            var quote=economy.QuotePurchase("hero-a","shop-a","food",2,1);Require(quote.Ok,quote.Reason);
            Require(economy.Execute(new TradeCommand(Guid.Parse("66666666-6666-6666-6666-666666666666"),quote.Quote)).Ok,"真实交易夹具失败");
            economy.Advance(17.125);
            var dto=economy.Capture();var food=dto.inventory.items.Single(i=>i.definitionId=="food"&&i.containerId=="team");
            dto.dialogueFlags=new[]{"sample.dialogue.choiceA"};dto.ownerKeys=new[]{"hero-a","hero-b"};
            dto.worldContainers=new[]{
                new CommerceWorldContainerSnapshot{id="external",mapId="map:one",kind="box",x=1.25f,y=.03f,z=-2.5f,emptied=true},
                new CommerceWorldContainerSnapshot{id="box:two",mapId="map:one",kind="box",x=5.125f,y=.03f,z=-2.5f,emptied=false},
                new CommerceWorldContainerSnapshot{id="drop:manual:codec",mapId="map:one",kind="ground",x=2.25f,y=.03f,z=3.5f,emptied=false}};
            dto.references=new[]{new CommerceReferenceSnapshot{key="hotbar:0",ownerKey="hero-a",definitionId="food",itemId=food.id,slot=0},
                new CommerceReferenceSnapshot{key="priority",ownerKey="hero-b",definitionId="food",itemId=null,slot=0}};
            var runtime=new InventoryRuntimeState{currentMapId="map:one",activeExternalId=null,priorityDefinitionId="food",selectedHotbar=0};
            runtime.hotbar[0]="food";runtime.hotbar[9]="ore";
            runtime.worldContainers=dto.worldContainers.Select(w=>{var c=setup.ledger.Container(w.id);return new InventoryWorldContainerState{id=w.id,mapId=w.mapId,displayName=w.kind=="ground"?"掉落包":"样例箱",x=w.x,y=w.y,z=w.z,width=c.Columns,height=c.Rows,isGround=w.kind=="ground",isEmpty=w.emptied};}).ToArray();
            return new CommerceRuntimeSnapshot{revision=37,economy=dto,inventoryRuntime=runtime,dialogueFlags=(string[])dto.dialogueFlags.Clone(),merchantIds=new[]{"shop-a","shop-b"},ownerIds=new[]{"hero-a","hero-b"}};
        }
    }
    static void Equivalent(object expected,object actual,string path)
    {
        if(expected==null||actual==null){Require(expected==null&&actual==null,path+" null改变");return;}
        var type=expected.GetType();Require(type==actual.GetType(),path+" DTO类型改变");
        if(type.IsPrimitive||type.IsEnum||expected is string||expected is decimal){Require(expected.Equals(actual),path+" 值改变");return;}
        if(expected is Array a)
        {var b=(Array)actual;Require(a.Length==b.Length,path+" 数组长度改变");for(int i=0;i<a.Length;i++)Equivalent(a.GetValue(i),b.GetValue(i),path+"["+i+"]");return;}
        foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))Equivalent(field.GetValue(expected),field.GetValue(actual),path+"."+field.Name);
    }
    static bool Rejected(Action action)
    {try{action();return false;}catch(ArgumentException){return true;}catch(Newtonsoft.Json.JsonException){return true;}}
    static byte[] Bytes(JObject json)=>Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));

    static int Main()
    {
        Test("full_production_codec_preserves_every_public_dto_field",()=>{var source=Fixture();var actual=CommerceSnapshotCodec.Decode(CommerceSnapshotCodec.Encode(source));Equivalent(source,actual,"snapshot");});
        Test("ordinary_items_keep_null_equipment_and_null_hotbar",()=>{var source=Fixture();var actual=CommerceSnapshotCodec.Decode(CommerceSnapshotCodec.Encode(source));Require(actual.economy.inventory.items.Where(i=>i.definitionId!="blade").All(i=>i.equipment==null),"普通物品被伪造装备");Require(actual.inventoryRuntime.hotbar.Length==10&&actual.inventoryRuntime.hotbar[1]==null&&actual.inventoryRuntime.hotbar[8]==null&&actual.inventoryRuntime.activeExternalId==null,"空引用变成默认对象/空串");});
        Test("broken_equipment_magazine_cooldown_resources_restore_without_refill",()=>
        {
            var decoded=CommerceSnapshotCodec.Decode(CommerceSnapshotCodec.Encode(Fixture()));var target=Setup();using(var economy=target.economy)
            {
                var result=economy.Restore(decoded.economy);Require(result.Ok,result.Reason);var item=target.ledger.Find("blade:broken").Equipment;
                Require(item.Durability==0,"破损装备重启后修复");Require(item.Runtime.Magazine.Capacity==3&&item.Runtime.Magazine.Count==2&&item.Runtime.Magazine.Next==AmmoKind.Mortar,"弹序/容量丢失或免费补满");
                Require(!item.Runtime.Magazine.IsReloading,"未完成装填重启后继续执行");Require(item.Runtime.Clock(AbilityCooldownPolicy.WhileEquipped)==1.25&&item.Runtime.Cooldowns.Remaining("skill:one",1.25)==8.25,"CD时基或剩余时间改变");
                Require(item.Runtime.Frost.Current==3.75&&item.Runtime.Frost.Capacity==5,"霜寒资源改变");Require(item.Runtime.SkillSelections[0]=="skill:one"&&item.Runtime.SkillSelections[1]=="","技能选择/主动清空改变");
                Require(item.Runtime.Sources.Contains("source:codec")&&item.Runtime.ShotEnhancements.Snapshot().CriticalSource=="source:codec","来源或射击增益丢失");
                Require(target.ledger.Items.Where(i=>i.DefinitionId=="trade-coin").Sum(i=>i.Quantity)==22&&target.ledger.Items.Single(i=>i.DefinitionId=="food"&&i.Location.ContainerId=="team").Quantity==5,"交易结果恢复不一致");
                Require(economy.Merchant("shop-a").stock.Single(i=>i.definitionId=="food").quantity==3&&economy.Merchant("shop-b").stock.Single(i=>i.definitionId=="food").quantity==5,"双商人状态串档");
            }
        });
        Test("decoded_mutations_do_not_change_original_or_encoded_bytes",()=>
        {
            var source=Fixture();var bytes=CommerceSnapshotCodec.Encode(source);var decoded=CommerceSnapshotCodec.Decode(bytes);
            decoded.economy.inventory.items.Single(i=>i.id=="blade:broken").equipment.runtime.magazine.rounds[0]=(int)AmmoKind.Normal;
            decoded.economy.merchants[0].stock[0].quantity=99;decoded.inventoryRuntime.hotbar[0]="ore";decoded.inventoryRuntime.worldContainers[0].x=999;
            Require(source.economy.inventory.items.Single(i=>i.id=="blade:broken").equipment.runtime.magazine.rounds[0]==(int)AmmoKind.Mortar&&source.inventoryRuntime.hotbar[0]=="food"&&source.inventoryRuntime.worldContainers[0].x==1.25f,"解码仍引用源快照");
            Equivalent(source,CommerceSnapshotCodec.Decode(bytes),"second decode");
        });
        Test("invalid_payload_schema_and_null_rejected",()=>
        {
            Require(Rejected(()=>CommerceSnapshotCodec.Encode(null))&&Rejected(()=>CommerceSnapshotCodec.Decode(null)),"null载荷未拒绝");
            foreach(var schema in new[]{1,0,99}){var json=JObject.Parse(Encoding.UTF8.GetString(CommerceSnapshotCodec.Encode(Fixture())));json["schema"]=schema;Require(Rejected(()=>CommerceSnapshotCodec.Decode(Bytes(json))),"未知schema被接受: "+schema);}
            var old=Fixture();old.schema=1;Require(Rejected(()=>CommerceSnapshotCodec.Encode(old)),"旧schema可重新编码");
        });
        foreach(var property in new[]{"$type","$ref","$id"})
        {
            var key=property;
            Test("root_metadata_"+key+"_rejected",()=>{var json=JObject.Parse(Encoding.UTF8.GetString(CommerceSnapshotCodec.Encode(Fixture())));json.AddFirst(new JProperty(key,"forbidden"));Require(Rejected(()=>CommerceSnapshotCodec.Decode(Bytes(json))),key+"根元数据未拒绝");});
            Test("nested_metadata_"+key+"_rejected",()=>{var json=JObject.Parse(Encoding.UTF8.GetString(CommerceSnapshotCodec.Encode(Fixture())));((JObject)json["inventoryRuntime"]).AddFirst(new JProperty(key,"forbidden"));Require(Rejected(()=>CommerceSnapshotCodec.Decode(Bytes(json))),key+"嵌套元数据未拒绝");});
        }
        Test("unknown_root_and_nested_fields_rejected",()=>
        {
            var json=JObject.Parse(Encoding.UTF8.GetString(CommerceSnapshotCodec.Encode(Fixture())));json["unexpectedEconomicOwner"]=true;Require(Rejected(()=>CommerceSnapshotCodec.Decode(Bytes(json))),"未知根字段被忽略");
            json=JObject.Parse(Encoding.UTF8.GetString(CommerceSnapshotCodec.Encode(Fixture())));var equipment=(JObject)((JArray)json["economy"]["inventory"]["items"]).Single(i=>(string)i["id"]=="blade:broken")["equipment"];equipment["injectedDefaultMagazine"]=new JObject();Require(Rejected(()=>CommerceSnapshotCodec.Decode(Bytes(json))),"未知装备字段被忽略");
        });
        Console.WriteLine("Production codec: "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}
