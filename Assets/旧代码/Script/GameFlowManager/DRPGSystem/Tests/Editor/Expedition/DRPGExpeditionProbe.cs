#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class DRPGExpeditionProbe
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    [MenuItem("Tools/ANKALUTE/Expedition/Run Exploration Probes")]
    public static void Menu()=>Debug.Log(Run());
    public static string Run()
    {
        for(int seed=0;seed<50;seed++){
            var d=new DRPGDungeonState(seed);var seen=new System.Collections.Generic.HashSet<int>{0};
            for(int n=0;n<10;n++)foreach(var e in d.Corridors)if(seen.Contains(e.A)||seen.Contains(e.B)){seen.Add(e.A);seen.Add(e.B);}
            Check(seen.Count==10,"Disconnected seed "+seed);Check(d.Corridors.Count>=10,"Missing cycle");
            Check(d.Rooms.Count(r=>r.Discovered)<10,"Map leaked entire layout");
            foreach(var e in d.Corridors.Where(e=>e.A==0||e.B==0))Check(d.Rooms[e.A].Discovered&&d.Rooms[e.B].Discovered,"Hidden exit");
        }
        var map=new DRPGDungeonState(42,true);Check(map.Travel(1),"Travel failed");
        Check(!map.EnterDoor()||map.Room==0,"Origin door incorrect");map.Travel(1);
        var eventCell=map.Move(100);Check(eventCell.Kind==DRPGDungeonEvent.Rubble&&map.Distance==8,"Cross-frame obstacle missed");
        Check(map.Move(10)==eventCell&&map.Distance==8,"Obstacle bypassed");Check(!map.EnterDoor(),"Entered room mid corridor");
        map.Complete(eventCell);eventCell=map.Move(100);Check(eventCell.Kind==DRPGDungeonEvent.Treasure,"Next event missed");
        map.Complete(eventCell);eventCell=map.Move(100);map.Complete(eventCell);eventCell=map.Move(100);Check(eventCell.Kind==DRPGDungeonEvent.Battle,"Battle missed");map.Complete(eventCell);map.Move(100);
        Check(map.Edge>=0&&map.Room==0,"Movement auto entered room");Check(map.EnterDoor()&&map.Room==1&&map.Edge<0,"Door didn't enter");
        var a=ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();var b=ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();
        try{
            Set(a,"a",5);Set(b,"b",5);
            var fine=new DRPGDungeonState(42,true);fine.Travel(1);fine.Move(3.9995f);fine.Move(4.0001f);
            Check(fine.Corridors[0].Cells[0].Visited,"Slow movement skipped cell crossing");
            fine.Complete(fine.Move(8));var chest=fine.Move(12);chest.Loot.Add(a,1);fine.Complete(chest);fine.Move(13);
            Check(fine.Move(11)==chest&&!chest.Loot.IsEmpty,"Unclaimed loot lost on revisit");
            var bag=new DRPGPartyInventoryState(1,100);bag.TryAddLoot(a,5);var loot=new DRPGLootContainerState();loot.Add(b,2);
            Check(DRPGExpeditionItems.Collect(loot,bag,"b")==0,"Full bag accepted loot");Check(DRPGExpeditionItems.Collect(loot,bag,"b",0)==0,"Loot swapped occupied slot");
            Check(!DRPGExpeditionItems.Consume(bag,0,"b")&&bag.GetSlot(0).Item.Quantity==5,"Wrong provision consumed");
            Check(DRPGExpeditionItems.Consume(bag,0,"a")&&bag.GetSlot(0).Item.Quantity==4,"Consume not exactly one");
            Check(DRPGInventoryTransferService.Discard(bag,0,null).Success,"Discard failed");Check(DRPGExpeditionItems.Collect(loot,bag,"b")==2&&loot.IsEmpty,"Freed slot failed pickup");
        }finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        return "PASS: 50 seeded graph/fog checks; continuous crossings, obstacle, explicit door, battle cell, full bag/no swap, wrong/valid provision, discard and pickup";
    }
    static void Set(DRPGLootItemDefinitionSO item,string id,int max){var s=new SerializedObject(item);s.FindProperty("itemId").stringValue=id;s.FindProperty("maxStack").intValue=max;s.ApplyModifiedPropertiesWithoutUndo();}
}
#endif
