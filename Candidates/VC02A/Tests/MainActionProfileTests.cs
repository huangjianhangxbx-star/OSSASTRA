using System;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
internal static class MainActionProfileTests
{
    private static int passed,failed;
    private static Type Find(string name)=>typeof(BasicAttackDefinition).Assembly.GetType("Ossastra.CharacterCore."+name)??throw new Exception("Missing frozen main-action contract: "+name);
    private static void Assert(bool condition,string label){if(!condition)throw new Exception(label);}
    private static void Test(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+(e.InnerException??e).Message);}}
    private static dynamic Stage(string id,int damage=15,bool replace=false)=>Activator.CreateInstance(Find("MainActionStage"),id,new BasicAttackDefinition(.1,.2,.2,.5,damage,10),1.8,114.6,0.0,.2,.1,replace,null);
    private static Array Stages(params object[] items){var array=Array.CreateInstance(Find("MainActionStage"),items.Length);for(int i=0;i<items.Length;i++)array.SetValue(items[i],i);return array;}
    private static dynamic Sequence(Array stages)=>Activator.CreateInstance(Find("MainActionSequence"),"hunter-I",stages,.25,.625,.25,1.0);
    private static dynamic Compose(dynamic sequence,EffectSnapshot effects)=>sequence.Compose(Stage("chisel",25),Stages(Stage("R1",18),Stage("R2",18),Stage("R3",18)),Stages(Stage("R+1",18),Stage("R+2",18),Stage("R+3",18),Stage("R+4",18)),effects);
    private static EffectSnapshot Effects(bool chisel=false,bool rapid=false,bool upgraded=false)
    {
        var set=new EffectSourceSet();if(chisel)set.Grant("held-chisel",new EffectSourceDefinition("chisel","blue","chisel",damage:upgraded?40:25));
        if(rapid)set.Grant("held-prefix",new EffectSourceDefinition("rapid","blue","combo-prefix",comboPrefix:upgraded?"rapid-plus":"rapid"));
        return set.Snapshot("blue",Array.Empty<string>(),3,false);
    }
    private static dynamic Base()=>Sequence(Stages(Stage("A1",15,true),Stage("A2",15,true),Stage("A3",20),Stage("A4",35)));
    private static int Main()
    {
        Test("Frozen stage array cannot be rewritten by author list",()=>{var items=Stages(Stage("A1"),Stage("A2"));dynamic s=Sequence(items);items.SetValue(Stage("changed",99),0);Assert(s.Stage(0).Id=="A1","input alias");});
        Test("No active rune leaves all four stages and resource metadata intact",()=>{dynamic s=Compose(Base(),Effects());Assert(s.Count==4&&s.Stage(2).Timeline.Damage==20,"unchanged base");Assert(s.BufferSeconds==.25&&s.ContinuationSeconds==.625&&s.FullComboRecovery==.25,"chain metadata");});
        Test("Chisel replaces stage three and carries delayed source identity",()=>{dynamic s=Compose(Base(),Effects(chisel:true));Assert(s.Count==4&&s.Stage(2).Id=="chisel"&&s.Stage(2).Timeline.Damage==25&&s.Stage(2).SourceId=="held-chisel","replacement/source");});
        Test("Upgraded chisel changes frozen damage without changing template",()=>{dynamic s=Compose(Base(),Effects(chisel:true,upgraded:true));Assert(s.Stage(2).Timeline.Damage==40&&Stage("chisel",25).Timeline.Damage==25,"40 versus immutable25");});
        Test("Rapid prefix replaces two stages with three",()=>{dynamic s=Compose(Base(),Effects(rapid:true));Assert(s.Count==5&&s.Stage(0).Id=="R1"&&s.Stage(3).Id=="A3"&&s.Stage(4).Id=="A4","prefix topology");});
        Test("Rapid plus and chisel compose in original order",()=>{dynamic s=Compose(Base(),Effects(true,true,true));Assert(s.Count==6&&s.Stage(3).Id=="R+4"&&s.Stage(4).Id=="chisel"&&s.Stage(4).Timeline.Damage==40&&s.Stage(5).Id=="A4","both effects survive");});
        Test("Accepted sequence survives subsequent registry withdrawal",()=>{var set=new EffectSourceSet();set.Grant("chisel",new EffectSourceDefinition("chisel","blue","chisel",damage:25));dynamic s=Compose(Base(),set.Snapshot("blue",Array.Empty<string>(),3,false));set.Remove("chisel");Assert(s.Stage(2).Timeline.Damage==25,"accepted snapshot stays");});
        Test("Invalid dash bounds rejected",()=>{bool rejected=false;try{Activator.CreateInstance(Find("MainActionStage"),"bad",new BasicAttackDefinition(.1,.2,.2,.5,15),1.8,114.6,.4,.3,.2,false,null);}catch(System.Reflection.TargetInvocationException e)when(e.InnerException is ArgumentException){rejected=true;}Assert(rejected,"dash crosses finish");});
        Test("Low frame crossing entire dash preserves authored distance",()=>{dynamic s=Stage("dash");Assert(Math.Abs((double)s.TravelBetween(0.0,.4)-.2)<1e-9,"lost short dash");});
        Test("Partitioned dash movement equals one large frame",()=>{dynamic s=Stage("dash");double sum=s.TravelBetween(0.0,.035)+s.TravelBetween(.035,.08)+s.TravelBetween(.08,.3);Assert(Math.Abs(sum-.2)<1e-9,"frame dependent distance");Assert(s.TravelBetween(.3,.4)==0,"tail keeps moving");});
        Console.WriteLine($"RESULT passed={passed} failed={failed}");return failed==0?0:1;
    }
}
