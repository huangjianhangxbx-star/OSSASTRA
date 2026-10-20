using System;using Ossastra.Npcs;
class Program{static int Main(){int failed=0;void Check(string label,bool pass){Console.WriteLine((pass?"PASS ":"FAIL ")+label);if(!pass)failed++;}
var a=new NpcInteraction("npc-a",2);var b=new NpcInteraction("npc-b",2);int events=0;a.Replied+=r=>events++;
Check("near genuine reply",a.TryInteract("player",1,true).accepted&&a.Count==1&&events==1);
Check("independent NPC state",b.Count==0);
Check("repeat valid request once each",a.TryInteract("player",4,true).count==2&&events==2);
var before=a.Count;Check("outside refused",!a.TryInteract("player",4.01f,true).accepted&&a.Count==before);
Check("disabled refused",!a.TryInteract("player",0,false).accepted);
Check("invalid actor refused",!a.TryInteract("",0,true).accepted);
Check("NaN refused",!a.TryInteract("player",float.NaN,true).accepted);
Check("negative distance refused",!a.TryInteract("player",-1,true).accepted);
a.Dispose();Check("disposed refused",!a.TryInteract("player",0,true).accepted&&events==2);
Console.WriteLine("FAILED "+failed);return failed==0?0:1;}}
