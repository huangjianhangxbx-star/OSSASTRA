/* 功能：场景内唯一环境显示权威；组合时间、太阳、天气与气候为不可变快照，先登记相机再通知接收者。
 * 边界：只推进自己的演示时钟，不推进游戏日历、不改Profile、不控制业务/声音/管线。
 * 生命周期：仅明确相机所在的当前场景可取得显示租约；禁用、原貌或失效时归还登记及灯光。
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
  public WeatherProfileSO CurrentWeather{get;private set;}
  public WeatherProfileSO TargetWeather{get;private set;}
  public float WeatherTransitionProgress=>transitioning?(float)WeatherBlend.Progress(blendElapsed,blendDuration):1;
  // Changed仅发送完整快照；停用由Registry.Released表达，不发送半初始化或null快照。
  public event Action<EnvironmentFrame> Changed;
  static EnvironmentRoot activeRoot;
  const int maximumWeatherTransitionsPerStep=64;
  WeatherProfileSO initializedWeather;
  ClimateProfileSO scheduledClimate;
  WeatherValues weatherFrom,weatherTo,weatherNow;
  bool initialized,transitioning,scheduleInitialized;
  double blendElapsed,blendDuration,elapsedSeconds,weatherRemaining,lastRealtime;
  long revision;
  System.Random weatherRandom;
  int scheduledSeed;
  Camera publishedCamera;
  EnvironmentLightingBinding appliedBinding;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){activeRoot=null;}
  void OnEnable(){lastRealtime=Time.realtimeSinceStartupAsDouble;SceneManager.activeSceneChanged+=OnActiveSceneChanged;RefreshNow();}
  void OnDisable(){SceneManager.activeSceneChanged-=OnActiveSceneChanged;ReleaseDisplay();}
  void OnDestroy(){ReleaseDisplay();}
  void OnActiveSceneChanged(Scene oldScene,Scene newScene){RefreshNow();}
  void OnValidate(){if(isActiveAndEnabled)RefreshNow();}
  void Update()
  {
   double now=Time.realtimeSinceStartupAsDouble;
   double delta=Application.isPlaying?Time.unscaledDeltaTime:Math.Max(0,Math.Min(now-lastRealtime,1));lastRealtime=now;
   if(Application.isPlaying||previewInEditMode)Advance(delta);
  }
  public void SetTimeHours(double hours){timeHours=EnvironmentClock.WrapHours(hours);RefreshNow();}
  public void SetClockRunning(bool running){clockRunning=running;if(running)timeMode=EnvironmentTimeMode.PreviewClock;RefreshNow();}
  public void SetManualSun(bool enabled,float azimuthDegrees,float elevationDegrees)
  {var checkedPose=new SunPose(azimuthDegrees,elevationDegrees);manualSun=enabled;sunAzimuthDegrees=checkedPose.AzimuthDegrees;sunElevationDegrees=checkedPose.ElevationDegrees;RefreshNow();}
  public void SetClimate(ClimateProfileSO profile){climate=profile;scheduleInitialized=false;RefreshNow();}
  public void SetAutomaticWeather(bool enabled){automaticWeather=enabled;scheduleInitialized=false;RefreshNow();}
  public void SetContribution(EnvironmentContribution value){contribution=value;RefreshNow();}
  public void TransitionWeather(WeatherProfileSO target,float durationSeconds)
  {
   if(target==null)throw new ArgumentNullException(nameof(target));
   float duration=EnvironmentNumbers.Finite(durationSeconds);if(duration<0)throw new ArgumentOutOfRangeException(nameof(durationSeconds));
   EnsureWeather();weatherFrom=weatherNow;weatherTo=target.Capture();TargetWeather=target;blendElapsed=0;blendDuration=duration;
   transitioning=duration>0;if(!transitioning){weatherNow=weatherTo;CurrentWeather=target;}
   RefreshNow();
  }
  public void ResetPreview()
  {
   timeMode=EnvironmentTimeMode.Manual;timeHours=12;clockRunning=false;manualSun=false;effectsEnabled=true;contribution=EnvironmentContribution.Combined;
   sunIntensityMultiplier=skyMultiplier=cloudSpeedMultiplier=windMultiplier=fogMultiplier=rainMultiplier=wetnessMultiplier=1;
   ambientMultiplier=1.99f;indoorAmbientScale=1;characterReadability=.02f;groundLightResponse=decorationLightResponse=characterLightResponse=2;
   sunTemperatureOffset=0;shadowStrength=.85f;cloudCoverageOverride=cloudDensityOverride=cloudShadowStrengthOverride=windYawDegreesOverride=-1;
   automaticWeather=false;initialized=false;transitioning=false;scheduleInitialized=false;elapsedSeconds=0;RefreshNow();
  }
  public void Advance(double deltaSeconds)
  {
   if(double.IsNaN(deltaSeconds)||double.IsInfinity(deltaSeconds)||deltaSeconds<0)throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
   try
   {
    EnsureWeather();
    if(automaticWeather)
    {
     // 在推进时刻/秒数/天气前核对最坏切换次数；失败保留上一份实际显示，绝不进入超长while。
     try
     {
      EnsureSchedule();
      double firstOverBudget=weatherRemaining+(double)climate.weatherDurationSeconds.x*maximumWeatherTransitionsPerStep;
      if(deltaSeconds>=firstOverBudget){Status="自动天气步长超过64次切换预算；保持上一有效环境与时刻";return;}
     }
     catch(Exception e) when(e is ArgumentException||e is InvalidOperationException)
     {Status="自动天气配置无效："+e.Message+"；保持上一有效环境与时刻";return;}
    }
    if(timeMode==EnvironmentTimeMode.PreviewClock&&clockRunning)timeHours=EnvironmentClock.AdvanceHours(timeHours,deltaSeconds,EnvironmentNumbers.Finite(cycleMinutes)*60d);
    if(timeMode==EnvironmentTimeMode.External)
    {
     if(!(externalTimeSource is IEnvironmentTimeSource source)||!source.TryReadHours(out double hours))
     {Status="外部时间源无效；保持最后有效环境与时刻";return;}
     try{timeHours=EnvironmentClock.WrapHours(hours);}
     catch(ArgumentException){Status="外部时间源小时无效；保持最后有效环境与时刻";return;}
    }
    // 秒只用于循环视觉噪声，限制其范围避免长期精度退化及上传float溢出，不是业务计时。
    elapsedSeconds=(elapsedSeconds+deltaSeconds%86400d)%86400d;AdvanceWeather(deltaSeconds);RefreshNow();
   }
   catch(Exception e) when(e is ArgumentException||e is InvalidOperationException){Status="环境输入无效："+e.Message;ReleaseDisplay();}
  }
  void EnsureWeather()
  {
   if(initialWeather==null)throw new InvalidOperationException("尚未指定初始天气档。");
   if(initialized&&initializedWeather==initialWeather)return;
   initializedWeather=initialWeather;CurrentWeather=TargetWeather=initialWeather;weatherNow=weatherFrom=weatherTo=initialWeather.Capture();
   initialized=true;transitioning=false;scheduleInitialized=false;blendElapsed=blendDuration=0;
  }
  void AdvanceBlend(double seconds)
  {
   if(!transitioning){if(CurrentWeather!=null)weatherNow=CurrentWeather.Capture();return;}
   blendElapsed+=seconds;float t=(float)WeatherBlend.Progress(blendElapsed,blendDuration);weatherNow=WeatherValues.Lerp(weatherFrom,weatherTo,t);
   if(t>=1){transitioning=false;CurrentWeather=TargetWeather;weatherNow=weatherTo;}
  }
  void EnsureSchedule()
  {
   if(climate==null)throw new InvalidOperationException("自动天气需要明确气候档。");
   int combinedSeed=unchecked(weatherSeed*397^climate.seed);
   // Inspector可在运行中编辑源档，必须先校验当前值，不能用已初始化缓存绕过0时长。
   climate.ValidateWeatherSchedule();
   if(scheduleInitialized&&scheduledClimate==climate&&scheduledSeed==combinedSeed)return;
   weatherRandom=new System.Random(combinedSeed);scheduledClimate=climate;scheduledSeed=combinedSeed;scheduleInitialized=true;weatherRemaining=NextDuration();
  }
  double NextDuration()=>Mathf.Lerp(climate.weatherDurationSeconds.x,climate.weatherDurationSeconds.y,(float)weatherRandom.NextDouble());
  void AdvanceWeather(double seconds)
  {
   if(!automaticWeather){AdvanceBlend(seconds);return;}
   EnsureSchedule();double remaining=seconds;
   while(remaining>=weatherRemaining)
   {
    AdvanceBlend(weatherRemaining);remaining-=weatherRemaining;ChooseWeather();weatherRemaining=NextDuration();
   }
   AdvanceBlend(remaining);weatherRemaining-=remaining;
  }
  void ChooseWeather()
  {
   climate.ValidateWeatherSchedule();double total=0;for(int i=0;i<climate.weatherWeights.Length;i++)total+=climate.weatherWeights[i];
   double sample=weatherRandom.NextDouble()*total;int selected=climate.weatherWeights.Length-1;
   for(int i=0;i<climate.weatherWeights.Length;i++){sample-=climate.weatherWeights[i];if(sample<0){selected=i;break;}}
   weatherFrom=weatherNow;TargetWeather=climate.weatherProfiles[selected];weatherTo=TargetWeather.Capture();blendDuration=EnvironmentNumbers.Nonnegative(weatherTransitionSeconds);blendElapsed=0;transitioning=blendDuration>0;
   if(!transitioning){weatherNow=weatherTo;CurrentWeather=TargetWeather;}
  }
  public void RefreshNow()
  {
   try
   {
    EnsureWeather();if(dayCycle==null)throw new InvalidOperationException("尚未指定昼夜档。");
    timeHours=EnvironmentClock.WrapHours(timeHours);if(!transitioning)weatherNow=CurrentWeather.Capture();
    Current=BuildFrame();
    bool allowed=isActiveAndEnabled&&(Application.isPlaying||previewInEditMode)&&Current.Enabled&&gameObject.scene==SceneManager.GetActiveScene()&&gameplayCamera!=null&&gameplayCamera.gameObject.scene==gameObject.scene;
    if(!allowed){ReleaseDisplay();Status=Current.Enabled?"已采样；未取得当前场景预览租约":"已恢复场景原貌";return;}
    if(activeRoot!=null&&activeRoot!=this)throw new InvalidOperationException("当前场景已有另一个环境显示根。");
    activeRoot=this;
    if(!ReferenceEquals(publishedCamera,null)&&publishedCamera!=gameplayCamera)EnvironmentCameraRegistry.Release(this,publishedCamera);
    publishedCamera=gameplayCamera;EnvironmentCameraRegistry.Publish(this,gameplayCamera,Current);
    if(appliedBinding!=null&&appliedBinding!=lightingBinding)appliedBinding.Release(this);
    appliedBinding=lightingBinding;if(appliedBinding!=null)appliedBinding.Apply(this,Current);
    Status="显示环境："+Current.WeatherName+" / "+Current.Hours.ToString("0.00")+" 时";Changed?.Invoke(Current);
   }
   catch(Exception e) when(e is ArgumentException||e is InvalidOperationException){Status="环境输入无效："+e.Message;ReleaseDisplay();}
  }
  void ReleaseDisplay()
  {
   if(!ReferenceEquals(publishedCamera,null)){EnvironmentCameraRegistry.Release(this,publishedCamera);publishedCamera=null;}
   if(appliedBinding!=null){appliedBinding.Release(this);appliedBinding=null;}
   if(activeRoot==this)activeRoot=null;
  }
  EnvironmentFrame BuildFrame()
  {
   var w=weatherNow;var p=dayCycle;
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
