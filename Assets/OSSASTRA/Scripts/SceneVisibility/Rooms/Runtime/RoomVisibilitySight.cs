/* 功能：从原Current稳定观察点求解种子房间及双向单跳门窗视域，生成正向绘制帧。
 * 边界：镜头窗景只给洞口像素不授予人物视野；不递归Partial、不推进发现/经济/物理业务。
 * 协作：Core负责几何；Rendering只画显式登记的合法片元；原Root持有唯一相机生命周期。
 * 观测：BuildPositiveFrame仅增加五个同步CPU子范围，保留原顺序/算法；静态/动态表面范围含既有Dedup/Tag，不能重复求和。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ossastra.SceneVisibility.Rooms.Core;
using Ossastra.SceneVisibility.Rooms.Rendering;
using Ossastra.EnvironmentSimulation;
namespace Ossastra.SceneVisibility.Rooms
{
 public sealed partial class RoomVisibilityModule
 {
  // 只记录原有同步阶段，未开启采样时不引入渲染命令；CPU范围可能含原生调用。
  internal static readonly ProfilingSampler perfBuildPositiveFrame=new ProfilingSampler("RoomVisibility.BuildPositiveFrame");
  internal static readonly ProfilingSampler perfPositiveDedup=new ProfilingSampler("RoomVisibility.AddPositiveSurface.Dedup");
  internal static readonly ProfilingSampler perfEnvironmentTag=new ProfilingSampler("RoomVisibility.ApplyEnvironmentTag");
  internal static readonly ProfilingSampler perfPositivePortalCore=new ProfilingSampler("RoomVisibility.BuildPositiveFrame.PortalCore");
  internal static readonly ProfilingSampler perfPositiveCameraWindow=new ProfilingSampler("RoomVisibility.BuildPositiveFrame.CameraWindow");
  internal static readonly ProfilingSampler perfPositiveDynamicSurfaces=new ProfilingSampler("RoomVisibility.BuildPositiveFrame.DynamicSurfaces");
  internal static readonly ProfilingSampler perfPositiveStaticSurfaces=new ProfilingSampler("RoomVisibility.BuildPositiveFrame.StaticSurfaces");
  internal static readonly ProfilingSampler perfPositiveFinalizeMasks=new ProfilingSampler("RoomVisibility.BuildPositiveFrame.FinalizeMasks");
  [Header("当帧观察权威；相机旋转不改变人物观察权威")]
  public bool positiveRenderingEnabled=true;
  public bool truthMode=true;
  public bool localObjectRevealEnabled=false;
  [Min(.1f)] public float observerHeight=1.4f;
  [Tooltip("明确登记的不透光实体Box；透明玻璃不得登记。动态窗板保留原Transform。")]
  public BoxCollider[] sightBlockers=Array.Empty<BoxCollider>();
  public Vector3 ObserverPosition{get;private set;}
  public Vector3 ObserverForward{get;private set;}
  public int PartialViewCount=>sightResult==null?0:sightResult.Volumes.Count;
  public int HiddenFrontWallCount{get;private set;}
  public string SightStatus{get;private set;}="等待观察快照";
  public IReadOnlyList<PortalSightVolume> ObserverSightVolumes=>sightResult==null?Array.Empty<PortalSightVolume>():sightResult.Volumes;
  readonly RoomPositiveScope positiveScope=new RoomPositiveScope();
  readonly List<PortalSightSnapshot> sightPortals=new List<PortalSightSnapshot>(8);
  readonly List<RoomSightDomain> sightDomains=new List<RoomSightDomain>(8);
  readonly List<RoomPositiveSurface> positiveSurfaces=new List<RoomPositiveSurface>(512);
  readonly HashSet<Renderer> positiveSurfaceMembers=new HashSet<Renderer>();
  // 仅本次Build的当前sharedSurfaces成员；保留旧Unity Equals及特殊null谓词语义。
  readonly HashSet<Renderer> sharedSurfaceMembers=new HashSet<Renderer>();
  Renderer[] sharedSurfaceMembershipSnapshot;
  // 每次真实Build内的源快照；不跨相机/构建保留语义或材质结论。
  readonly List<RoomPositiveSourceFacts> positiveSourceFacts=new List<RoomPositiveSourceFacts>(512);
  readonly List<RoomMaskVolume> allBuildingBoxes=new List<RoomMaskVolume>(32);
  readonly List<RoomMaskVolume> blockerBoxes=new List<RoomMaskVolume>(64);
  readonly List<PortalSightBlocker> coreBlockers=new List<PortalSightBlocker>(64);
  readonly List<RoomPresentationRegistry.Entry> dynamicPresentations=new List<RoomPresentationRegistry.Entry>(64);
  readonly HashSet<Renderer> hiddenFrontMembers=new HashSet<Renderer>();
  readonly HashSet<Renderer> presentationMembers=new HashSet<Renderer>();
  readonly List<Renderer> rejectedPresentationMembers=new List<Renderer>(32);
  Renderer[] sceneSurfaces=Array.Empty<Renderer>();
  readonly Dictionary<Renderer,Transform> movingAnchors=new Dictionary<Renderer,Transform>();
  PortalSightResult sightResult;
  bool validObserver;
  bool environmentSurfaceRouting;
  static SightVector3 S(Vector3 p)=>new SightVector3(p.x,p.y,p.z);
  static Vector3 U(SightVector3 p)=>new Vector3(p.X,p.Y,p.Z);
  static bool IsOwnedVisualCoverage(Renderer renderer)
  {
   if(renderer==null)return false;
   var coverageOwner=renderer.GetComponentInParent<Ossastra.Characters.Rendering.CharacterUprightDepth>();
   return coverageOwner!=null&&coverageOwner.OwnsRenderer(renderer);
  }
  void InitializeSightRegistration()
  {
   var list=new List<Renderer>();
   var terrains=new List<Ossastra.VoxelTerrain.VoxelTerrainInstallation>();
   foreach(var root in gameObject.scene.GetRootGameObjects())
    terrains.AddRange(root.GetComponentsInChildren<Ossastra.VoxelTerrain.VoxelTerrainInstallation>(true));
   foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
    if(!IsOwnedVisualCoverage(renderer))list.Add(renderer);
   sceneSurfaces=list.ToArray();
   staticTerrainPreparation.BindOwned(terrains.ToArray(),gameObject.scene,this);
   movingAnchors.Clear();foreach(var r in sceneSurfaces)if(r!=null){var actor=r.GetComponentInParent<Ossastra.Characters.CharacterRoot>();var enemy=r.GetComponentInParent<Ossastra.Enemies.EnemyRoot>();if(actor!=null)movingAnchors[r]=actor.transform;else if(enemy!=null)movingAnchors[r]=enemy.transform;}
   foreach(var r in sceneSurfaces)if(r!=null&&(r.renderingLayerMask&RoomPositiveRenderingLayers.ReservedMask)!=0)
    throw new InvalidOperationException("可见性保留渲染位已被Renderer占用："+r.name);
   foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var light in root.GetComponentsInChildren<Light>(true))
    if((light.renderingLayerMask&RoomPositiveRenderingLayers.ReservedMask)!=0)throw new InvalidOperationException("可见性保留渲染位已被Light占用："+light.name);
  }
  void BuildPositiveFrame()
  {
   try
   {
   using(new ProfilingScope(null,perfBuildPositiveFrame))
   {
   using(new ProfilingScope(null,perfPositivePortalCore))
   {
   CaptureSharedSurfaceMembership();positiveSurfaces.Clear();positiveSurfaceMembers.Clear();positiveSourceFacts.Clear();sightPortals.Clear();sightDomains.Clear();allBuildingBoxes.Clear();blockerBoxes.Clear();coreBlockers.Clear();hiddenFrontMembers.Clear();labelRaycastExclusionScratch.Clear();presentationMembers.Clear();rejectedPresentationMembers.Clear();
   using(new ProfilingScope(null,RoomStaticTerrainPreparation.perfCaptureBuild))
    staticTerrainPreparation.CaptureBuild(boundCamera);
   var current=owner.coordinator.Current;validObserver=current!=null&&current.IsAlive&&current.isActiveAndEnabled;
   environmentSurfaceRouting=EnvironmentCameraRegistry.TryGet(boundCamera,out var environmentFrame)&&environmentFrame.Enabled;
   ObserverPosition=validObserver?current.transform.position+Vector3.up*observerHeight:Vector3.zero;
   ObserverForward=validObserver?current.LogicalDirection:Vector3.zero;
   foreach(var b in registeredBuildings)if(b!=null&&b.isActiveAndEnabled)
   {
    RoomRegion.AppendBoxes(b.transform,b.occupancyVolumes,allBuildingBoxes);
    foreach(var p in b.portals)if(p!=null&&p.isActiveAndEnabled)
    {
     var polygon=p.GetWorldAperture();var vertices=new SightVector3[polygon.Length];for(int i=0;i<vertices.Length;i++)vertices[i]=S(polygon[i]);
     sightPortals.Add(new PortalSightSnapshot(p.portalId,p.inside==null?ObserverSnapshot.Exterior:p.inside.roomId,p.outside==null?ObserverSnapshot.Exterior:p.outside.roomId,vertices,S(p.OutwardNormal),p.open&&p.canSeeThrough,p.observerRange,p.maxSightDistance));
    }
   }
   if(allBuildingBoxes.Count>32||sightBlockers.Length>64)throw new InvalidOperationException("观察范围超过32建筑盒/64不透光盒预算；拒绝截断放行");
   var snapshot=new ObserverSnapshot(ActiveRoom==null?ObserverSnapshot.Exterior:ActiveRoom.roomId,S(ObserverPosition),S(ObserverForward));
   sightResult=validObserver?PortalSightSolver.Solve(snapshot,sightPortals,naturalPlayerVision?PortalSightPolicy.PlayerNatural:PortalSightPolicy.Legacy):null;
   if(sightResult!=null)foreach(var volume in sightResult.Volumes)
   {
    var planes=new Vector4[volume.ClipPlanes.Count];for(int i=0;i<planes.Length;i++){var p=volume.ClipPlanes[i];planes[i]=new Vector4(p.Normal.X,p.Normal.Y,p.Normal.Z,p.Distance);}
    var targets=new List<RoomMaskVolume>();foreach(var r in registeredRooms)if(r!=null&&r.roomId==volume.TargetSpaceId)r.AppendMaskVolumes(targets);
    sightDomains.Add(new RoomSightDomain{Planes=planes,ObserverPosition=ObserverPosition,TargetVolumes=targets.ToArray(),TargetExterior=volume.TargetSpaceId==ObserverSnapshot.Exterior});
   }
   foreach(var box in sightBlockers)
   {
    if(box==null||!box.enabled||!box.gameObject.activeInHierarchy||box.isTrigger)continue;
    blockerBoxes.Add(new RoomMaskVolume{WorldToLocal=Matrix4x4.Translate(-box.center)*box.transform.worldToLocalMatrix,HalfExtents=box.size*.5f});
    var scale=box.transform.lossyScale;var half=Vector3.Scale(box.size*.5f,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
    coreBlockers.Add(new PortalSightBlocker(S(box.transform.TransformPoint(box.center)),S(box.transform.right),S(box.transform.up),S(box.transform.forward),S(half)));
   }
   HiddenFrontWallCount=0;
   if(ActiveBuilding!=null&&ActiveRoom!=null)
   {
    foreach(var wall in ActiveBuilding.frontWalls)if(wall!=null&&wall.isActiveAndEnabled&&wall.primaryRoom!=null)
    {
     var w=new FrontWallSnapshot(wall.wallId,wall.primaryRoom.roomId,wall.secondaryRoom==null?null:wall.secondaryRoom.roomId,S(wall.transform.position),S(wall.OutwardNormal),wall.allowHide);
     if(!FrontWallSolver.ShouldHide(w,ActiveRoom.roomId,S(boundCamera.transform.forward)))continue;
     ++HiddenFrontWallCount;foreach(var r in wall.members)if(r!=null){hiddenFrontMembers.Add(r);var collider=r.GetComponent<Collider>();if(collider!=null)labelRaycastExclusionScratch.Add(collider);}
    }
    if(revealRoofs)foreach(var roof in ActiveBuilding.roofGroups)foreach(var c in roof.labelRaycastExclusions)if(c!=null)labelRaycastExclusionScratch.Add(c);
   }
   }
   using(new ProfilingScope(null,perfPositiveCameraWindow))
   {
   BuildCameraWindowFrame();UpdatePresentationEpoch();
   }
   using(new ProfilingScope(null,perfPositiveDynamicSurfaces))
   {
   dynamicPresentations.Clear();RoomPresentationRegistry.Append(dynamicPresentations);
   if(validObserver)foreach(var e in dynamicPresentations)if(e.Renderer!=null&&e.Renderer.gameObject.scene==gameObject.scene)
   {
    // 显式叶合同先于初始缓存，避免已缓存预警线/信息条绕过 Partial 或锚点许可。
    presentationMembers.Add(e.Renderer);
    if(e.RequireAnchorPermission&&!AllowsSightPoint(e.Anchor)){rejectedPresentationMembers.Add(e.Renderer);continue;}
    AddPositiveSurface(e.Renderer,e.Anchor,e.ForcePartial,e.Owner);
   }
   // 兼容原副手Sprite的晚初始化；只读已登记角色的公开表现端口，不扫描角色子树或修改旧组件。
   if(validObserver&&owner.targets!=null)foreach(var target in owner.targets)if(target!=null&&target.actor!=null)
   {
    var view=target.actor.GetComponent<Ossastra.Characters.CharacterEquipmentView>();
    if(view!=null&&view.PresentedRenderer!=null)AddPositiveSurface(view.PresentedRenderer,target.actor.transform.position,presentationOwner:view);
   }
   }
   using(new ProfilingScope(null,perfPositiveStaticSurfaces))
   {
   if(validObserver)foreach(var r in sceneSurfaces)if(r!=null&&!presentationMembers.Contains(r))AddPositiveSurface(r,null);
   }
   using(new ProfilingScope(null,perfPositiveFinalizeMasks))
   {
   knownVolumeScratch.Clear();if(ActiveRoom!=null)ActiveRoom.AppendMaskVolumes(knownVolumeScratch);
   frame.Enabled=positiveRenderingEnabled;
   frame.PositiveRenderingEnabled=positiveRenderingEnabled;frame.TruthMode=truthMode;frame.ObserverPosition=ObserverPosition;frame.ExteriorView=validObserver&&ActiveBuilding==null;
   frame.SeedVolumes=knownVolumeScratch.ToArray();frame.BuildingVolumes=allBuildingBoxes.ToArray();frame.SightDomains=sightDomains.ToArray();frame.OpaqueBlockers=blockerBoxes.ToArray();frame.PositiveSurfaces=positiveSurfaces.ToArray();
   RoomPositiveRenderingLayers.AssignDrawMasks(frame.PositiveSurfaces,positiveSourceFacts);
   var previewRoofs=new List<Renderer>();
   if(validObserver&&ActiveBuilding==null&&sightResult!=null)foreach(var domain in sightResult.Volumes)
    foreach(var b in registeredBuildings)if(b!=null&&Array.Exists(b.regions,r=>r!=null&&r.roomId==domain.TargetSpaceId))
     foreach(var roof in b.roofGroups)foreach(var r in roof.members)if(r!=null&&!previewRoofs.Contains(r))previewRoofs.Add(r);
   frame.PreviewRoofSurfaces=previewRoofs.ToArray();
   frame.LabelRaycastExclusions=labelRaycastExclusionScratch.ToArray();frame.AllowsPoint=AllowsSightPoint;
   frame.KnownVolumes=frame.SeedVolumes;frame.Volumes=frame.BuildingVolumes;
   SightStatus="观察 "+ObserverPosition.ToString("F2")+" 朝向 "+ObserverForward.ToString("F2")+" / 人物片段 "+PartialViewCount+" / 真窗景 "+WindowViewCount+" / 前墙 "+HiddenFrontWallCount;
   }
   }
   }
   catch { ClearSharedSurfaceMembership();throw; }
  }
  void AddPositiveSurface(Renderer r,Vector3? anchor,bool forcePartial=false,object presentationOwner=null)
  {
   if(r==null||!r.enabled||!r.gameObject.activeInHierarchy||r.forceRenderingOff)return;
   using(new ProfilingScope(null,perfPositiveDedup))
    if(positiveSurfaceMembers.Contains(r))return;
   if(hiddenFrontMembers.Contains(r))return;
   // Exact installed shared terrain only. Current components/tags/slots were freshly captured;
   // no cached permission, visibility history, ancestry absence or external MPB is accepted here.
   if(!anchor.HasValue&&!forcePartial&&presentationOwner==null&&!presentationMembers.Contains(r)&&
      !roofMembers.Contains(r)&&!permanentMembers.Contains(r)&&!contentOwners.ContainsKey(r)&&
      !movingAnchors.ContainsKey(r)&&IsSharedOwnerSurface(r)&&IsStaticEnvironment(r)&&
      staticTerrainPreparation.TryBuild(r,out var retainedSurface,out var retainedFacts))
   {
    positiveSurfaces.Add(retainedSurface);positiveSourceFacts.Add(retainedFacts);positiveSurfaceMembers.Add(r);return;
   }
   if(IsOwnedVisualCoverage(r))return;
   if(!anchor.HasValue&&movingAnchors.TryGetValue(r,out var moving)&&moving!=null)anchor=moving.position;
   var kind=RoomPositiveSurfaceKind.Partial;
   if(roofMembers.Contains(r))
   {
    if(ActiveBuilding!=null&&Array.Exists(ActiveBuilding.roofGroups,g=>g!=null&&Array.IndexOf(g.members,r)>=0)&&revealRoofs)return;
    kind=ActiveBuilding==null?RoomPositiveSurfaceKind.Roof:RoomPositiveSurfaceKind.Partial;
   }
   else if(!forcePartial&&ActiveBuilding==null&&permanentMembers.Contains(r))kind=RoomPositiveSurfaceKind.Full;
   else if(!forcePartial&&!anchor.HasValue&&contentOwners.TryGetValue(r,out var room)&&room==ActiveRoom)kind=RoomPositiveSurfaceKind.Full;
   else if(!forcePartial&&anchor.HasValue&&ActiveRoom!=null&&ActiveRoom.Contains(anchor.Value))kind=RoomPositiveSurfaceKind.Full;
   else if(!forcePartial&&owner.targets!=null)foreach(var target in owner.targets)if(target!=null&&target.actor!=null&&r.transform.IsChildOf(target.actor.transform)&&RoomForActor(target.actor)==ActiveRoom&&ActiveRoom!=null){kind=RoomPositiveSurfaceKind.Full;break;}
   RoomRegion ownerRoom=null;
   bool ownerExterior=false;
   if(anchor.HasValue){ownerRoom=RoomAt(anchor.Value,null);ownerExterior=ownerRoom==null;}
   else if(contentOwners.TryGetValue(r,out var contentRoom))ownerRoom=contentRoom;
   else if(!permanentMembers.Contains(r)&&!roofMembers.Contains(r)&&!IsSharedOwnerSurface(r))ownerExterior=true;
   var ownerVolumes=new List<RoomMaskVolume>();if(ownerRoom!=null)ownerRoom.AppendMaskVolumes(ownerVolumes);
   bool staticEnvironment=IsStaticEnvironment(r);
   if(staticEnvironment&&fog!=null&&fog.Enabled&&kind==RoomPositiveSurfaceKind.Full)kind=RoomPositiveSurfaceKind.Partial;
   bool environmentFull=environmentSurfaceRouting&&kind==RoomPositiveSurfaceKind.Full;
   if(environmentFull)kind=RoomPositiveSurfaceKind.Partial;
   bool information=presentationMembers.Contains(r);var sourceFacts=InspectPositiveSource(r,information,presentationOwner,out bool worldLook,out float environmentRole);
   var surface=new RoomPositiveSurface{Renderer=r,Kind=kind,OwnerVolumes=ownerVolumes.ToArray(),OwnerExterior=ownerExterior,IndependentDraw=information,StaticEnvironment=staticEnvironment,WorldLookEnvironment=worldLook,
    EnvironmentFullSurface=environmentFull,EnvironmentSurfaceRole=environmentRole,EnvironmentResponseOverride=1,EnvironmentReceiveShadow=1,EnvironmentMinimumOverride=0};
   ApplyEnvironmentTag(r,ref surface);positiveSurfaces.Add(surface);positiveSourceFacts.Add(sourceFacts);positiveSurfaceMembers.Add(r);
  }
  void CaptureSharedSurfaceMembership()
  {
   sharedSurfaceMembers.Clear();sharedSurfaceMembershipSnapshot=sharedSurfaces;
   if(sharedSurfaceMembershipSnapshot!=null)foreach(var renderer in sharedSurfaceMembershipSnapshot)sharedSurfaceMembers.Add(renderer);
  }
  void ClearSharedSurfaceMembership(){sharedSurfaceMembers.Clear();sharedSurfaceMembershipSnapshot=null;}
  bool IsSharedOwnerSurface(Renderer renderer)
  {
   // Array.IndexOf的默认Equals与Array.Exists的Unity ==在null/销毁包装器上不合并推断。
   if(sharedSurfaceMembershipSnapshot==null||renderer==null)return Array.IndexOf(sharedSurfaceMembershipSnapshot,renderer)>=0;
   return sharedSurfaceMembers.Contains(renderer);
  }
  bool IsSharedShadowSurface(Renderer renderer)
  {
   if(sharedSurfaceMembershipSnapshot==null||renderer==null)return Array.Exists(sharedSurfaceMembershipSnapshot,item=>item==renderer);
   return sharedSurfaceMembers.Contains(renderer);
  }
  static RoomPositiveSourceFacts InspectPositiveSource(Renderer renderer,bool information,object presentationOwner,out bool worldLook,out float environmentRole)
  {
   var sources=RoomPositiveRenderingLayers.CaptureSourceFacts(renderer);
   bool particle=renderer is ParticleSystemRenderer;
   bool needsWorld=!information&&!particle,needsRole=presentationOwner==null&&!particle;
   bool needsIndependent=!information&&!(renderer is SpriteRenderer)&&sources.Family!=0;
   bool actorOrEnemy=false,actorOrNpc=false;
   if(needsWorld||needsRole||needsIndependent)
    actorOrEnemy=renderer.GetComponentInParent<Ossastra.Characters.CharacterRoot>(true)!=null||renderer.GetComponentInParent<Ossastra.Enemies.EnemyRoot>(true)!=null;
   actorOrNpc=actorOrEnemy;
   if((needsWorld||needsRole)&&!actorOrNpc)actorOrNpc=renderer.GetComponentInParent<Ossastra.Npcs.NpcRoot>(true)!=null;
   // Registered grass chunks remain environment pixels, despite using the transient
   // presentation registry for precise rebuild/disable ownership. This grants no view.
   bool grass=sources.Family==RoomPositiveRenderingLayers.PartialDecorationBit&&sources.Materials.Length>0;
   if(grass)foreach(var material in sources.Materials)if(material==null||material.shader==null||material.shader.name!=RoomPositiveRenderingLayers.GrassShaderName){grass=false;break;}
   worldLook=(needsWorld||grass)&&!particle&&!actorOrNpc&&renderer.GetComponentInParent<Spine.Unity.SkeletonRenderer>(true)==null;
   sources.Independent|=actorOrEnemy;
   environmentRole=EnvironmentRoleFor(renderer,presentationOwner,actorOrNpc,sources.Family);return sources;
  }
  static float EnvironmentRoleFor(Renderer renderer,object presentationOwner,bool actorOrNpc,uint family)
  {
   // 登记叶的语义先于人物祖先：敌人挂有预警组件，不代表敌人身体属于信息。
   if(presentationOwner is RoomPresentationBinding binding)return binding.EnvironmentRole>=-1&&binding.EnvironmentRole<=2?binding.EnvironmentRole:-1;
   if(presentationOwner is Ossastra.Characters.CharacterEquipmentView||presentationOwner is Ossastra.Characters.CharacterEquipmentSpineView||
      presentationOwner is Ossastra.Characters.CharacterEquipment||presentationOwner is Ossastra.Characters.CharacterOffhand||presentationOwner is Ossastra.Inventory.TransientItemView)return 2;
   if(presentationOwner is Ossastra.WorldProps.WorldPropView||presentationOwner is Ossastra.UserInterface.InventoryWorldView)return 1;
   if(presentationOwner!=null||renderer is ParticleSystemRenderer)return -1;
   if(actorOrNpc)return 2;
   return family==RoomPositiveRenderingLayers.PartialThinTopBit||family==RoomPositiveRenderingLayers.PartialWorldTileBit?0:1;
  }
  static void ApplyEnvironmentTag(Renderer renderer,ref RoomPositiveSurface surface)
  {
   using(new ProfilingScope(null,perfEnvironmentTag))
   {
   foreach(var tag in renderer.GetComponentsInParent<EnvironmentMaterialResponse>(true))
   {
    if(tag==null||!tag.isActiveAndEnabled)continue;
    bool explicitMember=tag.renderers!=null&&Array.IndexOf(tag.renderers,renderer)>=0;
    bool ownRenderer=(tag.renderers==null||tag.renderers.Length==0)&&tag.transform==renderer.transform;
    if(!explicitMember&&!ownRenderer)continue;
    // 原生Lit仍走URP BRDF；不能把仅无光照适配支持的倍率/最低亮度伪装成原生材质控制。
    if(tag.role==EnvironmentMaterialRole.NativeLit)return;
    surface.EnvironmentSurfaceRole=tag.role==EnvironmentMaterialRole.Ground?0:tag.role==EnvironmentMaterialRole.Decoration?1:2;
    surface.EnvironmentResponseOverride=float.IsNaN(tag.response)||float.IsInfinity(tag.response)?1:Mathf.Clamp01(tag.response);
    surface.EnvironmentReceiveShadow=tag.receiveSunShadow?1:0;
    surface.EnvironmentMinimumOverride=float.IsNaN(tag.minimumReadability)||float.IsInfinity(tag.minimumReadability)?0:Mathf.Clamp01(tag.minimumReadability);
    return;
   }
   }
  }
  void ApplyPositiveRegistration()
  {
   if(!frame.PositiveRenderingEnabled)return;
   foreach(var r in hiddenFrontMembers)visualScope.Suppress(r);
   foreach(var r in rejectedPresentationMembers)positiveScope.Apply(r,0,true);
   foreach(var surface in frame.PositiveSurfaces)
   {
    uint bits=RoomPositiveRenderingLayers.GetMask(surface);
    // 非全房内容不保留整件投影；结构/顶盖遮光仍由原Caster承担。
    bool staticFullRoom=surface.StaticEnvironment&&contentOwners.TryGetValue(surface.Renderer,out var contentRoom)&&contentRoom==ActiveRoom;
    bool shadowOff=surface.Kind==RoomPositiveSurfaceKind.Partial&&!surface.EnvironmentFullSurface&&!staticFullRoom&&!permanentMembers.Contains(surface.Renderer)&&!roofMembers.Contains(surface.Renderer)&&!IsSharedShadowSurface(surface.Renderer);
    positiveScope.Apply(surface.Renderer,bits,shadowOff);
   }
  }
  bool AllowsSightPoint(Vector3 point)
  {
   if(!validObserver)return false;
   if(ActiveRoom!=null&&ActiveRoom.Contains(point))return true;
   bool inBuilding=false;foreach(var b in registeredBuildings)if(b!=null&&b.Contains(point)){inBuilding=true;break;}
   if(ActiveBuilding==null&&!inBuilding)return true;
   if(sightResult==null)return false;
   foreach(var volume in sightResult.Volumes)
   {
    if(volume.TargetSpaceId==ObserverSnapshot.Exterior){if(inBuilding)continue;}
    else {bool inside=false;foreach(var r in registeredRooms)if(r!=null&&r.roomId==volume.TargetSpaceId&&r.Contains(point)){inside=true;break;}if(!inside)continue;}
    if(!volume.ContainsPoint(S(point)))continue;
    bool blocked=false;foreach(var blocker in coreBlockers)if(blocker.OccludesSegment(S(ObserverPosition),S(point))){blocked=true;break;}
    if(!blocked)return true;
   }
   return false;
  }
  void PublishRejectedFrame(string reason)
  {
   // 当前已启用观察策略时，作者预算/快照失效只撤销许可，不撤销黑底合成注册。
   positiveScope.Dispose();visualScope.Dispose();validObserver=false;presentationSampled=false;++presentationEpoch;
   frame.Enabled=true;frame.PositiveRenderingEnabled=true;frame.ExteriorView=false;frame.TruthMode=truthMode;
   frame.SeedVolumes=Array.Empty<RoomMaskVolume>();frame.BuildingVolumes=Array.Empty<RoomMaskVolume>();frame.SightDomains=Array.Empty<RoomSightDomain>();frame.WindowSightDomains=Array.Empty<RoomSightDomain>();frame.CameraOpaqueBlockers=Array.Empty<RoomMaskVolume>();frame.OpaqueBlockers=Array.Empty<RoomMaskVolume>();frame.PositiveSurfaces=Array.Empty<RoomPositiveSurface>();frame.PreviewRoofSurfaces=Array.Empty<Renderer>();frame.LabelRaycastExclusions=Array.Empty<Collider>();frame.AllowsPoint=AllowsSightPoint;frame.VisibilityEpoch=presentationEpoch;
   Status="显示许可被拒绝："+reason;SightStatus=Status;
   RoomMaskCameraRegistry.Publish(this,boundCamera,frame);
  }
 }
}
