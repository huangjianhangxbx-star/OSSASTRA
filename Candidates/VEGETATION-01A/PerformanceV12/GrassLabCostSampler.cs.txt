/* 功能：独立草场v1.2四态非阻塞采样：原地表、Tutorial静止、Tutorial完整风、Environment加投影。
 * 边界：不推进天气、不改画质/机位/数据/密度，不把线程含wait总时长当有效CPU，不把未取得GPU写0。
 * 生命周期：只借用本场控制字段/草态；完成、停用、销毁或主动中止均释放Recorder并有条件归还。
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Newtonsoft.Json;
using Ossastra.EnvironmentSimulation;
namespace Ossastra.WorldRendering.Vegetation.Development
{
 [DisallowMultipleComponent]
 public sealed class GrassLabCostSampler:MonoBehaviour
 {
  const string ScenePrefix="Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/";
  static readonly string[] MarkerNames={"Main Thread","Render Thread","GC Allocated In Frame","Draw Calls Count","SetPass Calls Count","Triangles Count","Vertices Count","Ossastra.GrassLayer.Camera"};
  public GrassLabControls controls;
  public GrassLayer[] layers=Array.Empty<GrassLayer>();
  public bool IsRunning{get;private set;}
  public string Status{get;private set;}="未开始；录制与性能采样须分开。";
  public string ResultPath{get;private set;}="";
  Coroutine routine;RunReport report;RecorderSlot[] recorders;
  GrassLayer[] ownedLayers;LayerBorrow[] beforeLayers;
  bool borrowed,previousPanel,previousInput;GrassWindMode previousMode,lastMode;bool previousShadows,lastShadows;
  GrassAppearance previousAppearance,lastAppearance;GrassMotion previousMotion,lastMotion;
  readonly FrameTiming[] timing=new FrameTiming[1];HashSet<ulong> seenGpuStamps=new HashSet<ulong>();
  string gpuReadError="";

  public bool Begin(string outputPath,float warmupSeconds=2,float sampleSeconds=8)
  {
   if(IsRunning){Status="已有采样任务，未重复启动。";return false;}
   ResultPath="";report=null;
   try
   {
    Validate(outputPath,warmupSeconds,sampleSeconds,out string path);
    var configured=(controls.layers??Array.Empty<GrassLayer>()).Where(l=>l!=null).Distinct().ToArray();
    var requested=(layers==null||layers.Length==0?configured:layers).Where(l=>l!=null).Distinct().ToArray();
    if(configured.Length==0||requested.Length!=configured.Length||configured.Any(l=>!requested.Contains(l)))throw new InvalidOperationException("采样层必须精确覆盖controls.layers，不能切换未计入的草层。");
    if(requested.Any(l=>l.gameObject.scene!=gameObject.scene||l.profile==null||l.data==null||!l.isActiveAndEnabled))throw new InvalidOperationException("需要本场启用且有保存配置/数据的明确草层。");
    ownedLayers=requested;beforeLayers=ownedLayers.Select(l=>new LayerBorrow{layer=l,profile=l.profile,mode=l.mode,shadows=l.castShadows}).ToArray();
    previousPanel=controls.showPanel;previousInput=controls.acceptKeyboardInput;previousMode=lastMode=controls.CurrentMode;previousShadows=lastShadows=controls.Shadows;previousAppearance=lastAppearance=controls.CurrentAppearance;previousMotion=lastMotion=controls.CurrentMotion;
    if(ownedLayers.Any(l=>l.profile.appearance!=previousAppearance||l.profile.motion!=previousMotion))throw new InvalidOperationException("草层必须使用控制器当前运行副本的同一配色/运动路线。");
    report=new RunReport{startedUtc=DateTime.UtcNow.ToString("o"),scene=gameObject.scene.path,editor=Application.isEditor,unityVersion=Application.unityVersion,
     graphicsDevice=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),processor=SystemInfo.processorType,warmupSeconds=warmupSeconds,sampleSeconds=sampleSeconds};
    seenGpuStamps=new HashSet<ulong>(Mathf.CeilToInt((warmupSeconds+sampleSeconds)*4*500));gpuReadError="";ResultPath=path;recorders=OpenRecorders();report.markers=recorders.Select(r=>r.description).ToArray();
    borrowed=true;controls.showPanel=false;controls.acceptKeyboardInput=false;IsRunning=true;Status="正在进行S0–S3四态采样；不录制。";
    var started=StartCoroutine(Run(warmupSeconds,sampleSeconds));routine=IsRunning?started:null;return IsRunning;
   }
   catch(Exception e){Status="采样未启动："+e.Message;ReleaseRecorders();RestoreBorrow();IsRunning=false;return false;}
  }
  void Validate(string outputPath,float warmup,float seconds,out string path)
  {
   if(!Application.isPlaying||!isActiveAndEnabled||!gameObject.scene.IsValid()||!gameObject.scene.path.StartsWith(ScenePrefix,StringComparison.Ordinal))throw new InvalidOperationException("仅服务运行中的GrassLab独立场。");
   if(controls==null||controls.gameObject.scene!=gameObject.scene||controls.camera==null||controls.camera.gameObject.scene!=gameObject.scene||controls.environment==null||controls.environment.gameObject.scene!=gameObject.scene||controls.environment.gameplayCamera!=controls.camera)throw new InvalidOperationException("明确本场控制器/相机/同相机环境。");
   if(!EnvironmentCameraRegistry.TryGet(controls.camera,out var frame)||!frame.Enabled)throw new InvalidOperationException("同相机环境显示尚未就绪。");
   if(!Finite(warmup)||!Finite(seconds)||warmup<0||warmup>60||seconds<1||seconds>120)throw new ArgumentException("warmup为0..60秒，sample为1..120秒。");
   if(string.IsNullOrWhiteSpace(outputPath))throw new ArgumentException("需要明确输出JSON路径。");
   string project=Path.GetDirectoryName(Application.dataPath);string allowed=Path.GetFullPath(Path.Combine(project,"artifacts/VEGETATION-01A"))+Path.DirectorySeparatorChar;
   path=Path.GetFullPath(Path.IsPathRooted(outputPath)?outputPath:Path.Combine(project,outputPath));
   if(!path.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("输出仅允许项目artifacts/VEGETATION-01A/下的JSON。");
   if(File.Exists(path))throw new InvalidOperationException("结果已存在；请用新路径，不覆盖旧证据。");
  }
  IEnumerator Run(float warmup,float seconds)
  {
   try
   {
    for(int index=0;index<4;index++)
    {
     int state=index;if(!Guard(()=>ApplyState(state)))yield break;
     double start=Time.realtimeSinceStartupAsDouble;
     while(IsRunning&&Time.realtimeSinceStartupAsDouble-start<warmup){ReadGpu(false);yield return null;}
     if(!IsRunning)yield break;
     StateReport item=null;if(!Guard(()=>{item=new StateReport{name="S"+state,mode=lastMode.ToString(),appearance=lastAppearance.ToString(),motion=lastMotion.ToString(),shadows=lastShadows,before=CaptureConditions(),layers=CaptureLayers(),raw=new List<FrameSample>(Mathf.CeilToInt(seconds*500))};CheckPassModes(state,item.layers);report.states.Add(item);}))yield break;
     yield return null;ReadGpu(false);yield return null;ReadGpu(false); // drain metadata/allocation frames before timing
     start=Time.realtimeSinceStartupAsDouble;item.environmentSecondsStart=controls.environment.Current.Seconds;
     while(IsRunning&&Time.realtimeSinceStartupAsDouble-start<seconds)
     {
      yield return null;if(!IsRunning)yield break;
      if(!TrySample(item))yield break;
     }
     if(!Guard(()=>{item.actualSeconds=Time.realtimeSinceStartupAsDouble-start;item.environmentSecondsEnd=controls.environment.Current.Seconds;item.after=CaptureConditions();item.afterLayers=CaptureLayers();item.completed=true;CheckConditions(item);}))yield break;
    }
    report.completed=true;report.stopReason="completed";
   }
   finally{Finish();}
  }
  bool Guard(Action body){try{body();return true;}catch(Exception e){report.error=e.GetType().Name+": "+e.Message;report.stopReason="sampling-error";return false;}}
  void ApplyState(int state)
  {
   if(controls==null||ownedLayers.Any(l=>l==null))throw new InvalidOperationException("采样对象已卸载。");
   lastMode=state==0?GrassWindMode.Original:state==1?GrassWindMode.Static:GrassWindMode.Light;lastShadows=state==3;lastAppearance=state==3?GrassAppearance.Environment:GrassAppearance.Tutorial;lastMotion=GrassMotion.CombinedColor;
   controls.SetAppearance(lastAppearance);controls.SetMotion(lastMotion);controls.SetMode(lastMode,lastShadows);
   if(controls.CurrentAppearance!=lastAppearance||controls.CurrentMotion!=lastMotion||ownedLayers.Any(l=>l.mode!=lastMode||l.castShadows!=lastShadows||l.profile==null||l.profile.appearance!=lastAppearance||l.profile.motion!=lastMotion))throw new InvalidOperationException("实际草态/配色/运动路线未按指定状态切换；不写作者资产。");
   Status="采样 S"+state+"："+lastMode+" / "+lastAppearance+" / "+lastMotion+(lastShadows?" / 草投影":" / 无草投影");
  }
  bool TrySample(StateReport state){try{Sample(state);return true;}catch(Exception e){report.error=e.GetType().Name+": "+e.Message;report.stopReason="sampling-error";return false;}}
  void Sample(StateReport state)
  {
   float dt=Time.unscaledDeltaTime;if(!Finite(dt)||dt<=0)throw new InvalidOperationException("无效实际帧间隔。");
   var s=new FrameSample{frame=Time.frameCount,frameMilliseconds=dt*1000d,mainThreadRaw=recorders[0].Read(),renderThreadRaw=recorders[1].Read(),gcBytesRaw=recorders[2].Read(),drawCallsRaw=recorders[3].Read(),setPassRaw=recorders[4].Read(),trianglesRaw=recorders[5].Read(),verticesRaw=recorders[6].Read(),grassCameraNs=recorders[7].description.unit=="TimeNanoseconds"?recorders[7].Read():null,gpuMilliseconds=ReadGpu(true)};
   if(!StableView(state.before))state.changedViewFrames++;
   if(controls==null||controls.CurrentAppearance!=lastAppearance||controls.CurrentMotion!=lastMotion)state.changedGrassFrames++;
   else for(int i=0;i<ownedLayers.Length;i++){var l=ownedLayers[i];if(l==null||l.mode!=lastMode||l.castShadows!=lastShadows||l.profile==null||l.profile.appearance!=lastAppearance||l.profile.motion!=lastMotion){state.changedGrassFrames++;break;}}
   state.raw.Add(s);
  }
  double? ReadGpu(bool keep)
  {
   try
   {
    uint count=FrameTimingManager.GetLatestTimings(1,timing);double? result=null;
    if(count>0){var t=timing[0];if(t.frameStartTimestamp!=0&&seenGpuStamps.Add(t.frameStartTimestamp)&&keep&&Finite(t.gpuFrameTime)&&t.gpuFrameTime>0)result=t.gpuFrameTime;}
    FrameTimingManager.CaptureFrameTimings();return result;
   }
   catch(Exception e){gpuReadError=e.GetType().Name+": "+e.Message;return null;}
  }
  RecorderSlot[] OpenRecorders()
  {
   var handles=new List<ProfilerRecorderHandle>();string discoveryError="";try{ProfilerRecorderHandle.GetAvailable(handles);}catch(Exception e){discoveryError=e.GetType().Name+": "+e.Message;}
   var result=new RecorderSlot[MarkerNames.Length];recorders=result;for(int i=0;i<result.Length;i++)
   {
    var slot=new RecorderSlot{description=new MarkerDescription{name=MarkerNames[i],status="unavailable: marker not returned by GetAvailable"}};result[i]=slot;
    if(discoveryError!=""){slot.description.status="unavailable: "+discoveryError;continue;}
    foreach(var handle in handles)
    {
     ProfilerRecorderDescription d;try{d=ProfilerRecorderHandle.GetDescription(handle);}catch{continue;}if(d.Name!=MarkerNames[i])continue;
     slot.description.category=d.Category.Name;slot.description.unit=d.UnitType.ToString();slot.description.dataType=d.DataType.ToString();
     if(slot.description.dataType!="Int64"&&slot.description.dataType!="Int32"&&slot.description.dataType!="UInt64"&&slot.description.dataType!="UInt32"){slot.description.status="unavailable: unsupported recorder data type "+slot.description.dataType;break;}
     try{slot.recorder=ProfilerRecorder.StartNew(d.Category,d.Name,1);slot.created=true;slot.description.available=slot.recorder.Valid;slot.description.status=slot.description.available?"registered; per-state samples determine data availability":"unavailable: invalid recorder";}catch(Exception e){slot.description.status="unavailable: "+e.GetType().Name+": "+e.Message;}
     break;
    }
   }
   return result;
  }
  sealed class RecorderSlot
  {
   internal ProfilerRecorder recorder;internal bool created;internal MarkerDescription description;
   internal long? Read(){if(!created||!recorder.Valid||recorder.Count==0)return null;long n=recorder.LastValue;if(description.unit=="TimeNanoseconds"&&n<=0)return null;return n;}
   internal void Release(){if(!created)return;try{recorder.Dispose();}finally{created=false;}}
  }
  void ReleaseRecorders(){if(recorders==null)return;foreach(var r in recorders)if(r!=null){try{r.Release();}catch(Exception e){if(report!=null)report.cleanupWarnings.Add("Recorder: "+e.Message);}}recorders=null;}
  Conditions CaptureConditions()
  {
   var c=controls.camera;var e=controls.environment;var f=e.Current;
   return new Conditions{screenWidth=Screen.width,screenHeight=Screen.height,cameraWidth=c.pixelWidth,cameraHeight=c.pixelHeight,quality=QualitySettings.GetQualityLevel(),vSync=QualitySettings.vSyncCount,targetFrameRate=Application.targetFrameRate,
    cameraPosition=V(c.transform.position),cameraRotation=Q(c.transform.rotation),fieldOfView=c.fieldOfView,orthographic=c.orthographic,orthographicSize=c.orthographicSize,renderScaleX=ScalableBufferManager.widthScaleFactor,renderScaleY=ScalableBufferManager.heightScaleFactor,
    environmentConfiguration=JsonUtility.ToJson(e),environmentLighting=f==null?null:new[]{f.Hours,f.SunIntensity,f.AmbientIntensity,f.WindDirection.x,f.WindDirection.y,f.WindSpeed,f.AmbientSky.r,f.AmbientSky.g,f.AmbientSky.b,f.AmbientEquator.r,f.AmbientEquator.g,f.AmbientEquator.b,f.AmbientGround.r,f.AmbientGround.g,f.AmbientGround.b,f.SunColor.r,f.SunColor.g,f.SunColor.b,f.SunDirection.x,f.SunDirection.y,f.SunDirection.z,f.FogDensity,f.Rain,f.Wetness},weather=f==null?"unavailable":f.WeatherName};
  }
  bool StableView(Conditions before)
  {
   var c=controls.camera;if(c==null)return false;var p=c.transform.position;var r=c.transform.rotation;
   return Screen.width==before.screenWidth&&Screen.height==before.screenHeight&&c.pixelWidth==before.cameraWidth&&c.pixelHeight==before.cameraHeight&&QualitySettings.GetQualityLevel()==before.quality&&QualitySettings.vSyncCount==before.vSync&&Application.targetFrameRate==before.targetFrameRate&&
    Mathf.Abs(p.x-before.cameraPosition[0])<.0001f&&Mathf.Abs(p.y-before.cameraPosition[1])<.0001f&&Mathf.Abs(p.z-before.cameraPosition[2])<.0001f&&
    Mathf.Abs(r.x-before.cameraRotation[0])<.00001f&&Mathf.Abs(r.y-before.cameraRotation[1])<.00001f&&Mathf.Abs(r.z-before.cameraRotation[2])<.00001f&&Mathf.Abs(r.w-before.cameraRotation[3])<.00001f&&Mathf.Abs(c.fieldOfView-before.fieldOfView)<.00001f&&c.orthographic==before.orthographic&&Mathf.Abs(c.orthographicSize-before.orthographicSize)<.00001f&&ScalableBufferManager.widthScaleFactor==before.renderScaleX&&ScalableBufferManager.heightScaleFactor==before.renderScaleY;
  }
  LayerReport[] CaptureLayers()
  {
   var planes=GeometryUtility.CalculateFrustumPlanes(controls.camera);
   return ownedLayers.Select(l=>
   {
    var materials=new HashSet<Material>();int enabled=0,frustum=0,observedVisible=0,submittedVertices=0,submittedTriangles=0;var keywords=new HashSet<bool>();var casterModes=new HashSet<string>();var shaderAppearance=new HashSet<float>();var shaderMotion=new HashSet<float>();bool artProperties=true;string profileJson=JsonUtility.ToJson(l.profile);
    foreach(var r in l.Renderers){if(r==null)continue;var m=r.sharedMaterial;if(m!=null){materials.Add(m);keywords.Add(m.IsKeywordEnabled("_GRASS_WIND"));if(m.HasProperty("_ArtMode")&&m.HasProperty("_WindMode")){shaderAppearance.Add(m.GetFloat("_ArtMode"));shaderMotion.Add(m.GetFloat("_WindMode"));}else artProperties=false;}casterModes.Add(r.shadowCastingMode.ToString());if(r.enabled&&!r.forceRenderingOff){enabled++;if(GeometryUtility.TestPlanesAABB(planes,r.bounds))frustum++;if(r.isVisible)observedVisible++;var filter=r.GetComponent<MeshFilter>();var mesh=filter==null?null:filter.sharedMesh;if(mesh!=null){submittedVertices+=mesh.vertexCount;submittedTriangles+=(int)mesh.GetIndexCount(0)/3;}}}
    return new LayerReport{name=l.name,mode=l.mode.ToString(),appearance=l.profile.appearance.ToString(),motion=l.profile.motion.ToString(),castShadows=l.castShadows,profile=l.profile.name,data=l.data.name,profileJson=profileJson,invariantProfileJson=InvariantProfile(profileJson),shaderArtProperties=artProperties,shaderAppearance=shaderAppearance.ToArray(),shaderMotion=shaderMotion.ToArray(),dataDensity=l.data.density,dataMaxDensity=l.data.maxDensity,dataSeed=l.data.seed,placementsFingerprint=Fingerprint(l.data),
     clumps=l.ClumpCount,ownedRenderers=l.BlockCount,ownedMaterials=l.OwnedMaterialCount,referencedSharedMaterials=materials.Count,fullGeneratedVertices=l.VertexCount,fullGeneratedTriangles=l.TriangleCount,enabledRendererMeshVertices=submittedVertices,enabledRendererMeshTriangles=submittedTriangles,
     enabledRenderers=enabled,frustumCandidates=frustum,rendererIsVisibleObservations=observedVisible,windKeywords=keywords.ToArray(),shadowCasterModes=casterModes.ToArray()};
   }).ToArray();
  }
  static void CheckPassModes(int state,LayerReport[] layers)
  {
   foreach(var l in layers){var expectedAppearance=state==3?GrassAppearance.Environment:GrassAppearance.Tutorial;if(l.appearance!=expectedAppearance.ToString()||l.motion!=GrassMotion.CombinedColor.ToString()||!l.shaderArtProperties||l.shaderAppearance.Any(v=>Mathf.Abs(v-(int)expectedAppearance)>.001f)||l.shaderMotion.Any(v=>Mathf.Abs(v-(int)GrassMotion.CombinedColor)>.001f))throw new InvalidOperationException("实际Profile/材质配色或运动路线不匹配。");if(state==0&&l.enabledRenderers!=0)throw new InvalidOperationException("S0仍有草Renderer启用。");if(state<3&&l.shadowCasterModes.Any(v=>v!="Off"))throw new InvalidOperationException("无投影态仍存在草Caster。");if(state==3&&l.shadowCasterModes.Any(v=>v!="TwoSided"))throw new InvalidOperationException("最终态未启用实际草Caster。");if(state==1&&l.windKeywords.Any(v=>v))throw new InvalidOperationException("S1仍执行_GRASS_WIND变体。");if(state>=2&&l.windKeywords.Any(v=>!v))throw new InvalidOperationException("风动态有静态变体。");}
  }
  void CheckConditions(StateReport state)
  {
   state.sameWithinState=state.changedViewFrames==0&&state.changedGrassFrames==0&&JsonConvert.SerializeObject(state.before)==JsonConvert.SerializeObject(state.after)&&SameGeometry(state.layers,state.afterLayers);
   if(report.states.Count>1){var baseline=report.states[0];state.sameAsS0=JsonConvert.SerializeObject(baseline.before)==JsonConvert.SerializeObject(state.before)&&SameGeometry(baseline.layers,state.layers,true);}else state.sameAsS0=true;
  }
  static string InvariantProfile(string json){var value=Newtonsoft.Json.Linq.JObject.Parse(json);value.Remove("appearance");value.Remove("motion");return value.ToString(Formatting.None);}
  static bool SameGeometry(LayerReport[] a,LayerReport[] b,bool expectedArtChange=false)
  {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if((expectedArtChange?a[i].invariantProfileJson!=b[i].invariantProfileJson:a[i].profileJson!=b[i].profileJson)||a[i].data!=b[i].data||a[i].dataDensity!=b[i].dataDensity||a[i].dataMaxDensity!=b[i].dataMaxDensity||a[i].dataSeed!=b[i].dataSeed||a[i].placementsFingerprint!=b[i].placementsFingerprint||a[i].clumps!=b[i].clumps||a[i].fullGeneratedVertices!=b[i].fullGeneratedVertices||a[i].fullGeneratedTriangles!=b[i].fullGeneratedTriangles)return false;return true;}
  static string Fingerprint(GrassPatchDataSO data)
  {
   ulong hash=14695981039346656037ul;
   unchecked{foreach(var p in data.placements??Array.Empty<GrassPlacement>()){hash=(hash^(uint)p.surfaceIndex)*1099511628211ul;hash=(hash^(uint)p.cellX)*1099511628211ul;hash=(hash^(uint)p.cellZ)*1099511628211ul;hash=(hash^(uint)p.localRoot.x.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.localRoot.y.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.localRoot.z.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.height.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.width.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.yaw.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.phase.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.worldRoot.x.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.worldRoot.y.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.worldRoot.z.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.localNormal.x.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.localNormal.y.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.localNormal.z.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.worldNormal.x.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.worldNormal.y.GetHashCode())*1099511628211ul;hash=(hash^(uint)p.worldNormal.z.GetHashCode())*1099511628211ul;}}
   return hash.ToString("X16");
  }
  public void Abort(){if(!IsRunning)return;if(report!=null)report.stopReason="aborted";var running=routine;routine=null;try{if(running!=null)StopCoroutine(running);}finally{Finish();}}
  void OnDisable(){Abort();}
  void OnDestroy(){Abort();}
  void Finish()
  {
   if(!IsRunning)return;IsRunning=false;routine=null;
   try
   {
    RestoreBorrow();ReleaseRecorders();
    if(report!=null){report.finishedUtc=DateTime.UtcNow.ToString("o");report.gpuReadError=gpuReadError;report.comparable=report.completed&&report.states.Count==4&&report.states.All(s=>s.sameWithinState&&s.sameAsS0);foreach(var state in report.states)Summarize(state);
     Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));if(File.Exists(ResultPath))throw new IOException("结果路径被其他写者占用，未覆盖。");File.WriteAllText(ResultPath,JsonConvert.SerializeObject(report,Formatting.Indented));}
    Status=report!=null&&report.completed?"四态采样完成；同条件="+report.comparable+"；GPU未取得时为null。":"采样已中止；已归还借用并写有限结果。";
   }
   catch(Exception e){Status="采样收尾失败："+e.Message;}
   finally{try{RestoreBorrow();}finally{ReleaseRecorders();}}
  }
  void RestoreBorrow()
  {
   if(!borrowed)return;borrowed=false;
   bool ownModes=controls!=null&&controls.CurrentMode==lastMode&&controls.Shadows==lastShadows&&beforeLayers.All(b=>b.layer==null||(b.layer.mode==lastMode&&b.layer.castShadows==lastShadows));
   if(ownModes){controls.SetMode(previousMode,previousShadows);foreach(var b in beforeLayers)if(b.layer!=null)b.layer.SetMode(b.mode,b.shadows);}
   else if(report!=null)report.cleanupWarnings.Add("草态已被外部修改；未用旧快照覆盖。");
   bool sameProfiles=beforeLayers.All(b=>b.layer==null||ReferenceEquals(b.layer.profile,b.profile));
   if(controls!=null&&sameProfiles&&controls.CurrentAppearance==lastAppearance)controls.SetAppearance(previousAppearance);
   else if(report!=null)report.cleanupWarnings.Add("配色路线或运行副本已被外部修改；未恢复旧配色。");
   if(controls!=null&&sameProfiles&&controls.CurrentMotion==lastMotion)controls.SetMotion(previousMotion);
   else if(report!=null)report.cleanupWarnings.Add("运动路线或运行副本已被外部修改；未恢复旧运动。");
   if(controls!=null){if(!controls.showPanel)controls.showPanel=previousPanel;if(!controls.acceptKeyboardInput)controls.acceptKeyboardInput=previousInput;}
  }
  void Summarize(StateReport state)
  {
   state.frameMilliseconds=Summary.Of(state.raw.Select(v=>(double?)v.frameMilliseconds));state.frameMilliseconds.source="Time.unscaledDeltaTime";state.frameMilliseconds.unit="ms";state.gpuMilliseconds=Summary.Of(state.raw.Select(v=>v.gpuMilliseconds));state.gpuMilliseconds.source="FrameTimingManager.gpuFrameTime: unique timestamp, finite and positive only";state.gpuMilliseconds.unit="ms";
   state.profiler=new Summary[MarkerNames.Length];for(int i=0;i<MarkerNames.Length;i++){int n=i;state.profiler[i]=Summary.Of(state.raw.Select(v=>Value(v,n)));state.profiler[i].source=report.markers[i].category+"/"+report.markers[i].name;state.profiler[i].unit=report.markers[i].unit;
    if(report.markers[i].unit=="TimeNanoseconds"){state.profiler[i].milliseconds=Summary.Of(state.raw.Select(v=>{var value=Value(v,n);return value.HasValue?(double?)(value.Value/1000000d):null;}));state.profiler[i].milliseconds.unit="ms";state.profiler[i].milliseconds.source=state.profiler[i].source+(i==7?" / 1e6; GrassLayer.BeginCamera scoped elapsed sum, may include work/waits inside scope; not GPU or total effective CPU":" / 1e6; total including waits");}}
  }
  static double? Value(FrameSample s,int i){switch(i){case 0:return s.mainThreadRaw;case 1:return s.renderThreadRaw;case 2:return s.gcBytesRaw;case 3:return s.drawCallsRaw;case 4:return s.setPassRaw;case 5:return s.trianglesRaw;case 6:return s.verticesRaw;default:return s.grassCameraNs;}}
  static float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};static float[] Q(Quaternion q)=>new[]{q.x,q.y,q.z,q.w};static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
  sealed class LayerBorrow{internal GrassLayer layer;internal GrassLayerProfileSO profile;internal GrassWindMode mode;internal bool shadows;}
  sealed class RunReport
  {
   public string startedUtc,finishedUtc,scene,unityVersion,graphicsDevice,graphicsApi,processor;public bool editor,completed,comparable;public float warmupSeconds,sampleSeconds;public string stopReason="interrupted",error,gpuReadError;
   public string scope="Independent GrassLab; no main-scene60FPS conclusion. CPU marker durations include waits; never add main+render totals. Raw recorder values are latest completed samples, not synchronized GPU frame pairs.";
   public string geometrySemantics="fullGenerated counts describe saved near geometry; enabledRendererMesh counts inspect currently assigned meshes and may include off-frustum geometry. Frustum candidates and Renderer.isVisible are observations, not GPU draw/occlusion proof.";
   public string measurementOverhead="Single coroutine plus eight recorders and FrameTimingManager requests; collections preallocated. The sampler itself can affect CPU/GC; no zero-overhead claim. Caller must keep recordings/builds/otherGPUloads separate.";
   public MarkerDescription[] markers;public List<StateReport> states=new List<StateReport>();public List<string> cleanupWarnings=new List<string>();
  }
  sealed class MarkerDescription{public string name,category,unit,dataType,status;public bool available;}
  sealed class StateReport
  {
   public string name,mode,appearance,motion;public bool shadows,completed,sameWithinState,sameAsS0;public Conditions before,after;public LayerReport[] layers,afterLayers;
   public double actualSeconds,environmentSecondsStart,environmentSecondsEnd;public int changedViewFrames,changedGrassFrames;public Summary frameMilliseconds,gpuMilliseconds;public Summary[] profiler;public List<FrameSample> raw;
  }
  sealed class Conditions
  {
   public int screenWidth,screenHeight,cameraWidth,cameraHeight,quality,vSync,targetFrameRate;public float[] cameraPosition,cameraRotation;public float fieldOfView,orthographicSize,renderScaleX,renderScaleY;public bool orthographic;public string environmentConfiguration,weather;public double[] environmentLighting;
  }
  sealed class LayerReport
  {
   public string name,mode,appearance,motion,profile,data,profileJson,invariantProfileJson,placementsFingerprint;public bool castShadows,shaderArtProperties;public float[] shaderAppearance,shaderMotion;public float dataDensity,dataMaxDensity;public int dataSeed,clumps,ownedRenderers,ownedMaterials,referencedSharedMaterials,fullGeneratedVertices,fullGeneratedTriangles,enabledRendererMeshVertices,enabledRendererMeshTriangles,enabledRenderers,frustumCandidates,rendererIsVisibleObservations;public bool[] windKeywords;public string[] shadowCasterModes;
  }
  struct FrameSample{public int frame;public double frameMilliseconds;public long? mainThreadRaw,renderThreadRaw,gcBytesRaw,drawCallsRaw,setPassRaw,trianglesRaw,verticesRaw,grassCameraNs;public double? gpuMilliseconds;}
  sealed class Summary
  {
   public string source,unit,status;public int count;public double? p50,p95,p99;public Summary milliseconds;
   internal static Summary Of(IEnumerable<double?> input){var values=input.Where(v=>v.HasValue&&Finite(v.Value)).Select(v=>v.Value).OrderBy(v=>v).ToArray();return new Summary{count=values.Length,status=values.Length==0?"unavailable: no valid samples":"available",p50=Percentile(values,.5),p95=Percentile(values,.95),p99=Percentile(values,.99)};}
   static double? Percentile(double[] values,double p){if(values.Length==0)return null;double x=(values.Length-1)*p;int low=(int)Math.Floor(x),high=(int)Math.Ceiling(x);return values[low]+(values[high]-values[low])*(x-low);}
  }
 }
}
