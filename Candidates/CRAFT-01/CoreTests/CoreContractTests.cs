using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

// 测试通过反射调用真实产品类型：生产目录为空时也能得到行为断言RED，无测试替身。
internal static class CoreContractTests
{
    private static int passed;
    private static int failed;
    private static Type T(string name)
    {
        var type = Assembly.GetExecutingAssembly().GetType("Ossastra.Crafting.Core." + name);
        Assert(type != null, "尚未实现核心类型 " + name);
        return type;
    }
    private static object New(string name, params object[] args)
    {
        try { return Activator.CreateInstance(T(name), args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Call(string name, string method, params object[] args)
    {
        try { return T(name).GetMethod(method).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Get(object value, string prop) { return value.GetType().GetProperty(prop).GetValue(value); }
    private static object Field(object value, string field) { return value.GetType().GetField(field).GetValue(value); }
    private static void Set(object value, string field, object data) { value.GetType().GetField(field).SetValue(value, data); }
    private static object Invoke(object value, string method, params object[] args)
    {
        try { return value.GetType().GetMethod(method).Invoke(value,args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static Array Of(string name, params object[] values)
    {
        var array = Array.CreateInstance(T(name), values.Length);
        for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
        return array;
    }
    private static object I(string id, int q) { return New("CraftIngredient", id, q); }
    private static object O(string id, int q) { return New("CraftOutput", id, q); }
    private static object Stack(string id, string def, int q, bool reserved = false, bool equipment = false)
    { return New("CraftMaterialStack", id, def, q, reserved, equipment); }
    private static object C(string id, string def, int q) { return New("CraftMaterialConsumption", id, def, q); }
    private static object Recipe(int maxBatch = 9, int output = 1)
    { return New("CraftRecipe", "blade", 1, "bench", "weapon", Of("CraftIngredient", I("fibre", 3), I("stone", 2)), Of("CraftOutput", O("blade", output)), maxBatch); }
    private static object Plan(object r, int q, params object[] stacks)
    { return Call("CraftPlanner", "Plan", r, q, Of("CraftMaterialStack", stacks)); }
    private static object[] List(object obj, string prop) { return ((IEnumerable)Get(obj, prop)).Cast<object>().ToArray(); }
    private static int Count(object plan, string prop, string key)
    { return ((IDictionary<string, int>)Get(plan, prop))[key]; }
    private static string Summary(object p)
    { return string.Join(";", List(p,"Consumptions").Select(c => Get(c,"DefinitionId")+"/"+Get(c,"ItemId")+"="+Get(c,"Quantity"))); }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Eq(object actual, object expected) { Assert(object.Equals(actual, expected), "expected " + expected + ", actual " + actual); }
    private static void Throws<TEx>(Action action) where TEx : Exception
    {
        try { action(); }
        catch (TEx) { return; }
        throw new InvalidOperationException("expected rejection " + typeof(TEx).Name);
    }
    private static void Reject(object plan, string reason)
    { Eq(Get(plan,"Ok"),false); Eq(Get(plan,"Reason"),reason); Eq(List(plan,"Consumptions").Length,0); }
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static object[] IdentityArgs()
    { return new object[] {"blade",1,"bench-def","bench-1","map-1","actor-1",4L,"interaction-1","request-1",1,10L,2L,Of("CraftMaterialConsumption",C("s2","stone",2),C("s1","fibre",3))}; }
    private static string Finger(object[] args) { return (string)Call("CraftIdentity","Fingerprint",args); }
    private static object ReceiptPart(string name,string id,string def,int quantity)
    {var x=New(name);Set(x,"itemId",id);Set(x,"definitionId",def);Set(x,"quantity",quantity);return x;}
    private static void Sign(object r)
    {
        var items=((IEnumerable)Field(r,"consumptions")).Cast<object>().Select(x=>C((string)Field(x,"itemId"),(string)Field(x,"definitionId"),(int)Field(x,"quantity"))).ToArray();
        Set(r,"fingerprint",Finger(new object[]{Field(r,"recipeId"),Field(r,"version"),Field(r,"stationDefinitionId"),Field(r,"stationInstanceId"),Field(r,"mapId"),Field(r,"actorId"),Field(r,"controlRevision"),Field(r,"sessionId"),Field(r,"requestId"),Field(r,"quantity"),Field(r,"ledgerRevision"),Field(r,"accessGeneration"),Of("CraftMaterialConsumption",items)}));
    }
    private static object Receipt(int sequence=1)
    {
        var r=New("CraftReceiptRecord");
        Set(r,"requestId",sequence.ToString("x32"));Set(r,"recipeId","blade");Set(r,"version",1);Set(r,"quantity",1);
        Set(r,"stationDefinitionId","bench-def");Set(r,"stationInstanceId","bench-1");Set(r,"mapId","map-1");Set(r,"actorId","actor-1");
        Set(r,"controlRevision",4L);Set(r,"sessionId",(1000+sequence).ToString("x32"));Set(r,"ledgerRevision",10L);Set(r,"accessGeneration",2L);
        Set(r,"consumptions",Of("CraftReceiptConsumption",ReceiptPart("CraftReceiptConsumption","s1","fibre",3),ReceiptPart("CraftReceiptConsumption","s2","stone",2)));
        Set(r,"outputs",Of("CraftReceiptOutput",ReceiptPart("CraftReceiptOutput","made","blade",1)));Sign(r);return r;
    }
    private static object[] Find(object journal,object receipt)
    {var a=new object[]{Field(receipt,"requestId"),Field(receipt,"fingerprint"),null,null};var found=Invoke(journal,"Find",a);return new[]{found,a[2],a[3]};}
    private static object Restore(params object[] records)
    {return Call("CraftReceiptJournal","Restore",Of("CraftReceiptRecord",records),New("CraftCatalog",Of("CraftRecipe",Recipe())));}

    public static int Main()
    {
        Test("ingredient rejects whitespace identity",()=>Throws<ArgumentException>(()=>I("  ",1)));
        Test("ingredient rejects zero quantity",()=>Throws<ArgumentOutOfRangeException>(()=>I("stone",0)));
        Test("output rejects negative quantity",()=>Throws<ArgumentOutOfRangeException>(()=>O("blade",-1)));
        Test("source rejects empty item identity",()=>Throws<ArgumentException>(()=>Stack("","stone",1)));
        Test("source rejects zero quantity",()=>Throws<ArgumentOutOfRangeException>(()=>Stack("a","stone",0)));
        Test("consumption rejects invalid quantity",()=>Throws<ArgumentOutOfRangeException>(()=>C("a","stone",0)));
        Test("recipe checked aggregates same material",()=> {
            var r=New("CraftRecipe","r",1,"bench","cat",Of("CraftIngredient",I("stone",2),I("stone",3)),Of("CraftOutput",O("blade",1)),9);
            var p=Plan(r,1,Stack("a","stone",5));Eq(Get(p,"Ok"),true);Eq(Count(p,"Required","stone"),5);Eq(Summary(p),"stone/a=5");
        });
        Test("recipe ingredients are frozen from caller array",()=> {
            var input=Of("CraftIngredient",I("stone",2));
            var r=New("CraftRecipe","r",1,"bench","cat",input,Of("CraftOutput",O("blade",1)),1);
            input.SetValue(I("stone",99),0);Eq(Get(List(r,"Ingredients")[0],"Quantity"),2);
            Throws<NotSupportedException>(()=>((IList)Get(r,"Ingredients"))[0]=I("stone",77));
        });
        Test("recipe outputs are frozen from caller array",()=> {
            var output=Of("CraftOutput",O("blade",1));
            var r=New("CraftRecipe","r",1,"bench","cat",Of("CraftIngredient",I("stone",2)),output,1);
            output.SetValue(O("other",2),0);Eq(Get(List(r,"Outputs")[0],"DefinitionId"),"blade");
            Throws<NotSupportedException>(()=>((IList)Get(r,"Outputs"))[0]=O("other",1));
        });
        Test("recipe rejects material aggregate overflow",()=>Throws<OverflowException>(()=>New("CraftRecipe","r",1,"bench","cat",Of("CraftIngredient",I("stone",int.MaxValue),I("stone",1)),Of("CraftOutput",O("blade",1)),1)));
        Test("recipe rejects total material overflow",()=>Throws<OverflowException>(()=>New("CraftRecipe","r",1,"bench","cat",Of("CraftIngredient",I("stone",int.MaxValue),I("fibre",1)),Of("CraftOutput",O("blade",1)),1)));
        Test("recipe rejects invalid version",()=>Throws<ArgumentOutOfRangeException>(()=>New("CraftRecipe","r",0,"bench","cat",Of("CraftIngredient",I("stone",1)),Of("CraftOutput",O("blade",1)),1)));
        Test("recipe rejects missing capability",()=>Throws<ArgumentException>(()=>New("CraftRecipe","r",1," ","cat",Of("CraftIngredient",I("stone",1)),Of("CraftOutput",O("blade",1)),1)));
        Test("recipe rejects maxbatch 100",()=>Throws<ArgumentOutOfRangeException>(()=>Recipe(100)));
        Test("recipe rejects output budget above 99",()=>Throws<ArgumentOutOfRangeException>(()=>Recipe(1,100)));
        Test("recipe rejects empty materials",()=>Throws<ArgumentException>(()=>New("CraftRecipe","r",1,"bench","cat",Of("CraftIngredient"),Of("CraftOutput",O("blade",1)),1)));
        Test("recipe rejects empty outputs",()=>Throws<ArgumentException>(()=>New("CraftRecipe","r",1,"bench","cat",Of("CraftIngredient",I("stone",1)),Of("CraftOutput"),1)));
        Test("catalog rejects duplicate recipe identity",()=>Throws<ArgumentException>(()=>New("CraftCatalog",Of("CraftRecipe",Recipe(),Recipe()))));
        Test("catalog freezes caller array",()=> {
            var r=Recipe();var a=Of("CraftRecipe",r);var catalog=New("CraftCatalog",a);a.SetValue(null,0);Eq(List(catalog,"Recipes").Length,1);Eq(Get(List(catalog,"Recipes")[0],"Id"),"blade");
        });
        Test("catalog resolves exact case-sensitive identity",()=> {
            var catalog=New("CraftCatalog",Of("CraftRecipe",Recipe()));
            var method=T("CraftCatalog").GetMethod("TryGetRecipe");var args=new object[]{"blade",null};
            Eq(method.Invoke(catalog,args),true);Eq(Get(args[1],"Id"),"blade");
            args=new object[]{"BLADE",null};Eq(method.Invoke(catalog,args),false);Eq(args[1],null);
        });
        Test("planner allocates ordinal definitions and item IDs",()=> {
            var p=Plan(Recipe(),2,Stack("z","stone",9),Stack("f2","fibre",4),Stack("a","stone",3),Stack("f1","fibre",4));
            Eq(Get(p,"Ok"),true);Eq(Summary(p),"fibre/f1=4;fibre/f2=2;stone/a=3;stone/z=1");Eq(Count(p,"Required","fibre"),6);Eq(Count(p,"Available","stone"),12);
        });
        Test("planner permutation does not alter allocation",()=> {
            var a=Stack("b","stone",3);var b=Stack("a","stone",4);var c=Stack("c","fibre",3);
            Eq(Summary(Plan(Recipe(),1,a,b,c)),Summary(Plan(Recipe(),1,c,b,a)));
        });
        Test("planner excludes whole reserved stack",()=> {
            var p=Plan(Recipe(),1,Stack("a","stone",50,true),Stack("b","stone",1),Stack("c","fibre",3));Reject(p,"InsufficientMaterials");Eq(Count(p,"Available","stone"),1);
        });
        Test("planner excludes equipment from materials",()=> {
            var p=Plan(Recipe(),1,Stack("a","stone",50,false,true),Stack("b","stone",1),Stack("c","fibre",3));Reject(p,"InsufficientMaterials");Eq(Count(p,"Available","stone"),1);
        });
        Test("planner rejection never exposes partial consumption",()=>Reject(Plan(Recipe(),1,Stack("a","fibre",3)),"InsufficientMaterials"));
        Test("planner rejects duplicate identity across definitions",()=>Reject(Plan(Recipe(),1,Stack("a","stone",2),Stack("a","fibre",3)),"DuplicateItemId"));
        Test("planner rejects duplicate even when one reserved",()=>Reject(Plan(Recipe(),1,Stack("a","stone",2,true),Stack("a","stone",3)),"DuplicateItemId"));
        Test("planner rejects zero batch",()=>Reject(Plan(Recipe(),0),"InvalidQuantity"));
        Test("planner accepts bounded batch 99",()=> {
            var p=Plan(Recipe(99),99,Stack("s","stone",198),Stack("f","fibre",297));Eq(Get(p,"Ok"),true);Eq(Summary(p),"fibre/f=297;stone/s=198");
        });
        Test("planner rejects missing source list",()=>Reject(Call("CraftPlanner","Plan",Recipe(),1,null),"InvalidMaterials"));
        Test("planner rejects null source record",()=>Reject(Plan(Recipe(),1,new object[]{null}),"InvalidMaterials"));
        Test("planner rejects batch above recipe limit",()=>Reject(Plan(Recipe(1),2),"InvalidQuantity"));
        Test("planner rejects final output budget above 99",()=>Reject(Plan(Recipe(99,2),50),"OutputBudgetExceeded"));
        Test("planner rejects batched material overflow",()=> {
            var r=New("CraftRecipe","r",1,"bench","cat",Of("CraftIngredient",I("stone",int.MaxValue)),Of("CraftOutput",O("blade",1)),2);Reject(Plan(r,2),"Overflow");
        });
        Test("planner rejects available count overflow",()=>Reject(Plan(Recipe(),1,Stack("a","stone",int.MaxValue),Stack("b","stone",1),Stack("c","fibre",3)),"Overflow"));
        Test("planner never mutates source snapshots",()=> {
            var s=Stack("a","stone",5);Plan(Recipe(),1,s,Stack("f","fibre",3));Eq(Get(s,"Quantity"),5);Eq(Get(s,"Reserved"),false);
        });
        Test("planner freezes its returned facts",()=> {
            var p=Plan(Recipe(),1,Stack("s","stone",2),Stack("f","fibre",3));
            Throws<NotSupportedException>(()=>((IList)Get(p,"Consumptions"))[0]=C("x","fibre",3));
            Throws<NotSupportedException>(()=>((IDictionary<string,int>)Get(p,"Required"))["stone"]=99);
        });
        Test("fingerprint is 64 lowercase hex",()=> {
            var s=Finger(IdentityArgs());Assert(s.Length==64 && s.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f')),"not SHA256 hex");
        });
        // 期望摘要由独立Python struct.pack('<I') + hashlib算出，不复用产品序列化辅助方法。
        Test("fingerprint matches independent canonical golden",()=>Eq(Finger(IdentityArgs()),"b515525b7211779ec971f39837bd14b319e50d0ef5318abde8f57c65646caf33"));
        Test("fingerprint canonicalizes source order",()=> {
            var a=IdentityArgs();var b=IdentityArgs();b[12]=Of("CraftMaterialConsumption",C("s1","fibre",3),C("s2","stone",2));Eq(Finger(a),Finger(b));
        });
        Test("fingerprint changes for every command fact",()=> {
            var baseline=IdentityArgs();var hash=Finger(baseline);
            for(int i=0;i<12;i++){var changed=IdentityArgs();if(changed[i] is string)changed[i]=(string)changed[i]+"x";else if(changed[i] is int)changed[i]=(int)changed[i]+1;else changed[i]=(long)changed[i]+1;Assert(Finger(changed)!=hash,"field omitted at "+i);}
        });
        Test("fingerprint changes for material item definition and quantity",()=> {
            var hash=Finger(IdentityArgs());
            foreach(var c in new[]{C("different","fibre",3),C("s1","different",3),C("s1","fibre",4)}) {var a=IdentityArgs();a[12]=Of("CraftMaterialConsumption",c,C("s2","stone",2));Assert(Finger(a)!=hash,"consumption fact omitted");}
        });
        Test("fingerprint length prefixes prevent delimiter ambiguity",()=> {
            var a=IdentityArgs();var b=IdentityArgs();a[0]="a|b";a[2]="c";b[0]="a";b[2]="b|c";Assert(Finger(a)!=Finger(b),"ambiguous boundary");
        });
        Test("fingerprint rejects duplicate material item IDs",()=>Throws<ArgumentException>(()=> {var a=IdentityArgs();a[12]=Of("CraftMaterialConsumption",C("same","fibre",1),C("same","stone",2));Finger(a);}));
        Test("fingerprint rejects negative ledger revision",()=>Throws<ArgumentOutOfRangeException>(()=> {var a=IdentityArgs();a[10]=-1L;Finger(a);}));
        Test("fingerprint unicode byte-length matches independent golden",()=> {var a=IdentityArgs();a[0]="样板|武器\n一";Eq(Finger(a),"cdae1adb64c57b9d74272dcc83f9c824f1355656b8b973260c21f29262977990");});

        Test("receipt append returns new immutable journal",()=> {
            var empty=New("CraftReceiptJournal");var next=Invoke(empty,"PrepareAppend",Receipt());Eq(List(empty,"Records").Length,0);Eq(List(next,"Records").Length,1);
        });
        Test("receipt lookup absent is false with no error",()=> {var found=Find(New("CraftReceiptJournal"),Receipt());Eq(found[0],false);Eq(found[1],null);Eq(found[2],null);});
        Test("receipt lookup exact request and hash returns fact",()=> {
            var r=Receipt();var found=Find(Invoke(New("CraftReceiptJournal"),"PrepareAppend",r),r);Eq(found[0],true);Eq(found[2],null);Eq(Field(found[1],"recipeId"),"blade");
        });
        Test("receipt lookup changed hash reports conflict",()=> {
            var r=Receipt();var journal=Invoke(New("CraftReceiptJournal"),"PrepareAppend",r);Set(r,"fingerprint",new string('a',64));var found=Find(journal,r);Eq(found[0],false);Eq(found[1],null);Eq(found[2],"RequestConflict");
        });
        Test("receipt append snapshots nested caller arrays",()=> {
            var r=Receipt();var journal=Invoke(New("CraftReceiptJournal"),"PrepareAppend",r);Set(((Array)Field(r,"consumptions")).GetValue(0),"quantity",99);
            Eq(Field(((Array)Field(List(journal,"Records")[0],"consumptions")).GetValue(0),"quantity"),3);
        });
        Test("receipt returned records and lookup are deep copies",()=> {
            var r=Receipt();var journal=Invoke(New("CraftReceiptJournal"),"PrepareAppend",r);var view=List(journal,"Records")[0];Set(((Array)Field(view,"outputs")).GetValue(0),"quantity",88);
            var first=Find(journal,r);Set(first[1],"actorId","mutated");Eq(Field(Find(journal,r)[1],"actorId"),"actor-1");Eq(Field(((Array)Field(List(journal,"Records")[0],"outputs")).GetValue(0),"quantity"),1);
        });
        Test("receipt duplicate exact request never overwrites",()=> {var r=Receipt();var journal=Invoke(New("CraftReceiptJournal"),"PrepareAppend",r);Throws<ArgumentException>(()=>Invoke(journal,"PrepareAppend",r));Eq(List(journal,"Records").Length,1);});
        Test("receipt duplicate changed content never overwrites",()=> {var r=Receipt();var journal=Invoke(New("CraftReceiptJournal"),"PrepareAppend",r);Set(r,"actorId","actor-2");Sign(r);Throws<ArgumentException>(()=>Invoke(journal,"PrepareAppend",r));Eq(Field(List(journal,"Records")[0],"actorId"),"actor-1");});
        Test("receipt keeps newest 128 and evicts oldest",()=> {
            var journal=New("CraftReceiptJournal");for(int i=1;i<=129;i++)journal=Invoke(journal,"PrepareAppend",Receipt(i));Eq(List(journal,"Records").Length,128);Eq(Find(journal,Receipt(1))[0],false);Eq(Find(journal,Receipt(2))[0],true);Eq(Field(List(journal,"Records")[127],"requestId"),129.ToString("x32"));
        });
        Test("receipt rejects request not in Guid N format",()=> {var r=Receipt();Set(r,"requestId","not-a-guid");Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects whitespace padded Guid even if hash matches",()=> {var r=Receipt();Set(r,"requestId"," "+Field(r,"requestId")+" ");Sign(r);Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects session not in Guid N format",()=> {var r=Receipt();Set(r,"sessionId",Guid.NewGuid().ToString("D"));Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects malformed lowercase digest",()=> {var r=Receipt();Set(r,"fingerprint",new string('F',64));Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects duplicate source identity",()=> {var r=Receipt();var a=(Array)Field(r,"consumptions");Set(a.GetValue(1),"itemId","s1");Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects duplicate output identity",()=> {var r=Receipt();Set(r,"outputs",Of("CraftReceiptOutput",ReceiptPart("CraftReceiptOutput","same","blade",1),ReceiptPart("CraftReceiptOutput","same","blade",1)));Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects zero output quantity",()=> {var r=Receipt();Set(((Array)Field(r,"outputs")).GetValue(0),"quantity",0);Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects output budget above 99",()=> {var r=Receipt();Set(((Array)Field(r,"outputs")).GetValue(0),"quantity",100);Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt rejects negative revision",()=> {var r=Receipt();Set(r,"ledgerRevision",-1L);Throws<ArgumentException>(()=>Invoke(New("CraftReceiptJournal"),"PrepareAppend",r));});
        Test("receipt restore accepts valid records without global output uniqueness",()=> {var restored=Restore(Receipt(1),Receipt(2));Eq(List(restored,"Records").Length,2);Eq(Find(restored,Receipt(1))[0],true);});
        Test("receipt restore rejects over-capacity persisted history",()=>Throws<ArgumentException>(()=>Restore(Enumerable.Range(1,129).Select(i=>Receipt(i)).ToArray())));
        Test("receipt restore rejects duplicate persisted request",()=>Throws<ArgumentException>(()=>Restore(Receipt(),Receipt())));
        Test("receipt restore rejects tampered material cost even resigned",()=> {var r=Receipt();Set(((Array)Field(r,"consumptions")).GetValue(0),"quantity",2);Sign(r);Throws<ArgumentException>(()=>Restore(r));});
        Test("receipt restore rejects tampered output quantity",()=> {var r=Receipt();Set(((Array)Field(r,"outputs")).GetValue(0),"quantity",2);Throws<ArgumentException>(()=>Restore(r));});
        Test("receipt restore rejects tampered output definition",()=> {var r=Receipt();Set(((Array)Field(r,"outputs")).GetValue(0),"definitionId","other");Throws<ArgumentException>(()=>Restore(r));});
        Test("receipt restore rejects tampered fingerprint",()=> {var r=Receipt();Set(r,"fingerprint",new string('a',64));Throws<ArgumentException>(()=>Restore(r));});
        Test("receipt restore rejects changed recipe version even resigned",()=> {var r=Receipt();Set(r,"version",2);Sign(r);Throws<ArgumentException>(()=>Restore(r));});
        Test("receipt restore rejects quantity above recipe max even resigned",()=> {var r=Receipt();Set(r,"quantity",10);Sign(r);Throws<ArgumentException>(()=>Restore(r));});
        Test("receipt DTO JSON roundtrip preserves journal facts",()=> {
            var r=Receipt();var options=new JsonSerializerOptions{IncludeFields=true};var json=JsonSerializer.Serialize(r,T("CraftReceiptRecord"),options);
            var decoded=JsonSerializer.Deserialize(json,T("CraftReceiptRecord"),options);var restored=Restore(decoded);Eq(Field(Find(restored,r)[1],"fingerprint"),Field(r,"fingerprint"));
            Set(((Array)Field(decoded,"outputs")).GetValue(0),"quantity",7);Eq(Field(((Array)Field(List(restored,"Records")[0],"outputs")).GetValue(0),"quantity"),1);
        });
        Console.WriteLine("TOTAL "+passed+" passed, "+failed+" failed");
        return failed==0?0:1;
    }
}
