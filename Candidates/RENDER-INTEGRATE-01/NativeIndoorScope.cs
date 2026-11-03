/* 功能：用原Teleport入口短暂进入房间，检查新Look随原房间权威切换；超时恢复所有角色与镜头。 */
using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Ossastra.WorldRendering;using Funplay.Editor.Tools.Scripting;
public class NativeIndoorScope:IFunplayCommand{
static Ossastra.SceneVisibility.SceneVisibilityRoot root;static Ossastra.Characters.CharacterRoot[] actors;static Vector3[] positions,directions,attacks;static object lease=new object();static double deadline;static bool active;
static Ossastra.MapResearch.MapResearchRoot map;static bool follow;static float smooth;static Vector3 fixedTarget,camPosition;static Quaternion camRotation;
public static object Restore(){
if(!active)return new{restored=true,already=true};active=false;EditorApplication.update-=Watch;
foreach(var a in actors){int i=Array.IndexOf(actors,a);if(a==null)continue;a.GetComponent<Ossastra.Characters.CharacterMovement>().Teleport(positions[i]);var o=a.GetComponent<Ossastra.Characters.CharacterOrientation>();o.OrientTowards(positions[i]+directions[i]*5);o.FaceAttackDirection();o.OrientTowards(positions[i]+attacks[i]*5);a.SetViewInputCapture(lease,false);}
map.followParticipant=follow;map.followSmoothTime=smooth;map.fixedTarget=fixedTarget;map.SetUiCameraCapture(lease,false);root.gameplayCamera.transform.SetPositionAndRotation(camPosition,camRotation);root.roomModule.RequestImmediateMembershipRefresh();return new{restored=true,actors=actors.Length};}
static void Watch(){if(!Application.isPlaying||EditorApplication.timeSinceStartup>deadline)Restore();}
public void Execute(ExecutionContext c){
if(!Application.isPlaying||active)throw new Exception("Play/lease guard");
var w=UnityEngine.Object.FindObjectOfType<WorldLookController>();root=w.visibilityRoot;actors=root.coordinator.Characters.ToArray();positions=actors.Select(a=>a.transform.position).ToArray();directions=actors.Select(a=>a.LogicalDirection).ToArray();attacks=actors.Select(a=>a.GetComponent<Ossastra.Characters.CharacterOrientation>().AttackDirection).ToArray();
map=UnityEngine.Object.FindObjectOfType<Ossastra.MapResearch.MapResearchRoot>();follow=map.followParticipant;smooth=map.followSmoothTime;fixedTarget=map.fixedTarget;camPosition=root.gameplayCamera.transform.position;camRotation=root.gameplayCamera.transform.rotation;
active=true;deadline=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Watch;
try{foreach(var a in actors)a.SetViewInputCapture(lease,true);map.SetUiCameraCapture(lease,true);map.followParticipant=false;map.followSmoothTime=0;map.fixedTarget=new Vector3(-16,0,-12);
var actor=root.coordinator.Current;actor.GetComponent<Ossastra.Characters.CharacterMovement>().Teleport(new Vector3(-16,.1f,-12));actor.GetComponent<Ossastra.Characters.CharacterOrientation>().OrientTowards(new Vector3(-16,.1f,-7));root.roomModule.RequestImmediateMembershipRefresh();c.ReturnValue=new{entered=true,expiresSeconds=60};}catch{Restore();throw;}
}}
