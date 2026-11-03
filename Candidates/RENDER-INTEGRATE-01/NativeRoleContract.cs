// 功能：验证环境渲染角色不会将角色、敌人与信息表现误当环境；测试对象均精确销毁。
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Funplay.Editor.Tools.Scripting;
public class NativeRoleContract : IFunplayCommand
{
 public void Execute(ExecutionContext ctx)
 {
  var field=typeof(Ossastra.SceneVisibility.Rooms.Rendering.RoomPositiveSurface).GetField("WorldLookEnvironment");
  var type=typeof(Ossastra.SceneVisibility.Rooms.RoomVisibilityModule).Assembly.GetType("Ossastra.SceneVisibility.Rooms.RoomWorldLookRole");
  if(field==null||type==null){ctx.ReturnValue=new{pass=false,reason="环境与动态/历史角色尚未独立定义",field=field!=null,policy=type!=null};return;}
  var method=type.GetMethod("IsEnvironment",BindingFlags.Static|BindingFlags.Public);
  if(method==null)throw new Exception("Contract API missing");
  var owned=new List<GameObject>();var results=new List<object>();
  try
  {
   var world=new GameObject("RoleContract_world");owned.Add(world);var mesh=world.AddComponent<MeshRenderer>();
   var sprite=new GameObject("RoleContract_world_sprite");owned.Add(sprite);var sr=sprite.AddComponent<SpriteRenderer>();
   var actor=new GameObject("RoleContract_actor");owned.Add(actor);actor.SetActive(false);actor.AddComponent<Ossastra.Characters.CharacterRoot>();
   var child=new GameObject("RoleContract_equipment");child.transform.SetParent(actor.transform);var equipment=child.AddComponent<SpriteRenderer>();
   var enemy=new GameObject("RoleContract_enemy");owned.Add(enemy);enemy.SetActive(false);enemy.AddComponent<Ossastra.Enemies.EnemyRoot>();var er=enemy.AddComponent<MeshRenderer>();
   var npc=new GameObject("RoleContract_npc");owned.Add(npc);npc.SetActive(false);npc.AddComponent<Ossastra.Npcs.NpcRoot>();var nr=npc.AddComponent<SpriteRenderer>();
   bool a=(bool)method.Invoke(null,new object[]{mesh,false});bool b=(bool)method.Invoke(null,new object[]{sr,false});
   bool d=(bool)method.Invoke(null,new object[]{equipment,false});bool e=(bool)method.Invoke(null,new object[]{er,false});
   bool f=(bool)method.Invoke(null,new object[]{mesh,true});bool n=(bool)method.Invoke(null,new object[]{null,false});
   bool npcRole=(bool)method.Invoke(null,new object[]{nr,false});
   ctx.ReturnValue=new{pass=a&&b&&!d&&!e&&!f&&!n&&!npcRole,worldMesh=a,worldSprite=b,characterEquipment=d,enemy=e,npc=npcRole,registeredInformation=f,invalid=n};
  }
  finally{foreach(var go in owned)if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
 }
}
