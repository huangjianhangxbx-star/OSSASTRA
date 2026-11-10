// Finite recipe switcher. Only EnvironmentRoot submits world lighting. No scene search,
// per-frame material clone, Light/RenderSettings writes, weather root or author mutation.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Ossastra.EnvironmentSimulation;
using Ossastra.EnvironmentSimulation.Rendering;
using Ossastra.SceneVisibility;
using Ossastra.MapResearch;
using Ossastra.Characters;
namespace Ossastra.WorldRendering.Stylization
{
 [DefaultExecutionOrder(1000),DisallowMultipleComponent]
 public sealed class SurfaceLightingRecipe:MonoBehaviour
 {
  public EnvironmentRoot environment;
  public EnvironmentSkyBinding skyBinding;
  public WorldLookController look;
  public LookDevFiniteToon finiteToon;
  public SceneVisibilityRoot visibility;
  public MapResearchRoot map;
  public SurfaceLightingRecipeSO recipe;
  public EnvironmentMaterialResponse groundRegistration;
  public GameObject windowGroup,windowEmission;
  public Light windowLight;
  public GameObject[] previousOnlyObjects=Array.Empty<GameObject>();
  public SurfaceLightingState initialState=SurfaceLightingState.BlueComplete;
  public bool showPanel=true,expanded,advanced;
  public SurfaceLightingState State{get;private set;}
  public string Status{get;private set;}="尚未接管";
  public StylizedLookProfileSO RuntimeProfile=>runtime;
  public bool HasLease=>leased;
  static readonly Dictionary<EnvironmentRoot,SurfaceLightingRecipe> owners=new Dictionary<EnvironmentRoot,SurfaceLightingRecipe>();
  EnvironmentRoot boundEnvironment;EnvironmentSkyBinding boundSky;WorldLookController boundLook;LookDevFiniteToon boundToon;
  EnvironmentMaterialResponse boundGroundRegistration;bool priorGroundEnabled,writtenGroundEnabled;
  SurfaceLightingRecipeSO boundRecipe;Camera boundCamera;GameObject boundWindowGroup,boundWindowEmission;Light boundWindowLight;GameObject[] boundPreviousOnly=Array.Empty<GameObject>();
  EnvironmentState beforeEnvironment,writtenEnvironment;bool priorSkyOverride,writtenSkyOverride;
  SceneEnvironmentProfileSO priorSceneProfile,writtenSceneProfile;bool priorSceneEnabled,writtenSceneEnabled;
  StylizedLookProfileSO priorLook,runtime;
  LookDevToonVariant priorToon,writtenToon;bool priorOutline,writtenOutline;float priorWidth,writtenWidth;Color priorDark,writtenDark;
  readonly List<ObjectState> objects=new List<ObjectState>();bool priorLight,writtenLight,leased,pendingInitial;
  bool startupCameraHooked,startupCameraObserved;Camera startupRenderedCamera;
  CharacterControlCoordinator capturedCoordinator;MapResearchRoot capturedMap;bool pointerCaptured;
  Font font;GUISkin skin;int boundObjectCount,boundSlotCount;
  Rect Panel=>new Rect(Mathf.Max(8,Screen.width*.34f),110,440,expanded?(advanced?430:190):32);
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetOwners(){owners.Clear();}
  void OnEnable()
  {
   CancelStartupCameraWait();startupCameraObserved=false;startupRenderedCamera=null;pendingInitial=Application.isPlaying;
   if(pendingInitial){RenderPipelineManager.endCameraRendering+=OnStartupCameraRendered;startupCameraHooked=true;}
  }
  bool IsStartupCamera(Camera camera)
  {
   return camera!=null&&environment!=null&&look!=null&&environment.gameObject.scene==gameObject.scene&&look.gameObject.scene==gameObject.scene&&camera.gameObject.scene==gameObject.scene&&environment.gameplayCamera==camera&&look.gameplayCamera==camera;
  }
  void OnStartupCameraRendered(ScriptableRenderContext context,Camera camera)
  {
   if(!pendingInitial||!Application.isPlaying||!isActiveAndEnabled){CancelStartupCameraWait();return;}
   if(!IsStartupCamera(camera))return;
   startupRenderedCamera=camera;startupCameraObserved=true;CancelStartupCameraWait();
  }
  void CancelStartupCameraWait(){if(startupCameraHooked)RenderPipelineManager.endCameraRendering-=OnStartupCameraRendered;startupCameraHooked=false;}
  public bool ValidateState(SurfaceLightingState state,out string error)
  {
   error="";if(state<SurfaceLightingState.Previous||state>SurfaceLightingState.BlueComplete){error="未知完整状态";return false;}
   if(environment==null||look==null||finiteToon==null||skyBinding==null||recipe==null||visibility==null||map==null){error="缺少明确配方/环境/天空/材质/控制引用";return false;}
   if(environment.gameObject.scene!=gameObject.scene||look.gameObject.scene!=gameObject.scene||finiteToon.gameObject.scene!=gameObject.scene||skyBinding.gameObject.scene!=gameObject.scene||visibility.gameObject.scene!=gameObject.scene||map.gameObject.scene!=gameObject.scene||skyBinding.environment!=environment||environment.gameplayCamera==null||environment.gameplayCamera!=look.gameplayCamera||environment.gameplayCamera.gameObject.scene!=gameObject.scene){error="配方仅服务同一场景、同一明确相机与环境根";return false;}
   if(previousOnlyObjects!=null){if(previousOnlyObjects.Length>4){error="历史对照最多4个明确自有亮源";return false;}foreach(var g in previousOnlyObjects)if(g==null||g.scene!=gameObject.scene||g.GetComponent<StylizedLookEmitter>()==null){error="历史对照只允许本场自有HDR亮源对象";return false;}}
   var style=state==SurfaceLightingState.Previous?recipe.previous:state==SurfaceLightingState.MaterialLighting?recipe.neutral:recipe.blue;
   if(style==null){error="完整状态缺少风格配置";return false;}
   if(state!=SurfaceLightingState.Previous)
   {
    var day=state==SurfaceLightingState.MaterialLighting?recipe.neutralDay:recipe.blueDay;var settings=state==SurfaceLightingState.MaterialLighting?recipe.neutralLighting:recipe.blueLighting;
    if(day==null||settings==null||!settings.Valid||!ValidDay(day)){error="缺少或无效的日光副本/照明参数";return false;}
    if(style.pixelSize!=1||style.bandsEnabled||style.themeMode!=StylizedThemeMode.LightingLed||!style.themeEnabled){error="B/C作者起点必须是无像素、无分档的LightingLed";return false;}
    if(state==SurfaceLightingState.MaterialLighting&&(style.airEnabled||style.bloomEnabled||style.ledEnvironmentStrength!=0||style.ledActorStrength!=0)){error="B必须无主题增益、空气或Bloom";return false;}
    if(state==SurfaceLightingState.BlueComplete&&(!style.airEnabled||!style.bloomEnabled||!style.visibleSourceBloom)){error="C必须使用空气与可见源Bloom";return false;}
    if(state==SurfaceLightingState.BlueComplete&&recipe.blueSceneEnvironment!=null&&!ValidGroundRegistration()){error="C基础色档需要同场景明确Ground登记";return false;}
    if(windowGroup==null||windowEmission==null||windowLight==null||windowGroup.scene!=gameObject.scene||!windowEmission.transform.IsChildOf(windowGroup.transform)||!windowLight.transform.IsChildOf(windowGroup.transform)){error="正常窗灯须有同场景独立灯芯与实光";return false;}
   }
   return true;
  }
  bool ValidGroundRegistration(){if(groundRegistration==null||groundRegistration.gameObject.scene!=gameObject.scene||groundRegistration.role!=EnvironmentMaterialRole.Ground||groundRegistration.renderers==null||groundRegistration.renderers.Length==0)return false;foreach(var r in groundRegistration.renderers)if(r==null||r.gameObject.scene!=gameObject.scene||!r.transform.IsChildOf(groundRegistration.transform))return false;return true;}
  static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
  static bool ValidColor(Color c)=>Finite(c.r)&&Finite(c.g)&&Finite(c.b)&&Finite(c.a)&&c.r>=0&&c.g>=0&&c.b>=0&&c.a>=0;
  static bool ValidDay(DayCycleProfileSO p)=>p!=null&&Finite(p.daySunIntensity)&&p.daySunIntensity>=0&&Finite(p.nightSunIntensity)&&p.nightSunIntensity>=0&&Finite(p.dayFillIntensity)&&p.dayFillIntensity>=0&&Finite(p.nightFillIntensity)&&p.nightFillIntensity>=0&&Finite(p.dayAmbientIntensity)&&p.dayAmbientIntensity>=0&&Finite(p.nightAmbientIntensity)&&p.nightAmbientIntensity>=0&&Finite(p.daySkyIntensity)&&p.daySkyIntensity>=0&&Finite(p.nightSkyIntensity)&&p.nightSkyIntensity>=0&&ValidColor(p.daySunColor)&&ValidColor(p.nightSunColor)&&ValidColor(p.sunsetSunColor)&&ValidColor(p.dayAmbientSky)&&ValidColor(p.nightAmbientSky)&&ValidColor(p.dayAmbientEquator)&&ValidColor(p.nightAmbientEquator)&&ValidColor(p.dayAmbientGround)&&ValidColor(p.nightAmbientGround)&&ValidColor(p.daySkyZenith)&&ValidColor(p.nightSkyZenith)&&ValidColor(p.daySkyHorizon)&&ValidColor(p.nightSkyHorizon)&&(p.sunlightResponse==null||Finite(p.sunlightResponse.Evaluate(1)))&&(p.ambientResponse==null||Finite(p.ambientResponse.Evaluate(1)));
  static bool SameObjects(GameObject[] a,GameObject[] b){int n=a!=null?a.Length:0;if(b==null||n!=b.Length)return false;for(int i=0;i<n;i++)if(a[i]!=b[i])return false;return true;}
  bool OwnsInputs()
  {
   return leased&&groundRegistration==boundGroundRegistration&&environment==boundEnvironment&&look==boundLook&&finiteToon==boundToon&&skyBinding==boundSky&&recipe==boundRecipe&&boundEnvironment!=null&&boundLook!=null&&boundToon!=null&&boundSky!=null&&boundEnvironment.gameplayCamera==boundCamera&&boundLook.gameplayCamera==boundCamera&&windowGroup==boundWindowGroup&&windowEmission==boundWindowEmission&&windowLight==boundWindowLight&&SameObjects(previousOnlyObjects,boundPreviousOnly);
  }
  bool OwnsWritten()
  {
   if(!OwnsInputs()||(boundGroundRegistration!=null&&boundGroundRegistration.enabled!=writtenGroundEnabled)||boundLook.stylizedLook!=runtime||!writtenEnvironment.Matches(boundEnvironment)||boundSky.overrideSkyColors!=writtenSkyOverride||boundSky.sceneEnvironmentProfile!=writtenSceneProfile||boundSky.sceneEnvironmentEnabled!=writtenSceneEnabled||boundToon.Variant!=writtenToon||boundToon.OutlineEnabled!=writtenOutline||boundToon.OutlineOutputPixels!=writtenWidth||boundToon.OutlineDarkLinear.gamma!=writtenDark)return false;
   foreach(var o in objects)if(!o.Matches())return false;
   return windowLight==null||windowLight.enabled==writtenLight;
  }
  void Capture()
  {
   boundGroundRegistration=groundRegistration;priorGroundEnabled=writtenGroundEnabled=groundRegistration!=null&&groundRegistration.enabled;
   boundEnvironment=environment;boundSky=skyBinding;boundLook=look;boundToon=finiteToon;boundRecipe=recipe;boundCamera=environment.gameplayCamera;boundWindowGroup=windowGroup;boundWindowEmission=windowEmission;boundWindowLight=windowLight;boundPreviousOnly=previousOnlyObjects!=null?(GameObject[])previousOnlyObjects.Clone():Array.Empty<GameObject>();
   beforeEnvironment=EnvironmentState.Capture(environment);writtenEnvironment=beforeEnvironment;priorSkyOverride=writtenSkyOverride=skyBinding.overrideSkyColors;priorSceneProfile=writtenSceneProfile=skyBinding.sceneEnvironmentProfile;priorSceneEnabled=writtenSceneEnabled=skyBinding.sceneEnvironmentEnabled;priorLook=look.stylizedLook;
   priorToon=writtenToon=finiteToon.Variant;priorOutline=writtenOutline=finiteToon.OutlineEnabled;priorWidth=writtenWidth=finiteToon.OutlineOutputPixels;priorDark=writtenDark=finiteToon.OutlineDarkLinear.gamma;
   objects.Clear();AddObject(windowGroup);AddObject(windowEmission);if(previousOnlyObjects!=null)foreach(var o in previousOnlyObjects)AddObject(o);priorLight=writtenLight=windowLight!=null&&windowLight.enabled;
   var rendered=new HashSet<Renderer>();foreach(var binding in finiteToon.Bindings)if(binding!=null&&binding.renderer!=null)rendered.Add(binding.renderer);boundObjectCount=rendered.Count;boundSlotCount=finiteToon.Bindings.Count;owners[environment]=this;leased=true;
  }
  void AddObject(GameObject value){if(value==null)return;foreach(var o in objects)if(o.value==value)return;objects.Add(new ObjectState(value));}
  void SetObject(GameObject value,bool enabled){if(value==null)return;foreach(var o in objects)if(o.value==value){o.Write(enabled);return;}}
  public bool TrySetState(SurfaceLightingState state,out string error)
  {
   error="";if(!Application.isPlaying){error=Status="只在Play切换运行副本";return false;}
   if(!ValidateState(state,out error)){Status=error;return false;}
   if(leased&&!OwnsWritten()){error=Status="配方控制已由其他写入者改变；保留现场，先释放再接管";return false;}
   if(owners.TryGetValue(environment,out var owner)&&owner!=null&&owner!=this){error=Status="环境已被另一候选配方占用";return false;}
   bool newlyAcquired=!leased;if(newlyAcquired)Capture();else owners[environment]=this;
   var source=state==SurfaceLightingState.Previous?recipe.previous:state==SurfaceLightingState.MaterialLighting?recipe.neutral:recipe.blue;
   var candidate=Instantiate(source);candidate.name=source.name+" recipe runtime";candidate.hideFlags=HideFlags.HideAndDontSave;
   var oldEnvironment=EnvironmentState.Capture(environment);bool oldSkyOverride=skyBinding.overrideSkyColors;var oldSceneProfile=skyBinding.sceneEnvironmentProfile;bool oldSceneEnabled=skyBinding.sceneEnvironmentEnabled;bool oldGroundEnabled=groundRegistration!=null&&groundRegistration.enabled;var oldToon=finiteToon.Variant;bool oldOutline=finiteToon.OutlineEnabled;float oldWidth=finiteToon.OutlineOutputPixels;Color oldDark=finiteToon.OutlineDarkLinear.gamma;var oldFrame=environment.Current;
   var backend=state==SurfaceLightingState.Previous?priorToon:recipe.materialBackend;
   if(!finiteToon.TrySetVariant(backend,out error)){DestroyOwned(candidate);if(newlyAcquired)Release();Status=error;return false;}
   if(state==SurfaceLightingState.Previous){beforeEnvironment.Apply(environment);skyBinding.overrideSkyColors=priorSkyOverride;finiteToon.SetOutline(priorOutline,priorWidth);finiteToon.SetOutlineTheme(priorDark);}
   else{var settings=state==SurfaceLightingState.MaterialLighting?recipe.neutralLighting:recipe.blueLighting;ApplyEnvironment(environment,state==SurfaceLightingState.MaterialLighting?recipe.neutralDay:recipe.blueDay,settings);skyBinding.overrideSkyColors=false;finiteToon.SetOutline(recipe.outline,recipe.outlinePixels);finiteToon.SetOutlineTheme(recipe.outlineDarkSrgb);}
   var sceneProfile=state==SurfaceLightingState.BlueComplete&&recipe.blueSceneEnvironment!=null?recipe.blueSceneEnvironment:priorSceneProfile;skyBinding.sceneEnvironmentProfile=sceneProfile;skyBinding.sceneEnvironmentEnabled=state==SurfaceLightingState.BlueComplete&&recipe.blueSceneEnvironment!=null?true:priorSceneEnabled;
   if(groundRegistration!=null)groundRegistration.enabled=state==SurfaceLightingState.BlueComplete?true:priorGroundEnabled;
   environment.RefreshNow();
   if(environment.Current==null||ReferenceEquals(environment.Current,oldFrame)||(state!=SurfaceLightingState.Previous&&!environment.Current.Enabled))
   {var attempted=EnvironmentState.Capture(environment);EnvironmentState.RestoreOwned(environment,oldEnvironment,attempted);if(skyBinding.overrideSkyColors==(state==SurfaceLightingState.Previous?priorSkyOverride:false))skyBinding.overrideSkyColors=oldSkyOverride;if(skyBinding.sceneEnvironmentProfile==sceneProfile)skyBinding.sceneEnvironmentProfile=oldSceneProfile;if(skyBinding.sceneEnvironmentEnabled==(state==SurfaceLightingState.BlueComplete&&recipe.blueSceneEnvironment!=null?true:priorSceneEnabled))skyBinding.sceneEnvironmentEnabled=oldSceneEnabled;if(groundRegistration!=null&&groundRegistration.enabled==(state==SurfaceLightingState.BlueComplete?true:priorGroundEnabled))groundRegistration.enabled=oldGroundEnabled;environment.RefreshNow();if(finiteToon.Variant==backend){finiteToon.TrySetVariant(oldToon,out _);finiteToon.SetOutline(oldOutline,oldWidth);finiteToon.SetOutlineTheme(oldDark);}DestroyOwned(candidate);if(newlyAcquired)Release();error=Status="环境根未生成新有效帧，已撤销本次切档";return false;}
   var old=runtime;runtime=candidate;look.stylizedLook=runtime;DestroyOwned(old);
   foreach(var o in objects)o.Write(state==SurfaceLightingState.Previous?o.before:o.value==windowGroup||o.value==windowEmission);
   if(windowLight!=null)windowLight.enabled=state==SurfaceLightingState.Previous?priorLight:true;
   writtenGroundEnabled=groundRegistration!=null&&groundRegistration.enabled;writtenLight=windowLight!=null&&windowLight.enabled;writtenEnvironment=EnvironmentState.Capture(environment);writtenSkyOverride=skyBinding.overrideSkyColors;writtenSceneProfile=skyBinding.sceneEnvironmentProfile;writtenSceneEnabled=skyBinding.sceneEnvironmentEnabled;writtenToon=finiteToon.Variant;writtenOutline=finiteToon.OutlineEnabled;writtenWidth=finiteToon.OutlineOutputPixels;writtenDark=finiteToon.OutlineDarkLinear.gamma;
   State=state;Status="完整状态已切换；不改作者参数";return true;
  }
  static void ApplyEnvironment(EnvironmentRoot e,DayCycleProfileSO day,SurfaceLightingEnvironmentSettings p)
  {
   e.dayCycle=day;e.effectsEnabled=true;e.contribution=EnvironmentContribution.Combined;e.timeMode=EnvironmentTimeMode.Manual;e.clockRunning=false;e.timeHours=12;e.manualSun=true;e.sunAzimuthDegrees=p.azimuth;e.sunElevationDegrees=p.elevation;e.sunIntensityMultiplier=p.sunMultiplier;e.sunTemperatureOffset=0;e.ambientMultiplier=p.ambientMultiplier;e.skyMultiplier=p.skyMultiplier;e.shadowStrength=p.shadowStrength;e.automaticWeather=false;e.cloudCoverageOverride=0;e.cloudDensityOverride=0;e.cloudShadowStrengthOverride=0;e.fogMultiplier=0;
  }
  public bool TrySetMaterialDiagnostic(LookDevToonVariant variant,out string error)
  {
   error="";if(State==SurfaceLightingState.Previous||!OwnsWritten()){error=Status="同灯材质对照需要当前B/C仍由本配方占用";return false;}
   if(!finiteToon.TrySetVariant(variant,out error)){Status=error;return false;}writtenToon=finiteToon.Variant;writtenOutline=finiteToon.OutlineEnabled;writtenWidth=finiteToon.OutlineOutputPixels;writtenDark=finiteToon.OutlineDarkLinear.gamma;Status="同灯材质诊断（非完整预设）";return true;
  }
  public bool SetLightContributions(bool main,bool fill)
  {
   if(State==SurfaceLightingState.Previous||!OwnsWritten())return false;var p=State==SurfaceLightingState.MaterialLighting?recipe.neutralLighting:recipe.blueLighting;environment.sunIntensityMultiplier=main?p.sunMultiplier:0;environment.ambientMultiplier=fill?p.ambientMultiplier:0;writtenEnvironment=EnvironmentState.Capture(environment);environment.RefreshNow();Status="照明分项诊断（非完整预设）";return true;
  }
  public bool SetSolarDiagnostic(double hours,float elevation)
  {
   if(State==SurfaceLightingState.Previous||!OwnsWritten()||double.IsNaN(hours)||double.IsInfinity(hours)||float.IsNaN(elevation)||float.IsInfinity(elevation))return false;environment.timeHours=EnvironmentClock.WrapHours(hours);environment.sunElevationDegrees=Mathf.Clamp(elevation,-89,89);writtenEnvironment=EnvironmentState.Capture(environment);environment.RefreshNow();Status="时刻/太阳诊断（非完整预设）";return true;
  }
  public bool SetWindow(bool realLight,bool emission)
  {
   if(State==SurfaceLightingState.Previous||!OwnsWritten())return false;SetObject(windowEmission,emission);if(windowLight!=null)windowLight.enabled=realLight;writtenLight=windowLight!=null&&windowLight.enabled;Status="窗灯分项诊断（非完整预设）";return true;
  }
  public void SetPixel(bool enabled){if(State==SurfaceLightingState.BlueComplete&&runtime!=null&&look!=null&&look.stylizedLook==runtime)runtime.pixelSize=enabled?2:1;}
  public void SetBloom(bool enabled){if(State==SurfaceLightingState.BlueComplete&&runtime!=null&&look!=null&&look.stylizedLook==runtime)runtime.bloomEnabled=enabled;}
  public void Release()
  {
   CapturePointer(false);pendingInitial=false;startupCameraObserved=false;startupRenderedCamera=null;CancelStartupCameraWait();if(!leased)return;
   if(boundGroundRegistration!=null&&boundGroundRegistration.enabled==writtenGroundEnabled)boundGroundRegistration.enabled=priorGroundEnabled;
   if(boundSky!=null){if(boundSky.overrideSkyColors==writtenSkyOverride)boundSky.overrideSkyColors=priorSkyOverride;if(boundSky.sceneEnvironmentProfile==writtenSceneProfile)boundSky.sceneEnvironmentProfile=priorSceneProfile;if(boundSky.sceneEnvironmentEnabled==writtenSceneEnabled)boundSky.sceneEnvironmentEnabled=priorSceneEnabled;}
   if(boundEnvironment!=null){EnvironmentState.RestoreOwned(boundEnvironment,beforeEnvironment,writtenEnvironment);boundEnvironment.RefreshNow();}
   if(boundLook!=null&&boundLook.stylizedLook==runtime)boundLook.stylizedLook=priorLook;
   if(boundToon!=null&&boundToon.Variant==writtenToon&&boundToon.OutlineEnabled==writtenOutline&&boundToon.OutlineOutputPixels==writtenWidth&&boundToon.OutlineDarkLinear.gamma==writtenDark){boundToon.TrySetVariant(priorToon,out _);boundToon.SetOutline(priorOutline,priorWidth);boundToon.SetOutlineTheme(priorDark);}
   foreach(var o in objects)o.Restore();objects.Clear();if(boundWindowLight!=null&&boundWindowLight.enabled==writtenLight)boundWindowLight.enabled=priorLight;
   if(!ReferenceEquals(boundEnvironment,null)&&owners.TryGetValue(boundEnvironment,out var owner)&&owner==this)owners.Remove(boundEnvironment);
   DestroyOwned(runtime);runtime=null;leased=false;boundGroundRegistration=null;boundEnvironment=null;boundSky=null;boundLook=null;boundToon=null;boundRecipe=null;boundCamera=null;boundWindowGroup=null;boundWindowEmission=null;boundWindowLight=null;boundPreviousOnly=Array.Empty<GameObject>();State=SurfaceLightingState.Previous;Status="已归还仍由本配方占有的引用与参数";
  }
  void Update()
  {
   if(!Application.isPlaying)return;if(leased&&!OwnsInputs()){Release();Status="显式环境/相机/配方引用改变，旧租约已归还";return;}
   if(pendingInitial&&startupCameraObserved){pendingInitial=false;bool ready=IsStartupCamera(startupRenderedCamera);startupCameraObserved=false;startupRenderedCamera=null;if(ready)TrySetState(initialState,out _);else Status="首次相机许可已失效；保留现场，未自动切档";}
   if(pointerCaptured&&(capturedMap!=map||capturedCoordinator!=(visibility!=null?visibility.coordinator:null)))CapturePointer(false);bool over=false;if(showPanel&&Mouse.current!=null){var p=Mouse.current.position.ReadValue();over=Panel.Contains(new Vector2(p.x,Screen.height-p.y));}if(over!=pointerCaptured)CapturePointer(over);
  }
  void CapturePointer(bool value)
  {
   if(!value){if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,false);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,false);capturedCoordinator=null;capturedMap=null;pointerCaptured=false;return;}
   capturedCoordinator=visibility!=null?visibility.coordinator:null;capturedMap=map;if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,true);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,true);pointerCaptured=true;
  }
  void EnsureSkin(){if(skin!=null)return;skin=Instantiate(GUI.skin);skin.hideFlags=HideFlags.HideAndDontSave;font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},14);if(font!=null){font.hideFlags=HideFlags.HideAndDontSave;skin.font=font;}skin.label.wordWrap=true;skin.label.fontSize=skin.button.fontSize=14;}
  void OnGUI()
  {
   if(!Application.isPlaying||!showPanel)return;EnsureSkin();var previous=GUI.skin;GUI.skin=skin;GUILayout.BeginArea(Panel,GUI.skin.box);try{
    if(GUILayout.Button((expanded?"收起":"展开")+" 卡通材质与光影 / "+StateLabel(State)))expanded=!expanded;if(!expanded)return;
    GUILayout.BeginHorizontal();if(GUILayout.Button("A 上轮原样"))TrySetState(SurfaceLightingState.Previous,out _);if(GUILayout.Button("B 材质与布光"))TrySetState(SurfaceLightingState.MaterialLighting,out _);if(GUILayout.Button("C 强蓝完整"))TrySetState(SurfaceLightingState.BlueComplete,out _);GUILayout.EndHorizontal();
    GUILayout.Label("实际后端 "+(finiteToon!=null?finiteToon.Variant.ToString():"缺失")+" / "+boundObjectCount+"物体 "+boundSlotCount+"槽 / "+(Application.isEditor?"Editor":"Player"));
    if(State==SurfaceLightingState.BlueComplete&&runtime!=null){bool pixel=GUILayout.Toggle(runtime.pixelSize==2,"2像素（默认关闭；HUD原分辨率）");SetPixel(pixel);}
    GUILayout.Label(Status);advanced=GUILayout.Toggle(advanced,"开发分项对照");if(advanced){GUILayout.BeginHorizontal();if(GUILayout.Button("原材质"))TrySetMaterialDiagnostic(LookDevToonVariant.Original,out _);if(GUILayout.Button("自主基准"))TrySetMaterialDiagnostic(LookDevToonVariant.ThreeBand,out _);if(finiteToon!=null&&finiteToon.RealToonAvailable&&GUILayout.Button("RealToon"))TrySetMaterialDiagnostic(LookDevToonVariant.RealToon,out _);GUILayout.EndHorizontal();GUILayout.BeginHorizontal();if(GUILayout.Button("主光"))SetLightContributions(true,false);if(GUILayout.Button("填充"))SetLightContributions(false,true);if(GUILayout.Button("组合"))SetLightContributions(true,true);GUILayout.EndHorizontal();GUILayout.BeginHorizontal();if(GUILayout.Button("窗灯全开"))SetWindow(true,true);if(GUILayout.Button("只灯芯"))SetWindow(false,true);if(GUILayout.Button("只实光"))SetWindow(true,false);GUILayout.EndHorizontal();if(State==SurfaceLightingState.BlueComplete&&runtime!=null)SetBloom(GUILayout.Toggle(runtime.bloomEnabled,"宽Bloom单项对照"));GUILayout.Label("完整状态比较含布光变化；同灯材质对照另由测试记录提供。参数只改运行副本。");}
   }finally{GUILayout.EndArea();GUI.skin=previous;}
  }
  static string StateLabel(SurfaceLightingState s)=>s==SurfaceLightingState.Previous?"A 上轮原样":s==SurfaceLightingState.MaterialLighting?"B 材质与布光":"C 强蓝完整";
  static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
  void OnDisable(){Release();}void OnDestroy(){Release();DestroyOwned(skin);DestroyOwned(font);}
  sealed class ObjectState{public readonly GameObject value;public readonly bool before;bool written;public ObjectState(GameObject g){value=g;before=written=g.activeSelf;}public bool Matches()=>value==null||value.activeSelf==written;public void Write(bool x){if(value!=null)value.SetActive(x);written=x;}public void Restore(){if(value!=null&&value.activeSelf==written)value.SetActive(before);}}
  struct EnvironmentState
  {
   DayCycleProfileSO day;EnvironmentContribution contribution;EnvironmentTimeMode timeMode;bool effects,clock,manual,automatic;double hours;float azimuth,elevation,sun,temperature,ambient,sky,shadow,cloudCoverage,cloudDensity,cloudShadow,fog;
   public static EnvironmentState Capture(EnvironmentRoot e)=>new EnvironmentState{day=e.dayCycle,contribution=e.contribution,timeMode=e.timeMode,effects=e.effectsEnabled,clock=e.clockRunning,manual=e.manualSun,automatic=e.automaticWeather,hours=e.timeHours,azimuth=e.sunAzimuthDegrees,elevation=e.sunElevationDegrees,sun=e.sunIntensityMultiplier,temperature=e.sunTemperatureOffset,ambient=e.ambientMultiplier,sky=e.skyMultiplier,shadow=e.shadowStrength,cloudCoverage=e.cloudCoverageOverride,cloudDensity=e.cloudDensityOverride,cloudShadow=e.cloudShadowStrengthOverride,fog=e.fogMultiplier};
   public bool Matches(EnvironmentRoot e){var s=Capture(e);return day==s.day&&contribution==s.contribution&&timeMode==s.timeMode&&effects==s.effects&&clock==s.clock&&manual==s.manual&&automatic==s.automatic&&(hours==s.hours||(clock&&timeMode==EnvironmentTimeMode.PreviewClock)||timeMode==EnvironmentTimeMode.External)&&azimuth==s.azimuth&&elevation==s.elevation&&sun==s.sun&&temperature==s.temperature&&ambient==s.ambient&&sky==s.sky&&shadow==s.shadow&&cloudCoverage==s.cloudCoverage&&cloudDensity==s.cloudDensity&&cloudShadow==s.cloudShadow&&fog==s.fog;}
   public void Apply(EnvironmentRoot e){e.dayCycle=day;e.contribution=contribution;e.timeMode=timeMode;e.effectsEnabled=effects;e.clockRunning=clock;e.manualSun=manual;e.automaticWeather=automatic;e.timeHours=hours;e.sunAzimuthDegrees=azimuth;e.sunElevationDegrees=elevation;e.sunIntensityMultiplier=sun;e.sunTemperatureOffset=temperature;e.ambientMultiplier=ambient;e.skyMultiplier=sky;e.shadowStrength=shadow;e.cloudCoverageOverride=cloudCoverage;e.cloudDensityOverride=cloudDensity;e.cloudShadowStrengthOverride=cloudShadow;e.fogMultiplier=fog;}
   public static void RestoreOwned(EnvironmentRoot e,EnvironmentState p,EnvironmentState w){if(e.dayCycle==w.day)e.dayCycle=p.day;if(e.contribution==w.contribution)e.contribution=p.contribution;if(e.timeMode==w.timeMode)e.timeMode=p.timeMode;if(e.effectsEnabled==w.effects)e.effectsEnabled=p.effects;if(e.clockRunning==w.clock)e.clockRunning=p.clock;if(e.manualSun==w.manual)e.manualSun=p.manual;if(e.automaticWeather==w.automatic)e.automaticWeather=p.automatic;if(e.timeHours==w.hours)e.timeHours=p.hours;if(e.sunAzimuthDegrees==w.azimuth)e.sunAzimuthDegrees=p.azimuth;if(e.sunElevationDegrees==w.elevation)e.sunElevationDegrees=p.elevation;if(e.sunIntensityMultiplier==w.sun)e.sunIntensityMultiplier=p.sun;if(e.sunTemperatureOffset==w.temperature)e.sunTemperatureOffset=p.temperature;if(e.ambientMultiplier==w.ambient)e.ambientMultiplier=p.ambient;if(e.skyMultiplier==w.sky)e.skyMultiplier=p.sky;if(e.shadowStrength==w.shadow)e.shadowStrength=p.shadow;if(e.cloudCoverageOverride==w.cloudCoverage)e.cloudCoverageOverride=p.cloudCoverage;if(e.cloudDensityOverride==w.cloudDensity)e.cloudDensityOverride=p.cloudDensity;if(e.cloudShadowStrengthOverride==w.cloudShadow)e.cloudShadowStrengthOverride=p.cloudShadow;if(e.fogMultiplier==w.fog)e.fogMultiplier=p.fog;}
  }
 }
}
