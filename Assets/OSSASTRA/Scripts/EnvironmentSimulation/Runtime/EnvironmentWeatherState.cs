/* 功能：持有环境天气的当前值、目标、混合与确定性调度；可在无相机、Renderer或面板时独立推进。
 * 边界：只读天气/气候作者数据，不登记显示、不提交灯光、不推进游戏日历、不保存业务档。
 * 生命周期：显示暂停不清空状态；只有作者更换初始档或显式Reset才重置天气，步长预算在时钟推进前预检。
 */
using System;
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 internal sealed class EnvironmentWeatherState
 {
  const int maximumTransitionsPerStep=64;
  WeatherProfileSO initializedWeather;
  ClimateProfileSO scheduledClimate;
  WeatherValues from,to,now;
  bool initialized,transitioning,scheduleInitialized;
  double blendElapsed,blendDuration,weatherRemaining;
  System.Random weatherRandom;
  int scheduledSeed;
  internal WeatherProfileSO CurrentWeather{get;private set;}
  internal WeatherProfileSO TargetWeather{get;private set;}
  internal float TransitionProgress=>transitioning?(float)WeatherBlend.Progress(blendElapsed,blendDuration):1;
  internal WeatherValues Values=>now;
  internal void Reset(){initialized=false;transitioning=false;scheduleInitialized=false;}
  internal void InvalidateSchedule(){scheduleInitialized=false;}
  internal void EnsureInitial(WeatherProfileSO initial)
  {
   if(initial==null)throw new InvalidOperationException("尚未指定初始天气档。");
   if(initialized&&initializedWeather==initial)return;
   // 捕获成功后才改变状态；非法新作者档不能留下半更新的目标引用。
   var captured=initial.Capture();initializedWeather=initial;CurrentWeather=TargetWeather=initial;now=from=to=captured;
   initialized=true;transitioning=false;scheduleInitialized=false;blendElapsed=blendDuration=0;
  }
  internal void RefreshCurrent(){if(!transitioning&&CurrentWeather!=null)now=CurrentWeather.Capture();}
  internal void Transition(WeatherProfileSO target,float durationSeconds)
  {
   if(target==null)throw new ArgumentNullException(nameof(target));
   float duration=EnvironmentNumbers.Finite(durationSeconds);if(duration<0)throw new ArgumentOutOfRangeException(nameof(durationSeconds));
   var captured=target.Capture();from=now;to=captured;TargetWeather=target;blendElapsed=0;blendDuration=duration;
   transitioning=duration>0;if(!transitioning){now=to;CurrentWeather=target;}
  }
  internal bool TryPrepareAutomatic(double seconds,ClimateProfileSO climate,int seed,out string reason)
  {
   reason="";
   try
   {
    EnsureSchedule(climate,seed);
    double firstOverBudget=weatherRemaining+(double)climate.weatherDurationSeconds.x*maximumTransitionsPerStep;
    if(seconds>=firstOverBudget){reason="自动天气步长超过64次切换预算；保持上一有效环境与时刻";return false;}
    return true;
   }
   catch(Exception error) when(error is ArgumentException||error is InvalidOperationException)
   {reason="自动天气配置无效："+error.Message+"；保持上一有效环境与时刻";return false;}
  }
  internal void AdvancePrepared(double seconds,bool automatic,ClimateProfileSO climate,int seed,float transitionSeconds)
  {
   if(!automatic){AdvanceBlend(seconds);return;}
   EnsureSchedule(climate,seed);double remaining=seconds;
   while(remaining>=weatherRemaining)
   {
    AdvanceBlend(weatherRemaining);remaining-=weatherRemaining;ChooseWeather(climate,transitionSeconds);weatherRemaining=NextDuration(climate);
   }
   AdvanceBlend(remaining);weatherRemaining-=remaining;
  }
  void AdvanceBlend(double seconds)
  {
   if(!transitioning){RefreshCurrent();return;}
   blendElapsed+=seconds;float progress=(float)WeatherBlend.Progress(blendElapsed,blendDuration);now=WeatherValues.Lerp(from,to,progress);
   if(progress>=1){transitioning=false;CurrentWeather=TargetWeather;now=to;}
  }
  void EnsureSchedule(ClimateProfileSO climate,int seed)
  {
   if(climate==null)throw new InvalidOperationException("自动天气需要明确气候档。");
   int combinedSeed=unchecked(seed*397^climate.seed);
   // 作者在运行中也能编辑源档；先校验当前配置，不能用已初始化缓存绕过非法时长。
   climate.ValidateWeatherSchedule();
   if(scheduleInitialized&&scheduledClimate==climate&&scheduledSeed==combinedSeed)return;
   weatherRandom=new System.Random(combinedSeed);scheduledClimate=climate;scheduledSeed=combinedSeed;scheduleInitialized=true;weatherRemaining=NextDuration(climate);
  }
  double NextDuration(ClimateProfileSO climate)=>Mathf.Lerp(climate.weatherDurationSeconds.x,climate.weatherDurationSeconds.y,(float)weatherRandom.NextDouble());
  void ChooseWeather(ClimateProfileSO climate,float transitionSeconds)
  {
   climate.ValidateWeatherSchedule();double total=0;for(int i=0;i<climate.weatherWeights.Length;i++)total+=climate.weatherWeights[i];
   double sample=weatherRandom.NextDouble()*total;int selected=climate.weatherWeights.Length-1;
   for(int i=0;i<climate.weatherWeights.Length;i++){sample-=climate.weatherWeights[i];if(sample<0){selected=i;break;}}
   float duration=EnvironmentNumbers.Nonnegative(transitionSeconds);var target=climate.weatherProfiles[selected];var captured=target.Capture();
   from=now;TargetWeather=target;to=captured;blendDuration=duration;blendElapsed=0;transitioning=blendDuration>0;
   if(!transitioning){now=to;CurrentWeather=TargetWeather;}
  }
 }
}
