/* 功能：显式表面采样、保存数据重建小合并块，并消费同相机环境风。
 * 边界：只创建自有贴图承载层，不动碰撞/地皮/天气；可选接原Room叶登记，始终服从片元许可。
 * 生命周期：无逐草Update/射线；仅生成时采样。每相机有限写自有材质，禁用释放全部显示资源与事件。
 */
using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using UnityEngine.Rendering;using Unity.Profiling;using Ossastra.EnvironmentSimulation;
namespace Ossastra.WorldRendering.Vegetation
{
 public enum GrassWindMode{Original,Static,Environment,Light,Gust}
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class GrassLayer:MonoBehaviour
 {
  static readonly ProfilerMarker CameraMarker=new ProfilerMarker("Ossastra.GrassLayer.Camera");
  public GrassLayerProfileSO profile;public GrassPatchDataSO data;
  public GrassTerrainColorSourceSO terrainColor;
  public MeshRenderer[] terrainSources=Array.Empty<MeshRenderer>();
  public GrassSurfaceBinding[] surfaces=Array.Empty<GrassSurfaceBinding>();
  public BoxCollider[] exclusions=Array.Empty<BoxCollider>();
  public EnvironmentRoot environment;public Camera gameplayCamera;
  [Tooltip("主场接入时启用；使用原Room叶登记，禁用/重建时精确撤销，不写作者sharedSurfaces。")]
  public bool registerRoomVisibility;
  [Tooltip("明确已核对的室外锚点；草层只铺室外，片元许可仍按实际草的位置判断。")]
  public Vector3 roomExteriorAnchor;
  public GrassWindMode mode=GrassWindMode.Environment;
  public bool castShadows;public Vector2 testWindDirection=new Vector2(1,.35f);
  public string Status{get;private set;}="尚未生成";
  public int ClumpCount{get;private set;}public int VertexCount{get;private set;}public int TriangleCount{get;private set;}
  public int BlockCount=>renderers.Count;
  public int OwnedMaterialCount=>(staticMaterial!=null?1:0)+(windMaterial!=null?1:0);
  public IReadOnlyList<MeshRenderer> Renderers=>renderers;
  readonly List<MeshRenderer> renderers=new List<MeshRenderer>();readonly List<Mesh> meshes=new List<Mesh>();
  readonly List<MeshFilter> filters=new List<MeshFilter>();readonly List<Mesh> nearMeshes=new List<Mesh>(),farMeshes=new List<Mesh>();
  Material staticMaterial,windMaterial;bool subscribed;Camera leasedCamera;bool[] priorHidden;
  Vector4 priorStaticWind,priorDynamicWind;Vector2 smoothedWind;double lastWindSeconds;bool hasWindTime;float bakedMaximumBend;
  void OnEnable(){Subscribe();if(profile!=null&&data!=null)RebuildFromSaved();}
  void Subscribe(){if(subscribed)return;RenderPipelineManager.beginCameraRendering+=BeginCamera;RenderPipelineManager.endCameraRendering+=EndCamera;EnvironmentCameraRegistry.Released+=Released;subscribed=true;}
  void OnDisable(){if(subscribed){RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderPipelineManager.endCameraRendering-=EndCamera;EnvironmentCameraRegistry.Released-=Released;subscribed=false;}RestoreCamera();ReleaseView();}
  void OnDestroy(){OnDisable();}
  void Released(int id){if(gameplayCamera!=null&&id==gameplayCamera.GetInstanceID()){foreach(var r in renderers)if(r!=null)r.forceRenderingOff=true;Status="环境显示登记已释放；停止草层提交";}}
  public GrassPlacement[] Generate(float density=-1)
  {
   if(profile==null||surfaces==null||surfaces.Length==0)throw new InvalidOperationException("需要明确草配置与表面");
   if(surfaces.Any(s=>s==null||s.collider==null||s.collider.gameObject.scene!=gameObject.scene||string.IsNullOrWhiteSpace(s.id))||surfaces.Select(s=>s.id).Distinct().Count()!=surfaces.Length)throw new InvalidOperationException("表面身份缺失/重复/跨场景");
   if(profile.edgeInset<0||float.IsNaN(profile.edgeInset)||profile.edgeInset>2)throw new InvalidOperationException("边缘内缩无效");
   Physics.SyncTransforms();var areas=surfaces.Select((s,i)=>new GrassSurfaceArea{surfaceIndex=i,worldBounds=s.collider.bounds}).ToArray();
   Func<int,Vector2,GrassSurfaceSample?> sample=(i,xz)=>{var collider=surfaces[i].collider;var bounds=collider.bounds;float top=bounds.max.y+2,length=bounds.size.y+4;RaycastHit hit;
    if(!collider.Raycast(new Ray(new Vector3(xz.x,top,xz.y),Vector3.down),out hit,length))return null;
    foreach(var offset in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down}){RaycastHit edge;var q=xz+offset*profile.edgeInset;if(!collider.Raycast(new Ray(new Vector3(q.x,top,q.y),Vector3.down),out edge,length)||Mathf.Abs(edge.point.y-hit.point.y)>.5f)return null;}
    var t=collider.transform;return new GrassSurfaceSample{worldRoot=hit.point,localRoot=t.InverseTransformPoint(hit.point),worldNormal=hit.normal.normalized,localNormal=t.localToWorldMatrix.transpose.MultiplyVector(hit.normal).normalized};};
   Func<Vector3,bool> exclude=p=>{foreach(var box in exclusions??Array.Empty<BoxCollider>()){if(box==null)continue;var point=box.transform.InverseTransformPoint(p)-box.center;var pad=Vector3.one*(profile.edgeInset+profile.maximumBend);var half=box.size*.5f+pad;if(Mathf.Abs(point.x)<=half.x&&Mathf.Abs(point.y)<=half.y&&Mathf.Abs(point.z)<=half.z)return true;}return false;};
   return GrassScatter.Generate(new GrassScatterSettings{seed=profile.seed,density=density<0?profile.density:density,maxDensity=profile.maxDensity,minHeight=profile.heightRange.x,maxHeight=profile.heightRange.y,minWidth=profile.widthRange.x,maxWidth=profile.widthRange.y,maxSlopeDegrees=profile.maximumSlope,maximumPlacements=profile.maximumPlacements},areas,sample,exclude);
  }
  public void RebuildFromSaved()
  {
   ReleaseView();if(profile==null||profile.image==null||profile.material==null||data==null){Status="配置/原图/材质/数据缺失，未提交";return;}
   if(data.surfaceIds==null||data.surfaceIds.Length!=surfaces.Length)throw new InvalidOperationException("保存表面集不匹配");
   try{
    staticMaterial=new Material(profile.material){name="Grass static [owned]",hideFlags=HideFlags.DontSave};windMaterial=new Material(profile.material){name="Grass wind [owned]",hideFlags=HideFlags.DontSave};staticMaterial.DisableKeyword("_GRASS_WIND");windMaterial.EnableKeyword("_GRASS_WIND");
    foreach(var m in new[]{staticMaterial,windMaterial}){m.SetTexture("_MainTex",profile.image);m.SetColor("_Color",profile.tint);m.SetColor("_RootTint",profile.rootTint);m.SetColor("_TipTint",profile.tipTint);m.SetFloat("_Cutoff",profile.cutoff);m.SetFloat("_EnvironmentResponseOverride",1);m.SetFloat("_EnvironmentReceiveShadow",1);m.SetFloat("_EnvironmentMinimumOverride",0);}
    if(float.IsNaN(profile.maximumBend)||float.IsInfinity(profile.maximumBend)||profile.maximumBend<0||profile.maximumBend>.5f)throw new InvalidOperationException("草摆幅无效");bakedMaximumBend=profile.maximumBend;if(profile.farEnd<=profile.farStart||float.IsNaN(profile.farEnd)||float.IsInfinity(profile.farEnd))throw new InvalidOperationException("远处密度区间无效");var farBlocks=GrassMeshBuilder.Build(profile,data,surfaces,transform,true).ToDictionary(b=>b.cell);foreach(var b in farBlocks.Values)meshes.Add(b.mesh);var blocks=GrassMeshBuilder.Build(profile,data,surfaces,transform);foreach(var b in blocks){meshes.Add(b.mesh);var go=new GameObject("GrassBlock_"+b.cell){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=b.mesh;filters.Add(filter);nearMeshes.Add(b.mesh);farMeshes.Add(farBlocks.TryGetValue(b.cell,out var farBlock)?farBlock.mesh:null);var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=windMaterial;renderer.shadowCastingMode=castShadows?ShadowCastingMode.TwoSided:ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderers.Add(renderer);ClumpCount+=b.count;VertexCount+=b.mesh.vertexCount;TriangleCount+=(int)b.mesh.GetIndexCount(0)/3;}
    RefreshAppearance();priorHidden=new bool[renderers.Count];ApplyMode();
    if(registerRoomVisibility)foreach(var renderer in renderers)Ossastra.SceneVisibility.Rooms.RoomPresentationBinding.Attach(renderer,roomExteriorAnchor,true,null,false,0);
    Status="二维草层："+ClumpCount+"簇 / "+BlockCount+"合并块；"+(registerRoomVisibility?"原Room片元许可":"独立URP候选");
   }catch{ReleaseView();throw;}
  }
  public void SetMode(GrassWindMode value,bool shadows){mode=value;castShadows=shadows;ApplyMode();}
  // 配色修改只更新自有的两份材质，不重建散布或网格；terrain身份不匹配时明确退回BottomColor。
  public void RefreshAppearance()
  {
   if(profile==null)return;
   bool valid=terrainColor!=null&&terrainColor.texture!=null&&terrainColor.version>0&&surfaces!=null&&surfaces.Length==1&&surfaces[0]!=null&&terrainColor.surfaceId==surfaces[0].id&&terrainColor.worldXZ.width>0&&terrainColor.worldXZ.height>0;
   foreach(var m in new[]{staticMaterial,windMaterial})if(m!=null)
   {
    m.SetColor("_Color",profile.tint);m.SetColor("_RootTint",profile.rootTint);m.SetColor("_TipTint",profile.tipTint);
    m.SetFloat("_ArtMode",(int)profile.appearance);m.SetFloat("_WindMode",(int)profile.motion);m.SetFloat("_RGBDetail",profile.rgbDetail);
    m.SetColor("_NearColor",profile.nearColor);m.SetColor("_FarColor",profile.farColor);m.SetColor("_BottomColor",profile.bottomColor);m.SetColor("_ShadowColor",profile.shadowColor);
    m.SetVector("_NearFarRange",new Vector4(profile.nearFarRange.x,profile.nearFarRange.y,0,0));m.SetFloat("_HeightBlend",profile.heightBlend);m.SetFloat("_DepthColorStrength",profile.depthColorStrength);m.SetFloat("_UseTerrainColor",profile.useTerrainColor?1:0);
    m.SetTexture("_TerrainColor",valid?terrainColor.texture:Texture2D.blackTexture);m.SetFloat("_TerrainColorValid",valid?1:0);var rect=valid?terrainColor.worldXZ:new Rect(0,0,1,1);m.SetVector("_TerrainRect",new Vector4(rect.x,rect.y,rect.width,rect.height));
    m.SetTexture("_WindTexture",profile.windTexture!=null?profile.windTexture:Texture2D.grayTexture);m.SetVector("_WindNoiseScale",new Vector4(profile.windNoiseScale.x,profile.windNoiseScale.y,0,0));m.SetVector("_WindNoiseContrast",new Vector4(profile.windNoiseContrast.x,profile.windNoiseContrast.y,0,0));m.SetFloat("_WindNoiseSpeed",profile.windNoiseSpeed);m.SetFloat("_FineWindStrength",profile.fineWindStrength);m.SetFloat("_CoarseWindStrength",profile.coarseWindStrength);m.SetFloat("_WindColorStrength",profile.windColorStrength);m.SetFloat("_WindTextureLod",profile.windTextureLod);m.SetFloat("_ArtDebug",profile.artDebug);
   }
  }
  void ApplyMode(){foreach(var r in renderers){if(r==null)continue;r.sharedMaterial=mode==GrassWindMode.Static?staticMaterial:windMaterial;r.enabled=mode!=GrassWindMode.Original;r.shadowCastingMode=castShadows?ShadowCastingMode.TwoSided:ShadowCastingMode.Off;}}
  void BeginCamera(ScriptableRenderContext context,Camera camera)
  {
   using(CameraMarker.Auto()){
   if(leasedCamera!=null)RestoreCamera();if(renderers.Count==0)return;
   bool valid=isActiveAndEnabled&&environment!=null&&environment.isActiveAndEnabled&&environment.gameplayCamera==gameplayCamera&&camera==gameplayCamera&&EnvironmentCameraRegistry.TryGet(camera,out var frame)&&frame.Enabled;
   for(int i=0;i<renderers.Count;i++){var r=renderers[i];if(r==null)continue;priorHidden[i]=r.forceRenderingOff;r.forceRenderingOff=!valid;}
   leasedCamera=camera;priorStaticWind=staticMaterial.GetVector("_GrassWind");priorDynamicWind=windMaterial.GetVector("_GrassWind");
   if(!valid)return;var cameraPosition=camera.transform.position;var lod=new Vector4(cameraPosition.x,cameraPosition.z,profile.farStart,profile.farEnd);staticMaterial.SetVector("_GrassCameraLod",lod);windMaterial.SetVector("_GrassCameraLod",lod);for(int i=0;i<filters.Count;i++){var b=nearMeshes[i].bounds;var local=transform.InverseTransformPoint(cameraPosition);float dx=Mathf.Max(0,Mathf.Abs(local.x-b.center.x)-b.extents.x),dz=Mathf.Max(0,Mathf.Abs(local.z-b.center.z)-b.extents.z);bool far=dx*dx+dz*dz>=profile.farEnd*profile.farEnd;var mesh=far?farMeshes[i]:nearMeshes[i];if(filters[i].sharedMesh!=mesh)filters[i].sharedMesh=mesh;}EnvironmentCameraRegistry.TryGet(camera,out var current);var dir=mode==GrassWindMode.Environment?current.WindDirection:testWindDirection;if(dir.sqrMagnitude<.000001f)dir=Vector2.right;dir.Normalize();
   float strength=(mode==GrassWindMode.Static||mode==GrassWindMode.Original)?0:mode==GrassWindMode.Environment?1-Mathf.Exp(-Mathf.Max(0,current.WindSpeed)*profile.windResponse):mode==GrassWindMode.Light?.4f:.9f;
   // 固定16秒相位，86400秒环境绕回也连续；速度只影响有界摆幅，不重乘累计时间。
   double delta=hasWindTime?current.Seconds-lastWindSeconds:0;if(delta<0)delta+=86400;delta=Math.Min(delta,.25);lastWindSeconds=current.Seconds;hasWindTime=true;smoothedWind=Vector2.Lerp(smoothedWind,dir*strength,1-Mathf.Exp(-(float)delta*7));var wind=new Vector4(smoothedWind.x,smoothedWind.y,(float)(current.Seconds%16d),Mathf.Min(profile.maximumBend,bakedMaximumBend));staticMaterial.SetVector("_GrassWind",wind);windMaterial.SetVector("_GrassWind",wind);
  }
  }
  void EndCamera(ScriptableRenderContext context,Camera camera){if(camera==leasedCamera)RestoreCamera();}
  void RestoreCamera(){if(leasedCamera==null)return;for(int i=0;i<renderers.Count&&i<priorHidden.Length;i++)if(renderers[i]!=null)renderers[i].forceRenderingOff=priorHidden[i];if(staticMaterial!=null)staticMaterial.SetVector("_GrassWind",priorStaticWind);if(windMaterial!=null)windMaterial.SetVector("_GrassWind",priorDynamicWind);leasedCamera=null;}
  void ReleaseView(){RestoreCamera();foreach(var r in renderers)if(r!=null){var binding=r.GetComponent<Ossastra.SceneVisibility.Rooms.RoomPresentationBinding>();if(binding!=null)binding.Suspend();GrassMeshBuilder.Dispose(r.gameObject);}renderers.Clear();filters.Clear();nearMeshes.Clear();farMeshes.Clear();foreach(var mesh in meshes)GrassMeshBuilder.Dispose(mesh);meshes.Clear();GrassMeshBuilder.Dispose(staticMaterial);GrassMeshBuilder.Dispose(windMaterial);staticMaterial=windMaterial=null;priorHidden=Array.Empty<bool>();ClumpCount=VertexCount=TriangleCount=0;}
 }
}
