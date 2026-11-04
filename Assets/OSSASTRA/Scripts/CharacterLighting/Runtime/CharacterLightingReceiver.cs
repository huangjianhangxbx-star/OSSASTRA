/* 功能：登记单个角色的完整身体与当前装备轮廓成员；由角色根显式初始化和推进。
 * 边界：不驱动动画、装备、朝向或输入；不接管材质/Renderer显隐，不搜索全场对象。
 * 生命周期：身体由作者明确指定；装备仅读现有表现端口，脱手/禁用即时失效；启用时重建运行成员与登记，禁用和销毁精确撤销。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using PlayerCharacterRoot=Ossastra.Characters.CharacterRoot;
using UnityEngine;
using Ossastra.CharacterLighting.Rendering;
namespace Ossastra.CharacterLighting
{
 [DisallowMultipleComponent]
 public sealed class CharacterLightingReceiver:MonoBehaviour
 {
  [Tooltip("只登记身体实际Renderer；不包括深度代理、阴影代理、血条或技能特效。")]
  public Renderer[] bodyRenderers=Array.Empty<Renderer>();
  [Tooltip("明确的游戏相机；SceneView与其他相机不消费此登记。")]
  public Camera gameplayCamera;
  [Tooltip("仅保存作者设定；运行时副本与共享Profile隔离。") ] public CharacterLightingProfileSO profile;
  CharacterLightingSettings runtimeSettings;
  public CharacterLightingSettings Settings
  {
   get{if(runtimeSettings==null){runtimeSettings=profile!=null&&profile.settings!=null?profile.settings.Clone():new CharacterLightingSettings();runtimeSettings.Sanitize();}return runtimeSettings;}
   set{runtimeSettings=value==null?new CharacterLightingSettings():value.Clone();runtimeSettings.Sanitize();}
  }
  public string Status{get;private set;}="未初始化";
  public PlayerCharacterRoot Owner=>owner;
  public IReadOnlyList<Renderer> Members=>members;
  public CharacterCoverageActor CoverageActor=>actor;
  static readonly List<CharacterLightingReceiver> active=new List<CharacterLightingReceiver>(2);
  readonly List<Renderer> members=new List<Renderer>(8),explicitAccessories=new List<Renderer>(4);
  PlayerCharacterRoot owner;CharacterEquipmentSpineView spineEquipment;CharacterEquipmentView spriteEquipment;
  Renderer oldLeft,oldRight,oldSprite;CharacterCoverageActor actor;bool initialized,registered;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){foreach(var receiver in active)if(receiver!=null)receiver.registered=false;active.Clear();}
  public void Initialize(PlayerCharacterRoot root)
  {
   Suspend();owner=root;initialized=root!=null;
   // Initialize/Disable不重置已存在的运行时A/B与用户调参，Profile只在首次访问时复制。
   if(root==null){Status="缺少角色根";return;}
   spineEquipment=root.GetComponent<CharacterEquipmentSpineView>();spriteEquipment=root.GetComponent<CharacterEquipmentView>();
   if(gameplayCamera==null)Status="缺少明确游戏相机；不借Camera.main猜测";
   oldLeft=oldRight=oldSprite=null;RebuildMembers();TickPresentation();
  }
  public void TickPresentation()
  {
   if(!initialized||owner==null||!isActiveAndEnabled||!owner.isActiveAndEnabled||!owner.IsAlive||gameplayCamera==null){Suspend();return;}
   var left=spineEquipment==null||spineEquipment.LeftSkeleton==null?null:spineEquipment.LeftSkeleton.GetComponent<Renderer>();
   var right=spineEquipment==null||spineEquipment.RightSkeleton==null?null:spineEquipment.RightSkeleton.GetComponent<Renderer>();
   var sprite=spriteEquipment==null?null:spriteEquipment.PresentedRenderer;
   if(!ReferenceEquals(left,oldLeft)||!ReferenceEquals(right,oldRight)||!ReferenceEquals(sprite,oldSprite))
   {oldLeft=left;oldRight=right;oldSprite=sprite;RebuildMembers();}
   if(members.Count==0){Status="没有明确身体/装备轮廓成员";Suspend();return;}
   if(!registered){active.Add(this);registered=true;}
   Status="登记 "+members.Count+" 个身体/装备成员";
  }
  void RebuildMembers()
  {
   members.Clear();if(bodyRenderers!=null)foreach(var body in bodyRenderers)AddOwned(body);
   foreach(var accessory in explicitAccessories)AddOwned(accessory);
   AddOwned(oldLeft);AddOwned(oldRight);AddOwned(oldSprite);actor=new CharacterCoverageActor(this,members);
  }
  void AddOwned(Renderer renderer)
  {if(renderer!=null&&owner!=null&&renderer.transform.IsChildOf(owner.transform)&&!members.Contains(renderer))members.Add(renderer);}
  public void RegisterAccessory(Renderer renderer)
  {if(renderer==null||explicitAccessories.Contains(renderer))return;explicitAccessories.Add(renderer);RebuildMembers();}
  public void UnregisterAccessory(Renderer renderer)
  {if(explicitAccessories.Remove(renderer))RebuildMembers();}
  public void Suspend(){if(registered)active.Remove(this);registered=false;}
  public void DisposeReceiver(){Suspend();members.Clear();explicitAccessories.Clear();owner=null;actor=null;initialized=false;oldLeft=oldRight=oldSprite=null;}
  public static void Collect(Camera camera,List<CharacterCoverageActor> destination)
  {
   destination.Clear();foreach(var receiver in active)
    if(receiver!=null&&receiver.registered&&receiver.isActiveAndEnabled&&receiver.owner!=null&&receiver.owner.isActiveAndEnabled&&receiver.owner.IsAlive&&receiver.gameplayCamera==camera&&receiver.actor!=null)
     destination.Add(receiver.actor);
  }
  void OnEnable()
  {
   // 热重载可能保留initialized/装备引用/registered，却丢失运行列表、actor和静态登记；先撤销旧标记再重建。
   Suspend();
   if(initialized&&owner!=null){RebuildMembers();TickPresentation();}
  }
  void OnDisable(){Suspend();}
  void OnDestroy(){DisposeReceiver();}
 }
}
