using System;
using Ossastra.CharacterCore;
class LifeTests {
 static int count; static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static void Main(){
 var s=new RespawnSchedule(2,10,.5);var alive=new[]{false,true};var available=new[]{true,true};
 s.Tick(alive,available,9,false);Check(s.ReadyIndices.Length==0,"no early respawn");
 s.Tick(alive,available,200,true);Check(s.Time==9,"paused does not advance");
 s.Tick(alive,available,1,false);Check(s.ReadyIndices.Length==1&&s.ReadyIndices[0]==0,"single due");
 s.Tick(alive,available,1,false);Check(s.ReadyIndices.Length==1,"placement failure remains pending");
 s.Complete(0);alive[0]=true;s.Tick(alive,available,0,false);Check(s.ReadyIndices.Length==0&&s.Generation(0)==1,"one completion generation");
 alive[0]=false;s.Tick(alive,available,2,false);alive[1]=false;s.Tick(alive,available,.49,false);Check(s.ReadyIndices.Length==0,"wipe supersedes old single");
 s.Tick(alive,available,.01,false);Check(s.IsWipe&&s.ReadyIndices.Length==2,"wipe due as batch");
 s.Complete(0);s.Complete(1);alive[0]=alive[1]=true;s.Tick(alive,available,0,false);Check(!s.IsWipe&&s.Generation(0)==2,"wipe clears after restored facts");
 alive[1]=false;available[1]=false;s.Tick(alive,available,50,false);Check(s.ReadyIndices.Length==0,"unavailable entity not restored");
 Check(s.Generation(1)==1,"no phantom generation");
 Console.WriteLine(count+" life contracts passed");
 }}
