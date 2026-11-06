/* 功能：登记单个角色的完整身体与当前装备轮廓成员；由角色根显式初始化和推进。
 * 边界：不驱动动画、装备、朝向或输入；不接管材质/Renderer显隐，不搜索全场对象。
 * 生命周期：身体由作者明确指定；装备仅读现有表现端口，脱手/禁用即时失效；启用时重建运行成员与登记，禁用和销毁精确撤销。
 */
using System;
using System.Collections.Generic;
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
  public Component Owner=>source==null?null:source.Owner;
  public string EntityId=>source==null?"":source.EntityId;
  public bool SourceAvailable=>source!=null&&Owner!=null&&source.IsAvailable;
  public IReadOnlyList<Renderer> Members=>members;
  public CharacterCoverageActor CoverageActor=>actor;
  static readonly List<CharacterLightingReceiver> active=new List<CharacterLightingReceiver>(2);
  readonly List<Renderer> members=new List<Renderer>(8),explicitAccessories=new List<Renderer>(4);
  ICharacterLightingSource source; readonly List<Renderer> equipment=new List<Renderer>(3),scratch=new List<Renderer>(3);
  CharacterCoverageActor actor;bool initialized,registered;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){foreach(var receiver in active)if(receiver!=null)receiver.registered=false;active.Clear();}
  public void Initialize(ICharacterLightingSource root)
  {
   Suspend();source=root;initialized=root!=null&&root.Owner!=null;
   // Initialize/Disable不重置已存在的运行时A/B与用户调参，Profile只在首次访问时复制。
   if(!initialized){Status="缺少实体表现来源";return;}
   equipment.Clear();
   if(gameplayCamera==null)Status="缺少明确游戏相机；不借Camera.main猜测";
   RebuildMembers();TickPresentation();
  }
  public void TickPresentation()
  {
   if(!initialized||!SourceAvailable||!isActiveAndEnabled||gameplayCamera==null){Suspend();return;}
   scratch.Clear();source.AppendEquipmentRenderers(scratch);
   bool changed=scratch.Count!=equipment.Count;for(int i=0;!changed&&i<scratch.Count;i++)changed=!ReferenceEquals(scratch[i],equipment[i]);
   if(changed){equipment.Clear();equipment.AddRange(scratch);RebuildMembers();}
   if(members.Count==0){Status="没有明确身体/装备轮廓成员";Suspend();return;}
   if(!registered){active.Add(this);registered=true;}
   Status="登记 "+members.Count+" 个身体/装备成员";
  }
  void RebuildMembers()
  {
   members.Clear();if(bodyRenderers!=null)foreach(var body in bodyRenderers)AddOwned(body);
   foreach(var accessory in explicitAccessories)AddOwned(accessory);
   foreach(var r in equipment)AddOwned(r);actor=new CharacterCoverageActor(this,members);
  }
  void AddOwned(Renderer renderer)
  {if(renderer!=null&&Owner!=null&&renderer.transform.IsChildOf(Owner.transform)&&!members.Contains(renderer))members.Add(renderer);}
  public void RegisterAccessory(Renderer renderer)
  {if(renderer==null||explicitAccessories.Contains(renderer))return;explicitAccessories.Add(renderer);RebuildMembers();}
  public void UnregisterAccessory(Renderer renderer)
  {if(explicitAccessories.Remove(renderer))RebuildMembers();}
  public void Suspend(){if(registered)active.Remove(this);registered=false;}
  public void DisposeReceiver(){Suspend();members.Clear();explicitAccessories.Clear();source=null;actor=null;initialized=false;equipment.Clear();scratch.Clear();}
  public static void Collect(Camera camera,List<CharacterCoverageActor> destination)
  {
   destination.Clear();foreach(var receiver in active)
    if(receiver!=null&&receiver.registered&&receiver.isActiveAndEnabled&&receiver.SourceAvailable&&receiver.gameplayCamera==camera&&receiver.actor!=null)
     destination.Add(receiver.actor);
  }
  void OnEnable()
  {
   // 热重载可能保留initialized/装备引用/registered，却丢失运行列表、actor和静态登记；先撤销旧标记再重建。
   Suspend();
   if(initialized&&SourceAvailable){RebuildMembers();TickPresentation();}
  }
  void OnDisable(){Suspend();}
  void OnDestroy(){DisposeReceiver();}
 }
}
