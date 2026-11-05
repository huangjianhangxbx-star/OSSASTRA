/* 功能：为明确主场提供可折叠uGUI环境模拟面板，通过EnvironmentRoot统一调节昼夜、日照、天空与天气。
 * 边界：不是正式HUD、不拥有时间或天气状态、不硬读设备按键、不暂停世界；所有参数调用原环境接口。
 * 协作：显式环境根、角色协调器与地图根；指针/拖动/下拉只捕获本面板owner的一份输入租约。
 * 生命周期：Canvas、可选动态中文字体均为本组件自有；禁用立即释放输入，销毁精确清理自有对象。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using Ossastra.EnvironmentSimulation;
using Ossastra.MapResearch;
using Ossastra.SceneVisibility;

namespace Ossastra.Development.Environment
{
 [DisallowMultipleComponent]
 public sealed class EnvironmentSimulationPanel:MonoBehaviour
 {
  [Header("明确主场引用；不查全场或替换原HUD")]
  public EnvironmentRoot environment;
  public Ossastra.EnvironmentSimulation.Rendering.EnvironmentSkyBinding skyBinding;
  public Ossastra.Development.SceneVisibility.RoomVisibilityLabPanel legacyDiagnostics;
  public Transform observationAnchor;
  public SceneVisibilityRoot visibility;
  public MapResearchRoot map;
  public WeatherProfileSO[] weatherPresets=Array.Empty<WeatherProfileSO>();
  public ClimateProfileSO[] climatePresets=Array.Empty<ClimateProfileSO>();
  public TMP_FontAsset font;
  [Header("可折叠开发窗口；位置尺寸可调整")]
  public Vector2 anchor=new Vector2(1,1),offset=new Vector2(-12,-63);
  [Min(280)] public float width=380;
  [Min(260)] public float expandedHeight=620;
  [SerializeField] bool expanded;
  public bool Expanded=>expanded;
  public RectTransform Surface=>surface;
  public bool IsCapturingInput=>captured;
  public string Status{get;private set;}="未建立环境面板";

  readonly HashSet<int> hovering=new HashSet<int>(),pressed=new HashSet<int>(),selected=new HashSet<int>();
  readonly List<Action> refreshers=new List<Action>();
  readonly List<TMP_Dropdown> dropdowns=new List<TMP_Dropdown>();
  GameObject viewRoot,body;
  RectTransform surface;
  TextMeshProUGUI header,statusText;
  EnvironmentPanelWidgets widgets;
  Ossastra.Characters.CharacterControlCoordinator capturedCoordinator;
  MapResearchRoot capturedMap;
  bool captured,building;
  float nextRefresh;
  TMP_FontAsset ownedFont;
  Font ownedOsFont;
  UnityEngine.UI.RawImage previewImage;
  Texture previewTexture;
  Ossastra.Development.SceneVisibility.RoomVisibilityLabPanel leasedDiagnostics;
  bool hasDiagnosticsLease,diagnosticsShowBefore;

  void OnEnable()
  {
   if(!Application.isPlaying)return;
   if(viewRoot==null)Build();else viewRoot.SetActive(true);
   SetExpanded(expanded);
  }
  void Update()
  {
   if(!Application.isPlaying)return;
   ReconcileLegacyDiagnostics();
   if(viewRoot==null){Build();return;}
   ReconcileCapture();
   if(Time.unscaledTime<nextRefresh)return;
   nextRefresh=Time.unscaledTime+.12f;RefreshWidgets();
  }
  void Build()
  {
   if(building||viewRoot!=null||environment==null)return;
   if(EventSystem.current==null){Status="缺少原场EventSystem，未建立无法交互的面板";return;}
   building=true;
   try
   {
    var uiFont=ResolveFont();widgets=new EnvironmentPanelWidgets(this,uiFont);
    viewRoot=new GameObject("EnvironmentPanelCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
    viewRoot.transform.SetParent(transform,false);viewRoot.hideFlags=HideFlags.DontSave;
    var canvas=viewRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2400;
    var scaler=viewRoot.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
    surface=widgets.Rect("EnvironmentPanel",viewRoot.transform);surface.anchorMin=surface.anchorMax=anchor;surface.pivot=anchor;surface.anchoredPosition=offset;
    widgets.Image(surface,EnvironmentPanelWidgets.Background,true);widgets.Relay(surface.gameObject);
    var edge=surface.gameObject.AddComponent<UnityEngine.UI.Outline>();edge.effectColor=EnvironmentPanelWidgets.Line;edge.effectDistance=new Vector2(1,-1);
    var title=widgets.Rect("Header",surface);title.anchorMin=new Vector2(0,1);title.anchorMax=Vector2.one;title.pivot=new Vector2(.5f,1);title.anchoredPosition=Vector2.zero;title.sizeDelta=new Vector2(0,35);
    var button=title.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=widgets.Image(title,new Color(.09f,.15f,.16f,1),true);header=widgets.Text("Label",title,"环境模拟",16);widgets.Stretch(header.rectTransform,10,0,10,0);button.onClick.AddListener(()=>SetExpanded(!expanded));widgets.Relay(title.gameObject);
    var bodyRect=widgets.Rect("Body",surface);widgets.Stretch(bodyRect,8,8,8,40);body=bodyRect.gameObject;
    var scroll=body.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
    var viewport=widgets.Rect("Viewport",body.transform);widgets.Stretch(viewport);widgets.Image(viewport,Color.white,true);viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
    var content=widgets.Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
    var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.padding=new RectOffset(4,9,3,5);layout.spacing=4;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
    var fitter=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    scroll.viewport=viewport;scroll.content=content;
    AddControls(content);Status="面板已建立；环境状态仍由EnvironmentRoot统一持有";RefreshWidgets();
   }
   catch(Exception error){Status="环境面板建立失败："+error.Message;Debug.LogException(error,this);if(viewRoot!=null)Destroy(viewRoot);viewRoot=null;refreshers.Clear();dropdowns.Clear();}
   finally{building=false;}
  }
  void AddControls(RectTransform content)
  {
   if(legacyDiagnostics!=null&&observationAnchor!=null&&legacyDiagnostics.gameObject.scene==gameObject.scene&&observationAnchor.gameObject.scene==gameObject.scene)
    widgets.Buttons(content,"ObservationLocations",new[]{"去光影样板","到房屋门外"},i=>legacyDiagnostics.PlaceCurrent(i==0?observationAnchor.position+Vector3.up*.1f:legacyDiagnostics.exteriorPosition));
   widgets.Title(content,"时间与昼夜");
   Float(content,"TimeHoursSlider","时刻",0,24,()=>environment.Current==null?(float)environment.timeHours:(float)environment.Current.Hours,v=>environment.SetTimeHours(v),v=>v.ToString("00.00")+" h");
   Toggle(content,"ClockRunning","自动推进昼夜",()=>environment.clockRunning,v=>environment.SetClockRunning(v));
   Float(content,"CycleMinutes","每昼夜演示用时",.25f,30,()=>environment.cycleMinutes,v=>environment.cycleMinutes=v,v=>v.ToString("0.00")+" 分钟");
   widgets.Buttons(content,"TimePresets",new[]{"日出","正午","日落","深夜"},i=>Change(()=>environment.SetTimeHours(new[]{6d,12d,18d,0d}[i])));
   widgets.Title(content,"太阳与真实阴影");
   Toggle(content,"ManualSun","手动太阳角度（时刻保持）",()=>environment.manualSun,v=>environment.SetManualSun(v,ReadSunAzimuth(),ReadSunElevation()));
   Float(content,"SunAzimuth","太阳方位",0,360,ReadSunAzimuth,v=>environment.SetManualSun(true,v,ReadSunElevation()),Degree);
   Float(content,"SunElevation","太阳高度",-12,85,ReadSunElevation,v=>environment.SetManualSun(true,ReadSunAzimuth(),v),Degree);
   Float(content,"SunIntensity","日照强度倍率",0,3,()=>environment.sunIntensityMultiplier,v=>environment.sunIntensityMultiplier=v);
   Float(content,"SunTemperature","日照色温偏移",-2500,2500,()=>environment.sunTemperatureOffset,v=>environment.sunTemperatureOffset=v,v=>v.ToString("+0;-0;0")+" K");
   Float(content,"ShadowStrength","太阳阴影强度",0,1,()=>environment.shadowStrength,v=>environment.shadowStrength=v);
   var quality=widgets.Text("QualityInfo",widgets.Row(content,"QualityRow",29),"阴影预算：使用当前URP配置",12);
   refreshers.Add(()=>{var p=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;quality.text=p==null?"当前相机没有URP阴影预算":"当前阴影预算 "+p.mainLightShadowmapResolution+" / "+p.shadowCascadeCount+" 级联（Inspector配置）";});
   widgets.Title(content,"天空、环境底光与角色阅读");
   Float(content,"SkyIntensity","天空亮度倍率",0,2,()=>environment.skyMultiplier,v=>environment.skyMultiplier=v);
   if(skyBinding!=null&&skyBinding.environment==environment)
   {
    Float(content,"ReflectionIntensity","天空反射强度",0,2,()=>skyBinding.reflectionIntensity,v=>skyBinding.reflectionIntensity=v);
    Toggle(content,"SkyColorOverride","手动覆盖天顶 / 地平线颜色",()=>skyBinding.overrideSkyColors,v=>skyBinding.overrideSkyColors=v);
    Toggle(content,"SkyColorCharacterSync","自然 / 覆盖环境色同步到角色",()=>skyBinding.syncSkyColorTo2D,v=>skyBinding.syncSkyColorTo2D=v);
    Float(content,"SkyColorCharacterStrength","天空与角色环境色联动强度",0,1,()=>skyBinding.stylizedSkyTintStrength,v=>skyBinding.stylizedSkyTintStrength=v);
    widgets.Buttons(content,"SkyColors",new[]{"昼夜原色","晴蓝","暮霞","冷夜"},i=>Change(()=>
    {
     skyBinding.overrideSkyColors=i!=0;if(i==0)return;
     skyBinding.zenithColor=new[]{Color.black,new Color(.13f,.36f,.65f),new Color(.28f,.13f,.15f),new Color(.015f,.022f,.06f)}[i];
     skyBinding.horizonColor=new[]{Color.black,new Color(.67f,.78f,.82f),new Color(.9f,.45f,.19f),new Color(.04f,.055f,.12f)}[i];
    }));
   }
   Float(content,"AmbientIntensity","环境底光倍率",0,2,()=>environment.ambientMultiplier,v=>environment.ambientMultiplier=v);
   Float(content,"IndoorAmbient","室内底光系数",0,1,()=>environment.indoorAmbientScale,v=>environment.indoorAmbientScale=v);
   Float(content,"CharacterReadability","角色最低可读度",0,.5f,()=>environment.characterReadability,v=>environment.characterReadability=v);
   Float(content,"GroundResponse","地表受光响应",0,2,()=>environment.groundLightResponse,v=>environment.groundLightResponse=v);
   Float(content,"DecorationResponse","装饰受光响应",0,2,()=>environment.decorationLightResponse,v=>environment.decorationLightResponse=v);
   Float(content,"CharacterResponse","角色/装备受光响应",0,2,()=>environment.characterLightResponse,v=>environment.characterLightResponse=v);
   var previewRect=widgets.Row(content,"SkyPreview",135);previewImage=previewRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();previewImage.raycastTarget=false;previewImage.color=Color.white;previewImage.texture=previewTexture;previewRect.gameObject.SetActive(previewTexture!=null);
   widgets.Title(content,"天气、气候与空气");
   AddPresets(content);
   Float(content,"WeatherTransition","天气过渡用时",0,20,()=>environment.weatherTransitionSeconds,v=>environment.weatherTransitionSeconds=v,v=>v.ToString("0.0")+" s");
   Toggle(content,"AutomaticWeather","按当前气候自动切换天气",()=>environment.automaticWeather,v=>environment.SetAutomaticWeather(v));
   Float(content,"CloudCoverage","云量（手动覆盖）",0,1,()=>environment.cloudCoverageOverride>=0?environment.cloudCoverageOverride:environment.Current==null?0:environment.Current.CloudCoverage,v=>environment.cloudCoverageOverride=v);
   Float(content,"CloudDensity","云密度（手动覆盖）",0,2,()=>environment.cloudDensityOverride>=0?environment.cloudDensityOverride:environment.Current==null?0:environment.Current.CloudDensity,v=>environment.cloudDensityOverride=v);
   Float(content,"CloudSpeed","云移动速度倍率",0,3,()=>environment.cloudSpeedMultiplier,v=>environment.cloudSpeedMultiplier=v);
   Float(content,"CloudShadow","云影强度（手动覆盖）",0,.6f,()=>environment.cloudShadowStrengthOverride>=0?environment.cloudShadowStrengthOverride:environment.Current==null?0:environment.Current.CloudShadowStrength,v=>environment.cloudShadowStrengthOverride=v);
   Float(content,"WindDirection","风向（手动覆盖）",0,360,()=>environment.windYawDegreesOverride>=0?environment.windYawDegreesOverride:environment.Current==null?0:Mathf.Repeat(Mathf.Atan2(environment.Current.WindDirection.x,environment.Current.WindDirection.y)*Mathf.Rad2Deg,360),v=>environment.windYawDegreesOverride=v,Degree);
   Float(content,"WindSpeed","风速倍率",0,3,()=>environment.windMultiplier,v=>environment.windMultiplier=v);
   Float(content,"FogAmount","薄雾倍率",0,3,()=>environment.fogMultiplier,v=>environment.fogMultiplier=v);
   Float(content,"RainAmount","雨量倍率（需选择有雨天气）",0,2,()=>environment.rainMultiplier,v=>environment.rainMultiplier=v);
   Float(content,"Wetness","湿润倍率",0,2,()=>environment.wetnessMultiplier,v=>environment.wetnessMultiplier=v);
   widgets.Button(content,"ClearWeatherOverrides","清除云量 / 云密度 / 云影 / 风向覆盖",()=>Change(()=>{environment.cloudCoverageOverride=environment.cloudDensityOverride=environment.cloudShadowStrengthOverride=environment.windYawDegreesOverride=-1;}));
   widgets.Title(content,"贡献对照与复原");
   var names=new[]{"原场","仅日照","仅底光","仅云影","完整效果"};var contribution=widgets.Dropdown(content,"Contribution","当前显示贡献",names,i=>Change(()=>environment.SetContribution((EnvironmentContribution)i)));dropdowns.Add(contribution);refreshers.Add(()=>contribution.SetValueWithoutNotify((int)environment.contribution));
   Toggle(content,"EffectsEnabled","启用本环境系统",()=>environment.effectsEnabled,v=>environment.effectsEnabled=v);
   widgets.Button(content,"ResetEnvironment","复原演示基线（正午 / 初始天气）",()=>Change(()=>environment.ResetPreview()));
   var status=widgets.Row(content,"Status",90);statusText=widgets.Text("Text",status,"​",12);statusText.enableWordWrapping=true;statusText.alignment=TextAlignmentOptions.TopLeft;
  }
  void AddPresets(Transform content)
  {
   var weather=weatherPresets.Where(p=>p!=null).ToArray();
   if(weather.Length>0){var drop=widgets.Dropdown(content,"WeatherPreset","天气目标",weather.Select(p=>p.displayName).ToArray(),i=>Change(()=>environment.TransitionWeather(weather[i],environment.weatherTransitionSeconds)));dropdowns.Add(drop);refreshers.Add(()=>{int i=Array.IndexOf(weather,environment.TargetWeather);if(i>=0)drop.SetValueWithoutNotify(i);});}
   var climates=climatePresets.Where(p=>p!=null).ToArray();
   if(climates.Length>0){var drop=widgets.Dropdown(content,"ClimatePreset","气候基线",climates.Select(p=>p.displayName).ToArray(),i=>Change(()=>environment.SetClimate(climates[i])));dropdowns.Add(drop);refreshers.Add(()=>{int i=Array.IndexOf(climates,environment.climate);if(i>=0)drop.SetValueWithoutNotify(i);});}
  }
  void Float(Transform content,string name,string title,float min,float max,Func<float> read,Action<float> write,Func<float,string> format=null)
  {
   TextMeshProUGUI caption;var slider=widgets.Slider(content,name,title,min,max,Mathf.Clamp(read(),min,max),v=>Change(()=>write(v)),out caption);
   refreshers.Add(()=>{float value=read();slider.SetValueWithoutNotify(value);caption.text=title+"  "+(format==null?value.ToString("0.00"):format(value));});
  }
  void Toggle(Transform content,string name,string title,Func<bool> read,Action<bool> write)
  {var toggle=widgets.Toggle(content,name,title,read(),v=>Change(()=>write(v)));refreshers.Add(()=>toggle.SetIsOnWithoutNotify(read()));}
  static string Degree(float value)=>value.ToString("0.0")+"°";
  float ReadSunAzimuth()=>environment.manualSun?environment.sunAzimuthDegrees:environment.Current==null?environment.sunAzimuthDegrees:environment.Current.Sun.AzimuthDegrees;
  float ReadSunElevation()=>environment.manualSun?environment.sunElevationDegrees:environment.Current==null?environment.sunElevationDegrees:environment.Current.Sun.ElevationDegrees;
  void Change(Action change)
  {
   if(environment==null)return;
   try{change();environment.RefreshNow();RefreshWidgets();}
   catch(Exception error){Status="参数未应用："+error.Message;Debug.LogWarning(Status,this);}
  }
  void RefreshWidgets()
  {
   if(environment==null)return;
   foreach(var refresh in refreshers)refresh();
   if(header!=null)header.text=(expanded?"收起  ":"展开  ")+"环境模拟  "+(environment.Current==null?"等待快照":environment.Current.Hours.ToString("00.0")+" h");
   if(statusText!=null){var f=environment.Current;statusText.text=environment.Status+(f==null?"":"\n"+f.WeatherName+"  太阳 "+f.Sun.ElevationDegrees.ToString("0.0")+"° / "+f.Sun.AzimuthDegrees.ToString("0.0")+"°\n雨 "+f.Rain.ToString("0.00")+"  风 "+f.WindSpeed.ToString("0.00")+"  环境版本 "+f.Revision);}
  }
  public void SetExpanded(bool value)
  {
   expanded=value;if(surface!=null){surface.sizeDelta=new Vector2(width,value?expandedHeight:35);surface.anchorMin=surface.anchorMax=anchor;surface.pivot=anchor;surface.anchoredPosition=offset;}
   if(!value&&surface!=null&&EventSystem.current!=null){var current=EventSystem.current.currentSelectedGameObject;if(current!=null&&current.transform.IsChildOf(surface))EventSystem.current.SetSelectedGameObject(null);}
   if(body!=null)body.SetActive(value);ReconcileLegacyDiagnostics();ReconcileCapture();RefreshWidgets();
  }
  void ReconcileLegacyDiagnostics()
  {
   bool want=Application.isPlaying&&isActiveAndEnabled&&expanded&&legacyDiagnostics!=null&&legacyDiagnostics.gameObject.scene==gameObject.scene;
   if(hasDiagnosticsLease&&(!want||leasedDiagnostics!=legacyDiagnostics))ReleaseLegacyDiagnostics();
   if(!want)return;
   if(!hasDiagnosticsLease){leasedDiagnostics=legacyDiagnostics;diagnosticsShowBefore=leasedDiagnostics.showPanel;hasDiagnosticsLease=true;}
   leasedDiagnostics.showPanel=false;
  }
  void ReleaseLegacyDiagnostics()
  {
   if(hasDiagnosticsLease&&leasedDiagnostics!=null)leasedDiagnostics.showPanel=diagnosticsShowBefore;
   hasDiagnosticsLease=false;leasedDiagnostics=null;
  }
  public void SetSkyPreviewTexture(Texture value)
  {previewTexture=value;if(previewImage!=null){previewImage.texture=value;previewImage.gameObject.SetActive(value!=null);}}
  internal void NotifyHover(EnvironmentPanelInputRelay relay,bool value){if(value)hovering.Add(relay.GetInstanceID());else hovering.Remove(relay.GetInstanceID());ReconcileCapture();}
  internal void NotifyPressed(EnvironmentPanelInputRelay relay,bool value){if(value)pressed.Add(relay.GetInstanceID());else pressed.Remove(relay.GetInstanceID());ReconcileCapture();}
  internal void NotifySelection(EnvironmentPanelInputRelay relay,bool value){if(value)selected.Add(relay.GetInstanceID());else selected.Remove(relay.GetInstanceID());ReconcileCapture();}
  internal void ForgetPointer(EnvironmentPanelInputRelay relay){hovering.Remove(relay.GetInstanceID());pressed.Remove(relay.GetInstanceID());selected.Remove(relay.GetInstanceID());ReconcileCapture();}
  void ReconcileCapture()
  {
   bool want=isActiveAndEnabled&&viewRoot!=null&&viewRoot.activeInHierarchy&&(hovering.Count>0||pressed.Count>0||selected.Count>0||dropdowns.Any(d=>d!=null&&d.IsExpanded));
   var coordinator=visibility==null?null:visibility.coordinator;
   if(captured&&(capturedCoordinator!=coordinator||capturedMap!=map))Capture(false);
   if(want!=captured)Capture(want);
  }
  void Capture(bool value)
  {
   if(!value){if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,false);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,false);capturedCoordinator=null;capturedMap=null;captured=false;return;}
   capturedCoordinator=visibility==null?null:visibility.coordinator;capturedMap=map;
   if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,true);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,true);captured=true;
  }
  TMP_FontAsset ResolveFont()
  {
   if(font!=null)return font;
   try
   {
    ownedOsFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial Unicode MS"},16);
    if(ownedOsFont!=null){ownedFont=TMP_FontAsset.CreateFontAsset(ownedOsFont);ownedFont.name="Environment panel owned Chinese font";ownedFont.hideFlags=HideFlags.HideAndDontSave;ownedFont.atlasPopulationMode=AtlasPopulationMode.Dynamic;ownedFont.isMultiAtlasTexturesEnabled=true;ownedFont.material.hideFlags=HideFlags.HideAndDontSave;return ownedFont;}
   }
   catch(Exception error){Debug.LogWarning("环境面板中文字体未建立："+error.Message,this);}
   return TMP_Settings.defaultFontAsset;
  }
  void OnDisable(){ReleaseLegacyDiagnostics();hovering.Clear();pressed.Clear();selected.Clear();Capture(false);if(viewRoot!=null)viewRoot.SetActive(false);}
  void OnDestroy()
  {
   ReleaseLegacyDiagnostics();Capture(false);if(viewRoot!=null)Destroy(viewRoot);viewRoot=null;refreshers.Clear();dropdowns.Clear();
   if(ownedFont!=null){if(ownedFont.atlasTextures!=null)foreach(var texture in ownedFont.atlasTextures)if(texture!=null)Destroy(texture);if(ownedFont.material!=null)Destroy(ownedFont.material);Destroy(ownedFont);ownedFont=null;}
   if(ownedOsFont!=null)Destroy(ownedOsFont);ownedOsFont=null;
  }
 }
}
