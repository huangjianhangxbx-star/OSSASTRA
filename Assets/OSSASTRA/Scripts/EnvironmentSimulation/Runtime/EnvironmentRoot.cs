/* 功能：组装显式时间来源、独立天气状态与环境帧求值，准备原生照明后提交当前相机显示。
 * 边界：预览时钟不替代游戏日历；世界求值不依赖相机，消费者故障不能反向撤销有效环境。
 * 生命周期：OnValidate只标记工作，主线程Update提交；显示暂停只归还登记/灯光，不重置天气状态。
 */
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ossastra.EnvironmentSimulation
{
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class EnvironmentRoot:MonoBehaviour
 {
  [Header("明确场景引用；不自动寻找其他场景的相机或配置")]
  public Camera gameplayCamera;
  public DayCycleProfileSO dayCycle;
  public WeatherProfileSO initialWeather;
  public ClimateProfileSO climate;
  public EnvironmentLightingBinding lightingBinding;
  [Header("显示开关；编辑态须主动开启预览")]
  public bool effectsEnabled=true,previewInEditMode;
  public EnvironmentContribution contribution=EnvironmentContribution.Combined;
  [Header("时间：默认手动正午；不改Time.timeScale")]
  public EnvironmentTimeMode timeMode=EnvironmentTimeMode.Manual;
  public bool clockRunning;
  [Range(0,24)] public double timeHours=12;
  [Min(.01f)] public float cycleMinutes=6;
  public MonoBehaviour externalTimeSource;
  [Header("手动太阳；与时钟分开")]
  public bool manualSun;
  public float sunAzimuthDegrees=135;
  [Range(-89,89)] public float sunElevationDegrees=45;
  [Min(0)] public float sunIntensityMultiplier=1;
  [Range(-5500,33500)] public float sunTemperatureOffset;
  [Range(0,1)] public float shadowStrength=.85f;
  [Header("天空与底光；室内响应不冒充实时GI")]
  // 用户2026-11-04截图批准的作者缺省；已有序列化场景仍由其自己的值决定。
  [Min(0)] public float ambientMultiplier=1.99f,skyMultiplier=1;
  [Range(0,1)] public float indoorAmbientScale=1,characterReadability=.02f;
  [Range(0,2)] public float groundLightResponse=2,decorationLightResponse=2,characterLightResponse=2;
  [Header("天气覆盖；-1表示读取天气档")]
  [Range(-1,1)] public float cloudCoverageOverride=-1;
  [Range(-1,2)] public float cloudDensityOverride=-1;
  [Range(-1,1)] public float cloudShadowStrengthOverride=-1;
  public float windYawDegreesOverride=-1;
  [Min(0)] public float cloudSpeedMultiplier=1,windMultiplier=1,fogMultiplier=1,rainMultiplier=1,wetnessMultiplier=1;
  [Header("可重复的自动天气；不涉及生存或作物业务")]
  public bool automaticWeather;
  public int weatherSeed=12345;
  [Min(0)] public float weatherTransitionSeconds=3;
  public EnvironmentFrame Current{get;private set;}
  public string Status{get;private set;}="尚未采样";
  public WeatherProfileSO CurrentWeather=>weather.CurrentWeather;
  public WeatherProfileSO TargetWeather=>weather.TargetWeather;
  public float WeatherTransitionProgress=>weather.TransitionProgress;
  public long ConsumerFaultCount{get;private set;}
  public string LastConsumerFault{get;private set;}="";
  // Changed是相机无关的完整世界快照；显示撤权另由Registry.Released表达，绝不发布半初始化/null。
  public event Action<EnvironmentFrame> Changed;
  static EnvironmentRoot activeRoot;
  readonly EnvironmentWeatherState weather=new EnvironmentWeatherState();
  volatile bool refreshRequested;
  bool refreshing;
  double elapsedSeconds,lastRealtime;
  long revision;
  Camera publishedCamera;
  EnvironmentLightingBinding appliedBinding;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){activeRoot=null;}
  void OnEnable(){lastRealtime=Time.realtimeSinceStartupAsDouble;SceneManager.activeSceneChanged+=OnActiveSceneChanged;RefreshNow();}
  void OnDisable(){SceneManager.activeSceneChanged-=OnActiveSceneChanged;ReleaseDisplay();}
  void OnDestroy(){ReleaseDisplay();}
  void OnActiveSceneChanged(Scene oldScene,Scene newScene){RefreshNow();}
  // Unity可在非主线程调用OnValidate；这里只写普通标记，不调用任何Unity对象或委托。
  void OnValidate(){refreshRequested=true;}
  void Update()
  {
   double now=Time.realtimeSinceStartupAsDouble;
   double delta=Application.isPlaying?Time.unscaledDeltaTime:Math.Max(0,Math.Min(now-lastRealtime,1));lastRealtime=now;
   if(Application.isPlaying||previewInEditMode){refreshRequested=false;Advance(delta);}
   else if(refreshRequested){refreshRequested=false;RefreshNow();}
  }
  public void SetTimeHours(double hours){timeHours=EnvironmentClock.WrapHours(hours);RefreshNow();}
  public void SetClockRunning(bool running){clockRunning=running;if(running)timeMode=EnvironmentTimeMode.PreviewClock;RefreshNow();}
  public void SetManualSun(bool enabled,float azimuthDegrees,float elevationDegrees)
  {var checkedPose=new SunPose(azimuthDegrees,elevationDegrees);manualSun=enabled;sunAzimuthDegrees=checkedPose.AzimuthDegrees;sunElevationDegrees=checkedPose.ElevationDegrees;RefreshNow();}
  public void SetClimate(ClimateProfileSO profile){climate=profile;weather.InvalidateSchedule();RefreshNow();}
  public void SetAutomaticWeather(bool enabled){automaticWeather=enabled;weather.InvalidateSchedule();RefreshNow();}
  public void SetContribution(EnvironmentContribution value){contribution=value;RefreshNow();}
  public void TransitionWeather(WeatherProfileSO target,float durationSeconds)
  {
   if(target==null)throw new ArgumentNullException(nameof(target));
   float duration=EnvironmentNumbers.Finite(durationSeconds);if(duration<0)throw new ArgumentOutOfRangeException(nameof(durationSeconds));
   EnsureWeather();weather.Transition(target,duration);
   RefreshNow();
  }
  public void ResetPreview()
  {
   timeMode=EnvironmentTimeMode.Manual;timeHours=12;clockRunning=false;manualSun=false;effectsEnabled=true;contribution=EnvironmentContribution.Combined;
   sunIntensityMultiplier=skyMultiplier=cloudSpeedMultiplier=windMultiplier=fogMultiplier=rainMultiplier=wetnessMultiplier=1;
   ambientMultiplier=1.99f;indoorAmbientScale=1;characterReadability=.02f;groundLightResponse=decorationLightResponse=characterLightResponse=2;
   sunTemperatureOffset=0;shadowStrength=.85f;cloudCoverageOverride=cloudDensityOverride=cloudShadowStrengthOverride=windYawDegreesOverride=-1;
   automaticWeather=false;weather.Reset();elapsedSeconds=0;RefreshNow();
  }
  public void Advance(double deltaSeconds)
  {
   if(double.IsNaN(deltaSeconds)||double.IsInfinity(deltaSeconds)||deltaSeconds<0)throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
   try
   {
    EnsureWeather();
    if(automaticWeather&&!weather.TryPrepareAutomatic(deltaSeconds,climate,weatherSeed,out var reason)){Status=reason;return;}
    if(timeMode==EnvironmentTimeMode.PreviewClock&&clockRunning)timeHours=EnvironmentClock.AdvanceHours(timeHours,deltaSeconds,EnvironmentNumbers.Finite(cycleMinutes)*60d);
    if(timeMode==EnvironmentTimeMode.External)
    {
     if(!(externalTimeSource is IEnvironmentTimeSource source)||!source.TryReadHours(out double hours))
     {Status="外部时间源无效；保持最后有效环境与时刻";return;}
     try{timeHours=EnvironmentClock.WrapHours(hours);}
     catch(ArgumentException){Status="外部时间源小时无效；保持最后有效环境与时刻";return;}
    }
    // 秒只用于循环视觉噪声，限制其范围避免长期精度退化及上传float溢出，不是业务计时。
    elapsedSeconds=(elapsedSeconds+deltaSeconds%86400d)%86400d;weather.AdvancePrepared(deltaSeconds,automaticWeather,climate,weatherSeed,weatherTransitionSeconds);RefreshNow();
   }
   catch(Exception e) when(e is ArgumentException||e is InvalidOperationException){Status="环境输入无效："+e.Message;ReleaseDisplay();}
  }
  void EnsureWeather(){weather.EnsureInitial(initialWeather);}
  public void RefreshNow()
  {
   // 消费者若在回调中请求改参数，推迟到下一次主线程更新，避免同步重入发布链。
   if(refreshing){refreshRequested=true;return;}
   refreshing=true;refreshRequested=false;
   try{EvaluateAndCommit();}finally{refreshing=false;}
  }
  void EvaluateAndCommit()
  {
   EnvironmentFrame next;
   try
   {
    EnsureWeather();if(dayCycle==null)throw new InvalidOperationException("尚未指定昼夜档。");
    timeHours=EnvironmentClock.WrapHours(timeHours);weather.RefreshCurrent();next=BuildFrame();
   }
   catch(Exception error) when(error is ArgumentException||error is InvalidOperationException){Status="环境求值无效："+error.Message;ReleaseDisplay();return;}
   Current=next;
   // Edit显式预览允许不输出画面的Camera；Play相机停用只撤显示，世界天气继续求值。
   bool cameraReady=gameplayCamera!=null&&(!Application.isPlaying||gameplayCamera.isActiveAndEnabled);
   bool allowed=isActiveAndEnabled&&(Application.isPlaying||previewInEditMode)&&next.Enabled&&gameObject.scene==SceneManager.GetActiveScene()&&cameraReady&&gameplayCamera.gameObject.scene==gameObject.scene;
   if(!allowed){ReleaseDisplay();Status=next.Enabled?"已采样；未取得当前场景预览租约":"已恢复场景原貌";NotifyWorld(next);return;}
   try
   {
    if(activeRoot!=null&&activeRoot!=this)throw new InvalidOperationException("当前场景已有另一个环境显示根。");
    EnvironmentCameraRegistry.ValidatePublish(this,gameplayCamera,next);
   }
   catch(Exception error) when(error is ArgumentException||error is InvalidOperationException){Status="环境显示登记无效："+error.Message;ReleaseDisplay();NotifyWorld(next);return;}
   try
   {
    if(appliedBinding!=null&&appliedBinding!=lightingBinding)appliedBinding.Release(this);
    appliedBinding=lightingBinding;if(appliedBinding!=null)appliedBinding.Apply(this,next);
   }
   catch(Exception error) when(error is ArgumentException||error is InvalidOperationException){Status="原生照明提交失败："+error.Message;ReleaseDisplay();NotifyWorld(next);return;}
   activeRoot=this;
   if(!ReferenceEquals(publishedCamera,null)&&publishedCamera!=gameplayCamera)EnvironmentCameraRegistry.Release(this,publishedCamera);
   publishedCamera=gameplayCamera;long faults=EnvironmentCameraRegistry.ConsumerFaultCount;
   EnvironmentCameraRegistry.Publish(this,gameplayCamera,next);
   Status="显示环境："+next.WeatherName+" / "+next.Hours.ToString("0.00")+" 时";
   if(EnvironmentCameraRegistry.ConsumerFaultCount!=faults)Status+="；可选显示消费者故障，环境提交保留";
   NotifyWorld(next);
  }
  void NotifyWorld(EnvironmentFrame frame)
  {
   var listeners=Changed;if(listeners==null)return;
   foreach(Action<EnvironmentFrame> listener in listeners.GetInvocationList())
   {
    try{listener(frame);}
    catch(Exception error)
    {
     if(ConsumerFaultCount<long.MaxValue)ConsumerFaultCount++;
     string message=error.Message??"";if(message.Length>256)message=message.Substring(0,256);
     LastConsumerFault="Changed / "+listener.Method.DeclaringType?.FullName+"."+listener.Method.Name+" / "+error.GetType().Name+": "+message;
     Status+="；世界快照消费者故障，已隔离";
    }
   }
  }
  void ReleaseDisplay()
  {
   if(!ReferenceEquals(publishedCamera,null)){EnvironmentCameraRegistry.Release(this,publishedCamera);publishedCamera=null;}
   if(appliedBinding!=null){appliedBinding.Release(this);appliedBinding=null;}
   if(activeRoot==this)activeRoot=null;
  }
  EnvironmentFrame BuildFrame()
  {
   var w=weather.Values;var p=dayCycle;
   var sun=manualSun?new SunPose(sunAzimuthDegrees,sunElevationDegrees):SunPath.Sample(timeHours,EnvironmentNumbers.Range(p.maximumSunElevation,1,89),EnvironmentNumbers.Finite(p.sunAzimuthOffset));
   float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-6,12,sun.ElevationDegrees));
   // 太阳/月亮共用主灯，交接必须发生在直射强度为零的地平线处，不能在余辉中翻转影子。
   float solarVisibility=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,12,sun.ElevationDegrees));
   float lunarVisibility=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,-12,sun.ElevationDegrees));
   float direct=EnvironmentNumbers.Unit(p.sunlightResponse==null?solarVisibility:p.sunlightResponse.Evaluate(solarVisibility));
   float ambient=EnvironmentNumbers.Unit(p.ambientResponse==null?daylight:p.ambientResponse.Evaluate(daylight));
   float night=1-daylight;Color climateTint=climate==null?Color.white:EnvironmentNumbers.Color(climate.environmentTint);
   Color daySun=Color.Lerp(EnvironmentNumbers.Color(p.daySunColor),EnvironmentNumbers.Color(p.sunsetSunColor),(1-Mathf.Clamp01(Mathf.Abs(sun.ElevationDegrees)/22))*daylight);
   Color sunColor=(sun.ElevationDegrees<0?EnvironmentNumbers.Color(p.nightSunColor):daySun)*w.sunTint*EnvironmentNumbers.Temperature(6500+sunTemperatureOffset);
   float yaw=windYawDegreesOverride<0?w.yaw:EnvironmentNumbers.Finite(windYawDegreesOverride);float radians=yaw*Mathf.Deg2Rad;
   var d=new EnvironmentFrameData{enabled=effectsEnabled&&contribution!=EnvironmentContribution.Original,contribution=contribution,revision=++revision,hours=timeHours,seconds=elapsedSeconds,sun=sun,
    keyLightDirection=sun.ElevationDegrees>=0?sun.DirectionToSun:-sun.DirectionToSun,moonDirection=-sun.DirectionToSun,moonIntensity=EnvironmentNumbers.Nonnegative(p.nightSunIntensity)*lunarVisibility,nightFactor=night,
    sunColor=EnvironmentNumbers.Color(sunColor),sunIntensity=(EnvironmentNumbers.Nonnegative(p.daySunIntensity)*direct+EnvironmentNumbers.Nonnegative(p.nightSunIntensity)*lunarVisibility)*w.sun*EnvironmentNumbers.Nonnegative(sunIntensityMultiplier),
    fillColor=Color.Lerp(EnvironmentNumbers.Color(p.nightAmbientSky),EnvironmentNumbers.Color(p.dayAmbientSky),ambient)*w.ambientTint,
    fillIntensity=Mathf.Lerp(EnvironmentNumbers.Nonnegative(p.nightFillIntensity),EnvironmentNumbers.Nonnegative(p.dayFillIntensity),ambient)*w.ambient*EnvironmentNumbers.Nonnegative(ambientMultiplier),
    ambientSky=Color.Lerp(EnvironmentNumbers.Color(p.nightAmbientSky),EnvironmentNumbers.Color(p.dayAmbientSky),ambient)*w.ambientTint*climateTint,
    ambientEquator=Color.Lerp(EnvironmentNumbers.Color(p.nightAmbientEquator),EnvironmentNumbers.Color(p.dayAmbientEquator),ambient)*w.ambientTint*climateTint,
    ambientGround=Color.Lerp(EnvironmentNumbers.Color(p.nightAmbientGround),EnvironmentNumbers.Color(p.dayAmbientGround),ambient)*w.ambientTint*climateTint,
    ambientIntensity=Mathf.Lerp(EnvironmentNumbers.Nonnegative(p.nightAmbientIntensity),EnvironmentNumbers.Nonnegative(p.dayAmbientIntensity),ambient)*w.ambient*EnvironmentNumbers.Nonnegative(ambientMultiplier),
    skyZenith=Color.Lerp(EnvironmentNumbers.Color(p.nightSkyZenith),EnvironmentNumbers.Color(p.daySkyZenith),daylight)*climateTint,
    skyHorizon=Color.Lerp(EnvironmentNumbers.Color(p.nightSkyHorizon),EnvironmentNumbers.Color(p.daySkyHorizon),daylight)*climateTint,skyNight=EnvironmentNumbers.Color(p.nightSkyZenith),
    skyIntensity=Mathf.Lerp(EnvironmentNumbers.Nonnegative(p.nightSkyIntensity),EnvironmentNumbers.Nonnegative(p.daySkyIntensity),daylight)*w.sky*EnvironmentNumbers.Nonnegative(skyMultiplier),
    cloudCoverage=cloudCoverageOverride<0?w.coverage:EnvironmentNumbers.Unit(cloudCoverageOverride),cloudDensity=cloudDensityOverride<0?w.density:EnvironmentNumbers.Range(cloudDensityOverride,0,2),cloudSpeed=w.speed*EnvironmentNumbers.Nonnegative(cloudSpeedMultiplier),
    cloudShadowStrength=cloudShadowStrengthOverride<0?w.shadow:EnvironmentNumbers.Unit(cloudShadowStrengthOverride),cloudWorldScale=w.scale,
    windDirection=new Vector2(Mathf.Sin(radians),Mathf.Cos(radians)),windSpeed=w.wind*EnvironmentNumbers.Nonnegative(windMultiplier)*(climate==null?1:EnvironmentNumbers.Nonnegative(climate.windMultiplier)),
    fogDensity=EnvironmentNumbers.Range(w.fog*EnvironmentNumbers.Nonnegative(fogMultiplier),0,.2f),fogColor=w.fogColor,
    rain=EnvironmentNumbers.Unit(w.rain*EnvironmentNumbers.Nonnegative(rainMultiplier)),wetness=EnvironmentNumbers.Unit(w.wetness*EnvironmentNumbers.Nonnegative(wetnessMultiplier)),
    indoorAmbientScale=EnvironmentNumbers.Unit(indoorAmbientScale),characterReadability=EnvironmentNumbers.Unit(characterReadability),groundLightResponse=EnvironmentNumbers.Range(groundLightResponse,0,2),decorationLightResponse=EnvironmentNumbers.Range(decorationLightResponse,0,2),characterLightResponse=EnvironmentNumbers.Range(characterLightResponse,0,2),shadowStrength=EnvironmentNumbers.Unit(shadowStrength),
    temperature=EnvironmentNumbers.Finite(w.temperature+(climate==null?0:EnvironmentNumbers.Finite(climate.temperatureOffset))),humidity=EnvironmentNumbers.Unit(w.humidity+(climate==null?0:EnvironmentNumbers.Finite(climate.humidityOffset))),
    weatherName=TargetWeather==null?"未指定":TargetWeather.displayName,climateName=climate==null?"未指定":climate.displayName};
   if(contribution==EnvironmentContribution.SunOnly){d.ambientIntensity=d.fillIntensity=0;d.cloudCoverage=d.cloudShadowStrength=0;d.fogDensity=d.rain=d.wetness=0;}
   else if(contribution==EnvironmentContribution.AmbientOnly){d.sunIntensity=d.moonIntensity=d.fillIntensity=0;d.cloudCoverage=d.cloudShadowStrength=0;d.fogDensity=d.rain=d.wetness=0;}
   else if(contribution==EnvironmentContribution.CloudOnly){d.fogDensity=d.rain=d.wetness=0;}
   return new EnvironmentFrame(d);
  }
 }
}
