/* 功能：登记允许短暂黑幕过渡的静态环境，并生成观察历史的明确重置代号。
 * 边界：白名单不授予人物视野；动态角色、可破坏物、动画与活动遮板不进入历史。
 * 生命周期：初始化重建登记；Current身份、传送、失效或门窗阻挡拓扑变化清理历史，普通走门保留。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.SceneVisibility.Rooms.Rendering;

namespace Ossastra.SceneVisibility.Rooms
{
 public sealed partial class RoomVisibilityModule
 {
  [Header("自然观察与窗口外景；不授予邻室或交互权限")]
  [Tooltip("开启时玩家前方180°自然视野无旧6/8米硬截断；关闭只用于旧有限策略对照。")]
  public bool naturalPlayerVision=true;
  [Tooltip("从当前房间真实外窗显示相机可见景物，不增加人物标签、锁定或邻室许可。")]
  public bool realWindowScenery=true;
  [Header("黑幕过渡；仅静态环境保留短暂退场")]
  public RoomFogSettings fog=new RoomFogSettings();
  [Tooltip("明确登记固定地表、墙体、地板和柜体；不能登记角色、敌人、掉落、可破坏物或活动门窗。")]
  public Renderer[] staticEnvironmentSurfaces=Array.Empty<Renderer>();
  [Tooltip("明确的固定地表作者根；只在初始化登记其生成的显示叶，不逐帧扫描场景。禁止包含角色或业务实体。")]
  public Transform[] staticEnvironmentRoots=Array.Empty<Transform>();
  [Tooltip("明确登记会移动/开合的实体遮板；这些对象不能进入静态历史，原碰撞不变。")]
  public BoxCollider[] movingSightBlockers=Array.Empty<BoxCollider>();
  public int WindowViewCount{get;private set;}
  public int StaticEnvironmentCount=>staticEnvironmentRegistration.Count;
  public int PresentationEpoch=>presentationEpoch;
  readonly Dictionary<Renderer,Matrix4x4> staticEnvironmentRegistration=new Dictionary<Renderer,Matrix4x4>();
  readonly List<RoomSightDomain> cameraSightDomains=new List<RoomSightDomain>(8);
  readonly List<RoomMaskVolume> cameraBlockerBoxes=new List<RoomMaskVolume>(64);
  readonly HashSet<Collider> cameraHiddenColliders=new HashSet<Collider>();
  int presentationEpoch,previousActorId,previousLife,previousTopology;
  bool previousValid,presentationSampled;
  Vector3 previousPresentationFoot;

  void InitializePresentationRegistration()
  {
   staticEnvironmentRegistration.Clear();
   foreach(var r in staticEnvironmentSurfaces)
   {
    if(r==null||r.gameObject.scene!=gameObject.scene||staticEnvironmentRegistration.ContainsKey(r))continue;
    if(!CanBeStaticEnvironment(r))throw new InvalidOperationException("动态或业务对象不能登记静态历史："+r.name);
    staticEnvironmentRegistration.Add(r,r.localToWorldMatrix);
   }
   foreach(var root in staticEnvironmentRoots)if(root!=null&&root.gameObject.scene==gameObject.scene)
    foreach(var r in root.GetComponentsInChildren<Renderer>(true))
    {
     if(r==null||IsOwnedVisualCoverage(r)||staticEnvironmentRegistration.ContainsKey(r))continue;
     if(!CanBeStaticEnvironment(r))throw new InvalidOperationException("静态作者根混入动态/业务对象："+r.name);
     staticEnvironmentRegistration.Add(r,r.localToWorldMatrix);
    }
   presentationSampled=false;++presentationEpoch;
  }
  bool CanBeStaticEnvironment(Renderer r)
  {
   if(r==null||r is ParticleSystemRenderer||movingAnchors.ContainsKey(r))return false;
   if(r.GetComponentInParent<Animator>()!=null||r.GetComponentInParent<Animation>()!=null||
      r.GetComponentInParent<Spine.Unity.SkeletonRenderer>()!=null||
      r.GetComponentInParent<Ossastra.WorldProps.WorldPropRoot>()!=null)return false;
   foreach(var box in movingSightBlockers)if(box!=null&&(box.transform==r.transform||r.transform.IsChildOf(box.transform)))return false;
   return true;
  }
  bool IsStaticEnvironment(Renderer r)
  {
   if(!staticEnvironmentRegistration.TryGetValue(r,out var original)||presentationMembers.Contains(r)||movingAnchors.ContainsKey(r))return false;
   // 作者误移动后即时降级为当帧内容；不因白名单在旧位置留下残像。
   var now=r.localToWorldMatrix;
   for(int i=0;i<16;i++)if(Mathf.Abs(original[i]-now[i])>.0001f)return false;
   return true;
  }
  void UpdatePresentationEpoch()
  {
   var actor=owner.coordinator.Current;
   int id=actor==null?0:actor.GetInstanceID(),life=actor==null?0:actor.LifeGeneration;
   Vector3 foot=actor==null?Vector3.zero:actor.transform.position;
   int hash=17;
   unchecked
   {
    foreach(var p in sightPortals){hash=hash*31+(p.IsSightOpen?1:0);hash=hash*31+p.Id.GetHashCode();foreach(var v in p.Aperture){hash=hash*31+v.X.GetHashCode();hash=hash*31+v.Y.GetHashCode();hash=hash*31+v.Z.GetHashCode();}}
    foreach(var box in sightBlockers)
    {
     bool active=box!=null&&box.enabled&&box.gameObject.activeInHierarchy&&!box.isTrigger;
     hash=hash*31+(active?1:0);if(!active)continue;
     var matrix=box.transform.localToWorldMatrix;for(int i=0;i<16;i++)hash=hash*31+matrix[i].GetHashCode();
     hash=hash*31+box.center.GetHashCode();hash=hash*31+box.size.GetHashCode();
    }
    hash=hash*31+(naturalPlayerVision?1:0);hash=hash*31+(realWindowScenery?1:0);
   }
   if(!presentationSampled||id!=previousActorId||life!=previousLife||previousValid!=validObserver||hash!=previousTopology||
      (foot-previousPresentationFoot).sqrMagnitude>immediateResolveDistance*immediateResolveDistance)++presentationEpoch;
   previousActorId=id;previousLife=life;previousTopology=hash;previousValid=validObserver;
   previousPresentationFoot=foot;presentationSampled=true;
   frame.VisibilityEpoch=presentationEpoch;frame.PresentationTime=Time.unscaledTimeAsDouble;frame.Fog=fog;
  }
  static RoomMaskVolume BoxVolume(BoxCollider box)=>new RoomMaskVolume{WorldToLocal=Matrix4x4.Translate(-box.center)*box.transform.worldToLocalMatrix,HalfExtents=box.size*.5f};
  void BuildCameraWindowFrame()
  {
   cameraSightDomains.Clear();cameraBlockerBoxes.Clear();cameraHiddenColliders.Clear();
   frame.CameraObserverPosition=boundCamera.transform.position;
   if(validObserver&&realWindowScenery&&ActiveRoom!=null)
   {
    var result=Ossastra.SceneVisibility.Rooms.Core.PortalSightSolver.SolveWindowScene(ActiveRoom.roomId,S(frame.CameraObserverPosition),sightPortals);
    foreach(var volume in result.Volumes)
    {
     var planes=new Vector4[volume.ClipPlanes.Count];for(int i=0;i<planes.Length;i++){var p=volume.ClipPlanes[i];planes[i]=new Vector4(p.Normal.X,p.Normal.Y,p.Normal.Z,p.Distance);}
     cameraSightDomains.Add(new RoomSightDomain{Planes=planes,ObserverPosition=frame.CameraObserverPosition,IsCameraWindow=true,TargetExterior=true});
    }
   }
   foreach(var r in hiddenFrontMembers)if(r!=null){var c=r.GetComponent<Collider>();if(c!=null)cameraHiddenColliders.Add(c);}
   if(ActiveBuilding!=null&&revealRoofs)foreach(var roof in ActiveBuilding.roofGroups)if(roof!=null)foreach(var c in roof.labelRaycastExclusions)if(c!=null)cameraHiddenColliders.Add(c);
   foreach(var box in sightBlockers)if(box!=null&&box.enabled&&box.gameObject.activeInHierarchy&&!box.isTrigger&&!cameraHiddenColliders.Contains(box))cameraBlockerBoxes.Add(BoxVolume(box));
   frame.WindowSightDomains=cameraSightDomains.ToArray();frame.CameraOpaqueBlockers=cameraBlockerBoxes.ToArray();WindowViewCount=cameraSightDomains.Count;
  }
 }
}
